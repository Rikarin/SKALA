using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Rikarin.Skala.Rules.Correctness;
using Rikarin.Skala.Rules.Metadata;
using System.Collections.Immutable;

namespace Rikarin.Skala.Rules.Tests;

/// <summary>
///     Exact counts and exact fixes for the small locally-decidable correctness rules.
/// </summary>
/// <remarks>
///     <see cref="RuleFixtureTests" /> asks only "at least one" on a positive fixture, which is the
///     right question for the shipping bar and the wrong one for a rule whose defect would be firing
///     twice on the same expression. These assert the number.
/// </remarks>
public sealed class SmallCorrectnessBatchTests {
    static readonly ImmutableArray<DiagnosticAnalyzer> Analyzers = [
        new NanComparisonAnalyzer(), new FloatingPointEqualityAnalyzer(), new UnusedValueParameterAnalyzer()
    ];

    static readonly string[] Ids = ["SK2030", "SK2031"];

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
    public void Fixtures_HaveExactCountsAndTheDeclaredFix(RuleFixture fixture) {
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

    [Theory]
    [InlineData("bool M(double x) => x == double.NaN;", "double.IsNaN(x)")]
    [InlineData("bool M(double x) => x != double.NaN;", "!double.IsNaN(x)")]
    [InlineData("bool M(float x) => x == float.NaN;", "float.IsNaN(x)")]
    [InlineData("bool M(double a, double b) => a + b != double.NaN;", "!double.IsNaN(a + b)")]
    public void SK2030_RewritesToTheNegationTheOperatorImplies(string member, string expected) {
        var finding = Assert.Single(Findings("class C { " + member + " }", "test.cs", "SK2030"));
        Assert.Equal(expected, finding.Properties[FixEdits.TextKey(0)]);
    }

    /// <summary>
    ///     ⚠ The two floating-point rules do not overlap, and the reason is asymmetric.
    /// </summary>
    /// <remarks>
    ///     <c>SK2003</c> excludes a comparison whose constant side is a sentinel and names NaN as one of
    ///     them, so it is already silent on every shape <c>SK2030</c> reports — including the one where
    ///     the other operand is the floating-point arithmetic <c>SK2003</c> exists for. Asserting it here
    ///     keeps that a fact rather than a claim in a comment: the day the sentinel list changes, this is
    ///     what says the two rules started double-reporting.
    /// </remarks>
    [Fact]
    public void SK2003_IsSilentOnEverythingSK2030Reports() {
        const string source = "class C { bool M(double a, double b) => a / b == double.NaN; }";
        Assert.Single(Findings(source, "test.cs", "SK2030"));
        Assert.Empty(Findings(source, "test.cs", "SK2003"));
    }

    static Diagnostic[] Findings(string source, string path, string ruleId) =>
        RuleFixtures
            .Analyze(
                RuleFixtures.Compile(source, path),
                Analyzers,
                TestContext.Current.CancellationToken
            )
            .Where(diagnostic => diagnostic.Id == ruleId)
            .ToArray();
}
