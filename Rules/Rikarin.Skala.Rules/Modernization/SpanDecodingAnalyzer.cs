using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using Rikarin.Skala.Rules.Metadata;
using System.Collections.Immutable;
using System.Linq;

namespace Rikarin.Skala.Rules.Modernization;

/// <summary>SK1028: decode an existing byte span without first allocating an array copy.</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class SpanDecodingAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.SpanDecoding);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(static start => {
                if (SkalaRule.MeetsLanguageVersion(start.Compilation, "7.2")) {
                    start.RegisterSyntaxNodeAction(Analyze, SyntaxKind.InvocationExpression);
                }
            }
        );
    }

    static void Analyze(SyntaxNodeAnalysisContext context) {
        var invocation = (InvocationExpressionSyntax)context.Node;
        if (invocation.Expression is not MemberAccessExpressionSyntax { Name.Identifier.ValueText: "GetString" }
            || invocation.ArgumentList.Arguments.Count != 1
            || invocation.ArgumentList.Arguments[0] is not { NameColon: null } argument
            || argument.Expression is not InvocationExpressionSyntax {
                Expression: MemberAccessExpressionSyntax access
            } copy
            || invocation.ContainsDirectives
            || invocation.SpanContainsComment()) {
            return;
        }

        var model = context.SemanticModel;
        var cancellation = context.CancellationToken;
        if (Utf8EncodingCall.Bind(model, invocation, cancellation) is not { } call
            || call.TargetMethod.Parameters[0].Type is not IArrayTypeSymbol {
                ElementType.SpecialType: SpecialType.System_Byte,
                Rank: 1
            }
            || model.GetOperation(copy, cancellation) is not IInvocationOperation allocation
            || allocation.TargetMethod.Name != "ToArray"
            || allocation.Arguments.Length != 0
            || !IsByteSpan(allocation.TargetMethod.ContainingType, model.Compilation)
            || NullComparison.InsideExpressionTree(model, invocation, cancellation)) {
            return;
        }

        // Bind the proposed call: a framework without the span overload must remain untouched.
        var replacementNode = invocation.ReplaceNode(argument.Expression, access.Expression.WithoutTrivia());
        if (model.GetSpeculativeSymbolInfo(
                invocation.SpanStart,
                replacementNode,
                SpeculativeBindingOption.BindAsExpression
            ).Symbol
            is not IMethodSymbol replacementMethod
            || !SymbolEqualityComparer.Default.Equals(
                replacementMethod.ContainingType,
                call.TargetMethod.ContainingType
            )
            || replacementMethod.Parameters.Length != 1
            || !IsByteSpan(replacementMethod.Parameters[0].Type, model.Compilation)) {
            return;
        }

        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                invocation.GetLocation(),
                FixEdits.Pack((copy.Span, access.Expression.ToString())),
                "Decode the byte span directly without allocating a temporary array"
            )
        );
    }

    static bool IsByteSpan(ITypeSymbol type, Compilation compilation) =>
        type is INamedTypeSymbol { TypeArguments.Length: 1 } named
        && named.TypeArguments[0].SpecialType == SpecialType.System_Byte
        && (SymbolEqualityComparer.Default.Equals(
                named.OriginalDefinition,
                compilation.GetTypeByMetadataName("System.Span`1")
            )
            || SymbolEqualityComparer.Default.Equals(
                named.OriginalDefinition,
                compilation.GetTypeByMetadataName("System.ReadOnlySpan`1")
            ));
}
