using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Diagnostics.CodeAnalysis;

namespace Rikarin.Skala.Formatting.CSharp.Arrangement;

/// <summary>
///     The base every arrangement rewriter derives from, and the single place <c>@formatter:off</c> is
///     enforced for the twelve rules of <see cref="Arranger.Rules" />.
/// </summary>
/// <remarks>
///     ⚠ <see cref="Visit" /> is sealed on purpose. Twelve rewriters each remembering to ask the guard is
///     twelve chances to forget, and the one that forgets is the one that eats somebody's table. Rules
///     override <c>VisitXxx</c>; the choke point is above all of them.
/// </remarks>
public abstract class GuardedRewriter(FormatterTagGuard guard) : CSharpSyntaxRewriter {
    protected FormatterTagGuard Guard { get; } = guard;

    /// <remarks>
    ///     ⚠ <see cref="NotNullIfNotNullAttribute" /> is not decoration: <c>CSharpSyntaxRewriter.Visit</c>
    ///     carries it, every generated <c>VisitXxx</c> relies on it, and an override that drops it turns
    ///     eleven call sites into <c>CS8603</c>. It is honest here because no rule in the catalogue
    ///     deletes a node by returning null from a visit.
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

    /// <summary>
    ///     Whether the node is somewhere inside the argument of a <c>nameof</c>.
    /// </summary>
    /// <remarks>
    ///     ⚠ Shared rather than copied, and the copies were identical token for token — <c>SK7020</c>
    ///     reported them as one clone group. <see cref="PredefinedTypeRule" /> called it
    ///     <c>IsInsideNameOf</c> and <see cref="StaticQualifierRule" /> called it <c>IsInNameOf</c>; two
    ///     names for one guard is how a fix to one of them misses the other.
    ///     <para>
    ///         ⚠ The walk looks at <c>current.Parent</c> rather than at <c>current</c>, because the node
    ///         inside <c>nameof(Int32)</c> is an <c>ArgumentSyntax</c>'s child — two nodes below the
    ///         invocation, not one. The first version of this guard matched a
    ///         <c>MemberAccessExpressionSyntax</c> parent and so never fired inside a <c>nameof</c> at
    ///         all; safety layer 2 caught the resulting <c>nameof(int)</c> as <c>CS1525</c>.
    ///     </para>
    ///     <para>
    ///         It stops at the first enclosing statement or member declaration: a <c>nameof</c> further
    ///         out than that does not contain this node's evaluation.
    ///     </para>
    /// </remarks>
    protected static bool IsInNameOf(SyntaxNode node) {
        for (var current = node; current is not null; current = current.Parent) {
            if (current.Parent is ArgumentSyntax { Parent.Parent: InvocationExpressionSyntax invocation }
                && invocation.Expression is IdentifierNameSyntax { Identifier.ValueText: "nameof" }) {
                return true;
            }

            if (current is StatementSyntax or MemberDeclarationSyntax) {
                return false;
            }
        }

        return false;
    }

    /// <summary>
    ///     Whether the bare name of <paramref name="node" />, looked up at exactly this position, finds
    ///     <paramref name="symbol" /> and nothing else.
    /// </summary>
    /// <remarks>
    ///     ⚠ The precondition for dropping a qualifier, and it was written out twice — once in
    ///     <see cref="StaticQualifierRule" /> and once in <see cref="ThisQualifierRule" />, whose comment
    ///     already said "the same precondition ThisQualifierRule uses, for the same reason". A
    ///     <c>using static</c> import or a local of the same name makes the unqualified form mean
    ///     something else, and that is true of every rule that removes a qualifier.
    ///     <para>
    ///         ⚠ <c>LookupSymbols</c> is asked rather than the syntax re-bound, because the rewritten tree
    ///         is not in the model and re-binding it would need a whole new compilation — which is safety
    ///         layer 2's job, not layer 1's.
    ///     </para>
    /// </remarks>
    protected static bool BareNameResolvesTo(
        SemanticModel model,
        MemberAccessExpressionSyntax node,
        ISymbol symbol
    ) {
        var candidates = model.LookupSymbols(node.SpanStart, name: node.Name.Identifier.ValueText);
        return candidates.Length == 1 && SymbolEqualityComparer.Default.Equals(candidates[0], symbol);
    }

    /// <summary>
    ///     <c>receiver.Name</c> reduced to <c>Name</c>, keeping the whole access's trivia.
    /// </summary>
    /// <remarks>
    ///     ⚠ The trivia comes from the access and not from the name: the leading trivia belongs to the
    ///     receiver that is being dropped, and moving it onto the surviving name is what keeps a comment
    ///     before the qualifier from disappearing with it.
    /// </remarks>
    protected static SimpleNameSyntax Unqualified(MemberAccessExpressionSyntax visited) =>
        visited.Name.WithLeadingTrivia(visited.GetLeadingTrivia())
            .WithTrailingTrivia(visited.GetTrailingTrivia());
}
