using Microsoft.CodeAnalysis;
using Rikarin.Skala.Analysis.Hosting;
using Rikarin.Skala.Analysis.Loading;
using Rikarin.Skala.Core.Diagnostics;
using Rikarin.Skala.Reporting;
using System.Collections.Immutable;

namespace Rikarin.Skala.Analysis.Tests;

/// <summary>
///     Roslyn's unused-code and primary-constructor analyzers, hosted with defaults a project overrides
///     (docs/plan/08 § "Roslyn's unused-code and primary-constructor analyzers").
/// </summary>
/// <remarks>
///     ⚠ Every end-to-end test here runs with <b>no configuration at all</b> unless it writes one: the
///     scratch tree carries empty <c>Directory.Build.*</c> files so that nothing above it in the temp
///     directory can reach the project, and no <c>.editorconfig</c> unless the test is about one. The
///     defaults are the subject, and a stray configuration would make every assertion about them
///     vacuous.
/// </remarks>
[Collection(SerialWorkspace.Name)]
public sealed class HostedRoslynDefaultsTests {
    const string Project = """
                           <Project Sdk="Microsoft.NET.Sdk">
                             <PropertyGroup>
                               <TargetFramework>net10.0</TargetFramework>
                               <Nullable>enable</Nullable>
                               <ImplicitUsings>enable</ImplicitUsings>
                             </PropertyGroup>
                           </Project>
                           """;

    /// <summary>
    ///     One of every shape the hosted ids are about, plus an unread parameter and a discarded call
    ///     value — the shapes <c>IDE0060</c> and <c>IDE0058</c> report, measured to fire when a project
    ///     asks for them, so that "they stay silent" is a claim about the filter and not about the code.
    /// </summary>
    const string Shapes = """
                          namespace Scratch;

                          public sealed class Widget {
                              readonly string name;
                              int written;

                              public Widget(string name) {
                                  this.name = name;
                              }

                              public string Name => name;

                              void NeverCalled() => Console.WriteLine("never");

                              public void Record(int value) {
                                  written = value;
                              }

                              public int Run() {
                                  var x = Compute();
                                  x = 2;
                                  return x;
                              }

                              static int Compute() => Environment.TickCount;

                              public void OnClick(object? sender, EventArgs e) => Console.WriteLine("click");

                              public int Constant(int ignored) => 2;

                              public void Discard() {
                                  Compute();
                              }
                          }
                          """;

    const string OptIn = """
                         root = true

                         [*.cs]
                         dotnet_diagnostic.IDE0051.severity = warning
                         dotnet_diagnostic.IDE0052.severity = warning
                         dotnet_diagnostic.IDE0059.severity = warning
                         dotnet_diagnostic.IDE0060.severity = warning
                         dotnet_diagnostic.IDE0058.severity = warning
                         """;

    static readonly string[] Hosted = ["IDE0051", "IDE0052", "IDE0058", "IDE0059", "IDE0060", "IDE0290"];

    /// <summary>
    ///     ⚠ The request this exists for: no <c>.editorconfig</c>, no <c>skala config init</c>, and
    ///     <c>IDE0290</c> reports as a suggestion. The three unused-code ids stay silent because they
    ///     were measured off, and <c>IDE0060</c> and <c>IDE0058</c> because they are never Skala's.
    /// </summary>
    [Fact]
    public void WithNoConfiguration_IDE0290IsASuggestion_AndNothingElseReports() {
        var report = Check(null, Shapes);

        var primary = Assert.Single(report.Reportable, static finding => finding.RuleId == "IDE0290");
        Assert.Equal(SkalaSeverity.Info, primary.Severity);
        Assert.Equal(["IDE0290"], HostedIn(report));
    }

    [Fact]
    public void AnEditorConfigNone_SilencesTheDefault() {
        var report = Check(
            "root = true\n\n[*.cs]\ndotnet_diagnostic.IDE0290.severity = none\n",
            Shapes
        );

        Assert.Empty(HostedIn(report));
    }

    /// <summary>
    ///     ⚠ A section, not a global: Skala's default for these three is <c>none</c> at the global level
    ///     too, and Roslyn skips an analyzer whose every descriptor is globally off — unless a tree turns
    ///     one on. This is the test that the per-tree setting is still seen.
    /// </summary>
    [Fact]
    public void AnOptIn_ReportsTheUnusedCodeIds_ButNeverIDE0058OrIDE0060() {
        var report = Check(OptIn, Shapes);

        Assert.Equal(["IDE0051", "IDE0052", "IDE0059", "IDE0290"], HostedIn(report));
        Assert.Equal(
            SkalaSeverity.Warning,
            Assert.Single(report.Reportable, static finding => finding.RuleId == "IDE0059").Severity
        );
        Assert.Contains("NeverCalled", Assert.Single(report.Reportable, static f => f.RuleId == "IDE0051").Message);
    }

    /// <summary>
    ///     The compiler's warning and the hosted id state one fact on one span, so the compiler's is kept.
    /// </summary>
    [Fact]
    public void AnOptedInId_ThatTheCompilerAlsoReports_IsSuperseded() {
        var report = Check(
            OptIn,
            """
            namespace Scratch;

            public sealed class Holder {
                int never;

                public int Run() {
                    int constant = 5;
                    return 1;
                }
            }
            """
        );

        Assert.Contains(report.Reportable, static finding => finding.RuleId == "CS0169");
        Assert.Contains(report.Reportable, static finding => finding.RuleId == "CS0219");
        Assert.DoesNotContain(report.Reportable, static finding => finding.RuleId is "IDE0051" or "IDE0059");
        Assert.Equal(
            2,
            report.Findings.Count(static finding => finding.Suppression == SuppressionKind.Superseded)
        );
    }

    /// <summary><c>NoWarn</c> is the build saying something about the id, so it wins over the default.</summary>
    [Fact]
    public void NoWarn_WinsOverTheDefault() {
        var report = Check(
            null,
            Shapes,
            Project.Replace(
                "<Nullable>enable</Nullable>",
                "<Nullable>enable</Nullable>\n    <NoWarn>$(NoWarn);IDE0290</NoWarn>",
                StringComparison.Ordinal
            )
        );

        Assert.Empty(HostedIn(report));
    }

    /// <summary>
    ///     <c>skala fix --include</c> applies Roslyn's own fixer for each id that has one; none of them is
    ///     part of <c>--safe</c>.
    /// </summary>
    [Fact]
    public void Fix_AppliesRoslynsFixers_OnlyWhenIncluded() {
        using var scratch = Tree(OptIn, Shapes, Project, out var source, out _);

        var safe = FixCommand.Run(
            new() { RepositoryRoot = scratch.Root, Paths = [scratch.Root], Mode = LoadMode.Workspace },
            TestContext.Current.CancellationToken
        );
        Assert.True(safe.ExitCode == ExitCodes.Ok, safe.Output);

        // ⚠ `--safe` applies Skala's own safe fixes (one makes `NeverCalled` static) and none of these.
        var afterSafe = File.ReadAllText(source);
        Assert.Contains("public Widget(string name) {", afterSafe, StringComparison.Ordinal);
        Assert.Contains("NeverCalled", afterSafe, StringComparison.Ordinal);
        Assert.Contains("var x = Compute();", afterSafe, StringComparison.Ordinal);

        var result = FixCommand.Run(
            new() {
                RepositoryRoot = scratch.Root,
                Paths = [scratch.Root],
                Mode = LoadMode.Workspace,
                SafeOnly = false,
                Include = ["IDE0051", "IDE0059", "IDE0290"]
            },
            TestContext.Current.CancellationToken
        );

        Assert.True(result.ExitCode == ExitCodes.Ok, result.Output);
        Assert.Contains("applied 3 fixes", result.Output, StringComparison.Ordinal);
        var text = File.ReadAllText(source);
        Assert.Contains("class Widget(string name)", text, StringComparison.Ordinal);
        Assert.Contains("readonly string name = name;", text, StringComparison.Ordinal);
        Assert.DoesNotContain("NeverCalled", text, StringComparison.Ordinal);
        Assert.Contains("_ = Compute();", text, StringComparison.Ordinal);

        // ⚠ The unread parameter is what IDE0060 would remove, and the config asked for it; it is
        // silenced, so it is neither reported nor fixed.
        Assert.Contains("Constant(int ignored)", text, StringComparison.Ordinal);
    }

    /// <summary>
    ///     A warm run after another file changed reports exactly what a cold one does — including an
    ///     <c>IDE0051</c> whose only caller was just deleted from the <em>other</em> part of a partial type.
    /// </summary>
    /// <remarks>
    ///     ⚠ <b>This does not prove the per-file partition, and it was sabotage-checked to find out.</b>
    ///     Adding <c>IDE0051</c>/<c>IDE0052</c> to <c>AnalyzerHost.PerFileRoslynIds</c> leaves it green:
    ///     #516 re-runs the semantic half of every unchanged file whenever anything in the compilation
    ///     changed, and Roslyn's tree-scoped run of the unused-members analyzer sees the other part of
    ///     the type. The partition stays conservative because nothing measured that this holds for every
    ///     shape; this test holds the outcome, not the mechanism.
    /// </remarks>
    [Fact]
    public void AWarmRun_AfterAnotherFileChanged_ReportsWhatAColdOneDoes() {
        using var scratch = Tree(OptIn, Shapes, Project, out _, out var project);
        scratch.Write(
            "Split.cs",
            "namespace Scratch;\n\npublic partial class Split {\n    void Helper() { }\n}\n"
        );
        var caller = scratch.Write(
            "Caller.cs",
            "namespace Scratch;\n\npublic partial class Split {\n    public void Go() => Helper();\n}\n"
        );

        var first = Run(scratch, project, false);
        Assert.DoesNotContain(first, static line => line.StartsWith("IDE0051 Split.cs", StringComparison.Ordinal));
        Assert.Contains("IDE0059 Widget.cs:20", first);

        File.WriteAllText(caller, "namespace Scratch;\n\npublic partial class Split {\n    public void Go() { }\n}\n");
        var warm = Run(scratch, project, false);
        var cold = Run(scratch, project, true);

        Assert.Equal(cold, warm);
        Assert.Contains("IDE0051 Split.cs:4", warm);
        Assert.Contains("IDE0059 Widget.cs:20", warm);
    }

    static string[] Run(Scratch scratch, string project, bool noCache) {
        var (_, report) = CheckCommand.Run(
            new() {
                RepositoryRoot = scratch.Root,
                Paths = [scratch.Root],
                Mode = LoadMode.Workspace,
                ProjectPath = project,
                Output = string.Empty,
                NoCache = noCache,
                IncludeFormatting = false
            },
            TestContext.Current.CancellationToken
        );

        return [
            ..report.Reportable.Where(static finding => finding.RuleId.StartsWith("IDE", StringComparison.Ordinal))
                .Select(static finding => $"{finding.RuleId} {Path.GetFileName(finding.Path)}:{finding.Line}")
                .Order(StringComparer.Ordinal)
        ];
    }

    [Fact]
    public void ALooseLoad_ListsEveryHostedIdAsSkipped() {
        var skipped = AnalyzerHost.SkippedFor(LoadMode.Loose).Select(static rule => rule.RuleId).ToHashSet();

        foreach (var id in RoslynCodeStyle.HostedIds) {
            Assert.Contains(id, skipped);
        }
    }

    [Fact]
    public void TheSilencedIds_AreExactlyTheUnhostedOnesTheSelectedAnalyzersCarry() {
        var codeStyle = RoslynCodeStyle.Load();

        Assert.Empty(codeStyle.Diagnostics);
        Assert.Equal(["IDE0058", "IDE0060"], codeStyle.SilencedIds.Order(StringComparer.Ordinal));
        Assert.Equal(
            ["IDE0051", "IDE0059", "IDE0290", "IDE1006"],
            codeStyle.Fixers.Keys.Order(StringComparer.Ordinal)
        );
    }

    /// <summary>Each setting a project can make about an id outranks the default, and none is needed.</summary>
    [Theory]
    [InlineData("IDE0290", null, null, true, ReportDiagnostic.Info)]
    [InlineData("IDE0059", null, null, true, ReportDiagnostic.Suppress)]
    [InlineData("IDE0060", null, null, true, ReportDiagnostic.Suppress)]
    [InlineData("IDE0290", "csharp_style_prefer_primary_constructors", "true:none", true, ReportDiagnostic.Suppress)]
    [InlineData("IDE0290", "csharp_style_prefer_primary_constructors", "true:warning", true, ReportDiagnostic.Warn)]
    [InlineData("IDE0290", "csharp_style_prefer_primary_constructors", "true", true, ReportDiagnostic.Info)]
    [InlineData("IDE0059", "csharp_style_unused_value_assignment_preference", "discard_variable:warning", true, ReportDiagnostic.Warn)]
    [InlineData("IDE0290", "dotnet_analyzer_diagnostic.category-Style.severity", "none", false, ReportDiagnostic.Default)]
    [InlineData("IDE0290", "dotnet_analyzer_diagnostic.severity", "warning", false, ReportDiagnostic.Default)]
    [InlineData("SK1002", null, null, false, ReportDiagnostic.Default)]
    public void TheDefault_IsTheLowestPrecedence(
        string id,
        string? key,
        string? value,
        bool answers,
        ReportDiagnostic expected
    ) {
        var defaults = new HostedSeverityDefaults(ImmutableDictionary<string, ReportDiagnostic>.Empty);
        var options = key is null
            ? ImmutableDictionary<string, string>.Empty
            : ImmutableDictionary<string, string>.Empty.Add(key.ToLowerInvariant(), value!);

        Assert.Equal(answers, defaults.TryGet(id, options, null, out var severity));
        if (answers) {
            Assert.Equal(expected, severity);
        }
    }

    [Fact]
    public void TheCompilersOwnOptions_OutrankTheDefault() {
        var defaults = new HostedSeverityDefaults(
            ImmutableDictionary<string, ReportDiagnostic>.Empty.Add("IDE0290", ReportDiagnostic.Suppress)
        );

        Assert.False(defaults.TryGet("IDE0290", null, null, out _));
    }

    static RunReport Check(string? editorConfig, string source, string project = Project) {
        using var scratch = Tree(editorConfig, source, project, out _, out var projectPath);
        var (_, report) = CheckCommand.Run(
            new() {
                RepositoryRoot = scratch.Root,
                Paths = [scratch.Root],
                Mode = LoadMode.Workspace,
                ProjectPath = projectPath,
                Output = string.Empty,
                NoCache = true,
                IncludeFormatting = false
            },
            TestContext.Current.CancellationToken
        );

        return report;
    }

    static Scratch Tree(string? editorConfig, string source, string project, out string sourcePath, out string projectPath) {
        var scratch = new Scratch();
        scratch.Write("Directory.Build.props", "<Project />");
        scratch.Write("Directory.Build.targets", "<Project />");
        if (editorConfig is not null) {
            scratch.Write(".editorconfig", editorConfig);
        }

        sourcePath = scratch.Write("Widget.cs", source);
        projectPath = scratch.Write("Scratch.csproj", project);
        return scratch;
    }

    static string[] HostedIn(RunReport report) =>
        [
            ..report.Reportable.Select(static finding => finding.RuleId)
                .Where(static id => Hosted.Contains(id))
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
        ];
}
