using Microsoft.CodeAnalysis.Diagnostics;
using Rikarin.Skala.Rules.Correctness;
using Rikarin.Skala.Rules.Modernization;
using System.Collections.Immutable;

namespace Rikarin.Skala.Rules.Tests;

/// <summary>
///     The batch level for <c>SK1120</c>–<c>SK1123</c>: what the fixture harness cannot ask.
/// </summary>
/// <remarks>
///     ⚠ <b>A crashed analyzer passes every negative fixture.</b> Roslyn catches the exception,
///     reports it as <c>AD0001</c>, and the analyzer then produces nothing at all — so the positives
///     fail, which reads as "the rule needs another condition", and every "should not fire" fixture
///     passes, which reads as a spotless false-positive record. <c>skala check</c> records
///     <c>AD0001</c> only in the SARIF's <c>toolExecutionNotifications</c> without failing the gate
///     (issue #295), so these tests are the place that can see it.
///     <para>
///         This batch has two specific reasons to worry. <c>SK1122</c> walks every anonymous object
///         creation in the enclosing member and indexes a dictionary built from member names — a
///         missing key is a throw, not a decline — and <c>SK1120</c> calls
///         <c>ClassifyConversion</c> with two symbols it obtained separately, which is a null
///         dereference away from the same thing.
///     </para>
/// </remarks>
public sealed class ExpressionModernizationBatchTests {
    static readonly ImmutableArray<DiagnosticAnalyzer> Analyzers = [
        new ReflectiveTypeTestAnalyzer(), new MergeableTryAnalyzer(),
        new ReorderedAnonymousTypeAnalyzer(), new MergedPropertyPatternAnalyzer()
    ];

    static readonly string[] Ids = ["SK1120", "SK1121", "SK1122", "SK1123"];

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
    ///     ⚠ <c>SK1120</c> against <c>SK2181</c>, on source that satisfies <b>both</b> rules' shapes.
    /// </summary>
    /// <remarks>
    ///     ⚠ A test showing that two rules match different shapes proves nothing about disjointness:
    ///     the shapes differ whether or not either rule looks at them. The only file that tests it is
    ///     one both rules would report if neither declined.
    ///     <c>typeof(Type).IsAssignableFrom(t.GetType())</c> is such a file — <c>SK2181</c> owns the
    ///     <c>GetType()</c> on a receiver that is already a <c>Type</c>, where the call returns
    ///     <c>System.RuntimeType</c> for every input, and <c>SK1120</c>'s <c>is</c> rewrite would
    ///     contradict that finding rather than complete it. <c>SK1120</c> declines.
    /// </remarks>
    [Fact]
    public void TheGetTypeOnATypeThatSK2181Owns_IsDeclinedBySK1120() {
        const string source = """
                              using System;

                              class Registry {
                                  public bool Test(Type contract) => typeof(Type).IsAssignableFrom(contract.GetType());
                              }
                              """;

        var produced = RuleFixtures
            .Analyze(
                RuleFixtures.Compile(source, "disjoint.cs"),
                [..Analyzers, new GetTypeOnATypeAnalyzer()],
                TestContext.Current.CancellationToken
            )
            .ToArray();

        Assert.Contains(produced, static d => d.Id == "SK2181");
        Assert.DoesNotContain(produced, static d => d.Id == "SK1120");
    }

    /// <summary>
    ///     ⚠ <c>SK1123</c> against <c>SK1011</c>, on source carrying both rules' shapes at once.
    /// </summary>
    /// <remarks>
    ///     ⚠ The hazard is not that both fire; it is that one consumes the other's output and
    ///     <c>skala fix</c> never settles. <c>SK1011</c> is registered on <c>&amp;&amp;</c> and emits
    ///     a single property pattern with one subpattern and no <c>or</c>; <c>SK1123</c> is
    ///     registered on <c>or</c> patterns and emits a single property pattern whose subpattern is
    ///     an <c>or</c>. Neither output is the other's input, and this asserts it on one file rather
    ///     than arguing it.
    /// </remarks>
    [Fact]
    public void SK1011AndSK1123_ReportTheirOwnShapeAndNotEachOthers() {
        const string source = """
                              class Document {
                                  public int Status { get; set; }
                              }

                              class Both {
                                  public bool Merged(Document d) => d is { Status: 1 } or { Status: 2 };

                                  public bool Guarded(Document d) => d != null && d.Status == 1;
                              }
                              """;

        var produced = RuleFixtures
            .Analyze(
                RuleFixtures.Compile(source, "patterns.cs"),
                [..Analyzers, new PropertyPatternAnalyzer()],
                TestContext.Current.CancellationToken
            )
            .ToArray();

        Assert.Equal(1, produced.Count(static d => d.Id == "SK1123"));
        Assert.Equal(1, produced.Count(static d => d.Id == "SK1011"));

        // ⚠ And the settling half: each rule's own replacement text, re-analysed, is quiet.
        Assert.DoesNotContain(
            RuleFixtures.Analyze(
                RuleFixtures.Compile(
                    source.Replace("d is { Status: 1 } or { Status: 2 }", "d is { Status: 1 or 2 }")
                        .Replace("d != null && d.Status == 1", "d is { Status: 1 }"),
                    "patterns.cs"
                ),
                [..Analyzers, new PropertyPatternAnalyzer()],
                TestContext.Current.CancellationToken
            ),
            static d => d.Id is "SK1011" or "SK1123"
        );
    }
}
