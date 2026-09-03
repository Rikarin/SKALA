using Rikarin.Skala.Core.Configuration;

namespace Rikarin.Skala.Core.Tests;

public sealed class FixTests {
    /// <summary>
    ///     The export as Skala reads it.
    /// </summary>
    /// <remarks>
    ///     ⚠ <c>Fixer</c> adds <c>max_line_length</c> beside the option that carries the column limit,
    ///     and it finds that option by resolving. Handed the raw export it resolves nothing, adds
    ///     nothing, and <c>Changed</c> comes back false — which reads as "the export needs no fixing".
    /// </remarks>
    static EditorConfigDocument TranslatedTemplate() =>
        EditorConfigDocument.FromText(
            RepositoryPaths.Template,
            CanonicalEditorConfig.Translate(File.ReadAllText(RepositoryPaths.Template))
        );

    [Fact]
    public void Fix_AddsRootAndMaxLineLength_ToTheRealTemplate() {
        // docs/plan/15 § M0: `config check` reports the missing root and the missing
        // max_line_length; `config fix` offers to add both.
        var result = Fixer.Fix(TranslatedTemplate());

        Assert.True(result.Changed);
        Assert.Contains(result.Applied, static change => change.Contains("root = true", StringComparison.Ordinal));
        Assert.Contains(
            result.Applied,
            static change => change.Contains("max_line_length = 120", StringComparison.Ordinal)
        );

        var fixed_ = EditorConfigDocument.FromText("/repo/.editorconfig", result.Text);
        Assert.True(fixed_.IsRoot);
        Assert.Equal("120", Assert.Single(fixed_.Assignments, static a => a.Key == "max_line_length").Value);
    }

    [Fact]
    public void Fix_ChangesNothingElse() {
        var original = TranslatedTemplate();
        var fixed_ = EditorConfigDocument.FromText("/repo/.editorconfig", Fixer.Fix(original).Text);

        // Two keys added; nothing removed, nothing rewritten.
        Assert.Equal(original.Assignments.Count() + 2, fixed_.Assignments.Count());
        foreach (var assignment in original.Assignments) {
            Assert.Contains(fixed_.Assignments, a => a.Key == assignment.Key && a.Value == assignment.Value);
        }
    }

    [Fact]
    public void Fix_IsIdempotent() {
        var once = Fixer.Fix(TranslatedTemplate()).Text;
        var twice = Fixer.Fix(EditorConfigDocument.FromText("/repo/.editorconfig", once));

        Assert.False(twice.Changed);
        Assert.Equal(once, twice.Text);
    }

    [Fact]
    public void Fix_DoesNotTouchAnAlreadyHealthyFile() {
        var document = EditorConfigDocument.FromText(
            "/repo/.editorconfig",
            """
            root = true
            [*]
            max_line_length = 120
            skala_max_line_length = 120
            """
        );

        Assert.False(Fixer.Fix(document).Changed);
    }

    [Fact]
    public void ResolveContradictions_MakesTheLosingKeyAgreeWithTheWinner() {
        var document = EditorConfigDocument.FromText(
            "/repo/.editorconfig",
            """
            root = true
            [*]
            insert_final_newline = false
            trim_trailing_whitespace = false
            skala_insert_final_newline = true
            skala_remove_spaces_on_blank_lines = true
            """
        );

        var result = Fixer.Fix(document, true);
        var fixed_ = EditorConfigDocument.FromText("/repo/.editorconfig", result.Text);

        Assert.Equal("true", Assert.Single(fixed_.Assignments, static a => a.Key == "skala_insert_final_newline").Value);
        Assert.Equal("true", Assert.Single(fixed_.Assignments, static a => a.Key == "trim_trailing_whitespace").Value);
    }

    [Fact]
    public void ResolveContradictions_LeavesTheLineEndingPairAlone() {
        // There is no value of end_of_line that agrees with `skala_enforce_line_ending_style =
        // false`. Turning enforcement on is a style decision, and `fix` does not make those.
        var document = EditorConfigDocument.FromText(
            "/repo/.editorconfig",
            """
            root = true
            [*]
            end_of_line = lf
            skala_enforce_line_ending_style = false
            """
        );

        var fixed_ = EditorConfigDocument.FromText(
            "/repo/.editorconfig",
            Fixer.Fix(document, true).Text
        );
        Assert.Equal("lf", Assert.Single(fixed_.Assignments, static a => a.Key == "end_of_line").Value);
        Assert.Equal(
            "false",
            Assert.Single(fixed_.Assignments, static a => a.Key == "skala_enforce_line_ending_style").Value
        );
    }
}
