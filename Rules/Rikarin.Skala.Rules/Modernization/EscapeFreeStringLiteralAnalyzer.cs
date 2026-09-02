using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Rikarin.Skala.Rules.Metadata;
using System;
using System.Collections.Immutable;
using System.Text;

namespace Rikarin.Skala.Rules.Modernization;

/// <summary>
///     <c>SK1062</c> — the literal form that spells the value without escaping it.
/// </summary>
/// <remarks>
///     ⚠ <b>The delimiter arithmetic is where this rule can emit code that does not compile.</b> A
///     single-line raw string's fence is the longest run of quotes in the content plus one, never
///     fewer than three, and the content may not begin or end with a quote at all. So the rule does
///     not trust its own arithmetic: <see cref="ParsesBackToTheSameValue" /> re-parses every
///     replacement it is about to propose and compares the resulting token's value against the
///     original's. A wrong fence cannot reach a diagnostic, let alone a fix.
///     <para>
///         ⚠ <b>The version gate is per shape.</b> The three raw-string shapes need C# 11; simplifying
///         an escape sequence to the character it denotes needs nothing, and a rule-level floor of 11
///         would have silenced it on exactly the older projects it is for.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class EscapeFreeStringLiteralAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.EscapeFreeStringLiteral);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(static start => {
                var raw = SkalaRule.MeetsLanguageVersion(start.Compilation, "11.0");
                start.RegisterSyntaxNodeAction(
                    context => Analyze(context, raw),
                    SyntaxKind.StringLiteralExpression
                );
            }
        );
    }

    static void Analyze(SyntaxNodeAnalysisContext context, bool raw) {
        var literal = (LiteralExpressionSyntax)context.Node;
        var token = literal.Token;

        // ⚠ A literal inside an interpolation hole is SK1063's to move, not this rule's to respell.
        // Two rules rewriting one span is one of them being wrong.
        for (var node = literal.Parent; node is not null; node = node.Parent) {
            if (node is InterpolatedStringExpressionSyntax) {
                return;
            }
        }

        var text = token.Text;
        var value = token.ValueText;

        if (token.IsKind(SyntaxKind.SingleLineRawStringLiteralToken)) {
            if (raw) {
                ReportShorterFence(context, literal, text, value);
            }

            return;
        }

        if (!token.IsKind(SyntaxKind.StringLiteralToken)) {
            return;
        }

        var verbatim = text.StartsWith("@", StringComparison.Ordinal);

        // ⚠ The trigger is what the *current* form costs, not what the value looks like. A verbatim
        // literal gains from the raw form only if it doubles a quote; a regular one only if it
        // escapes a backslash or a quote. Anything else is already as plain as it gets.
        var worthRewriting = verbatim
            ? value.IndexOf('"') >= 0
            : text.IndexOf("""\\""", StringComparison.Ordinal) >= 0
            || text.IndexOf("\\\"", StringComparison.Ordinal) >= 0;

        if (raw
            && worthRewriting
            && RawStringFor(value) is { } rewritten
            && ParsesBackToTheSameValue(rewritten, value)) {
            Report(
                context,
                literal,
                rewritten,
                "The literal needs no escapes as a raw string"
            );

            return;
        }

        if (verbatim) {
            return;
        }

        if (SimplifiedEscapes(text) is { } simplified && ParsesBackToTheSameValue(simplified, value)) {
            Report(context, literal, simplified, "The escape sequence is simply a character");
        }
    }

    /// <summary>A raw string whose fence is longer than the content requires.</summary>
    static void ReportShorterFence(
        SyntaxNodeAnalysisContext context,
        LiteralExpressionSyntax literal,
        string text,
        string value
    ) {
        if (RawStringFor(value) is not { } shortest
            || shortest.Length >= text.Length
            || !ParsesBackToTheSameValue(shortest, value)) {
            return;
        }

        Report(context, literal, shortest, "The raw string's delimiter is longer than the content needs");
    }

    static void Report(
        SyntaxNodeAnalysisContext context,
        LiteralExpressionSyntax literal,
        string replacement,
        string reason
    ) =>
        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                literal.GetLocation(),
                FixEdits.Pack((literal.Span, replacement)),
                reason + ": `" + RewriteGuards.Trim(replacement) + "`"
            )
        );

    /// <summary>
    ///     The shortest single-line raw string spelling of a value, or null where there is none.
    /// </summary>
    /// <remarks>
    ///     ⚠ Three separate reasons a value has no single-line raw spelling, and only the third is
    ///     obvious: it is empty; it contains a character that cannot be written literally on one line —
    ///     a newline, a tab, any other control character; or it begins or ends with a quote, which the
    ///     greedy fence would swallow.
    ///     <para>
    ///         ⚠ <b>The leading/trailing-quote test is redundant and is kept deliberately.</b> A
    ///         sabotage that deleted it turned nothing red, because
    ///         <see cref="ParsesBackToTheSameValue" /> already refuses every spelling it would have
    ///         refused — the text it produces does not parse at all. It stays because it says *why*
    ///         those values are declined, which a parse failure does not.
    ///     </para>
    /// </remarks>
    static string? RawStringFor(string value) {
        if (value.Length == 0
            || value.StartsWith("\"", StringComparison.Ordinal)
            || value.EndsWith("\"", StringComparison.Ordinal)) {
            return null;
        }

        var longest = 0;
        var run = 0;
        foreach (var character in value) {
            if (character < ' ' || character == '\u007f') {
                return null;
            }

            if (character == '"') {
                run++;
                if (run > longest) {
                    longest = run;
                }
            } else {
                run = 0;
            }
        }

        var fence = new string('"', Math.Max(3, longest + 1));
        return fence + value + fence;
    }

    /// <summary>
    ///     ⚠ Every escape sequence that denotes a plainly writable character, written as that character.
    /// </summary>
    /// <remarks>
    ///     <c>\x</c> is variable-length and greedy: <c>"\x41B"</c> is one escape denoting U+041B, not
    ///     <c>\x41</c> followed by <c>B</c>. The scan consumes it exactly the way the lexer does, and
    ///     then declines it, because U+041B is not in the printable ASCII range this shape admits.
    ///     <c>\\</c>, <c>\"</c>, <c>\n</c>, <c>\r</c>, <c>\t</c>, <c>\0</c> and <c>\U</c> are never
    ///     touched — each of them is either load-bearing or denotes something that cannot be written
    ///     literally.
    /// </remarks>
    static string? SimplifiedEscapes(string text) {
        var builder = new StringBuilder(text.Length);
        var changed = false;
        var index = 0;
        while (index < text.Length) {
            var character = text[index];
            if (character != '\\' || index + 1 >= text.Length) {
                builder.Append(character);
                index++;
                continue;
            }

            var kind = text[index + 1];
            if (kind == '\'') {
                builder.Append('\'');
                index += 2;
                changed = true;
                continue;
            }

            if (kind is 'u' or 'x') {
                var digits = 0;
                var end = index + 2;
                while (end < text.Length && digits < 4 && IsHex(text[end])) {
                    end++;
                    digits++;
                }

                if (kind == 'u' ? digits == 4 : digits > 0) {
                    var denoted = (char)Convert.ToInt32(text.Substring(index + 2, digits), 16);
                    if (IsPlainlyWritable(denoted)) {
                        builder.Append(denoted);
                        index = end;
                        changed = true;
                        continue;
                    }
                }
            }

            builder.Append(character);
            builder.Append(kind);
            index += 2;
        }

        return changed ? builder.ToString() : null;
    }

    static bool IsHex(char character) => character is >= '0' and <= '9' or >= 'a' and <= 'f' or >= 'A' and <= 'F';

    static bool IsPlainlyWritable(char character) =>
        character is >= ' ' and <= '~' && character != '"' && character != '\\';

    /// <summary>
    ///     ⚠ The guard that makes a wrong delimiter impossible: parse the replacement and compare.
    /// </summary>
    /// <remarks>
    ///     A fence one quote too short does not produce a subtly different string — it produces text
    ///     that either fails to parse or parses as something else entirely, and both are caught here.
    ///     The alternative is trusting arithmetic that has to be right for every content, which is the
    ///     kind of claim <c>SK9099</c> turns into a crash report rather than a silent corruption.
    /// </remarks>
    static bool ParsesBackToTheSameValue(string replacement, string expected) {
        var parsed = SyntaxFactory.ParseExpression(
            replacement,
            0,
            new CSharpParseOptions(LanguageVersion.CSharp11)
        );

        return parsed is LiteralExpressionSyntax { RawKind: (int)SyntaxKind.StringLiteralExpression } literal
            && !parsed.ContainsDiagnostics
            && parsed.FullSpan.Length == replacement.Length
            && string.Equals(literal.Token.ValueText, expected, StringComparison.Ordinal);
    }
}
