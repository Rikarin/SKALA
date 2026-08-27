using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Rikarin.Skala.Formatting.CSharp.Arrangement;

/// <summary>
/// Sorts using directives, and removes the ones no compilation needs.
/// </summary>
/// <remarks>
/// ⚠ docs/plan/06 § "Usings": sort alphabetically with <c>System</c> <em>not</em> hoisted
/// (<c>dotnet_sort_system_directives_first = false</c>), no group separation, outside the namespace,
/// one blank line after.
/// <para>
/// ⚠ Removal is the one rewrite that must consider more than one compilation. "Unused in this file"
/// is not the question; "unused in <em>every</em> compilation this file participates in" is, because
/// a using can be needed only under one target framework's <c>#if</c>, or only for an extension
/// method that another target resolves differently. Multi-targeting is not an edge case in this
/// ecosystem. <see cref="Unused"/> answers the per-compilation half and the driver intersects across
/// compilations; with a single compilation the intersection is the identity, which is why a
/// one-framework repository sees no difference and a multi-targeted one is not silently broken.
/// </para>
/// </remarks>
public sealed class UsingsRule : ArrangementRule {
    /// <summary>
    /// The directives that may be removed, computed per compilation and intersected by the caller.
    /// </summary>
    /// <remarks>
    /// ⚠ Passed in rather than computed here, because the rule sees one <see cref="SemanticModel"/>
    /// and the decision needs all of them. An empty set means "remove nothing", which is the correct
    /// answer when only one compilation was available and it could not be trusted to speak for the
    /// others — the syntactic scope takes exactly that path.
    /// </remarks>
    readonly ImmutableHashSet<string> _removable;

    public UsingsRule(ImmutableHashSet<string>? removable = null) => _removable = removable ?? [];

    public override string Id => ArrangeIds.Usings;

    /// <summary>
    /// ⚠ False. Sorting needs no semantics at all, and removal takes its answer from
    /// <see cref="_removable"/> rather than from a model — so the rule runs in the syntactic subset
    /// and simply removes nothing there. An agent on a loose file still gets its usings sorted.
    /// </summary>
    public override bool NeedsSemantics => false;

    public override bool IsEnabled(in ArrangementOptions options) => options.SortUsings || !_removable.IsEmpty;

    public override SyntaxNode Apply(ArrangementContext context) {
        if (context.Root is not CompilationUnitSyntax unit || unit.Usings.Count == 0) {
            return context.Root;
        }

        var kept = new List<UsingDirectiveSyntax>();
        foreach (var directive in unit.Usings) {
            if (!IsRemovable(directive)) {
                kept.Add(directive);
            }
        }

        var ordered = context.Options.SortUsings ? Sort(kept, context.Options) : kept;
        if (ordered.Count == unit.Usings.Count && Same(ordered, unit.Usings)) {
            return context.Root;
        }

        return unit.WithUsings(SyntaxFactory.List(Renormalise(ordered, unit.Usings)));
    }

    bool IsRemovable(UsingDirectiveSyntax directive) =>
        // ⚠ An alias and a `global using` are never removed here. `remove_unused_only_aliases =
        // false` in the export means "do not restrict removal to aliases", not "remove aliases"; and
        // a `global using` is used by files this one cannot see, so a per-file answer is the wrong
        // shape entirely.
        directive.Alias is null
        && !directive.GlobalKeyword.IsKind(SyntaxKind.GlobalKeyword)
        && directive.StaticKeyword == default
        && HasNoComment(directive)
        && _removable.Contains(directive.Name?.ToString() ?? string.Empty);

    /// <summary>
    /// ⚠ A using with a comment on it is not removed. The comment is the author saying something
    /// about that line, and a cleanup that deletes prose to save a using has made the file worse.
    /// </summary>
    static bool HasNoComment(UsingDirectiveSyntax directive) {
        foreach (var trivia in directive.DescendantTrivia(descendIntoTrivia: true)) {
            if (trivia.IsKind(SyntaxKind.SingleLineCommentTrivia)
                || trivia.IsKind(SyntaxKind.MultiLineCommentTrivia)
                || trivia.IsDirective) {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// The names a compilation does not need, for one file.
    /// </summary>
    /// <remarks>
    /// ⚠ Roslyn already answers this, and answers it better than a hand-rolled reference walk would:
    /// <c>CS8019</c> is the "unnecessary using directive" diagnostic the compiler itself emits, so
    /// the set below is the compiler's own opinion about its own binding rather than Skala's model
    /// of it. A using needed only by a disabled <c>#if</c> branch is *not* reported, which is
    /// correct for that compilation and is exactly why the caller intersects across all of them.
    /// </remarks>
    public static ImmutableHashSet<string> Unused(
        SemanticModel model,
        SyntaxTree tree,
        CancellationToken cancellation = default
    ) {
        var names = ImmutableHashSet.CreateBuilder(StringComparer.Ordinal);
        foreach (var diagnostic in model.GetDiagnostics(null, cancellation)) {
            if (!string.Equals(diagnostic.Id, "CS8019", StringComparison.Ordinal)) {
                continue;
            }

            var node = tree.GetRoot(cancellation).FindNode(diagnostic.Location.SourceSpan);
            if (node.FirstAncestorOrSelf<UsingDirectiveSyntax>() is { Name: { } name }) {
                names.Add(name.ToString());
            }
        }

        return names.ToImmutable()!;
    }

    /// <summary>
    /// ⚠ Ordinal, and <c>System</c> is not hoisted. <c>dotnet_sort_system_directives_first =
    /// false</c> is an unusual choice and it is the author's; sorting <c>System</c> first "because
    /// everyone does" would move every using block in the repository on the first run.
    /// </summary>
    static List<UsingDirectiveSyntax> Sort(List<UsingDirectiveSyntax> directives, in ArrangementOptions options) {
        var systemFirst = options.SystemDirectivesFirst;
        return [
            .. directives.OrderBy(directive => Rank(directive, systemFirst))
                .ThenBy(static directive => directive.Name?.ToString() ?? string.Empty, StringComparer.Ordinal)
        ];
    }

    static int Rank(UsingDirectiveSyntax directive, bool systemFirst) {
        // ⚠ A `global using` must precede every non-global one — CS8915, and it is a hard language
        // rule rather than a style. Sorting the block ordinally puts `global using X;` wherever `X`
        // falls and breaks the file. Two files on Vixen, found by the re-bind.
        if (directive.GlobalKeyword.IsKind(SyntaxKind.GlobalKeyword)) {
            return int.MinValue / 2;
        }

        // ⚠ Plain, then alias, then `using static` — measured, not assumed. Roslyn's own organiser
        // puts `using static` before aliases and the oracle puts it after, so the "obvious" order is
        // the wrong one here by exactly one swap.
        var group = directive.StaticKeyword != default ? 2 : directive.Alias is not null ? 1 : 0;
        if (!systemFirst || group != 0) {
            return group * 2;
        }

        var name = directive.Name?.ToString() ?? string.Empty;
        return name.StartsWith("System", StringComparison.Ordinal) ? -1 : 0;
    }

    static bool Same(List<UsingDirectiveSyntax> ordered, SyntaxList<UsingDirectiveSyntax> original) {
        for (var i = 0; i < ordered.Count; i++) {
            if (!ReferenceEquals(ordered[i], original[i])) {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Moves the leading trivia of the original block onto whatever is first now.
    /// </summary>
    /// <remarks>
    /// ⚠ A file's header comment and its <c>#region</c> live on the first using's leading trivia. If
    /// sorting moves a different directive to the front and the trivia rides along with the old one,
    /// the licence header ends up in the middle of the block. This re-pins the block's opening trivia
    /// to the block rather than to a directive.
    /// </remarks>
    static List<UsingDirectiveSyntax> Renormalise(
        List<UsingDirectiveSyntax> ordered,
        SyntaxList<UsingDirectiveSyntax> original
    ) {
        if (ordered.Count == 0) {
            return ordered;
        }

        var header = original[0].GetLeadingTrivia();
        var result = new List<UsingDirectiveSyntax>(ordered.Count);
        for (var i = 0; i < ordered.Count; i++) {
            var directive = ordered[i];
            result.Add(
                i == 0
                    ? directive.WithLeadingTrivia(header)
                    : directive.WithLeadingTrivia(SyntaxFactory.TriviaList())
            );
        }

        return result;
    }
}

/// <summary>
/// <c>string.Empty</c> ⇒ <c>""</c>, under <c>resharper_empty_string = empty_literal</c>.
/// </summary>
/// <remarks>
/// ⚠ SK-DIV-0013 again: the oracle does not perform this one either — it normalises
/// <c>String.Empty</c> to <c>string.Empty</c> and stops — so it is fixture-pinned rather than
/// oracle-pinned, and excluded from the agreement number. Skala performs it because the export asks
/// for it and doc 06 lists it.
/// </remarks>
public sealed class EmptyStringRule : ArrangementRule {
    public override string Id => ArrangeIds.EmptyString;

    public override bool NeedsSemantics => true;

    public override bool IsEnabled(in ArrangementOptions options) => options.EmptyStringIsLiteral;

    public override SyntaxNode Apply(ArrangementContext context) =>
        new Rewriter(context.Guard, context.Semantics).Visit(context.Root);

    sealed class Rewriter(FormatterTagGuard guard, SemanticModel model) : GuardedRewriter(guard) {
        public override SyntaxNode? VisitMemberAccessExpression(MemberAccessExpressionSyntax node) {
            var visited = (MemberAccessExpressionSyntax)base.VisitMemberAccessExpression(node)!;
            if (!string.Equals(node.Name.Identifier.ValueText, "Empty", StringComparison.Ordinal)) {
                return visited;
            }

            if (model.GetSymbolInfo(node).Symbol is not IFieldSymbol {
                    IsStatic: true,
                    ContainingType.SpecialType: SpecialType.System_String
                }) {
                return visited;
            }

            // ⚠ `case string.Empty:` and `[DefaultValue(string.Empty)]` are constant contexts and
            // `""` is a constant too, so both are safe; there is no position where one is legal and
            // the other is not. The rewrite is total once the symbol is confirmed.
            return SyntaxFactory.LiteralExpression(
                SyntaxKind.StringLiteralExpression,
                SyntaxFactory.Literal(string.Empty)
            )
                .WithLeadingTrivia(visited.GetLeadingTrivia())
                .WithTrailingTrivia(visited.GetTrailingTrivia());
        }
    }
}
