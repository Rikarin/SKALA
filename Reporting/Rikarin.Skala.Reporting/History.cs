using Rikarin.Skala.Core.Diagnostics;
using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Rikarin.Skala.Reporting;

/// <summary>One line of <c>.skala/history.jsonl</c>.</summary>
/// <remarks>
///     docs/plan/09 § "History". ⚠ Deliberately not a database. One append-only line of JSON per run
///     means the answer to "is this getting better" is a <c>git log</c> away with no infrastructure at
///     all, and the file is diffable, greppable and reviewable — which is the SonarQube dashboard's
///     actual job, minus the server.
/// </remarks>
public sealed record HistoryEntry {
    [JsonPropertyName("timestamp")]
    public DateTimeOffset Timestamp { get; init; }

    [JsonPropertyName("sha")]
    public string Sha { get; init; } = string.Empty;

    [JsonPropertyName("branch")]
    public string Branch { get; init; } = string.Empty;

    [JsonPropertyName("errors")]
    public int Errors { get; init; }

    [JsonPropertyName("warnings")]
    public int Warnings { get; init; }

    [JsonPropertyName("suggestions")]
    public int Suggestions { get; init; }

    [JsonPropertyName("hints")]
    public int Hints { get; init; }

    [JsonPropertyName("new")]
    public int New { get; init; }

    /// <summary>
    ///     ⚠ Two keys, and neither of them is <c>fixable</c>.
    /// </summary>
    /// <remarks>
    ///     The key used to be one <c>fixable</c> counting every finding with a fix, which is not a
    ///     number any renderer can print beside <c>skala fix</c> — that command defaults to
    ///     <c>--safe</c>. Recording the total under the ambiguous name is how a future <c>trend</c>
    ///     column would inherit the same claim, so the ambiguous name is retired rather than redefined:
    ///     a line written before this change has neither key and reads as zero, which is visibly
    ///     missing data rather than a wrong count silently spliced into the series.
    /// </remarks>
    [JsonPropertyName("fixableSafe")]
    public int FixableSafe { get; init; }

    /// <summary>Findings whose fix needs <c>--include</c> and a review. See <see cref="FixableSafe" />.</summary>
    [JsonPropertyName("fixableUnsafe")]
    public int FixableUnsafe { get; init; }

    [JsonPropertyName("files")]
    public int Files { get; init; }

    [JsonPropertyName("lines")]
    public int Lines { get; init; }

    [JsonPropertyName("duplication")]
    public double Duplication { get; init; }

    [JsonPropertyName("cognitiveComplexityP95")]
    public int CognitiveComplexityP95 { get; init; }

    [JsonPropertyName("gate")]
    public string Gate { get; init; } = string.Empty;

    [JsonPropertyName("gatePassed")]
    public bool GatePassed { get; init; }

    [JsonPropertyName("durationSeconds")]
    public double DurationSeconds { get; init; }

    /// <summary>
    ///     ⚠ The configuration fingerprint travels with every entry.
    /// </summary>
    /// <remarks>
    ///     Two runs with different fingerprints are not comparable (doc 09 § "SARIF is the report"), and
    ///     a trend line that silently splices them together is a trend line that shows an improvement
    ///     somebody made by turning a rule off.
    /// </remarks>
    [JsonPropertyName("configurationFingerprint")]
    public string ConfigurationFingerprint { get; init; } = string.Empty;

    public int Total => Errors + Warnings + Suggestions + Hints;
}

/// <summary>
///     <c>.skala/history.jsonl</c> — appended by <c>skala check --record</c>, rendered by
///     <c>skala trend</c>.
/// </summary>
public static class History {
    public const string RelativePath = ".skala/history.jsonl";

    public static string PathFor(string repositoryRoot) => Path.Combine(repositoryRoot, ".skala", "history.jsonl");

    static readonly JsonSerializerOptions Options =
        new() { DefaultIgnoreCondition = JsonIgnoreCondition.Never, WriteIndented = false };

    /// <summary>Builds the entry one run would append.</summary>
    public static HistoryEntry Entry(RunReport report, string sha, string branch) =>
        new() {
            Timestamp = DateTimeOffset.UtcNow,
            Sha = sha,
            Branch = branch,
            Errors = report.Count(SkalaSeverity.Error),
            Warnings = report.Count(SkalaSeverity.Warning),
            Suggestions = report.Count(SkalaSeverity.Info),
            Hints = report.Count(SkalaSeverity.Hidden),
            New = report.HasBaseline || report.ChangedCodeReference is not null ? report.New.Count() : 0,
            FixableSafe = report.SafelyFixable.Count(),
            FixableUnsafe = report.UnsafelyFixable.Count(),
            Files = report.FileCount,
            Lines = report.LineCount,
            Duplication = report.Metrics.Duplication,
            CognitiveComplexityP95 = report.Metrics.CognitiveComplexityP95,
            Gate = report.Gate?.Name ?? string.Empty,
            GatePassed = report.Gate?.Passed ?? true,
            DurationSeconds = Math.Round(report.Duration.TotalSeconds, 2),
            ConfigurationFingerprint = report.ConfigurationFingerprint
        };

    /// <summary>
    ///     Appends one line.
    /// </summary>
    /// <remarks>
    ///     ⚠ Append, never rewrite. The file's whole value is that it is an unedited record; a writer
    ///     that rewrote it could not be trusted to have kept what was there, and a partial run must not
    ///     be able to truncate the history it failed to extend.
    /// </remarks>
    public static void Append(string repositoryRoot, HistoryEntry entry) {
        var path = PathFor(repositoryRoot);
        Core.SkalaDirectory.EnsureForFile(path);
        File.AppendAllText(path, JsonSerializer.Serialize(entry, Options) + "\n");
    }

    /// <summary>
    ///     Reads the history, skipping lines that do not parse.
    /// </summary>
    /// <remarks>
    ///     ⚠ A malformed line is skipped rather than fatal. The file is appended to by concurrent CI
    ///     jobs and hand-edited by people; one torn line must not make <c>skala trend</c> refuse to
    ///     render six months of history.
    /// </remarks>
    public static ImmutableArray<HistoryEntry> Read(string repositoryRoot) {
        var path = PathFor(repositoryRoot);
        if (!File.Exists(path)) {
            return [];
        }

        var entries = ImmutableArray.CreateBuilder<HistoryEntry>();
        foreach (var line in File.ReadLines(path)) {
            if (line.Trim().Length == 0) {
                continue;
            }

            try {
                if (JsonSerializer.Deserialize<HistoryEntry>(line, Options) is { } entry) {
                    entries.Add(entry);
                }
            } catch (JsonException) {
                // See the remarks: one torn line is not a reason to lose the rest.
            }
        }

        return entries.ToImmutable();
    }

    /// <summary>
    ///     <c>skala trend</c> — the history as a table with a sparkline per column.
    /// </summary>
    /// <remarks>
    ///     ⚠ A run whose configuration fingerprint differs from the newest one is marked. Doc 09 says
    ///     two reports with different fingerprints are not comparable, and a trend is nothing but a
    ///     comparison — so the rows that are not comparable have to say so rather than being quietly
    ///     plotted beside the ones that are.
    /// </remarks>
    public static string Render(ImmutableArray<HistoryEntry> entries, int limit) {
        if (entries.IsEmpty) {
            return "no history yet. `skala check --record` appends to " + RelativePath + ".\n";
        }

        var shown = entries.Length <= limit ? entries : [.. entries[^limit..]];
        var fingerprint = shown[^1].ConfigurationFingerprint;

        var builder = new StringBuilder();
        builder.Append("date              sha       findings   new  dup %  cog p95  gate\n");
        builder.Append("────────────────  ────────  ────────  ────  ─────  ───────  ────────\n");

        foreach (var entry in shown) {
            builder.Append(entry.Timestamp.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture).PadRight(18));
            builder.Append((entry.Sha.Length > 8 ? entry.Sha[..8] : entry.Sha).PadRight(10));
            builder.Append(entry.Total.ToString(CultureInfo.InvariantCulture).PadLeft(8));
            builder.Append(entry.New.ToString(CultureInfo.InvariantCulture).PadLeft(6));
            builder.Append(entry.Duplication.ToString("0.0", CultureInfo.InvariantCulture).PadLeft(7));
            builder.Append(entry.CognitiveComplexityP95.ToString(CultureInfo.InvariantCulture).PadLeft(9));
            builder.Append("  ").Append(entry.GatePassed ? "PASS" : "FAIL");

            if (!string.Equals(entry.ConfigurationFingerprint, fingerprint, StringComparison.Ordinal)) {
                builder.Append("  ⚠ other config");
            }

            builder.Append('\n');
        }

        builder.Append('\n');
        builder.Append("findings  ").Append(Spark([.. shown.Select(static e => (double)e.Total)])).Append('\n');
        if (shown.Any(static e => e.Duplication > 0)) {
            builder.Append("dup %     ").Append(Spark([.. shown.Select(static e => e.Duplication)])).Append('\n');
        }

        var first = shown[0].Total;
        var last = shown[^1].Total;
        builder.Append('\n')
            .Append(shown.Length.ToString(CultureInfo.InvariantCulture))
            .Append(" run(s); findings ")
            .Append(first.ToString(CultureInfo.InvariantCulture))
            .Append(" → ")
            .Append(last.ToString(CultureInfo.InvariantCulture))
            .Append(last == first ? " (unchanged)" : last < first ? " (better)" : " (worse)")
            .Append('\n');

        return builder.ToString();
    }

    /// <summary>A sparkline. Eight levels, which is what the block characters give.</summary>
    static string Spark(ImmutableArray<double> values) {
        if (values.IsEmpty) {
            return string.Empty;
        }

        const string blocks = "▁▂▃▄▅▆▇█";
        var min = values.Min();
        var max = values.Max();
        var range = max - min;

        var builder = new StringBuilder(values.Length);
        foreach (var value in values) {
            // ⚠ A flat series renders as the lowest block, not as the highest. All-equal values
            // scaled by an empty range would otherwise show as a full bar and read as "at maximum".
            var level = range <= 0 ? 0 : (int)Math.Round((value - min) / range * (blocks.Length - 1));
            builder.Append(blocks[Math.Clamp(level, 0, blocks.Length - 1)]);
        }

        return builder.ToString();
    }
}
