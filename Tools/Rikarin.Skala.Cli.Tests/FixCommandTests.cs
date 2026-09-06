using Rikarin.Skala.Testing;

namespace Rikarin.Skala.Cli.Tests;

/// <summary>The fix command discovers the same workspace as arrange and verify.</summary>
public sealed class FixCommandTests {
    const string IncludeOption = "--include";

    const string Project = """
                           <Project Sdk="Microsoft.NET.Sdk">
                             <PropertyGroup>
                               <TargetFramework>net10.0</TargetFramework>
                             </PropertyGroup>
                           </Project>
                           """;

    const string Source = """
                          public static class Factory {
                              public static System.Func<int, int> Create() => value => value + 1;
                          }
                          """;

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void Fix_AutoAppliesSemanticFixes(bool explicitAuto, bool safe) {
        using var scratch = new Scratch();
        var source = scratch.Write("Factory.cs", Source);
        scratch.Write("Scratch.csproj", Project);
        var arguments = new List<string> { "fix", source };
        if (explicitAuto) {
            arguments.Add("--load=auto");
        }

        if (safe) {
            arguments.Add("--safe");
        } else {
            arguments.AddRange([IncludeOption, "SK4020"]);
        }

        var run = CliRunner.Run([.. arguments]);

        Assert.True(run.ExitCode == 0, run.StandardOutput + run.StandardError);
        Assert.Contains("static value => value + 1", File.ReadAllText(source), StringComparison.Ordinal);
    }

    [Fact]
    public void Fix_ProjectSelectsOneOfSeveralTargetsAndDryRunDoesNotWrite() {
        using var scratch = new Scratch();
        var source = scratch.Write("Factory.cs", Source);
        var project = scratch.Write("First.csproj", Project);
        scratch.Write("Second.csproj", Project);
        var before = File.ReadAllText(source);

        var run = CliRunner.Run("fix", source, IncludeOption, "SK4020", "--project", project, "--dry-run");

        Assert.True(run.ExitCode == 0, run.StandardOutput + run.StandardError);
        Assert.Contains("applied 1 fix (dry run, nothing written)", run.StandardOutput, StringComparison.Ordinal);
        Assert.Equal(before, File.ReadAllText(source));
    }

    [Fact]
    public void Fix_AutoRefusesAmbiguousTargets() {
        using var scratch = new Scratch();
        var source = scratch.Write("Factory.cs", Source);
        scratch.Write("First.csproj", Project);
        scratch.Write("Second.csproj", Project);
        var before = File.ReadAllText(source);

        var run = CliRunner.Run("fix", source, IncludeOption, "SK4020");

        Assert.NotEqual(0, run.ExitCode);
        Assert.Contains("multiple '*.csproj' workspace targets", run.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("--project", run.StandardOutput, StringComparison.Ordinal);
        Assert.Equal(before, File.ReadAllText(source));
    }

    [Fact]
    public void Fix_AutoRefusesAFailedWorkspaceLoad() {
        using var scratch = new Scratch();
        var source = scratch.Write("Factory.cs", Source);
        scratch.Write("Scratch.csproj", "<Project>");
        var before = File.ReadAllText(source);

        var run = CliRunner.Run("fix", source, IncludeOption, "SK4020");

        Assert.NotEqual(0, run.ExitCode);
        Assert.Contains("no compilation could be built", run.StandardOutput, StringComparison.Ordinal);
        Assert.Equal(before, File.ReadAllText(source));
    }

    [Fact]
    public void Fix_ExplicitLooseDoesNotLoadTheWorkspace() {
        using var scratch = new Scratch();
        var source = scratch.Write("Factory.cs", Source);
        scratch.Write("Scratch.csproj", "<Project>");
        var before = File.ReadAllText(source);

        var run = CliRunner.Run("fix", source, IncludeOption, "SK4020", "--load=loose");

        Assert.Equal(0, run.ExitCode);
        Assert.Contains("nothing to apply", run.StandardOutput, StringComparison.Ordinal);
        Assert.Equal(before, File.ReadAllText(source));
    }

    [Fact]
    public void Fix_WithoutAWorkspaceStillAppliesSyntacticFixes() {
        using var scratch = new Scratch();
        var source = scratch.Write(
            "Thrower.cs",
            """
            public static class Thrower {
                public static void Run() {
                    try { System.Console.WriteLine(); } catch (System.Exception ex) { throw ex; }
                }
            }
            """
        );

        var run = CliRunner.Run("fix", source, IncludeOption, "SK2015");

        Assert.True(run.ExitCode == 0, run.StandardOutput + run.StandardError);
        Assert.Contains("throw;", File.ReadAllText(source), StringComparison.Ordinal);
    }

    sealed class Scratch : IDisposable {
        public Scratch() {
            Root = Path.Combine(Path.GetTempPath(), "skala-cli-fix", Guid.NewGuid().ToString("n"));
            Directory.CreateDirectory(Path.Combine(Root, ".git"));
            Write(".editorconfig", "root = true");
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
