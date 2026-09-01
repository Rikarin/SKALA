using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Rikarin.Skala.Rules.Metadata;
using System.Collections.Immutable;

namespace Rikarin.Skala.Rules.Correctness;

/// <summary><c>SK2014</c> — an empty, uncommented catch block.</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class EmptyCatchAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.EmptyCatchSwallowsException);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.CatchClause);
    }

    static void Analyze(SyntaxNodeAnalysisContext context) {
        var clause = (CatchClauseSyntax)context.Node;
        if (clause.Filter is not null || clause.Block.Statements.Count != 0 || HasComment(clause.Block)) {
            return;
        }

        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                clause.CatchKeyword.GetLocation(),
                "empty `catch` silently discards the exception"
            )
        );
    }

    static bool HasComment(BlockSyntax block) {
        foreach (var trivia in block.DescendantTrivia(descendIntoTrivia: true)) {
            if (trivia.IsKind(SyntaxKind.SingleLineCommentTrivia)
                || trivia.IsKind(SyntaxKind.MultiLineCommentTrivia)
                || trivia.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia)
                || trivia.IsKind(SyntaxKind.MultiLineDocumentationCommentTrivia)) {
                return true;
            }
        }

        return false;
    }
}
