using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Rules.Metadata;
using System.Collections.Immutable;
using System.Threading;

namespace Rikarin.Skala.Rules.Modernization;

/// <summary>
///     <c>SK1044</c> — a hand-written null-or-empty check on a string.
/// </summary>
/// <remarks>
///     ⚠ <b><c>x.Length == 0</c> and <c>x == ""</c> are the same check and both are reported.</b>
///     <c>string</c>'s <c>==</c> is ordinal value equality that compares lengths first, so
///     <c>x == ""</c> is true exactly when <c>x.Length == 0</c> is — there is no third behaviour to
///     preserve and no reason for two rules. <c>string.Empty</c> is admitted alongside the literal
///     because it is the same value under a different spelling, which is what <c>SK0206</c> is about
///     and this rule is not.
///     <para>
///         ⚠ <b>The rule this one must not become is <c>IsNullOrWhiteSpace</c>.</b>
///         <c>x == null || x.Trim().Length == 0</c> is a <em>different predicate</em>: it is true for
///         <c>" "</c> and <c>string.IsNullOrEmpty(" ")</c> is false. Nothing separates the two shapes
///         except what stands between the receiver and <c>.Length</c>, so the receiver is required to
///         be a chain of plain names — which excludes <c>Trim()</c>, every other call, and the double
///         evaluation an indexer would suffer at the same time.
///     </para>
///     <para>
///         The negated form is reported too. <c>string.IsNullOrEmpty</c> carries
///         <c>[NotNullWhen(false)]</c>, so <c>!string.IsNullOrEmpty(x)</c> tells the compiler's null
///         analysis exactly what <c>x != null &amp;&amp; x.Length > 0</c> did.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class NullOrEmptyCheckAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.NullOrEmptyCheck);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(
            Analyze,
            SyntaxKind.LogicalOrExpression,
            SyntaxKind.LogicalAndExpression
        );
    }

    static void Analyze(SyntaxNodeAnalysisContext context) {
        var binary = (BinaryExpressionSyntax)context.Node;

        // `||` spells the positive check and `&&` its negation. Anything else is not this shape.
        var negated = binary.IsKind(SyntaxKind.LogicalAndExpression);
        var cancellation = context.CancellationToken;

        // ⚠ The null test comes first, and that is the rule rather than a stylistic preference:
        // `x.Length == 0 || x == null` dereferences null before it tests for it.
        if (NullOperand(binary.Left, negated) is not { } target
            || EmptyOperand(context.SemanticModel, binary.Right, negated, cancellation) is not { } other) {
            return;
        }

        if (!RewriteGuards.IsPlainNamePath(target) || !RewriteGuards.Same(target, other)) {
            return;
        }

        if (context.SemanticModel.GetTypeInfo(target, cancellation).Type?.SpecialType
            != SpecialType.System_String) {
            return;
        }

        if (RewriteGuards.ContainsCommentOrDirective(binary)) {
            return;
        }

        var call = (negated ? "!" : string.Empty)
            + "string.IsNullOrEmpty("
            + binary.SyntaxTree.GetText().ToString(target.Span)
            + ")";

        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                Location.Create(binary.SyntaxTree, binary.Span),
                FixEdits.Pack((binary.Span, call)),
                "Use `string.IsNullOrEmpty`: `" + RewriteGuards.Trim(call) + "`"
            )
        );
    }

    /// <summary>The operand of a <c>== null</c> / <c>is null</c> test, or its negation.</summary>
    static ExpressionSyntax? NullOperand(ExpressionSyntax expression, bool negated) {
        switch (expression) {
            case BinaryExpressionSyntax binary
                when binary.IsKind(negated ? SyntaxKind.NotEqualsExpression : SyntaxKind.EqualsExpression):
                if (IsNullLiteral(binary.Right)) {
                    return binary.Left;
                }

                return IsNullLiteral(binary.Left) ? binary.Right : null;

            case IsPatternExpressionSyntax { Pattern: UnaryPatternSyntax { Pattern: { } inner } unary } pattern
                when negated && unary.IsKind(SyntaxKind.NotPattern) && IsNullPattern(inner):
                return pattern.Expression;

            case IsPatternExpressionSyntax pattern when !negated && IsNullPattern(pattern.Pattern):
                return pattern.Expression;

            default:
                return null;
        }
    }

    static bool IsNullPattern(PatternSyntax pattern) =>
        pattern is ConstantPatternSyntax constant && IsNullLiteral(constant.Expression);

    static bool IsNullLiteral(ExpressionSyntax expression) => expression.IsKind(SyntaxKind.NullLiteralExpression);

    /// <summary>The receiver of an emptiness test, or of its negation.</summary>
    static ExpressionSyntax? EmptyOperand(
        SemanticModel model,
        ExpressionSyntax expression,
        bool negated,
        CancellationToken cancellation
    ) {
        if (expression is not BinaryExpressionSyntax binary) {
            return null;
        }

        var kind = binary.Kind();
        if (LengthReceiver(binary.Left) is { } left && IsZero(model, binary.Right, cancellation)) {
            return LengthComparison(kind, negated, false) ? left : null;
        }

        if (LengthReceiver(binary.Right) is { } right && IsZero(model, binary.Left, cancellation)) {
            return LengthComparison(kind, negated, true) ? right : null;
        }

        if (kind != (negated ? SyntaxKind.NotEqualsExpression : SyntaxKind.EqualsExpression)) {
            return null;
        }

        if (IsEmptyString(model, binary.Right, cancellation)) {
            return binary.Left;
        }

        return IsEmptyString(model, binary.Left, cancellation) ? binary.Right : null;
    }

    /// <summary>
    ///     Which <c>Length</c> comparisons mean "empty", and which mean "not empty".
    /// </summary>
    /// <remarks>
    ///     ⚠ <c>&gt;</c> and <c>&lt;</c> are direction-sensitive: <c>x.Length &gt; 0</c> and
    ///     <c>0 &lt; x.Length</c> both say "not empty", and reading either one the other way round
    ///     inverts the rule.
    /// </remarks>
    static bool LengthComparison(SyntaxKind kind, bool negated, bool zeroFirst) {
        if (!negated) {
            return kind == SyntaxKind.EqualsExpression;
        }

        return kind == SyntaxKind.NotEqualsExpression
            || kind == (zeroFirst ? SyntaxKind.LessThanExpression : SyntaxKind.GreaterThanExpression);
    }

    static ExpressionSyntax? LengthReceiver(ExpressionSyntax expression) =>
        expression is MemberAccessExpressionSyntax {
            RawKind: (int)SyntaxKind.SimpleMemberAccessExpression,
            Name.Identifier.ValueText: "Length"
        } access
            ? access.Expression
            : null;

    static bool IsZero(SemanticModel model, ExpressionSyntax expression, CancellationToken cancellation) =>
        model.GetConstantValue(expression, cancellation) is { HasValue: true, Value: int value } && value == 0;

    /// <summary>
    ///     ⚠ <c>string.Empty</c> is a static readonly field and not a constant, so the constant folder
    ///     never sees it. It is recognised by symbol instead.
    /// </summary>
    static bool IsEmptyString(SemanticModel model, ExpressionSyntax expression, CancellationToken cancellation) {
        if (model.GetConstantValue(expression, cancellation) is { HasValue: true, Value: string text }) {
            return text.Length == 0;
        }

        return model.GetSymbolInfo(expression, cancellation).Symbol
            is IFieldSymbol { Name: "Empty", IsStatic: true, ContainingType.SpecialType: SpecialType.System_String };
    }
}
