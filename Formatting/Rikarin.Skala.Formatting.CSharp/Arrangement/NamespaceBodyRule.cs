using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Rikarin.Skala.Options;

namespace Rikarin.Skala.Formatting.CSharp.Arrangement;

/// <summary>
/// <c>namespace N { … }</c> ⇒ <c>namespace N;</c>, under
/// <c>csharp_style_namespace_declarations</c>.
/// </summary>
/// <remarks>
/// ⚠ The oracle performs this under a cleanup task the M4 sweep did not find:
/// <c>ArrangeNamespaces</c>, an attribute of <c>CSCodeStyleAttributes</c>. Until it was added to
/// <c>OracleProfile.Cleanup</c> the reference tool left every block-scoped namespace alone, which
/// reads exactly like "the oracle does not do this" and is why the key sat at Tier D. See
/// docs/oracle-cleanup-profile.md § "Two tasks the first sweep missed".
/// <para>
/// ⚠ Purely syntactic, so it is in the subset an agent gets on a loose file. Converting to
/// file-scoped removes one level of indentation from the whole file; this rule does not re-indent,
/// because the arranger never emits whitespace decisions of its own and the formatter that follows
/// lays the members out (doc 06 § "Interaction with the formatter").
/// </para>
/// <para>
/// ⚠ The preconditions are the ones the language imposes and they are not stylistic: a file-scoped
/// namespace must be the *only* namespace in its file and may not itself be nested, so a file with
/// two namespace declarations, or one namespace inside another, is left alone entirely. A
/// declaration carrying <c>#if</c> inside its braces is also left alone — ADR-003's rule about
/// directives that open in one region and close in another.
/// </para>
/// </remarks>
public sealed class NamespaceBodyRule : ArrangementRule {
    public override string Id => ArrangeIds.NamespaceBody;

    public override bool NeedsSemantics => false;

    public override bool IsEnabled(in ArrangementOptions options) =>
        options.NamespaceDeclarations == NamespaceDeclarationStyle.FileScoped;

    public override SyntaxNode Apply(ArrangementContext context) {
        if (context.Root is not CompilationUnitSyntax unit) {
            return context.Root;
        }

        // ⚠ Counted over the whole file rather than matched per node: the legality of the rewrite is
        // a property of the file, not of the declaration.
        var declarations = unit.DescendantNodes().OfType<BaseNamespaceDeclarationSyntax>().ToArray();
        if (declarations.Length != 1 || declarations[0] is not NamespaceDeclarationSyntax block) {
            return context.Root;
        }

        if (block.Parent is not CompilationUnitSyntax) {
            return context.Root;
        }

        if (!context.Guard.IsEmpty && !context.Guard.Preserves(block, block)) {
            return context.Root;
        }

        if (context.Guard.Encloses(block.Span) || context.Guard.Straddles(block.Span)) {
            return context.Root;
        }

        foreach (var trivia in block.DescendantTrivia(descendIntoTrivia: true)) {
            if (trivia.IsDirective || trivia.IsKind(SyntaxKind.DisabledTextTrivia)) {
                return context.Root;
            }
        }

        // ⚠ The closing brace's leading trivia is the only place a trailing comment at the bottom of
        // the file can live, and dropping the brace drops it with the brace unless it is moved onto
        // the end of the unit.
        var tail = block.CloseBraceToken.LeadingTrivia;

        var scoped = SyntaxFactory.FileScopedNamespaceDeclaration(
                block.AttributeLists,
                block.Modifiers,
                SyntaxFactory.Token(SyntaxKind.NamespaceKeyword).WithTriviaFrom(block.NamespaceKeyword),
                block.Name,
                SyntaxFactory.Token(SyntaxKind.SemicolonToken),
                block.Externs,
                block.Usings,
                block.Members
            )
            .WithLeadingTrivia(block.GetLeadingTrivia());

        var rewritten = unit.ReplaceNode(block, scoped);
        return tail.Count == 0
            ? rewritten
            : rewritten.WithEndOfFileToken(rewritten.EndOfFileToken.WithLeadingTrivia(tail));
    }
}
