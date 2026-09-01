using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Rikarin.Skala.Rules.Metadata;
using System.Collections.Immutable;
using System.Linq;

namespace Rikarin.Skala.Rules.Performance;

/// <summary>SK4002: a delegate captures fresh storage in a loop body.</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class LoopClosureAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.LoopClosure);
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(
            Analyze,
            SyntaxKind.SimpleLambdaExpression,
            SyntaxKind.ParenthesizedLambdaExpression,
            SyntaxKind.AnonymousMethodExpression
        );
    }

    static void Analyze(SyntaxNodeAnalysisContext context) {
        var lambda = (AnonymousFunctionExpressionSyntax)context.Node;
        var model = context.SemanticModel;
        var cancellation = context.CancellationToken;
        if (EnclosingLoop(lambda) is not { } loop
            || model.GetTypeInfo(lambda, cancellation).ConvertedType?.TypeKind != TypeKind.Delegate
            || model.AnalyzeDataFlow(lambda) is not { Succeeded: true } flow) {
            return;
        }

        var body = Body(loop)!;
        if (loop is ForEachStatementSyntax
            && context.Compilation is CSharpCompilation { LanguageVersion: < LanguageVersion.CSharp5 }) {
            return;
        }

        var captured = flow.CapturedInside.OfType<ILocalSymbol>()
            .FirstOrDefault(local =>
                local.DeclaringSyntaxReferences.Any(reference => reference.SyntaxTree == lambda.SyntaxTree
                    && !lambda.Span.Contains(reference.Span)
                    && (body.Span.Contains(reference.Span)
                        || loop is ForEachStatementSyntax each
                        && SymbolEqualityComparer.Default.Equals(local, model.GetDeclaredSymbol(each, cancellation)))
                )
            );
        if (captured is null) {
            return;
        }

        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                lambda.GetLocation(),
                "Delegate captures iteration-local `" + captured.Name + "`; review closure allocation in this loop"
            )
        );
    }

    static SyntaxNode? EnclosingLoop(SyntaxNode node) {
        foreach (var ancestor in node.Ancestors()) {
            if (ancestor is AnonymousFunctionExpressionSyntax
                or LocalFunctionStatementSyntax
                or BaseMethodDeclarationSyntax) {
                return null;
            }

            if (Body(ancestor) is { } body && body.Span.Contains(node.Span)) {
                return ancestor;
            }
        }

        return null;
    }

    static StatementSyntax? Body(SyntaxNode node) =>
        node switch {
            ForStatementSyntax loop => loop.Statement,
            CommonForEachStatementSyntax loop => loop.Statement,
            WhileStatementSyntax loop => loop.Statement,
            DoStatementSyntax loop => loop.Statement,
            _ => null
        };
}
