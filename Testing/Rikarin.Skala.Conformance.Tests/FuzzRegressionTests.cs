using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Formatting.CSharp;
using Rikarin.Skala.Testing;

namespace Rikarin.Skala.Conformance.Tests;

public sealed class FuzzRegressionTests {
    [Theory]
    [InlineData("nested-collection-in-generated-switch", false)]
    [InlineData("nested-collection-in-generated-switch", true)]
    [InlineData("nested-collection-in-generated-while", false)]
    [InlineData("nested-collection-in-generated-while", true)]
    [InlineData("nested-switch-in-generated-tuple-conditional", false)]
    [InlineData("nested-switch-in-generated-tuple-conditional", true)]
    [InlineData("nested-switch-in-generated-tuple-condition", false)]
    [InlineData("nested-switch-in-generated-tuple-condition", true)]
    public void NestedMultilineItems_AreIdempotentUnderTheFuzzerConfiguration(string fixture, bool defined) {
        // Minimized nightly findings need the repository configuration, not bare CLI defaults.
        var path = Path.Combine(Corpus.Root, "pathological", $"{fixture}.cs");
        var source = SourceText.From(File.ReadAllText(path));
        var options = new PhaseOneOptions(Fuzzer.OptionsFor(path));
        IReadOnlyList<string> symbols = defined ? Corpus.PropertySymbols : [];
        var first = CSharpFormatter.Format(path, source, options, preprocessorSymbols: symbols);
        var second = CSharpFormatter.Format(
            path,
            SourceText.From(first.Formatted),
            options,
            preprocessorSymbols: symbols
        );
        Assert.True(first.Changed);
        Assert.Empty(second.Edits);
        Assert.Null(
            TokenEquivalence.Compare(source, SourceText.From(first.Formatted), CSharpFormatter.ParseOptionsFor(symbols))
        );
    }

    [Theory]
    [InlineData(5423343295399047858UL)]
    [InlineData(11149039553341969427UL)]
    [InlineData(13458345604094946523UL)]
    [InlineData(6636340479617988337UL)]

    // ⚠ The Nightly's September findings, kept here because the runs they came from are deleted
    // once fixed: this theory is the only record that replays. The first six are one class —
    // a property pattern heading a switch arm or a `case` label, chopped on pass one and joined
    // on pass two (#378) — and the last is the flat direction of a collection-valued `=` (#379).
    [InlineData(2801382500469017888UL)]
    [InlineData(13095184792041486380UL)]
    [InlineData(11255509907099259375UL)]
    [InlineData(857717698562573229UL)]
    [InlineData(14871250529025744122UL)]
    [InlineData(2742638269065363150UL)]
    [InlineData(7611825995831206751UL)]
    [InlineData(3296757264995743770UL)]

    // ⚠ Fourteen consecutive Nightly runs, 2026-09-23 to 2026-10-06, all on one commit and all one
    // defect: a switch arm whose body is a multi-line raw string. The pattern before the arrow read
    // the body's unbounded flat width as its own line and chopped; pass two re-joined it.
    [InlineData(2120897534779346985UL)]
    [InlineData(9749290611115768490UL)]
    [InlineData(15958279914763084359UL)]
    [InlineData(15444912073777749680UL)]
    [InlineData(18393674522974205944UL)]
    [InlineData(12485847646168391438UL)]
    [InlineData(7912736926820264633UL)]
    [InlineData(16468989038966499649UL)]
    [InlineData(7775043994036919290UL)]
    [InlineData(16738553386079377947UL)]
    [InlineData(5651812525606868025UL)]
    [InlineData(11606463289314822479UL)]
    [InlineData(15931495183721029956UL)]
    [InlineData(15010799596576293816UL)]
    public void ReportedGeneratedSeeds_HaveNoViolations(ulong seed) {
        var test = Fuzzer.Build(seed, FuzzMode.Both, Corpus.All());
        var (violations, _) = Fuzzer.Execute(
            test,
            false,
            cancellation: TestContext.Current.CancellationToken
        );
        Assert.Empty(violations);
    }

    // ⚠ A mutate finding needs its origin as well as its seed: the seed draws the file by index into
    // the corpus, so every corpus file added since re-targets it — 11718305405350914591 alone now
    // mutates a blank-lines construct and passes whatever the formatter does. The pair is the exact
    // reconstruction `fuzz --replay=<seed> --origin=<path>` prints.
    // #404 (found by `fuzz --seed=393150`): an arrow body `(`↵`a)[0] .C()` held SK-DIV-0101's level
    // on pass one, broke the chain, and gave the level up on pass two.
    [Theory]
    [InlineData(11718305405350914591UL, "constructs/breaks/chain-after-parenthesised-head.cs")]
    public void ReportedMutateSeeds_HaveNoViolations(ulong seed, string origin) {
        var test = Fuzzer.Build(seed, FuzzMode.Both, Corpus.All(), origin);
        var (violations, _) = Fuzzer.Execute(
            test,
            false,
            cancellation: TestContext.Current.CancellationToken
        );
        Assert.Empty(violations);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AbsorbedMutations_ProtectUnterminatedInterpolatedStringThroughEof(bool finalNewline) {
        var path = Path.Combine(Corpus.Root, "pathological", "interpolated-raw-string-with-nested-braces.cs");
        var source = File.ReadAllText(path).TrimEnd('\r', '\n') + (finalNewline ? "\n" : string.Empty);
        var applied = 0;
        foreach (var name in FuzzMutations.AbsorbedNames) {
            for (ulong seed = 0; seed < 100; seed++) {
                var mutated = FuzzMutations.Apply(name, source, new FuzzRandom(seed), Corpus.PropertySymbols);
                if (mutated is null) {
                    continue;
                }

                applied++;
                foreach (var symbols in (IReadOnlyList<string>[])[[], Corpus.PropertySymbols]) {
                    Assert.Null(
                        TokenEquivalence.Compare(
                            SourceText.From(source),
                            SourceText.From(mutated),
                            CSharpFormatter.ParseOptionsFor(symbols)
                        )
                    );
                }
            }
        }

        // Safe code before the string must still be exercised; skipping the entire file is not a fix.
        Assert.True(applied > 0);
        Assert.NotEqual(
            source,
            FuzzMutations.Apply(FuzzMutations.Indent, source, new FuzzRandom(0), Corpus.PropertySymbols)
        );
    }
}
