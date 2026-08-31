using Rikarin.Skala.Analysis.Loading;
using Rikarin.Skala.Core.Configuration;
using Rikarin.Skala.Core.Diagnostics;
using Rikarin.Skala.Formatting.CSharp;
using Rikarin.Skala.Reporting;

namespace Rikarin.Skala.Analysis;

/// <summary>What <c>skala verify</c> was asked to do.</summary>
public sealed record VerifyRequest {
    public IReadOnlyList<string> Paths { get; init; } = [];

    public string? RepositoryRoot { get; init; }

    /// <summary>
    ///     Null means auto: use one unambiguous workspace target when present, otherwise loose.
    /// </summary>
    public LoadMode? Mode { get; init; }

    public string? BinlogPath { get; init; }

    public string? ProjectPath { get; init; }

    public ReportFormat Format { get; init; } = ReportFormat.Agent;

    /// <summary>Apply the safe fixes and re-verify, rather than only reporting.</summary>
    public bool Fix { get; init; }

    public bool NoCache { get; init; }

    public IReadOnlyList<string> Define { get; init; } = [];

    /// <summary>
    ///     A git reference; only findings on lines this branch touched are "to do".
    /// </summary>
    /// <remarks>
    ///     ⚠ Same shape as <see cref="CheckRequest.Since" />, and it exists here for the reason doc 10
    ///     gives for <c>verify</c> existing at all: an agent has to be able to tell what it is
    ///     responsible for. Without it, an agent that changed five lines is handed the repository's
    ///     whole history of findings and has no way to tell which five are its own.
    /// </remarks>
    public string? Since { get; init; }

    /// <summary>
    ///     <c>null</c> for no baseline, <c>""</c> for <c>.skala/baseline.sarif</c> if it exists, or a
    ///     path. Same tri-state as <see cref="CheckRequest.BaselinePath" />.
    /// </summary>
    public string? BaselinePath { get; init; }
}

/// <summary>
///     <c>skala verify</c> — the one command an agent runs.
/// </summary>
/// <remarks>
///     docs/plan/10 § "`skala verify` — the one command". It is <c>format --check</c> plus
///     <c>check --gate=local</c> in one pass, with output shaped for a model rather than a terminal,
///     and its contract is deliberately narrow so that it can be memorised:
///     <list type="bullet">
///         <item>⚠ <b>Exit 0 means "nothing to do". Nothing else means that.</b></item>
///         <item>
///             Every finding either carries a fix or carries a one-sentence instruction. Never both, never neither.
///         </item>
///         <item>Output is bounded and ordered by actionability, not by file.</item>
///         <item>
///             It works with no project, no build and no network, so an agent that just wrote a file into a
///             scratch directory can run it.
///         </item>
///     </list>
/// </remarks>
public static class VerifyCommand {
    public static CommandResult Run(VerifyRequest request, CancellationToken cancellation = default) {
        request = request with { Mode = ResolveMode(request) };
        if (request.Fix) {
            var fixResult = FixCommand.Run(
                new FixRequest {
                    Paths = request.Paths,
                    RepositoryRoot = request.RepositoryRoot,
                    Mode = request.Mode,
                    ProjectPath = request.ProjectPath,
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
                Mode = request.Mode ?? LoadMode.Loose,
                BinlogPath = request.BinlogPath,
                ProjectPath = request.ProjectPath,
                AllowLoadFallback = request.Mode != LoadMode.Workspace,
                Gate = "local",
                Format = request.Format,
                IncludeFormatting = true,
                NoCache = request.NoCache,
                Define = request.Define,

                // ⚠ <b>The one command an agent runs was the one command that could not be told
                // what had already been accepted.</b> On the first repository to adopt Skala,
                // `verify` reported 778 findings needing a decision — every run, for ever, because
                // it had neither of the two scopings `check` has had since M6. Doc 10's
                // three-bucket report is the right shape and it was reading an unscoped world.
                Since = request.Since,
                BaselinePath = request.BaselinePath,

                // ⚠ No SARIF file. `verify` runs after every agent turn; writing
                // `.skala/report.sarif` each time would put a churning artefact in the working tree
                // that the agent then has to be told to ignore. `skala check` writes it.
                Output = string.Empty,

                // ⚠ No metrics and no duplication either, for the same reason and a harder budget:
                // docs/plan/15 § M5 holds `verify` to well under a second on a five-file change, and
                // both are whole-repository passes whose answer nobody reads between agent turns.
                IncludeMetrics = false,
                IncludeDuplication = false
            },
            cancellation
        );

        if (result.ExitCode == ExitCodes.LoadFailure) {
            return result;
        }

        // ⚠ Exit 0 means nothing to do, which is stricter than the gate passing: a tree with
        // formatting to do and a hundred suggestions passes `local` and is not finished.
        //
        // ⚠ `New`, not `Reportable`, and the distinction is the whole point of `--baseline` and
        // `--since`. With neither in play `New` *is* `Reportable`, so the unscoped contract is
        // unchanged; with either, "nothing to do" means nothing the agent is responsible for.
        // Reading `Reportable` here would have accepted the options and then ignored them.
        var clean = report.New.All(static finding => finding.Severity == Core.Diagnostics.SkalaSeverity.Hidden);

        return new CommandResult(clean ? ExitCodes.Ok : ExitCodes.GateFailed, result.Output);
    }

    static LoadMode ResolveMode(VerifyRequest request) {
        if (request.Mode is { } explicitMode) {
            return explicitMode;
        }

        var root = Path.GetFullPath(
            request.RepositoryRoot
            ?? FormatCommand.FindRepositoryRoot(request.Paths.Count > 0 ? request.Paths[0] : ".")
            ?? Directory.GetCurrentDirectory()
        );
        var target = WorkspaceLoader.Resolve(
            new LoadRequest {
                RepositoryRoot = root,
                Mode = LoadMode.Workspace,
                ProjectPath = request.ProjectPath,
                Paths = request.Paths
            }
        );

        return target.ShouldAttemptWorkspace ? LoadMode.Workspace : LoadMode.Loose;
    }
}
