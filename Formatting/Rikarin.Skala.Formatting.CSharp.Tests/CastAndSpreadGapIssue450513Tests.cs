using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Core.Configuration;

namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     #450's flat half — a cast before a one-line collection expression reads the cast's key — and
///     #513, the spread gap Skala now governs on purpose.
/// </summary>
public sealed class CastAndSpreadGapIssue450513Tests {
    static string FormatWith(string source, params (string Key, string Value)[] overrides) {
        var options = new PhaseOneOptions(
            OptionResolver.Resolve(
                    Path.Combine(Rikarin.Skala.Testing.Corpus.RepositoryRoot, "Test.cs"),
                    [..overrides.Select(static o => new KeyValuePair<string, string>(o.Key, o.Value))]
                )
                .Options
        );

        return CSharpFormatter.Format("Test.cs", SourceText.From(source), options).Formatted;
    }

    static string Body(string statement) =>
        "class C {\n    int[] xs = [1];\n\n"
        + $"    object T(object o) {{\n        {statement}\n        return o;\n    }}\n}}\n";

    static void AssertFormats(string statement, string expected, params (string Key, string Value)[] overrides) {
        var once = FormatWith(Body(statement), overrides);
        Assert.Contains("        " + expected + "\n", once, StringComparison.Ordinal);
        Assert.Equal(once, FormatWith(once, overrides));
    }

    /// <summary>
    ///     ⚠ Measured 2026-10-08: <c>(int[])[1, 2]</c> and <c>(int[]) [1, 2]</c> both come back closed at
    ///     the export and both spaced at <c>space_after_cast = true</c>. Skala ignored the key here.
    /// </summary>
    [Theory]
    [InlineData("o = (int[])[1, 2];", "o = (int[])[1, 2];", "false")]
    [InlineData("o = (int[]) [1, 2];", "o = (int[])[1, 2];", "false")]
    [InlineData("o = (int[])[1, 2];", "o = (int[]) [1, 2];", "true")]
    [InlineData("o = (int[]) [1, 2];", "o = (int[]) [1, 2];", "true")]
    public void AOneLineCollectionAfterACast_ReadsTheCastKey(string statement, string expected, string value) =>
        AssertFormats(statement, expected, ("skala_space_after_cast", value));

    /// <summary>The same key before a parenthesized operand, measured on the same probe.</summary>
    [Theory]
    [InlineData("o = (int)(1 + 2);", "o = (int)(1 + 2);", "false")]
    [InlineData("o = (int) (1 + 2);", "o = (int)(1 + 2);", "false")]
    [InlineData("o = (int)(1 + 2);", "o = (int) (1 + 2);", "true")]
    public void AParenthesizedOperandAfterACast_ReadsTheCastKey(string statement, string expected, string value) =>
        AssertFormats(statement, expected, ("skala_space_after_cast", value));

    /// <summary>
    ///     ⚠ Not the oracle's answer, deliberately: it returns a spread exactly as written at both values
    ///     (SK-DIV-0009), and Skala writes one spelling (#513, SK-DIV-0310). The registry default is
    ///     <c>false</c>, and so is the repository's own configuration.
    /// </summary>
    [Theory]
    [InlineData(
        "o = new int[][] { [..xs], [.. xs], [..   xs] };",
        "o = new int[][] { [..xs], [..xs], [..xs] };",
        "false"
    )]
    [InlineData(
        "o = new int[][] { [..xs], [.. xs], [..   xs] };",
        "o = new int[][] { [.. xs], [.. xs], [.. xs] };",
        "true"
    )]
    [InlineData("o = (int[])[1, ..xs, 2];", "o = (int[])[1, ..xs, 2];", "false")]
    [InlineData("o = (int[])[1, ..xs, 2];", "o = (int[])[1, .. xs, 2];", "true")]
    public void ASpread_IsSpelledOneWay(string statement, string expected, string value) =>
        AssertFormats(statement, expected, ("skala_space_within_spread_pattern", value));

    [Fact]
    public void ASpread_HasNoSpaceInTheRepositorysConfiguration() =>
        AssertFormats("o = (int[])[.. xs];", "o = (int[])[..xs];");

    /// <summary>
    ///     The other two <c>..</c> gaps are not this key's: a slice pattern's is
    ///     <c>space_within_slice_pattern</c>'s and a range's stays as written.
    /// </summary>
    [Theory]
    [InlineData("false")]
    [InlineData("true")]
    public void ASlicePatternAndARange_AreNotTheSpreadKeys(string value) {
        AssertFormats(
            "o = xs is [1, ..var r] ? r : xs;",
            "o = xs is [1, .. var r] ? r : xs;",
            ("skala_space_within_spread_pattern", value)
        );
        AssertFormats("o = xs[1..2];", "o = xs[1..2];", ("skala_space_within_spread_pattern", value));
        AssertFormats("o = xs[1 .. 2];", "o = xs[1 .. 2];", ("skala_space_within_spread_pattern", value));
    }

    /// <summary>A spread whose operand the author put on the next line keeps the break.</summary>
    [Fact]
    public void ASpreadsKeptBreak_IsNotJoined() {
        const string source =
            "class C {\n    int[] xs = [1];\n\n    int[] T() =>\n        [\n            ..\n            xs\n        ];\n}\n";
        var once = FormatWith(source);
        Assert.Contains("..\n", once, StringComparison.Ordinal);
        Assert.Equal(once, FormatWith(once));
    }
}
