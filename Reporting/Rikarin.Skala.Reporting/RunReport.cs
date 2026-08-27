using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using Rikarin.Skala.Core.Diagnostics;

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
/// Everything one <c>skala check</c> produced. The object SARIF is written from and every renderer
/// reads.
/// </summary>
/// <remarks>
/// ⚠ ADR-009: this is the canonical result and nothing downstream may recompute any of it. In
/// particular no renderer decides what counts as a failure — the gate does that, once, and the
/// answer travels here.
/// </remarks>
public sealed record RunReport {
    public required string RepositoryRoot { get; init; }

    public required LoadMode Mode { get; init; }

    public ImmutableArray<Finding> Findings { get; init; } = [];

    /// <summary>Tool diagnostics about the run itself: SK9020, SK9030, SK9031, load failures.</summary>
    public ImmutableArray<SkalaDiagnostic> Diagnostics { get; init; } = [];

    public ImmutableArray<SkippedRule> SkippedRules { get; init; } = [];

    public ImmutableArray<ToolExtension> Extensions { get; init; } = [];

    /// <summary>How many compilations were loaded, and from where.</summary>
    public string LoadSummary { get; init; } = string.Empty;

    public int FileCount { get; init; }

    public int LineCount { get; init; }

    /// <summary>
    /// ⚠ The hash of the effective option set and rule severities. Two reports with different
    /// fingerprints are not comparable, and a report that does not say so invites comparing them.
    /// </summary>
    public string ConfigurationFingerprint { get; init; } = string.Empty;

    /// <summary>⚠ Whether any <c>--option</c> override was active. A clean run with overrides is not a clean run.</summary>
    public bool HasOverrides { get; init; }

    public TimeSpan Duration { get; init; }

    /// <summary>Cancelled, or an analyzer failed. The findings are what was found so far.</summary>
    public bool Partial { get; init; }

    public string ToolVersion { get; init; } = SkalaVersion.Value;

    /// <summary>The gate's verdict, or null when no gate was evaluated.</summary>
    public GateResult? Gate { get; init; }

    public int Count(SkalaSeverity severity) =>
        Findings.Count(finding => finding.Severity == severity && finding.Suppression == SuppressionKind.None);

    public IEnumerable<Finding> Reportable =>
        Findings.Where(static finding => finding.Suppression == SuppressionKind.None);

    public IEnumerable<Finding> Fixable => Reportable.Where(static finding => finding.HasFix);
}

/// <summary>The version stamped into every report.</summary>
public static class SkalaVersion {
    public static string Value { get; } =
        typeof(SkalaVersion).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";
}

/// <summary>
/// A gate's verdict. ⚠ Computed once, by the gate, and carried; never recomputed by a renderer.
/// </summary>
/// <remarks>
/// docs/plan/09 § "Gates" describes the full condition set — baselines, <c>newIssues</c>,
/// <c>metrics.*</c>, <c>ruleOverrides</c>. M5 implements the two that need no baseline
/// infrastructure (<c>maxSeverity</c> and <c>formatting</c>); the rest is M6's, and a gate
/// definition that names them is reported as unsupported rather than silently ignored.
/// </remarks>
public sealed record GateResult(string Name, bool Passed, ImmutableArray<string> Failures) {
    public static GateResult Pass(string name) => new(name, true, []);
}

/// <summary>docs/plan/09 § "Exit codes". Fixed, documented, and depended upon.</summary>
public static class ExitCodes {
    /// <summary>The gate passed. Findings may exist below it.</summary>
    public const int Ok = 0;

    /// <summary>The gate failed.</summary>
    public const int GateFailed = 1;

    /// <summary>⚠ Formatting changes are needed. Distinct from 1 so that a hook can auto-format on 2 and stop on 1.</summary>
    public const int FormattingNeeded = 2;

    /// <summary>A configuration error under <c>--strict-config</c>.</summary>
    public const int ConfigurationError = 3;

    /// <summary>No compilation could be built.</summary>
    public const int LoadFailure = 4;

    /// <summary>Internal error, including SK9099.</summary>
    public const int InternalError = 5;

    public const int Cancelled = 130;
}

/// <summary>The summary line every renderer ends with, computed once.</summary>
public static class ReportTotals {
    public static string Render(RunReport report) {
        var builder = new StringBuilder();
        var findings = report.Reportable.Count();
        var fixable = report.Fixable.Count();
        builder.Append(findings.ToString(CultureInfo.InvariantCulture))
            .Append(findings == 1 ? " finding" : " findings");

        if (fixable > 0) {
            builder.Append("  ·  ")
                .Append(fixable.ToString(CultureInfo.InvariantCulture))
                .Append(" fixable (`skala fix`)");
        }

        var suppressed = report.Findings.Count(static f => f.Suppression != SuppressionKind.None);
        if (suppressed > 0) {
            builder.Append("  ·  ")
                .Append(suppressed.ToString(CultureInfo.InvariantCulture))
                .Append(" suppressed");
        }

        return builder.ToString();
    }
}
