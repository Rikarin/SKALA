using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Rikarin.Skala.Rules.Async;
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
        new NullSequenceReturnAnalyzer(),
    ];

    static readonly string[] Ids = ["SK6050", "SK6051", "SK6052"];

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

    /// <summary>
    ///     ⚠ <c>SK6052</c> and <c>SK3020</c> divide one concept, and this asserts the line rather than
    ///     describing it.
    /// </summary>
    /// <remarks>
    ///     A negative fixture proves only that <c>SK6052</c> stayed quiet, which is equally true if the
    ///     shape is reported by nobody. The claim written on the fixture is stronger: the other rule
    ///     takes it. Both halves are checked here, so a change to either rule that opens a gap or starts
    ///     a double report fails a test instead of changing a number in a report.
    /// </remarks>
    [Theory]
    [InlineData("negative/a-non-async-task-of-a-sequence-is-SK3020s.cs", "SK3020")]
    [InlineData("positive/an-async-method-returning-a-null-sequence.cs", "SK6052")]
    public void TheNullSequenceConcept_IsSplitWithSK3020(string relativePath, string expected) {
        var path = Path.Combine(RuleFixtures.Root, "SK6052", relativePath.Replace('/', Path.DirectorySeparatorChar));
        var compilation = RuleFixtures.Compile(File.ReadAllText(path), path);
        var produced = RuleFixtures
            .Analyze(
                compilation,
                [new NullSequenceReturnAnalyzer(), new NullTaskReturnAnalyzer()],
                TestContext.Current.CancellationToken
            )
            .Where(static diagnostic => diagnostic.Id is "SK6052" or "SK3020")
            .Select(static diagnostic => diagnostic.Id)
            .ToArray();

        Assert.Equal([expected], produced);
    }

    static ImmutableArray<Diagnostic> Analyze(RuleFixture fixture) {
        var compilation = RuleFixtures.Compile(File.ReadAllText(fixture.Path), fixture.Path);

        return RuleFixtures.Analyze(compilation, Analyzers, TestContext.Current.CancellationToken);
    }
}
