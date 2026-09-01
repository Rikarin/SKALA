using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Rikarin.Skala.Rules.Metadata;
using System.Collections.Immutable;
using System.Linq;

namespace Rikarin.Skala.Rules.Performance;

/// <summary>SK4001: an explicit per-path policy asks for review of LINQ in hot code.</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class HotPathLinqAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.HotPathLinq);
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.InvocationExpression);
    }

    static void Analyze(SyntaxNodeAnalysisContext context) {
        var invocation = (InvocationExpressionSyntax)context.Node;
        if (!IsPipelineCall(context.SemanticModel, invocation, context.CancellationToken)) {
            return;
        }

        // One finding for a fluent pipeline, rather than one for each stage.
        SyntaxNode expression = invocation;
        while (true) {
            while (expression.Parent is ParenthesizedExpressionSyntax parentheses) {
                expression = parentheses;
            }

            if (expression.Parent is not MemberAccessExpressionSyntax { Parent: InvocationExpressionSyntax consumer }
                || context.SemanticModel.GetSymbolInfo(
                    consumer,
                    context.CancellationToken
                ).Symbol is not IMethodSymbol method
                || !IsEnumerable(method, context.Compilation)) {
                break;
            }

            if (IsPipelineCall(context.SemanticModel, consumer, context.CancellationToken)) {
                return;
            }

            // An identity stage such as AsEnumerable must not split one pipeline into two reports.
            expression = consumer;
        }

        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                invocation.GetLocation(),
                "Review this LINQ call or pipeline in the configured hot path; measure enumeration and allocation costs"
            )
        );
    }

    internal static bool IsEnumerable(IMethodSymbol method, Compilation compilation) {
        var enumerable = compilation.GetTypeByMetadataName("System.Linq.Enumerable");
        return enumerable is not null
            && !enumerable.Locations.Any(static location => location.IsInSource)
            && SymbolEqualityComparer.Default.Equals((method.ReducedFrom ?? method).ContainingType, enumerable);
    }

    static bool IsPipelineCall(
        SemanticModel model,
        InvocationExpressionSyntax invocation,
        System.Threading.CancellationToken cancellation
    ) =>
        model.GetSymbolInfo(invocation, cancellation).Symbol is IMethodSymbol method
        && method.Name is not ("Empty" or "AsEnumerable" or "TryGetNonEnumeratedCount")
        && IsEnumerable(method, model.Compilation);
}
