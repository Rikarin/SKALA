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
    static readonly ImmutableArray<DiagnosticAnalyzer> Analyzers = [
        new IndexFromEndAnalyzer(), new NameofExpressionAnalyzer()
    ];

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

    [Theory]
    [InlineData("class W { } class C { string M() => typeof(W).Name; }", "nameof(W)")]
    [InlineData("namespace N { class W { } } class C { string M() => typeof(N.W).Name; }", "nameof(N.W)")]
    [InlineData("enum E { Red } class C { string M() => E.Red.ToString(); }", "nameof(E.Red)")]
    [InlineData(
        "using System; class C { void M(int count) { throw new ArgumentOutOfRangeException(\"count\", count, null); } }",
        "nameof(count)"
    )]
    [InlineData(
        "using System; class C { void M(object o) { ArgumentNullException.ThrowIfNull(o, \"o\"); } }",
        "nameof(o)"
    )]
    [InlineData(
        "using System.ComponentModel; class C { public string T { get; set; } = \"\"; "
        + "PropertyChangedEventArgs M() => new PropertyChangedEventArgs(\"T\"); }",
        "nameof(T)"
    )]
    public void Nameof_Fires(string source, string replacement) {
        var finding = Assert.Single(Analyze(source, LanguageVersion.CSharp12));
        Assert.Equal("SK1061", finding.Id);
        Assert.Contains(replacement, finding.GetMessage(), StringComparison.Ordinal);
    }

    /// <summary>
    ///     ⚠ The refusals, which are the rule. Four of these are literals that equal an identifier and
    ///     must never follow a rename; the rest are names <c>nameof</c> would answer differently.
    /// </summary>
    [Theory]
    // A bare literal that matches a member, in a position that says nothing about its meaning.
    [InlineData("class C { public int Count { get; set; } string M() => \"Count\"; }")]
    // The same literal as a dictionary key: a wire format, not a name.
    [InlineData(
        "using System.Collections.Generic; class C { public int Count { get; set; } "
        + "Dictionary<string, object> M() => new() { [\"Count\"] = Count }; }"
    )]
    // `typeof(List<int>).Name` is "List`1".
    [InlineData("using System.Collections.Generic; class C { string M() => typeof(List<int>).Name; }")]
    // `nameof(int)` does not compile.
    [InlineData("class C { string M() => typeof(int).Name; }")]
    // The run-time argument's name, not "T".
    [InlineData("class C<T> { string M() => typeof(T).Name; }")]
    // `nameof(Text)` is "Text" and `typeof(Text).Name` is "String".
    [InlineData("using Text = System.String; class C { string M() => typeof(Text).Name; }")]
    // An array's metadata name carries the brackets.
    [InlineData("class W { } class C { string M() => typeof(W[]).Name; }")]
    // ⚠ `Enum.ToString` answers with the first member declared with the value.
    [InlineData("enum E { Done = 1, Finished = 1 } class C { string M() => E.Finished.ToString(); }")]
    // A variable's `ToString`, which is a value and not a name.
    [InlineData("enum E { Red } class C { string M(E e) => e.ToString(); }")]
    // SK2017's shape: the literal names no parameter.
    [InlineData("using System; class C { void M(int count) { throw new ArgumentOutOfRangeException(\"size\"); } }")]
    // The message argument is prose, not an identifier, even when it happens to be one.
    [InlineData("using System; class C { void M(int count) { throw new ArgumentException(\"count\"); } }")]
    // No such property on this type: `nameof` written here would not bind.
    [InlineData(
        "using System.ComponentModel; class C { PropertyChangedEventArgs M() => new PropertyChangedEventArgs(\"V\"); }"
    )]
    // The empty name means "all properties changed".
    [InlineData(
        "using System.ComponentModel; class C { public string T { get; set; } = \"\"; "
        + "PropertyChangedEventArgs M() => new PropertyChangedEventArgs(\"\"); }"
    )]
    public void Nameof_DeclinesTheNearestMiss(string source) =>
        Assert.Empty(Analyze(source, LanguageVersion.CSharp12).Where(static d => d.Id == "SK1061"));

    /// <summary>
    ///     ⚠ <c>ArgumentException(string message)</c> is the counter-example that makes the
    ///     position test worth having: its single parameter is <c>message</c>, and the value people
    ///     pass it is frequently a parameter name.
    /// </summary>
    [Fact]
    public void Nameof_ReadsTheParameterAndNotTheValue() {
        const string message = "using System; class C { void M(int count) { throw new ArgumentException(\"count\"); } }";
        const string paramName =
            "using System; class C { void M(int count) { throw new ArgumentException(\"bad\", \"count\"); } }";

        Assert.Empty(Analyze(message, LanguageVersion.CSharp12).Where(static d => d.Id == "SK1061"));
        Assert.Single(Analyze(paramName, LanguageVersion.CSharp12).Where(static d => d.Id == "SK1061"));
    }

    [Fact]
    public void Nameof_RequiresCSharp6() {
        const string source = "class W { } class C { string M() => typeof(W).Name; }";

        Assert.Empty(Below(source, LanguageVersion.CSharp5).Where(static d => d.Id == "SK1061"));
        Assert.NotEmpty(Below(source, LanguageVersion.CSharp6).Where(static d => d.Id == "SK1061"));
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
