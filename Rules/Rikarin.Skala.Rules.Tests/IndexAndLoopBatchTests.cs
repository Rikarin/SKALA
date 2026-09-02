using Microsoft.CodeAnalysis.Diagnostics;
using Rikarin.Skala.Rules.Correctness;
using System.Collections.Immutable;

namespace Rikarin.Skala.Rules.Tests;

/// <summary>
///     The batch level for <c>SK2210</c>–<c>SK2213</c>: what the fixture harness cannot ask.
/// </summary>
/// <remarks>
///     ⚠ <b>A crashed analyzer passes every negative fixture.</b> Roslyn catches the exception, reports
///     it as <c>AD0001</c>, and the analyzer then produces nothing at all — so the positives fail,
///     which reads as "the rule needs another condition", and every "should not fire" fixture passes,
///     which reads as a spotless false-positive record. The fixture harness does not look for
///     <c>AD0001</c> (issue #279) and <c>skala check</c> records it only in the SARIF's
///     <c>toolExecutionNotifications</c> without failing the gate (issue #295), so these tests do.
///     <para>
///         This batch has its own reason to worry about it. Three of the four rules ask the semantic
///         model a question that can come back <c>null</c> — a receiver with no type, an element access
///         that binds to nothing, a condition whose identifier resolves to no symbol — and two of them
///         run <c>AnalyzeControlFlow</c> and <c>AnalyzeDataFlow</c> over a region, which throws rather
///         than failing softly when handed a node the model does not own.
///     </para>
/// </remarks>
public sealed class IndexAndLoopBatchTests {
    static readonly ImmutableArray<DiagnosticAnalyzer> Analyzers = [
        new InvalidConstantIndexOrRangeAnalyzer(), new UnchangingLoopConditionAnalyzer(),
        new SingleIterationLoopAnalyzer(), new IndexOfComparedToPositiveAnalyzer()
    ];

    static readonly string[] Ids = ["SK2210", "SK2211", "SK2212", "SK2213"];

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
}
