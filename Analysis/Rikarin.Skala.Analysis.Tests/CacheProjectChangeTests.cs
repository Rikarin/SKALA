using Rikarin.Skala.Reporting;

namespace Rikarin.Skala.Analysis.Tests;

/// <summary>
///     #514 end to end: a project property changes, the source does not, and the cached run must say
///     what an uncached run says.
/// </summary>
/// <remarks>
///     ⚠ The repro was <c>LangVersion</c> 14.0 → 9.0 over a byte-identical <c>Q.cs</c>, both
///     <c>--load=binlog</c>: <c>SK1133</c> (floor C# 14) reported on both runs, and 0 under
///     <c>--no-cache</c>. This uses <c>SK1005</c> (file-scoped namespace, floor C# 10) because it is
///     Syntax-scoped — the scope that looks as if it could key on the file's text alone and cannot,
///     since <c>SkalaRule.MeetsLanguageVersion</c> reads the compilation.
///     <para>
///         ⚠ Each state is checked twice, cached and with <c>NoCache</c>, and the assertion is that the
///         two agree <em>and</em> equal the expected count — so a state where the uncached answer is
///         not what this file believes reddens too, rather than letting the cache agree with a wrong
///         answer. The last state returns to the first one's inputs, which is the warm hit the cache
///         exists for. Sabotage: restore the pre-#514 compilation fingerprint (language version and
///         compilation options absent) and the 9.0 and <c>NoWarn</c> states report the 10.0 finding.
///     </para>
/// </remarks>
[Collection(SerialWorkspace.Name)]
public sealed class CacheProjectChangeTests {
    const string Source = """
                          namespace N {
                              public class C { }
                          }

                          """;

    [Fact]
    public void AProjectPropertyChange_IsNeverAnsweredFromTheCache() {
        using var scratch = new Scratch();
        scratch.Write("Directory.Build.props", "<Project />");
        scratch.Write("Directory.Build.targets", "<Project />");
        scratch.Write(
            ".editorconfig",
            """
            root = true

            [*.cs]
            dotnet_diagnostic.SK1005.severity = warning
            """
        );
        scratch.Write("N.cs", Source);

        (string LangVersion, string? NoWarn, int Expected)[] states = [
            ("10.0", null, 1),
            ("9.0", null, 0),
            ("10.0", "SK1005", 0),
            ("10.0", null, 1)
        ];

        foreach (var (langVersion, noWarn, expected) in states) {
            var project = scratch.Write(
                "Probe.csproj",
                $"""
                 <Project Sdk="Microsoft.NET.Sdk">
                   <PropertyGroup>
                     <TargetFramework>net10.0</TargetFramework>
                     <LangVersion>{langVersion}</LangVersion>
                     <NoWarn>{noWarn}</NoWarn>
                   </PropertyGroup>
                 </Project>
                 """
            );

            var binlog = Path.Combine(scratch.Root, "probe.binlog");
            Build(project, binlog);

            var cached = Count(Check(scratch.Root, project, binlog, false));
            var uncached = Count(Check(scratch.Root, project, binlog, true));
            var state = $"LangVersion {langVersion}, NoWarn '{noWarn}'";
            Assert.True(uncached == expected, $"{state}: uncached run reported {uncached} SK1005, expected {expected}");
            Assert.True(cached == expected, $"{state}: cached run reported {cached} SK1005, expected {expected}");
        }
    }

    static int Count(RunReport report) => report.Findings.Count(static finding => finding.RuleId == "SK1005");

    static RunReport Check(string root, string project, string binlog, bool noCache) {
        var (_, report) = CheckCommand.Run(
            new CheckRequest {
                RepositoryRoot = root,
                Paths = [root],
                Mode = LoadMode.Binlog,
                BinlogPath = binlog,
                ProjectPath = project,
                AllowLoadFallback = false,
                Output = string.Empty,
                IncludeFormatting = false,
                NoCache = noCache
            },
            TestContext.Current.CancellationToken
        );

        Assert.Equal(LoadMode.Binlog, report.Mode);
        return report;
    }

    /// <summary>A clean build each time: an incremental one can skip <c>Csc</c> and leave no invocation.</summary>
    static void Build(string project, string binlog) {
        var directory = Path.GetDirectoryName(project)!;
        foreach (var output in new[] { "bin", "obj" }) {
            var path = Path.Combine(directory, output);
            if (Directory.Exists(path)) {
                Directory.Delete(path, true);
            }
        }

        var start = new System.Diagnostics.ProcessStartInfo("dotnet") {
            RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false
        };

        foreach (var argument in new[] { "build", project, "-bl:" + binlog, "--nologo" }) {
            start.ArgumentList.Add(argument);
        }

        using var process = System.Diagnostics.Process.Start(start)!;
        var stdout = process.StandardOutput.ReadToEndAsync(TestContext.Current.CancellationToken);
        var stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();
        Assert.True(process.ExitCode == 0, "`dotnet build` on the #514 fixture failed:\n" + stdout.Result + stderr);
    }
}
