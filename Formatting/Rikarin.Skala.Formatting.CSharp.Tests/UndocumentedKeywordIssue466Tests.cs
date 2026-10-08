using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Core.Configuration;

namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     #466: <c>__makeref</c>, <c>__reftype</c>, <c>__refvalue</c> and <c>__arglist</c> — the gap in
///     front of their parenthesis and both gaps inside it are nobody's, and come back as written.
/// </summary>
/// <remarks>
///     ⚠ Every expected string is the oracle's answer, measured 2026-10-08 with <c>Testing ask</c> at the
///     export and again with <c>space_before_method_call_parentheses</c>,
///     <c>space_between_keyword_and_expression</c>, <c>space_within_parentheses</c> and
///     <c>space_between_method_call_parameter_list_parentheses</c> flipped: none of them moves these gaps.
///     Skala used to answer the first three's <c>(</c> from the keyword-and-expression key and wrote
///     <c>__makeref (o)</c> at the export.
/// </remarks>
public sealed class UndocumentedKeywordIssue466Tests {
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
        "class C {\n    static void N(int a, __arglist) { }\n\n"
        + $"    static void M(int o) {{\n        {statement}\n    }}\n}}\n";

    static void AssertFormats(string statement, string expected, params (string Key, string Value)[] overrides) {
        var once = FormatWith(Body(statement), overrides);
        Assert.Contains("        " + expected + "\n", once, StringComparison.Ordinal);
        Assert.Equal(once, FormatWith(once, overrides));
    }

    public static TheoryData<string, string> Shapes =>
        new() {
            { "var r = __makeref(o);", "var r = __makeref(o);" },
            { "var r = __makeref (o);", "var r = __makeref (o);" },
            { "var r = __makeref(  o  );", "var r = __makeref( o );" },
            { "var r = __makeref( o);", "var r = __makeref( o);" },
            { "var k = __reftype(__makeref(o));", "var k = __reftype(__makeref(o));" },
            { "var k = __reftype (__makeref(o));", "var k = __reftype (__makeref(o));" },
            { "var v = __refvalue(__makeref(o),int);", "var v = __refvalue(__makeref(o), int);" },
            { "var v = __refvalue ( __makeref(o), int );", "var v = __refvalue ( __makeref(o), int );" },
            { "N(1, __arglist(1, 2));", "N(1, __arglist(1, 2));" },
            { "N(1, __arglist (1, 2));", "N(1, __arglist (1, 2));" },
            { "N(1, __arglist(  1, 2 ));", "N(1, __arglist( 1, 2 ));" },
            { "N(1, __arglist( ));", "N(1, __arglist( ));" }
        };

    [Theory]
    [MemberData(nameof(Shapes))]
    public void TheGapsComeBackAsWritten_AtTheExport(string statement, string expected) =>
        AssertFormats(statement, expected);

    [Theory]
    [MemberData(nameof(Shapes))]
    public void TheGapsComeBackAsWritten_WithTheParenthesisKeysFlipped(string statement, string expected) =>
        AssertFormats(
            statement,
            expected,
            ("skala_space_between_keyword_and_expression", "false"),
            ("skala_space_within_parentheses", "true")
        );

    /// <summary>
    ///     The call-site key opens the enclosing call and leaves <c>__arglist</c>'s own parentheses alone:
    ///     the oracle writes <c>N( __arglist(  1, 2) )</c> as <c>N( __arglist( 1, 2) )</c>.
    /// </summary>
    [Theory]
    [InlineData("N(1, __arglist(1, 2));", "N( 1, __arglist(1, 2) );")]
    [InlineData("N(1, __arglist(  1, 2));", "N( 1, __arglist( 1, 2) );")]
    [InlineData("N(1, __arglist( ));", "N( 1, __arglist( ) );")]
    public void TheCallSiteKey_OpensOnlyTheEnclosingCall(string statement, string expected) =>
        AssertFormats(statement, expected, ("skala_space_between_method_call_parameter_list_parentheses", "true"));

    [Fact]
    public void TheMethodCallKey_StillMovesTheEnclosingCall_AndNotArglist() =>
        AssertFormats(
            "N(1, __arglist(1, 2));",
            "N (1, __arglist(1, 2));",
            ("skala_space_before_method_call_parentheses", "true")
        );

    /// <summary>The gap behind the <c>)</c> is not theirs: it is whatever follows.</summary>
    [Fact]
    public void TheGapBehindTheParenthesis_IsStillGoverned() =>
        AssertFormats("var k = __reftype(__makeref(o))  .Name;", "var k = __reftype(__makeref(o)).Name;");
}
