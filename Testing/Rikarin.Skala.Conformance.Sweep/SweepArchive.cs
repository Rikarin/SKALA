using System.Text.Json;
using System.Text.Json.Serialization;

namespace Rikarin.Skala.Conformance.Sweep;

/// <summary>One option's row, in the shape another pass can read back.</summary>
public sealed record SweepRow(
    string Key,
    string Tier,
    string Outcome,
    int OracleDistinct,
    int SkalaDistinct,
    bool BaselineAgrees);

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
                            option.BaselineAgrees
                        )
                    )
                    .ToArray(),
                Options
            )
            + "\n"
        );

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
