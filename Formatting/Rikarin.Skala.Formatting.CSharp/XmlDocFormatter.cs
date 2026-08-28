using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Rikarin.Skala.Formatting.CSharp;

/// <summary>What the sub-formatter did to one file.</summary>
/// <param name="Reflowed">Comments re-wrapped.</param>
/// <param name="Refused">
///     Comments left exactly as written. Malformed XML, a multi-line tag header, glue a re-wrap could
///     not honour, or a round trip that did not come back identical.
/// </param>
/// <param name="Replacements">
///     ⚠ The regions of the input that were replaced, and by how much the text after each one moved.
///     The pipeline's anchor points (docs/plan/04 § "Emitting minimal edits") are offsets into the text
///     this pass rewrites, so they have to be shifted by exactly this or the minimal-edit machinery
///     starts describing edits at the wrong place.
/// </param>
public readonly record struct XmlDocOutcome(
    string Text,
    int Reflowed,
    int Refused,
    ImmutableArray<XmlDocReplacement> Replacements,
    ImmutableArray<XmlDocRefusal> Refusals);

/// <summary>Why one comment was left exactly as written.</summary>
/// <remarks>
///     ⚠ Every refusal is safe, and none of them is silent. A sub-formatter with no oracle has to be
///     able to say <em>why</em> it declined, or "it left sixteen comments alone" is indistinguishable
///     from "it has a bug in sixteen places".
/// </remarks>
public enum XmlDocRefusalReason {
    /// <summary>Not well-formed XML. Reported at hint as <c>SK0003</c>; hazard 2 of docs/plan/05.</summary>
    Malformed,

    /// <summary>A shape the model declines to represent: a tag header spanning lines, a mismatched end tag.</summary>
    Unmodelled,

    /// <summary>The trivia's line range holds something that is not a <c>///</c> line.</summary>
    NotDocLines,

    /// <summary>A re-wrap would have separated a tag from the word welded to it.</summary>
    Glue,

    /// <summary>⚠ The round trip did not come back identical. The only reason here that is a defect.</summary>
    RoundTrip
}

/// <summary>One comment the sub-formatter declined, and why.</summary>
public readonly record struct XmlDocRefusal(int Line, XmlDocRefusalReason Reason);

/// <summary>One replaced region: where it was in the input, and how long it is in the output.</summary>
public readonly record struct XmlDocReplacement(TextSpan Span, int Length);

/// <summary>
///     The documentation-comment sub-formatter of docs/plan/05 § "Phase 4".
/// </summary>
/// <remarks>
///     ⚠
///     <b>
///         This is the one part of Skala that no committed fixture pins, and the reason is the
///         profile rather than the tool.
///     </b> <c>jb cleanupcode</c> 2025.2.6 formats documentation comments
///     perfectly well — it inserts the space after <c>///</c>, wraps a 128-column summary, splits two
///     <c>&lt;param&gt;</c> tags onto their own lines, and rewrites tag headers — but only under a
///     cleanup profile that enables its <c>CSharpFormatDocComments</c> task, and neither
///     <c>OracleProfile.FormatOnly</c> nor <c>OracleProfile.Cleanup</c> does. Every committed
///     <c>.expected.cs</c> therefore returns its documentation comments exactly as written.
///     <para>
///         ⚠ That is SK-DIV-0006, and the sentence that used to stand here — "does not format
///         documentation comments at all" — was the wrong inference from a correct measurement, kept
///         alive in this file after <c>docs/divergences.md</c> had already corrected it. Every other
///         option in Skala is pinned by a committed fixture showing Rider doing the thing; these are not
///         pinned <em>yet</em>, and what stands between them and Tier A is one element in a profile plus
///         a reviewed fixture regeneration.
///     </para>
///     <para>
///         What replaces the oracle is not "careful reading". It is a property that holds or the comment is
///         not written: <b>the comment's content must survive the round trip</b>. The re-wrapped text is
///         re-parsed and reduced to a signature — element names, attribute source text, prose words,
///         verbatim regions byte-for-byte — and if that signature differs from the original's by so much as
///         one word, the comment is put back exactly as it was. See <see cref="XmlDocSignature" />.
///     </para>
///     <para>
///         ⚠ It runs on the <em>formatted</em> text rather than inside the document builder, and that is
///         deliberate. The width a line has to fit in depends on the code indentation, which this engine
///         resolves at layout time (<see cref="LayoutWriter" />); wrapping against the indentation the
///         source happened to have would make <c>format(format(x))</c> differ from <c>format(x)</c> on
///         every badly-indented file. Running last means the indentation is final and the pass is a fixed
///         point. It also keeps the whole sub-formatter out of <see cref="CSharpDocumentBuilder" />.
///     </para>
/// </remarks>
public static class XmlDocFormatter {
    /// <summary>Re-wraps every well-formed <c>///</c> comment in already-formatted text.</summary>
    /// <param name="tags">
    ///     ⚠ <c>@formatter:off</c>, and this pass is the sharpest of the three places it used to be
    ///     missed. It runs on the document builder's <em>output</em>, downstream of the verbatim chunk
    ///     the builder had just protected, and re-parses it — so a <c>///</c> comment between the tags
    ///     looked to it exactly like any other. The guard is computed over this pass's own tree because
    ///     that is the text whose offsets the replacements are in.
    /// </param>
    public static XmlDocOutcome Rewrite(
        string text,
        in XmlDocOptions options,
        CSharpParseOptions parseOptions,
        string newLine,
        FormatterTags tags = default
    ) {
        var tree = CSharpSyntaxTree.ParseText(SourceText.From(text), parseOptions);
        var source = tree.GetText();
        var root = tree.GetRoot();
        var guard = FormatterTagGuard.For(root, tags);
        var replacements = new List<(TextSpan Span, string Text)>();
        var refusals = ImmutableArray.CreateBuilder<XmlDocRefusal>();
        var reflowed = 0;

        foreach (var trivia in root.DescendantTrivia(descendIntoTrivia: false)) {
            if (!trivia.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia)) {
                // ⚠ `/** */` is out of scope, not pending. Its interior lines have no marker of
                // their own, so re-wrapping one means inventing the `*` prefix convention the author
                // did not necessarily use.
                continue;
            }

            if (trivia.GetStructure() is not DocumentationCommentTriviaSyntax structure) {
                continue;
            }

            // ⚠ Not a refusal. A refusal is the sub-formatter declining a comment it could not
            // re-wrap safely and `--verbose` reports it as a near-miss; a tag is the author saying
            // the question does not arise. Counting one as the other would make the refusal number —
            // which is how the round-trip property is audited — mean two different things.
            if (guard.Touches(trivia.FullSpan)) {
                continue;
            }

            var attempt = Replacement(source, trivia, structure, options, newLine);
            if (attempt.Reason is { } reason) {
                refusals.Add(new XmlDocRefusal(source.Lines.GetLinePosition(trivia.SpanStart).Line + 1, reason));
            } else {
                replacements.Add((attempt.Span, attempt.Text!));
                reflowed++;
            }
        }

        if (replacements.Count == 0) {
            return new XmlDocOutcome(text, 0, refusals.Count, [], refusals.ToImmutable());
        }

        var builder = new StringBuilder(text);
        var applied = ImmutableArray.CreateBuilder<XmlDocReplacement>(replacements.Count);
        for (var i = replacements.Count - 1; i >= 0; i--) {
            var (span, replacement) = replacements[i];
            builder.Remove(span.Start, span.Length).Insert(span.Start, replacement);
        }

        foreach (var (span, replacement) in replacements) {
            applied.Add(new XmlDocReplacement(span, replacement.Length));
        }

        return new XmlDocOutcome(
            builder.ToString(),
            reflowed,
            refusals.Count,
            applied.ToImmutable(),
            refusals.ToImmutable()
        );
    }

    /// <summary>
    ///     Moves the layout's anchor points to where the re-wrap left the text.
    /// </summary>
    /// <remarks>
    ///     ⚠ An anchor inside a replaced region is dropped rather than guessed at. The pieces inside a
    ///     re-wrapped comment are exactly the ones whose output position stopped being a function of
    ///     their input position, and an anchor that lies about where a piece went produces an edit that
    ///     overwrites the wrong bytes. Dropping it costs granularity in <c>--diff</c> and costs nothing
    ///     in correctness: <see cref="EditEmitter" /> then covers the region with the surrounding gap.
    /// </remarks>
    public static Layout Reanchor(Layout layout, string text, ImmutableArray<XmlDocReplacement> replacements) {
        if (replacements.IsDefaultOrEmpty) {
            return layout with { Text = text };
        }

        var anchors = new List<AnchorPoint>(layout.Anchors.Count);
        foreach (var anchor in layout.Anchors) {
            var shift = 0;
            var dropped = false;
            foreach (var replacement in replacements) {
                if (replacement.Span.End <= anchor.OutputStart) {
                    shift += replacement.Length - replacement.Span.Length;
                    continue;
                }

                if (replacement.Span.Start < anchor.OutputEnd) {
                    dropped = true;
                    break;
                }
            }

            if (!dropped) {
                anchors.Add(
                    anchor with { OutputStart = anchor.OutputStart + shift, OutputEnd = anchor.OutputEnd + shift }
                );
            }
        }

        return layout with { Text = text, Anchors = anchors };
    }

    static Attempt Replacement(
        SourceText source,
        SyntaxTrivia trivia,
        DocumentationCommentTriviaSyntax structure,
        in XmlDocOptions options,
        string newLine
    ) {
        // ⚠ Hazard 2 of docs/plan/05 § "Phase 4", and the half milestone 3 already shipped: a doc
        // comment that is not well-formed XML is left exactly as it is and reported at hint
        // (SK0003). Malformed doc comments are extremely common in real code and "fixing" one is
        // worse than ignoring it.
        if (!XmlDocComments.WellFormed(structure)) {
            return new Attempt(default, null, XmlDocRefusalReason.Malformed);
        }

        if (XmlDocModel.Build(structure, options.SpaceAfterTripleSlash) is not { } nodes) {
            return new Attempt(default, null, XmlDocRefusalReason.Unmodelled);
        }

        var first = source.Lines.GetLineFromPosition(trivia.SpanStart);
        var last = source.Lines.GetLineFromPosition(Math.Max(trivia.SpanStart, trivia.Span.End - 1));
        var span = TextSpan.FromBounds(first.Start, last.End);

        var indent = Indent(first.ToString());
        if (indent is null) {
            return new Attempt(default, null, XmlDocRefusalReason.NotDocLines);
        }

        for (var i = first.LineNumber; i <= last.LineNumber; i++) {
            // Every line of the region must be a `///` line, or the region is not the sub-formatter's.
            if (Indent(source.Lines[i].ToString()) is null) {
                return new Attempt(default, null, XmlDocRefusalReason.NotDocLines);
            }
        }

        var marker = options.SpaceAfterTripleSlash ? " " : string.Empty;
        var budget = options.MaxLineLength - TextWidth.Measure(indent) - 3 - marker.Length;
        if (XmlDocRenderer.Render(nodes, options, budget) is not { } lines || lines.Length == 0) {
            return new Attempt(default, null, XmlDocRefusalReason.Glue);
        }

        var rendered = new StringBuilder();
        for (var i = 0; i < lines.Length; i++) {
            if (i > 0) {
                rendered.Append(newLine);
            }

            // ⚠ The marker space is written on a verbatim line too, and that is SK-DIV-0023 closed
            // rather than a widening. `space_after_triple_slash` is Tier A and governs the marker of
            // every `///` line of a comment, including the ones holding a processing instruction or a
            // `<code>` body; `XmlDocModel.SourceLines` takes that same space off on the way in, so the
            // sample's own columns are what is left in `Text` and what comes back out here.
            //
            // ⚠ Skipping an empty line is not a special case for verbatim — it is the rule the prose
            // branch has always had, and it is why a blank line inside a `<code>` block does not
            // acquire the trailing space every other pass in Skala strips.
            rendered.Append(indent).Append("///");
            if (lines[i].Text.Length > 0) {
                rendered.Append(marker).Append(lines[i].Text);
            }
        }

        var text = rendered.ToString();

        // ⚠ The property, checked on every comment of every run rather than on a fixture. Nothing
        // else in this file is allowed to be the last word: an oracle-less formatter that only
        // checks itself against its own reading of a settings page is a formatter that will one day
        // eat a sentence.
        return XmlDocSignature.RoundTrips(structure, text, options.SpaceAfterTripleSlash)
            ? new Attempt(span, text, null)
            : new Attempt(default, null, XmlDocRefusalReason.RoundTrip);
    }

    /// <summary>One comment's outcome: a replacement, or the reason there is not one.</summary>
    readonly record struct Attempt(TextSpan Span, string? Text, XmlDocRefusalReason? Reason);

    /// <summary>
    ///     The line's indentation, when the line is a <c>///</c> line and nothing else.
    /// </summary>
    static string? Indent(string line) {
        var index = 0;
        while (index < line.Length && line[index] is ' ' or '\t') {
            index++;
        }

        if (index + 3 > line.Length || line.AsSpan(index, 3) is not "///") {
            return null;
        }

        // ⚠ `////` is a line comment that happens to start with three slashes, not a doc comment.
        // Roslyn agrees, but the line-range walk above reaches lines Roslyn did not classify.
        return index + 3 < line.Length && line[index + 3] == '/' ? null : line[..index];
    }
}

/// <summary>
///     What a documentation comment says, reduced to the parts a re-wrap may not change.
/// </summary>
/// <remarks>
///     ⚠ The signature is what stands in for the oracle. It is deliberately asymmetric: prose is
///     compared with its whitespace normalised, because re-flowing prose is the whole point, while a
///     <c>&lt;code&gt;</c> or <c>&lt;c&gt;</c> body is compared byte-for-byte including its indentation,
///     because whitespace there is the content. Tag names and the source text of every attribute are
///     compared exactly.
///     <para>
///         ⚠ This is narrower than "ignore comments", and narrower on purpose. The
///         <see cref="TokenEquivalence" /> allowance the sub-formatter needs cannot see inside a
///         <c>&lt;code&gt;</c> block; this can, and it runs first.
///     </para>
///     <para>
///         ⚠ "Byte-for-byte" is byte-for-byte <em>after the marker</em>, which is one column narrower
///         than it sounds and is stated here because the difference is exactly one option.
///         <c>space_after_triple_slash</c> owns the space between <c>///</c> and everything else on the
///         line, a <c>&lt;code&gt;</c> body's lines included, so <c>XmlDocModel.SourceLines</c> takes it
///         off both sides of this comparison and the writer puts it back. What is still compared to the
///         byte is every column the sample has of its own, and the all-or-nothing rule there is what
///         stops a marker-less block being read as one indented by a column it does not have.
///     </para>
/// </remarks>
public static class XmlDocSignature {
    /// <summary>Whether the re-wrapped lines still say what the original said.</summary>
    public static bool RoundTrips(DocumentationCommentTriviaSyntax original, string rewritten, bool markerSpace) {
        if (Reparse(rewritten) is not { } produced) {
            return false;
        }

        return string.Equals(Of(original, markerSpace), Of(produced, markerSpace), StringComparison.Ordinal);
    }

    /// <summary>
    ///     Parses standalone <c>///</c> lines back into a documentation comment.
    /// </summary>
    /// <remarks>
    ///     ⚠ Attached to a declaration, because a doc comment with nothing after it is trailing trivia
    ///     on the end-of-file token and Roslyn does not give it XML structure there.
    /// </remarks>
    static DocumentationCommentTriviaSyntax? Reparse(string lines) {
        var tree = CSharpSyntaxTree.ParseText(
            SourceText.From(lines + "\nclass SkalaXmlDocProbe { }\n"),
            CSharpFormatter.ParseOptions
        );

        foreach (var trivia in tree.GetRoot().DescendantTrivia(descendIntoTrivia: false)) {
            if (trivia.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia)
                && trivia.GetStructure() is DocumentationCommentTriviaSyntax structure) {
                return structure;
            }
        }

        return null;
    }

    public static string Of(DocumentationCommentTriviaSyntax comment, bool markerSpace = true) =>
        Content(comment.Content, markerSpace);

    /// <summary>
    ///     The signature of a content list.
    /// </summary>
    /// <remarks>
    ///     ⚠ Whitespace between two things is compared only when at least one of them is prose, and
    ///     that asymmetry is the whole rule. <c>&lt;c&gt;x&lt;/c&gt;s</c> and <c>&lt;c&gt;x&lt;/c&gt; s</c>
    ///     are different sentences, so a word beside a tag is compared with its separator; whereas
    ///     <c>&lt;/summary&gt;&lt;param&gt;</c> and <c>&lt;/summary&gt;</c>-newline-<c>&lt;param&gt;</c>
    ///     are the same document, and <c>linebreak_before_elements</c> exists precisely to turn the
    ///     first into the second. Whitespace at the two ends is dropped for the same reason:
    ///     <c>spaces_inside_tags</c>, and the choice between a one-line and a three-line element, change
    ///     it and change nothing else.
    /// </remarks>
    static string Content(SyntaxList<XmlNodeSyntax> content, bool markerSpace) {
        var items = new List<(bool IsText, string Text, bool SpaceBefore)>();
        var pending = false;

        foreach (var node in content) {
            if (node is XmlTextSyntax text) {
                var (words, before, after) = Prose(text);
                if (words.Length == 0) {
                    pending |= before || after;
                    continue;
                }

                items.Add((true, words, pending || before));
                pending = after;
                continue;
            }

            items.Add((false, Markup(node, markerSpace), pending));
            pending = false;
        }

        var builder = new StringBuilder();
        for (var i = 0; i < items.Count; i++) {
            if (i > 0 && items[i].SpaceBefore && (items[i].IsText || items[i - 1].IsText)) {
                builder.Append(' ');
            }

            builder.Append(items[i].Text);
        }

        return builder.ToString();
    }

    static string Markup(XmlNodeSyntax node, bool markerSpace) {
        var builder = new StringBuilder();
        switch (node) {
            case XmlElementSyntax element:
                builder.Append("<e:").Append(element.StartTag.Name.ToString());
                Attributes(builder, element.StartTag.Attributes);
                builder.Append('>');
                if (XmlDocModel.IsVerbatimElement(element.StartTag.Name.ToString())) {
                    // ⚠ Byte-for-byte, minus only the lines the tags sat on. This is the check that
                    // catches a re-indented code sample, and it is the reason `<code>` is safe.
                    builder.Append("|v:")
                        .Append(string.Join("\n", XmlDocModel.VerbatimBody(element.Content.ToString(), markerSpace)))
                        .Append('|');
                } else {
                    builder.Append(Content(element.Content, markerSpace));
                }

                builder.Append("</e>");
                break;

            case XmlEmptyElementSyntax empty:
                builder.Append("<x:").Append(empty.Name.ToString());
                Attributes(builder, empty.Attributes);
                builder.Append("/>");
                break;

            default:
                builder.Append("|r:")
                    .Append(string.Join("\n", XmlDocModel.VerbatimBody(node.ToString(), markerSpace)))
                    .Append('|');
                break;
        }

        return builder.ToString();
    }

    /// <summary>
    ///     Every attribute of a tag header: its name, and its quoted value byte-for-byte.
    /// </summary>
    /// <remarks>
    ///     ⚠
    ///     <b>
    ///         The whitespace around the <c>=</c> is the only thing dropped, and dropping it is not a
    ///         weakening of the check.
    ///     </b> <c>name="a"</c> and <c>name = "a"</c> are the same attribute of
    ///     the same element in the same document — XML says so — and two keys of this family,
    ///     <c>spaces_around_eq_in_attribute</c> and <c>space_after_last_attribute</c>, exist to change
    ///     exactly that whitespace and nothing else. Comparing the header's raw source text would make
    ///     the signature refuse every comment those two keys act on, which is a formatter that cannot
    ///     honour its own options rather than a safety net.
    ///     <para>
    ///         ⚠ What is still compared exactly is everything that carries meaning: the attribute's name,
    ///         its quote character, and every byte between the quotes. A <c>cref</c> is read by the
    ///         compiler and a <c>name</c> is matched against a parameter, and neither may move by so much
    ///         as a space. The split is taken at <c>EqualsToken</c>, so an <c>=</c> inside a value is
    ///         value.
    ///     </para>
    ///     <para>
    ///         ⚠ An attribute that does not split — no <c>=</c> where the token says one is — contributes
    ///         its raw text instead. The model refuses such a header outright, so the two sides agree by
    ///         both declining to touch it.
    ///     </para>
    /// </remarks>
    static void Attributes(StringBuilder builder, SyntaxList<XmlAttributeSyntax> attributes) {
        foreach (var attribute in attributes) {
            var text = attribute.ToString();
            var equals = attribute.EqualsToken.SpanStart - attribute.Span.Start;
            builder.Append(' ');
            if (equals <= 0 || equals >= text.Length || text[equals] != '=') {
                builder.Append(text);
                continue;
            }

            builder.Append(text[..equals].TrimEnd()).Append('=').Append(text[(equals + 1)..].TrimStart());
        }
    }

    /// <summary>
    ///     A text run's words, whitespace-normalised, and whether it had whitespace at each end.
    /// </summary>
    /// <remarks>
    ///     ⚠ An entity token contributes its source spelling, so <c>&amp;lt;</c> is compared as the four
    ///     characters the author wrote rather than as the character it denotes. Resolving it would make
    ///     <c>&amp;#60;</c> and <c>&amp;lt;</c> compare equal, and the sub-formatter would then be free
    ///     to swap one for the other.
    /// </remarks>
    static (string Words, bool Before, bool After) Prose(XmlTextSyntax text) {
        var raw = new StringBuilder();
        foreach (var token in text.TextTokens) {
            raw.Append(token.IsKind(SyntaxKind.XmlTextLiteralNewLineToken) ? " " : token.Text);
        }

        var value = raw.ToString();
        if (value.Length == 0) {
            return (string.Empty, false, false);
        }

        var words = value.Split([' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        return (
            string.Join(' ', words),
            char.IsWhiteSpace(value[0]),
            char.IsWhiteSpace(value[^1])
        );
    }
}
