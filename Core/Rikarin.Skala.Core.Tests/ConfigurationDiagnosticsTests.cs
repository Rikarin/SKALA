using Rikarin.Skala.Core.Configuration;
using Rikarin.Skala.Core.Diagnostics;
using Rikarin.Skala.Options;
using System.Collections.Immutable;

namespace Rikarin.Skala.Core.Tests;

public sealed class ConfigurationDiagnosticsTests {
    /// <summary>
    ///     The export, as Skala reads it.
    /// </summary>
    /// <remarks>
    ///     ⚠ Translated first, and it has to be: the export is spelled in ReSharper's namespace, Skala
    ///     resolves none of it, and analysing the raw file reports every finding below as absent —
    ///     three contradictions became zero, which reads as "the export is clean" rather than as "the
    ///     analyser was handed a file it cannot read". The configuration being analysed is the same
    ///     one; only the spelling changed.
    /// </remarks>
    static ImmutableArray<SkalaDiagnostic> AnalyzeTemplate() {
        var document = EditorConfigDocument.FromText(
            RepositoryPaths.Template,
            CanonicalEditorConfig.Translate(File.ReadAllText(RepositoryPaths.Template))
        );
        var probe = Path.Combine(RepositoryPaths.Root, "Probe.cs");
        return ConfigurationAnalyzer.Analyze(
            OptionResolver.Resolve(EditorConfigChain.Of(probe, document)),
            RepositoryPaths.Root
        );
    }

    /// <summary>
    ///     The contradictions the export contains between two <em>different</em> options.
    /// </summary>
    /// <remarks>
    ///     ⚠ <b>Two, where docs/plan/15 § M0 counted three.</b> The third was
    ///     <c>resharper_csharp_insert_final_newline = true</c> against the bare
    ///     <c>insert_final_newline = false</c> — one option written twice, disagreeing — and it is not
    ///     missing, it is <em>resolved</em>: <see cref="CanonicalEditorConfig.Translate" /> emits one
    ///     line per option per section carrying the value ReSharper's specificity ordering picks, so
    ///     a same-option contradiction cannot survive into anything Skala reads or ships.
    ///     <see cref="TheTranslatedExport_HasNoSameOptionContradiction" /> is what holds that ground,
    ///     because a count that simply dropped from three to two would look identical to the
    ///     translation quietly losing a diagnostic.
    ///     <para>
    ///         The two that remain are between different options that disagree about the same
    ///         behaviour, which no rename can resolve — somebody has to choose.
    ///     </para>
    /// </remarks>
    [Fact]
    public void SK9005_NamesTheContradictionsInTheRealTemplate() {
        var messages = AnalyzeTemplate()
            .Where(static diagnostic => diagnostic.Id == ConfigDiagnosticIds.ContradictoryOptions)
            .Select(static diagnostic => diagnostic.Message)
            .ToArray();

        Assert.Equal(2, messages.Length);

        var whitespace = Assert.Single(
            messages,
            static m => m.Contains("trim_trailing_whitespace", StringComparison.Ordinal)
        );
        Assert.Contains("skala_remove_spaces_on_blank_lines = true", whitespace, StringComparison.Ordinal);
        Assert.Contains("the C# key wins", whitespace, StringComparison.Ordinal);

        var lineEndings = Assert.Single(messages, static m => m.Contains("end_of_line = lf", StringComparison.Ordinal));
        Assert.Contains("skala_enforce_line_ending_style = false", lineEndings, StringComparison.Ordinal);
        Assert.Contains("the C# key wins", lineEndings, StringComparison.Ordinal);
    }

    /// <summary>
    ///     No option is written twice, disagreeing, in the file Skala reads or ships.
    /// </summary>
    /// <remarks>
    ///     ⚠ This is the third contradiction's replacement, and it asserts something stronger than the
    ///     diagnostic did: not "we report it" but "it cannot occur". The export writes
    ///     <c>resharper_csharp_insert_final_newline = true</c> and <c>insert_final_newline = false</c>
    ///     in one section; a translation that emitted both under one name would leave editorconfig's
    ///     last-assignment-wins rule to choose, which is the <em>opposite</em> of ReSharper's answer
    ///     wherever the export happens to put the generic key second. Every consuming repository would
    ///     have been formatted against that.
    /// </remarks>
    [Fact]
    public void TheTranslatedExport_HasNoSameOptionContradiction() {
        var translated = CanonicalEditorConfig.Translate(File.ReadAllText(RepositoryPaths.Template));
        var document = EditorConfigDocument.FromText(RepositoryPaths.Template, translated);
        var resolution = OptionResolver.Resolve(
            EditorConfigChain.Of(Path.Combine(RepositoryPaths.Root, "Probe.cs"), document)
        );

        Assert.NotEmpty(resolution.Configured);
        foreach (var option in resolution.Configured) {
            var values = option.Candidates.Select(static c => c.Value).Distinct(StringComparer.Ordinal).ToArray();
            Assert.True(
                values.Length == 1,
                $"{option.Info.Key} is set to {values.Length} different values by one translated export: "
                + string.Join(", ", values)
            );
        }

        // The value kept is the one ReSharper's specificity ordering picks, not the last one written.
        Assert.True(OptionRegistry.TryResolve("insert_final_newline", out var id));
        Assert.Equal("true", resolution[id].Value);
    }

    [Fact]
    public void SK9001_IsInfo_AndSuggestsANearbyKey() {
        var document = EditorConfigDocument.FromText(
            "/repo/.editorconfig",
            """
            root = true
            [*]
            skala_wrap_argument_style = chop_if_long
            """
        );

        var diagnostic = Assert.Single(
            ConfigurationAnalyzer.Analyze(OptionResolver.Resolve(EditorConfigChain.Of("/repo/File.cs", document))),
            static d => d.Id == ConfigDiagnosticIds.UnknownKey
        );

        // ⚠ Info, not warning: the export contains ~2 000 keys Skala will never implement, and a
        // tool that emits two thousand warnings on first run gets uninstalled on first run.
        Assert.Equal(SkalaSeverity.Info, diagnostic.Severity);
        Assert.Contains(
            "did you mean 'skala_wrap_arguments_style'",
            diagnostic.Message,
            StringComparison.Ordinal
        );
    }

    /// <summary>
    ///     A Roslyn diagnostic severity and a naming key are classified rather than reported as unknown
    ///     options; an inspection severity is not, and no longer has a namespace of its own.
    /// </summary>
    /// <remarks>
    ///     ⚠ <b>The sample is inline on purpose.</b> This test used to read the repository's own
    ///     <c>editor_config_template</c> and assert it contained an <c>InspectionSeverity</c> key —
    ///     so it broke the moment somebody legitimately removed the 1 062 <c>_highlighting</c> lines
    ///     from it, which says nothing about whether the classifier still works.
    ///     <para>
    ///         ⚠
    ///         <b>
    ///             <c>KeyNamespace.InspectionSeverity</c> is gone and this test now asserts its
    ///             absence.
    ///         </b> It existed so that a Rider export's ~3 000 inspection severities did not each
    ///         become an <c>SK9001</c>. Skala reads none of that vocabulary any more, so a
    ///         <c>_highlighting</c> key is an ordinary unknown key — which is the second assertion here,
    ///         and it is the one that fails if the special case comes back.
    ///     </para>
    ///     <para>
    ///         The two that stay are not Skala's to implement: <c>dotnet_diagnostic.*.severity</c> and
    ///         <c>dotnet_naming_*</c> are read by Roslyn, so reporting them as options Skala lacks would
    ///         be wrong rather than merely noisy.
    ///     </para>
    /// </remarks>
    [Fact]
    public void SK9001_IgnoresTheSeverityNamespacesRoslynOwns_AndReportsAnInspectionSeverity() {
        var document = EditorConfigDocument.FromText(
            "/repo/.editorconfig",
            """
            root = true
            [*]
            resharper_web_config_module_not_resolved_highlighting = warning
            dotnet_diagnostic.CA1822.severity = suggestion
            dotnet_naming_rule.constants_rule.severity = warning
            """
        );

        var resolution = OptionResolver.Resolve(EditorConfigChain.Of("/repo/File.cs", document));
        var diagnostics = ConfigurationAnalyzer.Analyze(resolution);

        Assert.Equal(
            KeyNamespace.Option,
            Assert.Single(
                resolution.Unknown,
                key => key.Assignment.Key.EndsWith("_highlighting", StringComparison.Ordinal)
            ).Namespace
        );

        Assert.Single(diagnostics, d => d.Id == ConfigDiagnosticIds.UnknownKey);

        // The two Roslyn owns are still classified out of SK9001, and named so this cannot pass by
        // finding nothing: a resolution that dropped them entirely would satisfy a DoesNotContain.
        Assert.Contains(resolution.Unknown, static key => key.Namespace == KeyNamespace.DiagnosticSeverity);
        Assert.Contains(resolution.Unknown, static key => key.Namespace == KeyNamespace.NamingRule);
    }

    [Fact]
    public void SK9002_ReportsConfigurationFromAboveTheRepositoryRoot() {
        using var tree = new TemporaryTree();
        tree.Write(".editorconfig", "[*]\nskala_max_line_length = 100\n");
        var repository = Path.Combine(tree.Root, "repo");
        tree.Write("repo/.editorconfig", "[*]\nindent_size = 4\n");
        var source = tree.Write("repo/File.cs", "class C;");

        var diagnostic = Assert.Single(
            ConfigurationAnalyzer.Analyze(OptionResolver.Resolve(source), repository),
            static d => d.Id == ConfigDiagnosticIds.InheritedFromAbove
        );

        Assert.Equal(SkalaSeverity.Info, diagnostic.Severity);
        Assert.Contains("skala_max_line_length", diagnostic.Detail!, StringComparison.Ordinal);
    }

    [Fact]
    public void SK9003_RejectsAStyleKeyInTheToolConfiguration() {
        var config = ToolConfiguration.FromText(
            "/repo/skala.jsonc",
            """
            {
              // where to look
              "include": ["**/*.cs"],
              "skala_max_line_length": 120
            }
            """
        );

        var diagnostic = Assert.Single(
            config.Diagnostics,
            static d => d.Id == ConfigDiagnosticIds.StyleKeyInToolConfig
        );
        Assert.Equal(SkalaSeverity.Error, diagnostic.Severity);
        Assert.Contains("move it to .editorconfig", diagnostic.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     SK9004 cannot fire, and this is the invariant that makes it so.
    /// </summary>
    /// <remarks>
    ///     ⚠ <b>This was a test that constructed the diagnostic and asserted its message.</b> It cannot
    ///     be constructed any more: SK9004 needs two <em>different</em> spellings of one option at the
    ///     <em>same</em> specificity rank, and dropping ReSharper's alias spellings left every option
    ///     with at most one spelling per rank — its <c>skala_</c> key, and at most one alias in
    ///     Microsoft's namespace, which is a different rank.
    ///     <para>
    ///         The mechanical rename turned the old fixture into <c>skala_space_after_attribute_colon</c>
    ///         set twice, which is one spelling twice — SK9004 skips it, later-wins applies, and the
    ///         test failed rather than passing vacuously only because it asserted a diagnostic it no
    ///         longer got. The branch in <c>ConfigurationAnalyzer.AddDuplicateAliases</c> is kept: it
    ///         costs nothing and becomes reachable again the day an option is given a second alias at
    ///         an existing rank. This test is what says that day has not come, and it is a stronger
    ///         claim than the old one — the old one said the diagnostic works, this says the
    ///         configuration it warns about cannot be written.
    ///     </para>
    /// </remarks>
    [Fact]
    public void SK9004_IsUnreachable_BecauseNoOptionHasTwoSpellingsAtOneRank() {
        Assert.NotEmpty(OptionRegistry.All);
        foreach (var info in OptionRegistry.All) {
            var ranks = new[] { info.Key }
                .Concat(info.Aliases)
                .GroupBy(Rank)
                .Where(static group => group.Count() > 1)
                .ToArray();

            Assert.True(
                ranks.Length == 0,
                $"{info.Key} has two spellings at one specificity rank ("
                + string.Join(", ", ranks.SelectMany(static g => g))
                + "). Precedence cannot choose between them, which is what SK9004 reports — restore "
                + "the test that constructs it."
            );
        }
    }

    static int Rank(string spelling) {
        for (var i = 0; i < OptionKeyPrefixes.Ordered.Length; i++) {
            if (spelling.StartsWith(OptionKeyPrefixes.Ordered[i], StringComparison.Ordinal)) {
                return i;
            }
        }

        return OptionKeyPrefixes.Ordered.Length;
    }

    [Fact]
    public void SK9006_FiresWhenIndentAutodetectionIsSwitchedBackOn() {
        // docs/plan/16 § Q1: with autodetection on, the IDE formats against a detected indent and
        // the oracle against the configured one, and Skala cannot match both.
        var document = EditorConfigDocument.FromText(
            "/repo/.editorconfig",
            """
            root = true
            [*]
            skala_autodetect_indent_settings = true
            """
        );

        var diagnostic = Assert.Single(
            ConfigurationAnalyzer.Analyze(OptionResolver.Resolve(EditorConfigChain.Of("/repo/File.cs", document))),
            static d => d.Id == ConfigDiagnosticIds.UnhonourableSetting
        );

        Assert.Equal(SkalaSeverity.Warning, diagnostic.Severity);
    }

    [Fact]
    public void SK9006_IsSilentForTheTemplate_BecauseTheTemplateTurnsAutodetectionOff() {
        Assert.DoesNotContain(AnalyzeTemplate(), static d => d.Id == ConfigDiagnosticIds.UnhonourableSetting);
    }
}
