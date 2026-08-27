using Rikarin.Skala.Core.Configuration;
using Rikarin.Skala.Reporting;

namespace Rikarin.Skala.Analysis;

/// <summary>
/// <c>skala report</c> — re-render a stored SARIF, running nothing.
/// </summary>
/// <remarks>
/// docs/plan/09 § "The human report": "re-renders a stored SARIF without re-running anything, which
/// is what CI uses to produce a PR comment from an artifact".
/// <para>
/// ⚠ The separation is the feature. The job that analyses uploads one artifact; the job that
/// comments — which may run on a different runner, with different permissions, after the analysing
/// job has finished — reads it. A comment step that re-analysed would analyse a different tree
/// (the merge commit, or main having moved) and would report findings the gate never saw.
/// </para>
/// </remarks>
public static class ReportCommand {
    public static CommandResult Run(
        string sarifPath,
        string repositoryRoot,
        ReportFormat format,
        bool includeHints,
        bool summary
    ) {
        if (!File.Exists(sarifPath)) {
            return new CommandResult(
                ExitCodes.ConfigurationError,
                "skala report: " + sarifPath + " does not exist. `skala check` writes .skala/report.sarif.\n"
            );
        }

        RunReport report;
        try {
            report = SarifReader.Read(sarifPath, repositoryRoot);
        } catch (Exception exception) when (exception is IOException or InvalidDataException) {
            return new CommandResult(
                ExitCodes.ConfigurationError,
                "skala report: " + sarifPath + " could not be read: " + exception.Message + "\n"
            );
        }

        if (format == ReportFormat.Github) {
            GithubRenderer.WriteStepSummary(report);
        }

        var output = summary ? Renderer.Summary(report) : Renderer.Render(report, format, includeHints);

        // ⚠ The stored gate verdict decides the exit code, not a fresh evaluation. `report` is a
        // renderer over a run that already happened; re-deciding here would be a second
        // implementation of the gate, which doc 09 forbids in as many words.
        var exit = report.Gate is { Passed: false } ? ExitCodes.GateFailed : ExitCodes.Ok;
        return new CommandResult(exit, output);
    }
}

/// <summary>
/// <c>skala trend</c> — <c>.skala/history.jsonl</c>, rendered.
/// </summary>
/// <remarks>
/// docs/plan/09 § "History". ⚠ "The answer to 'is this getting better' is a <c>git log</c> away,
/// which is the SonarQube dashboard's actual job, minus the server."
/// </remarks>
public static class TrendCommand {
    public const int DefaultLimit = 20;

    public static CommandResult Run(string repositoryRoot, int limit) =>
        new(ExitCodes.Ok, History.Render(History.Read(repositoryRoot), limit));
}
