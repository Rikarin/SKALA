using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Core.Configuration;

namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     #492, SK-DIV-0311: the inside of an interpolation hole is code, and the oracle spaces it as code.
/// </summary>
/// <remarks>
///     ⚠ Every expected string is the oracle's answer, measured 2026-10-08 with <c>Testing ask</c> at the
///     export and with the assignment, additive, relational, multiplicative, parenthesis, call-site and
///     comma keys flipped, and at <c>skala_space_before_trailing_comment = false</c>. Every row is checked
///     on a second pass.
/// </remarks>
public sealed class InterpolationHoleIssue492Tests {
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

    static string Body(string statement) =>
        "class C {\n    int M(int a, int b) => a;\n\n"
        + $"    void T(int f, int[] xs, string name) {{\n        {statement}\n    }}\n}}\n";

    static void AssertFormats(string statement, string expected, params (string Key, string Value)[] overrides) {
        var once = FormatWith(Body(statement), overrides);
        Assert.Contains("        " + expected + "\n", once, StringComparison.Ordinal);
        Assert.Equal(once, FormatWith(once, overrides));
    }

    [Theory]
    [InlineData("var s = $\"{ f }\";", "var s = $\"{f}\";")]
    [InlineData("var s = $\"{f+1}\";", "var s = $\"{f + 1}\";")]
    [InlineData("var s = $\"{ f , 5}\";", "var s = $\"{f,5}\";")]
    [InlineData("var s = $\"{f :N2}\";", "var s = $\"{f:N2}\";")]
    [InlineData("var s = $\"{ f ,-5 :N2}\";", "var s = $\"{f,-5:N2}\";")]
    [InlineData("var s = $\"{M(f,f)}\";", "var s = $\"{M(f, f)}\";")]
    [InlineData("var s = $\"{(f>0?f:-f)}\";", "var s = $\"{(f > 0 ? f : -f)}\";")]
    [InlineData("var s = $\"{name?.Length??0}\";", "var s = $\"{name?.Length ?? 0}\";")]
    [InlineData("var s = $\"x{ f }y{ f }z\";", "var s = $\"x{f}y{f}z\";")]
    [InlineData("var s = $@\"{ f }\";", "var s = $@\"{f}\";")]
    [InlineData("var s = $\"\"\"{ f }\"\"\";", "var s = $\"\"\"{f}\"\"\";")]
    [InlineData("var s = $\"{$\"{ f }\"}\";", "var s = $\"{$\"{f}\"}\";")]
    [InlineData("var s = $\"{new[] {1,2}.Length}\";", "var s = $\"{new[] { 1, 2 }.Length}\";")]
    [InlineData("var s = $\"{ - f }\";", "var s = $\"{-f}\";")]
    [InlineData("var s = $\"{f: N2}\";", "var s = $\"{f: N2}\";")]
    [InlineData("var s = $\"{  f  }  text  {f}\";", "var s = $\"{f}  text  {f}\";")]
    public void AHolesTokens_AreSpacedAsCode_AndTheTextIsNot(string statement, string expected) =>
        AssertFormats(statement, expected);

    [Theory]
    [InlineData("var s = $\"{f/*f*/}\";", "var s = $\"{f /*f*/}\";")]
    [InlineData("var s = $\"{/*f*/f}\";", "var s = $\"{ /*f*/f}\";")]
    [InlineData("var s = $\"{ /*f*/ f}\";", "var s = $\"{ /*f*/f}\";")]
    [InlineData("var s = $\"{f /*f*/ }\";", "var s = $\"{f /*f*/ }\";")]
    [InlineData("var s = $\"{ f/*f*/,5}\";", "var s = $\"{f /*f*/,5}\";")]
    [InlineData("var s = $\"{f/*a*//*b*/}\";", "var s = $\"{f /*a*/ /*b*/}\";")]
    public void ACommentInAHole_IsACommentInAnExpression(string statement, string expected) =>
        AssertFormats(statement, expected);

    [Theory]
    [InlineData("var s = $\"{f /*f*/}\";", "var s = $\"{f/*f*/}\";")]
    [InlineData("var s = $\"{ /*f*/ f}\";", "var s = $\"{/*f*/f}\";")]
    [InlineData("var s = $\"{f /*f*/ }\";", "var s = $\"{f/*f*/ }\";")]
    public void TheTrailingCommentKey_StillDecidesTheGapBeforeIt(string statement, string expected) =>
        AssertFormats(statement, expected, ("skala_space_before_trailing_comment", "false"));

    /// <summary>Every key that moves the same tokens outside a string moves them inside a hole.</summary>
    [Theory]
    [InlineData("var s = $\"{M(f,f)}\";", "var s=$\"{M( f,f )}\";")]
    [InlineData("var s = $\"{(f>0?f:-f)}\";", "var s=$\"{( f>0 ? f : -f )}\";")]
    [InlineData("var s = $\"{ f , 5}\";", "var s=$\"{f,5}\";")]
    public void TheKeysReachIntoAHole(string statement, string expected) =>
        AssertFormats(
            statement,
            expected,
            ("skala_space_around_assignment_op", "false"),
            ("skala_space_around_relational_op", "false"),
            ("skala_space_within_parentheses", "true"),
            ("skala_space_between_method_call_parameter_list_parentheses", "true"),
            ("skala_space_after_comma", "false")
        );

    /// <summary>
    ///     ⚠ A hole the author broke keeps its breaks and the indentation after them; only the gaps on one
    ///     line are respaced (`deltaValue )}` at the within key's <c>true</c>).
    /// </summary>
    [Fact]
    public void ABrokenHole_KeepsItsBreaks_AndRespacesTheRest() {
        const string source =
            "class C {\n    int M(int a, int b) => a;\n\n    void T(int f) {\n        var s = $\"{M(\n            f,\n            f)}\";\n    }\n}\n";
        var once = FormatWith(source, ("skala_space_within_parentheses", "true"), ("skala_space_between_method_call_parameter_list_parentheses", "true"));
        Assert.Contains("var s = $\"{M(\n            f,\n            f )}\";\n", once, StringComparison.Ordinal);
    }

    /// <summary>
    ///     ⚠ The oracle never breaks inside a hole: one past the margin moves the whole string to a
    ///     continuation line and leaves it long.
    /// </summary>
    [Fact]
    public void ALongHole_IsNotBrokenInside() {
        var hole = string.Join(" + ", Enumerable.Repeat("f", 60));
        var once = FormatWith(Body("var s = $\"{" + hole + "}\";"));
        Assert.Contains("$\"{" + hole + "}\";", once, StringComparison.Ordinal);
    }
}
