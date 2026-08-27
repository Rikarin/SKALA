using System.Collections.Immutable;
using Rikarin.Skala.Options;

namespace Rikarin.Skala.Formatting.CSharp;

/// <summary>
/// The <c>resharper_xmldoc_*</c> subset the documentation-comment sub-formatter reads.
/// </summary>
/// <remarks>
/// ⚠ These keys are pinned differently from every other formatter option in Skala, and the
/// difference is not a shortcut — it is the only thing available. Every other option is Tier A
/// because a committed <c>.expected.cs</c> produced by <c>jb cleanupcode</c> shows the oracle doing
/// the thing the option names. <c>jb cleanupcode</c> 2025.2.6 does not format documentation
/// comments at all (SK-DIV-0006, measured), so no fixture can ever show it, and an oracle that
/// never moves would score a correct re-wrap as a divergence.
/// <para>
/// ⚠ That used to be the argument for keeping the sub-formatter behind a flag, and it was the
/// wrong conclusion from a correct measurement. <b>Rider's editor formats documentation comments;
/// <c>jb cleanupcode</c> does not.</b> Both are true, and where they disagree ADR-011's oracle is
/// not the specification — Rider is. So the sub-formatter runs by default, these keys govern real
/// output, and the only way to switch them off wholesale is <c>skala format --no-xmldoc</c>.
/// </para>
/// <para>
/// The ids are registered <c>OfUnoracled</c> rather than <c>OfInert</c>: read, honoured, and never
/// claiming Tier A, because Tier A is fixture evidence and there is none to be had. What pins them
/// instead is two things that need no oracle: hand-written fixtures asserting the semantics
/// JetBrains' own settings pages state, and the round-trip property in
/// <see cref="XmlDocFormatter"/>, which is checked on every comment of every run rather than on a
/// fixture.
/// </para>
/// <para>
/// ⚠ The ten keys of the family that are <em>not</em> here are refused rather than pending, and
/// <see cref="XmlDocIds.Refused"/> carries the reason for each.
/// </para>
/// </remarks>
public readonly struct XmlDocOptions {
    public XmlDocOptions(in FormattingOptions options) {
        WrapLines = options.GetBool(XmlDocIds.WrapLines);
        MaxLineLength = options.GetInt(XmlDocIds.MaxLineLength) is var width and > 0 ? width : 120;
        WrapText = options.GetBool(XmlDocIds.WrapText);
        WrapTagsAndPi = options.GetBool(XmlDocIds.WrapTagsAndPi);
        KeepUserLinebreaks = options.GetBool(XmlDocIds.KeepUserLinebreaks);
        MaxBlankLinesBetweenTags = Math.Max(0, options.GetInt(XmlDocIds.MaxBlankLinesBetweenTags));
        IndentChildElements = (ChildIndentStyle)options.GetRaw(XmlDocIds.IndentChildElements);
        IndentText = (ChildIndentStyle)options.GetRaw(XmlDocIds.IndentText);
        LinebreaksInsideTagsForElementsWithChildElements =
            options.GetBool(XmlDocIds.LinebreaksInsideTagsForElementsWithChildElements);
        LinebreaksInsideTagsForMultilineElements =
            options.GetBool(XmlDocIds.LinebreaksInsideTagsForMultilineElements);
        LinebreakBeforeMultilineElements = options.GetBool(XmlDocIds.LinebreakBeforeMultilineElements);
        LinebreakBeforeSinglelineElements = options.GetBool(XmlDocIds.LinebreakBeforeSinglelineElements);
        SpacesInsideTags = options.GetBool(XmlDocIds.SpacesInsideTags);
        SpaceBeforeSelfClosing = options.GetBool(XmlDocIds.SpaceBeforeSelfClosing);
        SpaceAfterTripleSlash = options.GetBool(XmlDocIds.SpaceAfterTripleSlash);

        IndentSize = Math.Max(1, options.GetInt(XmlDocIds.IndentSize));
        UseTabs = options.GetRaw(XmlDocIds.IndentStyle) == (int)IndentStyle.Tab;

        LinebreakBeforeElements = Split(options.GetString(XmlDocIds.LinebreakBeforeElements));
    }

    /// <summary><c>resharper_xmldoc_wrap_lines</c>: the master switch for width-driven wrapping.</summary>
    public bool WrapLines { get; }

    /// <summary><c>resharper_xmldoc_max_line_length</c>: the column the whole line must fit in.</summary>
    /// <remarks>
    /// ⚠ Measured from column 0 of the file, including the code indentation and the <c>///</c>
    /// marker. The alternative reading — a budget for the comment's own text — would make the same
    /// sentence wrap differently at two nesting depths and produce lines past the margin, which is
    /// the one thing a hard wrap exists to prevent.
    /// </remarks>
    public int MaxLineLength { get; }

    /// <summary><c>resharper_xmldoc_wrap_text</c>: whether prose may be re-flowed.</summary>
    public bool WrapText { get; }

    /// <summary><c>resharper_xmldoc_wrap_tags_and_pi</c>: whether a tag may be moved to a new line to fit.</summary>
    public bool WrapTagsAndPi { get; }

    /// <summary>
    /// <c>resharper_xmldoc_keep_user_linebreaks</c>: a line break the author wrote is a line break.
    /// </summary>
    /// <remarks>
    /// ⚠ The key with the largest effect, and the export leaves it at <c>true</c>. True means the
    /// sub-formatter may <em>split</em> a line that does not fit and may never <em>join</em> two the
    /// author separated, so a hand-shaped paragraph and an ASCII table in a <c>&lt;remarks&gt;</c>
    /// keep their shape. Implementing <c>wrap_text</c> without deciding this is not possible, which
    /// is why it is in the implemented set rather than the refused one.
    /// </remarks>
    public bool KeepUserLinebreaks { get; }

    /// <summary><c>resharper_xmldoc_max_blank_lines_between_tags</c>: the export sets 0.</summary>
    public int MaxBlankLinesBetweenTags { get; }

    /// <summary><c>resharper_xmldoc_indent_child_elements</c>: inside an element with no text.</summary>
    /// <remarks>
    /// ⚠ <c>do_not_touch</c> is mapped to "no indent", not to "keep the author's". Under a re-wrap
    /// the author's indentation no longer exists to keep — the line breaks it hung off have been
    /// recomputed — so honouring the name literally would mean honouring an input that is gone.
    /// </remarks>
    public ChildIndentStyle IndentChildElements { get; }

    /// <summary><c>resharper_xmldoc_indent_text</c>: inside an element that contains text.</summary>
    public ChildIndentStyle IndentText { get; }

    /// <summary>
    /// <c>resharper_xmldoc_linebreaks_inside_tags_for_elements_with_child_elements</c>: whether
    /// <c>&lt;remarks&gt;&lt;para&gt;…&lt;/para&gt;&lt;/remarks&gt;</c> puts its children on their
    /// own lines even when they would fit.
    /// </summary>
    public bool LinebreaksInsideTagsForElementsWithChildElements { get; }

    /// <summary>
    /// <c>resharper_xmldoc_linebreaks_inside_tags_for_multiline_elements</c>: whether an element
    /// that does not fit on one line puts its content on lines of its own.
    /// </summary>
    /// <remarks>
    /// False means the start tag keeps the first words of its content —
    /// <c>&lt;summary&gt;Some text that</c> — which is the layout ReSharper calls "do not break
    /// inside the tag".
    /// </remarks>
    public bool LinebreaksInsideTagsForMultilineElements { get; }

    /// <summary><c>resharper_xmldoc_linebreak_before_multiline_elements</c>.</summary>
    public bool LinebreakBeforeMultilineElements { get; }

    /// <summary><c>resharper_xmldoc_linebreak_before_singleline_elements</c>.</summary>
    public bool LinebreakBeforeSinglelineElements { get; }

    /// <summary>
    /// <c>resharper_xmldoc_spaces_inside_tags</c>: <c>&lt;summary&gt; Text &lt;/summary&gt;</c>.
    /// </summary>
    public bool SpacesInsideTags { get; }

    /// <summary><c>resharper_xmldoc_space_before_self_closing</c>: <c>&lt;br/&gt;</c> or <c>&lt;br /&gt;</c>.</summary>
    public bool SpaceBeforeSelfClosing { get; }

    /// <summary>
    /// <c>resharper_space_after_triple_slash</c>, live only inside the sub-formatter.
    /// </summary>
    /// <remarks>
    /// ⚠ Demoted from Tier A in milestone 3 because the oracle does not insert the space and doing
    /// it anyway cost 79 lines across 15 files of <c>corpus/real/</c> (SK-DIV-0006). The demotion
    /// stands and its reason does not: those 79 lines were <c>jb cleanupcode</c> declining to do
    /// what Rider does, charged to Skala. The space is inserted again — by the sub-formatter, on
    /// every well-formed comment, which is every comment whose marker is being rewritten anyway —
    /// and the key is Tier D for ever, because no fixture can pin it.
    /// </remarks>
    public bool SpaceAfterTripleSlash { get; }

    /// <summary><c>resharper_xmldoc_indent_size</c>: the indent unit inside the comment.</summary>
    public int IndentSize { get; }

    /// <summary><c>resharper_xmldoc_indent_style</c>.</summary>
    public bool UseTabs { get; }

    /// <summary>
    /// <c>resharper_xmldoc_linebreak_before_elements</c>: the export lists
    /// <c>summary,remarks,example,returns,param,typeparam,value,para</c>.
    /// </summary>
    /// <remarks>
    /// ⚠ Read as "this element owns its own line", which is a line break before it <em>and</em>
    /// before whatever follows it. JetBrains' page says only "always place the following elements on
    /// a new line"; the strict reading leaves <c>&lt;/param&gt;&lt;param …&gt;</c> pairs sharing a
    /// line with the text after them, which is not a layout anyone configuring this key is asking
    /// for. The choice is recorded because it is a choice and no oracle settles it.
    /// </remarks>
    public ImmutableArray<string> LinebreakBeforeElements { get; }

    public bool BreakBefore(string element) => LinebreakBeforeElements.Contains(element, StringComparer.Ordinal);

    /// <summary>The indent unit, as text.</summary>
    public string IndentUnit => UseTabs ? "\t" : new string(' ', IndentSize);

    /// <summary>How many indent units the content of an element is worth.</summary>
    public static int Delta(ChildIndentStyle style) => style == ChildIndentStyle.OneIndent ? 1 : 0;

    static ImmutableArray<string> Split(string? value) {
        if (string.IsNullOrWhiteSpace(value)) {
            return [];
        }

        var builder = ImmutableArray.CreateBuilder<string>();
        foreach (var part in value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)) {
            builder.Add(part);
        }

        return builder.ToImmutable();
    }
}

/// <summary>
/// The registry ids the sub-formatter reads, and the ones it refuses.
/// </summary>
/// <remarks>
/// ⚠ Every id here is registered through <see cref="Ids"/>'s unoracled path, so none of them enters
/// <see cref="PhaseOneOptions.Implemented"/> and none of them claims Tier A. That is the honest
/// state: Tier A means "pinned by an oracle fixture" and the oracle has nothing to say here. It is
/// <em>not</em> the inert path, which would say these keys change nothing, and they change output on
/// every file with a documentation comment in it.
/// </remarks>
public static class XmlDocIds {
    public static readonly OptionId WrapLines = Ids.XmlDocWrapLines;
    public static readonly OptionId MaxLineLength = Ids.XmlDocMaxLineLength;
    public static readonly OptionId WrapText = Ids.XmlDocWrapText;
    public static readonly OptionId WrapTagsAndPi = Ids.XmlDocWrapTagsAndPi;
    public static readonly OptionId KeepUserLinebreaks = Ids.XmlDocKeepUserLinebreaks;
    public static readonly OptionId MaxBlankLinesBetweenTags = Ids.XmlDocMaxBlankLinesBetweenTags;
    public static readonly OptionId IndentChildElements = Ids.XmlDocIndentChildElements;
    public static readonly OptionId IndentText = Ids.XmlDocIndentText;

    public static readonly OptionId LinebreaksInsideTagsForElementsWithChildElements =
        Ids.XmlDocLinebreaksInsideTagsForElementsWithChildElements;

    public static readonly OptionId LinebreaksInsideTagsForMultilineElements =
        Ids.XmlDocLinebreaksInsideTagsForMultilineElements;

    public static readonly OptionId LinebreakBeforeMultilineElements = Ids.XmlDocLinebreakBeforeMultilineElements;
    public static readonly OptionId LinebreakBeforeSinglelineElements = Ids.XmlDocLinebreakBeforeSinglelineElements;
    public static readonly OptionId SpacesInsideTags = Ids.XmlDocSpacesInsideTags;
    public static readonly OptionId SpaceBeforeSelfClosing = Ids.XmlDocSpaceBeforeSelfClosing;
    public static readonly OptionId SpaceAfterTripleSlash = Ids.SpaceAfterTripleSlash;
    public static readonly OptionId IndentSize = Ids.XmlDocIndentSize;
    public static readonly OptionId IndentStyle = Ids.XmlDocIndentStyle;
    public static readonly OptionId LinebreakBeforeElements = Ids.XmlDocLinebreakBeforeElements;

    /// <summary>The seventeen <c>resharper_xmldoc_*</c> keys the sub-formatter honours.</summary>
    public static ImmutableArray<OptionId> Honoured => [
        WrapLines,
        MaxLineLength,
        WrapText,
        WrapTagsAndPi,
        KeepUserLinebreaks,
        MaxBlankLinesBetweenTags,
        IndentChildElements,
        IndentText,
        LinebreaksInsideTagsForElementsWithChildElements,
        LinebreaksInsideTagsForMultilineElements,
        LinebreakBeforeMultilineElements,
        LinebreakBeforeSinglelineElements,
        SpacesInsideTags,
        SpaceBeforeSelfClosing,
        IndentSize,
        IndentStyle,
        LinebreakBeforeElements
    ];

    /// <summary>
    /// The ten keys of the family the sub-formatter refuses, and why each one.
    /// </summary>
    /// <remarks>
    /// ⚠ Refused, not pending. Each of these needs a fact the project does not have and cannot get:
    /// six of them describe rewriting the inside of a tag header, which Skala does not do at all;
    /// three describe a behaviour that only becomes observable at a value the export does not set,
    /// so choosing one would be inventing it; and one has no meaning for a <c>///</c> comment.
    /// </remarks>
    public static ImmutableArray<KeyValuePair<string, string>> Refused => [
        new(
            "resharper_xmldoc_attribute_indent",
            "Skala emits a tag header byte-for-byte and never breaks inside one, so attributes are never indented."
        ),
        new(
            "resharper_xmldoc_attribute_style",
            "Same: the header is copied from the source, so there is no arrangement to choose."
        ),
        new(
            "resharper_xmldoc_space_after_last_attribute",
            "Same. A space before the closing '>' would be a rewrite of the header."
        ),
        new(
            "resharper_xmldoc_spaces_around_eq_in_attribute",
            "Same, and this one is load-bearing: a cref= or name= attribute is read by the compiler and by the doc build, and Skala will not edit inside it for a whitespace preference."
        ),
        new(
            "resharper_xmldoc_alignment_tab_fill_style",
            "Alignment applies to a wrapped tag header, which never happens."
        ),
        new(
            "resharper_xmldoc_allow_far_alignment",
            "Same: nothing is ever aligned, so 'too large' has no subject."
        ),
        new(
            "resharper_xmldoc_linebreaks_inside_tags_for_elements_longer_than",
            "The export sets int.MaxValue — 'never' — and what ReSharper measures against it (the flat element, its text, its child count) is not stated anywhere. A threshold that is never crossed cannot be pinned by a fixture and cannot be guessed from behaviour."
        ),
        new(
            "resharper_xmldoc_wrap_around_elements",
            "Indistinguishable from resharper_xmldoc_wrap_tags_and_pi without an oracle. Honouring both would mean inventing a difference between them and then pinning the invention."
        ),
        new(
            "resharper_xmldoc_tab_width",
            "It only changes how wide a tab is when measuring a line, and the only tab a re-wrap can meet is inside a <code> block, which is emitted verbatim and never measured."
        ),
        new(
            "resharper_xmldoc_insert_final_newline",
            "A '///' comment has no file end to put a newline at."
        )
    ];
}
