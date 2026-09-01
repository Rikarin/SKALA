using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Rules.Async;
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
        new RedundantDisposeAnalyzer(), new UsingResourceInitializerAnalyzer(), new TaskReturnedFromUsingAnalyzer()
    ];

    public static TheoryData<RuleFixture> Fixtures {
        get {
            var data = new TheoryData<RuleFixture>();
            foreach (var fixture in RuleFixtures.All().Where(static f => f.RuleId is "SK3510" or "SK3511")) {
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
        Assert.All(findings, static d => Assert.True(d.Properties.ContainsKey(FixEdits.CountKey)));
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

    [Theory]
    [InlineData("SK3510", "a-using-declaration")]
    [InlineData("SK3511", "a-using-declaration")]
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
