using Rikarin.Skala.Core.Configuration;
using Rikarin.Skala.Options;
using Rikarin.Skala.Testing;

namespace Rikarin.Skala.Cli.Tests;

/// <summary>
/// docs/plan/15 § M0, "Done when": these four are the milestone's acceptance criteria, run against
/// the real 4 238-line export through the real command line.
/// </summary>
public sealed class ConfigCommandTests {
    [Fact]
    public void Explain_PrintsEveryOptionWithItsSourceLineAndTier() {
        var run = CliRunner.Run("config", "explain", "Core/Rikarin.Skala.Core/Sample.cs");

        Assert.Equal(0, run.ExitCode);
        Assert.Contains("Effective configuration for", run.StandardOutput, StringComparison.Ordinal);

        // One row per option in the registry, and every configured one names a file and a line.
        var rows = run.Lines.Where(static line => line.Contains(".editorconfig:", StringComparison.Ordinal)).ToArray();
        Assert.True(rows.Length > 400, $"only {rows.Length} options carried provenance");

        var width = Assert.Single(
            run.Lines,
            line => line.StartsWith("resharper_csharp_max_line_length ", StringComparison.Ordinal)
        );
        Assert.Contains("120", width, StringComparison.Ordinal);
        Assert.Contains(".editorconfig:", width, StringComparison.Ordinal);

        // ⚠ Tier A since milestone 3, and it was Tier D before it for a reason worth remembering:
        // milestone 1 read `max_line_length` and could not act on it, because nothing wrapped. A
        // tier is a claim about behaviour, so it moved when the behaviour arrived and not when the
        // option was first read.
        Assert.Contains(" A ", width, StringComparison.Ordinal);
    }

    [Fact]
    public void Explain_ListsTheChainAndWhetherItTerminated() {
        var run = CliRunner.Run("config", "explain", "Core/Rikarin.Skala.Core/Sample.cs");

        Assert.Contains(".editorconfig  (root = true)", run.StandardOutput, StringComparison.Ordinal);
        Assert.DoesNotContain("reached the filesystem root", run.StandardOutput, StringComparison.Ordinal);
    }

    [Fact]
    public void Explain_CanBePointedAtTheExportBeforeItIsInstalled() {
        var run = CliRunner.Run("config", "explain", "Core/Foo.cs", "--config", "editor_config_template");

        Assert.Equal(0, run.ExitCode);
        Assert.Contains(
            "reached the filesystem root without finding `root = true`",
            run.StandardOutput,
            StringComparison.Ordinal
        );

        var width = Assert.Single(
            run.Lines,
            line => line.StartsWith("resharper_csharp_max_line_length ", StringComparison.Ordinal)
        );
        Assert.Contains("editor_config_template:", width, StringComparison.Ordinal);
    }

    [Fact]
    public void TheExport_IsNeverWrittenTo() {
        // It is the input and the fixture for everything in M0, and no command may change it.
        var before = File.ReadAllBytes(CliRunner.Template);
        CliRunner.Run("config", "check", "editor_config_template");
        CliRunner.Run("config", "explain", "Core/Foo.cs", "--config", "editor_config_template");
        CliRunner.Run("config", "fix", "editor_config_template");

        Assert.Equal(before, File.ReadAllBytes(CliRunner.Template));
    }

    [Fact]
    public void Check_NamesTheThreeContradictions_TheMissingRoot_AndTheMissingWidth() {
        var run = CliRunner.Run("config", "check", "editor_config_template");

        Assert.Equal(0, run.ExitCode);

        Assert.Contains("SK9005", run.StandardOutput, StringComparison.Ordinal);
        Assert.Contains(
            "'insert_final_newline = false' contradicts 'resharper_csharp_insert_final_newline = true'",
            run.StandardOutput,
            StringComparison.Ordinal
        );
        Assert.Contains(
            "'trim_trailing_whitespace = false' contradicts 'resharper_remove_spaces_on_blank_lines = true'",
            run.StandardOutput,
            StringComparison.Ordinal
        );
        Assert.Contains(
            "'end_of_line = lf' contradicts 'resharper_enforce_line_ending_style = false'",
            run.StandardOutput,
            StringComparison.Ordinal
        );

        Assert.Contains("has no `root = true`", run.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("no `max_line_length`", run.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("resharper_csharp_max_line_length = 120", run.StandardOutput, StringComparison.Ordinal);
    }

    [Fact]
    public void Check_UnderStrict_FailsOnTheTemplate() {
        var run = CliRunner.Run("config", "check", "editor_config_template", "--strict");

        Assert.Equal(ConfigCommands.StrictFailure, run.ExitCode);
    }

    [Fact]
    public void Check_ReportsTheTierMatrixAndTheSeverityNamespacesSeparately() {
        var run = CliRunner.Run("config", "check", "editor_config_template");

        Assert.Contains($"{OptionRegistry.Count} options known", run.StandardOutput, StringComparison.Ordinal);
        // Milestone 1 promoted the phase-1 keys; the count is a progress bar and moves per
        // milestone, so the assertion is that it is honest rather than that it is a number.
        var implemented = OptionRegistry.All.Count(static info => info.Tier == OptionTier.A);
        Assert.Contains($"Tiers — A (implemented): {implemented}", run.StandardOutput, StringComparison.Ordinal);
        Assert.True(implemented > 0);
        Assert.Contains("InspectionSeverity", run.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Milestone 5", run.StandardOutput, StringComparison.Ordinal);
    }

    [Fact]
    public void Distill_WritesAFileThatResolvesIdentically() {
        var output = Path.Combine(Path.GetTempPath(), $"skala-distill-{Guid.NewGuid():N}.editorconfig");
        try {
            var run = CliRunner.Run("config", "distill", "editor_config_template", "--out", output);

            Assert.Equal(0, run.ExitCode);
            Assert.True(File.Exists(output));

            var probe = Path.Combine(CliRunner.RepositoryRoot, "Probe.cs");
            var before = ConfigCommands.ResolveStandalone(CliRunner.Template, probe);
            var after = OptionResolver.Resolve(
                EditorConfigChain.Of(
                    probe,
                    EditorConfigDocument.FromText(
                        Path.Combine(CliRunner.RepositoryRoot, ".x.editorconfig"),
                        File.ReadAllText(output)
                    )
                )
            );

            for (var i = 0; i < OptionRegistry.Count; i++) {
                Assert.Equal(before[(OptionId)i].Value, after[(OptionId)i].Value);
            }
        } finally {
            File.Delete(output);
        }
    }

    [Fact]
    public void Distill_SaysWhatAKeyHasToProveBeforeItIsDropped() {
        var run = CliRunner.Run(
            "config",
            "distill",
            "editor_config_template",
            "--out",
            Path.Combine(Path.GetTempPath(), $"skala-{Guid.NewGuid():N}.editorconfig")
        );

        // ⚠ Not "0 key(s) dropped" any more. Until milestone 3 the answer was that distill could
        // drop nothing, because no default had been checked against anything; the derived table
        // changed the answer and the explanation has to say what the evidence now is.
        Assert.Contains("key(s) dropped", run.StandardOutput, StringComparison.Ordinal);
        Assert.DoesNotContain("0 key(s) dropped", run.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("never its default", run.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("oracle-probe", run.StandardOutput, StringComparison.Ordinal);
    }

    [Fact]
    public void Diff_ReportsNoSemanticDifferenceBetweenTheTemplateAndTheRepositoryConfig() {
        // .editorconfig is the export with `root = true` prepended (ADR-015), and `root` is not a
        // style option, so the two must resolve to the same set.
        var run = CliRunner.Run("config", "diff", "editor_config_template", ".editorconfig");

        Assert.Equal(0, run.ExitCode);
        Assert.Contains("No semantic difference", run.StandardOutput, StringComparison.Ordinal);
    }

    [Fact]
    public void Diff_ReportsAChangedOption() {
        var changed = Path.Combine(CliRunner.RepositoryRoot, $"skala-diff-{Guid.NewGuid():N}.editorconfig");
        try {
            File.WriteAllText(changed, "root = true\n[*]\nresharper_csharp_max_line_length = 100\n");
            var baseline = Path.Combine(CliRunner.RepositoryRoot, $"skala-diff-{Guid.NewGuid():N}.editorconfig");
            File.WriteAllText(baseline, "root = true\n[*]\nresharper_csharp_max_line_length = 120\n");

            var run = CliRunner.Run("config", "diff", baseline, changed);

            Assert.Contains(
                "resharper_csharp_max_line_length: 120 -> 100",
                run.StandardOutput,
                StringComparison.Ordinal
            );
            Assert.Contains("1 option(s) differ", run.StandardOutput, StringComparison.Ordinal);
            File.Delete(baseline);
        } finally {
            File.Delete(changed);
        }
    }

    [Fact]
    public void Fix_WithoutApply_ChangesNothingOnDisk() {
        var before = File.ReadAllText(CliRunner.Template);
        var run = CliRunner.Run("config", "fix", "editor_config_template");

        Assert.Equal(0, run.ExitCode);
        Assert.Contains("Would apply to", run.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("root = true", run.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("max_line_length = 120", run.StandardOutput, StringComparison.Ordinal);
        Assert.Equal(before, File.ReadAllText(CliRunner.Template));
    }

    [Fact]
    public void Fix_WithApply_RepairsACopy() {
        var copy = Path.Combine(Path.GetTempPath(), $"skala-fix-{Guid.NewGuid():N}.editorconfig");
        try {
            File.Copy(CliRunner.Template, copy);
            var run = CliRunner.Run("config", "fix", copy, "--apply");

            Assert.Equal(0, run.ExitCode);
            Assert.Contains("Applied to", run.StandardOutput, StringComparison.Ordinal);

            var document = EditorConfigDocument.Load(copy);
            Assert.True(document.IsRoot);
            Assert.Equal("120", Assert.Single(document.Assignments, static a => a.Key == "max_line_length").Value);
        } finally {
            File.Delete(copy);
        }
    }

    [Fact]
    public void UnknownCommand_FailsWithoutAStackTrace() {
        var run = CliRunner.Run("config", "nonsense");

        Assert.NotEqual(0, run.ExitCode);
        Assert.DoesNotContain("Unhandled exception", run.StandardError, StringComparison.Ordinal);
    }
}
