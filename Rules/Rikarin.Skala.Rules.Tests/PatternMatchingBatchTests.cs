using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Rikarin.Skala.Rules.Modernization;
using System.Collections.Immutable;

namespace Rikarin.Skala.Rules.Tests;

/// <summary>
///     <c>SK1050</c>–<c>SK1054</c>: the pattern-matching batch, shape by shape.
/// </summary>
/// <remarks>
///     ⚠ <b>One assertion per reported message, not per rule.</b> <c>SK1050</c> covers four separate
///     shapes and a fixture pair proves only that the analyzer is wired up — a rule tested for two of
///     four has two untested, and the untested ones are the ones that will over-fire. Every shape here
///     is asserted to fire on the text it is for, to stay silent on the nearest text it is not for, and
///     to produce a fix that both compiles and silences it.
/// </remarks>
public sealed class PatternMatchingBatchTests {
    static readonly ImmutableArray<DiagnosticAnalyzer> Analyzers = [
        new TestAndCastPatternAnalyzer(), new PatternSimplificationAnalyzer(), new MergedConditionalAccessAnalyzer(),
        new DiscardAssignmentAnalyzer(), new InlineOutVariableAnalyzer()
    ];

    [Theory]
    [InlineData(
        "class N { } class C { N? M(object o) { var n = o as N; if (n != null) { return n; } return null; } }",
        "conversion and the null check"
    )]
    [InlineData("class N { } class C { bool M(object o) => o as N != null; }", "safe cast is used as a type check")]
    [InlineData("class C { bool M(object o) => !(o is string); }", "negated type check")]
    [InlineData("class C { bool M(string? s) => s is object; }", "succeeds for everything non-null")]
    public void EveryShape_Fires(string source, string fragment) {
        var finding = Assert.Single(Analyze(source, LanguageVersion.CSharp12));
        Assert.Equal("SK1050", finding.Id);
        Assert.Contains(fragment, finding.GetMessage(), StringComparison.Ordinal);
    }

    /// <summary>The nearest text each shape must not touch.</summary>
    [Theory]
    // SK1015's shape: `is T` and then a cast. Never restated here.
    [InlineData("class N { } class C { object? M(object o) { if (o is N) { var n = (N)o; return n; } return null; } }")]
    // The local escapes the `if`, so a pattern variable would not be definitely assigned.
    [InlineData("class N { } class C { object? M(object o) { var n = o as N; if (n != null) { } return n; } }")]
    // The conversion operand is an invocation, so the rewrite would move a side effect.
    [InlineData(
        "class N { } class C { N? G() => null; N? M() { var n = G() as N; if (n != null) { return n; } return null; } }"
    )]
    // `object` does not convert to `string`: the test can genuinely fail.
    [InlineData("class C { bool M(object o) => o is string; }")]
    // A value type is never null, so `is not null` is a different claim.
    [InlineData("class C { bool M(object o) => o is int; }")]
    // Under another `!`, a bare `is not` would rebind to `(!o) is not string`.
    [InlineData("class C { bool M(object o) => !!(o is string); }")]
    public void EveryShape_DeclinesTheNearestMiss(string source) =>
        Assert.Empty(Analyze(source, LanguageVersion.CSharp12));

    /// <summary>
    ///     ⚠ The two shapes that emit a <c>not</c> pattern are gated at C# 9 on their own, because the
    ///     rule's declared floor is the C# 7 one shape 1 needs.
    /// </summary>
    [Theory]
    [InlineData("class C { bool M(object o) => !(o is string); }", false)]
    [InlineData("class C { bool M(string? s) => s is object; }", false)]
    [InlineData("class N { } class C { bool M(object o) => o as N != null; }", true)]
    [InlineData(
        "class N { } class C { N? M(object o) { var n = o as N; if (n != null) { return n; } return null; } }",
        true
    )]
    public void PatternProducingShapes_AreGatedIndependently(string source, bool firesOnCSharp8) {
        Assert.Empty(Analyze(source, LanguageVersion.CSharp6));
        Assert.Equal(
            firesOnCSharp8,
            Analyze(source, LanguageVersion.CSharp8).Any(static d => d.Id == "SK1050")
        );

        Assert.NotEmpty(Analyze(source, LanguageVersion.CSharp9));
    }

    /// <summary>
    ///     ⚠ <c>SK1051</c>'s inversion is a behaviour change on every type whose order is not total,
    ///     and the two ways that happens do not look alike.
    /// </summary>
    [Theory]
    // NaN is greater than nothing and less than nothing: it falls into `not (> 5)` and out of `<= 5`.
    [InlineData("class C { bool M(double d) => d is not (> 5); }", false)]
    [InlineData("class C { bool M(float f) => f is not (> 5); }", false)]
    // `null` does the same thing to a lifted comparison.
    [InlineData("class C { bool M(int? n) => n is not (> 5); }", false)]
    // The input type is the property's, and this rule does not walk into a subpattern to find it.
    [InlineData("class B { public double X { get; set; } } class C { bool M(B b) => b is { X: not (> 5) }; }", false)]
    [InlineData("class C { bool M(int i) => i is not (> 5); }", true)]
    [InlineData("class C { bool M(char c) => c is not (> 'a'); }", true)]
    [InlineData("class C { bool M(decimal d) => d is not (> 5); }", true)]
    [InlineData("enum E { A, B } class C { bool M(E e) => e is not (> E.A); }", true)]
    // The governing expression of a `switch` is an input the walk can see.
    [InlineData("class C { int M(int i) => i switch { not (> 5) => 0, _ => 1 }; }", true)]
    public void RelationalInversion_RequiresATotalOrder(string source, bool inverts) =>
        Assert.Equal(inverts, Analyze(source, LanguageVersion.CSharp12).Any(static d => d.Id == "SK1051"));

    /// <summary>Cancelling a run of `not` needs no total order, and the whole run goes at once.</summary>
    [Theory]
    [InlineData("class C { bool M(double d) => d is not not (> 5); }", "(> 5)")]
    [InlineData("class C { bool M(object o) => o is not not string; }", "string")]
    [InlineData("class C { bool M(object o) => o is not not not string; }", "not string")]
    [InlineData("class C { bool M(int i) => i is not not not > 5; }", "<= 5")]
    // ⚠ `not` binds tighter than `and`, so the parentheses have to survive the collapse.
    [InlineData("class C { bool M(int i) => i is > 0 and not not (1 or 2); }", "(1 or 2)")]
    public void ANotRun_CollapsesInOneEdit(string source, string expected) {
        var finding = Assert.Single(Analyze(source, LanguageVersion.CSharp12));
        Assert.Equal("SK1051", finding.Id);
        Assert.Contains("`" + expected + "`", finding.GetMessage(), StringComparison.Ordinal);
    }

    [Fact]
    public void PatternSimplification_RequiresCSharp9() {
        const string source = "class C { bool M(int i) => i is not not > 5; }";
        Assert.Empty(Analyze(source, LanguageVersion.CSharp8).Where(static d => d.Id == "SK1051"));
        Assert.NotEmpty(Analyze(source, LanguageVersion.CSharp9).Where(static d => d.Id == "SK1051"));
    }

    /// <summary>
    ///     <c>SK1052</c>: the four spellings of the guard, and the receiver shapes it will not touch.
    /// </summary>
    [Theory]
    [InlineData("class E { } class D { public E? R; } class C { E? M(D? d) => d != null ? d.R : null; }", "d?.R")]
    [InlineData("class E { } class D { public E? R; } class C { E? M(D? d) => d == null ? null : d.R; }", "d?.R")]
    [InlineData(
        "class E { } class D { public E? R; } class C { E? M(D? d) => d is not null ? d.R : null; }",
        "d?.R"
    )]
    [InlineData("class E { } class D { public E? R; } class C { E? M(D? d) => d is null ? null : d.R; }", "d?.R")]
    // An element access is a legal suffix; a call on the receiver itself is not.
    [InlineData(
        "using System.Collections.Generic; class C { string? M(List<string>? l) => l != null ? l[0] : null; }",
        "l?[0]"
    )]
    public void ConditionalAccess_MergesEverySpellingOfTheGuard(string source, string expected) {
        var finding = Assert.Single(Analyze(source, LanguageVersion.CSharp12));
        Assert.Equal("SK1052", finding.Id);
        Assert.Contains("`" + expected + "`", finding.GetMessage(), StringComparison.Ordinal);
    }

    [Theory]
    // `x?()` is not syntax.
    [InlineData("using System; class C { string? M(Func<string>? f) => f != null ? f() : null; }")]
    // The member is a value type, so `?.` produces `int?` by a different route.
    [InlineData("class D { public int N; } class C { int? M(D? d) => d != null ? d.N : null; }")]
    // Two evaluations of a call are not one.
    [InlineData(
        "class D { public string? N; } class C { D? G() => null; string? M() => G() != null ? G()!.N : null; }"
    )]
    // The guard and the access are about different objects.
    [InlineData("class E { } class D { public E? R; } class C { E? M(D? a, D b) => a != null ? b.R : null; }")]
    // Already a conditional access: appending the suffix would splice `d??.R`.
    [InlineData("class E { } class D { public E? R; } class C { E? M(D? d) => d != null ? d?.R : null; }")]
    public void ConditionalAccess_DeclinesTheNearestMiss(string source) =>
        Assert.Empty(Analyze(source, LanguageVersion.CSharp12).Where(static d => d.Id == "SK1052"));

    /// <summary><c>SK1053</c>: both places a name is invented for a value nobody reads.</summary>
    [Theory]
    [InlineData("class C { bool R() => true; void M() { var x = R(); } }", true)]
    [InlineData("class C { bool R(out int v) { v = 1; return true; } void M() { R(out var v); } }", true)]
    [InlineData("class C { object M() { var x = new object(); return x; } }", false)]
    // A constant initializer is dead code whose repair is deletion, not a discard.
    [InlineData("class C { void M() { var x = 5; } }", false)]
    // ⚠ `_` is a name here, so the rewrite would assign to the parameter.
    [InlineData("class C { bool R() => true; string M(string _) { var x = R(); return _; } }", false)]
    // An explicitly typed out-variable carries type information into overload resolution.
    [InlineData("class C { bool R(out int v) { v = 1; return true; } void M() { R(out int v); } }", false)]
    [InlineData(
        "using System; class H : IDisposable { public void Dispose() { } } class C { void M() { using var h = new H(); } }",
        false
    )]
    public void Discard_ReplacesOnlyAnUnreadName(string source, bool fires) =>
        Assert.Equal(fires, Analyze(source, LanguageVersion.CSharp12).Any(static d => d.Id == "SK1053"));

    [Fact]
    public void Discard_RequiresCSharp7() {
        const string source = "class C { bool R() => true; void M() { var x = R(); } }";
        Assert.Empty(Analyze(source, LanguageVersion.CSharp6).Where(static d => d.Id == "SK1053"));
        Assert.NotEmpty(Analyze(source, LanguageVersion.CSharp7).Where(static d => d.Id == "SK1053"));
    }

    /// <summary>
    ///     <c>SK1054</c>: the declared type travels verbatim, and scope decides everything else.
    /// </summary>
    [Fact]
    public void InlineOutVariable_CarriesTheWrittenTypeRatherThanVar() {
        const string source = "class C { bool T(out int v) { v = 0; return true; } "
            + "bool M() { int value; return T(out value); } }";

        var finding = Assert.Single(Analyze(source, LanguageVersion.CSharp12));
        Assert.Equal("SK1054", finding.Id);
        Assert.Contains("out int value", finding.GetMessage(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("class C { bool T(out int v) { v = 0; return true; } bool M() { int a; return T(out a); } }", true)]
    // The read is below the statement that writes it, where the expression variable may not reach.
    [InlineData("class C { bool T(out int v) { v = 0; return true; } int M() { int a; T(out a); return a; } }", false)]
    // A nested block scopes the inline declaration somewhere the local was not.
    [InlineData(
        "class C { bool T(out int v) { v = 0; return true; } void M(bool e) { int a; if (e) { T(out a); } } }",
        false
    )]
    // An initializer is a value somebody chose.
    [InlineData(
        "class C { bool T(out int v) { v = 0; return true; } bool M() { int a = 1; return T(out a); } }",
        false
    )]
    // `ref` reads as well as writes.
    [InlineData("class C { void B(ref int v) { v++; } int M() { int a = 0; B(ref a); return a; } }", false)]
    // One name in two `out` positions would be two declarations.
    [InlineData(
        "class C { void S(out int x, out int y) { x = 0; y = 0; } void M() { int a; S(out a, out a); } }",
        false
    )]
    public void InlineOutVariable_MovesOnlyWhereTheScopeSurvives(string source, bool fires) =>
        Assert.Equal(fires, Analyze(source, LanguageVersion.CSharp12).Any(static d => d.Id == "SK1054"));

    [Fact]
    public void InlineOutVariable_RequiresCSharp7() {
        const string source = "class C { bool T(out int v) { v = 0; return true; } "
            + "bool M() { int value; return T(out value); } }";

        Assert.Empty(Analyze(source, LanguageVersion.CSharp6).Where(static d => d.Id == "SK1054"));
        Assert.NotEmpty(Analyze(source, LanguageVersion.CSharp7).Where(static d => d.Id == "SK1054"));
    }

    static ImmutableArray<Diagnostic> Analyze(string source, LanguageVersion version) =>
        RuleFixtures.Analyze(
            RuleFixtures.Compile(source, "test.cs", version),
            Analyzers,
            TestContext.Current.CancellationToken
        );
}
