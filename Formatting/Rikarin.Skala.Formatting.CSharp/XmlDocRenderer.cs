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

    /// <summary>How many elements enclose what is being written. Zero is the comment itself.</summary>
    /// <remarks>
    ///     ⚠ Not <see cref="_level" />, which is an indent level and can stay at zero through a whole
    ///     nest when <c>indent_child_elements</c> says so. This counts enclosing elements, because the
    ///     rule it serves is about structure: an element written directly under the <c>///</c> marker
    ///     always gets a line of its own, and <c>linebreak_before_elements</c> has nothing to say about
    ///     it. Measured, with the whole comment written on one line and the key set to the export's
    ///     list, to <c>b</c> alone, and to an empty list: the oracle splits
    ///     <c>&lt;summary&gt;…&lt;para&gt;…&lt;para&gt;…&lt;b&gt;…&lt;c&gt;…</c> onto five lines at every
    ///     one of the three. Set the same key to <c>b,c</c> and the *nested* elements answer it exactly
    ///     — <c>&lt;b&gt;</c> and <c>&lt;c&gt;</c> inside a <c>&lt;summary&gt;</c> take their own lines
    ///     while <c>&lt;para&gt;</c> inside a <c>&lt;remarks&gt;</c> does not — so the key is real, and
    ///     it is a rule about nesting rather than about the element's name.
    /// </remarks>
    int _depth;

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

        // ⚠ No floor, and the `Math.Max(20, …)` that used to be here was a guess that hid a
        // measurement. At `xmldoc_max_line_length = 0` and at `1` the oracle puts one word on each
        // line; the floor gave Skala a 20-column fill at both, which is neither value's answer and
        // is not the 120-column answer either. Nothing needs protecting: `Flush` only wraps when the
        // line already holds something, so a budget of zero still emits one unit per line rather
        // than looping.
        _budget = Math.Max(0, budget);
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

        // ⚠ An element written directly under the marker owns its line whatever the list says. See
        // `_depth`: the oracle splits five top-level elements off one another at the export's list,
        // at `b` alone and at an empty list alike, and answers the list only for nested ones.
        var owns = _depth == 0 || _options.BreakBefore(element.Name);

        // ⚠ Glue to a *word* wins over every break rule. A line break between `<c>x</c>` and the
        // `s` after it would insert whitespace the author did not write, which is the one thing a
        // formatter that touches prose must never do. Two adjacent tags are a different case: a
        // break between them changes no sentence, and `linebreak_before_elements` exists to ask for
        // exactly that.
        var breaksBefore = !element.GluedToWord
            && (owns
                || (multiline
                        ? _options.LinebreakBeforeMultilineElements
                        : _options.LinebreakBeforeSinglelineElements));

        if (breaksBefore) {
            Break();
        }

        if (!multiline) {
            Push(flat!, element.Glued, tag: true);

            // ⚠ A break *after* as well, and it is the same rule read once rather than twice.
            // Measured on `<remarks>` holding `Leading prose. <c>Code.</c> Trailing prose.` and a
            // second line: at `linebreak_before_singleline_elements = true` the oracle writes the
            // prose, the `<c>` and the trailing prose on three lines — so "place single-line elements
            // on a new line" puts what follows on a new line too, exactly as
            // `linebreak_before_elements` does. Skala broke only in front of the element and left
            // `<c>Code.</c> Trailing prose.` sharing a line.
            if (owns || breaksBefore) {
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

        // ⚠ `linebreaks_inside_tags_for_multiline_elements` is not asked here, and it used to be.
        // It chooses where the *tags* sit once the element spans lines — see `Open`'s hug mode — not
        // whether it spans them. Reading it here made `false` mean "leave the element on one line
        // however long", which the oracle does not do.
        return !FitsOpen(element, flat);
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

        if (element.HasChildElements) {
            if (_options.LinebreaksInsideTagsForElementsWithChildElements) {
                if (!element.HasText) {
                    return true;
                }
            }

            // ⚠ `false` does not close every element up, and this is measured rather than reasoned
            // about. At `linebreaks_inside_tags_for_elements_with_child_elements = false` the oracle
            // still opens an element that holds a *grandchild* element and closes up only the
            // innermost one. On one comment, all at `false`: `<remarks><b>One.</b></remarks>`,
            // `<summary><b>One.</b></summary>`, `<list><b>One.</b></list>` and `<foo><b>One.</b></foo>`
            // all come back flat, while `<remarks><b><i>One.</i></b></remarks>`,
            // `<foo><bar><baz>One.</baz></bar></foo>` and `<remarks><list><item>One.</item></list></remarks>`
            // are opened — and inside them the innermost element-with-children stays flat.
            // `<aa><bb><cc><dd>One.</dd></cc></bb></aa>` opens `<aa>` and `<bb>` and leaves
            // `<cc><dd>One.</dd></cc>` on one line, which is the same rule three deep. Mixed content
            // does not exempt it either: `<remarks>Text <b><i>One.</i></b></remarks>` opens, where the
            // `true` branch above would have left it flat for holding text.
            else if (HasGrandchildElement(element)) {
                return true;
            }
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
    ///     Whether any child of this element is itself an element with element children.
    /// </summary>
    /// <remarks>
    ///     ⚠ The shape <c>linebreaks_inside_tags_for_elements_with_child_elements = false</c> still
    ///     opens. It is asked of the children rather than of the element, which is why the innermost
    ///     element-with-children is the one that stays flat.
    /// </remarks>
    static bool HasGrandchildElement(XmlDocElement element) =>
        element.Children.Any(static child => child is XmlDocElement { SelfClosing: false } child_
            && child_.HasChildElements
        );

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
    /// <remarks>
    ///     ⚠ <c>linebreaks_inside_tags_for_multiline_elements = false</c> does not stop the element
    ///     spanning lines; it stops the *tags* taking lines of their own. Measured on a
    ///     <c>&lt;summary&gt;</c> whose prose cannot fit: at <c>false</c> the oracle keeps the start tag
    ///     on the first content line and welds <c>&lt;/summary&gt;</c> to the last word, and wraps the
    ///     prose in between exactly as it does at <c>true</c>. Skala used to read the key as "do not
    ///     wrap", and returned the whole element on one 170-column line — which is neither value's
    ///     answer.
    /// </remarks>
    void Open(XmlDocElement element) {
        var hug = !_options.LinebreaksInsideTagsForMultilineElements && element.Verbatim is null;
        Push(Tag(element, ">"), glued: false, tag: true);

        // ⚠ The start tag's closing column, kept across the break it is about to take. See `_carry`.
        Flush();
        var carry = _width;
        if (hug) {
            // ⚠ Welded, not merely kept on the line. The oracle writes `<summary>A summary …` with no
            // gap behind the tag at `linebreaks_inside_tags_for_multiline_elements = false`, and a
            // start tag that is only "on the same line" picks up the ordinary word separator.
            _weld = true;
        } else {
            EndLine();
        }

        var outer = _level;
        _level = outer
            + XmlDocOptions.Delta(
                element.Verbatim is not null || element.HasText ? _options.IndentText : _options.IndentChildElements
            );

        var depth = _depth;
        _depth = depth + 1;
        if (element.Verbatim is { } verbatim) {
            Lines(verbatim);
        } else {
            // ⚠ No carry in hug mode: the content really is on the start tag's line, so the width is
            // already counted and handing it to `Start` a second time would reserve it twice.
            _carry = hug ? 0 : carry;
            Nodes(element.Children);
            _carry = 0;
        }

        _depth = depth;

        if (!hug) {
            Break();
        }

        _level = outer;
        Push("</" + element.Name + ">", glued: hug, tag: true);
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
    ///     <para>
    ///         ⚠ And everything fits when there is nowhere in the content a break could go, which is
    ///         what <c>wrap_text = false</c> does to an element holding nothing but prose. Measured on
    ///         two fixtures at that value: a <c>&lt;summary&gt;</c> of 170 columns of plain prose comes
    ///         back whole, on one line, tags and all — but the same key over prose carrying a
    ///         <c>&lt;see/&gt;</c> opens the element and moves the <c>&lt;see/&gt;</c> to the next line,
    ///         leaving the words around it exactly where they were. So <c>wrap_text</c> is permission
    ///         for a <em>word</em> to move, an element may always move, and an element with no movable
    ///         content has no reason to be opened.
    ///     </para>
    /// </remarks>
    bool FitsOpen(XmlDocElement element, string flat) =>
        !_options.WrapLines
        || !(_options.WrapText || element.HasChildElements)
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
    ///     ⚠
    ///     <b>
    ///         A unit carrying a tag may always move, and <c>wrap_tags_and_pi</c> is not what says
    ///         so.
    ///     </b> This used to read <c>_tokenIsTag ? WrapTagsAndPi : WrapText</c>, and both halves of
    ///     that were measured wrong on the same pair of probes. At
    ///     <c>wrap_tags_and_pi = false</c> the oracle still moves a <c>&lt;see/&gt;</c> off the end of a
    ///     line of prose — byte-identical to <c>true</c> on that fixture — and what the key really
    ///     governs is a break *inside* a tag header: a two-attribute <c>&lt;see&gt;</c> 170 columns wide
    ///     comes back with its second attribute on a continuation line at <c>true</c> and whole at
    ///     <c>false</c>. And at <c>wrap_text = false</c> the same <c>&lt;see/&gt;</c> still moves while
    ///     the words around it stay put, so <c>wrap_text</c> is permission for a *word*.
    ///     <para>
    ///         ⚠ A word with a <c>&lt;see/&gt;</c> glued to it counts as a tag, because moving it moves
    ///         the tag.
    ///     </para>
    /// </remarks>
    void Flush() {
        if (_token.Length == 0) {
            return;
        }

        var text = _token.ToString();
        var weld = _weld;
        var mayWrap = !weld && _options.WrapLines && (_tokenIsTag || _options.WrapText);
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

    int IndentWidth() => _level * _options.IndentSize;

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
