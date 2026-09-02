using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Rikarin.Skala.Rules.Correctness;
using System.Collections.Immutable;

namespace Rikarin.Skala.Rules.Tests;

/// <summary>
///     <c>SK2160</c>–<c>SK2164</c>: the clock, the missing time zone, the ambient culture, the wall-clock
///     duration and the assertion that changes the program.
/// </summary>
/// <remarks>
///     ⚠
///     <b>
///         The assertion this file exists for is the <c>AD0001</c> one, and it is here because
///         <see cref="RuleFixtureTests" /> does not make it.
///     </b> That harness filters every run down to
///     <c>diagnostic.Id == fixture.RuleId</c>, so an analyzer that throws produces no finding, and no
///     finding is exactly what a negative fixture asserts. A crashed analyzer therefore passes every
///     negative fixture it has — silently, and in the direction that decides whether a rule ships. The
///     positives would fail, but a rule whose positives fail is a rule somebody is already looking at;
///     the danger is the batch where the crash is conditional on a shape only the negatives contain.
/// </remarks>
public sealed class TimeAndDeterminismBatchTests {
    static readonly ImmutableArray<DiagnosticAnalyzer> Analyzers = [
        new StaticClockReadAnalyzer(), new UnspecifiedDateTimeKindAnalyzer(),
        new ImplicitDateParseCultureAnalyzer(), new WallClockElapsedAnalyzer(),
        new SideEffectInAssertionAnalyzer()
    ];

    static readonly ImmutableArray<string> Rules = ["SK2160", "SK2161", "SK2162", "SK2163", "SK2164"];

    public static TheoryData<RuleFixture> Fixtures {
        get {
            var data = new TheoryData<RuleFixture>();
            foreach (var fixture in RuleFixtures.All()) {
                if (Rules.Contains(fixture.RuleId)) {
                    data.Add(fixture);
                }
            }

            return data;
        }
    }

    /// <summary>
    ///     ⚠ No analyzer in this batch may throw on any of its own fixtures.
    /// </summary>
    /// <remarks>
    ///     Roslyn turns an analyzer exception into an <c>AD0001</c> diagnostic and carries on, so the run
    ///     stays green and the rule stays quiet. The message is asserted into the failure text because
    ///     <c>AD0001</c> alone names the analyzer and not the shape that broke it.
    /// </remarks>
    [Theory]
    [MemberData(nameof(Fixtures))]
    public void NoAnalyzerThrows(RuleFixture fixture) {
        var source = File.ReadAllText(fixture.Path);
        var produced = RuleFixtures.Analyze(
            RuleFixtures.Compile(source, fixture.Path),
            Analyzers,
            TestContext.Current.CancellationToken
        );

        var crashes = produced.Where(static d => d.Id == "AD0001").ToArray();
        Assert.True(
            crashes.Length == 0,
            $"{fixture}: an analyzer threw, which makes every negative fixture pass for the wrong "
            + $"reason:\n  {string.Join("\n  ", crashes.Select(static d => d.GetMessage()))}"
        );
    }

    /// <summary>
    ///     ⚠ The batch's own anti-vacuity check: the fixture set must actually exist.
    /// </summary>
    /// <remarks>
    ///     Every theory above passes with zero rows, and zero rows is what a renamed directory or a
    ///     mistyped id produces. This is the same shape as <c>verify_ledger.py</c>'s guard against an
    ///     empty catalogue.
    /// </remarks>
    [Fact]
    public void EveryRuleInTheBatch_HasFixturesOnBothSides() {
        var fixtures = RuleFixtures.All();
        foreach (var rule in Rules) {
            var positive = fixtures.Count(f => f.RuleId == rule && f.ShouldFire);
            var negative = fixtures.Count(f => f.RuleId == rule && !f.ShouldFire);
            Assert.True(positive > 0, $"{rule} has no positive fixture.");
            Assert.True(negative >= positive, $"{rule} has {positive} positive and {negative} negative.");
        }
    }
}
