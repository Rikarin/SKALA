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
///     The rules the oracle has no opinion about, pinned by hand.
/// </summary>
/// <remarks>
///     ⚠ SK-DIV-0013. <c>jb cleanupcode</c> 2025.2.6 performs none of <c>is not null</c>,
///     <c>string.Empty</c> ⇒ <c>""</c>, or redundant-brace removal, under any profile shape and with the
///     inspections raised to <c>warning</c> — the sweep is in <c>docs/oracle-cleanup-profile.md</c>. The
///     export configures all three and doc 06 lists all three, so Skala performs them; but an oracle
///     that never moves cannot pin them, and pretending otherwise would score every correct rewrite as
///     a divergence. These are the fixtures that stand in for it.
///     <para>
///         ⚠ The <c>operator ==</c> case is the reason this file is not a formality. <c>a != null</c> and
///         <c>a is not null</c> are different expressions when the operand's type overloads <c>==</c>: the
///         first calls the user's operator, the second is a reference comparison the language performs. The
///         rewritten code still compiles, so no diagnostic appears and layer 2 cannot see it; and no
///         identifier changes meaning, so layer 3 cannot either. Only the rule's own precondition stops it,
///         which makes this test the only thing standing between the tool and a silent behaviour change.
///     </para>
/// </remarks>
public sealed class ArrangementRuleTests {
    /// <param name="removeUnused">
    ///     ⚠ Supply the removable-usings set the product computes, instead of nothing. Removal takes
    ///     its answer from that set rather than from a model, so a helper that always passes
    ///     <c>null</c> exercises sorting and never removal — and a test written against it would pass
    ///     whatever the removal did.
    /// </param>
    /// <param name="overrides">
    ///     ⚠ The keys this test is <em>about</em>, pinned rather than inherited. Options are resolved
    ///     from the repository's own <c>.editorconfig</c>, so without this a test asserting a rewrite
    ///     is really asserting that Skala's house style still asks for it — and the day the house
    ///     style changes, the rule test goes red for a reason that has nothing to do with the rule.
    /// </param>
    static string Arrange(
        string source,
        string? only = null,
        bool removeUnused = false,
        IReadOnlyList<KeyValuePair<string, string>>? overrides = null
    ) {
        var result = Attempt(source, only, removeUnused, overrides);
        Assert.NotEqual(ArrangementOutcome.Reverted, result.Outcome);
        return result.Text;
    }

    /// <summary>
    ///     The same run as <see cref="Arrange" />, with the outcome and the diagnostics left for the
    ///     caller to assert on.
    /// </summary>
    /// <remarks>
    ///     ⚠ <see cref="Arrange" /> swallows the interesting half. A rule whose precondition is wrong
    ///     does not produce wrong output — the safety re-bind catches it and the file comes back
    ///     <see cref="ArrangementOutcome.Reverted" /> carrying <c>SK9098</c> — so a test that only reads
    ///     the text is asserting about the safety net rather than about the rule, and its failure names
    ///     <c>NotEqual(Reverted)</c> in a shared helper instead of the case that broke. The two
    ///     regression tests for #326 want to say "this rewrite was never attempted", which is a
    ///     statement about <see cref="ArrangementResult.Diagnostics" />.
    /// </remarks>
    static ArrangementResult Attempt(
        string source,
        string? only = null,
        bool removeUnused = false,
        IReadOnlyList<KeyValuePair<string, string>>? overrides = null
    ) {
        const string path = "/arrangement/Probe.cs";
        var text = SourceText.From(source);
        var tree = CSharpSyntaxTree.ParseText(text, CSharpFormatter.ParseOptions, path);

        // The kind is chosen from the file, as `RuleFixtures.Compile` chose it in #314: a probe
        // holding top-level statements is an executable, and compiled as a library it draws
        // `CS8805` — "Program using top-level statements must be an executable".
        //
        // ⚠ Measured, and it is NOT what lets `NamespaceBody_IsLeftAloneInATopLevelProgram` catch
        // the bug; believing it was is the claim this comment used to make, and it is refuted.
        // `CS8805` is present before *and* after the rewrite, so it cancels out of the safety
        // layer's appeared-set: with the old library-only kind restored, that fixture still goes red
        // on `CS8956` alone. What this buys is that the probe binds the compilation a real
        // `skala arrange` run binds, instead of one carrying an error no user's build has — which
        // matters for the next top-level fixture, not for this one.
        var topLevel = tree.GetRoot() is CompilationUnitSyntax unit
            && unit.Members.Any(static member => member is GlobalStatementSyntax);

        var compilation = CSharpCompilation.Create(
            "probe",
            [tree],
            SharedFrameworkReferences.Value,
            new(
                topLevel ? OutputKind.ConsoleApplication : OutputKind.DynamicallyLinkedLibrary,
                allowUnsafe: true,
                nullableContextOptions: NullableContextOptions.Enable
            )
        );

        var options = OptionResolver.Resolve(
            Path.Combine(Rikarin.Skala.Testing.Corpus.RepositoryRoot, "Probe.cs"),
            overrides
        ).Options;

        return Arranger.Arrange(
            path,
            text,
            new(options),
            compilation,
            removeUnused ? UsingsRule.Unused(compilation.GetSemanticModel(tree), tree) : null,
            null,
            only is null ? ArrangementFilter.All : new ArrangementFilter([only], [])
        );
    }

    /// <summary>
    ///     Asserts the run never reached the safety layer, and returns its text.
    /// </summary>
    /// <remarks>
    ///     ⚠ "Not reverted" is not the property these tests want. A rewrite that <em>was</em> produced
    ///     and then reverted leaves the file byte-identical, so every <c>Assert.Contains</c> about the
    ///     original text still passes — the bug is invisible to the assertions and visible only in the
    ///     outcome. The two rewrites #326 found had exactly that shape: correct output, on disk, for the
    ///     wrong reason. So the assertion is that <c>SK9098</c> never appeared.
    /// </remarks>
    static string Declined(ArrangementResult result) {
        Assert.DoesNotContain(
            result.Diagnostics,
            static diagnostic => diagnostic.Id is ArrangeIds.Reverted or ArrangeIds.SymbolChanged
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
            ArrangeIds.NullCheckingPattern
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

        Assert.Contains("b != null", Arrange(source, ArrangeIds.NullCheckingPattern), StringComparison.Ordinal);
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

        Assert.Contains("d != null", Arrange(source, ArrangeIds.NullCheckingPattern), StringComparison.Ordinal);
    }

    /// <summary>
    ///     ⚠ <c>string</c> overloads <c>==</c> and the pattern form matches it, so the rewrite is safe —
    ///     a naive "does the type declare operator ==" check would refuse every string null check in the
    ///     corpus.
    /// </summary>
    [Fact]
    public void IsNotNull_RewritesForString() {
        var output = Arrange(
            """
            namespace P;
            public class C { public bool M(string? s) { return s != null; } }
            """,
            ArrangeIds.NullCheckingPattern
        );

        Assert.Contains("s is not null", output, StringComparison.Ordinal);
    }

    /// <summary>
    ///     The preamble every expression-tree fixture shares: the NSubstitute shape from #347, where the
    ///     conversion to <c>Expression&lt;TDelegate&gt;</c> comes from a method parameter rather than from
    ///     a variable's declared type.
    /// </summary>
    const string ExpressionTreePreamble = """
                                          namespace P;
                                          using System;
                                          using System.Linq.Expressions;
                                          public sealed class Row { public string? Banner; public string? Icon; }
                                          public static class Arg {
                                              public static T Is<T>(Expression<Predicate<T>> predicate) => default!;
                                          }
                                          """;

    /// <summary>⚠ The positive control. An ordinary delegate lambda is not an expression tree.</summary>
    [Fact]
    public void IsNotNull_StillRewritesInsideAnOrdinaryDelegateLambda() {
        var output = Arrange(
            $$"""
              {{ExpressionTreePreamble}}
              public class C {
                  public Func<Row, bool> M() { return r => r.Banner == null; }
              }
              """,
            ArrangeIds.NullCheckingPattern
        );

        Assert.Contains("r.Banner is null", output, StringComparison.Ordinal);
    }

    /// <summary>
    ///     ⚠ #347. An expression tree may not contain an <c>is</c> pattern (CS8122), whatever the
    ///     operand's <c>operator ==</c> resolves to — here it is <c>string</c>'s, which
    ///     <see cref="IsNotNull_RewritesForString" /> pins as safe everywhere else. So the operator check
    ///     passes and only the syntactic-context check can refuse this.
    /// </summary>
    [Theory]
    [InlineData("public Expression<Func<Row, bool>> M() { return r => r.Banner == null; }")]
    [InlineData("public Expression<Predicate<Row>> M() { return r => r.Banner == null; }")]
    [InlineData("""public Row M() { return Arg.Is<Row>(x => x.Icon == "d" && x.Banner == null); }""")]
    public void IsNotNull_RefusesInsideAnExpressionTree(string member) {
        var result = Attempt(
            $$"""
              {{ExpressionTreePreamble}}
              public class C {
                  {{member}}
              }
              """,
            ArrangeIds.NullCheckingPattern
        );

        Assert.DoesNotContain("is null", Declined(result), StringComparison.Ordinal);
    }

    /// <summary>
    ///     ⚠ Measured against the compiler, not assumed: the inner lambda's own <c>ConvertedType</c> here
    ///     is <c>Func&lt;Row, bool&gt;</c> — not an <c>Expression</c> at all — and <c>csc</c> still reports
    ///     CS8122 on the rewrite. Checking only the *nearest* enclosing lambda therefore lets this through,
    ///     which is why the precondition walks every enclosing lambda instead of stopping at the first.
    /// </summary>
    [Fact]
    public void IsNotNull_RefusesInALambdaNestedInsideAnExpressionTree() {
        var result = Attempt(
            $$"""
              {{ExpressionTreePreamble}}
              public class C {
                  static bool Apply(Row row, Func<Row, bool> predicate) => predicate(row);
                  public Expression<Func<Row, bool>> M() { return r => Apply(r, x => x.Banner == null); }
              }
              """,
            ArrangeIds.NullCheckingPattern
        );

        Assert.DoesNotContain("is null", Declined(result), StringComparison.Ordinal);
    }

    /// <summary>
    ///     ⚠ The cost #347 is actually about, and the reason this is not merely a missed rewrite. The
    ///     safety net is all-or-nothing per file: one illegal rewrite sends the whole file back, so the
    ///     legal null check in the same file is discarded too and the file is permanently unarrangeable,
    ///     re-dropping a crash artefact on every run. Refusing at layer 1 is what keeps the other rewrite.
    /// </summary>
    [Fact]
    public void IsNotNull_KeepsTheLegalRewriteInAFileThatAlsoHoldsAnExpressionTree() {
        var result = Attempt(
            $$"""
              {{ExpressionTreePreamble}}
              public class C {
                  public Expression<Predicate<Row>> Tree() { return r => r.Banner == null; }
                  public bool Plain(Row? r) { return r != null; }
              }
              """,
            ArrangeIds.NullCheckingPattern
        );

        var output = Declined(result);
        Assert.Contains("r is not null", output, StringComparison.Ordinal);
        Assert.Contains("r.Banner == null", output, StringComparison.Ordinal);
    }

    const string EmptyStringProbe = """
                                    namespace P;
                                    public class C {
                                        public string F = string.Empty;
                                        public string M() { return System.String.Empty; }
                                    }
                                    """;

    /// <summary>
    ///     ⚠ The key is pinned here rather than inherited from the repository's own
    ///     <c>.editorconfig</c>. This test read that file and asserted the rewrite unconditionally, so
    ///     when <c>9193c537</c> deliberately flipped Skala's house style to
    ///     <c>skala_empty_string = string_empty</c> — across the export, the canonical distribution
    ///     and doc 06 together, which is what makes it a decision rather than drift — the test went red
    ///     reporting a rule regression that had not happened. The rule was correctly disabled.
    /// </summary>
    [Fact]
    public void EmptyString_BecomesTheLiteral_UnderEmptyLiteral() {
        var output = Arrange(
            EmptyStringProbe,
            ArrangeIds.EmptyString,
            overrides: [new KeyValuePair<string, string>("skala_empty_string", "empty_literal")]
        );

        Assert.DoesNotContain(".Empty", output, StringComparison.Ordinal);
        Assert.Contains("\"\"", output, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Under <c>string_empty</c> a <c>string.Empty</c> that is already there stays exactly as written.
    /// </summary>
    /// <remarks>
    ///     ⚠ The negative control for the pinned test above: with only that one, a rule that turned
    ///     every <c>string.Empty</c> into <c>""</c> whatever the option said would still be green. Until
    ///     #383 this test also claimed that <c>string_empty</c> was the rule declining to run — which was
    ///     true, and was the defect: the value was in the key's domain with nothing behind it.
    /// </remarks>
    [Fact]
    public void EmptyString_FieldIsLeftAlone_UnderStringEmpty() {
        var output = Declined(
            Attempt(
                EmptyStringProbe,
                ArrangeIds.EmptyString,
                overrides: [new KeyValuePair<string, string>("skala_empty_string", "string_empty")]
            )
        );

        Assert.Contains("string.Empty", output, StringComparison.Ordinal);
        Assert.DoesNotContain("\"\"", output, StringComparison.Ordinal);
    }

    const string EmptyStringPreamble = """
                                       namespace P;
                                       using System;
                                       using System.ComponentModel;
                                       using System.Linq.Expressions;
                                       public sealed class Row { public string? Banner; }
                                       public class C {
                                       """;

    static ArrangementResult AttemptEmptyString(string member, string style) =>
        Attempt(
            EmptyStringPreamble + "\n    " + member + "\n}\n",
            ArrangeIds.EmptyString,
            overrides: [new KeyValuePair<string, string>("skala_empty_string", style)]
        );

    /// <summary>
    ///     ⚠ #383: <c>""</c> ⇒ <c>string.Empty</c> under <c>string_empty</c>, in every position where a
    ///     non-constant is legal.
    /// </summary>
    /// <remarks>
    ///     The <c>when</c> clauses and the switch-expression arm body are here on purpose: they sit
    ///     beside a pattern without being part of it, so a constant-context test that matched on the
    ///     enclosing switch rather than on the pattern itself would wrongly decline them.
    /// </remarks>
    [Theory]
    [InlineData("""public string F = "";""")]
    [InlineData("""public static readonly string R = "";""")]
    [InlineData("""public string M() { return ""; }""")]
    [InlineData("""public string M() => @"";""")]
    [InlineData("""public void M() { Console.WriteLine(""); }""")]
    [InlineData("""public void M() { string s = ""; Console.WriteLine(s); }""")]
    [InlineData("""public string M(string s) => s + "";""")]
    [InlineData("""public string M() => $"{""}";""")]
    [InlineData("""public int M(string s) => s switch { var x when x == "" => 1, _ => 0 };""")]
    [InlineData("""public int M(string s) { switch (s) { case var x when x == "": return 1; default: return 0; } }""")]
    [InlineData("""public string M(int i) => i switch { 0 => "", _ => "x" };""")]
    [InlineData("""public Func<string> M() => () => "";""")]
    public void EmptyString_BecomesTheField_UnderStringEmpty(string member) {
        var output = Declined(AttemptEmptyString(member, "string_empty"));

        Assert.Contains("string.Empty", output, StringComparison.Ordinal);
        Assert.DoesNotContain("\"\"", output, StringComparison.Ordinal);
    }

    /// <summary>
    ///     ⚠ #383: every position where the rewrite would not compile, or would change more than the
    ///     spelling, is declined by the rule itself — and the file is not reverted.
    /// </summary>
    /// <remarks>
    ///     <c>string.Empty</c> is a <c>static readonly</c> field, not a constant, so each constant
    ///     context below would be <c>CS0133</c>, <c>CS0182</c>, <c>CS1736</c> or <c>CS0150</c> after the
    ///     rewrite. Safety layer 2 would catch those, but by reverting the whole file — which is why
    ///     <see cref="Declined" /> asserts that it never had to: the rule declining is the property under
    ///     test, not the safety net catching it. The last five are not a <c>string</c> literal at all
    ///     (<c>$""</c>, <c>$@""</c>, <c>""u8</c>) or sit in an expression tree, where the rewrite
    ///     compiles but changes what a query provider is handed.
    /// </remarks>
    [Theory]
    [InlineData("""public const string F = "";""")]
    [InlineData("""public void M() { const string L = ""; Console.WriteLine(L); }""")]
    [InlineData("""public const string B = "b"; public const string A = "" + B;""")]
    [InlineData("""public const string T = true ? "" : "x";""")]
    [InlineData("""[Obsolete("")] public void M() { }""")]
    [InlineData("""[DefaultValue("" + "x")] public string P { get; set; } = "x";""")]
    [InlineData("""public void M(string s = "") { }""")]
    [InlineData("""public void M() { Func<string, string> f = (string s = "") => s; f("x"); }""")]
    [InlineData("""public void M() { void L(string s = "") { } L(); }""")]
    [InlineData("""public string this[string key = ""] => key;""")]
    [InlineData("""public int M(string s) { switch (s) { case "": return 1; default: return 0; } }""")]
    [InlineData(
        """public int M(string s) { switch (s) { case "": return 1; case "x": goto case ""; default: return 0; } }"""
    )]
    [InlineData(
        """public int M(string s) { switch (s) { case "" when s.Length == 0: return 1; default: return 0; } }"""
    )]
    [InlineData("""public bool M(string s) => s is "";""")]
    [InlineData("""public bool M(string s) => s is not ("" or "x");""")]
    [InlineData("""public int M(string s) => s switch { "" => 1, _ => 0 };""")]
    [InlineData("""public bool M(Row r) => r is { Banner: "" };""")]
    [InlineData("""public bool M(string[] a) => a is ["", ..];""")]
    [InlineData("""public string M() => $"";""")]
    [InlineData("""public string M() => $@"";""")]
    [InlineData("""public ReadOnlySpan<byte> M() => ""u8;""")]
    [InlineData("""public Expression<Func<string>> M() => () => "";""")]
    [InlineData("""public Expression<Func<string, bool>> M() => s => s == "";""")]
    public void EmptyString_IsDeclined_WhereTheFieldIsNotTheSameExpression(string member) {
        var result = AttemptEmptyString(member, "string_empty");
        var output = Declined(result);

        Assert.Equal(ArrangementOutcome.Unchanged, result.Outcome);
        Assert.DoesNotContain("string.Empty", output, StringComparison.Ordinal);
    }

    /// <summary>
    ///     ⚠ One declined literal costs only itself: the legal rewrite beside it still happens.
    /// </summary>
    /// <remarks>
    ///     The point of declining at the rule rather than leaving it to safety layer 2, whose unit is
    ///     the file. With the decline removed and the guard relied on instead, this file comes back
    ///     <c>Reverted</c> with neither rewrite.
    /// </remarks>
    [Fact]
    public void EmptyString_KeepsTheLegalRewriteBesideAConstantContext() {
        var output = Declined(
            AttemptEmptyString(
                """public const string K = ""; public string M(string s) => s switch { "" => K, _ => "" };""",
                "string_empty"
            )
        );

        Assert.Contains("""public const string K = "";""", output, StringComparison.Ordinal);
        Assert.Contains("""{ "" => K, _ => string.Empty }""", output, StringComparison.Ordinal);
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
            ArrangeIds.RedundantBraces
        );

        // The declaring block keeps its braces; the other loses them.
        Assert.Contains("int scoped", output, StringComparison.Ordinal);
        Assert.Equal(1, CountBareBlocks(output));
    }

    /// <summary>
    ///     ⚠ #341. The three statement kinds the rule refused were the declaration <em>statements</em>,
    ///     and every one of these introduces a local without being any of them.
    /// </summary>
    /// <remarks>
    ///     ⚠ The test above is not the control it looks like. It pins <c>int scoped = a;</c>, a
    ///     <c>LocalDeclarationStatementSyntax</c>, and stayed green through the entire life of the bug —
    ///     deleting <c>LocalDeclarationStatementSyntax</c> from the predicate turns it red, which is
    ///     exactly why nobody looked further. The forms below are an <c>ExpressionStatementSyntax</c>
    ///     twice and an <c>IfStatementSyntax</c> once, so no kind test reaches them.
    ///     <para>
    ///         ⚠ These assert on <see cref="Declined" /> rather than on the text, and the distinction is
    ///         the whole point of the issue. The re-bind <em>does</em> catch this and reverts, so a test
    ///         reading only the output was green while the bug was live. What is broken is
    ///         <c>--arrange=syntactic</c>, which has no compilation to re-bind against: measured on the
    ///         issue's probe before the fix, <c>skala format --arrange=syntactic</c> wrote the file and
    ///         the result drew four <c>CS0128</c> and one <c>CS0165</c>.
    ///     </para>
    /// </remarks>
    [Theory]
    [InlineData(
        "deconstruction",
        """
        static (int, int) Values() => (1, 2);

        public void M() {
            {
                var (a, b) = Values();
                System.Console.WriteLine(a + b);
            }
            {
                var (a, b) = Values();
                System.Console.WriteLine(a - b);
            }
        }
        """
    )]
    [InlineData(
        "out var",
        """
        public void M(string s) {
            {
                int.TryParse(s, out var n);
                System.Console.WriteLine(n);
            }
            {
                int.TryParse(s, out var n);
                System.Console.WriteLine(-n);
            }
        }
        """
    )]
    [InlineData(
        "is-pattern designation",
        """
        public void M(object o) {
            {
                if (o is string s) {
                    System.Console.WriteLine(s);
                }
            }
            {
                if (o is string s) {
                    System.Console.WriteLine(s.Length);
                }
            }
        }
        """
    )]
    public void RedundantBraces_AreKeptWhenTheDeclarationIsAnExpression(string form, string body) {
        var source = $$"""
                       namespace P;

                       public class C {
                       {{body}}
                       }
                       """;

        var arranged = Declined(Attempt(source, ArrangeIds.RedundantBraces));

        // Both pairs survive: lifting either one collides with the other's names in the method body.
        Assert.Equal(2, CountBareBlocks(arranged));
        Assert.Equal(source, arranged);

        // `form` names the case in the runner's output; asserting on it keeps xUnit1026 quiet.
        Assert.NotEmpty(form);
    }

    /// <summary>
    ///     The other side of #341: a designation the enclosing block provably cannot see still lifts.
    /// </summary>
    /// <remarks>
    ///     ⚠ This is what the issue's proposed <c>statement.DescendantNodes()</c> predicate would have
    ///     cost, and it is why the walk stops at a <c>BlockSyntax</c> and at an
    ///     <c>AnonymousFunctionExpressionSyntax</c> instead. Both are declaration spaces in every C#
    ///     version, so skipping them asserts nothing that could later stop being true; every other
    ///     scope-introducing construct is deliberately *not* skipped and over-rejects.
    ///     <para>
    ///         ⚠ The nested-block case is the one worth reading twice. The outer braces go and the inner
    ///         braces stay — <c>n</c> ends one level shallower and still cannot reach the method body,
    ///         which is precisely the difference between widening a scope and moving one.
    ///     </para>
    /// </remarks>
    [Fact]
    public void RedundantBraces_AreStillRemovedWhenTheDesignationIsScopedDeeper() {
        var arranged = Declined(
            Attempt(
                """
                namespace P;

                public class C {
                    public void Lambda(System.Collections.Generic.List<string> xs) {
                        {
                            xs.ForEach(x => {
                                if (x is { Length: > 0 } text) {
                                    System.Console.WriteLine(text);
                                }
                            });
                        }
                    }

                    public void Nested(string s) {
                        {
                            {
                                int.TryParse(s, out var n);
                                System.Console.WriteLine(n);
                            }
                        }
                    }
                }
                """,
                ArrangeIds.RedundantBraces
            )
        );

        // Three bare blocks in, one out: the lambda's holder and both of `Nested`'s outer braces go,
        // and the block actually scoping `n` stays.
        Assert.Equal(1, CountBareBlocks(arranged));
        Assert.Contains("out var n", arranged, StringComparison.Ordinal);
    }

    [Fact]
    public void Parentheses_AreRemovedByDefault_AndOnlyWherePrecedenceAllows() {
        // ⚠ This test asserted the opposite until the gate was lifted. SK-DIV-0014 gated parenthesis
        // removal behind `--aggressive` for the first release and named the condition for revisiting
        // it; the condition is met and the gate cost 4.25 points of changed-span agreement against an
        // oracle whose own profile removes these by default. The flag outlived the gate as a no-op
        // and #389 removed it.
        const string source = """
                              namespace P;
                              public class C {
                                  public int M(int a, int b, int c) { return a + (b * c); }
                                  public int N(int a, int b, int c) { return a - (b - c); }
                                  public int O(int a, int b, int c) { return a | (b & c); }
                                  public bool P(int a, int b, int c) { return (a < b) && (b < c); }
                              }
                              """;

        var arranged = Arrange(source, ArrangeIds.RedundantParentheses);
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
            ArrangeIds.Var
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
            new(options, ArrangementScope.Syntactic),
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

    [Fact]
    public void ArgumentStyle_StopsAtTheFirstNameHoldingTheCallTogether() {
        // ⚠ Regression. Removing a name from an argument that follows an out-of-position named
        // one produces CS8323, "named argument used out-of-position but followed by an unnamed
        // argument". Safety layer 2 caught this on Vixen rather than letting it out, which means the
        // file was reverted whole — correct, and still a rule that could not arrange those files.
        var arranged = Arrange(
            """
            namespace P;
            public class C {
                public void Take(int first, int second, int third) { }

                public void M() {
                    Take(second: 2, first: 1, third: 3);
                }
            }
            """,
            ArrangeIds.ArgumentStyle
        );

        // `second:` is out of position and must keep its name; everything after it must too, or the
        // call stops compiling.
        Assert.Contains("second: 2", arranged, StringComparison.Ordinal);
        Assert.Contains("third: 3", arranged, StringComparison.Ordinal);
    }

    [Fact]
    public void ArgumentStyle_RemovesANameOnlyWhereTheArgumentIsAlreadyInPosition() {
        var arranged = Arrange(
            """
            namespace P;
            public class C {
                public void Take(int first, int second) { }

                public void M() {
                    Take(first: 1, second: 2);
                }
            }
            """,
            ArrangeIds.ArgumentStyle
        );

        Assert.Contains("Take(1, 2)", arranged, StringComparison.Ordinal);
    }

    /// <summary>
    ///     ⚠ #326 (1). A discard imposes no target type, so the explicit type name stays.
    /// </summary>
    /// <remarks>
    ///     <c>ObjectCreationRule.TargetTypeOf</c> answered a simple assignment with
    ///     <c>GetTypeInfo(assignment.Left).Type</c>, and ⚠
    ///     <b>
    ///         a discard infers its type from the
    ///         right-hand side
    ///     </b> — so for <c>_ = new Regex(p, o)</c> the model answered <c>Regex</c>, the
    ///     "target equals created type" precondition passed, and the rewrite produced <c>_ = new(p, o)</c>:
    ///     <c>CS8754: There is no target type for 'new(string, RegexOptions)'</c>. The question the
    ///     precondition means to ask is what the position <em>imposes</em>, and a discard imposes
    ///     nothing; it takes whatever it is given. Found on
    ///     <c>Rules/…/Correctness/MalformedRegexPatternAnalyzer.cs</c> as an <c>SK9098</c> revert.
    ///     <para>
    ///         ⚠ <c>held</c> is the control and it is not decoration. Both arms are the same
    ///         <c>SimpleAssignmentExpression</c> case, so without it the test would still pass with the
    ///         rule switched off entirely, and "the discard was left alone" would be measuring nothing.
    ///     </para>
    /// </remarks>
    [Fact]
    public void ObjectCreation_LeavesADiscardAssignmentExplicitBecauseADiscardIsNoTarget() {
        var arranged = Declined(
            Attempt(
                """
                using System.Text.RegularExpressions;

                namespace P;

                public class C {
                    public void Discarded(string pattern, RegexOptions options) {
                        _ = new Regex(pattern, options);
                    }

                    public Regex Assigned(string pattern, RegexOptions options) {
                        Regex held;
                        held = new Regex(pattern, options);
                        return held;
                    }
                }
                """,
                ArrangeIds.ObjectCreation
            )
        );

        Assert.Contains("_ = new Regex(pattern, options);", arranged, StringComparison.Ordinal);
        Assert.Contains("held = new(pattern, options);", arranged, StringComparison.Ordinal);
    }

    /// <summary>
    ///     ⚠ #326 (1), the other half: the guard asks the model, not the spelling.
    /// </summary>
    /// <remarks>
    ///     A local genuinely named <c>_</c> is a real target — a declared <c>_</c> in scope wins over the
    ///     discard — so <c>assignment.Left is IdentifierNameSyntax { Identifier.ValueText: "_" }</c> is a
    ///     guard that reads the same and is wrong, and it would cost this rewrite for no reason. This
    ///     fixture is what separates the two, and it is the one that goes red if the guard is ever
    ///     rewritten syntactically for speed.
    /// </remarks>
    [Fact]
    public void ObjectCreation_StillRewritesAnAssignmentToALocalNamedUnderscore() {
        var arranged = Declined(
            Attempt(
                """
                using System.Text.RegularExpressions;

                namespace P;

                public class C {
                    public Regex M(string pattern, RegexOptions options) {
                        Regex _;
                        _ = new Regex(pattern, options);
                        return _;
                    }
                }
                """,
                ArrangeIds.ObjectCreation
            )
        );

        Assert.Contains("_ = new(pattern, options);", arranged, StringComparison.Ordinal);
    }

    /// <summary>The probe #461's rows are asked of, compiled as one file.</summary>
    const string ArgumentProbe = """
                                 using System;
                                 using System.Collections.Generic;

                                 namespace P;

                                 public class Foo { public Foo() { } public Foo(Bar b) { } }
                                 public class Two { public Two(Bar b) { } public Two(Baz z) { } }
                                 public class Bar { }
                                 public class Baz { }
                                 public class Derived : Foo { }
                                 public struct Val { public int X; }
                                 public class Box<T> { public static void Put(T value) { } }
                                 public static class Ext { public static void Use(this string s, Foo f) { } }
                                 public class Base { public Base(Foo f) { } }
                                 public class Primary() : Base(new Foo());

                                 public class C : Base {
                                     public C() : base(new Foo()) { }
                                     public C(int unused) : this() { }

                                     static void One(Foo f) { }
                                     static void Over(Foo f) { }
                                     static void Over(Bar b) { }
                                     static void Arity(Foo f) { }
                                     static void Arity(Foo f, int i) { }
                                     static void Gen<T>(T value) { }
                                     static void Params(params object[] values) { }
                                     static void ParamsFoo(params Foo[] values) { }
                                     static void Optional(Foo? f = null) { }
                                     static void NullableVal(Val? v) { }
                                     static void TakeVal(Val v) { }
                                     static void TakeIn(in Foo f) { }
                                     static void Pair(Foo a, Foo b) { }
                                     static void Cross(Foo a, Bar b) { }
                                     static void Cross(Bar a, Foo b) { }
                                     static void TakeObj(object o) { }
                                     static void TakeObj(string s) { }

                                     event EventHandler? Changed;

                                     void M(Action<Foo> action, Dictionary<Foo, int> map) {
                                         Changed?.Invoke(this, new EventArgs());
                                         One(new Foo());
                                         Over(new Foo());
                                         Arity(new Foo(), 1);
                                         Gen(new Foo());
                                         Gen<Foo>(new Foo());
                                         Params(new object());
                                         ParamsFoo(new Foo());
                                         One(new Derived());
                                         Optional(new Foo());
                                         NullableVal(new Val());
                                         TakeVal(new Val());
                                         TakeIn(new Foo());
                                         Pair(b: new Foo(), a: new Foo());
                                         Cross(new Foo(), new Bar());
                                         Cross(new Bar(), new Foo());
                                         TakeObj(new object());
                                         action(new Foo());
                                         Console.WriteLine(map[new Foo()]);
                                         Box<Foo>.Put(new Foo());
                                         "x".Use(new Foo());
                                         One(new Foo(new Bar()));
                                         Console.WriteLine(new Two(new Bar()));
                                         Console.WriteLine(new object());
                                     }
                                 }
                                 """;

    /// <summary>
    ///     #461: an argument is a target-typed position, and the oracle's rows are reproduced — each
    ///     line here is what <c>jb cleanupcode</c> 2025.2.6 wrote for it under <c>SkalaCleanup</c>.
    /// </summary>
    /// <remarks>
    ///     ⚠ The two <c>Cross</c> rows are the order-dependence: the first argument alone keeps one
    ///     overload applicable, both together would make the call ambiguous, and the oracle converts the
    ///     first and stops — in either argument order. <c>One(new(new()))</c> is the oracle's too, and it
    ///     compiles: the outer creation's constructor is chosen against <c>Foo</c> exactly as before.
    /// </remarks>
    [Theory]
    [InlineData("public class Primary() : Base(new());")]
    [InlineData("public C() : base(new()) { }")]
    [InlineData("One(new());")]
    [InlineData("Arity(new(), 1);")]
    [InlineData("Gen<Foo>(new());")]
    [InlineData("Optional(new());")]
    [InlineData("TakeVal(new());")]
    [InlineData("TakeIn(new());")]
    [InlineData("Pair(b: new(), a: new());")]
    [InlineData("Cross(new(), new Bar());")]
    [InlineData("Cross(new(), new Foo());")]
    [InlineData("action(new());")]
    [InlineData("Console.WriteLine(map[new()]);")]
    [InlineData("Box<Foo>.Put(new());")]
    [InlineData("\"x\".Use(new());")]
    [InlineData("One(new(new()));")]
    [InlineData("Changed?.Invoke(this, new());")]
    public void ObjectCreation_AnArgumentIsTargetTyped(string expected) {
        var arranged = Declined(Attempt(ArgumentProbe, ArrangeIds.ObjectCreation));
        Assert.Contains(expected, arranged, StringComparison.Ordinal);
    }

    /// <summary>
    ///     #461's refusals: each of these would bind another member, fail to infer, or name a type the
    ///     parameter is not. The oracle leaves every one as written.
    /// </summary>
    [Theory]
    [InlineData("Over(new Foo());")]
    [InlineData("Gen(new Foo());")]
    [InlineData("Params(new object());")]
    [InlineData("ParamsFoo(new Foo());")]
    [InlineData("One(new Derived());")]
    [InlineData("NullableVal(new Val());")]
    [InlineData("TakeObj(new object());")]
    [InlineData("Console.WriteLine(new Two(new Bar()));")]
    [InlineData("Console.WriteLine(new object());")]
    public void ObjectCreation_AnArgumentThatWouldRebindTheCall_KeepsItsType(string kept) {
        var arranged = Declined(Attempt(ArgumentProbe, ArrangeIds.ObjectCreation));
        Assert.Contains(kept, arranged, StringComparison.Ordinal);
    }

    /// <summary>
    ///     ⚠ Arguments are <c>object_creation_when_type_not_evident</c>'s: measured, flipping that key
    ///     alone restored every argument row and flipping the evident key moved none.
    /// </summary>
    [Fact]
    public void ObjectCreation_AnArgumentIsNotEvident() {
        var arranged = Declined(
            Attempt(
                ArgumentProbe,
                ArrangeIds.ObjectCreation,
                overrides: [new("skala_object_creation_when_type_not_evident", "explicitly_typed")]
            )
        );
        Assert.Contains("One(new Foo());", arranged, StringComparison.Ordinal);

        arranged = Declined(
            Attempt(
                ArgumentProbe,
                ArrangeIds.ObjectCreation,
                overrides: [new("skala_object_creation_when_type_evident", "explicitly_typed")]
            )
        );
        Assert.Contains("One(new());", arranged, StringComparison.Ordinal);
    }

    /// <summary>The probe #524's rows are asked of: a <c>new</c> as the value of a lambda.</summary>
    const string LambdaProbe = """
                               using System;
                               using System.Linq.Expressions;
                               using System.Threading.Tasks;

                               namespace P;

                               public class Foo { public Foo() { } public Foo(int x) { } }
                               public class Bar { }

                               public class C {
                                   static void TakeFunc(Func<Foo> f) { }
                                   static void TakeArg(Func<int, Foo> f) { }
                                   static void Over(Func<Foo> f) { }
                                   static void Over(Func<Bar> f) { }
                                   static void Gen<T>(Func<T> f) { }
                                   static void TakeExpr(Expression<Func<Foo>> e) { }
                                   static void TakeObj(Func<object> f) { }
                                   static void TakeAsync(Func<Task<Foo>> f) { }

                                   Func<Foo> _field = () => new Foo();
                                   Func<Foo> Property => () => new Foo();

                                   Foo Plain() {
                                       Func<Foo> local = () => new Foo(1);
                                       return local();
                                   }

                                   void M() {
                                       TakeFunc(() => new Foo());
                                       TakeArg(x => new Foo(x));
                                       Over(() => new Foo());
                                       Gen(() => new Foo());
                                       Gen<Foo>(() => new Foo());
                                       TakeExpr(() => new Foo());
                                       TakeObj(() => new Foo());
                                       TakeAsync(async () => new Foo());
                                       TakeFunc(() => { return new Foo(); });
                                       TakeFunc(delegate { return new Foo(); });
                                       var inferred = () => new Foo();
                                       Func<Foo> assigned;
                                       assigned = () => new Foo();
                                       Task.Run(() => new Foo());
                                       Console.WriteLine(inferred() + "" + assigned());
                                   }
                               }
                               """;

    /// <summary>
    ///     #524: a <c>new</c> that a lambda returns is target-typed when the delegate's return type is fixed
    ///     from outside the lambda. Each row is the oracle's answer under <c>SkalaCleanup</c>.
    /// </summary>
    [Theory]
    [InlineData("Func<Foo> _field = () => new();")]
    [InlineData("Func<Foo> Property => () => new();")]
    [InlineData("TakeFunc(() => new());")]
    [InlineData("TakeArg(x => new(x));")]
    [InlineData("Gen<Foo>(() => new());")]
    [InlineData("TakeExpr(() => new());")]
    [InlineData("TakeAsync(async () => new());")]
    [InlineData("TakeFunc(() => { return new(); });")]
    [InlineData("TakeFunc(delegate { return new(); });")]
    [InlineData("assigned = () => new();")]
    public void ObjectCreation_ALambdaValueIsTargetTyped(string expected) {
        var arranged = Declined(Attempt(LambdaProbe, ArrangeIds.ObjectCreation));
        Assert.Contains(expected, arranged, StringComparison.Ordinal);
    }

    /// <summary>
    ///     #524's refusals: the lambda's return type is read off the very body being rewritten, or the
    ///     call would bind something else. The oracle leaves every one as written.
    /// </summary>
    /// <remarks>
    ///     ⚠ <c>return local();</c>'s method is the control for the old behaviour that stopped at a lambda:
    ///     the method's own <c>Foo</c> return type must not be read for a <c>return</c> inside a lambda.
    /// </remarks>
    [Theory]
    [InlineData("Over(() => new Foo());")]
    [InlineData("Gen(() => new Foo());")]
    [InlineData("TakeObj(() => new Foo());")]
    [InlineData("var inferred = () => new Foo();")]
    [InlineData("Task.Run(() => new Foo());")]
    public void ObjectCreation_ALambdaWhoseTypeComesFromItsBody_KeepsItsType(string kept) {
        var arranged = Declined(Attempt(LambdaProbe, ArrangeIds.ObjectCreation));
        Assert.Contains(kept, arranged, StringComparison.Ordinal);
    }

    /// <summary>⚠ #524: a lambda's value is <c>when_type_not_evident</c>'s, its block <c>return</c> included.</summary>
    [Fact]
    public void ObjectCreation_ALambdaValueIsNotEvident() {
        var arranged = Declined(
            Attempt(
                LambdaProbe,
                ArrangeIds.ObjectCreation,
                overrides: [new("skala_object_creation_when_type_not_evident", "explicitly_typed")]
            )
        );
        Assert.Contains("TakeFunc(() => new Foo());", arranged, StringComparison.Ordinal);
        Assert.Contains("TakeFunc(() => { return new Foo(); });", arranged, StringComparison.Ordinal);
        Assert.Contains("Func<Foo> _field = () => new Foo();", arranged, StringComparison.Ordinal);
    }

    /// <summary>The probe #462's rows are asked of: every predefined keyword, written as one.</summary>
    const string KeywordProbe = """
                                using System;
                                using System.Collections.Generic;

                                namespace P;

                                enum Small : byte { A }

                                delegate int Handler(string s);

                                interface IThing<T> where T : IComparable<int> { }

                                class Keywords {
                                    int _count;
                                    public bool Enabled { get; set; }
                                    event Func<int>? Raised;
                                    int this[int i] => i;
                                    public static Keywords operator +(Keywords a, int b) => a;
                                    (int, string) _tuple;
                                    int? _maybe;
                                    nint _native;

                                    string Name() => nameof(Int32);

                                    void M(ref int r, out int o, params int[] rest) {
                                        o = 1;
                                        Dictionary<string, int> map = new Dictionary<string, int>();
                                        long cast = (long)r;
                                        object boxed = 1;
                                        var t = typeof(decimal);
                                        var d = default(double);
                                        var s = boxed as string;
                                        var max = int.MaxValue;
                                        var empty = string.Empty;
                                        Console.WriteLine(map.Count + cast + t.Name + d + s + max + empty + _count + _maybe + _native);
                                    }
                                }
                                """;

    /// <summary>
    ///     #462: at <c>predefined_type_for_locals_parameters_members = false</c> the keyword is expanded
    ///     to its framework name — every row the oracle's, under <c>SkalaCleanup</c>.
    /// </summary>
    [Theory]
    [InlineData("delegate Int32 Handler(String s);")]
    [InlineData("where T : IComparable<Int32>")]
    [InlineData("Int32 _count;")]
    [InlineData("public Boolean Enabled")]
    [InlineData("event Func<Int32>? Raised;")]
    [InlineData("Int32 this[Int32 i] => i;")]
    [InlineData("operator +(Keywords a, Int32 b)")]
    [InlineData("(Int32, String) _tuple;")]
    [InlineData("Int32? _maybe;")]
    [InlineData("String Name() => nameof(Int32);")]
    [InlineData("void M(ref Int32 r, out Int32 o, params Int32[] rest)")]
    [InlineData("Dictionary<String, Int32> map = new Dictionary<String, Int32>();")]
    [InlineData("Int64 cast = (Int64)r;")]
    [InlineData("Object boxed = 1;")]
    [InlineData("typeof(Decimal)")]
    [InlineData("default(Double)")]
    [InlineData("boxed as String;")]
    [InlineData("enum Small : byte { A }")]
    [InlineData("nint _native;")]
    [InlineData("var max = int.MaxValue;")]
    [InlineData("var empty = string.Empty;")]
    public void PredefinedType_AtFalse_ExpandsToTheFrameworkName(string expected) {
        var arranged = Declined(
            Attempt(
                KeywordProbe,
                ArrangeIds.PredefinedType,
                overrides: [new("dotnet_style_predefined_type_for_locals_parameters_members", "false")]
            )
        );
        Assert.Contains(expected, arranged, StringComparison.Ordinal);
    }

    /// <summary>
    ///     #462, the sibling key: at <c>predefined_type_for_member_access = false</c> the receiver expands
    ///     and the declarations, which the other key still owns at <c>true</c>, do not.
    /// </summary>
    [Theory]
    [InlineData("var max = Int32.MaxValue;")]
    [InlineData("var empty = String.Empty;")]
    [InlineData("int _count;")]
    [InlineData("typeof(decimal)")]
    public void PredefinedType_MemberAccessAtFalse_ExpandsOnlyTheReceiver(string expected) {
        var arranged = Declined(
            Attempt(
                KeywordProbe,
                ArrangeIds.PredefinedType,
                overrides: [new("dotnet_style_predefined_type_for_member_access", "false")]
            )
        );
        Assert.Contains(expected, arranged, StringComparison.Ordinal);
    }

    /// <summary>
    ///     ⚠ #462: with <em>both</em> keys at <c>false</c> the rule must still run. It used to be enabled
    ///     only when one of them was <c>true</c>, so this configuration expanded nothing at all.
    /// </summary>
    [Fact]
    public void PredefinedType_BothKeysAtFalse_ExpandBothPositions() {
        var arranged = Declined(
            Attempt(
                KeywordProbe,
                ArrangeIds.PredefinedType,
                overrides: [
                    new("dotnet_style_predefined_type_for_locals_parameters_members", "false"),
                    new("dotnet_style_predefined_type_for_member_access", "false")
                ]
            )
        );
        Assert.Contains("Int32 _count;", arranged, StringComparison.Ordinal);
        Assert.Contains("var max = Int32.MaxValue;", arranged, StringComparison.Ordinal);
    }

    /// <summary>
    ///     ⚠ #462: where something else answers to <c>Int32</c>, the oracle writes <c>System.Int32</c> —
    ///     measured with a class of that name beside the field — and <c>String</c>, which nothing shadows,
    ///     stays short.
    /// </summary>
    [Fact]
    public void PredefinedType_AtFalse_QualifiesAShadowedName() {
        var arranged = Declined(
            Attempt(
                """
                using System;

                namespace P {
                    class Int32 { }

                    class Masked {
                        int _count;
                        string Name() => "x" + _count;
                    }
                }
                """,
                ArrangeIds.PredefinedType,
                overrides: [new("dotnet_style_predefined_type_for_locals_parameters_members", "false")]
            )
        );
        Assert.Contains("System.Int32 _count;", arranged, StringComparison.Ordinal);
        Assert.Contains("String Name()", arranged, StringComparison.Ordinal);
    }

    [Fact]
    public void NamespaceBody_IsLeftAloneWhenTheFileHasMoreThanOne() {
        // A file-scoped namespace must be the only one in its file, so this is not a style question.
        var arranged = Arrange(
            """
            namespace A {
                public class X { }
            }

            namespace B {
                public class Y { }
            }
            """,
            ArrangeIds.NamespaceBody
        );

        Assert.DoesNotContain("namespace A;", arranged, StringComparison.Ordinal);
        Assert.DoesNotContain("namespace B;", arranged, StringComparison.Ordinal);
    }

    /// <summary>
    ///     ⚠ #326 (2). A top-level program keeps its block namespace.
    /// </summary>
    /// <remarks>
    ///     Top-level statements are members of the generated <c>Program</c>, so a file-scoped namespace
    ///     cannot open after them: the rewrite is
    ///     <c>CS8956: File-scoped namespace must precede all other members in a file</c>. Found on
    ///     <c>Testing/Rikarin.Skala.Testing/Program.cs</c> by the first
    ///     <c>skala arrange --check</c> anything had ever run over master, as an <c>SK9098</c> revert —
    ///     the promise held and the file was left untouched, which is exactly why it went unnoticed:
    ///     the output was right and the reason was wrong.
    ///     <para>
    ///         ⚠ The assertion that matters is <see cref="Declined" />'s, not the two
    ///         <c>Assert.Contains</c> lines. Restore the old guard and the text assertions still pass,
    ///         because a reverted rewrite leaves the file byte-identical; only the absence of
    ///         <c>SK9098</c> separates "the rule declined" from "the rule tried and was caught".
    ///     </para>
    ///     <para>
    ///         ⚠ The issue expected this fixture to be impossible before #314's <c>OutputKind</c>
    ///         selection, and <b>that is refuted</b> — measured by restoring the library-only kind with
    ///         the guard sabotaged, where the case still goes red on <c>CS8956</c>. #314's constraint
    ///         was the analyzer harness's, where <c>CS8805</c> makes a fixture "does not compile" and
    ///         is rejected outright; here <c>CS8805</c> merely appears before <em>and</em> after and
    ///         cancels out of the appeared-set. <see cref="Attempt" /> picks the kind from the file
    ///         anyway, so the probe binds what a real run binds.
    ///     </para>
    /// </remarks>
    [Fact]
    public void NamespaceBody_IsLeftAloneInATopLevelProgram() {
        var arranged = Declined(
            Attempt(
                """
                using System;

                Console.WriteLine("the entry point");

                namespace P
                {
                    public class C {
                        public int N;
                    }
                }
                """,
                ArrangeIds.NamespaceBody
            )
        );

        Assert.DoesNotContain("namespace P;", arranged, StringComparison.Ordinal);
        Assert.Contains("namespace P", arranged, StringComparison.Ordinal);
    }

    [Fact]
    public void NamespaceBody_KeepsTheSemicolonOnTheNameLine() {
        // ⚠ Regression, and it compiled: leaving the name's trailing newline on the name emitted
        // a semicolon stranded on its own line with the first member behind it. Only a diff showed it.
        var arranged = Arrange(
            """
            namespace P
            {
                public class C {
                    public int N;
                }
            }
            """,
            ArrangeIds.NamespaceBody
        );

        Assert.Contains("namespace P;", arranged, StringComparison.Ordinal);
        Assert.DoesNotContain("\n;", arranged, StringComparison.Ordinal);
    }

    [Fact]
    public void Parentheses_AreKeptAroundAnOperandOfANonObviousOperation() {
        // ⚠ Regression. `parentheses_non_obvious_operations = shift, bitwise_*` is about the
        // *enclosing* operation, so an arithmetic operand of one keeps its parentheses. The first
        // version keyed on the inner expression alone and stripped these.
        var arranged = Arrange(
            """
            namespace P;
            public class C {
                public int And(int a, int b) { return a & (b + 1); }
                public int Shift(int a, int b) { return a << (b + 1); }
                public int Plain(int a, int b, int c) { return a + (b * c); }
            }
            """,
            ArrangeIds.RedundantParentheses
        );

        Assert.Contains("a & (b + 1)", arranged, StringComparison.Ordinal);
        Assert.Contains("a << (b + 1)", arranged, StringComparison.Ordinal);
        Assert.Contains("a + b * c", arranged, StringComparison.Ordinal);
    }

    [Fact]
    public void Parentheses_AreKeptWhereRemovalWouldReassociate() {
        // Equal precedence is not associativity, and on floating point the grouping is arithmetic
        // rather than decoration. The re-parse proof refuses this without a table saying so.
        var arranged = Arrange(
            """
            namespace P;
            public class C {
                public float M(float a, float x, float y) { return a * (x * y); }
            }
            """,
            ArrangeIds.RedundantParentheses
        );

        Assert.Contains("a * (x * y)", arranged, StringComparison.Ordinal);
    }

    [Fact]
    public void TrailingComma_IsRemovedFromEveryListShapeTheGrammarAllows() {
        var arranged = Arrange(
            """
            namespace P;
            public class C {
                public int[] Array = new[] { 1, 2, 3, };
                public int[] Collection = [4, 5, 6,];
            }

            public enum E {
                A,
                B,
            }
            """,
            ArrangeIds.TrailingComma
        );

        Assert.Contains("new[] { 1, 2, 3 }", arranged, StringComparison.Ordinal);
        Assert.Contains("[4, 5, 6]", arranged, StringComparison.Ordinal);
        Assert.DoesNotContain("B,", arranged, StringComparison.Ordinal);
    }

    /// <summary>
    ///     ⚠ SK-FUZZ-0011: sorting a using block must not eat the trivia between its directives.
    /// </summary>
    /// <remarks>
    ///     The fuzzer reported this as an arrangement-idempotency violation on a generated file, and the
    ///     symptom hid how ordinary the input is. Two usings that both bind, a comment between them, no
    ///     removal in play: <c>Renormalise</c> blanked the leading trivia of every directive except the
    ///     first, so sorting deleted the comment. This is the plain statement of it, without a fuzzer in
    ///     the way.
    /// </remarks>
    [Fact]
    public void SortingUsings_KeepsTheCommentBetweenThem() {
        var arranged = Arrange(
            """
            using System.Text;
            // keep me
            using System.Collections;

            namespace P;
            public class C {
                public StringBuilder B() => new();
                public Hashtable H() => new();
            }
            """,
            ArrangeIds.Usings
        );

        Assert.Contains("// keep me", arranged, StringComparison.Ordinal);
        Assert.Contains("using System.Collections;", arranged, StringComparison.Ordinal);
        Assert.Contains("using System.Text;", arranged, StringComparison.Ordinal);
    }

    /// <summary>
    ///     ⚠ The same defect one step worse: what it deleted was a preprocessor directive.
    /// </summary>
    /// <remarks>
    ///     A comment is prose and losing it makes the file worse; an <c>#if</c> is structure and losing it
    ///     changes what compiles. Same line of code, and this is the half that says why it mattered.
    /// </remarks>
    [Fact]
    public void SortingUsings_KeepsAPreprocessorDirectiveBetweenThem() {
        var arranged = Arrange(
            """
            using System.Text;
            #if NEVER_DEFINED
            #endif
            using System.Collections;

            namespace P;
            public class C {
                public StringBuilder B() => new();
                public Hashtable H() => new();
            }
            """,
            ArrangeIds.Usings
        );

        Assert.Contains("#if NEVER_DEFINED", arranged, StringComparison.Ordinal);
        Assert.Contains("#endif", arranged, StringComparison.Ordinal);
    }

    /// <summary>
    ///     ⚠ And the header still re-pins to the front, which is what <c>Renormalise</c> is for.
    /// </summary>
    /// <remarks>
    ///     The fix must not be "keep every directive's trivia where it was" — that is the bug this method
    ///     was written to prevent, with the licence header stranded in the middle of the block. The header
    ///     rides to whatever sorts first and the directive it came from surrenders it, so it is emitted
    ///     once.
    /// </remarks>
    [Fact]
    public void SortingUsings_LeavesTheFileHeaderAtTheTop() {
        var arranged = Arrange(
            """
            // Copyright the author.
            using System.Text;
            using System.Collections;

            namespace P;
            public class C {
                public StringBuilder B() => new();
                public Hashtable H() => new();
            }
            """,
            ArrangeIds.Usings
        );

        Assert.StartsWith("// Copyright the author.", arranged, StringComparison.Ordinal);
        Assert.Equal(
            1,
            arranged.Split("// Copyright the author.").Length - 1
        );
    }

    /// <summary>
    ///     ⚠ SK-FUZZ-0013. Which rules fire may not depend on how the author spaced a dotted name.
    /// </summary>
    /// <remarks>
    ///     The removable-usings set is Roslyn's <c>CS8019</c> keyed by <c>Name.ToString()</c>, and that
    ///     carries the trivia <em>between</em> a qualified name's tokens — so
    ///     <c>using  System .Text;</c> keyed as <c>"System .Text"</c>. The set is computed once, before
    ///     the pipeline, and the formatter rewrites exactly that spacing on its first pass: the removal
    ///     was offered on pass 1, could no longer match its own key on pass 2, and the *next* pipeline
    ///     run — which recomputes the set — removed a using the first had left. That is
    ///     <c>pipeline(pipeline(x)) ≠ pipeline(x)</c> decided by whitespace.
    ///     <para>
    ///         ⚠ Both spellings are asserted, not just the spaced one. A key that normalised only on the
    ///         way in, or only on the way out, would pass one of these two and fail the other.
    ///     </para>
    /// </remarks>
    [Theory]
    [InlineData("using System.Threading.Tasks;")]
    [InlineData("using  System .Threading. Tasks;")]
    public void AnUnusedUsing_IsRemovedWhateverTheAuthorPutBetweenItsDots(string directive) {
        // ⚠ The minimised reproduction, and the odd shape is load-bearing rather than incidental.
        // A tidier case does not fail: the pipeline's first pass removes the using, the spelling
        // never gets a chance to change, and the stale key is never consulted. What is needed is a
        // first pass that *tries* the removal and is thrown away — here `NamespaceBodyRule` and the
        // removal together make the re-bind report `CS1027: #endif directive expected`, so safety
        // layer 2 reverts the whole arrangement — after which the formatter rewrites the name's
        // spacing and pass 2 can no longer match the set computed before pass 1.
        //
        // ⚠ Committed as `pathological/unused-using-whose-name-carries-spaces.cs` too, but that set
        // is not in `Corpus.Arrangeable()`, so the corpus copy documents the case and this asserts
        // it.
        var source = directive
            + "\n   namespace  Fuzz . N1 {\n#if true\n   public sealed  readonly struct T10 {  \n   }\n   }\n#endif";

        // ⚠ Through the *pipeline*, twice, and not through one `Arranger.Arrange`. A single arrange
        // computes the removable set and consumes it against the same tree, so the two spellings
        // agree by construction and the defect is invisible.
        var first = Pipeline(source);
        var second = Pipeline(first.Text);
        Assert.True(
            second.Edits.IsEmpty,
            "arrange-and-format is not a fixed point of itself; the second pass still wants "
            + $"{second.Edits.Length} edit(s): {string.Join(", ", second.Edits.Take(3))}"
        );
    }

    /// <summary>
    ///     ⚠ SK-FUZZ-0018. The removable-usings set is an answer about a text, and the pipeline rewrites
    ///     that text.
    /// </summary>
    /// <remarks>
    ///     SK-FUZZ-0013 was this defect's <em>key</em> half — the set keyed on a spelling the formatter
    ///     was about to change. This is its <em>timing</em> half, and the key being stable does not
    ///     touch it: the set is computed once, before pass 1, and a rule then makes a directive
    ///     removable that was not removable when the question was asked. Pass 2 of the same run reuses
    ///     the stale answer and converges; the caller who feeds that output back in recomputes, and
    ///     removes what the first run had only moved. <c>pipeline(pipeline(x)) ≠ pipeline(x)</c>.
    ///     <para>
    ///         ⚠ Two shapes, and the second is why the fix is not "refuse the move". The first is
    ///         SK-FUZZ-0018's minimised finding — a
    ///         <c>using System;</c> written after a file-scoped namespace declaration is *inside* it,
    ///         where it is the thing that binds <c>Console</c>; hoisted out by
    ///         <c>csharp_using_directive_placement = outside_namespace</c> it duplicates the implicit
    ///         <c>global using System;</c> and Roslyn answers <c>CS8933</c> and then <c>CS8019</c>. The
    ///         second has no namespace boundary anywhere in it — <see cref="EmptyStringRule" /> rewrites
    ///         <c>String.Empty</c> to <c>""</c> and *that* is what leaves the using unused. A fix that
    ///         taught <see cref="UsingsRule" /> not to move a directive across the namespace boundary
    ///         would pass the first of these and fail the second.
    ///     </para>
    ///     <para>
    ///         ⚠ The last assertion is the one that keeps the fix honest. Refusing to arrange makes the
    ///         pipeline a fixed point too — of a file it declined to touch — so the property alone would
    ///         be satisfied by a regression. The directive has to be *gone*.
    ///     </para>
    /// </remarks>
    [Theory]
    [InlineData(
        true,
        """
        namespace Serilog
          .Configuration;
        using System;
        public class Foo {
          void M() {
          Console.WriteLine(1);
          }
        }
        """
    )]
    [InlineData(
        false,
        """
        using System;

        public class Foo {
            public string M() => String.Empty;
        }
        """
    )]
    public void AUsingAnEarlierPassMadeRedundant_GoesInThatSameRun(bool implicitUsings, string source) {
        var first = Pipeline(source, implicitUsings);
        Assert.True(first.Converged, "the first run did not reach a fixed point.");

        var second = Pipeline(first.Text, implicitUsings);
        Assert.True(
            second.Edits.IsEmpty,
            "arrange-and-format is not a fixed point of itself; the second pass still wants "
            + $"{second.Edits.Length} edit(s): {string.Join(", ", second.Edits.Take(3))}\n"
            + $"first  ⇒\n{first.Text}\nsecond ⇒\n{second.Text}"
        );

        Assert.DoesNotContain("using System;", first.Text, StringComparison.Ordinal);
    }

    /// <summary>
    ///     ⚠ #292, as an assertion rather than an open issue: <c>SK0210</c> does see a file-level
    ///     <c>using</c> that a <c>global using</c> duplicates.
    /// </summary>
    /// <remarks>
    ///     The issue reports that this shape is <c>CS8933</c> and that <c>CS8019</c> is silent, which
    ///     would mean <see cref="UsingsRule.Unused" /> — whose filter reads <c>CS8019</c> alone —
    ///     cannot see it, and that the fix is to add <c>CS8933</c> to that filter. **It does not
    ///     reproduce.** Roslyn reports <c>CS8019</c> alongside <c>CS8933</c> in every shape measured,
    ///     so the name is in the removal set already and adding <c>CS8933</c> is a strict no-op.
    ///     <para>
    ///         ⚠ The <c>Unused</c> assertion is what makes this a test of the filter rather than of
    ///         the pipeline. Asserting only that the directive is gone from the output would stay green
    ///         if some later rule removed it for an unrelated reason, which is the shape of the defect
    ///         SK-FUZZ-0018 was: the answer arriving from somewhere other than where it was asked for.
    ///     </para>
    ///     <para>
    ///         ⚠ The ordering case is here because #292 asked for it. Removing the duplicated directive
    ///         must leave the survivors sorted and must not disturb them; two unrelated usings, given
    ///         out of order, come back in order with the redundant one gone.
    ///     </para>
    ///     <para>
    ///         ⚠
    ///         <b>
    ///             Sabotaged twice, and the first sabotage stayed green — which is the refutation
    ///             restated as an experiment.
    ///         </b> Swapping the filter from <c>CS8019</c> to <c>CS8933</c>
    ///         leaves this test passing, because on this shape the two diagnostics land on the same
    ///         directive and either one puts the name in the set. That is precisely why adding
    ///         <c>CS8933</c> to the filter is a no-op rather than a fix. Making the filter match
    ///         <em>neither</em> turns the test red at the <c>Unused</c> assertion, so it is not passing
    ///         vacuously.
    ///     </para>
    /// </remarks>
    [Fact]
    public void AUsingDuplicatedByAGlobalUsing_IsSeenByTheCs8019FilterAndRemoved() {
        const string source = """
                              using System.Text;
                              using System.Xml;
                              using System.Reflection;

                              public class Probe {
                                  public StringBuilder A() => new();

                                  public XmlDocument B() => new();

                                  public Assembly D() => typeof(Probe).Assembly;
                              }
                              """;

        const string path = "/arrangement/Probe.cs";
        var text = SourceText.From(source);
        var tree = CSharpSyntaxTree.ParseText(
            text,
            CSharpFormatter.ParseOptions,
            path,
            TestContext.Current.CancellationToken
        );
        var compilation = CSharpCompilation.Create(
            "probe",
            [
                CSharpSyntaxTree.ParseText(
                    SourceText.From("global using global::System.Text;"),
                    CSharpFormatter.ParseOptions,
                    "GlobalUsings.g.cs",
                    TestContext.Current.CancellationToken
                ),
                tree
            ],
            SharedFrameworkReferences.Value,
            new(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true)
        );

        var model = compilation.GetSemanticModel(tree);

        // ⚠ The compiler says both things about one directive, which is the whole refutation.
        var ids = model.GetDiagnostics(null, TestContext.Current.CancellationToken)
            .Where(static d => d.Id is "CS8019" or "CS8933")
            .Select(static d => d.Id)
            .ToHashSet(StringComparer.Ordinal);

        Assert.Contains("CS8933", ids);
        Assert.Contains("CS8019", ids);

        var unused = UsingsRule.Unused(model, tree, TestContext.Current.CancellationToken);
        Assert.Contains("System.Text", unused);

        var options = OptionResolver.Resolve(
            Path.Combine(Rikarin.Skala.Testing.Corpus.RepositoryRoot, "Probe.cs")
        ).Options;

        var arranged = ArrangementPipeline.Run(
            path,
            text,
            new(options),
            new(options),
            compilation,
            unused,
            cancellation: TestContext.Current.CancellationToken
        ).Text;

        Assert.DoesNotContain("using System.Text;", arranged, StringComparison.Ordinal);
        Assert.Contains("using System.Reflection;\nusing System.Xml;", arranged, StringComparison.Ordinal);
    }

    /// <summary>
    ///     ⚠ #381: a using the compiler calls unnecessary is removable only if it is also well-formed.
    /// </summary>
    /// <remarks>
    ///     Roslyn reports <c>CS8019</c> for a directive that does not resolve, beside the
    ///     <c>CS0246</c>/<c>CS0234</c> that says why, so a filter on <c>CS8019</c> alone deleted every
    ///     row below. Each row carries an unused, resolvable <c>using System.Text;</c> and a used
    ///     <c>using System;</c> in the same file, and asserts both, because a fix that simply stopped
    ///     removing would pass every "kept" assertion: the removal has to stay live in the very
    ///     compilation that keeps the broken directive.
    ///     <para>
    ///         ⚠ The <c>Unused</c> assertion is the one that can fail for the <c>global</c> and
    ///         <c>static</c> rows. <c>UsingsRule.IsRemovable</c> already refuses both shapes, so the
    ///         output keeps them whatever the set says; the set is still asserted, because it is the
    ///         compiler-facing half and the next caller of <c>Unused</c> may not have that guard.
    ///     </para>
    ///     <para>
    ///         ⚠ The compilation's references are <see cref="SharedFrameworkReferences" />, which is
    ///         exactly what the loose loader binds against; the workspace half is asserted end to end in
    ///         <c>ArrangeCommandTests</c>.
    ///     </para>
    /// </remarks>
    [Theory]
    [InlineData("using Xyz.Alpha;", "Xyz.Alpha", "CS0246")]
    [InlineData("using System.DoesNotExist;", "System.DoesNotExist", "CS0234")]
    [InlineData("using A = Missing.Type;", "A=Missing.Type", "CS0246")]
    [InlineData("using static Missing.Statics;", "Missing.Statics", "CS0246")]
    [InlineData("global using Glob.Missing;", "Glob.Missing", "CS0246")]
    public void AnUnresolvableUsing_IsKept_WhileAnUnusedResolvableOneBesideItGoes(
        string directive,
        string key,
        string error
    ) {
        // `global using` must precede every other using, so it leads; the others follow `System`.
        var block = directive.StartsWith("global ", StringComparison.Ordinal)
            ? directive + "\nusing System;\nusing System.Text;"
            : "using System;\nusing System.Text;\n" + directive;
        var source = block + "\n\npublic class Probe {\n    public void M() => Console.WriteLine();\n}\n";

        var (model, tree) = Bind(source);
        var ids = model.GetDiagnostics(null, TestContext.Current.CancellationToken)
            .Select(static d => d.Id)
            .ToHashSet(StringComparer.Ordinal);

        // The premise, measured rather than assumed: the compiler calls the broken directive both
        // unnecessary and unresolvable. If a Roslyn update stops saying CS8019 here, this row is no
        // longer testing the second predicate and should say so by failing.
        Assert.Contains("CS8019", ids);
        Assert.Contains(error, ids);

        var unused = UsingsRule.Unused(model, tree, TestContext.Current.CancellationToken);
        Assert.DoesNotContain(key, unused);
        Assert.Contains("System.Text", unused);
        Assert.DoesNotContain("System", unused);

        var arranged = Pipeline(source).Text;
        Assert.Contains(directive, arranged, StringComparison.Ordinal);
        Assert.Contains("using System;", arranged, StringComparison.Ordinal);
        Assert.DoesNotContain("using System.Text;", arranged, StringComparison.Ordinal);
    }

    /// <summary>
    ///     ⚠ #381's scope: the well-formedness predicate reads the directive's own span, not the file.
    /// </summary>
    /// <remarks>
    ///     A file that does not compile three methods down still has an answer about
    ///     <c>using System.Text;</c>. A predicate widened to "any error in the file" would pass every
    ///     row of <see cref="AnUnresolvableUsing_IsKept_WhileAnUnusedResolvableOneBesideItGoes" /> and
    ///     fail this.
    /// </remarks>
    [Fact]
    public void AnErrorElsewhereInTheFile_DoesNotStopAnUnusedResolvableUsingGoing() {
        const string source = """
                              using System;
                              using System.Text;

                              public class Probe {
                                  public void M() {
                                      Console.WriteLine();
                                      Undefined x = null;
                                  }
                              }

                              """;

        var (model, tree) = Bind(source);
        Assert.Contains(
            model.GetDiagnostics(null, TestContext.Current.CancellationToken),
            static d => d.Severity == DiagnosticSeverity.Error
        );

        var unused = UsingsRule.Unused(model, tree, TestContext.Current.CancellationToken);
        Assert.Equal(["System.Text"], unused);

        var arranged = Pipeline(source).Text;
        Assert.DoesNotContain("using System.Text;", arranged, StringComparison.Ordinal);
        Assert.Contains("using System;", arranged, StringComparison.Ordinal);
    }

    static (SemanticModel Model, SyntaxTree Tree) Bind(string source) {
        var tree = CSharpSyntaxTree.ParseText(
            SourceText.From(source),
            CSharpFormatter.ParseOptions,
            "/arrangement/Probe.cs",
            TestContext.Current.CancellationToken
        );
        var compilation = CSharpCompilation.Create(
            "probe",
            [tree],
            SharedFrameworkReferences.Value,
            new(
                OutputKind.DynamicallyLinkedLibrary,
                allowUnsafe: true,
                nullableContextOptions: NullableContextOptions.Enable
            )
        );

        return (compilation.GetSemanticModel(tree), tree);
    }

    /// <summary>One arrange-and-format pipeline run over a loose source string.</summary>
    /// <param name="implicitUsings">
    ///     ⚠ Whether the compilation carries the SDK's <c>global using</c>s, as
    ///     <see cref="Rikarin.Skala.Testing.ArrangementDifferential.ImplicitUsings" /> spells them. Off because a
    ///     probe wants the narrowest compilation that answers the question; on, an explicit
    ///     <c>using System;</c> at compilation-unit level is redundant, which is the whole subject of
    ///     SK-FUZZ-0018 and invisible without it.
    /// </param>
    static PipelineResult Pipeline(string source, bool implicitUsings = false) {
        const string path = "/arrangement/Probe.cs";
        var text = SourceText.From(source);
        var tree = CSharpSyntaxTree.ParseText(text, CSharpFormatter.ParseOptions, path);
        var trees = implicitUsings
            ? (SyntaxTree[])[
                CSharpSyntaxTree.ParseText(
                    SourceText.From(Rikarin.Skala.Testing.ArrangementDifferential.ImplicitUsings),
                    CSharpFormatter.ParseOptions,
                    "GlobalUsings.g.cs"
                ),
                tree
            ]
            : [tree];

        var compilation = CSharpCompilation.Create(
            "probe",
            trees,
            SharedFrameworkReferences.Value,
            new(
                OutputKind.DynamicallyLinkedLibrary,
                allowUnsafe: true,
                nullableContextOptions: NullableContextOptions.Enable
            )
        );

        var options = OptionResolver.Resolve(
            Path.Combine(Rikarin.Skala.Testing.Corpus.RepositoryRoot, "Probe.cs")
        ).Options;

        return ArrangementPipeline.Run(
            path,
            text,
            new(options),
            new(options),
            compilation,
            UsingsRule.Unused(compilation.GetSemanticModel(tree), tree)
        );
    }

    /// <summary>
    ///     ⚠ SK-FUZZ-0012. A rule that throws costs its own rewrite, not the process.
    /// </summary>
    /// <remarks>
    ///     <c>Func&lt;int&gt; v = new () { … }</c> — a target-typed <c>new</c> whose target is a
    ///     <b>delegate</b> type — with a LINQ query in its object initializer makes Roslyn's own binder
    ///     throw <c>IndexOutOfRangeException</c> out of <c>SemanticModel.GetSymbolInfo</c>, on a node of
    ///     the model's own tree. <c>PredefinedTypeRule</c> makes that call and there is no version of it
    ///     that can know in advance which node will do it, so the tool's obligation is not to avoid the
    ///     throw but to survive it: the exception used to leave <c>Arranger.Arrange</c>, the pipeline
    ///     and the caller, which for <c>skala arrange</c> is the process and for the nightly fuzz run
    ///     was the whole run's report.
    ///     <para>
    ///         ⚠ Asserted as "returns", not as "arranges correctly". What the file should become is a
    ///         question about semantically invalid code and has no interesting answer; that the tool
    ///         answers at all is the property.
    ///     </para>
    ///     <para>
    ///         ⚠ The query is written without parentheses. The fuzzer's seed had them, and once
    ///         <c>SK0209</c> stopped declining a parenthesised query (#392) it removed them — so the file
    ///         had a rewrite, the safety re-bind ran, met the same binder throw and reverted the whole
    ///         file as designed (<see cref="ArrangementSafety" />). That is a different property; this
    ///         test is about the throw inside a rule, and the parentheses never reached the binder.
    ///     </para>
    /// </remarks>
    [Fact]
    public void ARuleThatThrows_CostsItsOwnRewriteAndNotTheProcess() {
        const string source = """
                              using System;
                              using System.Linq;

                              class C {
                                  void M() {
                                      Func<int> v = new () { P = from item in items select null };
                                  }
                              }
                              """;

        var arranged = Arrange(source);
        Assert.NotNull(arranged);
    }

    /// <summary>
    ///     ⚠ Regression, found by #397's sweep: a C# 14 <c>field</c>-backed get-only property can carry an
    ///     initializer, and an arrow has nowhere to keep one.
    /// </summary>
    /// <remarks>
    ///     The collapse wrote <c>=&gt; field = new Bag();</c>, which re-parses as an assignment inside the
    ///     arrow. It compiles, so neither safety layer saw it, and every read then replaced the value. The
    ///     control below is the same collapse on a property with no initializer, which must still happen,
    ///     so that this test cannot pass by the rule having stopped running.
    /// </remarks>
    [Fact]
    public void BodyStyle_LeavesAFieldBackedPropertyWithAnInitializerItsAccessorList() {
        var output = Declined(
            Attempt(
                """
                namespace P;
                public class Bag { }
                public class C {
                    public Bag Parameters { get => field; } = new Bag();

                    public int Count { get { return 1; } }
                }
                """,
                ArrangeIds.BodyStyle
            )
        );

        Assert.Contains("public Bag Parameters { get => field; } = new Bag();", output, StringComparison.Ordinal);
        Assert.DoesNotContain("=> field =", output, StringComparison.Ordinal);
        Assert.DoesNotContain("return 1;", output, StringComparison.Ordinal);
        Assert.Contains("=> 1;", output, StringComparison.Ordinal);
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
