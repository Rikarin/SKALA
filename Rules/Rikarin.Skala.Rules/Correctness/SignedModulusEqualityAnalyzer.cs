using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using Rikarin.Skala.Rules.Metadata;
using System.Collections.Immutable;

namespace Rikarin.Skala.Rules.Correctness;

/// <summary>SK2054: a signed remainder compared for equality against a non-zero value.</summary>
/// <remarks>
///     ⚠ C# gives <c>%</c> the sign of the dividend, so <c>-5 % 2</c> is <c>-1</c> and
///     <c>value % 2 == 1</c> is false for every negative odd value. ⚠ <c>value % 2 == 0</c> is the one
///     spelling that is right, because zero has no sign — a rule that reported it would be wrong about
///     the correct code and right about the broken code, which is worse than not shipping.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class SignedModulusEqualityAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.SignedModulusEquality);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.EqualsExpression, SyntaxKind.NotEqualsExpression);
    }

    static void Analyze(SyntaxNodeAnalysisContext context) {
        var binary = (BinaryExpressionSyntax)context.Node;
        var model = context.SemanticModel;
        var cancellation = context.CancellationToken;
        if (model.GetOperation(binary, cancellation) is not IBinaryOperation {
                OperatorMethod: null,
                IsLifted: false
            }) {
            return;
        }

        var modulus = Modulus(binary.Left);
        if (!IntegralDomain.TryConstant(model, binary.Right, cancellation, out var expected)) {
            modulus = Modulus(binary.Right);
            if (!IntegralDomain.TryConstant(model, binary.Left, cancellation, out expected)) {
                return;
            }
        }

        // ⚠ The whole rule turns on this: zero is the sign-free remainder, so `% 2 == 0` is correct
        // for both signs and must never be reported.
        if (modulus is null || expected == 0) {
            return;
        }

        if (model.GetOperation(modulus, cancellation) is not IBinaryOperation {
                OperatorKind: BinaryOperatorKind.Remainder,
                OperatorMethod: null,
                IsLifted: false
            } operation) {
            return;
        }

        // A remainder in an unsigned type cannot be negative, and neither can one whose dividend is
        // already proven non-negative — an unsigned dividend promoted to `int`, a count, a length,
        // a non-negative constant or Math.Abs.
        if (!IntegralDomain.TryGet(operation.Type, out var minimum, out _)
            || minimum >= 0
            || NonNegativeIntegral.IsProvenNonNegative(operation.LeftOperand)) {
            return;
        }

        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                binary.OperatorToken.GetLocation(),
                "'"
                + modulus
                + "' keeps the sign of its left operand, so this test is decided by that sign as well "
                + "as by the remainder"
            )
        );
    }

    /// <summary>The <c>%</c> under any number of parentheses, or null when the operand is not one.</summary>
    static ExpressionSyntax? Modulus(ExpressionSyntax expression) {
        while (expression is ParenthesizedExpressionSyntax parenthesized) {
            expression = parenthesized.Expression;
        }

        return expression.IsKind(SyntaxKind.ModuloExpression) ? expression : null;
    }
}
