using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Core.Configuration;

namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     #410: the gap between a block comment and the token after it, which Skala answered with one
///     space whatever stood there.
/// </summary>
/// <remarks>
///     ⚠ Every expected string is the oracle's own answer, measured 2026-10-07 with <c>Testing ask</c>
///     under the repository's configuration, each gap written closed and spaced, and again with 37
///     space keys flipped in two sets and with <c>skala_space_before_trailing_comment = false</c>. The
///     gap falls into three classes (<see cref="SpaceRules.AfterBlockComment" />): the next token's rule
///     read across the comment, the author's bit, and one space. Every row is also checked on a second
///     pass, because a gap that keeps the author's bit is only right if it reads back the same.
/// </remarks>
public sealed class BlockCommentGapIssue410Tests {
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
        "class G<T> { }\n\nclass C {\n    int f;\n    int[] a = [1, 2];\n\n"
        + "    void M(int x, int y) { }\n\n    void Rf(ref int z) { }\n\n"
        + $"    int T() {{\n        {statement}\n        return 0;\n    }}\n}}\n";

    static void AssertFormats(string statement, string expected, params (string Key, string Value)[] overrides) {
        var once = FormatWith(Body(statement), overrides);
        Assert.Contains("        " + expected + "\n", once, StringComparison.Ordinal);
        Assert.Equal(once, FormatWith(once, overrides));
    }

    /// <summary>
    ///     The next token owns the gap, and its rule is read as though the comment were not there:
    ///     nothing before <c>,</c> <c>)</c> <c>]</c> <c>&gt;</c> <c>;</c> <c>.</c> a call's <c>(</c>, an
    ///     element access's <c>[</c>, a postfix operator or a named argument's colon — a written space
    ///     is removed — and one space before a binary operator, an <c>if</c>'s parenthesis and an
    ///     initializer's closing brace — a missing one is added.
    /// </summary>
    [Theory]
    [InlineData("M(1/*f*/, 2);", "M(1 /*f*/, 2);")]
    [InlineData("M(1 /*f*/ , 2);", "M(1 /*f*/, 2);")]
    [InlineData("M(1, 2/*f*/);", "M(1, 2 /*f*/);")]
    [InlineData("M(1, 2 /*f*/ );", "M(1, 2 /*f*/);")]
    [InlineData("var v = 1 /*f*/ ;", "var v = 1 /*f*/;")]
    [InlineData("var v = f /*f*/ .ToString();", "var v = f /*f*/.ToString();")]
    [InlineData("var v = a[1 /*f*/ ];", "var v = a[1 /*f*/];")]
    [InlineData("var v = (f /*f*/ );", "var v = (f /*f*/);")]
    [InlineData("var v = new G<int /*f*/ >();", "var v = new G<int /*f*/>();")]
    [InlineData("M /*f*/ (1, 2);", "M /*f*/(1, 2);")]
    [InlineData("var v = a /*f*/ [1];", "var v = a /*f*/[1];")]
    [InlineData("var v = new G /*f*/ <int>();", "var v = new G /*f*/<int>();")]
    [InlineData("var v = a /*f*/ ?.Length;", "var v = a /*f*/?.Length;")]
    [InlineData("var v = f /*f*/ ++;", "var v = f /*f*/++;")]
    [InlineData("M(x /*f*/ : 1, y: 2);", "M(x /*f*/: 1, y: 2);")]
    [InlineData("if/*f*/(f > 0) { }", "if /*f*/ (f > 0) { }")]
    [InlineData("var v = f/*f*/+ 1;", "var v = f /*f*/ + 1;")]
    [InlineData("var v = f > 0/*f*/? 1 : 2;", "var v = f > 0 /*f*/ ? 1 : 2;")]
    [InlineData("f/*f*/= 2;", "f /*f*/ = 2;")]
    [InlineData("var v = new int[] { 1/*f*/};", "var v = new int[] { 1 /*f*/ };")]
    [InlineData("M(1 /*a*/ /*b*/ , 2);", "M(1 /*a*/ /*b*/, 2);")]
    [InlineData("M(1/*a*//*b*/, 2);", "M(1 /*a*/ /*b*/, 2);")]
    public void TheNextTokensOwnRule_DecidesTheGap(string statement, string expected) =>
        AssertFormats(statement, expected);

    /// <summary>
    ///     ⚠ A pair the comment sits inside is not empty: <c>[ /*f*/]</c> reads the non-empty bracket
    ///     rule and <c>{ /*f*/ }</c> the initializer's, not <c>space_within_empty_braces</c>.
    /// </summary>
    [Theory]
    [InlineData("int[] v = [/*f*/];", "int[] v = [ /*f*/];")]
    [InlineData("int[] v = [ /*f*/ ];", "int[] v = [ /*f*/];")]
    [InlineData("var v = new int[] {/*f*/};", "var v = new int[] { /*f*/ };")]
    public void AnEmptyPair_IsNotEmptyOnceItHoldsAComment(string statement, string expected) =>
        AssertFormats(statement, expected);

    /// <summary>
    ///     The token before the comment owns the gap, and the oracle has no rule for a comment in it:
    ///     closed stays closed and spaced stays spaced.
    /// </summary>
    [Theory]
    [InlineData("M(/*f*/1, 2);", "M( /*f*/1, 2);")]
    [InlineData("M( /*f*/ 1, 2);", "M( /*f*/ 1, 2);")]
    [InlineData("M(1, /*f*/2);", "M(1, /*f*/2);")]
    [InlineData("M(1,/*f*/2);", "M(1, /*f*/2);")]
    [InlineData("M(1, /*f*/ 2);", "M(1, /*f*/ 2);")]
    [InlineData("var v = a[/*f*/1];", "var v = a[ /*f*/1];")]
    [InlineData("var v = f +/*f*/1;", "var v = f + /*f*/1;")]
    [InlineData("var v = f + /*f*/ 1;", "var v = f + /*f*/ 1;")]
    [InlineData("var v = f + /*f*/(f);", "var v = f + /*f*/(f);")]
    [InlineData("f =/*f*/2;", "f = /*f*/2;")]
    [InlineData("var v = f > 0 ?/*f*/1 : 2;", "var v = f > 0 ? /*f*/1 : 2;")]
    [InlineData("var v = (int)/*f*/f;", "var v = (int) /*f*/f;")]
    [InlineData("var v = f./*f*/ToString();", "var v = f. /*f*/ToString();")]
    [InlineData("var v = -/*f*/f;", "var v = - /*f*/f;")]
    [InlineData("var v = new int[] {/*f*/1 };", "var v = new int[] { /*f*/1 };")]
    [InlineData("var v = new /*f*/C();", "var v = new /*f*/C();")]
    [InlineData("for (var i = 0; /*f*/i < 1; i++) { }", "for (var i = 0; /*f*/i < 1; i++) { }")]
    [InlineData("foreach (var x /*f*/in a) { }", "foreach (var x /*f*/in a) { }")]
    [InlineData("foreach (var x in/*f*/a) { }", "foreach (var x in /*f*/a) { }")]
    [InlineData("throw/*f*/new System.Exception();", "throw /*f*/new System.Exception();")]
    public void TheTokenBeforeTheComment_KeepsTheAuthorsGap(string statement, string expected) =>
        AssertFormats(statement, expected);

    /// <summary>
    ///     One space whatever the author wrote, where the oracle was measured to insert it.
    /// </summary>
    [Theory]
    [InlineData("var v = f is/*f*/int;", "var v = f is /*f*/ int;")]
    [InlineData("var v = f as/*f*/object;", "var v = f as /*f*/ object;")]
    [InlineData("M(x: /*f*/1, y: 2);", "M(x: /*f*/ 1, y: 2);")]
    [InlineData("Rf(ref/*f*/f);", "Rf(ref /*f*/ f);")]
    [InlineData("int[] v = /*f*/[1];", "int[] v = /*f*/ [1];")]
    public void TheRestTakeOneSpace(string statement, string expected) => AssertFormats(statement, expected);

    /// <summary>A return keeps the author's gap, and a return type's name does not.</summary>
    [Fact]
    public void AReturnKeepsTheGap_AndAMemberNameDoesNot() {
        var formatted = FormatWith("class C {\n    int /*f*/T() {\n        return/*f*/1;\n    }\n}\n");
        Assert.Contains("int /*f*/ T() {", formatted, StringComparison.Ordinal);
        Assert.Contains("return /*f*/1;", formatted, StringComparison.Ordinal);
    }

    /// <summary>
    ///     ⚠ The first class reads its key: flipped, the gap moves with it. The second class does not:
    ///     the key that governs the gap without a comment leaves the author's bit alone.
    /// </summary>
    [Theory]
    [InlineData("M(1/*f*/, 2);", "M(1 /*f*/ , 2);", "skala_space_before_comma", "true")]
    [InlineData(
        "var v = f > 0/*f*/? 1 : 2;",
        "var v = f > 0 /*f*/? 1 : 2;",
        "skala_space_before_ternary_quest",
        "false"
    )]
    [InlineData("var v = f /*f*/ + 1;", "var v = f /*f*/+1;", "skala_space_around_additive_op", "false")]
    [InlineData("M(1, /*f*/ 2);", "M(1, /*f*/ 2);", "skala_space_after_comma", "false")]
    [InlineData("var v = f + /*f*/1;", "var v = f+ /*f*/1;", "skala_space_around_additive_op", "false")]
    public void AKeyMovesOnlyTheGapItOwns(string statement, string expected, string key, string value) =>
        AssertFormats(statement, expected, (key, value));

    /// <summary>
    ///     ⚠ The gap <em>before</em> the comment: <c>skala_space_before_trailing_comment</c> owns it,
    ///     except behind an assignment and a lambda's or an expression body's arrow, where the operator's
    ///     own key does — at either value of the comment key.
    /// </summary>
    [Theory]
    [InlineData("M(1 /*f*/, 2);", "M(1/*f*/, 2);", "skala_space_before_trailing_comment", "false")]
    [InlineData("f = /*f*/2;", "f = /*f*/2;", "skala_space_before_trailing_comment", "false")]
    [InlineData("f = /*f*/ 2;", "f=/*f*/ 2;", "skala_space_around_assignment_op", "false")]
    [InlineData("f += /*f*/1;", "f+=/*f*/1;", "skala_space_around_assignment_op", "false")]
    [InlineData(
        "System.Func<int, int> l = x => /*f*/x;",
        "System.Func<int, int> l = x=>/*f*/x;",
        "skala_space_around_lambda_arrow",
        "false"
    )]
    public void TheGapBeforeTheComment_FollowsTheOperatorsKey(
        string statement,
        string expected,
        string key,
        string value
    ) =>
        AssertFormats(statement, expected, (key, value));

    /// <summary>
    ///     A comment that starts a line is not this gap: the oracle puts what follows it on a line of its
    ///     own, and Skala does too.
    /// </summary>
    [Fact]
    public void ACommentThatStartsALine_IsNotTheSameLineGap() {
        var formatted = FormatWith(Body("/*f*/M(1, 2);"));
        Assert.Contains("        /*f*/\n        M(1, 2);\n", formatted, StringComparison.Ordinal);
    }
}
