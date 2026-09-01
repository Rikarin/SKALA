using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using Rikarin.Skala.Rules.Metadata;
using Rikarin.Skala.Rules.Modernization;
using System.Collections.Immutable;
using System.Linq;

namespace Rikarin.Skala.Rules.Performance;

/// <summary>SK4003: an explicit temporary params array with an accessible span overload.</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ParamsSpanOverloadAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.ParamsSpanOverload);
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(static start => {
                if (SkalaRule.MeetsLanguageVersion(start.Compilation, "12.0")) {
                    start.RegisterSyntaxNodeAction(Analyze, SyntaxKind.InvocationExpression);
                }
            }
        );
    }

    static void Analyze(SyntaxNodeAnalysisContext context) {
        var invocation = (InvocationExpressionSyntax)context.Node;
        var model = context.SemanticModel;
        var cancellation = context.CancellationToken;
        var arguments = invocation.ArgumentList.Arguments;
        if (arguments.Count == 0
            || arguments.Any(static argument => argument.NameColon is not null)
            || model.GetOperation(invocation, cancellation) is not IInvocationOperation call
            || call.TargetMethod.Parameters.Length != arguments.Count
            || call.TargetMethod.Parameters[arguments.Count - 1] is not {
                IsParams: true,
                Type: IArrayTypeSymbol { Rank: 1 } array
            }
            || NullComparison.InsideExpressionTree(model, invocation, cancellation)) {
            return;
        }

        var argument = arguments[arguments.Count - 1];
        if (array.ElementType.TypeKind is TypeKind.Pointer or TypeKind.FunctionPointer or TypeKind.Error) {
            return;
        }

        var initializer = PatternSafety.Unwrap(argument.Expression) switch {
            ArrayCreationExpressionSyntax creation => creation.Initializer,
            ImplicitArrayCreationExpressionSyntax creation => creation.Initializer,
            _ => null
        };
        if (initializer is not { Expressions.Count: > 0 }
            || initializer.ContainsDirectives
            || initializer.Expressions.Any(static expression => expression.DescendantNodesAndSelf()
                    .OfType<AwaitExpressionSyntax>()
                    .Any()
            )
            || PrivateFieldUsage.FrameworkType(model.Compilation, "System.ReadOnlySpan`1") is not { } span) {
            return;
        }

        var spanType = span.Construct(array.ElementType);
        var replacement = SyntaxFactory.ParseExpression(
            "("
            + spanType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
            + ")["
            + string.Join(", ", initializer.Expressions.Select(static expression => expression.ToString()))
            + "]"
        );
        var proposed = invocation.ReplaceNode(argument.Expression, replacement);
        if (model.GetSpeculativeSymbolInfo(
                invocation.SpanStart,
                proposed,
                SpeculativeBindingOption.BindAsExpression
            ).Symbol
            is not IMethodSymbol alternative
            || !SymbolEqualityComparer.Default.Equals(alternative.ContainingType, call.TargetMethod.ContainingType)
            || !SymbolEqualityComparer.Default.Equals(alternative.ReturnType, call.TargetMethod.ReturnType)
            || alternative.IsStatic != call.TargetMethod.IsStatic
            || alternative.Parameters.Length != arguments.Count
            || !SymbolEqualityComparer.Default.Equals(alternative.Parameters[arguments.Count - 1].Type, spanType)
            || alternative.Parameters.Where((parameter, index) => index < arguments.Count - 1)
                .Any(parameter => parameter.RefKind != call.TargetMethod.Parameters[parameter.Ordinal].RefKind
                    || !SymbolEqualityComparer.Default.Equals(
                        parameter.Type,
                        call.TargetMethod.Parameters[parameter.Ordinal].Type
                    )
                )) {
            return;
        }

        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                argument.GetLocation(),
                "This temporary params array has an accessible ReadOnlySpan overload; review a span-based call"
            )
        );
    }
}
