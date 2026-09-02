using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Rikarin.Skala.Formatting.CSharp.Arrangement;

/// <summary>
///     The trailing comma of a list, added or removed, under
///     <c>resharper_trailing_comma_in_multiline_lists</c> and
///     <c>…_in_singleline_lists</c>.
/// </summary>
/// <remarks>
///     ⚠ Two keys, one rewrite, and which one applies is decided by whether the list's closing token
///     sits on a later line than its last element — that is what ReSharper means by "multiline", not
///     whether the whole declaration spans lines. Measured: with both keys at the export's <c>false</c>
///     the oracle *deletes* an existing trailing comma from a multiline collection initializer, a
///     multiline enum and a single-line array initializer alike; with both at <c>true</c> it adds one to
///     each.
///     <para>
///         ⚠ Only lists whose grammar actually admits a trailing comma are touched, and that set is smaller
///         than it looks: initializers (collection, object, array), enum member lists, and the list of an
///         anonymous object. ⚠ An **argument list and a parameter list are not in it** — C# does not allow
///         `f(a, b,)`, so a rule that treated "list" as one concept would produce a file that does not
///         parse. That is the whole reason this rewrite enumerates node types rather than looking for
///         separated lists generically.
///     </para>
/// </remarks>
public sealed class TrailingCommaRule : ArrangementRule {
    public override string Id => ArrangeIds.TrailingComma;

    public override bool NeedsSemantics => false;

    public override bool IsEnabled(in ArrangementOptions options) => true;

    public override SyntaxNode Apply(ArrangementContext context) =>
        new Rewriter(context.Guard, context.Options).Visit(context.Root);

    sealed class Rewriter(FormatterTagGuard guard, ArrangementOptions options) : GuardedRewriter(guard) {
        public override SyntaxNode? VisitInitializerExpression(InitializerExpressionSyntax node) {
            var visited = (InitializerExpressionSyntax)base.VisitInitializerExpression(node)!;
            var expressions = Adjust(visited.Expressions, node.CloseBraceToken, node);
            return expressions is null ? visited : visited.WithExpressions(expressions.Value);
        }

        public override SyntaxNode? VisitEnumDeclaration(EnumDeclarationSyntax node) {
            var visited = (EnumDeclarationSyntax)base.VisitEnumDeclaration(node)!;
            var members = Adjust(visited.Members, node.CloseBraceToken, node);
            return members is null ? visited : visited.WithMembers(members.Value);
        }

        /// <remarks>
        ///     ⚠ A collection expression is a list the grammar admits a trailing comma in, and it is not
        ///     an <c>InitializerExpressionSyntax</c> — <c>[1, 2, 3,]</c> is a
        ///     <c>CollectionExpressionSyntax</c>. Leaving it out made
        ///     <c>trailing_comma_in_singleline_lists</c> unobservable, which the option's own coverage
        ///     test reported as a failure rather than letting the Tier A claim through.
        /// </remarks>
        public override SyntaxNode? VisitCollectionExpression(CollectionExpressionSyntax node) {
            var visited = (CollectionExpressionSyntax)base.VisitCollectionExpression(node)!;
            var elements = Adjust(visited.Elements, node.CloseBracketToken, node);
            return elements is null ? visited : visited.WithElements(elements.Value);
        }

        public override SyntaxNode? VisitAnonymousObjectCreationExpression(
            AnonymousObjectCreationExpressionSyntax node
        ) {
            var visited = (AnonymousObjectCreationExpressionSyntax)base.VisitAnonymousObjectCreationExpression(node)!;
            var initializers = Adjust(visited.Initializers, node.CloseBraceToken, node);
            return initializers is null ? visited : visited.WithInitializers(initializers.Value);
        }

        /// <summary>
        ///     The list with its trailing comma brought into line with the configuration, or null when
        ///     it already is.
        /// </summary>
        SeparatedSyntaxList<T>? Adjust<T>(SeparatedSyntaxList<T> list, SyntaxToken closing, SyntaxNode original)
            where T : SyntaxNode {
            if (list.Count == 0) {
                return null;
            }

            var wanted = IsMultiline(original, closing)
                ? options.TrailingCommaInMultilineLists
                : options.TrailingCommaInSinglelineLists;

            // A separator count equal to the element count is a trailing comma; one less is not.
            var has = list.SeparatorCount == list.Count;
            if (has == wanted) {
                return null;
            }

            // ⚠ Rebuilt from an alternating node/separator sequence in both directions.
            // `SeparatedSyntaxList` has no operation that appends or drops a *separator*: every
            // method that looks like one is about nodes, and `RemoveAt(list.Count)` — the obvious
            // way to drop a trailing comma — indexes the node list and throws.
            var last = list[^1];
            var parts = new List<SyntaxNodeOrToken>();

            if (!wanted) {
                // The comma's own trailing trivia is not dropped with it: on a multiline list that
                // trivia is the newline before the closing brace, and losing it joins two lines.
                var comma = list.GetSeparator(list.Count - 1);
                for (var i = 0; i < list.Count; i++) {
                    parts.Add(
                        i == list.Count - 1
                            ? last.WithTrailingTrivia(last.GetTrailingTrivia().AddRange(comma.TrailingTrivia))
                            : list[i]
                    );

                    if (i < list.Count - 1) {
                        parts.Add(list.GetSeparator(i));
                    }
                }

                return SyntaxFactory.SeparatedList<T>(parts);
            }

            var trailing = last.GetTrailingTrivia();
            for (var i = 0; i < list.Count; i++) {
                parts.Add(i == list.Count - 1 ? last.WithoutTrailingTrivia() : list[i]);
                if (i < list.SeparatorCount) {
                    parts.Add(list.GetSeparator(i));
                }
            }

            parts.Add(SyntaxFactory.Token(SyntaxKind.CommaToken).WithTrailingTrivia(trailing));
            return SyntaxFactory.SeparatedList<T>(parts);
        }

        /// <summary>
        ///     ⚠ "Multiline" is about the closing token, not the node: <c>new[] { 1, 2, 3 }</c> written
        ///     inside a declaration that wraps is still a single-line list, and the oracle treats it as
        ///     one.
        /// </summary>
        static bool IsMultiline(SyntaxNode node, SyntaxToken closing) {
            var text = node.SyntaxTree?.GetText();
            if (text is null) {
                return closing.GetPreviousToken().ToFullString().Contains('\n', StringComparison.Ordinal)
                    || closing.LeadingTrivia.ToFullString().Contains('\n', StringComparison.Ordinal);
            }

            var previous = closing.GetPreviousToken();
            return text.Lines.GetLinePosition(previous.Span.End).Line
                != text.Lines.GetLinePosition(closing.SpanStart).Line;
        }
    }
}
