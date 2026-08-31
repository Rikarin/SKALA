using Rikarin.Skala.Core.Diagnostics;
using System.Globalization;
using System.Text;
using System.Xml;

namespace Rikarin.Skala.Reporting;

/// <summary>
///     The GitHub Actions surfaces: inline annotations and the step-summary table.
/// </summary>
/// <remarks>
///     docs/plan/09 § "SARIF is the report". ⚠ The two are different jobs and the split matters: an
///     annotation appears on the diff line and is what a reviewer sees without leaving the PR; the step
///     summary is the run-level report and is what someone reads when the annotations are too many to
///     scroll. Emitting one and calling it CI support leaves the other job undone.
///     <para>
///         ⚠ Annotations go to stdout because that is the only channel the Actions runner parses. The
///         summary goes to the file <c>$GITHUB_STEP_SUMMARY</c> names, because writing markdown to stdout
///         puts it in the log where nobody looks.
///     </para>
/// </remarks>
public static class GithubRenderer {
    /// <summary>The environment variable the Actions runner sets to the summary file's path.</summary>
    public const string StepSummaryVariable = "GITHUB_STEP_SUMMARY";

    /// <summary>
    ///     Appends the run's summary table to <c>$GITHUB_STEP_SUMMARY</c>, when running under Actions.
    /// </summary>
    /// <returns>Whether anything was written.</returns>
    /// <remarks>
    ///     ⚠ Silently does nothing off CI, rather than failing. The same command line has to work on a
    ///     laptop, and a check that refused to run because an environment variable was absent would be
    ///     a check nobody puts in their `ci` gate.
    /// </remarks>
    public static bool WriteStepSummary(RunReport report) {
        var path = Environment.GetEnvironmentVariable(StepSummaryVariable);
        if (string.IsNullOrEmpty(path)) {
            return false;
        }

        try {
            File.AppendAllText(path, StepSummary(report));
            return true;
        } catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) {
            // A report that could not be written is not a reason to fail a build that otherwise
            // passed; the annotations are already on stdout.
            return false;
        }
    }

    /// <summary>The markdown the step summary carries.</summary>
    public static string StepSummary(RunReport report) {
        var builder = new StringBuilder();
        builder.Append("## Skala — gate `")
            .Append(report.Gate?.Name ?? "none")
            .Append("`: ")
            .Append(report.Gate is null ? "not evaluated" : report.Gate.Passed ? "✅ PASS" : "❌ FAIL")
            .Append("\n\n");

        if (report.Gate is { Passed: false } gate) {
            foreach (var failure in gate.Failures) {
                builder.Append("- ⚠ ").Append(failure).Append('\n');
            }

            builder.Append('\n');
        }

        builder.Append("| | |\n|---|---|\n");
        Row(builder, "Findings", report.Reportable.Count().ToString(CultureInfo.InvariantCulture));
        Row(builder, "Errors", report.Count(SkalaSeverity.Error).ToString(CultureInfo.InvariantCulture));
        Row(builder, "Warnings", report.Count(SkalaSeverity.Warning).ToString(CultureInfo.InvariantCulture));
        Row(builder, "Fixable", report.Fixable.Count().ToString(CultureInfo.InvariantCulture) + " (`skala fix`)");

        if (report.HasBaseline || report.ChangedCodeReference is not null) {
            Row(builder, "New", report.New.Count().ToString(CultureInfo.InvariantCulture));
        }

        if (!report.Fixed.IsEmpty) {
            Row(builder, "Fixed since the baseline", report.Fixed.Length.ToString(CultureInfo.InvariantCulture));
        }

        if (report.Metrics.HasDuplication) {
            Row(
                builder,
                "Duplication",
                report.Metrics.Duplication.ToString("0.0", CultureInfo.InvariantCulture) + " %"
            );
        }

        if (report.Metrics.MemberCount > 0) {
            Row(
                builder,
                "Cognitive complexity p95",
                report.Metrics.CognitiveComplexityP95.ToString(CultureInfo.InvariantCulture)
            );
        }

        Row(builder, "Files", report.FileCount.ToString("N0", CultureInfo.InvariantCulture));
        Row(builder, "Duration", Renderer.FormatDuration(report.Duration));
        Row(builder, "Configuration", "`" + report.ConfigurationFingerprint + "`");

        builder.Append('\n').Append(MarkdownRenderer.Table(report, 20));
        return builder.ToString();
    }

    static void Row(StringBuilder builder, string name, string value) =>
        builder.Append("| ").Append(name).Append(" | ").Append(value).Append(" |\n");
}

/// <summary>
///     <c>skala report --format=markdown</c> — the PR comment.
/// </summary>
/// <remarks>
///     ⚠ Bounded, for the same reason the agent renderer is: a PR comment with nine hundred rows is a
///     PR comment nobody reads and GitHub truncates anyway. The elision says what was elided and where
///     the rest is.
/// </remarks>
public static class MarkdownRenderer {
    public const int DefaultLimit = 50;

    public static string Render(RunReport report, bool includeHints) {
        var builder = new StringBuilder();
        builder.Append("## Skala\n\n");
        builder.Append(ReportTotals.Render(report).Replace("  ·  ", " · ", StringComparison.Ordinal)).Append("\n\n");

        var metrics = report.Metrics.Render(report.GateThresholds.IsEmpty ? null : report.GateThresholds);
        if (metrics.Length > 0) {
            builder.Append(metrics.Replace("  ·  ", " · ", StringComparison.Ordinal)).Append("\n\n");
        }

        builder.Append(Table(report, DefaultLimit, includeHints));

        if (report.Gate is { } gate) {
            builder.Append("\n**gate `")
                .Append(gate.Name)
                .Append("`: ")
                .Append(gate.Passed ? "PASS" : "FAIL")
                .Append("** in ")
                .Append(Renderer.FormatDuration(report.Duration))
                .Append('\n');

            foreach (var failure in gate.Failures) {
                builder.Append("\n- ").Append(failure);
            }

            builder.Append('\n');
        }

        return builder.ToString();
    }

    /// <summary>The findings table, shared by the markdown report and the GitHub step summary.</summary>
    public static string Table(RunReport report, int limit, bool includeHints = false) {
        var findings = Renderer.Ordered(report, includeHints).ToList();
        if (findings.Count == 0) {
            return "No findings.\n";
        }

        var builder = new StringBuilder();
        builder.Append("| | File | Rule | Message |\n|---|---|---|---|\n");

        foreach (var finding in findings.Take(limit)) {
            builder.Append("| ")
                .Append(finding.HasFix ? "⟳" : string.Empty)
                .Append(" | `")
                .Append(SarifWriter.Relative(report.RepositoryRoot, finding.Path))
                .Append(':')
                .Append(finding.Line.ToString(CultureInfo.InvariantCulture))
                .Append("` | ")
                .Append(finding.RuleId)
                .Append(" | ")
                .Append(Escape(finding.Message))
                .Append(" |\n");
        }

        if (findings.Count > limit) {
            builder.Append("\n_… and ")
                .Append((findings.Count - limit).ToString(CultureInfo.InvariantCulture))
                .Append(" more; the full list is in the SARIF artifact._\n");
        }

        return builder.ToString();
    }

    /// <summary>
    ///     ⚠ A pipe in a message ends the table cell. Rule messages contain code, and code contains pipes.
    /// </summary>
    static string Escape(string text) =>
        text.Replace("|", "\\|", StringComparison.Ordinal).Replace("\n", " ", StringComparison.Ordinal);
}

/// <summary>
///     JUnit XML, for a CI system whose only report surface is a test result.
/// </summary>
/// <remarks>
///     ⚠ One test case per finding, grouped into a suite per rule. The alternative — one case per file
///     — makes a file with twelve findings look like one failure, and CI systems dedupe on the case
///     name, so eleven of them would vanish.
/// </remarks>
public static class JUnitRenderer {
    public static string Render(RunReport report, bool includeHints) {
        var findings = Renderer.Ordered(report, includeHints).ToList();

        // ⚠ `NewLineChars` is not decoration. It defaults to `Environment.NewLine`, so an indenting
        // `XmlWriter` emits CRLF on Windows and LF everywhere else — the same defect `Lines` in
        // Renderers.cs was written to kill, arriving through the one renderer that builds no
        // `StringBuilder` and so was missed by the sweep that fixed the other six.
        var settings = new XmlWriterSettings { Indent = true, OmitXmlDeclaration = false, NewLineChars = "\n" };

        using var stream = new StringWriter(CultureInfo.InvariantCulture);
        using (var writer = XmlWriter.Create(stream, settings)) {
            writer.WriteStartElement("testsuites");
            writer.WriteAttributeString("name", "Skala");
            writer.WriteAttributeString("tests", findings.Count.ToString(CultureInfo.InvariantCulture));
            writer.WriteAttributeString("failures", findings.Count.ToString(CultureInfo.InvariantCulture));
            writer.WriteAttributeString(
                "time",
                report.Duration.TotalSeconds.ToString("0.###", CultureInfo.InvariantCulture)
            );

            foreach (var group in findings.GroupBy(static finding => finding.RuleId, StringComparer.Ordinal)
                         .OrderBy(static group => group.Key, StringComparer.Ordinal)) {
                writer.WriteStartElement("testsuite");
                writer.WriteAttributeString("name", group.Key);
                writer.WriteAttributeString("tests", group.Count().ToString(CultureInfo.InvariantCulture));
                writer.WriteAttributeString("failures", group.Count().ToString(CultureInfo.InvariantCulture));

                foreach (var finding in group) {
                    var where = SarifWriter.Relative(report.RepositoryRoot, finding.Path)
                        + ":"
                        + finding.Line.ToString(CultureInfo.InvariantCulture);

                    writer.WriteStartElement("testcase");
                    writer.WriteAttributeString("classname", group.Key);
                    writer.WriteAttributeString("name", where);
                    writer.WriteStartElement("failure");
                    writer.WriteAttributeString("type", Renderer.Word(finding.Severity));
                    writer.WriteAttributeString("message", finding.Message);
                    writer.WriteString(where + ": " + finding.RuleId + ": " + finding.Message);
                    writer.WriteEndElement();
                    writer.WriteEndElement();
                }

                writer.WriteEndElement();
            }

            writer.WriteEndElement();
        }

        return stream.ToString();
    }
}
