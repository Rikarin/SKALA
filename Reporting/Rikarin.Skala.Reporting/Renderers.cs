using System.Globalization;
using System.Text;
using Rikarin.Skala.Core.Diagnostics;
using Rikarin.Skala.Rules.Metadata;

namespace Rikarin.Skala.Reporting;

/// <summary>The surfaces a <see cref="RunReport" /> can be rendered to.</summary>
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
    Agent,

    /// <summary><c>skala report --format=markdown</c> — a PR comment.</summary>
    Markdown,

    /// <summary>JUnit XML, for a CI system that only knows how to render tests.</summary>
    JUnit
}

/// <summary>
///     Every human- and machine-facing surface, rendered from the one <see cref="RunReport" />.
/// </summary>
/// <remarks>
///     ⚠ docs/plan/09: <b>no renderer contains analysis logic.</b> A renderer that decides what counts
///     as a failure is a second implementation of the gate, and the two will disagree on the day it
///     matters. Renderers read; the gate decides, once, into <see cref="RunReport.Gate" />. The only
///     arithmetic here is counting and sorting.
/// </remarks>
public static class Renderer {
    public static string Render(RunReport report, ReportFormat format, bool includeHints = false) =>
        format switch {
            ReportFormat.Plain => Plain(report, includeHints),
            ReportFormat.Json => SarifWriter.Serialize(SarifWriter.Build(report)),
            ReportFormat.Github => Github(report, includeHints),
            ReportFormat.Agent => AgentRenderer.Render(report),
            ReportFormat.Markdown => MarkdownRenderer.Render(report, includeHints),
            ReportFormat.JUnit => JUnitRenderer.Render(report, includeHints),
            _ => Terminal(report, includeHints)
        };

    /// <summary>
    ///     <c>--summary</c>: doc 09's last three lines and nothing else.
    /// </summary>
    /// <remarks>
    ///     ⚠ Rendered from the same report by the same code as the tail of <see cref="Terminal" />, not
    ///     re-derived. Two implementations of "the summary" is two chances for the summary to disagree
    ///     with the report it summarises.
    /// </remarks>
    public static string Summary(RunReport report) {
        var builder = new StringBuilder();
        Tail(builder, report);
        return builder.ToString();
    }

    /// <summary>The totals, the metrics and the gate — the three lines doc 09's example ends with.</summary>
    static void Tail(StringBuilder builder, RunReport report) {
        builder.Append("  ").Line(ReportTotals.Render(report));

        var thresholds = report.Gate is null ? null : GateThresholds(report);
        var metrics = report.Metrics.Render(thresholds);
        if (metrics.Length > 0) {
            builder.Append("  ").Line(metrics);
        }

        if (report.Gate is { } gate) {
            builder.Append("  gate `")
                .Append(gate.Name)
                .Append("`: ")
                .Append(gate.Passed ? "PASS" : "FAIL")
                .Append(" in ")
                .Append(FormatDuration(report.Duration))
                .Line(report.Partial ? "  ·  ⚠ partial run" : string.Empty);

            foreach (var failure in gate.Failures) {
                builder.Append("    ").Line(failure);
            }

            return;
        }

        builder.Append("  ")
            .Append(FormatDuration(report.Duration))
            .Line(report.Partial ? "  ·  ⚠ partial run" : string.Empty);
    }

    /// <summary>
    ///     ⚠ The thresholds are read back off the report's own gate result rather than re-read from
    ///     <c>skala.jsonc</c>. A renderer that re-read the configuration could render a gate line that
    ///     disagrees with the verdict beside it.
    /// </summary>
    static System.Collections.Immutable.ImmutableDictionary<string, double>? GateThresholds(RunReport report) =>
        report.GateThresholds.IsEmpty ? null : report.GateThresholds;

    /// <summary>
    ///     ⚠ Determinism is enforced after the fact, not during (docs/plan/07 § "Parallelism").
    ///     Analyzers run concurrently; the order they finish in may never be observable in output, so
    ///     every renderer sorts through here.
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
                .Line(finding.Message);
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
            .Line(report.LoadSummary);
        builder.Line();

        var findings = Ordered(report, includeHints).ToList();
        foreach (var group in findings.GroupBy(
                     finding => SarifWriter.Relative(report.RepositoryRoot, finding.Path),
                     StringComparer.Ordinal
                 )) {
            builder.Append("  ").Line(group.Key);
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
                    .Line(finding.Message);
            }

            builder.Line();
        }

        foreach (var diagnostic in report.Diagnostics.Where(static d => d.Severity >= SkalaSeverity.Info)) {
            builder.Append("  ").Line(diagnostic.ToString());
        }

        if (!report.SkippedRules.IsEmpty) {
            builder.Append("  ")
                .Append(report.SkippedRules.Length.ToString(CultureInfo.InvariantCulture))
                .Append(" rule(s) did not run: ")
                .Line(string.Join(", ", report.SkippedRules.Select(static rule => rule.RuleId)));

            // ⚠ Without --verbose this prints the first rule's reason and lets it stand for all of
            // them, which is right when they share one — "no compilation" skips every semantic rule
            // for the same reason — and wrong the moment they do not.
            if (report.Verbose) {
                foreach (var rule in report.SkippedRules) {
                    builder.Append("    ").Append(rule.RuleId).Append("  ").Line(rule.Reason);
                }
            } else {
                builder.Append("  ").Line(report.SkippedRules[0].Reason);
            }

            builder.Line();
        }

        Tail(builder, report);
        return builder.ToString();
    }

    /// <summary>
    ///     ⚠ The annotations, and then the verdict — because a log that ends at the annotations does not
    ///     say what happened.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         This renderer used to emit findings and nothing else. The gate's verdict and its reasons go
    ///         to <c>$GITHUB_STEP_SUMMARY</c>, which is a different page, so the *log* of a failing
    ///         `Check` step was two hundred annotations followed by
    ///         <c>
    /// Process completed with exit code
    ///          1
    ///         </c> and no statement of why. Read from the log alone, this repository's own master gate
    ///         looked like twenty-four errors in one rule family; it was in fact failing four conditions,
    ///         of which those errors were one, and the largest was that the baseline the `ci` gate names
    ///         did not exist.
    ///     </para>
    ///     <para>
    ///         ⚠ That last one is the reason the notifications are emitted too, and not only the gate
    ///         failures. <c>SK9030</c> says in as many words: "the gate names a baseline at
    ///         .skala/baseline.sarif and there is no such file, so every finding counts as new." The tool
    ///         had diagnosed itself correctly and put the answer somewhere the log could not reach.
    ///     </para>
    ///     <para>
    ///         ⚠ It is not a second gate (doc 09 forbids that, and this file's own remarks repeat it). It
    ///         reads <see cref="RunReport.Gate" /> — the verdict the gate already reached — and prints it.
    ///         Nothing here decides anything.
    ///     </para>
    /// </remarks>
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
                .Line(finding.Message.Replace("\n", "%0A", StringComparison.Ordinal));
        }

        // The run's own diagnostics about the run: a missing baseline, a binlog that covers too
        // little, a rule that could not be loaded. They are what explains the numbers above.
        foreach (var diagnostic in report.Diagnostics) {
            builder.Append(diagnostic.Severity >= SkalaSeverity.Error ? "::error::" : "::notice::")
                .Append(diagnostic.Id)
                .Append(": ")
                .Line(diagnostic.Message.Replace("\n", "%0A", StringComparison.Ordinal));
        }

        if (report.Gate is { } gate) {
            builder.Append(gate.Passed ? "::notice::" : "::error::")
                .Append("gate `")
                .Append(gate.Name)
                .Append("`: ")
                .Line(gate.Passed ? "PASS" : "FAIL");

            foreach (var failure in gate.Failures) {
                builder.Append("::error::  ").Line(failure.Replace("\n", "%0A", StringComparison.Ordinal));
            }
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
///     The three-bucket report of docs/plan/10 § "Agent-shaped output".
/// </summary>
/// <remarks>
///     Every line of this is a decision from that document, and the ordering is the load-bearing one:
///     <list type="number">
///         <item>
///             <b>FORMAT first</b>, because it is free and unconditional. An agent reading top-down does the
///             cheap work first and arrives at the hard part with a clean tree.
///         </item>
///         <item><b>FIXABLE second</b>, because the next command is mechanical.</item>
///         <item><b>ACTION last</b>, because it is the only part that needs the model to think.</item>
///     </list>
///     ⚠ The command to run is printed complete, with paths. Not "run skala format" — the exact
///     invocation, which removes a whole class of agent error (guessing flags) at the cost of a longer
///     line.
///     <para>
///         ⚠ Output is bounded. An unbounded lint dump eats the context window the agent needs in order to
///         fix anything, so the cap is real and the elision says exactly what was elided and how to see it.
///     </para>
/// </remarks>
public static class AgentRenderer {
    public const int MaxFindings = 50;
    public const int MaxCharacters = 8000;

    public static string Render(RunReport report) {
        var builder = new StringBuilder();

        // ⚠ <b>Scoped, because this report is a queue and a queue nobody can drain is noise.</b>
        // Every one of the three buckets below is phrased as work to do — "needs a decision", "run
        // skala fix", "run skala format" — so a finding the repository has already accepted, or one
        // on a line this branch never touched, does not belong in any of them. On the first
        // repository to adopt Skala this was the difference between 778 findings needing a decision
        // on every run for ever and 3.
        //
        // ⚠ With neither `--baseline` nor `--since` in play `IsNew` is true for everything, so the
        // unscoped output is byte-for-byte what it was.
        var ordered = Renderer.Ordered(report, includeHints: false).Where(report.IsNew).ToList();

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
                .Line(string.Join(" ", paths.Take(20)));
            if (paths.Count > 20) {
                builder.Append("        … and ")
                    .Append((paths.Count - 20).ToString(CultureInfo.InvariantCulture))
                    .Line(" more; `skala format .` does all of them.");
            }

            builder.Line();
        }

        var budget = MaxFindings;
        if (fixable.Count > 0) {
            builder.Append("FIXABLE ")
                .Append(fixable.Count.ToString(CultureInfo.InvariantCulture))
                .Append(fixable.Count == 1 ? " finding has" : " findings have")
                .Line(" safe automatic fixes — run: skala fix --safe");
            budget -= Emit(builder, report, fixable, budget, indent: "  ");
            builder.Line();
        }

        if (action.Count > 0) {
            builder.Append("ACTION  ")
                .Append(action.Count.ToString(CultureInfo.InvariantCulture))
                .Append(action.Count == 1 ? " finding needs" : " findings need")
                .Line(" a decision");
            Emit(builder, report, action, budget, indent: "  ");
            builder.Line();
        }

        var suppressed = report.Findings.Count(static f => f.Suppression is SuppressionKind.Pragma
                or SuppressionKind.Attribute
        );
        if (suppressed > 0) {
            // ⚠ docs/plan/10 point 3: given a warning and the ability to edit, `#pragma warning
            // disable` is a valid move for a model optimising for the check passing. Surfacing
            // suppressions unprompted is what makes the dishonest path visible.
            builder.Append(suppressed.ToString(CultureInfo.InvariantCulture))
                .Line(" finding(s) suppressed by #pragma or [SuppressMessage] — see: skala check --show-suppressions");
            builder.Line();
        }

        // ⚠ Said first and unconditionally when there is nothing to do. The SKIPPED block below is
        // context, not work, and an agent that reads a report starting with SKIPPED has to infer
        // that the answer was yes — inference the contract exists to remove.
        if (builder.Length == 0) {
            builder.Line("OK  nothing to do.");
            if (!report.SkippedRules.IsEmpty) {
                builder.Line();
            }
        }

        if (!report.SkippedRules.IsEmpty) {
            builder.Append("SKIPPED ")
                .Append(report.SkippedRules.Length.ToString(CultureInfo.InvariantCulture))
                .Append(" rule(s) did not run (")
                .Append(report.Mode.ToString().ToLowerInvariant())
                .Line(" load): " + string.Join(", ", report.SkippedRules.Select(static r => r.RuleId)));
            builder.Line();
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
                    .Line(" more elided. Run `skala check --format=json` for all of them.");
                break;
            }

            builder.Append(indent)
                .Append(finding.RuleId)
                .Append("  ")
                .Append(SarifWriter.Relative(report.RepositoryRoot, finding.Path))
                .Append(':')
                .Append(finding.Line.ToString(CultureInfo.InvariantCulture))
                .Append("  ")
                .Line(finding.Message);

            // ⚠ "Every finding either carries a fix or carries a one-sentence instruction. Never
            // both, never neither." A finding with no fix gets the rule's summary as an imperative.
            if (!finding.HasFix && RuleCatalog.Find(finding.RuleId) is { } rule) {
                builder.Append(indent).Append("        → ").Line(rule.Summary);
            }

            shown++;
        }

        return shown;
    }

    static string Quote(string path) => path.Contains(' ', StringComparison.Ordinal) ? "\"" + path + "\"" : path;
}

/// <summary>
///     <c>StringBuilder.AppendLine</c>, with the line ending fixed at <c>\n</c>.
/// </summary>
/// <remarks>
///     ⚠ <c>AppendLine</c> appends <see cref="Environment.NewLine" />, which is CRLF on Windows, so
///     every renderer in this file emitted CRLF there and LF everywhere else. Only one assertion in
///     the tree compared a whole rendered string against a literal —
///     <c>ReportingTests.AgentRenderer_SaysNothingToDoWhenThereIsNothingToDo</c>, expecting
///     <c>"OK  nothing to do.\n"</c> — so one test failed on Windows and the other seven surfaces
///     changed shape unobserved.
///     <para>
///         ⚠ It is not a cosmetic difference, because these are not all human surfaces. <c>plain</c> is
///         "greppable, and the format every editor's error parser already understands" (doc 09) and
///         <c>agent</c> is doc 10's machine report. An output contract that varies by the platform the
///         tool happens to run on is not a contract. <see cref="GithubRenderer" /> and
///         <see cref="MarkdownRenderer" />, one file over, already append <c>'\n'</c> by hand for exactly
///         this reason, and <c>DocsSite</c> makes the same argument at length; this file was the one that
///         had not been told.
///     </para>
/// </remarks>
static class Lines {
    internal static StringBuilder Line(this StringBuilder builder) => builder.Append('\n');

    internal static StringBuilder Line(this StringBuilder builder, string text) => builder.Append(text).Append('\n');
}
