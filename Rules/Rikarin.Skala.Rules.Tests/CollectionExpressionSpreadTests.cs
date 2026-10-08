using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Rikarin.Skala.Rules.Metadata;
using System.Collections.Immutable;

namespace Rikarin.Skala.Rules.Tests;

/// <summary>
///     <c>SK1133</c>: what the fix writes, and which rule owns a shape two rules can see (#512).
/// </summary>
public sealed class CollectionExpressionSpreadTests {
    const string Header = """
                          // fixture-option: LangVersion = 14
                          using System.Collections.Generic;
                          using System.Linq;

                          public sealed class Site {
                              public int[] Copy(List<int> list, List<int>? other, bool flag) {

                          """;

    const string Footer = """

                                  return copied;
                              }
                          }
                          """;

    /// <summary>
    ///     ⚠ No space after <c>..</c>. The formatter keeps a spread exactly as written (SK-DIV-0009), so
    ///     the fix's spelling is the one that stays; and parentheses go only where the spread reads back
    ///     as the same expression without them.
    /// </summary>
    [Theory]
    [InlineData("int[] copied = list.ToArray();", "int[] copied = [..list];")]
    [InlineData("int[] copied = (list).ToArray();", "int[] copied = [..list];")]
    [InlineData("int[] copied = ((list)).ToArray();", "int[] copied = [..list];")]
    [InlineData(
        "int[] copied = ((IEnumerable<int>)list).ToArray();",
        "int[] copied = [..(IEnumerable<int>)list];"
    )]
    [InlineData(
        "int[] copied = list.Where(value => value > 0).ToArray();",
        "int[] copied = [..list.Where(value => value > 0)];"
    )]
    [InlineData("int[] copied = (other ?? list).ToArray();", "int[] copied = [..other ?? list];")]
    [InlineData("int[] copied = (flag ? list : other!).ToArray();", "int[] copied = [..flag ? list : other!];")]
    [InlineData(
        "int[] copied = (from value in list select value).ToArray();",
        "int[] copied = [..from value in list select value];"
    )]
    [InlineData("int[] copied = (other = list).ToArray();", "int[] copied = [..other = list];")]
    [InlineData(
        "int[] copied = (list as IEnumerable<int>).ToArray();",
        "int[] copied = [..list as IEnumerable<int>];"
    )]
    public void TheFix_WritesTheSpreadGlued(string written, string expected) {
        var source = Header + "        " + written + Footer;

        Assert.Equal(
            Header + "        " + expected + Footer,
            CollectionCallShapeBatchTests.Apply(source, RuleIds.CollectionExpressionSpread, SkalaAnalyzers.All)
        );
    }

    /// <summary>
    ///     ⚠ <c>SK1081</c> owns a copy of a copy. This rule waits for its fix, then takes the shortened call
    ///     — one defect, one finding, at every step.
    /// </summary>
    [Fact]
    public void ACopyOfACopy_IsSK1081s_ThenThisRulesOnTheNextPass() {
        var source = Header + "        int[] copied = list.ToList().ToArray();" + Footer;
        var first = Analyze(source);

        Assert.Single(first, static d => d.Id == RuleIds.RedundantSequenceCall);
        Assert.DoesNotContain(first, static d => d.Id == RuleIds.CollectionExpressionSpread);

        var shortened = CollectionCallShapeBatchTests.Apply(source, RuleIds.RedundantSequenceCall, SkalaAnalyzers.All);
        Assert.Contains("int[] copied = list.ToArray();", shortened, StringComparison.Ordinal);

        var second = Analyze(shortened);
        Assert.Single(second, static d => d.Id == RuleIds.CollectionExpressionSpread);
        Assert.DoesNotContain(second, static d => d.Id == RuleIds.RedundantSequenceCall);
    }

    /// <summary>
    ///     <c>SK4006</c>'s <c>foreach</c> over a copy has no target type, so it is that rule's alone.
    /// </summary>
    [Fact]
    public void AForeachOverACopy_IsSK4006sAlone() {
        const string source = """
                              // fixture-option: LangVersion = 14
                              using System.Collections.Generic;
                              using System.Linq;

                              public static class Loop {
                                  public static int Sum(int[] values) {
                                      var total = 0;
                                      foreach (var value in values.ToArray()) {
                                          total += value;
                                      }

                                      return total;
                                  }
                              }
                              """;
        var found = Analyze(source);

        Assert.Single(found, static d => d.Id == RuleIds.ImmediateMaterialization);
        Assert.DoesNotContain(found, static d => d.Id == RuleIds.CollectionExpressionSpread);
    }

    /// <summary>
    ///     <c>SK1001</c> rewrites a <c>new</c> and never a call; a <c>new</c> copied by <c>ToArray</c> is
    ///     this rule's, and <c>SK1072</c> then takes the array apart.
    /// </summary>
    [Fact]
    public void ACopiedCreation_IsThisRules_ThenSK1072s() {
        var source = Header + "        int[] copied = new[] { 1, 2 }.ToArray();" + Footer;
        var first = Analyze(source);

        Assert.Single(first, static d => d.Id == RuleIds.CollectionExpressionSpread);
        Assert.DoesNotContain(first, static d => d.Id == RuleIds.CollectionExpression);
        Assert.DoesNotContain(first, static d => d.Id == RuleIds.RedundantSpreadElement);

        var spread = CollectionCallShapeBatchTests.Apply(
            source,
            RuleIds.CollectionExpressionSpread,
            SkalaAnalyzers.All
        );
        Assert.Contains("int[] copied = [..new[] { 1, 2 }];", spread, StringComparison.Ordinal);

        var second = Analyze(spread);
        Assert.Single(second, static d => d.Id == RuleIds.RedundantSpreadElement);
        Assert.DoesNotContain(second, static d => d.Id == RuleIds.CollectionExpressionSpread);
    }

    /// <summary>
    ///     ⚠ The floor is a <em>written</em> C# 14: the same shape at <c>latest</c>, at <c>preview</c> and
    ///     at 13 is silent, and at 14 it fires — the control that makes the three silences mean something.
    /// </summary>
    [Theory]
    [InlineData(LanguageVersion.CSharp14, true)]
    [InlineData(LanguageVersion.Latest, false)]
    [InlineData(LanguageVersion.LatestMajor, false)]
    [InlineData(LanguageVersion.Preview, false)]
    [InlineData(LanguageVersion.CSharp13, false)]
    public void TheLanguageVersion_MustBeWrittenAndAtLeast14(LanguageVersion version, bool fires) {
        var source = Header + "        int[] copied = list.ToArray();" + Footer;
        var found = RuleFixtures.Analyze(
            RuleFixtures.Compile(source, "probe.cs", version),
            SkalaAnalyzers.All,
            TestContext.Current.CancellationToken
        );

        Assert.Equal(fires, found.Any(static d => d.Id == RuleIds.CollectionExpressionSpread));
    }

    static ImmutableArray<Diagnostic> Analyze(string source) =>
        RuleFixtures.Analyze(
            RuleFixtures.Compile(source, "probe.cs"),
            SkalaAnalyzers.All,
            TestContext.Current.CancellationToken
        );
}
