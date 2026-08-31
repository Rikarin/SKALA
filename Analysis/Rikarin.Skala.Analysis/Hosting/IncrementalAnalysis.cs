using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Rikarin.Skala.Analysis.Caching;
using Rikarin.Skala.Analysis.Loading;
using Rikarin.Skala.Core.Diagnostics;
using Rikarin.Skala.Reporting;
using System.Collections.Immutable;
using System.Text;

namespace Rikarin.Skala.Analysis.Hosting;

/// <summary>What one incremental pass over a compilation produced, and how much of it was reused.</summary>
public sealed record IncrementalOutcome(
    ImmutableArray<Finding> Findings,
    ImmutableArray<SkalaDiagnostic> Diagnostics,
    int CacheHits,
    int CacheMisses,
    bool Partial,
    ImmutableArray<AnalyzerCost> Costs = default) {
    /// <summary>⚠ A <c>default</c> ImmutableArray throws on enumeration; profiling is opt-in.</summary>
    public ImmutableArray<AnalyzerCost> Costs { get; init; } = Costs.IsDefault ? [] : Costs;
}

/// <summary>
///     The per-file cache in front of the analyzer driver.
/// </summary>
/// <remarks>
///     docs/plan/07 § "The incremental cache". The shape is two paths and one guard:
///     <list type="number">
///         <item>
///             <b>Cold</b> — nothing cached, or a compilation-scoped rule is enabled and something changed.
///             One <c>GetAllDiagnosticsAsync</c>, then every file's findings are written to the cache.
///         </item>
///         <item>
///             <b>Warm</b> — every unchanged file's findings come from the cache; the changed ones are run
///             through <c>GetAnalysisResultAsync(tree)</c> and <c>GetAnalysisResultAsync(semanticModel)</c>,
///             which is what makes "changed files in under 5 s on a 4 691-file tree" reachable.
///         </item>
///         <item>
///             ⚠ <b>The guard</b> — if any enabled rule is <c>Compilation</c>-scoped, the warm path is not
///             available at all when anything changed, because such a rule's answer for <c>A.cs</c> depends on
///             files the key for <c>A.cs</c> does not name. See <see cref="DiagnosticCache" />.
///         </item>
///     </list>
/// </remarks>
public static class IncrementalAnalysis {
    public static IncrementalOutcome Run(
        CompilationUnit unit,
        AnalyzerOptions options,
        ImmutableArray<DiagnosticAnalyzer> hosted,
        LoadMode mode,
        string repositoryRoot,
        string editorConfigFingerprint,
        bool useCache,
        CancellationToken cancellation,
        bool profile = false
    ) {
        if (!useCache) {
            var cold = AnalyzerHost.Run(unit, options, hosted, mode, cancellation, profile);
            return new(
                cold.Findings,
                cold.Diagnostics,
                0,
                unit.ReportablePaths.Count,
                cold.Partial,
                cold.Costs
            );
        }

        var cache = new DiagnosticCache(repositoryRoot, unit.Name + "." + unit.TargetFramework);
        cache.Load();

        var compilationFingerprint = CacheKey.CompilationFingerprint(unit);
        var analyzers = AnalyzerHost.EnabledFor(mode, hosted);
        var ruleSetFingerprint = CacheKey.RuleSetFingerprint(analyzers);

        var keys = new Dictionary<SyntaxTree, string>();
        var hits = new List<Finding>();
        var misses = new List<SyntaxTree>();

        foreach (var tree in unit.Compilation.SyntaxTrees) {
            var path = Path.GetFullPath(tree.FilePath);
            if (!unit.ReportablePaths.Contains(path)) {
                continue;
            }

            var key = CacheKey.For(
                path,
                Encoding.UTF8.GetBytes(tree.GetText(cancellation).ToString()),
                compilationFingerprint,
                ruleSetFingerprint,
                editorConfigFingerprint
            );

            keys[tree] = key;
            if (cache.TryGet(key, out var cached, path)) {
                hits.AddRange(cached);
            } else {
                misses.Add(tree);
            }
        }

        // ⚠ The guard. A compilation-scoped rule cannot be served from a per-file cache, so any
        // change at all sends the whole compilation down the cold path.
        //
        // ⚠ <b>Enabled</b> compilation-scoped rules, not merely supported ones. M6 added SK3001,
        // whose event-handler check has to see the whole compilation and which therefore ships
        // `defaultSeverity: none`. Testing `SupportedDiagnostics` alone would let a rule nobody
        // turned on disable the warm path for every run in every repository — the whole incremental
        // cache traded away for a rule that is not running. Roslyn's own driver filters on the same
        // property before it ever invokes the analyzer, so this asks the question the driver
        // already answered.
        var hasCompilationScopedRule = analyzers
            .SelectMany(static analyzer => analyzer.SupportedDiagnostics)
            .Any(static descriptor =>
                descriptor.IsEnabledByDefault && DiagnosticCache.Uncacheable.Contains(descriptor.Id)
            );

        if (misses.Count == 0 && !hasCompilationScopedRule) {
            cache.Save();
            return new IncrementalOutcome([.. hits], [], cache.Hits, cache.Misses, false);
        }

        if (hasCompilationScopedRule || misses.Count == keys.Count) {
            var cold = AnalyzerHost.Run(unit, options, hosted, mode, cancellation, profile);
            Store(cache, keys, unit, cold.Findings);
            cache.Save();
            return new(cold.Findings, cold.Diagnostics, 0, keys.Count, cold.Partial, cold.Costs);
        }

        var warm = AnalyzerHost.RunForTrees(unit, options, hosted, mode, misses, cancellation, profile);
        Store(cache, keys.Where(pair => misses.Contains(pair.Key)), unit, warm.Findings);
        cache.Save();

        return new IncrementalOutcome(
            [.. hits, .. warm.Findings],
            warm.Diagnostics,
            cache.Hits,
            cache.Misses,
            warm.Partial,
            warm.Costs
        );
    }

    static void Store(
        DiagnosticCache cache,
        IEnumerable<KeyValuePair<SyntaxTree, string>> keys,
        CompilationUnit unit,
        ImmutableArray<Finding> findings
    ) {
        var byPath = findings.GroupBy(static finding => finding.Path, StringComparer.Ordinal)
            .ToDictionary(static group => group.Key, static group => group.ToImmutableArray(), StringComparer.Ordinal);

        foreach (var (tree, key) in keys) {
            var path = Path.GetFullPath(tree.FilePath);
            if (!unit.ReportablePaths.Contains(path)) {
                continue;
            }

            // ⚠ A file with no findings gets an entry too. Without one, "clean" is
            // indistinguishable from "not in the cache" and every clean file is a miss forever —
            // which on a tree that is mostly clean is the whole cache.
            cache.Put(key, path, byPath.TryGetValue(path, out var found) ? found : []);
        }
    }
}
