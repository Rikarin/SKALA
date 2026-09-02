using Rikarin.Skala.Core.Diagnostics;
using System.Collections.Immutable;
using System.Globalization;
using System.Text;

namespace Rikarin.Skala.Reporting;

/// <summary>Which of docs/plan/07's three load modes produced the compilations.</summary>
public enum LoadMode {
    /// <summary>⚠ No project at all. Semantic rules do not run and the report says which.</summary>
    Loose,

    /// <summary>A real build's compiler command lines (ADR-007, the default).</summary>
    Binlog,

    /// <summary>MSBuildLocator + MSBuildWorkspace, with WorkspaceDiagnostics surfaced.</summary>
    Workspace
}

/// <summary>A rule that could have fired and did not run, with the reason.</summary>
public sealed record SkippedRule(string RuleId, string Reason);

/// <summary>A hosted third-party analyzer package that took part in the run (ADR-008).</summary>
public sealed record ToolExtension(string Name, string Version, int RuleCount);

/// <summary>
///     Everything one <c>skala check</c> produced. The object SARIF is written from and every renderer
///     reads.
/// </summary>
/// <remarks>
///     ⚠ ADR-009: this is the canonical result and nothing downstream may recompute any of it. In
///     particular no renderer decides what counts as a failure — the gate does that, once, and the
///     answer travels here.
/// </remarks>
public sealed record RunReport {
    public required string RepositoryRoot { get; init; }

    public required LoadMode Mode { get; init; }

    public ImmutableArray<Finding> Findings { get; init; } = [];

    /// <summary>Tool diagnostics about the run itself: SK9020, SK9030, SK9031, load failures.</summary>
    public ImmutableArray<SkalaDiagnostic> Diagnostics { get; init; } = [];

    public ImmutableArray<SkippedRule> SkippedRules { get; init; } = [];

    /// <summary>
    ///     <c>--verbose</c>: say what did not run, one line per rule, rather than one reason for all.
    /// </summary>
    /// <remarks>
    ///     ⚠ A rendering flag, and it is on the report only because the renderers take nothing else.
    ///     It must never change what is in <see cref="Findings" /> or what the gate decided (ADR-009).
    /// </remarks>
    public bool Verbose { get; init; }

    public ImmutableArray<ToolExtension> Extensions { get; init; } = [];

    /// <summary>How many compilations were loaded, and from where.</summary>
    public string LoadSummary { get; init; } = string.Empty;

    public int FileCount { get; init; }

    public int LineCount { get; init; }

    /// <summary>
    ///     ⚠ The hash of the effective option set and rule severities. Two reports with different
    ///     fingerprints are not comparable, and a report that does not say so invites comparing them.
    /// </summary>
    public string ConfigurationFingerprint { get; init; } = string.Empty;

    /// <summary>
    ///     ⚠ Whether any <c>--option</c> override was active. A clean run with overrides is not a clean run.
    /// </summary>
    public bool HasOverrides { get; init; }

    public TimeSpan Duration { get; init; }

    /// <summary>Cancelled, or an analyzer failed. The findings are what was found so far.</summary>
    public bool Partial { get; init; }

    public string ToolVersion { get; init; } = SkalaVersion.Value;

    /// <summary>The gate's verdict, or null when no gate was evaluated.</summary>
    public GateResult? Gate { get; init; }

    /// <summary>The aggregate metrics, and what <c>metrics.*</c> in a gate reads.</summary>
    public MetricsSummary Metrics { get; init; } = MetricsSummary.Empty;

    /// <summary>
    ///     The <c>metrics.*</c> thresholds the evaluated gate carried, so a renderer can print
    ///     "duplication 1.8 % (gate 3.0 %)" without re-reading <c>skala.jsonc</c>.
    /// </summary>
    public ImmutableDictionary<string, double> GateThresholds { get; init; } =
        ImmutableDictionary<string, double>.Empty;

    /// <summary>⚠ Whether a baseline took part. Distinct from "the baseline was empty".</summary>
    public bool HasBaseline { get; init; }

    /// <summary>Where the baseline came from, for the failure message.</summary>
    public string BaselineSummary { get; init; } = string.Empty;

    /// <summary>
    ///     Baseline entries that no longer fire.
    /// </summary>
    /// <remarks>
    ///     ⚠ Carried rather than acted on. docs/plan/09: pruning must be explicit, because "a baseline
    ///     that self-prunes lets a rule that silently stopped working look like progress".
    /// </remarks>
    public ImmutableArray<BaselineEntry> Fixed { get; init; } = [];

    /// <summary>The git ref <c>--since</c> named, or null when the run was not scoped.</summary>
    public string? ChangedCodeReference { get; init; }

    /// <summary>What <c>--no-new-suppressions</c> found, or <see cref="SuppressionAudit.Off" />.</summary>
    public SuppressionAudit Suppressions { get; init; } = SuppressionAudit.Off;

    /// <summary>The clone groups duplication detection found, for the report.</summary>
    public int CloneGroupCount => Metrics.CloneGroupCount;

    /// <summary>
    ///     The findings that count as new, under whichever scopings are in play.
    /// </summary>
    /// <remarks>
    ///     ⚠ The intersection of the scopings, not the union — see <see cref="Gate" />. With a baseline
    ///     and <c>--since</c> both active, a finding is new only if it is absent from the baseline
    ///     <em>and</em> on a line the branch touched.
    /// </remarks>
    public IEnumerable<Finding> New => Reportable.Where(IsNew);

    /// <summary>
    ///     The same test <see cref="New" /> applies, for one finding — so a renderer can scope without
    ///     re-deriving the rule.
    /// </summary>
    /// <remarks>
    ///     ⚠ True for everything when no scoping is in play, which is what keeps the unscoped report
    ///     identical to what it has always been.
    /// </remarks>
    public bool IsNew(Finding finding) =>
        (!HasBaseline || finding.Bucket == BaselineBucket.New)
        && (ChangedCodeReference is null || finding.IsInChangedCode);

    public int Count(SkalaSeverity severity) =>
        Findings.Count(finding => finding.Severity == severity && finding.Suppression == SuppressionKind.None);

    public IEnumerable<Finding> Reportable =>
        Findings.Where(static finding => finding.Suppression == SuppressionKind.None);

    /// <summary>
    ///     The findings <c>skala fix</c>, invoked with no arguments, would actually change.
    /// </summary>
    /// <remarks>
    ///     ⚠ <b>There is deliberately no <c>Fixable</c> property.</b> There was, it counted every
    ///     finding carrying a fix, and every renderer printed that total beside the words
    ///     <c>skala fix</c> — which defaults to <c>--safe</c> and calls that "the default and the only
    ///     unqualified mode". On this repository the line read <c>fixable 297 (skala fix)</c> while
    ///     <c>skala fix --safe --dry-run</c> reported nothing to apply: <b>0 of those 297 were safe</b>.
    ///     A property that cannot be printed truthfully next to a command name is not worth keeping, so
    ///     the two sets are named separately and a caller has to say which one it means.
    /// </remarks>
    public IEnumerable<Finding> SafelyFixable =>
        Reportable.Where(static finding => finding is { HasFix: true, FixIsSafe: true });

    /// <summary>
    ///     The findings carrying a fix <c>skala fix</c> applies only when <c>--include</c> names its rule.
    /// </summary>
    /// <remarks>
    ///     ⚠ Not a smaller batch of the same work. Every one of the seven unsafe rules was applied
    ///     individually to this repository: 3 landed, 3 were reverted as defective — one of them
    ///     emitting code that does not compile (#328, #329, #330) — and 1 had nothing to offer. 5 edits
    ///     of 316 survived review, so this count is a reading list, not a queue.
    /// </remarks>
    public IEnumerable<Finding> UnsafelyFixable =>
        Reportable.Where(static finding => finding is { HasFix: true, FixIsSafe: false });
}

/// <summary>The version stamped into every report.</summary>
public static class SkalaVersion {
    public static string Value { get; } =
        typeof(SkalaVersion).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";
}

/// <summary>
///     A gate's verdict. ⚠ Computed once, by the gate, and carried; never recomputed by a renderer.
/// </summary>
/// <remarks>
///     docs/plan/09 § "Gates" describes the full condition set — baselines, <c>newIssues</c>,
///     <c>metrics.*</c>, <c>ruleOverrides</c>. M5 implements the two that need no baseline
///     infrastructure (<c>maxSeverity</c> and <c>formatting</c>); the rest is M6's, and a gate
///     definition that names them is reported as unsupported rather than silently ignored.
/// </remarks>
public sealed record GateResult(string Name, bool Passed, ImmutableArray<string> Failures) {
    public static GateResult Pass(string name) => new(name, true, []);
}

/// <summary>The summary line every renderer ends with, computed once.</summary>
public static class ReportTotals {
    public static string Render(RunReport report) {
        var builder = new StringBuilder();
        var findings = report.Reportable.Count();
        var safe = report.SafelyFixable.Count();
        var unsafeFixes = report.UnsafelyFixable.Count();
        builder.Append(findings.ToString(CultureInfo.InvariantCulture))
            .Append(findings == 1 ? " finding" : " findings");

        // ⚠ The safe count is printed even at zero, whenever anything carries a fix at all. "0 safe
        // fixes (`skala fix`)" beside a large unsafe count is the whole point: it is the line that
        // stops a reader running the bare command and watching nothing happen. What used to stand
        // here printed one total called "fixable" against `skala fix`, which defaults to `--safe` —
        // so the number named exactly the fixes that command declines.
        if (safe > 0 || unsafeFixes > 0) {
            builder.Append("  ·  ")
                .Append(safe.ToString(CultureInfo.InvariantCulture))
                .Append(safe == 1 ? " safe fix (`skala fix`)" : " safe fixes (`skala fix`)");
        }

        if (unsafeFixes > 0) {
            builder.Append("  ·  ")
                .Append(unsafeFixes.ToString(CultureInfo.InvariantCulture))
                .Append(unsafeFixes == 1 ? " unsafe fix" : " unsafe fixes")
                .Append(" (`skala fix --include …`, review each)");
        }

        var suppressed = report.Findings.Count(static f => f.Suppression != SuppressionKind.None);
        if (suppressed > 0) {
            builder.Append("  ·  ")
                .Append(suppressed.ToString(CultureInfo.InvariantCulture))
                .Append(" suppressed");
        }

        // ⚠ The new count only appears when something defines "new". Printing "0 new" on an
        // unscoped run reads as "nothing was added", which is a claim the run cannot make.
        if (report.HasBaseline || report.ChangedCodeReference is not null) {
            builder.Append("  ·  ")
                .Append(report.New.Count().ToString(CultureInfo.InvariantCulture))
                .Append(" new");

            if (report.ChangedCodeReference is { } reference) {
                builder.Append(" since ").Append(reference);
            }
        }

        if (!report.Fixed.IsEmpty) {
            builder.Append("  ·  ")
                .Append(report.Fixed.Length.ToString(CultureInfo.InvariantCulture))
                .Append(" fixed (`skala baseline prune`)");
        }

        return builder.ToString();
    }
}
