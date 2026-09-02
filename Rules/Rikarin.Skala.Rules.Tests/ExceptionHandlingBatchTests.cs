using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Rikarin.Skala.Rules.Correctness;
using Rikarin.Skala.Rules.Maintainability;
using System.Collections.Immutable;

namespace Rikarin.Skala.Rules.Tests;

/// <summary>
///     Exact counts, and the boundaries between the four exception rules and the two that shipped
///     before them.
/// </summary>
/// <remarks>
///     <see cref="RuleFixtureTests" /> asks "at least one" on a positive fixture and "none" on a
///     negative one. That is the shipping bar and it is the wrong question for a rule whose defect
///     would be firing twice on one keyword, and it is no question at all about whether two rules
///     report the same clause.
///     <para>
///         ⚠ <b>It is also blind to the failure that costs the most.</b> When an analyzer throws, Roslyn
///         swallows it as <c>AD0001</c> and the analyzer produces nothing at all — so every negative
///         fixture passes, and only the positives fail. Issue #279 is that the harness does not look;
///         <see cref="Fixtures_DoNotCrashAnAnalyzer" /> looks.
///     </para>
/// </remarks>
public sealed class ExceptionHandlingBatchTests {
    static readonly ImmutableArray<DiagnosticAnalyzer> Analyzers = [
        new ThrowingFinalizerAnalyzer(), new ThrowInFinallyAnalyzer(),
        new CaughtNullReferenceAnalyzer(), new DiscardedCaughtExceptionAnalyzer(),
        new EmptyCatchAnalyzer(), new LoggedAndRethrownAnalyzer()
    ];

    static readonly string[] Ids = ["SK2090", "SK2091", "SK2092", "SK2093"];

    public static TheoryData<RuleFixture> Fixtures {
        get {
            var data = new TheoryData<RuleFixture>();
            foreach (var fixture in RuleFixtures.All().Where(static fixture => Ids.Contains(fixture.RuleId))) {
                data.Add(fixture);
            }

            return data;
        }
    }

    /// <summary>
    ///     ⚠ The check the fixture harness does not make: a crashed analyzer passes every negative.
    /// </summary>
    [Theory]
    [MemberData(nameof(Fixtures))]
    public void Fixtures_DoNotCrashAnAnalyzer(RuleFixture fixture) {
        var crashes = All(File.ReadAllText(fixture.Path), fixture.Path)
            .Where(static diagnostic => diagnostic.Id == "AD0001")
            .Select(static diagnostic => diagnostic.GetMessage())
            .ToArray();

        Assert.True(
            crashes.Length == 0,
            $"{fixture}: an analyzer threw, so it produced nothing and every negative fixture passed "
            + "for the wrong reason:\n  "
            + string.Join("\n  ", crashes)
        );
    }

    /// <summary>
    ///     ⚠ Anti-vacuity: the theory above proves nothing if it is running over an empty set.
    /// </summary>
    [Fact]
    public void TheBatch_HasFixturesForEveryRuleInIt() {
        var fixtures = RuleFixtures.All();
        foreach (var id in Ids) {
            Assert.True(fixtures.Any(f => f.RuleId == id && f.ShouldFire), $"{id} has no positive fixture.");
            Assert.True(fixtures.Any(f => f.RuleId == id && !f.ShouldFire), $"{id} has no negative fixture.");
        }
    }

    /// <summary>
    ///     One <c>throw</c>, one finding — however many <c>finally</c> blocks enclose it.
    /// </summary>
    /// <remarks>
    ///     ⚠ The rule registers per <c>FinallyClauseSyntax</c> and every enclosing clause sees the same
    ///     descendant node, so without the ownership test a doubly-nested keyword would be reported
    ///     twice and read as two problems. The fixture harness cannot say this: it asks only whether the
    ///     count is above zero.
    /// </remarks>
    [Fact]
    public void SK2091_ReportsANestedThrowAgainstTheInnermostFinallyOnly() {
        const string source = """
                              class C {
                                  void M() {
                                      try {
                                          Work();
                                      } finally {
                                          try {
                                              Work();
                                          } finally {
                                              throw new System.InvalidOperationException("inner");
                                          }
                                      }
                                  }

                                  static void Work() { }
                              }
                              """;

        var finding = Assert.Single(Findings(source, "SK2091"));
        Assert.Equal(source.IndexOf("throw new", System.StringComparison.Ordinal), finding.Location.SourceSpan.Start);
    }

    /// <summary>
    ///     ⚠ <c>SK2093</c> and <c>SK7092</c> are negations of one another, asserted rather than claimed.
    /// </summary>
    /// <remarks>
    ///     <c>SK7092</c> requires the clause to propagate what it caught; <c>SK2093</c> requires that it
    ///     does not. The day either condition is relaxed into a filter, this is what says the two rules
    ///     started reporting the same clause twice.
    ///     <para>
    ///         ⚠ <b>The fourth case is the only one that tests anything.</b> A clause holding
    ///         <em>only</em> a <c>throw;</c> has no <c>throw new</c> for <c>SK2093</c> to match, and one
    ///         holding only a <c>throw new</c> has no propagation for <c>SK7092</c> to match, so both
    ///         stay silent whether or not the guard exists — three green rows proving the shapes are
    ///         different rather than the rules disjoint. A sabotage that deleted the guard left every
    ///         one of them passing. Only a clause holding <b>both</b> forms can double-report, and that
    ///         is the row that goes red.
    ///     </para>
    /// </remarks>
    [Theory]
    [InlineData("System.Console.WriteLine(error); throw;", "SK7092")]
    [InlineData("System.Console.WriteLine(error); throw error;", "SK7092")]
    [InlineData("""throw new Wrapped("failed");""", "SK2093")]
    [InlineData(
        """System.Console.WriteLine(error); if (error.HResult == 0) { throw new Wrapped("failed"); } throw;""",
        "SK7092"
    )]
    [InlineData("""System.Console.WriteLine(error); throw new Wrapped("failed");""", "SK2093")]
    public void SK2093_AndSK7092_NeverBothFireOnOneClause(string body, string expected) {
        var source = Clause(body);
        Assert.Single(Findings(source, expected));
        Assert.Empty(Findings(source, expected == "SK2093" ? "SK7092" : "SK2093"));
    }

    /// <summary>
    ///     ⚠ <c>SK2093</c> is not <c>SK2014</c>: that rule needs an empty block and this one needs a
    ///     <c>throw</c>, so no clause satisfies both.
    /// </summary>
    [Theory]
    [InlineData("", "SK2014")]
    [InlineData("""throw new Wrapped("failed");""", "SK2093")]
    public void SK2093_AndSK2014_NeverBothFireOnOneClause(string body, string expected) {
        var source = Clause(body);
        Assert.Single(Findings(source, expected));
        Assert.Empty(Findings(source, expected == "SK2093" ? "SK2014" : "SK2093"));
    }

    /// <summary>
    ///     ⚠ The recall decision, as a test. One hop and no further, stated so the boundary is a fact.
    /// </summary>
    /// <remarks>
    ///     A throw in the method the finalizer calls is reported; a throw one call further down is not,
    ///     and that is the cost of not being an interprocedural analysis rather than an oversight. If
    ///     the hop ever becomes transitive, the second case here is what has to be updated deliberately.
    /// </remarks>
    [Fact]
    public void SK2090_FollowsOneCallHopAndStops() {
        const string oneHop = """
                              sealed class C {
                                  ~C() {
                                      Release();
                                  }

                                  void Release() {
                                      throw new System.InvalidOperationException("one hop");
                                  }
                              }
                              """;

        const string twoHops = """
                               sealed class C {
                                   ~C() {
                                       Release();
                                   }

                                   void Release() {
                                       Actually();
                                   }

                                   void Actually() {
                                       throw new System.InvalidOperationException("two hops");
                                   }
                               }
                               """;

        Assert.Single(Findings(oneHop, "SK2090"));
        Assert.Empty(Findings(twoHops, "SK2090"));
    }

    /// <summary>
    ///     ⚠ The guard the rule would be unusable without, and its exact edge.
    /// </summary>
    /// <remarks>
    ///     `if (disposing)` is the disposal pattern's managed half and the finalizer passes `false`, so a
    ///     throw inside it is unreachable from `~T()`. Everything outside that branch — and the `else`,
    ///     and the negated spelling — is on the finalizer's path and is reported.
    /// </remarks>
    [Theory]
    [InlineData("""if (disposing) { throw new System.InvalidOperationException("managed"); }""", 0)]
    [InlineData("""if (!disposing) { return; } else { throw new System.InvalidOperationException("m"); }""", 0)]
    [InlineData("""if (!disposing) { throw new System.InvalidOperationException("unmanaged"); }""", 1)]
    [InlineData("""if (disposing) { return; } else { throw new System.InvalidOperationException("u"); }""", 1)]
    [InlineData("""throw new System.InvalidOperationException("always");""", 1)]
    public void SK2090_ReadsTheDisposingBranchTheFinalizerActuallyTakes(string body, int expected) {
        var source = """
                     sealed class C {
                         ~C() {
                             Dispose(false);
                         }

                         void Dispose(bool disposing) {
                             BODY
                         }
                     }
                     """.Replace("BODY", body, System.StringComparison.Ordinal);

        Assert.Equal(expected, Findings(source, "SK2090").Length);
    }

    /// <summary>
    ///     ⚠ <c>catch (Exception)</c> catches it too and is deliberately a different question.
    /// </summary>
    [Theory]
    [InlineData("NullReferenceException", 1)]
    [InlineData("System.NullReferenceException", 1)]
    [InlineData("global::System.NullReferenceException", 1)]
    [InlineData("Exception", 0)]
    [InlineData("SystemException", 0)]
    [InlineData("ArgumentNullException", 0)]
    [InlineData("InvalidOperationException", 0)]
    public void SK2092_ReportsTheTypeTheClauseNamesAndNothingThatMerelyCatchesIt(string type, int expected) {
        var source = "using System; class C { void M() { try { M(); } catch (" + type + ") { } } }";
        Assert.Equal(expected, Findings(source, "SK2092").Length);
    }

    /// <summary>
    ///     The fix is one argument, appended in the position the chaining constructor expects.
    /// </summary>
    [Theory]
    [InlineData("""throw new Wrapped("failed");""", """throw new Wrapped("failed", error);""")]
    [InlineData("throw new Wrapped();", "throw new Wrapped(error);")]
    public void SK2093_AppendsTheCaughtVariableAsTheTrailingArgument(string body, string expected) {
        var source = Clause(body);
        var finding = Assert.Single(Findings(source, "SK2093"));
        var start = int.Parse(
            finding.Properties[FixEdits.StartKey(0)]!,
            System.Globalization.CultureInfo.InvariantCulture
        );
        var length = int.Parse(
            finding.Properties[FixEdits.LengthKey(0)]!,
            System.Globalization.CultureInfo.InvariantCulture
        );

        var after = source[..start] + finding.Properties[FixEdits.TextKey(0)] + source[(start + length)..];
        Assert.Contains(expected, after, System.StringComparison.Ordinal);
    }

    /// <summary>A <c>catch</c> body, wrapped in everything it needs to compile.</summary>
    static string Clause(string body) =>
        """
        using System;
        using System.IO;

        sealed class Wrapped : Exception {
            public Wrapped() { }

            public Wrapped(Exception inner) : base("failed", inner) { }

            public Wrapped(string message) : base(message) { }

            public Wrapped(string message, Exception inner) : base(message, inner) { }
        }

        sealed class C {
            public void M(string path) {
                try {
                    File.ReadAllText(path);
                } catch (IOException error) {
                    BODY
                }
            }
        }
        """.Replace("BODY", body, System.StringComparison.Ordinal);

    static ImmutableArray<Diagnostic> All(string source, string path) =>
        RuleFixtures.Analyze(
            RuleFixtures.Compile(source, path),
            Analyzers,
            TestContext.Current.CancellationToken
        );

    static Diagnostic[] Findings(string source, string ruleId) =>
        All(source, "test.cs").Where(diagnostic => diagnostic.Id == ruleId).ToArray();
}
