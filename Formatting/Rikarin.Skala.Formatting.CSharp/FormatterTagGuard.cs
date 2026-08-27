using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

namespace Rikarin.Skala.Formatting.CSharp;

/// <summary>
///     The four <c>resharper_formatter_tag*</c> keys, in the one shape every pass that has to honour
///     them can take.
/// </summary>
/// <remarks>
///     ⚠ A struct of its own rather than another overload per options type, because the passes that
///     need it read three different options structs — <see cref="PhaseOneOptions" /> for the document
///     builder, <c>ArrangementOptions</c> for the arranger, <see cref="XmlDocOptions" />'s caller for the
///     sub-formatter — and the escape hatch has to mean the same thing in all of them or it is not an
///     escape hatch.
/// </remarks>
public readonly record struct FormatterTags(bool Enabled, string Off, string On, bool AcceptRegexp) {
    /// <summary>Tags off. <see cref="FormatterTagGuard.For" /> returns an open guard for this.</summary>
    public static FormatterTags None { get; }
}

/// <summary>
///     The <c>@formatter:off</c> … <c>@formatter:on</c> regions of one tree, and the question every
///     rewriter asks before it is allowed to keep a rewrite.
/// </summary>
/// <remarks>
///     ⚠ The escape hatch is not a formatting setting. <c>format</c> honoured it from milestone 1 —
///     <see cref="CSharpDocumentBuilder" /> emits the span verbatim — and <c>arrange</c> did not, which is
///     the worse of the two holes: formatting moves whitespace and arrangement moves the *tree*, and
///     docs/plan/06 § "The line between <c>format</c> and <c>arrange</c>" says the second is reversible
///     only by <c>git revert</c>. A person who writes <c>@formatter:off</c> over a hand-aligned table
///     means "nothing touches this", and the destructive pass was the one that did not listen.
///     <para>
///         ⚠ The regions are recomputed from the *current* tree rather than carried as offsets, because
///         <see cref="Arranger" /> runs twelve rules in sequence and every rule that fires shifts every span
///         after it. A guard built once from the original text and consulted after rule three protects the
///         wrong bytes.
///     </para>
///     <para>
///         The tag comments are found the same way <see cref="SourcePieces" /> finds them — the four comment
///         trivia kinds, in source order, matched by <see cref="Piece.IsComment" />'s set — so that the two
///         halves of the pipeline cannot disagree about where a region begins. The one deliberate
///         difference: a <c>///</c> block is one trivia here and one piece per line there, so a tag written
///         inside a documentation comment ends the region at the end of the whole block. Nobody writes that,
///         and erring long is erring safe.
///     </para>
/// </remarks>
public sealed class FormatterTagGuard {
    /// <summary>No regions: every rewrite is allowed. The state of almost every file.</summary>
    public static FormatterTagGuard Open { get; } = new([], string.Empty);

    readonly ImmutableArray<TextSpan> _regions;
    readonly string _source;

    FormatterTagGuard(ImmutableArray<TextSpan> regions, string source) {
        _regions = regions;
        _source = source;
    }

    public bool IsEmpty => _regions.IsEmpty;

    /// <summary>The regions, in source order. Empty when the tags are off or absent.</summary>
    public ImmutableArray<TextSpan> Regions => _regions;

    /// <summary>
    ///     The guard for one tree, or <see cref="Open" /> when there is nothing to protect.
    /// </summary>
    /// <remarks>
    ///     ⚠ The trivia walk happens on every document and the <see cref="SyntaxNode.ToFullString" />
    ///     only when a tag was actually found, which is what keeps this off the hot path: an untagged
    ///     file pays one descendant-trivia enumeration and allocates nothing.
    /// </remarks>
    public static FormatterTagGuard For(SyntaxNode root, in FormatterTags tags) {
        if (!tags.Enabled) {
            return Open;
        }

        // ⚠ `resharper_formatter_tags_accept_regexp = true` is not implemented, in any pass. The
        // document builder makes the same call (`ContainsTag`), and a guard that silently treated a
        // regexp tag as a literal would protect nothing while looking like it protected something.
        if (tags.AcceptRegexp) {
            return Open;
        }

        var off = tags.Off;
        var on = tags.On;
        if (string.IsNullOrEmpty(off) || string.IsNullOrEmpty(on)) {
            return Open;
        }

        var end = root.FullSpan.End;
        var regions = ImmutableArray.CreateBuilder<TextSpan>();
        var start = -1;

        foreach (var trivia in root.DescendantTrivia(descendIntoTrivia: false)) {
            if (!IsComment(trivia)) {
                continue;
            }

            var text = trivia.ToString();
            if (start < 0) {
                if (IsTag(text, off)) {
                    start = trivia.SpanStart;
                }

                continue;
            }

            if (IsTag(text, on)) {
                regions.Add(TextSpan.FromBounds(start, trivia.Span.End));
                start = -1;
            }
        }

        // ⚠ An unterminated `off` runs to the end of the file. That is the document builder's rule
        // too, and it is the only reading that does not punish a typo with a rewritten file.
        if (start >= 0) {
            regions.Add(TextSpan.FromBounds(start, end));
        }

        return regions.Count == 0 ? Open : new FormatterTagGuard(regions.ToImmutable(), root.ToFullString());
    }

    /// <summary>
    ///     Whether a comment <em>is</em> the tag rather than mentioning it: the tag must be the first
    ///     thing in the comment, after the marker and any whitespace.
    /// </summary>
    /// <remarks>
    ///     ⚠ SK-DIV-0017 and SK-FUZZ-0005. The oracle's own test is a plain substring over the whole
    ///     comment — measured, not assumed — so <c>// we support @formatter:off here</c> turns
    ///     formatting off to end of file in <c>jb cleanupcode</c> 2025.2.6. Skala reads it more narrowly
    ///     on purpose; the argument is in <c>CSharpDocumentBuilder.ContainsTag</c> and the measurement is
    ///     in <c>docs/divergences.md</c>.
    ///     <para>
    ///         One definition, called from both halves of the pipeline, because "which comment is a tag" is
    ///         the single question the escape hatch rests on and two answers to it is one too many.
    ///     </para>
    /// </remarks>
    public static bool IsTag(string comment, string tag) {
        if (tag.Length == 0) {
            return false;
        }

        var body = comment.AsSpan();
        foreach (var marker in Markers) {
            if (body.StartsWith(marker, StringComparison.Ordinal)) {
                body = body[marker.Length..];
                break;
            }
        }

        return body.TrimStart().StartsWith(tag, StringComparison.Ordinal);
    }

    /// <summary>
    ///     ⚠ Longest first. <c>//</c> is a prefix of <c>///</c>, and stripping the shorter one leaves a
    ///     <c>/</c> in front of the tag that no trim removes.
    /// </summary>
    static readonly string[] Markers = ["///", "/**", "/*", "//"];

    static bool IsComment(SyntaxTrivia trivia) =>
        trivia.IsKind(SyntaxKind.SingleLineCommentTrivia)
        || trivia.IsKind(SyntaxKind.MultiLineCommentTrivia)
        || trivia.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia)
        || trivia.IsKind(SyntaxKind.MultiLineDocumentationCommentTrivia);

    /// <summary>
    ///     Whether a span meets a region at all.
    /// </summary>
    /// <remarks>
    ///     ⚠ The question the passes that produce <em>edits</em> ask, rather than the tree-shaped one
    ///     <see cref="Encloses" /> and <see cref="Straddles" /> split between them. An edit has no children
    ///     and no ancestors: it either lands in protected text or it does not, and one that lands
    ///     half-in is still an edit to protected text. A zero-width span — a pure insertion — counts as
    ///     touching when it falls inside a region, which <see cref="TextSpan.OverlapsWith" /> alone would
    ///     say no to.
    /// </remarks>
    public bool Touches(TextSpan span) {
        foreach (var region in _regions) {
            if (region.OverlapsWith(span) || region.Contains(span.Start)) {
                return true;
            }
        }

        return false;
    }

    /// <summary>A node that lies entirely inside a region. It is never visited and never rewritten.</summary>
    public bool Encloses(TextSpan span) {
        foreach (var region in _regions) {
            if (region.Contains(span)) {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    ///     A node that crosses a tag: part of it is protected and part of it is not.
    /// </summary>
    /// <remarks>
    ///     ⚠ The decision, written down: <b>a straddling node is skipped whole</b>, even when the rewrite
    ///     would only have touched the half that is outside. A method whose signature is above the
    ///     <c>off</c> and whose body is below it does not get its return type rewritten.
    ///     <para>
    ///         The alternative — protect the bytes and let the outside half be rewritten — is more precise
    ///         and is the wrong contract. The tag is a promise made in prose, and "nothing in here changes"
    ///         is a promise a person can check; "the bytes between the tags are preserved, but the
    ///         declaration they hang off may be rewritten so that the region now sits inside something else"
    ///         is not. It is also unstable: <c>arrange</c> and <c>format</c> run to a fixed point together,
    ///         and a signature rewrite re-lays-out the line the <c>off</c> comment sits on.
    ///     </para>
    ///     <para>
    ///         A node that *contains* a whole region is not straddling and is not skipped — otherwise one
    ///         <c>off</c> anywhere in a class would freeze the class, then the namespace, then the file.
    ///         That case is governed by <see cref="Preserves" /> instead.
    ///     </para>
    /// </remarks>
    public bool Straddles(TextSpan span) {
        foreach (var region in _regions) {
            if (region.OverlapsWith(span) && !region.Contains(span) && !span.Contains(region)) {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    ///     Whether every protected byte inside <paramref name="original" /> survives verbatim in
    ///     <paramref name="rewritten" />.
    /// </summary>
    /// <remarks>
    ///     ⚠ This is the case <see cref="Straddles" /> cannot see: a node whose span *contains* a region
    ///     outright and whose rewrite eats it anyway. A method whose whole body is between the tags is
    ///     such a node — the region is inside the method, the method is not straddling, and body-style
    ///     arrangement would still fold <c>{ return 1; }</c> into <c>=&gt; 1</c> and take the tags with
    ///     it. So the test is on the text rather than on the spans: the change is kept only if the
    ///     region's original bytes are still there.
    ///     <para>
    ///         ⚠ Deliberately a substring test and not a positional one. By the time an ancestor is asked,
    ///         its descendants have already been rewritten and every offset inside it has moved; asking
    ///         "are these bytes still present" is the only question that survives the shift. It can in
    ///         principle say yes to a change that deleted the region and left an identical copy of it
    ///         elsewhere in the same node — <see cref="PreservesAll" /> is the backstop, and neither has ever
    ///         been observed to matter.
    ///     </para>
    /// </remarks>
    public bool Preserves(SyntaxNode original, SyntaxNode rewritten) {
        var full = original.FullSpan;
        string? text = null;

        foreach (var region in _regions) {
            var overlap = region.Intersection(full);
            if (overlap is not { Length: > 0 } slice) {
                continue;
            }

            text ??= rewritten.ToFullString();
            if (!text.AsSpan().Contains(_source.AsSpan()[slice.Start..slice.End], StringComparison.Ordinal)) {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    ///     Whether every region of the document survives verbatim in <paramref name="text" />.
    /// </summary>
    /// <remarks>
    ///     ⚠ The document-level backstop, and it exists because <see cref="GuardedRewriter" /> is not the
    ///     only shape a rule has. <see cref="UsingsRule" /> rebuilds the using block by hand rather than
    ///     through a rewriter, and any rule written that way in future is invisible to the per-node
    ///     guard. A rule whose output fails this is dropped entirely — coarse, and correct: a using
    ///     block that was reordered across an <c>off</c> tag has no partial answer worth keeping.
    /// </remarks>
    public bool PreservesAll(string text) {
        foreach (var region in _regions) {
            if (!text.AsSpan().Contains(_source.AsSpan()[region.Start..region.End], StringComparison.Ordinal)) {
                return false;
            }
        }

        return true;
    }
}
