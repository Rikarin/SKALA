using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Formatting.CSharp;
using Rikarin.Skala.Testing;

namespace Rikarin.Skala.Conformance.Tests;

public sealed class FuzzRegressionTests {
    [Theory]
    [InlineData("switch", false)]
    [InlineData("switch", true)]
    [InlineData("while", false)]
    [InlineData("while", true)]
    public void NestedCollections_AreIdempotentUnderTheFuzzerConfiguration(string statement, bool defined) {
        // Minimized findings #337 and #339. Bare CLI defaults do not reproduce either failure.
        var path = Path.Combine(Corpus.Root, "pathological", $"nested-collection-in-generated-{statement}.cs");
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
    public void ReportedGeneratedSeeds_HaveNoViolations(ulong seed) {
        var test = Fuzzer.Build(seed, FuzzMode.Both, Corpus.All());
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
        var source = File.ReadAllText(path).TrimEnd('\r', '\n') + (finalNewline ? "\n" : "");
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
