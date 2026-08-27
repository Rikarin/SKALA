using Rikarin.Skala.Core.Configuration;
using Rikarin.Skala.Testing;

namespace Rikarin.Skala.Cli.Tests;

/// <summary>
/// The command surface for docs/plan/03 § "Canonical distribution across repositories", driven as a
/// process. Exit codes are the contract (ADR-010) and are what a gate reads.
/// </summary>
public sealed class CanonicalCommandTests {
    [Fact]
    public void DiffCanonical_OnSkalaItself_IsUnmanagedAndDoesNotFail() {
        // ⚠ Skala's own .editorconfig is the export with `root = true` (ADR-015) and has not been
        // synced. A repository that has not opted in must not be failed by a command it did not
        // ask for; the exit code is 0 and the report says what to run.
        var run = CliRunner.Run("config", "diff", "--canonical", ".");

        Assert.Equal(0, run.ExitCode);
        Assert.Contains("UNMANAGED", run.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("SK9014", run.StandardOutput, StringComparison.Ordinal);
    }

    [Fact]
    public void Sync_WithoutApply_WritesNothing() {
        using var repository = new TemporaryRepository();
        repository.Write(".editorconfig", "root = true\n\n[*.cs]\nindent_size = 2\n");
        var before = File.ReadAllBytes(repository.At(".editorconfig"));

        var run = CliRunner.Run("config", "sync", repository.Root);

        Assert.Equal(0, run.ExitCode);
        Assert.Contains("Would apply to", run.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Re-run with --apply", run.StandardOutput, StringComparison.Ordinal);
        Assert.Equal(before, File.ReadAllBytes(repository.At(".editorconfig")));
    }

    [Fact]
    public void Sync_ThenDiff_IsAClosedLoop() {
        using var repository = new TemporaryRepository();
        repository.Write(
            ".editorconfig",
            File.ReadAllText(
                Path.Combine(
                    CliRunner.RepositoryRoot,
                    "Core",
                    "Rikarin.Skala.Core.Tests",
                    "Fixtures",
                    "vixen.editorconfig"
                )
            )
        );

        var sync = CliRunner.Run("config", "sync", repository.Root, "--apply");
        Assert.Equal(0, sync.ExitCode);
        Assert.Contains("adopted the file", sync.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("[{Core,Gameplay,Platform}/**/*.cs]", sync.StandardOutput, StringComparison.Ordinal);

        var diff = CliRunner.Run("config", "diff", "--canonical", repository.Root);
        Assert.Equal(0, diff.ExitCode);
        Assert.Contains("CLEAN", diff.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("local sections  56", diff.StandardOutput, StringComparison.Ordinal);
    }

    [Fact]
    public void DiffCanonical_ExitsThreeWhenTheManagedBlockHasBeenEdited() {
        // The gate condition. `skala config diff --canonical` in CI is what turns drift from a
        // surprise into a finding.
        using var repository = new TemporaryRepository();
        CliRunner.Run("config", "sync", repository.Root, "--apply");

        var text = File.ReadAllText(repository.At(".editorconfig"));
        repository.Write(".editorconfig", text.Replace("indent_size = 4", "indent_size = 2", StringComparison.Ordinal));

        var run = CliRunner.Run("config", "diff", "--canonical", repository.Root);

        Assert.Equal(ConfigCommands.ConfigurationFailure, run.ExitCode);
        Assert.Contains("DRIFTED", run.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("SK9008", run.StandardOutput, StringComparison.Ordinal);
    }

    [Fact]
    public void Diff_WithoutTwoFilesAndWithoutCanonical_SaysSo() {
        var run = CliRunner.Run("config", "diff", "editor_config_template");

        // ⚠ 3, `ExitCodes.ConfigurationError`, not 2 — 2 is "formatting changes are needed" and
        // `config diff` formats nothing.
        Assert.Equal(3, run.ExitCode);
        Assert.Contains("needs two files", run.StandardOutput, StringComparison.Ordinal);
    }

    [Fact]
    public void Canonical_RegeneratesThePayloadIdentically() {
        // ADR-001's maintainer loop, end to end: composing from the export must reproduce the
        // checked-in payload byte for byte, or a re-export has silently diverged from what is
        // published.
        using var scratch = new TemporaryRepository();
        var run = CliRunner.Run(
            "config",
            "canonical",
            "editor_config_template",
            "--out",
            scratch.Root,
            "--version",
            "0.1.0"
        );

        Assert.Equal(0, run.ExitCode);
        Assert.Equal(
            File.ReadAllText(
                Path.Combine(
                    CliRunner.RepositoryRoot,
                    "Distribution",
                    "Rikarin.Skala.Canonical",
                    "canonical.editorconfig"
                )
            ),
            File.ReadAllText(scratch.At("canonical.editorconfig"))
        );
        Assert.Equal(
            File.ReadAllText(
                Path.Combine(CliRunner.RepositoryRoot, "Distribution", "Rikarin.Skala.Canonical", "canonical.json")
            ),
            File.ReadAllText(scratch.At("canonical.json"))
        );
    }

    sealed class TemporaryRepository : IDisposable {
        public TemporaryRepository() {
            Root = Path.Combine(Path.GetTempPath(), "skala-cli-canonical", Guid.NewGuid().ToString("n"));
            Directory.CreateDirectory(Root);
        }

        public string Root { get; }

        public string At(string relative) => System.IO.Path.Combine(Root, relative);

        public void Write(string relative, string text) => File.WriteAllText(At(relative), text);

        public void Dispose() {
            try {
                Directory.Delete(Root, recursive: true);
            } catch (IOException) {
                // A leftover temp directory is not worth failing a test over.
            }
        }
    }
}
