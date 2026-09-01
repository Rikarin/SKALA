using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Rikarin.Skala.Rules.Design;
using System.Collections.Immutable;

namespace Rikarin.Skala.Rules.Tests;

/// <summary>
///     The declaration rules that report a promise the declaration does not keep, with exact counts.
/// </summary>
/// <remarks>
///     ⚠ The same guard <see cref="DesignDeclarationBatchTests" /> exists for, one range along.
///     <see cref="RuleFixtureTests.Rule_FiresExactlyWhereTheFixtureSaysItShould" /> asks only whether a
///     positive fixture produced <em>anything</em>, and every rule in this batch reads a declaration
///     that a partial type has more than one of — so a rule that reports per declaration where it
///     should report per symbol passes that test while doubling every count in a report.
/// </remarks>
public sealed class DesignPromiseBatchTests {
    static readonly ImmutableArray<DiagnosticAnalyzer> Analyzers = [
        new GlobalNamespaceTypeAnalyzer(),
    ];

    static readonly string[] Ids = ["SK6030"];

    public static TheoryData<RuleFixture> Fixtures {
        get {
            var data = new TheoryData<RuleFixture>();
            foreach (var fixture in RuleFixtures.All()
                         .Where(static fixture => Ids.Contains(fixture.RuleId, StringComparer.Ordinal))) {
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

    static Diagnostic[] Analyze(RuleFixture fixture) {
        var compilation = RuleFixtures.Compile(File.ReadAllText(fixture.Path), fixture.Path);

        return RuleFixtures
            .Analyze(compilation, Analyzers, TestContext.Current.CancellationToken)
            .Where(diagnostic => diagnostic.Id == fixture.RuleId)
            .ToArray();
    }
}
