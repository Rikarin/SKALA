using System.Globalization;
using Rikarin.Skala.Testing;

namespace Rikarin.Skala.Release.Surfaces;

/// <summary>What the corpus looks like after the previous release's tool, and after this one's.</summary>
public sealed record OutputMeasurement(
    int Comparable,
    int ChangedFiles,
    int ChangedLines,
    int AddedToCorpus,
    IReadOnlyList<string> BaselineRefused,
    IReadOnlyList<string> CandidateRefused,
    IReadOnlyList<(string Class, int Lines, int Files, string Example)> Classes
);

/// <summary>
/// ⚠ <b>The detector that matters.</b> docs/plan/18 § "The output detector".
/// </summary>
/// <remarks>
/// Skala's compatibility surface is its output, so the question a release has to answer is not
/// "what did the code change" but "what does the corpus look like afterwards". This is the
/// differential harness with the oracle taken out of it: the same <see cref="Fidelity"/> line diff
/// and the same divergence classifier, run over <b>two Skala builds</b> instead of over Skala and
/// <c>jb cleanupcode</c>.
/// <para>
/// ⚠ <b>Four things make it a measurement instead of a tautology.</b>
/// </para>
/// <list type="number">
/// <item>
/// <b>Two binaries, and they are checked for being two.</b> If the two paths resolve to bytes with
/// the same SHA-256 this throws, because a detector comparing a build against itself reports "no
/// change" forever and looks exactly like a green one.
/// </item>
/// <item>
/// <b>The comparison set is the two corpora's intersection.</b> The corpus only grows (doc 12
/// § "Corpus expansion"), and a file added since the previous release has no "before" — counting it
/// as changed would report a diff on every release that added a fixture, and counting it as
/// unchanged would be a lie. It is reported as an addition and excluded from the change count.
/// ⚠ This is not a detail: the first real run of this detector, 1.0.0 against `master`, had 60 such
/// files and the previous release's tool <b>crashed</b> on one of them, which is the point — those
/// files exist because they broke the formatter.
/// </item>
/// <item>
/// <b>A tool that refuses a file is recorded, not fatal.</b> Formatting runs in chunks, and a chunk
/// that fails is retried one file at a time so that one bad input costs one input rather than the
/// release. A comparable file that one side cannot format and the other can <i>is</i> an output
/// change and is counted as one.
/// </item>
/// <item>
/// <b>Nothing is formatted in this process.</b> Both sides are read back off disk after an external
/// <c>skala format</c> wrote them. This assembly links the formatter — <c>Fidelity</c> lives beside
/// it — and using it here would silently make the candidate both sides of the comparison.
/// </item>
/// </list>
/// <para>
/// ⚠ The inputs and the configuration are held constant and both come from the candidate: the
/// corpus, the repository's <c>.editorconfig</c> and <c>skala.jsonc</c> are staged at their real
/// relative paths, so config discovery walks the directories it walks in the repository. The only
/// variable in the experiment is which binary ran.
/// </para>
/// <para>
/// ⚠ <c>pathological/open/</c> is excluded, the same exclusion <see cref="Corpus.Files"/> makes and
/// for the same reason: one of those files makes <c>skala format</c> throw, and they are held to
/// account by <c>OpenDefectTests</c> instead.
/// </para>
/// </remarks>
public static class OutputSurface {
    public const string Name = "formatted output";

    /// <summary>
    /// Files per <c>skala format</c> invocation. ⚠ Small enough that one refusing input costs one
    /// chunk's worth of single-file retries, large enough that 700 files is seven process starts.
    /// </summary>
    const int ChunkSize = 128;

    public static (DetectorResult Result, OutputMeasurement? Measurement) Run(
        SkalaTool? baseline,
        SkalaTool candidate,
        string? baselineRoot,
        string candidateCorpus,
        string configurationRoot,
        string workRoot
    ) {
        if (baseline is null || baselineRoot is null) {
            return (
                DetectorResult.Unmeasured(
                    Name,
                    "no previous release to format the corpus with — this release establishes the baseline"
                ),
                null);
        }

        if (string.Equals(baseline.Fingerprint, candidate.Fingerprint, StringComparison.Ordinal)) {
            throw new InvalidOperationException(
                $"The baseline and candidate tools are the same bytes ({baseline.Fingerprint[..12]}). "
                + "A differential against itself reports 'no change' unconditionally; build the baseline "
                + "release's tool separately, or pass --baseline-tool."
            );
        }

        var candidateInputs = Inputs(candidateCorpus);
        var baselineInputs = Inputs(Path.Combine(baselineRoot, "Testing", "corpus"));
        var comparable = candidateInputs.Intersect(baselineInputs, StringComparer.Ordinal)
            .OrderBy(static relative => relative, StringComparer.Ordinal)
            .ToList();

        if (comparable.Count == 0) {
            throw new InvalidOperationException(
                $"The two corpora share no files ({candidateInputs.Count} here, {baselineInputs.Count} at the "
                + "baseline). An empty comparison set reports 'no change' for any pair of tools."
            );
        }

        var left = Stage(comparable, candidateCorpus, configurationRoot, Path.Combine(workRoot, "baseline"));
        var right = Stage(comparable, candidateCorpus, configurationRoot, Path.Combine(workRoot, "candidate"));

        var baselineRefused = Format(baseline, left, candidateCorpus, comparable);
        var candidateRefused = Format(candidate, right, candidateCorpus, comparable);

        var refused = baselineRefused.Union(candidateRefused, StringComparer.Ordinal)
            .OrderBy(static relative => relative, StringComparer.Ordinal)
            .ToList();

        var measurement = Measure(
            comparable.Where(relative => !refused.Contains(relative, StringComparer.Ordinal)).ToList(),
            left,
            right,
            candidateInputs.Count - comparable.Count,
            baselineRefused,
            candidateRefused
        );

        // ⚠ A file only one side can format is an output change too — the strongest kind. It is
        // folded into the verdict rather than reported beside it.
        var bump = measurement.ChangedFiles > 0 || refused.Count > 0 ? BumpKind.Minor : BumpKind.Patch;

        var headline = bump == BumpKind.Patch
            ? string.Create(
                CultureInfo.InvariantCulture,
                $"identical on all {measurement.Comparable} comparable corpus files ({measurement.AddedToCorpus} added since the previous release, not comparable)"
            )
            : string.Create(
                CultureInfo.InvariantCulture,
                $"formatting output changed on {measurement.ChangedFiles} of {measurement.Comparable} corpus files ({measurement.ChangedLines} lines)"
            );

        var details = new List<string>();
        details.AddRange(
            measurement.Classes.Select(static entry => string.Create(
                    CultureInfo.InvariantCulture,
                    $"{entry.Lines} lines across {entry.Files} files — {entry.Class} (e.g. {entry.Example})"
                )
            )
        );

        foreach (var relative in baselineRefused.Where(relative => !candidateRefused.Contains(
                         relative,
                         StringComparer.Ordinal
                     )
                 )) {
            details.Add($"**`{relative}` now formats** — the previous release refused it");
        }

        foreach (var relative in candidateRefused.Where(relative => !baselineRefused.Contains(
                         relative,
                         StringComparer.Ordinal
                     )
                 )) {
            details.Add($"⚠ **`{relative}` no longer formats** — the previous release handled it");
        }

        return (DetectorResult.Measured(Name, bump, headline, details), measurement);
    }

    /// <summary>
    /// Every corpus file the measured sets contain, as corpus-relative paths.
    /// </summary>
    /// <remarks>
    /// ⚠ Mirrors <see cref="Corpus.Files"/>'s two exclusions rather than calling it, because the
    /// corpus root is a parameter here — a release compares two trees, and neither is necessarily
    /// the one this assembly was stamped with.
    /// </remarks>
    static IReadOnlyCollection<string> Inputs(string corpusRoot) =>
        Directory.Exists(corpusRoot)
            ? [
                .. Directory.EnumerateFiles(corpusRoot, "*.cs", SearchOption.AllDirectories)
                    .Where(static path => !path.EndsWith(".expected.cs", StringComparison.Ordinal))
                    .Select(path => Path.GetRelativePath(corpusRoot, path).Replace('\\', '/'))
                    .Where(static relative => !relative.StartsWith("pathological/open/", StringComparison.Ordinal))
            ]
            : [];

    /// <summary>
    /// One scratch tree: the corpus at its real relative path, under the repository's own
    /// configuration.
    /// </summary>
    static string Stage(
        IReadOnlyList<string> inputs,
        string corpusRoot,
        string configurationRoot,
        string destination
    ) {
        if (Directory.Exists(destination)) {
            Directory.Delete(destination, recursive: true);
        }

        Directory.CreateDirectory(destination);

        // ⚠ The tree shape is reproduced, not flattened. `.editorconfig` resolution walks parent
        // directories, and a flat scratch directory would resolve a different configuration than
        // the repository does — measuring the staging rather than the tool.
        foreach (var name in new[] { ".editorconfig", "skala.jsonc" }) {
            var source = Path.Combine(configurationRoot, name);
            if (File.Exists(source)) {
                File.Copy(source, Path.Combine(destination, name));
            }
        }

        foreach (var relative in inputs) {
            Restore(relative, corpusRoot, destination);
        }

        return destination;
    }

    static void Restore(string relative, string corpusRoot, string tree) {
        var target = Staged(tree, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        File.Copy(
            Path.Combine(corpusRoot, relative.Replace('/', Path.DirectorySeparatorChar)),
            target,
            overwrite: true
        );
    }

    static string Staged(string tree, string relative) =>
        Path.Combine(tree, "Testing", "corpus", relative.Replace('/', Path.DirectorySeparatorChar));

    /// <summary>
    /// Formats the staged tree in chunks, and returns the files this tool refused.
    /// </summary>
    /// <remarks>
    /// ⚠ A failed chunk is retried one file at a time from a pristine copy rather than abandoned.
    /// The corpus contains the formatter's enemies on purpose, and a release process that stops at
    /// the first of them measures nothing — which is exactly what happened on this detector's first
    /// real run.
    /// </remarks>
    static List<string> Format(SkalaTool tool, string tree, string corpusRoot, IReadOnlyList<string> inputs) {
        var refused = new List<string>();

        foreach (var chunk in inputs.Chunk(ChunkSize)) {
            if (Succeeded(tool.Run(tree, ["format", "--quiet", .. chunk.Select(relative => Staged(tree, relative))]))) {
                continue;
            }

            foreach (var relative in chunk) {
                // The chunk ran in parallel and may have written some of its files before failing.
                Restore(relative, corpusRoot, tree);

                if (!Succeeded(tool.Run(tree, "format", "--quiet", Staged(tree, relative)))) {
                    refused.Add(relative);
                }
            }
        }

        return refused;
    }

    // ⚠ Exit 2 is "changes were made", which is the normal outcome here — the corpus is deliberately
    // misformatted. Anything else is the tool failing on an input.
    static bool Succeeded(ToolRun run) => run.ExitCode is 0 or 2;

    static OutputMeasurement Measure(
        IReadOnlyList<string> comparable,
        string left,
        string right,
        int added,
        IReadOnlyList<string> baselineRefused,
        IReadOnlyList<string> candidateRefused
    ) {
        var report = Fidelity.Compare(
            comparable.Select(relative => (
                    File: relative,
                    Expected: File.ReadAllText(Staged(left, relative)),
                    Actual: File.ReadAllText(Staged(right, relative))
                )
            )
        );

        var classes = report.Divergences
            .GroupBy(static divergence => divergence.Class, StringComparer.Ordinal)
            .OrderByDescending(static group => group.Count())
            .Select(static group => (
                    Class: group.Key,
                    Lines: group.Count(),
                    Files: group.Select(static d => d.File).Distinct(StringComparer.Ordinal).Count(),
                    Example: group.First().File
                    + ":"
                    + group.First().Line.ToString(CultureInfo.InvariantCulture)
                )
            )
            .ToList();

        return new OutputMeasurement(
            report.Files,
            report.Files - report.IdenticalFiles,
            report.Divergences.Count,
            added,
            baselineRefused,
            candidateRefused,
            classes
        );
    }
}
