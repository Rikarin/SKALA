using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Core.Configuration;
using Rikarin.Skala.Formatting;
using Rikarin.Skala.Formatting.CSharp;
using Rikarin.Skala.Options;
using Rikarin.Skala.Testing;

namespace Rikarin.Skala.Conformance.Tests;

/// <summary>Formats a corpus file with the options its own <c>.editorconfig</c> chain resolves to.</summary>
public static class CorpusFormatter {
    static readonly Dictionary<string, FormattingOptions> Cache = [];
    static readonly Lock Gate = new();

    /// <summary>
    /// A symbol set that makes a conditional body live, for the properties to be asserted under.
    /// </summary>
    /// <remarks>
    /// ⚠ The list moved to <see cref="Corpus.PropertySymbols"/> when the fuzzer needed it, because
    /// the fuzzer is a library and this is a test class. What <c>defined</c> means has one answer or
    /// it drifts; this alias is what keeps every existing call site pointing at it.
    /// </remarks>
    public static ImmutableArray<string> Symbols => Corpus.PropertySymbols;

    public static FormatResult Format(CorpusFile file, bool defined = false) {
        var text = CSharpFormatter.Read(file.Path);
        return CSharpFormatter.Format(file.Path, text, OptionsFor(file.Path), null, defined ? Symbols : []);
    }

    public static FormatResult Format(CorpusFile file, string source, bool defined = false) =>
        CSharpFormatter.Format(
            file.Path,
            SourceText.From(source),
            OptionsFor(file.Path),
            null,
            defined ? Symbols : []
        );

    public static FormattingOptions OptionsFor(string path) {
        var directory = Path.GetDirectoryName(path) ?? path;
        lock (Gate) {
            if (!Cache.TryGetValue(directory, out var options)) {
                Cache[directory] = options = OptionResolver.Resolve(path).Options;
            }

            return options;
        }
    }
}

/// <summary>
/// The properties from docs/plan/12 § "Properties — where the real bugs are".
/// </summary>
/// <remarks>
/// ⚠ Idempotency and token equivalence are the two that catch nearly everything, both are cheap,
/// and both run over every corpus file on every test run. They are at 100 % or the build is broken;
/// there is no ratchet and no allowance.
/// </remarks>
public sealed class PropertyTests {
    /// <summary>
    /// Every corpus file, under **both** symbol sets.
    /// </summary>
    /// <remarks>
    /// ⚠ The second column is milestone 3.1's, and the reason is milestone 5's: a file wrapped in a
    /// <c>#if</c> is disabled text for a formatter with no symbols, copied byte-for-byte, so every
    /// property holds over it trivially and the code that formats its body is never asserted at
    /// all. The <c>&gt;</c>-before-<c>(</c> defect survived four milestones in exactly that blind
    /// spot. Doubling the theory doubles the cheapest tests in the suite and lights the half of the
    /// corpus nobody was testing.
    /// </remarks>
    public static TheoryData<CorpusFile, bool> AllFiles {
        get {
            var data = new TheoryData<CorpusFile, bool>();
            foreach (var file in Corpus.All()) {
                data.Add(file, false);
                data.Add(file, true);
            }

            return data;
        }
    }

    [Theory]
    [MemberData(nameof(AllFiles))]
    public void Idempotency_FormatOfFormatIsFormat(CorpusFile file, bool defined) {
        var first = CorpusFormatter.Format(file, defined);
        if (first.Outcome is FormatOutcome.NotParseable or FormatOutcome.Generated) {
            return;
        }

        var second = CorpusFormatter.Format(file, first.Formatted, defined);
        Assert.True(
            second.Edits.IsEmpty,
            $"{file} is not idempotent; the second pass still wants {second.Edits.Length} edit(s): "
            + string.Join(", ", second.Edits.Take(3))
        );
    }

    [Theory]
    [MemberData(nameof(AllFiles))]
    public void TokenEquivalence_HoldsForEveryCorpusFile(CorpusFile file, bool defined) {
        var result = CorpusFormatter.Format(file, defined);
        Assert.NotEqual(FormatOutcome.VerificationFailed, result.Outcome);
    }

    [Theory]
    [MemberData(nameof(AllFiles))]
    public void ParseStability_TheOutputParsesWithTheSameDiagnostics(CorpusFile file, bool defined) {
        var result = CorpusFormatter.Format(file, defined);
        if (result.Outcome is not FormatOutcome.Formatted) {
            return;
        }

        var cancellation = TestContext.Current.CancellationToken;
        var before = CSharpSyntaxTree
            .ParseText(result.Original, CSharpFormatter.ParseOptions, string.Empty, cancellation)
            .GetDiagnostics(cancellation)
            .Select(static d => d.Id)
            .Order(StringComparer.Ordinal);

        var after = CSharpSyntaxTree
            .ParseText(SourceText.From(result.Formatted), CSharpFormatter.ParseOptions, string.Empty, cancellation)
            .GetDiagnostics(cancellation)
            .Select(static d => d.Id)
            .Order(StringComparer.Ordinal);

        Assert.Equal(before, after);
    }

    [Theory]
    [MemberData(nameof(AllFiles))]
    public void Determinism_ThreeRunsProduceIdenticalBytes(CorpusFile file, bool defined) {
        var first = CorpusFormatter.Format(file, defined).Formatted;
        Assert.Equal(first, CorpusFormatter.Format(file, defined).Formatted);
        Assert.Equal(first, CorpusFormatter.Format(file, defined).Formatted);
    }

    [Theory]
    [MemberData(nameof(AllFiles))]
    public void RangeConsistency_ARangeFormatIsTheWholeFilesEditsFiltered(CorpusFile file, bool defined) {
        var result = CorpusFormatter.Format(file, defined);
        if (result.Edits.IsEmpty) {
            return;
        }

        var half = result.Original.Length / 2;
        var range = SourceSpan.FromBounds(half, result.Original.Length);
        var restricted = EditEmitter.Restrict(result.Edits, range);

        Assert.All(restricted, edit => Assert.Contains(edit, result.Edits));
        Assert.Equal(
            result.Edits.Count(edit => edit.Span.IntersectsWith(range)),
            restricted.Count
        );
    }

    [Theory]
    [MemberData(nameof(AllFiles))]
    public void WhitespaceMutation_IsAbsorbed(CorpusFile file, bool defined) {
        // ⚠ format(mutate_whitespace(x)) ≡ format(x) — a strong property that the preservation model
        // makes non-trivial (docs/plan/12 § "Fuzzing"). The mutation here is deliberately the one
        // phase 1 must absorb completely: extra spaces inside a line.
        var result = CorpusFormatter.Format(file, defined);
        if (result.Outcome is not FormatOutcome.Formatted) {
            return;
        }

        // ⚠ A verbatim region is not whitespace, it is data: a `@formatter:off` span and a member
        // whose braces straddle a #if are copied byte-for-byte, so a mutation inside one is not
        // something the formatter is allowed to absorb. Those files opt out of this property, not
        // out of idempotency or token equivalence.
        var source = result.Original.ToString();
        if (source.Contains("@formatter:off", StringComparison.Ordinal)
            || result.Diagnostics.Any(static d => d.Id == FormatDiagnosticIds.UnbalancedPreprocessor)) {
            return;
        }

        var mutated = MutateIndentationOnly(source, defined);
        var second = CorpusFormatter.Format(file, mutated, defined);
        if (second.Outcome is not FormatOutcome.Formatted) {
            return;
        }

        Assert.Equal(result.Formatted, second.Formatted);
    }

    /// <summary>
    /// Doubles the leading whitespace of every line that is not inside a multi-line token. Only
    /// indentation is touched, because a space inside a raw string is not whitespace, it is data.
    /// </summary>
    static string MutateIndentationOnly(string source, bool defined) {
        // ⚠ Parsed with the same symbols the formatter is about to use. Which lines are disabled
        // text is a function of the symbol set, and a mutation computed from a different set
        // protects the wrong half of a `#if`/`#else` — it then adds whitespace inside a region the
        // formatter copies byte-for-byte, and the property fails for the test's reason rather than
        // the formatter's.
        var tree = CSharpSyntaxTree.ParseText(
            SourceText.From(source),
            defined
                ? CSharpFormatter.ParseOptionsFor(CorpusFormatter.Symbols)
                : CSharpFormatter.ParseOptions
        );
        var text = tree.GetText();
        var multiline = new HashSet<int>();
        foreach (var token in tree.GetRoot().DescendantTokens(descendIntoTrivia: true)) {
            var start = text.Lines.GetLineFromPosition(token.SpanStart).LineNumber;
            var end = text.Lines.GetLineFromPosition(token.Span.End).LineNumber;
            for (var line = start + 1; line <= end; line++) {
                multiline.Add(line);
            }
        }

        // ⚠ An interpolated string spanning lines is data too, and it is not one token — it is a run
        // of them with expressions between, so the per-token test above lets the mutation into it.
        // Skala emits the whole expression byte-for-byte (`NodeLayout.Verbatim`, docs/plan/04 §
        // "where a moved space changes the value") and so does the oracle, so a space added inside
        // one is a space neither tool is allowed to absorb. C# 11 put newlines inside interpolation
        // holes, which is what made this reachable from real code at all.
        foreach (var node in tree.GetRoot().DescendantNodes()) {
            if (node is not InterpolatedStringExpressionSyntax) {
                continue;
            }

            var first = text.Lines.GetLineFromPosition(node.SpanStart).LineNumber;
            var last = text.Lines.GetLineFromPosition(node.Span.End).LineNumber;
            for (var line = first + 1; line <= last; line++) {
                multiline.Add(line);
            }
        }

        foreach (var trivia in tree.GetRoot().DescendantTrivia(descendIntoTrivia: true)) {
            if (!trivia.IsKind(SyntaxKind.DisabledTextTrivia)
                && !trivia.IsKind(SyntaxKind.MultiLineCommentTrivia)) {
                continue;
            }

            var start = text.Lines.GetLineFromPosition(trivia.SpanStart).LineNumber;
            var end = text.Lines.GetLineFromPosition(trivia.Span.End).LineNumber;
            for (var line = start; line <= end; line++) {
                multiline.Add(line);
            }
        }

        var builder = new System.Text.StringBuilder(source.Length * 2);
        for (var i = 0; i < text.Lines.Count; i++) {
            var line = text.Lines[i];
            var content = line.ToString();
            if (!multiline.Contains(i)) {
                var indent = 0;
                while (indent < content.Length && content[indent] is ' ' or '\t') {
                    indent++;
                }

                builder.Append(content[..indent]);
            }

            builder.Append(content);
            builder.Append(source.AsSpan(line.End, line.EndIncludingLineBreak - line.End));
        }

        return builder.ToString();
    }
}
