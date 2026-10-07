using Rikarin.Skala.Analysis.Hosting;
using Rikarin.Skala.Analysis.Loading;
using Rikarin.Skala.Core.Diagnostics;
using Rikarin.Skala.Reporting;
using Rikarin.Skala.Rules.Metadata;

namespace Rikarin.Skala.Analysis.Tests;

/// <summary>
///     #422 through the host: a finding's fix loses its safe mark inside a captured argument, on the cold
///     path and on a cache hit whose file did not change but whose callee did.
/// </summary>
/// <remarks>
///     ⚠ The fixture is <c>SK1051</c>, a rule whose catalogue entry says <c>fixIsSafe: true</c>, at a
///     call to a method in <em>another</em> file. Adding <c>[CallerArgumentExpression]</c> to that method
///     leaves <c>Use.cs</c> byte-identical, so its cache key does not move, and only the re-ask in
///     <see cref="AnalyzerHost.Reassessed" /> can withdraw the mark. Sabotage: drop the re-ask from
///     <c>IncrementalAnalysis</c> and the second assertion here goes red; make
///     <c>AnalyzerHost.FixIsSafe</c> read only the catalogue and the cold assertion does.
/// </remarks>
[Collection(SerialWorkspace.Name)]
public sealed class CallerArgumentFixSafetyHostTests {
    const string Rule = "SK1051";

    const string Use =
        """
        namespace Demo;

        public static class Use {
            public static string Run(int count) => Checks.Check(count is not not 5);
        }
        """;

    const string Plain =
        """
        namespace Demo;

        public static class Checks {
            public static string Check(bool condition) => condition.ToString();
        }
        """;

    const string Capturing =
        """
        using System.Runtime.CompilerServices;

        namespace Demo;

        public static class Checks {
            public static string Check(bool condition, [CallerArgumentExpression("condition")] string text = "") =>
                text;
        }
        """;

    [Fact]
    public void ACapturedArgument_WithdrawsTheSafeMark_ColdAndFromTheCache() {
        Assert.True(RuleCatalog.Get(Rule).FixIsSafe);

        using var scratch = new Scratch();
        var project = scratch.Write(
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
        scratch.Write("Use.cs", Use);
        scratch.Write("Checks.cs", Plain);

        var plain = Assert.Single(Run(scratch, project, true).Findings, static finding => finding.RuleId == Rule);
        Assert.True(plain.HasFix);
        Assert.True(plain.FixIsSafe, "Nothing captures the argument yet, so the catalogue's answer stands.");

        scratch.Write("Checks.cs", Capturing);
        var warm = Run(scratch, project, true);
        Assert.True(warm.CacheHits > 0, "Use.cs did not change, so its findings must come from the cache.");
        var cached = Assert.Single(warm.Findings, static finding => finding.RuleId == Rule);
        Assert.True(cached.HasFix);
        Assert.False(cached.FixIsSafe, "The cached finding kept a safe mark its callee now withdraws.");

        var cold = Assert.Single(Run(scratch, project, false).Findings, static finding => finding.RuleId == Rule);
        Assert.False(cold.FixIsSafe);
    }

    /// <summary>A target that withdraws the mark wins the multi-target merge, whichever comes first.</summary>
    [Fact]
    public void TheMerge_IsSafeOnlyWhereEveryTargetIsSafe() {
        Finding Make(string framework, bool safe) =>
            new() {
                RuleId = Rule,
                Severity = SkalaSeverity.Info,
                Message = "m",
                Path = "/a.cs",
                Line = 1,
                Column = 1,
                EndLine = 1,
                EndColumn = 2,
                Fix = [new FixEdit("/a.cs", 0, 1, "x")],
                FixIsSafe = safe,
                TargetFrameworks = [framework]
            };

        Assert.False(Assert.Single(AnalyzerHost.Merge([Make("net8.0", true), Make("net9.0", false)])).FixIsSafe);
        Assert.False(Assert.Single(AnalyzerHost.Merge([Make("net9.0", false), Make("net8.0", true)])).FixIsSafe);
        Assert.True(Assert.Single(AnalyzerHost.Merge([Make("net8.0", true), Make("net9.0", true)])).FixIsSafe);
    }

    static IncrementalOutcome Run(Scratch scratch, string project, bool useCache) {
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
}
