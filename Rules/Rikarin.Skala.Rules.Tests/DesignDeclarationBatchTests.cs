using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Rikarin.Skala.Rules.Design;
using Rikarin.Skala.Rules.Metadata;
using System.Collections.Immutable;

namespace Rikarin.Skala.Rules.Tests;

/// <summary>
///     The four declaration-shaped design rules, with exact counts rather than "at least one".
/// </summary>
/// <remarks>
///     <see cref="RuleFixtureTests.Rule_FiresExactlyWhereTheFixtureSaysItShould" /> asks only whether a
///     positive fixture produced anything, and a rule that reports the same declaration twice — a
///     partial type visited once per part, a symbol action reporting every location — passes it while
///     doubling every count in a report.
/// </remarks>
public sealed class DesignDeclarationBatchTests {
    static readonly ImmutableArray<DiagnosticAnalyzer> Analyzers = [
        new EnumConstraintAnalyzer(), new ExceptionNameAnalyzer(), new TypeKindSuffixAnalyzer(),
        new EmptyTypeAnalyzer()
    ];

    public static TheoryData<RuleFixture> Fixtures {
        get {
            var data = new TheoryData<RuleFixture>();
            foreach (var fixture in RuleFixtures.All()
                         .Where(static fixture => fixture.RuleId is "SK6020" or "SK6021" or "SK6022" or "SK6023")) {
                data.Add(fixture);
            }

            return data;
        }
    }

    [Theory]
    [MemberData(nameof(Fixtures))]
    public void EveryFixture_ProducesExactlyTheCountItClaims(RuleFixture fixture) {
        var findings = Analyze(fixture);
        Assert.Equal(fixture.ShouldFire ? 1 : 0, findings.Length);
    }

    /// <summary>
    ///     ⚠ Three of the four ship with no fix, which is a decision and not an omission.
    /// </summary>
    /// <remarks>
    ///     docs/plan/08's bar is "a rule ships with a fix", and this batch is where that stopped being
    ///     unconditional: renaming a type is a solution-wide edit and deleting one is not mechanical,
    ///     so <c>SK6021</c>, <c>SK6022</c> and <c>SK6023</c> carry <c>hasFix: false</c>. Asserting it
    ///     here means a later commit cannot quietly attach an edit that guesses at intent without also
    ///     changing a test that says why there is none.
    /// </remarks>
    [Theory]
    [MemberData(nameof(Fixtures))]
    public void OnlyTheConstraintRule_CarriesAnEdit(RuleFixture fixture) {
        Assert.Equal(fixture.RuleId == "SK6020", RuleCatalog.Get(fixture.RuleId).HasFix);

        foreach (var diagnostic in Analyze(fixture)) {
            Assert.Equal(fixture.RuleId == "SK6020", diagnostic.Properties.ContainsKey(FixEdits.CountKey));
        }
    }

    /// <summary>
    ///     ⚠ A partial type has one symbol and several declarations, and <c>SK6021</c> reads the symbol.
    /// </summary>
    /// <remarks>
    ///     <c>ISymbol.Locations</c> holds one entry per part, so reporting them all would produce a
    ///     finding per file for a single naming mistake — and the fixture set could not catch it,
    ///     because a fixture is one file.
    /// </remarks>
    [Fact]
    public void APartialTypeNamedLikeAnException_IsReportedOnce() {
        const string source = """
                              public partial class BrokenPipeException {
                                  public int Code { get; init; }
                              }

                              public partial class BrokenPipeException {
                                  public string Detail { get; init; } = string.Empty;
                              }
                              """;

        var findings = RuleFixtures
            .Analyze(RuleFixtures.Compile(source, "partial.cs"), Analyzers, TestContext.Current.CancellationToken)
            .Where(static diagnostic => diagnostic.Id == RuleIds.ExceptionNameWithoutExceptionBase)
            .ToArray();

        Assert.Single(findings);
    }

    static Diagnostic[] Analyze(RuleFixture fixture) {
        var compilation = RuleFixtures.Compile(File.ReadAllText(fixture.Path), fixture.Path);

        return RuleFixtures
            .Analyze(compilation, Analyzers, TestContext.Current.CancellationToken)
            .Where(diagnostic => diagnostic.Id == fixture.RuleId)
            .ToArray();
    }
}
