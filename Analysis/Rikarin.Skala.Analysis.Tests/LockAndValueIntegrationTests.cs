using Rikarin.Skala.Core.Diagnostics;
using Rikarin.Skala.Reporting;

namespace Rikarin.Skala.Analysis.Tests;

[Collection(SerialWorkspace.Name)]
public sealed class LockAndValueIntegrationTests {
    [Fact]
    public void Workspace_CheckVerifyFixAndCacheHonorTheFiveRules() {
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
                                     dotnet_diagnostic.SK4004.severity = warning
                                     dotnet_diagnostic.SK4007.severity = warning
                                     dotnet_diagnostic.SK7060.severity = warning
                                     dotnet_code_quality.SK4007.threshold = 64
                                     [Suppressed.cs]
                                     dotnet_diagnostic.SK2003.severity = none
                                     """;
        scratch.Write(".editorconfig", configuration);
        var checks = scratch.Write(
            "Checks.cs",
            """
            interface I { void Step(); }
            struct Large { public long A, B, C, D, E, F, G, H, J; }
            class Checks {
                readonly object gate = new();
                void Synchronize() { lock (gate) { } }
                bool Compare(double x, double y, double expected) => x + y == expected;
                void Box<T>(T value) where T : struct, I { ((I)value).Step(); }
                void Consume(Large value) { }
                void Copy(Large value) { for (int i = 0; i < 10; i++) Consume(value); }
                void Comments() {
                    // Start();
                    // Process();
                }
            }
            """
        );
        scratch.Write(
            "Suppressed.cs",
            "class Suppressed { bool M(double x, double y, double expected) => x + y == expected; }"
        );
        scratch.Write("Unrelated.cs", "class Unrelated { }");
        string[] ids = ["SK1023", "SK2003", "SK4004", "SK4007", "SK7060"];
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
            Assert.Equal(id == "SK1023", finding.HasFix);
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
                Include = ["SK1023"]
            },
            TestContext.Current.CancellationToken
        );
        Assert.Equal(ExitCodes.Ok, fixedResult.ExitCode);
        Assert.Contains("System.Threading.Lock", File.ReadAllText(checks), StringComparison.Ordinal);
        var (_, afterFix) = CheckCommand.Run(request with { NoCache = true }, TestContext.Current.CancellationToken);
        Assert.DoesNotContain(afterFix.Reportable, static finding => finding.RuleId == "SK1023");
        Assert.Equal(4, afterFix.Reportable.Count(finding => ids.Contains(finding.RuleId)));

        scratch.Write(
            ".editorconfig",
            configuration.Replace("threshold = 64", "threshold = 128", StringComparison.Ordinal)
        );
        Core.Configuration.ConfigurationCache.Clear();
        var (_, changed) = CheckCommand.Run(request, TestContext.Current.CancellationToken);
        Assert.DoesNotContain(changed.Reportable, static finding => finding.RuleId == "SK4007");
        Assert.Equal(3, changed.Reportable.Count(finding => ids.Contains(finding.RuleId)));
    }

    static string[] Describe(RunReport report) =>
        report.Reportable
            .Select(static finding => $"{finding.RuleId}:{finding.Path}:{finding.Line}:{finding.Column}:{finding.Message}"
            )
            .Order(StringComparer.Ordinal)
            .ToArray();
}
