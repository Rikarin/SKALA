using System.Collections.Immutable;
using Rikarin.Skala.Core.Diagnostics;
using Rikarin.Skala.Reporting;

namespace Rikarin.Skala.Analysis.Loading;

/// <summary>
/// The three load modes of docs/plan/07, and the order they fall through in.
/// </summary>
/// <remarks>
/// ⚠ Falling through is deliberate and is reported. "I asked for binlog and there is no binlog" is
/// the common case on a developer machine and on the agent path, and failing hard there means the
/// caller gets nothing rather than the syntactic half of the answer. What may never happen is
/// falling through <em>silently</em>: the mode that actually ran is in the SARIF's
/// <c>tool.driver.properties.loadMode</c>, in the terminal header, and in the agent report's
/// SKIPPED block, so a loose result can never be mistaken for a binlog one.
/// </remarks>
public static class ProjectLoader {
    public static LoadedProject Load(LoadRequest request, CancellationToken cancellation = default) {
        var attempted = ImmutableArray.CreateBuilder<SkalaDiagnostic>();

        foreach (var mode in Ladder(request)) {
            var loaded = mode switch {
                LoadMode.Binlog => BinlogLoader.Load(request, cancellation),
                LoadMode.Workspace => WorkspaceLoader.Load(request, cancellation),
                _ => LooseLoader.Load(request)
            };

            attempted.AddRange(loaded.Diagnostics);
            if (!loaded.IsEmpty) {
                return loaded with { Diagnostics = attempted.ToImmutable() };
            }

            if (!request.AllowFallback) {
                return loaded with { Diagnostics = attempted.ToImmutable() };
            }

            if (mode != LoadMode.Loose) {
                attempted.Add(
                    new SkalaDiagnostic(
                        "SK9025",
                        SkalaSeverity.Info,
                        $"--load={mode.ToString().ToLowerInvariant()} produced no compilation; falling back",
                        request.RepositoryRoot
                    )
                );
            }
        }

        return new LoadedProject {
            Mode = LoadMode.Loose, Diagnostics = attempted.ToImmutable(), Summary = "nothing could be loaded"
        };
    }

    /// <summary>
    /// ⚠ <c>--load=loose</c> never falls <em>up</em>. Asking for loose is asking for speed and for
    /// the semantics-free rule set; quietly running a build's worth of work instead would blow the
    /// budget the mode exists to meet.
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
