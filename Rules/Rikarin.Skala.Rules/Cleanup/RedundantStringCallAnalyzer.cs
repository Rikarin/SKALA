using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Rules.Metadata;
using Rikarin.Skala.Rules.Modernization;
using System;
using System.Collections.Immutable;

namespace Rikarin.Skala.Rules.Cleanup;

/// <summary>
///     <c>SK0231</c> — a call on a string that produces the string it was given.
/// </summary>
/// <remarks>
///     <para>
///         Five shapes, each a deletion: <c>s.ToString()</c> on something already a string,
///         <c>foreach (var c in s.ToCharArray())</c>, <c>string.Format</c> of a literal with no
///         placeholders, an interpolated string with no holes, and a verbatim prefix on a literal with
///         nothing to escape. Two of them are allocations rather than noise — <c>ToCharArray</c> copies
///         the whole string so that <c>foreach</c> can walk what <c>string</c> already indexes, and
///         <c>string.Format</c> builds a <c>ReadOnlySpan</c> parse over a format that has no items.
///     </para>
///     <para>
///         ⚠
///         <b>
///             Every branch here is guarded by what the deletion would <em>mean</em>, not by what it
///             would look like.
///         </b> An interpolated string is only reported where the compiler was converting
///         it to <c>string</c>: a <c>FormattableString</c> target and an interpolated-string handler both
///         accept <c>$"…"</c> and reject <c>"…"</c>, and the difference is invisible in the syntax. A
///         verbatim prefix is only reported where the body holds no backslash, no doubled quote and no
///         newline, because the prefix is what makes each of those mean what it means. And
///         <c>string.Format</c> is only reported where the literal holds no brace at all, because
///         <c>string.Format("{{0}}")</c> returns <c>{0}</c> and the literal returns <c>{{0}}</c>.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class RedundantStringCallAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.RedundantStringCall);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeInvocation, SyntaxKind.InvocationExpression);
        context.RegisterSyntaxNodeAction(AnalyzeInterpolation, SyntaxKind.InterpolatedStringExpression);
        context.RegisterSyntaxNodeAction(AnalyzeLiteral, SyntaxKind.StringLiteralExpression);
    }

    static void AnalyzeInvocation(SyntaxNodeAnalysisContext context) {
        var invocation = (InvocationExpressionSyntax)context.Node;

        // ⚠ The receiver's type is never read from the written name. The symbol's containing type is
        // what separates `s.ToString()` from `((object)s).ToString()` and from `count.ToString()`,
        // which look the same and are three different calls.
        if (context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol
            is not IMethodSymbol method
            || method.ContainingType.SpecialType != SpecialType.System_String) {
            return;
        }

        switch (method.Name) {
            case "ToString" when method.Parameters.IsEmpty && !method.IsStatic:
                AnalyzeToString(context, invocation);
                break;

            case "ToCharArray" when method.Parameters.IsEmpty && !method.IsStatic:
                AnalyzeToCharArray(context, invocation);
                break;

            case "Format" when method.IsStatic:
                AnalyzeFormat(context, invocation);
                break;
        }
    }

    static void AnalyzeToString(SyntaxNodeAnalysisContext context, InvocationExpressionSyntax invocation) {
        if (invocation.Expression is not MemberAccessExpressionSyntax {
                RawKind: (int)SyntaxKind.SimpleMemberAccessExpression
            } access) {
            return;
        }

        // ⚠ `s.ToString();` as a whole statement is the null-check idiom, not a conversion. The
        // result is discarded, so what the call is there for is the dereference, and `s;` is not
        // even an expression statement.
        if (invocation.Parent is ExpressionStatementSyntax) {
            return;
        }

        if (!IsDefinitelyNotNull(context, access.Expression)) {
            return;
        }

        Report(
            context,
            TextSpan.FromBounds(access.Expression.Span.End, invocation.Span.End),
            string.Empty,
            "`ToString()` on a string returns the string"
        );
    }

    static void AnalyzeToCharArray(SyntaxNodeAnalysisContext context, InvocationExpressionSyntax invocation) {
        if (invocation.Expression is not MemberAccessExpressionSyntax {
                RawKind: (int)SyntaxKind.SimpleMemberAccessExpression
            } access) {
            return;
        }

        // ⚠ Only in a `foreach` header. Elsewhere the array is a `char[]` the caller may keep, index,
        // sort or hand on, and `string` is none of those things; here the copy exists only so that
        // the loop can read what `string` already enumerates.
        if (invocation.Parent is not ForEachStatementSyntax loop || loop.Expression != invocation) {
            return;
        }

        if (!IsDefinitelyNotNull(context, access.Expression)) {
            return;
        }

        Report(
            context,
            TextSpan.FromBounds(access.Expression.Span.End, invocation.Span.End),
            string.Empty,
            "`foreach` over `ToCharArray()` copies the whole string; a string enumerates its chars"
        );
    }

    static void AnalyzeFormat(SyntaxNodeAnalysisContext context, InvocationExpressionSyntax invocation) {
        // One argument, and the parameter it fills is the format rather than an IFormatProvider.
        if (invocation.ArgumentList.Arguments.Count != 1) {
            return;
        }

        var argument = invocation.ArgumentList.Arguments[0];
        if (argument.NameColon is not null
            || argument.Expression is not LiteralExpressionSyntax literal
            || !literal.IsKind(SyntaxKind.StringLiteralExpression)) {
            return;
        }

        // ⚠ A brace of any kind withdraws it. `string.Format("{{0}}")` returns `{0}` and the literal
        // returns `{{0}}`, so the shape that looks most obviously safe is the one that is not.
        var text = literal.Token.Text;
        if (text.IndexOf('{') >= 0 || text.IndexOf('}') >= 0) {
            return;
        }

        Report(
            context,
            invocation.Span,
            text,
            "`string.Format` of a literal with no placeholders returns the literal"
        );
    }

    static void AnalyzeInterpolation(SyntaxNodeAnalysisContext context) {
        var node = (InterpolatedStringExpressionSyntax)context.Node;
        foreach (var content in node.Contents) {
            // A hole is the whole point of the `$`; a brace in the text is an escape the plain
            // literal would stop performing.
            if (content is not InterpolatedStringTextSyntax text
                || text.TextToken.Text.IndexOf('{') >= 0
                || text.TextToken.Text.IndexOf('}') >= 0) {
                return;
            }
        }

        var start = node.StringStartToken.Text;
        var dollar = start.IndexOf('$');
        if (dollar < 0 || start.IndexOf('$', dollar + 1) >= 0) {
            return;
        }

        // ⚠ The conversion, not the syntax, is what makes the `$` removable. `FormattableString fs =
        // $"a";` and every interpolated-string handler overload accept `$"a"` and reject `"a"`, and
        // the two spellings are identical up to this question.
        if (context.SemanticModel.GetTypeInfo(node, context.CancellationToken).ConvertedType?.SpecialType
            != SpecialType.System_String) {
            return;
        }

        Report(
            context,
            new TextSpan(node.StringStartToken.SpanStart + dollar, 1),
            string.Empty,
            "The interpolated string has no interpolations"
        );
    }

    static void AnalyzeLiteral(SyntaxNodeAnalysisContext context) {
        var text = ((LiteralExpressionSyntax)context.Node).Token.Text;
        if (text.Length < 3
            || !text.StartsWith("@\"", StringComparison.Ordinal)
            || text[text.Length - 1] != '"') {
            return;
        }

        // The three things the prefix is for. A backslash would become an escape, a doubled quote
        // would become two string ends, and a newline does not fit in a regular literal at all.
        var body = text.Substring(2, text.Length - 3);
        if (body.IndexOf('\\') >= 0
            || body.IndexOf('"') >= 0
            || body.IndexOf('\n') >= 0
            || body.IndexOf('\r') >= 0) {
            return;
        }

        Report(
            context,
            new TextSpan(context.Node.SpanStart, 1),
            string.Empty,
            "The verbatim prefix escapes nothing"
        );
    }

    /// <summary>
    ///     ⚠ Whether removing the call can leave a nullable value where a non-nullable one stood.
    /// </summary>
    /// <remarks>
    ///     <c>s.ToString()</c> on a <c>string?</c> is typed <c>string</c> and <c>s</c> is not, so the
    ///     deletion is where <c>CS8600</c> comes from. The flow state is the right question rather than
    ///     the annotation: a <c>string?</c> the compiler has already proved non-null at this point is
    ///     one the deletion cannot change. In a nullable-oblivious file the state is
    ///     <see cref="NullableFlowState.None" /> and nothing is being promised either way.
    /// </remarks>
    static bool IsDefinitelyNotNull(SyntaxNodeAnalysisContext context, ExpressionSyntax expression) =>
        context.SemanticModel.GetTypeInfo(expression, context.CancellationToken).Nullability.FlowState
        != NullableFlowState.MaybeNull;

    static void Report(SyntaxNodeAnalysisContext context, TextSpan span, string replacement, string message) {
        if (RewriteGuards.ContainsCommentOrDirectiveWithinTheEdit(context.Node.SyntaxTree, span)) {
            return;
        }

        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                Location.Create(context.Node.SyntaxTree, span),
                FixEdits.Pack((span, replacement)),
                message
            )
        );
    }
}
