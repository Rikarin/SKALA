using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Rikarin.Skala.Analysis.Caching;
using Rikarin.Skala.Analysis.Loading;
using Rikarin.Skala.Core.Diagnostics;
using Rikarin.Skala.Reporting;
using Rikarin.Skala.Rules.Metadata;
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
///         <item>
///             ⚠ <b>The other guard</b> — nothing is written from a run that did not cover the files it
///             ran over: an analyzer threw, or the run was cancelled. See <see cref="Covered" />.
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
            if (Covered(cold)) {
                Store(cache, keys, unit, cold.Findings);
                cache.Save();
            }

            return new(cold.Findings, cold.Diagnostics, 0, keys.Count, cold.Partial, cold.Costs);
        }

        var warm = AnalyzerHost.RunForTrees(unit, options, hosted, mode, misses, cancellation, profile);
        if (Covered(warm)) {
            Store(cache, keys.Where(pair => misses.Contains(pair.Key)), unit, warm.Findings);
            cache.Save();
        }

        return new IncrementalOutcome(
            [.. hits, .. warm.Findings],
            warm.Diagnostics,
            cache.Hits,
            cache.Misses,
            warm.Partial,
            warm.Costs
        );
    }

    /// <summary>
    ///     Whether a run's findings are the whole answer for the files it ran over — the one condition
    ///     under which they may be written to the cache.
    /// </summary>
    /// <remarks>
    ///     ⚠ #363. <c>Store</c> writes an entry for every file, and a file with no findings gets one
    ///     too — that is what makes "clean" distinguishable from "not cached". So when an analyzer threw
    ///     (<c>SK9030</c>), the findings its rules would have reported are simply absent from
    ///     <c>Findings</c>, and storing that writes "clean" against every file it should have inspected.
    ///     The <c>SK9030</c> itself is a run diagnostic, not a finding, so it is not stored. The next run
    ///     hits on every key, the all-hits return above hands back the cached findings with no
    ///     diagnostics, and the gate has nothing to fail on: the crash and the rules it took with it are
    ///     gone from every later report until something else moves the key. Measured on the real binary
    ///     over a two-file loose tree: run one exit 1 with <c>SK9030</c>, run two exit 0 with
    ///     <c>SK2014</c> served from the cache. It is #359 one layer down — there a baseline, here a cache,
    ///     written from a run that did not cover the tree and read by every run after it.
    ///     <para>
    ///         A cancelled run is the same shape and worse: <c>AnalyzerHost</c> returns <em>no</em>
    ///         findings and <c>Partial</c>, so storing it writes every file as clean, not just the
    ///         crashed analyzer's share.
    ///     </para>
    ///     <para>
    ///         ⚠ Blunt on purpose: the whole run is withheld, not the crashed analyzer's share of it.
    ///         The precise version needs to know which files the analyzer threw on and which rules it
    ///         would have reported there, and neither is in the outcome — Roslyn's exception callback
    ///         carries <c>Location.None</c>, and <see cref="Finding" /> attributes a finding to a rule,
    ///         not to the analyzer that carried it. A partial entry the read path could complete would
    ///         be a new cache format and a per-analyzer re-run the driver does not have. The cost of the
    ///         blunt guard is one cold run after a crash, which is the right price for a crash.
    ///     </para>
    /// </remarks>
    static bool Covered(AnalysisOutcome outcome) =>
        !outcome.Partial
        && !outcome.Diagnostics.Any(static diagnostic =>
            string.Equals(diagnostic.Id, RuleIds.AnalyzerThrew, StringComparison.Ordinal)
        );

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
