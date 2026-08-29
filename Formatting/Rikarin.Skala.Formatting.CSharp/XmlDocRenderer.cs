using System.Collections.Immutable;
using System.Text;

namespace Rikarin.Skala.Formatting.CSharp;

/// <summary>One line of a re-wrapped documentation comment, minus the <c>///</c> marker.</summary>
/// <param name="Verbatim">
///     ⚠ The line is a region reproduced byte for byte — a <c>&lt;code&gt;</c> body, a processing
///     instruction, a CDATA section — so nothing here re-flows it, re-indents it, or measures it
///     against the margin.
///     <para>
///         ⚠ It used to mean one thing more, and that was the defect: the marker space was not applied
///         to it either. <c>space_after_triple_slash</c> governs the marker of every <c>///</c> line, so
///         the exemption produced <c>///&lt;?skala-probe …?&gt;</c> against an oracle that writes
///         <c>/// &lt;?skala-probe …?&gt;</c> (SK-DIV-0023), and a <c>&lt;c&gt;</c> whose content starts on
///         its start tag's line came out as <c>///Func&lt;int&gt;</c> the moment the element had to be
///         opened up. What keeps a code block's columns intact is not skipping the space on the way out
///         but taking it off on the way in — see <c>XmlDocModel.SourceLines</c>.
///     </para>
/// </param>
public readonly record struct XmlDocLine(string Text, bool Verbatim);

/// <summary>
///     Lays out an <see cref="XmlDocNode" /> tree as lines.
/// </summary>
/// <remarks>
///     ⚠ Greedy, single pass, no backtracking — deliberately not the <see cref="Fitter" />. The fitting
///     algorithm of docs/plan/04 chooses between break points that a construct owns; prose has no
///     construct, and every gap between two words is the same kind of gap, so the machinery would buy
///     nothing and would have to be taught about text.
/// </remarks>
public sealed class XmlDocRenderer {
    readonly XmlDocOptions _options;
    readonly int _budget;
    readonly string _indentUnit;
    readonly List<XmlDocLine> _lines = [];
    readonly StringBuilder _current = new();

    /// <summary>The unbreakable unit being accumulated — a word plus everything glued to it.</summary>
    readonly StringBuilder _token = new();

    bool _tokenIsTag;

    /// <summary>⚠ The next flush must not be separated from what is already on the line.</summary>
    bool _weld;

    int _level;
    int _width;
    bool _empty = true;

    /// <summary>⚠ Something has been placed on the current line, so the next unit needs a space.</summary>
    /// <remarks>
    ///     ⚠ This used to be <c>_width &gt; IndentWidth()</c>, which is the same question only while a
    ///     line's measured width starts at its indent. <see cref="_carry" /> is exactly the case where
    ///     it does not.
    /// </remarks>
    bool _placed;

    /// <summary>
    ///     ⚠ The column the next line's fill starts at, when its content began on a start tag's line.
    /// </summary>
    /// <remarks>
    ///     ⚠ SK-DIV-0019's second half, measured. The oracle lays an element's content out starting at
    ///     the column its start tag closes at, and moving the start tag onto a line of its own does not
    ///     reset that — so the first content line of <c>&lt;summary&gt;</c> carries nine columns fewer
    ///     than the ones after it, and the first content line of <c>&lt;param name="…"&gt;</c> carries
    ///     the whole header's width fewer. Any break already emitted clears it, which is why a comment
    ///     the author had already opened up wraps at the plain indent.
    ///     <para>
    ///         ⚠ Carried into the <em>fill</em> and not into <see cref="FitsOpen" />: at content 105 an
    ///         <c>&lt;item&gt;</c> inside a <c>&lt;list&gt;</c> that carries 10 comes back flat, which it
    ///         could not if the fit were asked from column 16. Measured, and the asymmetry is the
    ///         oracle's.
    ///     </para>
    /// </remarks>
    int _carry;

    /// <summary>⚠ Set when glue could not be honoured, which makes the whole comment untouchable.</summary>
    bool _glueBroken;

    XmlDocRenderer(in XmlDocOptions options, int budget) {
        _options = options;
        _budget = Math.Max(20, budget);
        _indentUnit = options.IndentUnit;
    }

    /// <summary>
    ///     The comment's lines, or null when the layout could not be produced without changing the text.
    /// </summary>
    public static ImmutableArray<XmlDocLine>? Render(
        ImmutableArray<XmlDocNode> nodes,
        in XmlDocOptions options,
        int budget
    ) {
        var renderer = new XmlDocRenderer(options, budget);
        renderer.Nodes(nodes);
        renderer.Break();
        if (renderer._glueBroken) {
            return null;
        }

        var lines = renderer._lines;
        while (lines.Count > 0 && lines[^1] is { Verbatim: false, Text.Length: 0 }) {
            lines.RemoveAt(lines.Count - 1);
        }

        return [.. lines];
    }

    void Nodes(ImmutableArray<XmlDocNode> nodes) {
        foreach (var node in nodes) {
            switch (node) {
                case XmlDocWord word:
                    Push(word.Text, word.Glued, tag: false);
                    break;

                case XmlDocBreak hard:
                    HardBreak(hard);
                    break;

                case XmlDocVerbatim verbatim:
                    Lines(verbatim.Lines);

                    // ⚠ `blank_line_after_pi` is on by default and Skala had never done it, because
                    // the oracle profile never enabled the doc-comment task that would have shown
                    // it. The blank line is dropped again by `Render`'s trailing-blank trim when the
                    // instruction is the last thing in the comment, which is the right answer.
                    if (verbatim.ProcessingInstruction && _options.BlankLineAfterPi) {
                        _lines.Add(new XmlDocLine(string.Empty, false));
                    }

                    break;

                case XmlDocElement element:
                    Element(element);
                    break;
            }
        }
    }

    void Element(XmlDocElement element) {
        var flat = Flat(element);
        var multiline = IsMultiline(element, flat);
        var owns = _options.BreakBefore(element.Name);

        // ⚠ Glue to a *word* wins over every break rule. A line break between `<c>x</c>` and the
        // `s` after it would insert whitespace the author did not write, which is the one thing a
        // formatter that touches prose must never do. Two adjacent tags are a different case: a
        // break between them changes no sentence, and `linebreak_before_elements` exists to ask for
        // exactly that.
        if (!element.GluedToWord
            && (owns
                || (multiline
                        ? _options.LinebreakBeforeMultilineElements
                        : _options.LinebreakBeforeSinglelineElements))) {
            Break();
        }

        if (!multiline) {
            Push(flat!, element.Glued, tag: true);
            if (owns) {
                Break();
            }

            return;
        }

        if (element.GluedToWord) {
            // An element that has to span lines cannot stay welded to the word before it, and
            // moving it would change the text. The whole comment is left as written.
            _glueBroken = true;
            return;
        }

        Open(element);

        // ⚠ No unconditional break after the end tag, and the reason is glue again. Vixen has
        // `<i>…</i>.` where the sentence's full stop is welded to the closing tag and the italic
        // text is three lines long; breaking here would move the full stop to a line of its own.
        // The end tag is left as the current token so that whatever is welded to it lands beside it.
        if (owns) {
            Break();
        }
    }

    /// <summary>
    ///     Whether the element has to occupy more than one line.
    /// </summary>
    /// <remarks>
    ///     ⚠ Structure is asked before width.
    ///     <c>linebreaks_inside_tags_for_elements_with_child_elements</c> fires on an element that would
    ///     have fitted: <c>&lt;remarks&gt;&lt;para&gt;x&lt;/para&gt;&lt;/remarks&gt;</c> is 38 columns
    ///     and still goes on three lines when the key is true.
    /// </remarks>
    bool IsMultiline(XmlDocElement element, string? flat) {
        // ⚠ A self-closing element has no inside to break open, and treating one as multi-line
        // rewrites `<code … />` into `<code …>` with a closing tag that was never there.
        // Newtonsoft's `<code source="…" title="…" />` is 130 columns wide and found this — twice.
        // The second time it was this method losing the guard while `Structural` was split out of
        // it, and `XmlDocSignature.RoundTrips` refused four `corpus/real/` comments rather than let
        // it through.
        if (element.SelfClosing) {
            return false;
        }

        if (Structural(element)) {
            return true;
        }

        if (flat is null) {
            return true;
        }

        // ⚠ `linebreaks_inside_tags_for_multiline_elements = false` means an element that does not
        // fit is left long rather than opened up, which is the same answer docs/plan/04 gives for a
        // line of code nothing can break.
        return !FitsOpen(element, flat) && _options.LinebreaksInsideTagsForMultilineElements;
    }

    /// <summary>
    ///     Whether the element has to occupy more than one line for a reason that is not width.
    /// </summary>
    /// <remarks>
    ///     ⚠ Split out of <see cref="IsMultiline" /> so that <see cref="FlatNodes" /> can ask it of a
    ///     <em>child</em> without dragging the width question — which is the parent's own — down a
    ///     level with it. SK-DIV-0020: an element holding a child that is open cannot itself be flat,
    ///     and its prose is hoisted along with the child.
    /// </remarks>
    bool Structural(XmlDocElement element) {
        // ⚠ A self-closing element has no inside to break open, and treating one as multi-line
        // rewrites `<code … />` into `<code …>` with a closing tag that was never there.
        // Newtonsoft's `<code source="…" title="…" />` is 130 columns wide and found this.
        if (element.SelfClosing || element.Verbatim is not null) {
            // ⚠ Verbatim elements are exempt from the threshold below. The oracle does apply it to
            // `<c>`; doing the same here would move a byte-for-byte code body onto a re-indented
            // line, and that is the one thing the verbatim rule exists to prevent. A measured
            // narrowing, not an oversight.
            return false;
        }

        if (_options.LinebreaksInsideTagsForElementsWithChildElements
            && element.HasChildElements
            && !element.HasText) {
            return true;
        }

        // ⚠ `linebreaks_inside_tags_for_elements_longer_than`, measured: the length compared is the
        // element's flat inner content — no start tag, no end tag — and the comparison is strictly
        // greater. Asked before width, like the key above it, because a threshold the content
        // crosses opens the element up however well it would have fitted.
        return _options.LinebreaksInsideTagsForElementsLongerThan != int.MaxValue
            && FlatNodes(element.Children) is { } inner
            && TextWidth.Measure(inner) > _options.LinebreaksInsideTagsForElementsLongerThan;
    }

    /// <summary>
    ///     The element's start tag, from its name and its attributes.
    /// </summary>
    /// <remarks>
    ///     ⚠ Re-emitted rather than copied, which is what gives
    ///     <c>spaces_around_eq_in_attribute</c> and <c>space_after_last_attribute</c> a subject. Each
    ///     attribute's name and its quoted value are the source bytes; the separators are the only
    ///     thing chosen here, and the run of spaces between two attributes is normalised to one, as
    ///     the oracle does.
    /// </remarks>
    /// <param name="close">
    ///     What follows the last attribute: <c>&gt;</c> for a start tag, <c>/&gt;</c> for a
    ///     self-closing one. ⚠ <c>space_after_last_attribute</c> applies only to the first — measured;
    ///     a self-closing tag's gap belongs to <c>space_before_self_closing</c> alone.
    /// </param>
    string Tag(XmlDocElement element, string close) {
        var builder = new StringBuilder(element.Header);
        var equals = _options.SpacesAroundEqInAttribute ? " = " : "=";
        foreach (var attribute in element.Attributes) {
            builder.Append(' ').Append(attribute.Name).Append(equals).Append(attribute.Value);
        }

        if (close == ">" && _options.SpaceAfterLastAttribute && element.Attributes.Length > 0) {
            builder.Append(' ');
        }

        return builder.Append(close).ToString();
    }

    /// <summary>The element's self-closing tag.</summary>
    string SelfClosingTag(XmlDocElement element) => Tag(element, _options.SpaceBeforeSelfClosing ? " />" : "/>");

    /// <summary>Writes an element across several lines: start tag, indented content, end tag.</summary>
    void Open(XmlDocElement element) {
        Push(Tag(element, ">"), glued: false, tag: true);

        // ⚠ The start tag's closing column, kept across the break it is about to take. See `_carry`.
        Flush();
        var carry = _width;
        EndLine();

        var outer = _level;
        _level = outer
            + XmlDocOptions.Delta(
                element.Verbatim is not null || element.HasText ? _options.IndentText : _options.IndentChildElements
            );

        if (element.Verbatim is { } verbatim) {
            Lines(verbatim);
        } else {
            _carry = carry;
            Nodes(element.Children);
            _carry = 0;
        }

        Break();
        _level = outer;
        Push("</" + element.Name + ">", glued: false, tag: true);
    }

    /// <summary>
    ///     The element on one line, or null when it cannot be one.
    /// </summary>
    /// <remarks>
    ///     ⚠ Null for anything holding a break the author is entitled to keep, a verbatim block of more
    ///     than one line, or a child element that owns its own line. It says nothing about width; that
    ///     is <see cref="FitsAlone" />'s question, and the two are separate so that an element which is
    ///     short enough but structurally multi-line is still handled as multi-line.
    /// </remarks>
    string? Flat(XmlDocElement element) {
        if (element.SelfClosing) {
            return SelfClosingTag(element);
        }

        var start = Tag(element, ">");
        var close = "</" + element.Name + ">";
        if (element.Verbatim is { } verbatim) {
            // ⚠ No `spaces_inside_tags` here. A space inside `<c>` is part of the code.
            return verbatim.Length == 1 ? start + verbatim[0] + close : null;
        }

        if (FlatNodes(element.Children) is not { } inner) {
            return null;
        }

        // ⚠ SK-DIV-0022, measured at both values. `true` is a statement about the output — exactly one
        // space each side, and the author's double collapses to one. `false` is a statement about what
        // the run may *insert*: it adds nothing and the author's own gap survives, per side and
        // verbatim. Only here, because only a flat element has an author's gap left to keep: an
        // element the run opens up has its content re-flowed, and the oracle drops the spaces too.
        if (inner.Length == 0) {
            return start + close;
        }

        return _options.SpacesInsideTags
            ? start + " " + inner + " " + close
            : start + element.InnerLead + inner + element.InnerTrail + close;
    }

    string? FlatNodes(ImmutableArray<XmlDocNode> nodes) {
        var builder = new StringBuilder();
        foreach (var node in nodes) {
            switch (node) {
                case XmlDocWord word:
                    Join(builder, word.Text, word.Glued);
                    break;

                case XmlDocBreak hard:
                    if (hard.BlankLines > 0 || _options.KeepUserLinebreaks) {
                        return null;
                    }

                    break;

                case XmlDocVerbatim:
                    return null;

                case XmlDocElement element:
                    // ⚠ `Structural` is the SK-DIV-0020 half. A child the structure rules open —
                    // a `<list>` that holds only `<item>`s, an element past
                    // `linebreaks_inside_tags_for_elements_longer_than` — cannot appear inside a flat
                    // parent, so the parent is opened too and the prose beside the child is hoisted
                    // with it. Skala used to apply `with_child_elements` only to an element whose
                    // content was *nothing but* elements, which left mixed content on one line.
                    if (_options.BreakBefore(element.Name)
                        || Flat(element) is not { } flat
                        || Structural(element)) {
                        return null;
                    }

                    Join(builder, flat, element.Glued);
                    break;
            }
        }

        return builder.ToString();
    }

    static void Join(StringBuilder builder, string text, bool glued) {
        if (builder.Length > 0 && !glued) {
            builder.Append(' ');
        }

        builder.Append(text);
    }

    /// <summary>
    ///     Whether an element's <em>content</em> fits on the line its start tag closes on.
    /// </summary>
    /// <remarks>
    ///     ⚠ Measured, and it is the third of SK-DIV-0019's three parts. What is compared is the flat
    ///     element minus its end tag: the oracle opens an element up when the content overflows from
    ///     the start tag's closing column, and the <c>&lt;/item&gt;</c> that follows the last word is
    ///     not in the comparison and rides past the margin. That is what keeps the committed
    ///     <c>linebreak_before_multiline_elements</c> fixture's 131-column <c>&lt;item&gt;</c> on one
    ///     line — SK-DIV-0021 attributed it to <c>linebreak_before_elements</c> not naming
    ///     <c>item</c>, and the same <c>&lt;item&gt;</c> with longer content is opened up and wrapped,
    ///     so that reading is measured false.
    ///     <para>
    ///         ⚠ Everything fits when <c>wrap_lines</c> is false: with no hard wrap there is no width to
    ///         fail, so a long element is left long rather than opened up.
    ///     </para>
    /// </remarks>
    bool FitsOpen(XmlDocElement element, string flat) =>
        !_options.WrapLines
        || IndentWidth() + TextWidth.Measure(flat) - element.Name.Length - "</>".Length <= _budget;

    void Push(string text, bool glued, bool tag) {
        if (glued) {
            // ⚠ Glue has to survive an empty token buffer. Whatever came before may already be on
            // the line, and forgetting that here is how `<c>x</c>s` becomes `<c>x</c> s`.
            _weld |= _token.Length == 0;
        } else {
            Flush();
        }

        _tokenIsTag |= tag;
        _token.Append(text);
    }

    /// <summary>Ends the unbreakable unit and places it, wrapping the line first if it must.</summary>
    /// <remarks>
    ///     ⚠ <c>wrap_text</c> and <c>wrap_tags_and_pi</c> are asked separately, and which one applies is
    ///     decided by what the unit contains rather than by what started it: a word with a
    ///     <c>&lt;see/&gt;</c> glued to it is a tag as far as the permission to move it goes, because
    ///     moving it moves the tag.
    /// </remarks>
    void Flush() {
        if (_token.Length == 0) {
            return;
        }

        var text = _token.ToString();
        var weld = _weld;
        var mayWrap = !weld && _options.WrapLines && (_tokenIsTag ? _options.WrapTagsAndPi : _options.WrapText);
        _token.Clear();
        _tokenIsTag = false;
        _weld = false;

        var width = TextWidth.Measure(text);
        if (!_empty && mayWrap && _width + 1 + width > _budget) {
            EndLine();
        }

        Start();
        if (!weld && _placed) {
            _current.Append(' ');
            _width++;
        }

        _current.Append(text);
        _width += width;
        _placed = true;
    }

    void Start() {
        if (!_empty) {
            return;
        }

        for (var i = 0; i < _level; i++) {
            _current.Append(_indentUnit);
        }

        // ⚠ The emitted text is the indent; the measured width may be more. See `_carry`.
        _width = Math.Max(IndentWidth(), _carry);
        _carry = 0;
        _empty = false;
        _placed = false;
    }

    int IndentWidth() => _level * (_options.UseTabs ? TextWidth.TabStop : _options.IndentSize);

    void Break() {
        Flush();
        EndLine();
    }

    void EndLine() {
        // ⚠ Cleared before the early return, not after it. A break the caller asked for ends the
        // carry whether or not there was anything on the line to end — `linebreak_before_elements`
        // breaks in front of a `<para>` that is the first thing in its parent, and the `<para>` line
        // starts at its own indent rather than at the parent's start tag.
        _carry = 0;
        if (_empty) {
            return;
        }

        _lines.Add(new XmlDocLine(_current.ToString(), false));
        _current.Clear();
        _width = 0;
        _empty = true;
        _placed = false;
    }

    void HardBreak(XmlDocBreak hard) {
        if (hard.BlankLines > 0) {
            Break();
            for (var i = 0; i < Math.Min(hard.BlankLines, _options.MaxBlankLinesBetweenTags); i++) {
                _lines.Add(new XmlDocLine(string.Empty, false));
            }

            return;
        }

        if (_options.KeepUserLinebreaks) {
            Break();
        }
    }

    void Lines(ImmutableArray<string> lines) {
        Break();
        foreach (var line in lines) {
            _lines.Add(new XmlDocLine(line, true));
        }
    }
}
