using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Rikarin.Skala.Rules.Metadata;
using Rikarin.Skala.Rules.Performance;
using System.Collections.Immutable;

namespace Rikarin.Skala.Rules.Tests;

/// <summary>
///     The checks <see cref="RuleFixtureTests" /> does not make, for <c>SK4040</c>–<c>SK4041</c>, plus
///     the refutation that decided how large this batch was.
/// </summary>
/// <remarks>
///     ⚠ <b>A crashed analyzer passes every negative fixture.</b> Roslyn catches the exception, turns
///     it into <c>AD0001</c> and returns no diagnostics from that analyzer, so every "should not fire"
///     fixture goes green and only the positives go red — which reads as a rule that does not work
///     rather than as one that threw. The shared harness does not look (issue #279) and
///     <c>skala check</c> drops it into the SARIF's <c>toolExecutionNotifications</c> without failing
///     the gate (issue #295). This file looks.
/// </remarks>
public sealed class CollectionCopyAndBufferBatchTests {
    static readonly ImmutableArray<DiagnosticAnalyzer> Batch = [
        new CopyingPropertyAnalyzer(), new UnreadStringBuilderAnalyzer(), new ImmediateMaterializationAnalyzer()
    ];

    static readonly string[] Ids = [RuleIds.CopyingProperty, RuleIds.UnreadStringBuilder];

    public static TheoryData<RuleFixture> Fixtures {
        get {
            var data = new TheoryData<RuleFixture>();
            foreach (var fixture in RuleFixtures.All()) {
                if (Ids.Contains(fixture.RuleId)) {
                    data.Add(fixture);
                }
            }

            return data;
        }
    }

    /// <summary>⚠ Anti-vacuity: an empty theory is the shape of this file having stopped working.</summary>
    [Fact]
    public void TheBatch_HasFixturesToCheck() {
        var all = RuleFixtures.All();
        foreach (var id in Ids) {
            Assert.True(
                all.Count(fixture => fixture.RuleId == id) >= 8,
                $"{id} has fewer than eight fixtures; the checks below would be nearly vacuous."
            );
        }
    }

    [Theory]
    [MemberData(nameof(Fixtures))]
    public void NoAnalyzerThrows(RuleFixture fixture) {
        var source = File.ReadAllText(fixture.Path);
        var produced = RuleFixtures.Analyze(
            RuleFixtures.Compile(source, fixture.Path),
            Batch,
            TestContext.Current.CancellationToken
        );

        var crashes = produced.Where(static diagnostic => diagnostic.Id == "AD0001").ToArray();
        Assert.True(
            crashes.Length == 0,
            $"{fixture}: an analyzer threw, which silently passes every negative fixture:\n  "
            + string.Join("\n  ", crashes.Select(static d => d.GetMessage()))
        );
    }

    /// <summary>
    ///     ⚠ <b>Issue #267 — "the sequence is enumerated more than once" — is not <c>SK4006</c>, and
    ///     this is the refutation as a test rather than as a sentence.</b>
    /// </summary>
    /// <remarks>
    ///     <c>catalogued.json</c> credited ReSharper's <c>PossibleMultipleEnumeration</c> to
    ///     <c>SK4006</c>, and <c>SK4006</c> is <em>Review a materialization used only by foreach</em> —
    ///     a <c>ToList()</c> that should be <b>removed</b>. Multiple enumeration is a
    ///     <c>ToList()</c> that should be <b>added</b>. The two are mirror images: one needs an
    ///     explicit materialization to exist and exactly one consumer, the other needs no
    ///     materialization and two consumers, so no program satisfies both, and the pair below pins
    ///     that in both directions.
    ///     <para>
    ///         ⚠ No <c>SK</c> id was allocated for #267 either, and for a different reason: the concept
    ///         is hosted by <c>CA1851</c>, measured <c>enabledByDefault: false, defaultSeverity:
    ///         Warning</c> against the 10.0.400 SDK, whose flow-sensitive analysis strictly dominates
    ///         what a static-type rule could report. See docs/plan/08 § <c>SK4040</c>–<c>SK4041</c>.
    ///     </para>
    /// </remarks>
    [Fact]
    public void MultipleEnumeration_IsNotTheShapeSk4006Reports() {
        const string enumeratedTwice = """
            using System.Collections.Generic;
            using System.Linq;

            public sealed class Feed {
                public static int Total(IEnumerable<int> source) => source.Count() + source.Sum();
            }
            """;

        const string materializedForOneForeach = """
            using System.Collections.Generic;

            public sealed class Feed {
                public static int Total(List<int> source) {
                    var total = 0;
                    foreach (var value in source.ToArray()) {
                        total += value;
                    }

                    return total;
                }
            }
            """;

        Assert.DoesNotContain(
            Analyze(enumeratedTwice, "enumerated-twice.cs"),
            static diagnostic => diagnostic.Id == RuleIds.ImmediateMaterialization
        );

        Assert.Contains(
            Analyze(materializedForOneForeach, "materialized-for-one-foreach.cs"),
            static diagnostic => diagnostic.Id == RuleIds.ImmediateMaterialization
        );
    }

    /// <summary>
    ///     ⚠ The fix's promise, at the one place it could be wrong: the edit has to keep the property's
    ///     declared type.
    /// </summary>
    /// <remarks>
    ///     <c>SK4040</c> reports only where the source already converts to the property's type by
    ///     identity or by reference, which is what makes "delete the materializing call" an edit rather
    ///     than a rewrite. A rule that dropped that test would emit <c>int[] Values => values;</c> for a
    ///     <c>List&lt;int&gt;</c> field and break the build on the tool's own advice.
    /// </remarks>
    [Fact]
    public void TheFix_IsOfferedOnlyWhereTheSourceTypeSurvivesIt() {
        const string convertible = """
            using System.Collections.Generic;
            using System.Linq;

            public sealed class Feed {
                readonly List<string> entries = new();

                public IReadOnlyList<string> Items => entries.ToList();
            }
            """;

        const string notConvertible = """
            using System.Collections.Generic;
            using System.Linq;

            public sealed class Feed {
                readonly List<string> entries = new();

                public string[] Items => entries.ToArray();
            }
            """;

        var reported = Analyze(convertible, "convertible.cs")
            .Where(static diagnostic => diagnostic.Id == RuleIds.CopyingProperty)
            .ToArray();

        Assert.Single(reported);
        Assert.True(
            reported[0].Properties.TryGetValue(FixEdits.CountKey, out var count) && count == "1",
            "SK4040 reported without carrying its edit, and the catalogue says it has a fix."
        );

        Assert.DoesNotContain(
            Analyze(notConvertible, "not-convertible.cs"),
            static diagnostic => diagnostic.Id == RuleIds.CopyingProperty
        );
    }

    /// <summary>
    ///     ⚠ A chain of appends is one reference and many writes; the same chain ending in a read is a
    ///     read. The walk that separates them is the only non-obvious part of <c>SK4041</c>.
    /// </summary>
    [Fact]
    public void AChainOfAppends_IsAWriteUntilSomethingReadsIt() {
        const string chainDiscarded = """
            using System.Text;

            public sealed class Report {
                public void Write(string name) {
                    var builder = new StringBuilder();
                    builder.Append(name).Append('!').AppendLine();
                }
            }
            """;

        const string chainRead = """
            using System.Text;

            public sealed class Report {
                public string Write(string name) {
                    var builder = new StringBuilder();
                    return builder.Append(name).Append('!').ToString();
                }
            }
            """;

        Assert.Contains(
            Analyze(chainDiscarded, "chain-discarded.cs"),
            static diagnostic => diagnostic.Id == RuleIds.UnreadStringBuilder
        );

        Assert.DoesNotContain(
            Analyze(chainRead, "chain-read.cs"),
            static diagnostic => diagnostic.Id == RuleIds.UnreadStringBuilder
        );
    }

    static ImmutableArray<Diagnostic> Analyze(string source, string path) {
        var compilation = RuleFixtures.Compile(source, path);
        var errors = compilation.GetDiagnostics(TestContext.Current.CancellationToken)
            .Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .ToArray();

        Assert.True(
            errors.Length == 0,
            $"{path} does not compile, so it proves nothing: "
            + string.Join("; ", errors.Take(3).Select(static d => d.ToString()))
        );

        var produced = RuleFixtures.Analyze(compilation, Batch, TestContext.Current.CancellationToken);
        Assert.DoesNotContain(produced, static diagnostic => diagnostic.Id == "AD0001");
        return produced;
    }
}
