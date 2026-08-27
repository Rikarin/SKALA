using System.Diagnostics.CodeAnalysis;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Rikarin.Skala.Formatting.CSharp.Arrangement;

/// <summary>
/// The base every arrangement rewriter derives from, and the single place <c>@formatter:off</c> is
/// enforced for the twelve rules of <see cref="Arranger.Rules"/>.
/// </summary>
/// <remarks>
/// ⚠ <see cref="Visit"/> is sealed on purpose. Twelve rewriters each remembering to ask the guard is
/// twelve chances to forget, and the one that forgets is the one that eats somebody's table. Rules
/// override <c>VisitXxx</c>; the choke point is above all of them.
/// </remarks>
public abstract class GuardedRewriter(FormatterTagGuard guard) : CSharpSyntaxRewriter {
    protected FormatterTagGuard Guard { get; } = guard;

    /// <remarks>
    /// ⚠ <see cref="NotNullIfNotNullAttribute"/> is not decoration: <c>CSharpSyntaxRewriter.Visit</c>
    /// carries it, every generated <c>VisitXxx</c> relies on it, and an override that drops it turns
    /// eleven call sites into <c>CS8603</c>. It is honest here because no rule in the catalogue
    /// deletes a node by returning null from a visit.
    /// </remarks>
    [return: NotNullIfNotNull(nameof(node))]
    public sealed override SyntaxNode? Visit(SyntaxNode? node) {
        if (node is null || Guard.IsEmpty) {
            return base.Visit(node);
        }

        // Entirely inside a region: not descended into, so no rule ever sees it and no semantic
        // model is ever asked about it.
        if (Guard.Encloses(node.Span)) {
            return node;
        }

        var rewritten = base.Visit(node);
        if (ReferenceEquals(rewritten, node)) {
            return node;
        }

        // Crossing a tag: skipped whole — see FormatterTagGuard.Straddles for why.
        if (Guard.Straddles(node.Span)) {
            return node;
        }

        return Guard.Preserves(node, rewritten) ? rewritten : node;
    }
}
