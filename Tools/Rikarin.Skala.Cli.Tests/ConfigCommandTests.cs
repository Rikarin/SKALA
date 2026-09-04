using Rikarin.Skala.Core.Configuration;
using Rikarin.Skala.Options;
using Rikarin.Skala.Testing;

namespace Rikarin.Skala.Cli.Tests;

/// <summary>
///     docs/plan/15 § M0, "Done when": these four are the milestone's acceptance criteria, run against
///     the real 4 238-line export through the real command line.
/// </summary>
public sealed class ConfigCommandTests {
    [Fact]
    public void Explain_PrintsEveryOptionWithItsSourceLineAndTier() {
        var run = CliRunner.Run("config", "explain", "Core/Rikarin.Skala.Core/Sample.cs");

        Assert.Equal(0, run.ExitCode);
        Assert.Contains("Effective configuration for", run.StandardOutput, StringComparison.Ordinal);

        // One row per option in the registry, and every configured one names a file and a line.
        var rows = run.Lines.Where(static line => line.Contains(".editorconfig:", StringComparison.Ordinal)).ToArray();
        // ⚠ 373, measured. The floor was 400 against a registry of 510 whose export set the same
        // option under two spellings; it is 436 options now and one line per option. A floor above
        // the real figure fails loudly, which is why this is a ratchet and not a guess — moving it
        // down is a deliberate edit that says which options stopped being configured.
        Assert.True(rows.Length >= 373, $"only {rows.Length} options carried provenance");

        var width = Assert.Single(
            run.Lines,
            static line => line.StartsWith("skala_max_line_length ", StringComparison.Ordinal)
        );
        Assert.Contains("120", width, StringComparison.Ordinal);
        Assert.Contains(".editorconfig:", width, StringComparison.Ordinal);

        // ⚠ Tier D, and it has now been D for two different reasons — which is the point of the
        // tier. Milestone 1 read `max_line_length` and could not act on it, because nothing wrapped;
        // milestone 3 promoted it to A when the wrapping arrived. The key-flip sweep put it back:
        // both engines move across `120`, `0` and `1`, and they agree only at `120`. Confirmed
        // unbatched with `sweep verify skala_max_line_length`, which is what doc 12
        // requires before a row is acted on.
        //
        // A tier is a claim about behaviour matching Rider's, so it tracks the measurement in both
        // directions. This asserts the tier is printed, not which one it is — the tier itself is
        // pinned by the sweep's committed table and by OptionCoverageTests, and duplicating the
        // claim here only means two places to update when the measurement moves.
        Assert.Matches(@"\s[ABCD]\s", width);
    }

    [Fact]
    public void Explain_ListsTheChainAndWhetherItTerminated() {
        var run = CliRunner.Run("config", "explain", "Core/Rikarin.Skala.Core/Sample.cs");

        Assert.Contains(".editorconfig  (root = true)", run.StandardOutput, StringComparison.Ordinal);
        Assert.DoesNotContain("reached the filesystem root", run.StandardOutput, StringComparison.Ordinal);
    }

    [Fact]
    public void Explain_CanBePointedAtTheExportBeforeItIsInstalled() {
        // ⚠ The probe sits beside the configuration. A section glob is matched relative to the
        // directory its .editorconfig is in, so a probe under the repository root resolves nothing
        // against a file elsewhere and every row comes back `(default)` with no provenance.
        var run = CliRunner.Run(
            "config",
            "explain",
            CliRunner.TranslatedTemplateProbe,
            "--config",
            CliRunner.TranslatedTemplate
        );

        Assert.Equal(0, run.ExitCode);
        Assert.Contains(
            "reached the filesystem root without finding `root = true`",
            run.StandardOutput,
            StringComparison.Ordinal
        );

        var width = Assert.Single(
            run.Lines,
            static line => line.StartsWith("skala_max_line_length ", StringComparison.Ordinal)
        );

        // ⚠ The file it was pointed at, which is no longer `editor_config_template` itself. Skala
        // reads `skala_*`; a Rider export is spelled in ReSharper's namespace and `config explain`
        // on it prints a table of defaults with no provenance at all. What is still worth asserting,
        // and is what this test was for, is that `--config` resolves against a file that is not yet
        // installed anywhere and reports where each value came from.
        Assert.Contains(Path.GetFileName(CliRunner.TranslatedTemplate) + ":", width, StringComparison.Ordinal);
    }

    [Fact]
    public void TheExport_IsNeverWrittenTo() {
        // It is the input and the fixture for everything in M0, and no command may change it.
        var before = File.ReadAllBytes(CliRunner.Template);
        CliRunner.Run("config", "check", CliRunner.TranslatedTemplate);
        CliRunner.Run("config", "explain", "Core/Foo.cs", "--config", CliRunner.TranslatedTemplate);
        CliRunner.Run("config", "fix", CliRunner.TranslatedTemplate);

        Assert.Equal(before, File.ReadAllBytes(CliRunner.Template));
    }

    [Fact]
    public void Check_NamesTheThreeContradictions_TheMissingRoot_AndTheMissingWidth() {
        var run = CliRunner.Run("config", "check", CliRunner.TranslatedTemplate);

        Assert.Equal(0, run.ExitCode);

        Assert.Contains("SK9005", run.StandardOutput, StringComparison.Ordinal);

        // ⚠ Two, not three. The third was `resharper_csharp_insert_final_newline = true` against the
        // bare `insert_final_newline = false` — one option written twice — and it is resolved rather
        // than lost: the translation emits one line per option per section, carrying the value
        // ReSharper's specificity ordering picks. `ConfigurationDiagnosticsTests
        // .TheTranslatedExport_HasNoSameOptionContradiction` is what holds that ground.
        Assert.DoesNotContain("skala_insert_final_newline' contradicts", run.StandardOutput, StringComparison.Ordinal);
        Assert.Contains(
            "'trim_trailing_whitespace = false' contradicts 'skala_remove_spaces_on_blank_lines = true'",
            run.StandardOutput,
            StringComparison.Ordinal
        );
        Assert.Contains(
            "'end_of_line = lf' contradicts 'skala_enforce_line_ending_style = false'",
            run.StandardOutput,
            StringComparison.Ordinal
        );

        Assert.Contains("has no `root = true`", run.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("no `max_line_length`", run.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("skala_max_line_length = 120", run.StandardOutput, StringComparison.Ordinal);
    }

    [Fact]
    public void Check_UnderStrict_FailsOnTheTemplate() {
        var run = CliRunner.Run("config", "check", CliRunner.TranslatedTemplate, "--strict");

        Assert.Equal(ConfigCommands.StrictFailure, run.ExitCode);
    }

    /// <summary>
    ///     The tier matrix is about the registry; the severity namespaces are about keys the registry
    ///     deliberately does not own, and the two are reported separately.
    /// </summary>
    /// <remarks>
    ///     ⚠ <b>The severity half runs against its own file on purpose.</b> It used to read
    ///     <c>editor_config_template</c> and assert the output named <c>InspectionSeverity</c> — which
    ///     broke the moment the 1 062 <c>resharper_*_highlighting</c> lines were removed from that
    ///     export, because the block prints only the namespace groups that are present. That says
    ///     nothing about whether the reporting works, and a real Rider export still carries about three
    ///     thousand of these. The registry half still reads the template, because the tier matrix is a
    ///     fact about the registry and holds whatever the file contains.
    /// </remarks>
    [Fact]
    public void Check_ReportsTheTierMatrixAndTheSeverityNamespacesSeparately() {
        var run = CliRunner.Run("config", "check", CliRunner.TranslatedTemplate);

        Assert.Contains($"of {OptionRegistry.Count} known options", run.StandardOutput, StringComparison.Ordinal);
        // Milestone 1 promoted the phase-1 keys; the count is a progress bar and moves per
        // milestone, so the assertion is that it is honest rather than that it is a number.
        var implemented = OptionRegistry.All.Count(static info => info.Tier == OptionTier.A);
        Assert.Contains(
            $"Registry-wide — A (implemented, pinned by an oracle fixture): {implemented}",
            run.StandardOutput,
            StringComparison.Ordinal
        );

        // ⚠ Tier D is labelled "not pinned by a fixture", and the wording is the point rather than
        // decoration. It read "not implemented", which is false: Tier A is the narrow claim that the
        // formatter reads the option *and* a fixture pins it, so D has only ever meant "not A", and
        // 70 of the 161 C+D options are read by production code by name — `skala_max_line_length`,
        // the column limit the wrapping engine runs on, among them.
        Assert.DoesNotContain("D (not implemented)", run.StandardOutput, StringComparison.Ordinal);
        Assert.Contains(
            $"D (not pinned by a fixture): {OptionRegistry.All.Count(static info => info.Tier == OptionTier.D)}",
            run.StandardOutput,
            StringComparison.Ordinal
        );

        Assert.True(implemented > 0);

        var directory = Path.Combine(Path.GetTempPath(), $"skala-namespaces-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try {
            File.WriteAllText(
                Path.Combine(directory, ".editorconfig"),
                """
                root = true

                [*.cs]
                resharper_web_config_module_not_resolved_highlighting = warning
                dotnet_diagnostic.CA1822.severity = suggestion

                """
            );
            File.WriteAllText(Path.Combine(directory, "a.cs"), "class C { }\n");

            var namespaces = CliRunner.Run("config", "check", directory);

            // ⚠ `InspectionSeverity` is gone as a namespace, so the `_highlighting` key is reported
            // as an ordinary unknown option and `DiagnosticSeverity` is the only severity namespace
            // `config check` still names. Asserting the absence is the half that fails if the
            // ReSharper special case comes back.
            Assert.DoesNotContain("InspectionSeverity", namespaces.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("DiagnosticSeverity", namespaces.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("SK9001", namespaces.StandardOutput, StringComparison.Ordinal);
        } finally {
            Directory.Delete(directory, true);
        }
    }

    /// <summary>
    ///     ⚠ The number a user needs is about <em>their</em> configuration, not about the registry.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         This block reported only the registry-wide split until M9. On the real export that reads
    ///         "A: 221, D: 293" — true, and not an answer to the question being asked, which is "of the
    ///         keys I set, which ones does the tool ignore?" On the export the answer is in the hundreds,
    ///         and nothing looked wrong because fidelity is 99.7 %: an unimplemented key whose configured
    ///         value happens to match what Skala does anyway costs no fidelity. The exposure is
    ///         forward-looking — change one of those settings in Rider and Skala keeps formatting the old
    ///         way, reporting nothing.
    ///     </para>
    ///     <para>
    ///         ⚠ And the per-configuration split has to add up, or it is worse than the registry-wide one:
    ///         a number that does not reconcile invites the reader to assume the remainder is fine.
    ///     </para>
    /// </remarks>
    [Fact]
    public void Check_ReportsTheTierSplitOfTheKeysTheConfigurationActuallySets() {
        var run = CliRunner.Run("config", "check", CliRunner.TranslatedTemplate);
        var resolution = ConfigCommands.ResolveStandalone(
            CliRunner.TranslatedTemplate,
            CliRunner.TranslatedTemplateProbe
        );

        var configured = resolution.Configured.ToList();
        var applied = configured.Count(static o => o.Info.Tier is OptionTier.A or OptionTier.B);
        var inert = configured.Count(static o => o.Info.Inert is not null);
        var ignored = configured.Count(static o =>
            o.Info.Tier is OptionTier.C or OptionTier.D && o.Info.Inert is null
        );

        // The export sets hundreds of keys, and a good few of them are not implemented. If either
        // of those stops being true this test is measuring nothing.
        Assert.True(configured.Count > 100, $"The export set only {configured.Count} options.");
        Assert.True(ignored > 0, "No unimplemented key is set, so the gap this report exists for is untested.");
        Assert.Equal(configured.Count, applied + inert + ignored);

        Assert.Contains(
            $"This configuration sets {configured.Count} of {OptionRegistry.Count} known options.",
            run.StandardOutput,
            StringComparison.Ordinal
        );

        Assert.Contains(
            $"{applied} applied · {ignored} not implemented · {inert} inert",
            run.StandardOutput,
            StringComparison.Ordinal
        );

        // ⚠ And it comes before the registry-wide totals. The order is the point: the first number
        // a reader meets should be the one about them.
        var mine = run.StandardOutput.IndexOf("This configuration sets", StringComparison.Ordinal);
        var registry = run.StandardOutput.IndexOf("Registry-wide", StringComparison.Ordinal);
        Assert.True(mine >= 0 && registry > mine, "The per-configuration split must precede the registry totals.");
    }

    /// <summary>
    ///     ⚠ Inert is reported apart from unimplemented, and the xmldoc family says why it is neither.
    /// </summary>
    /// <remarks>
    ///     An inert key is honoured vacuously — no input distinguishes its values — so counting it as
    ///     a gap makes the gap number noise and people stop reading it. The xmldoc family is the
    ///     opposite trap: unimplemented keys with no explanation read as neglect, when the cause is
    ///     documented (SK-DIV-0006).
    ///     <para>
    ///         ⚠ The explanation this printed used to be "the oracle does not format documentation
    ///         comments, so there is nothing to verify them against", and it was doubly wrong: the oracle
    ///         does format them under a profile that enables <c>CSharpFormatDocComments</c>, and the
    ///         obstacle is therefore the committed profiles rather than the tool. "Permanent" was the load
    ///         -bearing word and it was the false one.
    ///     </para>
    /// </remarks>
    [Fact]
    public void Check_SeparatesInertFromUnimplemented_AndExplainsTheXmldocFamily() {
        var run = CliRunner.Run("config", "check", CliRunner.TranslatedTemplate);

        Assert.Contains("inert (honoured vacuously", run.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("largest unimplemented families:", run.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("xmldoc*", run.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("SK-DIV-0006", run.StandardOutput, StringComparison.Ordinal);
    }

    /// <summary>
    ///     The end-to-end shape of SK9017: six bad values, six diagnostics, exit 3.
    /// </summary>
    /// <remarks>
    ///     ⚠ Driven through the real binary and a real <c>.editorconfig</c>, because the defect was
    ///     never in the detection. <c>OptionResolver</c> has always detected every one of these and put
    ///     the reason in <c>ResolutionResult.ValueErrors</c>, which no command read: <c>config check</c>
    ///     printed "No configuration diagnostics"-worth of nothing about them and exited 0, and
    ///     <c>config explain</c> reported the keys as <c>(default)</c>. A unit test on the resolver
    ///     would have passed throughout.
    ///     <para>
    ///         ⚠ <c>skala_keep_existing_declaration_block_arrangement</c> is in the fixture on purpose. It is
    ///         the one where a discarded value is not a cosmetic difference: the arranger goes on to
    ///         rearrange the file on the strength of a setting the tool threw away.
    ///     </para>
    /// </remarks>
    [Fact]
    public void Check_ReportsEveryOutOfDomainValue_AndFailsWithoutStrict() {
        var directory = Path.Combine(Path.GetTempPath(), $"skala-valcheck-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try {
            File.WriteAllText(
                Path.Combine(directory, ".editorconfig"),
                """
                root = true

                [*.cs]
                indent_size = tab
                skala_max_line_length = -1
                skala_accessor_owner_body = sideways
                skala_wrap_arguments_style = sideways
                skala_keep_existing_declaration_block_arrangement = maybe
                csharp_using_directive_placement = nowhere
                skala_indent_size = 0

                """
            );
            File.WriteAllText(Path.Combine(directory, "a.cs"), "class C { }\n");

            var run = CliRunner.Run("config", "check", directory);

            // docs/plan/09 § "Exit codes": 3 is the configuration error, and this one does not wait
            // for --strict.
            Assert.Equal(ConfigCommands.ConfigurationFailure, run.ExitCode);
            Assert.Contains("SK9017: 6", run.StandardOutput, StringComparison.Ordinal);

            foreach (var (key, value, effective) in new[] {
                         ("skala_max_line_length", "-1", "120"),
                         ("skala_accessor_owner_body", "sideways", "expression_body"),
                         ("skala_wrap_arguments_style", "sideways", "chop_if_long"),
                         ("skala_keep_existing_declaration_block_arrangement", "maybe", "false"),
                         ("csharp_using_directive_placement", "nowhere", "outside_namespace"),
                         ("skala_indent_size", "0", "4")
                     }) {
                Assert.Contains(
                    $"'{key} = {value}' is not a value this option accepts",
                    run.StandardOutput,
                    StringComparison.Ordinal
                );

                // ⚠ The clause a reader can act on. Without it they know their line was refused and
                // still cannot tell what their code is being formatted with.
                Assert.Contains($"'{effective}' is in force instead", run.StandardOutput, StringComparison.Ordinal);
            }

            // ⚠ `indent_size = tab` is the seventh line and is *not* one of the six. It is legal
            // EditorConfig — the specification defines it as "use tab_width" — and Skala refused it
            // for as long as the key was typed `int`.
            Assert.DoesNotContain("indent_size = tab' is not", run.StandardOutput, StringComparison.Ordinal);
        } finally {
            Directory.Delete(directory, true);
        }
    }

    /// <summary>A refused key may not be printed as <c>(default)</c> with nothing else said.</summary>
    [Fact]
    public void Explain_ShowsTheEffectiveValueAndTheRefusedLine() {
        var directory = Path.Combine(Path.GetTempPath(), $"skala-valexplain-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try {
            File.WriteAllText(
                Path.Combine(directory, ".editorconfig"),
                "root = true\n[*.cs]\nskala_wrap_arguments_style = sideways\n"
            );
            var source = Path.Combine(directory, "a.cs");
            File.WriteAllText(source, "class C { }\n");

            var run = CliRunner.Run("config", "explain", source);

            var row = Assert.Single(
                run.Lines,
                static line => line.StartsWith("skala_wrap_arguments_style ", StringComparison.Ordinal)
            );

            Assert.Contains("chop_if_long", row, StringComparison.Ordinal);
            Assert.Contains("SK9017", row, StringComparison.Ordinal);
            Assert.Contains(".editorconfig:3", row, StringComparison.Ordinal);
            Assert.Contains("not what the file says", run.StandardOutput, StringComparison.Ordinal);
        } finally {
            Directory.Delete(directory, true);
        }
    }

    [Fact]
    public void Distill_WritesAFileThatResolvesIdentically() {
        var output = Path.Combine(Path.GetTempPath(), $"skala-distill-{Guid.NewGuid():N}.editorconfig");
        try {
            var run = CliRunner.Run("config", "distill", CliRunner.TranslatedTemplate, "--out", output);

            Assert.Equal(0, run.ExitCode);
            Assert.True(File.Exists(output));

            var probe = CliRunner.TranslatedTemplateProbe;
            var before = ConfigCommands.ResolveStandalone(CliRunner.TranslatedTemplate, probe);
            var after = OptionResolver.Resolve(
                EditorConfigChain.Of(
                    probe,
                    EditorConfigDocument.FromText(
                        Path.Combine(Path.GetDirectoryName(CliRunner.TranslatedTemplate)!, ".x.editorconfig"),
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
            CliRunner.TranslatedTemplate,
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
        // .editorconfig is the *translated* export with `root = true` prepended (ADR-015), and `root`
        // is not a style option, so the two must resolve to the same set.
        //
        // ⚠ Both files are compared from one directory. `config diff` resolves each against its own
        // location, and a section glob is relative to that, so comparing across directories reports
        // every option as "-> (default)" — the same value on both sides, labelled a difference.
        var directory = Path.GetDirectoryName(CliRunner.TranslatedTemplate)!;
        var repository = Path.Combine(directory, "repository.editorconfig");
        File.Copy(Path.Combine(CliRunner.RepositoryRoot, ".editorconfig"), repository, true);
        try {
            var run = CliRunner.Run("config", "diff", CliRunner.TranslatedTemplate, repository);

            Assert.Equal(0, run.ExitCode);
            Assert.Contains("No semantic difference", run.StandardOutput, StringComparison.Ordinal);
        } finally {
            File.Delete(repository);
        }
    }

    [Fact]
    public void Diff_ReportsAChangedOption() {
        var changed = Path.Combine(CliRunner.RepositoryRoot, $"skala-diff-{Guid.NewGuid():N}.editorconfig");
        try {
            File.WriteAllText(changed, "root = true\n[*]\nskala_max_line_length = 100\n");
            var baseline = Path.Combine(CliRunner.RepositoryRoot, $"skala-diff-{Guid.NewGuid():N}.editorconfig");
            File.WriteAllText(baseline, "root = true\n[*]\nskala_max_line_length = 120\n");

            var run = CliRunner.Run("config", "diff", baseline, changed);

            Assert.Contains(
                "skala_max_line_length: 120 -> 100",
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
        var run = CliRunner.Run("config", "fix", CliRunner.TranslatedTemplate);

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
            File.Copy(CliRunner.TranslatedTemplate, copy);
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
