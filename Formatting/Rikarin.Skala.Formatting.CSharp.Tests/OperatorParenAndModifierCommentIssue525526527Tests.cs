using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Core.Configuration;

namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     #525 (an operator's gap before a parenthesis), #526 (a comment after a parameter's modifier) and
///     #527 (a switch section's block among other statements at the embedded key's <c>true</c>).
/// </summary>
/// <remarks>⚠ Every expected string is the oracle's answer, measured 2026-10-08 with <c>Testing ask</c>.</remarks>
public sealed class OperatorParenAndModifierCommentIssue525526527Tests {
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

    static void AssertContains(string source, string expected, params (string Key, string Value)[] overrides) {
        var once = FormatWith(source, overrides);
        Assert.Contains(expected + "\n", once, StringComparison.Ordinal);
        Assert.Equal(once, FormatWith(once, overrides));
    }

    static string Statement(string statement) =>
        $"class C {{\n    int f;\n    int g;\n\n    void T(int a, int b) {{\n        {statement}\n    }}\n}}\n";

    [Theory]
    [InlineData("f = (1);", "f=(1);")]
    [InlineData("f += (1);", "f+=(1);")]
    [InlineData("f <<= (1);", "f<<=(1);")]
    [InlineData("var x = (a + b);", "var x=(a+b);")]
    [InlineData("f = (int)g;", "f=(int)g;")]
    [InlineData("f = 1 + (2);", "f=1+(2);")]
    [InlineData("var c = a < (b);", "var c=a<(b);")]
    [InlineData("f = a + (b) + (a);", "f=a+(b)+(a);")]
    [InlineData("f = -(a);", "f=-(a);")]
    public void AnOperatorsKey_DecidesTheGapBeforeAParenthesis(string statement, string expected) =>
        AssertContains(
            Statement(statement),
            "        " + expected,
            ("skala_space_around_assignment_op", "false"),
            ("skala_space_around_additive_op", "false"),
            ("skala_space_around_relational_op", "false"),
            ("skala_space_around_shift_op", "false")
        );

    [Theory]
    [InlineData("f = (1);")]
    [InlineData("f = 1 + (2);")]
    [InlineData("var c = a < (b);")]
    [InlineData("f = -(a);")]
    public void AtTheExport_NothingMoves(string statement) => AssertContains(Statement(statement), "        " + statement);

    static string Members(string member) => $"static class C {{\n    {member}\n}}\n";

    [Theory]
    [InlineData("static void K(params /*f*/int[] a) { }")]
    [InlineData("static void K(params /*f*/ int[] a) { }")]
    [InlineData("static void L(ref /*f*/int a) { }")]
    [InlineData("static void I(in /*f*/int a) { }")]
    [InlineData("static void S(this /*f*/int a) { }")]
    [InlineData("static void R(ref readonly /*f*/int a) { }")]
    [InlineData("static void Sc(scoped /*f*/ref int a) { }")]
    public void AParametersModifier_KeepsTheAuthorsGapAfterAComment(string member) =>
        AssertContains(Members(member), "    " + member);

    [Theory]
    [InlineData("static /*f*/int F;", "static /*f*/ int F;")]
    [InlineData("public static /*f*/int P => 1;", "public static /*f*/ int P => 1;")]
    public void AMembersModifier_StillTakesOneSpace(string member, string expected) =>
        AssertContains(Members(member), "    " + expected);

    static string Switch(string section) =>
        "class C {\n    void M() { }\n\n    void T(int o) {\n        switch (o) {\n            "
        + section
        + "\n        }\n    }\n}\n";

    [Theory]
    [InlineData("case 3: { M(); } break;", "            case 3: {\n                M();\n            }\n                break;")]
    [InlineData("case 7: { M(); }\n            break;", "            case 7: {\n                M();\n            }\n                break;")]
    public void ASectionsBlockAmongOtherStatements_IsExpanded_AtTheEmbeddedKeysTrue(string section, string expected) =>
        AssertContains(Switch(section), expected, ("skala_keep_existing_embedded_block_arrangement", "true"));

    [Fact]
    public void ASectionsOnlyBlock_IsStillKept_AtTheEmbeddedKeysTrue() =>
        AssertContains(
            Switch("case 1: { M(); }"),
            "            case 1: { M(); }",
            ("skala_keep_existing_embedded_block_arrangement", "true")
        );

    [Fact]
    public void AnEmptyBlockThenBreak_StaysOnTheLabelsLine() =>
        AssertContains(Switch("case 6: { } break;"), "            case 6: { }\n                break;");
}
