using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

namespace Rikarin.Skala.Formatting.CSharp;

/// <summary>What a piece of the source is.</summary>
/// <remarks>
/// docs/plan/04 § "Trivia — where formatters actually break". Roslyn attaches trivia to tokens with
/// rules that are subtle; Skala re-associates it into this explicit model before building the
/// document, because "which token owns this comment" must be a decision Skala makes rather than one
/// it inherits.
/// </remarks>
public enum PieceKind {
    Token,

    /// <summary>A <c>//</c> comment.</summary>
    LineComment,

    /// <summary>A <c>/* */</c> comment. Never reflowed, never reindented past its first line.</summary>
    BlockComment,

    /// <summary>One <c>///</c> line of a documentation comment. Phase 4 reformats the contents; phase 1 reindents the line.</summary>
    DocCommentLine,

    /// <summary>A <c>/** */</c> documentation comment, kept whole.</summary>
    BlockDocComment,

    /// <summary><c>#if</c>, <c>#else</c>, <c>#elif</c>, <c>#endif</c>, <c>#define</c>, <c>#error</c>.</summary>
    ConditionalDirective,

    /// <summary><c>#region</c> / <c>#endregion</c> — indented like code (<c>indent_preprocessor_region = usual_indent</c>).</summary>
    RegionDirective,

    /// <summary><c>#pragma</c>, <c>#nullable</c>, <c>#line</c> — own line, column 0, no grouping effect.</summary>
    OtherDirective,

    /// <summary>⚠ The inactive branch of a <c>#if</c>. An unstructured string, emitted byte-for-byte and never reindented.</summary>
    DisabledText,

    /// <summary>Tokens Roslyn could not place, and merge-conflict markers. Emitted untouched.</summary>
    Skipped
}

/// <summary>One indivisible thing in the source: a token, a comment, a directive, a disabled block.</summary>
public readonly record struct Piece(
    PieceKind Kind,
    TextSpan Span,
    string Text,
    int TokenIndex,
    bool StartsLine) {
    public bool IsComment =>
        Kind is PieceKind.LineComment or PieceKind.BlockComment or PieceKind.DocCommentLine or PieceKind.BlockDocComment;

    public bool IsDirective =>
        Kind is PieceKind.ConditionalDirective or PieceKind.RegionDirective or PieceKind.OtherDirective;
}

/// <summary>
/// Splits a parsed file into an ordered piece stream, which is what the document builder walks.
/// </summary>
public static class SourcePieces {
    public static (Piece[] Pieces, SyntaxToken[] Tokens) Split(SyntaxNode root, SourceText text) {
        var pieces = new List<Piece>(1024);
        var tokens = new List<SyntaxToken>(512);

        foreach (var token in root.DescendantTokens(descendIntoTrivia: false)) {
            foreach (var trivia in token.LeadingTrivia) {
                AddTrivia(pieces, trivia, text);
            }

            // ⚠ Zero-width tokens — the omitted sizes of `int[,]`, missing tokens in a partial
            // tree — are not pieces. Treated as one, the gap rules fire on both sides of nothing
            // and `int[,]` comes out as `int[, ]`.
            if (!token.IsKind(SyntaxKind.EndOfFileToken) && token.Span.Length > 0) {
                pieces.Add(
                    new Piece(PieceKind.Token, token.Span, token.Text, tokens.Count, StartsLine(text, token.SpanStart))
                );
                tokens.Add(token);
            }

            foreach (var trivia in token.TrailingTrivia) {
                AddTrivia(pieces, trivia, text);
            }
        }

        return ([.. pieces], [.. tokens]);
    }

    static void AddTrivia(List<Piece> pieces, SyntaxTrivia trivia, SourceText text) {
        switch (trivia.Kind()) {
            case SyntaxKind.WhitespaceTrivia:
            case SyntaxKind.EndOfLineTrivia:
                return;

            case SyntaxKind.SingleLineCommentTrivia:
                pieces.Add(Make(PieceKind.LineComment, trivia, text));
                return;

            case SyntaxKind.MultiLineCommentTrivia:
                pieces.Add(Make(PieceKind.BlockComment, trivia, text));
                return;

            case SyntaxKind.MultiLineDocumentationCommentTrivia:
                pieces.Add(Make(PieceKind.BlockDocComment, trivia, text));
                return;

            case SyntaxKind.SingleLineDocumentationCommentTrivia:
                // ⚠ Split per line. The trivia spans every consecutive `///` line including the
                // whitespace between them; kept whole, a doc comment would carry its old indentation
                // with it when the member it documents moves.
                AddDocumentationLines(pieces, trivia, text);
                return;

            case SyntaxKind.DisabledTextTrivia:
                pieces.Add(Make(PieceKind.DisabledText, trivia, text));
                return;

            case SyntaxKind.RegionDirectiveTrivia:
            case SyntaxKind.EndRegionDirectiveTrivia:
                pieces.Add(Make(PieceKind.RegionDirective, trivia, text));
                return;

            case SyntaxKind.IfDirectiveTrivia:
            case SyntaxKind.ElifDirectiveTrivia:
            case SyntaxKind.ElseDirectiveTrivia:
            case SyntaxKind.EndIfDirectiveTrivia:
            case SyntaxKind.DefineDirectiveTrivia:
            case SyntaxKind.UndefDirectiveTrivia:
            case SyntaxKind.ErrorDirectiveTrivia:
            case SyntaxKind.WarningDirectiveTrivia:
                pieces.Add(Make(PieceKind.ConditionalDirective, trivia, text));
                return;

            case SyntaxKind.SkippedTokensTrivia:
            case SyntaxKind.ConflictMarkerTrivia:
            case SyntaxKind.BadDirectiveTrivia:
                pieces.Add(Make(PieceKind.Skipped, trivia, text));
                return;

            default:
                if (trivia.IsDirective) {
                    pieces.Add(Make(PieceKind.OtherDirective, trivia, text));
                    return;
                }

                // Documentation-comment exterior trivia and anything else Roslyn grows later: keep
                // it, untouched, rather than dropping it.
                if (trivia.Span.Length > 0) {
                    pieces.Add(Make(PieceKind.Skipped, trivia, text));
                }

                return;
        }
    }

    static void AddDocumentationLines(List<Piece> pieces, SyntaxTrivia trivia, SourceText text) {
        var span = trivia.FullSpan;
        var start = span.Start;
        var content = text.ToString(span);
        var lineStart = 0;

        for (var i = 0; i <= content.Length; i++) {
            var atEnd = i == content.Length;
            if (!atEnd && content[i] != '\n') {
                continue;
            }

            var lineEnd = i;
            while (lineEnd > lineStart && (content[lineEnd - 1] == '\r' || content[lineEnd - 1] == '\n')) {
                lineEnd--;
            }

            var trimmedStart = lineStart;
            while (trimmedStart < lineEnd && (content[trimmedStart] == ' ' || content[trimmedStart] == '\t')) {
                trimmedStart++;
            }

            var trimmedEnd = lineEnd;
            while (trimmedEnd > trimmedStart && (content[trimmedEnd - 1] == ' ' || content[trimmedEnd - 1] == '\t')) {
                trimmedEnd--;
            }

            if (trimmedEnd > trimmedStart) {
                pieces.Add(
                    new Piece(
                        PieceKind.DocCommentLine,
                        TextSpan.FromBounds(start + trimmedStart, start + trimmedEnd),
                        content[trimmedStart..trimmedEnd],
                        -1,
                        StartsLine(text, start + trimmedStart)
                    )
                );
            }

            lineStart = i + 1;
        }
    }

    static Piece Make(PieceKind kind, SyntaxTrivia trivia, SourceText text) =>
        new(kind, trivia.Span, text.ToString(trivia.Span), -1, StartsLine(text, trivia.SpanStart));

    /// <summary>True when only whitespace separates <paramref name="position"/> from the line start.</summary>
    static bool StartsLine(SourceText text, int position) {
        for (var i = position - 1; i >= 0; i--) {
            var c = text[i];
            if (c == '\n') {
                return true;
            }

            if (c != ' ' && c != '\t' && c != '\r') {
                return false;
            }
        }

        return true;
    }
}
