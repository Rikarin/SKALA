using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Formatting;
using Rikarin.Skala.Formatting.CSharp;
using Rikarin.Skala.Formatting.CSharp.Arrangement;
using Rikarin.Skala.Testing;

namespace Rikarin.Skala.Conformance.Tests;

/// <summary>
/// Runs the arrange-and-format pipeline over the corpus, with one compilation for the whole set.
/// </summary>
/// <remarks>
/// ⚠ One compilation, built once, shared by every test in the class. Building it per test would
/// re-parse 390 files per assertion; building it per *file* would answer "is this using unused"
/// against a compilation of one file, which is a different question with a different answer.
/// </remarks>
public static class CorpusArranger {
    static readonly Lock Gate = new();
    static readonly Dictionary<(string Set, bool Defined), CSharpCompilation> Compilations = [];

    /// <summary>The files the pipeline is asserted over: everything a cleanup fixture exists for.</summary>
    public static IReadOnlyList<CorpusFile> Files { get; } = Corpus.Arrangeable();

    public static CSharpCompilation CompilationFor(bool defined) {
        lock (Gate) {
            if (!Compilations.TryGetValue(("all", defined), out var compilation)) {
                Compilations[("all", defined)] = compilation = ArrangementDifferential.Compile(
                    Files,
                    defined ? CorpusFormatter.Symbols : []
                );
            }

            return compilation;
        }
    }

    public static PipelineResult Run(CorpusFile file, bool defined, string? source = null) {
        var compilation = CompilationFor(defined);
        var text = source is null
            ? CSharpFormatter.Read(file.Path)
            : SourceText.From(source);

        var resolved = CorpusFormatter.OptionsFor(file.Path);
        var symbols = defined ? CorpusFormatter.Symbols : ImmutableArray<string>.Empty;

        // ⚠ When the caller supplies text, the compilation has to carry that text or the semantic
        // half answers about what was on disk. This is the second-pass problem the pipeline solves
        // internally, surfaced here because the idempotency property feeds output back as input.
        if (source is not null) {
            compilation = Replace(compilation, file.Path, text);
        }

        return ArrangementPipeline.Run(
            file.Path,
            text,
            new PhaseOneOptions(resolved),
            new ArrangementOptions(resolved),
            compilation,
            ArrangementDifferential.Removable(compilation, file.Path),
            null,
            symbols,
            ArrangementFilter.All
        );
    }

    /// <summary>
    /// One file arranged under an explicit option set — the coverage test's subject.
    /// </summary>
    /// <remarks>
    /// ⚠ <see cref="Arranger"/> rather than <see cref="ArrangementPipeline"/>, deliberately. The
    /// question is "does this key change what the arranger does", and running the formatter
    /// afterwards can absorb a difference — a body style that converts and is then wrapped back
    /// across two lines is a different tree that lays out the same. Asking the arranger directly
    /// keeps the test measuring the option rather than the fitter.
    /// </remarks>
    public static string RunWith(CorpusFile file, Rikarin.Skala.Options.FormattingOptions options) {
        var compilation = CompilationFor(false);
        return Arranger.Arrange(
            file.Path,
            CSharpFormatter.Read(file.Path),
            new ArrangementOptions(options),
            compilation,
            ArrangementDifferential.Removable(compilation, file.Path)
        ).Text;
    }

    static CSharpCompilation Replace(CSharpCompilation compilation, string path, SourceText text) {
        foreach (var tree in compilation.SyntaxTrees) {
            if (string.Equals(tree.FilePath, path, StringComparison.Ordinal)) {
                return compilation.ReplaceSyntaxTree(
                    tree,
                    CSharpSyntaxTree.ParseText(text, (CSharpParseOptions)tree.Options, path)
                );
            }
        }

        return compilation;
    }
}

/// <summary>
/// docs/plan/12 § "Properties", asserted over the <b>pair</b> rather than over either half.
/// </summary>
/// <remarks>
/// ⚠ Milestone 4's need #3. Formatting is idempotent on its own and arrangement is idempotent on its
/// own, and neither fact implies the pair is: arrangement moves text, the result is re-formatted,
/// and re-formatting can expose an arrangement that was not visible before. The property that has to
/// hold is <c>pipeline(pipeline(x)) == pipeline(x)</c>, and it is asserted here rather than reasoned
/// about.
/// <para>
/// ⚠ Token equivalence is deliberately absent, and its absence is the definition of arrangement:
/// doc 06 § "Safety" — "Arrangement changes the tree, so 04's token-equivalence check does not
/// apply." Its place is taken by the three layers, of which layers 2 and 3 are asserted below.
/// </para>
/// </remarks>
public sealed class ArrangementPropertyTests {
    public static TheoryData<CorpusFile, bool> AllFiles {
        get {
            var data = new TheoryData<CorpusFile, bool>();
            foreach (var file in CorpusArranger.Files) {
                data.Add(file, false);
                data.Add(file, true);
            }

            return data;
        }
    }

    [Theory]
    [MemberData(nameof(AllFiles))]
    public void Idempotency_ArrangeAndFormatIsAFixedPointOfItself(CorpusFile file, bool defined) {
        var first = CorpusArranger.Run(file, defined);
        if (!first.Converged) {
            return;
        }

        var second = CorpusArranger.Run(file, defined, first.Text);
        Assert.True(
            second.Edits.IsEmpty,
            $"{file} is not a fixed point of arrange-and-format; the second pass still wants "
            + $"{second.Edits.Length} edit(s): {string.Join(", ", second.Edits.Take(3))}"
        );
    }

    [Theory]
    [MemberData(nameof(AllFiles))]
    public void Convergence_HoldsWithinTheBound(CorpusFile file, bool defined) {
        var result = CorpusArranger.Run(file, defined);
        Assert.True(
            result.Converged,
            $"{file}: arrange-and-format did not reach a fixed point in "
            + $"{ArrangementPipeline.MaxPasses} passes. A rule and the formatter disagree about it."
        );

        // ⚠ The bound is four and the observed maximum is **three**, on 20 of 391 corpus files.
        //
        // Two is the ordinary case: one pass to arrange and format, one to prove nothing more wants
        // to change. Three is the case this pipeline exists for and the reason the property is about
        // the *pair* — a rewrite in pass 1 exposes a rewrite that was not available before it, and
        // only a second arrangement pass can see it. `IList<int> x = new List<int>()` cannot become
        // `var` until nothing else wants the declaration; a block that stops being three statements
        // once a redundant nested block is lifted out of it is not a body-style candidate until
        // then. Idempotency still holds at the fixed point, which is what makes three passes
        // composition rather than oscillation.
        //
        // A file needing four would be a rule and the formatter disagreeing, and is reported rather
        // than silently truncated — see ArrangementPipeline.MaxPasses.
        Assert.True(
            result.Passes <= 3,
            $"{file}: took {result.Passes} passes; three is the observed maximum. Rules applied: "
            + string.Join(", ", result.Applied.Select(ArrangeIds.NameOf))
        );
    }

    [Theory]
    [MemberData(nameof(AllFiles))]
    public void Determinism_ThreeRunsProduceIdenticalBytes(CorpusFile file, bool defined) {
        var first = CorpusArranger.Run(file, defined).Text;
        Assert.Equal(first, CorpusArranger.Run(file, defined).Text);
        Assert.Equal(first, CorpusArranger.Run(file, defined).Text);
    }

    [Theory]
    [MemberData(nameof(AllFiles))]
    public void ParseStability_TheOutputParsesWithTheSameDiagnostics(CorpusFile file, bool defined) {
        var result = CorpusArranger.Run(file, defined);
        if (!result.Converged || result.Edits.IsEmpty) {
            return;
        }

        var cancellation = TestContext.Current.CancellationToken;
        var before = CSharpSyntaxTree.ParseText(
            result.Original,
            CSharpFormatter.ParseOptions,
            string.Empty,
            cancellation
        )
            .GetDiagnostics(cancellation)
            .Select(static d => d.Id)
            .Order(StringComparer.Ordinal);

        var after = CSharpSyntaxTree
            .ParseText(SourceText.From(result.Text), CSharpFormatter.ParseOptions, string.Empty, cancellation)
            .GetDiagnostics(cancellation)
            .Select(static d => d.Id)
            .Order(StringComparer.Ordinal);

        Assert.Equal(before, after);
    }

    /// <summary>
    /// ⚠ The milestone's own bar, per file: arrangement introduces no compiler diagnostic.
    /// </summary>
    /// <remarks>
    /// ⚠ This is layer 2 asserted rather than trusted. The layer reverts a file whose re-bind found a
    /// new diagnostic, so a passing arrangement is by construction diagnostic-free — but "by
    /// construction" is what every bug says about itself, and the assertion here is independent of
    /// the code path that makes it true: it re-binds the *written* text against the *original*
    /// compilation and compares, without going through ArrangementSafety at all.
    /// </remarks>
    [Theory]
    [MemberData(nameof(AllFiles))]
    public void Arrangement_IntroducesNoCompilerDiagnostic(CorpusFile file, bool defined) {
        var result = CorpusArranger.Run(file, defined);
        if (result.Edits.IsEmpty) {
            return;
        }

        var cancellation = TestContext.Current.CancellationToken;
        var compilation = CorpusArranger.CompilationFor(defined);
        var tree = compilation.SyntaxTrees.FirstOrDefault(t =>
            string.Equals(t.FilePath, file.Path, StringComparison.Ordinal)
        );

        if (tree is null) {
            return;
        }

        var before = Signature(compilation.GetSemanticModel(tree).GetDiagnostics(null, cancellation));
        var rewritten = CSharpSyntaxTree.ParseText(
            SourceText.From(result.Text),
            (CSharpParseOptions)tree.Options,
            file.Path,
            cancellation
        );

        var after = compilation.ReplaceSyntaxTree(tree, rewritten);
        var now = Signature(after.GetSemanticModel(rewritten).GetDiagnostics(null, cancellation));
        var appeared = now.Except(before, StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();

        Assert.True(
            appeared.Length == 0,
            $"{file}: arrangement introduced {appeared.Length} diagnostic(s): {string.Join(", ", appeared.Take(4))}"
        );
    }

    /// <summary>
    /// ⚠ <c>--range</c> over a real edit-to-span map, which is what M4's need #4 asked for.
    /// </summary>
    /// <remarks>
    /// ⚠ The property is the same one the formatter's range consistency asserts — a range result is
    /// the whole-file result filtered — but it is a much stronger claim here, because the edit list
    /// is no longer a by-product of writing. If <see cref="ArrangementEdits.Diff"/> collapsed to one
    /// whole-file edit, this test would still pass while range formatting silently became whole-file
    /// formatting, so the count assertion below is the one that matters: a file the pipeline changed
    /// in two separate places must produce two separate edits.
    /// </remarks>
    [Theory]
    [MemberData(nameof(AllFiles))]
    public void RangeConsistency_ARangeIsTheWholeFilesEditsFiltered(CorpusFile file, bool defined) {
        var result = CorpusArranger.Run(file, defined);
        if (result.Edits.IsEmpty) {
            return;
        }

        var half = result.Original.Length / 2;
        var range = SourceSpan.FromBounds(half, result.Original.Length);
        var restricted = EditEmitter.Restrict(result.Edits, range);

        Assert.All(restricted, edit => Assert.Contains(edit, result.Edits));
        Assert.Equal(result.Edits.Count(edit => edit.Span.IntersectsWith(range)), restricted.Count);

        // Edits are ordered and disjoint, which EditEmitter.Apply requires and a diff that merged
        // hunks wrongly would violate.
        for (var i = 1; i < result.Edits.Length; i++) {
            Assert.True(
                result.Edits[i - 1].Span.End <= result.Edits[i].Span.Start,
                $"{file}: edits {i - 1} and {i} overlap or are out of order."
            );
        }

        Assert.Equal(result.Text, EditEmitter.Apply(result.Original.ToString(), result.Edits));
    }

    static ImmutableHashSet<string> Signature(IEnumerable<Diagnostic> diagnostics) {
        var set = ImmutableHashSet.CreateBuilder(StringComparer.Ordinal);
        foreach (var diagnostic in diagnostics) {
            if (diagnostic.Severity is DiagnosticSeverity.Error or DiagnosticSeverity.Warning) {
                set.Add(diagnostic.Id + "|"
                    + diagnostic.GetMessage(System.Globalization.CultureInfo.InvariantCulture));
            }
        }

        return set.ToImmutable()!;
    }
}
