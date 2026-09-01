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
