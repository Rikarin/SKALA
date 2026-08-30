using System.Collections.Immutable;
using Rikarin.Skala.Options;

namespace Rikarin.Skala.Formatting.CSharp;

/// <summary>
///     The <c>resharper_xmldoc_*</c> subset the documentation-comment sub-formatter reads.
/// </summary>
/// <remarks>
///     ⚠ These keys used to be pinned differently from every other formatter option in Skala, and
///     the paragraph that said so has been withdrawn. It ran: every committed fixture was generated
///     under <see cref="OracleProfile.FormatOnly" />, which does not enable
///     <c>CSharpFormatDocComments</c>, so no fixture can ever show the oracle doing any of this
///     (SK-DIV-0006). The first half is true and the conclusion does not follow — it is a fact about
///     which fixtures happen to exist, not about what a fixture can be.
///     <para>
///         ⚠ That was the original SK-DIV-0006 mistake repeated one level down: a limitation of the
///         oracle <em>profile</em>, read as a limitation of the corpus.
///         <see cref="OracleProfile.DocComments" /> is <see cref="OracleProfile.FormatOnly" /> plus that
///         one element, <c>constructs/xmldoc/</c> carries a corpus file per key with its answer beside
///         it, and the family splits <b>13 Tier A / 9 measured-and-disagreeing</b>.
///     </para>
///     <para>
///         ⚠ Rider formats documentation comments and the pinned cleanup profile does not; where the two
///         disagree, ADR-011's oracle is not the specification — Rider is. So the sub-formatter runs by
///         default, these keys govern real output, and the only way to switch them off wholesale is
///         <c>skala format --no-xmldoc</c>.
///     </para>
///     <para>
///         The nine that stay are still registered <c>OfUnoracled</c>, and the mark now means something
///         narrower and more useful: not "the oracle cannot be asked" but "the oracle was asked and said
///         something else". SK-DIV-0019 … SK-DIV-0023 carry the shapes, and
///         <c>XmlDocOracleTests</c> asserts that each of them still fails, so a divergence that gets
///         fixed cannot quietly stay Tier D. What pins all 22 besides the fixtures is unchanged:
///         hand-written cases asserting the semantics JetBrains' own settings pages state, and the
///         round-trip property in <see cref="XmlDocFormatter" />, which is checked on every comment of
///         every run rather than on a fixture.
///     </para>
///     <para>
///         ⚠ The ten keys of the family that are <em>not</em> here are refused rather than pending, and
///         <see cref="XmlDocIds.Refused" /> carries the reason for each.
///     </para>
/// </remarks>
public readonly struct XmlDocOptions {
    public XmlDocOptions(in FormattingOptions options) {
        WrapLines = options.GetBool(XmlDocIds.WrapLines);
        // ⚠ `0` is a width of zero and not a stand-in for 120, and the line here used to say the
        // opposite. Measured under `OracleProfile.DocComments` on a 170-column `<summary>`: at
        // `resharper_xmldoc_max_line_length = 0` and again at `1` the oracle puts every single word
        // of the prose on its own line, which is what a budget of zero means and is nothing like the
        // two-line output it gives at 120. A negative width is not a width and still falls back.
        MaxLineLength = options.GetInt(XmlDocIds.MaxLineLength) is var width and >= 0 ? width : 120;
        WrapText = options.GetBool(XmlDocIds.WrapText);
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
        SpaceAfterLastAttribute = options.GetBool(XmlDocIds.SpaceAfterLastAttribute);
        SpacesAroundEqInAttribute = options.GetBool(XmlDocIds.SpacesAroundEqInAttribute);
        BlankLineAfterPi = options.GetBool(XmlDocIds.BlankLineAfterPi);
        LinebreaksInsideTagsForElementsLongerThan =
            options.GetInt(XmlDocIds.LinebreaksInsideTagsForElementsLongerThan) is var threshold and >= 0
                ? threshold
                : int.MaxValue;

        // ⚠ The *C#* indent, not `resharper_xmldoc_indent_size`, and this is measured. On
        // `/// <remarks>` holding a `/// <para>`, under `OracleProfile.DocComments`:
        // `resharper_xmldoc_indent_size = 1` leaves the child at four columns, while `indent_size = 1`
        // moves it to one and `indent_size = 2` (with `tab_width = 8` in the same run, so the tab
        // width could not be answering) moves it to two. `resharper_xmldoc_indent_style = tab` leaves
        // the child indented with spaces; `indent_style = tab` puts tabs on the *code* lines and
        // leaves the comment's inner indent four spaces. So a documentation comment inside a C# file
        // takes its indent width from the file's own `indent_size` and always spends spaces on it.
        // Both `xmldoc_` keys are registered inert on that measurement.
        IndentSize = Math.Max(1, options.GetInt(Ids.IndentSize));

        // ⚠ Read so the plumbing exists and the crash snapshot records them; the values above are
        // what the layout uses. See `XmlDocIds.Inert`.
        _ = options.GetInt(XmlDocIds.IndentSize);
        _ = options.GetRaw(XmlDocIds.IndentStyle);

        LinebreakBeforeElements = Split(options.GetString(XmlDocIds.LinebreakBeforeElements));
    }

    /// <summary><c>resharper_xmldoc_wrap_lines</c>: the master switch for width-driven wrapping.</summary>
    public bool WrapLines { get; }

    /// <summary>
    ///     <c>resharper_xmldoc_max_line_length</c>: the column the line must fit in, measured from
    ///     the character after the <c>///</c>.
    /// </summary>
    /// <remarks>
    ///     ⚠
    ///     <b>
    ///         This remark used to say the opposite and the opposite was an argument, not a
    ///         measurement.
    ///     </b> It read: "measured from column 0 of the file, including the code
    ///     indentation and the <c>///</c> marker. The alternative reading — a budget for the comment's
    ///     own text — would make the same sentence wrap differently at two nesting depths and produce
    ///     lines past the margin, which is the one thing a hard wrap exists to prevent." Probed at four
    ///     content indents, three code indents, four start-tag widths and two margins, the oracle wraps
    ///     a documentation line when <c>marker + indent + content</c> passes this value: the same
    ///     sentence wraps <em>identically</em> at every nesting depth, and the file's own columns run
    ///     <c>codeIndent + 3</c> past the margin. The rejected reading is the one the tool has.
    ///     <para>
    ///         ⚠ That was SK-DIV-0019, and with it five keys — every fixture in the family that has to
    ///         wrap measured this one arithmetic. Two more came with it once the same probe reached
    ///         them: an element's content is laid out from the column its start tag closes at
    ///         (<c>XmlDocRenderer</c>'s carry), and an element is opened when that content overflows,
    ///         with the end tag outside the comparison. The entry's own summary — "the oracle keeps the
    ///         word that crosses the margin" — fits the five committed fixtures and nothing else; with
    ///         five-letter words a probe cannot tell a budget of 113 from one of 118, which is how it
    ///         survived.
    ///     </para>
    /// </remarks>
    public int MaxLineLength { get; }

    /// <summary><c>resharper_xmldoc_wrap_text</c>: whether prose may be re-flowed.</summary>
    public bool WrapText { get; }

    /// <summary>
    ///     <c>resharper_xmldoc_keep_user_linebreaks</c>: a line break the author wrote is a line break.
    /// </summary>
    /// <remarks>
    ///     ⚠ The key with the largest effect, and the export leaves it at <c>true</c>. True means the
    ///     sub-formatter may <em>split</em> a line that does not fit and may never <em>join</em> two the
    ///     author separated, so a hand-shaped paragraph and an ASCII table in a <c>&lt;remarks&gt;</c>
    ///     keep their shape. Implementing <c>wrap_text</c> without deciding this is not possible, which
    ///     is why it is in the implemented set rather than the refused one.
    /// </remarks>
    public bool KeepUserLinebreaks { get; }

    /// <summary><c>resharper_xmldoc_max_blank_lines_between_tags</c>: the export sets 0.</summary>
    public int MaxBlankLinesBetweenTags { get; }

    /// <summary><c>resharper_xmldoc_indent_child_elements</c>: inside an element with no text.</summary>
    /// <remarks>
    ///     ⚠ <c>do_not_touch</c> is mapped to "no indent", not to "keep the author's". Under a re-wrap
    ///     the author's indentation no longer exists to keep — the line breaks it hung off have been
    ///     recomputed — so honouring the name literally would mean honouring an input that is gone.
    /// </remarks>
    public ChildIndentStyle IndentChildElements { get; }

    /// <summary><c>resharper_xmldoc_indent_text</c>: inside an element that contains text.</summary>
    public ChildIndentStyle IndentText { get; }

    /// <summary>
    ///     <c>resharper_xmldoc_linebreaks_inside_tags_for_elements_with_child_elements</c>: whether
    ///     <c>&lt;remarks&gt;&lt;para&gt;…&lt;/para&gt;&lt;/remarks&gt;</c> puts its children on their
    ///     own lines even when they would fit.
    /// </summary>
    public bool LinebreaksInsideTagsForElementsWithChildElements { get; }

    /// <summary>
    ///     <c>resharper_xmldoc_linebreaks_inside_tags_for_multiline_elements</c>: whether an element
    ///     that does not fit on one line puts its content on lines of its own.
    /// </summary>
    /// <remarks>
    ///     False means the start tag keeps the first words of its content —
    ///     <c>&lt;summary&gt;Some text that</c> — which is the layout ReSharper calls "do not break
    ///     inside the tag".
    /// </remarks>
    public bool LinebreaksInsideTagsForMultilineElements { get; }

    /// <summary><c>resharper_xmldoc_linebreak_before_multiline_elements</c>.</summary>
    public bool LinebreakBeforeMultilineElements { get; }

    /// <summary><c>resharper_xmldoc_linebreak_before_singleline_elements</c>.</summary>
    public bool LinebreakBeforeSinglelineElements { get; }

    /// <summary>
    ///     <c>resharper_xmldoc_spaces_inside_tags</c>: <c>&lt;summary&gt; Text &lt;/summary&gt;</c>.
    /// </summary>
    /// <remarks>
    ///     ⚠ SK-DIV-0022, and the two values are not symmetric readings. <b>True</b> is a statement
    ///     about the output: exactly one space each side, and the author's two collapse to one.
    ///     <b>False</b> is a statement about what the run may <em>insert</em>: it adds nothing and the
    ///     author's own gap survives, per side and verbatim, even while the same run splits the
    ///     elements around it. Skala used to read false as "no gap, whatever the author wrote". Both
    ///     values probed, because taking the second from the first is how a key gets demoted.
    ///     <para>
    ///         ⚠ Only a <em>flat</em> element has an author's gap left to keep — see
    ///         <see cref="XmlDocElement.InnerLead" />. One the run opens up has its content re-flowed,
    ///         and the oracle drops the spaces there too.
    ///     </para>
    /// </remarks>
    public bool SpacesInsideTags { get; }

    /// <summary><c>resharper_xmldoc_space_before_self_closing</c>: <c>&lt;br/&gt;</c> or <c>&lt;br /&gt;</c>.</summary>
    public bool SpaceBeforeSelfClosing { get; }

    /// <summary>
    ///     <c>resharper_space_after_triple_slash</c>, live only inside the sub-formatter.
    /// </summary>
    /// <remarks>
    ///     ⚠ Tier A, then inert, then unoracled, and Tier A again — no other key in the registry has
    ///     that history and each step was a correction of the one before. Milestone 3 demoted it
    ///     because the oracle did not insert the space and doing it anyway cost 79 lines across 15
    ///     files of <c>corpus/real/</c>; the default flip made it unoracled on the reading that no
    ///     fixture could ever pin it; <c>constructs/xmldoc/resharper_space_after_triple_slash.cs</c>
    ///     is that fixture (SK-DIV-0006).
    ///     <para>
    ///         ⚠ The 79 lines are still not fully re-explained, and the reason is a measured shape. The
    ///         oracle does not rewrite a <c>///</c> marker on a comment it is otherwise leaving alone: a
    ///         lone short <c>///&lt;summary&gt;Docs.&lt;/summary&gt;</c> comes back byte-identical even
    ///         with <c>CSharpFormatDocComments</c> on. Skala inserts the space on every well-formed
    ///         comment. On a comment that needs no other change the two genuinely differ, and
    ///         <c>corpus/real/</c>'s fixtures are all of that kind.
    ///     </para>
    /// </remarks>
    public bool SpaceAfterTripleSlash { get; }

    /// <summary>
    ///     <c>resharper_xmldoc_space_after_last_attribute</c>: <c>&lt;param name="a" &gt;</c>.
    /// </summary>
    /// <remarks>
    ///     ⚠ Start tags only. A self-closing tag's gap is <c>space_before_self_closing</c>'s, and the
    ///     oracle leaves <c>&lt;see cref="C" /&gt;</c> alone when this key is on — measured, because the
    ///     two keys reading the same gap would have been the obvious guess.
    ///     <para>⚠ And only when there is a last attribute: <c>&lt;summary&gt;</c> never grows a space.</para>
    /// </remarks>
    public bool SpaceAfterLastAttribute { get; }

    /// <summary>
    ///     <c>resharper_xmldoc_spaces_around_eq_in_attribute</c>: <c>name = "a"</c> or <c>name="a"</c>.
    /// </summary>
    /// <remarks>
    ///     ⚠ Whitespace around the <c>=</c> only. The attribute's name and its quoted value are the
    ///     source bytes either way, quote character included — the oracle keeps <c>name='x'</c> single
    ///     quoted, and so does this.
    /// </remarks>
    public bool SpacesAroundEqInAttribute { get; }

    /// <summary>
    ///     <c>resharper_xmldoc_blank_line_after_pi</c>: a blank <c>///</c> line after a
    ///     <c>&lt;?…?&gt;</c>.
    /// </summary>
    /// <remarks>
    ///     ⚠ The export leaves this at its default <c>true</c>, so a processing instruction in a doc
    ///     comment has been getting a blank line after it from Rider all along and not from Skala.
    ///     <para>
    ///         ⚠ The blank line is <c>///</c> plus the marker space, and this remark used to say it was
    ///         not: "the trailing space is not reproduced, for the same reason
    ///         <c>max_blank_lines_between_tags</c>'s blank lines do not carry one: an empty line's
    ///         trailing whitespace is the one thing every other pass in Skala strips." That is a fact
    ///         about Skala standing where a measurement belonged, and it named its own refutation —
    ///         probed at <c>max_blank_lines_between_tags = 1</c>, <em>those</em> blank lines carry the
    ///         space too. The space belongs to the marker, which is what SK-DIV-0023's first half had
    ///         already concluded for verbatim lines. A blank line inside a <c>&lt;code&gt;</c> block
    ///         still has none: those columns are the sample's.
    ///     </para>
    /// </remarks>
    public bool BlankLineAfterPi { get; }

    /// <summary>
    ///     <c>resharper_xmldoc_linebreaks_inside_tags_for_elements_longer_than</c>: open an element up
    ///     once its content is longer than this, however well it would have fitted.
    /// </summary>
    /// <remarks>
    ///     ⚠ Measured rather than guessed, and both halves of the guess were wrong. What is compared is
    ///     the element's <em>flat inner content</em> — its text and its child markup, not the start tag
    ///     and not the end tag — and the comparison is strictly greater: at 12, a twelve-character
    ///     content stays on the line and a thirteen-character one does not.
    ///     <para>
    ///         ⚠ <c>0</c> therefore means "always", not "never"; the registry's bounds note said the
    ///         opposite. "Never" is the export's own <c>int.MaxValue</c>, which is why the key looked
    ///         unpinnable.
    ///     </para>
    ///     <para>
    ///         ⚠ Narrower than the oracle by one deliberate step: the oracle applies the threshold to
    ///         <c>&lt;c&gt;</c> too, and breaking a verbatim element open would put Skala's byte-for-byte
    ///         code body on a re-indented line. Recorded as a measured narrowing rather than left as an
    ///         unmeasured guess.
    ///     </para>
    /// </remarks>
    public int LinebreaksInsideTagsForElementsLongerThan { get; }

    /// <summary>
    ///     The indent unit inside the comment, in columns — the C# <c>indent_size</c>.
    /// </summary>
    /// <remarks>
    ///     ⚠ <b>Not <c>resharper_xmldoc_indent_size</c>, which the oracle does not read.</b> On
    ///     `/// &lt;remarks&gt;` holding a `/// &lt;para&gt;`, under <c>OracleProfile.DocComments</c>:
    ///     <c>resharper_xmldoc_indent_size = 1</c> leaves the child at four columns, while
    ///     <c>indent_size = 1</c> moves it to one and <c>indent_size = 2</c> — asked with
    ///     <c>tab_width = 8</c> in the same run so a tab width could not be answering — moves it to two.
    ///     A documentation comment inside a C# file takes its indent width from the file's own
    ///     <c>indent_size</c>.
    /// </remarks>
    public int IndentSize { get; }


    /// <summary>
    ///     <c>resharper_xmldoc_linebreak_before_elements</c>: the export lists
    ///     <c>summary,remarks,example,returns,param,typeparam,value,para</c>.
    /// </summary>
    /// <remarks>
    ///     ⚠ Read as "this element owns its own line", which is a line break before it <em>and</em>
    ///     before whatever follows it. JetBrains' page says only "always place the following elements on
    ///     a new line"; the strict reading leaves <c>&lt;/param&gt;&lt;param …&gt;</c> pairs sharing a
    ///     line with the text after them, which is not a layout anyone configuring this key is asking
    ///     for. The choice is recorded because it is a choice and no oracle settles it.
    /// </remarks>
    public ImmutableArray<string> LinebreakBeforeElements { get; }

    public bool BreakBefore(string element) => LinebreakBeforeElements.Contains(element, StringComparer.Ordinal);

    /// <summary>
    ///     The indent unit, as text. ⚠ Always spaces, and <c>resharper_xmldoc_indent_style</c> does not
    ///     change that.
    /// </summary>
    /// <remarks>
    ///     ⚠ Measured on the same fixture as <see cref="IndentSize" />:
    ///     <c>resharper_xmldoc_indent_style = tab</c> leaves the child element indented with four
    ///     spaces, and <c>indent_style = tab</c> puts tabs on the file's <em>code</em> lines while
    ///     leaving the comment's inner indent four spaces. The character inside a documentation comment
    ///     is not configurable in either direction.
    /// </remarks>
    public string IndentUnit => new(' ', IndentSize);

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
///     The registry ids the sub-formatter reads, and the ones it refuses.
/// </summary>
/// <remarks>
///     ⚠ Most ids here are registered through <see cref="Ids" />'s unoracled path, so they do not enter
///     <see cref="PhaseOneOptions.Implemented" />. It is <em>not</em> the inert path, which would say
///     these keys change nothing, and they change output on every file with a documentation comment in
///     it.
///     <para>
///         ⚠
///         <b>
///             This remark used to end "none of them claims Tier A … the oracle has nothing to say
///             here". That stopped being true on 2026-08-29 and the sentence outlived the fact.
///         </b> The
///         <c>DocComments</c> profile enables <c>CSharpFormatDocComments</c>, the fixtures under
///         <c>constructs/xmldoc/</c> carry <c>oracle</c> globs, and the committed sweep holds a row for
///         each. Three came back Conformant at every value and moved to <c>Ids.Of</c>, which is what
///         makes them Tier A: they were always phase-one ids and only the <c>OfUnoracled</c> mark held
///         them out of <c>Ids.All</c>.
///     </para>
///     <para>
///         ⚠ So "the sub-formatter is a component the tier invariant cannot see" is <em>not</em> the
///         defect, and it was briefly fixed that way before being reverted. Nothing was invisible; the
///         exclusion was deliberate, and its stated premise — "the sweep has never swept these" —
///         simply expired. Fix a stale premise where it is written rather than by widening the
///         invariant that believed it.
///     </para>
/// </remarks>
public static class XmlDocIds {
    public static readonly OptionId WrapLines = Ids.XmlDocWrapLines;
    public static readonly OptionId MaxLineLength = Ids.XmlDocMaxLineLength;
    public static readonly OptionId WrapText = Ids.XmlDocWrapText;
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
    public static readonly OptionId SpaceAfterLastAttribute = Ids.XmlDocSpaceAfterLastAttribute;
    public static readonly OptionId SpacesAroundEqInAttribute = Ids.XmlDocSpacesAroundEqInAttribute;
    public static readonly OptionId BlankLineAfterPi = Ids.XmlDocBlankLineAfterPi;

    public static readonly OptionId LinebreaksInsideTagsForElementsLongerThan =
        Ids.XmlDocLinebreaksInsideTagsForElementsLongerThan;

    /// <summary>The twenty-one <c>resharper_xmldoc_*</c> keys the sub-formatter honours.</summary>
    public static ImmutableArray<OptionId> Honoured => [
        WrapLines,
        MaxLineLength,
        WrapText,
        KeepUserLinebreaks,
        MaxBlankLinesBetweenTags,
        IndentChildElements,
        IndentText,
        LinebreaksInsideTagsForElementsWithChildElements,
        LinebreaksInsideTagsForMultilineElements,
        LinebreaksInsideTagsForElementsLongerThan,
        LinebreakBeforeMultilineElements,
        LinebreakBeforeSinglelineElements,
        SpacesInsideTags,
        SpaceBeforeSelfClosing,
        SpaceAfterLastAttribute,
        SpacesAroundEqInAttribute,
        BlankLineAfterPi,
        LinebreakBeforeElements
    ];

    /// <summary>
    ///     The keys of the family the sub-formatter does not honour, and why each one.
    /// </summary>
    /// <remarks>
    ///     ⚠ <b>This list was four-fifths wrong, and every wrong entry was wrong the same way.</b> Each
    ///     said "no oracle can settle this" when what was true is that the profile the oracle runs under
    ///     never asked it. <c>CSharpFormatDocComments</c> is a real cleanup task in
    ///     <c>jb cleanupcode</c> 2025.2.6 and neither profile in <c>OracleProfile</c> enables it, so every
    ///     probe of every one of these keys came back "unchanged" — and eight of them were written up as
    ///     properties of the key. Turn the task on and the oracle rewrites tag headers freely: it
    ///     collapses the spaces between attributes, drops the space before <c>&gt;</c>, moves the
    ///     whitespace around <c>=</c>, and wraps a header that does not fit onto a continuation line.
    ///     That is SK-DIV-0006's exact shape, one level down, six milestones later.
    ///     <para>
    ///         ⚠ Four of the eight are now implemented and have left this list. What remains is split into
    ///         two honest kinds, and only the second kind is a property of the key.
    ///     </para>
    /// </remarks>
    public static ImmutableArray<KeyValuePair<string, string>> Refused => [
        // ── Measured, real, and not implemented: header wrapping ─────────────────────────────
        // ⚠ These four are pending rather than refused, and they share one prerequisite: Skala
        // never breaks a line inside a tag header, and the oracle does. Until the renderer can
        // wrap a header, none of the four has a subject in Skala's output — but each of them
        // plainly has one in the oracle's, so the reason is Skala's shape and says nothing
        // about the key.
        new(
            "resharper_xmldoc_attribute_indent",
            "Pending, not refused, and now MEASURED rather than assumed. It chooses how a wrapped tag header's continuation lines are indented and all three of its values separate: with the tag opening at column 12, `single_indent` (the export) puts them at 16, `double_indent` at 20, and `align_by_first_attribute` at 17 — under the first attribute. Skala does not yet wrap or re-read a header, so it has no subject here. SK-DIV-0079."
        ),
        new(
            "resharper_xmldoc_attribute_style",
            "Pending, not refused. ⚠ The reason recorded here — 'it arranges the attributes of a header Skala does not yet wrap' — is MEASURED FALSE: it does not wait for a wrap. `on_different_lines` puts the tag name alone and every attribute on its own line, and `first_attribute_on_single_line` keeps the first on the tag's line and breaks the rest, and BOTH do so to a header that fits on one line comfortably. What survives is the second half: the export leaves it at `do_not_touch`, so the default costs nothing today. ⚠ `on_single_line` and `do_not_touch` are not distinguished by that probe — the shape that would is an already-wrapped short header, which `do_not_touch` demonstrably keeps wrapped — and that is unmeasured. SK-DIV-0079."
        ),
        new(
            "resharper_xmldoc_alignment_tab_fill_style",
            "Pending on the same prerequisite: it fills the alignment of a wrapped header's continuation line, and nothing is wrapped yet. ⚠ It is also a *pairwise* question and no one-key probe can reach it — like `resharper_csharp_alignment_tab_fill_style` it needs `indent_style = tab` flipped beside it (SK-DIV-0032)."
        ),
        new(
            "resharper_xmldoc_allow_far_alignment",
            "Pending on the same prerequisite: no header is aligned yet, so 'too large' still has no subject. ⚠ Asked anyway, on a header the oracle wraps of its own accord, it returns the probe byte-identical — which says nothing about the key: it governs when an `align_by_first_attribute` alignment is too far, so it needs THAT key flipped beside it and a tag name long enough to push the alignment out. Pairwise, and still unmeasured. SK-DIV-0079."
        ),
        // ⚠ The fifth of that family, and it used to be in `Honoured` under a reading of its name.
        new(
            "resharper_xmldoc_wrap_tags_and_pi",
            "Pending on the same prerequisite, and the reading it used to carry is measured false. It was read as 'whether a tag may be moved to a new line to fit', and at both values the oracle moves a <see/> off the end of a line of prose identically — the committed fixture is byte-identical at true and at false, which is the SPURIOUS row the sweep reported. What it really governs is a break INSIDE a tag header: a <see cref=... href=...> 170 columns wide comes back with its second attribute on a continuation line at true and whole at false, and the same probe leaves a <?pi ...?> alone. ⚠ And it governs only whether a break is INTRODUCED: handed a header that is already wrapped the oracle preserves the wrap at BOTH values, even when the header is short enough to fit — so `false` is 'do not break', never 'join'. That is why the reader-only fix is refused: lifting XmlDocModel's Unmodelled refusal without recording where the author's breaks fall would make XmlDocRenderer.Tag rebuild the header joined, which diverges at both values in place of one. SK-DIV-0079."
        ),

        // ── Measured inert in the oracle: the indent is the C# file's ────────────────────────
        // ⚠ These two used to be in `Honoured`, and were promoted on a fixture that could not tell
        // them from the C# indent — it agrees at `4`/`space`, which is what `indent_size = 4` and
        // ReSharper's always-spaces inner indent produce anyway. Probed under
        // `OracleProfile.DocComments` on `/// <remarks>` holding a `/// <para>`.
        new(
            "resharper_xmldoc_indent_size",
            "Inert in the oracle, and the C# `indent_size` is what really governs the gap. At `resharper_xmldoc_indent_size = 1` the child element stays at four columns; at `indent_size = 1` it moves to one, and at `indent_size = 2` — asked with `tab_width = 8` in the same run, so a tab width could not be answering — it moves to two. Skala takes the width from `indent_size` and reads this key only so the plumbing exists."
        ),
        new(
            "resharper_xmldoc_indent_style",
            "Inert in the oracle, and there is nothing to choose. At `resharper_xmldoc_indent_style = tab` the child element is still indented with spaces; at `indent_style = tab` the file's *code* lines take tabs and the comment's inner indent stays four spaces. A documentation comment's inner indent is always spaces, so Skala spends spaces and reads this key only so the plumbing exists."
        ),

        // ── Measured, and genuinely not distinguishable ──────────────────────────────────────
        new(
            "resharper_xmldoc_wrap_around_elements",
            "Measured with the doc-comment task enabled and at both values over prose containing inline elements, long and short: the oracle's output is byte-identical. Either it is subsumed by resharper_xmldoc_wrap_tags_and_pi in this build or its subject is a construct no C# doc comment produces. Refused, and now for a measured reason rather than a supposed one."
        ),

        // ── Properties of the key, unchanged ─────────────────────────────────────────────────
        new(
            "resharper_xmldoc_tab_width",
            "It only changes how wide a tab is when measuring a line, and the only tab a re-wrap can meet is inside a <code> block, which is emitted verbatim and never measured."
        ),
        new(
            "resharper_xmldoc_insert_final_newline",
            "A '///' comment has no file end to put a newline at."
        ),

        // ── The processing-instruction header family ─────────────────────────────────────────
        // ⚠ Same prerequisite as the tag-header four, one construct over: Skala emits a
        // processing instruction verbatim, so its header has no attributes to space out. The
        // export leaves pi_attribute_style at do_not_touch, which is what the oracle was
        // observed to do to `<?pi first = "1" second="2" ?>` — it left it alone.
        new(
            "resharper_xmldoc_pi_attribute_style",
            "Pending. A processing instruction is emitted verbatim, so there is no arrangement to choose. The export's do_not_touch is what the oracle was measured doing."
        ),
        new(
            "resharper_xmldoc_pi_attributes_indent",
            "Pending on the same prerequisite: a verbatim processing instruction is never wrapped, so its attributes are never indented."
        ),
        new(
            "resharper_xmldoc_space_after_last_pi_attribute",
            "Pending on the same prerequisite: the '?>' is copied from the source with whatever precedes it."
        ),
        new(
            "resharper_xmldoc_spaces_around_eq_in_pi_attribute",
            "Pending on the same prerequisite: the instruction's bytes are copied, '=' included."
        )
    ];
}
