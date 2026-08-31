using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Formatting.CSharp;
using Rikarin.Skala.Testing;

namespace Rikarin.Skala.Conformance.Tests;

/// <summary>
///     The whole corpus, over the documentation-comment sub-formatter that now runs by default.
/// </summary>
/// <remarks>
///     ⚠ <b>This file is what stands in for an oracle.</b> Every other formatter option in Skala is
///     pinned by a committed <c>.expected.cs</c> that <c>jb cleanupcode</c> produced; the
///     <c>resharper_xmldoc_*</c> family is not, because the profile those fixtures were generated under
///     does not run ReSharper's own <c>CSharpFormatDocComments</c> task and so returns every doc
///     comment exactly as written (SK-DIV-0006). What is checkable without waiting for that
///     regeneration is that the re-wrap does not change what a comment says and does not touch anything
///     that is not one, and that is checked here over all 716 corpus files rather than over fixtures.
///     <para>
///         ⚠ <see cref="TheCodeAroundTheComments_IsUntouched" /> is the one that keeps the fidelity ratchet
///         honest, and it is now load-bearing rather than reassuring: the ratchet's basis excludes
///         <c>///</c> lines, so this assertion is the entire reason that exclusion is not a place for
///         regressions to hide.
///     </para>
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
            false
        );
        if (with.Outcome is not FormatOutcome.Formatted || without.Outcome is not FormatOutcome.Formatted) {
            return;
        }

        Assert.Equal(NonDocLines(without.Formatted), NonDocLines(with.Formatted));
    }

    [Theory]
    [MemberData(nameof(Files))]
    public void TheMarkerSpace_IsOnEveryLineTheSubFormatterWrote(CorpusFile file) {
        // ⚠ `resharper_space_after_triple_slash` as a property over the corpus rather than as one
        // fixture's line, and it is here because the fixture could not reach the shape that broke it.
        // A verbatim region — a `<code>` or `<c>` body, a processing instruction, a CDATA section —
        // is the only thing in a doc comment written by a path that does not compose the line out of
        // an indent and a marker, and twice now that path has emitted `///` with the option's space
        // missing: on a processing instruction (SK-DIV-0023) and on the one body line of an inline
        // element whose content starts on its start tag's line, which came out `///Func&lt;int&gt;`.
        //
        // ⚠ The assertion is over lines the sub-formatter *wrote*, which is why it is a comparison
        // rather than a scan. A comment it refused is returned exactly as written and may carry
        // anything the author put there, `////` and a bare `///` included; what it may not do is
        // *introduce* a marker without the option's space. That is exactly the check that caught the
        // second occurrence — `git diff | grep '^+\s*///[^ /]'` over this repository's own sources —
        // and running it here means the next occurrence fails a test instead of a review.
        if (!new XmlDocOptions(CorpusFormatter.OptionsFor(file.Path)).SpaceAfterTripleSlash) {
            return;
        }

        var result = Format(file);
        if (result.Outcome is not FormatOutcome.Formatted) {
            return;
        }

        var before = Unspaced(result.Original.ToString());
        var after = Unspaced(result.Formatted);
        Assert.True(
            after.Count <= before.Count,
            $"{file}: the sub-formatter introduced a `///` line with no space after the marker.\n"
            + $"  before: {before.Count}\n  after:  {after.Count}\n"
            + string.Join("\n", after.Except(before).Take(5).Select(static line => "  + " + line))
        );
    }

    /// <summary>Every <c>///</c> line whose marker is followed by something other than a space.</summary>
    /// <remarks>
    ///     ⚠ <c>////</c> is excluded, and not as a convenience. Roslyn does not classify it as a
    ///     documentation comment and neither does <c>XmlDocFormatter.Indent</c>, so a run of four
    ///     slashes is a line comment this pass never touches and counting one would make the property
    ///     fail on a file the sub-formatter did not write a line of.
    /// </remarks>
    static List<string> Unspaced(string text) => [
        .. TextNormalisation.Lines(text)
            .Select(static line => line.TrimStart())
            .Where(static line =>
                line.StartsWith("///", StringComparison.Ordinal)
                && line.Length > 3
                && line[3] is not (' ' or '/')
            )
    ];

    static string NonDocLines(string text) =>
        string.Join(
            '\n',
            TextNormalisation.Lines(text)
                .Where(static line => !line.TrimStart().StartsWith("///", StringComparison.Ordinal))
        );

    /// <summary>Every non-whitespace character of every <c>///</c> line, in order.</summary>
    /// <remarks>
    ///     ⚠ The doc comments are found through the parser, not by testing whether a line starts with
    ///     <c>///</c>, and the difference is a false failure this suite reported for real. A `///` run
    ///     may begin on the same line as code —
    ///     <code>
    /// interface I { /// &lt;summary&gt;x&lt;/summary&gt;
    ///   /// &lt;remarks&gt;y&lt;/remarks&gt;
    ///     </code>
    ///     — and a line-prefix test cannot see that first line at all. Reading the input that way and
    ///     the output the same way is not symmetric, because the formatter moves the run onto its own
    ///     line (as the oracle does): the `&lt;summary&gt;` was missing from the "before" side and
    ///     present in the "after", and the property reported a comment that had changed when nothing
    ///     about it had. Nothing in `corpus/real/` puts a `///` after code, so the blind spot survived
    ///     until SK-FUZZ-0002's reproduction was retired into the corpus.
    ///     <para>
    ///         ⚠ Still <see cref="SyntaxKind.SingleLineDocumentationCommentTrivia" /> only, which is what the
    ///         line test matched: a <c>/** */</c> block never starts a line with <c>///</c> and was never in
    ///         this measure. Widening it here would be a different assertion smuggled in as a bug fix.
    ///     </para>
    /// </remarks>
    static string DocText(string text) =>
        string.Concat(
            CSharpSyntaxTree.ParseText(SourceText.From(text), CSharpFormatter.ParseOptions)
                .GetRoot()
                .DescendantTrivia(descendIntoTrivia: false)
                .Where(static trivia => trivia.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia))
                .SelectMany(static trivia => TextNormalisation.Lines(trivia.ToFullString()))
                .Select(static line => line.TrimStart())
                .Where(static line => line.StartsWith("///", StringComparison.Ordinal))
                .SelectMany(static line => line[3..].Where(static c => !char.IsWhiteSpace(c)))
        );
}
