using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Core.Configuration;

namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     #439: at <c>keep_user_linebreaks = false</c> an author's break in a gap no rule governs is joined,
///     taking its bit from the indentation of the line it ended on.
/// </summary>
/// <remarks>
///     ⚠ Every expected string is the oracle's own answer, measured 2026-10-07 with <c>Testing ask</c> at
///     <c>skala_keep_user_linebreaks = false</c> (and <c>skala_keep_existing_linebreaks = false</c>, which
///     answered the same) and at the defaults. Every row is also checked on a second pass.
/// </remarks>
public sealed class JoinAtKeepFalseIssue439Tests {
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
        "class P {\n    void M() { }\n\n"
        + $"    void T(object o, int[] a) {{\n        {statement}\n        goto lab;\n    }}\n}}\n";

    static void AssertFormats(string statement, string expected, params (string Key, string Value)[] overrides) {
        var once = FormatWith(InMethod(statement), overrides);
        Assert.Contains(expected, once, StringComparison.Ordinal);
        Assert.Equal(once, FormatWith(once, overrides));
    }

    [Theory]
    [InlineData("var r = a[1\n..2];", "        var r = a[1..2];\n")]
    [InlineData("var r = a[1\n            ..2];", "        var r = a[1 ..2];\n")]
    [InlineData("var r = a[1..\n            2];", "        var r = a[1.. 2];\n")]
    [InlineData("var r = a[1\n            ..];", "        var r = a[1 ..];\n")]
    [InlineData("var r = a[..\n            2];", "        var r = a[.. 2];\n")]
    [InlineData("var r = a[1  \n..2];", "        var r = a[1..2];\n")]
    [InlineData("var l = a is [1, ..\n            var rest];", "        var l = a is [1, .. var rest];\n")]
    [InlineData("var q = o is P\n(1, 2);", "        var q = o is P(1, 2);\n")]
    [InlineData("var q = o is P\n            (1, 2);", "        var q = o is P (1, 2);\n")]
    [InlineData("lab\n: M();", "        lab:\n        M();\n")]
    [InlineData("lab\n            : M();", "        lab :\n        M();\n")]
    public void AnUngovernedGapsBreak_IsJoined(string statement, string expected) {
        AssertFormats(statement, expected, ("skala_keep_user_linebreaks", "false"));
        AssertFormats(statement, expected, ("skala_keep_existing_linebreaks", "false"));
    }

    /// <summary>
    ///     ⚠ A collection expression's spread was in the theory above and is not ungoverned any more: Skala
    ///     governs it on purpose (#513, SK-DIV-0310), so a joined break takes the key's answer rather than
    ///     the indentation's bit. The oracle's <c>[1, .. a]</c> here is the author's bit, which Skala no
    ///     longer reads.
    /// </summary>
    [Theory]
    [InlineData("false", "        int[] s = [1, ..a];\n")]
    [InlineData("true", "        int[] s = [1, .. a];\n")]
    public void ASpreadsJoinedBreak_TakesTheSpreadKey(string value, string expected) {
        const string statement = "int[] s = [1, ..\n            a];";
        AssertFormats(
            statement,
            expected,
            ("skala_keep_user_linebreaks", "false"),
            ("skala_space_within_spread_pattern", value)
        );
        AssertFormats(
            statement,
            expected,
            ("skala_keep_existing_linebreaks", "false"),
            ("skala_space_within_spread_pattern", value)
        );
    }

    /// <summary>At the defaults every one of those breaks is kept.</summary>
    [Theory]
    [InlineData("var r = a[1\n..2];", "        var r = a[1\n            ..2];\n")]
    [InlineData("var q = o is P\n(1, 2);", "        var q = o is P\n            (1, 2);\n")]
    [InlineData("lab\n: M();", "        lab\n            :\n        M();\n")]
    public void AtTheDefaults_TheBreakIsKept(string statement, string expected) => AssertFormats(statement, expected);

    /// <summary>
    ///     ⚠ A break after a spread's or a slice pattern's <c>..</c> puts the operand on the element's own
    ///     column, with no level of its own — measured at the defaults, where the break is kept.
    /// </summary>
    [Theory]
    [InlineData(
        "int[] s = [1, ..\n            a];",
        "        int[] s = [\n            1, ..\n            a\n        ];\n"
    )]
    [InlineData(
        "var l = a is [1, ..\n            var rest];",
        "        var l = a is [\n            1, ..\n            var rest\n        ];\n"
    )]
    public void TheOperandAfterASpreadsBreak_TakesNoLevel(string statement, string expected) =>
        AssertFormats(statement, expected);
}
