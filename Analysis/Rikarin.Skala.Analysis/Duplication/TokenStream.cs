using Microsoft.CodeAnalysis.CSharp;

namespace Rikarin.Skala.Analysis.Duplication;

/// <summary>
/// One file lexed to the normalised token stream type-2 clone detection compares.
/// </summary>
/// <remarks>
/// docs/plan/09 § "Duplication", step 1: "lex every file to a token stream, dropping trivia, mapping
/// identifiers to a canonical class (<c>ID</c>), keeping keywords and punctuation exact".
/// <para>
/// ⚠ The normalisation is <see cref="Microsoft.CodeAnalysis.SyntaxToken.RawKind"/> and nothing else,
/// because Roslyn's kind <i>is</i> the equivalence class the algorithm wants: every identifier is
/// <c>IdentifierToken</c>, every number is <c>NumericLiteralToken</c>, every string is
/// <c>StringLiteralToken</c> — one class per literal kind — and every keyword and every piece of
/// punctuation is its own kind already. Writing a mapping table on top of that would be a second
/// place for the classes to be wrong.
/// </para>
/// <para>
/// ⚠ Lexer tokens, not parser tokens. Two consequences, both deliberate: contextual keywords
/// (<c>var</c>, <c>async</c>, <c>record</c>, <c>value</c>) are identifiers here and normalise away,
/// which is right for type-2 — <c>var x</c> and <c>Foo x</c> are the same shape; and an interpolated
/// string arrives as one <c>InterpolatedStringToken</c> rather than as its parts, so its content
/// normalises away wholesale. That is the same decision as normalising any other literal, one level
/// coarser.
/// </para>
/// <para>
/// Trivia carries the comments, the whitespace and the disabled <c>#if</c> regions, and all of it is
/// dropped — reformatting a file must not change its clones.
/// </para>
/// </remarks>
internal sealed class TokenStream {
    TokenStream(ushort[] codes, int[] starts, int[] ends) {
        Codes = codes;
        Starts = starts;
        Ends = ends;
    }

    /// <summary>The normalised class of each token. ⚠ <c>SyntaxKind</c> is a <c>ushort</c> enum; this is it.</summary>
    public ushort[] Codes { get; }

    /// <summary>Character offset of each token, trivia excluded.</summary>
    public int[] Starts { get; }

    /// <summary>Character offset just past each token, trivia excluded.</summary>
    public int[] Ends { get; }

    public int Count => Codes.Length;

    /// <summary>Rebuilds a stream from the persisted index. No validation — the index verifies itself.</summary>
    public static TokenStream FromArrays(ushort[] codes, int[] starts, int[] ends) => new(codes, starts, ends);

    /// <summary>
    /// Lexes one file.
    /// </summary>
    /// <remarks>
    /// ⚠ This is the expensive half of the whole feature, which is why the index exists: everything
    /// downstream is integer arithmetic over the arrays this produces.
    /// </remarks>
    public static TokenStream Lex(string text) {
        // A C# token averages a little over three characters including its trivia; over-guessing
        // costs one Array.Resize at the end, under-guessing costs a copy per doubling.
        var capacity = Math.Max(16, text.Length / 3);
        var codes = new ushort[capacity];
        var starts = new int[capacity];
        var ends = new int[capacity];
        var count = 0;

        foreach (var token in SyntaxFactory.ParseTokens(text)) {
            if (token.RawKind == (int)SyntaxKind.EndOfFileToken) {
                continue;
            }

            var span = token.Span;
            if (span.Length == 0) {
                // Zero-width tokens carry no structure and would give an occurrence a boundary that
                // is not in the file.
                continue;
            }

            if (count == codes.Length) {
                var grown = codes.Length * 2;
                Array.Resize(ref codes, grown);
                Array.Resize(ref starts, grown);
                Array.Resize(ref ends, grown);
            }

            codes[count] = (ushort)token.RawKind;
            starts[count] = span.Start;
            ends[count] = span.End;
            count++;
        }

        if (count != codes.Length) {
            Array.Resize(ref codes, count);
            Array.Resize(ref starts, count);
            Array.Resize(ref ends, count);
        }

        return new TokenStream(codes, starts, ends);
    }
}
