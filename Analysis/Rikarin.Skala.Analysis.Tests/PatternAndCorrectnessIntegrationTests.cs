using Rikarin.Skala.Core.Diagnostics;
using Rikarin.Skala.Reporting;

namespace Rikarin.Skala.Analysis.Tests;

public sealed class PatternAndCorrectnessIntegrationTests {
    [Fact]
    public void Workspace_CheckVerifySafeFixAndCacheHonorAllFiveRules() {
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
                                     dotnet_diagnostic.SK2001.severity = warning
                                     [Suppressed.cs]
                                     dotnet_diagnostic.SK2001.severity = none
                                     """;
        scratch.Write(".editorconfig", configuration);
        scratch.Write(
            "Checks.cs",
            """
            using System;
            using System.Text;
            class Checks {
                int Value { get; set; }
                int Select(int x) { if (x == 0) return 10; else if (x == 1) return 20; else return 30; }
                bool Elements(int[]? a) => a != null && a.Length == 2 && a[0] == 1;
                void Consume(ReadOnlySpan<byte> bytes) { }
                void Encode() => Consume(Encoding.UTF8.GetBytes("OK"));
                bool Range(byte x) => x >= 0;
                void Assign() { Value = Value; }
            }
            """
        );
        scratch.Write("Suppressed.cs", "class Suppressed { bool M(byte x) => x >= 0; }");
        scratch.Write("Unrelated.cs", "class Unrelated { }");
        string[] ids = ["SK1012", "SK1013", "SK1026", "SK2001", "SK2012"];
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
            Assert.Equal(id is "SK1012" or "SK1013" or "SK1026", finding.HasFix);
        }

        scratch.Write("Unrelated.cs", "class Unrelated { int Value { get; set; } }");
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
                Include = ["SK1012", "SK1013", "SK1026"]
            },
            TestContext.Current.CancellationToken
        );
        Assert.Equal(ExitCodes.Ok, fixedResult.ExitCode);
        var (_, fixedReport) = CheckCommand.Run(request with { NoCache = true }, TestContext.Current.CancellationToken);
        Assert.DoesNotContain(
            fixedReport.Reportable,
            static finding => finding.RuleId is "SK1012" or "SK1013" or "SK1026"
        );
        Assert.Single(fixedReport.Reportable, static finding => finding.RuleId == "SK2001");
        Assert.Single(fixedReport.Reportable, static finding => finding.RuleId == "SK2012");

        scratch.Write(".editorconfig", configuration.Replace("= warning", "= none", StringComparison.Ordinal));
        Core.Configuration.ConfigurationCache.Clear();
        var (_, changed) = CheckCommand.Run(request, TestContext.Current.CancellationToken);
        Assert.DoesNotContain(changed.Reportable, static finding => finding.RuleId == "SK2001");
    }

    static string[] Describe(RunReport report) =>
        report.Reportable
            .Select(static finding => $"{finding.RuleId}:{finding.Path}:{finding.Line}:{finding.Column}:{finding.Message}"
            )
            .Order(StringComparer.Ordinal)
            .ToArray();
}
