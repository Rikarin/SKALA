using System.Collections.Immutable;

namespace Rikarin.Skala.Analysis.Loading;

/// <summary>
///     Joining the compilations a multi-targeted project was opened as back together (#343).
/// </summary>
/// <remarks>
///     ⚠
///     <b>
///         Both real loaders already produce one unit per target framework and neither says they
///         belong together.
///     </b> <c>MSBuildWorkspace</c> hands back one <c>Project</c> per moniker —
///     <c>Probe (netstandard2.1)</c> and <c>Probe (net10.0)</c> — and a binlog carries one <c>csc</c>
///     invocation per moniker. <c>CheckCommand</c> then analyses each in turn and unions the findings,
///     which is right for a rule that reads source and wrong for one whose condition is a fact about
///     the framework: the union says "some moniker can do this", the source file needs "every moniker
///     can".
///     <para>
///         Applied at the single funnel in <see cref="ProjectLoader.Load" /> so that binlog, workspace
///         and any future mode get it once. Grouping is by <see cref="CompilationUnit.ProjectPath" />,
///         which is what identifies the monikers of one project — <see cref="CompilationUnit.Name" />
///         is not, because the workspace decorates it with the moniker and the binlog does not.
///     </para>
///     <para>
///         ⚠ A unit with no <c>ProjectPath</c> is left alone rather than grouped with the other
///         path-less ones. The loose load has no project at all and the empty string would make every
///         loose compilation a sibling of every other, which is a claim about frameworks that nothing
///         measured.
///     </para>
/// </remarks>
public static class MultiTargetLink {
    public static ImmutableArray<CompilationUnit> Apply(ImmutableArray<CompilationUnit> units) {
        if (units.Length < 2) {
            return units;
        }

        var byProject = new Dictionary<string, List<CompilationUnit>>(StringComparer.Ordinal);
        foreach (var unit in units) {
            if (unit.ProjectPath is not { Length: > 0 } path) {
                continue;
            }

            var key = Normalise(path);
            if (!byProject.TryGetValue(key, out var group)) {
                byProject[key] = group = [];
            }

            group.Add(unit);
        }

        if (!byProject.Values.Any(static group => group.Count > 1)) {
            return units;
        }

        var linked = ImmutableArray.CreateBuilder<CompilationUnit>(units.Length);
        foreach (var unit in units) {
            if (unit.ProjectPath is not { Length: > 0 } path
                || !byProject.TryGetValue(Normalise(path), out var group)
                || group.Count < 2) {
                linked.Add(unit);
                continue;
            }

            linked.Add(
                unit with {
                    Siblings = [
                        .. group.Where(other => !ReferenceEquals(other.Compilation, unit.Compilation))
                            .Select(static other => other.Compilation)
                    ]
                }
            );
        }

        return linked.ToImmutable();
    }

    /// <summary>
    ///     ⚠ Case-insensitive, and only here. Two <c>csc</c> invocations for one project reach the
    ///     loaders through different MSBuild properties and a binlog has been seen to spell the same
    ///     path with a different drive-letter case; a grouping that misses on that produces no
    ///     siblings and silently restores the defect this type exists to close.
    /// </summary>
    static string Normalise(string path) {
        try {
            return Path.GetFullPath(path).ToLowerInvariant();
        } catch (Exception exception) when (exception is ArgumentException or IOException or NotSupportedException) {
            return path.ToLowerInvariant();
        }
    }
}
