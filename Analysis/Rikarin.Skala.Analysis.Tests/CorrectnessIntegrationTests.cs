using Rikarin.Skala.Core.Diagnostics;
using Rikarin.Skala.Reporting;

namespace Rikarin.Skala.Analysis.Tests;

[Collection(SerialWorkspace.Name)]
public sealed class CorrectnessIntegrationTests {
    [Fact]
    public void Workspace_CheckAndVerifyRunTheNewCorrectnessRules() {
        using var scratch = new Scratch();
        var project = scratch.Write(
            "Scratch.csproj",
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <Nullable>enable</Nullable>
              </PropertyGroup>
            </Project>
            """
        );
        scratch.Write(
            ".editorconfig",
            """
            root = true
            [*.cs]
            dotnet_diagnostic.SK2010.severity = error
            """
        );
        scratch.Write(
            "Checks.cs",
            """
            using System;
            using System.Collections.Generic;
            class Key : IEquatable<Key> {
                public int Id;
                public bool Equals(Key? other) => other?.Id == Id;
            }
            struct Point { public int X; }
            class Checks {
                void Normalize(string value) { value.Trim(); }
                int Compare(string a, string b) => string.Compare(a, b);
                bool Same(Point a, Point b) => a.Equals(b);
                List<Action> Build() {
                    var actions = new List<Action>();
                    for (var i = 0; i < 3; i++) { actions.Add(() => Console.WriteLine(i)); }
                    return actions;
                }
            }
            """
        );
        string[] ids = ["SK2002", "SK2004", "SK2008", "SK2010", "SK2011"];
        var (result, report) = CheckCommand.Run(
            new CheckRequest {
                RepositoryRoot = scratch.Root,
                Paths = [scratch.Root],
                Mode = LoadMode.Workspace,
                ProjectPath = project,
                Output = string.Empty,
                Rules = ids,
                NoCache = true
            },
            TestContext.Current.CancellationToken
        );

        Assert.NotEqual(ExitCodes.LoadFailure, result.ExitCode);
        Assert.Equal(LoadMode.Workspace, report.Mode);
        foreach (var id in ids) {
            var finding = Assert.Single(report.Findings, finding => finding.RuleId == id);
            Assert.False(finding.HasFix);
        }

        Assert.Equal(
            SkalaSeverity.Error,
            report.Findings.Single(static finding => finding.RuleId == "SK2010").Severity
        );

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
        Assert.NotEqual(ExitCodes.Ok, verified.ExitCode);
        Assert.NotEqual(ExitCodes.LoadFailure, verified.ExitCode);
        foreach (var id in ids) {
            Assert.Contains(id, verified.Output, StringComparison.Ordinal);
        }
    }
}
