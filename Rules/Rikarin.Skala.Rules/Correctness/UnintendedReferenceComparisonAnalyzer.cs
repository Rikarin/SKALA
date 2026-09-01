using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using Rikarin.Skala.Rules.Metadata;
using System.Collections.Immutable;
using System.Linq;

namespace Rikarin.Skala.Rules.Correctness;

/// <summary><c>SK2040</c> — the comparison is by reference where value equality was meant.</summary>
/// <remarks>
///     ⚠ The test is on the <em>bound operator</em>, never on a list of type names: an operation that
///     resolves to a user-defined <c>operator ==</c> is doing what it says, whatever the type is
///     called. That is what keeps records, <c>string</c>, <c>Uri</c> and every type with its own
///     operator out without an exclusion list to maintain.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class UnintendedReferenceComparisonAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.UnintendedReferenceComparison);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterOperationAction(AnalyzeComparison, OperationKind.Binary);
        context.RegisterOperationAction(AnalyzeReferenceEquals, OperationKind.Invocation);
    }

    static void AnalyzeComparison(OperationAnalysisContext context) {
        var operation = (IBinaryOperation)context.Operation;
        if (operation.OperatorKind is not (BinaryOperatorKind.Equals or BinaryOperatorKind.NotEquals)
            || operation.OperatorMethod is not null
            || operation.Syntax is not BinaryExpressionSyntax syntax
            || EqualityMembers.InsideAnEqualityMember(syntax)) {
            return;
        }

        var left = Unwrap(operation.LeftOperand);
        var right = Unwrap(operation.RightOperand);
        if (IsNullOrDefault(left)
            || IsNullOrDefault(right)
            || left is IInstanceReferenceOperation
            || right is IInstanceReferenceOperation
            || left.Type is not INamedTypeSymbol type
            || right.Type is not INamedTypeSymbol other
            || !SymbolEqualityComparer.Default.Equals(type, other)
            || !DefinesValueEquality(type)) {
            return;
        }

        var replacement = "Equals(" + syntax.Left + ", " + syntax.Right + ")";
        if (operation.OperatorKind == BinaryOperatorKind.NotEquals) {
            replacement = "!" + replacement;
        }

        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                syntax.GetLocation(),
                FixEdits.Pack((syntax.Span, replacement)),
                "`"
                + type.Name
                + "` defines value equality and no `operator ==`, so this compares references; "
                + "use Equals if value equality was meant"
            )
        );
    }

    static void AnalyzeReferenceEquals(OperationAnalysisContext context) {
        var operation = (IInvocationOperation)context.Operation;
        if (operation.TargetMethod is not { Name: "ReferenceEquals", IsStatic: true, Parameters.Length: 2 } target
            || target.ContainingType?.SpecialType != SpecialType.System_Object
            || operation.Arguments.Length != 2
            || operation.Syntax is not InvocationExpressionSyntax syntax) {
            return;
        }

        var first = Unwrap(operation.Arguments[0].Value);
        var second = Unwrap(operation.Arguments[1].Value);
        if (!IsDefinitelyAValueType(first.Type) && !IsDefinitelyAValueType(second.Type)) {
            return;
        }

        var replacement = "Equals(" + first.Syntax + ", " + second.Syntax + ")";
        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                syntax.GetLocation(),
                FixEdits.Pack((syntax.Span, replacement)),
                "ReferenceEquals boxes a value type into a fresh object, so this is false for every "
                + "input including a value compared with itself"
            )
        );
    }

    /// <summary>
    ///     ⚠ Only a value type the compiler is <em>certain</em> about. An unconstrained type parameter
    ///     answers <c>false</c> here and is left alone, because it is a reference type at half its
    ///     instantiations and the call is correct at those.
    /// </summary>
    static bool IsDefinitelyAValueType(ITypeSymbol? type) =>
        type is { IsValueType: true, TypeKind: not TypeKind.Error };

    static bool DefinesValueEquality(INamedTypeSymbol type) =>
        type is { TypeKind: TypeKind.Class, IsRecord: false, IsAnonymousType: false, SpecialType: SpecialType.None }
        && type.Locations.Any(static location => location.IsInSource)
        && EqualityMembers.InheritsObjectEquals(type);

    static IOperation Unwrap(IOperation operation) {
        var current = operation;
        while (current is IConversionOperation { IsImplicit: true } conversion
               && !conversion.Conversion.IsUserDefined) {
            current = conversion.Operand;
        }

        return current;
    }

    static bool IsNullOrDefault(IOperation operation) =>
        operation is IDefaultValueOperation
        || operation.Syntax.IsKind(SyntaxKind.NullLiteralExpression)
        || operation.Syntax.IsKind(SyntaxKind.DefaultLiteralExpression)
        || operation.Syntax.IsKind(SyntaxKind.DefaultExpression)
        || (operation.ConstantValue.HasValue && operation.ConstantValue.Value is null);
}
