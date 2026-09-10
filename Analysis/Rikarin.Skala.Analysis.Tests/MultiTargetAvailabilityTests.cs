using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
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
    // ⚠ Named for the concept, not for its role in this file. `ToolDiagnosticIdTests` allows one id
    // to be declared twice only when both declarations name the *same* concept — a mirror across an
    // assembly boundary rather than a collision — and it compares the constant's name to decide.
    // Named `Rule`, this and `LockAndValueBatchTests.DedicatedLock` read as one id with two meanings
    // and the ADR-012 guard fails — which is the guard working, on a genuine mirror.
    const string DedicatedLock = "SK1023";

    /// <summary>#351's rule: a <c>languageVersion</c> floor rather than a missing type.</summary>
    const string FileScopedNamespace = "SK1005";

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

    /// <summary>
    ///     ⚠ <b>No <c>&lt;LangVersion&gt;</c>, and that omission is the entire fixture (#351).</b>
    /// </summary>
    /// <remarks>
    ///     The pair above pins <c>preview</c> for both monikers, which is what a project does when it
    ///     wants one language across the board — and it hides this defect completely. Left unset, the
    ///     SDK picks a default <em>per target framework</em>: measured with
    ///     <c>dotnet msbuild -getProperty:LangVersion -p:TargetFramework=…</c> on exactly this file,
    ///     <c>netstandard2.0</c> evaluates to <b>7.3</b> and <c>net10.0</c> to <b>14.0</b>. So one
    ///     project over one source file has two language versions, and every <c>SK1xxx</c> rule that
    ///     opens with <c>SkalaRule.MeetsLanguageVersion</c> is answered by whichever moniker it runs
    ///     in.
    /// </remarks>
    const string MultiTargetedDefaultLanguage = """
                                                <Project Sdk="Microsoft.NET.Sdk">
                                                  <PropertyGroup>
                                                    <TargetFrameworks>netstandard2.0;net10.0</TargetFrameworks>
                                                  </PropertyGroup>
                                                </Project>
                                                """;

    const string SingleTargetedDefaultLanguage = """
                                                 <Project Sdk="Microsoft.NET.Sdk">
                                                   <PropertyGroup>
                                                     <TargetFramework>net10.0</TargetFramework>
                                                   </PropertyGroup>
                                                 </Project>
                                                 """;

    /// <summary>A file <c>SK1005</c> converts to a file-scoped namespace — C# 10 syntax.</summary>
    const string BlockNamespaceSource = """
                                        namespace Probe {
                                            public sealed class Widget {
                                                public int Value { get; set; }
                                            }
                                        }

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
            Rules = [DedicatedLock],
            NoCache = true
        };

        var (result, report) = CheckCommand.Run(request, TestContext.Current.CancellationToken);
        Assert.NotEqual(ExitCodes.LoadFailure, result.ExitCode);
        Assert.DoesNotContain(report.Reportable, static finding => finding.RuleId == DedicatedLock);

        // `fix --safe` has nothing to apply, so the file is untouched and every moniker still builds.
        var fixResult = FixCommand.Run(
            new FixRequest {
                RepositoryRoot = scratch.Root,
                Paths = [scratch.Root],
                Mode = LoadMode.Workspace,
                ProjectPath = project,
                SafeOnly = true,
                Include = [DedicatedLock]
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
            Rules = [DedicatedLock],
            NoCache = true
        };

        var (_, report) = CheckCommand.Run(request, TestContext.Current.CancellationToken);
        var finding = Assert.Single(report.Reportable, static entry => entry.RuleId == DedicatedLock);
        Assert.True(finding.HasFix);

        var fixResult = FixCommand.Run(
            new FixRequest {
                RepositoryRoot = scratch.Root,
                Paths = [scratch.Root],
                Mode = LoadMode.Workspace,
                ProjectPath = project,
                SafeOnly = true,
                Include = [DedicatedLock]
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
    ///     #351: the same union, decided by the <em>language version</em> rather than by a type.
    /// </summary>
    /// <remarks>
    ///     ⚠ <b>Roughly forty <c>SK1xxx</c> rules gate on
    ///     <c>SkalaRule.MeetsLanguageVersion</c> and not one of them was guarded</b>, so this is the
    ///     larger half of #343's bug class rather than a footnote to it. <c>SK1005</c> stands for all
    ///     of them: <c>hasFix</c>, <c>fixIsSafe</c>, floor C# 10. The <c>net10.0</c> moniker compiles
    ///     at C# 14 and reports it, <c>skala fix --safe</c> rewrites the block namespace to
    ///     <c>namespace Probe;</c>, and the <c>netstandard2.0</c> moniker — compiling the same file at
    ///     C# 7.3 — fails with <c>CS8773</c>.
    ///     <para>
    ///         ⚠ <b>Sabotage, and it was run:</b> reverting <c>CheckCommand</c>'s call to
    ///         <see cref="MultiTargetLanguageFloor.Filter" /> turns this red exactly as described —
    ///         the finding returns, <c>fix --safe</c> writes the file-scoped namespace, and
    ///         <see cref="AssertEveryTargetFrameworkCompiles" /> reports <c>CS8773</c> against the
    ///         <c>netstandard2.0</c> leg.
    ///     </para>
    /// </remarks>
    [Fact]
    public void MultiTargetedProject_WithholdsARewriteAnOlderMonikersLanguageVersionCannotParse() {
        using var scratch = new Scratch();
        var project = scratch.Write("Probe.csproj", MultiTargetedDefaultLanguage);
        var source = scratch.Write("Probe.cs", BlockNamespaceSource);
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

        // ⚠ The instrument, before the claim, and here it is the language version itself. If the
        // fixture ever acquires a `<LangVersion>` — from a stray Directory.Build.props above the
        // scratch directory, or from someone "tidying" the csproj to match the pair above — both
        // monikers compile at the same version, there is nothing to withhold, and every assertion
        // below passes over an unguarded pipeline. `Scratch` roots under the temp directory rather
        // than under the repository precisely so this cannot inherit Skala's own `latest`.
        Assert.Equal(2, loaded.Units.Length);
        Assert.All(loaded.Units, static unit => Assert.Single(unit.Siblings));
        var old = Assert.Single(
            loaded.Units,
            static unit => unit.TargetFramework.StartsWith("netstandard", StringComparison.OrdinalIgnoreCase)
        );

        var current = Assert.Single(loaded.Units, unit => !ReferenceEquals(unit, old));
        Assert.True(
            old.Compilation.LanguageVersion < LanguageVersion.CSharp10,
            "the netstandard2.0 moniker compiles at " + old.Compilation.LanguageVersion + ", so nothing is withheld"
        );

        Assert.True(
            current.Compilation.LanguageVersion >= LanguageVersion.CSharp10,
            "the net10.0 moniker compiles at " + current.Compilation.LanguageVersion + ", so the rule never fires"
        );

        // And the references really restored, so the older leg is a real compilation (#343's trap).
        Assert.NotNull(old.Compilation.GetTypeByMetadataName("System.Threading.Monitor"));

        var request = new CheckRequest {
            RepositoryRoot = scratch.Root,
            Paths = [scratch.Root],
            Mode = LoadMode.Workspace,
            ProjectPath = project,
            Output = string.Empty,
            Rules = [FileScopedNamespace],
            NoCache = true
        };

        var (result, report) = CheckCommand.Run(request, TestContext.Current.CancellationToken);
        Assert.NotEqual(ExitCodes.LoadFailure, result.ExitCode);
        Assert.DoesNotContain(report.Reportable, static finding => finding.RuleId == FileScopedNamespace);

        var fixResult = FixCommand.Run(
            new FixRequest {
                RepositoryRoot = scratch.Root,
                Paths = [scratch.Root],
                Mode = LoadMode.Workspace,
                ProjectPath = project,
                SafeOnly = true,
                Include = [FileScopedNamespace]
            },
            TestContext.Current.CancellationToken
        );

        Assert.Equal(ExitCodes.Ok, fixResult.ExitCode);
        Assert.Equal(BlockNamespaceSource, File.ReadAllText(source));
        AssertEveryTargetFrameworkCompiles(scratch.Root, project, source);
    }

    /// <summary>
    ///     ⚠ The other half of the pair: the language floor must not have silenced the rule outright.
    /// </summary>
    /// <remarks>
    ///     A zero from a withheld finding and a zero from a rule that no longer fires are the same
    ///     zero, and the filter added for #351 runs over every finding in the report. Same source,
    ///     same command, same absent <c>&lt;LangVersion&gt;</c> — with one <c>TargetFramework</c> that
    ///     defaults to C# 14 — and <c>SK1005</c> must still fire and still apply.
    /// </remarks>
    [Fact]
    public void SingleTargetedProject_StillConvertsTheNamespaceAtTheDefaultLanguageVersion() {
        using var scratch = new Scratch();
        var project = scratch.Write("Probe.csproj", SingleTargetedDefaultLanguage);
        var source = scratch.Write("Probe.cs", BlockNamespaceSource);

        var (_, report) = CheckCommand.Run(
            new CheckRequest {
                RepositoryRoot = scratch.Root,
                Paths = [scratch.Root],
                Mode = LoadMode.Workspace,
                ProjectPath = project,
                Output = string.Empty,
                Rules = [FileScopedNamespace],
                NoCache = true
            },
            TestContext.Current.CancellationToken
        );

        var finding = Assert.Single(report.Reportable, static entry => entry.RuleId == FileScopedNamespace);
        Assert.True(finding.HasFix);

        var fixResult = FixCommand.Run(
            new FixRequest {
                RepositoryRoot = scratch.Root,
                Paths = [scratch.Root],
                Mode = LoadMode.Workspace,
                ProjectPath = project,
                SafeOnly = true,
                Include = [FileScopedNamespace]
            },
            TestContext.Current.CancellationToken
        );

        Assert.Equal(ExitCodes.Ok, fixResult.ExitCode);
        Assert.Contains("namespace Probe;", File.ReadAllText(source), StringComparison.Ordinal);
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
