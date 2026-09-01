using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using Rikarin.Skala.Rules.Metadata;
using System.Collections.Immutable;

namespace Rikarin.Skala.Rules.Correctness;

/// <summary><c>SK2016</c> — interpolation passed to a Microsoft.Extensions.Logging template parameter.</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class InterpolatedLoggerMessageAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.InterpolatedLoggerMessage);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(static start => {
                var extensions = start.Compilation.GetTypeByMetadataName(
                    "Microsoft.Extensions.Logging.LoggerExtensions"
                );
                if (extensions is null) {
                    return;
                }

                start.RegisterSyntaxNodeAction(
                    context => Analyze(context, extensions),
                    SyntaxKind.InvocationExpression
                );
            }
        );
    }

    static void Analyze(SyntaxNodeAnalysisContext context, INamedTypeSymbol extensions) {
        var invocation = (InvocationExpressionSyntax)context.Node;
        if (context.SemanticModel.GetOperation(invocation, context.CancellationToken)
            is not IInvocationOperation operation
            || !SymbolEqualityComparer.Default.Equals(
                (operation.TargetMethod.ReducedFrom ?? operation.TargetMethod).ContainingType,
                extensions
            )) {
            return;
        }

        foreach (var argument in operation.Arguments) {
            if (argument.Parameter?.Name != "message"
                || argument.Value.Syntax is not InterpolatedStringExpressionSyntax interpolation) {
                continue;
            }

            context.ReportDiagnostic(
                Diagnostic.Create(
                    Descriptor,
                    interpolation.GetLocation(),
                    "interpolation eagerly renders a logger message; use a constant template and arguments"
                )
            );
        }
    }
}
