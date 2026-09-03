using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Core.Diagnostics;
using System.Xml;

namespace Rikarin.Skala.Formatting.CSharp;

/// <summary>
///     What milestone 3 does about documentation comments, and — measured — why it is not a re-wrap.
/// </summary>
/// <remarks>
///     ⚠ docs/plan/05 § "Phase 4" describes a sub-formatter: parse the doc comment as XML, re-wrap text
///     to <c>skala_xmldoc_max_line_length = 120</c>, break before
///     <c>summary,remarks,example,returns,param,…</c>. That formatter is <em>not</em> implemented, and
///     the reason is evidence rather than schedule: <c>jb cleanupcode</c> does not touch documentation
///     comments at all. Asked directly, with the export's whole <c>resharper_xmldoc_*</c> family in
///     force, it leaves every one of these exactly as written:
///     <code>
/// ///&lt;summary&gt;No space after the marker.&lt;/summary&gt;
/// /// &lt;summary&gt;A summary line 128 columns wide …&lt;/summary&gt;
/// /// &lt;param name="x"&gt;…&lt;/param&gt;&lt;param name="y"&gt;…&lt;/param&gt;
///     </code>
///     So a Skala that re-wrapped them would diverge from the oracle on every doc comment in the corpus
///     — and would have no oracle to check itself against while doing it, which is how a formatter
///     acquires behaviour nobody asked for. SK-DIV-0006 records the finding and the two options it
///     makes inert.
///     <para>
///         What <em>is</em> implemented is the half docs/plan/05 calls a hazard and that needs no oracle: a
///         doc comment that is not well-formed XML is left exactly as it is and reported at
///         <c>hint</c>. Extremely common in real code, invisible to the compiler in a `NoWarn`-ed build, and
///         the thing a re-wrapping formatter would destroy first.
///     </para>
/// </remarks>
public static class XmlDocComments {
    /// <summary>Reports every doc comment in the file that is not well-formed XML.</summary>
    public static void Report(
        string path,
        SourceText text,
        SyntaxNode root,
        ICollection<SkalaDiagnostic> diagnostics
    ) {
        foreach (var trivia in root.DescendantTrivia(descendIntoTrivia: false)) {
            if (!trivia.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia)
                && !trivia.IsKind(SyntaxKind.MultiLineDocumentationCommentTrivia)) {
                continue;
            }

            if (trivia.GetStructure() is not DocumentationCommentTriviaSyntax structure) {
                continue;
            }

            if (WellFormed(structure)) {
                continue;
            }

            diagnostics.Add(
                new SkalaDiagnostic(
                    FormatDiagnosticIds.MalformedXmlDoc,
                    SkalaSeverity.Hidden,
                    "the documentation comment is not well-formed XML; it was left exactly as written",
                    path,
                    text.Lines.GetLinePosition(trivia.SpanStart).Line + 1
                )
            );
        }
    }

    /// <summary>
    ///     Whether the comment's content parses as XML.
    /// </summary>
    /// <remarks>
    ///     ⚠ Wrapped in a synthetic root, because a doc comment is a <em>fragment</em>: two sibling
    ///     <c>&lt;param&gt;</c> elements are perfectly ordinary and are not a well-formed document.
    ///     Judging them by document rules would report most of the corpus.
    ///     <para>
    ///         ⚠ Entities are not resolved and a DTD is refused. The text comes from a source file that may
    ///         have been written by anybody, and an XML parser that fetches what a document tells it to is
    ///         the oldest remote-code-execution in the format.
    ///     </para>
    /// </remarks>
    public static bool WellFormed(DocumentationCommentTriviaSyntax comment) {
        var content = Strip(comment);
        if (content.Trim().Length == 0) {
            return true;
        }

        var settings = new XmlReaderSettings {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            ConformanceLevel = ConformanceLevel.Fragment,
            IgnoreWhitespace = false
        };

        try {
            using var reader = XmlReader.Create(new StringReader("<skala>" + content + "</skala>"), settings);
            while (reader.Read()) {
                // Reading to the end is the check.
            }

            return true;
        } catch (XmlException) {
            return false;
        }
    }

    /// <summary>The comment's text with the <c>///</c> or <c>/** */</c> markers removed.</summary>
    static string Strip(DocumentationCommentTriviaSyntax comment) {
        var builder = new System.Text.StringBuilder();
        foreach (var token in comment.DescendantTokens()) {
            foreach (var trivia in token.LeadingTrivia) {
                if (!trivia.IsKind(SyntaxKind.DocumentationCommentExteriorTrivia)) {
                    builder.Append(trivia.ToFullString());
                }
            }

            builder.Append(token.Text);
            foreach (var trivia in token.TrailingTrivia) {
                if (!trivia.IsKind(SyntaxKind.DocumentationCommentExteriorTrivia)) {
                    builder.Append(trivia.ToFullString());
                }
            }
        }

        return builder.ToString();
    }
}
