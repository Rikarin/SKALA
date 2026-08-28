using System.Globalization;
using Rikarin.Skala.Core.Configuration;
using Rikarin.Skala.Core.Diagnostics;
using Rikarin.Skala.Reporting;

namespace Rikarin.Skala.Analysis;

/// <summary>
///     <c>skala report</c> — re-render a stored SARIF, running nothing.
/// </summary>
/// <remarks>
///     docs/plan/09 § "The human report": "re-renders a stored SARIF without re-running anything, which
///     is what CI uses to produce a PR comment from an artifact".
///     <para>
///         ⚠ The separation is the feature. The job that analyses uploads one artifact; the job that
///         comments — which may run on a different runner, with different permissions, after the analysing
///         job has finished — reads it. A comment step that re-analysed would analyse a different tree
///         (the merge commit, or main having moved) and would report findings the gate never saw.
///     </para>
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
        if (report.Gate is not { Passed: false } failed) {
            return new CommandResult(ExitCodes.Ok, output);
        }

        // ⚠ **On stderr, because the render goes to stdout and stdout is very often a file.**
        // `.github/workflows/skala.yml` runs this as `report … --format=markdown > skala-report.md`,
        // so on a failing gate the whole explanation — the rendered report, the verdict, the reasons
        // — went into the file and the CI log got one line: `Process completed with exit code 1`. A
        // step that fails in total silence is indistinguishable from a crash, and this one is not
        // even a fault: it is the *stored* verdict of a gate that a previous step already failed on
        // and already reported. Saying which is the difference between a second red X somebody has
        // to investigate and one they can read.
        Console.Error.WriteLine(
            "skala report: exiting "
            + ExitCodes.GateFailed.ToString(CultureInfo.InvariantCulture)
            + " because the stored gate `"
            + failed.Name
            + "` failed in the run that wrote "
            + sarifPath
            + ". Nothing was re-analysed and this step found nothing new; the run that did the "
            + "analysis is where the failure belongs. Its reasons, verbatim:"
        );

        foreach (var reason in failed.Failures) {
            Console.Error.WriteLine("  " + reason);
        }

        return new CommandResult(ExitCodes.GateFailed, output);
    }
}

/// <summary>
///     <c>skala trend</c> — <c>.skala/history.jsonl</c>, rendered.
/// </summary>
/// <remarks>
///     docs/plan/09 § "History". ⚠ "The answer to 'is this getting better' is a <c>git log</c> away,
///     which is the SonarQube dashboard's actual job, minus the server."
/// </remarks>
public static class TrendCommand {
    public const int DefaultLimit = 20;

    public static CommandResult Run(string repositoryRoot, int limit) =>
        new(ExitCodes.Ok, History.Render(History.Read(repositoryRoot), limit));
}
