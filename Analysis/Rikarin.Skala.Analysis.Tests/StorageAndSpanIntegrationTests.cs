using Rikarin.Skala.Core.Diagnostics;
using Rikarin.Skala.Reporting;

namespace Rikarin.Skala.Analysis.Tests;

public sealed class StorageAndSpanIntegrationTests {
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
                                     dotnet_diagnostic.SK1022.severity = warning
                                     dotnet_diagnostic.SK1025.severity = warning
                                     dotnet_diagnostic.SK4003.severity = warning
                                     [Suppressed.cs]
                                     dotnet_diagnostic.SK4003.severity = none
                                     """;
        scratch.Write(".editorconfig", configuration);
        var checks = scratch.Write(
            "Checks.cs",
            """
            using System;
            using System.Collections.Generic;
            class PropertyOwner {
                int count;
                public int Count { get => count; set => count = Math.Max(0, value); }
            }
            class Search {
                static readonly char[] chars = "aeiou".ToCharArray();
                int Find(ReadOnlySpan<char> text) => text.IndexOfAny(chars);
            }
            class Lookup {
                static readonly Dictionary<string,int> map = new() { {"a",1} };
                int Find(string key) => map[key];
            }
            struct Counter { public int Value; public void Increment() => Value++; }
            class Copies {
                readonly Counter counter;
                void Change() => counter.Increment();
                static void Consume(params int[] items) { }
                static void Consume(ReadOnlySpan<int> items) { }
                void Allocate() => Consume(new[] {1,2,3});
            }
            """
        );
        scratch.Write(
            "Suppressed.cs",
            """
            using System;
            class Suppressed {
                static void Consume(params int[] items) { }
                static void Consume(ReadOnlySpan<int> items) { }
                void Allocate() => Consume(new[] {1,2,3});
            }
            """
        );
        scratch.Write("Unrelated.cs", "class Unrelated { }");
        string[] ids = ["SK1003", "SK1022", "SK1025", "SK2005", "SK4003"];
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
            Assert.Equal(id is "SK1003" or "SK1022" or "SK1025", finding.HasFix);
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
                Include = ["SK1003", "SK1022", "SK1025"]
            },
            TestContext.Current.CancellationToken
        );
        Assert.Equal(ExitCodes.Ok, fixedResult.ExitCode);
        var fixedSource = File.ReadAllText(checks);
        Assert.Contains("field", fixedSource, StringComparison.Ordinal);
        Assert.Contains("SearchValues", fixedSource, StringComparison.Ordinal);
        Assert.Contains("FrozenDictionary", fixedSource, StringComparison.Ordinal);
        var (_, after) = CheckCommand.Run(request with { NoCache = true }, TestContext.Current.CancellationToken);
        Assert.Equal(2, after.Reportable.Count(finding => ids.Contains(finding.RuleId)));
        Assert.DoesNotContain(after.Reportable, static finding => finding.RuleId is "SK1003" or "SK1022" or "SK1025");

        scratch.Write(
            ".editorconfig",
            configuration.Replace("SK4003.severity = warning", "SK4003.severity = none", StringComparison.Ordinal)
        );
        Core.Configuration.ConfigurationCache.Clear();
        var (_, changed) = CheckCommand.Run(request, TestContext.Current.CancellationToken);
        Assert.DoesNotContain(changed.Reportable, static finding => finding.RuleId == "SK4003");
        Assert.Single(changed.Reportable, static finding => finding.RuleId == "SK2005");
    }

    static string[] Describe(RunReport report) =>
        report.Reportable
            .Select(static finding => $"{finding.RuleId}:{finding.Path}:{finding.Line}:{finding.Column}:{finding.Message}"
            )
            .Order(StringComparer.Ordinal)
            .ToArray();
}
