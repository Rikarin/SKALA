using System.Globalization;
using Rikarin.Skala.Testing;

namespace Rikarin.Skala.Release.Surfaces;

/// <summary>What the corpus looks like after the previous release's tool, and after this one's.</summary>
public sealed record OutputMeasurement(
    int Files,
    int ChangedFiles,
    int ChangedLines,
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
/// ⚠ <b>Three things make it a measurement instead of a tautology, and all three have failed
/// somewhere in this repository's history:</b>
/// </para>
/// <list type="number">
/// <item>
/// <b>Two binaries, and they are checked for being two.</b> The comparison is between the previous
/// release's tool and the candidate's, launched as processes. If the two paths resolve to bytes
/// with the same SHA-256 this throws, because a detector comparing a build against itself reports
/// "no change" forever and looks exactly like a green one.
/// </item>
/// <item>
/// <b>The inputs and the configuration are held constant, and both come from the candidate.</b>
/// The corpus, the repository's own <c>.editorconfig</c> and <c>skala.jsonc</c> are copied into two
/// scratch trees at their real relative paths, so that config discovery walks the same directories
/// it walks in the repository. The only variable in the experiment is which binary ran.
/// </item>
/// <item>
/// <b>Nothing is formatted in this process.</b> Both sides are read back off disk after an external
/// <c>skala format</c> wrote them. This assembly links the formatter — <c>Fidelity</c> lives beside
/// it — and using it here would silently make the candidate both sides of the comparison.
/// </item>
/// </list>
/// <para>
/// ⚠ <c>pathological/open/</c> is excluded, the same exclusion <see cref="Corpus.Files"/> makes and
/// for the same reason: one of those files makes <c>skala format</c> throw, and a throwing file does
/// not fail one comparison — it takes the whole run down and the release with it.
/// </para>
/// </remarks>
public static class OutputSurface {
    public const string Name = "formatted output";

    public static (DetectorResult Result, OutputMeasurement? Measurement) Run(
        SkalaTool? baseline,
        SkalaTool candidate,
        string corpusRoot,
        string configurationRoot,
        string workRoot
    ) {
        if (baseline is null) {
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

        var inputs = Inputs(corpusRoot);
        if (inputs.Count == 0) {
            throw new InvalidOperationException($"No corpus inputs under '{corpusRoot}'.");
        }

        var left = Stage(inputs, corpusRoot, configurationRoot, Path.Combine(workRoot, "baseline"));
        var right = Stage(inputs, corpusRoot, configurationRoot, Path.Combine(workRoot, "candidate"));

        Format(baseline, left);
        Format(candidate, right);

        var measurement = Measure(inputs, left, right);
        var bump = measurement.ChangedFiles > 0 ? BumpKind.Minor : BumpKind.Patch;

        var headline = measurement.ChangedFiles == 0
            ? string.Create(
                CultureInfo.InvariantCulture,
                $"identical on all {measurement.Files} corpus files"
            )
            : string.Create(
                CultureInfo.InvariantCulture,
                $"formatting output changed on {measurement.ChangedFiles} of {measurement.Files} corpus files ({measurement.ChangedLines} lines)"
            );

        var details = measurement.Classes
            .Select(static entry => string.Create(
                    CultureInfo.InvariantCulture,
                    $"{entry.Lines} lines across {entry.Files} files — {entry.Class} (e.g. {entry.Example})"
                )
            )
            .ToList();

        return (DetectorResult.Measured(Name, bump, headline, details), measurement);
    }

    /// <summary>
    /// Every corpus file the measured sets contain, as repository-relative paths.
    /// </summary>
    /// <remarks>
    /// ⚠ Mirrors <see cref="Corpus.Files"/>'s two exclusions rather than calling it, because the
    /// corpus root is a parameter here — a release measures the *candidate's* corpus, which is not
    /// necessarily the one this assembly was stamped with.
    /// </remarks>
    static IReadOnlyList<string> Inputs(string corpusRoot) => [
        .. Directory.EnumerateFiles(corpusRoot, "*.cs", SearchOption.AllDirectories)
            .Where(static path => !path.EndsWith(".expected.cs", StringComparison.Ordinal))
            .Select(path => Path.GetRelativePath(corpusRoot, path).Replace('\\', '/'))
            .Where(static relative => !relative.StartsWith("pathological/open/", StringComparison.Ordinal))
            .OrderBy(static relative => relative, StringComparer.Ordinal)
    ];

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

        var corpus = Path.Combine(destination, "Testing", "corpus");
        foreach (var relative in inputs) {
            var target = Path.Combine(corpus, relative.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(Path.Combine(corpusRoot, relative.Replace('/', Path.DirectorySeparatorChar)), target);
        }

        return destination;
    }

    static void Format(SkalaTool tool, string tree) {
        var corpus = Path.Combine(tree, "Testing", "corpus");
        var run = tool.Run(tree, "format", "--quiet", corpus);

        // ⚠ Exit 2 is "changes were made", which is the normal outcome here — the corpus is
        // deliberately misformatted. Anything else is the tool failing, and a failed side would
        // otherwise be measured as "the output changed everywhere".
        if (run.ExitCode is not (0 or 2)) {
            throw new InvalidOperationException(
                $"'{tool.Path}' exited {run.ExitCode} formatting the corpus.\n{run.StandardOutput}\n{run.StandardError}"
            );
        }
    }

    static OutputMeasurement Measure(IReadOnlyList<string> inputs, string left, string right) {
        var pairs = inputs.Select(relative => (
                    File: relative,
                    Expected: File.ReadAllText(Path.Combine(left, "Testing", "corpus", relative)),
                    Actual: File.ReadAllText(Path.Combine(right, "Testing", "corpus", relative))
                )
            )
            .ToList();

        var report = Fidelity.Compare(pairs);

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
            classes
        );
    }
}
