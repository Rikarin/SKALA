using Rikarin.Skala.Analysis.Loading;
using Rikarin.Skala.Core.Configuration;
using Rikarin.Skala.Reporting;

namespace Rikarin.Skala.Analysis;

/// <summary>What <c>skala verify</c> was asked to do.</summary>
public sealed record VerifyRequest {
    public IReadOnlyList<string> Paths { get; init; } = [];

    public string? RepositoryRoot { get; init; }

    /// <summary>⚠ <c>loose</c> by default: verify must work with no project, no build and no network.</summary>
    public LoadMode Mode { get; init; } = LoadMode.Loose;

    public string? BinlogPath { get; init; }

    public ReportFormat Format { get; init; } = ReportFormat.Agent;

    /// <summary>Apply the safe fixes and re-verify, rather than only reporting.</summary>
    public bool Fix { get; init; }

    public bool NoCache { get; init; }

    public IReadOnlyList<string> Define { get; init; } = [];
}

/// <summary>
/// <c>skala verify</c> — the one command an agent runs.
/// </summary>
/// <remarks>
/// docs/plan/10 § "`skala verify` — the one command". It is <c>format --check</c> plus
/// <c>check --gate=local</c> in one pass, with output shaped for a model rather than a terminal,
/// and its contract is deliberately narrow so that it can be memorised:
/// <list type="bullet">
/// <item>⚠ <b>Exit 0 means "nothing to do". Nothing else means that.</b></item>
/// <item>Every finding either carries a fix or carries a one-sentence instruction. Never both, never neither.</item>
/// <item>Output is bounded and ordered by actionability, not by file.</item>
/// <item>
/// It works with no project, no build and no network, so an agent that just wrote a file into a
/// scratch directory can run it.
/// </item>
/// </list>
/// </remarks>
public static class VerifyCommand {
    public static CommandResult Run(VerifyRequest request, CancellationToken cancellation = default) {
        if (request.Fix) {
            var fixResult = FixCommand.Run(
                new FixRequest {
                    Paths = request.Paths,
                    RepositoryRoot = request.RepositoryRoot,
                    Mode = request.Mode,
                    SafeOnly = true,
                    Define = request.Define
                },
                cancellation
            );

            // ⚠ Fix, then verify again, then report what is left. A fixing command that reports the
            // findings it just fixed teaches an agent that fixing does not work.
            var after = Verify(request with { Fix = false }, cancellation);
            return new CommandResult(after.ExitCode, fixResult.Output + after.Output);
        }

        return Verify(request, cancellation);
    }

    static CommandResult Verify(VerifyRequest request, CancellationToken cancellation) {
        var (result, report) = CheckCommand.Run(
            new CheckRequest {
                Paths = request.Paths,
                RepositoryRoot = request.RepositoryRoot,
                Mode = request.Mode,
                BinlogPath = request.BinlogPath,
                Gate = "local",
                Format = request.Format,
                IncludeFormatting = true,
                NoCache = request.NoCache,
                Define = request.Define,

                // ⚠ No SARIF file. `verify` runs after every agent turn; writing
                // `.skala/report.sarif` each time would put a churning artefact in the working tree
                // that the agent then has to be told to ignore. `skala check` writes it.
                Output = string.Empty
            },
            cancellation
        );

        if (result.ExitCode == ExitCodes.LoadFailure) {
            return result;
        }

        // ⚠ Exit 0 means nothing to do, which is stricter than the gate passing: a tree with
        // formatting to do and a hundred suggestions passes `local` and is not finished.
        var clean = report.Reportable.All(static finding => finding.Severity == Core.Diagnostics.SkalaSeverity.Hidden);

        return new CommandResult(clean ? ExitCodes.Ok : ExitCodes.GateFailed, result.Output);
    }
}
