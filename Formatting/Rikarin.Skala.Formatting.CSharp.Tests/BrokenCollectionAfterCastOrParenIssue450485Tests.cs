using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Core.Configuration;

namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     #450 and #485: the gap in front of a collection expression behind a cast or a parenthesis is one
///     space once the collection breaks, and a cast collection that fits on the line below moves there.
/// </summary>
/// <remarks>
///     ⚠ Every expected string is the oracle's answer, measured 2026-10-08 with <c>Testing ask</c> at the
///     export and at <c>space_after_cast = true</c> with <c>space_within_parentheses = true</c>. Every row
///     is checked on a second pass.
/// </remarks>
public sealed class BrokenCollectionAfterCastOrParenIssue450485Tests {
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

    static string Body(string statements) =>
        "class C {\n    object T(int alphaValueNumber, int betaValueNumber, int gammaValueNumber) {\n        "
        + statements
        + "\n        return null;\n    }\n}\n";

    static void AssertFormats(string statements, string expected, params (string Key, string Value)[] overrides) {
        var once = FormatWith(Body(statements), overrides);
        Assert.Contains(expected, once, StringComparison.Ordinal);
        Assert.Equal(once, FormatWith(once, overrides));
    }

    [Theory]
    [InlineData("false")]
    [InlineData("true")]
    public void ABrokenCollectionAfterACast_TakesASpace(string value) =>
        AssertFormats(
            "var kept = (int[])[\n            1,\n            2\n        ];",
            "        var kept = (int[]) [\n            1,\n            2\n        ];\n",
            ("skala_space_after_cast", value)
        );

    [Fact]
    public void AFlatCollectionAfterACast_StillReadsTheKey() =>
        AssertFormats("var flat = (int[])[1, 2];", "        var flat = (int[])[1, 2];\n");

    [Fact]
    public void ACastCollectionThatFitsBelow_MovesThere() =>
        AssertFormats(
            "var fits = (int[])[alphaValueNumber, betaValueNumber, "
            + "gammaValueNumber, alphaValueNumber, betaValueNumber, gammaValueNumber];",
            "        var fits = (int[])\n            [alphaValueNumber, betaValueNumber, "
            + "gammaValueNumber, alphaValueNumber, betaValueNumber, gammaValueNumber];\n"
        );

    [Fact]
    public void AKeptBreakAfterACast_StaysWhenTheCollectionFits() =>
        AssertFormats(
            "var kept = (int[])\n            [1, 2, 3];",
            "        var kept = (int[])\n            [1, 2, 3];\n"
        );

    [Fact]
    public void AKeptBreakAfterACast_YieldsToABrokenCollection() =>
        AssertFormats(
            "var kept = (int[])\n            [\n                1,\n                2\n            ];",
            "        var kept = (int[]) [\n            1,\n            2\n        ];\n"
        );

    [Theory]
    [InlineData("false", "        int[] z = ( [\n", "        int[] y = ([1, 2]);\n")]
    [InlineData("true", "        int[] z = ( [\n", "        int[] y = ( [1, 2] );\n")]
    public void ABrokenCollectionInAParenthesis_TakesASpace(string value, string broken, string flat) {
        var statements = "int[] z = ([\n            1,\n            2\n        ]);\n        int[] y = ([1, 2]);";
        AssertFormats(statements, broken, ("skala_space_within_parentheses", value));
        AssertFormats(statements, flat, ("skala_space_within_parentheses", value));
    }

    [Theory]
    [InlineData("false", "        var q = (( [\n")]
    [InlineData("true", "        var q = ( ( [\n")]
    public void OnlyTheInnermostParenthesis_TakesIt(string value, string expected) =>
        AssertFormats(
            "var q = (([\n            1,\n            2\n        ]));",
            expected,
            ("skala_space_within_parentheses", value)
        );
}
