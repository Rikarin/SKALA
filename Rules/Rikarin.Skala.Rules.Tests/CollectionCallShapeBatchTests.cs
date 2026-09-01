using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Rules.Metadata;
using Rikarin.Skala.Rules.Modernization;
using Rikarin.Skala.Rules.Performance;
using System.Collections.Immutable;
using System.Globalization;

namespace Rikarin.Skala.Rules.Tests;

/// <summary>
///     The five rules decided by the receiver's static type plus the operator called on it.
/// </summary>
/// <remarks>
///     ⚠ <c>SK1034</c> is in the analyzer list on purpose. <c>SK4033</c> declares
///     <c>supersedes: ["SK1034"]</c>, and whether the two land on the same span — which is the only
///     thing <c>Supersession.Apply</c> can match on — is a property of the pair rather than of either
///     rule.
/// </remarks>
public sealed class CollectionCallShapeBatchTests {
    static readonly ImmutableArray<DiagnosticAnalyzer> Analyzers = [
        new CollectionOwnMethodAnalyzer(), new DictionaryKeyRelookupAnalyzer(),
        new SubstringBeforeSearchAnalyzer(), new ConcurrentDictionaryMemberAnalyzer(),
        new SortBeforeFilterAnalyzer(), new CountPropertyAnalyzer(), new WhereBeforeOperatorAnalyzer(),
    ];

    static readonly string[] Ids = ["SK4030", "SK4031", "SK4032", "SK4033", "SK4034"];

    public static TheoryData<RuleFixture> Fixtures {
        get {
            var data = new TheoryData<RuleFixture>();
            foreach (var fixture in RuleFixtures.All().Where(static f => Ids.Contains(f.RuleId))) {
                data.Add(fixture);
            }

            return data;
        }
    }

    [Theory]
    [MemberData(nameof(Fixtures))]
    public void Fixtures_HaveExactCountsAndCarryTheirFix(RuleFixture fixture) {
        var findings = Analyze(RuleFixtures.Compile(File.ReadAllText(fixture.Path), fixture.Path))
            .Where(diagnostic => diagnostic.Id == fixture.RuleId)
            .ToArray();

        Assert.Equal(fixture.ShouldFire ? 1 : 0, findings.Length);

        // ⚠ Both directions. A rule the catalogue says has no fix must not smuggle edits into the
        // property bag either: `skala fix --safe` reads the bag rather than `hasFix`.
        var fixable = RuleCatalog.Get(fixture.RuleId).HasFix;
        Assert.All(findings, d => Assert.Equal(fixable, d.Properties.ContainsKey(FixEdits.CountKey)));
    }

    /// <summary>
    ///     ⚠ The <c>Contains</c> rewrite is the one that can be wrong, and declining it must leave
    ///     <c>Exists</c> standing rather than leaving the call alone.
    /// </summary>
    /// <remarks>
    ///     Every row here is a <c>List&lt;T&gt;.Any(x =&gt; x == v)</c> the rule reads correctly as
    ///     <em>not</em> a <c>Contains</c>. It is still an <c>Any</c> on a list, so the answer is
    ///     <c>Exists</c> — which is why none of these can be a "should not fire" fixture, and why the
    ///     assertion has to be on the text of the fix rather than on a count.
    /// </remarks>
    [Theory]
    [InlineData("double", "value == wanted", "⚠ NaN == NaN is false; the default comparer says true")]
    [InlineData("float", "value == wanted", "same, one width down")]
    [InlineData("Widget", "value == wanted", "`==` is identity here; Contains calls Equals")]
    public void ContainsIsDeclinedAndExistsIsStillOffered(string element, string body, string why) {
        Assert.NotEmpty(why);

        var source = $$"""
                       using System.Collections.Generic;
                       using System.Linq;
                       public sealed class Widget { }
                       public sealed class Registry {
                           public static bool Knows(List<{{element}}> values, {{element}} wanted) =>
                               values.Any(value => {{body}});
                       }
                       """;

        var fixedText = Apply(source, "SK4030");
        Assert.Contains("values.Exists(", fixedText, StringComparison.Ordinal);
        Assert.DoesNotContain("Contains", fixedText, StringComparison.Ordinal);
    }

    /// <summary>The element types where <c>==</c> and the default comparer are the same test.</summary>
    [Theory]
    [InlineData("string")]
    [InlineData("int")]
    [InlineData("long")]
    [InlineData("char")]
    [InlineData("bool")]
    [InlineData("decimal")]
    public void ContainsIsOfferedForTypesWhoseDefaultComparerIsTheirOperator(string element) {
        var source = $$"""
                       using System.Collections.Generic;
                       using System.Linq;
                       public sealed class Registry {
                           public static bool Knows(List<{{element}}> values, {{element}} wanted) =>
                               values.Any(value => value == wanted);
                       }
                       """;

        Assert.Contains("values.Contains(wanted)", Apply(source, "SK4030"), StringComparison.Ordinal);
    }

    /// <summary>An <c>enum</c> element compares by value under both, and is named by neither table.</summary>
    [Fact]
    public void ContainsIsOfferedForAnEnumElement() {
        const string source = """
                              using System.Collections.Generic;
                              using System.Linq;
                              public enum State { Idle, Ready }
                              public sealed class Registry {
                                  public static bool Knows(List<State> values, State wanted) =>
                                      values.Any(value => value == wanted);
                              }
                              """;

        Assert.Contains("values.Contains(wanted)", Apply(source, "SK4030"), StringComparison.Ordinal);
    }

    /// <summary>
    ///     ⚠ A comment inside the lambda is content the <c>Contains</c> edit would delete, so the
    ///     rule falls back to the rename — which touches one token and cannot lose anything.
    /// </summary>
    [Fact]
    public void ACommentInsideTheLambdaFallsBackToTheRename() {
        const string source = """
                              using System.Collections.Generic;
                              using System.Linq;
                              public sealed class Registry {
                                  public static bool Knows(List<string> names, string wanted) =>
                                      names.Any(
                                          // ordinal on purpose: these are paths, not display names
                                          name => name == wanted
                                      );
                              }
                              """;

        var fixedText = Apply(source, "SK4030");
        Assert.Contains("names.Exists(", fixedText, StringComparison.Ordinal);
        Assert.Contains("// ordinal on purpose", fixedText, StringComparison.Ordinal);
    }

    /// <summary>
    ///     ⚠ The compared value leaves the lambda, so it is evaluated once instead of once per
    ///     element. A call there is a change to the program even when the answer is the same.
    /// </summary>
    [Fact]
    public void AComputedComparisonValueIsNotHoistedIntoContains() {
        const string source = """
                              using System.Collections.Generic;
                              using System.Linq;
                              public sealed class Registry {
                                  public static bool Knows(List<int> codes) => codes.Any(code => code == Wanted());

                                  static int Wanted() => 7;
                              }
                              """;

        Assert.Contains("codes.Exists(", Apply(source, "SK4030"), StringComparison.Ordinal);
    }

    /// <summary>
    ///     ⚠ The whole rewrite, as text. The header and every lookup site move at once, and a fix
    ///     that repaired the header alone would parse, bind, and leave the loop reading a key that no
    ///     longer means anything.
    /// </summary>
    [Fact]
    public void TheRelookupFix_DeconstructsAndReplacesEveryLookup() {
        const string source = """
                              using System;
                              using System.Collections.Generic;
                              public sealed class Report {
                                  public static void Write(Dictionary<string, int> totals) {
                                      foreach (var key in totals.Keys) {
                                          Console.WriteLine(totals[key] + totals[key] + key);
                                      }
                                  }
                              }
                              """;

        var fixedText = Apply(source, "SK4031");
        Assert.Contains("foreach (var (key, value) in totals) {", fixedText, StringComparison.Ordinal);
        Assert.Contains("Console.WriteLine(value + value + key);", fixedText, StringComparison.Ordinal);
    }

    /// <summary>
    ///     ⚠ Inside a <c>set</c> accessor <c>value</c> is the implicit parameter, and a local of that
    ///     name is CS0136 whether or not the accessor mentions it.
    /// </summary>
    [Fact]
    public void TheValueNameStepsAsideForASetterImplicitParameter() {
        const string source = """
                              using System;
                              using System.Collections.Generic;
                              public sealed class Report {
                                  readonly Dictionary<string, int> totals = new();

                                  public int Threshold {
                                      set {
                                          foreach (var key in this.totals.Keys) {
                                              Console.WriteLine(this.totals[key]);
                                          }
                                      }
                                  }
                              }
                              """;

        Assert.Contains("var (key, entryValue) in", Apply(source, "SK4031"), StringComparison.Ordinal);
    }

    /// <summary>A name the body already uses is not taken over by the fix.</summary>
    [Fact]
    public void AnExistingValueNameIsNotShadowed() {
        const string source = """
                              using System;
                              using System.Collections.Generic;
                              public sealed class Report {
                                  public static void Write(Dictionary<string, int> totals, int value) {
                                      foreach (var key in totals.Keys) {
                                          Console.WriteLine(totals[key] + value);
                                      }
                                  }
                              }
                              """;

        Assert.Contains("var (key, entryValue) in", Apply(source, "SK4031"), StringComparison.Ordinal);
    }

    /// <summary>⚠ A comment inside a replaced span is content the fix would delete.</summary>
    [Fact]
    public void ACommentInsideALookupWithholdsTheFinding() {
        const string source = """
                              using System;
                              using System.Collections.Generic;
                              public sealed class Report {
                                  public static void Write(Dictionary<string, int> totals) {
                                      foreach (var key in totals.Keys) {
                                          Console.WriteLine(totals[/* the running total */ key]);
                                      }
                                  }
                              }
                              """;

        Assert.Empty(Analyze(RuleFixtures.Compile(source, "probe.cs")).Where(d => d.Id == "SK4031"));
    }

    /// <summary>
    ///     ⚠ The offset goes in as the <em>second</em> argument, not the last one.
    /// </summary>
    /// <remarks>
    ///     <c>IndexOf(value, startIndex, comparisonType)</c> puts it in the middle, so appending it
    ///     would compile against the <c>(string, int)</c> overload for a two-argument call and quietly
    ///     drop the comparison — a fix that binds and searches with the wrong semantics.
    /// </remarks>
    [Theory]
    [InlineData("text.Substring(4).IndexOf(needle) >= 0", "text.IndexOf(needle, 4) >= 0")]
    [InlineData(
        "text.Substring(4).IndexOf(needle, StringComparison.Ordinal) != -1",
        "text.IndexOf(needle, 4, StringComparison.Ordinal) != -1"
    )]
    [InlineData("-1 == text.Substring(start).IndexOf(needle)", "-1 == text.IndexOf(needle, start)")]
    public void TheStartIndexGoesInAsTheSecondArgument(string written, string expected) {
        var source = $$"""
                       using System;
                       public sealed class Paths {
                           public static bool Probe(string text, string needle, int start) => {{written}};
                       }
                       """;

        Assert.Contains(expected, Apply(source, "SK4032"), StringComparison.Ordinal);
    }

    /// <summary>
    ///     ⚠ Every presence test, and nothing else. The rewritten call returns a different number, so
    ///     this list is the whole safety argument rather than a convenience.
    /// </summary>
    [Theory]
    [InlineData("i >= 0", true)]
    [InlineData("i < 0", true)]
    [InlineData("i > -1", true)]
    [InlineData("i <= -1", true)]
    [InlineData("i == -1", true)]
    [InlineData("i != -1", true)]
    [InlineData("0 <= i", true)]
    [InlineData("-1 < i", true)]
    [InlineData("i > 0", false)]
    [InlineData("i == 0", false)]
    [InlineData("i >= 1", false)]
    [InlineData("i != 0", false)]
    public void OnlyAPresenceTestIsReported(string test, bool reported) {
        var source = $$"""
                       public sealed class Paths {
                           public static bool Probe(string text, int start) =>
                               {{test.Replace("i", "text.Substring(start).IndexOf('/')")}};
                       }
                       """;

        var findings = Analyze(RuleFixtures.Compile(source, "probe.cs")).Where(d => d.Id == "SK4032");
        Assert.Equal(reported, findings.Any());
    }

    /// <summary>
    ///     ⚠ The supersession, as the only thing that can actually make it work: the same span.
    /// </summary>
    /// <remarks>
    ///     <c>Supersession.Apply</c> pairs findings by rule, file, line and column. A rule that
    ///     declared <c>supersedes</c> and reported one character to the left would suppress nothing
    ///     and nothing would say so — the two findings would simply both appear, which is what this
    ///     asserts against.
    /// </remarks>
    [Theory]
    [InlineData("entries.Keys.Count()")]
    [InlineData("entries.Values.Count()")]
    public void TheSupersededCountFindingLandsOnTheSameSpan(string written) {
        var source = $$"""
                       using System.Collections.Concurrent;
                       using System.Linq;
                       public sealed class Cache {
                           public static int Size(ConcurrentDictionary<string, int> entries) => {{written}};
                       }
                       """;

        var findings = Analyze(RuleFixtures.Compile(source, "probe.cs"));
        var mine = Assert.Single(findings.Where(d => d.Id == "SK4033"));
        var superseded = Assert.Single(findings.Where(d => d.Id == "SK1034"));

        Assert.Equal(
            superseded.Location.GetLineSpan().StartLinePosition,
            mine.Location.GetLineSpan().StartLinePosition
        );
        Assert.Contains("SK1034", RuleCatalog.Get("SK4033").Supersedes);
    }

    /// <summary>The same, for the negated form, whose span is the negation's rather than the call's.</summary>
    [Fact]
    public void TheNegatedAnyAlsoLandsWhereSK1034Reports() {
        const string source = """
                              using System.Collections.Concurrent;
                              using System.Linq;
                              public sealed class Cache {
                                  public static bool Empty(ConcurrentDictionary<string, int> entries) {
                                      if (!entries.Keys.Any()) {
                                          return true;
                                      }

                                      return false;
                                  }
                              }
                              """;

        var findings = Analyze(RuleFixtures.Compile(source, "probe.cs"));
        var mine = Assert.Single(findings.Where(d => d.Id == "SK4033"));
        var superseded = Assert.Single(findings.Where(d => d.Id == "SK1034"));

        Assert.Equal(superseded.Location.SourceSpan, mine.Location.SourceSpan);
        Assert.Contains("entries.IsEmpty", Apply(source, "SK4033"), StringComparison.Ordinal);
    }

    /// <summary>
    ///     ⚠ One finding carrying the whole rewrite, not two whose fixes have to be applied in order.
    /// </summary>
    [Theory]
    [InlineData("entries.Count == 0", "entries.IsEmpty")]
    [InlineData("entries.Count <= 0", "entries.IsEmpty")]
    [InlineData("entries.Count < 1", "entries.IsEmpty")]
    [InlineData("0 == entries.Count", "entries.IsEmpty")]
    [InlineData("entries.Count != 0", "!entries.IsEmpty")]
    [InlineData("entries.Count > 0", "!entries.IsEmpty")]
    [InlineData("entries.Count >= 1", "!entries.IsEmpty")]
    [InlineData("0 < entries.Count", "!entries.IsEmpty")]
    [InlineData("entries.Keys.Count > 0", "!entries.IsEmpty")]
    [InlineData("entries.Keys.Count() == 0", "entries.IsEmpty")]
    public void AnEmptinessComparisonBecomesOneIsEmptyRead(string written, string expected) {
        var source = $$"""
                       using System.Collections.Concurrent;
                       using System.Linq;
                       public sealed class Cache {
                           public static bool Probe(ConcurrentDictionary<string, int> entries) => {{written}};
                       }
                       """;

        Assert.Contains("=> " + expected + ";", Apply(source, "SK4033"), StringComparison.Ordinal);
    }

    /// <summary>
    ///     ⚠ The swap moves both calls verbatim and leaves the dot and the layout between them alone.
    /// </summary>
    [Fact]
    public void TheSwapKeepsBothCallsAndTheLayoutBetweenThem() {
        const string source = """
                              using System.Collections.Generic;
                              using System.Linq;
                              public sealed class Feed {
                                  public static IEnumerable<int> Recent(List<int> entries) {
                                      return entries
                                          .OrderBy(entry => entry)
                                          .Where(entry => entry > 0);
                                  }
                              }
                              """;

        var fixedText = Apply(source, "SK4034");
        Assert.Contains(
            """
                    return entries
                        .Where(entry => entry > 0)
                        .OrderBy(entry => entry);
            """.TrimEnd(),
            fixedText,
            StringComparison.Ordinal
        );
    }

    /// <summary>
    ///     ⚠ Exactly one of <c>SK4010</c> and <c>SK4034</c> speaks on
    ///     <c>xs.OrderBy(k).Where(p).First()</c>, and it has to be exactly one: their edits overlap,
    ///     so two findings would be two fixes that cannot both be applied.
    /// </summary>
    [Fact]
    public void OnlySK4010SpeaksWhereTheirEditsWouldOverlap() {
        const string source = """
                              using System.Collections.Generic;
                              using System.Linq;
                              public sealed class Feed {
                                  public static int Recent(List<int> entries) =>
                                      entries.OrderBy(entry => entry).Where(entry => entry > 0).First();
                              }
                              """;

        var findings = Analyze(RuleFixtures.Compile(source, "probe.cs"));
        Assert.Single(findings.Where(d => d.Id == "SK4010"));
        Assert.Empty(findings.Where(d => d.Id == "SK4034"));
    }

    /// <summary>
    ///     ⚠ And where <c>SK4010</c> does <em>not</em> reach, this rule must — otherwise the guard
    ///     above is silently doing more than it says.
    /// </summary>
    [Fact]
    public void SK4034StillSpeaksWhereTheConsumerIsNotOneSK4010Folds() {
        const string source = """
                              using System.Collections.Generic;
                              using System.Linq;
                              public sealed class Feed {
                                  public static List<int> Recent(List<int> entries) =>
                                      entries.OrderBy(entry => entry).Where(entry => entry > 0).ToList();
                              }
                              """;

        var findings = Analyze(RuleFixtures.Compile(source, "probe.cs"));
        Assert.Empty(findings.Where(d => d.Id == "SK4010"));
        Assert.Single(findings.Where(d => d.Id == "SK4034"));
    }

    [Theory]
    [InlineData("SK4030", "any-becomes-exists")]
    [InlineData("SK4031", "a-dictionary-local")]
    [InlineData("SK4032", "a-char-search")]
    [InlineData("SK4033", "the-keys-count-property")]
    [InlineData("SK4034", "order-by-then-where")]
    public void GeneratedCode_IsIgnored(string id, string name) {
        var source = "// <auto-generated/>\n"
            + File.ReadAllText(Path.Combine(RuleFixtures.Root, id, "positive", name + ".cs"));
        Assert.Empty(Analyze(RuleFixtures.Compile(source, "generated.cs")).Where(d => d.Id == id));
    }

    internal static ImmutableArray<Diagnostic> Analyze(CSharpCompilation compilation) =>
        RuleFixtures.Analyze(compilation, Analyzers, TestContext.Current.CancellationToken);

    /// <summary>Applies every edit the single finding of <paramref name="id" /> carries.</summary>
    internal static string Apply(string source, string id) {
        var diagnostic = Assert.Single(Analyze(RuleFixtures.Compile(source, "probe.cs")).Where(d => d.Id == id));

        var count = int.Parse(diagnostic.Properties[FixEdits.CountKey]!, CultureInfo.InvariantCulture);
        var edits = Enumerable.Range(0, count)
            .Select(index => new TextChange(
                    new TextSpan(
                        int.Parse(diagnostic.Properties[FixEdits.StartKey(index)]!, CultureInfo.InvariantCulture),
                        int.Parse(diagnostic.Properties[FixEdits.LengthKey(index)]!, CultureInfo.InvariantCulture)
                    ),
                    diagnostic.Properties[FixEdits.TextKey(index)]!
                )
            );

        return SourceText.From(source).WithChanges(edits).ToString();
    }
}
