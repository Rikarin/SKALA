using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Rules.Metadata;
using Rikarin.Skala.Rules.Modernization;
using System.Collections.Immutable;

namespace Rikarin.Skala.Rules.Cleanup;

/// <summary><c>SK0260</c> — a boolean expression that restates its own operand.</summary>
/// <remarks>
///     <para>
///         Five shapes, one concept: a comparison against a boolean literal, a double negation, a
///         negated equality, a conditional whose two branches are the boolean literals, and a
///         <c>&amp;&amp;</c>/<c>||</c> operand that cannot change the result.
///     </para>
///     <para>
///         ⚠ <b><c>bool?</c> is what this rule is written around, and it defeats all five shapes.</b>
///         <c>maybe == true</c> is not <c>maybe</c> when <c>maybe</c> is <c>bool?</c>: the comparison is
///         three-valued and answers <c>false</c> for <c>null</c>, so the rewrite either stops compiling
///         or silently takes the other branch. Every shape therefore asks for
///         <see cref="SpecialType.System_Boolean" /> on the operand it reads, and
///         <c>Nullable&lt;bool&gt;</c> reports <see cref="SpecialType.System_Nullable_T" /> — it is
///         declined by the type question itself rather than by a special case beside it.
///     </para>
///     <para>
///         ⚠ <b>A user-defined <c>==</c> and a user-defined <c>!=</c> need not be each other's
///         negation.</b> Nothing in the language requires the pair to agree, so <c>!(a == b)</c> is
///         <em>not</em> <c>a != b</c> for a type that overloads them; the negated-equality shape is
///         reported only when the comparison binds to <see cref="MethodKind.BuiltinOperator" />. A type
///         whose two operators do happen to agree is still declined, because whether they agree is not
///         a question the written shape answers.
///     </para>
///     <para>
///         ⚠ <b>A <c>null</c> operand belongs to <c>SK1010</c> and is handed to it.</b>
///         <c>!(x == null)</c> is that rule's <c>x is not null</c>, and reporting the outer <c>!</c>
///         here as well would put two ids on one line — the collision <c>SK0244</c> records against
///         <c>SK6023</c>, avoided here by reading the neighbour before allocating rather than after.
///     </para>
///     <para>
///         ⚠ <b><c>x &amp;&amp; false</c> is deliberately absent.</b> The operand that cannot change the
///         result is <c>&amp;&amp; true</c> and <c>|| false</c> and only those. Deleting the other side
///         drops an evaluation, which is a behaviour change wearing a redundancy's clothes — and where
///         the dropped side has an effect it is a defect that deserves its own finding, not a cleanup.
///     </para>
///     <para>
///         ⚠ The comment guard asks about the span the fix <em>replaces</em>, never the node's leading
///         trivia — the defect #302 describes and <c>SK0240</c> paid for twice.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class RedundantBooleanExpressionAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.RedundantBooleanExpression);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(
            AnalyzeComparison,
            SyntaxKind.EqualsExpression,
            SyntaxKind.NotEqualsExpression
        );
        context.RegisterSyntaxNodeAction(AnalyzeNegation, SyntaxKind.LogicalNotExpression);
        context.RegisterSyntaxNodeAction(AnalyzeConditional, SyntaxKind.ConditionalExpression);
        context.RegisterSyntaxNodeAction(
            AnalyzeLogical,
            SyntaxKind.LogicalAndExpression,
            SyntaxKind.LogicalOrExpression
        );
    }

    /// <summary><c>ready == true</c>, <c>ready != false</c> → <c>ready</c>; the other two → <c>!ready</c>.</summary>
    /// <remarks>
    ///     ⚠ Exactly one side may be the literal. <c>true == false</c> is a constant this rule has
    ///     nothing to say about, and reading it as "the left operand is redundant" would report a
    ///     deletion that leaves the other literal behind.
    ///     <para>
    ///         The replacement never needs parentheses of its own: the operand of an <c>==</c> already
    ///         binds at least as tightly as the <c>==</c> it is replacing, so whatever the parent
    ///         expected, a tighter expression satisfies it.
    ///     </para>
    /// </remarks>
    static void AnalyzeComparison(SyntaxNodeAnalysisContext context) {
        var binary = (BinaryExpressionSyntax)context.Node;
        var left = AsBooleanLiteral(binary.Left);
        var right = AsBooleanLiteral(binary.Right);
        if (left is null == right is null) {
            return;
        }

        var literal = (left ?? right)!.Value;
        var operand = left is null ? binary.Left : binary.Right;
        if (!IsPlainBoolean(context, operand) || !Replaceable(binary)) {
            return;
        }

        // `== true` and `!= false` keep the operand; `== false` and `!= true` negate it.
        var keep = binary.IsKind(SyntaxKind.EqualsExpression) == literal;
        var replacement = keep ? operand.ToString() : Negated(context, operand);
        Report(
            context,
            binary,
            replacement,
            $"`{binary.OperatorToken.ValueText} {(literal ? "true" : "false")}` restates the operand, which is "
            + "already a `bool`"
        );
    }

    /// <summary><c>!!ready</c> → <c>ready</c>, and <c>!(a == b)</c> → <c>a != b</c>.</summary>
    /// <remarks>
    ///     ⚠ <b>Only the outermost <c>!</c> of a run is reported.</b> In <c>!!!ready</c> the middle
    ///     <c>!</c> also has a <c>!</c> for an operand and would report the same run a second time, so
    ///     the fix would be offered twice over overlapping spans and one pass would leave a finding on
    ///     its own output. A <c>!</c> whose parent is a <c>!</c> stands down.
    /// </remarks>
    static void AnalyzeNegation(SyntaxNodeAnalysisContext context) {
        var negation = (PrefixUnaryExpressionSyntax)context.Node;
        if (negation.Parent.IsKind(SyntaxKind.LogicalNotExpression) || !Replaceable(negation)) {
            return;
        }

        if (negation.Operand is PrefixUnaryExpressionSyntax { RawKind: (int)SyntaxKind.LogicalNotExpression } inner) {
            if (IsPlainBoolean(context, inner.Operand)) {
                Report(
                    context,
                    negation,
                    inner.Operand.ToString(),
                    "the two `!` cancel, so the expression is its own operand"
                );
            }

            return;
        }

        AnalyzeNegatedEquality(context, negation);
    }

    /// <summary><c>!(a == b)</c> → <c>a != b</c>.</summary>
    /// <remarks>
    ///     ⚠ Parenthesised only — <c>!a == b</c> parses as <c>(!a) == b</c> and is a different program,
    ///     so the shape cannot occur without the parentheses.
    ///     <para>
    ///         ⚠ <b>The fix is withdrawn where <c>!=</c> could not stand unparenthesised.</b> Under an
    ///         operator that binds tighter than equality the replacement would have to carry
    ///         parentheses of its own, and <c>SK0209</c> reports exactly those as redundant — two rules
    ///         undoing each other is a <c>skala fix</c> that does not terminate. The finding is dropped
    ///         instead of rewritten.
    ///     </para>
    /// </remarks>
    static void AnalyzeNegatedEquality(SyntaxNodeAnalysisContext context, PrefixUnaryExpressionSyntax negation) {
        if (negation.Operand is not ParenthesizedExpressionSyntax { Expression: BinaryExpressionSyntax comparison }
            || !comparison.IsKind(SyntaxKind.EqualsExpression) && !comparison.IsKind(SyntaxKind.NotEqualsExpression)) {
            return;
        }

        // ⚠ `SK1010` owns `== null`; see the type remarks. Reporting it here too is one span, two ids.
        if (comparison.Left.IsKind(SyntaxKind.NullLiteralExpression)
            || comparison.Right.IsKind(SyntaxKind.NullLiteralExpression)) {
            return;
        }

        if (context.SemanticModel.GetSymbolInfo(comparison, context.CancellationToken).Symbol
            is not IMethodSymbol { MethodKind: MethodKind.BuiltinOperator }) {
            return;
        }

        if (BindsTighterThanEquality(negation.Parent)) {
            return;
        }

        var flipped = comparison.IsKind(SyntaxKind.EqualsExpression) ? "!=" : "==";
        Report(
            context,
            negation,
            comparison.Left + " " + flipped + " " + comparison.Right,
            $"the `!` inverts a comparison the language spells `{flipped}`"
        );
    }

    /// <summary><c>found ? true : false</c> → <c>found</c>; the reverse order → <c>!found</c>.</summary>
    /// <remarks>
    ///     ⚠ The condition's type is asked rather than assumed. A type with an <c>operator true</c> may
    ///     stand as the condition of a <c>?:</c> without being a <c>bool</c>, and replacing the
    ///     conditional with it would change the expression's type from <c>bool</c> to that type.
    /// </remarks>
    static void AnalyzeConditional(SyntaxNodeAnalysisContext context) {
        var conditional = (ConditionalExpressionSyntax)context.Node;
        if (AsBooleanLiteral(conditional.WhenTrue) is not { } whenTrue
            || AsBooleanLiteral(conditional.WhenFalse) is not { } whenFalse
            || whenTrue == whenFalse
            || !IsPlainBoolean(context, conditional.Condition)
            || !Replaceable(conditional)) {
            return;
        }

        Report(
            context,
            conditional,
            whenTrue ? conditional.Condition.ToString() : Negated(context, conditional.Condition),
            "the branches are the two `bool` literals, so the conditional is its own condition"
        );
    }

    /// <summary><c>ready &amp;&amp; true</c> and <c>ready || false</c> → <c>ready</c>.</summary>
    /// <remarks>
    ///     ⚠ Only the operand that cannot change the result. <c>ready &amp;&amp; false</c> would delete
    ///     <c>ready</c>'s evaluation along with it, which is not this concept — see the type remarks.
    /// </remarks>
    static void AnalyzeLogical(SyntaxNodeAnalysisContext context) {
        var binary = (BinaryExpressionSyntax)context.Node;

        // `&&` is neutral in `true`, `||` is neutral in `false`.
        var neutral = binary.IsKind(SyntaxKind.LogicalAndExpression);
        var left = AsBooleanLiteral(binary.Left);
        var right = AsBooleanLiteral(binary.Right);
        if (left is null == right is null) {
            return;
        }

        if ((left ?? right) != neutral) {
            return;
        }

        var operand = left is null ? binary.Left : binary.Right;
        if (!IsPlainBoolean(context, operand) || !Replaceable(binary)) {
            return;
        }

        Report(
            context,
            binary,
            operand.ToString(),
            $"`{binary.OperatorToken.ValueText} {(neutral ? "true" : "false")}` cannot change the result"
        );
    }

    static void Report(SyntaxNodeAnalysisContext context, ExpressionSyntax node, string replacement, string what) =>
        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                node.GetLocation(),
                FixEdits.Pack((node.Span, replacement)),
                what
            )
        );

    /// <summary>The literal's value, or <c>null</c> when the expression is not a boolean literal.</summary>
    static bool? AsBooleanLiteral(ExpressionSyntax expression) =>
        expression switch {
            LiteralExpressionSyntax { RawKind: (int)SyntaxKind.TrueLiteralExpression } => true,
            LiteralExpressionSyntax { RawKind: (int)SyntaxKind.FalseLiteralExpression } => false,
            _ => null
        };

    /// <summary>Whether the expression's type is exactly <c>bool</c>.</summary>
    /// <remarks>
    ///     ⚠ <c>bool?</c> answers <see cref="SpecialType.System_Nullable_T" /> and is refused here, which
    ///     is the whole false-positive story of this rule in one comparison. An error type is refused
    ///     too: a rule running under <c>--load=loose</c> sees plenty of them, and a shape that cannot
    ///     bind is a shape nothing is known about.
    /// </remarks>
    static bool IsPlainBoolean(SyntaxNodeAnalysisContext context, ExpressionSyntax expression) =>
        context.SemanticModel.GetTypeInfo(expression, context.CancellationToken).Type
            is { SpecialType: SpecialType.System_Boolean };

    /// <summary>Whether the node's own span is free of the trivia a replacement would delete.</summary>
    static bool Replaceable(ExpressionSyntax node) =>
        !RewriteGuards.ContainsCommentOrDirective(node.SyntaxTree, node.Span);

    /// <summary>The negation of an expression, as text.</summary>
    /// <remarks>
    ///     ⚠ <b>An equality operand is flipped rather than wrapped, and that is a termination
    ///     requirement rather than a nicety.</b> Writing <c>flag == false</c> as <c>!(a == b)</c> would
    ///     hand this rule's own negated-equality shape a finding on the fix's output, so one
    ///     <c>skala fix</c> pass would not settle — the defect <c>SK0240</c> records for its composite
    ///     <c>try</c> edit, in a different rule. The flip asks the same two questions that shape asks,
    ///     for the same reasons: a user-defined <c>==</c> need not be the negation of its <c>!=</c>, and
    ///     a <c>null</c> operand belongs to <c>SK1010</c>.
    ///     <para>
    ///         The flipped text never needs parentheses here: it replaces a node that was itself at
    ///         equality precedence (the comparison shape) or at conditional precedence (the
    ///         <c>?:</c> shape), and equality binds at least as tightly as both.
    ///     </para>
    ///     <para>
    ///         Otherwise <c>!</c> is prefixed. <c>!</c> is unary, so the result never needs parentheses
    ///         of its own whatever the parent is; the parentheses that can be needed are the
    ///         <em>inner</em> ones, because <c>!a &amp;&amp; b</c> is not <c>!(a &amp;&amp; b)</c>.
    ///     </para>
    /// </remarks>
    static string Negated(SyntaxNodeAnalysisContext context, ExpressionSyntax expression) {
        if (Unwrap(expression) is BinaryExpressionSyntax comparison
            && (comparison.IsKind(SyntaxKind.EqualsExpression) || comparison.IsKind(SyntaxKind.NotEqualsExpression))
            && !comparison.Left.IsKind(SyntaxKind.NullLiteralExpression)
            && !comparison.Right.IsKind(SyntaxKind.NullLiteralExpression)
            && context.SemanticModel.GetSymbolInfo(comparison, context.CancellationToken).Symbol
                is IMethodSymbol { MethodKind: MethodKind.BuiltinOperator }) {
            var flipped = comparison.IsKind(SyntaxKind.EqualsExpression) ? "!=" : "==";

            return comparison.Left + " " + flipped + " " + comparison.Right;
        }

        return Prefixed(expression);
    }

    /// <summary>An expression with any number of enclosing parentheses removed.</summary>
    static ExpressionSyntax Unwrap(ExpressionSyntax expression) {
        while (expression is ParenthesizedExpressionSyntax parenthesized) {
            expression = parenthesized.Expression;
        }

        return expression;
    }

    static string Prefixed(ExpressionSyntax expression) =>
        expression is IdentifierNameSyntax
            or MemberAccessExpressionSyntax
            or InvocationExpressionSyntax
            or ElementAccessExpressionSyntax
            or ParenthesizedExpressionSyntax
            or ThisExpressionSyntax
            or LiteralExpressionSyntax
            or PrefixUnaryExpressionSyntax
            or PostfixUnaryExpressionSyntax
            or CastExpressionSyntax
            ? "!" + expression
            : "!(" + expression + ")";

    /// <summary>Whether the parent would re-bind an unparenthesised equality placed under it.</summary>
    /// <remarks>
    ///     Tighter than equality: everything primary and unary, and every binary operator except the
    ///     six that are looser — <c>&amp;</c>, <c>^</c>, <c>|</c>, <c>&amp;&amp;</c>, <c>||</c> and
    ///     <c>??</c>. <c>is</c> and <c>as</c> sit at relational precedence, which is tighter, so an
    ///     <c>is</c> pattern parent counts too.
    /// </remarks>
    static bool BindsTighterThanEquality(SyntaxNode? parent) =>
        parent switch {
            BinaryExpressionSyntax binary => !binary.IsKind(SyntaxKind.BitwiseAndExpression)
                && !binary.IsKind(SyntaxKind.BitwiseOrExpression)
                && !binary.IsKind(SyntaxKind.ExclusiveOrExpression)
                && !binary.IsKind(SyntaxKind.LogicalAndExpression)
                && !binary.IsKind(SyntaxKind.LogicalOrExpression)
                && !binary.IsKind(SyntaxKind.CoalesceExpression),
            MemberAccessExpressionSyntax => true,
            ConditionalAccessExpressionSyntax => true,
            ElementAccessExpressionSyntax => true,
            CastExpressionSyntax => true,
            PrefixUnaryExpressionSyntax => true,
            PostfixUnaryExpressionSyntax => true,
            RangeExpressionSyntax => true,
            IsPatternExpressionSyntax => true,
            _ => false
        };
}
