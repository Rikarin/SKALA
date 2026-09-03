using Rikarin.Skala.Core.Configuration;
using Rikarin.Skala.Options;

namespace Rikarin.Skala.Core.Tests;

/// <summary>docs/plan/03-configuration-model.md § "Precedence".</summary>
public sealed class PrecedenceTests {
    /// <summary>
    ///     Documents are given outermost first and laid out one directory apart, so that document
    ///     <c>n</c> is nearer the source file than document <c>n - 1</c>.
    /// </summary>
    static ResolutionResult Resolve(params string[] documents) {
        var directory = "/repo";
        var parsed = new EditorConfigDocument[documents.Length];
        for (var i = 0; i < documents.Length; i++) {
            parsed[i] = EditorConfigDocument.FromText($"{directory}/.editorconfig", documents[i]);
            directory += "/inner";
        }

        return OptionResolver.Resolve(EditorConfigChain.Of($"{directory}/File.cs", parsed));
    }

    [Fact]
    public void LanguageSpecificKey_BeatsTheGenericOne() {
        // The contradiction the plan found: [*] skala_insert_final_newline = false while
        // skala_insert_final_newline = true. They are one option and the C# key wins.
        var resolution = Resolve(
            """
            root = true
            [*]
            skala_insert_final_newline = false
            skala_insert_final_newline = true
            """
        );

        Assert.True(OptionRegistry.TryResolve("skala_insert_final_newline", out var id));
        Assert.Equal("true", resolution[id].Value);
        Assert.Equal("skala_insert_final_newline", resolution[id].Origin!.Spelling);
    }

    [Fact]
    public void OrderWithinASection_DoesNotOverrideSpecificity() {
        // The generic key comes last and still loses: ReSharper resolves by language specificity,
        // not by position.
        var resolution = Resolve(
            """
            root = true
            [*]
            skala_insert_final_newline = true
            skala_insert_final_newline = false
            """
        );

        Assert.True(OptionRegistry.TryResolve("skala_insert_final_newline", out var id));
        Assert.Equal("true", resolution[id].Value);
    }

    [Fact]
    public void LaterSection_BeatsEarlierSection_ForTheSameSpelling() {
        var resolution = Resolve(
            """
            root = true
            [*]
            skala_max_line_length = 100
            [*.cs]
            skala_max_line_length = 140
            """
        );

        Assert.True(OptionRegistry.TryResolve("skala_max_line_length", out var id));
        Assert.Equal("140", resolution[id].Value);
    }

    [Fact]
    public void NearerFile_BeatsFartherFile() {
        var resolution = Resolve(
            """
            [*]
            skala_max_line_length = 100
            """,
            """
            [*]
            skala_max_line_length = 80
            """
        );

        Assert.True(OptionRegistry.TryResolve("skala_max_line_length", out var id));
        Assert.Equal("80", resolution[id].Value);
    }

    [Fact]
    public void MicrosoftKey_BeatsTheBareEditorConfigKey_AndLosesToTheReSharperKey() {
        var resolution = Resolve(
            """
            root = true
            [*]
            skala_space_after_cast = true
            skala_space_after_cast = false
            skala_space_after_cast = true
            """
        );

        Assert.True(OptionRegistry.TryResolve("skala_space_after_cast", out var id));
        Assert.Equal("true", resolution[id].Value);
        Assert.Equal("skala_space_after_cast", resolution[id].Origin!.Spelling);
    }

    [Fact]
    public void CommandLineOverride_BeatsEverything() {
        // Recorded, never silent: docs/plan/03 § "Precedence" step 1.
        var document = EditorConfigDocument.FromText(
            "/repo/.editorconfig",
            """
            root = true
            [*]
            skala_max_line_length = 120
            """
        );

        var resolution = OptionResolver.Resolve(
            EditorConfigChain.Of("/repo/File.cs", document),
            [new KeyValuePair<string, string>("skala_max_line_length", "200")]
        );

        Assert.True(OptionRegistry.TryResolve("skala_max_line_length", out var id));
        Assert.Equal("200", resolution[id].Value);
        Assert.Equal("(command line)", resolution[id].Origin!.File);
    }

    [Fact]
    public void UnsetOption_FallsBackToTheRegistryDefault() {
        var resolution = Resolve(
            """
            root = true
            [*]
            indent_size = 4
            """
        );

        var option = resolution[OptionId.ResharperCsharpWrapArgumentsStyle];
        Assert.True(option.IsDefault);
        Assert.Equal("(default)", option.SourceText);
        Assert.Equal(OptionRegistry.Get(OptionId.ResharperCsharpWrapArgumentsStyle).Default, option.Value);
    }

    [Fact]
    public void EveryResolvedOption_KnowsItsFileAndLine() {
        // `config explain` is useless without it (docs/plan/15 § M0).
        var resolution = OptionResolver.Resolve(RepositoryPaths.SampleSourceFile);

        Assert.NotEmpty(resolution.Configured);
        foreach (var option in resolution.Configured) {
            Assert.NotNull(option.Origin);
            Assert.True(option.Origin.Line > 0);
            Assert.True(File.Exists(option.Origin.File));
        }
    }

    [Fact]
    public void ValueOutsideAnOptionsDomain_IsReportedRatherThanApplied() {
        var resolution = Resolve(
            """
            root = true
            [*]
            skala_wrap_arguments_style = sideways
            """
        );

        var error = Assert.Single(resolution.ValueErrors);
        Assert.Equal(OptionId.ResharperCsharpWrapArgumentsStyle, error.Id);
        Assert.Equal("sideways", error.Value);
        Assert.Contains("chop_if_long", error.Reason, StringComparison.Ordinal);

        // ⚠ The part that makes the report actionable: what the file is being formatted with now.
        // Reporting the refusal alone leaves the reader unable to tell, and the fallback is not
        // guessable from the key.
        Assert.Equal(OptionRegistry.Get(OptionId.ResharperCsharpWrapArgumentsStyle).Default, error.Effective);

        var option = resolution[OptionId.ResharperCsharpWrapArgumentsStyle];
        Assert.True(option.IsDefault);
        Assert.NotNull(option.Refused);
        Assert.Equal(3, option.Refused.Line);

        // ⚠ `config explain` used to print a bare `(default)` here, beside an option the
        // .editorconfig visibly sets three lines in. The row has to name the line it refused.
        Assert.Contains("SK9017", option.SourceText, StringComparison.Ordinal);
        Assert.Contains("/repo/.editorconfig:3", option.SourceText, StringComparison.Ordinal);
    }
}
