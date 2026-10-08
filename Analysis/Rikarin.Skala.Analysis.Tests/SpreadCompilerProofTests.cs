using Microsoft.CodeAnalysis.CSharp;
using Rikarin.Skala.Analysis.Hosting;
using Rikarin.Skala.Analysis.Loading;
using Rikarin.Skala.Core.Configuration;
using Rikarin.Skala.Core.Diagnostics;
using Rikarin.Skala.Reporting;

namespace Rikarin.Skala.Analysis.Tests;

/// <summary>
///     #515: <c>SK1133</c> takes a <c>net10.0</c> reference set as proof of the compiler, on real projects
///     the SDK resolved — the fixture harness only stands in for them.
/// </summary>
/// <remarks>
///     ⚠ Each scratch project writes its own empty <c>Directory.Build.props</c>, so nothing above it can
///     supply a <c>&lt;LangVersion&gt;</c> and turn a <c>latest</c> fixture into a written one.
/// </remarks>
[Collection(SerialWorkspace.Name)]
public sealed class SpreadCompilerProofTests {
    /// <summary>⚠ One constant: the id is a mirror of <c>RuleIds.CollectionExpressionSpread</c>.</summary>
    const string CollectionExpressionSpread = "SK1133";

    const string Source = """
                          using System.Collections.Generic;
                          using System.Linq;

                          namespace Probe;

                          public sealed class Site {
                              public int[] Copy(List<int> list) {
                                  int[] copied = list.ToArray();
                                  return copied;
                              }
                          }

                          """;

    /// <summary>
    ///     ⚠ The net9.0 targeting pack pinned to 9.0.19, and no transitive packs: restore then needs
    ///     exactly the package the Rules tests already download, and nothing else.
    /// </summary>
    const string NineReferences = """
                                    <ItemGroup>
                                      <FrameworkReference Update="Microsoft.NETCore.App" TargetingPackVersion="9.0.19" />
                                    </ItemGroup>
                                    <PropertyGroup>
                                      <DisableTransitiveFrameworkReferenceDownloads>true</DisableTransitiveFrameworkReferenceDownloads>
                                    </PropertyGroup>
                                  """;

    /// <summary>
    ///     ⚠ <b><c>netstandard2.1;net10.0</c> at <c>latest</c>: both legs are C# 14 to Skala</b>, so
    ///     <c>MultiTargetLanguageFloor</c> withholds nothing, and only the analyzer's own sibling question
    ///     stands between the <c>net10.0</c> leg's proof and a file the <c>netstandard2.1</c> leg compiles.
    /// </summary>
    [Fact]
    public void MultiTargetedAtLatest_WithholdsTheSpreadTheNetstandardLegCannotProve() {
        using var scratch = new Scratch();
        var project = Project(scratch, "<TargetFrameworks>netstandard2.1;net10.0</TargetFrameworks>", "latest");
        var source = scratch.Write("Probe.cs", Source);
        MultiTargetAvailabilityTests.Restore(project);

        var loaded = Load(scratch, project);

        // The instrument, before the claim: two monikers that know each other, both at C# 14, and the
        // older one's core library really is netstandard's — a leg with no references would decline for
        // the wrong reason.
        Assert.Equal(2, loaded.Units.Length);
        Assert.All(loaded.Units, static unit => Assert.Single(unit.Siblings));
        Assert.All(
            loaded.Units,
            static unit => Assert.True(unit.Compilation.LanguageVersion >= LanguageVersion.CSharp14)
        );
        Assert.Equal(
            ["System.Runtime 10", "netstandard 2"],
            loaded.Units.Select(static unit => CoreLibrary(unit.Compilation)).Order(StringComparer.Ordinal)
        );

        var (result, report) = CheckCommand.Run(Check(scratch, project), TestContext.Current.CancellationToken);
        Assert.NotEqual(ExitCodes.LoadFailure, result.ExitCode);
        Assert.DoesNotContain(report.Reportable, static finding => finding.RuleId == CollectionExpressionSpread);

        Assert.Equal(ExitCodes.Ok, Fix(scratch, project).ExitCode);
        Assert.Equal(Source, File.ReadAllText(source));
    }

    /// <summary>
    ///     The other half: the same file and the same <c>latest</c>, with <c>net10.0</c> alone — reported,
    ///     fixed, and still compiling.
    /// </summary>
    [Fact]
    public void SingleTargetedNet10AtLatest_ReportsAndFixesTheSpread() {
        using var scratch = new Scratch();
        var project = Project(scratch, "<TargetFramework>net10.0</TargetFramework>", "latest");
        var source = scratch.Write("Probe.cs", Source);

        var (_, report) = CheckCommand.Run(Check(scratch, project), TestContext.Current.CancellationToken);
        Assert.True(Assert.Single(report.Reportable, static f => f.RuleId == CollectionExpressionSpread).HasFix);

        Assert.Equal(ExitCodes.Ok, Fix(scratch, project).ExitCode);
        Assert.Contains("int[] copied = [..list];", File.ReadAllText(source), StringComparison.Ordinal);
        MultiTargetAvailabilityTests.AssertEveryTargetFrameworkCompiles(scratch.Root, project, source);
    }

    /// <summary>
    ///     ⚠ <c>net9.0</c> at <c>latest</c> is SDK 9.0.1xx's Roslyn 4.12 in the field, so it is declined —
    ///     and the same project with a written <c>14</c> is the control that the rule runs on it at all.
    /// </summary>
    [Theory]
    [InlineData("latest", false)]
    [InlineData("14.0", true)]
    public void Net9_IsProvedOnlyByAWrittenVersion(string languageVersion, bool fires) {
        using var scratch = new Scratch();
        var project = Project(scratch, "<TargetFramework>net9.0</TargetFramework>", languageVersion, NineReferences);
        scratch.Write("Probe.cs", Source);
        MultiTargetAvailabilityTests.Restore(project);

        var unit = Assert.Single(Load(scratch, project).Units);
        Assert.Equal("System.Runtime 9", CoreLibrary(unit.Compilation));

        var (_, report) = CheckCommand.Run(Check(scratch, project), TestContext.Current.CancellationToken);
        Assert.Equal(fires, report.Reportable.Any(static f => f.RuleId == CollectionExpressionSpread));
    }

    /// <summary>
    ///     ⚠ <c>--load=loose</c> knows nothing about the build, and SK1133 does not run there: it needs a
    ///     semantic model, and the loose compilation's core library is the running runtime's
    ///     <c>System.Private.CoreLib</c>, which the proof refuses anyway.
    /// </summary>
    [Fact]
    public void LooseMode_SkipsTheRuleOverAProjectThatWouldProveIt() {
        using var scratch = new Scratch();
        Project(scratch, "<TargetFramework>net10.0</TargetFramework>", "latest");
        scratch.Write("Probe.cs", Source);

        Assert.Contains(
            AnalyzerHost.SkippedFor(LoadMode.Loose),
            static rule => rule.RuleId == CollectionExpressionSpread
        );

        var (_, report) = CheckCommand.Run(
            new CheckRequest {
                RepositoryRoot = scratch.Root,
                Paths = [scratch.Root],
                Mode = LoadMode.Loose,
                Output = string.Empty,
                Rules = [CollectionExpressionSpread],
                NoCache = true
            },
            TestContext.Current.CancellationToken
        );

        Assert.DoesNotContain(report.Reportable, static finding => finding.RuleId == CollectionExpressionSpread);
    }

    static string Project(Scratch scratch, string frameworks, string languageVersion, string extra = "") {
        scratch.Write("Directory.Build.props", "<Project />");
        scratch.Write("Directory.Build.targets", "<Project />");
        return scratch.Write(
            "Probe.csproj",
            $"""
             <Project Sdk="Microsoft.NET.Sdk">
               <PropertyGroup>
                 {frameworks}
                 <LangVersion>{languageVersion}</LangVersion>
               </PropertyGroup>
             {extra}
             </Project>
             """
        );
    }

    static LoadedProject Load(Scratch scratch, string project) =>
        ProjectLoader.Load(
            new LoadRequest {
                RepositoryRoot = scratch.Root,
                Mode = LoadMode.Workspace,
                ProjectPath = project,
                Paths = [scratch.Root],
                AllowFallback = false
            },
            TestContext.Current.CancellationToken
        );

    static CheckRequest Check(Scratch scratch, string project) =>
        new() {
            RepositoryRoot = scratch.Root,
            Paths = [scratch.Root],
            Mode = LoadMode.Workspace,
            ProjectPath = project,
            Output = string.Empty,
            Rules = [CollectionExpressionSpread],
            NoCache = true
        };

    static CommandResult Fix(Scratch scratch, string project) =>
        FixCommand.Run(
            new FixRequest {
                RepositoryRoot = scratch.Root,
                Paths = [scratch.Root],
                Mode = LoadMode.Workspace,
                ProjectPath = project,
                SafeOnly = true,
                Include = [CollectionExpressionSpread]
            },
            TestContext.Current.CancellationToken
        );

    static string CoreLibrary(Microsoft.CodeAnalysis.Compilation compilation) {
        var identity = compilation.ObjectType.ContainingAssembly.Identity;
        return identity.Name + " " + identity.Version.Major;
    }
}
