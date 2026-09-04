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

    /// <remarks>
    ///     ⚠ The two spellings here were <c>resharper_csharp_insert_final_newline</c> and
    ///     <c>insert_final_newline</c>, and the rename collapsed them onto one key — leaving the same
    ///     assignment twice and a test asserting that the <em>first</em> of two identical lines wins,
    ///     which is the opposite of editorconfig's rule and passed only by accident. Specificity is
    ///     still a real ladder, but it is now Skala's key over Microsoft's alias rather than
    ///     ReSharper's C# key over its generic one.
    /// </remarks>
    [Fact]
    public void OrderWithinASection_DoesNotOverrideSpecificity() {
        // The less specific key comes last and still loses: resolution is by specificity, not by
        // position.
        var resolution = Resolve(
            """
            root = true
            [*]
            skala_space_after_cast = true
            csharp_space_after_cast = false
            """
        );

        Assert.True(OptionRegistry.TryResolve("skala_space_after_cast", out var id));
        Assert.Equal("true", resolution[id].Value);
        Assert.Equal("skala_space_after_cast", resolution[id].Origin!.Spelling);
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

    /// <remarks>
    ///     ⚠ This was <c>MicrosoftKey_BeatsTheBareEditorConfigKey_AndLosesToTheReSharperKey</c>, over
    ///     three spellings of one option. Two of the three were ReSharper's and are gone, and the
    ///     mechanical rename left the case asserting <c>skala_space_after_cast</c> beats itself twice
    ///     — three identical lines, a test that passes on any implementation at all. The ladder that
    ///     is left is two rungs and it is the one that still matters: Microsoft's spelling is accepted,
    ///     and Skala's own beats it.
    /// </remarks>
    [Fact]
    public void MicrosoftKey_IsAccepted_AndLosesToSkalasOwn() {
        var resolution = Resolve(
            """
            root = true
            [*]
            csharp_space_after_cast = false
            skala_space_after_cast = true
            """
        );

        Assert.True(OptionRegistry.TryResolve("csharp_space_after_cast", out var alias));
        Assert.True(OptionRegistry.TryResolve("skala_space_after_cast", out var id));
        Assert.Equal(id, alias);
        Assert.Equal("true", resolution[id].Value);
        Assert.Equal("skala_space_after_cast", resolution[id].Origin!.Spelling);
    }

    /// <summary>A <c>resharper_*</c> key configures nothing and is reported as unknown.</summary>
    /// <remarks>
    ///     ⚠ The claim the whole rename rests on, and it is asserted here rather than left implicit:
    ///     pointing Skala at a Rider export must <em>not</em> configure it. The failure this catches is
    ///     silent in both directions — an export spelling re-admitted to
    ///     <see cref="OptionRegistry.TryResolve" /> would quietly restore ingestion, and nothing else
    ///     in the suite would go red.
    /// </remarks>
    [Fact]
    public void ReSharperKey_IsAnUnknownKey() {
        Assert.False(OptionRegistry.TryResolve("resharper_space_after_cast", out _));
        Assert.False(OptionRegistry.TryResolve("resharper_csharp_wrap_arguments_style", out _));

        var resolution = Resolve(
            """
            root = true
            [*]
            resharper_space_after_cast = true
            """
        );

        Assert.True(OptionRegistry.TryResolve("skala_space_after_cast", out var id));
        Assert.True(resolution[id].IsDefault);
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

        var option = resolution[OptionId.SkalaWrapArgumentsStyle];
        Assert.True(option.IsDefault);
        Assert.Equal("(default)", option.SourceText);
        Assert.Equal(OptionRegistry.Get(OptionId.SkalaWrapArgumentsStyle).Default, option.Value);
    }

    [Fact]
    public void MissingAllowCommentAfterLbrace_DefaultsToTrue() {
        var resolution = Resolve("root = true");

        Assert.True(OptionRegistry.TryResolve("skala_allow_comment_after_lbrace", out var id));
        Assert.True(resolution[id].IsDefault);
        Assert.Equal("true", resolution[id].Value);
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
        Assert.Equal(OptionId.SkalaWrapArgumentsStyle, error.Id);
        Assert.Equal("sideways", error.Value);
        Assert.Contains("chop_if_long", error.Reason, StringComparison.Ordinal);

        // ⚠ The part that makes the report actionable: what the file is being formatted with now.
        // Reporting the refusal alone leaves the reader unable to tell, and the fallback is not
        // guessable from the key.
        Assert.Equal(OptionRegistry.Get(OptionId.SkalaWrapArgumentsStyle).Default, error.Effective);

        var option = resolution[OptionId.SkalaWrapArgumentsStyle];
        Assert.True(option.IsDefault);
        Assert.NotNull(option.Refused);
        Assert.Equal(3, option.Refused.Line);

        // ⚠ `config explain` used to print a bare `(default)` here, beside an option the
        // .editorconfig visibly sets three lines in. The row has to name the line it refused.
        Assert.Contains("SK9017", option.SourceText, StringComparison.Ordinal);
        Assert.Contains("/repo/.editorconfig:3", option.SourceText, StringComparison.Ordinal);
    }
}
