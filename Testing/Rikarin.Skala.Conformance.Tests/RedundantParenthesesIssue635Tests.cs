using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Analysis.Loading;
using Rikarin.Skala.Core.Configuration;
using Rikarin.Skala.Formatting.CSharp;
using Rikarin.Skala.Formatting.CSharp.Arrangement;
using Rikarin.Skala.Options;

namespace Rikarin.Skala.Conformance.Tests;

/// <summary>
///     <c>SK0209</c> on a doubled pair of parentheses (#635): <c>-((x - 1f)) * w</c> became
///     <c>-x - 1f * w</c>, because each pair was proved redundant against the tree in which the other
///     one was still standing.
/// </summary>
/// <remarks>
///     ⚠ The rewrite compiles, so the re-bind (<c>SK9098</c>) cannot catch it, and under
///     <c>--load=none</c> nothing re-binds at all: every row here is also run through the rule alone,
///     without a compilation.
/// </remarks>
public sealed class RedundantParenthesesIssue635Tests {
    const string Prelude = """
                           using System;
                           using System.Threading.Tasks;

                           namespace P;

                           public class Probe {

                           """;

    /// <summary>
    ///     The doubled pair loses its outer half and keeps the one the parse needs.
    /// </summary>
    [Theory]
    // The issue's three.
    [InlineData("public static float A(float x, float w) => -((x - 1f)) * w;", "=> -(x - 1f) * w;")]
    [InlineData("public static float B(float x, float w) => -(((x * 2f) - 1f)) * w;", "=> -(x * 2f - 1f) * w;")]
    [InlineData("public static bool D(bool a, bool b) => !((a && b)) || a;", "=> !(a && b) || a;")]
    // Three pairs, and every other unary operator.
    [InlineData("public static float E(float x) => -(((x - 1f)));", "=> -(x - 1f);")]
    [InlineData("public static int F(int a, int b) => ~((a + b));", "=> ~(a + b);")]
    [InlineData("public static int G(int a, int b) => +((a - b)) * 2;", "=> +(a - b) * 2;")]
    [InlineData("public static Index H(int i) => ^((i + 1));", "=> ^(i + 1);")]
    [InlineData("public static bool I(object o, bool b) => !((o is string)) && b;", "=> !(o is string) && b;")]
    [InlineData("public static long J(int a, int b) => (long)((a + b));", "=> (long)(a + b);")]
    // A postfix position: member access, element access, invocation, `!`, `?.`.
    [InlineData("public static string K(int a, int b) => ((a + b)).ToString();", "=> (a + b).ToString();")]
    [InlineData("public static char L(string? s, string t) => ((s ?? t))[0];", "=> (s ?? t)[0];")]
    [InlineData("public static int M(Func<int>? f, Func<int> g) => ((f ?? g))();", "=> (f ?? g)();")]
    [InlineData("public static int? N(string? s, string? t) => ((s ?? t))?.Length;", "=> (s ?? t)?.Length;")]
    [InlineData("public static string O(string? s, string? t) => ((s ?? t))!;", "=> (s ?? t)!;")]
    // A range, `is`/`as`, a conditional, a binary operand, an interpolation hole, a lambda.
    [InlineData("public static int[] Q(int[] a, int i) => a[((i + 1))..];", "=> a[(i + 1)..];")]
    [InlineData("public static int? R(object o) => ((o as string))?.Length;", "=> (o as string)?.Length;")]
    [InlineData("public static int S(bool b) => ((b ? 1 : 2)) + 1;", "=> (b ? 1 : 2) + 1;")]
    [InlineData("public static int T(int a, int b, int c) => a - ((b - c));", "=> a - (b - c);")]
    [InlineData("public static int U(int a, int b, int c) => a * ((b + c));", "=> a * (b + c);")]
    [InlineData("""public static string V(bool b) => $"{((b ? 1 : 2))}";""", """=> $"{(b ? 1 : 2)}";""")]
    [InlineData("public static Func<int, int> W(Func<int, int>? f) => f ?? ((x => x));", "=> f ?? (x => x);")]
    [InlineData("public static bool X(bool a, bool b, bool c) => ((a ? b : c)) && a;", "=> (a ? b : c) && a;")]
    // ⚠ The policy half: `a & (b + 1)` keeps its pair (`resharper_parentheses_non_obvious_operations`),
    // and a doubled pair used to be judged by its inner *pair* — not a binary — and so lost both.
    [InlineData("public static int Y(int a, int b) => a & ((b + 1));", "=> a & (b + 1);")]
    [InlineData("public static bool Z(bool a, bool b, bool c) => a || ((b && c));", "=> a || (b && c);")]
    public void TheDoubledPair_KeepsOneHalf(string member, string expected) {
        Assert.Contains(expected, Arrange(member), StringComparison.Ordinal);
        Assert.Contains(expected, ArrangeWithoutSemantics(member), StringComparison.Ordinal);
    }

    /// <summary>
    ///     Should not fire beyond the outer pair: where neither pair is needed both go, and where a single
    ///     pair is needed it is left exactly as it was.
    /// </summary>
    [Theory]
    // Both halves redundant, and both removed.
    [InlineData("public static int A(int a, int b) { return ((a + b)); }", "return a + b;")]
    [InlineData("public static float B(float x) => -((x));", "=> -x;")]
    [InlineData("public static int C(int[] a, int i) => a[((i + 1))];", "=> a[i + 1];")]
    [InlineData("public static bool D(int a, int b) => ((a + b)) is 3;", "=> a + b is 3;")]
    [InlineData(
        "public static int E(int v) => v switch { 1 => ((v + 1)), _ => 0 };",
        "=> v switch { 1 => v + 1, _ => 0 };"
    )]
    [InlineData("public static int F(int a, int b, int c) => a + ((b * c));", "=> a + b * c;")]
    [InlineData(
        "public static string G(int a, int b) => ((a + b)).ToString() + (((a))).ToString();",
        "=> (a + b).ToString() + a.ToString();"
    )]
    // A single pair the parse needs is untouched.
    [InlineData("public static float H(float a, float b) => -(a - b) * 2f;", "=> -(a - b) * 2f;")]
    [InlineData("public static bool I(bool a, bool b) => !(a && b) || a;", "=> !(a && b) || a;")]
    [InlineData("public static int J(int a, int b) => ~(a + b);", "=> ~(a + b);")]
    [InlineData("public static long K(int a, int b) => (long)(a + b);", "=> (long)(a + b);")]
    [InlineData("public static string L(int a, int b) => (a + b).ToString();", "=> (a + b).ToString();")]
    [InlineData("public static int M(int a, int b, int c) => a - (b - c);", "=> a - (b - c);")]
    // The issue's own single-pair control: the inner pair goes, the outer stays.
    [InlineData("public static float N(float x, float w) => -((x * 2f) - 1f) * w;", "=> -(x * 2f - 1f) * w;")]
    [InlineData("public static int[] O(int[] a, int i) => a[(i + 1)..];", "=> a[(i + 1)..];")]
    [InlineData("public static int Q(int a, int b) => a & (b + 1);", "=> a & (b + 1);")]
    [InlineData("public static bool R(bool a, bool b, bool c) => a || (b && c);", "=> a || (b && c);")]
    [InlineData("public static int S(bool b) => (b ? 1 : 2) + 1;", "=> (b ? 1 : 2) + 1;")]
    [InlineData("""public static string T(bool b) => $"{(b ? 1 : 2)}";""", """=> $"{(b ? 1 : 2)}";""")]
    [InlineData("public static Func<int, int> U(Func<int, int>? f) => f ?? (x => x);", "=> f ?? (x => x);")]
    [InlineData("public static async Task<int> V(Task<int>? t, Task<int> u) => await (t ?? u);", "=> await (t ?? u);")]
    // ⚠ Under `await` the proof keeps both: it re-parses outside an `async` body, where `await (…)`
    // is an invocation, so nothing there is ever proved removable. Conservative, and pre-existing.
    [InlineData(
        "public static async Task<int> V2(Task<int>? t, Task<int> u) => await ((t ?? u));",
        "=> await ((t ?? u));"
    )]
    [InlineData("public static Index W(int i) => ^(i + 1);", "=> ^(i + 1);")]
    [InlineData("public static int? X(string? s, string? t) => (s ?? t)?.Length;", "=> (s ?? t)?.Length;")]
    [InlineData("public static bool Y(bool a, bool b, bool c) => (a ? b : c) && a;", "=> (a ? b : c) && a;")]
    [InlineData("public static int Z(int a, int b, int c) => a * (b + c);", "=> a * (b + c);")]
    public void WhatTheParseAllows_IsAllThatGoes(string member, string expected) {
        Assert.Contains(expected, Arrange(member), StringComparison.Ordinal);
        Assert.Contains(expected, ArrangeWithoutSemantics(member), StringComparison.Ordinal);
    }

    static string Source(string member) => Prelude + "    " + member + "\n}\n";

    static FormattingOptions Options() =>
        OptionResolver.Resolve(Path.Combine(Rikarin.Skala.Testing.Corpus.RepositoryRoot, "Probe.cs")).Options;

    /// <summary>The rule alone, with no compilation behind it — what <c>--load=none</c> gets.</summary>
    static string ArrangeWithoutSemantics(string member) {
        var tree = CSharpSyntaxTree.ParseText(
            Source(member),
            CSharpFormatter.ParseOptions,
            cancellationToken: TestContext.Current.CancellationToken
        );
        var arranged = new RedundantParenthesesRule().Apply(
            new(tree.GetRoot(TestContext.Current.CancellationToken), null, new(Options()))
        );
        return arranged.ToFullString();
    }

    static string Arrange(string member) {
        const string path = "/arrangement/Probe635.cs";
        var text = SourceText.From(Source(member));
        var tree = CSharpSyntaxTree.ParseText(text, CSharpFormatter.ParseOptions, path);
        var compilation = CSharpCompilation.Create(
            "probe635",
            [tree],
            SharedFrameworkReferences.Value,
            new(
                OutputKind.DynamicallyLinkedLibrary,
                allowUnsafe: true,
                nullableContextOptions: NullableContextOptions.Enable
            )
        );

        // The probe must compile, or a removal that breaks it would be indistinguishable from one that
        // does not.
        Assert.Empty(
            compilation.GetDiagnostics().Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
        );

        var result = Arranger.Arrange(
            path,
            text,
            new(Options()),
            compilation,
            null,
            null,
            new ArrangementFilter([ArrangeIds.RedundantParentheses], [])
        );

        Assert.NotEqual(ArrangementOutcome.Reverted, result.Outcome);
        return result.Text;
    }
}
