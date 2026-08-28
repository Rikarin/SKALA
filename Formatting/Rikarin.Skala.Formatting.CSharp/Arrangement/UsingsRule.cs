using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Rikarin.Skala.Options;

namespace Rikarin.Skala.Formatting.CSharp.Arrangement;

/// <summary>
///     Sorts using directives, and removes the ones no compilation needs.
/// </summary>
/// <remarks>
///     ⚠ docs/plan/06 § "Usings": sort alphabetically with <c>System</c> <em>not</em> hoisted
///     (<c>dotnet_sort_system_directives_first = false</c>), no group separation, outside the namespace,
///     one blank line after.
///     <para>
///         ⚠ Removal is the one rewrite that must consider more than one compilation. "Unused in this file"
///         is not the question; "unused in <em>every</em> compilation this file participates in" is, because
///         a using can be needed only under one target framework's <c>#if</c>, or only for an extension
///         method that another target resolves differently. Multi-targeting is not an edge case in this
///         ecosystem. <see cref="Unused" /> answers the per-compilation half and the driver intersects across
///         compilations; with a single compilation the intersection is the identity, which is why a
///         one-framework repository sees no difference and a multi-targeted one is not silently broken.
///     </para>
/// </remarks>
public sealed class UsingsRule : ArrangementRule {
    /// <summary>
    ///     The directives that may be removed, computed per compilation and intersected by the caller.
    /// </summary>
    /// <remarks>
    ///     ⚠ Passed in rather than computed here, because the rule sees one <see cref="SemanticModel" />
    ///     and the decision needs all of them. An empty set means "remove nothing", which is the correct
    ///     answer when only one compilation was available and it could not be trusted to speak for the
    ///     others — the syntactic scope takes exactly that path.
    /// </remarks>
    readonly ImmutableHashSet<string> _removable;

    public UsingsRule(ImmutableHashSet<string>? removable = null) => _removable = removable ?? [];

    public override string Id => ArrangeIds.Usings;

    /// <summary>
    ///     ⚠ False. Sorting needs no semantics at all, and removal takes its answer from
    ///     <see cref="_removable" /> rather than from a model — so the rule runs in the syntactic subset
    ///     and simply removes nothing there. An agent on a loose file still gets its usings sorted.
    /// </summary>
    public override bool NeedsSemantics => false;

    public override bool IsEnabled(in ArrangementOptions options) =>
        options.SortUsings
        || !_removable.IsEmpty
        || options.SeparateImportDirectiveGroups
        || options.UsingDirectivePlacement == UsingDirectivePlacement.InsideNamespace;

    public override SyntaxNode Apply(ArrangementContext context) {
        if (context.Root is not CompilationUnitSyntax unit) {
            return context.Root;
        }

        var namespaceDeclaration = SingleNamespace(unit);
        var toInside = context.Options.UsingDirectivePlacement == UsingDirectivePlacement.InsideNamespace
            && namespaceDeclaration is not null;

        // ⚠ Which directives are *moving* is settled before anything else, because a move that has to
        // be refused must not take the sort and the removal down with it: a file whose using block
        // carries a licence header still gets sorted, it just stays where it is.
        var moved = Movable(unit, namespaceDeclaration, toInside, context);
        var source = toInside ? namespaceDeclaration!.Usings : unit.Usings;
        var block = new List<UsingDirectiveSyntax>(source.Count + moved.Count);
        block.AddRange(source);
        block.AddRange(moved);
        if (block.Count == 0) {
            return context.Root;
        }

        var kept = new List<UsingDirectiveSyntax>(block.Count);
        foreach (var directive in block) {
            if (!IsRemovable(directive, context.Options)) {
                kept.Add(directive);
            }
        }

        var ordered = context.Options.SortUsings ? Sort(kept, context.Options) : kept;
        var renormalised = Separate(
            Renormalise(ordered, source.Count > 0 ? source : SyntaxFactory.List(moved)),
            context.Options.SeparateImportDirectiveGroups
        );

        if (moved.Count == 0 && renormalised.Count == source.Count && Same(renormalised, source)) {
            return context.Root;
        }

        var list = SyntaxFactory.List(renormalised);
        if (namespaceDeclaration is null) {
            return unit.WithUsings(list);
        }

        // ⚠ Both ends are written in one `ReplaceNode`, because writing the namespace first and the
        // unit second would look up a node that the first edit has already replaced.
        var rewrittenNamespace = toInside
            ? WithUsings(namespaceDeclaration, list)
            : WithUsings(namespaceDeclaration, Without(namespaceDeclaration.Usings, moved));

        var withNamespace = unit.ReplaceNode(namespaceDeclaration, rewrittenNamespace);
        return toInside ? withNamespace.WithUsings(default) : withNamespace.WithUsings(list);
    }

    /// <summary>
    ///     The directives that <c>csharp_using_directive_placement</c> wants on the other side of the
    ///     namespace declaration, or nothing when the move is refused.
    /// </summary>
    /// <remarks>
    ///     ⚠ Measured: at <c>inside_namespace</c> the oracle puts the whole block below
    ///     <c>namespace N;</c>, and at the export's <c>outside_namespace</c> it hoists a block written
    ///     inside a block-scoped namespace above it — but it leaves an <em>alias</em> directive at nested
    ///     scope where it stands. An alias resolves against the names in scope where it is written, so
    ///     hoisting one out of the namespace can silently make it name something else.
    ///     <para>
    ///         ⚠ A directive carrying a comment or a preprocessor directive is not moved, and a comment
    ///         anywhere in the block being moved refuses the whole move. SK-FUZZ-0011's lesson: a using's
    ///         trivia is the author writing about that line, and a file header lives on the first directive
    ///         of the block. Moving the block into the namespace would take the header with it.
    ///     </para>
    /// </remarks>
    static List<UsingDirectiveSyntax> Movable(
        CompilationUnitSyntax unit,
        BaseNamespaceDeclarationSyntax? namespaceDeclaration,
        bool toInside,
        ArrangementContext context
    ) {
        var empty = new List<UsingDirectiveSyntax>();
        if (namespaceDeclaration is null) {
            return empty;
        }

        var candidates = new List<UsingDirectiveSyntax>();
        if (toInside) {
            candidates.AddRange(unit.Usings);
        } else {
            foreach (var directive in namespaceDeclaration.Usings) {
                if (directive.Alias is null) {
                    candidates.Add(directive);
                }
            }
        }

        if (candidates.Count == 0) {
            return empty;
        }

        foreach (var directive in candidates) {
            if (!HasNoComment(directive)) {
                return empty;
            }

            if (!context.Guard.IsEmpty && !context.Guard.Preserves(directive, directive)) {
                return empty;
            }

            if (context.Guard.Encloses(directive.Span) || context.Guard.Straddles(directive.Span)) {
                return empty;
            }
        }

        // The block's own leading trivia rides with its first directive, so a header comment above the
        // block is a comment on a directive that is about to move.
        var first = candidates[0];
        foreach (var trivia in first.GetLeadingTrivia()) {
            if (trivia.IsKind(SyntaxKind.SingleLineCommentTrivia)
                || trivia.IsKind(SyntaxKind.MultiLineCommentTrivia)
                || trivia.IsDirective) {
                return empty;
            }
        }

        return candidates;
    }

    /// <summary>
    ///     The one namespace a using block can move across, or null.
    /// </summary>
    /// <remarks>
    ///     ⚠ The same shape as <see cref="NamespaceBodyRule" />'s precondition and for the same reason:
    ///     "inside the namespace" only names a place when there is one namespace, directly under the
    ///     compilation unit. Two namespaces in a file, or one nested in another, and the question has no
    ///     answer — so the block stays where the author put it.
    /// </remarks>
    static BaseNamespaceDeclarationSyntax? SingleNamespace(CompilationUnitSyntax unit) {
        BaseNamespaceDeclarationSyntax? found = null;
        foreach (var declaration in unit.DescendantNodes().OfType<BaseNamespaceDeclarationSyntax>()) {
            if (found is not null) {
                return null;
            }

            found = declaration;
        }

        return found?.Parent is CompilationUnitSyntax ? found : null;
    }

    static BaseNamespaceDeclarationSyntax WithUsings(
        BaseNamespaceDeclarationSyntax declaration,
        SyntaxList<UsingDirectiveSyntax> usings
    ) =>
        declaration switch {
            FileScopedNamespaceDeclarationSyntax scoped => scoped.WithUsings(usings),
            NamespaceDeclarationSyntax block => block.WithUsings(usings),
            _ => declaration
        };

    static SyntaxList<UsingDirectiveSyntax> Without(
        SyntaxList<UsingDirectiveSyntax> usings,
        List<UsingDirectiveSyntax> removed
    ) {
        if (removed.Count == 0) {
            return usings;
        }

        var kept = new List<UsingDirectiveSyntax>();
        foreach (var directive in usings) {
            if (!removed.Contains(directive)) {
                kept.Add(directive);
            }
        }

        return SyntaxFactory.List(kept);
    }

    bool IsRemovable(UsingDirectiveSyntax directive, in ArrangementOptions options) {
        // ⚠ A `global using` is used by files this one cannot see, so a per-file answer is the wrong
        // shape entirely, and a `using static` brings in members the reference walk does not key by
        // namespace name.
        if (directive.GlobalKeyword.IsKind(SyntaxKind.GlobalKeyword)
            || directive.StaticKeyword != default
            || !HasNoComment(directive)
            || !_removable.Contains(Key(directive))) {
            return false;
        }

        // ⚠ An alias is governed by its own pair of keys, and they AND: an unused non-trivial alias
        // goes only when `keep_nontrivial_alias` is false *and* `remove_only_unused_aliases` is true,
        // which is the export's pair and the only one of the four that removes it. An unused
        // *trivial* alias — one whose name is the aliased type's own name — goes at all four.
        // Measured on a probe carrying a used and an unused instance of each shape.
        return directive.Alias is null
            || IsTrivialAlias(directive)
            || (!options.KeepNontrivialAlias && options.RemoveOnlyUnusedAliases);
    }

    /// <summary>
    ///     Whether the alias merely renames a type to the name it already has.
    /// </summary>
    /// <remarks>
    ///     ⚠ "Trivial" is measured rather than guessed at: <c>using Regex =
    ///     System.Text.RegularExpressions.Regex;</c> is trivial and <c>using Trivial = System.String;</c>
    ///     is not, so it is the *identifier* comparison and not "the target is short" or "the target is a
    ///     predefined type". A generic target is never trivial — <c>Map = Dictionary&lt;string, int&gt;</c>
    ///     survives even when the names match, because the alias is carrying the type arguments.
    /// </remarks>
    static bool IsTrivialAlias(UsingDirectiveSyntax directive) {
        if (directive.Alias is not { } alias) {
            return false;
        }

        var last = directive.Name switch {
            QualifiedNameSyntax qualified => qualified.Right,
            SimpleNameSyntax simple => simple,
            _ => null
        };

        return last is IdentifierNameSyntax identifier
            && string.Equals(
                identifier.Identifier.ValueText,
                alias.Name.Identifier.ValueText,
                StringComparison.Ordinal
            );
    }

    /// <summary>
    ///     ⚠ A using with a comment on it is not removed. The comment is the author saying something
    ///     about that line, and a cleanup that deletes prose to save a using has made the file worse.
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
    ///     The names a compilation does not need, for one file.
    /// </summary>
    /// <remarks>
    ///     ⚠ Roslyn already answers this, and answers it better than a hand-rolled reference walk would:
    ///     <c>CS8019</c> is the "unnecessary using directive" diagnostic the compiler itself emits, so
    ///     the set below is the compiler's own opinion about its own binding rather than Skala's model
    ///     of it. A using needed only by a disabled <c>#if</c> branch is *not* reported, which is
    ///     correct for that compilation and is exactly why the caller intersects across all of them.
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
            if (node.FirstAncestorOrSelf<UsingDirectiveSyntax>() is { Name: not null } directive) {
                names.Add(Key(directive));
            }
        }

        return names.ToImmutable()!;
    }

    /// <summary>
    ///     A directive's key: its name, and its alias when it has one.
    /// </summary>
    /// <remarks>
    ///     ⚠ The alias is part of the key and not decoration. Once alias removal exists, two aliases can
    ///     name the same target — <c>using A = System.String;</c> beside <c>using B = System.String;</c> —
    ///     and keying on the target alone makes one directive's diagnostic authorise the other's removal.
    ///     The set is also intersected across compilations by the caller, so the key has to identify the
    ///     directive rather than the namespace it imports.
    /// </remarks>
    static string Key(UsingDirectiveSyntax directive) =>
        directive.Alias is { } alias
            ? alias.Name.Identifier.ValueText + "=" + Key(directive.Name)
            : Key(directive.Name);

    /// <summary>
    ///     A using's name as a key: the identifiers and the dots, with the author's spacing dropped.
    /// </summary>
    /// <remarks>
    ///     ⚠ <c>ToString()</c> on its own carries the trivia <em>between</em> a qualified name's tokens,
    ///     so <c>using System .Threading. Tasks;</c> keys as <c>"System .Threading. Tasks"</c> — and this
    ///     set is computed once, before the pipeline, while the formatter rewrites exactly that spacing
    ///     on its first pass. The removal then fired or did not fire depending on how the author had
    ///     spaced a dotted name, which is a whitespace-dependence in *which rules run*: SK-FUZZ-0013,
    ///     found as an arrangement non-idempotency, where pass 1 tried the removal, was reverted for an
    ///     unrelated reason, and pass 2 could no longer match its own key — so the second pipeline run
    ///     removed a using the first had left. Nothing between the dots of a namespace name is
    ///     significant, so the key drops all of it.
    /// </remarks>
    static string Key(NameSyntax? name) {
        if (name is null) {
            return string.Empty;
        }

        var text = name.ToString();
        if (!text.Any(char.IsWhiteSpace)) {
            return text;
        }

        var key = new System.Text.StringBuilder(text.Length);
        foreach (var character in text) {
            if (!char.IsWhiteSpace(character)) {
                key.Append(character);
            }
        }

        return key.ToString();
    }

    /// <summary>
    ///     ⚠ Ordinal, and <c>System</c> is not hoisted.
    ///     <c>
    /// dotnet_sort_system_directives_first =
    ///  false
    ///     </c> is an unusual choice and it is the author's; sorting <c>System</c> first "because
    ///     everyone does" would move every using block in the repository on the first run.
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
    ///     Moves the leading trivia of the original block onto whatever is first now.
    /// </summary>
    /// <remarks>
    ///     ⚠ A file's header comment and its <c>#region</c> live on the first using's leading trivia. If
    ///     sorting moves a different directive to the front and the trivia rides along with the old one,
    ///     the licence header ends up in the middle of the block. This re-pins the block's opening trivia
    ///     to the block rather than to a directive.
    ///     <para>
    ///         ⚠ SK-FUZZ-0011: every <em>other</em> directive keeps its own leading trivia, and the line
    ///         that blanked it was silent code deletion rather than a layout nicety. `using System.Text;`
    ///         / `// keep me` / `using System.Collections;` sorts to two lines with the comment gone — no
    ///         removal involved, both usings live, an ordinary file. With `#if X` / `#endif` in that
    ///         position it deleted a preprocessor directive, which changes what compiles.
    ///     </para>
    ///     <para>
    ///         ⚠ And it is what made arrangement stop being a fixed point. <see cref="HasNoComment" />
    ///         keeps a using that carries a comment or a directive; blanking that trivia on the first pass
    ///         makes the same directive removable on the second, so the pipeline deleted the comment, then
    ///         the using, then converged on a file that had lost both. The idempotency violation was the
    ///         symptom and the trivia loss was the defect.
    ///     </para>
    /// </remarks>
    static List<UsingDirectiveSyntax> Renormalise(
        List<UsingDirectiveSyntax> ordered,
        SyntaxList<UsingDirectiveSyntax> original
    ) {
        if (ordered.Count == 0) {
            return ordered;
        }

        // The block's opening trivia belongs to the block. It rides to whatever is first now, and the
        // directive it came from surrenders it — otherwise a header whose directive merely moved down
        // would be emitted twice.
        var header = original[0].GetLeadingTrivia();
        var result = new List<UsingDirectiveSyntax>(ordered.Count);
        for (var i = 0; i < ordered.Count; i++) {
            var directive = ordered[i];
            var own = ReferenceEquals(directive, original[0])
                ? SyntaxFactory.TriviaList()
                : directive.GetLeadingTrivia();

            result.Add(Retrivia(directive, i == 0 ? header.AddRange(own) : own));
        }

        return result;
    }

    /// <summary>
    ///     ⚠ Identity is preserved when the trivia does not change, and that is load-bearing rather than
    ///     an allocation nicety. <see cref="Same" /> asks <c>ReferenceEquals</c>, and
    ///     <c>WithLeadingTrivia</c> hands back a fresh node even when the trivia it is given is the
    ///     trivia already there — so a rule that renormalises unconditionally reports every file as
    ///     changed, layer 2 re-binds a tree that is textually identical, and a file whose code does not
    ///     compile comes back <c>Reverted</c> because re-binding invalid code does not produce the same
    ///     diagnostics twice. Found by <c>ARuleThatThrows_CostsItsOwnRewriteAndNotTheProcess</c>.
    /// </summary>
    static UsingDirectiveSyntax Retrivia(UsingDirectiveSyntax directive, SyntaxTriviaList trivia) =>
        directive.GetLeadingTrivia().ToFullString().Equals(trivia.ToFullString(), StringComparison.Ordinal)
            ? directive
            : directive.WithLeadingTrivia(trivia);

    /// <summary>
    ///     The blank lines <c>dotnet_separate_import_directive_groups</c> asks for, and only those.
    /// </summary>
    /// <remarks>
    ///     ⚠ Both directions are the oracle's, measured on a block written with blank lines in the wrong
    ///     places: at <c>true</c> it puts exactly one blank line between directives whose first namespace
    ///     segment differs, and at the export's <c>false</c> it takes every blank line inside the block
    ///     back out. So the rule owns the blank lines *within* the block — the one after it is
    ///     <c>resharper_blank_lines_after_using_list</c> and the formatter's.
    ///     <para>
    ///         ⚠ The grouping is by first segment and nothing finer: <c>System</c> and <c>System.Text</c> are
    ///         one group, <c>Alpha.Wide</c> and <c>Beta.Wide</c> are two.
    ///     </para>
    ///     <para>
    ///         ⚠ The first directive is never touched, because its leading trivia is the block's — a file
    ///         header, a <c>#region</c>, the blank line under a licence comment. Only the separations
    ///         *between* directives are this rule's to write.
    ///     </para>
    /// </remarks>
    static List<UsingDirectiveSyntax> Separate(List<UsingDirectiveSyntax> ordered, bool separate) {
        for (var i = 1; i < ordered.Count; i++) {
            var leading = ordered[i].GetLeadingTrivia();

            // A blank line shows up as an end-of-line trivia at the *front* of the next directive's
            // leading trivia — the newline that ends the previous line is that line's trailing
            // trivia. Anything else in there is a comment or a directive and stays.
            var start = 0;
            while (start < leading.Count && leading[start].IsKind(SyntaxKind.EndOfLineTrivia)) {
                start++;
            }

            var rest = start == 0 ? leading : SyntaxFactory.TriviaList(leading.Skip(start));
            var wanted = separate && !SameGroup(ordered[i - 1], ordered[i])
                ? SyntaxFactory.TriviaList(SyntaxFactory.ElasticCarriageReturnLineFeed).AddRange(rest)
                : rest;

            ordered[i] = Retrivia(ordered[i], wanted);
        }

        return ordered;
    }

    static bool SameGroup(UsingDirectiveSyntax left, UsingDirectiveSyntax right) =>
        string.Equals(FirstSegment(left), FirstSegment(right), StringComparison.Ordinal);

    static string FirstSegment(UsingDirectiveSyntax directive) {
        var name = directive.Name;
        while (name is QualifiedNameSyntax qualified) {
            name = qualified.Left;
        }

        return name is SimpleNameSyntax simple ? simple.Identifier.ValueText : string.Empty;
    }
}

/// <summary>
///     <c>string.Empty</c> ⇒ <c>""</c>, under <c>resharper_empty_string = empty_literal</c>.
/// </summary>
/// <remarks>
///     ⚠ SK-DIV-0013 again: the oracle does not perform this one either — it normalises
///     <c>String.Empty</c> to <c>string.Empty</c> and stops — so it is fixture-pinned rather than
///     oracle-pinned, and excluded from the agreement number. Skala performs it because the export asks
///     for it and doc 06 lists it.
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
