using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Core.Configuration;

namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     #433: a labelled statement starts a line of its own, an empty one stays behind <c>label: ;</c>, and
///     the gap in front of a label's colon is the author's.
/// </summary>
/// <remarks>
///     ⚠ Every expected string is the oracle's own answer, measured 2026-10-07 with <c>Testing ask</c>
///     under the repository's configuration, each gap written closed and spaced, and again with twelve
///     colon, semicolon, comment and label keys flipped one at a time. Every row is also checked on a
///     second pass.
/// </remarks>
public sealed class LabelledStatementIssue433Tests {
    static string FormatWith(string source, params (string Key, string Value)[] overrides) {
        var options = new PhaseOneOptions(
            OptionResolver.Resolve(
                Path.Combine(Rikarin.Skala.Testing.Corpus.RepositoryRoot, "Test.cs"),
                [.. overrides.Select(static o => new KeyValuePair<string, string>(o.Key, o.Value))]
            )
                .Options
        );

        return CSharpFormatter.Format("Test.cs", SourceText.From(source), options).Formatted;
    }

    static string InMethod(string statements) =>
        "class L {\n    void M(int a) { }\n\n    void T(int k) {\n        "
        + statements
        + "\n        goto a;\n    }\n}\n";

    static void AssertFormats(string statements, string expected, params (string Key, string Value)[] overrides) {
        var once = FormatWith(InMethod(statements), overrides);
        Assert.Contains(expected, once, StringComparison.Ordinal);
        Assert.Equal(once, FormatWith(once, overrides));
    }

    [Theory]
    [InlineData("a: M(1);", "        a:\n        M(1);\n")]
    [InlineData("a:M(1);", "        a:\n        M(1);\n")]
    [InlineData("a: var v = 1;", "        a:\n        var v = 1;\n")]
    [InlineData("a: for (var i = 0; i < 1; i++) { }", "        a:\n        for (var i = 0; i < 1; i++) { }\n")]
    [InlineData("a: { M(6); }", "        a:\n        {\n            M(6);\n        }\n")]
    [InlineData("a: { }", "        a:\n        { }\n")]
    [InlineData("b: a: M(10);", "        b:\n        a:\n        M(10);\n")]
    [InlineData("a: M(5); M(6);", "        a:\n        M(5);\n        M(6);\n")]
    [InlineData("a: /*c*/M(3);", "        a: /*c*/\n        M(3);\n")]
    [InlineData("a:/*c*/ M(4);", "        a: /*c*/\n        M(4);\n")]
    [InlineData("a\n        : M(13);", "        a\n            :\n        M(13);\n")]
    public void ALabelledStatement_StartsALineOfItsOwn(string statements, string expected) =>
        AssertFormats(statements, expected);

    [Fact]
    public void InsideASwitchSection_TheStatementStillStartsALine() =>
        AssertFormats(
            "switch (k) {\n            case 1:\n                a: M(11);\n                break;\n        }",
            "                a:\n                M(11);\n                break;\n"
        );

    /// <summary>An empty statement stays behind its label, one space after the colon.</summary>
    [Theory]
    [InlineData("a:;")]
    [InlineData("a: ;")]
    [InlineData("a:   ;")]
    public void AnEmptyStatement_StaysBehindTheLabel(string statements) {
        AssertFormats(statements, "        a: ;\n");
        AssertFormats(statements, "        a: ;\n", ("skala_space_before_semicolon", "true"));
    }

    /// <summary>The gap in front of a label's colon is the author's; a run collapses to one space.</summary>
    [Theory]
    [InlineData("a: M(1);", "        a:\n")]
    [InlineData("a :M(1);", "        a :\n")]
    [InlineData("a  :  M(1);", "        a :\n")]
    [InlineData("a /*c*/: M(1);", "        a /*c*/:\n")]
    [InlineData("a/*c*/ : M(1);", "        a /*c*/ :\n")]
    public void TheGapBeforeALabelsColon_IsTheAuthors(string statements, string expected) {
        AssertFormats(statements, expected);
        AssertFormats(statements, expected, ("skala_space_before_attribute_colon", "true"));
        AssertFormats(statements, expected, ("skala_space_before_colon_in_case", "true"));
    }

    /// <summary>
    ///     <c>skala_outdent_statement_labels = true</c> moves the label out, an empty statement with it,
    ///     and leaves every other statement at its level.
    /// </summary>
    [Fact]
    public void AnOutdentedLabel_LeavesItsStatementIn() {
        AssertFormats("a: M(1);", "    a:\n        M(1);\n", ("skala_outdent_statement_labels", "true"));
        AssertFormats("a:;", "    a: ;\n", ("skala_outdent_statement_labels", "true"));
    }
}
