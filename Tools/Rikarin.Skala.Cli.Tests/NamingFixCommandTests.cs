using Rikarin.Skala.Testing;

namespace Rikarin.Skala.Cli.Tests;

/// <summary>The naming fix through the standalone CLI process, including its private Roslyn payload.</summary>
public sealed class NamingFixCommandTests {
    const string NamingConfig = """
                                root = true

                                [*.cs]
                                dotnet_naming_rule.types.symbols = types
                                dotnet_naming_rule.types.style = pascal
                                dotnet_naming_rule.types.severity = warning
                                dotnet_naming_symbols.types.applicable_kinds = class
                                dotnet_naming_symbols.types.applicable_accessibilities = *
                                dotnet_naming_style.pascal.capitalization = pascal_case
                                """;

    const string Project = """
                           <Project Sdk="Microsoft.NET.Sdk">
                             <PropertyGroup>
                               <TargetFramework>net10.0</TargetFramework>
                               <Nullable>enable</Nullable>
                             </PropertyGroup>
                           </Project>
                           """;

    [Fact]
    public void Fix_IDE1006_LoadsTheFixerFromTheStandaloneCliPayload() {
        using var scratch = new Scratch();
        scratch.Write(".editorconfig", NamingConfig);
        var source = scratch.Write("bad_name.cs", "namespace Scratch;\n\npublic sealed class bad_name;\n");
        var project = scratch.Write("Scratch.csproj", Project);
        var before = File.ReadAllText(source);

        var run = CliRunner.Run(
            "fix",
            scratch.Root,
            "--include",
            "IDE1006",
            "--load",
            "workspace",
            "--project",
            project,
            "--dry-run"
        );

        Assert.Equal(0, run.ExitCode);
        Assert.Contains("applied 1 fix (dry run, nothing written)", run.StandardOutput, StringComparison.Ordinal);
        Assert.DoesNotContain("could not be loaded", run.StandardOutput, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(before, File.ReadAllText(source));
    }

    sealed class Scratch : IDisposable {
        public Scratch() {
            Root = Path.Combine(Path.GetTempPath(), "skala-cli-naming", Guid.NewGuid().ToString("n"));
            Directory.CreateDirectory(Root);
        }

        public string Root { get; }

        public string Write(string relative, string text) {
            var path = Path.Combine(Root, relative);
            File.WriteAllText(path, text);
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
