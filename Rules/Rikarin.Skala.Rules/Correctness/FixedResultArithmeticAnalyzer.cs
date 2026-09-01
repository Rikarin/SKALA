using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using Rikarin.Skala.Rules.Metadata;
using System.Collections.Immutable;
using System.Globalization;

namespace Rikarin.Skala.Rules.Correctness;

/// <summary>SK2051: built-in integral arithmetic a constant operand has already decided.</summary>
/// <remarks>
///     ⚠ Integral only. <c>x + 0.0</c> looks like the same shape and is not the identity: it turns
///     negative zero into positive zero, so the "obvious" generalisation to floating point would ship a
///     wrong fix. ⚠ Nothing here depends on the <c>checked</c> context either — an identity cannot
///     overflow, and neither can a result that is a constant.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class FixedResultArithmeticAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.FixedResultArithmetic);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(
            Analyze,
            SyntaxKind.AddExpression,
            SyntaxKind.SubtractExpression,
            SyntaxKind.MultiplyExpression,
            SyntaxKind.DivideExpression,
            SyntaxKind.ModuloExpression,
            SyntaxKind.BitwiseAndExpression,
            SyntaxKind.BitwiseOrExpression,
            SyntaxKind.ExclusiveOrExpression
        );
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

        var result = model.GetTypeInfo(binary, cancellation).Type;
        if (!IntegralDomain.TryGet(result, out var minimum, out var maximum)) {
            return;
        }

        // The constant may sit on either side; the other side is the value that survives, and its
        // type has to be the operation's own so that removing the operator cannot change what a
        // `var` target binds to.
        var constantIsOnTheRight = IntegralDomain.TryConstant(model, binary.Right, cancellation, out var constant);
        var value = constantIsOnTheRight ? binary.Left : binary.Right;
        if (!constantIsOnTheRight && !IntegralDomain.TryConstant(model, binary.Left, cancellation, out constant)) {
            return;
        }

        if (model.GetConstantValue(value, cancellation).HasValue
            || !SymbolEqualityComparer.Default.Equals(model.GetTypeInfo(value, cancellation).Type, result)) {
            return;
        }

        var allOnes = minimum < 0 ? -1 : maximum;
        var outcome = Outcome(binary.Kind(), constant, constantIsOnTheRight, allOnes);
        if (outcome is null) {
            return;
        }

        if (outcome == Identity) {
            Report(context, binary, "has no effect", (binary.Span, value.ToString()));
            return;
        }

        // ⚠ A constant result discards the other operand's evaluation, so the fix may only be offered
        // where that evaluation provably has none. Anything else is left alone rather than reported
        // without a fix: doc 08's round trip requires every positive finding of a fixable rule to
        // carry edits.
        if (model.GetOperation(value, cancellation) is ILocalReferenceOperation
            or IParameterReferenceOperation
            or IFieldReferenceOperation { Field.IsVolatile: false }) {
            Report(
                context,
                binary,
                "always produces " + outcome.Value.ToString(CultureInfo.InvariantCulture),
                (binary.Span, outcome.Value.ToString(CultureInfo.InvariantCulture))
            );
        }
    }

    /// <summary>The sentinel for "the surviving operand is the answer".</summary>
    const decimal Identity = decimal.MinValue;

    static decimal? Outcome(SyntaxKind kind, decimal constant, bool onTheRight, decimal allOnes) =>
        kind switch {
            SyntaxKind.AddExpression when constant == 0 => Identity,
            // ⚠ `0 - x` is negation. Only a zero on the right is the identity.
            SyntaxKind.SubtractExpression when constant == 0 && onTheRight => Identity,
            SyntaxKind.MultiplyExpression when constant == 1 => Identity,
            SyntaxKind.MultiplyExpression when constant == 0 => 0,
            SyntaxKind.DivideExpression when constant == 1 && onTheRight => Identity,
            SyntaxKind.ModuloExpression when constant == 1 && onTheRight => 0,
            SyntaxKind.BitwiseAndExpression when constant == allOnes => Identity,
            SyntaxKind.BitwiseAndExpression when constant == 0 => 0,
            SyntaxKind.BitwiseOrExpression when constant == 0 => Identity,
            SyntaxKind.BitwiseOrExpression when constant == allOnes => allOnes,
            SyntaxKind.ExclusiveOrExpression when constant == 0 => Identity,
            _ => null
        };

    static void Report(
        SyntaxNodeAnalysisContext context,
        BinaryExpressionSyntax binary,
        string outcome,
        (Microsoft.CodeAnalysis.Text.TextSpan Span, string Text) edit
    ) =>
        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                binary.OperatorToken.GetLocation(),
                FixEdits.Pack(edit),
                "This '" + binary.OperatorToken.ValueText + "' " + outcome + ": the constant operand decides it"
            )
        );
}
