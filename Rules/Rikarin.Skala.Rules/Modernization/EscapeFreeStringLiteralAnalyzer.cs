using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Rikarin.Skala.Rules.Metadata;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
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
///     <para>
///         ⚠
///         <b>
///             The raw rewrite is all-or-nothing over a run, and that is not a refinement of the
///             per-literal decision — it overrules it
///         </b> (issue #331). Every per-literal verdict this rule
///         reaches is correct; applied to a run of sibling calls assembling one document it converted the
///         literals it could and left the ones it may not, so a block the author had written uniformly came
///         back in two spellings at once. The defect only exists <i>between</i> neighbours, which is why no
///         single-literal fixture could ever have shown it. See <see cref="RunIsMixed" /> for where the run
///         begins and ends.
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

        if (raw
            && WorthRewriting(text, value)
            && RawStringFor(value) is { } rewritten
            && ParsesBackToTheSameValue(rewritten, value)
            && !RunIsMixed(literal)) {
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
    ///     ⚠ Whether the <i>current</i> form costs escapes, which is the trigger — not what the value
    ///     looks like.
    /// </summary>
    /// <remarks>
    ///     A verbatim literal gains from the raw form only if it doubles a quote; a regular one only if it
    ///     escapes a backslash or a quote. Anything else is already as plain as it gets.
    /// </remarks>
    static bool WorthRewriting(string text, string value) =>
        text.StartsWith("@", StringComparison.Ordinal)
            ? value.IndexOf('"') >= 0
            : text.IndexOf("""\\""", StringComparison.Ordinal) >= 0
            || text.IndexOf("\\\"", StringComparison.Ordinal) >= 0;

    /// <summary>
    ///     Whether some literal in <paramref name="literal" />'s run wants the raw form and cannot have it
    ///     — <b>issue #331</b>.
    /// </summary>
    /// <remarks>
    ///     ⚠ <b>The run is the unit, because the reader's unit is the run.</b> A literal that pays for
    ///     escapes and has no single-line raw spelling — one whose value begins or ends with a quote — can
    ///     never join its neighbours in the raw form, so converting the neighbours around it leaves the
    ///     block in two spellings at once, which is worse than either uniform state. Where one member is
    ///     stuck, the whole run stays as written.
    ///     <para>
    ///         ⚠ <b>Only the raw rewrite is gated.</b> Simplifying <c>\x41</c> to <c>A</c> respells a
    ///         character inside a literal whose <i>form</i> does not change, so it cannot make a block
    ///         mixed and is left to fire on its own. Shortening an over-long fence is likewise still a raw
    ///         string afterwards.
    ///     </para>
    ///     <para>
    ///         ⚠ <b>A member that pays no escapes at all is not a blocker.</b> <c>builder.AppendLine("{")</c>
    ///         sitting beside a raw literal is what uniform code looks like; the mixed block this rule
    ///         guards against is an <i>escaped</i> literal beside a raw one spelling the same kind of
    ///         content.
    ///     </para>
    /// </remarks>
    static bool RunIsMixed(LiteralExpressionSyntax literal) {
        foreach (var statement in Run(literal)) {
            foreach (var member in Literals(statement)) {
                if (member.Token.IsKind(SyntaxKind.StringLiteralToken)
                    && WorthRewriting(member.Token.Text, member.Token.ValueText)
                    && (RawStringFor(member.Token.ValueText) is not { } spelling
                        || !ParsesBackToTheSameValue(spelling, member.Token.ValueText))) {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    ///     The nodes whose literals a reader takes as one block with <paramref name="literal" />'s.
    /// </summary>
    /// <remarks>
    ///     ⚠ <b>Two boundaries, and the tighter one is deliberate.</b> The unambiguous group is the
    ///     invocation chain: <c>builder.Append(a).Append(b).AppendLine(c)</c> is one expression a reader
    ///     takes in at once. The useful group is wider — a run of consecutive expression statements whose
    ///     chains all root on the same receiver, which is how a <c>StringBuilder</c> assembling a document
    ///     is actually written. It is bounded on both sides by the <i>first</i> statement that is not one
    ///     of those, and by a blank line or a comment between two members.
    ///     <para>
    ///         ⚠ <b>Not the whole method, and the difference is the point.</b> Taking every literal in the
    ///         enclosing member would let one awkward literal in an early paragraph freeze every literal
    ///         after it, however far away and however unrelated — the rule would then be wrong about the
    ///         group in the other direction. A blank line is the author's own paragraph mark and is
    ///         treated as one; so is an intervening comment, for the same reason.
    ///     </para>
    ///     <para>
    ///         ⚠ The receiver is compared by name, not by symbol. This rule is <c>Syntax</c>-scoped
    ///         (<c>rules.json</c>) and runs with no compilation at all under <c>--load=loose</c>, so a
    ///         symbol comparison would silently stop grouping in exactly the mode the rule is most used in.
    ///         Two different <c>builder</c>s in one run of statements is not a shape that occurs, and
    ///         mis-grouping costs a diagnostic that is not reported rather than a wrong rewrite.
    ///     </para>
    /// </remarks>
    static List<SyntaxNode> Run(LiteralExpressionSyntax literal) {
        var statement = literal.FirstAncestorOrSelf<ExpressionStatementSyntax>();
        if (statement is null) {
            // Not a statement at all — an initialiser, an argument, an arrow body. The chain it sits in
            // is the whole of its group.
            return [Chain(literal)];
        }

        if (statement.Parent is not BlockSyntax block || Receiver(statement.Expression) is not { } receiver) {
            return [statement];
        }

        var statements = block.Statements;
        var index = statements.IndexOf(statement);

        // ⚠ The statement being *added* is the one whose receiver decides, in both directions. Testing
        // the one already in the run instead is a test that always passes, and it reads as working:
        // every blocked case still blocks, and the run silently grows backwards over statements
        // belonging to somebody else.
        var first = index;
        while (first > 0
               && RootedOn(statements[first - 1], receiver)
               && !Separated(statements[first - 1], statements[first])) {
            first--;
        }

        var last = index;
        while (last + 1 < statements.Count
               && RootedOn(statements[last + 1], receiver)
               && !Separated(statements[last], statements[last + 1])) {
            last++;
        }

        var run = new List<SyntaxNode>(last - first + 1);
        for (var i = first; i <= last; i++) {
            run.Add(statements[i]);
        }

        return run;
    }

    /// <summary>Whether <paramref name="statement" /> is a call chain rooted on <paramref name="receiver" />.</summary>
    static bool RootedOn(StatementSyntax statement, string receiver) =>
        statement is ExpressionStatementSyntax expression
        && Receiver(expression.Expression) is { } other
        && string.Equals(other, receiver, StringComparison.Ordinal);

    /// <summary>
    ///     Whether a blank line or a comment stands between two adjacent statements.
    /// </summary>
    /// <remarks>
    ///     ⚠ Counted from the trivia, not from the text: one end-of-line is the newline that ends
    ///     <paramref name="previous" />, so a second one is a line with nothing on it. A comment carries
    ///     its own end-of-line and therefore separates by the same count, which is the intended answer —
    ///     a reader who wrote a comment between two calls has said they are two things.
    /// </remarks>
    static bool Separated(StatementSyntax previous, StatementSyntax next) {
        var newlines = 0;
        foreach (var trivia in previous.GetTrailingTrivia()) {
            if (trivia.IsKind(SyntaxKind.EndOfLineTrivia)) {
                newlines++;
            }
        }

        foreach (var trivia in next.GetLeadingTrivia()) {
            if (trivia.IsKind(SyntaxKind.EndOfLineTrivia)) {
                newlines++;
            }
        }

        return newlines > 1;
    }

    /// <summary>
    ///     The name at the root of an invocation chain — the <c>builder</c> in
    ///     <c>builder.Append(a).AppendLine(b)</c> — or null where the root is not a plain name.
    /// </summary>
    static string? Receiver(ExpressionSyntax expression) {
        var invocations = 0;
        for (var node = expression; node is not null;) {
            switch (node) {
                case InvocationExpressionSyntax invocation:
                    invocations++;
                    node = invocation.Expression;
                    continue;
                case MemberAccessExpressionSyntax access:
                    node = access.Expression;
                    continue;
                case ConditionalAccessExpressionSyntax conditional:
                    node = conditional.Expression;
                    continue;
                case AwaitExpressionSyntax await:
                    node = await.Expression;
                    continue;

                // ⚠ At least one call, or `x;` and `x.y;` would root a run on a name that never
                // assembles anything.
                case IdentifierNameSyntax name when invocations > 0:
                    return name.Identifier.ValueText;
                case ThisExpressionSyntax when invocations > 0:
                    return "this";
                default:
                    return null;
            }
        }

        return null;
    }

    /// <summary>The outermost invocation chain <paramref name="literal" /> is an argument of.</summary>
    static SyntaxNode Chain(LiteralExpressionSyntax literal) {
        SyntaxNode outermost = literal;
        for (var node = literal.Parent; node is not null; node = node.Parent) {
            if (node is InvocationExpressionSyntax) {
                outermost = node;
            } else if (node is not (ArgumentSyntax or ArgumentListSyntax or MemberAccessExpressionSyntax)) {
                break;
            }
        }

        return outermost;
    }

    /// <summary>
    ///     The string literals in <paramref name="scope" /> that belong to the run.
    /// </summary>
    /// <remarks>
    ///     ⚠ A lambda's body is not part of the run. <c>builder.Append(items.Select(i =&gt; "\"" + i))</c>
    ///     holds a literal a reader reads as part of the lambda, not as part of the document being
    ///     assembled, and letting it block the run would freeze a builder over an argument nobody groups
    ///     with it.
    /// </remarks>
    static IEnumerable<LiteralExpressionSyntax> Literals(SyntaxNode scope) =>
        scope.DescendantNodes(static node => node is not (AnonymousFunctionExpressionSyntax
                or LocalFunctionStatementSyntax
                or InterpolatedStringExpressionSyntax)
        )
            .OfType<LiteralExpressionSyntax>()
            .Where(static node => node.IsKind(SyntaxKind.StringLiteralExpression));

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
