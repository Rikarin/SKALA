using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Analysis.Loading;
using Rikarin.Skala.Core.Configuration;
using Rikarin.Skala.Formatting;
using Rikarin.Skala.Formatting.CSharp;
using Rikarin.Skala.Formatting.CSharp.Arrangement;

namespace Rikarin.Skala.Testing;

/// <summary>One region of the original that at least one of the two tools rewrote.</summary>
/// <remarks>
///     ⚠ M4's bar is per-*span* rather than per-line, which is the shape milestone 3.1's <c>locate</c>
///     established the need for. A line-keyed number would score a member that moved as dozens of
///     divergent lines and a <c>String</c> that became <c>string</c> as one, which ranks the cheap
///     disagreement above the expensive one.
/// </remarks>
public sealed record ChangedSpan(string File, int Line, string Original, string Oracle, string Skala) {
    public bool Agrees =>
        string.Equals(
            TextNormalisation.Normalise(Oracle),
            TextNormalisation.Normalise(Skala),
            StringComparison.Ordinal
        );

    /// <summary>Which tool moved this span, for the ranked report.</summary>
    public string Class =>
        Agrees ? "agreed"
        : string.Equals(
            TextNormalisation.Normalise(Oracle),
            TextNormalisation.Normalise(Original),
            StringComparison.Ordinal
        ) ? "skala only"
        : string.Equals(
            TextNormalisation.Normalise(Skala),
            TextNormalisation.Normalise(Original),
            StringComparison.Ordinal
        ) ? "oracle only"
        : "both, differently";
}

/// <summary>The arrangement half of docs/plan/12's level 2: Skala against the cleanup profile.</summary>
public sealed record ArrangementReport(
    int Files,
    int Spans,
    int Agreed,
    ImmutableArray<ChangedSpan> Divergences,
    int NotConverged,
    int Reverted,
    IReadOnlyDictionary<int, int>? Passes = null) {
    public double Agreement => Spans == 0 ? 1 : (double)Agreed / Spans;

    public string Render(int classes = 8) {
        var builder = new StringBuilder();
        builder.Append("changed spans agreed: ")
            .Append((Agreement * 100).ToString("F2", CultureInfo.InvariantCulture))
            .Append(" % (")
            .Append(Agreed.ToString(CultureInfo.InvariantCulture))
            .Append('/')
            .Append(Spans.ToString(CultureInfo.InvariantCulture))
            .Append(") over ")
            .Append(Files.ToString(CultureInfo.InvariantCulture))
            .AppendLine(" files");

        // ⚠ The fixed point's cost, measured rather than assumed. Two passes is the ordinary case;
        // three means a rewrite exposed a rewrite that was not available before it, which is the
        // whole reason the pipeline loops. Four would mean a rule and the formatter disagree.
        if (Passes is { Count: > 0 }) {
            builder.Append("passes to a fixed point: ")
                .AppendLine(
                    string.Join(
                        ", ",
                        Passes.OrderBy(static pair => pair.Key)
                            .Select(static pair =>
                                pair.Key.ToString(CultureInfo.InvariantCulture)
                                + "×"
                                + pair.Value.ToString(CultureInfo.InvariantCulture)
                            )
                    )
                );
        }

        if (NotConverged > 0 || Reverted > 0) {
            builder.Append("⚠ did not converge: ")
                .Append(NotConverged.ToString(CultureInfo.InvariantCulture))
                .Append("; reverted by a safety layer: ")
                .Append(Reverted.ToString(CultureInfo.InvariantCulture))
                .AppendLine();
        }

        foreach (var group in Divergences.GroupBy(static span => span.Class, StringComparer.Ordinal)
                     .OrderByDescending(static group => group.Count())
                     .Take(classes)) {
            builder.Append("  ")
                .Append(group.Count().ToString(CultureInfo.InvariantCulture).PadLeft(5))
                .Append("  ")
                .AppendLine(group.Key);

            foreach (var sample in group.Take(3)) {
                builder.Append("         ")
                    .Append(sample.File)
                    .Append(':')
                    .Append(sample.Line.ToString(CultureInfo.InvariantCulture))
                    .AppendLine();
                builder.Append("           oracle │ ").AppendEscaped(FirstLine(sample.Oracle)).AppendLine();
                builder.Append("           skala  │ ").AppendEscaped(FirstLine(sample.Skala)).AppendLine();
            }
        }

        return builder.ToString();
    }

    static string FirstLine(string text) {
        var lines = TextNormalisation.Lines(text);
        var first = lines.Length > 0 ? lines[0].Trim() : string.Empty;
        var suffix = lines.Length > 1
            ? " …+" + (lines.Length - 1).ToString(CultureInfo.InvariantCulture)
            : string.Empty;
        return (first.Length > 110 ? first[..110] : first) + suffix;
    }
}

/// <summary>
///     Runs the arrangement pipeline over a corpus set and compares it to the cleanup fixtures.
/// </summary>
public static class ArrangementDifferential {
    /// <summary>
    ///     ⚠ The SDK's implicit usings for a library, as an explicit tree.
    /// </summary>
    /// <remarks>
    ///     ⚠ This is the difference between a measurement and a systematic 100 %-disagreement on every
    ///     using in the corpus. <see cref="OracleRunner.ProjectFile" /> sets
    ///     <c>&lt;ImplicitUsings&gt;enable&lt;/ImplicitUsings&gt;</c>, so the oracle sees
    ///     <c>using System;</c> as redundant and deletes it; a compilation built without them sees it as
    ///     load-bearing and keeps it. Neither is wrong — they are answers to different questions — and
    ///     comparing them measures the scratch project rather than the arranger. The fix is to ask both
    ///     sides the same question.
    /// </remarks>
    public const string ImplicitUsings = """
                                         global using global::System;
                                         global using global::System.Collections.Generic;
                                         global using global::System.IO;
                                         global using global::System.Linq;
                                         global using global::System.Net.Http;
                                         global using global::System.Threading;
                                         global using global::System.Threading.Tasks;
                                         """;

    /// <summary>
    ///     A loose compilation over a set of files, which is what gives the semantic half its model.
    /// </summary>
    /// <remarks>
    ///     ⚠ One compilation for the whole set rather than one per file, and it is not only for speed:
    ///     "is this using unused" is a question about a compilation, and a compilation of one file
    ///     answers it differently from a compilation of the project the file lives in. A per-file
    ///     compilation would report every cross-file using as removable.
    /// </remarks>
    public static CSharpCompilation Compile(IEnumerable<CorpusFile> files, IReadOnlyList<string>? symbols = null) {
        var options = CSharpFormatter.ParseOptionsFor(symbols);
        var trees = new List<SyntaxTree> {
            CSharpSyntaxTree.ParseText(SourceText.From(ImplicitUsings), options, "GlobalUsings.g.cs")
        };

        trees.AddRange(
            files.Select(file => CSharpSyntaxTree.ParseText(CSharpFormatter.Read(file.Path), options, file.Path))
        );

        return CSharpCompilation.Create(
            "arrangement",
            trees,
            SharedFrameworkReferences.Value,
            // ⚠ Nullable enabled and unsafe allowed, both to match OracleRunner.ProjectFile. The
            // nullable context changes which `!= null` checks the compiler considers meaningful and
            // therefore what the null-pattern rule sees, so a mismatch here is not cosmetic.
            new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                allowUnsafe: true,
                nullableContextOptions: NullableContextOptions.Enable
            )
        );
    }

    /// <summary>What Skala's pipeline produces for one file, with the compilation already built.</summary>
    public static PipelineResult Run(
        CorpusFile file,
        CSharpCompilation compilation,
        bool aggressive = false,
        ArrangementFilter? filter = null,
        IReadOnlyList<string>? symbols = null
    ) {
        var text = CSharpFormatter.Read(file.Path);
        var resolved = OptionResolver.Resolve(file.Path).Options;
        var arrangement = new ArrangementOptions(resolved, ArrangementScope.Full, aggressive);
        var removable = Removable(compilation, file.Path);
        return ArrangementPipeline.Run(
            file.Path,
            text,
            new PhaseOneOptions(resolved),
            arrangement,
            compilation,
            removable,
            // ⚠ One compilation, so the pipeline's own single-compilation recomputation is the same
            // answer this call site would give — see ArrangementPipeline.Recompute.
            null,
            null,
            symbols,
            filter
        );
    }

    /// <summary>
    ///     The usings this file may lose.
    /// </summary>
    /// <remarks>
    ///     ⚠ The intersection across every compilation that names the file, and with one compilation the
    ///     intersection is that compilation's own answer. docs/plan/06: "Skala removes a using only when
    ///     it is unused in *every* compilation the file participates in — multi-targeting is not an edge
    ///     case in this ecosystem." The corpus is single-target, so this call site exercises the
    ///     degenerate case; <c>ArrangeCommand</c> exercises the real one.
    /// </remarks>
    public static ImmutableHashSet<string> Removable(CSharpCompilation compilation, string path) {
        foreach (var tree in compilation.SyntaxTrees) {
            if (string.Equals(tree.FilePath, path, StringComparison.Ordinal)) {
                return UsingsRule.Unused(compilation.GetSemanticModel(tree), tree);
            }
        }

        return [];
    }

    public static ArrangementReport Measure(
        IReadOnlyList<CorpusFile> files,
        bool aggressive = false,
        ArrangementFilter? filter = null,
        TextWriter? log = null
    ) {
        var measured = files.Where(static file => file.HasFixtureFor(OracleProfile.Cleanup)).ToArray();
        if (measured.Length == 0) {
            return new ArrangementReport(0, 0, 0, [], 0, 0);
        }

        var compilation = Compile(measured);
        var spans = 0;
        var agreed = 0;
        var notConverged = 0;
        var reverted = 0;
        var divergences = ImmutableArray.CreateBuilder<ChangedSpan>();
        var passes = new Dictionary<int, int>();

        for (var i = 0; i < measured.Length; i++) {
            var file = measured[i];
            if (log is not null && i % 25 == 0) {
                log.WriteLine(
                    $"  {i.ToString(CultureInfo.InvariantCulture)}/{measured.Length.ToString(CultureInfo.InvariantCulture)}"
                );
            }

            var original = CSharpFormatter.Read(file.Path).ToString();
            var oracle = OracleFixture.Read(file, OracleProfile.Cleanup);
            var result = Run(file, compilation, aggressive, filter);
            passes[result.Passes] = passes.GetValueOrDefault(result.Passes) + 1;
            if (!result.Converged) {
                notConverged++;
            }

            foreach (var diagnostic in result.Diagnostics) {
                if (diagnostic.Id is not (ArrangeIds.Reverted
                        or ArrangeIds.SymbolChanged
                        or ArrangementPipeline.DidNotConverge)) {
                    continue;
                }

                reverted++;

                // ⚠ Always printed, never counted and forgotten. A revert is the tool saying it
                // found a rewrite that changed meaning; a run that reports "1 reverted" and not
                // which one is a run that has hidden the only interesting thing it found.
                log?.WriteLine($"    ⚠ {file}: {diagnostic.Id} {diagnostic.Message}");
            }

            var skipUsings = (filter ?? ArrangementFilter.All).Exclude.Contains(ArrangeIds.Usings);
            foreach (var span in Compare(file.ToString(), original, oracle, result.Text, skipUsings)) {
                spans++;
                if (span.Agrees) {
                    agreed++;
                } else {
                    divergences.Add(span);
                }
            }
        }

        return new ArrangementReport(
            measured.Length,
            spans,
            agreed,
            divergences.ToImmutable(),
            notConverged,
            reverted,
            passes
        );
    }

    /// <summary>
    ///     The changed spans of one file, and what each tool made of each.
    /// </summary>
    /// <remarks>
    ///     ⚠ The definition, written down because "agree on 99 % of changed spans" is only a number once
    ///     "a changed span" is one. Both tools' edits are computed against the same original text and
    ///     their spans are merged into maximal disjoint regions; a region is a *changed span*, and it
    ///     *agrees* when applying each tool's own edits to that region produces the same text.
    ///     <para>
    ///         ⚠ Merging matters. Comparing the two edit lists pairwise would count one tool's single
    ///         three-line rewrite against the other's three one-line rewrites as six disagreements over the
    ///         same code; merging first asks the question once per region of the original, which is the
    ///         question a reviewer asks.
    ///     </para>
    /// </remarks>
    public static IEnumerable<ChangedSpan> Compare(
        string name,
        string original,
        string oracle,
        string skala,
        bool skipUsingBlock = false
    ) {
        var oracleEdits = ArrangementEdits.Diff(original, oracle);
        var skalaEdits = ArrangementEdits.Diff(original, skala);
        var merged = Merge(oracleEdits, skalaEdits);
        var usingsEnd = skipUsingBlock ? UsingBlockEnd(original) : 0;

        foreach (var region in merged) {
            // ⚠ Excluded rather than counted as a disagreement, when the usings rule is out of the
            // comparison. Disabling Skala's rule does not remove the oracle's own using removal from
            // its fixture, so every using the oracle deleted would still be scored — against a rule
            // that was not allowed to run. See ArrangementFilter.OracleComparable for why the
            // oracle's answer here is untrustworthy over corpus/real/.
            if (region.End <= usingsEnd) {
                continue;
            }

            yield return new ChangedSpan(
                name,
                LineOf(original, region.Start),
                original[region.Start..region.End],
                Project(original, oracleEdits, region),
                Project(original, skalaEdits, region)
            );
        }
    }

    /// <summary>
    ///     The offset just past the file's last using directive, or 0 when it has none.
    /// </summary>
    /// <remarks>
    ///     ⚠ Parsed rather than scanned for the string "using": a `using` *statement* inside a method
    ///     and a `using` inside a raw string are both text that looks like a directive, and treating
    ///     either as one would exclude a span in the middle of the file from the measurement.
    /// </remarks>
    static int UsingBlockEnd(string source) {
        var root = CSharpSyntaxTree.ParseText(SourceText.From(source), CSharpFormatter.ParseOptions).GetRoot();
        var end = 0;
        foreach (var directive in root.DescendantNodes()
                     .OfType<Microsoft.CodeAnalysis.CSharp.Syntax.UsingDirectiveSyntax>()) {
            end = Math.Max(end, directive.FullSpan.End);
        }

        // ⚠ Past the blank line that follows the block, too. A using's own FullSpan ends after its
        // newline; the *blank* line after it is leading trivia of whatever comes next. A diff hunk
        // that deletes the block covers both lines, so a boundary drawn at the directive's end is
        // one character short and the exclusion silently does nothing.
        while (end < source.Length && (source[end] == '\n' || source[end] == '\r' || source[end] == ' ')) {
            end++;
        }

        return end;
    }

    static List<SourceSpan> Merge(IReadOnlyList<TextEdit> left, IReadOnlyList<TextEdit> right) {
        var spans = new List<SourceSpan>(left.Count + right.Count);
        foreach (var edit in left) {
            spans.Add(edit.Span);
        }

        foreach (var edit in right) {
            spans.Add(edit.Span);
        }

        spans.Sort(static (a, b) => a.Start != b.Start ? a.Start.CompareTo(b.Start) : a.End.CompareTo(b.End));

        var merged = new List<SourceSpan>();
        foreach (var span in spans) {
            if (merged.Count > 0 && span.Start <= merged[^1].End) {
                merged[^1] = SourceSpan.FromBounds(merged[^1].Start, Math.Max(merged[^1].End, span.End));
                continue;
            }

            merged.Add(span);
        }

        return merged;
    }

    /// <summary>What one tool made of one region, by applying only the edits inside it.</summary>
    static string Project(string original, IReadOnlyList<TextEdit> edits, SourceSpan region) {
        var builder = new StringBuilder();
        var cursor = region.Start;
        foreach (var edit in edits) {
            if (edit.Span.Start < region.Start || edit.Span.End > region.End) {
                continue;
            }

            builder.Append(original, cursor, edit.Span.Start - cursor);
            builder.Append(edit.NewText);
            cursor = edit.Span.End;
        }

        builder.Append(original, cursor, region.End - cursor);
        return builder.ToString();
    }

    static int LineOf(string text, int offset) {
        var line = 1;
        for (var i = 0; i < offset && i < text.Length; i++) {
            if (text[i] == '\n') {
                line++;
            }
        }

        return line;
    }
}
