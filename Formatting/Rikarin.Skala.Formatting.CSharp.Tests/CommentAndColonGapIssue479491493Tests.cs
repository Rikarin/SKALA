using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Core.Configuration;

namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     #479, #491 and #493: three gaps beside a colon or a block comment that Skala answered from the
///     wrong rule.
/// </summary>
/// <remarks>
///     ⚠ Every expected string is the oracle's answer, measured 2026-10-08 with <c>Testing ask</c> at the
///     export and with the keys named on each test flipped. Every row is also checked on a second pass.
/// </remarks>
public sealed class CommentAndColonGapIssue479491493Tests {
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

    static void AssertContains(string source, string expected, params (string Key, string Value)[] overrides) {
        var once = FormatWith(source, overrides);
        Assert.Contains(expected + "\n", once, StringComparison.Ordinal);
        Assert.Equal(once, FormatWith(once, overrides));
    }

    static string Statement(string statement) =>
        "class C {\n    int f;\n    System.Func<int, int> g;\n\n    void M(int a, int b) { }\n\n"
        + $"    void T() {{\n        {statement}\n    }}\n}}\n";

    static string Switch(string section) =>
        $"class C {{\n    void M(int o) {{\n        switch (o) {{\n            {section}\n        }}\n    }}\n}}\n";

    // ── #479 ─────────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    ///     A case label's empty statement takes the colon's gap, not the semicolon's: one space at the
    ///     export however it was written.
    /// </summary>
    [Theory]
    [InlineData("case 1:; break;", "case 1: ; break;")]
    [InlineData("case 1: ; break;", "case 1: ; break;")]
    [InlineData("case 1:   ; break;", "case 1: ; break;")]
    [InlineData("default:; break;", "default: ; break;")]
    [InlineData("case 3 when o > 0:; break;", "case 3 when o > 0: ; break;")]
    [InlineData("case 4: /*c*/; break;", "case 4: /*c*/ ; break;")]
    public void ACaseLabelsEmptyStatement_TakesTheColonsSpace(string section, string expected) =>
        AssertContains(Switch(section), "            " + expected);

    /// <summary>
    ///     ⚠ <c>space_after_colon_in_case = false</c> closes it; <c>space_before_semicolon = true</c> does
    ///     not open it.
    /// </summary>
    [Fact]
    public void TheColonKey_GovernsIt_AndTheSemicolonKeyDoesNot() {
        AssertContains(
            Switch("case 1: ; break;"),
            "            case 1:; break;",
            ("skala_space_after_colon_in_case", "false")
        );
        AssertContains(
            Switch("case 1:; break;"),
            "            case 1: ; break ;",
            ("skala_space_before_semicolon", "true")
        );
    }

    // ── #491 ─────────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    ///     A <c>/** … */</c> inside an expression is answered exactly as its <c>/* … */</c> twin: the
    ///     next token's rule after it, or the author's bit.
    /// </summary>
    [Theory]
    [InlineData("M(1 /** f */, 2);", "M(1 /** f */, 2);")]
    [InlineData("M(1 /** f */ , 2);", "M(1 /** f */, 2);")]
    [InlineData("M(1 /** f */,2);", "M(1 /** f */, 2);")]
    [InlineData("M(1, /** f */2);", "M(1, /** f */2);")]
    [InlineData("M(1, /** f */ 2);", "M(1, /** f */ 2);")]
    [InlineData("M(1, 2 /** i */ );", "M(1, 2 /** i */);")]
    [InlineData("M(/** h */1, 2);", "M( /** h */1, 2);")]
    [InlineData("var x = 1 /** g */+ 2;", "var x = 1 /** g */ + 2;")]
    [InlineData("f = 1 /** b */ ;", "f = 1 /** b */;")]
    [InlineData("f = /** a */2;", "f = /** a */2;")]
    [InlineData("f +=/** a */1;", "f += /** a */1;")]
    [InlineData("g = x =>/** a */x;", "g = x => /** a */x;")]
    public void ABlockDocComment_IsABlockCommentToTheGap(string statement, string expected) =>
        AssertContains(Statement(statement), "        " + expected);

    [Theory]
    [InlineData("f = (1 /** c */);", "f = ( 1 /** c */ );")]
    [InlineData("f = ( /** c */1);", "f = ( /** c */1 );")]
    public void ABlockDocComment_ReadsTheParenthesisKeyAcrossIt(string statement, string expected) =>
        AssertContains(Statement(statement), "        " + expected, ("skala_space_within_parentheses", "true"));

    // ── #493 ─────────────────────────────────────────────────────────────────────────────────────

    static string Members(string member) => $"class C {{\n    C(int a) {{ }}\n\n    {member}\n}}\n";

    /// <summary>
    ///     ⚠ An empty list holding only a comment, with its non-empty key on: the key's space, plus the
    ///     author's bit when the trailing-comment key also asks for one. ⚠ <c>F(/*f*/)</c> is the oracle's
    ///     <c>F( /*f*/ )</c> on its first run and <c>F(  /*f*/ )</c> on its second; Skala writes the fixed
    ///     point, because it has to be idempotent and the oracle here is not.
    /// </summary>
    [Theory]
    [InlineData("void E( /*f*/) { }", "void E(  /*f*/ ) { }")]
    [InlineData("void F(/*f*/) { }", "void F(  /*f*/ ) { }")]
    [InlineData("void G(/*f*/ ) { }", "void G(  /*f*/ ) { }")]
    [InlineData("void H(  /*f*/  ) { }", "void H(  /*f*/ ) { }")]
    [InlineData("void I( /*f*/ ) { }", "void I(  /*f*/ ) { }")]
    [InlineData("void K( /*f*/int a) { }", "void K( /*f*/ int a ) { }")]
    public void AnEmptyParameterList_AddsTheKeysSpaceToTheAuthors(string member, string expected) =>
        AssertContains(
            Members(member),
            "    " + expected,
            ("skala_space_between_method_declaration_parameter_list_parentheses", "true")
        );

    [Theory]
    [InlineData("T( /*f*/);", "        T(  /*f*/ );")]
    [InlineData("T(/*f*/);", "        T(  /*f*/ );")]
    [InlineData("var c = new C( /*f*/);", "        var c = new C(  /*f*/ );")]
    public void AnEmptyArgumentList_AddsTheKeysSpaceToTheAuthors(string statement, string expected) =>
        AssertContains(
            Statement(statement),
            expected,
            ("skala_space_between_method_call_parameter_list_parentheses", "true")
        );

    /// <summary>
    ///     ⚠ A call's empty list holding a comment is not empty in front of it either:
    ///     <c>space_before_method_call_parentheses = true</c> gives <c>T ( /*f*/)</c>.
    /// </summary>
    [Fact]
    public void ACallHoldingOnlyAComment_ReadsTheNonEmptyKeyInFrontOfIt() {
        AssertContains(
            Statement("T( /*f*/);"),
            "        T ( /*f*/);",
            ("skala_space_before_method_call_parentheses", "true")
        );
        AssertContains(
            Statement("T( /*f*/);"),
            "        T( /*f*/);",
            ("skala_space_before_empty_method_call_parentheses", "true")
        );
        AssertContains(
            Members("void D( /*f*/) { }"),
            "    void D ( /*f*/) { }",
            ("skala_space_before_method_parentheses", "true")
        );
        AssertContains(
            Members("void D( /*f*/) { }"),
            "    void D( /*f*/) { }",
            ("skala_space_before_empty_method_parentheses", "true")
        );
    }

    [Fact]
    public void AConstructorInitializersEmptyList_AddsTheKeysSpaceToTheAuthors() =>
        AssertContains(
            Members("C() : this( /*f*/) { }"),
            "    C() : this(  /*f*/ ) { }",
            ("skala_space_between_method_call_parameter_list_parentheses", "true")
        );

    /// <summary>At the trailing-comment key's <c>false</c>, the list key's one space alone.</summary>
    [Theory]
    [InlineData("void E( /*f*/) { }", "void E( /*f*/ ) { }")]
    [InlineData("void H(  /*f*/  ) { }", "void H( /*f*/ ) { }")]
    [InlineData("void K( /*f*/int a) { }", "void K(/*f*/ int a ) { }")]
    public void WithoutTheTrailingCommentSpace_TheListKeyAlone(string member, string expected) =>
        AssertContains(
            Members(member),
            "    " + expected,
            ("skala_space_between_method_declaration_parameter_list_parentheses", "true"),
            ("skala_space_before_trailing_comment", "false")
        );

    /// <summary>At the list key's <c>false</c>, the trailing-comment key alone, as before.</summary>
    [Theory]
    [InlineData("void E( /*f*/) { }", "void E( /*f*/) { }")]
    [InlineData("void F(/*f*/) { }", "void F( /*f*/) { }")]
    [InlineData("void H(  /*f*/  ) { }", "void H( /*f*/) { }")]
    public void AtTheExport_TheTrailingCommentKeyAlone(string member, string expected) =>
        AssertContains(Members(member), "    " + expected);

    /// <summary>
    ///     A declaration's first parameter after a comment takes one space, at every value measured; a
    ///     lambda's and a later parameter's keep the author's gap.
    /// </summary>
    [Theory]
    [InlineData("void A( /*f*/int a) { }", "void A( /*f*/ int a) { }")]
    [InlineData("void D( /*f*/ref int a) { }", "void D( /*f*/ ref int a) { }")]
    [InlineData("void B(int a, /*f*/int b) { }", "void B(int a, /*f*/int b) { }")]
    [InlineData("void H(int /*f*/a) { }", "void H(int /*f*/a) { }")]
    public void ADeclarationsFirstParameter_TakesOneSpaceAfterAComment(string member, string expected) =>
        AssertContains(Members(member), "    " + expected);

    [Fact]
    public void ALambdasFirstParameter_KeepsTheAuthorsGap() =>
        AssertContains(Statement("g = ( /*f*/int x) => x;"), "        g = ( /*f*/int x) => x;");
}
