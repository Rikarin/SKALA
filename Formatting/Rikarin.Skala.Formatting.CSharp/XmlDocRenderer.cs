using System.Collections.Immutable;
using System.Text;

namespace Rikarin.Skala.Formatting.CSharp;

/// <summary>One line of a re-wrapped documentation comment, minus the <c>///</c> marker.</summary>
/// <param name="Verbatim">
///     ⚠ The line already carries whatever followed the marker in the source, so the marker space is
///     <em>not</em> re-applied to it. Applying it would add a column to every line of every
///     <c>&lt;code&gt;</c> block, which is the "re-wrapping changes what it says" hazard in its
///     quietest form.
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
        // Newtonsoft's `<code source="…" title="…" />` is 130 columns wide and found this.
        if (element.SelfClosing) {
            return false;
        }

        if (element.Verbatim is null
            && _options.LinebreaksInsideTagsForElementsWithChildElements
            && element.HasChildElements
            && !element.HasText) {
            return true;
        }

        if (flat is null) {
            return true;
        }

        // ⚠ `linebreaks_inside_tags_for_multiline_elements = false` means an element that does not
        // fit is left long rather than opened up, which is the same answer docs/plan/04 gives for a
        // line of code nothing can break.
        return !FitsAlone(flat) && _options.LinebreaksInsideTagsForMultilineElements;
    }

    /// <summary>Writes an element across several lines: start tag, indented content, end tag.</summary>
    void Open(XmlDocElement element) {
        Push(element.Header + ">", glued: false, tag: true);
        Break();

        var outer = _level;
        _level = outer
            + XmlDocOptions.Delta(
                element.Verbatim is not null || element.HasText ? _options.IndentText : _options.IndentChildElements
            );

        if (element.Verbatim is { } verbatim) {
            Lines(verbatim);
        } else {
            Nodes(element.Children);
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
            return element.Header + (_options.SpaceBeforeSelfClosing ? " /" : "/") + ">";
        }

        var close = "</" + element.Name + ">";
        if (element.Verbatim is { } verbatim) {
            // ⚠ No `spaces_inside_tags` here. A space inside `<c>` is part of the code.
            return verbatim.Length == 1 ? element.Header + ">" + verbatim[0] + close : null;
        }

        if (FlatNodes(element.Children) is not { } inner) {
            return null;
        }

        var pad = _options.SpacesInsideTags && inner.Length > 0 ? " " : "";
        return element.Header + ">" + pad + inner + pad + close;
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
                    if (_options.BreakBefore(element.Name) || Flat(element) is not { } flat) {
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
    ///     Whether a token fits on a line of its own at the current level.
    /// </summary>
    /// <remarks>
    ///     ⚠ Everything fits when <c>wrap_lines</c> is false: with no hard wrap there is no width to
    ///     fail, so a long element is left long rather than opened up.
    /// </remarks>
    bool FitsAlone(string text) => !_options.WrapLines || TextWidth.Measure(text) + IndentWidth() <= _budget;

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
        if (!weld && _width > IndentWidth()) {
            _current.Append(' ');
            _width++;
        }

        _current.Append(text);
        _width += width;
    }

    void Start() {
        if (!_empty) {
            return;
        }

        for (var i = 0; i < _level; i++) {
            _current.Append(_indentUnit);
        }

        _width = IndentWidth();
        _empty = false;
    }

    int IndentWidth() => _level * (_options.UseTabs ? TextWidth.TabStop : _options.IndentSize);

    void Break() {
        Flush();
        EndLine();
    }

    void EndLine() {
        if (_empty) {
            return;
        }

        _lines.Add(new XmlDocLine(_current.ToString(), false));
        _current.Clear();
        _width = 0;
        _empty = true;
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
