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
/// <remarks>
///     ⚠ <see cref="CacheHits" /> and <see cref="CacheMisses" /> count <em>halves</em>, two per file —
///     its Syntax half and its semantic half (#516) — so an untouched two-file compilation is 4 hits, and
///     one edit in it is 1 hit (the other file's Syntax half) and 3 misses.
/// </remarks>
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
///     docs/plan/07 § "The incremental cache". The shape is two paths, a partition and one guard:
///     <list type="number">
///         <item>
///             <b>Cold</b> — nothing cached. One <c>GetAllDiagnosticsAsync</c> over every analyzer, then
///             every file's findings are written to the cache.
///         </item>
///         <item>
///             <b>Warm</b> — every unchanged file's findings come from the cache; the changed ones are run
///             through <c>GetAnalysisResultAsync(tree)</c> and <c>GetAnalysisResultAsync(semanticModel)</c>
///             with the per-file analyzers only.
///         </item>
///         <item>
///             ⚠ <b>The halves</b> (#516) — "unchanged" is per half. A file's Syntax-scoped findings are
///             keyed on its own text; the rest on every tree's text too, because a semantic model of
///             <c>A.cs</c> is a function of <c>B.cs</c>. After an edit the unchanged files serve their
///             Syntax half and re-run the semantic one.
///         </item>
///         <item>
///             ⚠ <b>The partition</b> — a <c>Compilation</c>-scoped rule's answer for <c>A.cs</c> depends
///             on files the key for <c>A.cs</c> does not name, so its analyzer is never run per tree and
///             its findings are never stored: on every warm run it re-runs over the whole compilation
///             (<see cref="AnalyzerHost.RunCompilationScoped" />) and its findings are added to what the
///             cache and the per-tree run produced. <see cref="AnalyzerHost.IsPerFileCacheable" /> decides
///             the side. ⚠ Until #364 this was a guard rather than a partition: one such rule enabled
///             sent <em>everything</em> cold, five of them ship enabled, and so from 2026-09-01 every
///             project-backed run in every repository wrote the per-file cache and none read it. See
///             <see cref="DiagnosticCache" />.
///         </item>
///         <item>
///             ⚠ <b>The guard</b> — nothing is written from a run that did not cover the files it
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
        var semanticFingerprint = CacheKey.SemanticFingerprint(unit, compilationFingerprint);
        var analyzers = AnalyzerHost.EnabledFor(mode, hosted);
        var ruleSetFingerprint = CacheKey.RuleSetFingerprint(analyzers);

        // ⚠ #516: two entries per file, because a file's findings have two different sets of inputs.
        // The Syntax half (rules that declare `RuleScope.Syntax`) is a function of the file's own text
        // and keeps the per-file key. Everything else — Semantic rules and the compiler's diagnostics —
        // reads a semantic model, which is a function of every tree in the compilation, so its key
        // also names every tree (`CacheKey.SemanticFingerprint`). A change anywhere moves every file's
        // semantic key together: the changed files re-run whole, the unchanged ones re-run only the
        // analyzers that can report into the semantic half, and their Syntax half is still served.
        var keys = new Dictionary<SyntaxTree, (string Syntax, string Semantic)>();
        var hits = new List<Finding>();
        var changed = new List<SyntaxTree>();
        var rebound = new List<SyntaxTree>();
        var served = 0;

        foreach (var tree in unit.Compilation.SyntaxTrees) {
            var path = Path.GetFullPath(tree.FilePath);
            if (!unit.ReportablePaths.Contains(path)) {
                continue;
            }

            var content = Encoding.UTF8.GetBytes(tree.GetText(cancellation).ToString());
            var syntaxKey = CacheKey.For(path, content, compilationFingerprint, ruleSetFingerprint, editorConfigFingerprint);
            var semanticKey = CacheKey.For(path, content, semanticFingerprint, ruleSetFingerprint, editorConfigFingerprint);
            keys[tree] = (syntaxKey, semanticKey);

            if (!cache.TryGet(syntaxKey, out var syntaxHalf, path)) {
                changed.Add(tree);
                continue;
            }

            served++;
            hits.AddRange(syntaxHalf);
            if (cache.TryGet(semanticKey, out var semanticHalf, path)) {
                served++;
                hits.AddRange(semanticHalf);
            } else {
                rebound.Add(tree);
            }
        }

        // ⚠ The partition. The rule ids no per-file entry may hold: everything carried by an
        // analyzer the warm path does not run per tree. `DiagnosticCache.Put` drops the catalogue's
        // uncacheable ids on its own; this is the wider set, because an analyzer that carries one
        // uncacheable descriptor beside a cacheable one runs in the whole-compilation bucket as a
        // unit, and a finding of its cacheable rule must then come from that bucket every time
        // rather than from the cache once and the bucket again.
        //
        // ⚠ No severity is read here, effective or default. The old guard tested
        // `IsEnabledByDefault` so that SK3001 (`defaultSeverity: none`) would not disable the warm
        // path for everyone; it did not read `dotnet_diagnostic.SK3001.severity`, so a repository
        // that opted in kept the warm path and lost the rule on every unchanged file. In a partition
        // membership costs nothing while the rule is off -- Roslyn skips an analyzer whose every
        // descriptor is off -- so the question the guard was answering no longer exists.
        var compilationScopedIds = analyzers
            .Where(static analyzer => !AnalyzerHost.IsPerFileCacheable(analyzer))
            .SelectMany(static analyzer => analyzer.SupportedDiagnostics)
            .Select(static descriptor => descriptor.Id)
            .ToImmutableHashSet(StringComparer.Ordinal);

        var halves = 2 * keys.Count;
        if (changed.Count == keys.Count) {
            var cold = AnalyzerHost.Run(unit, options, hosted, mode, cancellation, profile);
            if (Covered(cold)) {
                Store(cache, keys, unit, cold.Findings, compilationScopedIds, true);
                cache.Save();
            }

            return new(cold.Findings, cold.Diagnostics, 0, halves, cold.Partial, cold.Costs);
        }

        var warm = changed.Count == 0
            ? new AnalysisOutcome([], [], false)
            : AnalyzerHost.RunForTrees(unit, options, hosted, mode, changed, cancellation, profile);
        if (changed.Count > 0 && Covered(warm)) {
            Store(cache, keys.Where(pair => changed.Contains(pair.Key)), unit, warm.Findings, compilationScopedIds, true);
        }

        // ⚠ The unchanged files of a compilation something else in changed. Their Syntax half came
        // from the cache above, so a Syntax finding an analyzer with a mixed rule set reports here is
        // dropped rather than reported twice.
        var again = rebound.Count == 0
            ? new AnalysisOutcome([], [], false)
            : AnalyzerHost.RunForTrees(unit, options, hosted, mode, rebound, cancellation, profile, true);
        if (rebound.Count > 0 && Covered(again)) {
            Store(cache, keys.Where(pair => rebound.Contains(pair.Key)), unit, again.Findings, compilationScopedIds, false);
        }

        cache.Save();

        // ⚠ Every warm run, changed or not, whenever the bucket is non-empty: the cache holds nothing
        // for these rules by construction, so there is no "all hits" return that could skip them. A
        // key that did not move says the *file* did not change, not that the answer a
        // whole-compilation rule gives about it did not.
        var whole = compilationScopedIds.IsEmpty
            ? new AnalysisOutcome([], [], false)
            : AnalyzerHost.RunCompilationScoped(unit, options, hosted, mode, cancellation, profile);

        return new IncrementalOutcome(
            [
                .. AnalyzerHost.Reassessed(unit, hits),
                .. warm.Findings,
                .. again.Findings.Where(static finding => !AnalyzerHost.IsSyntaxHalf(finding.RuleId)),
                .. whole.Findings
            ],
            [.. warm.Diagnostics, .. again.Diagnostics, .. whole.Diagnostics],
            served,
            halves - served,
            warm.Partial || again.Partial || whole.Partial,
            [.. warm.Costs, .. again.Costs, .. whole.Costs]
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

    /// <param name="compilationScopedIds">
    ///     The rule ids the whole-compilation bucket owns. ⚠ Dropped here as well as by
    ///     <see cref="DiagnosticCache.Put" />, which knows only the catalogue's uncacheable ids: the cold
    ///     run produces every rule's findings in one pass, and an entry holding a finding this set names
    ///     would be served beside the bucket's own answer on the next warm run — the same finding twice
    ///     — or, for a rule the bucket no longer runs, once from a run that can no longer be reproduced.
    /// </param>
    /// <param name="bothHalves">
    ///     Whether the run covered the files' Syntax half too. <c>false</c> for the unchanged files of a
    ///     touched compilation, whose run carried only the analyzers that report into the semantic half
    ///     (#516) — writing its findings as their Syntax half would store "clean" for every Syntax rule.
    /// </param>
    static void Store(
        DiagnosticCache cache,
        IEnumerable<KeyValuePair<SyntaxTree, (string Syntax, string Semantic)>> keys,
        CompilationUnit unit,
        ImmutableArray<Finding> findings,
        ImmutableHashSet<string> compilationScopedIds,
        bool bothHalves
    ) {
        var byPath = findings
            .Where(finding => !compilationScopedIds.Contains(finding.RuleId))
            .GroupBy(static finding => finding.Path, StringComparer.Ordinal)
            .ToDictionary(static group => group.Key, static group => group.ToImmutableArray(), StringComparer.Ordinal);

        foreach (var (tree, (syntaxKey, semanticKey)) in keys) {
            var path = Path.GetFullPath(tree.FilePath);
            if (!unit.ReportablePaths.Contains(path)) {
                continue;
            }

            // ⚠ A file with no findings gets an entry too. Without one, "clean" is
            // indistinguishable from "not in the cache" and every clean file is a miss forever —
            // which on a tree that is mostly clean is the whole cache.
            var found = byPath.TryGetValue(path, out var all) ? all : [];
            if (bothHalves) {
                cache.Put(syntaxKey, path, [.. found.Where(static finding => AnalyzerHost.IsSyntaxHalf(finding.RuleId))]);
            }

            cache.Put(
                semanticKey,
                path,
                [.. found.Where(static finding => !AnalyzerHost.IsSyntaxHalf(finding.RuleId))],
                true
            );
        }
    }
}
