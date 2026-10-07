using Rikarin.Skala.Testing;

namespace Rikarin.Skala.Cli.Tests;

/// <summary>
///     ⚠ #398, through the real binary: a file in no compilation under <c>--load=binlog</c> is printed
///     by <c>--format=plain</c> and <c>--format=agent</c>, and a clean run prints nothing extra.
/// </summary>
/// <remarks>
///     <para>
///         The shape is the self-gate's own: Skala's <c>build/Build.cs</c> and
///         <c>build/Configuration.cs</c> sit under the repository root and in no project the solution
///         builds, so the binlog names no compilation containing them and <c>BinlogLoader</c> writes
///         <c>SK9021</c> for each. Here <c>src/Probe.csproj</c> is the only project and
///         <c>build/Orphan.cs</c> is beside it, outside its glob. Measured on master before #398 against
///         Skala's own tree: three <c>SK9021</c> in the SARIF, zero lines under either format.
///     </para>
///     <para>
///         One build serves every case: the clean case selects <c>src/</c> only, so the orphan is not a
///         selected file and no <c>SK9021</c> is written. The project does not set
///         <c>GenerateDocumentationFile</c>, so every run also carries the info <c>SK9032</c> — and the
///         clean case asserting nothing extra is also the assertion that info reaches neither surface.
///     </para>
///     <para>
///         ⚠ Exit codes are asserted unchanged: <c>SK9021</c> at warning fails no gate, so the run is
///         exit 0 with the warning printed, exactly as it was exit 0 with the warning hidden.
///     </para>
/// </remarks>
public sealed class ToolWarningOutputTests : IClassFixture<ToolWarningOutputTests.Tree> {
    const string NotAnalysed = "SK9021";

    readonly Tree tree;

    public ToolWarningOutputTests(Tree tree) => this.tree = tree;

    CliRun Check(string format, string path) =>
        CliRunner.Run(
            "check",
            "--load=binlog",
            "--binlog",
            tree.Binlog,
            "--no-cache",
            "--gate=local",
            "--format=" + format,
            path
        );

    [Fact]
    public void Plain_PrintsTheFileThatWasNotAnalysed() {
        var run = Check("plain", tree.Root);

        Assert.Equal(0, run.ExitCode);
        Assert.Contains(
            "build/Orphan.cs:1:1: warning SK9021: the binary log names no compilation containing this file, "
            + "so it was not analysed; rebuild\n",
            run.StandardOutput,
            StringComparison.Ordinal
        );
        Assert.Contains(
            "probe.binlog:1:1: warning SK9021: the binary log covers 1 of 2",
            run.StandardOutput,
            StringComparison.Ordinal
        );
        Assert.All(
            run.StandardOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries),
            static line => Assert.Matches(@"^[^:\s][^:]*:\d+:\d+: (error|warning|suggestion|hint) SK\d{4}: \S", line)
        );
        Assert.DoesNotContain("SK9032", run.StandardOutput, StringComparison.Ordinal);
    }

    [Fact]
    public void Plain_PrintsNothingExtra_OnACleanRun() {
        var run = Check("plain", Path.Combine(tree.Root, "src"));

        Assert.Equal(0, run.ExitCode);
        Assert.Equal(string.Empty, run.StandardOutput);
    }

    [Fact]
    public void Agent_PrintsTheFileThatWasNotAnalysed() {
        var run = Check("agent", tree.Root);

        Assert.Equal(0, run.ExitCode);
        Assert.StartsWith("WARNING 2 warnings about this run", run.StandardOutput, StringComparison.Ordinal);
        Assert.Contains(
            "  SK9021  build/Orphan.cs  the binary log names no compilation containing this file",
            run.StandardOutput,
            StringComparison.Ordinal
        );
        Assert.DoesNotContain("INCOMPLETE", run.StandardOutput, StringComparison.Ordinal);
        Assert.DoesNotContain("nothing to do", run.StandardOutput, StringComparison.Ordinal);
        Assert.DoesNotContain("SK9032", run.StandardOutput, StringComparison.Ordinal);
    }

    [Fact]
    public void Agent_PrintsNothingExtra_OnACleanRun() {
        var run = Check("agent", Path.Combine(tree.Root, "src"));

        Assert.Equal(0, run.ExitCode);
        Assert.Equal("OK  nothing to do.\n", run.StandardOutput);
    }

    /// <summary>
    ///     The control: the SARIF of the same run carries the diagnostic, so the plain and agent
    ///     assertions above measure the renderer and not a loader that stopped writing it.
    /// </summary>
    [Fact]
    public void TheSarifCarriesTheSameDiagnostic() {
        var run = Check("json", tree.Root);

        Assert.Equal(0, run.ExitCode);
        Assert.Contains(NotAnalysed, run.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("build/Orphan.cs", run.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("SK9032", run.StandardOutput, StringComparison.Ordinal);
    }

    /// <summary>A real project, really built, with a source file beside it that no compilation names.</summary>
    public sealed class Tree : IDisposable {
        public Tree() {
            Root = Directory.CreateTempSubdirectory("skala-398-").FullName;
            Directory.CreateDirectory(Path.Combine(Root, ".git"));
            Directory.CreateDirectory(Path.Combine(Root, "src"));
            Directory.CreateDirectory(Path.Combine(Root, "build"));

            // ⚠ Cut the inheritance chains, so the temp directory's ancestors cannot change the build.
            Write("Directory.Build.props", "<Project />");
            Write("Directory.Build.targets", "<Project />");
            Write(
                "src/Probe.csproj",
                """
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <TargetFramework>net10.0</TargetFramework>
                  </PropertyGroup>
                </Project>
                """
            );
            Write("src/Lib.cs", Clean);
            Write("build/Orphan.cs", Clean.Replace("Lib", "Orphan", StringComparison.Ordinal));

            Binlog = Path.Combine(Root, "probe.binlog");
            Build(Path.Combine(Root, "src", "Probe.csproj"), Binlog);
        }

        const string Clean =
            "namespace Probe;\n\ninternal static class Lib {\n    internal static int Twice(int value) => value * 2;\n}\n";

        public string Root { get; }

        public string Binlog { get; }

        public void Dispose() => Directory.Delete(Root, true);

        void Write(string name, string content) => File.WriteAllText(Path.Combine(Root, name), content);

        static void Build(string project, string binlog) {
            var start = new System.Diagnostics.ProcessStartInfo("dotnet") {
                RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false
            };

            foreach (var argument in new[] { "build", project, "-bl:" + binlog, "--nologo" }) {
                start.ArgumentList.Add(argument);
            }

            using var process = System.Diagnostics.Process.Start(start)!;
            var output = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
            process.WaitForExit();
            Assert.True(process.ExitCode == 0, "`dotnet build` on the #398 fixture failed:\n" + output);
        }
    }
}
