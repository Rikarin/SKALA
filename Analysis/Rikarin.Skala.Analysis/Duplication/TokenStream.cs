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
    /// <summary>A line-state flag: the header skip dropped a token on this line.</summary>
    const byte HeaderToken = 1;

    /// <summary>A line-state flag: a token on this line reached the stream, so the line is not header.</summary>
    const byte CountedToken = 2;

    /// <summary>
    ///     ⚠ Three, because two identical siblings is a copy-paste and three is a table.
    /// </summary>
    /// <remarks>
    ///     A block pasted into the slot beside itself is exactly the finding <c>SK7020</c> is for, and it
    ///     is a pair. The shape with nothing to extract is the one that goes on: rows of a list, all the
    ///     same, differing only in the names the normalisation has already erased.
    /// </remarks>
    const int MinimumRunElements = 3;

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

            // ⚠ A uniform sibling run of four elements, so a change to `UniformRuns` moves the stamp.
            static readonly Probe[] Table = [new Probe(), new Probe(), new Probe(), new Probe()];

            public async Task<int> RunAsync(string name) {
                var total = 0;
                using var handle = File.OpenRead(name);
                using (var other = File.OpenRead(name)) {
                    total += other.ReadByte();
                }

        #if DEBUG
                total -= 1;
        #endif

                return total + $"{name} {Number:X}".Length + Verbatim.Length + Table.Length;
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

    TokenStream(ushort[] codes, int[] starts, int[] ends, int headerLines, int[] runs) {
        Codes = codes;
        Starts = starts;
        Ends = ends;
        HeaderLines = headerLines;
        Runs = runs;
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

    /// <summary>
    ///     The file's uniform sibling runs, as flat triples <c>(first, past, stride)</c> of token indices
    ///     — <b>issue #333</b>.
    /// </summary>
    /// <remarks>
    ///     ⚠
    ///     <b>
    ///         A list of similar rows matches itself, shifted, and that is the normalisation and not
    ///         duplication.
    ///     </b> Every identifier is one class here, so
    ///     <c>new FieldBackedPropertyAnalyzer(), new SearchValuesAnalyzer(),</c> and the two rows under it
    ///     are the <i>same</i> five-token sequence repeated: a 290-element analyzer list is 1 450 tokens
    ///     with a period of 5, so its first hundred tokens are a verified token-for-token clone of its
    ///     second hundred, and of its third. There is nothing to extract — the "duplication" is the list
    ///     being a list. This is the same artefact <b>#323</b> removed for file headers, surviving wherever
    ///     a file holds a run of similar declarations.
    ///     <para>
    ///         ⚠ <b>The test is structural, never lexical.</b> A run is <see cref="Uniform" />: three or
    ///         more <i>consecutive elements of one syntactic list</i> whose normalised token sequences are
    ///         identical and whose token stride is constant. Both halves earn their place. Reading
    ///         periodicity out of the token array alone would decline a genuinely repeated block that
    ///         happens to be periodic, and "elements of one list" alone would decline two duplicated method
    ///         bodies sitting in one class's member list — which is real duplication and the exact thing
    ///         the rule is for.
    ///     </para>
    ///     <para>
    ///         ⚠ <b>Three elements, not two.</b> Two identical siblings are a copy-paste of one element
    ///         into the next slot, which is a finding; three or more is a table. The floor is where the
    ///         artefact starts and not where it is convenient.
    ///     </para>
    ///     <para>
    ///         ⚠ The stride is what <see cref="CloneDetector" /> compares an occurrence's length against,
    ///         so a match that fits <i>inside</i> one element is still reported: three identical
    ///         200-token siblings are three copies of a block and are exactly what <c>SK7020</c> exists to
    ///         find. Recording the period rather than a token count is what keeps that case alive without
    ///         a threshold anybody has to pick.
    ///     </para>
    /// </remarks>
    public int[] Runs { get; }

    public int Count => Codes.Length;

    /// <summary>Rebuilds a stream from the persisted index. No validation — the index verifies itself.</summary>
    public static TokenStream FromArrays(ushort[] codes, int[] starts, int[] ends, int headerLines, int[] runs) =>
        new(codes, starts, ends, headerLines, runs);

    /// <summary>
    ///     The uniform run covering tokens <c>[first, past)</c>, or <c>-1</c> where no one run covers them.
    /// </summary>
    /// <remarks>
    ///     ⚠ Covering, not overlapping. A window straddling the edge of a table has half its tokens in
    ///     ordinary code, and that half is evidence the run's interior is not.
    /// </remarks>
    public int RunCovering(int first, int past) {
        var low = 0;
        var high = Runs.Length / 3 - 1;
        while (low <= high) {
            var middle = low + (high - low) / 2;
            if (Runs[middle * 3 + 1] <= first) {
                low = middle + 1;
            } else if (Runs[middle * 3] > first) {
                high = middle - 1;
            } else {
                return past <= Runs[middle * 3 + 1] ? middle : -1;
            }
        }

        return -1;
    }

    /// <summary>The token period of run <paramref name="run" />: one element and its separator.</summary>
    public int StrideOf(int run) => Runs[run * 3 + 2];

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

        var root = SyntaxFactory.ParseCompilationUnit(text);
        var skipped = HeaderSpans(root);
        var lineStarts = LineStarts(text);

        // ⚠ Flags, not a state: a line carrying both is a counted line, and only a line that is
        // HeaderToken and nothing else leaves both halves of the ratio.
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

            var header = IsHeader(skipped, ref next, span.Start);
            Mark(lines, lineStarts, span, header ? HeaderToken : CountedToken);
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

        return new(codes, starts, ends, HeaderLineCount(lines), UniformRuns(root, codes, starts));
    }

    /// <summary>
    ///     Whether the token at <paramref name="start" /> is inside the header.
    /// </summary>
    /// <remarks>
    ///     ⚠ One forward pointer, not a search. <see cref="SyntaxFactory.ParseTokens" /> yields in source
    ///     order and <paramref name="skipped" /> is sorted, so the whole skip costs one pass over a list
    ///     that is usually a dozen long — <paramref name="next" /> is carried across calls and never
    ///     rewinds.
    /// </remarks>
    static bool IsHeader(List<TextSpan> skipped, ref int next, int start) {
        while (next < skipped.Count && skipped[next].End <= start) {
            next++;
        }

        return next < skipped.Count && skipped[next].Start <= start;
    }

    /// <summary>Lines that held a header token and no counted one. ⚠ A line with neither is neither.</summary>
    static int HeaderLineCount(byte[] lines) => lines.Count(static state => state == HeaderToken);

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
    static List<TextSpan> HeaderSpans(CompilationUnitSyntax root) {
        var spans = new List<TextSpan>();
        Collect(root, spans);
        return spans;
    }

    /// <summary>
    ///     Every uniform sibling run in the file, as flat <c>(first, past, stride)</c> triples over the
    ///     token stream — see <see cref="Runs" />.
    /// </summary>
    /// <remarks>
    ///     ⚠ The lists considered are the ones a table is actually written as: an initialiser or a
    ///     collection expression, an argument list, a switch expression's arms, an enum's members, and a
    ///     type's own member list. Anything else — a parameter list, a block's statements, a base list —
    ///     either cannot reach a hundred tokens of repetition or is ordinary code whose repetition is the
    ///     finding.
    ///     <para>
    ///         ⚠ Runs nest: <c>[new Foo(1, 2, 3), new Foo(4, 5, 6), new Foo(7, 8, 9)]</c> is one run of
    ///         three elements, and each element's argument list is another. Only the outermost is kept —
    ///         an inner run says nothing the run containing it does not already say, and overlapping
    ///         entries would make <see cref="RunCovering" />'s binary search a range query for no gain.
    ///     </para>
    /// </remarks>
    static int[] UniformRuns(CompilationUnitSyntax root, ushort[] codes, int[] starts) {
        var runs = new List<(int First, int Past, int Stride)>();
        foreach (var node in root.DescendantNodesAndSelf()) {
            var elements = Elements(node);
            if (elements.Count >= MinimumRunElements) {
                Uniform(elements, codes, starts, runs);
            }
        }

        if (runs.Count == 0) {
            return [];
        }

        // Outermost wins: earliest start first, and the longer of two starting together.
        runs.Sort(static (left, right) =>
            left.First != right.First ? left.First.CompareTo(right.First) : right.Past.CompareTo(left.Past)
        );

        var kept = new List<int>(runs.Count * 3);
        var reached = 0;
        foreach (var (first, past, stride) in runs) {
            if (first < reached) {
                continue;
            }

            kept.Add(first);
            kept.Add(past);
            kept.Add(stride);
            reached = past;
        }

        return [.. kept];
    }

    /// <summary>The sibling elements of the lists a table is written as, or an empty list.</summary>
    static IReadOnlyList<SyntaxNode> Elements(SyntaxNode node) =>
        node switch {
            InitializerExpressionSyntax initializer => initializer.Expressions,
            CollectionExpressionSyntax collection => collection.Elements,
            ArgumentListSyntax arguments => arguments.Arguments,
            SwitchExpressionSyntax switched => switched.Arms,
            EnumDeclarationSyntax enumeration => enumeration.Members,
            TypeDeclarationSyntax type => type.Members,
            _ => []
        };

    /// <summary>
    ///     Adds every maximal run of <see cref="MinimumRunElements" /> or more consecutive elements that
    ///     lex to one token sequence at a constant stride.
    /// </summary>
    /// <remarks>
    ///     ⚠ The comparison is over <paramref name="codes" /> — the stream's own normalisation — and not
    ///     over the parse tree's tokens. They are not the same tokens: <see cref="Lex" /> reads lexer
    ///     tokens, where a contextual keyword is an identifier and an interpolated string is one token, so
    ///     comparing the tree's would be a second normalisation free to disagree with the one every
    ///     verdict is actually made against.
    /// </remarks>
    static void Uniform(
        IReadOnlyList<SyntaxNode> elements,
        ushort[] codes,
        int[] starts,
        List<(int First, int Past, int Stride)> runs
    ) {
        var first = new int[elements.Count];
        var past = new int[elements.Count];
        for (var i = 0; i < elements.Count; i++) {
            var span = elements[i].Span;
            first[i] = TokenAt(starts, span.Start);
            past[i] = PastToken(starts, span.End);
            if (first[i] < 0 || past[i] <= first[i]) {
                // A zero-width or header-skipped element: it has no run and it breaks any it touches.
                first[i] = -1;
            }
        }

        var start = 0;
        while (start < elements.Count) {
            var end = start;
            if (Matches(codes, first, past, start, start + 1, elements.Count)) {
                var stride = first[start + 1] - first[start];
                end = start + 1;
                while (Matches(codes, first, past, end, end + 1, elements.Count)
                       && first[end + 1] - first[end] == stride) {
                    end++;
                }

                if (end - start + 1 >= MinimumRunElements) {
                    runs.Add((first[start], Periodic(codes, past[end], first[end] + stride, stride), stride));
                }
            }

            // ⚠ Restart *on* the element that ended the run, never past it. A run can end because the
            // stride moved rather than because the shapes stopped agreeing, and that element is then the
            // first row of the next table.
            start = Math.Max(end, start + 1);
        }
    }

    /// <summary>Whether two adjacent elements lex alike.</summary>
    /// <remarks>
    ///     ⚠ The stride has to be constant across the whole run, so the caller compares it against the
    ///     run's <i>first</i> pair rather than against the previous one. A trailing comma, an attribute on
    ///     one member or a directive between two rows moves the period, and a period that is only locally
    ///     constant is not a period.
    /// </remarks>
    static bool Matches(ushort[] codes, int[] first, int[] past, int left, int right, int count) {
        if (right >= count) {
            return false;
        }

        if (first[left] < 0 || first[right] < 0) {
            return false;
        }

        var length = past[left] - first[left];
        if (past[right] - first[right] != length) {
            return false;
        }

        for (var i = 0; i < length; i++) {
            if (codes[first[left] + i] != codes[first[right] + i]) {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    ///     Carries the run's end forward over whatever still repeats at <paramref name="stride" />, up to
    ///     <paramref name="limit" />.
    /// </summary>
    /// <remarks>
    ///     ⚠
    ///     <b>
    ///         The separator after the last element belongs to the run, and leaving it out was an
    ///         off-by-one with real consequences.
    ///     </b> A list of 60 four-token elements is 299 tokens to the end
    ///     of the last one and 300 with its trailing comma, so the third 100-token window over it ran one
    ///     token past the recorded span, came back uncovered, and was reported as duplication — the exact
    ///     finding this exists to decline, surviving in the one window that reaches the end of the table.
    ///     The run is therefore the maximal region the period actually covers, not the span of the
    ///     elements.
    /// </remarks>
    static int Periodic(ushort[] codes, int past, int limit, int stride) {
        while (past < limit && past < codes.Length && codes[past] == codes[past - stride]) {
            past++;
        }

        return past;
    }

    /// <summary>The index of the token starting exactly at <paramref name="offset" />, or -1.</summary>
    static int TokenAt(int[] starts, int offset) {
        var found = Array.BinarySearch(starts, offset);
        return found >= 0 ? found : -1;
    }

    /// <summary>The index just past the last token starting before <paramref name="offset" />.</summary>
    static int PastToken(int[] starts, int offset) {
        var found = Array.BinarySearch(starts, offset);
        return found >= 0 ? found : ~found;
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
        var bytes = new byte[stream.Count * 10 + stream.Runs.Length * 4 + 8];
        var at = 0;
        for (var i = 0; i < stream.Count; i++) {
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(at), stream.Codes[i]);
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(at + 2), stream.Starts[i]);
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(at + 6), stream.Ends[i]);
            at += 10;
        }

        // ⚠ The runs are in the stamp because they are in the index. A change to `UniformRuns` alone
        // moves no token and no offset, so without this the whole corpus would be served the previous
        // build's runs — which is issue #322's failure exactly, one field later.
        foreach (var value in stream.Runs) {
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(at), value);
            at += 4;
        }

        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(at), stream.Count);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(at + 4), stream.HeaderLines);
        return Convert.ToHexStringLower(XxHash128.Hash(bytes));
    }
}
