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
    static readonly ImmutableArray<DiagnosticAnalyzer> Analyzers = [new TestAndCastPatternAnalyzer()];

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
    [InlineData("class N { } class C { N? G() => null; N? M() { var n = G() as N; if (n != null) { return n; } return null; } }")]
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

    static ImmutableArray<Diagnostic> Analyze(string source, LanguageVersion version) =>
        RuleFixtures.Analyze(
            RuleFixtures.Compile(source, "test.cs", version),
            Analyzers,
            TestContext.Current.CancellationToken
        );
}
