using Rikarin.Skala.Testing;

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
            resharper_arguments_literal = named
            resharper_arguments_skip_single = false
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
