using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Buffers.Binary;
using System.IO.Hashing;

namespace Rikarin.Skala.Analysis.Duplication;

/// <summary>
///     One file lexed to the normalised token stream type-2 clone detection compares.
/// </summary>
/// <remarks>
///     docs/plan/09 § "Duplication", step 1: "lex every file to a token stream, dropping trivia, mapping
///     identifiers to a canonical class (<c>ID</c>), keeping keywords and punctuation exact".
///     <para>
///         ⚠ The normalisation is <see cref="Microsoft.CodeAnalysis.SyntaxToken.RawKind" /> and nothing else,
///         because Roslyn's kind <i>is</i> the equivalence class the algorithm wants: every identifier is
///         <c>IdentifierToken</c>, every number is <c>NumericLiteralToken</c>, every string is
///         <c>StringLiteralToken</c> — one class per literal kind — and every keyword and every piece of
///         punctuation is its own kind already. Writing a mapping table on top of that would be a second
///         place for the classes to be wrong.
///     </para>
///     <para>
///         ⚠ Lexer tokens, not parser tokens. Two consequences, both deliberate: contextual keywords
///         (<c>var</c>, <c>async</c>, <c>record</c>, <c>value</c>) are identifiers here and normalise away,
///         which is right for type-2 — <c>var x</c> and <c>Foo x</c> are the same shape; and an interpolated
///         string arrives as one <c>InterpolatedStringToken</c> rather than as its parts, so its content
///         normalises away wholesale. That is the same decision as normalising any other literal, one level
///         coarser.
///     </para>
///     <para>
///         Trivia carries the comments, the whitespace and the disabled <c>#if</c> regions, and all of it is
///         dropped — reformatting a file must not change its clones.
///     </para>
/// </remarks>
internal sealed class TokenStream {
    /// <summary>
    ///     The source the tokeniser's own fingerprint is taken over.
    /// </summary>
    /// <remarks>
    ///     ⚠ This is not a test fixture; it is the cache key. Every construct this file contains is here
    ///     because a change to how <see cref="Lex" /> treats it must invalidate <c>clones.idx</c>: the
    ///     header run in both namespace forms, a <c>using</c> statement and a <c>using</c> declaration that
    ///     must keep counting, one literal of every class, an interpolated string, a contextual keyword and
    ///     a preprocessor region. Adding a construct here is cheap and strictly safer than leaving it out —
    ///     the cost of a missing one is a silently stale index, which is the defect this exists to close.
    /// </remarks>
    internal const string Canary =
        """
        extern alias Legacy;
        global using System;
        using System.Text;
        using static System.Math;
        using Alias = System.Collections.Generic.List<int>;

        namespace Fingerprint.Canary;

        using System.Linq;

        internal sealed class Probe {
            const string Verbatim = @"a""b";
            const int Number = 0x2A;

            public async Task<int> RunAsync(string name) {
                var total = 0;
                using var handle = File.OpenRead(name);
                using (var other = File.OpenRead(name)) {
                    total += other.ReadByte();
                }

        #if DEBUG
                total -= 1;
        #endif

                return total + $"{name} {Number:X}".Length + Verbatim.Length;
            }
        }
        """;

    /// <summary>
    ///     A hash of what this build's lexer does, for the <c>clones.idx</c> stamp — <b>issue #322</b>.
    /// </summary>
    /// <remarks>
    ///     ⚠ The index is keyed on <c>(path, content hash)</c> and stamped with the format version and the
    ///     tool version, and <b>a change to this file moves none of them</b>. Editing the normalisation, the
    ///     header skip or the token filter therefore used to leave every file served its <i>old</i> token
    ///     stream, so the run reported the previous tokeniser's answer with no warning of any kind: measured
    ///     at 12.89 % warm against 6.9 % cold, same binary, same tree, identical finding sets.
    ///     <para>
    ///         ⚠ This is derived, not declared. A constant somebody has to remember to bump is the same
    ///         weakness <c>FormatVersion</c> already has, and the failure mode is invisible. Lexing
    ///         <see cref="Canary" /> and hashing the arrays that come out means the stamp moves whenever the
    ///         lexer's output moves, for free, with nothing to remember — the price being that a change the
    ///         canary does not exercise still slips through, which is why the canary is broad and why adding
    ///         to it is the correct reflex.
    ///     </para>
    ///     <para>
    ///         ⚠ The window length (<c>minTokens</c>) is deliberately <i>not</i> in here. The index stores
    ///         token streams, not window hashes — the windows are re-derived every run — so a different
    ///         <c>minTokens</c> does not make a cached stream wrong.
    ///     </para>
    /// </remarks>
    public static string Fingerprint { get; } = ComputeFingerprint();

    TokenStream(ushort[] codes, int[] starts, int[] ends, int headerLines) {
        Codes = codes;
        Starts = starts;
        Ends = ends;
        HeaderLines = headerLines;
    }

    /// <summary>The normalised class of each token. ⚠ <c>SyntaxKind</c> is a <c>ushort</c> enum; this is it.</summary>
    public ushort[] Codes { get; }

    /// <summary>Character offset of each token, trivia excluded.</summary>
    public int[] Starts { get; }

    /// <summary>Character offset just past each token, trivia excluded.</summary>
    public int[] Ends { get; }

    /// <summary>
    ///     How many of the file's lines held only header tokens and are therefore out of the denominator.
    /// </summary>
    /// <remarks>
    ///     ⚠ Out of the numerator <i>and</i> the denominator, which is the same call
    ///     <c>DuplicationPass</c> makes for generated files. A line whose every token was skipped can never
    ///     be reported as duplicated, so leaving it in <c>TotalLines</c> would mean a file's duplication
    ///     percentage falls when somebody adds an import — a second artefact replacing the one being
    ///     removed.
    ///     <para>
    ///         A line holding no tokens at all — blank, or comment-only — is neither, and stays counted
    ///         exactly as it always was.
    ///     </para>
    /// </remarks>
    public int HeaderLines { get; }

    public int Count => Codes.Length;

    /// <summary>Rebuilds a stream from the persisted index. No validation — the index verifies itself.</summary>
    public static TokenStream FromArrays(ushort[] codes, int[] starts, int[] ends, int headerLines) =>
        new(codes, starts, ends, headerLines);

    /// <summary>
    ///     Lexes one file, minus its header — <b>issue #323</b>.
    /// </summary>
    /// <remarks>
    ///     ⚠ This is the expensive half of the whole feature, which is why the index exists: everything
    ///     downstream is integer arithmetic over the arrays this produces.
    ///     <para>
    ///         ⚠ <b>The file header is skipped, and it is not duplication.</b> The normalisation above maps
    ///         every identifier to <c>IdentifierToken</c>, so <c>using Microsoft.CodeAnalysis.CSharp.Syntax;</c>
    ///         and <c>using Rikarin.Skala.Rules.Metadata;</c> are the <i>same</i> nine-token sequence: files
    ///         match on the number of dotted segments in the same order and on nothing else. Measured over
    ///         Skala's 289 analyzer files the header is a median 62 tokens — 62 % of the 100-token detection
    ///         window — so two files were a clone before either had done anything. Six of the repository's
    ///         12.9 duplication points were that artefact, and no amount of extraction could remove them.
    ///         <c>DuplicationPass</c> already makes this exact call for generated files, and a hand-written
    ///         preamble is the same phenomenon: moving it into a source generator would drop the number
    ///         sharply without changing one line of logic.
    ///     </para>
    ///     <para>
    ///         ⚠ <b>The boundary is a node type, never a token's text.</b> Only
    ///         <see cref="UsingDirectiveSyntax" /> and <see cref="ExternAliasDirectiveSyntax" /> whose parent
    ///         is a compilation unit or a namespace are skipped, plus each namespace declaration's own
    ///         <c>namespace</c> keyword and name. A <c>using</c> <i>statement</i> — <c>using var x = …</c>
    ///         (a <c>LocalDeclarationStatementSyntax</c>) or <c>using (…) { }</c> (a
    ///         <c>UsingStatementSyntax</c>) — is neither of those node types, is real code, and keeps
    ///         counting in full. Matching on the word <c>using</c> instead would have blinded the detector to
    ///         duplication in resource-management code, which is the kind of defect nobody notices for
    ///         months.
    ///     </para>
    ///     <para>
    ///         ⚠ The two namespace forms differ, deliberately. A file-scoped declaration is skipped through
    ///         its semicolon, because <c>namespace ID . ID . ID ;</c> is the same artefact as a
    ///         <c>using</c>. A block declaration is skipped only to the end of its <b>name</b>: its
    ///         <c>{</c>, its members and its closing <c>}</c> are all still tokenised, so the brace nesting
    ///         a block-scoped file actually has is still what it is compared on.
    ///     </para>
    ///     <para>
    ///         The skip is not restricted to a leading run. A <c>using</c> directive inside a namespace
    ///         body, or a second namespace half way down the file, is the same normalisation artefact
    ///         wherever it sits. Removing it leaves a gap that a clone may span, exactly as the dropped
    ///         trivia around a comment already does.
    ///     </para>
    /// </remarks>
    public static TokenStream Lex(string text) {
        ArgumentNullException.ThrowIfNull(text);

        var skipped = HeaderSpans(text);
        var lineStarts = LineStarts(text);

        // 1 = a header token sits on this line, 2 = a counted one does. A line that ends up 1 and not
        // 2 leaves both halves of the ratio; anything else is untouched.
        var lines = new byte[lineStarts.Count];

        // A C# token averages a little over three characters including its trivia; over-guessing
        // costs one Array.Resize at the end, under-guessing costs a copy per doubling.
        var capacity = Math.Max(16, text.Length / 3);
        var codes = new ushort[capacity];
        var starts = new int[capacity];
        var ends = new int[capacity];
        var count = 0;
        var next = 0;

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

            // ⚠ One forward pointer, not a search: `ParseTokens` yields in source order and the spans
            // are sorted, so the whole skip costs one pass over a list that is usually a dozen long.
            while (next < skipped.Count && skipped[next].End <= span.Start) {
                next++;
            }

            var header = next < skipped.Count && skipped[next].Start <= span.Start;
            Mark(lines, lineStarts, span, header ? (byte)1 : (byte)2);
            if (header) {
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

        var headerLines = 0;
        foreach (var state in lines) {
            if (state == 1) {
                headerLines++;
            }
        }

        return new(codes, starts, ends, headerLines);
    }

    /// <summary>Flags every line <paramref name="span" /> touches with <paramref name="state" />.</summary>
    static void Mark(byte[] lines, List<int> lineStarts, TextSpan span, byte state) {
        var first = LineOf(lineStarts, span.Start);
        var last = LineOf(lineStarts, span.End - 1);
        for (var line = first; line <= last && line < lines.Length; line++) {
            lines[line] |= state;
        }
    }

    static int LineOf(List<int> lineStarts, int offset) {
        var found = lineStarts.BinarySearch(offset);
        return found >= 0 ? found : ~found - 1;
    }

    /// <summary>
    ///     The offset each line starts at.
    /// </summary>
    /// <remarks>
    ///     ⚠ <c>\r\n</c>, <c>\n</c> and a lone <c>\r</c> all end a line, which is what
    ///     <see cref="SourceText" /> counts — and <see cref="CloneDetector" /> takes the denominator from
    ///     <c>SourceText.Lines.Count</c>, so a disagreement here would subtract lines from a total that
    ///     never had them.
    /// </remarks>
    static List<int> LineStarts(string text) {
        var starts = new List<int> { 0 };
        for (var i = 0; i < text.Length; i++) {
            var c = text[i];
            if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n') {
                i++;
            } else if (c is not ('\n' or '\r' or '\u0085' or '\u2028' or '\u2029')) {
                continue;
            }

            starts.Add(i + 1);
        }

        return starts;
    }

    /// <summary>
    ///     The spans the header skip removes, in source order and never overlapping.
    /// </summary>
    /// <remarks>
    ///     ⚠ A second parse, and it buys the node types. <see cref="SyntaxFactory.ParseTokens" /> alone
    ///     cannot tell a <c>using</c> directive from a <c>using</c> statement, and a rule that guessed from
    ///     the token stream would be wrong in exactly the case that matters. Namespaces nest only inside
    ///     namespaces and compilation units, so this recursion visits the declaration spine and never
    ///     descends into a type body.
    /// </remarks>
    static List<TextSpan> HeaderSpans(string text) {
        var spans = new List<TextSpan>();
        Collect(SyntaxFactory.ParseCompilationUnit(text), spans);
        return spans;
    }

    static void Collect(SyntaxNode scope, List<TextSpan> spans) {
        var (externs, usings, members) = scope switch {
            CompilationUnitSyntax unit => (unit.Externs, unit.Usings, unit.Members),
            BaseNamespaceDeclarationSyntax declaration => (declaration.Externs, declaration.Usings,
                declaration.Members),
            _ => default
        };

        foreach (var directive in externs) {
            spans.Add(directive.Span);
        }

        foreach (var directive in usings) {
            spans.Add(directive.Span);
        }

        foreach (var member in members) {
            if (member is not BaseNamespaceDeclarationSyntax declaration) {
                continue;
            }

            // ⚠ File-scoped through the `;`; block only to the end of the name, so the `{` survives.
            var end = declaration is FileScopedNamespaceDeclarationSyntax scoped
                ? scoped.SemicolonToken.Span.End
                : declaration.Name.Span.End;

            spans.Add(TextSpan.FromBounds(declaration.NamespaceKeyword.SpanStart, end));
            Collect(declaration, spans);
        }
    }

    static string ComputeFingerprint() {
        var stream = Lex(Canary);
        var bytes = new byte[(stream.Count * 10) + 8];
        var at = 0;
        for (var i = 0; i < stream.Count; i++) {
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(at), stream.Codes[i]);
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(at + 2), stream.Starts[i]);
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(at + 6), stream.Ends[i]);
            at += 10;
        }

        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(at), stream.Count);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(at + 4), stream.HeaderLines);
        return Convert.ToHexStringLower(XxHash128.Hash(bytes));
    }
}
