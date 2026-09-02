using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Rikarin.Skala.Rules.Correctness;
using Rikarin.Skala.Rules.Metadata;
using Rikarin.Skala.Rules.Modernization;
using Rikarin.Skala.Rules.Performance;
using System.Collections.Immutable;

namespace Rikarin.Skala.Rules.Tests;

/// <summary>
///     The batch level for <c>SK1080</c>–<c>SK1084</c>: what the fixture harness cannot ask.
/// </summary>
/// <remarks>
///     ⚠ <b>A crashed analyzer passes every negative fixture.</b> Roslyn catches the exception, reports
///     it as <c>AD0001</c> and the analyzer produces nothing at all — so the positives fail, which reads
///     as "the rule is wrong", and every "should not fire" fixture passes, which reads as a clean
///     false-positive record. <c>RuleFixtureTests</c> filters diagnostics down to the fixture's own rule
///     id before it looks at anything (issue #279), so it cannot see that; this does.
///     <para>
///         ⚠ The second thing the harness cannot ask is <em>disjointness</em>. Four of these five rules
///         rewrite a LINQ chain, and three shipped rules already do — <c>SK4006</c>, <c>SK4010</c> and
///         <c>SK4034</c>. Two rules reporting the same span is not a duplicate finding, it is two fixes
///         racing for one piece of text, so the claims made in <c>rules.json</c> about which rule keeps
///         which shape are asserted here rather than written down and hoped for.
///     </para>
/// </remarks>
public sealed class LinqChainAndLoopShapeBatchTests {
    static readonly ImmutableArray<DiagnosticAnalyzer> Analyzers = [
        new OfTypeChainAnalyzer(), new RedundantSequenceCallAnalyzer(),
        new IndexerOverElementAtAnalyzer(), new ForeachOverIndexedForAnalyzer(),
        new LoopFilterAsQueryAnalyzer(),
        // The neighbours whose spans must not overlap this batch's.
        new WhereBeforeOperatorAnalyzer(), new SortBeforeFilterAnalyzer(),
        new ImmediateMaterializationAnalyzer(), new CollectionOwnMethodAnalyzer()
    ];

    static readonly string[] Ids = [
        RuleIds.OfTypeOverFilterAndCast, RuleIds.RedundantSequenceCall, RuleIds.IndexerOverElementAt,
        RuleIds.ForeachOverIndexedFor, RuleIds.LoopFilterAsQuery
    ];

    public static TheoryData<RuleFixture> Fixtures {
        get {
            var data = new TheoryData<RuleFixture>();
            foreach (var fixture in RuleFixtures.All()) {
                if (System.Array.IndexOf(Ids, fixture.RuleId) >= 0) {
                    data.Add(fixture);
                }
            }

            return data;
        }
    }

    /// <summary>⚠ Anti-vacuity: an empty theory is a green test that asserts nothing.</summary>
    [Fact]
    public void TheBatch_HasFixtures() {
        var fixtures = RuleFixtures.All();
        foreach (var id in Ids) {
            Assert.True(
                fixtures.Any(fixture => fixture.RuleId == id && fixture.ShouldFire),
                $"{id} has no positive fixture, so every theory below passes vacuously for it."
            );
        }
    }

    [Theory]
    [MemberData(nameof(Fixtures))]
    public void Fixtures_FireExactlyOnceOrNotAtAll(RuleFixture fixture) {
        var findings = Findings(fixture).Where(d => d.Id == fixture.RuleId).ToArray();
        Assert.Equal(fixture.ShouldFire ? 1 : 0, findings.Length);
        Assert.All(
            findings,
            diagnostic => Assert.Equal(
                RuleCatalog.Get(fixture.RuleId).HasFix,
                diagnostic.Properties.ContainsKey(FixEdits.CountKey)
            )
        );
    }

    /// <summary>
    ///     ⚠ A crashed analyzer passes every negative fixture. This is the only thing that says so.
    /// </summary>
    [Theory]
    [MemberData(nameof(Fixtures))]
    public void NoAnalyzerInThisBatchCrashed_OnAnyFixture(RuleFixture fixture) {
        var crashes = Findings(fixture).Where(static d => d.Id == "AD0001").ToArray();
        Assert.True(
            crashes.Length == 0,
            $"{fixture}: an analyzer threw and Roslyn swallowed it as AD0001, so this file proves nothing:\n"
            + string.Join("\n", crashes.Select(static d => "  " + d.GetMessage()))
        );
    }

    /// <summary>
    ///     ⚠ <c>SK1080</c> and <c>SK4010</c> both fold a <c>Where</c>, and the claim that they cannot
    ///     collide is a claim about their consumer sets rather than about their guards.
    /// </summary>
    /// <remarks>
    ///     <c>SK4010</c>'s nine operators all take the predicate directly; <c>Cast</c> and <c>Select</c>
    ///     take neither. If either list ever grows into the other, one of these two chains reports
    ///     twice and <c>skala fix</c> has two edits for one span.
    /// </remarks>
    [Fact]
    public void TheOfTypeFoldAndThePredicateFold_NeverReportTheSameChain() {
        const string source = """
                              using System.Collections.Generic;
                              using System.Linq;

                              public sealed class Registry {
                                  public static IEnumerable<string> Names(IEnumerable<object> values) =>
                                      values.Where(value => value is string).Cast<string>();

                                  public static object First(IEnumerable<object> values) =>
                                      values.Where(value => value is string).First();
                              }
                              """;

        var diagnostics = Analyze(source, "Both.cs");
        var ofType = diagnostics.Where(static d => d.Id == RuleIds.OfTypeOverFilterAndCast).ToArray();
        var fold = diagnostics.Where(static d => d.Id == RuleIds.WhereBeforeLinqOperator).ToArray();

        Assert.Single(ofType);
        Assert.Single(fold);
        Assert.DoesNotContain(diagnostics, static d => d.Id == "AD0001");

        // The two spans are two different chains, not one chain reported twice.
        Assert.NotEqual(ofType[0].Location.SourceSpan, fold[0].Location.SourceSpan);
    }

    /// <summary>
    ///     ⚠ The claim in <c>SK1084</c>'s remarks, measured rather than reasoned about: the fix reuses
    ///     the loop variable's name as the lambda parameter, and that is only sound if the iteration
    ///     variable's scope does not reach the collection expression.
    /// </summary>
    /// <remarks>
    ///     It was expected to be <c>CS0136</c> and it is not. Had it been, the fix would have had to
    ///     rename every occurrence inside the condition instead of moving its source text across, which
    ///     is a different and much less safe edit.
    /// </remarks>
    [Fact]
    public void AForeachVariableName_MayBeReusedAsTheLambdaParameter() {
        const string source = """
                              using System.Collections.Generic;
                              using System.Linq;

                              public sealed class Registry {
                                  public static void Render(IEnumerable<int> numbers) {
                                      foreach (var number in numbers.Where(number => number > 0)) {
                                          System.Console.WriteLine(number);
                                      }
                                  }
                              }
                              """;

        var errors = RuleFixtures.Compile(source, "Scope.cs")
            .GetDiagnostics(TestContext.Current.CancellationToken)
            .Where(static d => d.Severity == DiagnosticSeverity.Error)
            .ToArray();

        Assert.True(
            errors.Length == 0,
            "The iteration variable's scope reaches the collection expression after all, so SK1084's "
            + "fix cannot reuse the name:\n  "
            + string.Join("\n  ", errors.Select(static d => d.ToString()))
        );
    }

    /// <summary>
    ///     ⚠ <c>SK1081</c>'s copy branch rests entirely on the inner call preserving multiplicity, and
    ///     "ToHashSet is not order-preserving" is a fact about the framework rather than about the rule.
    /// </summary>
    /// <remarks>
    ///     The guard that keeps <c>ToHashSet</c> out of the inner position is one identifier in a
    ///     <c>HashSet&lt;string&gt;</c>. Nothing about the code says why, and a later reader tidying the
    ///     two collections into one would make the rule delete a de-duplication.
    /// </remarks>
    [Fact]
    public void ADeduplicatingInnerCopy_IsNeverReported() {
        const string source = """
                              using System.Collections.Generic;
                              using System.Linq;

                              public sealed class Registry {
                                  public static List<int> Deduplicated(IEnumerable<int> source) =>
                                      source.ToHashSet().ToList();

                                  public static List<int> Faithful(IEnumerable<int> source) =>
                                      source.ToList().ToList();
                              }
                              """;

        var diagnostics = Analyze(source, "Copies.cs")
            .Where(static d => d.Id == RuleIds.RedundantSequenceCall)
            .ToArray();

        Assert.Single(diagnostics);

        // The one reported span is the faithful copy's, on the source line that spells it twice.
        var line = diagnostics[0].Location.SourceTree!
            .GetText(TestContext.Current.CancellationToken)
            .Lines[diagnostics[0].Location.GetLineSpan().StartLinePosition.Line]
            .ToString();

        Assert.Contains("source.ToList().ToList()", line, System.StringComparison.Ordinal);
    }

    /// <summary>
    ///     ⚠ <c>SK1083</c> refuses an array-backed receiver only through its *type*, and the exception
    ///     difference that motivates the exclusion is a runtime fact worth pinning rather than quoting.
    /// </summary>
    [Fact]
    public void AnArrayIndexerAndElementAt_ThrowDifferentExceptions() {
        int[] values = [1, 2, 3];

        Assert.Throws<IndexOutOfRangeException>(() => _ = values[7]);
        Assert.Throws<ArgumentOutOfRangeException>(() => _ = values.ElementAt(7));

        List<int> list = [1, 2, 3];
        Assert.Throws<ArgumentOutOfRangeException>(() => _ = list[7]);
        Assert.Throws<ArgumentOutOfRangeException>(() => _ = list[7]);
    }

    /// <summary>
    ///     ⚠ The fix would hand the author an <c>SK2212</c>, and only a cross-rule test can see it.
    /// </summary>
    /// <remarks>
    ///     ⚠ <b><c>EveryFix_SilencesTheRuleAndIntroducesNoDiagnostic</c> is blind to this class (#321),
    ///     because it filters the post-fix diagnostics to the fixture's own rule id</b> — so the rewrite
    ///     could go on producing a single-iteration loop and every existing test would stay green. This
    ///     one runs <c>SK2212</c> over both halves: the source is silent before, and the rewrite the fix
    ///     would have produced is not.
    /// </remarks>
    [Fact]
    public void TheRewriteOfAJumpOnlyBody_WouldBeAnSk2212AndIsDeclined() {
        const string before = """
                              using System.Collections.Generic;
                              using System.Linq;

                              public sealed class Guardrail {
                                  public static string? FirstError(IEnumerable<int> severities) {
                                      foreach (var severity in severities) {
                                          if (severity > 0) {
                                              return null;
                                          }
                                      }

                                      return "clean";
                                  }
                              }
                              """;

        var source = WithSingleIterationLoop(before);
        Assert.DoesNotContain(source, static d => d.Id == RuleIds.LoopFilterAsQuery);
        Assert.DoesNotContain(source, static d => d.Id == RuleIds.SingleIterationLoop);

        // ⚠ Anti-vacuity: the rewrite this rule declines to make really is the other rule's finding,
        // so the decline is buying something rather than describing a shape nobody would produce.
        var rewritten = before.Replace(
            """
                    foreach (var severity in severities) {
                        if (severity > 0) {
                            return null;
                        }
                    }
            """.TrimEnd(),
            """
                    foreach (var severity in severities.Where(severity => severity > 0)) {
                        return null;
                    }
            """.TrimEnd(),
            StringComparison.Ordinal
        );

        Assert.NotEqual(before, rewritten);
        Assert.Contains(WithSingleIterationLoop(rewritten), static d => d.Id == RuleIds.SingleIterationLoop);
    }

    /// <summary>
    ///     ⚠ <c>SK2212</c> is run beside this batch here only, and not added to
    ///     <see cref="Analyzers" /> — the overlap tests in this class assert what does <em>not</em> fire
    ///     over their sources, and widening the shared set would change what those assertions mean.
    /// </summary>
    static ImmutableArray<Diagnostic> WithSingleIterationLoop(string source) =>
        RuleFixtures.Analyze(
            RuleFixtures.Compile(source, "Guardrail.cs"),
            [new LoopFilterAsQueryAnalyzer(), new SingleIterationLoopAnalyzer()],
            TestContext.Current.CancellationToken
        );

    /// <summary>
    ///     ⚠ The narrowing guard reads the compiler's flow state, so the same syntax decides both ways.
    /// </summary>
    /// <remarks>
    ///     Two sources differing in one character — <c>string</c> against <c>string?</c> — and the
    ///     condition, the body and the loop are identical. A guard written against the syntax would
    ///     answer the same for both, and that is the version that shipped the CS8604 (#329).
    /// </remarks>
    [Fact]
    public void TheNarrowingGuard_SeparatesTwoSourcesThatDifferOnlyInNullability() {
        const string template = """
                                using System.Collections.Generic;
                                using System.Linq;

                                public sealed class Option {
                                    public string@ Default { get; init; } = "";
                                }

                                public sealed class Generator {
                                    public static string Strip(string text) => text;

                                    public static void Emit(IEnumerable<Option> options) {
                                        foreach (var option in options) {
                                            if (option.Default is not null) {
                                                System.Console.WriteLine(Strip(option.Default));
                                            }
                                        }
                                    }
                                }
                                """;

        var nullable = template.Replace("string@", "string?", StringComparison.Ordinal);
        var plain = template.Replace("string@", "string", StringComparison.Ordinal);

        Assert.DoesNotContain(Analyze(nullable, "Nullable.cs"), static d => d.Id == RuleIds.LoopFilterAsQuery);
        Assert.Contains(Analyze(plain, "Plain.cs"), static d => d.Id == RuleIds.LoopFilterAsQuery);
    }

    /// <summary>
    ///     ⚠ A <c>[NotNullWhen]</c> method narrows too, which reading only <c>!= null</c> would miss.
    /// </summary>
    [Fact]
    public void AConditionThatNarrowsThroughNotNullWhen_IsDeclined() {
        const string source = """
                              using System.Collections.Generic;
                              using System.Linq;

                              public sealed class Names {
                                  public static void Render(IEnumerable<string?> names) {
                                      foreach (var name in names) {
                                          if (!string.IsNullOrEmpty(name)) {
                                              System.Console.WriteLine(name.Length);
                                          }
                                      }
                                  }
                              }
                              """;

        Assert.DoesNotContain(Analyze(source, "Names.cs"), static d => d.Id == RuleIds.LoopFilterAsQuery);
    }

    static ImmutableArray<Diagnostic> Findings(RuleFixture fixture) =>
        Analyze(File.ReadAllText(fixture.Path), fixture.Path);

    static ImmutableArray<Diagnostic> Analyze(string source, string path) =>
        RuleFixtures.Analyze(
            RuleFixtures.Compile(source, path),
            Analyzers,
            TestContext.Current.CancellationToken
        );
}
