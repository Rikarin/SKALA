using System.Globalization;
using System.Text;
using Rikarin.Skala.Core.Diagnostics;
using Rikarin.Skala.Rules.Metadata;

namespace Rikarin.Skala.Reporting;

/// <summary>The surfaces a <see cref="RunReport"/> can be rendered to.</summary>
public enum ReportFormat {
    /// <summary>Default TTY output, grouped by file.</summary>
    Terminal,

    /// <summary><c>path:line:col: level SKxxxx: message</c> — greppable, and every editor parses it.</summary>
    Plain,

    /// <summary>The SARIF, verbatim.</summary>
    Json,

    /// <summary>GitHub Actions annotations plus a step-summary table.</summary>
    Github,

    /// <summary>The three-bucket agent report (docs/plan/10).</summary>
    Agent
}

/// <summary>
/// Every human- and machine-facing surface, rendered from the one <see cref="RunReport"/>.
/// </summary>
/// <remarks>
/// ⚠ docs/plan/09: <b>no renderer contains analysis logic.</b> A renderer that decides what counts
/// as a failure is a second implementation of the gate, and the two will disagree on the day it
/// matters. Renderers read; the gate decides, once, into <see cref="RunReport.Gate"/>. The only
/// arithmetic here is counting and sorting.
/// </remarks>
public static class Renderer {
    public static string Render(RunReport report, ReportFormat format, bool includeHints = false) =>
        format switch {
            ReportFormat.Plain => Plain(report, includeHints),
            ReportFormat.Json => SarifWriter.Serialize(SarifWriter.Build(report)),
            ReportFormat.Github => Github(report, includeHints),
            ReportFormat.Agent => AgentRenderer.Render(report),
            _ => Terminal(report, includeHints)
        };

    /// <summary>
    /// ⚠ Determinism is enforced after the fact, not during (docs/plan/07 § "Parallelism").
    /// Analyzers run concurrently; the order they finish in may never be observable in output, so
    /// every renderer sorts through here.
    /// </summary>
    public static IEnumerable<Finding> Ordered(RunReport report, bool includeHints) =>
        report.Reportable
            .Where(finding => includeHints || finding.Severity > SkalaSeverity.Hidden)
            .OrderBy(finding => SarifWriter.Relative(report.RepositoryRoot, finding.Path), StringComparer.Ordinal)
            .ThenBy(static finding => finding.Line)
            .ThenBy(static finding => finding.Column)
            .ThenBy(static finding => finding.RuleId, StringComparer.Ordinal)
            .ThenBy(static finding => finding.Message, StringComparer.Ordinal);

    static string Plain(RunReport report, bool includeHints) {
        var builder = new StringBuilder();
        foreach (var finding in Ordered(report, includeHints)) {
            builder.Append(SarifWriter.Relative(report.RepositoryRoot, finding.Path))
                .Append(':')
                .Append(finding.Line.ToString(CultureInfo.InvariantCulture))
                .Append(':')
                .Append(finding.Column.ToString(CultureInfo.InvariantCulture))
                .Append(": ")
                .Append(Word(finding.Severity))
                .Append(' ')
                .Append(finding.RuleId)
                .Append(": ")
                .AppendLine(finding.Message);
        }

        return builder.ToString();
    }

    static string Terminal(RunReport report, bool includeHints) {
        var builder = new StringBuilder();
        builder.Append(Path.GetFileName(report.RepositoryRoot.TrimEnd(Path.DirectorySeparatorChar)))
            .Append("  ·  ")
            .Append(report.FileCount.ToString("N0", CultureInfo.InvariantCulture))
            .Append(" files  ·  ")
            .Append(report.LineCount.ToString("N0", CultureInfo.InvariantCulture))
            .Append(" lines  ·  ")
            .AppendLine(report.LoadSummary);
        builder.AppendLine();

        var findings = Ordered(report, includeHints).ToList();
        foreach (var group in findings.GroupBy(
                     finding => SarifWriter.Relative(report.RepositoryRoot, finding.Path),
                     StringComparer.Ordinal
                 )) {
            builder.Append("  ").AppendLine(group.Key);
            foreach (var finding in group) {
                builder.Append("    ")
                    .Append(finding.HasFix ? "⟳ " : "  ")
                    .Append(
                        (finding.Line.ToString(CultureInfo.InvariantCulture)
                            + ":"
                            + finding.Column.ToString(CultureInfo.InvariantCulture)).PadRight(9)
                    )
                    .Append(Word(finding.Severity).PadRight(11))
                    .Append(finding.RuleId)
                    .Append("  ")
                    .AppendLine(finding.Message);
            }

            builder.AppendLine();
        }

        foreach (var diagnostic in report.Diagnostics.Where(static d => d.Severity >= SkalaSeverity.Info)) {
            builder.Append("  ").AppendLine(diagnostic.ToString());
        }

        if (!report.SkippedRules.IsEmpty) {
            builder.Append("  ")
                .Append(report.SkippedRules.Length.ToString(CultureInfo.InvariantCulture))
                .Append(" rule(s) did not run: ")
                .AppendLine(string.Join(", ", report.SkippedRules.Select(static rule => rule.RuleId)));
            builder.Append("  ").AppendLine(report.SkippedRules[0].Reason);
            builder.AppendLine();
        }

        builder.Append("  ").AppendLine(ReportTotals.Render(report));
        builder.Append("  ")
            .Append(FormatDuration(report.Duration))
            .AppendLine(report.Partial ? "  ·  ⚠ partial run" : string.Empty);

        if (report.Gate is { } gate) {
            builder.Append("  gate `")
                .Append(gate.Name)
                .Append("`: ")
                .AppendLine(gate.Passed ? "PASS" : "FAIL");
            foreach (var failure in gate.Failures) {
                builder.Append("    ").AppendLine(failure);
            }
        }

        return builder.ToString();
    }

    static string Github(RunReport report, bool includeHints) {
        var builder = new StringBuilder();
        foreach (var finding in Ordered(report, includeHints)) {
            builder.Append("::")
                .Append(
                    finding.Severity switch {
                        SkalaSeverity.Error => "error",
                        SkalaSeverity.Warning => "warning",
                        _ => "notice"
                    }
                )
                .Append(" file=")
                .Append(SarifWriter.Relative(report.RepositoryRoot, finding.Path))
                .Append(",line=")
                .Append(finding.Line.ToString(CultureInfo.InvariantCulture))
                .Append(",col=")
                .Append(finding.Column.ToString(CultureInfo.InvariantCulture))
                .Append(",title=")
                .Append(finding.RuleId)
                .Append("::")
                .AppendLine(finding.Message.Replace("\n", "%0A", StringComparison.Ordinal));
        }

        return builder.ToString();
    }

    internal static string Word(SkalaSeverity severity) =>
        severity switch {
            SkalaSeverity.Error => "error",
            SkalaSeverity.Warning => "warning",
            SkalaSeverity.Info => "suggestion",
            _ => "hint"
        };

    internal static string FormatDuration(TimeSpan duration) =>
        duration.TotalSeconds < 1
            ? duration.TotalMilliseconds.ToString("F0", CultureInfo.InvariantCulture) + " ms"
            : duration.TotalSeconds < 90
                ? duration.TotalSeconds.ToString("F1", CultureInfo.InvariantCulture) + " s"
                : ((int)duration.TotalMinutes).ToString(CultureInfo.InvariantCulture)
                + " m "
                + duration.Seconds.ToString(CultureInfo.InvariantCulture)
                + " s";
}

/// <summary>
/// The three-bucket report of docs/plan/10 § "Agent-shaped output".
/// </summary>
/// <remarks>
/// Every line of this is a decision from that document, and the ordering is the load-bearing one:
/// <list type="number">
/// <item>
/// <b>FORMAT first</b>, because it is free and unconditional. An agent reading top-down does the
/// cheap work first and arrives at the hard part with a clean tree.
/// </item>
/// <item><b>FIXABLE second</b>, because the next command is mechanical.</item>
/// <item><b>ACTION last</b>, because it is the only part that needs the model to think.</item>
/// </list>
/// ⚠ The command to run is printed complete, with paths. Not "run skala format" — the exact
/// invocation, which removes a whole class of agent error (guessing flags) at the cost of a longer
/// line.
/// <para>
/// ⚠ Output is bounded. An unbounded lint dump eats the context window the agent needs in order to
/// fix anything, so the cap is real and the elision says exactly what was elided and how to see it.
/// </para>
/// </remarks>
public static class AgentRenderer {
    public const int MaxFindings = 50;
    public const int MaxCharacters = 8000;

    public static string Render(RunReport report) {
        var builder = new StringBuilder();
        var ordered = Renderer.Ordered(report, includeHints: false).ToList();

        var formatting = ordered.Where(static f => f.RuleId == RuleIds.FileIsNotFormatted).ToList();
        var fixable = ordered
            .Where(static f => f.RuleId != RuleIds.FileIsNotFormatted && f.HasFix && f.FixIsSafe)
            .ToList();
        var action = ordered
            .Where(static f => f.RuleId != RuleIds.FileIsNotFormatted && (!f.HasFix || !f.FixIsSafe))
            .ToList();

        if (formatting.Count > 0) {
            var paths = formatting
                .Select(finding => Quote(SarifWriter.Relative(report.RepositoryRoot, finding.Path)))
                .Distinct(StringComparer.Ordinal)
                .ToList();

            builder.Append("FORMAT  ")
                .Append(paths.Count.ToString(CultureInfo.InvariantCulture))
                .Append(paths.Count == 1 ? " file needs" : " files need")
                .Append(" formatting — run: skala format ")
                .AppendLine(string.Join(" ", paths.Take(20)));
            if (paths.Count > 20) {
                builder.Append("        … and ")
                    .Append((paths.Count - 20).ToString(CultureInfo.InvariantCulture))
                    .AppendLine(" more; `skala format .` does all of them.");
            }

            builder.AppendLine();
        }

        var budget = MaxFindings;
        if (fixable.Count > 0) {
            builder.Append("FIXABLE ")
                .Append(fixable.Count.ToString(CultureInfo.InvariantCulture))
                .Append(fixable.Count == 1 ? " finding has" : " findings have")
                .AppendLine(" safe automatic fixes — run: skala fix --safe");
            budget -= Emit(builder, report, fixable, budget, indent: "  ");
            builder.AppendLine();
        }

        if (action.Count > 0) {
            builder.Append("ACTION  ")
                .Append(action.Count.ToString(CultureInfo.InvariantCulture))
                .Append(action.Count == 1 ? " finding needs" : " findings need")
                .AppendLine(" a decision");
            Emit(builder, report, action, budget, indent: "  ");
            builder.AppendLine();
        }

        var suppressed = report.Findings.Count(static f => f.Suppression is SuppressionKind.Pragma
                or SuppressionKind.Attribute
        );
        if (suppressed > 0) {
            // ⚠ docs/plan/10 point 3: given a warning and the ability to edit, `#pragma warning
            // disable` is a valid move for a model optimising for the check passing. Surfacing
            // suppressions unprompted is what makes the dishonest path visible.
            builder.Append(suppressed.ToString(CultureInfo.InvariantCulture))
                .AppendLine(
                    " finding(s) suppressed by #pragma or [SuppressMessage] — see: skala check --show-suppressions"
                );
            builder.AppendLine();
        }

        // ⚠ Said first and unconditionally when there is nothing to do. The SKIPPED block below is
        // context, not work, and an agent that reads a report starting with SKIPPED has to infer
        // that the answer was yes — inference the contract exists to remove.
        if (builder.Length == 0) {
            builder.AppendLine("OK  nothing to do.");
            if (!report.SkippedRules.IsEmpty) {
                builder.AppendLine();
            }
        }

        if (!report.SkippedRules.IsEmpty) {
            builder.Append("SKIPPED ")
                .Append(report.SkippedRules.Length.ToString(CultureInfo.InvariantCulture))
                .Append(" rule(s) did not run (")
                .Append(report.Mode.ToString().ToLowerInvariant())
                .AppendLine(" load): " + string.Join(", ", report.SkippedRules.Select(static r => r.RuleId)));
            builder.AppendLine();
        }

        var text = builder.ToString();
        if (text.Length > MaxCharacters) {
            text = text[..MaxCharacters]
                + "\n… output truncated at "
                + MaxCharacters.ToString(CultureInfo.InvariantCulture)
                + " characters. Run `skala check --format=json` for all of it.\n";
        }

        return text;
    }

    static int Emit(StringBuilder builder, RunReport report, List<Finding> findings, int budget, string indent) {
        var shown = 0;
        foreach (var finding in findings) {
            if (shown >= budget) {
                builder.Append(indent)
                    .Append("… ")
                    .Append((findings.Count - shown).ToString(CultureInfo.InvariantCulture))
                    .AppendLine(" more elided. Run `skala check --format=json` for all of them.");
                break;
            }

            builder.Append(indent)
                .Append(finding.RuleId)
                .Append("  ")
                .Append(SarifWriter.Relative(report.RepositoryRoot, finding.Path))
                .Append(':')
                .Append(finding.Line.ToString(CultureInfo.InvariantCulture))
                .Append("  ")
                .AppendLine(finding.Message);

            // ⚠ "Every finding either carries a fix or carries a one-sentence instruction. Never
            // both, never neither." A finding with no fix gets the rule's summary as an imperative.
            if (!finding.HasFix && RuleCatalog.Find(finding.RuleId) is { } rule) {
                builder.Append(indent).Append("        → ").AppendLine(rule.Summary);
            }

            shown++;
        }

        return shown;
    }

    static string Quote(string path) => path.Contains(' ', StringComparison.Ordinal) ? "\"" + path + "\"" : path;
}
