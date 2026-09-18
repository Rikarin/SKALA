using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Formatting.CSharp;
using Rikarin.Skala.Testing;
using System.Globalization;

namespace Rikarin.Skala.Conformance.Tests;

/// <summary>
///     The fuzzer, under test.
/// </summary>
/// <remarks>
///     ⚠ A fuzzer is a test that finds bugs, which makes it the one piece of test code whose own defects
///     are invisible: a fuzzer whose mutations never reach the formatter reports the same green run as a
///     formatter with no bugs in it. Every assertion here exists because the fuzzer had that defect at
///     some point during the day it was written —
///     <list type="bullet">
///         <item>
///             the grammar emitted a parse error in 147 units of 300, and ADR-003 leaves such a file
///             byte-identical, so every property held over it for free;
///         </item>
///         <item>
///             the minimiser normalised line endings in its split and returned an artefact that did not
///             fail;
///         </item>
///         <item>
///             the mutations wrote into text that is <c>DisabledTextTrivia</c> under the *other* symbol
///             set, and reported 1 639 absorption failures that were the fuzzer's own.
///         </item>
///     </list>
///     ⚠ The budgets here are small on purpose. This suite runs on every commit and its job is to prove
///     the machine works, not to find bugs; finding bugs is the nightly job's, with a time budget.
/// </remarks>
public sealed class FuzzerTests {
    /// <summary>
    ///     ⚠ The seed is the input: the same seed must rebuild the same case, byte for byte, forever.
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
    ///     ⚠ SplitMix64 is pinned by its constants, so this vector is the contract with every future
    ///     runtime. <see cref="Random" /> would not have one — its stream for a given seed has changed
    ///     between .NET versions, which would make a seed recorded in a nightly log a decoration.
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
    ///     The generator's contract: semantic nonsense is welcome, a parse error is not.
    /// </summary>
    /// <remarks>
    ///     ⚠ Zero, not "few". A generated unit that does not parse is a case the formatter refuses to
    ///     touch by policy, so it passes every property while asserting none of them — the failure mode
    ///     where the fuzzer looks healthiest.
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
    ///     ⚠ A mutation that breaks the parse is a case that asserted nothing.
    /// </summary>
    /// <remarks>
    ///     ⚠ <b>Zero, and it used to be "at most 5 %".</b> The allowance was the honest reading of
    ///     "parse-preserving is a property of nineteen text transforms over arbitrary C#" — and an
    ///     allowance is exactly the shape of hole this suite exists to close, because the cases inside
    ///     it are the ones that *look* like passes. The nightly of run 33 148 756 015 lost 360 property
    ///     checks that way and reported them as its own defect in its own words.
    ///     <para>
    ///         It is zero now because <c>Fuzzer.Build</c> no longer assumes the contract, it checks it: a
    ///         mutation whose result reports a parse error the input did not is dropped and counted, under
    ///         both symbol sets. So this test is no longer a bound on how often the catalogue is wrong; it
    ///         is an assertion that the guard is in place.
    ///     </para>
    ///     <para>
    ///         ⚠ The refusals are asserted to be zero as well, which is the stronger half. A guard that
    ///         quietly discards a tenth of the mutations it is handed would satisfy the first assertion
    ///         perfectly while halving the fuzzer.
    ///     </para>
    /// </remarks>
    [Fact]
    public void TheMutations_KeepTheFileParsing() {
        var corpus = Corpus.All();
        var lost = new List<string>();
        var refused = new List<string>();
        for (var index = 0; index < 300; index++) {
            var seed = FuzzRandom.Derive(23, index);
            var subject = Fuzzer.Build(seed, FuzzMode.Both, corpus);
            refused.AddRange(subject.Rejected.Select(name => FuzzRandom.Format(seed) + ": " + name));
            if (subject.Mutations.IsEmpty) {
                continue;
            }

            if (Errors(subject.Text) > Errors(subject.Baseline)) {
                lost.Add(
                    FuzzRandom.Format(seed) + ": " + string.Join(", ", subject.Mutations.Select(static m => m.Name))
                );
            }
        }

        Assert.True(
            lost.Count == 0,
            $"{lost.Count.ToString(CultureInfo.InvariantCulture)} of 300 cases stopped parsing, so they "
            + "asserted nothing:\n"
            + string.Join("\n", lost.Take(5))
        );

        Assert.True(
            refused.Count == 0,
            $"{refused.Count.ToString(CultureInfo.InvariantCulture)} mutations were refused for not "
            + "preserving the parse. The guard caught them, so no case was wasted — but a mutation in "
            + "the catalogue is not parse-preserving and should be fixed rather than filtered:\n"
            + string.Join("\n", refused.Take(5))
        );
    }

    /// <summary>
    ///     ⚠ A contextual keyword is an <c>IdentifierToken</c> and is not an identifier.
    /// </summary>
    /// <remarks>
    ///     Its meaning is its spelling. <c>var</c> is the one that matters, because the generator emits
    ///     it constantly and <c>foreach (var (k, w) in items)</c> renamed to
    ///     <c>foreach (var_wwww (k, w) in items)</c> is not a wider deconstruction — it is
    ///     <c>CS0230</c>, ADR-003 leaves the file byte-identical, and every property then holds over it
    ///     for free. Two of the three parse-lost seeds the nightly printed were this.
    ///     <para>
    ///         ⚠ Asserted on the mutation rather than on a sampled rate. A rate over 300 cases does not see
    ///         a defect that costs one case in six hundred, which is exactly how this one survived: the
    ///         bound above it was 5 % and the truth was 0.2 %, so nothing was ever red.
    ///     </para>
    /// </remarks>
    [Fact]
    public void WidenIdentifier_NeverRenamesAContextualKeyword() {
        const string source = """
                              class C {
                                  void M() {
                                      foreach (var (k, w) in items) {
                                          var value = k;
                                      }
                                  }
                              }
                              """;

        for (var index = 0; index < 200; index++) {
            var mutated = FuzzMutations.Apply(
                FuzzMutations.WidenIdentifier,
                source,
                new FuzzRandom(FuzzRandom.Derive(37, index)),
                []
            );

            if (mutated is null) {
                continue;
            }

            Assert.DoesNotContain("var_", mutated, StringComparison.Ordinal);
            Assert.Equal(0, Errors(mutated));
        }
    }

    /// <summary>
    ///     ⚠ A mutate case is a function of its seed <b>and its origin</b>, and the seed alone is not
    ///     enough.
    /// </summary>
    /// <remarks>
    ///     The file is <c>corpus[random.Next(corpus.Count)]</c>, so every mutate seed re-points the
    ///     moment the corpus grows — and "the corpus only grows" is this project's own policy. Measured:
    ///     the nightly reported a <c>token-equivalence</c> finding on
    ///     <c>pathological/mixed-line-endings-after-a-trailing-comment.cs</c> at seed
    ///     <c>10527204340983520508</c>; twenty-three corpus files later that seed replays
    ///     <c>pathological/very-long-line.cs</c>. The finding was real and had to be reproduced from its
    ///     artefact, because the seed had quietly stopped naming it.
    ///     <para>
    ///         ⚠ The second half is what makes the pair exact: the mutation sequence must be the one the
    ///         seed drew, unchanged by the substitution. A shorter corpus and a longer one must give the
    ///         same case for the same <c>(seed, origin)</c>, which is what the two builds below compare.
    ///     </para>
    /// </remarks>
    [Fact]
    public void AMutateCase_IsAFunctionOfItsSeedAndItsOrigin() {
        var corpus = Corpus.All();
        var pinned = corpus.First(static file => file.RelativePath.EndsWith(".cs", StringComparison.Ordinal));

        // A corpus of a different size, standing in for the corpus this seed will meet next month.
        var grown = corpus.Concat(corpus.Take(7)).ToList();

        for (var index = 0; index < 30; index++) {
            var seed = FuzzRandom.Derive(31, index);
            var here = Fuzzer.Build(seed, FuzzMode.Mutate, corpus, pinned.ToString());
            var later = Fuzzer.Build(seed, FuzzMode.Mutate, grown, pinned.ToString());

            Assert.Equal(pinned.ToString(), here.Origin);
            Assert.Equal(here.Origin, later.Origin);
            Assert.Equal(here.Text, later.Text);
            Assert.Equal(
                here.Mutations.Select(static mutation => mutation.Name),
                later.Mutations.Select(static mutation => mutation.Name)
            );
        }
    }

    /// <summary>
    ///     ⚠ Break the formatter deliberately, one property at a time, and check that the fuzzer notices.
    /// </summary>
    /// <remarks>
    ///     This is the answer to "a fuzzer that finds nothing on its first outing is more likely to be
    ///     weak than the code to be perfect". It caught a real gap: <c>range-consistency</c> as first
    ///     written could not tell a correct edit list from one collapsed into a single whole-file edit —
    ///     the count matched, the containment held, nothing overlapped — so range formatting could have
    ///     become whole-file formatting with every property still green. The `edit-merge` saboteur
    ///     survived 400 cases and the property was strengthened until it did not.
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
                    false,
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
    ///     ⚠ The minimiser's answer must still fail, and must be smaller.
    /// </summary>
    /// <remarks>
    ///     The predicate here is synthetic — "the text contains the marker" — so that this test measures
    ///     the reducer and not the formatter. A reducer that returns an artefact which does not exhibit
    ///     the failure is worse than one that returns the original: the corpus entry it produces pins
    ///     nothing and looks as though it does, which is what happened before <c>Split</c> stopped
    ///     normalising line endings.
    /// </remarks>
    [Fact]
    public void TheMinimiser_ReturnsSomethingSmallerThatStillFails() {
        var source = FuzzGenerator.Compile(new FuzzRandom(99));
        var marked = "class Marker { void Keep() { Trigger(); } }\n" + source;
        var budget = new MinimiseBudget(4000);
        var reduced = FuzzMinimiser.Minimise(
            marked,
            static candidate => candidate.Contains("Trigger()", StringComparison.Ordinal),
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
    ///     ⚠ A whitespace-only mutation must never touch a byte that is data.
    /// </summary>
    /// <remarks>
    ///     Asserted as token equivalence over the mutation itself, under <b>both</b> symbol sets, which
    ///     is the only statement of "this was whitespace" that does not beg the question. It is the
    ///     assertion that would have caught the two defects that produced 3 500 false absorption reports
    ///     between them: a space written into a `#if` branch that is disabled under the other set, and a
    ///     space written into an XML text token in the middle of a `///` run.
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
    ///     ⚠ An absorbed mutation may not move a comment off the first column.
    /// </summary>
    /// <remarks>
    ///     <c>skala_stick_comment</c> — "Don't indent comments started at first column" — makes
    ///     a comment's <em>source column</em> an input the oracle reads, and "at the first column" is
    ///     literal. The fixture's own oracle output is the measurement: the comment written at column 0
    ///     comes back at column 0, and the one written at column 2 comes back at the code's indent. So the
    ///     single space the <c>indent</c> mutation used to insert in front of a column-0 comment was not
    ///     whitespace in the sense absorption means — it was the key's entire input, and the violation it
    ///     produced was a fact about the <b>probe</b>, not about the formatter. Absorbing it would be
    ///     asserting that Skala should diverge from the oracle on purpose.
    ///     <para>
    ///         ⚠ This is the same correction <see cref="FuzzMutations.SourceMap.AbsorbableGaps" /> already
    ///         carried for the gap beside a <c>..</c>, arrived at from the other end: there a key
    ///         <em>preserves</em> the author's spacing, here a key <em>reads</em> it. Both make
    ///         <c>format(mutate_whitespace(x)) ≡ format(x)</c> false as stated over one span class.
    ///     </para>
    ///     <para>
    ///         ⚠ Asserted as the property itself rather than as the shape of the mutation, because the
    ///         mutation is the thing under suspicion. Seeded rather than replayed from
    ///         <c>17198075540958731069</c>: excluding a line changes how much randomness the draw
    ///         consumes, so that seed no longer builds that case, and a regression test keyed to a seed
    ///         would be pinning the fuzzer's arithmetic instead of the defect.
    ///     </para>
    /// </remarks>
    [Fact]
    public void AnAbsorbedMutation_NeverMovesACommentOffTheFirstColumn() {
        var path = Path.Combine(
            Corpus.SetRoot(Corpus.Constructs),
            "trivia",
            "skala_stick_comment.cs"
        );

        var source = File.ReadAllText(path);

        // The fixture is the origin the nightly's finding was minimised from, and it has to keep
        // carrying the shape: a comment hard against the left margin, inside a body and outside one.
        Assert.Contains("\n// stuck to the left margin", source, StringComparison.Ordinal);
        Assert.Contains("\n// inside a body, at column zero", source, StringComparison.Ordinal);

        var options = Fuzzer.OptionsFor(path);
        var applied = 0;
        for (var index = 0; index < 400; index++) {
            var random = new FuzzRandom(FuzzRandom.Derive(17198075540958731069, index));
            var mutation = FuzzMutations.Apply(source, random, Corpus.PropertySymbols, FuzzMutations.AbsorbedNames);
            if (mutation is null) {
                continue;
            }

            applied++;
            foreach (var symbols in (IReadOnlyList<string>[])[[], Corpus.PropertySymbols]) {
                Assert.Equal(
                    FuzzProperties.Format(path, source, options, symbols),
                    FuzzProperties.Format(path, mutation.Text, options, symbols)
                );
            }
        }

        // ⚠ A guard on the assertion above, not decoration. If the protection were widened until
        // nothing on this fixture were mutable at all, every `Assert.Equal` would pass vacuously and
        // the suite would go green on a fuzzer that had stopped fuzzing the file the defect lives in.
        Assert.True(
            applied > 200,
            $"only {applied.ToString(CultureInfo.InvariantCulture)} of 400 draws mutated the fixture; "
            + "the column-zero protection has been widened until the file is no longer fuzzed."
        );
    }

    /// <summary>
    ///     ⚠ The whitespace mutations may not touch a gap <see cref="SpaceRules.Preserves" /> answers.
    /// </summary>
    /// <remarks>
    ///     #376: <c>widen-gap</c> put a space into <c>o is Point(2, 3)</c>, the formatter kept it — which
    ///     is what the oracle does, #373 measured it in every position — and the absorption property
    ///     called the difference a violation. The exclusion used to be a hand-kept list ("any gap touching
    ///     a <c>..</c>"), which is why the range operator never tripped the property and the pattern gap
    ///     did the day it joined <c>SpaceRules.Ungoverned</c>. It is now the formatter's own predicate, so
    ///     this test asks the mutation, not the list: over a file holding one of each ungoverned gap and
    ///     one governed gap of the same surface shape, every draw of <c>widen-gap</c> must leave the two
    ///     ungoverned gaps as written, and at least one draw must widen the governed one — or the file is
    ///     no longer being fuzzed and the assertion above it is vacuous.
    /// </remarks>
    [Fact]
    public void WidenGap_LeavesEveryUngovernedGapAlone() {
        const string source = """
                              class C {
                                  bool P(object o) => o is Point(2, 3);
                                  int[] R(int[] a) => a[1..2];
                                  void M (int x) { }
                              }
                              record Point(int X, int Y);
                              """;

        static int After(string marker) => source.IndexOf(marker, StringComparison.Ordinal) + marker.Length;
        var pattern = After("o is Point");
        var rangeLeft = After("a[1");
        var rangeRight = After("a[1..");
        var governed = After("void M");

        // The map first: the two ungoverned gaps are gaps — a structural mutation may still split a
        // line there — and are not absorbable; the governed one is both.
        var map = FuzzMutations.SourceMap.Of(source, Corpus.PropertySymbols);
        Assert.Contains(map.Gaps, gap => gap.Start == pattern && gap.Length == 0);
        Assert.Contains(map.Gaps, gap => gap.Start == rangeLeft && gap.Length == 0);
        Assert.Contains(map.Gaps, gap => gap.Start == rangeRight && gap.Length == 0);
        Assert.DoesNotContain(map.AbsorbableGaps, gap => gap.Start == pattern);
        Assert.DoesNotContain(map.AbsorbableGaps, gap => gap.Start == rangeLeft);
        Assert.DoesNotContain(map.AbsorbableGaps, gap => gap.Start == rangeRight);
        Assert.Contains(map.AbsorbableGaps, gap => gap.Start == governed && gap.Length == 1);

        // Then the mutated text itself, which is what the property is asserted over.
        var applied = 0;
        var widenedTheGovernedGap = 0;
        for (ulong seed = 0; seed < 200; seed++) {
            var text = FuzzMutations.Apply(
                FuzzMutations.WidenGap,
                source,
                new FuzzRandom(seed),
                Corpus.PropertySymbols
            );
            if (text is null) {
                continue;
            }

            applied++;

            // ⚠ Walked token to token rather than matched as a substring: the gaps *around* each
            // probe — `o`→`is`, `void`→`M` — are governed and may be widened in the same draw.
            static int SkipSpaces(string text, int index) {
                while (text[index] == ' ') {
                    index++;
                }

                return index;
            }

            var afterIs = SkipSpaces(text, text.IndexOf(" is ", StringComparison.Ordinal) + " is".Length);
            Assert.StartsWith("Point(", text[afterIs..], StringComparison.Ordinal);
            Assert.Contains("1..2", text, StringComparison.Ordinal);

            var m = SkipSpaces(text, text.IndexOf("void", StringComparison.Ordinal) + "void".Length);
            Assert.Equal('M', text[m]);
            var open = SkipSpaces(text, m + 1);
            Assert.Equal('(', text[open]);
            if (open - (m + 1) > 1) {
                widenedTheGovernedGap++;
            }
        }

        Assert.True(
            applied > 100,
            $"only {applied.ToString(CultureInfo.InvariantCulture)} of 200 draws mutated the file"
        );
        Assert.True(
            widenedTheGovernedGap > 0,
            "no draw widened the governed gap `M (x)`; the exclusion has been widened until the file is not fuzzed"
        );
    }

    /// <summary>
    ///     ⚠ The property the exclusion above stands on, against the oracle's own answers: for every
    ///     ungoverned gap, the closed spelling and the spaced spelling each format to what the oracle
    ///     returns for that spelling, and each is a fixed point.
    /// </summary>
    /// <remarks>
    ///     The two constructs carry every shape twice, closed and spaced, and the oracle returned each as
    ///     written — that is what "ungoverned" means and how #373 established it. So the oracle's answer
    ///     for an input with one gap flipped is <em>known</em>: it is the fixture with the same gap
    ///     flipped, because the fixture already holds the other spelling of that shape in the
    ///     neighbouring member. Each flippable gap is flipped on its own and then all of them together,
    ///     and each input is formatted twice. <see cref="FuzzProperties.UngovernedGaps" /> is the same
    ///     claim without the oracle — the bit comes back — over every input the fuzzer builds.
    ///     <para>
    ///         ⚠ The count guards are not decoration. If <c>SpaceRules.Preserves</c> stopped answering these
    ///         gaps the flips would be empty and every assertion would pass over the unflipped file.
    ///     </para>
    /// </remarks>
    [Theory]
    [InlineData("positional-pattern-after-its-type", false)]
    [InlineData("positional-pattern-after-its-type", true)]
    [InlineData("range-operator-gap", false)]
    [InlineData("range-operator-gap", true)]
    public void UngovernedGaps_EitherSpelling_FormatsToTheOraclesAnswer(string construct, bool defined) {
        var file = Corpus.Files(Corpus.Constructs)
            .Single(entry => string.Equals(
                    entry.RelativePath.Replace('\\', '/'),
                    "syntax/" + construct + ".cs",
                    StringComparison.Ordinal
                )
            );

        var source = File.ReadAllText(file.Path);
        var oracle = TextNormalisation.Normalise(OracleFixture.Read(file));
        IReadOnlyList<string> symbols = defined ? Corpus.PropertySymbols : [];

        var gaps = FuzzProperties.UngovernedGapsOf(source, symbols);
        var oracleGaps = FuzzProperties.UngovernedGapsOf(oracle, symbols);
        Assert.True(
            gaps.Count(static gap => gap.Spaced is true) >= 3,
            "the construct no longer holds spaced spellings"
        );
        Assert.True(
            gaps.Count(static gap => gap.Spaced is false) >= 3,
            "the construct no longer holds closed spellings"
        );
        Assert.Equal(gaps.Length, oracleGaps.Length);

        // As written, first: the flipped claims below are read off this one.
        Assert.Equal(oracle, TextNormalisation.Normalise(CorpusFormatter.Format(file, source, defined).Formatted));
        for (var i = 0; i < gaps.Length; i++) {
            if (gaps[i].Spaced is { } wrote) {
                Assert.Equal(wrote, oracleGaps[i].Spaced);
            }
        }

        var flips = new List<int[]>();
        for (var i = 0; i < gaps.Length; i++) {
            if (gaps[i].Spaced is not null) {
                flips.Add([i]);
            }
        }

        flips.Add([.. flips.Select(static flip => flip[0])]);
        foreach (var flip in flips) {
            var input = FuzzProperties.FlipUngovernedGaps(source, [.. flip.Select(i => gaps[i])])!;
            var expected = FuzzProperties.FlipUngovernedGaps(oracle, [.. flip.Select(i => oracleGaps[i])])!;
            var first = CorpusFormatter.Format(file, input, defined);
            Assert.Equal(FormatOutcome.Formatted, first.Outcome);
            Assert.Equal(TextNormalisation.Normalise(expected), TextNormalisation.Normalise(first.Formatted));

            var second = CorpusFormatter.Format(file, first.Formatted, defined);
            Assert.Empty(second.Edits);
        }
    }

    /// <summary>
    ///     ⚠ <see cref="FuzzProperties.UngovernedGaps" /> holds over the two constructs as the fuzzer
    ///     asserts it, and reports when a formatter absorbs the gap.
    /// </summary>
    /// <remarks>
    ///     The <c>gap-closer</c> saboteur is the defect #373 removed, in the other direction: a rule that
    ///     answers an ungoverned gap. A property that stays green under it is the hole the exclusion
    ///     would otherwise be.
    /// </remarks>
    [Theory]
    [InlineData("positional-pattern-after-its-type")]
    [InlineData("range-operator-gap")]
    public void UngovernedGaps_IsAssertedByTheFuzzer_AndTripsOnAClosingRule(string construct) {
        var path = Path.Combine(Corpus.SetRoot(Corpus.Constructs), "syntax", construct + ".cs");
        var source = File.ReadAllText(path);
        var options = Fuzzer.OptionsFor(path);

        var cancellation = TestContext.Current.CancellationToken;
        var clean = FuzzProperties.Check(path, source, options, Corpus.PropertySymbols, cancellation: cancellation);
        Assert.Empty(clean);

        var closer = FuzzProperties.Saboteurs.Single(static saboteur => saboteur.Name == "gap-closer");
        var sabotaged = FuzzProperties.Check(
            path,
            source,
            options,
            Corpus.PropertySymbols,
            saboteur: closer,
            cancellation: cancellation
        );
        Assert.Contains(sabotaged, static violation => violation.Property == FuzzProperties.UngovernedGaps);
    }

    /// <summary>
    ///     A short, fixed-seed run, so the driver itself is exercised on every commit.
    /// </summary>
    /// <remarks>
    ///     ⚠ It asserts *coverage*, not absence of findings. Whether a 250-case run finds something is
    ///     not this suite's business — the nightly job runs for an hour and what it finds is triaged
    ///     into GitHub issues. What must hold on every commit is that the cases reach the formatter at
    ///     all, which is the one thing a broken fuzzer cannot fake.
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
