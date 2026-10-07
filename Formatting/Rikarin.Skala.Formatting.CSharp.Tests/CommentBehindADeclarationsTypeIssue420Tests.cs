using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Core.Configuration;

namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     #420: a block comment between a declaration's type and its first name — the oracle breaks the
///     line after the comment and puts the name one continuation level in, and Skala kept the line whole.
/// </summary>
/// <remarks>
///     ⚠ Every expected string is the oracle's own answer, measured 2026-10-07 with <c>Testing ask</c>
///     under the repository's configuration on about sixty shapes, each written closed and spaced. Every
///     row is also checked on a second pass.
/// </remarks>
public sealed class CommentBehindADeclarationsTypeIssue420Tests {
    static string Format(string source) {
        var options = new PhaseOneOptions(
            OptionResolver.Resolve(Path.Combine(Rikarin.Skala.Testing.Corpus.RepositoryRoot, "Test.cs"), []).Options
        );

        return CSharpFormatter.Format("Test.cs", SourceText.From(source), options).Formatted;
    }

    static string InMethod(string statement) =>
        "class C {\n    void M(out int x) { x = 0; }\n\n"
        + $"    unsafe void T(object o, int[] arr) {{\n        {statement}\n    }}\n}}\n";

    static void AssertFormats(string source, string expected) {
        var once = Format(source);
        Assert.Contains(expected, once, StringComparison.Ordinal);
        Assert.Equal(once, Format(once));
    }

    [Theory]
    [InlineData("var /*k01*/v1 = 1;", "        var /*k01*/\n            v1 = 1;\n")]
    [InlineData("int /*k02*/ v2 = 1;", "        int /*k02*/\n            v2 = 1;\n")]
    [InlineData(
        """(int, string) /*k03*/ v3 = (1, "");""",
        "        (int, string) /*k03*/\n            v3 = (1, \"\");\n"
    )]
    [InlineData("System.Func<int> /*k04*/ v4 = null;", "        System.Func<int> /*k04*/\n            v4 = null;\n")]
    [InlineData("int? /*k05*/v5 = 1;", "        int? /*k05*/\n            v5 = 1;\n")]
    [InlineData("const int /*k09*/ v9 = 1;", "        const int /*k09*/\n            v9 = 1;\n")]
    [InlineData("ref int /*k22*/ v22 = ref arr[0];", "        ref int /*k22*/\n            v22 = ref arr[0];\n")]
    [InlineData("int* /*u03*/ q = null;", "        int* /*u03*/\n            q = null;\n")]
    [InlineData("var /*a*/ /*b*/ v = 1;", "        var /*a*/ /*b*/\n            v = 1;\n")]
    [InlineData("int/*k27*/v27 = 1;", "        int /*k27*/\n            v27 = 1;\n")]
    [InlineData("int /*a02*/\n            y = 1;", "        int /*a02*/\n            y = 1;\n")]
    [InlineData("int\n            /*a03*/ z = 1;", "        int\n            /*a03*/\n            z = 1;\n")]
    [InlineData(
        "using (System.IDisposable /*u02*/ d = null) { }",
        "        using (System.IDisposable /*u02*/\n               d = null) { }\n"
    )]
    public void ACommentBehindTheType_BreaksTheLineAfterIt(string statement, string expected) =>
        AssertFormats(InMethod(statement), expected);

    /// <summary>The declarators stay together behind the broken head, as they would without it.</summary>
    [Theory]
    [InlineData("int /*k10*/ a10, b10;", "        int /*k10*/\n            a10, b10;\n")]
    [InlineData("int /*d03*/ a = 1, /*d04*/ b = 2;", "        int /*d03*/\n            a = 1, /*d04*/ b = 2;\n")]
    public void TheDeclaratorList_StaysWholeBehindTheBreak(string statement, string expected) =>
        AssertFormats(InMethod(statement), expected);

    /// <summary>
    ///     ⚠ A break after the name's <c>=</c> takes a second level on top of the head's: the name is a
    ///     continuation, and its value one more.
    /// </summary>
    [Fact]
    public void AnEqualsBreakBehindTheBrokenHead_TakesASecondLevel() {
        var literal = "\"" + new string('a', 110) + "\"";
        AssertFormats(
            InMethod($"string /*h04*/ s = {literal};"),
            $"        string /*h04*/\n            s =\n                {literal};\n"
        );
        AssertFormats(
            InMethod("var /*d01*/ x = arr.Length > 0\n            ? arr[0]\n            : 1;"),
            "        var /*d01*/\n            x = arr.Length > 0\n                ? arr[0]\n                : 1;\n"
        );
    }

    [Theory]
    [InlineData("int /*f01*/f1 = 1;", "    int /*f01*/\n        f1 = 1;\n")]
    [InlineData("event System.Action /*f10*/ E1;", "    event System.Action /*f10*/\n        E1;\n")]
    [InlineData(
        "System.Collections.Generic.List<int> /*f11*/ f11;",
        "    System.Collections.Generic.List<int> /*f11*/\n        f11;\n"
    )]
    public void AFieldBreaks_AsALocalDoes(string member, string expected) =>
        AssertFormats($"class C {{\n    int a;\n    {member}\n    int b;\n}}\n", expected);

    /// <summary>A comment anywhere else in a declaration head stays on the line.</summary>
    [Theory]
    [InlineData("int v6 /*k06*/ = 1;")]
    [InlineData("int v7 = /*k07*/ 1;")]
    [InlineData("const /*k08*/ int v8 = 1;")]
    [InlineData("int a11, /*k11*/ b11;")]
    [InlineData("foreach (var /*k14*/ e in arr) { }")]
    [InlineData("for (int /*k15*/ i = 0; i < 1; i++) { }")]
    [InlineData("fixed (int* /*u01*/ p = arr) { }")]
    [InlineData("M(out var /*k16*/ v16);")]
    [InlineData("if (o is int /*k17*/ v17) { }")]
    [InlineData("var /*k19*/ (x19, y19) = (1, 2);")]
    [InlineData("System.Func<int, int> l20 = (int /*k20*/ z) => z;")]
    [InlineData("int /*k21*/ Local(int q) => q;")]
    [InlineData("scoped /*k23*/ System.Span<int> v23 = default;")]
    public void ACommentElsewhereInTheHead_StaysOnTheLine(string statement) =>
        AssertFormats(InMethod(statement), "        " + statement + "\n");

    [Theory]
    [InlineData("public /*f02*/ int f2 = 1;")]
    [InlineData("int f4 /*f04*/ = 1;")]
    [InlineData("int /*f08*/ P1 { get; set; }")]
    [InlineData("void /*m01*/ A1() { }")]
    [InlineData("void A4(int /*m04*/ x, int y) { }")]
    [InlineData("void A6(ref /*m06*/ int x) { }")]
    public void AMembersOrParametersComment_StaysOnTheLine(string member) =>
        AssertFormats($"class C {{\n    {member}\n}}\n", "    " + member + "\n");
}
