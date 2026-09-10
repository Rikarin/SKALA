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
///     <c>arrange --check</c> plus <c>check --gate=local</c> in one pass, with output shaped for a model,
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
            return new(after.ExitCode, fixResult.Output + after.Output);
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
                IncludeArrangement = true,
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

        return Verdict(request.Format, result, report);
    }

    /// <summary>
    ///     <c>verify</c>'s verdict over <c>check</c>'s result — the whole of what this command adds.
    /// </summary>
    /// <remarks>
    ///     ⚠ Split out so it can be driven with a report built by hand. #345's failure is only
    ///     reachable end-to-end through an arrangement rule that is itself a bug, which makes an
    ///     end-to-end fixture for it a hostage to whoever fixes that rule; this is the same code on the
    ///     same path, and it stays forceable after every such bug is gone.
    /// </remarks>
    internal static CommandResult Verdict(ReportFormat format, CommandResult result, RunReport report) {
        // ⚠ A load failure has no verdict to be partial about: no compilation was built, so the report
        // is empty and every finding that would have existed is absent rather than clean. `CheckCommand`
        // has already written the reason and the load diagnostics into the output.
        if (result.ExitCode is ExitCodes.LoadFailure) {
            return result;
        }

        // ⚠ Exit 0 means nothing to do, which is stricter than the gate passing: a tree with
        // formatting to do and a hundred suggestions passes `local` and is not finished.
        //
        // ⚠ `New`, not `Reportable`, and the distinction is the whole point of `--baseline` and
        // `--since`. With neither in play `New` *is* `Reportable`, so the unscoped contract is
        // unchanged; with either, "nothing to do" means nothing the agent is responsible for.
        // Reading `Reportable` here would have accepted the options and then ignored them.
        var clean = report.New.All(static finding => finding.Severity == SkalaSeverity.Hidden);

        // ⚠ #345 asked whether one file's `SK9098` should take the whole run down, and the answer
        // taken here is: <b>it takes the exit code down and nothing else</b>.
        //
        // The exit code stays `InternalError`. Downgrading it to `GateFailed` would make a Skala bug
        // indistinguishable from a repository's own lint debt — the one distinction an exit code has
        // to preserve — and downgrading it to `Ok` would break the contract the whole command exists
        // for. What was actually wrong is that the exit code was the *only* thing that said so: the
        // renderers dropped the diagnostics, so `agent` printed `OK  nothing to do.` and `plain`
        // printed nothing at all on a run that exited 5. `Renderer.Blocking` fixes that for every
        // format, and the verdict below says what the rest of the tree looked like — because the
        // measured cost in #345 was not the exit code, it was that 752 files were arranged and
        // checked and nobody could tell.
        if (result.ExitCode is ExitCodes.InternalError) {
            return new(result.ExitCode, result.Output + PartialVerdict(format, report, clean));
        }

        return new(clean ? ExitCodes.Ok : ExitCodes.GateFailed, result.Output);
    }

    /// <summary>
    ///     The one line that says what the run <em>did</em> cover, appended after a partial run.
    /// </summary>
    /// <remarks>
    ///     ⚠ Text formats only. `json` and `junit` are parsed, and a prose line appended to either is
    ///     a corrupt document rather than a clearer one; both already carry the same facts
    ///     structurally — the notification and its location, and `executionSuccessful: false`.
    ///     <para>
    ///         ⚠ #355: "Exit 5 is that Skala bug" was unconditional here too, so a `verify` over one
    ///         unreadable file misattributed it twice — once in the INCOMPLETE banner and once in this
    ///         trailer — and the second copy would have survived a fix to the first. The attribution
    ///         is read off <see cref="Renderer.Causes" />, the same place the banner reads it, and the
    ///         word "bug" is kept for a run in which Skala's own fault is actually among the causes.
    ///     </para>
    /// </remarks>
    static string PartialVerdict(ReportFormat format, RunReport report, bool clean) {
        if (format is ReportFormat.Json or ReportFormat.JUnit) {
            return string.Empty;
        }

        var blocked = Renderer.BlockedFiles(report).Count();
        var checkedFiles = Math.Max(0, report.FileCount - blocked);
        var skalasFault = Renderer.Causes(report).Any(static entry => entry.Cause == IncompleteCause.Defect);
        return "PARTIAL  "
            + Count(checkedFiles)
            + (checkedFiles == 1 ? " file was checked and " : " files were checked and ")
            + (clean ? "had no work outstanding" : "carry the work listed above")
            + "; "
            + Count(blocked)
            + " could not be checked. Exit "
            + Count(ExitCodes.InternalError)
            + (skalasFault ? " is that Skala bug, not a gate failure.\n" : " reports that, not a gate failure.\n");
    }

    static string Count(int value) => value.ToString(System.Globalization.CultureInfo.InvariantCulture);

    static LoadMode ResolveMode(VerifyRequest request) {
        if (request.Mode is { } explicitMode) {
            return explicitMode;
        }

        var root = Path.GetFullPath(
            request.RepositoryRoot
            ?? FormatCommand.FindRepositoryRoot(request.Paths.Count > 0 ? request.Paths[0] : ".")
            ?? Directory.GetCurrentDirectory()
        );
        return ProjectLoader.ResolveAutoMode(
            new LoadRequest {
                RepositoryRoot = root,
                Mode = LoadMode.Workspace,
                ProjectPath = request.ProjectPath,
                Paths = request.Paths
            }
        );
    }
}
