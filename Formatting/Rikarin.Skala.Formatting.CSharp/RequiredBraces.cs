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
            MergeOwnerEdits(original, source, edits, owner);
        }

        return edits;
    }

    static void MergeOwnerEdits(SyntaxNode original, string source, List<TextEdit> edits, SyntaxNode owner) {
        var next = owner.GetLastToken().GetNextToken();
        var end = next.RawKind == 0 ? original.FullSpan.End : next.SpanStart;
        var first = edits.FindIndex(edit => edit.Span.End >= owner.SpanStart && edit.Span.Start <= end);
        var last = edits.FindLastIndex(edit => edit.Span.End >= owner.SpanStart && edit.Span.Start <= end);
        if (first < 0 || first == last) {
            return;
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

    static bool HasUnbracedBody(SyntaxNode node) => Body(node) is not (null or BlockSyntax);

    static StatementSyntax? Body(SyntaxNode node) =>
        node switch {
            IfStatementSyntax n => n.Statement,
            ElseClauseSyntax { Statement: not IfStatementSyntax } n => n.Statement,
            ForStatementSyntax n => n.Statement,
            CommonForEachStatementSyntax n => n.Statement,
            WhileStatementSyntax n => n.Statement,
            DoStatementSyntax n => n.Statement,
            UsingStatementSyntax n => n.Statement,
            LockStatementSyntax n => n.Statement,
            FixedStatementSyntax n => n.Statement,
            _ => null
        };

    public static SyntaxNode Rewrite(SyntaxNode root, in PhaseOneOptions options, string newLine) =>
        options.PreferBraces == BracePreference.False
            ? root
            : new Rewriter(options.PreferBraces, FormatterTagGuard.For(root, options.Tags), newLine).Visit(root);

    sealed class Rewriter(BracePreference preference, FormatterTagGuard guard, string newLine) :
        GuardedRewriter(guard) {
        public override SyntaxNode? VisitIfStatement(IfStatementSyntax node) =>
            RewriteBody(node, base.VisitIfStatement(node)!);

        public override SyntaxNode? VisitElseClause(ElseClauseSyntax node) =>
            RewriteBody(node, base.VisitElseClause(node)!);

        public override SyntaxNode? VisitForStatement(ForStatementSyntax node) =>
            RewriteBody(node, base.VisitForStatement(node)!);

        public override SyntaxNode? VisitForEachStatement(ForEachStatementSyntax node) =>
            RewriteBody(node, base.VisitForEachStatement(node)!);

        public override SyntaxNode? VisitForEachVariableStatement(ForEachVariableStatementSyntax node) =>
            RewriteBody(node, base.VisitForEachVariableStatement(node)!);

        public override SyntaxNode? VisitWhileStatement(WhileStatementSyntax node) =>
            RewriteBody(node, base.VisitWhileStatement(node)!);

        public override SyntaxNode? VisitDoStatement(DoStatementSyntax node) =>
            RewriteBody(node, base.VisitDoStatement(node)!);

        public override SyntaxNode? VisitUsingStatement(UsingStatementSyntax node) =>
            RewriteBody(node, base.VisitUsingStatement(node)!);

        public override SyntaxNode? VisitLockStatement(LockStatementSyntax node) =>
            RewriteBody(node, base.VisitLockStatement(node)!);

        public override SyntaxNode? VisitFixedStatement(FixedStatementSyntax node) =>
            RewriteBody(node, base.VisitFixedStatement(node)!);

        SyntaxNode RewriteBody(SyntaxNode original, SyntaxNode visited) {
            var statement = Body(original);
            if (statement is null || !NeedsBlock(statement, original)) {
                return visited;
            }

            var block = Block(Body(visited)!);
            return visited switch {
                IfStatementSyntax node => node.WithStatement(block),
                ElseClauseSyntax node => node.WithStatement(block),
                ForStatementSyntax node => node.WithStatement(block),
                ForEachStatementSyntax node => node.WithStatement(block),
                ForEachVariableStatementSyntax node => node.WithStatement(block),
                WhileStatementSyntax node => node.WithStatement(block),
                DoStatementSyntax node => node.WithStatement(block),
                UsingStatementSyntax node => node.WithStatement(block),
                LockStatementSyntax node => node.WithStatement(block),
                FixedStatementSyntax node => node.WithStatement(block),
                _ => visited
            };
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
