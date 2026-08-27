using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Formatting.CSharp;
using Rikarin.Skala.Testing;

namespace Rikarin.Skala.Conformance.Tests;

/// <summary>
/// The whole corpus, over the documentation-comment sub-formatter that now runs by default.
/// </summary>
/// <remarks>
/// ⚠ <b>This file is what stands in for an oracle.</b> Every other formatter option in Skala is
/// pinned by a committed <c>.expected.cs</c> that <c>jb cleanupcode</c> produced; the
/// <c>resharper_xmldoc_*</c> family is not, because the profile those fixtures were generated under
/// does not run ReSharper's own <c>CSharpFormatDocComments</c> task and so returns every doc
/// comment exactly as written (SK-DIV-0006). What is checkable without waiting for that
/// regeneration is that the re-wrap does not change what a comment says and does not touch anything
/// that is not one, and that is checked here over all 716 corpus files rather than over fixtures.
/// <para>
/// ⚠ <see cref="TheCodeAroundTheComments_IsUntouched"/> is the one that keeps the fidelity ratchet
/// honest, and it is now load-bearing rather than reassuring: the ratchet's basis excludes
/// <c>///</c> lines, so this assertion is the entire reason that exclusion is not a place for
/// regressions to hide.
/// </para>
/// </remarks>
public sealed class XmlDocPropertyTests {
    public static TheoryData<CorpusFile> Files {
        get {
            var data = new TheoryData<CorpusFile>();
            foreach (var file in Corpus.All()) {
                data.Add(file);
            }

            return data;
        }
    }

    static FormatResult Format(CorpusFile file, string? source = null) =>
        CSharpFormatter.Format(
            file.Path,
            source is null ? CSharpFormatter.Read(file.Path) : SourceText.From(source),
            CorpusFormatter.OptionsFor(file.Path)
        );

    [Theory]
    [MemberData(nameof(Files))]
    public void TokenEquivalence_HoldsOverDocComments(CorpusFile file) {
        Assert.NotEqual(FormatOutcome.VerificationFailed, Format(file).Outcome);
    }

    [Theory]
    [MemberData(nameof(Files))]
    public void Idempotency_HoldsOverDocComments(CorpusFile file) {
        var first = Format(file);
        if (first.Outcome is FormatOutcome.NotParseable or FormatOutcome.Generated) {
            return;
        }

        Assert.True(
            Format(file, first.Formatted).Edits.IsEmpty,
            $"{file} is not idempotent with the doc-comment sub-formatter on."
        );
    }

    [Theory]
    [MemberData(nameof(Files))]
    public void RoundTrip_EveryCommentSaysWhatItSaid(CorpusFile file) {
        // ⚠ Whitespace-insensitive, and deliberately not the sub-formatter's own signature:
        // checking the round trip with the function the sub-formatter checks it with would prove
        // only that the function agrees with itself. What whitespace-insensitivity cannot see —
        // the inside of a `<code>` block — is what the signature compares byte-for-byte, and the
        // hazard fixtures in Formatting.CSharp.Tests assert those bytes directly.
        var result = Format(file);
        if (result.Outcome is not FormatOutcome.Formatted) {
            return;
        }

        Assert.Equal(DocText(result.Original.ToString()), DocText(result.Formatted));
    }

    [Theory]
    [MemberData(nameof(Files))]
    public void TheCodeAroundTheComments_IsUntouched(CorpusFile file) {
        // ⚠ The containment property, and it is what makes the fidelity ratchet's basis honest
        // rather than convenient. The sub-formatter is allowed to cost fidelity on `///` lines —
        // that is SK-DIV-0006, and the cost is measured — and it is allowed to cost nothing
        // anywhere else. Excluding `///` lines from the ratchet is only defensible while this
        // passes, so it runs over every corpus file rather than over a sample.
        var with = Format(file);
        var without = CSharpFormatter.Format(
            file.Path,
            CSharpFormatter.Read(file.Path),
            CorpusFormatter.OptionsFor(file.Path),
            null,
            null,
            xmlDoc: false
        );
        if (with.Outcome is not FormatOutcome.Formatted || without.Outcome is not FormatOutcome.Formatted) {
            return;
        }

        Assert.Equal(NonDocLines(without.Formatted), NonDocLines(with.Formatted));
    }

    static string NonDocLines(string text) =>
        string.Join(
            '\n',
            TextNormalisation.Lines(text)
                .Where(static line => !line.TrimStart().StartsWith("///", StringComparison.Ordinal))
        );

    /// <summary>Every non-whitespace character of every <c>///</c> line, in order.</summary>
    static string DocText(string text) =>
        string.Concat(
            TextNormalisation.Lines(text)
                .Select(static line => line.TrimStart())
                .Where(static line => line.StartsWith("///", StringComparison.Ordinal))
                .SelectMany(static line => line[3..].Where(static c => !char.IsWhiteSpace(c)))
        );
}
