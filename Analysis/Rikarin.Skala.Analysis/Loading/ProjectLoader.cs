using Rikarin.Skala.Core.Diagnostics;
using Rikarin.Skala.Reporting;
using System.Collections.Immutable;

namespace Rikarin.Skala.Analysis.Loading;

/// <summary>
///     The three load modes of docs/plan/07, and the order they fall through in.
/// </summary>
/// <remarks>
///     ⚠ Falling through is deliberate and is reported. "I asked for binlog and there is no binlog" is
///     the common case on a developer machine and on the agent path, and failing hard there means the
///     caller gets nothing rather than the syntactic half of the answer. What may never happen is
///     falling through <em>silently</em>: the mode that actually ran is in the SARIF's
///     <c>tool.driver.properties.loadMode</c>, in the terminal header, and in the agent report's
///     SKIPPED block, so a loose result can never be mistaken for a binlog one.
/// </remarks>
public static class ProjectLoader {
    /// <summary>
    ///     Choose workspace when discovery found one target or found an ambiguity that the caller must
    ///     resolve; choose loose only when there is genuinely no workspace target.
    /// </summary>
    public static LoadMode ResolveAutoMode(LoadRequest request) =>
        WorkspaceLoader.Resolve(request).ShouldAttemptWorkspace ? LoadMode.Workspace : LoadMode.Loose;

    public static LoadedProject Load(LoadRequest request, CancellationToken cancellation = default) {
        var attempted = ImmutableArray.CreateBuilder<SkalaDiagnostic>();

        foreach (var mode in Ladder(request)) {
            var loaded = mode switch {
                LoadMode.Binlog => BinlogLoader.Load(request, cancellation),
                LoadMode.Workspace => WorkspaceLoader.Load(request, cancellation),
                _ => LooseLoader.Load(request)
            };

            attempted.AddRange(loaded.Diagnostics);

            // ⚠ **The one thing the ladder may not fall through, and it is tested before `IsEmpty` on
            // purpose.** Everything in the remarks above is about a mode that ran and found nothing,
            // which is a fact about the repository. `Failed` is a fact about the *tool* — MSBuild is
            // not there, an assembly is missing, the named solution would not open, every project in
            // it failed to evaluate — and no lower rung can answer the question the caller asked.
            //
            // ⚠ The ordering is load-bearing rather than tidy. A failed `MSBuildWorkspace` load hands
            // back a placeholder project with no documents, so `IsEmpty` is **false** for a load that
            // produced not one line of code. Asking `IsEmpty` first returns that placeholder as a
            // success, and the run reports a clean tree over a project MSBuild refused to evaluate.
            if (loaded.Failed) {
                // ⚠ Fatal only for the mode the caller named — which is the ladder's first rung.
                // Workspace is also the *middle* rung of the binlog ladder, and the default
                // `skala check` on a machine with no MSBuild must still reach loose rather than
                // refusing to run: there the caller asked for binlog, and workspace being unavailable
                // is not an answer to anything. Asking for a mode by name and getting the syntactic
                // rules instead is the fail-open; being handed them after asking for binlog is the
                // documented fallback.
                if (mode == request.Mode || !request.AllowFallback) {
                    return loaded with { Diagnostics = attempted.ToImmutable() };
                }

                attempted.Add(
                    new SkalaDiagnostic(
                        ConfigDiagnosticIds.LoadModeFellBack,
                        SkalaSeverity.Info,
                        $"--load={mode.ToString().ToLowerInvariant()} could not run; falling back",
                        request.RepositoryRoot
                    )
                );

                continue;
            }

            if (!loaded.IsEmpty) {
                return loaded with { Diagnostics = attempted.ToImmutable() };
            }

            if (!request.AllowFallback) {
                return loaded with { Diagnostics = attempted.ToImmutable() };
            }

            if (mode != LoadMode.Loose) {
                attempted.Add(
                    new SkalaDiagnostic(
                        ConfigDiagnosticIds.LoadModeFellBack,
                        SkalaSeverity.Info,
                        $"--load={mode.ToString().ToLowerInvariant()} produced no compilation; falling back",
                        request.RepositoryRoot
                    )
                );
            }
        }

        return new() {
            Mode = LoadMode.Loose, Diagnostics = attempted.ToImmutable(), Summary = "nothing could be loaded"
        };
    }

    /// <summary>
    ///     ⚠ <c>--load=loose</c> never falls <em>up</em>. Asking for loose is asking for speed and for
    ///     the semantics-free rule set; quietly running a build's worth of work instead would blow the
    ///     budget the mode exists to meet.
    /// </summary>
    static IEnumerable<LoadMode> Ladder(LoadRequest request) {
        switch (request.Mode) {
            case LoadMode.Binlog:
                yield return LoadMode.Binlog;
                yield return LoadMode.Workspace;
                yield return LoadMode.Loose;
                break;

            case LoadMode.Workspace:
                yield return LoadMode.Workspace;
                yield return LoadMode.Loose;
                break;

            default:
                yield return LoadMode.Loose;
                break;
        }
    }
}
