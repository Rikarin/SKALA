using Rikarin.Skala.Core.Diagnostics;
using Rikarin.Skala.Reporting;
using System.Security;

namespace Rikarin.Skala.Analysis.Tests;

[Collection(SerialWorkspace.Name)]
public sealed class PerformanceAndConcurrencyIntegrationTests {
    [Fact]
    public void Workspace_CheckVerifyCacheAndOptInPolicyHonorTheFiveRules() {
        using var scratch = new Scratch();
        var project = scratch.Write(
            "Scratch.csproj",
            $$"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup><TargetFramework>net10.0</TargetFramework><Nullable>enable</Nullable></PropertyGroup>
              <ItemGroup>
                <Reference Include="{{typeof(FactAttribute).Assembly.GetName().Name}}"><HintPath>{{SecurityElement.Escape(typeof(FactAttribute).Assembly.Location)}}</HintPath></Reference>
                <Reference Include="{{typeof(Assert).Assembly.GetName().Name}}"><HintPath>{{SecurityElement.Escape(typeof(Assert).Assembly.Location)}}</HintPath></Reference>
              </ItemGroup>
            </Project>
            """
        );
        const string configuration = """
                                     root = true
                                     [*.cs]
                                     dotnet_diagnostic.SK4002.severity = warning
                                     dotnet_diagnostic.SK4006.severity = warning
                                     [Hot.cs]
                                     dotnet_diagnostic.SK4001.severity = warning
                                     [Suppressed.cs]
                                     dotnet_diagnostic.SK3009.severity = none
                                     """;
        scratch.Write(".editorconfig", configuration);
        const string checks = """
                              using System;
                              using System.Linq;
                              using System.Collections.Generic;
                              class Checks {
                                  static readonly Lazy<int> Value = new(() => 1, false);
                                  void Capture(int[] values, List<Action> callbacks) {
                                      foreach (var value in values) callbacks.Add(() => Console.WriteLine(value));
                                  }
                                  void Enumerate(int[] values) {
                                      foreach (var value in values.ToArray()) Console.WriteLine(value);
                                  }
                              }
                              """;
        var checksPath = scratch.Write("Checks.cs", checks);
        scratch.Write(
            "Hot.cs",
            "using System.Linq; class Hot { int M(int[] values) => values.Where(x => x > 0).Count(); }"
        );
        scratch.Write("Cold.cs", "using System.Linq; class Cold { int M(int[] values) => values.Count(); }");
        scratch.Write(
            "Assertions.cs",
            "class Assertions { [Xunit.Fact] public void Check() { Xunit.Assert.NotEqual(System.Guid.Empty, System.Guid.NewGuid()); } }"
        );
        scratch.Write(
            "Suppressed.cs",
            "using System; class Suppressed { static Lazy<int> Value = new(() => 1, false); }"
        );
        string[] ids = ["SK3009", "SK4001", "SK4002", "SK4006", "SK8007"];
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
            Assert.False(finding.HasFix);
            Assert.Equal(
                id switch {
                    "SK4001" => "Hot.cs",
                    "SK8007" => "Assertions.cs",
                    _ => "Checks.cs"
                },
                Path.GetFileName(finding.Path)
            );
        }

        scratch.Write("Cold.cs", "using System.Linq; class Cold { int Value; int M(int[] values) => values.Count(); }");
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
                Include = ids
            },
            TestContext.Current.CancellationToken
        );
        Assert.Equal(ExitCodes.Ok, fixedResult.ExitCode);
        Assert.Equal(checks, File.ReadAllText(checksPath));

        scratch.Write(
            ".editorconfig",
            configuration.Replace(
                "dotnet_diagnostic.SK4001.severity = warning",
                "dotnet_diagnostic.SK4001.severity = none",
                StringComparison.Ordinal
            )
        );
        Core.Configuration.ConfigurationCache.Clear();
        var (_, changed) = CheckCommand.Run(request, TestContext.Current.CancellationToken);
        Assert.DoesNotContain(changed.Reportable, static finding => finding.RuleId == "SK4001");
        Assert.Equal(4, changed.Reportable.Count(finding => ids.Contains(finding.RuleId)));
    }

    static string[] Describe(RunReport report) =>
        report.Reportable
            .Select(static finding => $"{finding.RuleId}:{finding.Path}:{finding.Line}:{finding.Column}:{finding.Message}"
            )
            .Order(StringComparer.Ordinal)
            .ToArray();
}
