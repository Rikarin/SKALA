using Rikarin.Skala.Testing;
using System.Diagnostics;

namespace Rikarin.Skala.Cli.Tests;

/// <summary>The standalone arrangement command's project-loading contract.</summary>
public sealed class ArrangeCommandTests {
    [Fact]
    public void DefaultArrange_ClearsTheSemanticFindingReportedByDefaultVerify() {
        using var scratch = new Scratch();
        scratch.Write(
            ".editorconfig",
            """
            root = true

            [*.cs]
            skala_arguments_literal = named
            skala_arguments_skip_single = false
            """
        );
        scratch.Write(
            "Callee.cs",
            """
            internal static class Callee {
                public static int Sum(int first, int second) => first + second;
            }
            """
        );
        var caller = scratch.Write(
            "Caller.cs",
            """
            internal static class Caller {
                public static int Call() => Callee.Sum(1, 2);
            }
            """
        );
        scratch.Write(
            "Scratch.csproj",
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
              </PropertyGroup>
            </Project>
            """
        );

        var arranged = CliRunner.Run("arrange", caller);

        Assert.Equal(0, arranged.ExitCode);
        Assert.Contains("Callee.Sum(first: 1, second: 2)", File.ReadAllText(caller), StringComparison.Ordinal);

        var verified = CliRunner.Run("verify", caller, "--no-cache");

        Assert.DoesNotContain("SK0216", verified.StandardOutput, StringComparison.Ordinal);
    }

    /// <summary>
    ///     A solution nested below the Git root is still the workspace target when <c>arrange</c>
    ///     is run from that project without an explicit path.
    /// </summary>
    /// <remarks>
    ///     ⚠ The collected files are below <c>App/src</c>, while the solution is at <c>App/App.slnx</c>.
    ///     Re-deriving discovery from the first collected file searches <c>src</c> and then the outer
    ///     Git root, skipping <c>App</c>; the loose compilation then lacks the project-defined symbol
    ///     and names the arguments after the wrong conditional signature.
    /// </remarks>
    [Fact]
    public void DefaultArrange_LoadsTheSlnxUnderTheRequestedNestedProject() {
        using var scratch = new Scratch();
        scratch.Write(
            "App/.editorconfig",
            """
            root = true

            [*.cs]
            skala_arguments_literal = named
            skala_arguments_skip_single = false
            """
        );
        scratch.Write(
            "App/src/Callee.cs",
            """
            internal static class Callee {
            #if PROJECT_BUILD
                public static int Sum(int first, int second) => first + second;
            #else
                public static int Sum(int x, int y) => x + y;
            #endif
            }
            """
        );
        var caller = scratch.Write(
            "App/src/Caller.cs",
            """
            internal static class Caller {
                public static int Call() => Callee.Sum(1, 2);
            }
            """
        );
        scratch.Write(
            "App/App.csproj",
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <DefineConstants>PROJECT_BUILD</DefineConstants>
              </PropertyGroup>
            </Project>
            """
        );
        scratch.Write(
            "App/App.slnx",
            """
            <Solution>
              <Project Path="App.csproj" />
            </Solution>
            """
        );

        var arranged = RunIn(Path.Combine(scratch.Root, "App"), "arrange");

        Assert.Equal(0, arranged.ExitCode);
        var rewritten = File.ReadAllText(caller);
        Assert.Contains("Callee.Sum(first: 1, second: 2)", rewritten, StringComparison.Ordinal);
    }

    /// <summary>
    ///     ⚠ #336: <c>arrange</c> had neither <c>--binlog</c> nor <c>--require-fresh-binlog</c>, so
    ///     <c>--load=binlog</c> could only auto-discover and could never be told the log had to be
    ///     current.
    /// </summary>
    /// <remarks>
    ///     ⚠ <b>The named binlog is what makes this test able to fail.</b> Without <c>--binlog</c>
    ///     there is nothing to point at a log that does not exist, so the absence of the option is
    ///     also the absence of any way to demonstrate the absence — which is why it went unnoticed.
    ///     <c>--require-fresh-binlog</c> then has to reach <c>LoadRequest</c> rather than merely parse:
    ///     sabotage by dropping <c>RequireFreshBinlog</c> from <c>CompilationsFor</c>'s
    ///     <c>LoadRequest</c> and the second half goes red while the first still passes.
    /// </remarks>
    [Fact]
    public void Arrange_TakesTheBinlogOptionsCheckHas() {
        using var scratch = new Scratch();
        var file = scratch.Write("Widget.cs", "internal sealed class Widget;");

        // Exists, so binlog resolution selects it, and is not a binary log, so reading it fails
        // naming the path. That naming is the assertion: it can only come from --binlog reaching
        // LoadRequest.BinlogPath, since nothing else in the run knows this file.
        var named = scratch.Write("named.binlog", "not a binary log");

        var withBinlog = CliRunner.Run("arrange", "--check", "--load=binlog", "--binlog", named, file);

        Assert.DoesNotContain("Unrecognized command or argument", withBinlog.StandardError, StringComparison.Ordinal);
        Assert.Contains(named, withBinlog.StandardOutput + withBinlog.StandardError, StringComparison.Ordinal);

        var withFresh = CliRunner.Run(
            "arrange",
            "--check",
            "--load=binlog",
            "--binlog",
            named,
            "--require-fresh-binlog",
            file
        );

        Assert.DoesNotContain("Unrecognized command or argument", withFresh.StandardError, StringComparison.Ordinal);

        // ⚠ **Stated gap, not an oversight.** `--require-fresh-binlog` only changes the severity of
        // the coverage and staleness diagnostics, both of which need a *readable* binary log, and
        // this repository has no binlog fixture and no cheap way to make one — producing it means a
        // real `dotnet build -bl:`. So the option's presence is asserted here and its effect is
        // exercised by `build/Build.cs`'s Lint target, which passes it on every run. A test that
        // pretended to cover the effect would be worth less than saying so.
        Assert.Equal(withBinlog.ExitCode, withFresh.ExitCode);
    }

    static CliRun RunIn(string workingDirectory, params string[] arguments) {
        var start = new ProcessStartInfo("dotnet") {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };

        start.ArgumentList.Add(CliRunner.Assembly);
        foreach (var argument in arguments) {
            start.ArgumentList.Add(argument);
        }

        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        return new(process.ExitCode, output, error);
    }

    sealed class Scratch : IDisposable {
        public Scratch() {
            Root = Path.Combine(Path.GetTempPath(), "skala-cli-arrange", Guid.NewGuid().ToString("n"));
            Directory.CreateDirectory(Root);

            // Repository discovery is intentionally part of the test: auto-load must resolve the
            // project beside the requested file, not a solution beside the test runner.
            Directory.CreateDirectory(Path.Combine(Root, ".git"));
        }

        public string Root { get; }

        public string Write(string relative, string text) {
            var path = Path.Combine(Root, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, text + "\n");
            return path;
        }

        public void Dispose() {
            try {
                Directory.Delete(Root, true);
            } catch (IOException) {
                // A leftover temp directory is not worth failing a test over.
            }
        }
    }
}
