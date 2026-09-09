using Microsoft.CodeAnalysis;
using Rikarin.Skala.Analysis.Loading;
using Rikarin.Skala.Core.Diagnostics;
using Rikarin.Skala.Formatting.CSharp.Arrangement;
using Rikarin.Skala.Reporting;

namespace Rikarin.Skala.Analysis.Tests;

/// <summary>
///     #343: a framework-dependent rule may not answer for the one moniker it happens to run in.
/// </summary>
/// <remarks>
///     ⚠ <b>The suite had no fixture project with more than one <c>TargetFramework</c> at all</b>,
///     which is why this class exists rather than another case in an existing one.
///     <c>System.Threading.Lock</c> is <c>net9.0</c>+; <c>MSBuildWorkspace</c> and a binlog both open
///     a multi-targeted project as one compilation per moniker; <c>CheckCommand</c> unions the
///     findings. So <c>SK1023</c> fired from the <c>net10.0</c> half, <c>skala fix --safe</c> applied
///     its rewrite to the single source file both halves compile, and the <c>netstandard2.1</c> half —
///     the one that goes into Unity/IL2CPP — stopped building with <c>CS0234</c>.
///     <para>
///         ⚠ <b>Sabotage, and it was run:</b> reverting
///         <c>DedicatedLockAnalyzer.Analyze</c>'s <c>unavailable.Contains</c> guard (or
///         <c>ProjectLoader</c>'s call to <see cref="MultiTargetLink.Apply" />) turns
///         <see cref="MultiTargetedProject_WithholdsALockRewriteNoOlderFrameworkCanCompile" /> red on
///         both halves: the finding comes back and the rewritten file no longer compiles under
///         <c>netstandard2.1</c>.
///     </para>
/// </remarks>
[Collection(SerialWorkspace.Name)]
public sealed class MultiTargetAvailabilityTests {
    /// <summary>⚠ One constant, not six literals: <c>SK7083</c>'s threshold is five per file.</summary>
    const string Rule = "SK1023";

    const string MultiTargeted = """
                                 <Project Sdk="Microsoft.NET.Sdk">
                                   <PropertyGroup>
                                     <TargetFrameworks>netstandard2.1;net10.0</TargetFrameworks>
                                     <ImplicitUsings>enable</ImplicitUsings>
                                     <LangVersion>preview</LangVersion>
                                   </PropertyGroup>
                                 </Project>
                                 """;

    const string SingleTargeted = """
                                  <Project Sdk="Microsoft.NET.Sdk">
                                    <PropertyGroup>
                                      <TargetFramework>net10.0</TargetFramework>
                                      <ImplicitUsings>enable</ImplicitUsings>
                                      <LangVersion>preview</LangVersion>
                                    </PropertyGroup>
                                  </Project>
                                  """;

    /// <summary>The issue's reproduction, unchanged.</summary>
    const string Source = """
                          namespace Probe;

                          public sealed class Counter {
                              readonly object gate = new();
                              int value;

                              public int Next() {
                                  lock (gate) {
                                      return ++value;
                                  }
                              }
                          }

                          """;

    [Fact]
    public void MultiTargetedProject_WithholdsALockRewriteNoOlderFrameworkCanCompile() {
        using var scratch = new Scratch();
        var project = scratch.Write("Probe.csproj", MultiTargeted);
        var source = scratch.Write("Probe.cs", Source);
        Restore(project);

        var loaded = ProjectLoader.Load(
            new LoadRequest {
                RepositoryRoot = scratch.Root,
                Mode = LoadMode.Workspace,
                ProjectPath = project,
                Paths = [scratch.Root],
                AllowFallback = false
            },
            TestContext.Current.CancellationToken
        );

        // ⚠ The instrument, before the claim. Every assertion below is vacuous if the workspace
        // handed back one moniker, or handed back two with no references in them: a compilation with
        // nothing referenced says "no System.Threading.Lock" for the wrong reason and would make this
        // test pass over the unfixed analyzer.
        Assert.Equal(2, loaded.Units.Length);
        Assert.All(loaded.Units, static unit => Assert.Single(unit.Siblings));
        var old = Assert.Single(
            loaded.Units,
            static unit => unit.TargetFramework.StartsWith(
                "netstandard",
                StringComparison.OrdinalIgnoreCase
            )
        );

        var current = Assert.Single(loaded.Units, unit => !ReferenceEquals(unit, old));
        Assert.NotNull(old.Compilation.GetTypeByMetadataName("System.Threading.Monitor"));
        Assert.Null(old.Compilation.GetTypeByMetadataName("System.Threading.Lock"));
        Assert.NotNull(current.Compilation.GetTypeByMetadataName("System.Threading.Lock"));

        var request = new CheckRequest {
            RepositoryRoot = scratch.Root,
            Paths = [scratch.Root],
            Mode = LoadMode.Workspace,
            ProjectPath = project,
            Output = string.Empty,
            Rules = [Rule],
            NoCache = true
        };

        var (result, report) = CheckCommand.Run(request, TestContext.Current.CancellationToken);
        Assert.NotEqual(ExitCodes.LoadFailure, result.ExitCode);
        Assert.DoesNotContain(report.Reportable, static finding => finding.RuleId == Rule);

        // `fix --safe` has nothing to apply, so the file is untouched and every moniker still builds.
        var fixResult = FixCommand.Run(
            new FixRequest {
                RepositoryRoot = scratch.Root,
                Paths = [scratch.Root],
                Mode = LoadMode.Workspace,
                ProjectPath = project,
                SafeOnly = true,
                Include = [Rule]
            },
            TestContext.Current.CancellationToken
        );

        Assert.Equal(ExitCodes.Ok, fixResult.ExitCode);
        Assert.Equal(Source, File.ReadAllText(source));
        AssertEveryTargetFrameworkCompiles(scratch.Root, project, source);
    }

    /// <summary>
    ///     ⚠ The other half of the pair: the rule must not have been disabled into silence.
    /// </summary>
    /// <remarks>
    ///     A zero from a withheld finding and a zero from a dead rule are the same zero. This is the
    ///     same source, the same command and the same fix — with one <c>TargetFramework</c> — and it
    ///     must still fire, still apply, and still leave the tree in a shape <c>arrange</c> and
    ///     <c>format</c> agree with (#343's secondary defect: <c>fix --safe</c> then <c>verify</c> is a
    ///     fixpoint).
    /// </remarks>
    [Fact]
    public void SingleTargetedProject_StillReportsAndFixesInTheShapeArrangeLeavesAlone() {
        using var scratch = new Scratch();
        var project = scratch.Write("Probe.csproj", SingleTargeted);
        var source = scratch.Write("Probe.cs", Source);

        var request = new CheckRequest {
            RepositoryRoot = scratch.Root,
            Paths = [scratch.Root],
            Mode = LoadMode.Workspace,
            ProjectPath = project,
            Output = string.Empty,
            Rules = [Rule],
            NoCache = true
        };

        var (_, report) = CheckCommand.Run(request, TestContext.Current.CancellationToken);
        var finding = Assert.Single(report.Reportable, static entry => entry.RuleId == Rule);
        Assert.True(finding.HasFix);

        var fixResult = FixCommand.Run(
            new FixRequest {
                RepositoryRoot = scratch.Root,
                Paths = [scratch.Root],
                Mode = LoadMode.Workspace,
                ProjectPath = project,
                SafeOnly = true,
                Include = [Rule]
            },
            TestContext.Current.CancellationToken
        );

        Assert.Equal(ExitCodes.Ok, fixResult.ExitCode);

        // ⚠ `Lock`, not `global::System.Threading.Lock`, and `new()`, not `new Lock()`. The long form
        // compiled and was then reported by `arrange --check` as `SK0203 target-typed new` on a file
        // `fix` had just written, so `fix --safe` did not converge with `verify`.
        var rewritten = File.ReadAllText(source);
        Assert.Contains("readonly Lock gate = new();", rewritten, StringComparison.Ordinal);
        Assert.DoesNotContain("global::", rewritten, StringComparison.Ordinal);

        // ⚠ With the compilations supplied, because `ObjectCreationRule.NeedsSemantics` is true and a
        // syntactic-only `arrange` never asks the question — it would pass over the long form too.
        var reloaded = ProjectLoader.Load(
            new LoadRequest {
                RepositoryRoot = scratch.Root,
                Mode = LoadMode.Workspace,
                ProjectPath = project,
                Paths = [scratch.Root],
                AllowFallback = false
            },
            TestContext.Current.CancellationToken
        );

        var arranged = ArrangeCommand.Run(
            new ArrangeRequest {
                Paths = [scratch.Root],
                RepositoryRoot = scratch.Root,
                Check = true,
                Quiet = true,
                Compilations = _ => [.. reloaded.Units.Select(static unit => unit.Compilation)]
            },
            TestContext.Current.CancellationToken
        );

        Assert.Equal(ExitCodes.Ok, arranged.ExitCode);
        AssertEveryTargetFrameworkCompiles(scratch.Root, project, source);
    }

    /// <summary>
    ///     ⚠
    ///     <b>
    ///         The one fixture in this suite that has to restore, and skipping it would have made
    ///         this test pass over the unfixed analyzer.
    ///     </b>
    /// </summary>
    /// <remarks>
    ///     <c>net10.0</c>'s reference assemblies come from the installed SDK's <c>packs</c> directory,
    ///     which is why every other scratch project here loads without a restore.
    ///     <c>netstandard2.1</c>'s come from the <c>NETStandard.Library.Ref</c> NuGet package and
    ///     nowhere else — measured: without this call the <c>netstandard2.1</c> compilation comes back
    ///     with <b>no references at all</b>, so <c>System.Threading.Lock</c> is absent from it for a
    ///     reason that has nothing to do with the framework, and the assertions below would hold over
    ///     an analyzer that never consulted the sibling. The <c>Assert.NotNull</c> on
    ///     <c>System.Threading.Monitor</c> is what makes that failure loud rather than green.
    /// </remarks>
    static void Restore(string project) {
        using var process = System.Diagnostics.Process.Start(
            new System.Diagnostics.ProcessStartInfo("dotnet") {
                ArgumentList = { "restore", project, "--nologo" },
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            }
        )!;

        var output = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
        process.WaitForExit();
        Assert.True(process.ExitCode == 0, "restoring the multi-targeted fixture failed:\n" + output);
    }

    /// <summary>
    ///     Every moniker's compiler, over the file on disk — which is what "and then builds every TFM"
    ///     means without a second shell-out per assertion.
    /// </summary>
    /// <remarks>
    ///     ⚠ Scoped to diagnostics <em>in the fixture file</em>. An unrestored scratch project carries
    ///     its own project-level noise, and asserting over the whole compilation would make this
    ///     assertion about MSBuild rather than about the rewrite. <c>CS0234</c> — the issue's symptom —
    ///     is reported at the type name in <c>Probe.cs</c>, so this is exactly where it would appear.
    /// </remarks>
    static void AssertEveryTargetFrameworkCompiles(string root, string project, string source) {
        var loaded = ProjectLoader.Load(
            new LoadRequest {
                RepositoryRoot = root,
                Mode = LoadMode.Workspace,
                ProjectPath = project,
                Paths = [root],
                AllowFallback = false
            },
            TestContext.Current.CancellationToken
        );

        Assert.NotEmpty(loaded.Units);
        foreach (var unit in loaded.Units) {
            var errors = unit.Compilation.GetDiagnostics(TestContext.Current.CancellationToken)
                .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error
                    && string.Equals(
                        diagnostic.Location.SourceTree?.FilePath,
                        source,
                        StringComparison.Ordinal
                    )
                )
                .ToArray();

            Assert.True(
                errors.Length == 0,
                unit.Name
                + " ("
                + unit.TargetFramework
                + "): "
                + string.Join("; ", errors.Select(static d => d.ToString()))
            );
        }
    }
}
