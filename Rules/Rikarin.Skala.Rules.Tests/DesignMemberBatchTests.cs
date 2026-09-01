using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Rikarin.Skala.Rules.Design;
using System.Collections.Immutable;

namespace Rikarin.Skala.Rules.Tests;

/// <summary>
///     The members-and-signatures batch, with exact counts and a check that the analyzer ran at all.
/// </summary>
/// <remarks>
///     ⚠ <b>A crashed analyzer passes every negative fixture.</b> Roslyn catches the exception, turns it
///     into an <c>AD0001</c> and lets the analyzer produce nothing — so the positives fail and every
///     negative passes for the wrong reason.
///     <see cref="RuleFixtureTests.Rule_FiresExactlyWhereTheFixtureSaysItShould" /> filters the
///     diagnostics down to the fixture's own rule id before it looks at them, which throws the
///     <c>AD0001</c> away. That is how <c>SK6041</c> shipped dead once, with nine green negatives, after
///     a first draft passed a method symbol to
///     <see cref="Compilation.IsSymbolAccessibleWithin(ISymbol, ISymbol, ITypeSymbol)" />, which throws
///     unless the second argument is a type or an assembly.
///     <para>
///         The exact-count half is <see cref="DesignPromiseBatchTests" />'s, for the same reason: a
///         positive fixture asserts only that <em>something</em> was produced, so a rule reporting twice
///         where it should report once passes it while doubling every count in a report.
///     </para>
/// </remarks>
public sealed class DesignMemberBatchTests {
    static readonly ImmutableArray<DiagnosticAnalyzer> Analyzers = [
        new ConstantReturningMethodAnalyzer(), new DerivedTypeTestOnThisAnalyzer(),
    ];

    static readonly string[] Ids = ["SK6050", "SK6051"];

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
        var produced = Analyze(fixture);
        var findings = produced.Where(diagnostic => diagnostic.Id == fixture.RuleId).ToArray();
        Assert.Equal(fixture.ShouldFire ? 1 : 0, findings.Length);
    }

    /// <summary>
    ///     ⚠ The instrument check: no fixture in this batch may make an analyzer throw.
    /// </summary>
    [Theory]
    [MemberData(nameof(Fixtures))]
    public void NoFixture_MakesTheAnalyzerThrow(RuleFixture fixture) {
        var crashes = Analyze(fixture)
            .Where(static diagnostic => diagnostic.Id == "AD0001")
            .ToArray();

        Assert.True(
            crashes.Length == 0,
            $"{fixture}: an analyzer threw, so every negative fixture in this batch passes for the "
            + "wrong reason:\n"
            + string.Join("\n", crashes.Select(static d => "  " + d.GetMessage()))
        );
    }

    static ImmutableArray<Diagnostic> Analyze(RuleFixture fixture) {
        var compilation = RuleFixtures.Compile(File.ReadAllText(fixture.Path), fixture.Path);

        return RuleFixtures.Analyze(compilation, Analyzers, TestContext.Current.CancellationToken);
    }
}
