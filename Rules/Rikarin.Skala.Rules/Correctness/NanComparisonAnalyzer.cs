using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using Rikarin.Skala.Rules.Metadata;
using System.Collections.Immutable;

namespace Rikarin.Skala.Rules.Correctness;

/// <summary>
///     <c>SK2030</c> — equality against a constant <c>NaN</c>, which never answers its own question.
/// </summary>
/// <remarks>
///     NaN is unordered with every value including itself, so <c>x == double.NaN</c> is false even when
///     <c>x</c> is NaN. The comparison written to catch the bad case is the one shape guaranteed not to.
///     <para>
///         ⚠ <b><c>!=</c> is reported too, and it is a different defect.</b> <c>x != double.NaN</c> is
///         always <em>true</em>, not always false, so it silently passes rather than silently fails and
///         the message and the fix are both negated. Folding the two into one sentence would describe
///         one of them wrongly.
///     </para>
///     <para>
///         ⚠ <b>The indirection stops at <c>const</c>.</b> A <c>const double Missing = double.NaN;</c>
///         is the same defect one name away and the semantic model already folds it, so following it
///         costs nothing. A <c>static readonly double</c> initialised from <c>double.NaN</c> is the same
///         defect one <em>assignment</em> away, and proving that needs the field's initialiser and the
///         absence of any other writer — dataflow this rule deliberately does not do.
///     </para>
///     <para>
///         ⚠ <b>Disjoint from <c>SK2003</c> by construction.</b> <c>SK2003</c> excludes comparisons
///         whose constant side is a sentinel, and NaN is one of the sentinels it names, so the floating
///         point equality rule is already silent on exactly the shapes this one reports.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class NanComparisonAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.NanComparison);

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

        // ⚠ A lifted comparison over `double?` has different semantics on the null operand, and a
        // user-defined `operator ==` is a method call whose answer is not the IEEE one. Neither is
        // rewritable into `IsNaN`, so neither is reported.
        if (model.GetOperation(binary, cancellation) is not IBinaryOperation {
                OperatorMethod: null,
                IsLifted: false
            } operation
            || !IsFloating(operation.LeftOperand.Type)
            || !IsFloating(operation.RightOperand.Type)) {
            return;
        }

        var leftIsNan = IsNan(model.GetConstantValue(binary.Left, cancellation));
        var rightIsNan = IsNan(model.GetConstantValue(binary.Right, cancellation));

        // Neither side is NaN, or both are. `double.NaN == double.NaN` is degenerate: there is no
        // operand left to test, so there is nothing to rewrite it into.
        if (leftIsNan == rightIsNan) {
            return;
        }

        var operand = leftIsNan ? binary.Right : binary.Left;

        // ⚠ The operand's *own* type, before the conversion the comparison introduced. An `int`
        // widened to `double` and compared with NaN is a different mistake, and `double.IsNaN(i)`
        // would be just as constantly false as the code it replaced — a fix that changes nothing.
        var type = model.GetTypeInfo(operand, cancellation).Type?.SpecialType;
        if (type is not (SpecialType.System_Single or SpecialType.System_Double)) {
            return;
        }

        var negated = binary.IsKind(SyntaxKind.NotEqualsExpression);
        var call = (type == SpecialType.System_Single ? "float" : "double") + ".IsNaN(" + operand + ")";
        var fix = FixEdits.Pack((binary.Span, negated ? "!" + call : call));

        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                binary.GetLocation(),
                fix,
                negated
                    ? "Comparing with NaN using `!=` is always true, even for NaN; use `!"
                    + call
                    + "`"
                    : "Comparing with NaN using `==` is always false, even for NaN; use `" + call + "`"
            )
        );
    }

    static bool IsFloating(ITypeSymbol? type) =>
        type?.SpecialType is SpecialType.System_Single or SpecialType.System_Double;

    static bool IsNan(Optional<object?> constant) =>
        constant.HasValue
        && constant.Value switch {
            double value => double.IsNaN(value),
            float value => float.IsNaN(value),
            _ => false
        };
}
