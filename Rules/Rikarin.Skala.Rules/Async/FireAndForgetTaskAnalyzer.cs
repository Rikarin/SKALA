using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Rikarin.Skala.Rules.Metadata;
using System.Collections.Immutable;

namespace Rikarin.Skala.Rules.Async;

/// <summary><c>SK3005</c> — a Task-producing call discarded by a synchronous body.</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class FireAndForgetTaskAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.FireAndForgetTask);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(static start => {
                var task = start.Compilation.GetTypeByMetadataName("System.Threading.Tasks.Task");
                if (task is null) {
                    return;
                }

                start.RegisterSyntaxNodeAction(
                    context => Analyze(context, task),
                    SyntaxKind.ExpressionStatement
                );
            }
        );
    }

    static void Analyze(SyntaxNodeAnalysisContext context, INamedTypeSymbol task) {
        var statement = (ExpressionStatementSyntax)context.Node;
        if (statement.Expression is not InvocationExpressionSyntax invocation
            || AsyncContext.NearestAsyncOwner(invocation) is not { } owner
            || AsyncContext.HasAsyncModifier(owner)) {
            return;
        }

        var type = context.SemanticModel.GetTypeInfo(invocation, context.CancellationToken).Type;
        if (!IsTask(type, task)) {
            return;
        }

        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                invocation.GetLocation(),
                "Task returned by this call is neither awaited nor observed"
            )
        );
    }

    static bool IsTask(ITypeSymbol? type, INamedTypeSymbol task) {
        for (var current = type as INamedTypeSymbol; current is not null; current = current.BaseType) {
            if (SymbolEqualityComparer.Default.Equals(current.OriginalDefinition, task)) {
                return true;
            }
        }

        return false;
    }
}
