using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Rikarin.Skala.Rules.Correctness;
using System.Collections.Immutable;

namespace Rikarin.Skala.Rules.Tests;

/// <summary>
///     The switch, enum and type-check batch: <c>SK2120</c> and <c>SK2121</c>.
/// </summary>
/// <remarks>
///     ⚠
///     <b>
///         Three of the five issues this batch was opened for are refuted here rather than shipped,
///         and the refutations are tests rather than prose.
///     </b> Issue #29 (an unreachable switch arm) and
///     the always-false half of issue #1 (a constant type check) are compiler diagnostics — several
///     of them errors — and the switch-expression half of issue #28 is <c>CS8524</c>. A claim of the
///     form "the compiler already covers this" is worth nothing written down: the tests below make the
///     compiler say it, so the day it stops being true this file goes red.
///     <para>
///         ⚠ <c>NoFixture_CrashesAnAnalyzer</c> is the one that decides whether anything else here
///         means anything. Roslyn swallows an analyzer exception as <c>AD0001</c> and the analyzer then
///         produces nothing at all, so a crashed rule fails its positives and
///         <em>
///             passes every
///             negative
///         </em> — which reads in a report as a half-working rule rather than a dead one.
///     </para>
/// </remarks>
public sealed class EnumAndTypeCheckBatchTests {
    /// <summary>⚠ Named because #280 pushed the literal past SK7083's threshold in this file.</summary>
    const string EnumSwitch = "SK2009";

    static readonly ImmutableArray<DiagnosticAnalyzer> Analyzers = [
        new PlainEnumBitwiseAnalyzer(), new AlwaysSucceedingAsAnalyzer(),
        new EnumSwitchExhaustivenessAnalyzer(), new ConstantRangeComparisonAnalyzer(),
        new NonnegativeSizeComparisonAnalyzer()
    ];

    public static TheoryData<RuleFixture> Fixtures {
        get {
            var data = new TheoryData<RuleFixture>();
            foreach (var fixture in RuleFixtures.All()
                         .Where(static fixture => fixture.RuleId is "SK2120" or "SK2121")) {
                data.Add(fixture);
            }

            return data;
        }
    }

    [Fact]
    public void TheBatch_HasFixtures() {
        // Anti-vacuity: every theory below is satisfied by an empty set.
        var positive = RuleFixtures.All()
            .Count(static fixture => fixture.RuleId is "SK2120" or "SK2121" && fixture.ShouldFire);
        var negative = RuleFixtures.All()
            .Count(static fixture => fixture.RuleId is "SK2120" or "SK2121" && !fixture.ShouldFire);

        Assert.True(positive >= 11, $"Only {positive} positive fixture(s) were discovered for this batch.");
        Assert.True(negative >= positive, $"{negative} negative fixture(s) against {positive} positive.");
    }

    [Theory]
    [MemberData(nameof(Fixtures))]
    public void Fixtures_HaveExactCountsAndExpectedFixes(RuleFixture fixture) {
        var compilation = RuleFixtures.Compile(File.ReadAllText(fixture.Path), fixture.Path);
        var findings = Analyze(compilation).Where(diagnostic => diagnostic.Id == fixture.RuleId).ToArray();

        Assert.Equal(fixture.ShouldFire ? 1 : 0, findings.Length);
        Assert.All(
            findings,
            diagnostic => Assert.Equal(fixture.RuleId is "SK2121", diagnostic.Properties.ContainsKey(FixEdits.CountKey))
        );
    }

    /// <summary>
    ///     ⚠ A crashed analyzer produces nothing, so every negative fixture passes for the wrong
    ///     reason. The harness does not check this; this batch does.
    /// </summary>
    [Theory]
    [MemberData(nameof(Fixtures))]
    public void NoFixture_CrashesAnAnalyzer(RuleFixture fixture) {
        var crashes = Analyze(RuleFixtures.Compile(File.ReadAllText(fixture.Path), fixture.Path))
            .Where(static diagnostic => diagnostic.Id == "AD0001")
            .Select(static diagnostic => diagnostic.GetMessage())
            .ToArray();

        Assert.True(crashes.Length == 0, $"{fixture}: an analyzer threw:\n  {string.Join("\n  ", crashes)}");
    }

    /// <summary>
    ///     ⚠ <b>Issue #29, refuted.</b> "The switch arm is unreachable given the value's range" is
    ///     already a compiler <em>error</em>: <c>CS8510</c> for an arm and <c>CS8120</c> for a case, with
    ///     <c>CS0031</c> where the constant does not even fit. Nothing that reaches this rule would
    ///     compile, so no id was allocated. What the compiler does not do is reason from a range the
    ///     <em>flow</em> proved rather than the type — and that needs the value lattice issue #169 asks
    ///     for, which this batch did not build.
    /// </summary>
    [Fact]
    public void TheCompilerOwns_TheUnreachableSwitchArm() {
        Assert.Contains(
            Compiler("class C { int M(byte b) => b switch { < 0 => 1, _ => 0 }; }"),
            static diagnostic => diagnostic.Id == "CS8510"
        );

        Assert.Contains(
            Compiler("class C { int M(byte b) { switch (b) { case > 255: return 1; default: return 0; } } }"),
            static diagnostic => diagnostic.Id == "CS8120"
        );

        // ⚠ And they are errors, not warnings, so the code never reaches a linter at all.
        Assert.Contains(
            Compiler("class C { int M(byte b) => b switch { < 0 => 1, _ => 0 }; }"),
            static diagnostic => diagnostic.Id == "CS8510" && diagnostic.Severity == DiagnosticSeverity.Error
        );

        // The flow-proven range is the residue, and nothing reports it today.
        const string flow = """
                            class C {
                                int M(int x) {
                                    if (x is >= 0 and <= 10) {
                                        switch (x) { case 20: return 1; default: return 0; }
                                    }

                                    return -1;
                                }
                            }
                            """;
        Assert.DoesNotContain(Compiler(flow), static diagnostic => diagnostic.Id is "CS8120" or "CS8510");
    }

    /// <summary>
    ///     ⚠ <b>Issue #1, refuted except for <c>SK2121</c>.</b> Every always-<em>false</em> type check
    ///     is a compiler diagnostic, and two of the four are errors. This is why the shipped rule is
    ///     about <c>as</c> and nothing else.
    /// </summary>
    [Theory]
    [InlineData("class B { } sealed class D : B { } sealed class U { } class C { bool M(D d) => d is U; }", "CS0184")]
    [InlineData("class C { bool M(string s) => s is int; }", "CS0184")]
    [InlineData("class C { bool M(int v) => v is int; }", "CS0183")]
    [InlineData("class B { } sealed class D : B { } sealed class U { } class C { U M(D d) => d as U; }", "CS0039")]
    [InlineData(
        "class B { } sealed class D : B { } sealed class U { } class C { int M(D d) => d switch { U => 1, _ => 0 }; }",
        "CS8121"
    )]
    public void TheCompilerOwns_TheConstantTypeCheck(string source, string expected) {
        Assert.Contains(Compiler(source), diagnostic => diagnostic.Id == expected);
    }

    /// <summary>
    ///     ⚠ <b>The always-true <c>is</c> check is not the compiler's and is still not SK2121's.</b>
    ///     <c>d is D</c> is <c>false</c> when <c>d</c> is null, so it is a null test rather than a
    ///     redundant type test, and calling it redundant would mean treating a nullable annotation as a
    ///     runtime guarantee — which <c>SK2001</c>'s rationale already refuses to do. <c>as</c> needs no
    ///     such assumption, which is exactly why it is the half that shipped.
    /// </summary>
    [Fact]
    public void TheAlwaysTrueIsCheck_IsReportedByNobody() {
        const string source = "class B { } sealed class D : B { } class C { bool M(D d) => d is D; }";
        Assert.DoesNotContain(Compiler(source), static d => d.Id is "CS0183" or "CS0184");
        Assert.DoesNotContain(Findings(source), static d => d.Id == "SK2121");

        // The language fact the exclusion rests on, asserted rather than described.
        object? nothing = null;
        Assert.False(nothing is object);
    }

    /// <summary>
    ///     ⚠ <b>Issue #28's switch-expression half is <c>CS8524</c>, measured here.</b> An expression
    ///     that handles every declared member and has no catch-all already draws "does not handle some
    ///     values of its input type" from the compiler — which is precisely the undeclared value the
    ///     issue is about. The statement half is what remains, and it is the exact shape that gave
    ///     <c>SK2009</c> its six false positives (#280): a <c>switch</c> used as a filter, where falling
    ///     through means "do nothing" and is correct. No id was allocated for it.
    /// </summary>
    [Fact]
    public void TheCompilerOwns_TheNonExhaustiveSwitchExpression() {
        const string exhaustiveNoDefault = """
                                           enum Color { Red, Green, Blue }

                                           class C {
                                               int M(Color c) => c switch { Color.Red => 1, Color.Green => 2, Color.Blue => 3 };
                                           }
                                           """;
        Assert.Contains(Compiler(exhaustiveNoDefault), static diagnostic => diagnostic.Id == "CS8524");

        // ⚠ And SK2009 is silent there, so the compiler is not doubling anything Skala says.
        Assert.DoesNotContain(Findings(exhaustiveNoDefault), static diagnostic => diagnostic.Id == EnumSwitch);

        // The statement form draws nothing from the compiler — and is the SK2009 false-positive shape.
        const string statement = """
                                 enum Color { Red, Green, Blue }

                                 class C {
                                     int M(Color c) {
                                         switch (c) { case Color.Red: return 1; case Color.Green: return 2; case Color.Blue: return 3; }

                                         return 0;
                                     }
                                 }
                                 """;
        Assert.DoesNotContain(Compiler(statement), static diagnostic => diagnostic.Id is "CS8524" or "CS8509");

        // ⚠ #280's stand-down, and the half of it that is free. A switch expression missing a
        // *declared* value draws CS8509, so SK2009 no longer registers for the form at all — and
        // the assertion is on both halves at once, because a rule that stopped running would pass
        // the second one on its own.
        const string incompleteExpression = """
                                            enum Color { Red, Green, Blue }

                                            class C {
                                                int M(Color c) => c switch { Color.Red => 1, Color.Green => 2 };
                                            }
                                            """;
        Assert.Contains(Compiler(incompleteExpression), static diagnostic => diagnostic.Id == "CS8509");
        Assert.DoesNotContain(Findings(incompleteExpression), static diagnostic => diagnostic.Id == EnumSwitch);

        // …and the same omission written as a statement is un-hosted and still reported, so the
        // stand-down is scoped to the form rather than having retired the rule.
        const string incompleteStatement = """
                                           enum Color { Red, Green, Blue }

                                           class C {
                                               int M(Color c) {
                                                   switch (c) { case Color.Red: return 1; case Color.Green: return 2; }

                                                   return 0;
                                               }
                                           }
                                           """;
        Assert.DoesNotContain(Compiler(incompleteStatement), static d => d.Id is "CS8524" or "CS8509");
        Assert.Single(Findings(incompleteStatement), static diagnostic => diagnostic.Id == EnumSwitch);
    }

    /// <summary>
    ///     ⚠ <b><c>SK2009</c> reports a switch statement only where it already covers most of the
    ///     enum</b> (#280).
    /// </summary>
    /// <remarks>
    ///     A <c>switch</c> statement is under no obligation to be exhaustive — falling out of it
    ///     continues at the next statement — so the shape worth reporting is the one that visibly meant
    ///     to list everything and missed a value, not the one selecting a few values out of many. The
    ///     boundary is <c>missing &lt;= handled</c> over distinct declared <em>values</em>.
    ///     <para>
    ///         ⚠ The two boundaries #280 offered instead are refuted here rather than argued away. "Every
    ///         arm produces the same value" does not hold: two of the twelve false positives on Skala's
    ///         own tree mix a <c>return</c> section with a section that assigns and breaks. A member-count
    ///         threshold does not hold either: the <c>JsonValueKind</c> walker is a filter over an
    ///         eight-member enum.
    ///     </para>
    /// </remarks>
    [Fact]
    public void SK2009_ReportsTheStatementThatMeantToBeExhaustive_AndNotTheFilter() {
        const string prefix = "enum Wide { A, B, C, D, E, F, G, H }\n\nclass C {\n    int M(Wide w) {\n        switch (w) {\n";
        const string suffix = "        }\n\n        return 0;\n    }\n}\n";

        // Five of eight handled, three missing: 3 <= 5, an attempt at exhaustiveness.
        const string majority = prefix
            + "            case Wide.A: case Wide.B: case Wide.C: case Wide.D: case Wide.E: return 1;\n"
            + suffix;
        Assert.Single(Findings(majority), static diagnostic => diagnostic.Id == EnumSwitch);

        // Four of eight handled, four missing: 4 <= 4, still an attempt. The boundary is inclusive
        // deliberately, and this is the case that pins which side of it the tie falls on.
        const string half = prefix + "            case Wide.A: case Wide.B: case Wide.C: case Wide.D: return 1;\n" + suffix;
        Assert.Single(Findings(half), static diagnostic => diagnostic.Id == EnumSwitch);

        // Three of eight handled, five missing: 5 > 3, a selection.
        const string minority = prefix + "            case Wide.A: case Wide.B: case Wide.C: return 1;\n" + suffix;
        Assert.DoesNotContain(Findings(minority), static diagnostic => diagnostic.Id == EnumSwitch);
    }

    /// <summary>
    ///     ⚠
    ///     <b>
    ///         <c>SK2120</c> is provably disjoint from <c>CA1027</c>, by arithmetic and not by a
    ///         filter.
    ///     </b> <c>CA1027</c> was probed against the SDK at <c>AnalysisMode=All</c>: it needs at
    ///     least three distinct non-zero values that are all powers of two, and it is silent on
    ///     <c>{ A, B, C }</c> and <c>{ A, B, C, D }</c>. Consecutive numbering from zero reaches a third
    ///     non-zero value only at <c>3</c>, which is not a power of two — so no declaration
    ///     <c>SK2120</c> accepts can satisfy <c>CA1027</c>. The arithmetic is asserted here because the
    ///     probe is not in the repository and the claim is what the rule's boundary rests on.
    /// </summary>
    [Fact]
    public void ConsecutiveNumbering_CanNeverSatisfyCa1027() {
        // ⚠ The first draft of this test asserted "fewer than three non-zero powers of two" and went
        // red at five members, where 1, 2 and 4 are all present. That was the assertion being wrong,
        // not the boundary: CA1027 needs three non-zero values that are *all* powers, and the moment
        // a consecutive enum has three non-zero values it has 3, which is not one.
        for (var members = 1; members <= 64; members++) {
            var nonZero = Enumerable.Range(0, members).Where(static value => value != 0).ToArray();
            var allPowers = nonZero.All(static value => (value & (value - 1)) == 0);

            Assert.True(
                nonZero.Length < 3 || !allPowers,
                $"A consecutively numbered enum of {members} members satisfies CA1027's shape, "
                + "so SK2120 and CA1027 would report one mistake twice."
            );
        }

        // The two halves of the boundary, each demonstrated once rather than only ruled out.
        Assert.Equal(2, Enumerable.Range(1, 2).Count()); // three members: only 1 and 2, too few values
        Assert.False((3 & 2) == 0); // four members onward: 3 is not a power of two
    }

    /// <summary>
    ///     ⚠ <b><c>SK2120</c> is not <c>SK2009</c>.</b> <c>SK2009</c> asks whether a <c>switch</c> lists
    ///     every declared member; this asks whether a bitwise operator was applied to members that are
    ///     not bits. One enum can produce both findings and neither implies the other, so both
    ///     directions are pinned.
    /// </summary>
    [Fact]
    public void SK2120_AndSK2009_AnswerDifferentQuestions() {
        const string combine = "enum Color { Red, Green, Blue } class C { Color M() => Color.Green | Color.Blue; }";
        Assert.Single(Findings(combine), static d => d.Id == "SK2120");
        Assert.DoesNotContain(Findings(combine), static d => d.Id == EnumSwitch);

        // ⚠ A statement, not an expression: #280 stood SK2009 down on the expression form, where
        // CS8509 already names the missing value. Written as an expression this snippet asserted
        // SK2009 fires and would have gone red on the stand-down.
        const string partialSwitch = """
                                     enum Color { Red, Green, Blue }

                                     class C {
                                         int M(Color c) {
                                             switch (c) { case Color.Red: return 1; case Color.Green: return 2; }

                                             return 0;
                                         }
                                     }
                                     """;
        Assert.Single(Findings(partialSwitch), static d => d.Id == EnumSwitch);
        Assert.DoesNotContain(Findings(partialSwitch), static d => d.Id == "SK2120");
    }

    /// <summary>
    ///     ⚠ <b><c>SK2121</c> is neither <c>SK2001</c> nor <c>SK2053</c>.</b> Those two fold a
    ///     comparison an integral <em>range</em> decides — the type's for <c>SK2001</c>, a count's
    ///     non-negativity for <c>SK2053</c>. This one folds a <em>conversion</em> the type hierarchy
    ///     decides, and there is no number in it. Both directions, because a rule that stands down too
    ///     eagerly is as wrong as one that double-reports.
    /// </summary>
    [Fact]
    public void SK2121_IsNotTheRangeRules() {
        const string widening = "class B { } sealed class D : B { } class C { B? M(D d) => d as B; }";
        Assert.Single(Findings(widening), static d => d.Id == "SK2121");
        Assert.DoesNotContain(Findings(widening), static d => d.Id is "SK2001" or "SK2053");

        const string range = "class C { bool M(byte v) => v >= 0; }";
        Assert.Single(Findings(range), static d => d.Id == "SK2001");
        Assert.DoesNotContain(Findings(range), static d => d.Id == "SK2121");

        const string size = "class C { bool M(int[] v) => v.Length >= 0; }";
        Assert.Single(Findings(size), static d => d.Id == "SK2053");
        Assert.DoesNotContain(Findings(size), static d => d.Id == "SK2121");
    }

    /// <summary>
    ///     ⚠ <b>The fix keeps the expression's static type, and that is not cosmetic.</b>
    ///     <c>var b = d as B;</c> declares a <c>B</c>; rewriting it to <c>d</c> would declare a
    ///     <c>D</c> and change what every member access below it resolves to. A widening therefore
    ///     becomes the matching cast, and only an identity conversion is replaced by the operand.
    /// </summary>
    [Fact]
    public void TheFix_PreservesTheStaticType() {
        var widening = Findings("class B { } sealed class D : B { } class C { B? M(D d) => d as B; }")
            .Single(static d => d.Id == "SK2121");
        Assert.Equal("(B)d", widening.Properties[FixEdits.TextKey(0)]);

        var identity = Findings("sealed class W { } class C { W? M(W w) => w as W; }")
            .Single(static d => d.Id == "SK2121");
        Assert.Equal("w", identity.Properties[FixEdits.TextKey(0)]);

        // ⚠ A cast binds tighter than `??`, so the operand keeps its own parentheses.
        var compound = Findings("class B { } sealed class D : B { } class C { B? M(D a, D b) => (a ?? b) as B; }")
            .Single(static d => d.Id == "SK2121");
        Assert.Equal("(B)(a ?? b)", compound.Properties[FixEdits.TextKey(0)]);
    }

    /// <summary>Generated code is nobody's to fix.</summary>
    [Theory]
    [InlineData("SK2120", "or-operator")]
    [InlineData("SK2121", "widening-to-base")]
    public void GeneratedCode_IsIgnored(string id, string name) {
        var source = "// <auto-generated/>\n"
            + File.ReadAllText(Path.Combine(RuleFixtures.Root, id, "positive", name + ".cs"));

        Assert.DoesNotContain(Analyze(RuleFixtures.Compile(source, "generated.cs")), d => d.Id == id);
    }

    /// <summary>A severity override reaches these rules like any other.</summary>
    [Theory]
    [InlineData("SK2120", "enum Color { Red, Green, Blue } class C { Color M() => Color.Green | Color.Blue; }")]
    [InlineData("SK2121", "class B { } sealed class D : B { } class C { B? M(D d) => d as B; }")]
    public void SeverityOverride_Applies(string id, string source) {
        var original = RuleFixtures.Compile(source, "test.cs");
        var raised = original.WithOptions(
            original.Options.WithSpecificDiagnosticOptions(
                new Dictionary<string, ReportDiagnostic>(StringComparer.Ordinal) { [id] = ReportDiagnostic.Error }
            )
        );

        Assert.Contains(
            Analyze(raised),
            diagnostic => diagnostic.Id == id && diagnostic.Severity == DiagnosticSeverity.Error
        );
    }

    static ImmutableArray<Diagnostic> Analyze(CSharpCompilation compilation) =>
        RuleFixtures.Analyze(compilation, Analyzers, TestContext.Current.CancellationToken);

    static ImmutableArray<Diagnostic> Findings(string source) => Analyze(RuleFixtures.Compile(source, "test.cs"));

    static ImmutableArray<Diagnostic> Compiler(string source) =>
        RuleFixtures.Compile(source, "test.cs").GetDiagnostics(TestContext.Current.CancellationToken);
}
