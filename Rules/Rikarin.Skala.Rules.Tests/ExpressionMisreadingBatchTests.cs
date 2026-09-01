using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Rikarin.Skala.Rules.Correctness;
using Rikarin.Skala.Rules.Metadata;
using System.Collections.Immutable;

namespace Rikarin.Skala.Rules.Tests;

/// <summary>
///     Exact counts, the fix, and the two claims <c>SK2060</c>–<c>SK2064</c> rest on.
/// </summary>
/// <remarks>
///     <see cref="RuleFixtureTests" /> asks "at least one" on a positive fixture, which is the right
///     question for the shipping bar and the wrong one for a family whose characteristic defect is
///     firing twice on one expression, or firing on the rung above the one that repeats.
///     <para>
///         ⚠ <b>And it cannot see a crashed analyzer at all.</b> Roslyn turns an analyzer exception
///         into an <c>AD0001</c> diagnostic and produces nothing else, so a crash makes every positive
///         fixture fail — and every <em>negative</em> fixture pass, which is the larger half of this
///         batch and the half that decides whether the rules ship. #279:
///         <see cref="NoAnalyzerInThisBatchCrashed_OnAnyFixture" /> is the check the shared harness does
///         not do.
///     </para>
/// </remarks>
public sealed class ExpressionMisreadingBatchTests {
    static readonly ImmutableArray<DiagnosticAnalyzer> Analyzers = [
        new AssignmentInConditionAnalyzer(), new IdenticalOperandsAnalyzer(), new RepeatedConditionAnalyzer(),
        new MisleadingOperatorSequenceAnalyzer(), new NonShortCircuitBooleanAnalyzer()
    ];

    static readonly string[] Ids = ["SK2060", "SK2061", "SK2062", "SK2063", "SK2064"];

    public static TheoryData<RuleFixture> Fixtures {
        get {
            var data = new TheoryData<RuleFixture>();
            foreach (var fixture in RuleFixtures.All().Where(static fixture => Ids.Contains(fixture.RuleId))) {
                data.Add(fixture);
            }

            return data;
        }
    }

    [Theory]
    [MemberData(nameof(Fixtures))]
    public void Fixtures_FireExactlyOnceOrNotAtAll(RuleFixture fixture) {
        var findings = Findings(File.ReadAllText(fixture.Path), fixture.Path, fixture.RuleId);
        Assert.Equal(fixture.ShouldFire ? 1 : 0, findings.Length);
        Assert.All(
            findings,
            diagnostic => Assert.Equal(
                RuleCatalog.Get(fixture.RuleId).HasFix,
                diagnostic.Properties.ContainsKey(FixEdits.CountKey)
            )
        );
    }

    /// <summary>
    ///     ⚠ A crashed analyzer passes every negative fixture. This is the only thing that says so.
    /// </summary>
    [Theory]
    [MemberData(nameof(Fixtures))]
    public void NoAnalyzerInThisBatchCrashed_OnAnyFixture(RuleFixture fixture) {
        var source = File.ReadAllText(fixture.Path);
        var compilation = RuleFixtures.Compile(source, fixture.Path);
        var crashes = RuleFixtures.Analyze(compilation, Analyzers, TestContext.Current.CancellationToken)
            .Where(static diagnostic => diagnostic.Id == "AD0001")
            .ToArray();

        Assert.True(
            crashes.Length == 0,
            $"{fixture}: an analyzer threw and Roslyn swallowed it as AD0001, so this file proves nothing:\n"
            + string.Join("\n", crashes.Select(static d => "  " + d.GetMessage()))
        );
    }

    /// <summary>
    ///     ⚠ The claim that made <c>SK2060</c> drop its <c>ConditionalExpression</c> registration.
    /// </summary>
    /// <remarks>
    ///     Assignment binds looser than <c>?:</c>, so <c>x = flag = other ? a : b</c> parses as
    ///     <c>x = (flag = (other ? a : b))</c> and a ternary's condition can never be a bare
    ///     assignment. The first draft registered for it anyway, which is a guard that cannot fire —
    ///     indistinguishable in a green test run from a guard that works.
    /// </remarks>
    [Fact]
    public void ATernaryConditionCanNeverBeABareAssignment() {
        var tree = CSharpSyntaxTree.ParseText(
            "class C { bool M(bool flag, bool other) { var x = flag = other ? true : false; return x; } }",
            new CSharpParseOptions(LanguageVersion.Preview),
            cancellationToken: TestContext.Current.CancellationToken
        );

        var conditional = tree.GetRoot(TestContext.Current.CancellationToken)
            .DescendantNodes()
            .OfType<ConditionalExpressionSyntax>()
            .Single();

        Assert.IsNotType<AssignmentExpressionSyntax>(conditional.Condition);
        Assert.IsType<IdentifierNameSyntax>(conditional.Condition);

        // And the whole conditional is the *right-hand side* of the assignment, not its condition.
        Assert.IsType<AssignmentExpressionSyntax>(conditional.Parent);
    }

    [Theory]
    [InlineData("void M(bool a, bool b) { if (a = b) { } }", 1)]
    [InlineData("void M(bool a, bool b) { if ((a = b)) { } }", 0)]
    [InlineData("void M(bool a, bool b) { if (a == b) { } }", 0)]
    [InlineData("void M(bool a, bool b) { while (a = b) { } }", 1)]
    [InlineData("void M(bool a, bool b) { a |= b; if (a) { } }", 0)]
    public void SK2060_FiresOnlyWhereTheAssignmentIsTheWholeCondition(string member, int expected) =>
        Assert.Equal(expected, Findings("class C { " + member + " }", "test.cs", "SK2060").Length);

    /// <summary>
    ///     ⚠ <c>SK2063</c> reads trivia, so these differ in nothing else at all.
    /// </summary>
    [Theory]
    [InlineData("x =- 1;", 1)]
    [InlineData("x =+ 1;", 1)]
    [InlineData("x=- 1;", 1)]
    [InlineData("x = -1;", 0)]
    [InlineData("x = - 1;", 0)]
    [InlineData("x =-1;", 0)]
    [InlineData("x=-1;", 0)]
    [InlineData("x -= 1;", 0)]
    [InlineData("x =~ 1;", 0)]
    public void SK2063_IsDecidedEntirelyByTheSpacing(string statement, int expected) =>
        Assert.Equal(
            expected,
            Findings("class C { void M() { var x = 0; " + statement + " System.Console.Write(x); } }", "t.cs", "SK2063")
                .Length
        );

    [Theory]
    [InlineData("bool M(bool a, bool b) => a & b;", "&&")]
    [InlineData("bool M(bool a, bool b) => a | b;", "||")]
    public void SK2064_ReplacesOnlyTheOperatorToken(string member, string expected) {
        var finding = Assert.Single(Findings("class C { " + member + " }", "test.cs", "SK2064"));
        Assert.Equal(expected, finding.Properties[FixEdits.TextKey(0)]);
        Assert.Equal("1", finding.Properties[FixEdits.CountKey]);
        Assert.Equal("1", finding.Properties[FixEdits.LengthKey(0)]);
    }

    /// <summary>
    ///     ⚠ The precedence guard, stated as the thing that would go wrong without it.
    /// </summary>
    /// <remarks>
    ///     <c>a | b &amp; c</c> is <c>a | (b &amp; c)</c>. Swapping the <c>&amp;</c> alone would produce
    ///     <c>a | b &amp;&amp; c</c>, which is <c>(a | b) &amp;&amp; c</c> — a different program from a
    ///     fix the catalogue calls safe. Both halves are asserted: the regrouping is real, and the rule
    ///     declines.
    /// </remarks>
    [Fact]
    public void SK2064_DeclinesAMixedBitwiseExpression_BecauseTheFixWouldRegroupIt() {
        Assert.Empty(Findings("class C { bool M(bool a, bool b, bool c) => a | b & c; }", "t.cs", "SK2064"));
        Assert.Empty(Findings("class C { bool M(bool a, bool b, bool c) => a & b | c; }", "t.cs", "SK2064"));

        // The regrouping the guard exists to prevent, measured rather than assumed.
        var before = Parse("a | b & c");
        var after = Parse("a | b && c");
        Assert.Equal(SyntaxKind.BitwiseOrExpression, before.Kind());
        Assert.Equal(SyntaxKind.LogicalAndExpression, after.Kind());

        // ⚠ Parentheses pin the grouping, and then *both* operators come back — which is the guard
        // being a precedence guard rather than a blanket "any bitwise nesting" refusal. `a || (b & c)`
        // and `a | (b && c)` and `a || (b && c)` all mean what `a | (b & c)` meant.
        Assert.Equal(2, Findings("class C { bool M(bool a, bool b, bool c) => a | (b & c); }", "t.cs", "SK2064").Length);
    }

    /// <summary>
    ///     ⚠ The head-of-chain guard, and the case that is the only thing able to see it.
    /// </summary>
    /// <remarks>
    ///     Every <c>if</c> in a chain reaches the analyzer, so the walk starts only at the head. A
    ///     sabotage removing that guard stayed green until this test existed: with only two rungs
    ///     repeating, starting from a later rung finds nothing new and the counts agree either way.
    ///     Three rungs is where they diverge — the head sees two repeats, and a walk that also starts
    ///     at rung two sees a third, reporting the same mistake once per rung above it.
    /// </remarks>
    [Fact]
    public void SK2062_ReportsOneFindingPerRepeatedRung_NotOncePerChainHead() {
        const string source = """
            class C {
                void M(bool a) {
                    if (a) { A(); }
                    else if (a) { B(); }
                    else if (a) { D(); }
                }

                static void A() { }

                static void B() { }

                static void D() { }
            }
            """;

        Assert.Equal(2, Findings(source, "t.cs", "SK2062").Length);
    }

    [Fact]
    public void SK2062_PointsAtTheRepeatAndNotAtTheOriginal() {
        const string source = """
            class C {
                void M(bool a) {
                    if (a) { A(); }
                    else if (!a) { B(); }
                    else if (a) { D(); }
                }

                static void A() { }

                static void B() { }

                static void D() { }
            }
            """;

        var finding = Assert.Single(Findings(source, "t.cs", "SK2062"));

        // Zero-based: the third rung, not the first.
        Assert.Equal(4, finding.Location.GetLineSpan().StartLinePosition.Line);
    }

    /// <summary>
    ///     ⚠ Where <c>csc</c> already says it, measured rather than remembered.
    /// </summary>
    /// <remarks>
    ///     <c>SK2061</c>'s first draft examined the six comparison operators, on the strength of doc
    ///     08 § "The compiler already says it" recording that <c>CS1717</c>/<c>CS1718</c> "reach the
    ///     identifier spellings only". ⚠ <b>That sentence is wrong, and this theory is the
    ///     measurement that refutes it.</b> <c>CS1718</c> also covers <c>this.g == this.g</c>,
    ///     <c>b.v == b.v</c> and <c>Box.Which == Box.Which</c> — a member access to a <em>field</em>
    ///     is covered; only a <em>property</em> access is not, which is the example the sentence was
    ///     built from. Since this rule reports storage paths and never properties, the compiler
    ///     covered every comparison it could have made, and the operators were dropped.
    ///     <para>
    ///         The other half matters as much: the compiler says nothing at all about
    ///         <c>&amp;&amp;</c>, <c>&amp;</c>, <c>^</c>, <c>-</c>, <c>/</c> or <c>%</c>. That is
    ///         what is left of the rule, and it is why there still is one.
    ///     </para>
    /// </remarks>
    [Theory]
    [InlineData("bool M(int q) => q == q;", true)]
    [InlineData("bool M(int q) => q != q;", true)]
    [InlineData("bool M(int q) => q < q;", true)]
    [InlineData("bool M(int q) => q <= q;", true)]
    [InlineData("bool M(int q) => q > q;", true)]
    [InlineData("bool M(int q) => q >= q;", true)]
    [InlineData("int g; bool R() => this.g == this.g;", true)]
    [InlineData("bool N(Box b) => b.v == b.v;", true)]
    [InlineData("bool S() => Box.Which == Box.Which;", true)]
    [InlineData("bool T(string a) => a == a;", true)]
    [InlineData("bool V(Box b) => b == b;", true)]
    [InlineData("bool U(Box b) => b.Prop == b.Prop;", false)]
    [InlineData("bool W(bool q) => q && q;", false)]
    [InlineData("int X(int q) => q - q;", false)]
    [InlineData("int Y(int q) => q / q;", false)]
    [InlineData("int Z(int q) => q % q;", false)]
    [InlineData("int AA(int q) => q & q;", false)]
    [InlineData("int AB(int q) => q ^ q;", false)]
    public void TheCompilerCoversEveryComparisonSK2061CouldHaveMade(string member, bool compilerSaysIt) {
        const string scaffold =
            "class Box { public int v; public int Prop { get; set; } public static int Which; } class C { ";
        var source = scaffold + member + " }";
        var compilation = RuleFixtures.Compile(source, "t.cs");

        var covered = compilation.GetDiagnostics(TestContext.Current.CancellationToken)
            .Any(static d => d.Id is "CS1717" or "CS1718");

        Assert.Equal(compilerSaysIt, covered);

        // And where the compiler speaks, SK2061 stays silent — it no longer looks at those operators.
        if (compilerSaysIt) {
            Assert.Empty(Findings(source, "t.cs", "SK2061"));
        }
    }

    static ExpressionSyntax Parse(string expression) =>
        SyntaxFactory.ParseExpression(expression);

    static Diagnostic[] Findings(string source, string path, string ruleId) {
        var compilation = RuleFixtures.Compile(source, path);
        return RuleFixtures.Analyze(compilation, Analyzers, TestContext.Current.CancellationToken)
            .Where(diagnostic => diagnostic.Id == ruleId)
            .ToArray();
    }
}
