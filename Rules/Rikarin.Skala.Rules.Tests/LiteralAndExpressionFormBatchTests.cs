using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Rikarin.Skala.Rules.Modernization;
using System.Collections.Immutable;

namespace Rikarin.Skala.Rules.Tests;

/// <summary>
///     <c>SK1060</c>–<c>SK1064</c>: the literal and expression forms, shape by shape.
/// </summary>
/// <remarks>
///     ⚠ <b>Every rule in this batch is gated on a C# version, and two of them cover shapes whose
///     floors differ.</b> A single rule-level floor would either silence the older shape on an older
///     project or emit newer syntax into one, so the gate is asserted per shape wherever the shapes
///     disagree — not once per rule.
///     <para>
///         ⚠ <see cref="Analyze" /> refuses an <c>AD0001</c>. A crashed analyzer produces nothing,
///         which passes every negative fixture and fails only the positives; a batch that did not
///         check for it would read a crash as a well-behaved rule.
///     </para>
/// </remarks>
public sealed class LiteralAndExpressionFormBatchTests {
    static readonly ImmutableArray<DiagnosticAnalyzer> Analyzers = [new IndexFromEndAnalyzer()];

    [Theory]
    [InlineData("using System.Collections.Generic; class C { string M(List<string> x) => x[x.Count - 1]; }", "^1")]
    [InlineData("class C { byte M(byte[] b) => b[b.Length - 1]; }", "^1")]
    [InlineData("class C { char M(string s) => s[s.Length - 1]; }", "^1")]
    [InlineData("class C { byte M(byte[] b, int n) => b[b.Length - n]; }", "^n")]
    [InlineData("using System; class C { byte M(Span<byte> b) => b[b.Length - 2]; }", "^2")]
    [InlineData(
        "using System.Collections.Generic; class C { string M(IReadOnlyList<string> x) => x[x.Count - 1]; }",
        "^1"
    )]
    public void IndexFromEnd_Fires(string source, string replacement) {
        var finding = Assert.Single(Analyze(source, LanguageVersion.CSharp12));
        Assert.Equal("SK1060", finding.Id);
        Assert.Contains(replacement, finding.GetMessage(), StringComparison.Ordinal);
    }

    /// <summary>
    ///     ⚠ The type test, which is the rule. "Has a <c>Count</c>" is not it in either direction.
    /// </summary>
    [Theory]
    // A different collection on each side: the defect the rule exists to make unwritable.
    [InlineData("using System.Collections.Generic; class C { string M(List<string> a, List<string> b) => a[b.Count - 1]; }")]
    // Countable and `int`-indexed, and `^1` would read as an ordinal position it does not have.
    [InlineData("using System.Collections.Generic; class C { string M(Dictionary<int, string> d) => d[d.Count - 1]; }")]
    // The same shape on a hand-written type the language would accept and a reader would not.
    [InlineData("class T { public int Count => 0; public string this[int h] => \"\"; } class C { string M(T t) => t[t.Count - 1]; }")]
    // Evaluated twice today, once after the rewrite.
    [InlineData("using System.Collections.Generic; class C { List<string> G() => new(); string M() => G()[G().Count - 1]; }")]
    // `^0` is `Count`, which is not the last element.
    [InlineData("using System.Collections.Generic; class C { string M(List<string> x) => x[x.Count - 0]; }")]
    // `Index`'s constructor rejects a negative value; the subtraction reaches the indexer instead.
    [InlineData("using System.Collections.Generic; class C { const int B = -1; string M(List<string> x) => x[x.Count - B]; }")]
    // Not a name path, so the rule does not move it.
    [InlineData("using System.Collections.Generic; class C { int B() => 1; string M(List<string> x) => x[x.Count - B()]; }")]
    // An addition is not an index from the end.
    [InlineData("using System.Collections.Generic; class C { string M(List<string> x, int n) => x[x.Count + n]; }")]
    // A `long` offset is not an `Index` operand.
    [InlineData("using System.Collections.Generic; class C { string M(List<string> x, long n) => x[(int)(x.Count - n)]; }")]
    public void IndexFromEnd_DeclinesTheNearestMiss(string source) =>
        Assert.Empty(Analyze(source, LanguageVersion.CSharp12));

    [Fact]
    public void IndexFromEnd_RequiresCSharp8() {
        const string source = "using System.Collections.Generic; class C { string M(List<string> x) => x[x.Count - 1]; }";

        Assert.Empty(Below(source, LanguageVersion.CSharp7_3).Where(static d => d.Id == "SK1060"));
        Assert.NotEmpty(Analyze(source, LanguageVersion.CSharp8));
    }

    static ImmutableArray<Diagnostic> Analyze(string source, LanguageVersion version) {
        var compilation = RuleFixtures.Compile(source, "test.cs", version);
        Assert.DoesNotContain(
            compilation.GetDiagnostics(TestContext.Current.CancellationToken),
            static d => d.Severity == DiagnosticSeverity.Error
        );

        var diagnostics = RuleFixtures.Analyze(compilation, Analyzers, TestContext.Current.CancellationToken);
        Assert.DoesNotContain(diagnostics, static d => d.Id == "AD0001");
        return diagnostics;
    }

    /// <summary>
    ///     ⚠ The below-the-floor half, which cannot use <see cref="Analyze" />.
    /// </summary>
    /// <remarks>
    ///     <c>RuleFixtures.Compile</c> enables the nullable context unconditionally, and that is
    ///     <c>CS8630</c> below C# 8 — a compilation-option error, not a source one, and the source
    ///     still binds. Asserting "no error" here would make every floor below C# 8 untestable, so
    ///     this path asserts only that the analyzer did not crash and left the rule out.
    /// </remarks>
    static ImmutableArray<Diagnostic> Below(string source, LanguageVersion version) {
        var diagnostics = RuleFixtures.Analyze(
            RuleFixtures.Compile(source, "test.cs", version),
            Analyzers,
            TestContext.Current.CancellationToken
        );

        Assert.DoesNotContain(diagnostics, static d => d.Id == "AD0001");
        return diagnostics;
    }
}
