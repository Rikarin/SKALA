using System.Text.Json;
using System.Text.Json.Serialization;

namespace Rikarin.Skala.Conformance.Sweep;

/// <summary>One option's row, in the shape another pass can read back.</summary>
/// <remarks>
///     ⚠ <see cref="Fixture" /> and <see cref="Values" /> are nullable because a sweep committed before
///     they existed has neither, and a record that pretends otherwise would hand a reader an empty list
///     that looks like a measurement. <c>ProvenanceTests</c> treats a row without them as "this run
///     recorded nothing about Skala's side" and says so, rather than passing.
/// </remarks>
public sealed record SweepRow(
    string Key,
    string Tier,
    string Outcome,
    int OracleDistinct,
    int SkalaDistinct,
    bool BaselineAgrees,
    string? Fixture = null,
    IReadOnlyList<SweepValue>? Values = null);

/// <summary>
///     The sweep's result in machine-readable form, beside the table people read.
/// </summary>
/// <remarks>
///     ⚠ It exists for one job: the defaults pass needs to know which options the <em>export-base</em>
///     run watched the oracle distinguish, and that is the fact that separates "this fixture is too weak"
///     from "ReSharper's own defaults mask this option". The two passes run under different base
///     configurations and cannot share a process cheaply — one is the export, the other is bare
///     <c>root = true</c> — so the first writes what the second needs.
///     <para>
///         ⚠ Committed alongside the markdown, and for the same reason: the sweep is a nightly job, so what
///         the fast path has is the last run's answer, and a regression is visible as a diff.
///     </para>
///     <para>
///         ⚠
///         <b>
///             And for a second job since <c>603fbd3</c>: it carries Skala's own answer at every
///             configuration the run measured.
///         </b> The oracle half cannot be re-asked without JetBrains and
///         minutes of wall clock, but Skala's half is 485 ms, so recording it turns "has the formatter
///         moved since this table was measured?" into a question the fast path can answer on every commit.
///         Without it the table's verdicts were checkable only against a formatter nobody had compared
///         them to — the tier tests read this file and the 88 commits after the previous run were invisible
///         to every instrument in the repository.
///     </para>
/// </remarks>
public static class SweepArchive {
    static readonly JsonSerializerOptions Options =
        new() { WriteIndented = true, DefaultIgnoreCondition = JsonIgnoreCondition.Never };

    public static void Write(string path, SweepRun run) =>
        File.WriteAllText(
            path,
            JsonSerializer.Serialize(
                run.Options
                    .OrderBy(static option => option.Key, StringComparer.Ordinal)
                    .Select(static option => new SweepRow(
                            option.Key,
                            option.Tier.ToString(),
                            option.Outcome.ToString(),
                            option.OracleDistinct,
                            option.SkalaDistinct,
                            option.BaselineAgrees,
                            option.Fixture,
                            option.Values
                        )
                    )
                    .ToArray(),
                Options
            )
            + "\n"
        );

    /// <summary>
    ///     Whether the single sweep found the two engines agreeing at each (option, value) it measured.
    /// </summary>
    /// <remarks>
    ///     ⚠ <b>What this is for.</b> The pairwise pass has to tell an interaction from a disagreement one
    ///     of the two keys already owns on its own. Without it the first corrected pairwise run reported
    ///     <b>17 interactions</b> across the <c>wrap_*</c> family, every one of them disagreeing only at
    ///     <c>max_line_length = 0</c> and <c>= 1</c> — and this archive says <c>max_line_length</c> agrees
    ///     at <c>120</c> and disagrees at both of those, measured alone, on its own fixture. Seventeen
    ///     findings, one cause, and none of it about a pair.
    ///     <para>
    ///         ⚠ A key with no entry returns <see langword="null" /> rather than <see langword="false" />:
    ///         "never measured" and "measured and disagreed" are opposite states, and collapsing them would
    ///         let an unmeasured key excuse every corner it appears in.
    ///     </para>
    /// </remarks>
    public static Dictionary<(string Key, string Value), bool>? ReadAgreement(string path) {
        if (!File.Exists(path)) {
            return null;
        }

        var rows = JsonSerializer.Deserialize<SweepRow[]>(File.ReadAllText(path), Options);
        if (rows is null) {
            return null;
        }

        var agreement = new Dictionary<(string Key, string Value), bool>();
        foreach (var row in rows) {
            foreach (var value in row.Values ?? []) {
                agreement[(row.Key, value.Value)] = value.Agree;
            }
        }

        return agreement;
    }

    /// <summary>
    ///     The options a previous sweep watched the oracle distinguish, or <see langword="null" /> when
    ///     there is no previous sweep to ask.
    /// </summary>
    /// <remarks>
    ///     ⚠ Null rather than empty. An empty set and a missing file mean opposite things — "no option
    ///     is observable" versus "nobody has measured" — and collapsing them would let the defaults pass
    ///     report every <c>Insensitive</c> verdict as an unmasked weak fixture on the strength of a file
    ///     that was never written.
    /// </remarks>
    public static HashSet<string>? ReadObservable(string path) {
        if (!File.Exists(path)) {
            return null;
        }

        var rows = JsonSerializer.Deserialize<SweepRow[]>(File.ReadAllText(path), Options);
        return rows is null
            ? null
            : [
                .. rows.Where(static row => row.OracleDistinct > 1).Select(static row => row.Key)
            ];
    }
}
