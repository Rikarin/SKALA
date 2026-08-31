using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Analysis.Loading;
using Rikarin.Skala.Core.Configuration;
using Rikarin.Skala.Formatting.CSharp;
using Rikarin.Skala.Formatting.CSharp.Arrangement;
using System.Collections.Immutable;
using System.Globalization;
using System.Text;

namespace Rikarin.Skala.Testing;

/// <summary>
///     Arrangement over an arbitrary tree, measured read-only: milestone 4's second bar.
/// </summary>
/// <remarks>
///     ⚠ "Arrangement over Vixen introduces zero compiler diagnostics" is a question about a tree, and
///     milestone 3.1's <c>tree</c> command is the harness shape that asks a tree a question. This is the
///     arrangement half of it.
///     <para>
///         ⚠ The diagnostic comparison here is deliberately <em>independent</em> of
///         <see cref="ArrangementSafety" />. The safety layer reverts any file whose re-bind found a new
///         diagnostic, so "arrangement introduced none" is true by construction — and "by construction" is
///         what every bug says about itself. This re-binds the text the pipeline actually produced, through
///         its own code path, and counts what appeared. A number produced by the thing being measured is not
///         a measurement.
///     </para>
///     <para>
///         ⚠ Writes nothing. The caller supplies a scratch copy (<c>git archive</c>); this reads it.
///     </para>
/// </remarks>
public static class ArrangeTree {
    public sealed record TreeReport(
        int Files,
        int Arranged,
        int NewDiagnostics,
        int RevertedByRebind,
        int RevertedBySymbol,
        int NotConverged,
        int NotParseable,
        ImmutableArray<string> Samples,
        IReadOnlyDictionary<string, int> Applied,
        IReadOnlyDictionary<string, int> RevertCauses,
        IReadOnlyDictionary<string, string> RevertSamples) {
        public string Render() {
            var builder = new StringBuilder();
            builder.Append("files considered:  ").AppendLine(Files.ToString(CultureInfo.InvariantCulture));
            builder.Append("files arranged:    ").AppendLine(Arranged.ToString(CultureInfo.InvariantCulture));
            builder.Append("⚠ NEW DIAGNOSTICS: ").AppendLine(NewDiagnostics.ToString(CultureInfo.InvariantCulture));
            builder.Append("reverted, re-bind: ").AppendLine(RevertedByRebind.ToString(CultureInfo.InvariantCulture));
            builder.Append("reverted, symbol:  ").AppendLine(RevertedBySymbol.ToString(CultureInfo.InvariantCulture));
            builder.Append("did not converge:  ").AppendLine(NotConverged.ToString(CultureInfo.InvariantCulture));
            builder.Append("did not parse:     ").AppendLine(NotParseable.ToString(CultureInfo.InvariantCulture));
            builder.AppendLine();

            foreach (var (id, count) in Applied.OrderByDescending(static pair => pair.Value)) {
                builder.Append("  ")
                    .Append(count.ToString(CultureInfo.InvariantCulture).PadLeft(6))
                    .Append("  ")
                    .Append(id)
                    .Append(' ')
                    .AppendLine(ArrangeIds.NameOf(id));
            }

            if (RevertCauses.Count > 0) {
                builder.AppendLine();
                builder.AppendLine("what the re-bind found, ranked — the missing preconditions:");
                foreach (var (id, count) in RevertCauses.OrderByDescending(static pair => pair.Value)) {
                    builder.Append("  ")
                        .Append(count.ToString(CultureInfo.InvariantCulture).PadLeft(6))
                        .Append("  ")
                        .AppendLine(id);

                    if (RevertSamples.TryGetValue(id, out var sample)) {
                        builder.Append("          ").AppendLine(sample);
                    }
                }
            }

            if (!Samples.IsEmpty) {
                builder.AppendLine();
                builder.AppendLine("⚠ the diagnostics arrangement introduced:");
                foreach (var sample in Samples) {
                    builder.Append("    ").AppendLine(sample);
                }
            }

            return builder.ToString();
        }
    }

    /// <summary>
    ///     The arrangement of the single file whose path contains <paramref name="needle" />, as a diff.
    /// </summary>
    /// <remarks>
    ///     ⚠ The tool for reading a revert. The ranked cause list says <em>which diagnostic</em> the
    ///     re-bind found; it cannot say which rewrite caused it, because a reverted file reports no
    ///     applied rules — it applied none. This runs the rules one at a time and prints the first that
    ///     makes the diagnostic appear, which is the question a person actually has.
    /// </remarks>
    public static string Explain(string root, string mode, string needle, TextWriter log) {
        var loaded = ProjectLoader.Load(new LoadRequest { RepositoryRoot = root, Mode = LoadModes.Parse(mode) });
        log.WriteLine(loaded.Summary);

        foreach (var unit in loaded.Units) {
            foreach (var path in unit.ReportablePaths) {
                if (!path.Contains(needle, StringComparison.OrdinalIgnoreCase)) {
                    continue;
                }

                var text = CSharpFormatter.Read(path);
                var options = ConfigurationCache.Options(EditorConfigChain.For(path), null);
                var builder = new StringBuilder();
                builder.Append("file: ").AppendLine(Path.GetRelativePath(root, path));

                foreach (var rule in Arranger.Rules()) {
                    var result = Arranger.Arrange(
                        path,
                        text,
                        new ArrangementOptions(options),
                        unit.Compilation,
                        [],
                        null,
                        new ArrangementFilter([rule.Id], [])
                    );

                    var verdict = result.Outcome switch {
                        ArrangementOutcome.Reverted => "⚠ REVERTED — "
                            + string.Join(
                                "; ",
                                result.Diagnostics.Select(static d => d.Message)
                            ),
                        ArrangementOutcome.Arranged => "changed",
                        _ => "no change"
                    };

                    builder.Append("  ")
                        .Append(rule.Id)
                        .Append(' ')
                        .Append(ArrangeIds.NameOf(rule.Id).PadRight(24))
                        .AppendLine(verdict);
                }

                return builder.ToString();
            }
        }

        return $"no reportable file matching '{needle}'";
    }

    public static TreeReport Run(string root, string mode, bool aggressive, int limit, TextWriter log) {
        var loaded = ProjectLoader.Load(new LoadRequest { RepositoryRoot = root, Mode = LoadModes.Parse(mode) });
        log.WriteLine(loaded.Summary);

        // Every compilation each file participates in, so using removal can intersect across them —
        // which on a multi-targeted tree is the whole point (docs/plan/06 § "Usings").
        var owners = new Dictionary<string, List<CompilationUnit>>(StringComparer.Ordinal);
        foreach (var unit in loaded.Units) {
            foreach (var path in unit.ReportablePaths) {
                if (!owners.TryGetValue(path, out var list)) {
                    owners[path] = list = [];
                }

                list.Add(unit);
            }
        }

        var files = owners.Keys.Order(StringComparer.Ordinal).Take(limit).ToArray();
        log.WriteLine($"{files.Length.ToString(CultureInfo.InvariantCulture)} reportable files");

        var arranged = 0;
        var newDiagnostics = 0;
        var revertedRebind = 0;
        var revertedSymbol = 0;
        var notConverged = 0;
        var notParseable = 0;
        var applied = new Dictionary<string, int>(StringComparer.Ordinal);
        var reverts = new Dictionary<string, int>(StringComparer.Ordinal);
        var revertSamples = new Dictionary<string, string>(StringComparer.Ordinal);
        var samples = ImmutableArray.CreateBuilder<string>();

        for (var i = 0; i < files.Length; i++) {
            if (i % 200 == 0) {
                log.WriteLine(
                    $"  {i.ToString(CultureInfo.InvariantCulture)}/{files.Length.ToString(CultureInfo.InvariantCulture)}"
                );
            }

            var file = files[i];
            var units = owners[file];
            var text = CSharpFormatter.Read(file);
            var options = ConfigurationCache.Options(EditorConfigChain.For(file), null);
            var symbols = units[0].PreprocessorSymbols;

            var result = ArrangementPipeline.Run(
                file,
                text,
                new PhaseOneOptions(options),
                new ArrangementOptions(options, ArrangementScope.Full, aggressive),
                units[0].Compilation,
                Removable(units, file),
                (rewritten, _) => Removable(units, file, rewritten),
                null,
                symbols
            );

            foreach (var diagnostic in result.Diagnostics) {
                switch (diagnostic.Id) {
                    case ArrangeIds.Reverted:
                        revertedRebind++;

                        // ⚠ Ranked by the diagnostic id the re-bind produced. A revert count on its
                        // own says the layer fired; the ranking says which precondition is missing,
                        // and that is the difference between a number and a work queue.
                        foreach (var id in Ids(diagnostic.Message)) {
                            reverts[id] = reverts.GetValueOrDefault(id) + 1;
                            if (!revertSamples.ContainsKey(id)) {
                                revertSamples[id] = Path.GetRelativePath(root, file)
                                    + "  ["
                                    + string.Join(",", result.Applied)
                                    + "]  "
                                    + diagnostic.Message;
                            }
                        }

                        break;
                    case ArrangeIds.SymbolChanged:
                        revertedSymbol++;
                        break;
                    case ArrangementPipeline.DidNotConverge:
                        notConverged++;
                        break;
                    case FormatDiagnosticIds.NotParseable:
                        notParseable++;
                        break;
                }
            }

            if (result.Edits.IsEmpty) {
                continue;
            }

            arranged++;
            foreach (var id in result.Applied) {
                applied[id] = applied.GetValueOrDefault(id) + 1;
            }

            foreach (var appeared in Introduced(units, file, result.Text)) {
                newDiagnostics++;
                if (samples.Count < 20) {
                    samples.Add(Path.GetRelativePath(root, file) + ": " + appeared);
                }
            }
        }

        return new(
            files.Length,
            arranged,
            newDiagnostics,
            revertedRebind,
            revertedSymbol,
            notConverged,
            notParseable,
            samples.ToImmutable(),
            applied,
            reverts,
            revertSamples
        );
    }

    /// <summary>The compiler diagnostic ids named in a revert message.</summary>
    static IEnumerable<string> Ids(string message) {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var part in message.Split([':', ',', ' '], StringSplitOptions.RemoveEmptyEntries)) {
            var token = part.Split('|')[0];
            if (token.Length is >= 4 and <= 8
                && token.StartsWith("CS", StringComparison.Ordinal)
                && token[2..].All(char.IsAsciiDigit)
                && seen.Add(token)) {
                yield return token;
            }
        }
    }

    /// <summary>
    ///     ⚠ <paramref name="rewritten" /> is the pipeline re-asking about text it has since produced
    ///     (SK-FUZZ-0018). This harness owns several compilations per file, so it has to answer, and the
    ///     answer has to stay the intersection — the pipeline's own fallback holds one compilation.
    /// </summary>
    static ImmutableHashSet<string> Removable(
        List<CompilationUnit> units,
        string file,
        SourceText? rewritten = null
    ) {
        ImmutableHashSet<string>? intersection = null;
        foreach (var unit in units) {
            foreach (var tree in unit.Compilation.SyntaxTrees) {
                if (!string.Equals(tree.FilePath, file, StringComparison.Ordinal)) {
                    continue;
                }

                var current = rewritten is null
                    ? tree
                    : CSharpSyntaxTree.ParseText(rewritten, (CSharpParseOptions)tree.Options, file);

                var bound = rewritten is null
                    ? unit.Compilation
                    : unit.Compilation.ReplaceSyntaxTree(tree, current);

                var unused = UsingsRule.Unused(bound.GetSemanticModel(current), current);
                intersection = intersection is null ? unused : intersection.Intersect(unused);
                break;
            }
        }

        return intersection ?? [];
    }

    /// <summary>
    ///     The diagnostics the rewritten text has and the original did not, in every compilation that
    ///     contains the file.
    /// </summary>
    static IEnumerable<string> Introduced(List<CompilationUnit> units, string file, string arranged) {
        foreach (var unit in units) {
            var tree = unit.Compilation.SyntaxTrees.FirstOrDefault(t =>
                string.Equals(t.FilePath, file, StringComparison.Ordinal)
            );

            if (tree is null) {
                continue;
            }

            var before = Signature(unit.Compilation.GetSemanticModel(tree).GetDiagnostics());
            var rewritten = CSharpSyntaxTree.ParseText(
                SourceText.From(arranged),
                (CSharpParseOptions)tree.Options,
                file
            );

            var after = unit.Compilation.ReplaceSyntaxTree(tree, rewritten);
            foreach (var appeared in Signature(after.GetSemanticModel(rewritten).GetDiagnostics())
                         .Except(before, StringComparer.Ordinal)
                         .Order(StringComparer.Ordinal)) {
                yield return unit.Name + " " + appeared;
            }
        }
    }

    static ImmutableHashSet<string> Signature(IEnumerable<Diagnostic> diagnostics) {
        var set = ImmutableHashSet.CreateBuilder(StringComparer.Ordinal);
        foreach (var diagnostic in diagnostics) {
            if (diagnostic.Severity is DiagnosticSeverity.Error or DiagnosticSeverity.Warning) {
                set.Add(diagnostic.Id + "|" + diagnostic.GetMessage(CultureInfo.InvariantCulture));
            }
        }

        return set.ToImmutable()!;
    }
}
