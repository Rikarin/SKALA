using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Rikarin.Skala.Formatting.CSharp.Arrangement;

/// <summary>
///     <c>new System.Text.StringBuilder()</c> ⇒ <c>new StringBuilder()</c>, under
///     <c>skala_prefer_qualified_reference = false</c> (#460).
/// </summary>
/// <remarks>
///     ⚠ Measured against <c>jb cleanupcode</c> 2025.2.6 under <c>SkalaCleanup</c> (SK-DIV-0073 has the
///     whole table). At the export's <c>false</c> the oracle drops a namespace qualifier — written out,
///     <c>global::</c>-rooted, an enclosing namespace's or a namespace alias's — wherever the remaining
///     name binds to the same type at that position: a declaration, a <c>new</c>, a cast, <c>typeof</c>,
///     <c>default</c>, <c>nameof</c>, a pattern, a <c>catch</c>, a base list, a constraint, an attribute, a
///     type argument, an array, and the receiver of a static member access. What it does <b>not</b> do
///     is as much the rule:
///     <list type="bullet">
///         <item>it never adds a using: <c>System.Text.RegularExpressions.Regex</c> with no using for it stays;</item>
///         <item>
///             it never shortens part-way: with <c>using System.Text;</c> the same name stays whole and is never
///             written <c>RegularExpressions.Regex</c>;
///         </item>
///         <item>it never reaches for an alias: <c>using Abe = System.Text.StringBuilder;</c> is not used;</item>
///         <item>it never touches a documentation <c>cref</c>;</item>
///         <item>
///             a <c>global::</c> whose name cannot lose its namespace loses only the <c>global::</c>:
///             <c>[global::System.Diagnostics.CodeAnalysis.SuppressMessage]</c> becomes
///             <c>[System.Diagnostics.CodeAnalysis.SuppressMessage]</c>.
///         </item>
///     </list>
///     <para>
///         ⚠ <b>A shortened name can make a using necessary that was removable when the pass began</b>, and
///         the oracle keeps it: <c>using System.Text;</c> beside only <c>System.Text.StringBuilder</c> ends
///         as <c>using System.Text;</c> and <c>StringBuilder</c>. <see cref="UsingsRule" /> decides removal
///         from a set computed before the pass, so it would delete the directive the shortened name now
///         binds through, and safety layer 2 would revert the whole document on <c>CS0246</c>. Every
///         directive a shortening relies on is therefore recorded in <see cref="Required" />, which the
///         usings rule of the same pass reads.
///     </para>
///     <para>
///         ⚠ Only the <c>false</c> direction exists. At <c>true</c> the oracle qualifies the references
///         that only an explicit using made bind, and drops those usings; this rule does nothing at
///         <c>true</c>, which is why the key is Tier D.
///     </para>
/// </remarks>
public sealed class QualifiedReferenceRule : ArrangementRule {
    public override string Id => ArrangeIds.QualifiedReference;

    /// <summary>⚠ Semantic: a short name is only a spelling of the long one where it binds to the same type.</summary>
    public override bool NeedsSemantics => true;

    /// <summary>
    ///     The using directives (by <see cref="UsingsRule.Key(UsingDirectiveSyntax)" />) a shortening in this
    ///     pass binds through, and which therefore must not be removed in it.
    /// </summary>
    public HashSet<string> Required { get; } = new(StringComparer.Ordinal);

    public override bool IsEnabled(in ArrangementOptions options) => !options.PreferQualifiedReference;

    public override SyntaxNode Apply(ArrangementContext context) =>
        new Rewriter(context.Guard, context.Semantics, Required).Visit(context.Root);

    sealed class Rewriter(FormatterTagGuard guard, SemanticModel model, HashSet<string> required)
        : GuardedRewriter(guard) {
        public override SyntaxNode? VisitQualifiedName(QualifiedNameSyntax node) {
            var visited = (QualifiedNameSyntax)base.VisitQualifiedName(node)!;

            // Only the outermost name: `A.B.C` is one decision, never three overlapping ones.
            return node.Parent is QualifiedNameSyntax ? visited : Shorten(node, visited);
        }

        public override SyntaxNode? VisitAliasQualifiedName(AliasQualifiedNameSyntax node) {
            var visited = (AliasQualifiedNameSyntax)base.VisitAliasQualifiedName(node)!;
            return node.Parent is QualifiedNameSyntax or MemberAccessExpressionSyntax
                ? visited
                : Shorten(node, visited);
        }

        public override SyntaxNode? VisitMemberAccessExpression(MemberAccessExpressionSyntax node) {
            var visited = (MemberAccessExpressionSyntax)base.VisitMemberAccessExpression(node)!;
            if (!node.IsKind(SyntaxKind.SimpleMemberAccessExpression)
                || visited is not MemberAccessExpressionSyntax { RawKind: (int)SyntaxKind.SimpleMemberAccessExpression }
                || model.GetSymbolInfo(node).Symbol is not INamedTypeSymbol) {
                return visited;
            }

            // The outermost type in a chain: `System.Collections.Generic.List<int>.Enumerator` is
            // decided once, at `Enumerator`.
            return node.Parent is MemberAccessExpressionSyntax parent
                && parent.Expression == node
                && model.GetSymbolInfo(parent).Symbol is INamedTypeSymbol
                    ? visited
                    : Shorten(node, visited);
        }

        ExpressionSyntax Shorten(ExpressionSyntax node, ExpressionSyntax visited) {
            if (IsOutOfScope(node) || HasTriviaInside(node)) {
                return visited;
            }

            var attribute = node.Parent is AttributeSyntax owner && owner.Name == node ? owner : null;
            var bound = model.GetSymbolInfo(attribute ?? (SyntaxNode)node).Symbol;
            if (bound is null || (attribute is null ? bound is not INamedTypeSymbol : bound is not IMethodSymbol)) {
                return visited;
            }

            if (!Flatten(node, out var spine, out var segments, out var global)
                || !Flatten(visited, out _, out var written, out _)
                || written.Count != segments.Count) {
                return visited;
            }

            // The last segment of the namespace qualifier, if there is one: everything after it is
            // the type.
            var boundary = -1;
            for (var i = 0; i < spine.Count - 1; i++) {
                if (model.GetSymbolInfo(spine[i]).Symbol is INamespaceSymbol) {
                    boundary = i;
                }
            }

            var expression = node is MemberAccessExpressionSyntax
                || node is AliasQualifiedNameSyntax
                && !SyntaxFacts.IsInTypeOnlyContext(node);

            if (boundary >= 0) {
                var candidate = Build(segments, boundary + 1, expression);
                if (Binds(node, attribute, candidate, bound, expression)) {
                    Require(node, spine[boundary + 1], segments[boundary + 1]);
                    return Build(written, boundary + 1, expression).WithTriviaFrom(visited);
                }
            }

            // ⚠ `global::` alone, when the rest cannot lose its namespace: measured on an attribute.
            if (global && (expression || !AnySegmentIsShadowed(node, spine, segments))) {
                var candidate = Build(segments, 0, expression);
                if (Binds(node, attribute, candidate, bound, expression)) {
                    return Build(written, 0, expression).WithTriviaFrom(visited);
                }
            }

            return visited;
        }

        /// <summary>
        ///     Whether a later segment of the name, looked up on its own here, would find something else.
        /// </summary>
        /// <remarks>
        ///     ⚠ The oracle's, and stricter than the compiler. Measured on
        ///     <c>real/newtonsoft/…/CustomerDataSet.cs</c>, which sits in <c>Newtonsoft.Json.Tests.TestObjects</c>:
        ///     <c>global::System.Data.DataSet</c> and <c>global::System.Diagnostics.…</c> lost their
        ///     <c>global::</c>, while every <c>global::System.Xml.Schema.…</c> and
        ///     <c>global::System.Runtime.Serialization.…</c> kept it — 25 of them, all compiling without it.
        ///     <c>Schema</c> and <c>Serialization</c> are what separates them: from that namespace each also
        ///     names <c>Newtonsoft.Json.Schema</c> and <c>Newtonsoft.Json.Serialization</c>. The compiler
        ///     resolves the dotted name from its first segment and is not confused; a reader may be, and
        ///     <c>global::</c> is what says which one is meant.
        ///     <para>
        ///         ⚠ In a type position only. The same file's <c>global::System.Xml.Schema.XmlSchema.Read(…)</c>
        ///         — a static call's receiver — lost its <c>global::</c>. And a segment is matched with its
        ///         arity: <c>global::System.Collections.IEnumerable</c> lost it too, although
        ///         <c>IEnumerable&lt;T&gt;</c> is in scope.
        ///     </para>
        /// </remarks>
        bool AnySegmentIsShadowed(
            ExpressionSyntax node,
            List<ExpressionSyntax> spine,
            List<SimpleNameSyntax> segments
        ) {
            for (var i = 1; i < segments.Count; i++) {
                var meant = model.GetSymbolInfo(spine[i]).Symbol;
                meant = (meant as IMethodSymbol)?.ContainingType ?? meant;
                var arity = segments[i] is GenericNameSyntax generic ? generic.Arity : 0;
                // ⚠ A loop rather than two lambdas over the iteration's `arity` and `meant` (SK4002).
                var found = false;
                var meantIsFound = false;
                foreach (var symbol in model.LookupNamespacesAndTypes(
                             node.SpanStart,
                             name: segments[i].Identifier.ValueText
                         )) {
                    if (symbol is INamedTypeSymbol type && type.Arity != arity) {
                        continue;
                    }

                    found = true;
                    if (SymbolEqualityComparer.Default.Equals(symbol, meant)) {
                        meantIsFound = true;
                        break;
                    }
                }

                if (found && !meantIsFound) {
                    return true;
                }
            }

            return false;
        }

        bool Binds(
            ExpressionSyntax node,
            AttributeSyntax? attribute,
            ExpressionSyntax candidate,
            ISymbol bound,
            bool expression
        ) {
            var option = expression
                ? SpeculativeBindingOption.BindAsExpression
                : SpeculativeBindingOption.BindAsTypeOrNamespace;
            var info = attribute is not null
                ? model.GetSpeculativeSymbolInfo(attribute.SpanStart, attribute.WithName((NameSyntax)candidate))
                : model.GetSpeculativeSymbolInfo(node.SpanStart, candidate, option);

            return info.CandidateSymbols.IsEmpty && SymbolEqualityComparer.Default.Equals(info.Symbol, bound);
        }

        /// <summary>
        ///     Records the directives the shortened name now binds through, unless a <c>global using</c>
        ///     provides the namespace as well — then the file's own directive is redundant either way, and
        ///     the oracle removes it.
        /// </summary>
        void Require(ExpressionSyntax node, ExpressionSyntax first, SimpleNameSyntax name) {
            // An attribute's name binds to its constructor; the namespace is the type's.
            var symbol = model.GetSymbolInfo(first).Symbol;
            if ((symbol as INamedTypeSymbol ?? (symbol as IMethodSymbol)?.ContainingType)
                is not { ContainingNamespace: { } containing }) {
                return;
            }

            // ⚠ By name, not by symbol: a metadata type's namespace is its module's, while a using's name
            // binds to the compilation's merged namespace, and the two are never Equal (measured — the
            // first version of this compared symbols and kept nothing).
            var imported = containing.ToDisplayString();

            foreach (var scope in model.GetImportScopes(node.SpanStart)) {
                foreach (var import in scope.Imports) {
                    if (import.NamespaceOrType?.ToDisplayString() == imported
                        && import.DeclaringSyntaxReference?.GetSyntax() is UsingDirectiveSyntax {
                            GlobalKeyword.RawKind: (int)SyntaxKind.GlobalKeyword
                        }) {
                        return;
                    }
                }
            }

            if (node.SyntaxTree.GetRoot() is not CompilationUnitSyntax unit) {
                return;
            }

            foreach (var directive in unit.DescendantNodes().OfType<UsingDirectiveSyntax>()) {
                if (directive.Alias is { } alias
                        ? alias.Name.Identifier.ValueText == name.Identifier.ValueText
                        : directive.StaticKeyword == default
                        && model.GetSymbolInfo(directive.Name!).Symbol is INamespaceSymbol named
                        && named.ToDisplayString() == imported) {
                    required.Add(UsingsRule.Key(directive));
                }
            }
        }

        static bool IsOutOfScope(SyntaxNode node) {
            foreach (var ancestor in node.AncestorsAndSelf()) {
                if (ancestor is UsingDirectiveSyntax
                    or BaseNamespaceDeclarationSyntax
                    or ExternAliasDirectiveSyntax
                    or CrefSyntax
                    or DocumentationCommentTriviaSyntax
                    or ExplicitInterfaceSpecifierSyntax) {
                    return true;
                }

                if (ancestor is MemberDeclarationSyntax or StatementSyntax) {
                    return false;
                }
            }

            return false;
        }

        /// <summary>A comment or directive inside the name: the span the edit deletes is the author's.</summary>
        static bool HasTriviaInside(SyntaxNode node) =>
            node.DescendantTrivia()
                .Any(trivia => node.Span.Contains(trivia.Span)
                    && (trivia.IsDirective
                        || trivia.IsKind(SyntaxKind.SingleLineCommentTrivia)
                        || trivia.IsKind(SyntaxKind.MultiLineCommentTrivia))
                );

        /// <summary>
        ///     A dotted name as its segments, with the node that spells each prefix: <c>spine[i]</c> is the
        ///     name made of <c>segments[0..i]</c>.
        /// </summary>
        static bool Flatten(
            ExpressionSyntax node,
            out List<ExpressionSyntax> spine,
            out List<SimpleNameSyntax> segments,
            out bool global
        ) {
            spine = [];
            segments = [];
            global = false;
            return Walk(node, spine, segments, ref global);
        }

        static bool Walk(
            ExpressionSyntax node,
            List<ExpressionSyntax> spine,
            List<SimpleNameSyntax> segments,
            ref bool global
        ) {
            switch (node) {
                case QualifiedNameSyntax qualified:
                    if (!Walk(qualified.Left, spine, segments, ref global)) {
                        return false;
                    }

                    segments.Add(qualified.Right);
                    spine.Add(qualified);
                    return true;

                case MemberAccessExpressionSyntax { RawKind: (int)SyntaxKind.SimpleMemberAccessExpression } access:
                    if (!Walk(access.Expression, spine, segments, ref global)) {
                        return false;
                    }

                    segments.Add(access.Name);
                    spine.Add(access);
                    return true;

                // ⚠ `global::` only. An extern alias names an assembly the short form may not reach.
                case AliasQualifiedNameSyntax alias when alias.Alias.Identifier.IsKind(SyntaxKind.GlobalKeyword):
                    global = true;
                    segments.Add(alias.Name);
                    spine.Add(alias);
                    return true;

                case SimpleNameSyntax simple:
                    segments.Add(simple);
                    spine.Add(simple);
                    return true;

                default:
                    return false;
            }
        }

        static ExpressionSyntax Build(List<SimpleNameSyntax> segments, int from, bool expression) {
            ExpressionSyntax built = segments[from].WithoutTrivia();
            for (var i = from + 1; i < segments.Count; i++) {
                var right = segments[i].WithoutTrivia();
                built = expression
                    ? SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, built, right)
                    : SyntaxFactory.QualifiedName((NameSyntax)built, right);
            }

            return built;
        }
    }
}
