using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Analysis.Loading;
using Rikarin.Skala.Core.Configuration;
using Rikarin.Skala.Formatting.CSharp;
using Rikarin.Skala.Formatting.CSharp.Arrangement;

namespace Rikarin.Skala.Conformance.Tests;

/// <summary>
/// The rules the oracle has no opinion about, pinned by hand.
/// </summary>
/// <remarks>
/// ⚠ SK-DIV-0013. <c>jb cleanupcode</c> 2025.2.6 performs none of <c>is not null</c>,
/// <c>string.Empty</c> ⇒ <c>""</c>, or redundant-brace removal, under any profile shape and with the
/// inspections raised to <c>warning</c> — the sweep is in <c>docs/oracle-cleanup-profile.md</c>. The
/// export configures all three and doc 06 lists all three, so Skala performs them; but an oracle
/// that never moves cannot pin them, and pretending otherwise would score every correct rewrite as
/// a divergence. These are the fixtures that stand in for it.
/// <para>
/// ⚠ The <c>operator ==</c> case is the reason this file is not a formality. <c>a != null</c> and
/// <c>a is not null</c> are different expressions when the operand's type overloads <c>==</c>: the
/// first calls the user's operator, the second is a reference comparison the language performs. The
/// rewritten code still compiles, so no diagnostic appears and layer 2 cannot see it; and no
/// identifier changes meaning, so layer 3 cannot either. Only the rule's own precondition stops it,
/// which makes this test the only thing standing between the tool and a silent behaviour change.
/// </para>
/// </remarks>
public sealed class ArrangementRuleTests {
    static string Arrange(string source, bool aggressive = false, string? only = null) {
        const string path = "/arrangement/Probe.cs";
        var text = SourceText.From(source);
        var tree = CSharpSyntaxTree.ParseText(text, CSharpFormatter.ParseOptions, path);
        var compilation = CSharpCompilation.Create(
            "probe",
            [tree],
            SharedFrameworkReferences.Value,
            new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                allowUnsafe: true,
                nullableContextOptions: NullableContextOptions.Enable
            )
        );

        var options = OptionResolver.Resolve(
            Path.Combine(Rikarin.Skala.Testing.Corpus.RepositoryRoot, "Probe.cs")
        ).Options;

        var result = Arranger.Arrange(
            path,
            text,
            new ArrangementOptions(options, ArrangementScope.Full, aggressive),
            compilation,
            null,
            null,
            only is null ? ArrangementFilter.All : new ArrangementFilter([only], [])
        );

        Assert.NotEqual(ArrangementOutcome.Reverted, result.Outcome);
        return result.Text;
    }

    [Fact]
    public void IsNotNull_RewritesWhenTheOperandHasNoEqualityOperator() {
        var output = Arrange(
            """
            namespace P;
            public class Plain { public int V; }
            public class C {
                public bool M(Plain? p) { return p != null; }
                public bool N(Plain? p) { return p == null; }
                public bool R(Plain? p) { return null != p; }
            }
            """,
            only: ArrangeIds.NullCheckingPattern
        );

        Assert.Contains("p is not null", output, StringComparison.Ordinal);
        Assert.Contains("p is null", output, StringComparison.Ordinal);
        Assert.DoesNotContain("!= null", output, StringComparison.Ordinal);
    }

    /// <summary>⚠ The divergence doc 06 requires Skala to keep.</summary>
    [Fact]
    public void IsNotNull_RefusesWhenTheOperandTypeDeclaresAnEqualityOperator() {
        const string source = """
                              namespace P;
                              public class Boxed {
                                  public static bool operator ==(Boxed? a, Boxed? b) => ReferenceEquals(a, b);
                                  public static bool operator !=(Boxed? a, Boxed? b) => !ReferenceEquals(a, b);
                                  public override bool Equals(object? o) => false;
                                  public override int GetHashCode() => 0;
                              }
                              public class C {
                                  public bool M(Boxed? b) { return b != null; }
                              }
                              """;

        Assert.Contains("b != null", Arrange(source, only: ArrangeIds.NullCheckingPattern), StringComparison.Ordinal);
    }

    /// <summary>⚠ An operator inherited from a base class applies to a derived operand.</summary>
    [Fact]
    public void IsNotNull_RefusesThroughABaseClassOperator() {
        const string source = """
                              namespace P;
                              public class Boxed {
                                  public static bool operator ==(Boxed? a, Boxed? b) => ReferenceEquals(a, b);
                                  public static bool operator !=(Boxed? a, Boxed? b) => !ReferenceEquals(a, b);
                                  public override bool Equals(object? o) => false;
                                  public override int GetHashCode() => 0;
                              }
                              public class Derived : Boxed { }
                              public class C {
                                  public bool M(Derived? d) { return d != null; }
                              }
                              """;

        Assert.Contains("d != null", Arrange(source, only: ArrangeIds.NullCheckingPattern), StringComparison.Ordinal);
    }

    /// <summary>
    /// ⚠ <c>string</c> overloads <c>==</c> and the pattern form matches it, so the rewrite is safe —
    /// a naive "does the type declare operator ==" check would refuse every string null check in the
    /// corpus.
    /// </summary>
    [Fact]
    public void IsNotNull_RewritesForString() {
        var output = Arrange(
            """
            namespace P;
            public class C { public bool M(string? s) { return s != null; } }
            """,
            only: ArrangeIds.NullCheckingPattern
        );

        Assert.Contains("s is not null", output, StringComparison.Ordinal);
    }

    [Fact]
    public void EmptyString_BecomesTheLiteral() {
        var output = Arrange(
            """
            namespace P;
            public class C {
                public string F = string.Empty;
                public string M() { return System.String.Empty; }
            }
            """,
            only: ArrangeIds.EmptyString
        );

        Assert.DoesNotContain(".Empty", output, StringComparison.Ordinal);
        Assert.Contains("\"\"", output, StringComparison.Ordinal);
    }

    [Fact]
    public void RedundantBraces_AreRemovedOnlyWhenNothingIsDeclaredInside() {
        var output = Arrange(
            """
            namespace P;
            public class C {
                public void M(int a) {
                    {
                        System.Console.WriteLine(a);
                    }
                    {
                        int scoped = a;
                        System.Console.WriteLine(scoped);
                    }
                }
            }
            """,
            only: ArrangeIds.RedundantBraces
        );

        // The declaring block keeps its braces; the other loses them.
        Assert.Contains("int scoped", output, StringComparison.Ordinal);
        Assert.Equal(1, CountBareBlocks(output));
    }

    [Fact]
    public void Parentheses_AreRemovedByDefault_AndOnlyWherePrecedenceAllows() {
        // ⚠ This test asserted the opposite until the gate was lifted. SK-DIV-0014 gated parenthesis
        // removal behind `--aggressive` for the first release and named the condition for revisiting
        // it; the condition is met and the gate cost 4.25 points of changed-span agreement against an
        // oracle whose own profile removes these by default.
        const string source = """
                              namespace P;
                              public class C {
                                  public int M(int a, int b, int c) { return a + (b * c); }
                                  public int N(int a, int b, int c) { return a - (b - c); }
                                  public int O(int a, int b, int c) { return a | (b & c); }
                                  public bool P(int a, int b, int c) { return (a < b) && (b < c); }
                              }
                              """;

        var arranged = Arrange(source, only: ArrangeIds.RedundantParentheses);
        Assert.Contains("a + b * c", arranged, StringComparison.Ordinal);

        // ⚠ Never on the right of a non-associative operator: `a - (b - c)` is not `a - b - c`. The
        // re-parse proof refuses it rather than a precedence table remembering to.
        Assert.Contains("a - (b - c)", arranged, StringComparison.Ordinal);

        // The bitwise family is a `parentheses_non_obvious_operations` member and keeps its own.
        Assert.Contains("a | (b & c)", arranged, StringComparison.Ordinal);

        // Relational is `never_if_unnecessary`, even as an operand of `&&`.
        Assert.Contains("a < b && b < c", arranged, StringComparison.Ordinal);
    }

    [Fact]
    public void Var_RefusesWhereTheDeclaredTypeIsNotTheInitialisersType() {
        var output = Arrange(
            """
            using System.Collections.Generic;
            namespace P;
            public class C {
                public void M() {
                    IEnumerable<int> items = new List<int>();
                    const int limit = 3;
                    System.Console.WriteLine(items.ToString() + limit);
                }
            }
            """,
            only: ArrangeIds.Var
        );

        Assert.Contains("IEnumerable<int> items", output, StringComparison.Ordinal);
        Assert.Contains("const int limit", output, StringComparison.Ordinal);
    }

    [Fact]
    public void SyntacticScope_RunsTheRulesThatNeedNoCompilation() {
        const string path = "/arrangement/Probe.cs";
        const string source = """
                              namespace P;
                              public class C {
                                  private int _n;
                                  public int M() { return _n; }
                              }
                              """;

        var options = OptionResolver.Resolve(
            Path.Combine(Rikarin.Skala.Testing.Corpus.RepositoryRoot, "Probe.cs")
        ).Options;

        var result = Arranger.Arrange(
            path,
            SourceText.From(source),
            new ArrangementOptions(options, ArrangementScope.Syntactic),
            cancellation: TestContext.Current.CancellationToken
        );

        // ⚠ With no compilation the body style and the redundant `private` still go, and nothing
        // that needs a symbol does. This is the contract `skala format --arrange=syntactic` gives an
        // agent on a loose file.
        Assert.Contains("=> _n;", result.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("private int _n", result.Text, StringComparison.Ordinal);
        Assert.Contains(ArrangeIds.BodyStyle, result.Applied);
        Assert.DoesNotContain(ArrangeIds.Var, result.Applied);
    }

    static int CountBareBlocks(string text) {
        var count = 0;
        foreach (var line in text.Split('\n')) {
            if (line.Trim() == "{") {
                count++;
            }
        }

        // The namespace is file-scoped and the class and method braces sit on their own owner lines,
        // so a bare `{` on a line of its own is a block statement.
        return count;
    }
}
