using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using Rikarin.Skala.Rules.Metadata;
using System.Collections.Immutable;

namespace Rikarin.Skala.Rules.Correctness;

/// <summary>
///     <c>SK2064</c> — <c>&amp;</c> or <c>|</c> between two booleans where <c>&amp;&amp;</c> or
///     <c>||</c> was meant.
/// </summary>
/// <remarks>
///     <c>if (node != null &amp; node.Ready)</c> evaluates the right half whichever way the null check
///     went, so the guard stops guarding and the <c>NullReferenceException</c> it was written to
///     prevent is thrown anyway. One character.
///     <para>
///         ⚠
///         <b>
///             The operand type decides everything, and getting it wrong would be catastrophic rather
///             than noisy.
///         </b> <c>flags &amp; Mask</c> on an integer or a <c>[Flags]</c> enum is the only
///         way to write that operation and has no <c>&amp;&amp;</c> form at all; reporting it would
///         make the rule actively harmful. Both operands must be exactly non-nullable
///         <c>System.Boolean</c>, which is the whole reason this rule is <c>Semantic</c>.
///     </para>
///     <para>
///         ⚠ <b><c>bool?</c> is a separate exclusion with a separate reason.</b> Lifted <c>&amp;</c>
///         and <c>|</c> on nullable booleans are three-valued — <c>null &amp; false</c> is
///         <c>false</c>, not <c>null</c> — and <c>&amp;&amp;</c>/<c>||</c> cannot express that. The
///         rewrite would change the answer, not merely the evaluation order.
///     </para>
///     <para>
///         ⚠ <b>A right operand with a side effect is deliberate.</b>
///         <c>
/// if (ValidateName(x) &amp;
///         ValidateAge(x))
///         </c> is written that way so both validators run and both messages are
///         collected; short-circuiting it deletes work. Only a right operand built from names,
///         member-access paths, literals and tests over them is reported.
///     </para>
///     <para>
///         ⚠
///         <b>
///             A mixed bitwise expression is declined, and this is the guard that keeps the fix
///             honest.
///         </b> <c>&amp;</c> and <c>|</c> bind tighter than <c>&amp;&amp;</c> and <c>||</c>, so
///         swapping one token inside <c>a | b &amp; c</c> turns <c>a | (b &amp; c)</c> into
///         <c>(a | b) &amp;&amp; c</c> — a different program, produced by a fix the catalogue calls
///         safe. When the node's parent or either of its operands is another <c>&amp;</c>, <c>|</c> or
///         <c>^</c>, the rule stays silent. Parentheses withdraw the objection, because they are what
///         pins the grouping.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class NonShortCircuitBooleanAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.NonShortCircuitBoolean);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.BitwiseAndExpression, SyntaxKind.BitwiseOrExpression);
    }

    static void Analyze(SyntaxNodeAnalysisContext context) {
        var binary = (BinaryExpressionSyntax)context.Node;
        if (binary.ContainsDiagnostics || binary.ContainsDirectives) {
            return;
        }

        if (context.SemanticModel.GetOperation(binary, context.CancellationToken) is not IBinaryOperation {
                OperatorMethod: null,
                IsLifted: false,
                Type.SpecialType: SpecialType.System_Boolean,
                LeftOperand.Type.SpecialType: SpecialType.System_Boolean,
                RightOperand.Type.SpecialType: SpecialType.System_Boolean
            }) {
            return;
        }

        if (!ExpressionIdentity.IsRepeatable(binary.Right)
            || IsBitwise(binary.Parent)
            || IsBitwise(binary.Left)
            || IsBitwise(binary.Right)) {
            return;
        }

        var replacement = binary.IsKind(SyntaxKind.BitwiseAndExpression) ? "&&" : "||";
        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                binary.OperatorToken.GetLocation(),
                FixEdits.Pack((binary.OperatorToken.Span, replacement)),
                "`"
                + binary.OperatorToken.ValueText
                + "` on booleans always evaluates the right operand; use `"
                + replacement
                + "`"
            )
        );
    }

    /// <summary>
    ///     ⚠ Not unwrapped through parentheses, deliberately: a parenthesised operand or parent has its
    ///     grouping written down, so the precedence change the fix would otherwise cause cannot happen.
    /// </summary>
    static bool IsBitwise(SyntaxNode? node) =>
        node is BinaryExpressionSyntax binary
        && (binary.IsKind(SyntaxKind.BitwiseAndExpression)
            || binary.IsKind(SyntaxKind.BitwiseOrExpression)
            || binary.IsKind(SyntaxKind.ExclusiveOrExpression));
}
