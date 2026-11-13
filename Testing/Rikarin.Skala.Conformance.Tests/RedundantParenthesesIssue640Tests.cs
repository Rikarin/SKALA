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
///     <c>SK0209</c> where nothing separates a parenthesis from the token beside it (#640):
///     <c>return(a + 1);</c> arranged to <c>returna + 1;</c>.
/// </summary>
/// <remarks>
///     ⚠ The proof re-parses the outermost enclosing <em>expression</em>, and a statement keyword lies
///     outside it, so it never saw <c>return</c> and <c>a</c> lex as one identifier. With a compilation
///     loaded the re-bind (<c>SK9098</c>) reverted the file; under <c>--load=none</c> nothing re-binds
///     and the broken text was written. Every row is therefore asked of the rule alone as well as of the
///     whole arranger over a compiling probe.
/// </remarks>
public sealed class RedundantParenthesesIssue640Tests {
    const string Prelude = """
                           using System;
                           using System.Collections.Generic;
                           using System.Threading.Tasks;

                           namespace P;

                           public class Probe {
                               public static int In(in int v) => v;

                           """;

    /// <summary>The parentheses go, and one space keeps the two tokens either side apart.</summary>
    [Theory]
    // The issue's statement keywords, which lie outside the expression the proof re-parses.
    [InlineData("public static int A(int a) { return(a + 1); }", "return a + 1;")]
    [InlineData("public static string B(int a) { return(a).ToString(); }", "return a.ToString();")]
    [InlineData("public static int C(int[] a) { return(a[0]); }", "return a[0];")]
    [InlineData("public static void D(Exception e) { throw(e); }", "throw e;")]
    [InlineData("public static IEnumerable<int> E(int a) { yield return(a); }", "yield return a;")]
    [InlineData(
        "public static int F(int a) { switch (a) { case 1: return 1; case 2: goto case(1); default: return 0; } }",
        "goto case 1;"
    )]
    [InlineData("public static void G(bool b, Action a) { if (b) { } else(a).Invoke(); }", "else a.Invoke();")]
    // ⚠ These three looked unreachable end to end: `skala arrange` reported SK9010, "the file does not
    // parse", on input Roslyn parses. It was this bug — the pipeline arranges, then formats, and the
    // formatter was handed `inxs`, `case1:` and `wheni > 0:` and blamed the file for them.
    [InlineData("public static void O(int[] xs) { foreach (var x in(xs)) { } }", "foreach (var x in xs)")]
    [InlineData(
        "public static int Q(int a) { switch (a) { case(1): return 1; default: return 0; } }",
        "case 1: return 1;"
    )]
    [InlineData(
        "public static bool R(int a) { switch (a) { case int i when(i > 0): return true; default: return false; } }",
        "case int i when i > 0: return true;"
    )]
    // ⚠ `ref` is where `Outermost` deliberately stops climbing, so it is outside the proof too.
    [InlineData("public static ref int H(int[] a) { return ref(a[0]); }", "return ref a[0];")]
    // The right-hand side: a word after the closing parenthesis.
    [InlineData("public static bool I(object o) { return(o)is int; }", "return o is int;")]
    [InlineData("public static string? J(object o) => (o)as string;", "=> o as string;")]
    // ⚠ Inside the expression the re-parse used to see the join and keep the pair; with the space it
    // proves, so the answer no longer depends on whether the input carried whitespace.
    [InlineData("public static int K(int a, int b) => a-(-b);", "=> a- -b;")]
    [InlineData("public static int L(int a, int b) => a+(+b);", "=> a+ +b;")]
    [InlineData("public static int M(int a) => In(in(a));", "=> In(in a);")]
    [InlineData("public static int N(int a) => a switch { var v when(v > 0) => 1, _ => 0 };", "when v > 0 =>")]
    public void TheJoinedTokens_AreKeptApart(string member, string expected) {
        Assert.Contains(expected, Arrange(member), StringComparison.Ordinal);
        Assert.Contains(expected, ArrangeWithoutSemantics(member), StringComparison.Ordinal);
    }

    /// <summary>
    ///     Should not fire: no space where the tokens do not join, none added beside existing trivia, and
    ///     the parentheses the parse needs left as they were.
    /// </summary>
    [Theory]
    // Already separated: exactly the one space that was there.
    [InlineData("public static int A(int a) { return (a + 1); }", "return a + 1;")]
    [InlineData("public static int B(int a) { return /* c */(a); }", "return /* c */a;")]
    [InlineData("public static int C(int a) { return/* c */(a); }", "return/* c */a;")]
    // Tokens that do not join get nothing: arrangement is not the formatter.
    [InlineData("public static int D(int a) { return(-a); }", "return-a;")]
    [InlineData("public static int E(int a) { return(a)+1; }", "return a+1;")]
    [InlineData("public static int F(int a, int b) => a-(b);", "=> a-b;")]
    [InlineData("public static int G(int a) => In((a));", "=> In(a);")]
    [InlineData("public static Func<int, int> H() => x=>(x);", "=> x=>x;")]
    [InlineData("""public static string I(int a) => $"{(a)}";""", """=> $"{a}";""")]
    [InlineData("public static int[] J(int a) => new[]{(a)};", "=> new[]{a};")]
    [InlineData("public static string K() => (1).ToString();", "=> 1.ToString();")]
    // The parse needs these, so the space question never arises.
    [InlineData("public static int L(int a, int b, int c) { return(a-(b-c)); }", "return a-(b-c);")]
    [InlineData("public static int M(bool b) { return(b ? 1 : 2)+1; }", "return(b ? 1 : 2)+1;")]
    [InlineData("public static long N(int a, int b) { return(long)(a+b); }", "return(long)(a+b);")]
    public void TheSeparatorIsAddedOnlyWhereTheTokensJoin(string member, string expected) {
        Assert.Contains(expected, Arrange(member), StringComparison.Ordinal);
        Assert.Contains(expected, ArrangeWithoutSemantics(member), StringComparison.Ordinal);
    }

    /// <summary>
    ///     ⚠ The whole of what <c>skala arrange --load=none --include SK0209</c> runs: arrangement and
    ///     formatting to a fixed point, with no compilation to re-bind against.
    /// </summary>
    /// <remarks>
    ///     The first row is the issue's repro, which wrote <c>returna + 1;</c> to disk. The second
    ///     reported <c>SK9010</c> on a file that parses, because the formatter was handed the arranged
    ///     <c>inxs</c>.
    /// </remarks>
    [Theory]
    [InlineData("public static int A(int a) { return(a + 1); }", "return a + 1;")]
    [InlineData("public static void B(int[] xs) { foreach (var x in(xs)) { } }", "foreach (var x in xs) { }")]
    public void ThePipeline_WritesTheSeparatedTokens(string member, string expected) {
        var result = ArrangementPipeline.Run(
            "/arrangement/Probe640.cs",
            SourceText.From(Source(member)),
            new(Options()),
            new(Options(), ArrangementScope.Syntactic),
            filter: new ArrangementFilter([ArrangeIds.RedundantParentheses], []),
            cancellation: TestContext.Current.CancellationToken
        );

        Assert.Empty(result.Diagnostics);
        Assert.Contains(expected, result.Text, StringComparison.Ordinal);
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

        // ⚠ The arranged text must still parse — the corruption this file exists for did not.
        var output = arranged.ToFullString();
        Assert.False(CSharpSyntaxTree.ParseText(output, CSharpFormatter.ParseOptions).GetRoot().ContainsDiagnostics);
        return output;
    }

    static string Arrange(string member) {
        const string path = "/arrangement/Probe640.cs";
        var text = SourceText.From(Source(member));
        var tree = CSharpSyntaxTree.ParseText(text, CSharpFormatter.ParseOptions, path);
        var compilation = CSharpCompilation.Create(
            "probe640",
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
            filter: new ArrangementFilter([ArrangeIds.RedundantParentheses], [])
        );

        // ⚠ Before #640 the re-bind caught the join and reverted the whole file, which is safe and is
        // also the rule doing nothing; a revert here is a failure.
        Assert.NotEqual(ArrangementOutcome.Reverted, result.Outcome);
        return result.Text;
    }
}
