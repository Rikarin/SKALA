using Rikarin.Skala.Rules.Metadata;
using System.Collections.Immutable;

namespace Rikarin.Skala.Rules.Tests;

/// <summary>
///     The sweep's own instrument check: that the baseline names real things, and that the assertion
///     it feeds actually goes red when a fixture carries a defect it does not record.
/// </summary>
/// <remarks>
///     ⚠ The per-fixture equality assertion is worth exactly as much as this file. A sweep that cannot
///     be shown to fail is the shape of every measurement in this repository that turned out to have
///     been reporting a non-event.
/// </remarks>
public sealed class CrossRuleBaselineTests {
    [Fact]
    public void EveryRecordedLine_NamesAFixtureAndARuleThatStillExist() {
        var missing = new List<string>();
        foreach (var (fixture, rule) in CrossRuleBaseline.All()) {
            if (!File.Exists(Path.Combine(RuleFixtures.Root, fixture.Replace('/', Path.DirectorySeparatorChar)))) {
                missing.Add(fixture + ": the fixture is gone; delete the line");
            }

            if (RuleCatalog.Find(rule) is not { Retired: false }) {
                missing.Add(fixture + ": " + rule + " is not a live rule; delete the line");
            }
        }

        Assert.Empty(missing);
    }

    [Fact]
    public void EveryRecordedLine_IsOnANegativeFixture() {
        // The sweep only looks at negative fixtures, so a line on a positive one is never consulted —
        // it would sit in the file for ever, true or not, and the equality assertion would never see it.
        Assert.DoesNotContain(
            CrossRuleBaseline.All(),
            static entry => !entry.Fixture.Contains("/negative/", StringComparison.Ordinal)
        );
    }

    /// <summary>
    ///     ⚠ The sabotage test. A planted cross-rule defect on a fixture with no line must be seen.
    /// </summary>
    [Fact]
    public void APlantedDefect_IsSeenByTheSweep() {
        const string source = """
                              using System;

                              class C {
                                  void M() {
                                      try {
                                          Console.WriteLine("x");
                                      } catch (Exception) {
                                      }
                                  }
                              }
                              """;

        var compilation = RuleFixtures.Compile(source, "planted.cs");
        var all = RuleFixtures.Analyze(compilation, SkalaAnalyzers.All, TestContext.Current.CancellationToken);

        // SK2014 owns the empty catch. The fixture under test is pretending to belong to SK1005, which
        // has nothing to do with it — exactly the shape #285 found twice in the real corpus.
        var observed = CrossRuleBaseline.Observed(all, "SK1005");

        Assert.Contains(RuleIds.EmptyCatchSwallowsException, observed);
        Assert.False(observed.SetEquals(CrossRuleBaseline.For("SK1005/negative/planted.cs")));
    }

    /// <summary>⚠ And the other direction: a rule the sweep does not measure must stay invisible to it.</summary>
    [Fact]
    public void AStyleFinding_IsNotACrossRuleFinding() {
        var compilation = RuleFixtures.Compile("namespace Sample { class C { } }", "style.cs");
        var all = RuleFixtures.Analyze(compilation, SkalaAnalyzers.All, TestContext.Current.CancellationToken);

        Assert.Contains(all, static diagnostic => diagnostic.Id == RuleIds.FileScopedNamespace);
        Assert.DoesNotContain(RuleIds.FileScopedNamespace, CrossRuleBaseline.Observed(all, "SK9999"));
    }

    [Fact]
    public void TheBaseline_IsSortedAndFreeOfDuplicates() {
        var lines = File.ReadAllLines(CrossRuleBaseline.Path)
            .Where(static line => line.Length > 0 && line[0] != '#')
            .Select(static line => string.Join('\t', line.Split('\t').Take(2)))
            .ToImmutableArray();

        // A file people append to at the bottom stops being reviewable in a diff.
        Assert.Equal(lines.Order(StringComparer.Ordinal), lines);
        Assert.Equal(lines.Distinct(StringComparer.Ordinal).Count(), lines.Length);
    }
}
