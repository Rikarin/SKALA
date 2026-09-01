using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Rules.Async;
using Rikarin.Skala.Rules.Metadata;
using System.Collections.Immutable;
using System.Globalization;

namespace Rikarin.Skala.Rules.Tests;

/// <summary>
///     The three rules that ask what a <c>using</c> owns, and the two independents beside them.
/// </summary>
/// <remarks>
///     ⚠ <c>SK3007</c> is in the analyzer list on purpose. It is the <c>Task</c> shape of
///     <c>SK3512</c>, and the fact that exactly one of the two speaks on a given <c>return</c> is a
///     property of the pair rather than of either rule — asserting it needs both running at once.
/// </remarks>
public sealed class DisposalOwnershipBatchTests {
    static readonly ImmutableArray<DiagnosticAnalyzer> Analyzers = [
        new RedundantDisposeAnalyzer(), new UsingResourceInitializerAnalyzer(),
        new UsingVariableReturnedAnalyzer(), new NullTaskReturnAnalyzer(),
        new SpinLockInReadonlyFieldAnalyzer(), new TaskReturnedFromUsingAnalyzer()
    ];

    public static TheoryData<RuleFixture> Fixtures {
        get {
            var data = new TheoryData<RuleFixture>();
            foreach (var fixture in RuleFixtures.All()
                         .Where(static f => f.RuleId is "SK3510" or "SK3511" or "SK3512" or "SK3020" or "SK3021")) {
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
        // property bag either: `skala fix --safe` reads the bag, not `hasFix`, so a stray edit on a
        // rule declared fixless is applied without anything having decided it was safe.
        var fixable = RuleCatalog.Get(fixture.RuleId).HasFix;
        Assert.All(findings, d => Assert.Equal(fixable, d.Properties.ContainsKey(FixEdits.CountKey)));
    }

    /// <summary>
    ///     ⚠ The whole line, and only that line.
    /// </summary>
    /// <remarks>
    ///     The interesting failure is not a fix that does not compile — <c>RuleFixtureTests</c> covers
    ///     that — but one that leaves an orphaned indent behind, or swallows the comment written above
    ///     the statement. Both parse, both re-bind, and both are wrong; only comparing the text catches
    ///     them.
    /// </remarks>
    [Fact]
    public void RedundantDisposeFix_TakesTheLineAndLeavesTheCommentAboveIt() {
        const string source = """
                              using System;
                              public sealed class Handle : IDisposable {
                                  public void Dispose() { }
                              }
                              public sealed class Consumer {
                                  public void Report() {
                                      using var handle = new Handle();
                                      // Belt and braces.
                                      handle.Dispose();
                                  }
                              }
                              """;

        const string expected = """
                                using System;
                                public sealed class Handle : IDisposable {
                                    public void Dispose() { }
                                }
                                public sealed class Consumer {
                                    public void Report() {
                                        using var handle = new Handle();
                                        // Belt and braces.
                                    }
                                }
                                """;

        Assert.Equal(expected, Apply(source, "SK3510"));
    }

    [Fact]
    public void ATrailingCommentSurvivesTheDeletion() {
        const string source = """
                              using System;
                              public sealed class Handle : IDisposable {
                                  public void Dispose() { }
                              }
                              public sealed class Consumer {
                                  public void Report() {
                                      using var handle = new Handle();
                                      handle.Dispose(); // Explain why.
                                  }
                              }
                              """;

        Assert.Contains("// Explain why.", Apply(source, "SK3510"), StringComparison.Ordinal);
    }

    /// <summary>
    ///     ⚠ The hoist puts the parentheses back, because <c>new Channel</c> is not an expression.
    /// </summary>
    [Fact]
    public void InitializerHoist_ConstructsFirstAndAssignsAfter() {
        const string source = """
                              using System;
                              public sealed class Channel : IDisposable {
                                  public string Name { get; set; } = "";
                                  public int Retries { get; set; }
                                  public void Dispose() { }
                              }
                              public sealed class Consumer {
                                  public void Open() {
                                      using var channel = new Channel { Name = Configured(), Retries = 3 };
                                      Console.WriteLine(channel.Name);
                                  }

                                  static string Configured() => "main";
                              }
                              """;

        Assert.Contains(
            """
                    using var channel = new Channel();
                    channel.Name = Configured();
                    channel.Retries = 3;
                    Console.WriteLine(channel.Name);
            """,
            Apply(source, "SK3511"),
            StringComparison.Ordinal
        );
    }

    /// <summary>
    ///     ⚠ For the statement form the assignments go <em>inside</em> the block, not after it.
    /// </summary>
    /// <remarks>
    ///     Putting them after the <c>using</c> would assign to a disposed object, which parses,
    ///     binds, and is a worse bug than the one being fixed.
    /// </remarks>
    [Fact]
    public void InitializerHoist_MovesTheAssignmentsIntoTheUsingBlock() {
        const string source = """
                              using System.IO;
                              public sealed class Consumer {
                                  public void Write(string path) {
                                      using (var writer = new StreamWriter(path) { AutoFlush = true }) {
                                          writer.WriteLine("done");
                                      }
                                  }
                              }
                              """;

        Assert.Contains(
            """
                    using (var writer = new StreamWriter(path)) {
                        writer.AutoFlush = true;
                        writer.WriteLine("done");
            """,
            Apply(source, "SK3511"),
            StringComparison.Ordinal
        );
    }

    /// <summary>
    ///     ⚠ Exactly one of <c>SK3007</c> and <c>SK3512</c> speaks on the shape they share.
    /// </summary>
    /// <remarks>
    ///     <c>return x;</c> where <c>x</c> is a <c>using</c> variable of a task type is the whole of
    ///     the overlap, and <c>SK3007</c> is the rule that carries a fix for it. Removing the type
    ///     test in <see cref="UsingVariableReturnedAnalyzer" /> makes this file report twice, at the
    ///     same span, with two different remedies — which is what the assertion below is for. It is
    ///     also why <c>supersedes</c> is not used: it dedupes on a shared span and would suppress the
    ///     finding that has the fix.
    /// </remarks>
    [Fact]
    public void TheTaskShape_IsReportedByExactlyOneOfTheTwoRules() {
        var source = File.ReadAllText(
            Path.Combine(RuleFixtures.Root, "SK3512", "negative", "the-task-shape-belongs-to-sk3007.cs")
        );

        var findings = Analyze(RuleFixtures.Compile(source, "overlap.cs"));

        Assert.Single(findings, static d => d.Id == "SK3007");
        Assert.DoesNotContain(findings, static d => d.Id == "SK3512");
    }

    /// <summary>
    ///     ⚠ The replacement is resolved, not spelled out of the declaration's text.
    /// </summary>
    /// <remarks>
    ///     An alias makes the written return type a name that a string edit would get wrong, so the
    ///     rule asks whether the simple name <c>Task</c> means <c>System.Threading.Tasks.Task</c>
    ///     where the fix lands and qualifies fully when it does not. Both branches are exercised
    ///     here: the second file never imports the namespace.
    /// </remarks>
    [Theory]
    [InlineData("using System.Threading.Tasks;\npublic class C { Task M() { return null; } }", "Task.CompletedTask")]
    [InlineData(
        "using System.Threading.Tasks;\npublic class C { Task<int> M() { return null; } }",
        "Task.FromResult<int>(default!)"
    )]
    [InlineData(
        "using MyTask = System.Threading.Tasks.Task;\npublic class C { MyTask M() { return null; } }",
        "global::System.Threading.Tasks.Task.CompletedTask"
    )]
    public void NullTaskFix_SpellsTheCompletedTaskSoThatItBinds(string source, string expected) =>
        Assert.Contains(expected, Apply(source, "SK3020"), StringComparison.Ordinal);

    /// <summary>
    ///     ⚠ The keyword and the space after it, leaving the rest of the modifiers alone.
    /// </summary>
    [Theory]
    [InlineData("static readonly SpinLock Gate;", "static SpinLock Gate;")]
    [InlineData("internal readonly SpinLock Gate;", "internal SpinLock Gate;")]
    [InlineData("readonly SpinLock Gate;", "SpinLock Gate;")]
    public void SpinLockFix_RemovesOnlyTheReadonlyKeyword(string declaration, string expected) {
        var source = "using System.Threading;\npublic class C { " + declaration + " }";
        Assert.Contains("{ " + expected + " }", Apply(source, "SK3021"), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("SK3510", "a-using-declaration")]
    [InlineData("SK3511", "a-using-declaration")]
    [InlineData("SK3512", "a-using-declaration")]
    public void GeneratedCode_IsIgnored(string id, string name) {
        var source = "// <auto-generated/>\n"
            + File.ReadAllText(Path.Combine(RuleFixtures.Root, id, "positive", name + ".cs"));
        Assert.Empty(Analyze(RuleFixtures.Compile(source, "generated.cs")));
    }

    internal static ImmutableArray<Diagnostic> Analyze(CSharpCompilation compilation) =>
        RuleFixtures.Analyze(compilation, Analyzers, TestContext.Current.CancellationToken);

    /// <summary>Applies every edit one finding of <paramref name="id" /> carries.</summary>
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
