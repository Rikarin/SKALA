using Rikarin.Skala.Rules.Metadata;
using Rikarin.Skala.Testing;
using System.Collections.Immutable;
using System.Globalization;

namespace Rikarin.Skala.Conformance.Tests;

/// <summary>
///     The instrument issue #277 asked for, asserted rather than described.
/// </summary>
/// <remarks>
///     ⚠ <b>Every one of these is about the harness, not about a rule.</b> A rule's own correctness is
///     <c>RuleFixtureTests</c>'s question. What is asserted here is that a zero
///     <see cref="RuleCorpus" /> reports is a zero somebody may quote: the twins are out of the
///     compilation, the implicit usings are in it, no analyzer crashed, the semantic rules ran at all,
///     and the fixture tree's positives still fire under the same mass compile.
///     <para>
///         ⚠ The whole suite runs off one <see cref="Measurement" />, computed once. Four compilations
///         and their analyzer runs cost about forty-five seconds; computing them per test would cost
///         five minutes and tempt somebody to delete the assertions rather than the duplication.
///     </para>
/// </remarks>
public sealed class RuleCorpusTests {
    /// <summary>
    ///     ⚠ <b>The ratchet, and it may be raised but never lowered.</b>
    /// </summary>
    /// <remarks>
    ///     Committed positives that still fire when compiled as one mass compilation rather than one
    ///     file at a time. Measured at 960 of 1 131 (84.9%) on the commit that introduced this file.
    ///     <para>
    ///         ⚠ An <em>absolute</em> floor rather than a fraction, because the fixture tree only ever
    ///         grows: a new fixture can raise <see cref="CorpusRecall.Fired" /> and can never lower it,
    ///         so a drop below this number is the harness breaking and nothing else.
    ///         <see cref="TheHarness_KeepsFiringMostOfTheFixtureTree" /> carries the coarse fractional
    ///         guard beside it, for the opposite failure: fixtures added faster than the harness can
    ///         bind them.
    ///     </para>
    /// </remarks>
    const int RecallFloor = 960;

    /// <summary>The fraction below which the mass compile has stopped modelling the fixture tree.</summary>
    /// <remarks>⚠ Coarse on purpose. 0.849 when pinned; this catches a collapse, not a drift.</remarks>
    const double RecallFractionFloor = 0.75;

    /// <summary>
    ///     Rules whose shape the corpus is known not to contain, planted as canaries.
    /// </summary>
    /// <remarks>
    ///     ⚠ Cryptography, chosen because the absence is measured rather than assumed: not one of the
    ///     corpus's files names <c>System.Security.Cryptography</c>, <c>SslProtocols</c> or
    ///     <c>SecurityProtocolType</c>. Serilog, Newtonsoft.Json and Vixen are a logging library, a JSON
    ///     serialiser and a game engine, and between them they construct no cipher and generate no key
    ///     pair. So these two rules must report <see cref="CorpusVerdict.Declined" /> on every tree —
    ///     <b>never</b> <see cref="CorpusVerdict.Silent" />, which is what the same 0 looks like when
    ///     the analyzer never ran.
    /// </remarks>
    static readonly ImmutableArray<string> Canaries = ["SK5020", "SK5021"];

    /// <summary>⚠ Named here so that a tree quietly disappearing from the corpus fails a test.</summary>
    static readonly string[] VendoredTrees = ["newtonsoft", "serilog", "vixen"];

    static Measurement Measured { get; } = Measure();

    /// <summary>
    ///     ⚠ The exclusion the counts rest on, asserted against the file system rather than trusted.
    /// </summary>
    /// <remarks>
    ///     The corpus keeps three copies of every file — <c>X.cs</c>, <c>X.expected.cs</c> and
    ///     <c>X.arranged.expected.cs</c> — so its 1 140 files are 380 sources times three. Compiling all
    ///     three declares every type three times, and a semantic rule declines what it cannot bind, so
    ///     every count taken that way is a floor rather than a measurement. Re-measured on this commit
    ///     by removing the exclusion: the twins cost <b>73 312</b> compiler errors across the three trees
    ///     against <b>15 738</b> without them, both without the implicit usings.
    ///     <para>
    ///         ⚠ The figures issue #277 records for the same comparison — 53 658 → 13 036 — do
    ///         <em>not</em> reproduce here, and the shape of the claim does: they were taken over one
    ///         synthetic project spanning all three trees rather than three compilations, so they are a
    ///         different population. The multiplier is what carries over, not the numbers.
    ///     </para>
    /// </remarks>
    [Fact]
    public void TheCompiledSources_AreTheTreesOwnFilesAndNotTheOracleFixtures() {
        var onDisk = Directory.EnumerateFiles(Corpus.SetRoot(Corpus.Real), "*.cs", SearchOption.AllDirectories)
            .Count();

        Assert.Equal(onDisk / 3, RuleCorpus.Sources().Count);
        Assert.DoesNotContain(
            RuleCorpus.Sources(),
            static file => file.Path.EndsWith(".expected.cs", StringComparison.Ordinal)
        );

        Assert.Equal(VendoredTrees, RuleCorpus.Trees().ToArray());
        Assert.Equal(RuleCorpus.Sources().Count, RuleCorpus.Trees().Sum(tree => RuleCorpus.Sources(tree).Count));

        // ⚠ Every file a tree's sweep compiles is under that tree, asserted rather than assumed.
        // `skala check <path>` in the default workspace mode ignores its path argument and analyses
        // the working directory instead (issue #284) — 4 573 findings about Skala's own source, none
        // of them about the corpus, and nothing in the output says so. A sweep that inherited that
        // would attribute one tree's findings to another; this is the assertion that it cannot.
        foreach (var tree in RuleCorpus.Trees()) {
            var root = Path.Combine(Corpus.SetRoot(Corpus.Real), tree) + Path.DirectorySeparatorChar;
            Assert.All(
                RuleCorpus.Sources(tree),
                file => Assert.StartsWith(root, Path.GetFullPath(file.Path), StringComparison.Ordinal)
            );
        }
    }

    /// <summary>
    ///     ⚠ The instrument's own calibration. If this drops, every zero it produced is meaningless.
    /// </summary>
    [Fact]
    public void TheHarness_KeepsFiringMostOfTheFixtureTree() {
        var recall = Measured.Recall;
        Assert.True(
            recall.Fired >= RecallFloor,
            $"the mass compile fires {recall.Fired.ToString(CultureInfo.InvariantCulture)} of {recall.Total.ToString(CultureInfo.InvariantCulture)} committed "
            + $"positives and the pinned floor is {RecallFloor.ToString(CultureInfo.InvariantCulture)}. The fixture tree only grows, so "
            + "this can only fall by the harness breaking — every corpus zero taken after this point is "
            + $"meaningless until it is repaired. First five that stopped firing: "
            + string.Join(", ", recall.Missed.Take(5))
        );

        Assert.True(
            recall.Fraction >= RecallFractionFloor,
            $"the mass compile models {(recall.Fraction * 100).ToString("F1", CultureInfo.InvariantCulture)}% of the fixture tree, below "
            + $"the {(RecallFractionFloor * 100).ToString("F0", CultureInfo.InvariantCulture)}% floor."
        );
    }

    /// <summary>
    ///     ⚠ A crashed analyzer produces exactly the same clean zero as a correct one.
    /// </summary>
    /// <remarks>
    ///     <c>AD0001</c> never reaches a report here, because <c>AnalyzerHost</c> installs an
    ///     <c>onAnalyzerException</c> callback and records <c>SK9030</c> instead — which is invisible
    ///     unless something reads it. This reads it.
    /// </remarks>
    [Fact]
    public void NoAnalyzerCrashed_InAnyCompilationTheSweepBuilds() {
        Assert.Empty(Measured.Recall.AnalyzerFailures);
        foreach (var result in Measured.Trees) {
            Assert.Empty(result.AnalyzerFailures);
        }
    }

    /// <summary>
    ///     ⚠ The artefact that lies in both directions, measured in the direction that proves it is gone.
    /// </summary>
    /// <remarks>
    ///     Without the SDK's implicit global usings a vendored slice binds <c>Dictionary&lt;,&gt;</c>,
    ///     <c>Task</c> and <c>Enumerable</c> to error types, so rules go quiet <em>and</em> their
    ///     exclusions stop matching. Not one of Serilog's 70 files carries <c>using System;</c>. The
    ///     compiler-error count falling is the evidence the compilation binds more than it did.
    /// </remarks>
    [Fact]
    public void TheImplicitUsings_BindStrictlyMoreThanTheirAbsence() {
        foreach (var result in Measured.Trees) {
            var without = Measured.WithoutUsings[result.Tree];
            Assert.True(
                result.CompilerErrors < without,
                $"{result.Tree}: {result.CompilerErrors.ToString(CultureInfo.InvariantCulture)} compiler error(s) with the implicit "
                + $"usings and {without.ToString(CultureInfo.InvariantCulture)} without them. The synthesised usings tree is not "
                + "reaching this compilation."
            );
        }

        // ⚠ The two trees whose numbers were argued over. Serilog's drop is the `[ThreadStatic]`
        // exclusion becoming matchable at all; Vixen's is `System.Linq` coming into scope.
        Assert.True(Measured.WithoutUsings["serilog"] - Measured.Single("serilog").CompilerErrors > 100);
        Assert.True(Measured.WithoutUsings["vixen"] - Measured.Single("vixen").CompilerErrors > 100);
    }

    /// <summary>
    ///     ⚠ The obstacle the whole issue is about: under <c>--load=loose</c> not one of these runs.
    /// </summary>
    /// <remarks>
    ///     <c>AnalyzerHost.SkippedFor(LoadMode.Loose)</c> names every <c>requiresSemantics</c> rule as
    ///     skipped, correctly — a rule answering "no finding" through an unresolved symbol makes a clean
    ///     report mean two things. This asserts the sweep is not in that mode: a large number of
    ///     <em>distinct semantic rules</em> produce findings on the vendored trees, which is exactly
    ///     what a loose sweep produces none of.
    /// </remarks>
    [Fact]
    public void SemanticRules_ActuallyRunOverTheVendoredTrees() {
        var semantic = Measured.Trees
            .SelectMany(static result => result.Findings)
            .Select(static finding => finding.RuleId)
            .Distinct(StringComparer.Ordinal)
            .Where(static id => RuleCatalog.Find(id) is { RequiresSemantics: true })
            .Order(StringComparer.Ordinal)
            .ToList();

        // ⚠ 48 when pinned, of the 247 rules that declare requiresSemantics. The floor is a collapse
        // detector rather than a ratchet on rule quality: a loose load makes this exactly 0, and a
        // rule legitimately becoming quieter must not have to argue with a tight number.
        Assert.True(
            semantic.Count >= 30,
            $"only {semantic.Count.ToString(CultureInfo.InvariantCulture)} rule(s) that declare requiresSemantics fired over the three "
            + "trees. A loose load skips all of them, so this number collapsing to nothing means the sweep "
            + $"has fallen back to the mode the corpus could never measure with: {string.Join(", ", semantic)}"
        );
    }

    /// <summary>
    ///     ⚠ The zero that counts, separated from the zero that does not.
    /// </summary>
    [Fact]
    public void APlantedShape_TellsDeclinedApartFromSilent() {
        foreach (var result in Measured.Trees) {
            foreach (var rule in Canaries) {
                Assert.Equal(CorpusVerdict.Declined, result.Verdict(rule));
            }

            // ⚠ And a rule nobody planted stays unclassified rather than borrowing somebody else's
            // canary. "No canary" and "the canary did not fire" are opposite states.
            Assert.Equal(CorpusVerdict.Unplanted, result.Verdict("SK5005"));
            Assert.Empty(result.CanariesSilent);
        }
    }

    /// <summary>
    ///     The synthesised usings tree is part of the instrument, so a finding on it would be the
    ///     harness measuring itself. Same for a planted canary: it is never a corpus finding.
    /// </summary>
    [Fact]
    public void TheInstrumentsOwnFiles_AreNeverReportedAsCorpusFindings() {
        foreach (var result in Measured.Trees) {
            Assert.DoesNotContain(
                result.Findings,
                static finding => finding.Path.Contains("__implicit__", StringComparison.Ordinal)
                    || finding.Path.Contains("__planted__", StringComparison.Ordinal)
            );
        }
    }

    /// <summary>
    ///     Every rule with a positive fixture can be planted, so no rule is stuck at
    ///     <see cref="CorpusVerdict.Unplanted" /> for want of a hand-written canary.
    /// </summary>
    [Fact]
    public void EveryRuleWithAPositiveFixture_HasACanary() {
        var withPositives = RuleCorpus.Positives()
            .Select(static entry => entry.RuleId)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        Assert.NotEmpty(withPositives);
        Assert.All(withPositives, id => Assert.NotNull(RuleCorpus.Canary(id)));
        Assert.Null(RuleCorpus.Canary("SK0000"));
    }

    static Measurement Measure() {
        var shapes = Canaries.Select(RuleCorpus.Canary).OfType<PlantedShape>().ToList();
        var trees = RuleCorpus.Trees()
            .Select(tree => RuleCorpus.Sweep(tree, shapes, true, CancellationToken.None))
            .ToImmutableArray();

        return new Measurement(
            RuleCorpus.Recall(CancellationToken.None),
            trees,
            RuleCorpus.Trees()
                .ToImmutableDictionary(
                    static tree => tree,
                    tree => RuleCorpus.CompilerErrors(tree, false, CancellationToken.None),
                    StringComparer.Ordinal
                )
        );
    }

    sealed record Measurement(
        CorpusRecall Recall,
        ImmutableArray<CorpusSweepResult> Trees,
        ImmutableDictionary<string, int> WithoutUsings) {
        public CorpusSweepResult Single(string tree) =>
            Trees.First(result => string.Equals(result.Tree, tree, StringComparison.Ordinal));
    }
}
