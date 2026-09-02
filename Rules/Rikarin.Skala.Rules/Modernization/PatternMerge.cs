using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace Rikarin.Skala.Rules.Modernization;

/// <summary>
///     The report <see cref="TestAndCastPatternAnalyzer" /> and <see cref="TypePatternAnalyzer" /> share:
///     replace a test with an <c>is</c> pattern and delete the declaration it made redundant.
/// </summary>
/// <remarks>
///     ⚠ Extracted because the <em>guard pair</em> is the part that must not drift, not because a
///     duplication metric asked for it. Both rules make two edits — one rewrite and one whole-line
///     deletion — so each needs both comment questions: <c>AroundTheDeclaration</c> for the line it
///     deletes, and <c>WithinTheEdit</c> for the span it rewrites. Guarding only the deletion is how
///     three rules came to silently delete an author's comment (#325), and two copies of that pair is
///     two places for the next fix to get it half right.
/// </remarks>
static class PatternMerge {
    /// <summary>
    ///     Reports the merge, or returns silently when a comment sits in either edit.
    /// </summary>
    /// <param name="declaration">
    ///     The declaration whose whole line the fix deletes — the node the reader's comment would be
    ///     attached to, and the reason this asks the FullSpan question.
    /// </param>
    /// <param name="edit">The span the fix rewrites in place.</param>
    internal static void ReportOrDecline(
        SyntaxNodeAnalysisContext context,
        DiagnosticDescriptor descriptor,
        StatementSyntax declaration,
        TextSpan edit,
        ExpressionSyntax left,
        TypeSyntax tested,
        string name,
        string prose
    ) {
        var tree = declaration.SyntaxTree;
        if (RewriteGuards.ContainsCommentOrDirectiveAroundTheDeclaration(declaration)
            || RewriteGuards.ContainsCommentOrDirectiveWithinTheEdit(tree, edit)) {
            return;
        }

        var replacement = left + " is " + tested + " " + name;
        context.ReportDiagnostic(
            Diagnostic.Create(
                descriptor,
                Location.Create(tree, edit),
                FixEdits.Pack((edit, replacement), (RewriteGuards.LineSpanOf(declaration), string.Empty)),
                prose + ": `" + RewriteGuards.Trim(replacement) + "`"
            )
        );
    }
}
