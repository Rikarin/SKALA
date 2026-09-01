using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using Rikarin.Skala.Rules.Metadata;
using System.Collections.Immutable;

namespace Rikarin.Skala.Rules.Correctness;

/// <summary>
///     <c>SK2061</c> — the same expression on both sides of an operator, so the operator answers a
///     question about nothing.
/// </summary>
/// <remarks>
///     <c>a &amp;&amp; a</c>, <c>offset - offset</c>, <c>bits ^ bits</c>. One side was meant to name
///     something else, the compiler folds the result to a constant, and the check that was written to
///     catch a case cannot catch it.
///     <para>
///         ⚠
///         <b>
///             The six comparison operators are NOT examined, because <c>csc</c> already reports
///             every one of them.
///         </b> The rule's first draft did examine them, and measuring
///         <c>CS1718</c> rather than remembering doc 08's sentence about it disposed of that half
///         entirely: <c>q == q</c>, <c>q &lt; q</c>, <c>this.g == this.g</c>, <c>b.v == b.v</c>,
///         <c>Box.Which == Box.Which</c>, <c>a == a</c> on a string and <c>b == b</c> on a reference
///         all produce <c>CS1718</c>, on by default. The only comparison shape the compiler leaves
///         silent is a property access — and <c>SK2012</c> already owns that with a proof about the
///         accessors. There was nothing left over.
///         <see cref="Tests.ExpressionMisreadingBatchTests" /> pins the boundary.
///     </para>
///     <para>
///         ⚠ <b>Floating point is excluded outright, and that exclusion is why the rule is semantic.</b>
///         <c>x - x</c> on a <c>double</c> is a NaN-preserving zero rather than a constant nothing:
///         it is <c>0</c> for every finite value and NaN for NaN and the infinities, which is a real
///         technique and is textually identical to the defect. <c>double</c>, <c>float</c> and
///         <c>decimal</c> therefore never reach the report.
///     </para>
///     <para>
///         ⚠ <b>Only storage paths, never accessors or calls.</b> <c>reader.Read() - reader.Read()</c>
///         is two reads, not one expression twice. And a property is a call whose two evaluations this
///         rule cannot prove equal.
///     </para>
///     <para>
///         ⚠ <b><c>+</c>, <c>*</c>, <c>&lt;&lt;</c> and <c>&gt;&gt;</c> are not examined.</b> Doubling,
///         squaring and shifting a value by itself are ordinary arithmetic, and the family of operators
///         where the two sides being equal is <em>always</em> a mistake is the one listed below.
///         <c>=</c> is not examined either: the compiler's own <c>CS1717</c> already says it.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class IdenticalOperandsAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.IdenticalOperands);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(
            Analyze,
            SyntaxKind.LogicalAndExpression,
            SyntaxKind.LogicalOrExpression,
            SyntaxKind.BitwiseAndExpression,
            SyntaxKind.BitwiseOrExpression,
            SyntaxKind.ExclusiveOrExpression,
            SyntaxKind.SubtractExpression,
            SyntaxKind.DivideExpression,
            SyntaxKind.ModuloExpression
        );
    }

    static void Analyze(SyntaxNodeAnalysisContext context) {
        var binary = (BinaryExpressionSyntax)context.Node;
        var model = context.SemanticModel;
        var cancel = context.CancellationToken;

        if (binary.ContainsDiagnostics || binary.ContainsDirectives) {
            return;
        }

        // ⚠ A user-defined operator is a method call: it is entitled to answer anything, including
        // something useful, for two equal operands. A lifted operation over nullable operands has
        // its own three-valued answer. Neither is the operation this rule reasons about.
        //
        // ⚠ `string == string` is NOT user-defined in Roslyn's model — `OperatorMethod` is null for
        // it — which the first draft got wrong and a fixture caught. It no longer matters here,
        // because the comparison operators went to `CS1718`; the note stays because the next rule
        // that reaches for `OperatorMethod` to mean "not a built-in operation" will get it wrong too.
        if (model.GetOperation(binary, cancel) is not IBinaryOperation {
                OperatorMethod: null,
                IsLifted: false
            } operation) {
            return;
        }

        if (IsFloating(operation.LeftOperand.Type)
            || IsFloating(operation.RightOperand.Type)
            || operation.LeftOperand.Type is null
            || operation.RightOperand.Type is null
            || operation.LeftOperand.Type.TypeKind == TypeKind.Error
            || operation.RightOperand.Type.TypeKind == TypeKind.Error) {
            return;
        }

        if (!ExpressionIdentity.Same(binary.Left, binary.Right)
            || !ExpressionIdentity.IsStableDataPath(model, binary.Left, cancel)
            || !ExpressionIdentity.IsStableDataPath(model, binary.Right, cancel)) {
            return;
        }

        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                binary.GetLocation(),
                "Both operands of `"
                + binary.OperatorToken.ValueText
                + "` are `"
                + binary.Left.ToString().Trim()
                + "`; one side was meant to be something else"
            )
        );
    }

    /// <summary>⚠ Includes the nullable forms, which a lifted operation would already have excluded.</summary>
    static bool IsFloating(ITypeSymbol? type) {
        if (type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } nullable
            && nullable.TypeArguments.Length == 1) {
            type = nullable.TypeArguments[0];
        }

        return type?.SpecialType is SpecialType.System_Single
            or SpecialType.System_Double
            or SpecialType.System_Decimal;
    }
}
