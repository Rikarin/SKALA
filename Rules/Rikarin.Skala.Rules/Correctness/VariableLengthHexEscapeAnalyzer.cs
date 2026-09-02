using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Rules.Metadata;
using System.Collections.Immutable;

namespace Rikarin.Skala.Rules.Correctness;

/// <summary>
///     <c>SK2171</c> — a <c>\x</c> escape written with fewer than four hex digits, whose length the
///     next character decides.
/// </summary>
/// <remarks>
///     <c>\x</c> is the only escape in C# without a fixed length: the compiler takes up to four hex
///     digits and does not stop at the ones the author meant. <c>"\x41B"</c> is one character, U+041B —
///     not <c>A</c> followed by <c>B</c>. Append a letter to a string ending in <c>\x41</c> and its last
///     character silently changes: the diff shows one character added and the program changed two.
///     <para>
///         ⚠ <b><c>\u</c> has a fixed length of four and cannot do this</b>, which is why the fix is a
///         spelling change and nothing else — the same character by construction, and safe to apply
///         without review.
///     </para>
///     <para>
///         ⚠
///         <b>
///             The two neighbouring inspections this concept was drawn from are not here, and both were
///             measured out.
///         </b> <c>1l</c> is <c>CS0078</c>, on by default; a probe on SDK 10.0.400 confirms
///         it fires on <c>1l</c> and on <c>1lu</c> and stays silent on <c>1ul</c>. A <c>char</c> argument
///         widening to an <c>int</c> parameter is a question about overload resolution rather than about
///         how a literal reads, and it is a different rule.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class VariableLengthHexEscapeAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.VariableLengthHexEscape);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(
            AnalyzeLiteral,
            SyntaxKind.StringLiteralExpression,
            SyntaxKind.CharacterLiteralExpression,
            SyntaxKind.Utf8StringLiteralExpression
        );
        context.RegisterSyntaxNodeAction(AnalyzeInterpolatedText, SyntaxKind.InterpolatedStringText);
    }

    static void AnalyzeInterpolatedText(SyntaxNodeAnalysisContext context) =>
        Scan(context, ((InterpolatedStringTextSyntax)context.Node).TextToken);

    static void AnalyzeLiteral(SyntaxNodeAnalysisContext context) =>
        Scan(context, ((LiteralExpressionSyntax)context.Node).Token);

    static void Scan(SyntaxNodeAnalysisContext context, SyntaxToken token) {
        // ⚠ Verbatim and raw literals have no escapes at all: `@"\x41B"` is six characters, and a
        // rule that scanned it would be reporting text that does not mean what it assumes. The token
        // kind answers this, so no character of the text has to be trusted to say what form it is in.
        if (token.IsKind(SyntaxKind.SingleLineRawStringLiteralToken)
            || token.IsKind(SyntaxKind.MultiLineRawStringLiteralToken)
            || token.IsKind(SyntaxKind.Utf8SingleLineRawStringLiteralToken)
            || token.IsKind(SyntaxKind.Utf8MultiLineRawStringLiteralToken)) {
            return;
        }

        var text = token.Text;
        if (text.Length == 0 || text[0] == '@') {
            return;
        }

        if (token.Parent is InterpolatedStringTextSyntax
            && token.Parent.Parent is InterpolatedStringExpressionSyntax interpolated
            && !interpolated.StringStartToken.IsKind(SyntaxKind.InterpolatedStringStartToken)) {
            return;
        }

        for (var i = 0; i + 1 < text.Length; i++) {
            if (text[i] != '\\') {
                continue;
            }

            // ⚠ `\\x41B` is a backslash followed by `x41B`, not an escape. Consuming both characters
            // of every `\\` is the whole of the difference between this rule and a search for the
            // two-character string `\x`.
            if (text[i + 1] != 'x') {
                i++;
                continue;
            }

            var end = i + 2;
            while (end < text.Length && end - i - 2 < 4 && IsHex(text[end])) {
                end++;
            }

            var digits = end - i - 2;
            if (digits is >= 1 and <= 3) {
                Report(context, token.SpanStart + i, end - i, text.Substring(i + 2, digits));
            }

            i = end - 1;
        }
    }

    static void Report(SyntaxNodeAnalysisContext context, int start, int length, string digits) {
        var span = new TextSpan(start, length);
        var replacement = """\u""" + digits.PadLeft(4, '0');
        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                Location.Create(context.Node.SyntaxTree, span),
                FixEdits.Pack((span, replacement)),
                """`\x"""
                + digits
                + "` takes up to four hex digits and stopped at "
                + digits.Length
                + " because of what follows it; write `"
                + replacement
                + "`"
            )
        );
    }

    static bool IsHex(char c) => c is >= '0' and <= '9' or >= 'a' and <= 'f' or >= 'A' and <= 'F';
}
