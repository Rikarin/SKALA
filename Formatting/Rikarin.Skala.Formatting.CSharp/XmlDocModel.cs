using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Rikarin.Skala.Formatting.CSharp;

/// <summary>One thing inside a documentation comment, once the <c>///</c> markers are gone.</summary>
/// <remarks>
/// ⚠ Built from Roslyn's XML syntax rather than from an <see cref="System.Xml.XmlReader"/>, and the
/// difference is the whole safety argument. An <c>XmlReader</c> resolves <c>&amp;lt;</c> to
/// <c>&lt;</c> and <c>&amp;#65;</c> to <c>A</c>, so re-serialising its output is a rewrite of the
/// author's text disguised as a parse. Roslyn's tokens carry the source spelling, so an entity is
/// copied across as the characters the author typed.
/// </remarks>
/// <param name="Glued">
/// ⚠ No whitespace separated this node from the one before it, so nothing may be inserted between
/// them. <c>&lt;c&gt;x&lt;/c&gt;s</c> is one word and <c>&lt;c&gt;x&lt;/c&gt; s</c> is two; a
/// renderer that could not tell them apart would silently rewrite the first into the second.
/// </param>
public abstract record XmlDocNode(bool Glued);

/// <summary>One whitespace-delimited word of prose. Never split.</summary>
public sealed record XmlDocWord(string Text, bool Glued) : XmlDocNode(Glued);

/// <summary>A line break the author wrote, and the blank lines that followed it.</summary>
public sealed record XmlDocBreak(int BlankLines) : XmlDocNode(false);

/// <summary>
/// A block copied out of the source unchanged: a CDATA section, a processing instruction, an XML
/// comment.
/// </summary>
/// <remarks>
/// ⚠ Hazard 1 of docs/plan/05 § "Phase 4". Re-wrapping data changes what it says, and a code sample
/// is the part of a doc comment a reader is most likely to copy. Verbatim means the line's bytes
/// after the <c>///</c> marker, including its leading and trailing whitespace.
/// </remarks>
public sealed record XmlDocVerbatim(ImmutableArray<string> Lines) : XmlDocNode(false);

/// <summary>An element, with its start tag copied from the source byte-for-byte.</summary>
/// <param name="Name">The tag name, for <c>linebreak_before_elements</c> and the closing tag.</param>
/// <param name="Header">Everything from <c>&lt;</c> up to but not including <c>&gt;</c> or <c>/&gt;</c>.</param>
/// <param name="Verbatim">
/// ⚠ Non-null for <c>&lt;code&gt;</c> and <c>&lt;c&gt;</c>: the element's content as source lines,
/// which are emitted unchanged rather than re-wrapped.
/// </param>
/// <param name="GluedToWord">
/// ⚠ The thing directly before it, with no whitespace between, was <em>prose</em>. Only then is a
/// break before this element forbidden: <c>x&lt;see/&gt;</c> is one word, while
/// <c>&lt;/summary&gt;&lt;param&gt;</c> is two tags that <c>linebreak_before_elements</c> exists to
/// put on separate lines.
/// </param>
public sealed record XmlDocElement(
    string Name,
    string Header,
    bool SelfClosing,
    ImmutableArray<XmlDocNode> Children,
    ImmutableArray<string>? Verbatim,
    bool Glued,
    bool GluedToWord
) : XmlDocNode(Glued) {
    public bool HasChildElements => Children.Any(static child => child is XmlDocElement);

    public bool HasText => Children.Any(static child => child is XmlDocWord);
}

/// <summary>Turns one <see cref="DocumentationCommentTriviaSyntax"/> into <see cref="XmlDocNode"/>s.</summary>
public sealed class XmlDocModel {
    /// <summary>
    /// Elements whose content is data rather than prose.
    /// </summary>
    /// <remarks>
    /// ⚠ <c>&lt;code&gt;</c> and <c>&lt;c&gt;</c> are the two docs/plan/05 names, and the list is
    /// deliberately not longer. Adding <c>&lt;list&gt;</c> or <c>&lt;example&gt;</c> here because
    /// they "often contain code" would exempt the parts of a comment that most need re-wrapping.
    /// </remarks>
    static readonly string[] VerbatimElements = ["code", "c"];

    /// <summary>Whether whitespace has been seen since the last word or element.</summary>
    bool _separated = true;

    /// <summary>Whether the last thing emitted at this level was prose rather than markup.</summary>
    bool _afterWord;

    XmlDocModel() { }

    public static bool IsVerbatimElement(string name) =>
        VerbatimElements.Contains(name, StringComparer.Ordinal);

    /// <summary>
    /// The comment's content, or null when it is not something the sub-formatter will touch.
    /// </summary>
    /// <remarks>
    /// ⚠ Null is the safe answer and it is returned generously: a multi-line tag header, a start
    /// tag whose name does not match its end tag, an unrecognised node kind. A doc comment left
    /// exactly as written is never wrong; a doc comment re-wrapped on a guess can be.
    /// </remarks>
    public static ImmutableArray<XmlDocNode>? Build(DocumentationCommentTriviaSyntax comment) {
        var model = new XmlDocModel();
        var builder = ImmutableArray.CreateBuilder<XmlDocNode>();
        return model.Add(builder, comment.Content) ? builder.ToImmutable() : null;
    }

    bool Add(ImmutableArray<XmlDocNode>.Builder builder, SyntaxList<XmlNodeSyntax> content) {
        foreach (var node in content) {
            switch (node) {
                case XmlTextSyntax text:
                    AddText(builder, text);
                    break;

                case XmlElementSyntax element:
                    if (Element(element) is not { } built) {
                        return false;
                    }

                    builder.Add(built);
                    _separated = false;
                    _afterWord = false;
                    break;

                case XmlEmptyElementSyntax empty:
                    if (Header(empty.Name, empty.Attributes) is not { } header) {
                        return false;
                    }

                    builder.Add(
                        new XmlDocElement(
                            empty.Name.ToString(),
                            header,
                            true,
                            [],
                            null,
                            !_separated,
                            !_separated && _afterWord
                        )
                    );

                    _separated = false;
                    _afterWord = false;
                    break;

                case XmlCDataSectionSyntax:
                case XmlProcessingInstructionSyntax:
                case XmlCommentSyntax:
                    builder.Add(new XmlDocVerbatim(SourceLines(node.ToString())));
                    _separated = true;
                    _afterWord = false;
                    break;

                default:
                    return false;
            }
        }

        return true;
    }

    XmlDocElement? Element(XmlElementSyntax element) {
        var name = element.StartTag.Name.ToString();
        if (!string.Equals(name, element.EndTag.Name.ToString(), StringComparison.Ordinal)) {
            return null;
        }

        if (Header(element.StartTag.Name, element.StartTag.Attributes) is not { } header) {
            return null;
        }

        var glued = !_separated;
        var gluedToWord = glued && _afterWord;
        if (IsVerbatimElement(name)) {
            return new XmlDocElement(
                name,
                header,
                false,
                [],
                VerbatimBody(element.Content.ToString()),
                glued,
                gluedToWord
            );
        }

        // ⚠ The child walk owns `_separated` from here: whitespace inside the element decides
        // whether *its* children are glued, and the state must come back describing the element's
        // last child rather than the text before its start tag.
        _separated = true;
        _afterWord = false;
        var children = ImmutableArray.CreateBuilder<XmlDocNode>();
        return Add(children, element.Content)
            ? new XmlDocElement(name, header, false, children.ToImmutable(), null, glued, gluedToWord)
            : null;
    }

    /// <summary>
    /// <c>&lt;param name="x"</c>: the start tag's source text, minus the closing bracket.
    /// </summary>
    /// <remarks>
    /// ⚠ Copied, not rebuilt. A <c>cref</c> is resolved by the compiler and a <c>name</c> is matched
    /// against a parameter; re-emitting either from a parsed model risks changing a string two other
    /// tools read. It also settles six of the family's keys at once — see
    /// <see cref="XmlDocIds.Refused"/> — because a header nobody rewrites has no attribute style,
    /// no attribute indent and no spaces around its '='.
    /// <para>
    /// ⚠ A header that spans lines is refused outright rather than joined: joining it would be the
    /// rewrite this method exists to avoid.
    /// </para>
    /// </remarks>
    static string? Header(XmlNameSyntax name, SyntaxList<XmlAttributeSyntax> attributes) {
        var builder = new StringBuilder("<").Append(name.ToString());
        foreach (var attribute in attributes) {
            var text = attribute.ToFullString();
            if (text.Contains('\n', StringComparison.Ordinal)) {
                return null;
            }

            builder.Append(' ').Append(attribute.ToString());
        }

        var header = builder.ToString();
        return header.Contains('\n', StringComparison.Ordinal) ? null : header;
    }

    /// <summary>
    /// Splits a run of XML text into words and the author's line breaks.
    /// </summary>
    /// <remarks>
    /// ⚠ An entity token joins the word around it rather than becoming one: <c>a&amp;lt;b</c> is a
    /// single word, and breaking it would put a line break in the middle of what renders as one
    /// character. Roslyn hands entities back as separate tokens, so this has to be done
    /// deliberately.
    /// </remarks>
    void AddText(ImmutableArray<XmlDocNode>.Builder builder, XmlTextSyntax text) {
        var word = new StringBuilder();
        var glued = !_separated;
        var newLines = 0;

        void FlushWord() {
            if (word.Length == 0) {
                return;
            }

            builder.Add(new XmlDocWord(word.ToString(), glued));
            word.Clear();
            glued = true;
        }

        void FlushBreaks() {
            if (newLines == 0) {
                return;
            }

            builder.Add(new XmlDocBreak(newLines - 1));
            newLines = 0;
            glued = false;
        }

        foreach (var token in text.TextTokens) {
            if (token.IsKind(SyntaxKind.XmlTextLiteralNewLineToken)) {
                FlushWord();
                glued = false;
                newLines++;
                continue;
            }

            if (token.IsKind(SyntaxKind.XmlEntityLiteralToken)) {
                FlushBreaks();
                word.Append(token.Text);
                continue;
            }

            foreach (var character in token.Text) {
                if (character is ' ' or '\t') {
                    FlushWord();
                    glued = false;
                    continue;
                }

                FlushBreaks();
                word.Append(character);
            }
        }

        FlushWord();
        FlushBreaks();
        _separated = !glued;
        _afterWord = builder.Count > 0 && builder[^1] is XmlDocWord;
    }

    /// <summary>
    /// The lines of a verbatim region, with each continuation line's <c>///</c> marker removed and
    /// nothing else removed.
    /// </summary>
    /// <remarks>
    /// ⚠ Not <c>TrimEnd</c>. A trailing space inside a <c>&lt;code&gt;</c> block is part of the
    /// sample, and SK-DIV-0006 already establishes that a comment's own trailing whitespace is the
    /// author's — it is the one place in Skala's output that carries any.
    /// </remarks>
    public static ImmutableArray<string> SourceLines(string source) {
        var lines = ImmutableArray.CreateBuilder<string>();
        foreach (var raw in source.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n')) {
            lines.Add(StripMarker(raw));
        }

        return lines.ToImmutable();
    }

    /// <summary>
    /// A verbatim region's body: its source lines, minus only the whitespace-only lines its own
    /// tags sat on.
    /// </summary>
    /// <remarks>
    /// ⚠ Deliberately not <c>Trim()</c>. The indentation of the <em>first</em> line of a code
    /// sample is part of the sample, and a signature that trimmed it could not tell a preserved
    /// block from a re-indented one — which is the single thing the verbatim rule exists to
    /// guarantee.
    /// </remarks>
    public static ImmutableArray<string> VerbatimBody(string source) {
        var lines = SourceLines(source);
        var start = 0;
        var end = lines.Length;
        if (start < end && lines[start].Trim().Length == 0) {
            start++;
        }

        if (end > start && lines[end - 1].Trim().Length == 0) {
            end--;
        }

        return [.. lines[start..end]];
    }

    static string StripMarker(string line) {
        var index = 0;
        while (index < line.Length && line[index] is ' ' or '\t') {
            index++;
        }

        return index + 3 <= line.Length && line.AsSpan(index, 3) is "///" ? line[(index + 3)..] : line;
    }
}
