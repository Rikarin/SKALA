using Rikarin.Skala.Core.Diagnostics;
using Rikarin.Skala.Reporting;

namespace Rikarin.Skala.Analysis.Tests;

public sealed class ModernizationIntegrationTests {
    [Fact]
    public void Workspace_CheckVerifyAndWarmCacheHonorAllFiveRulesAndPerFilePolicy() {
        using var scratch = new Scratch();
        var project = scratch.Write(
            "Scratch.csproj",
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup><TargetFramework>net10.0</TargetFramework><Nullable>enable</Nullable></PropertyGroup>
            </Project>
            """
        );
        const string configuration = """
                                     root = true
                                     [*.cs]
                                     resharper_configure_await_analysis_mode = library
                                     dotnet_code_quality.SK7030.threshold = 8
                                     dotnet_diagnostic.SK7030.severity = warning
                                     [Ui.cs]
                                     resharper_configure_await_analysis_mode = ui
                                     [Suppressed.cs]
                                     dotnet_diagnostic.SK3003.severity = none
                                     """;
        scratch.Write(".editorconfig", configuration);
        scratch.Write(
            "Checks.cs",
            """
            using System;
            using System.Text;
            using System.Threading.Tasks;
            class Item { public int Count; }
            class Checks {
                bool Range(int x) => x >= 0 && x < 10;
                bool Property(Item? item) => item != null && item.Count == 3;
                string Decode(ReadOnlySpan<byte> bytes) => Encoding.UTF8.GetString(bytes.Slice(1).ToArray());
                async Task Await(Task task) { await task; }
            }
            """
        );
        scratch.Write("Ui.cs", "using System.Threading.Tasks; class Ui { async Task M(Task t) { await t; } }");
        scratch.Write(
            "Suppressed.cs",
            "using System.Threading.Tasks; class Suppressed { async Task M(Task t) { await t; } }"
        );
        string[] ids = ["SK1011", "SK1014", "SK1028", "SK3003", "SK7030"];
        var request = new CheckRequest {
            RepositoryRoot = scratch.Root,
            Paths = [scratch.Root],
            Mode = LoadMode.Workspace,
            ProjectPath = project,
            Output = string.Empty,
            Rules = ids,
            NoCache = false
        };
        var (result, report) = CheckCommand.Run(request, TestContext.Current.CancellationToken);
        Assert.NotEqual(ExitCodes.LoadFailure, result.ExitCode);
        Assert.Equal(LoadMode.Workspace, report.Mode);
        foreach (var id in ids) {
            var finding = Assert.Single(report.Reportable, finding => finding.RuleId == id);
            Assert.Equal("Checks.cs", Path.GetFileName(finding.Path));
            Assert.Equal(id is "SK1011" or "SK1014" or "SK1028", finding.HasFix);
        }

        // An unrelated changed tree must not make unchanged syntax/semantic findings disappear.
        scratch.Write(
            "Ui.cs",
            "using System.Threading.Tasks; class Ui { int Count; async Task M(Task t) { await t; } }"
        );
        var (_, warm) = CheckCommand.Run(request, TestContext.Current.CancellationToken);
        var (_, cold) = CheckCommand.Run(request with { NoCache = true }, TestContext.Current.CancellationToken);
        Assert.Equal(Describe(cold), Describe(warm));
        Assert.Equal(5, warm.Reportable.Count(finding => ids.Contains(finding.RuleId)));

        var verified = VerifyCommand.Run(
            new VerifyRequest {
                RepositoryRoot = scratch.Root,
                Paths = [scratch.Root],
                Mode = LoadMode.Workspace,
                ProjectPath = project,
                NoCache = true
            },
            TestContext.Current.CancellationToken
        );
        Assert.NotEqual(ExitCodes.LoadFailure, verified.ExitCode);
        Assert.NotEqual(ExitCodes.Ok, verified.ExitCode);
        foreach (var id in ids) {
            Assert.Contains(id, verified.Output, StringComparison.Ordinal);
        }

        var fixedResult = FixCommand.Run(
            new FixRequest {
                RepositoryRoot = scratch.Root,
                Paths = [scratch.Root],
                Mode = LoadMode.Workspace,
                ProjectPath = project,
                SafeOnly = true,
                Include = ["SK1011", "SK1014", "SK1028"]
            },
            TestContext.Current.CancellationToken
        );
        Assert.Equal(ExitCodes.Ok, fixedResult.ExitCode);
        var (_, fixedReport) = CheckCommand.Run(request with { NoCache = true }, TestContext.Current.CancellationToken);
        Assert.DoesNotContain(
            fixedReport.Reportable,
            static finding => finding.RuleId is "SK1011" or "SK1014" or "SK1028"
        );
        Assert.Contains(fixedReport.Reportable, static finding => finding.RuleId == "SK3003");

        scratch.Write(".editorconfig", configuration.Replace("= library", "= disabled", StringComparison.Ordinal));
        Core.Configuration.ConfigurationCache.Clear();
        var (_, changed) = CheckCommand.Run(request, TestContext.Current.CancellationToken);
        Assert.DoesNotContain(changed.Reportable, static finding => finding.RuleId == "SK3003");
    }

    static string[] Describe(RunReport report) =>
        report.Reportable
            .Select(static finding => $"{finding.RuleId}:{finding.Path}:{finding.Line}:{finding.Column}:{finding.Message}"
            )
            .Order(StringComparer.Ordinal)
            .ToArray();
}
