using System.Globalization;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Rikarin.Skala.Testing;

/// <summary>One syntax kind's presence in the corpus.</summary>
/// <param name="Kind">The kind, as Roslyn names it.</param>
/// <param name="Layout">The layout <c>corpus/syntax-kinds.txt</c> records for it.</param>
/// <param name="Occurrences">Nodes of this kind across every parseable corpus input.</param>
/// <param name="Files">How many distinct inputs contain at least one.</param>
/// <param name="Sets">Which corpus sets it occurs in, ordinal-sorted.</param>
public sealed record KindCoverage(
    string Kind,
    string Layout,
    int Occurrences,
    int Files,
    IReadOnlyList<string> Sets);

/// <summary>
///     The complement of <see cref="ConstructReport" />.
/// </summary>
/// <remarks>
///     ⚠ <see cref="ConstructReport" /> answers <em>of the constructs the corpus contains, which
///     diverge</em>. It cannot answer the other half, and the other half is the one with a deadline on
///     it: a construct that appears nowhere in <c>Testing/corpus/</c> has no fidelity number, no
///     fixture and no divergence entry, and once <c>jb</c> is uninstalled no authoritative fixture for
///     it can ever be authored. Absence is invisible to every other instrument this repository has.
///     <para>
///         ⚠ Enumerated rather than sampled. <c>corpus/syntax-kinds.txt</c> is the exhaustive list of
///         what the pinned Roslyn can express, so the denominator is a committed artefact rather than a
///         hand-written list of constructs somebody thought of — which is exactly the failure mode this
///         measurement exists to catch.
///     </para>
///     <para>
///         ⚠ Counted over the <em>inputs</em>, not the <c>.expected.cs</c> fixtures. The question is what
///         the corpus was authored to contain; an input and its fixture hold the same constructs, and
///         counting both would double every number for no gain. <c>ConstructReport</c> counts the
///         oracle's output instead, because a divergent line is a line of output.
///     </para>
///     <para>
///         ⚠ Parsed twice — bare and with <see cref="Corpus.PropertySymbols" /> — and the per-kind count
///         is the larger. A construct that lives only inside a <c>#if</c> body is disabled text under one
///         parse and a node under the other, and neither parse alone answers "does the corpus contain
///         it".
///     </para>
/// </remarks>
public static class SyntaxCoverage {
    /// <summary>docs/plan/16 § R1's "more than 50 times". At or below it a construct is thin.</summary>
    public const int Threshold = 50;

    /// <summary>The layout marker <c>syntax-kinds.txt</c> gives kinds that never reach the builder.</summary>
    public const string TokenOrTrivia = "token-or-trivia";

    /// <summary>Every kind the pinned Roslyn declares, with its layout, read from the committed inventory.</summary>
    public static IReadOnlyDictionary<string, string> Inventory() {
        var inventory = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var line in File.ReadLines(Path.Combine(Corpus.Root, "syntax-kinds.txt"))) {
            if (line.Length == 0 || line[0] == '#') {
                continue;
            }

            var parts = line.Split('\t');
            if (parts.Length == 2) {
                inventory[parts[0].Trim()] = parts[1].Trim();
            }
        }

        return inventory;
    }

    /// <summary>
    ///     True for the inventory rows that name a kind a <see cref="SyntaxNode" /> can actually carry.
    /// </summary>
    /// <remarks>
    ///     ⚠ Not simply "the layout is not <see cref="TokenOrTrivia" />", and the difference is a fact
    ///     about the probe rather than about the corpus. <c>NodeLayouts.IsNodeKind</c> — which is what
    ///     classified the inventory — is a range check on the enum's integer value, and three token kinds
    ///     sit above the boundary: <c>InterpolatedSingleLineRawStringStartToken</c>,
    ///     <c>InterpolatedMultiLineRawStringStartToken</c> and <c>InterpolatedRawStringEndToken</c>. They
    ///     are the <c>$"""</c> and <c>"""</c> delimiters of an interpolated raw string — tokens, never
    ///     nodes — so no corpus can ever exercise them and counting them makes the absent list three rows
    ///     longer than the real gap. They are excluded here rather than corrected in the committed
    ///     inventory, because the inventory is checked against <c>NodeLayouts.Classify</c> and it does
    ///     record what <c>Classify</c> returns.
    ///     <para>
    ///         ⚠ The corresponding <em>node</em> — an interpolated raw string — is not absent at all; only
    ///         these three token rows are.
    ///     </para>
    /// </remarks>
    static bool IsNodeKind(string kind, string layout) =>
        !string.Equals(layout, TokenOrTrivia, StringComparison.Ordinal)
        && !kind.EndsWith("Token", StringComparison.Ordinal);

    /// <summary>Every node kind the inventory declares, present or absent, with its corpus counts.</summary>
    public static IReadOnlyList<KindCoverage> Build(IReadOnlyList<string>? sets = null) {
        var inventory = Inventory();
        var occurrences = new Dictionary<string, int>(StringComparer.Ordinal);
        var files = new Dictionary<string, int>(StringComparer.Ordinal);
        var inSets = new Dictionary<string, SortedSet<string>>(StringComparer.Ordinal);

        foreach (var file in (sets ?? [Corpus.Constructs, Corpus.Real, Corpus.Pathological]).SelectMany(Corpus.Files)) {
            string text;
            try {
                text = File.ReadAllText(file.Path);
            } catch (IOException) {
                continue;
            }

            // ⚠ The per-kind maximum of the two parses, not the sum: the same node counted under both
            // symbol sets is one node. `pathological/` carries a file that does not parse at all, which
            // needs no special case — a tree with errors still yields the nodes it did recognise, and
            // that is the honest answer to "what is in the file".
            var bare = Count(text, null);
            var defined = Count(text, Corpus.PropertySymbols);

            foreach (var kind in bare.Keys.Union(defined.Keys, StringComparer.Ordinal)) {
                var count = Math.Max(bare.GetValueOrDefault(kind), defined.GetValueOrDefault(kind));
                occurrences[kind] = occurrences.GetValueOrDefault(kind) + count;
                files[kind] = files.GetValueOrDefault(kind) + 1;
                if (!inSets.TryGetValue(kind, out var owners)) {
                    inSets[kind] = owners = new SortedSet<string>(StringComparer.Ordinal);
                }

                owners.Add(file.Set);
            }
        }

        return [
            .. inventory
                .Where(static entry => IsNodeKind(entry.Key, entry.Value))
                .Select(entry => new KindCoverage(
                        entry.Key,
                        entry.Value,
                        occurrences.GetValueOrDefault(entry.Key),
                        files.GetValueOrDefault(entry.Key),
                        inSets.TryGetValue(entry.Key, out var owners) ? [.. owners] : []
                    )
                )
                .OrderBy(static coverage => coverage.Occurrences)
                .ThenBy(static coverage => coverage.Kind, StringComparer.Ordinal)
        ];
    }

    /// <summary>
    ///     The constructs a <see cref="SyntaxKind" /> census cannot see, counted by asking directly.
    /// </summary>
    /// <remarks>
    ///     ⚠ The kind enumeration is exhaustive over <em>nodes</em>, and several of the newest and most
    ///     format-sensitive constructs in C# are not nodes of their own. A raw string literal is a
    ///     <c>StringLiteralExpression</c> like any other — only its token kind differs; <c>required</c>,
    ///     <c>scoped</c>, <c>file</c> and <c>ref readonly</c> are modifier tokens; a generic attribute is
    ///     an <c>Attribute</c> whose name happens to be a <c>GenericName</c>; a primary constructor is a
    ///     <c>ParameterList</c> hanging off a type declaration. Reading "290 of 290 node kinds" as
    ///     "every construct" would be exactly the mistake this file exists to prevent, so the blind spot
    ///     is measured rather than described.
    /// </remarks>
    public static IReadOnlyList<KindCoverage> Probes(IReadOnlyList<string>? sets = null) {
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        var files = new Dictionary<string, int>(StringComparer.Ordinal);
        var inSets = new Dictionary<string, SortedSet<string>>(StringComparer.Ordinal);
        foreach (var name in ProbeNames) {
            counts[name] = 0;
            files[name] = 0;
            inSets[name] = new SortedSet<string>(StringComparer.Ordinal);
        }

        foreach (var file in (sets ?? [Corpus.Constructs, Corpus.Real, Corpus.Pathological]).SelectMany(Corpus.Files)) {
            string text;
            try {
                text = File.ReadAllText(file.Path);
            } catch (IOException) {
                continue;
            }

            var root = CSharpSyntaxTree
                .ParseText(text, Rikarin.Skala.Formatting.CSharp.CSharpFormatter.ParseOptionsFor(Corpus.PropertySymbols))
                .GetRoot();

            foreach (var (name, count) in ProbeCounts(root)) {
                if (count == 0) {
                    continue;
                }

                counts[name] += count;
                files[name]++;
                inSets[name].Add(file.Set);
            }
        }

        return [
            .. ProbeNames
                .Select(name => new KindCoverage(name, "probe", counts[name], files[name], [.. inSets[name]]))
                .OrderBy(static c => c.Occurrences)
                .ThenBy(static c => c.Kind, StringComparer.Ordinal)
        ];
    }

    static readonly string[] ProbeNames = [
        "raw string literal",
        "interpolated raw string",
        "utf-8 string literal",
        "required member",
        "file-local type",
        "scoped modifier",
        "ref readonly parameter",
        "generic attribute",
        "alias to a non-name type",
        "primary constructor (class/struct)",
        "checked operator",
        "static abstract interface member",
        "collection expression as an argument",
        "nested collection expression"
    ];

    static IEnumerable<(string Name, int Count)> ProbeCounts(SyntaxNode root) {
        var tokens = root.DescendantTokens(descendIntoTrivia: true).ToArray();
        var nodes = root.DescendantNodesAndSelf(descendIntoTrivia: true).ToArray();

        int Tokens(params SyntaxKind[] kinds) => tokens.Count(token => Array.IndexOf(kinds, token.Kind()) >= 0);

        yield return ("raw string literal",
            Tokens(SyntaxKind.SingleLineRawStringLiteralToken, SyntaxKind.MultiLineRawStringLiteralToken));

        yield return ("interpolated raw string",
            Tokens(
                SyntaxKind.InterpolatedSingleLineRawStringStartToken,
                SyntaxKind.InterpolatedMultiLineRawStringStartToken
            ));

        yield return ("utf-8 string literal", Tokens(SyntaxKind.Utf8StringLiteralToken));
        yield return ("required member", Tokens(SyntaxKind.RequiredKeyword));
        yield return ("file-local type", Tokens(SyntaxKind.FileKeyword));
        yield return ("scoped modifier", Tokens(SyntaxKind.ScopedKeyword));

        // ⚠ `ref readonly` in a parameter, not a `ref readonly` return and not a `readonly` field: the
        // three spell the same two keywords and only the parameter is the C# 12 addition.
        yield return ("ref readonly parameter",
            nodes.OfType<Microsoft.CodeAnalysis.CSharp.Syntax.ParameterSyntax>()
                .Count(parameter => parameter.Modifiers.Any(SyntaxKind.RefKeyword)
                    && parameter.Modifiers.Any(SyntaxKind.ReadOnlyKeyword)
                ));

        yield return ("generic attribute",
            nodes.OfType<Microsoft.CodeAnalysis.CSharp.Syntax.AttributeSyntax>()
                .Count(static attribute => attribute.Name is Microsoft.CodeAnalysis.CSharp.Syntax.GenericNameSyntax));

        // `using Point = (int X, int Y);` and `using Buffer = byte[];` — C# 12's alias-any-type. An alias
        // to an ordinary name has been legal since C# 1 and says nothing about it.
        yield return ("alias to a non-name type",
            nodes.OfType<Microsoft.CodeAnalysis.CSharp.Syntax.UsingDirectiveSyntax>()
                .Count(static directive => directive.Alias is not null
                    && directive.NamespaceOrType is not Microsoft.CodeAnalysis.CSharp.Syntax.NameSyntax
                ));

        yield return ("primary constructor (class/struct)",
            nodes.OfType<Microsoft.CodeAnalysis.CSharp.Syntax.TypeDeclarationSyntax>()
                .Count(static type => type.ParameterList is not null
                    && type is not Microsoft.CodeAnalysis.CSharp.Syntax.RecordDeclarationSyntax
                ));

        // ⚠ The C# 11 *declaration* — `static Money operator checked -(…)` — not the `checked` keyword,
        // which `checked { }` and `checked(…)` also spell and which have their own node kinds. Counting
        // the bare token reported three where the answer is zero, which is a fact about the probe.
        yield return ("checked operator",
            nodes.OfType<Microsoft.CodeAnalysis.CSharp.Syntax.BaseMethodDeclarationSyntax>()
                .Count(static member => member is Microsoft.CodeAnalysis.CSharp.Syntax.OperatorDeclarationSyntax {
                        CheckedKeyword.RawKind: not 0
                    }
                    or Microsoft.CodeAnalysis.CSharp.Syntax.ConversionOperatorDeclarationSyntax {
                        CheckedKeyword.RawKind: not 0
                    }
                ));

        yield return ("static abstract interface member",
            nodes.OfType<Microsoft.CodeAnalysis.CSharp.Syntax.MemberDeclarationSyntax>()
                .Count(static member => member.Modifiers.Any(SyntaxKind.StaticKeyword)
                    && member.Modifiers.Any(SyntaxKind.AbstractKeyword)
                    && member.Parent is Microsoft.CodeAnalysis.CSharp.Syntax.InterfaceDeclarationSyntax
                ));

        // Where a collection expression sits decides which wrapping rule owns it, and an argument
        // position is the one that can collide with the argument-list rules.
        yield return ("collection expression as an argument",
            nodes.OfType<Microsoft.CodeAnalysis.CSharp.Syntax.CollectionExpressionSyntax>()
                .Count(static collection => collection.Parent
                    is Microsoft.CodeAnalysis.CSharp.Syntax.ArgumentSyntax
                ));

        yield return ("nested collection expression",
            nodes.OfType<Microsoft.CodeAnalysis.CSharp.Syntax.CollectionExpressionSyntax>()
                .Count(static collection => collection.Ancestors()
                    .OfType<Microsoft.CodeAnalysis.CSharp.Syntax.CollectionExpressionSyntax>()
                    .Any()
                ));
    }

    static Dictionary<string, int> Count(string text, IReadOnlyList<string>? symbols) {
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        var root = CSharpSyntaxTree
            .ParseText(text, Rikarin.Skala.Formatting.CSharp.CSharpFormatter.ParseOptionsFor(symbols))
            .GetRoot();

        // ⚠ `DescendantNodes` over the whole tree *including* structured trivia: `#if`, `#region` and
        // every documentation-comment element are nodes that live in trivia, and a walk that skips
        // them reports `RegionDirectiveTrivia` absent from a corpus with 40 of them.
        foreach (var node in root.DescendantNodesAndSelf(descendIntoTrivia: true)) {
            var kind = node.Kind().ToString();
            counts[kind] = counts.GetValueOrDefault(kind) + 1;
        }

        return counts;
    }

    /// <summary>The coverage table, banded.</summary>
    public static string Render(IReadOnlyList<KindCoverage> coverage) {
        var builder = new StringBuilder();
        var absent = coverage.Where(static c => c.Occurrences == 0).ToArray();
        var thin = coverage.Where(static c => c.Occurrences is > 0 and <= Threshold).ToArray();
        var covered = coverage.Where(static c => c.Occurrences > Threshold).ToArray();

        builder.Append("node kinds: ")
            .Append(coverage.Count.ToString(CultureInfo.InvariantCulture))
            .Append("; exercised: ")
            .Append((coverage.Count - absent.Length).ToString(CultureInfo.InvariantCulture))
            .Append("; absent: ")
            .Append(absent.Length.ToString(CultureInfo.InvariantCulture))
            .Append("; thin (1..")
            .Append(Threshold.ToString(CultureInfo.InvariantCulture))
            .Append("): ")
            .Append(thin.Length.ToString(CultureInfo.InvariantCulture))
            .Append("; over threshold: ")
            .Append(covered.Length.ToString(CultureInfo.InvariantCulture))
            .AppendLine();

        Band(builder, "token-level constructs the kind census cannot see", [.. Probes()]);
        Band(builder, "absent", absent);
        Band(builder, "present once", [.. thin.Where(static c => c.Occurrences == 1)]);
        Band(builder, "2..9", [.. thin.Where(static c => c.Occurrences is >= 2 and <= 9)]);
        Band(builder, "10..50", [.. thin.Where(static c => c.Occurrences is >= 10 and <= Threshold)]);
        return builder.ToString();
    }

    /// <summary>The whole table, every kind, as the committed artefact's machine-written half.</summary>
    /// <remarks>
    ///     ⚠ Every row, including the well-covered ones. A coverage artefact that prints only the gaps
    ///     cannot be used to check a claim that something <em>is</em> covered, and "is X in the corpus"
    ///     is the question this file exists to answer without re-running anything.
    /// </remarks>
    public static string RenderMarkdown(IReadOnlyList<KindCoverage> coverage) {
        var builder = new StringBuilder();
        builder.AppendLine("| kind | layout | occurrences | files | sets |");
        builder.AppendLine("|---|---|---:|---:|---|");
        foreach (var row in coverage.OrderByDescending(static c => c.Occurrences)
                     .ThenBy(static c => c.Kind, StringComparer.Ordinal)) {
            builder.Append("| `")
                .Append(row.Kind)
                .Append("` | ")
                .Append(row.Layout)
                .Append(" | ")
                .Append(row.Occurrences.ToString(CultureInfo.InvariantCulture))
                .Append(" | ")
                .Append(row.Files.ToString(CultureInfo.InvariantCulture))
                .Append(" | ")
                .Append(row.Sets.Count == 0 ? "—" : string.Join(", ", row.Sets))
                .AppendLine(" |");
        }

        return builder.ToString();
    }

    static void Band(StringBuilder builder, string title, KindCoverage[] rows) {
        if (rows.Length == 0) {
            return;
        }

        builder.AppendLine();
        builder.Append("── ")
            .Append(title)
            .Append(" (")
            .Append(rows.Length.ToString(CultureInfo.InvariantCulture))
            .AppendLine(") ──");

        foreach (var row in rows.OrderBy(static c => c.Kind, StringComparer.Ordinal)) {
            builder.Append("  ")
                .Append(row.Kind.PadRight(42))
                .Append(row.Layout.PadRight(18))
                .Append(row.Occurrences.ToString(CultureInfo.InvariantCulture).PadLeft(6))
                .Append(row.Files.ToString(CultureInfo.InvariantCulture).PadLeft(6))
                .Append("  ")
                .AppendLine(string.Join(",", row.Sets));
        }
    }
}
