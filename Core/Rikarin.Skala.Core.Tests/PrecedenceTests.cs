using Rikarin.Skala.Core.Configuration;
using Rikarin.Skala.Options;

namespace Rikarin.Skala.Core.Tests;

/// <summary>docs/plan/03-configuration-model.md § "Precedence".</summary>
public sealed class PrecedenceTests {
    /// <summary>
    /// Documents are given outermost first and laid out one directory apart, so that document
    /// <c>n</c> is nearer the source file than document <c>n - 1</c>.
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
        // The contradiction the plan found: [*] insert_final_newline = false while
        // resharper_csharp_insert_final_newline = true. They are one option and the C# key wins.
        var resolution = Resolve("""
            root = true
            [*]
            insert_final_newline = false
            resharper_csharp_insert_final_newline = true
            """);

        Assert.True(OptionRegistry.TryResolve("insert_final_newline", out var id));
        Assert.Equal("true", resolution[id].Value);
        Assert.Equal("resharper_csharp_insert_final_newline", resolution[id].Origin!.Spelling);
    }

    [Fact]
    public void OrderWithinASection_DoesNotOverrideSpecificity() {
        // The generic key comes last and still loses: ReSharper resolves by language specificity,
        // not by position.
        var resolution = Resolve("""
            root = true
            [*]
            resharper_csharp_insert_final_newline = true
            insert_final_newline = false
            """);

        Assert.True(OptionRegistry.TryResolve("insert_final_newline", out var id));
        Assert.Equal("true", resolution[id].Value);
    }

    [Fact]
    public void LaterSection_BeatsEarlierSection_ForTheSameSpelling() {
        var resolution = Resolve("""
            root = true
            [*]
            resharper_csharp_max_line_length = 100
            [*.cs]
            resharper_csharp_max_line_length = 140
            """);

        Assert.True(OptionRegistry.TryResolve("resharper_csharp_max_line_length", out var id));
        Assert.Equal("140", resolution[id].Value);
    }

    [Fact]
    public void NearerFile_BeatsFartherFile() {
        var resolution = Resolve(
            """
            [*]
            resharper_csharp_max_line_length = 100
            """,
            """
            [*]
            resharper_csharp_max_line_length = 80
            """);

        Assert.True(OptionRegistry.TryResolve("resharper_csharp_max_line_length", out var id));
        Assert.Equal("80", resolution[id].Value);
    }

    [Fact]
    public void MicrosoftKey_BeatsTheBareEditorConfigKey_AndLosesToTheReSharperKey() {
        var resolution = Resolve("""
            root = true
            [*]
            space_after_cast = true
            csharp_space_after_cast = false
            resharper_space_after_cast = true
            """);

        Assert.True(OptionRegistry.TryResolve("csharp_space_after_cast", out var id));
        Assert.Equal("true", resolution[id].Value);
        Assert.Equal("resharper_space_after_cast", resolution[id].Origin!.Spelling);
    }

    [Fact]
    public void CommandLineOverride_BeatsEverything() {
        // Recorded, never silent: docs/plan/03 § "Precedence" step 1.
        var document = EditorConfigDocument.FromText("/repo/.editorconfig", """
            root = true
            [*]
            resharper_csharp_max_line_length = 120
            """);

        var resolution = OptionResolver.Resolve(
            EditorConfigChain.Of("/repo/File.cs", document),
            [new KeyValuePair<string, string>("resharper_csharp_max_line_length", "200")]);

        Assert.True(OptionRegistry.TryResolve("resharper_csharp_max_line_length", out var id));
        Assert.Equal("200", resolution[id].Value);
        Assert.Equal("(command line)", resolution[id].Origin!.File);
    }

    [Fact]
    public void UnsetOption_FallsBackToTheRegistryDefault() {
        var resolution = Resolve("""
            root = true
            [*]
            indent_size = 4
            """);

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
        var resolution = Resolve("""
            root = true
            [*]
            resharper_csharp_wrap_arguments_style = sideways
            """);

        Assert.Contains(resolution.ValueErrors, error => error.Contains("sideways", StringComparison.Ordinal));
        Assert.True(resolution[OptionId.ResharperCsharpWrapArgumentsStyle].IsDefault);
    }
}
