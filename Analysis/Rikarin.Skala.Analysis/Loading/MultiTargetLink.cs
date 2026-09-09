using Microsoft.CodeAnalysis.CSharp;
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
        if (units.Length < 2 || MultiTargetedProjects(units) is not { } byProject) {
            return units;
        }

        var linked = ImmutableArray.CreateBuilder<CompilationUnit>(units.Length);
        foreach (var unit in units) {
            // ⚠ `Remove`, not a filtering lambda over the iteration variable: the compilations are
            // distinct instances, so removing this unit's own is exactly "the other monikers", and it
            // allocates no closure. `SK4002` reports the `Where` shape and `SK1084` reports the hand
            // written loop that avoids it — this is the spelling neither objects to.
            linked.Add(
                unit.ProjectPath is { Length: > 0 } path && byProject.TryGetValue(Normalise(path), out var monikers)
                    ? unit with { Siblings = monikers.Remove(unit.Compilation) }
                    : unit
            );
        }

        return linked.ToImmutable();
    }

    /// <summary>
    ///     Every project opened as more than one compilation, and the compilations it was opened as.
    /// </summary>
    /// <remarks>Null rather than an empty map: nothing multi-targets, so nothing has to be rebuilt.</remarks>
    static Dictionary<string, ImmutableArray<CSharpCompilation>>? MultiTargetedProjects(
        ImmutableArray<CompilationUnit> units
    ) {
        var byProject = new Dictionary<string, List<CSharpCompilation>>(StringComparer.Ordinal);
        foreach (var unit in units) {
            if (unit.ProjectPath is not { Length: > 0 } path) {
                continue;
            }

            var key = Normalise(path);
            if (!byProject.TryGetValue(key, out var group)) {
                byProject[key] = group = [];
            }

            group.Add(unit.Compilation);
        }

        var multiTargeted = new Dictionary<string, ImmutableArray<CSharpCompilation>>(StringComparer.Ordinal);
        foreach (var (key, group) in byProject) {
            if (group.Count > 1) {
                multiTargeted[key] = [.. group];
            }
        }

        return multiTargeted.Count == 0 ? null : multiTargeted;
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
