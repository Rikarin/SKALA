using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Rikarin.Skala.Rules.Maintainability;

/// <summary>
///     The elements of a declaration's documentation comment, read from the parsed trivia, and the span
///     that deletes one of them.
/// </summary>
/// <remarks>
///     ⚠ <b>Structural, never textual.</b> <c>SK7100</c> searches its comment for the characters
///     <c>&lt;inheritdoc</c>, which is right for the question it asks and wrong for these two: a
///     <c>&lt;returns&gt;</c> inside a <c>&lt;code&gt;</c> sample, or the word in a <c>&lt;c&gt;</c>, is
///     prose about the tag, not the tag. Only a tree parsed with <c>DocumentationMode.Parse</c> or higher
///     has a structure to read — under <c>None</c> a <c>///</c> line is an ordinary comment and both rules
///     go silent, which is #388's territory rather than theirs.
/// </remarks>
internal static class DocumentationElements {
    /// <summary>The structured documentation comments leading <paramref name="declaration" />.</summary>
    public static IEnumerable<DocumentationCommentTriviaSyntax> CommentsOf(SyntaxNode declaration) {
        foreach (var trivia in declaration.GetLeadingTrivia()) {
            if ((trivia.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia)
                    || trivia.IsKind(SyntaxKind.MultiLineDocumentationCommentTrivia))
                && trivia.GetStructure() is DocumentationCommentTriviaSyntax structure) {
                yield return structure;
            }
        }
    }

    /// <summary>The top-level elements named <paramref name="name" />, both spellings.</summary>
    public static IEnumerable<XmlNodeSyntax> TopLevel(SyntaxNode declaration, string name) =>
        CommentsOf(declaration).SelectMany(comment => comment.Content).Where(node => NameOf(node) == name);

    /// <summary>Every element named <paramref name="name" /> at any depth, both spellings.</summary>
    public static IEnumerable<XmlNodeSyntax> Anywhere(SyntaxNode declaration, string name) =>
        CommentsOf(declaration)
            .SelectMany(comment => comment.DescendantNodes())
            .OfType<XmlNodeSyntax>()
            .Where(node => NameOf(node) == name);

    /// <summary>An element's local name, ordinal and case-sensitive the way the XML is.</summary>
    public static string? NameOf(XmlNodeSyntax node) =>
        node switch {
            XmlElementSyntax element => element.StartTag.Name.LocalName.ValueText,
            XmlEmptyElementSyntax empty => empty.Name.LocalName.ValueText,
            _ => null
        };

    /// <summary>The element's attributes, whichever spelling it is written in.</summary>
    public static SyntaxList<XmlAttributeSyntax> AttributesOf(XmlNodeSyntax node) =>
        node switch {
            XmlElementSyntax element => element.StartTag.Attributes,
            XmlEmptyElementSyntax empty => empty.Attributes,
            _ => default
        };

    /// <summary>
    ///     What to delete so that <paramref name="element" /> is gone and leaves no empty <c>///</c> behind.
    /// </summary>
    /// <remarks>
    ///     ⚠ <b>The whole line, or only the element — never something in between.</b> When the element's
    ///     first line holds nothing before it but indentation and the comment marker, and its last line
    ///     holds nothing after it, the lines go with their line break; otherwise only the element's own
    ///     span does, and a sentence sharing the line with it survives. A bare <c>///</c> left behind is
    ///     not something the formatter is allowed to remove, so deleting the element alone would leave a
    ///     blank documentation line on every fix.
    ///     <para>
    ///         ⚠ Nothing but documentation can be on the deleted lines: the element sits inside one
    ///         documentation-comment trivia, and that trivia runs to the end of its last line. So no
    ///         ordinary comment or directive is lost by taking the line — the guard <c>SK1015</c> and its
    ///         siblings needed (#325) has no second edit to cover here.
    ///     </para>
    /// </remarks>
    public static TextSpan DeletionSpan(SourceText text, XmlNodeSyntax element) {
        var span = element.Span;
        var first = text.Lines.GetLineFromPosition(span.Start);
        var last = text.Lines.GetLineFromPosition(span.End);

        var before = text.ToString(TextSpan.FromBounds(first.Start, span.Start)).Trim();
        var after = text.ToString(TextSpan.FromBounds(span.End, last.End)).Trim();
        var markerOnly = before.Length == 0
            || string.Equals(before, "///", StringComparison.Ordinal)
            || string.Equals(before, "*", StringComparison.Ordinal);

        return markerOnly && after.Length == 0 ? TextSpan.FromBounds(first.Start, last.EndIncludingLineBreak) : span;
    }
}
