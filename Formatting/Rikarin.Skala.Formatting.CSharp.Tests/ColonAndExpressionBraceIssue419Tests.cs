using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Core.Configuration;

namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     #419: three keys reach further in the oracle than they did in Skala — the inheritance-clause colon
///     keys a constructor initializer's colon, the attribute colon keys a named argument's, and the
///     array-initializer brace key every expression brace.
/// </summary>
/// <remarks>
///     ⚠ Every expected string is the oracle's own answer, measured 2026-10-07 with <c>Testing ask</c>
///     under the repository's configuration with each colon and brace key flipped one at a time, every
///     gap written closed and spaced. Each row is checked on a second pass as well.
/// </remarks>
public sealed class ColonAndExpressionBraceIssue419Tests {
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

    const string Declarations = "class B {\n    public B() { }\n\n    public B(int a) { }\n}\n\n"
        + "record R(int A);\n\nclass P {\n    public int X;\n    public P Q;\n"
        + "    public System.Collections.Generic.List<int> L = new();\n}\n\n";

    static string InMethod(string statement) =>
        Declarations
        + "class D {\n    void M(int a, int b) { }\n\n"
        + $"    void T(object o, R r) {{\n        {statement}\n    }}\n}}\n";

    static void AssertFormats(string source, string expected, params (string Key, string Value)[] overrides) {
        var once = FormatWith(source, overrides);
        Assert.Contains(expected + "\n", once, StringComparison.Ordinal);
        Assert.Equal(once, FormatWith(once, overrides));
    }

    /// <summary>
    ///     A constructor initializer's colon reads the inheritance clause's keys, on both sides, for
    ///     <c>base</c> and <c>this</c> alike — the remark in <see cref="SpaceRules" /> that it is always
    ///     <c> : </c> measured the inert ctor-initializer key and never this one.
    /// </summary>
    [Theory]
    [InlineData(
        "public C() : base() { }",
        "    public C(): base() { }",
        "skala_space_before_colon_in_inheritance_clause",
        "false"
    )]
    [InlineData(
        "public C(int a):base(a) { }",
        "    public C(int a): base(a) { }",
        "skala_space_before_colon_in_inheritance_clause",
        "false"
    )]
    [InlineData(
        "public C(string s) : this() { }",
        "    public C(string s): this() { }",
        "skala_space_before_colon_in_inheritance_clause",
        "false"
    )]
    [InlineData(
        "public C() : base() { }",
        "    public C() :base() { }",
        "skala_space_after_colon_in_inheritance_clause",
        "false"
    )]
    [InlineData(
        "public C(long l):this() { }",
        "    public C(long l) :this() { }",
        "skala_space_after_colon_in_inheritance_clause",
        "false"
    )]
    [InlineData(
        "public C(int a):base(a) { }",
        "    public C(int a) : base(a) { }",
        "skala_space_before_colon_in_ctor_initializer",
        "false"
    )]
    public void AConstructorInitializersColon_ReadsTheInheritanceKeys(
        string member,
        string expected,
        string key,
        string value
    ) =>
        AssertFormats($"class C : B {{\n    public C(bool b) {{ }}\n\n    {member}\n}}\n", expected, (key, value));

    /// <summary>The base list, a primary constructor's base call and a record's move with it.</summary>
    [Theory]
    [InlineData("class C : B { }", "class C: B { }")]
    [InlineData("class C(int x) :B(x) { }", "class C(int x): B(x) { }")]
    [InlineData("record S(int A) : R(A);", "record S(int A): R(A);")]
    public void TheBaseList_MovesWithTheSameKey(string declaration, string expected) =>
        AssertFormats(
            Declarations + declaration + "\n",
            expected,
            ("skala_space_before_colon_in_inheritance_clause", "false")
        );

    /// <summary>
    ///     A named argument's colon — in a call, a tuple and an attribute — reads the attribute colon's
    ///     keys, which the export labels "other colons".
    /// </summary>
    [Theory]
    [InlineData("M(a: 1, b:2);", "M(a : 1, b : 2);", "skala_space_before_attribute_colon", "true")]
    [InlineData("var t = (a:1, b :2);", "var t = (a : 1, b : 2);", "skala_space_before_attribute_colon", "true")]
    [InlineData("M(a : 1, b: 2);", "M(a:1, b:2);", "skala_space_after_attribute_colon", "false")]
    [InlineData("var t = (a: 1, b: 2);", "var t = (a:1, b:2);", "skala_space_after_attribute_colon", "false")]
    [InlineData("M(a :1, b : 2);", "M(a: 1, b: 2);", "skala_space_before_colon_in_case", "true")]
    public void ANamedArgumentsColon_ReadsTheAttributeColonKeys(
        string statement,
        string expected,
        string key,
        string value
    ) =>
        AssertFormats(InMethod(statement), "        " + expected, (key, value));

    [Fact]
    public void AnAttributesNamedArgument_ReadsTheSameKey() =>
        AssertFormats(
            "class A {\n    [System.Obsolete(\"x\", error: false)]\n    void Q() { }\n}\n",
            "    [System.Obsolete(\"x\", error : false)]",
            ("skala_space_before_attribute_colon", "true")
        );

    /// <summary>
    ///     A subpattern's colon is no key's: the author's gap in front of it survives, a run collapses to
    ///     one, and one space follows — at every value of every colon key.
    /// </summary>
    [Theory]
    [InlineData("var v = o is P { X: 1 };", "var v = o is P { X: 1 };")]
    [InlineData("var v = o is P { X : 1 };", "var v = o is P { X : 1 };")]
    [InlineData("var v = o is P { X  :  1 };", "var v = o is P { X : 1 };")]
    [InlineData("var v = o is P {X:1};", "var v = o is P { X: 1 };")]
    [InlineData("var v = o is P { X :1 };", "var v = o is P { X : 1 };")]
    [InlineData("var v = o is P { Q.X : 1 };", "var v = o is P { Q.X : 1 };")]
    [InlineData("var v = r is (A : 1, B :_);", "var v = r is (A : 1, B : _);")]
    [InlineData("var v = r is R(A : 1, B: _);", "var v = r is R(A : 1, B: _);")]
    public void ASubpatternsColon_KeepsTheAuthorsGapInFrontOfIt(string statement, string expected) {
        AssertFormats(InMethod(statement), "        " + expected);
        AssertFormats(InMethod(statement), "        " + expected, ("skala_space_before_attribute_colon", "true"));
        AssertFormats(InMethod(statement), "        " + expected, ("skala_space_after_attribute_colon", "false"));
    }

    /// <summary>
    ///     ⚠ A joined gap reads its bit from the indentation of the line it ends on, and from nothing
    ///     before the last line break — measured at <c>keep_user_linebreaks = false</c>, where the oracle
    ///     joins: <c>X</c> / <c>: 1</c> gives <c>X: 1</c>, <c>X</c> / <c>    : 1</c> gives <c>X : 1</c>, a
    ///     trailing space before the ending counts for nothing (#436). At the defaults the break is kept;
    ///     see <c>SubpatternColonBreakIssue436Tests</c>.
    /// </summary>
    [Theory]
    [InlineData("var v = o is P { X\n: 1 };", "var v = o is P { X: 1 };")]
    [InlineData("var v = o is P { X\n            : 1 };", "var v = o is P { X : 1 };")]
    [InlineData("var v = o is P { X  \n: 1 };", "var v = o is P { X: 1 };")]
    public void AJoinedSubpatternColon_ReadsTheIndentOfTheLineItEndsOn(string statement, string expected) =>
        AssertFormats(InMethod(statement), "        " + expected, ("skala_keep_user_linebreaks", "false"));

    /// <summary>
    ///     Every expression brace reads the array-initializer key, whatever stands just inside it.
    /// </summary>
    [Theory]
    [InlineData("var v = new P { X = 1 };", "var v = new P {X = 1};")]
    [InlineData("var v = new P() { X = 1 };", "var v = new P() {X = 1};")]
    [InlineData("P v = new() { X = 1 };", "P v = new() {X = 1};")]
    [InlineData("var v = new P { Q = new P { X = 1 } };", "var v = new P {Q = new P {X = 1}};")]
    [InlineData("var v = new P { L = { 1 } };", "var v = new P {L = {1}};")]
    [InlineData(
        "var v = new System.Collections.Generic.Dictionary<int, int> { [1] = 2 };",
        "var v = new System.Collections.Generic.Dictionary<int, int> {[1] = 2};"
    )]
    [InlineData(
        "var v = new System.Collections.Generic.Dictionary<int, int> { { 1, 2 } };",
        "var v = new System.Collections.Generic.Dictionary<int, int> {{1, 2}};"
    )]
    [InlineData(
        "var v = new System.Collections.Generic.List<(int, int)> { (1, 2) };",
        "var v = new System.Collections.Generic.List<(int, int)> {(1, 2)};"
    )]
    [InlineData(
        "var v = new System.Collections.Generic.List<System.Action> { () => { } };",
        "var v = new System.Collections.Generic.List<System.Action> {() => { }};"
    )]
    [InlineData("var v = new int[][] { new[] { 1 } };", "var v = new int[][] {new[] {1}};")]
    [InlineData("var v = new { X = 1 };", "var v = new {X = 1};")]
    [InlineData("var v = r with { A = 2 };", "var v = r with {A = 2};")]
    [InlineData("var v = o is P { X: 1 };", "var v = o is P {X: 1};")]
    [InlineData("var v = o is P { Q: { X: 1 } };", "var v = o is P {Q: {X: 1}};")]
    [InlineData("var v = new int[] { 1 };", "var v = new int[] {1};")]
    public void EveryExpressionBrace_ReadsTheArrayInitializerKey(string statement, string expected) =>
        AssertFormats(
            InMethod(statement),
            "        " + expected,
            ("skala_space_within_single_line_array_initializer_braces", "false")
        );

    /// <summary>
    ///     ⚠ Only a brace's own partner makes a pair empty. Two braces of two pairs side by side read the
    ///     key of the gap they are in, and the empty-braces key moves only a pair with nothing in it.
    /// </summary>
    [Theory]
    [InlineData("var v = new P { Q = { X = 1 } };", "var v = new P { Q = { X = 1 } };")]
    [InlineData("var v = new int[][] { new[] { 1 } };", "var v = new int[][] { new[] { 1 } };")]
    [InlineData(
        "var v = new System.Collections.Generic.List<System.Action> { () => { } };",
        "var v = new System.Collections.Generic.List<System.Action> { () => {} };"
    )]
    [InlineData("var v = new P { };", "var v = new P {};")]
    [InlineData("var v = new { };", "var v = new {};")]
    [InlineData("var v = r with { };", "var v = r with {};")]
    [InlineData("var v = o is { };", "var v = o is {};")]
    public void OnlyABracesOwnPartner_MakesThePairEmpty(string statement, string expected) =>
        AssertFormats(InMethod(statement), "        " + expected, ("skala_space_within_empty_braces", "false"));
}
