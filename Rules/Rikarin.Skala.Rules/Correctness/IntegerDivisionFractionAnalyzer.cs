using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using Rikarin.Skala.Rules.Metadata;
using System.Collections.Immutable;
using System.Threading;

namespace Rikarin.Skala.Rules.Correctness;

/// <summary>SK2050: an integral division whose result is then asked for as a fractional type.</summary>
/// <remarks>
///     ⚠ Deliberately independent of the <c>checked</c> context. Nothing here overflows: the fraction
///     is discarded inside the division operator, and it is discarded identically whether the project
///     sets <c>CheckForOverflowUnderflow</c> or not — so the rule never has to read a setting it may
///     not be able to see.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class IntegerDivisionFractionAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.IntegerDivisionFraction);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.DivideExpression);
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

        var info = model.GetTypeInfo(binary, cancellation);
        if (!IntegralDomain.TryGet(info.Type, out _, out _)) {
            return;
        }

        // ⚠ An explicit cast is not an implicit conversion and Roslyn does not pretend otherwise:
        // `GetTypeInfo((double)(a / b)).ConvertedType` on the *division* is `int`, not `double`, so
        // the conversion path alone misses the single most common wrong repair for this defect.
        // Refuted while building the rule, and the enclosing cast is read separately because of it.
        var converted = IsFractional(info.ConvertedType)
            ? info.ConvertedType
            : EnclosingCast(model, binary, cancellation);
        if (converted is null) {
            return;
        }

        // ⚠ Not every integral division discards something. Dividing by one — or dividing a constant
        // zero — is exact, so the shape is present and there is no fraction to lose. Reporting those
        // would be the rule firing on its own premise rather than on a defect, and `x / 1` under a
        // `double` target is exactly the overlap where SK2051 already has the useful thing to say.
        if (IntegralDomain.TryConstant(model, binary.Right, cancellation, out var divisor)
            && (divisor == 1 || divisor == -1)) {
            return;
        }

        if (IntegralDomain.TryConstant(model, binary.Left, cancellation, out var dividend) && dividend == 0) {
            return;
        }

        var target = converted.ToDisplayString();
        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                binary.OperatorToken.GetLocation(),
                FixEdits.Pack((binary.Left.Span, Cast(target, binary.Left))),
                "This division is evaluated on "
                + info.Type!.ToDisplayString()
                + ", so the fractional part is discarded before the result becomes "
                + target
            )
        );
    }

    /// <summary>
    ///     The fractional type of the cast written immediately around the division, or null. ⚠ Only
    ///     the innermost cast counts: in <c>(double)(int)(a / b)</c> the author wrote the truncation
    ///     down, and that is the documented way to keep the shape and silence the rule.
    /// </summary>
    static ITypeSymbol? EnclosingCast(SemanticModel model, ExpressionSyntax division, CancellationToken cancellation) {
        SyntaxNode? parent = division.Parent;
        while (parent is ParenthesizedExpressionSyntax) {
            parent = parent.Parent;
        }

        if (parent is not CastExpressionSyntax cast) {
            return null;
        }

        var type = model.GetTypeInfo(cast.Type, cancellation).Type;
        return IsFractional(type) ? type : null;
    }

    /// <summary>
    ///     ⚠ Only <c>float</c>, <c>double</c> and <c>decimal</c>. A conversion to a wider integral type
    ///     is not evidence that anybody wanted a fraction.
    /// </summary>
    static bool IsFractional(ITypeSymbol? type) =>
        type?.SpecialType is SpecialType.System_Single
            or SpecialType.System_Double
            or SpecialType.System_Decimal;

    /// <summary>
    ///     The cast binds tighter than <c>/</c>, so only the dividend needs it — but an operand that is
    ///     not already a primary expression needs its own parentheses first.
    /// </summary>
    static string Cast(string target, ExpressionSyntax left) =>
        left is IdentifierNameSyntax
            or MemberAccessExpressionSyntax
            or InvocationExpressionSyntax
            or ElementAccessExpressionSyntax
            or LiteralExpressionSyntax
            or ParenthesizedExpressionSyntax
            or CastExpressionSyntax
            ? "(" + target + ")" + left.ToString()
            : "(" + target + ")(" + left + ")";
}
