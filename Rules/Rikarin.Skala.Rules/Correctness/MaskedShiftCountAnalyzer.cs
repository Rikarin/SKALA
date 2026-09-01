using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using Rikarin.Skala.Rules.Metadata;
using System.Collections.Immutable;
using System.Globalization;

namespace Rikarin.Skala.Rules.Correctness;

/// <summary>SK2052: a shift count C# masks rather than honours.</summary>
/// <remarks>
///     ⚠ <c>x &lt;&lt; 32</c> on an <c>int</c> shifts by <c>32 &amp; 31</c>, which is zero — the
///     expression is the identity, not the zero it reads as. The same text on a <c>long</c> masks with
///     63 and is a real 32-bit shift, so nothing about this can be decided from syntax: the promoted
///     width of the left operand is the whole rule, and a <c>byte</c> left operand promotes to
///     <c>int</c> and masks with 31.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class MaskedShiftCountAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.MaskedShiftCount);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(
            Analyze,
            SyntaxKind.LeftShiftExpression,
            SyntaxKind.RightShiftExpression,
            SyntaxKind.UnsignedRightShiftExpression
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

        // The shift's own type is the promoted type of its left operand, which is where the mask
        // comes from. ⚠ nint/nuint are absent on purpose: their mask is 31 or 63 depending on the
        // process, so no single answer about the same source is right.
        var width = model.GetTypeInfo(binary, cancellation).Type?.SpecialType switch {
            SpecialType.System_Int32 or SpecialType.System_UInt32 => 32,
            SpecialType.System_Int64 or SpecialType.System_UInt64 => 64,
            _ => 0
        };
        if (width == 0) {
            return;
        }

        var operator_ = binary.OperatorToken.ValueText;
        if (!IntegralDomain.TryConstant(model, binary.Right, cancellation, out var written)) {
            // Nothing constant about the count, so the only decidable case left is a dividend of
            // zero, which no count can change.
            if (IntegralDomain.TryConstant(model, binary.Left, cancellation, out var left) && left == 0) {
                context.ReportDiagnostic(
                    Diagnostic.Create(
                        Descriptor,
                        binary.OperatorToken.GetLocation(),
                        "This '" + operator_ + "' is always zero: shifting zero by any count leaves zero"
                    )
                );
            }

            return;
        }

        var count = (int)written;
        var real = count & (width - 1);
        if (real == count) {
            return;
        }

        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                binary.OperatorToken.GetLocation(),
                "This '"
                + operator_
                + "' is written with a count of "
                + count.ToString(CultureInfo.InvariantCulture)
                + " and shifts by "
                + real.ToString(CultureInfo.InvariantCulture)
                + ": C# masks the count to the operand's "
                + width.ToString(CultureInfo.InvariantCulture)
                + "-bit width"
                + (real == 0 ? ", so the expression is its own left operand" : string.Empty)
            )
        );
    }
}
