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
        bool aggressive = false,
        string? only = null,
        bool removeUnused = false,
        IReadOnlyList<KeyValuePair<string, string>>? overrides = null
    ) {
        var result = Attempt(source, aggressive, only, removeUnused, overrides);
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
        bool aggressive = false,
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
            new CSharpCompilationOptions(
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
            new ArrangementOptions(options, ArrangementScope.Full, aggressive),
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
            diagnostic => diagnostic.Id is ArrangeIds.Reverted or ArrangeIds.SymbolChanged
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
            only: ArrangeIds.NullCheckingPattern
        );

        Assert.Contains("s is not null", output, StringComparison.Ordinal);
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
    ///     <c>resharper_empty_string = string_empty</c> — across the export, the canonical distribution
    ///     and doc 06 together, which is what makes it a decision rather than drift — the test went red
    ///     reporting a rule regression that had not happened. The rule was correctly disabled.
    /// </summary>
    [Fact]
    public void EmptyString_BecomesTheLiteral_UnderEmptyLiteral() {
        var output = Arrange(
            EmptyStringProbe,
            only: ArrangeIds.EmptyString,
            overrides: [new KeyValuePair<string, string>("resharper_empty_string", "empty_literal")]
        );

        Assert.DoesNotContain(".Empty", output, StringComparison.Ordinal);
        Assert.Contains("\"\"", output, StringComparison.Ordinal);
    }

    /// <summary>
    ///     The other direction of the same key, which is the setting in force in this repository.
    /// </summary>
    /// <remarks>
    ///     ⚠ <c>string_empty</c> is not the inverse rewrite — Skala has no <c>""</c> ⇒
    ///     <c>string.Empty</c> arrangement — it is the rule declining to run. Asserting that here is
    ///     what stops the pinned test above from being the only witness: with only the pinned case, a
    ///     rule that fired regardless of the option would still be green.
    /// </remarks>
    [Fact]
    public void EmptyString_IsLeftAlone_UnderStringEmpty() {
        var output = Arrange(
            EmptyStringProbe,
            only: ArrangeIds.EmptyString,
            overrides: [new KeyValuePair<string, string>("resharper_empty_string", "string_empty")]
        );

        Assert.Contains("string.Empty", output, StringComparison.Ordinal);
        Assert.DoesNotContain("\"\"", output, StringComparison.Ordinal);
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
            only: ArrangeIds.ArgumentStyle
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
            only: ArrangeIds.ArgumentStyle
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
                only: ArrangeIds.ObjectCreation
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
                only: ArrangeIds.ObjectCreation
            )
        );

        Assert.Contains("_ = new(pattern, options);", arranged, StringComparison.Ordinal);
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
            only: ArrangeIds.NamespaceBody
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
                only: ArrangeIds.NamespaceBody
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
            only: ArrangeIds.NamespaceBody
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
            only: ArrangeIds.RedundantParentheses
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
            only: ArrangeIds.RedundantParentheses
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
            only: ArrangeIds.TrailingComma
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
            only: ArrangeIds.Usings
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
            only: ArrangeIds.Usings
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
            only: ArrangeIds.Usings
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
    ///         ⚠ Two shapes, and the second is why the fix is not "refuse the move". The first is the
    ///         minimised finding, committed as
    ///         <c>pathological/open/using-inside-a-wrapped-file-scoped-namespace.cs</c>: a
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
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true)
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
            new PhaseOneOptions(options),
            new ArrangementOptions(options),
            compilation,
            unused,
            cancellation: TestContext.Current.CancellationToken
        ).Text;

        Assert.DoesNotContain("using System.Text;", arranged, StringComparison.Ordinal);
        Assert.Contains("using System.Reflection;\nusing System.Xml;", arranged, StringComparison.Ordinal);
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
            new CSharpCompilationOptions(
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
            new PhaseOneOptions(options),
            new ArrangementOptions(options),
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
    /// </remarks>
    [Fact]
    public void ARuleThatThrows_CostsItsOwnRewriteAndNotTheProcess() {
        const string source = """
                              using System;
                              using System.Linq;

                              class C {
                                  void M() {
                                      Func<int> v = new () { P = (from item in items select null) };
                                  }
                              }
                              """;

        var arranged = Arrange(source);
        Assert.NotNull(arranged);
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
