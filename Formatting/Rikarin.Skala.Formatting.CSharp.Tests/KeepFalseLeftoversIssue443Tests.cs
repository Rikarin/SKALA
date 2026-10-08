using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Core.Configuration;

namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     #443: a filled list's delimiters, the gap after a type test's or a pattern's keyword, and a
///     collection expression, each at <c>keep_user_linebreaks = false</c> — and the defaults the same
///     measurement turned up.
/// </summary>
/// <remarks>
///     ⚠ Every expected string is the oracle's own answer, measured 2026-10-07 with <c>Testing ask</c> at
///     <c>skala_keep_user_linebreaks = false</c>, at <c>skala_keep_existing_linebreaks = false</c> (which
///     answered the same everywhere) and at the defaults. Every row is checked on a second pass.
/// </remarks>
public sealed class KeepFalseLeftoversIssue443Tests {
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

    static string InMethod(string statement) =>
        "class P {\n    void M(int a, int b) { }\n\n"
        + $"    void T(object o, int[] a, int k) {{\n        {statement}\n    }}\n}}\n";

    static void AssertFormats(string statement, string expected, params (string Key, string Value)[] overrides) {
        var once = FormatWith(InMethod(statement), overrides);
        Assert.Contains(expected, once, StringComparison.Ordinal);
        Assert.Equal(once, FormatWith(once, overrides));
    }

    static void AssertAtBothKeys(string statement, string expected) {
        AssertFormats(statement, expected, ("skala_keep_user_linebreaks", "false"));
        AssertFormats(statement, expected, ("skala_keep_existing_linebreaks", "false"));
    }

    /// <summary>A filled list re-joins its delimiters without kept line breaks.</summary>
    [Theory]
    [InlineData("var q = o is P(\n            1, 2);", "        var q = o is P(1, 2);\n")]
    [InlineData("var q = o is P(1, 2\n            );", "        var q = o is P(1, 2);\n")]
    [InlineData("var q = o is (\n            1, 2);", "        var q = o is (1, 2);\n")]
    [InlineData("var t = (\n            1, 2);", "        var t = (1, 2);\n")]
    [InlineData("var t = (1,\n            2);", "        var t = (1, 2);\n")]
    public void AFilledListsDelimiters_AreJoined(string statement, string expected) =>
        AssertAtBothKeys(statement, expected);

    /// <summary>The gap after `is`, `as`, a pattern's `not` and `case` is joined.</summary>
    [Theory]
    [InlineData("var i = o is\n            (1, 2);", "        var i = o is (1, 2);\n")]
    [InlineData("var i = o is\n            P;", "        var i = o is P;\n")]
    [InlineData("var i = o is\n            null;", "        var i = o is null;\n")]
    [InlineData("var i = o is not\n            null;", "        var i = o is not null;\n")]
    [InlineData("var i = o as\n            P;", "        var i = o as P;\n")]
    [InlineData(
        "switch (o) {\n            case\n                (1, 2):\n                break;\n        }",
        "            case (1, 2):\n"
    )]
    public void TheGapAfterATypeTestsKeyword_IsJoined(string statement, string expected) =>
        AssertAtBothKeys(statement, expected);

    /// <summary>
    ///     ⚠ A collection expression and a list pattern answer to their own keep key alone: the oracle
    ///     chops a broken one at either value of the global key.
    /// </summary>
    [Theory]
    [InlineData(
        "int[] s = [1,\n            ..a];",
        "        int[] s = [\n            1,\n            ..a\n        ];\n"
    )]
    [InlineData("int[] s = [\n            ..a];", "        int[] s = [\n            ..a\n        ];\n")]
    [InlineData("int[] s = [1, 2\n        ];", "        int[] s = [\n            1, 2\n        ];\n")]
    [InlineData(
        "var l = a is [1,\n            2];",
        "        var l = a is [\n            1,\n            2\n        ];\n"
    )]
    public void ACollectionExpression_KeepsItsArrangement(string statement, string expected) {
        AssertAtBothKeys(statement, expected);
        AssertFormats(statement, expected);
    }

    [Theory]
    [InlineData("int[] s = [1,\n            ..a];", "        int[] s = [1, ..a];\n")]
    [InlineData("var l = a is [1,\n            2];", "        var l = a is [1, 2];\n")]
    public void OnlyItsOwnKey_JoinsIt(string statement, string expected) {
        AssertFormats(statement, expected, ("skala_keep_existing_list_patterns_arrangement", "false"));
        AssertFormats(
            statement,
            expected,
            ("skala_keep_existing_list_patterns_arrangement", "false"),
            ("skala_keep_user_linebreaks", "false")
        );
    }

    /// <summary>At the defaults: `is` / `P` and `as` / `P` are kept, the point before the operator whole.</summary>
    [Theory]
    [InlineData("var i = o is\n            P;", "        var i = o is\n            P;\n")]
    [InlineData("var i = o as\n            P;", "        var i = o as\n            P;\n")]
    public void AtTheDefaults_ABreakAfterIsOrAs_IsKept(string statement, string expected) =>
        AssertFormats(statement, expected);

    /// <summary>
    ///     ⚠ At the defaults a filled list's closer the author put on its own line stays one level in when
    ///     the first item shares the opener's line.
    /// </summary>
    [Theory]
    [InlineData("var q = o is P(1, 2\n            );", "        var q = o is P(1, 2\n            );\n")]
    [InlineData("var q = o is P(1, 2\n        );", "        var q = o is P(1, 2\n            );\n")]
    [InlineData("var t = (1, 2\n        );", "        var t = (1, 2\n            );\n")]
    [InlineData("var (d1, d2\n            ) = (1, 2);", "        var (d1, d2\n            ) = (1, 2);\n")]
    public void AtTheDefaults_AFilledListsKeptCloser_StaysOneLevelIn(string statement, string expected) =>
        AssertFormats(statement, expected);
}
