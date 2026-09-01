using Rikarin.Skala.Core.Diagnostics;
using Rikarin.Skala.Reporting;

namespace Rikarin.Skala.Analysis.Tests;

public sealed class DeclarationPerformanceIntegrationTests {
    [Fact]
    public void Workspace_CheckVerifySafeFixAndCacheHonorTheFiveRules() {
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
                                     dotnet_diagnostic.SK4020.severity = warning
                                     dotnet_diagnostic.SK4021.severity = warning
                                     dotnet_diagnostic.SK4022.severity = warning
                                     [Suppressed.cs]
                                     dotnet_diagnostic.SK4024.severity = none
                                     """;
        scratch.Write(".editorconfig", configuration);
        var checks = scratch.Write(
            "Checks.cs",
            """
            using System;
            using System.Collections.Generic;
            sealed class Callbacks {
                public Func<int, int> Twice() => value => value * 2;
            }
            sealed class Report {
                readonly string title = "report";
                public string Line() => Format(1) + title;
                string Format(int count) => count + " rows";
            }
            struct Point {
                public readonly int X;
                public Point(int x) => X = x;
                public int Doubled => X * 2;
            }
            static class Buffers {
                public static List<int> Make() => new List<int>(0);
                public static void Purge() => GC.Collect();
            }
            """
        );
        scratch.Write(
            "Suppressed.cs",
            """
            using System;
            static class Suppressed {
                public static void Purge() => GC.Collect();
            }
            """
        );
        scratch.Write("Unrelated.cs", "class Unrelated { }");
        string[] ids = ["SK4020", "SK4021", "SK4022", "SK4023", "SK4024"];
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
            Assert.Equal(id != "SK4024", finding.HasFix);
        }

        scratch.Write("Unrelated.cs", "class Unrelated { int Value; }");
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
                Include = ["SK4020", "SK4021", "SK4022", "SK4023"]
            },
            TestContext.Current.CancellationToken
        );
        Assert.Equal(ExitCodes.Ok, fixedResult.ExitCode);
        var fixedSource = File.ReadAllText(checks);
        Assert.Contains("static value =>", fixedSource, StringComparison.Ordinal);
        Assert.Contains("static string Format", fixedSource, StringComparison.Ordinal);
        Assert.Contains("readonly struct Point", fixedSource, StringComparison.Ordinal);
        Assert.Contains("new List<int>()", fixedSource, StringComparison.Ordinal);

        var (_, after) = CheckCommand.Run(request with { NoCache = true }, TestContext.Current.CancellationToken);
        Assert.Single(after.Reportable, finding => ids.Contains(finding.RuleId));
        Assert.Single(after.Reportable, static finding => finding.RuleId == "SK4024");

        scratch.Write(
            ".editorconfig",
            configuration.Replace("SK4022.severity = warning", "SK4022.severity = none", StringComparison.Ordinal)
        );
        Core.Configuration.ConfigurationCache.Clear();
        var (_, changed) = CheckCommand.Run(request, TestContext.Current.CancellationToken);
        Assert.DoesNotContain(changed.Reportable, static finding => finding.RuleId == "SK4022");
    }

    static string[] Describe(RunReport report) =>
        report.Reportable
            .Select(static finding => $"{finding.RuleId}:{finding.Path}:{finding.Line}:{finding.Column}:{finding.Message}"
            )
            .Order(StringComparer.Ordinal)
            .ToArray();
}
