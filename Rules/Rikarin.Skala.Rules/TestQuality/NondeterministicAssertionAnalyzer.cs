using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using Rikarin.Skala.Rules.Async;
using Rikarin.Skala.Rules.Metadata;
using System.Collections.Immutable;
using System.Linq;

namespace Rikarin.Skala.Rules.TestQuality;

/// <summary>SK8007: direct clock, GUID or unseeded random input to an xUnit assertion.</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class NondeterministicAssertionAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.NondeterministicAssertion);
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.InvocationExpression);
    }

    static void Analyze(SyntaxNodeAnalysisContext context) {
        var invocation = (InvocationExpressionSyntax)context.Node;
        var name = invocation.Expression switch {
            MemberAccessExpressionSyntax access => access.Name.Identifier.ValueText,
            SimpleNameSyntax simple => simple.Identifier.ValueText,
            _ => null
        };
        if (name is not ("Equal"
                or "NotEqual"
                or "StrictEqual"
                or "NotStrictEqual"
                or "Equivalent"
                or "True"
                or "False"
                or "InRange"
                or "NotInRange"
                or "Contains"
                or "DoesNotContain")) {
            return;
        }

        var model = context.SemanticModel;
        var cancellation = context.CancellationToken;
        if (model.GetOperation(invocation, cancellation) is not IInvocationOperation call
            || !IsFrameworkType(call.TargetMethod.ContainingType, "Xunit.Assert", context.Compilation)
            || AsyncContext.NearestAsyncOwner(invocation) is not MethodDeclarationSyntax method
            || model.GetDeclaredSymbol(method, cancellation) is not IMethodSymbol test
            || !test.GetAttributes().Any(attribute => IsTestAttribute(attribute.AttributeClass, context.Compilation))) {
            return;
        }

        foreach (var argument in call.Arguments) {
            if (argument.IsImplicit || argument.Parameter?.Name is "userMessage" or "message") {
                continue;
            }

            foreach (var node in argument.Value.Syntax.DescendantNodesAndSelf(node =>
                         node is not (AnonymousFunctionExpressionSyntax or LocalFunctionStatementSyntax)
                         && !(node is InvocationExpressionSyntax {
                                 Expression: IdentifierNameSyntax { Identifier.ValueText: "nameof" }
                             }
                             && model.GetOperation(node, cancellation) is INameOfOperation)
                     )) {
                if (node is not ExpressionSyntax expression || !IsNondeterministic(model, expression, cancellation)) {
                    continue;
                }

                context.ReportDiagnostic(
                    Diagnostic.Create(
                        Descriptor,
                        expression.GetLocation(),
                        "Assertion input uses a live clock, new GUID or unseeded random value; prefer controlled test input"
                    )
                );
                return;
            }
        }
    }

    static bool IsNondeterministic(
        SemanticModel model,
        ExpressionSyntax expression,
        System.Threading.CancellationToken cancellation
    ) {
        if (expression is MemberAccessExpressionSyntax or IdentifierNameSyntax
            && model.GetOperation(expression, cancellation) is IPropertyReferenceOperation {
                Property.IsStatic: true
            } property) {
            return property.Property.Name is "Now" or "UtcNow" or "Today"
                && (IsFrameworkType(property.Property.ContainingType, "System.DateTime", model.Compilation)
                    || IsFrameworkType(property.Property.ContainingType, "System.DateTimeOffset", model.Compilation));
        }

        if (expression is not InvocationExpressionSyntax
            || model.GetOperation(expression, cancellation) is not IInvocationOperation invocation) {
            return false;
        }

        if (invocation.TargetMethod is { IsStatic: true, Name: "NewGuid" }
            && IsFrameworkType(invocation.TargetMethod.ContainingType, "System.Guid", model.Compilation)) {
            return true;
        }

        return invocation.TargetMethod.Name is "Next" or "NextInt64" or "NextDouble" or "NextSingle"
            && IsFrameworkType(invocation.TargetMethod.ContainingType, "System.Random", model.Compilation)
            && (invocation.Instance is IPropertyReferenceOperation {
                    Property.Name: "Shared",
                    Property.IsStatic: true
                } shared
                && IsFrameworkType(shared.Property.ContainingType, "System.Random", model.Compilation)
                || invocation.Instance is IObjectCreationOperation { Arguments.Length: 0, Type: { } type }
                && IsFrameworkType(type, "System.Random", model.Compilation));
    }

    static bool IsTestAttribute(INamedTypeSymbol? type, Compilation compilation) {
        for (var current = type; current is not null; current = current.BaseType) {
            if (IsFrameworkType(current, "Xunit.FactAttribute", compilation)
                || IsFrameworkType(current, "Xunit.TheoryAttribute", compilation)) {
                return true;
            }
        }

        return false;
    }

    static bool IsFrameworkType(ITypeSymbol type, string name, Compilation compilation) =>
        !type.Locations.Any(static location => location.IsInSource)
        && SymbolEqualityComparer.Default.Equals(type, compilation.GetTypeByMetadataName(name));
}
