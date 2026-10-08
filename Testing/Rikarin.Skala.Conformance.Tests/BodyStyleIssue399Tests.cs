using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Analysis.Loading;
using Rikarin.Skala.Core.Configuration;
using Rikarin.Skala.Formatting.CSharp;
using Rikarin.Skala.Formatting.CSharp.Arrangement;
using System.Text.RegularExpressions;

namespace Rikarin.Skala.Conformance.Tests;

/// <summary>
///     <c>SK0201</c> against the oracle on what the converted expression holds (#399): a <c>switch</c>
///     expression, an anonymous function or an array initializer anywhere in it keeps the block, and so
///     does a <c>return</c> whose value is an assignment.
/// </summary>
/// <remarks>
///     ⚠ Every row is the oracle's answer. Each was asked of <c>jb cleanupcode</c> 2025.2.6 under
///     <c>SkalaCleanup</c>
///     (<c>dotnet run --project Testing/Rikarin.Skala.Testing -- ask &lt;dir&gt; --profile=SkalaCleanup</c>)
///     with the repository's <c>.editorconfig</c>, and the whole set again at
///     <c>skala_use_heuristics_for_body_style = false</c>, where every row converts. The issue asked
///     whether the criterion is a measure — multi-line, width — or a kind; it is a kind: a one-line
///     <c>() =&gt; 1</c> keeps its block while a multi-line query, a raw string and a chopped
///     argument list convert. The corpus construct
///     <c>constructs/arrangement/body-style/heuristics-expression.cs</c> pins the same rows through the
///     formatter.
///     <para>
///         ⚠ Arrangement only, through <see cref="ArrangementFilter" />: body style and the parentheses
///         rule, because one row is about the order of the two. <c>stackalloc</c> is in the construct
///         only: returning one from a method does not compile, and these probes must.
///     </para>
/// </remarks>
public sealed class BodyStyleIssue399Tests {
    const string Prelude = """
                           using System;
                           using System.Collections.Generic;
                           using System.Linq;

                           namespace P;

                           public class C {
                               int _n;
                               int? _m;
                               int[] _arr = new int[1];
                               Func<int> _f = () => 0;

                               public int Sum(int[] a) => a.Length;

                           """;

    /// <summary>Kept a block: the oracle leaves every one of these as written.</summary>
    [Theory]
    [InlineData("public int M(int v) { return v switch { 1 => 10, _ => 0 }; }")]
    [InlineData("public int M(int v) { return v switch { _ => 0 }; }")]
    [InlineData("public int M(int v) { return Math.Abs(v switch { _ => 0 }); }")]
    [InlineData("public Func<int, int> M() { return x => x + 1; }")]
    [InlineData("public Func<int, int> M() { return x => { return x + 1; }; }")]
    [InlineData("public Func<int> M() { return () => 1; }")]
    [InlineData("public Func<int, int> M() { return delegate(int x) { return x; }; }")]
    [InlineData("public int M(List<int> a) { return a.Count(x => x > 1); }")]
    [InlineData("public object M() { return new { F = (Func<int>)(() => 1) }; }")]
    [InlineData("public object M() { return new Lazy<int>(() => 1); }")]
    [InlineData("public int[] M() { return new[] { 1, 2 }; }")]
    [InlineData("public int[] M() { return new int[] { 1, 2 }; }")]
    [InlineData("public int[,] M() { return new int[,] { { 1 } }; }")]
    [InlineData("public int M() { return Sum(new[] { 1, 2 }); }")]
    [InlineData("public int M() { return new[] { 1 }.Length; }")]
    [InlineData("public int M(int a) { return _n = a; }")]
    [InlineData("public int M(int a) { return _n += a; }")]
    [InlineData("public int? M() { return _m ??= 1; }")]
    [InlineData("public int P { get { return _n switch { _ => 0 }; } }")]
    [InlineData("public Func<int> P { get { return () => 1; } }")]
    [InlineData("public int P { get { return _n = 1; } }")]
    [InlineData("public int[] P { get { return new[] { 1 }; } }")]
    [InlineData("public int this[int i] { get { return i switch { _ => 0 }; } }")]
    [InlineData("public static C operator +(C a, C b) { return a._n switch { _ => a }; }")]
    [InlineData("public static implicit operator Func<int>(C c) { return () => c._n; }")]
    public void TheBlock_IsKept(string member) {
        var arranged = Arrange(member);
        Assert.Contains(member, arranged, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Kept a block, and the parentheses go: the switch is what keeps it, not where it sits.
    /// </summary>
    [Fact]
    public void ASwitchInsideABinary_KeepsTheBlockAndLosesItsParentheses() {
        var arranged = Arrange("public int M(int v) { return 1 + (v switch { _ => 0 }); }");
        Assert.Contains("public int M(int v) { return 1 + v switch { _ => 0 }; }", arranged, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Kept a block, on an accessor whose sibling converts — the sibling is the canary that the
    ///     accessor path ran.
    /// </summary>
    [Theory]
    [InlineData("set { _n = value switch { _ => 0 }; }")]
    [InlineData("set { _arr = new[] { value }; }")]
    [InlineData("set { _f = () => value; }")]
    [InlineData("init { _f = () => value; }")]
    public void TheAccessorBlock_IsKept(string accessor) {
        var arranged = Arrange("public int P { get { return _n; } " + accessor + " }");
        Assert.Contains("get => _n;", arranged, StringComparison.Ordinal);
        Assert.Contains(accessor, arranged, StringComparison.Ordinal);
    }

    /// <summary>A local function asks the same question.</summary>
    [Fact]
    public void ALocalFunction_KeepsItsBlock() {
        var arranged = Arrange(
            "public int M() { return Inner(1) + Plain(1); "
            + "int Inner(int v) { return v switch { _ => 0 }; } int Plain(int v) { return v; } }"
        );

        Assert.Contains("int Inner(int v) { return v switch { _ => 0 }; }", arranged, StringComparison.Ordinal);
        Assert.Contains("int Plain(int v) => v;", arranged, StringComparison.Ordinal);
    }

    /// <summary>Converted: the oracle writes each of these as an expression body.</summary>
    [Theory]
    [InlineData("public int M(int a, int b) { return a + b; }", "public int M(int a, int b) => a + b;")]
    [InlineData("public int M(bool b) { return b ? 1 : 2; }", "public int M(bool b) => b ? 1 : 2;")]
    [InlineData("public int M(int a) { return Math.Abs(_n = a); }", "public int M(int a) => Math.Abs(_n = a);")]
    [InlineData("public int M(int a) { return (_n = a) + 1; }", "public int M(int a) => (_n = a) + 1;")]
    [InlineData(
        "public int M(bool b, int a) { return b ? _n = a : 0; }",
        "public int M(bool b, int a) => b ? _n = a : 0;"
    )]
    [InlineData("public int M() { return (_m ??= 1).GetHashCode(); }", "public int M() => (_m ??= 1).GetHashCode();")]
    [InlineData("public int[] M() { return new int[2]; }", "public int[] M() => new int[2];")]
    [InlineData(
        "public List<int> M() { return new List<int> { 1, 2 }; }",
        "public List<int> M() => new List<int> { 1, 2 };"
    )]
    [InlineData(
        """public Exception M() { return new Exception { Source = "x" }; }""",
        """public Exception M() => new Exception { Source = "x" };"""
    )]
    [InlineData("public object M() { return new { A = 1, B = 2 }; }", "public object M() => new { A = 1, B = 2 };")]
    [InlineData("public int[] M() { return [1, 2]; }", "public int[] M() => [1, 2];")]
    [InlineData(
        "public IEnumerable<int> M(int[] a) { return from x in a where x > 1 select x; }",
        "public IEnumerable<int> M(int[] a) => from x in a where x > 1 select x;"
    )]
    [InlineData(
        "public int M(int? v) { return v ?? throw new Exception(); }",
        "public int M(int? v) => v ?? throw new Exception();"
    )]
    [InlineData("public int P { get { return _n; } }", "public int P => _n;")]
    public void TheBody_IsConverted(string member, string expected) {
        var arranged = Arrange(member);
        Assert.Contains(expected, arranged, StringComparison.Ordinal);
    }

    /// <summary>
    ///     ⚠ The test is on the value as written: <c>return (_n = a);</c> converts and loses its
    ///     parentheses afterwards, while <c>return _n = a;</c> keeps its block. Body style used to run
    ///     after the parentheses rule and saw the two as one tree.
    /// </summary>
    [Fact]
    public void AParenthesizedAssignment_ConvertsAndLosesItsParentheses() {
        var arranged = Arrange("public int M(int a) { return (_n = a); }");
        Assert.Contains("public int M(int a) => _n = a;", arranged, StringComparison.Ordinal);
    }

    /// <summary>
    ///     An accessor that is already an arrow collapses onto its owner whatever it holds: the
    ///     heuristic is about turning a block into one.
    /// </summary>
    [Theory]
    [InlineData("public Func<int> P { get => () => 1; }", "public Func<int> P => () => 1;")]
    [InlineData("public int P { get => _n switch { _ => 0 }; }", "public int P => _n switch { _ => 0 };")]
    [InlineData("public int P { get => _n = 1; }", "public int P => _n = 1;")]
    public void AnArrowAccessor_CollapsesOntoItsOwner(string member, string expected) {
        var arranged = Arrange(member);
        Assert.Contains(expected, arranged, StringComparison.Ordinal);
    }

    /// <summary>At <c>skala_use_heuristics_for_body_style = false</c> every one of the kept rows converts.</summary>
    [Theory]
    [InlineData("public int M(int v) { return v switch { _ => 0 }; }", "public int M(int v) => v switch { _ => 0 };")]
    [InlineData("public Func<int> M() { return () => 1; }", "public Func<int> M() => () => 1;")]
    [InlineData("public int[] M() { return new[] { 1, 2 }; }", "public int[] M() => new[] { 1, 2 };")]
    [InlineData("public int M(int a) { return _n = a; }", "public int M(int a) => _n = a;")]
    public void WithoutTheHeuristic_TheBodyIsConverted(string member, string expected) {
        var arranged = Arrange(
            member,
            [new KeyValuePair<string, string>("skala_use_heuristics_for_body_style", "false")]
        );
        Assert.Contains(expected, arranged, StringComparison.Ordinal);
    }

    /// <summary>
    ///     #463: one <c>//</c> comment trailing the only statement is carried behind the new semicolon,
    ///     at both values of the heuristic.
    /// </summary>
    /// <remarks>
    ///     ⚠ Measured against <c>jb cleanupcode</c> 2025.2.6 under <c>SkalaCleanup</c> at each value, and
    ///     the issue's "at <c>true</c> both engines keep the block" is refuted: the oracle converts every
    ///     row here at the export's <c>true</c> as well. The corpus construct
    ///     <c>constructs/arrangement/body-style/trailing-comment.cs</c> pins the same rows through the
    ///     formatter.
    /// </remarks>
    [Theory]
    [InlineData("true", "public int M() { return 1; // c\n }", "public int M() => 1; // c")]
    [InlineData("false", "public int M() { return 1; // c\n }", "public int M() => 1; // c")]
    [InlineData("true", "public int P { get { return _n; // c\n } }", "public int P => _n; // c")]
    [InlineData("true", "public int P { get => _n; // c\n }", "public int P => _n; // c")]
    [InlineData("false", "public int P { get => _n; // c\n }", "public int P => _n; // c")]
    [InlineData(
        "true",
        "public int P { get { return _n; } set { _n = value; // c\n } }",
        "public int P { get => _n; set => _n = value; // c"
    )]
    [InlineData("false", "public void M() { Console.WriteLine(); // c\n }", "public void M() => Console.WriteLine(); // c")]
    [InlineData("false", "public void M() { throw new Exception(); // c\n }", "public void M() => throw new Exception(); // c")]
    public void ATrailingLineComment_RidesBehindTheSemicolon(string heuristics, string member, string expected) {
        var arranged = Arrange(
            member,
            [new KeyValuePair<string, string>("skala_use_heuristics_for_body_style", heuristics)]
        );
        Assert.Contains(expected, arranged, StringComparison.Ordinal);
    }

    /// <summary>
    ///     #463's declined placements: the oracle converts every one of these too, and each needs a
    ///     placement rule body style does not have, so Skala keeps the block.
    /// </summary>
    /// <remarks>
    ///     ⚠ Pinned as Skala's own answer, and each is a divergence recorded in SK-DIV-0086: a trailing
    ///     <c>/* … */</c> (the oracle writes <c>=&gt; 3 /* c */;</c>), a comment after the closing brace or
    ///     on its own line before it (the oracle moves it to a line of its own after the member), a
    ///     second trailing comment, and a comment above the statement (the oracle writes it between the
    ///     <c>=&gt;</c> and the value — at the export's <c>true</c> as well, which the heuristic's "no
    ///     comment" condition was believed to rule out).
    /// </remarks>
    [Theory]
    [InlineData("public int M() { return 3; /* c */ }")]
    [InlineData("public int M() { return 3; // c\n } // d\n")]
    [InlineData("public int M() { return 3; // c\n // d\n }")]
    [InlineData("public int M() { // c\n return 3; }")]
    [InlineData("public int M() { return /* c */ 3; }")]
    public void AnyOtherComment_KeepsTheBlock(string member) {
        foreach (var heuristics in (string[])["true", "false"]) {
            var arranged = Arrange(
                member,
                [new KeyValuePair<string, string>("skala_use_heuristics_for_body_style", heuristics)]
            );
            Assert.Contains("public int M() { ", arranged, StringComparison.Ordinal);
        }
    }

    static string Arrange(string member, IReadOnlyList<KeyValuePair<string, string>>? overrides = null) {
        const string path = "/arrangement/Probe399.cs";
        var source = Prelude + "    " + member + "\n}\n";
        var text = SourceText.From(source);
        var tree = CSharpSyntaxTree.ParseText(text, CSharpFormatter.ParseOptions, path);
        var compilation = CSharpCompilation.Create(
            "probe399",
            [tree],
            SharedFrameworkReferences.Value,
            new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                allowUnsafe: true,
                nullableContextOptions: NullableContextOptions.Enable
            )
        );

        // ⚠ The probe must compile, or a conversion that breaks it would be indistinguishable from one
        // that does not: the safety layer compares diagnostics before and after.
        Assert.Empty(
            compilation.GetDiagnostics().Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
        );

        var options = OptionResolver.Resolve(
            Path.Combine(Rikarin.Skala.Testing.Corpus.RepositoryRoot, "Probe.cs"),
            overrides
        ).Options;

        var result = Arranger.Arrange(
            path,
            text,
            new ArrangementOptions(options),
            compilation,
            null,
            null,
            new ArrangementFilter([ArrangeIds.BodyStyle, ArrangeIds.RedundantParentheses], [])
        );

        Assert.NotEqual(ArrangementOutcome.Reverted, result.Outcome);

        // ⚠ Arrangement emits one space each side of the `=>` and leaves the rest to the formatter, so
        // the unformatted text has `)  =>` where the brace's leading space was. Runs of whitespace are
        // one space for these assertions; what the formatter makes of it is the corpus construct's job.
        return Regex.Replace(result.Text, @"\s+", " ");
    }
}
