using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Rikarin.Skala.Formatting.CSharp.Arrangement;
using Rikarin.Skala.Options;

namespace Rikarin.Skala.Formatting.CSharp;

/// <summary>Adds required embedded-statement blocks to the formatted syntax tree.</summary>
static class RequiredBraces {
    public static bool HasCandidate(SyntaxNode root, in PhaseOneOptions options) =>
        options.PreferBraces != BracePreference.False && root.DescendantNodes().Any(HasUnbracedBody);

    public static IReadOnlyList<TextEdit> Edits(SyntaxNode original, string formatted) {
        var source = original.ToFullString();
        var edits = ArrangementEdits.Diff(source, formatted).ToList();
        foreach (var owner in original.DescendantNodes().Where(HasUnbracedBody)) {
            // Keep both inserted braces in one edit, even when the body itself is unchanged.
            // Otherwise restricting edits to a selected range can insert only the opening brace.
            var next = owner.GetLastToken().GetNextToken();
            var end = next.RawKind == 0 ? original.FullSpan.End : next.SpanStart;
            var first = edits.FindIndex(edit => edit.Span.End >= owner.SpanStart && edit.Span.Start <= end);
            var last = edits.FindLastIndex(edit => edit.Span.End >= owner.SpanStart && edit.Span.Start <= end);
            if (first < 0 || first == last) {
                continue;
            }

            var start = edits[first].Span.Start;
            var finish = edits[last].Span.End;
            var replacements = edits.GetRange(first, last - first + 1)
                .Select(edit => new TextEdit(
                        SourceSpan.FromBounds(edit.Span.Start - start, edit.Span.End - start),
                        edit.NewText
                    )
                )
                .ToArray();
            var merged = new TextEdit(
                SourceSpan.FromBounds(start, finish),
                EditEmitter.Apply(source[start..finish], replacements)
            );
            edits.RemoveRange(first, last - first + 1);
            edits.Insert(first, merged);
        }

        return edits;
    }

    static bool HasUnbracedBody(SyntaxNode node) =>
        node switch {
            IfStatementSyntax n => n.Statement is not BlockSyntax,
            ElseClauseSyntax n => n.Statement is not (BlockSyntax or IfStatementSyntax),
            ForStatementSyntax n => n.Statement is not BlockSyntax,
            CommonForEachStatementSyntax n => n.Statement is not BlockSyntax,
            WhileStatementSyntax n => n.Statement is not BlockSyntax,
            DoStatementSyntax n => n.Statement is not BlockSyntax,
            UsingStatementSyntax n => n.Statement is not BlockSyntax,
            LockStatementSyntax n => n.Statement is not BlockSyntax,
            FixedStatementSyntax n => n.Statement is not BlockSyntax,
            _ => false
        };

    public static SyntaxNode Rewrite(SyntaxNode root, in PhaseOneOptions options, string newLine) =>
        options.PreferBraces == BracePreference.False
            ? root
            : new Rewriter(options.PreferBraces, FormatterTagGuard.For(root, options.Tags), newLine).Visit(root);

    sealed class Rewriter(BracePreference preference, FormatterTagGuard guard, string newLine) :
        GuardedRewriter(guard) {
        public override SyntaxNode? VisitIfStatement(IfStatementSyntax node) {
            var visited = (IfStatementSyntax)base.VisitIfStatement(node)!;
            return NeedsBlock(node.Statement, node) ? visited.WithStatement(Block(visited.Statement)) : visited;
        }

        public override SyntaxNode? VisitElseClause(ElseClauseSyntax node) {
            var visited = (ElseClauseSyntax)base.VisitElseClause(node)!;
            return node.Statement is not IfStatementSyntax && NeedsBlock(node.Statement, node)
                ? visited.WithStatement(Block(visited.Statement))
                : visited;
        }

        public override SyntaxNode? VisitForStatement(ForStatementSyntax node) {
            var visited = (ForStatementSyntax)base.VisitForStatement(node)!;
            return NeedsBlock(node.Statement, node) ? visited.WithStatement(Block(visited.Statement)) : visited;
        }

        public override SyntaxNode? VisitForEachStatement(ForEachStatementSyntax node) {
            var visited = (ForEachStatementSyntax)base.VisitForEachStatement(node)!;
            return NeedsBlock(node.Statement, node) ? visited.WithStatement(Block(visited.Statement)) : visited;
        }

        public override SyntaxNode? VisitForEachVariableStatement(ForEachVariableStatementSyntax node) {
            var visited = (ForEachVariableStatementSyntax)base.VisitForEachVariableStatement(node)!;
            return NeedsBlock(node.Statement, node) ? visited.WithStatement(Block(visited.Statement)) : visited;
        }

        public override SyntaxNode? VisitWhileStatement(WhileStatementSyntax node) {
            var visited = (WhileStatementSyntax)base.VisitWhileStatement(node)!;
            return NeedsBlock(node.Statement, node) ? visited.WithStatement(Block(visited.Statement)) : visited;
        }

        public override SyntaxNode? VisitDoStatement(DoStatementSyntax node) {
            var visited = (DoStatementSyntax)base.VisitDoStatement(node)!;
            return NeedsBlock(node.Statement, node) ? visited.WithStatement(Block(visited.Statement)) : visited;
        }

        public override SyntaxNode? VisitUsingStatement(UsingStatementSyntax node) {
            var visited = (UsingStatementSyntax)base.VisitUsingStatement(node)!;
            return NeedsBlock(node.Statement, node) ? visited.WithStatement(Block(visited.Statement)) : visited;
        }

        public override SyntaxNode? VisitLockStatement(LockStatementSyntax node) {
            var visited = (LockStatementSyntax)base.VisitLockStatement(node)!;
            return NeedsBlock(node.Statement, node) ? visited.WithStatement(Block(visited.Statement)) : visited;
        }

        public override SyntaxNode? VisitFixedStatement(FixedStatementSyntax node) {
            var visited = (FixedStatementSyntax)base.VisitFixedStatement(node)!;
            return NeedsBlock(node.Statement, node) ? visited.WithStatement(Block(visited.Statement)) : visited;
        }

        bool NeedsBlock(StatementSyntax statement, SyntaxNode owner) {
            // A directive may select a different statement under another symbol set.
            if (statement is BlockSyntax || owner.ContainsDirectives) {
                return false;
            }

            var lines = statement.GetLocation().GetLineSpan();
            return preference == BracePreference.True
                || lines.StartLinePosition.Line != lines.EndLinePosition.Line;
        }

        BlockSyntax Block(StatementSyntax statement) =>
            SyntaxFactory.Block(statement)
                .WithOpenBraceToken(
                    SyntaxFactory.Token(SyntaxKind.OpenBraceToken)
                        .WithTrailingTrivia(SyntaxFactory.EndOfLine(newLine))
                )
                // A trailing // comment must not consume the inserted closing brace.
                    .WithCloseBraceToken(
                        SyntaxFactory.Token(SyntaxKind.CloseBraceToken)
                            .WithLeadingTrivia(SyntaxFactory.EndOfLine(newLine))
                            .WithTrailingTrivia(SyntaxFactory.EndOfLine(newLine))
                    );
    }
}
