using System.Globalization;
using System.Text.RegularExpressions;

namespace Rikarin.Skala.Release.Surfaces;

/// <summary>
///     The exit-code contract, as the document publishes it <b>and</b> as the binary produces it.
/// </summary>
/// <remarks>
///     ⚠ Both halves, because either alone has already failed here. <c>ExitCodeContractTests</c> records
///     that the contract was wrong from M1 to M9 and every test agreed with it: the constants matched
///     each other and neither matched the command. So this reads the published table out of
///     <c>docs/plan/09</c> — the thing hooks, CI and agents are written against — and separately
///     <b>runs both binaries</b> over the scenarios a table row can actually be observed on. A row that
///     moves in the document is a change to what people were told; a code that moves in the binary is a
///     change to what happens. Either is <b>major</b>: a hook that auto-formats on 2 and stops on 1 is
///     two lines long and has no way to notice it is now inverted.
///     <para>
///         ⚠ The probes are deliberately the ones that need no compilation. Anything requiring a project
///         load would make the detector's verdict depend on whether the runner had an SDK, which turns a
///         compatibility measurement into an environment measurement.
///     </para>
/// </remarks>
public static partial class ExitCodeSurface {
    public const string Name = "exit codes";

    static readonly string Clean = "class C {\n    void M() {\n        M();\n    }\n}\n";

    static readonly string Dirty = "class  C{ void  M( ){} }\n";

    public static DetectorResult Run(
        SkalaTool? baseline,
        SkalaTool candidate,
        string? baselineRoot,
        string candidateRoot,
        string workRoot
    ) {
        var candidateTable = Table(candidateRoot);
        var candidateProbes = Probe(candidate, Path.Combine(workRoot, "exit-candidate"));

        if (baseline is null || baselineRoot is null) {
            return DetectorResult.Unmeasured(
                Name,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"no previous release — this one publishes {candidateTable.Count} codes and is observed to produce {candidateProbes.Values.Distinct().Count()} of them"
                )
            );
        }

        var baselineTable = Table(baselineRoot);
        var baselineProbes = Probe(baseline, Path.Combine(workRoot, "exit-baseline"));

        var details = new List<string>();

        foreach (var code in baselineTable.Keys.Union(candidateTable.Keys).OrderBy(static code => code)) {
            var before = baselineTable.GetValueOrDefault(code);
            var after = candidateTable.GetValueOrDefault(code);

            if (before is null) {
                details.Add($"published: **{code} added** — {after}");
            } else if (after is null) {
                details.Add($"published: **{code} removed** — it meant \"{before}\"");
            } else if (!string.Equals(before, after, StringComparison.Ordinal)) {
                details.Add($"published: **{code} redefined** — \"{before}\" → \"{after}\"");
            }
        }

        foreach (var (scenario, after) in candidateProbes.OrderBy(static entry => entry.Key, StringComparer.Ordinal)) {
            var before = baselineProbes[scenario];
            if (before != after) {
                details.Add(
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"observed: **`{scenario}` exits {after}, was {before}**"
                    )
                );
            }
        }

        var bump = details.Count > 0 ? BumpKind.Major : BumpKind.Patch;
        var headline = details.Count == 0
            ? string.Create(
                CultureInfo.InvariantCulture,
                $"unchanged — {candidateTable.Count} published codes, {candidateProbes.Count} probes agree with the previous release"
            )
            : $"{details.Count} change(s) to the exit-code contract";

        return DetectorResult.Measured(Name, bump, headline, details);
    }

    /// <summary>
    ///     The table docs/plan/09 § "Exit codes" publishes.
    /// </summary>
    /// <remarks>
    ///     ⚠ The heading is asserted before the rows are read. A renamed section would otherwise yield
    ///     an empty table on both sides, and two empty tables compare equal — the vacuous pass that
    ///     <c>ExitCodeContractTests</c> guards against with the same check.
    /// </remarks>
    public static IReadOnlyDictionary<int, string> Table(string repositoryRoot) {
        var path = Path.Combine(repositoryRoot, "docs", "plan", "09-quality-gates-and-reporting.md");
        var text = File.ReadAllText(path);

        if (!text.Contains("### Exit codes", StringComparison.Ordinal)) {
            throw new InvalidOperationException(
                $"'{path}' has no '### Exit codes' section. The exit-code detector reads that table; "
                + "without it every comparison passes over nothing."
            );
        }

        var section = text[text.IndexOf("### Exit codes", StringComparison.Ordinal)..];
        var table = new Dictionary<int, string>();

        foreach (Match row in Row().Matches(section)) {
            var code = int.Parse(row.Groups["code"].Value, CultureInfo.InvariantCulture);
            table.TryAdd(code, row.Groups["meaning"].Value.Trim());
        }

        if (table.Count == 0) {
            throw new InvalidOperationException($"'{path}' § 'Exit codes' has no rows.");
        }

        return table;
    }

    /// <summary>What the binary actually does, for the rows a scenario can reach.</summary>
    static Dictionary<string, int> Probe(SkalaTool tool, string workRoot) {
        Directory.CreateDirectory(workRoot);
        var clean = Path.Combine(workRoot, "Clean.cs");
        var dirty = Path.Combine(workRoot, "Dirty.cs");
        File.WriteAllText(clean, Clean);
        File.WriteAllText(dirty, Dirty);

        return new Dictionary<string, int>(StringComparer.Ordinal) {
            ["format --check (already formatted)"] = tool.Run(workRoot, "format", "--check", clean).ExitCode,
            ["format --check (needs formatting)"] = tool.Run(workRoot, "format", "--check", dirty).ExitCode,
            ["format --diff (needs formatting)"] = tool.Run(workRoot, "format", "--diff", dirty).ExitCode,
            ["format --check (path does not exist)"] =
                tool.Run(workRoot, "format", "--check", Path.Combine(workRoot, "Absent.cs")).ExitCode,
            ["--help"] = tool.Run(workRoot, "--help").ExitCode
        };
    }

    [GeneratedRegex(@"^\|\s*(?<code>\d+)\s*\|\s*(?<meaning>[^|]+?)\s*\|\s*$", RegexOptions.Multiline)]
    private static partial Regex Row();
}
