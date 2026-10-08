using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Analysis.Loading;
using Rikarin.Skala.Core.Configuration;
using Rikarin.Skala.Formatting.CSharp;
using Rikarin.Skala.Formatting.CSharp.Arrangement;

namespace Rikarin.Skala.Conformance.Tests;

/// <summary>
///     <c>SK0209</c> against the oracle on the shapes #392 asked about: a <c>switch</c> expression, a
///     lambda, a query, a conditional and an assignment inside parentheses, in every position the issue
///     named and some it did not.
/// </summary>
/// <remarks>
///     ⚠ Every row is the oracle's answer, not a reading of the grammar. Each was asked of
///     <c>jb cleanupcode</c> 2025.2.6 under <c>SkalaCleanup</c>
///     (<c>dotnet run --project Testing/Rikarin.Skala.Testing -- ask &lt;dir&gt; --profile=SkalaCleanup</c>)
///     at both values of <c>skala_parentheses_redundancy_style</c>, which agreed on every row here, with
///     <c>return (a + b);</c> and <c>a | (b &amp; c)</c> as the canaries that the probe and the override
///     were live. The rule used to keep all five kinds whole on the belief that the oracle leaves them
///     alone; it removes them wherever the parse allows.
///     <para>
///         ⚠ Arrangement only, through <see cref="ArrangementFilter" />, and never formatted. A
///         parenthesised multi-line <c>switch</c> is indented a level too deep by the formatter (#393),
///         and a fixture written from formatted output would pin that.
///     </para>
/// </remarks>
public sealed class RedundantParenthesesIssue392Tests {
    const string Prelude = """
                           using System;
                           using System.Collections.Generic;
                           using System.Linq;
                           using System.Threading.Tasks;

                           namespace P;

                           public record R(int A);

                           public class C {
                               public static int F(bool a, bool b) => 0;

                               public Exception E() => new();

                           """;

    /// <summary>Removed: the oracle drops these, and the re-parse proves the tree is unchanged.</summary>
    [Theory]
    // ⚠ The issue's four, and the binary operands on both sides — a `switch` binds tighter than every
    // binary operator, so it is a primary-like operand of all of them.
    [InlineData(
        "public int M(int v) { return (v switch { 1 => 10, _ => 0 }); }",
        "return v switch { 1 => 10, _ => 0 };"
    )]
    [InlineData(
        "public int M(int y) { var x = (y switch { 1 => 10, _ => 0 }); return x; }",
        "var x = y switch { 1 => 10, _ => 0 };"
    )]
    [InlineData("public int M(int v) => (v switch { 1 => 10, _ => 0 });", "=> v switch { 1 => 10, _ => 0 };")]
    [InlineData("public int M(int v) => (v switch { 1 => 10, _ => 0 }) + 1;", "=> v switch { 1 => 10, _ => 0 } + 1;")]
    [InlineData("public int M(int v) => 1 + (v switch { 1 => 10, _ => 0 });", "=> 1 + v switch { 1 => 10, _ => 0 };")]
    [InlineData("public int M(int v) => (v switch { 1 => 10, _ => 0 }) * 2;", "=> v switch { 1 => 10, _ => 0 } * 2;")]
    [InlineData("public int M(int v) => 2 * (v switch { 1 => 10, _ => 0 });", "=> 2 * v switch { 1 => 10, _ => 0 };")]
    [InlineData("public bool M(int v) => (v switch { 1 => 10, _ => 0 }) < 3;", "=> v switch { 1 => 10, _ => 0 } < 3;")]
    [InlineData(
        "public bool M(int v) => (v switch { 1 => 10, _ => 0 }) == 3;",
        "=> v switch { 1 => 10, _ => 0 } == 3;"
    )]
    [InlineData(
        "public bool M(int v, bool b) => b && (v switch { 1 => true, _ => false });",
        "=> b && v switch { 1 => true, _ => false };"
    )]
    [InlineData(
        """public string? M(int v, string? s) => s ?? (v switch { 1 => "a", _ => null });""",
        """=> s ?? v switch { 1 => "a", _ => null };"""
    )]
    [InlineData(
        """public string? M(int v, string s) => (v switch { 1 => "a", _ => null }) ?? s;""",
        """=> v switch { 1 => "a", _ => null } ?? s;"""
    )]
    // ⚠ Under a non-obvious operation too: `resharper_parentheses_non_obvious_operations` keeps a
    // *binary* operand of `&` or `<<`, not every operand.
    [InlineData(
        "public int M(int v, int b) => (v switch { 1 => 10, _ => 0 }) & b;",
        "=> v switch { 1 => 10, _ => 0 } & b;"
    )]
    [InlineData(
        "public int M(int v, int b) => b << (v switch { 1 => 10, _ => 0 });",
        "=> b << v switch { 1 => 10, _ => 0 };"
    )]
    [InlineData(
        """public bool M(int v) => (v switch { 1 => (object)"a", _ => 1 }) is string;""",
        """=> v switch { 1 => (object)"a", _ => 1 } is string;"""
    )]
    [InlineData(
        """public string? M(int v) => (v switch { 1 => (object)"a", _ => 1 }) as string;""",
        """=> v switch { 1 => (object)"a", _ => 1 } as string;"""
    )]
    [InlineData(
        "public int M(int v, bool b) => b ? (v switch { 1 => 10, _ => 0 }) : 1;",
        "=> b ? v switch { 1 => 10, _ => 0 } : 1;"
    )]
    [InlineData(
        "public int M(int v, bool b) => b ? 1 : (v switch { 1 => 10, _ => 0 });",
        "=> b ? 1 : v switch { 1 => 10, _ => 0 };"
    )]
    [InlineData(
        "public int M(int v) => (v switch { 1 => true, _ => false }) ? 1 : 2;",
        "=> v switch { 1 => true, _ => false } ? 1 : 2;"
    )]
    [InlineData(
        "public Func<int, int> M() => v => (v switch { 1 => 10, _ => 0 });",
        "=> v => v switch { 1 => 10, _ => 0 };"
    )]
    [InlineData(
        "public int M(int v) => Math.Abs((v switch { 1 => 10, _ => 0 }));",
        "=> Math.Abs(v switch { 1 => 10, _ => 0 });"
    )]
    // ⚠ Interpolation holes, once declined whole: the `,` and the `:` after the expression are an
    // alignment and a format clause, and the arms' own `,` sit inside braces.
    [InlineData(
        """public string M(int v) => $"{(v switch { 1 => 10, _ => 0 })}";""",
        """=> $"{v switch { 1 => 10, _ => 0 }}";"""
    )]
    [InlineData(
        """public string M(int v) => $"{(v switch { 1 => 10, _ => 0 }),5}";""",
        """=> $"{v switch { 1 => 10, _ => 0 },5}";"""
    )]
    [InlineData(
        """public string M(int v) => $"{(v switch { 1 => 10, _ => 0 }):D2}";""",
        """=> $"{v switch { 1 => 10, _ => 0 }:D2}";"""
    )]
    [InlineData(
        "public R M(R r, int v) => r with { A = (v switch { 1 => 10, _ => 0 }) };",
        "=> r with { A = v switch { 1 => 10, _ => 0 } };"
    )]
    [InlineData(
        "public int M(int v, int w) => v switch { 1 => (w switch { 1 => 10, _ => 0 }), _ => 0 };",
        "=> v switch { 1 => w switch { 1 => 10, _ => 0 }, _ => 0 };"
    )]
    [InlineData(
        "public int M(int v, int w) => v switch { _ when (w switch { 1 => true, _ => false }) => 1, _ => 0 };",
        "_ when w switch { 1 => true, _ => false } => 1"
    )]
    [InlineData(
        "public int M(object o, int w) { switch (o) { case int when (w switch { 1 => true, _ => false }): "
        + "return 1; default: return 0; } }",
        "case int when w switch { 1 => true, _ => false }:"
    )]
    [InlineData(
        "public bool M(int v) { if ((v switch { 1 => true, _ => false })) { return true; } return false; }",
        "if (v switch { 1 => true, _ => false })"
    )]
    [InlineData(
        "public int M(int[] a, int v) => a[(v switch { 1 => 0, _ => 1 })];",
        "=> a[v switch { 1 => 0, _ => 1 }];"
    )]
    [InlineData("public void M(int v) { _ = (v switch { 1 => 10, _ => 0 }); }", "_ = v switch { 1 => 10, _ => 0 };")]
    [InlineData(
        "public IEnumerable<int> M(int v) { yield return (v switch { 1 => 10, _ => 0 }); }",
        "yield return v switch { 1 => 10, _ => 0 };"
    )]
    [InlineData(
        "public void M(int v) { throw (v switch { 1 => new Exception(), _ => new InvalidOperationException() }); }",
        "throw v switch { 1 => new Exception(), _ => new InvalidOperationException() };"
    )]
    [InlineData(
        "public object M(int v) => new List<int> { (v switch { 1 => 10, _ => 0 }) };",
        "=> new List<int> { v switch { 1 => 10, _ => 0 } };"
    )]
    [InlineData("public int[] M(int v) => [(v switch { 1 => 10, _ => 0 })];", "=> [v switch { 1 => 10, _ => 0 }];")]
    [InlineData(
        "public object M(int v) => new { A = (v switch { 1 => 10, _ => 0 }) };",
        "=> new { A = v switch { 1 => 10, _ => 0 } };"
    )]
    [InlineData(
        "public (int, int) M(int v) => ((v switch { 1 => 10, _ => 0 }), 1);",
        "=> (v switch { 1 => 10, _ => 0 }, 1);"
    )]
    // Lambdas.
    [InlineData("public Func<int> M() => (() => 1);", "=> () => 1;")]
    [InlineData("public Func<int, int> M() { return (x => x + 1); }", "return x => x + 1;")]
    [InlineData("public Func<int, Func<int, int>> M() => x => (y => x + y);", "=> x => y => x + y;")]
    [InlineData("public Func<int> M(bool b) => b ? (() => 1) : (() => 2);", "=> b ? () => 1 : () => 2;")]
    [InlineData(
        "public void M(List<int> l) => l.ForEach((x => Console.WriteLine(x)));",
        "=> l.ForEach(x => Console.WriteLine(x));"
    )]
    // Queries.
    [InlineData("public object M(int[] a) { return (from x in a select x); }", "return from x in a select x;")]
    [InlineData(
        "public IEnumerable<int> M(int[] a) => a.Concat((from x in a select x));",
        "=> a.Concat(from x in a select x);"
    )]
    [InlineData(
        "public IEnumerable<int> M(int[] a) => from x in (from y in a select y) select x;",
        "=> from x in from y in a select y select x;"
    )]
    [InlineData(
        "public IEnumerable<int> M(int[] a, int b) => from x in a where (x > b) select x;",
        "=> from x in a where x > b select x;"
    )]
    [InlineData(
        "public IEnumerable<int> M(int[] a, bool b) => b ? (from x in a select x) : a;",
        "=> b ? from x in a select x : a;"
    )]
    // Conditionals.
    [InlineData("public int M(bool b) { return (b ? 1 : 2); }", "return b ? 1 : 2;")]
    [InlineData("public int M(bool b) => Math.Abs((b ? 1 : 2));", "=> Math.Abs(b ? 1 : 2);")]
    [InlineData("public int M(bool a, bool b) => a ? 1 : (b ? 2 : 3);", "=> a ? 1 : b ? 2 : 3;")]
    [InlineData("public int M(bool a, bool b) => a ? (b ? 1 : 2) : 3;", "=> a ? b ? 1 : 2 : 3;")]
    [InlineData("public int[] M(int[][] a, bool b) => a[(b ? 0 : 1)];", "=> a[b ? 0 : 1];")]
    // Assignments.
    [InlineData("public int M(int a) { int x; return (x = a); }", "return x = a;")]
    [InlineData("public int M(int a) { int x, y; x = (y = a); return x + y; }", "x = y = a;")]
    // Non-binary operands of a non-obvious operation.
    [InlineData("public int M(int a, int b) => a & (-b);", "=> a & -b;")]
    [InlineData("public int M(int a, int b) => a << (-b);", "=> a << -b;")]
    [InlineData("public int M(int[] a, int b) => b & (a.Length);", "=> b & a.Length;")]
    [InlineData("public int M(object o, int b) => b & ((int)o);", "=> b & (int)o;")]
    [InlineData("public bool M(bool b, bool c) => b & (!c);", "=> b & !c;")]
    [InlineData("public bool M(bool b, object o) => b & (o is string);", "=> b & o is string;")]
    [InlineData("public bool M(bool b, object o) => b & (o is string s);", "=> b & o is string s;")]
    [InlineData(
        "public int M(bool? b, object o) => (b & (o as bool?)) == true ? 1 : 0;",
        "=> (b & o as bool?) == true ? 1 : 0;"
    )]
    // Other interpolation holes.
    [InlineData("""public string M(int a, int b) => $"{(a + b)}";""", """=> $"{a + b}";""")]
    [InlineData("""public string M(int a, int b) => $"{(a + b):D2}";""", """=> $"{a + b:D2}";""")]
    [InlineData("""public string M(string? s) => $"{(s ?? "x")}";""", """=> $"{s ?? "x"}";""")]
    // ⚠ A comment inside the parentheses comes out with the expression; it used to be deleted.
    [InlineData("public int M(int a, int b) { return (/* why */ a + b); }", "return /* why */ a + b;")]
    [InlineData("public int M(int a, int b) { return (a + b /* why */); }", "return a + b /* why */;")]
    // A `throw` expression's operand that is neither `switch` nor binary.
    [InlineData("public int M() => throw (E());", "=> throw E();")]
    // ⚠ Beneath a `ref`, which does not parse as an expression on its own: a proof that climbed into
    // it refused everything below. Vixen's `PolyMeshDetail`, found by the arrangement differential.
    [InlineData(
        "public void M(int[] a, int x, int z, int w) { ref var cell = ref a[x + (z * w)]; cell = 1; }",
        "ref var cell = ref a[x + z * w];"
    )]
    public void Removed(string member, string expected) {
        var arranged = Arrange(member);
        Assert.Contains(expected, arranged, StringComparison.Ordinal);
    }

    /// <summary>Kept: the oracle keeps these, and so must Skala.</summary>
    [Theory]
    // The parse needs them.
    [InlineData("""public string M(int v) => (v switch { 1 => "a", _ => "b" }).ToString();""")]
    [InlineData("""public string M(int v) => (v switch { 1 => (Func<string>)(() => "a"), _ => () => "b" })();""")]
    [InlineData("""public char M(int v) => (v switch { 1 => "a", _ => "b" })[0];""")]
    [InlineData("""public string M(int v) => (v switch { 1 => "a", _ => null })!;""")]
    [InlineData("""public int? M(int v) => (v switch { 1 => "a", _ => null })?.Length;""")]
    [InlineData("public bool M(int v) => !(v switch { 1 => true, _ => false });")]
    [InlineData("public int M(int v) => -(v switch { 1 => 10, _ => 0 });")]
    [InlineData("public async Task<int> M(int v, Task<int> t) => await (v switch { 1 => t, _ => t });")]
    [InlineData("public long M(int v) => (long)(v switch { 1 => 10, _ => 0 });")]
    [InlineData("public int[] M(int[] a, int v) => a[(v switch { 1 => 0, _ => 1 })..];")]
    [InlineData("public Func<int> M(Func<int>? f) => f ?? (() => 1);")]
    [InlineData("public Func<int, int> M(Func<int, int>? f) => f ?? (x => x);")]
    [InlineData("public IEnumerable<int> M(int[] a, IEnumerable<int>? b) => b ?? (from x in a select x);")]
    [InlineData("public IEnumerable<int> M(int[] a, IEnumerable<int> b) => (from x in a select x) ?? b;")]
    [InlineData("public int M(bool a, bool b) => (a ? b : !b) ? 1 : 2;")]
    [InlineData("public bool M(bool a, bool b) => (a ? b : !b) && b;")]
    [InlineData("public bool M(bool a, object o) => (a ? o : null) is string;")]
    [InlineData("""public string M(bool a, string? s) => s ?? (a ? "y" : "x");""")]
    [InlineData("public int M(int a) { var x = 0; var y = (x = a) + 1; return y; }")]
    [InlineData("""public string M(bool b) => $"{(b ? 1 : 2)}";""")]
    // ⚠ The parse does not need these, and the oracle keeps them anyway.
    [InlineData("public R M(R r, int v) => (v switch { 1 => r, _ => r }) with { A = 1 };")]
    [InlineData("public int M(int v) => (v switch { 1 => 10, _ => 0 }) switch { 10 => 1, _ => 0 };")]
    [InlineData(
        "public int M(int v) => throw (v switch { 1 => new Exception(), _ => new InvalidOperationException() });"
    )]
    [InlineData("public int M(Exception? e) => throw (e ?? new Exception());")]
    // `resharper_parentheses_non_obvious_operations`: a binary operand of a bitwise operator.
    [InlineData("public bool M(bool b, int x) => b & (x == 1);")]
    [InlineData("public int M(int a, int b) => a & (b + 1);")]
    public void Kept(string member) {
        var arranged = Arrange(member);
        Assert.Contains(member, arranged, StringComparison.Ordinal);
    }

    /// <summary>
    ///     ⚠ A directive inside the parentheses declines them; removing them used to drop the
    ///     <c>#if</c> and the <c>#endif</c> with the parentheses' own trivia.
    /// </summary>
    [Fact]
    public void ADirectiveInsideTheParentheses_KeepsThem() {
        const string member = """
                              public int M(int a, int b) {
                                      return (
                              #if SKALA_PROBE
                                          a
                              #else
                                          b
                              #endif
                                      );
                                  }
                              """;

        var arranged = Arrange(member);
        Assert.Contains(member, arranged, StringComparison.Ordinal);
    }

    /// <summary>
    ///     ⚠ The proof re-parses what the removal can reach, not the parenthesised expression alone.
    /// </summary>
    /// <remarks>
    ///     <c>F((a &lt; b), c &gt; (x = 1))</c> is two comparisons. Without the first pair it is
    ///     <c>F(a &lt; b, c &gt; (x = 1))</c>, which Roslyn parses as a call to a generic method
    ///     <c>a&lt;b, c&gt;</c>. The proof used to stop at the argument and re-parse <c>a &lt; b</c> on
    ///     its own, which is fine. The oracle keeps that pair.
    /// </remarks>
    [Theory]
    [InlineData("F((a < b), c > (x = 1))", "(a < b)", false)]
    [InlineData("v switch { _ => (a < b), _ => c > (x = 1) }", "(a < b)", true)]
    [InlineData("$\"{(b ? 1 : 2)}\"", "(b ? 1 : 2)", false)]
    [InlineData("$\"{(a + b):D2}\"", "(a + b)", true)]
    [InlineData("M(() => (y switch { 1 => 2, _ => 3 }))", "(y switch { 1 => 2, _ => 3 })", true)]
    public void TheProof_ReparsesWhatTheRemovalCanReach(string expression, string parenthesised, bool removable) {
        var parsed = SyntaxFactory.ParseExpression(expression);
        Assert.False(parsed.ContainsDiagnostics);
        var node = parsed.DescendantNodesAndSelf()
            .OfType<ParenthesizedExpressionSyntax>()
            .First(candidate => candidate.ToString() == parenthesised);

        Assert.Equal(removable, ParenthesesRedundancy.RemovalPreservesParse(node));
    }

    /// <summary>
    ///     ⚠ The generic-invocation shape end to end, without a compilation to catch it.
    /// </summary>
    /// <remarks>
    ///     Under <c>--load=none</c> nothing re-binds the result, so the proof is the only safety there
    ///     is: before #392 this wrote <c>F(a &lt; b, c &gt; (x = 1))</c>, a different program that does
    ///     not compile.
    /// </remarks>
    [Fact]
    public void AGenericInvocation_IsNeverWritten_EvenWithoutSemantics() {
        var source = Prelude + "    public int M(int a, int b, int c, int x) => F((a < b), c > (x = 1));\n}\n";
        var tree = CSharpSyntaxTree.ParseText(
            source,
            CSharpFormatter.ParseOptions,
            cancellationToken: TestContext.Current.CancellationToken
        );
        var options = OptionResolver.Resolve(
            Path.Combine(Rikarin.Skala.Testing.Corpus.RepositoryRoot, "Probe.cs")
        ).Options;
        var arranged = new RedundantParenthesesRule().Apply(
            new(
                tree.GetRoot(TestContext.Current.CancellationToken),
                null,
                new(options)
            )
        );

        Assert.Contains("F((a < b), c > (x = 1))", arranged.ToFullString(), StringComparison.Ordinal);
    }

    static string Arrange(string member) {
        const string path = "/arrangement/Probe392.cs";
        var source = Prelude + "    " + member + "\n}\n";
        var text = SourceText.From(source);
        var tree = CSharpSyntaxTree.ParseText(text, CSharpFormatter.ParseOptions, path);
        var compilation = CSharpCompilation.Create(
            "probe392",
            [tree],
            SharedFrameworkReferences.Value,
            new(
                OutputKind.DynamicallyLinkedLibrary,
                allowUnsafe: true,
                nullableContextOptions: NullableContextOptions.Enable
            )
        );

        // ⚠ The probe must compile, or a removal that breaks it would be indistinguishable from one
        // that does not: the safety layer compares diagnostics before and after.
        Assert.Empty(
            compilation.GetDiagnostics().Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
        );

        var options = OptionResolver.Resolve(Path.Combine(Rikarin.Skala.Testing.Corpus.RepositoryRoot, "Probe.cs"))
            .Options;

        var result = Arranger.Arrange(
            path,
            text,
            new(options),
            compilation,
            null,
            null,
            new ArrangementFilter([ArrangeIds.RedundantParentheses], [])
        );

        Assert.NotEqual(ArrangementOutcome.Reverted, result.Outcome);
        return result.Text;
    }
}
