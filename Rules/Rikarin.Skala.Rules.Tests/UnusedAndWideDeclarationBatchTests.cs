using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Rikarin.Skala.Rules.Design;
using Rikarin.Skala.Rules.Metadata;
using System.Collections.Immutable;

namespace Rikarin.Skala.Rules.Tests;

/// <summary>
///     The declaration rules from issue #121's family, with exact counts rather than "at least one".
/// </summary>
/// <remarks>
///     <see cref="RuleFixtureTests.Rule_FiresExactlyWhereTheFixtureSaysItShould" /> asks only whether a
///     positive fixture produced anything, and this batch's most likely defect is a duplicate: a local
///     function and its containing method could each raise an operation block, and an <c>out</c>
///     variable inside the local function would then be reported twice while every existing test stayed
///     green.
/// </remarks>
public sealed class UnusedAndWideDeclarationBatchTests {
    static readonly ImmutableArray<DiagnosticAnalyzer> Analyzers = [
        new UnusedOutVariableAnalyzer(), new WiderForeachVariableTypeAnalyzer()
    ];

    public static TheoryData<RuleFixture> Fixtures {
        get {
            var data = new TheoryData<RuleFixture>();
            foreach (var fixture in RuleFixtures.All()
                         .Where(static fixture => fixture.RuleId is "SK6040" or "SK6041")) {
                data.Add(fixture);
            }

            return data;
        }
    }

    [Theory]
    [MemberData(nameof(Fixtures))]
    public void EveryFixture_ProducesExactlyTheCountItClaims(RuleFixture fixture) {
        Assert.Equal(fixture.ShouldFire ? 1 : 0, Analyze(fixture).Length);
    }

    /// <summary>
    ///     ⚠ <c>SK6041</c> narrows a declaration, so the type it writes has to be the element's own.
    /// </summary>
    /// <remarks>
    ///     The round-trip tests prove the result compiles and that the rule falls silent, which a fix
    ///     that widened the type further would also satisfy. This asserts the direction: the replacement
    ///     is exactly what <c>GetForEachStatementInfo</c> reports as the element type, spelled minimally
    ///     at the loop's own position so no <c>using</c> is needed.
    /// </remarks>
    [Fact]
    public void TheForeachFix_WritesTheElementTypeAndNothingElse() {
        const string source = """
                              using System.Collections.Generic;
                              using System.Text;

                              public static class Loop {
                                  public static string Join(List<string> names) {
                                      var builder = new StringBuilder();
                                      foreach (object name in names) {
                                          builder.Append(name);
                                      }

                                      return builder.ToString();
                                  }
                              }
                              """;

        var finding = Assert.Single(
            RuleFixtures
                .Analyze(RuleFixtures.Compile(source, "loop.cs"), Analyzers, TestContext.Current.CancellationToken)
                .Where(static diagnostic => diagnostic.Id == RuleIds.WiderForeachVariableType)
        );

        Assert.Equal("string", finding.Properties[FixEdits.TextKey(0)]);

        var start = int.Parse(finding.Properties[FixEdits.StartKey(0)]!);
        var length = int.Parse(finding.Properties[FixEdits.LengthKey(0)]!);
        Assert.Equal("object", source.Substring(start, length));
    }

    /// <summary>
    ///     ⚠ The edit is a replacement of the declaration, never of the whole argument.
    /// </summary>
    /// <remarks>
    ///     A fix that replaced the <c>ArgumentSyntax</c> would delete the <c>out</c> keyword with it and
    ///     produce a call that binds to nothing. The round-trip tests catch that only because the result
    ///     fails to compile; this asserts the intent directly, so a future edit cannot widen the span and
    ///     leave the reason unrecorded.
    /// </remarks>
    [Theory]
    [MemberData(nameof(Fixtures))]
    public void EveryFinding_ReplacesOnlyTheDeclaration(RuleFixture fixture) {
        if (fixture.RuleId != RuleIds.UnusedOutVariable) {
            return;
        }

        var source = File.ReadAllText(fixture.Path);
        foreach (var diagnostic in Analyze(fixture)) {
            Assert.Equal("1", diagnostic.Properties[FixEdits.CountKey]);

            var start = int.Parse(diagnostic.Properties[FixEdits.StartKey(0)]!);
            var length = int.Parse(diagnostic.Properties[FixEdits.LengthKey(0)]!);

            Assert.Equal("_", diagnostic.Properties[FixEdits.TextKey(0)]);
            Assert.DoesNotContain("out", source.Substring(start, length), StringComparison.Ordinal);
        }
    }

    /// <summary>
    ///     ⚠ Two unread <c>out</c> variables in one member are two findings, not one and not three.
    /// </summary>
    [Fact]
    public void TwoUnreadOutVariablesInOneMember_AreReportedTwice() {
        const string source = """
                              using System.Collections.Generic;

                              public static class Pair {
                                  public static bool Both(Dictionary<string, int> lookup, string first, string second) =>
                                      lookup.TryGetValue(first, out var one) && lookup.TryGetValue(second, out var two);
                              }
                              """;

        var findings = RuleFixtures
            .Analyze(RuleFixtures.Compile(source, "pair.cs"), Analyzers, TestContext.Current.CancellationToken)
            .Where(static diagnostic => diagnostic.Id == RuleIds.UnusedOutVariable)
            .ToArray();

        Assert.Equal(2, findings.Length);
    }

    static Diagnostic[] Analyze(RuleFixture fixture) =>
        RuleFixtures
            .Analyze(
                RuleFixtures.Compile(File.ReadAllText(fixture.Path), fixture.Path),
                Analyzers,
                TestContext.Current.CancellationToken
            )
            .Where(diagnostic => diagnostic.Id == fixture.RuleId)
            .ToArray();
}
