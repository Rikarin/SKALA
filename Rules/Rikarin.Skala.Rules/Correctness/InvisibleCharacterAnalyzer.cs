using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Rules.Metadata;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;

namespace Rikarin.Skala.Rules.Correctness;

/// <summary>
///     <c>SK2072</c> — a literal holding a character that changes its value and that no reader can see.
/// </summary>
/// <remarks>
///     A zero-width space, a non-breaking space or a right-to-left override pasted into a literal is
///     invisible in the editor, in the diff and in the review, and it changes what the program does. An
///     escape sequence is the same value written so a person can see it.
///     <para>
///         ⚠ <b>Every character in this file is written as an escape, including in the comments.</b>
///         The rule's own source is the first place its subject matter can hide: a table of literal
///         zero-width characters is a table nobody can proofread, a fixture written that way cannot be
///         told from a broken one, and Skala's own <c>format --check</c> would carry the bytes along
///         silently. <c>InvisibleCharacterSourceTests</c> asserts that this file and the fixtures stay
///         escape-only, because the convention is exactly the kind that erodes.
///     </para>
///     <para>
///         ⚠ <b>Reported only where an escape is expressible.</b> A verbatim (<c>@"…"</c>) or raw
///         (<c>"""…"""</c>) literal has no escape sequences at all — that is what those literals are
///         for — so there is nothing to make explicit and the finding would be one nobody could act on.
///         UTF-8 literals are a separate token kind and are out for the same reason. ⚠ This is a real
///         hole rather than a tidy boundary: a bidirectional override inside a raw string literal is
///         exactly as dangerous and is not reported, and the honest form of that is to say so rather
///         than to emit a finding with no repair.
///     </para>
///     <para>
///         ⚠ <b>The scan reads the token's source spelling, never its value.</b> That is what makes an
///         already-escaped character silent: in the raw text <c>\u200B</c> is six ASCII characters, and
///         only a literal one is a finding. Reading <c>ValueText</c> would report the repair as the
///         problem, and the rule would never go quiet.
///     </para>
///     <para>
///         ⚠
///         <b>
///             Two classes of character, one severity, and that is a decision rather than an
///             oversight.
///         </b> A right-to-left override is the "Trojan Source" class — source that reads as
///         one program and compiles as another — and a stray non-breaking space is a typo. The message
///         says which one it found; the severity does not, because splitting them would spend a second
///         permanent id (ADR-012) to encode a ranking, and a repository that wants the harder line
///         writes <c>dotnet_diagnostic.SK2072.severity = error</c>.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class InvisibleCharacterAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.InvisibleCharacterInLiteral);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxTreeAction(Analyze);
    }

    static void Analyze(SyntaxTreeAnalysisContext context) {
        var root = context.Tree.GetRoot(context.CancellationToken);
        foreach (var token in root.DescendantTokens()) {
            if (!IsRegularStringOrChar(token)) {
                continue;
            }

            var text = token.Text;
            for (var i = 0; i < text.Length; i++) {
                var c = text[i];
                if (!Invisible.Contains(c)) {
                    continue;
                }

                var span = new TextSpan(token.SpanStart + i, 1);
                context.ReportDiagnostic(
                    Diagnostic.Create(
                        Descriptor,
                        Location.Create(context.Tree, span),
                        FixEdits.Pack((span, Escape(c))),
                        Describe(c) + "; write it as `" + Escape(c) + "` so it is visible"
                    )
                );
            }
        }
    }

    /// <summary>
    ///     Whether an escape sequence can be written inside this token at all.
    /// </summary>
    /// <remarks>
    ///     ⚠ A raw string literal and a UTF-8 literal each have their own <see cref="SyntaxKind" />, so
    ///     they are excluded by not being named here rather than by a test that could rot. Verbatim
    ///     strings share <c>StringLiteralToken</c> with regular ones and are separated by the <c>@</c>
    ///     the lexer keeps in <see cref="SyntaxToken.Text" />.
    /// </remarks>
    static bool IsRegularStringOrChar(SyntaxToken token) {
        if (token.IsKind(SyntaxKind.CharacterLiteralToken)) {
            return true;
        }

        if (token.IsKind(SyntaxKind.StringLiteralToken)) {
            return !token.Text.StartsWith("@", StringComparison.Ordinal);
        }

        return token.IsKind(SyntaxKind.InterpolatedStringTextToken)
            && token.Parent?.Parent is InterpolatedStringExpressionSyntax interpolation
            && interpolation.StringStartToken.IsKind(SyntaxKind.InterpolatedStringStartToken);
    }

    /// <summary>
    ///     ⚠ Always <c>\uXXXX</c>, never <c>\xNN</c>. The <c>\x</c> escape is variable length and
    ///     consumes greedily, so <c>\x9</c> followed by the letter <c>G</c> is a different character
    ///     than intended in one case and a compile error in another. A fix that is right for most
    ///     inputs is the fix that breaks a build on the tool's advice.
    /// </summary>
    static string Escape(char c) => """\u""" + ((int)c).ToString("X4", CultureInfo.InvariantCulture);

    static string Describe(char c) {
        var name = Names.TryGetValue(c, out var known)
            ? known
            : c is < ' ' or '\u007F' or >= '\u0080' and <= '\u009F'
                ? "a control character"
                : "an invisible character";

        var note = Bidi.Contains(c)
            ? """ — a bidirectional control, the "Trojan Source" class: it reorders how the """
            + "literal reads without changing what it contains"
            : string.Empty;

        return "the literal contains "
            + name
            + " (U+"
            + ((int)c).ToString("X4", CultureInfo.InvariantCulture)
            + ")"
            + note;
    }

    static readonly HashSet<char> Bidi = [
        '\u200E', '\u200F', '\u202A', '\u202B', '\u202C', '\u202D', '\u202E',
        '\u2066', '\u2067', '\u2068', '\u2069'
    ];

    /// <summary>
    ///     ⚠ <b>Every character here is one a reader cannot see, and none is one they can.</b> The
    ///     confusable-<em>width</em> spaces — <c>U+2000</c> to <c>U+200A</c> and <c>U+3000</c> — are
    ///     deliberately absent: a thin space in typography and an ideographic space in Japanese text
    ///     are visible, intentional and correct, and reporting them would make the rule an opinion
    ///     about typesetting. <c>U+00A0</c> and <c>U+202F</c> stay, because they are indistinguishable
    ///     from an ordinary space rather than merely narrower than one.
    /// </summary>
    static readonly HashSet<char> Invisible = Build();

    static HashSet<char> Build() {
        var set = new HashSet<char> {
            '\u00A0', // no-break space
            '\u00AD', // soft hyphen
            '\u034F', // combining grapheme joiner
            '\u061C', // Arabic letter mark
            '\u180E', // Mongolian vowel separator
            '\u200B', // zero width space
            '\u200C', // zero width non-joiner
            '\u200D', // zero width joiner
            '\u2028', // line separator
            '\u2029', // paragraph separator
            '\u202F', // narrow no-break space
            '\u205F', // medium mathematical space
            '\u2060', // word joiner
            '\uFEFF', // zero width no-break space, and the byte order mark
            '\uFFF9',
            '\uFFFA',
            '\uFFFB' // interlinear annotation
        };

        foreach (var c in Bidi) {
            set.Add(c);
        }

        // ⚠ C0 and C1 controls. `\n` and `\r` are absent because the language will not carry them:
        // a raw newline inside a regular string literal does not compile, so they are excluded by
        // being impossible rather than by a hand-written exclusion that could drift.
        for (var c = 0; c <= 0x1F; c++) {
            set.Add((char)c);
        }

        set.Add('\u007F');
        for (var c = 0x80; c <= 0x9F; c++) {
            set.Add((char)c);
        }

        return set;
    }

    static readonly Dictionary<char, string> Names = new() {
        ['\u0009'] = "a tab",
        ['\u000B'] = "a vertical tab",
        ['\u000C'] = "a form feed",
        ['\u001B'] = "an escape control",
        ['\u00A0'] = "a no-break space",
        ['\u00AD'] = "a soft hyphen",
        ['\u034F'] = "a combining grapheme joiner",
        ['\u061C'] = "an Arabic letter mark",
        ['\u180E'] = "a Mongolian vowel separator",
        ['\u200B'] = "a zero-width space",
        ['\u200C'] = "a zero-width non-joiner",
        ['\u200D'] = "a zero-width joiner",
        ['\u200E'] = "a left-to-right mark",
        ['\u200F'] = "a right-to-left mark",
        ['\u2028'] = "a line separator",
        ['\u2029'] = "a paragraph separator",
        ['\u202A'] = "a left-to-right embedding",
        ['\u202B'] = "a right-to-left embedding",
        ['\u202C'] = "a pop directional formatting",
        ['\u202D'] = "a left-to-right override",
        ['\u202E'] = "a right-to-left override",
        ['\u202F'] = "a narrow no-break space",
        ['\u205F'] = "a medium mathematical space",
        ['\u2060'] = "a word joiner",
        ['\u2066'] = "a left-to-right isolate",
        ['\u2067'] = "a right-to-left isolate",
        ['\u2068'] = "a first strong isolate",
        ['\u2069'] = "a pop directional isolate",
        ['\uFEFF'] = "a zero-width no-break space (byte order mark)"
    };
}
