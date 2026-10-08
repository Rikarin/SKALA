using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Text;

namespace Rikarin.Skala.Formatting.CSharp;

public sealed partial class CSharpDocumentBuilder {
    /// <summary>
    ///     An interpolated string written on one line, with every interpolation hole respaced by the
    ///     rules that govern the same code outside a string; null when the string must stay verbatim.
    /// </summary>
    /// <remarks>
    ///     ⚠ #492, SK-DIV-0311. The oracle formats the inside of a hole as ordinary code — <c>{ f }</c> is
    ///     <c>{f}</c>, <c>{f+1}</c> is <c>{f + 1}</c>, <c>{M(f,f)}</c> is <c>{M(f, f)}</c>, and every key
    ///     that moves the same tokens outside a string moves them here — while Skala wrote the whole string
    ///     verbatim, because a moved space in the string's <em>text</em> changes its value. Only the gaps
    ///     between a hole's own tokens are rewritten here; the literal text, the format string after a
    ///     <c>:</c> and every delimiter are copied byte for byte. Measured 2026-10-08 with <c>Testing ask</c>
    ///     at the export and with the binary, assignment, parenthesis and comma keys flipped:
    ///     <list type="bullet">
    ///         <item>
    ///             The hole's own <c>{</c> and <c>}</c> take no space, the alignment comma none on either
    ///             side and the format colon none before it, at every value of every key tried:
    ///             <c>{ f , -5 :N2}</c> is <c>{f,-5:N2}</c> with <c>space_after_comma = true</c>.
    ///         </item>
    ///         <item>
    ///             A comment in a hole is a comment in an expression (SK-DIV-0174): the trailing-comment key
    ///             in front of it, also after the hole's <c>{</c> (<c>{/*f*/f}</c> is <c>{ /*f*/f}</c>), and
    ///             after it the next token's rule, except that the hole's <c>{</c> owns the gap to the first
    ///             token (<c>{ /*f*/ f}</c> is <c>{ /*f*/f}</c>) and the hole's <c>}</c> keeps the author's bit
    ///             (<c>/*f*/ }</c> and <c>/*f*/}</c> both stay).
    ///         </item>
    ///         <item>
    ///             ⚠ The oracle never breaks inside a hole: a hole past the margin moves the whole string
    ///             to a continuation line and leaves it long. That is why this answer is a single verbatim
    ///             piece with no break points in it rather than a walk of the hole's nodes.
    ///         </item>
    ///     </list>
    ///     ⚠ A gap holding a line break is copied as written — the oracle keeps a hole's breaks and the indentation
    ///     after them — and a multi-line raw literal's text shifts afterwards, by <c>RawLiteralPlan</c> (#447).
    ///     Declined, and left verbatim as before: a hole holding a line comment or a directive outside a broken gap, a
    ///     formatter tag, and <c>disable_space_changes</c>.
    /// </remarks>
    string? RespacedInterpolatedString(SyntaxNode node) {
        if (node is not InterpolatedStringExpressionSyntax || options.DisableSpaceChanges) {
            return null;
        }

        var span = node.Span;
        var output = new StringBuilder(span.Length + 8);
        SyntaxToken previous = default;
        foreach (var token in node.DescendantTokens(descendIntoTrivia: false)) {
            if (previous.RawKind != 0 && !AppendGap(output, previous, token)) {
                return null;
            }

            output.Append(source, token.SpanStart, token.Span.Length);
            previous = token;
        }

        return output.ToString();
    }

    bool AppendGap(StringBuilder output, SyntaxToken previous, SyntaxToken next) {
        var start = previous.Span.End;
        var end = next.SpanStart;

        // ⚠ A gap that does not lie inside a hole is the string's own text and is copied as it is; in
        // practice there is none, because text tokens touch their braces. An empty gap inside a hole is
        // still asked: `{f+1}` is a gap of nothing that the binary operator's key fills.
        // ⚠ So is a gap the author broke: the oracle keeps a hole's line breaks and the indentation after
        // them, and respaces only what lies on one line (`deltaValue )}` at the within key's `true`).
        if (!InsideAHole(previous, next) || source.AsSpan(start, end - start).IndexOfAny('\r', '\n') >= 0) {
            output.Append(source, start, end - start);
            return true;
        }

        var comments = new List<SyntaxTrivia>();
        foreach (var trivia in previous.TrailingTrivia.Concat(next.LeadingTrivia)) {
            switch (trivia.Kind()) {
                case SyntaxKind.WhitespaceTrivia:
                    continue;
                case SyntaxKind.MultiLineCommentTrivia:
                case SyntaxKind.MultiLineDocumentationCommentTrivia:
                    var text = trivia.ToFullString();
                    if (FormatterTagGuard.IsOffTag(text, options.Tags)
                        || FormatterTagGuard.IsOnTag(text, options.Tags)) {
                        return false;
                    }

                    comments.Add(trivia);
                    continue;
                default:
                    return false;
            }
        }

        if (comments.Count == 0) {
            output.Append(Render(TokenGap(previous, next), start, end));
            return true;
        }

        var at = start;
        for (var i = 0; i < comments.Count; i++) {
            var commentSpan = SourcePieces.TextOf(comments[i]);
            var before = i > 0
                ? options.SpaceBeforeTrailingComment
                : IsHoleBrace(previous, SyntaxKind.OpenBraceToken)
                    ? options.SpaceBeforeTrailingComment
                    : SpaceRules.BeforeBlockComment(previous, options) ?? options.SpaceBeforeTrailingComment;
            output.Append(before ? " " : string.Empty);
            output.Append(source, commentSpan.Start, commentSpan.Length);
            at = commentSpan.End;
        }

        output.Append(Render(AfterACommentInAHole(previous, next), at, end));
        return true;
    }

    SpaceKind TokenGap(SyntaxToken previous, SyntaxToken next) =>
        IsHoleBrace(previous, SyntaxKind.OpenBraceToken)
        || IsHoleBrace(next, SyntaxKind.CloseBraceToken)
        || IsAlignmentComma(previous)
        || IsAlignmentComma(next)
        || IsFormatColon(next)
            ? SpaceKind.Forbidden
            : SpaceRules.Decide(previous, next, options);

    SpaceKind AfterACommentInAHole(SyntaxToken previous, SyntaxToken next) {
        if (IsHoleBrace(next, SyntaxKind.CloseBraceToken)) {
            return SpaceKind.Preserve;
        }

        if (IsHoleBrace(previous, SyntaxKind.OpenBraceToken) || IsAlignmentComma(next) || IsFormatColon(next)) {
            return SpaceKind.Forbidden;
        }

        return SpaceRules.AfterBlockComment(previous, next, options);
    }

    string Render(SpaceKind kind, int start, int end) =>
        kind switch {
            SpaceKind.Required => " ",
            SpaceKind.Forbidden => string.Empty,
            _ => source.AsSpan(start, end - start).IndexOfAny(' ', '\t') >= 0 ? " " : string.Empty
        };

    /// <summary>True when the gap between the two tokens is inside an interpolation hole.</summary>
    static bool InsideAHole(SyntaxToken previous, SyntaxToken next) =>
        !IsStringText(previous)
        && !IsStringText(next)
        && !IsHoleBrace(previous, SyntaxKind.CloseBraceToken)
        && !IsHoleBrace(next, SyntaxKind.OpenBraceToken);

    static bool IsStringText(SyntaxToken token) =>
        token.Kind()
            is SyntaxKind.InterpolatedStringStartToken
            or SyntaxKind.InterpolatedVerbatimStringStartToken
            or SyntaxKind.InterpolatedSingleLineRawStringStartToken
            or SyntaxKind.InterpolatedMultiLineRawStringStartToken
            or SyntaxKind.InterpolatedStringEndToken
            or SyntaxKind.InterpolatedRawStringEndToken
            or SyntaxKind.InterpolatedStringTextToken;

    static bool IsHoleBrace(SyntaxToken token, SyntaxKind kind) =>
        token.IsKind(kind) && token.Parent is InterpolationSyntax;

    static bool IsAlignmentComma(SyntaxToken token) =>
        token.IsKind(SyntaxKind.CommaToken) && token.Parent is InterpolationAlignmentClauseSyntax;

    static bool IsFormatColon(SyntaxToken token) =>
        token.IsKind(SyntaxKind.ColonToken) && token.Parent is InterpolationFormatClauseSyntax;
}
