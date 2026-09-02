using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Rikarin.Skala.Rules.Correctness;
using System.Collections.Immutable;

namespace Rikarin.Skala.Rules.Tests;

/// <summary>
///     The batch level for <c>SK2170</c>–<c>SK2174</c>: what the fixture harness cannot ask.
/// </summary>
/// <remarks>
///     ⚠ <b>A crashed analyzer passes every negative fixture.</b> Roslyn catches the exception, reports
///     it as <c>AD0001</c>, and the analyzer then produces nothing at all — so the positives fail,
///     which reads as "the rule needs another condition", and every "should not fire" fixture passes,
///     which reads as a spotless false-positive record. The fixture harness does not look for
///     <c>AD0001</c> (issue #279) and <c>skala check</c> records it only in the SARIF's
///     <c>toolExecutionNotifications</c> without failing the gate (issue #295), so these tests do.
///     <para>
///         This batch has a specific reason to worry about it. Four of the five rules read *text* rather
///         than structure — leading whitespace, a token's raw spelling, the characters of a literal —
///         and every one of them does arithmetic on source offsets. An index off the end is the failure
///         mode, and it is invisible from the fixtures alone.
///     </para>
/// </remarks>
public sealed class MisreadShapeBatchTests {
    static readonly ImmutableArray<DiagnosticAnalyzer> Analyzers = [
        new MisleadingBodyIndentationAnalyzer(), new VariableLengthHexEscapeAnalyzer(),
        new ForgivenIsOperandAnalyzer(), new NegatedEmptyPatternAnalyzer(),
        new UnparenthesisedPrecedenceMixAnalyzer()
    ];

    static readonly string[] Ids = ["SK2170", "SK2171", "SK2172", "SK2173", "SK2174"];

    public static TheoryData<string> Fixtures {
        get {
            var data = new TheoryData<string>();
            foreach (var fixture in RuleFixtures.All()) {
                if (Ids.Contains(fixture.RuleId, StringComparer.Ordinal)) {
                    data.Add(fixture.Path);
                }
            }

            return data;
        }
    }

    /// <summary>Every fixture in the batch, asserting only that no analyzer threw.</summary>
    [Theory]
    [MemberData(nameof(Fixtures))]
    public void NoFixture_CrashesAnAnalyzer(string path) {
        var diagnostics = RuleFixtures.Analyze(
            RuleFixtures.Compile(File.ReadAllText(path), path),
            Analyzers,
            TestContext.Current.CancellationToken
        );

        Assert.DoesNotContain(diagnostics, static diagnostic => diagnostic.Id == "AD0001");
    }

    /// <summary>
    ///     ⚠ Anti-vacuity for the test above: an analyzer set that never runs also never crashes.
    /// </summary>
    [Fact]
    public void TheFixtureSet_ReallyReachesEveryRuleInTheBatch() {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var fixture in RuleFixtures.All()) {
            if (!fixture.ShouldFire || !Ids.Contains(fixture.RuleId, StringComparer.Ordinal)) {
                continue;
            }

            foreach (var diagnostic in RuleFixtures.Analyze(
                         RuleFixtures.Compile(File.ReadAllText(fixture.Path), fixture.Path),
                         Analyzers,
                         TestContext.Current.CancellationToken
                     )) {
                seen.Add(diagnostic.Id);
            }
        }

        Assert.Equal(Ids.Order(StringComparer.Ordinal), seen.Order(StringComparer.Ordinal));
    }

    /// <summary>
    ///     ⚠ <c>SK2172</c> against <c>SK2111</c>, on source that satisfies <b>both</b> rules' shapes.
    /// </summary>
    /// <remarks>
    ///     ⚠ A test showing that two rules match different shapes proves nothing about disjointness: the
    ///     shapes differ whether or not either rule looks at them. The only file that tests it is one
    ///     both rules would report if neither declined, and every <c>!</c> below is one. <c>SK2111</c>
    ///     owns the two — warnings off at the position, and a non-nullable value operand — and
    ///     <c>SK2172</c> declines exactly those, which is the only reason it is a semantic rule at all.
    /// </remarks>
    [Fact]
    public void EveryForgivenIsThatSK2111Owns_IsDeclinedBySK2172() {
        const string source = """
                              #nullable disable
                              class WarningsOff {
                                  bool Test(object value) => value! is string;
                              }
                              #nullable restore

                              class ValueOperand {
                                  bool Test(int count) => count! is object;
                              }
                              """;

        var produced = RuleFixtures
            .Analyze(RuleFixtures.Compile(source, "disjoint.cs"), Both(new InertNullSuppressionAnalyzer()),
                TestContext.Current.CancellationToken)
            .ToArray();

        Assert.Equal(2, produced.Count(static d => d.Id == "SK2111"));
        Assert.DoesNotContain(produced, static d => d.Id == "SK2172");
    }

    /// <summary>
    ///     ⚠ <c>SK2174</c> against <c>SK2064</c>, on source carrying both shapes on one operator each.
    /// </summary>
    /// <remarks>
    ///     A comparison operand under <c>&amp;</c> only compiles when every operand is <c>bool</c>, which
    ///     is <c>SK2064</c>'s subject; an arithmetic operand is never <c>bool</c>, which is
    ///     <c>SK2174</c>'s. The two are disjoint by construction and this pins it on the one file where
    ///     both are present.
    /// </remarks>
    [Fact]
    public void TheBooleanAndTheIntegralAnd_AreReportedByExactlyOneRuleEach() {
        const string source = """
                              class C {
                                  bool Boolean(bool a, bool b, bool c) => a & b == c;

                                  int Integral(int mask, int offset) => mask & offset + 1;
                              }
                              """;

        var produced = RuleFixtures
            .Analyze(RuleFixtures.Compile(source, "operators.cs"), Both(new NonShortCircuitBooleanAnalyzer()),
                TestContext.Current.CancellationToken)
            .Where(static d => d.Id is "SK2064" or "SK2174")
            .ToArray();

        Assert.Equal(["SK2064", "SK2174"], produced.Select(static d => d.Id).Order(StringComparer.Ordinal));
        Assert.Equal(
            2,
            produced.Select(static d => d.Location.GetLineSpan().StartLinePosition.Line).Distinct().Count()
        );
    }

    /// <summary>
    ///     The text shapes four of these five rules do offset arithmetic over, in one compilation.
    /// </summary>
    /// <remarks>
    ///     ⚠ The assertion is deliberately not about what is reported. Each of these is source the rules
    ///     have to survive rather than source they have to judge, and pinning the verdicts here would
    ///     turn a robustness test into a second copy of the fixtures.
    /// </remarks>
    [Fact]
    public void DegenerateTextShapes_DoNotCrashAnAnalyzer() {
        const string source = """"
                              using System;

                              class Degenerate {
                                  string Empty = "";
                                  string TrailingBackslashEscape = "\\";
                                  string LoneEscapeAtTheEnd = "abc\n";
                                  string Raw = """ \x1 """;
                                  string Interpolated = $"{1}{2}";
                                  char Quote = '\'';

                                  int Shifted(int a, int b) => a << b >> a & b | a ^ b;

                                  bool Patterns(object? value) =>
                                      value is not { } || value is { } and not string || value is not { Length: 0 };

                                  void Layout(bool flag) {
                                      if (flag)
                                          Console.WriteLine();
                                  }

                                  void OneLiner(bool flag) { if (flag) Console.WriteLine(); Console.WriteLine(); }
                              }
                              """";

        var diagnostics = RuleFixtures.Analyze(
            RuleFixtures.Compile(source, "degenerate.cs"),
            Analyzers,
            TestContext.Current.CancellationToken
        );

        Assert.DoesNotContain(diagnostics, static diagnostic => diagnostic.Id == "AD0001");
    }

    static ImmutableArray<DiagnosticAnalyzer> Both(DiagnosticAnalyzer neighbour) => Analyzers.Add(neighbour);
}
