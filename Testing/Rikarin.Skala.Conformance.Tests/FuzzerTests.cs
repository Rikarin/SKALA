using System.Globalization;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Formatting.CSharp;
using Rikarin.Skala.Testing;

namespace Rikarin.Skala.Conformance.Tests;

/// <summary>
/// The fuzzer, under test.
/// </summary>
/// <remarks>
/// ⚠ A fuzzer is a test that finds bugs, which makes it the one piece of test code whose own defects
/// are invisible: a fuzzer whose mutations never reach the formatter reports the same green run as a
/// formatter with no bugs in it. Every assertion here exists because the fuzzer had that defect at
/// some point during the day it was written —
/// <list type="bullet">
/// <item>the grammar emitted a parse error in 147 units of 300, and ADR-003 leaves such a file
/// byte-identical, so every property held over it for free;</item>
/// <item>the minimiser normalised line endings in its split and returned an artefact that did not
/// fail;</item>
/// <item>the mutations wrote into text that is <c>DisabledTextTrivia</c> under the *other* symbol
/// set, and reported 1 639 absorption failures that were the fuzzer's own.</item>
/// </list>
/// ⚠ The budgets here are small on purpose. This suite runs on every commit and its job is to prove
/// the machine works, not to find bugs; finding bugs is the nightly job's, with a time budget.
/// </remarks>
public sealed class FuzzerTests {
    /// <summary>
    /// ⚠ The seed is the input: the same seed must rebuild the same case, byte for byte, forever.
    /// </summary>
    [Fact]
    public void ACase_IsAFunctionOfItsSeedAndNothingElse() {
        var corpus = Corpus.All();
        for (var index = 0; index < 40; index++) {
            var seed = FuzzRandom.Derive(11, index);
            var first = Fuzzer.Build(seed, FuzzMode.Both, corpus);
            var second = Fuzzer.Build(seed, FuzzMode.Both, corpus);
            Assert.Equal(first.Text, second.Text);
            Assert.Equal(first.Origin, second.Origin);
            Assert.Equal(
                first.Mutations.Select(static mutation => mutation.Name),
                second.Mutations.Select(static mutation => mutation.Name)
            );
        }
    }

    /// <summary>
    /// ⚠ SplitMix64 is pinned by its constants, so this vector is the contract with every future
    /// runtime. <see cref="Random"/> would not have one — its stream for a given seed has changed
    /// between .NET versions, which would make a seed recorded in a nightly log a decoration.
    /// </summary>
    [Fact]
    public void TheStream_IsPinned() {
        var random = new FuzzRandom(1);
        Assert.Equal(10451216379200822465UL, random.NextULong());
        Assert.Equal(13757245211066428519UL, random.NextULong());
        Assert.Equal(17911839290282890590UL, random.NextULong());
        Assert.Equal(6238072747940578789UL, FuzzRandom.Derive(1, 0));
        Assert.Equal(5UL, FuzzRandom.Parse("5"));
        Assert.Equal(255UL, FuzzRandom.Parse("0xff"));
    }

    /// <summary>
    /// The generator's contract: semantic nonsense is welcome, a parse error is not.
    /// </summary>
    /// <remarks>
    /// ⚠ Zero, not "few". A generated unit that does not parse is a case the formatter refuses to
    /// touch by policy, so it passes every property while asserting none of them — the failure mode
    /// where the fuzzer looks healthiest.
    /// </remarks>
    [Fact]
    public void TheGrammar_EmitsNoParseErrors() {
        var broken = new List<string>();
        for (var index = 0; index < 250; index++) {
            var source = FuzzGenerator.Compile(new FuzzRandom(FuzzRandom.Derive(7, index)));
            var errors = CSharpSyntaxTree
                .ParseText(
                    SourceText.From(source),
                    CSharpFormatter.ParseOptions,
                    string.Empty,
                    TestContext.Current.CancellationToken
                )
                .GetDiagnostics(TestContext.Current.CancellationToken)
                .Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
                .ToArray();

            if (errors.Length > 0) {
                broken.Add(
                    FuzzRandom.Format(FuzzRandom.Derive(7, index))
                    + ": "
                    + errors[0].Id
                    + " "
                    + errors[0].GetMessage(CultureInfo.InvariantCulture)
                );
            }
        }

        Assert.True(
            broken.Count == 0,
            $"{broken.Count.ToString(CultureInfo.InvariantCulture)} of 250 generated units do not parse. "
            + "`fuzz --grammar-check=N` prints the histogram:\n"
            + string.Join("\n", broken.Take(5))
        );
    }

    /// <summary>
    /// ⚠ A mutation that breaks the parse is a case that asserted nothing.
    /// </summary>
    /// <remarks>
    /// A small allowance rather than zero, because "parse-preserving" is a property of nineteen text
    /// transforms over arbitrary C# and the last fraction of a percent is not worth the coverage it
    /// would cost to buy. What the bound stops is a *regression* — a new mutation, or a widened
    /// existing one, that quietly starts throwing away one case in five. The measured rate over
    /// 15 624 cases of `corpus/` is 0.3 % of property checks.
    /// </remarks>
    [Fact]
    public void TheMutations_KeepTheFileParsing() {
        var corpus = Corpus.All();
        var lost = 0;
        var total = 0;
        for (var index = 0; index < 300; index++) {
            var subject = Fuzzer.Build(FuzzRandom.Derive(23, index), FuzzMode.Mutate, corpus);
            if (subject.Mutations.IsEmpty) {
                continue;
            }

            total++;
            var before = Errors(subject.Baseline);
            var after = Errors(subject.Text);
            if (after > before) {
                lost++;
            }
        }

        Assert.True(
            lost * 100 <= total * 5,
            $"{lost.ToString(CultureInfo.InvariantCulture)} of {total.ToString(CultureInfo.InvariantCulture)} "
            + "mutated cases stopped parsing; the bound is 5 %."
        );
    }

    /// <summary>
    /// ⚠ Break the formatter deliberately, one property at a time, and check that the fuzzer notices.
    /// </summary>
    /// <remarks>
    /// This is the answer to "a fuzzer that finds nothing on its first outing is more likely to be
    /// weak than the code to be perfect". It caught a real gap: <c>range-consistency</c> as first
    /// written could not tell a correct edit list from one collapsed into a single whole-file edit —
    /// the count matched, the containment held, nothing overlapped — so range formatting could have
    /// become whole-file formatting with every property still green. The `edit-merge` saboteur
    /// survived 400 cases and the property was strengthened until it did not.
    /// </remarks>
    [Fact]
    public void EverySaboteur_IsCaughtByThePropertyItBreaks() {
        var corpus = Corpus.All();
        var survivors = new List<string>();
        foreach (var saboteur in FuzzProperties.Saboteurs) {
            var caught = false;
            for (var index = 0; index < 60 && !caught; index++) {
                var subject = Fuzzer.Build(FuzzRandom.Derive(31, index), FuzzMode.Both, corpus);
                var (violations, _) = Fuzzer.Execute(
                    subject,
                    arrangement: false,
                    saboteur,
                    TestContext.Current.CancellationToken
                );

                caught = violations.Any(violation =>
                    string.Equals(violation.Property, saboteur.Target, StringComparison.Ordinal)
                );
            }

            if (!caught) {
                survivors.Add(saboteur.Name + " (breaks " + saboteur.Target + ")");
            }
        }

        Assert.True(
            survivors.Count == 0,
            "A property no saboteur can trip is a property that is not being asserted. Survivors: "
            + string.Join(", ", survivors)
        );
    }

    /// <summary>
    /// ⚠ The minimiser's answer must still fail, and must be smaller.
    /// </summary>
    /// <remarks>
    /// The predicate here is synthetic — "the text contains the marker" — so that this test measures
    /// the reducer and not the formatter. A reducer that returns an artefact which does not exhibit
    /// the failure is worse than one that returns the original: the corpus entry it produces pins
    /// nothing and looks as though it does, which is what happened before <c>Split</c> stopped
    /// normalising line endings.
    /// </remarks>
    [Fact]
    public void TheMinimiser_ReturnsSomethingSmallerThatStillFails() {
        var source = FuzzGenerator.Compile(new FuzzRandom(99));
        var marked = "class Marker { void Keep() { Trigger(); } }\n" + source;
        var budget = new MinimiseBudget(4000);
        var reduced = FuzzMinimiser.Minimise(
            marked,
            candidate => candidate.Contains("Trigger()", StringComparison.Ordinal),
            budget
        );

        Assert.Contains("Trigger()", reduced, StringComparison.Ordinal);
        Assert.True(
            reduced.Length < marked.Length / 4,
            $"reduced {marked.Length.ToString(CultureInfo.InvariantCulture)} characters to only "
            + $"{reduced.Length.ToString(CultureInfo.InvariantCulture)}."
        );
    }

    /// <summary>
    /// ⚠ A whitespace-only mutation must never touch a byte that is data.
    /// </summary>
    /// <remarks>
    /// Asserted as token equivalence over the mutation itself, under <b>both</b> symbol sets, which
    /// is the only statement of "this was whitespace" that does not beg the question. It is the
    /// assertion that would have caught the two defects that produced 3 500 false absorption reports
    /// between them: a space written into a `#if` branch that is disabled under the other set, and a
    /// space written into an XML text token in the middle of a `///` run.
    /// </remarks>
    [Fact]
    public void AnAbsorbedMutation_ChangesNoToken() {
        var corpus = Corpus.All();
        for (var index = 0; index < 200; index++) {
            var random = new FuzzRandom(FuzzRandom.Derive(43, index));
            var file = corpus[random.Next(corpus.Count)];
            var source = File.ReadAllText(file.Path);
            var mutation = FuzzMutations.Apply(source, random, Corpus.PropertySymbols, FuzzMutations.AbsorbedNames);
            if (mutation is null) {
                continue;
            }

            foreach (var symbols in (IReadOnlyList<string>[])[[], Corpus.PropertySymbols]) {
                var failure = TokenEquivalence.Compare(
                    SourceText.From(source),
                    SourceText.From(mutation.Text),
                    CSharpFormatter.ParseOptionsFor(symbols)
                );

                Assert.True(
                    failure is null,
                    $"{file}: `{mutation.Name}` is declared whitespace-only and changed a token"
                    + (symbols.Count == 0 ? " with no symbols" : " with symbols")
                    + ": "
                    + failure?.Before
                    + " became "
                    + failure?.After
                );
            }
        }
    }

    /// <summary>
    /// A short, fixed-seed run, so the driver itself is exercised on every commit.
    /// </summary>
    /// <remarks>
    /// ⚠ It asserts *coverage*, not absence of findings. Whether a 250-case run finds something is
    /// not this suite's business — the nightly job runs for an hour and the register in
    /// <c>corpus/pathological/open/</c> holds what it found. What must hold on every commit is that
    /// the cases reach the formatter at all, which is the one thing a broken fuzzer cannot fake.
    /// </remarks>
    [Fact]
    public void AShortRun_ReachesTheFormatter() {
        var report = Fuzzer.Run(
            new FuzzOptions {
                Seed = 3,
                Cases = 250,
                Mode = FuzzMode.Both,
                ArrangeEvery = 50,
                Minimise = false,
                OutputDirectory = null
            },
            TextWriter.Null,
            TestContext.Current.CancellationToken
        );

        Assert.Equal(250, report.Cases);
        Assert.True(
            report.CasesThatChangedSomething * 2 > report.Cases,
            $"only {report.CasesThatChangedSomething.ToString(CultureInfo.InvariantCulture)} of "
            + $"{report.Cases.ToString(CultureInfo.InvariantCulture)} cases produced an edit; the "
            + "mutations are not reaching the formatter."
        );

        Assert.True(report.GeneratedUnits > 0, "no compilation unit was generated in 250 cases.");
        Assert.NotEmpty(report.CorpusFilesTouched);
        Assert.True(report.ArrangementChecks > 0, "the arrange-and-format pair never ran.");

        // ⚠ Every mutation in the catalogue is drawn. A production that is never drawn is a
        // production that is not tested, and a weight typo is exactly how one goes quiet.
        var drawn = report.MutationsApplied.Keys.ToHashSet(StringComparer.Ordinal);
        var missing = FuzzMutations.Catalogue
            .Select(static entry => entry.Name)
            .Where(name => !drawn.Contains(name))
            .ToArray();

        Assert.True(missing.Length == 0, "never drawn in 250 cases: " + string.Join(", ", missing));
    }

    static int Errors(string source) =>
        CSharpSyntaxTree.ParseText(SourceText.From(source), CSharpFormatter.ParseOptions)
            .GetDiagnostics()
            .Count(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
}
