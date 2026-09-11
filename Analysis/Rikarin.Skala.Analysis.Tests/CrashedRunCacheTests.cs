using Microsoft.CodeAnalysis;
using Rikarin.Skala.Analysis.Caching;
using Rikarin.Skala.Analysis.Hosting;
using Rikarin.Skala.Analysis.Loading;
using Rikarin.Skala.Reporting;
using Rikarin.Skala.Rules.Metadata;

namespace Rikarin.Skala.Analysis.Tests;

/// <summary>
///     #363: a run in which an analyzer threw, or which was cancelled, writes nothing to the per-file
///     cache — so the next run re-runs it and fails again, instead of serving the crash's silence.
/// </summary>
/// <remarks>
///     <para>
///         Measured on the real binary before the fix, over a two-file loose tree with
///         <c>SKALA_FORCE_SK9030</c> set both times: run one exit 1 with <c>SK9030</c>; run two exit 0,
///         no <c>SK9030</c>, <c>SK2014</c> served from the cache. The crash and every rule the crashed
///         analyzer carried were gone from the report until something else moved the key.
///     </para>
///     <para>
///         ⚠ These drive <see cref="IncrementalAnalysis.Run" /> directly rather than the CLI, and the
///         reason is the one number the fix is about: <see cref="IncrementalOutcome.CacheHits" />. A
///         fixture that goes cold both times passes every assertion here vacuously — that is what
///         #362's own instrument did, because <c>ForcedCrash</c> declared a <c>Compilation</c>-scoped
///         descriptor and tripped the cold-path guard on every forced run. So every test that claims the
///         warm path asserts <c>CacheHits &gt; 0</c>, and the crash is requested through
///         <see cref="AnalyzerHost.ForcedCrashInProcess" /> rather than the environment, which a
///         parallel test process cannot set without every other test seeing the crash.
///     </para>
///     <para>
///         ⚠ Loose mode, on purpose. Under <c>--load=workspace</c> and <c>--load=binlog</c> the warm path
///         is unreachable in any repository: five shipped analyzers are enabled by default and
///         <c>Compilation</c>-scoped, and the guard reads the catalogue default rather than the
///         effective severity. Loose is the mode with a live cache, and the mode an agent uses.
///     </para>
///     <para>
///         Sabotage: drop <c>Covered</c> from either store site and the matching second run below
///         reports no <c>SK9030</c> and serves the lost finding count from the cache.
///     </para>
/// </remarks>
public sealed class CrashedRunCacheTests {
    const string CleanSource = "namespace Demo;\n\npublic sealed class Clean {\n    public int Value { get; init; }\n}\n";

    /// <summary>One <c>SK2014</c>, syntactic, so a poisoned cache shows as a lost finding and not as 0 against 0.</summary>
    const string SwallowSource =
        "namespace Demo;\n\npublic static class Swallow {\n    public static void Run() {\n"
        + "        try {\n            System.Console.WriteLine();\n        } catch {\n        }\n    }\n}\n";

    const string CrashFile = "Crash.cs";

    static readonly Func<SyntaxTree, bool> Everywhere = static _ => true;

    static readonly Func<SyntaxTree, bool> Nowhere = static _ => false;

    static readonly Func<SyntaxTree, bool> OnCrashFile = static tree =>
        string.Equals(Path.GetFileName(tree.FilePath), CrashFile, StringComparison.Ordinal);

    /// <summary>The cold store: a run in which every tree's analyzer threw writes nothing.</summary>
    [Fact]
    public void ColdCrash_IsNotPersisted_SoTheNextRunCrashesAgain() {
        using var scratch = new Scratch();
        scratch.Write("Clean.cs", CleanSource);
        scratch.Write("Swallow.cs", SwallowSource);

        var first = Run(scratch, Everywhere);
        Assert.Contains(first.Diagnostics, Threw);
        Assert.Single(first.Findings, static finding => finding.RuleId == "SK2014");

        var second = Run(scratch, Everywhere);
        Assert.Contains(second.Diagnostics, Threw);
        Assert.Single(second.Findings, static finding => finding.RuleId == "SK2014");

        // ⚠ Cold by construction, and that is the assertion: nothing was stored, so nothing could hit.
        Assert.Equal(0, second.CacheHits);
        Assert.Equal(2, second.CacheMisses);
        Assert.Equal(0, Persisted(scratch));
    }

    /// <summary>
    ///     The control: the same tree without a crash is stored on the first run and served on the
    ///     second, so the guard costs a healthy run nothing.
    /// </summary>
    [Fact]
    public void HealthyRuns_StillTakeTheWarmPath() {
        using var scratch = new Scratch();
        scratch.Write("Clean.cs", CleanSource);
        scratch.Write("Swallow.cs", SwallowSource);

        var first = Run(scratch, Nowhere);
        Assert.Empty(first.Diagnostics);
        Assert.Equal(0, first.CacheHits);
        Assert.Equal(2, Persisted(scratch));

        var second = Run(scratch, Nowhere);
        Assert.Empty(second.Diagnostics);
        Assert.Equal(2, second.CacheHits);
        Assert.Equal(0, second.CacheMisses);
        Assert.Single(second.Findings, static finding => finding.RuleId == "SK2014");
    }

    /// <summary>
    ///     The warm store: a crash during the partial re-run of the changed files does not persist the
    ///     partial set — and the cache hits on the unchanged files prove the warm path was the one taken.
    /// </summary>
    [Fact]
    public void WarmPartialCrash_IsNotPersisted_AndTheUnchangedFilesStillHit() {
        using var scratch = new Scratch();
        scratch.Write("Clean.cs", CleanSource);
        scratch.Write("Swallow.cs", SwallowSource);

        // The analyzer is selected but throws on no tree yet: a healthy run that fills the cache
        // under the same rule-set fingerprint the crashing runs below will have.
        var healthy = Run(scratch, OnCrashFile);
        Assert.Empty(healthy.Diagnostics);
        Assert.Equal(2, Persisted(scratch));

        scratch.Write(CrashFile, SwallowSource.Replace("Swallow", "Crash", StringComparison.Ordinal));

        var crashed = Run(scratch, OnCrashFile);
        Assert.Contains(crashed.Diagnostics, Threw);
        Assert.Equal(2, crashed.CacheHits);
        Assert.Equal(1, crashed.CacheMisses);
        Assert.Equal(2, crashed.Findings.Count(static finding => finding.RuleId == "SK2014"));

        var again = Run(scratch, OnCrashFile);
        Assert.Contains(again.Diagnostics, Threw);
        Assert.Equal(2, again.CacheHits);
        Assert.Equal(1, again.CacheMisses);
        Assert.Equal(2, again.Findings.Count(static finding => finding.RuleId == "SK2014"));
        Assert.Equal(2, Persisted(scratch));
    }

    /// <summary>
    ///     A cancelled run returns no findings at all, so storing it would write every file as clean —
    ///     the whole tree's findings lost, not one analyzer's share.
    /// </summary>
    [Fact]
    public void CancelledRun_IsNotPersisted() {
        using var scratch = new Scratch();
        scratch.Write("Clean.cs", CleanSource);
        scratch.Write("Swallow.cs", SwallowSource);

        using var source = new CancellationTokenSource();
        var cancelled = Run(
            scratch,
            tree => {
                source.Cancel();
                return false;
            },
            source.Token
        );
        Assert.True(cancelled.Partial);
        Assert.Empty(cancelled.Findings);
        Assert.Equal(0, Persisted(scratch));

        // ⚠ Same analyzer set, so the keys are the ones the cancelled run would have written under.
        // Sabotaged, this run hits twice and the SK2014 is gone.
        var after = Run(scratch, Nowhere);
        Assert.False(after.Partial);
        Assert.Equal(0, after.CacheHits);
        Assert.Single(after.Findings, static finding => finding.RuleId == "SK2014");
        Assert.Equal(2, Persisted(scratch));
    }

    static bool Threw(Core.Diagnostics.SkalaDiagnostic diagnostic) =>
        string.Equals(diagnostic.Id, RuleIds.AnalyzerThrew, StringComparison.Ordinal);

    static IncrementalOutcome Run(Scratch scratch, Func<SyntaxTree, bool> shouldThrow, CancellationToken? token = null) {
        var cancellation = token ?? TestContext.Current.CancellationToken;
        var loaded = ProjectLoader.Load(
            new LoadRequest { RepositoryRoot = scratch.Root, Mode = LoadMode.Loose },
            cancellation
        );
        var unit = Assert.Single(loaded.Units);
        var (options, fingerprint, _) = EditorConfigOptions.For(unit, scratch.Root);

        AnalyzerHost.ForcedCrashInProcess.Value = shouldThrow;
        try {
            return IncrementalAnalysis.Run(
                unit,
                options,
                [],
                LoadMode.Loose,
                scratch.Root,
                fingerprint,
                true,
                cancellation
            );
        } finally {
            AnalyzerHost.ForcedCrashInProcess.Value = null;
        }
    }

    /// <summary>How many files the on-disk cache holds an entry for, read the way the next run reads it.</summary>
    static int Persisted(Scratch scratch) {
        var cache = new DiagnosticCache(scratch.Root, "loose.");
        cache.Load();
        return cache.Held;
    }
}
