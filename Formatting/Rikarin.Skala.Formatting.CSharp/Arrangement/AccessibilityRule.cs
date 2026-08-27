using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Rikarin.Skala.Formatting.CSharp.Arrangement;

/// <summary>
/// <c>private int _n;</c> ⇒ <c>int _n;</c>, under
/// <c>dotnet_style_require_accessibility_modifiers = omit_if_default</c>.
/// </summary>
/// <remarks>
/// ⚠ Not in doc 06's own catalogue prose, which lists the key in passing under "Modifiers,
/// accessors, attributes" and then does not say what it does. Measuring the cleanup profile is what
/// put it here: it was the single largest divergence class in the first arrangement differential —
/// the oracle drops every redundant <c>private</c> and Skala kept them, on nearly every type in the
/// corpus.
/// <para>
/// ⚠ Syntactic, and only just. "The default" is a property of the *declaration site* — a member of a
/// class or struct defaults to <c>private</c>, a member of an interface to <c>public</c>, a
/// top-level type to <c>internal</c>, an <c>enum</c>'s members to nothing at all — and every one of
/// those is readable from the parent node. No symbol is consulted, which is what keeps it in the
/// subset an agent gets on a loose file.
/// </para>
/// </remarks>
public sealed class AccessibilityRule : ArrangementRule {
    public override string Id => ArrangeIds.Accessibility;

    public override bool NeedsSemantics => false;

    public override bool IsEnabled(in ArrangementOptions options) => options.OmitDefaultAccessibility;

    public override SyntaxNode Apply(ArrangementContext context) => new Rewriter().Visit(context.Root);

    sealed class Rewriter : CSharpSyntaxRewriter {
        public override SyntaxNode? VisitFieldDeclaration(FieldDeclarationSyntax node) =>
            Strip((FieldDeclarationSyntax)base.VisitFieldDeclaration(node)!, node);

        public override SyntaxNode? VisitMethodDeclaration(MethodDeclarationSyntax node) =>
            Strip((MethodDeclarationSyntax)base.VisitMethodDeclaration(node)!, node);

        public override SyntaxNode? VisitPropertyDeclaration(PropertyDeclarationSyntax node) =>
            Strip((PropertyDeclarationSyntax)base.VisitPropertyDeclaration(node)!, node);

        public override SyntaxNode? VisitEventFieldDeclaration(EventFieldDeclarationSyntax node) =>
            Strip((EventFieldDeclarationSyntax)base.VisitEventFieldDeclaration(node)!, node);

        public override SyntaxNode? VisitIndexerDeclaration(IndexerDeclarationSyntax node) =>
            Strip((IndexerDeclarationSyntax)base.VisitIndexerDeclaration(node)!, node);

        public override SyntaxNode? VisitConstructorDeclaration(ConstructorDeclarationSyntax node) =>
            Strip((ConstructorDeclarationSyntax)base.VisitConstructorDeclaration(node)!, node);

        static TMember Strip<TMember>(TMember visited, TMember original) where TMember : MemberDeclarationSyntax {
            if (!IsDefaultPrivate(original)) {
                return visited;
            }

            var modifiers = visited.Modifiers;
            var index = -1;
            for (var i = 0; i < modifiers.Count; i++) {
                if (modifiers[i].IsKind(SyntaxKind.PrivateKeyword)) {
                    index = i;
                    break;
                }
            }

            if (index < 0) {
                return visited;
            }

            // ⚠ `private protected` is one accessibility spelled in two words and is not the
            // default; dropping the `private` from it widens the member to `protected`.
            foreach (var modifier in modifiers) {
                if (modifier.IsKind(SyntaxKind.ProtectedKeyword)) {
                    return visited;
                }
            }

            var removed = modifiers[index];
            var remaining = modifiers.RemoveAt(index);

            // The removed token carried the member's leading trivia — its doc comment, its
            // attributes' blank line, its indentation. It moves to whatever is first now, and to the
            // member itself when nothing is.
            if (remaining.Count > 0) {
                return (TMember)visited.WithModifiers(
                    remaining.Replace(remaining[0], remaining[0].WithLeadingTrivia(removed.LeadingTrivia))
                );
            }

            return (TMember)visited.WithModifiers(remaining).WithLeadingTrivia(removed.LeadingTrivia);
        }

        /// <summary>
        /// Whether an omitted accessibility on this member would mean <c>private</c>.
        /// </summary>
        /// <remarks>
        /// ⚠ Only class, struct and record members. An interface member defaults to <c>public</c>, so
        /// an explicit <c>private</c> there is load-bearing; an <c>enum</c> has no member
        /// accessibility at all; and a member of a namespace is a type, whose default is
        /// <c>internal</c> and whose <c>private</c> is illegal anyway.
        /// </remarks>
        static bool IsDefaultPrivate(SyntaxNode member) =>
            member.Parent is ClassDeclarationSyntax or StructDeclarationSyntax or RecordDeclarationSyntax;
    }
}
