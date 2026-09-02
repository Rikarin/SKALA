using Rikarin.Skala.Core.Configuration;
using Rikarin.Skala.Core.Diagnostics;
using System.Collections.Immutable;

namespace Rikarin.Skala.Core.Tests;

public sealed class ConfigurationDiagnosticsTests {
    static ImmutableArray<SkalaDiagnostic> AnalyzeTemplate() {
        var document = EditorConfigDocument.Load(RepositoryPaths.Template);
        var probe = Path.Combine(RepositoryPaths.Root, "Probe.cs");
        return ConfigurationAnalyzer.Analyze(
            OptionResolver.Resolve(EditorConfigChain.Of(probe, document)),
            RepositoryPaths.Root
        );
    }

    [Fact]
    public void SK9005_NamesTheThreeContradictionsInTheRealTemplate() {
        // docs/plan/15 § M0, definition of done: `skala config check` names the three
        // contradictions this plan already found.
        var messages = AnalyzeTemplate()
            .Where(static diagnostic => diagnostic.Id == ConfigDiagnosticIds.ContradictoryOptions)
            .Select(static diagnostic => diagnostic.Message)
            .ToArray();

        Assert.Equal(3, messages.Length);

        var finalNewline = Assert.Single(
            messages,
            static m => m.Contains("insert_final_newline", StringComparison.Ordinal)
        );
        Assert.Contains("resharper_csharp_insert_final_newline = true", finalNewline, StringComparison.Ordinal);
        Assert.Contains("the C# key wins", finalNewline, StringComparison.Ordinal);
        Assert.Contains("the effective value is 'true'", finalNewline, StringComparison.Ordinal);

        var whitespace = Assert.Single(
            messages,
            static m => m.Contains("trim_trailing_whitespace", StringComparison.Ordinal)
        );
        Assert.Contains("resharper_remove_spaces_on_blank_lines = true", whitespace, StringComparison.Ordinal);
        Assert.Contains("the C# key wins", whitespace, StringComparison.Ordinal);

        var lineEndings = Assert.Single(messages, static m => m.Contains("end_of_line = lf", StringComparison.Ordinal));
        Assert.Contains("resharper_enforce_line_ending_style = false", lineEndings, StringComparison.Ordinal);
        Assert.Contains("the C# key wins", lineEndings, StringComparison.Ordinal);
    }

    [Fact]
    public void SK9001_IsInfo_AndSuggestsANearbyKey() {
        var document = EditorConfigDocument.FromText(
            "/repo/.editorconfig",
            """
            root = true
            [*]
            resharper_csharp_wrap_argument_style = chop_if_long
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
            "did you mean 'resharper_csharp_wrap_arguments_style'",
            diagnostic.Message,
            StringComparison.Ordinal
        );
    }

    /// <summary>
    ///     An inspection severity, a Roslyn diagnostic severity and a naming key are classified, not
    ///     reported as unknown options.
    /// </summary>
    /// <remarks>
    ///     ⚠ <b>The sample is inline on purpose.</b> This test used to read the repository's own
    ///     <c>editor_config_template</c> and assert it contained an <c>InspectionSeverity</c> key —
    ///     so it broke the moment somebody legitimately removed the 1 062 <c>resharper_*_highlighting</c>
    ///     lines from it, which says nothing about whether the classifier still works. A real Rider
    ///     export still carries about three thousand of these, and that is the input this is about;
    ///     what this repository happens to keep in its own configuration is a different question.
    /// </remarks>
    [Fact]
    public void SK9001_IgnoresTheSeverityNamespaces() {
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

        Assert.Contains(resolution.Unknown, static key => key.Namespace == KeyNamespace.InspectionSeverity);
        Assert.DoesNotContain(
            ConfigurationAnalyzer.Analyze(resolution),
            static d => d.Id == ConfigDiagnosticIds.UnknownKey
        );
    }

    [Fact]
    public void SK9002_ReportsConfigurationFromAboveTheRepositoryRoot() {
        using var tree = new TemporaryTree();
        tree.Write(".editorconfig", "[*]\nresharper_csharp_max_line_length = 100\n");
        var repository = Path.Combine(tree.Root, "repo");
        tree.Write("repo/.editorconfig", "[*]\nindent_size = 4\n");
        var source = tree.Write("repo/File.cs", "class C;");

        var diagnostic = Assert.Single(
            ConfigurationAnalyzer.Analyze(OptionResolver.Resolve(source), repository),
            static d => d.Id == ConfigDiagnosticIds.InheritedFromAbove
        );

        Assert.Equal(SkalaSeverity.Info, diagnostic.Severity);
        Assert.Contains("resharper_csharp_max_line_length", diagnostic.Detail!, StringComparison.Ordinal);
    }

    [Fact]
    public void SK9003_RejectsAStyleKeyInTheToolConfiguration() {
        var config = ToolConfiguration.FromText(
            "/repo/skala.jsonc",
            """
            {
              // where to look
              "include": ["**/*.cs"],
              "resharper_csharp_max_line_length": 120
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

    [Fact]
    public void SK9004_ReportsTwoEquallySpecificSpellingsThatDisagree() {
        var document = EditorConfigDocument.FromText(
            "/repo/.editorconfig",
            """
            root = true
            [*]
            csharp_space_after_attribute_colon = true
            csharp_space_after_colon = false
            """
        );

        var diagnostic = Assert.Single(
            ConfigurationAnalyzer.Analyze(OptionResolver.Resolve(EditorConfigChain.Of("/repo/File.cs", document))),
            static d => d.Id == ConfigDiagnosticIds.DuplicateAlias
        );

        Assert.Equal(SkalaSeverity.Warning, diagnostic.Severity);
        Assert.Contains("equally specific", diagnostic.Message, StringComparison.Ordinal);
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
            resharper_autodetect_indent_settings = true
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
