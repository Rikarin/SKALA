using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Rikarin.Skala.Analysis.Caching;
using Rikarin.Skala.Analysis.Hosting;
using Rikarin.Skala.Analysis.Loading;
using Rikarin.Skala.Reporting;
using Rikarin.Skala.Rules.Metadata;
using System.Collections.Immutable;

namespace Rikarin.Skala.Analysis.Tests;

/// <summary>
///     #364: under a project-backed load the per-file cache serves, and the compilation-scoped rules
///     re-run over the whole compilation beside it instead of sending everything cold.
/// </summary>
/// <remarks>
///     <para>
///         Measured before the fix, in-process over Skala's own 31 compilations under both
///         <c>Workspace</c> and <c>Binlog</c>: the guard was true on 31 of 31 units, the second run was
///         <c>hits=0 misses=727</c>, and 30 cache files had been written for nothing to read. The five
///         analyzers responsible cost 2.4 s alone against 6.8 s for all 299, so the cold path was
///         spending the other 4.4 s re-deriving answers the cache already held.
///     </para>
///     <para>
///         ⚠ Every test that claims the warm path asserts <see cref="IncrementalOutcome.CacheHits" />;
///         a fixture that goes cold both times passes the finding assertions vacuously (#363).
///     </para>
///     <para>
///         The fixture is <c>SK3051</c>, which reports an <c>async</c> method with no token to forward
///         <em>unless</em> some file in the compilation uses the method as a method group. So a change
///         to <c>Uses.cs</c> withdraws a finding in <c>Worker.cs</c>, which does not change — the exact
///         shape a per-file entry cannot answer and the bucket must. Sabotage, each of which turns a
///         test here red: serve the bucket's rules from the cache (drop the id filter in
///         <c>IncrementalAnalysis.Store</c>) and the withdrawn finding is served stale; skip
///         <c>RunCompilationScoped</c> and the finding is gone from every warm run; fold compiler
///         diagnostics into the bucket and <c>CS0168</c> is reported twice; run the bucket per tree and
///         the finding appears in a state where the other file has withdrawn it.
///     </para>
/// </remarks>
[Collection(SerialWorkspace.Name)]
public sealed class CompilationScopedBucketTests {
    const string AsyncWithoutToken = "SK3051";

    const string EmptyCatch = "SK2014";

    const string UnusedVariable = "CS0168";

    static readonly System.Text.Json.JsonSerializerOptions Json = new(System.Text.Json.JsonSerializerDefaults.Web);

    const string WorkerSource =
        """
        using System.IO;
        using System.Threading.Tasks;

        namespace Demo;

        public sealed class Worker {
            public async Task LoadAsync(Stream stream) {
                int unused;
                await stream.FlushAsync();
            }
        }
        """;

    const string UsesWithoutMethodGroup =
        """
        namespace Demo;

        public static class Uses {
            public static void Run() {
                try {
                    System.Console.WriteLine();
                } catch {
                }
            }
        }
        """;

    const string UsesWithMethodGroup =
        """
        using System;
        using System.IO;
        using System.Threading.Tasks;

        namespace Demo;

        public static class Uses {
            public static void Run() {
                Func<Stream, Task> load = new Worker().LoadAsync;
                try {
                    System.Console.WriteLine();
                } catch {
                }
            }
        }
        """;

    [Fact]
    public void Workspace_SecondRunIsServedFromTheCache_AndTheBucketStillReports() {
        using var scratch = new Scratch();
        var project = WriteProject(scratch);
        scratch.Write("Worker.cs", WorkerSource);
        scratch.Write("Uses.cs", UsesWithoutMethodGroup);

        var first = Run(scratch, project);
        Assert.Equal(0, first.CacheHits);
        Assert.Equal(2, first.CacheMisses);
        Assert.Single(first.Findings, static finding => finding.RuleId == AsyncWithoutToken);
        Assert.Single(first.Findings, static finding => finding.RuleId == EmptyCatch);
        Assert.Single(first.Findings, static finding => finding.RuleId == UnusedVariable);

        var second = Run(scratch, project);
        Assert.Equal(2, second.CacheHits);
        Assert.Equal(0, second.CacheMisses);
        Assert.Equal(Describe(first), Describe(second));
        Assert.Equal(Describe(Run(scratch, project, false)), Describe(second));
    }

    /// <summary>
    ///     The stale finding the partition exists to prevent: the file that withdraws <c>SK3051</c> is
    ///     not the file that carried it.
    /// </summary>
    [Fact]
    public void Workspace_AChangeInAnotherFileWithdrawsTheBucketFindingOnAWarmRun() {
        using var scratch = new Scratch();
        var project = WriteProject(scratch);
        scratch.Write("Worker.cs", WorkerSource);
        scratch.Write("Uses.cs", UsesWithoutMethodGroup);

        var cold = Run(scratch, project);
        Assert.Single(cold.Findings, static finding => finding.RuleId == AsyncWithoutToken);

        scratch.Write("Uses.cs", UsesWithMethodGroup);

        var warm = Run(scratch, project);
        Assert.Equal(1, warm.CacheHits);
        Assert.Equal(1, warm.CacheMisses);
        Assert.DoesNotContain(warm.Findings, static finding => finding.RuleId == AsyncWithoutToken);
        Assert.Single(warm.Findings, static finding => finding.RuleId == EmptyCatch);
        Assert.Single(warm.Findings, static finding => finding.RuleId == UnusedVariable);
        Assert.Equal(Describe(Run(scratch, project, false)), Describe(warm));

        // And back: the method-group use goes away, and the finding returns to the unchanged file.
        scratch.Write("Uses.cs", UsesWithoutMethodGroup);

        var restored = Run(scratch, project);
        Assert.Equal(2, restored.CacheHits);
        Assert.Equal(0, restored.CacheMisses);
        Assert.Single(restored.Findings, static finding => finding.RuleId == AsyncWithoutToken);
        Assert.Equal(Describe(cold), Describe(restored));
    }

    /// <summary>No entry on disk ever holds a rule the bucket owns, from the cold run or the warm one.</summary>
    [Fact]
    public void Workspace_TheBucketsRulesAreNeverPersisted() {
        using var scratch = new Scratch();
        var project = WriteProject(scratch);
        scratch.Write("Worker.cs", WorkerSource);
        scratch.Write("Uses.cs", UsesWithoutMethodGroup);

        Run(scratch, project);
        Assert.DoesNotContain(AsyncWithoutToken, PersistedRuleIds(scratch));
        Assert.Contains(EmptyCatch, PersistedRuleIds(scratch));

        // The warm store, for the changed file only: the bucket still reports it, the entry still
        // omits it.
        scratch.Write("Worker.cs", WorkerSource.Replace("LoadAsync", "FetchAsync", StringComparison.Ordinal));
        var warm = Run(scratch, project);
        Assert.Equal(1, warm.CacheHits);
        Assert.Equal(1, warm.CacheMisses);
        Assert.Single(warm.Findings, static finding => finding.RuleId == AsyncWithoutToken);
        Assert.DoesNotContain(AsyncWithoutToken, PersistedRuleIds(scratch));
    }

    /// <summary>
    ///     The partition over Skala's own analyzers is exactly the catalogue's uncacheable rules: every
    ///     one of them is in the bucket, nothing else is, and the five that ship enabled are among them.
    /// </summary>
    [Fact]
    public void Partition_MatchesTheCatalogue() {
        var bucket = AnalyzerHost.Own
            .Where(static analyzer => !AnalyzerHost.IsPerFileCacheable(analyzer))
            .SelectMany(static analyzer => analyzer.SupportedDiagnostics)
            .Select(static descriptor => descriptor.Id)
            .ToImmutableSortedSet(StringComparer.Ordinal);
        var carried = AnalyzerHost.Own
            .SelectMany(static analyzer => analyzer.SupportedDiagnostics)
            .Select(static descriptor => descriptor.Id)
            .Where(static id => DiagnosticCache.Uncacheable.Contains(id))
            .ToImmutableSortedSet(StringComparer.Ordinal);

        Assert.Equal(carried, bucket);
        Assert.Superset(ImmutableHashSet.Create("SK2290", "SK3043", "SK3044", "SK3051", "SK3061"), bucket);

        // ⚠ Off by default and still in the bucket: this is the SK3001 opt-in of docs/plan/16, which
        // the old guard silently under-reported. Membership is by scope, not by severity.
        Assert.Contains("SK3001", bucket);
    }

    /// <summary>An analyzer whose rules the catalogue does not know is not per-file, whatever it claims.</summary>
    [Fact]
    public void Partition_AnUnknownAnalyzerIsNotPerFile() {
        Assert.False(AnalyzerHost.IsPerFileCacheable(new Unknown()));
        Assert.Null(RuleCatalog.Find(Unknown.Id));
    }

    static string WriteProject(Scratch scratch) =>
        scratch.Write(
            "Scratch.csproj",
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <Nullable>enable</Nullable>
              </PropertyGroup>
            </Project>
            """
        );

    static IncrementalOutcome Run(Scratch scratch, string project, bool useCache = true) {
        var cancellation = TestContext.Current.CancellationToken;
        var loaded = ProjectLoader.Load(
            new LoadRequest {
                RepositoryRoot = scratch.Root, Mode = LoadMode.Workspace, ProjectPath = project, AllowFallback = false
            },
            cancellation
        );
        Assert.Equal(LoadMode.Workspace, loaded.Mode);
        var unit = Assert.Single(loaded.Units);
        var (options, fingerprint, _) = EditorConfigOptions.For(unit, scratch.Root);

        return IncrementalAnalysis.Run(
            unit,
            options,
            [],
            LoadMode.Workspace,
            scratch.Root,
            fingerprint,
            useCache,
            cancellation
        );
    }

    static string Describe(IncrementalOutcome outcome) =>
        string.Join(
            "\n",
            outcome.Findings
                .Select(static finding => $"{finding.RuleId} {Path.GetFileName(finding.Path)}:{finding.Line}:{finding.Column}"
                )
                .Order(StringComparer.Ordinal)
        );

    static ImmutableHashSet<string> PersistedRuleIds(Scratch scratch) {
        var directory = Path.Combine(scratch.Root, ".skala", "cache");
        var builder = ImmutableHashSet.CreateBuilder<string>(StringComparer.Ordinal);
        foreach (var file in Directory.GetFiles(directory, "*.diagnostics.json")) {
            var entries = System.Text.Json.JsonSerializer.Deserialize<List<CacheEntry>>(File.ReadAllText(file), Json);
            foreach (var entry in entries ?? []) {
                foreach (var finding in entry.Findings) {
                    builder.Add(finding.RuleId);
                }
            }
        }

        return builder.ToImmutable();
    }

#pragma warning disable RS1001 // an in-process fixture analyzer, never discovered from a file
    sealed class Unknown : DiagnosticAnalyzer {
#pragma warning restore RS1001
        internal const string Id = "XX0001";

#pragma warning disable RS2008 // a fixture descriptor, never reported and never shipped
        static readonly DiagnosticDescriptor Descriptor =
            new(Id, "Unknown", "{0}", "Test", DiagnosticSeverity.Warning, true);
#pragma warning restore RS2008

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Descriptor];

        public override void Initialize(AnalysisContext context) {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        }
    }
}
