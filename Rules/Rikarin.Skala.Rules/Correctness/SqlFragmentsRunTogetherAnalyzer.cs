using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Rules.Metadata;
using Rikarin.Skala.Rules.Modernization;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace Rikarin.Skala.Rules.Correctness;

/// <summary>
///     <c>SK2230</c> — two SQL fragments concatenated with no separator between them.
/// </summary>
/// <remarks>
///     <para>
///         The statement was split over two lines and the space that separated the halves went with
///         the line break. It compiles, it is a perfectly good <c>string</c>, and the failure arrives
///         from the database naming a token — <c>usersWHERE</c> — that the source file does not
///         contain.
///     </para>
///     <para>
///         ⚠
///         <b>
///             The whole risk is the "this is SQL" test, so it is three conditions rather than
///             one.
///         </b> The concatenation's first literal must open with a statement keyword; the join
///         must actually fuse two word characters; and the word the right-hand literal begins with
///         must itself be a SQL keyword, matched whole. Anything looser reports ordinary string
///         building.
///     </para>
///     <para>
///         ⚠ <b>Only the right-hand direction is tested.</b> "the left literal ends with a keyword"
///         reports <c>"select * from Order" + "Items"</c> — a table name split over two lines, where
///         <c>Order</c> is a keyword by coincidence and the fusion is deliberate. Nothing in the file
///         separates that from the defect, so the shape is not reported at all.
///     </para>
///     <para>
///         ⚠ <b><c>SK5001</c> is disjoint from this by construction.</b> It fires only when a value
///         that crossed a trust boundary reaches the SQL; this fires only when every fragment is a
///         written literal, and a literal is never tainted.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class SqlFragmentsRunTogetherAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.SqlFragmentsRunTogether);

    /// <summary>
    ///     ⚠ The keyword a fragment may <em>open</em> with, which is a different list from the one a
    ///     statement may open with. Short and ambiguous words — <c>as</c>, <c>by</c>, <c>in</c>,
    ///     <c>not</c>, <c>end</c> — are deliberately absent: they are common tails of ordinary English
    ///     and of identifiers, and the whole-word match is not enough protection on two letters.
    /// </summary>
    static readonly HashSet<string> ContinuationKeywords = new(StringComparer.Ordinal) {
        "SELECT",
        "FROM",
        "WHERE",
        "AND",
        "OR",
        "ORDER",
        "GROUP",
        "HAVING",
        "JOIN",
        "INNER",
        "LEFT",
        "RIGHT",
        "OUTER",
        "CROSS",
        "FULL",
        "ON",
        "SET",
        "VALUES",
        "INTO",
        "INSERT",
        "UPDATE",
        "DELETE",
        "UNION",
        "LIMIT",
        "OFFSET",
        "DISTINCT",
        "RETURNING"
    };

    /// <summary>The keyword a whole statement may open with — the "this is SQL" gate.</summary>
    static readonly HashSet<string> StatementKeywords = new(StringComparer.Ordinal) {
        "SELECT",
        "INSERT",
        "UPDATE",
        "DELETE",
        "MERGE",
        "WITH",
        "CREATE",
        "ALTER",
        "DROP",
        "TRUNCATE",
        "FROM",
        "WHERE"
    };

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.AddExpression);
    }

    static void Analyze(SyntaxNodeAnalysisContext context) {
        var node = (BinaryExpressionSyntax)context.Node;

        // ⚠ `a + b + c` parses left-associatively, so the outermost `+` is the only one that can see
        // the whole chain. Every inner `+` is visited too and returns here, which is what keeps one
        // chain from being flattened once per operand.
        if (node.Parent is BinaryExpressionSyntax { RawKind: (int)SyntaxKind.AddExpression }) {
            return;
        }

        var operands = new List<ExpressionSyntax>();
        Flatten(node, operands);
        if (operands.Count < 2) {
            return;
        }

        if (LiteralTextOf(operands[0]) is not { } opening || !OpensAStatement(opening)) {
            return;
        }

        // ⚠ Apostrophe parity across everything seen so far, not just the operand pair. A join
        // inside a `'…'` SQL literal is a value being assembled, where a space would change what the
        // statement *says* rather than repair how it *parses*.
        var quotes = CountApostrophes(opening);
        for (var i = 1; i < operands.Count; i++) {
            var right = LiteralTextOf(operands[i]);
            if (right is null) {
                return;
            }

            if (quotes % 2 == 0
                && LiteralTextOf(operands[i - 1]) is { Length: > 0 } left
                && right.Length > 0
                && IsWordCharacter(left[left.Length - 1])
                && IsWordCharacter(right[0])
                && ContinuationKeywords.Contains(LeadingWord(right).ToUpperInvariant())) {
                Report(context, (LiteralExpressionSyntax)operands[i - 1], (LiteralExpressionSyntax)operands[i], right);
                return;
            }

            quotes += CountApostrophes(right);
        }
    }

    static void Report(
        SyntaxNodeAnalysisContext context,
        LiteralExpressionSyntax left,
        LiteralExpressionSyntax right,
        string rightText
    ) {
        // ⚠ The span *between* the two literals, never one spanning them. A `#if` between the
        // operands of a `+` is legal and splits the concatenation across two compilations, so the
        // fix is withheld there — but a `/*` or a `#` inside the SQL text itself is ordinary content
        // and must not withhold anything, which a span covering the literals would let it do.
        var between = TextSpan.FromBounds(left.Span.End, right.SpanStart);
        var properties = RewriteGuards.ContainsCommentOrDirective(left.SyntaxTree, between)
            ? null

            // The edit inserts one space immediately before the left literal's closing quote, so it
            // cannot move a comment, reflow a line, or touch the right literal at all.
            : FixEdits.Pack((new TextSpan(left.Span.End - 1, 0), " "));

        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                right.GetLocation(),
                properties,
                "This joins onto the previous fragment with no space, so the SQL reads `"
                + RewriteGuards.Trim(LastWord(LiteralTextOf(left)!) + LeadingWord(rightText))
                + "`"
            )
        );
    }

    static void Flatten(ExpressionSyntax expression, List<ExpressionSyntax> into) {
        if (expression is BinaryExpressionSyntax { RawKind: (int)SyntaxKind.AddExpression } addition) {
            Flatten(addition.Left, into);
            Flatten(addition.Right, into);
            return;
        }

        into.Add(expression);
    }

    /// <summary>
    ///     The literal's value, or <c>null</c> where the operand is not a plain string literal.
    /// </summary>
    /// <remarks>
    ///     ⚠ Raw string literals are excluded outright. The fix inserts a space before a closing
    ///     delimiter, and for a multi-line raw literal that delimiter's own line decides the
    ///     indentation stripped from every content line — an edit there is not the local one this
    ///     rule promises. An interpolated string is not a <see cref="LiteralExpressionSyntax" /> at
    ///     all and never reaches here.
    /// </remarks>
    static string? LiteralTextOf(ExpressionSyntax expression) =>
        expression is LiteralExpressionSyntax { RawKind: (int)SyntaxKind.StringLiteralExpression } literal
        && literal.Token.IsKind(SyntaxKind.StringLiteralToken)
            ? literal.Token.ValueText
            : null;

    static bool OpensAStatement(string text) {
        var start = 0;
        while (start < text.Length && char.IsWhiteSpace(text[start])) {
            start++;
        }

        var end = start;
        while (end < text.Length && IsWordCharacter(text[end])) {
            end++;
        }

        return end > start && StatementKeywords.Contains(Upper(text, start, end - start));
    }

    static string LeadingWord(string text) {
        var end = 0;
        while (end < text.Length && IsWordCharacter(text[end])) {
            end++;
        }

        return text.Substring(0, end);
    }

    static string LastWord(string text) {
        var start = text.Length;
        while (start > 0 && IsWordCharacter(text[start - 1])) {
            start--;
        }

        return text.Substring(start);
    }

    static int CountApostrophes(string text) {
        var count = 0;
        foreach (var c in text) {
            if (c == '\'') {
                count++;
            }
        }

        return count;
    }

    static string Upper(string text, int start, int length) => text.Substring(start, length).ToUpperInvariant();

    static bool IsWordCharacter(char c) => char.IsLetterOrDigit(c) || c == '_';
}
