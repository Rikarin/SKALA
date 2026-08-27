using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Rikarin.Skala.Options;

namespace Rikarin.Skala.Formatting.CSharp.Arrangement;

/// <summary>
/// <c>StaticHost.Value</c> ⇒ <c>Value</c> inside <c>StaticHost</c>, under
/// <c>resharper_static_members_qualify_members</c>.
/// </summary>
/// <remarks>
/// ⚠ The key is a *member-kind set*, not a boolean, and it runs in both directions: a kind in the
/// set is qualified, a kind outside it is unqualified. The export writes <c>none</c>, so on this
/// repository's configuration the rule only ever removes — but the option is implemented both ways,
/// because "the export happens to ask for one direction" is not a reason to ship half a rewrite.
/// Measured against <c>jb cleanupcode</c> 2025.2.6 with <c>CSArrangeQualifiers</c>: at <c>none</c>
/// the oracle deletes an existing <c>StaticHost.</c>, and at <c>field, property, method</c> it adds
/// one.
/// <para>
/// ⚠ <c>resharper_static_members_qualify_with = declared_type</c> chooses the name written when a
/// qualifier is added — the type that declares the member rather than the type the code is in. It
/// only has an effect in the adding direction, which is why it is claimed by this rule and not a
/// separate one.
/// </para>
/// <para>
/// ⚠ There is no instance-member counterpart here and the asymmetry is the configuration's, not an
/// omission. <c>resharper_instance_members_qualify_members</c> — the key that would say which
/// instance members take a <c>this.</c> — is not in the author's export and so is not in the option
/// registry at all. Removing <c>this.</c> is <see cref="ThisQualifierRule"/> under
/// <c>resharper_remove_this_qualifier</c>; adding it has no key to read.
/// </para>
/// </remarks>
public sealed class StaticQualifierRule : ArrangementRule {
    public override string Id => ArrangeIds.StaticQualifier;

    /// <summary>
    /// ⚠ Semantic in both directions. Removing <c>T.M</c> is only legal when the bare <c>M</c> binds
    /// to the same symbol at that position, and adding one is only legal when <c>M</c> resolves to a
    /// static member in the first place — a local named <c>M</c> that shadows it must not acquire a
    /// type qualifier.
    /// </summary>
    public override bool NeedsSemantics => true;

    public override bool IsEnabled(in ArrangementOptions options) => true;

    public override SyntaxNode Apply(ArrangementContext context) =>
        new Rewriter(context.Guard, context.Semantics, context.Options).Visit(context.Root);

    /// <summary>Whether the configured member-kind set covers this symbol.</summary>
    internal static bool Covers(MemberKind kinds, ISymbol symbol) =>
        kinds switch {
            MemberKind.All => true,
            MemberKind.Field => symbol is IFieldSymbol,
            MemberKind.Property => symbol is IPropertySymbol,
            MemberKind.Event => symbol is IEventSymbol,
            MemberKind.Method => symbol is IMethodSymbol,
            _ => false
        };

    sealed class Rewriter(FormatterTagGuard guard, SemanticModel model, ArrangementOptions options)
        : GuardedRewriter(guard) {
        public override SyntaxNode? VisitMemberAccessExpression(MemberAccessExpressionSyntax node) {
            var visited = (MemberAccessExpressionSyntax)base.VisitMemberAccessExpression(node)!;
            if (!node.IsKind(SyntaxKind.SimpleMemberAccessExpression)) {
                return visited;
            }

            // Only a *type* receiver is a static-member qualifier. `instance.Member` is not one, and
            // neither is `Namespace.Type`.
            if (model.GetSymbolInfo(node.Expression).Symbol is not ITypeSymbol) {
                return visited;
            }

            if (model.GetSymbolInfo(node).Symbol is not { IsStatic: true } member) {
                return visited;
            }

            if (Covers(options.StaticMembersQualifyMembers, member)) {
                return visited;
            }

            // ⚠ The same precondition ThisQualifierRule uses, for the same reason: the bare name,
            // looked up at this position, must find exactly this symbol. A `using static` import or
            // a local of the same name makes the unqualified form mean something else.
            var candidates = model.LookupSymbols(node.SpanStart, name: node.Name.Identifier.ValueText);
            if (candidates.Length != 1 || !SymbolEqualityComparer.Default.Equals(candidates[0], member)) {
                return visited;
            }

            return visited.Name.WithLeadingTrivia(visited.GetLeadingTrivia())
                .WithTrailingTrivia(visited.GetTrailingTrivia());
        }

        public override SyntaxNode? VisitIdentifierName(IdentifierNameSyntax node) {
            var visited = (IdentifierNameSyntax)base.VisitIdentifierName(node)!;
            if (options.StaticMembersQualifyMembers == MemberKind.None) {
                return visited;
            }

            // The right-hand side of a member access is already qualified, and a declaration's own
            // name is not a reference to it.
            if (node.Parent is MemberAccessExpressionSyntax { Name: var name } && name == node) {
                return visited;
            }

            if (node.Parent is MemberBindingExpressionSyntax
                or QualifiedNameSyntax
                or NameColonSyntax
                or NameEqualsSyntax) {
                return visited;
            }

            if (model.GetSymbolInfo(node).Symbol is not { IsStatic: true } member
                || member.ContainingType is null
                || !Covers(options.StaticMembersQualifyMembers, member)) {
                return visited;
            }

            // ⚠ `nameof(Member)` and an attribute argument read a name rather than evaluate it;
            // qualifying inside `nameof` changes the string it produces.
            if (IsInNameOf(node)) {
                return visited;
            }

            var owner = options.StaticMembersQualifyWith == QualifyWith.DeclaredType
                ? member.ContainingType
                : model.GetEnclosingSymbol(node.SpanStart)?.ContainingType ?? member.ContainingType;

            return SyntaxFactory.MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    SyntaxFactory.IdentifierName(owner.Name),
                    visited.WithoutTrivia()
                )
                .WithLeadingTrivia(visited.GetLeadingTrivia())
                .WithTrailingTrivia(visited.GetTrailingTrivia());
        }

        static bool IsInNameOf(SyntaxNode node) {
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
    }
}
