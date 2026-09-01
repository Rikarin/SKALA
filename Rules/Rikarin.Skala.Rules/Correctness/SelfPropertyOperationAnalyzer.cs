using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using Rikarin.Skala.Rules.Metadata;
using Rikarin.Skala.Rules.Modernization;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;

namespace Rikarin.Skala.Rules.Correctness;

/// <summary>SK2012: self-operations on known auto-properties, not arbitrary accessor calls.</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class SelfPropertyOperationAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.SelfAssignmentOrComparison);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(
            Analyze,
            SyntaxKind.SimpleAssignmentExpression,
            SyntaxKind.EqualsExpression,
            SyntaxKind.NotEqualsExpression,
            SyntaxKind.LessThanExpression,
            SyntaxKind.LessThanOrEqualExpression,
            SyntaxKind.GreaterThanExpression,
            SyntaxKind.GreaterThanOrEqualExpression
        );
    }

    static void Analyze(SyntaxNodeAnalysisContext context) {
        var model = context.SemanticModel;
        var cancellation = context.CancellationToken;
        ExpressionSyntax left;
        ExpressionSyntax right;
        if (context.Node is AssignmentExpressionSyntax assignment) {
            left = assignment.Left;
            right = assignment.Right;
        } else if (context.Node is BinaryExpressionSyntax binary
                   && model.GetOperation(binary, cancellation) is IBinaryOperation {
                       OperatorMethod: null,
                       IsLifted: false
                   }) {
            left = binary.Left;
            right = binary.Right;
        } else {
            return;
        }

        if (model.GetOperation(PatternSafety.Unwrap(left), cancellation) is not IPropertyReferenceOperation a
            || model.GetOperation(PatternSafety.Unwrap(right), cancellation) is not IPropertyReferenceOperation b
            || !SymbolEqualityComparer.Default.Equals(a.Property, b.Property)
            || !IsAutomatic(a.Property, cancellation)
            || a.Property.DeclaringSyntaxReferences[0].SyntaxTree != context.Node.SyntaxTree
            || !SameReceiver(a.Instance, b.Instance, model, cancellation)
            || context.Node is BinaryExpressionSyntax
            && !(PatternSafety.IsIntegral(a.Property.Type)
                || a.Property.Type.SpecialType is SpecialType.System_Boolean or SpecialType.System_String
                || a.Property.Type.TypeKind == TypeKind.Enum)
            || model.GetDiagnostics(context.Node.Span, cancellation)
                .Any(static diagnostic => diagnostic.Id is "CS1717" or "CS1718")) {
            return;
        }

        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                context.Node.GetLocation(),
                "The same automatic property `"
                + a.Property.Name
                + "` is used on both sides; check the intended assignment or comparison"
            )
        );
    }

    static bool IsAutomatic(IPropertySymbol property, CancellationToken cancellation) =>
        !property.IsVirtual
        && !property.IsOverride
        && !property.IsAbstract
        && !property.IsIndexer
        && property.RefKind == RefKind.None
        && property.ExplicitInterfaceImplementations.IsEmpty
        && property.DeclaringSyntaxReferences.Length == 1
        && property.DeclaringSyntaxReferences[0].GetSyntax(cancellation) is PropertyDeclarationSyntax {
            AccessorList: { } list
        } declaration
        && !declaration.Modifiers.Any(SyntaxKind.PartialKeyword)
        && list.Accessors.All(static accessor => accessor.Body is null
            && accessor.ExpressionBody is null
            && !accessor.SemicolonToken.IsMissing
        );

    static bool SameReceiver(IOperation? a, IOperation? b, SemanticModel model, CancellationToken cancellation) {
        if (a is null || b is null) {
            return a is null && b is null;
        }

        if (a is IInstanceReferenceOperation { ReferenceKind: InstanceReferenceKind.ContainingTypeInstance }
            && b is IInstanceReferenceOperation { ReferenceKind: InstanceReferenceKind.ContainingTypeInstance }) {
            return true;
        }

        return a.Syntax is ExpressionSyntax left
            && b.Syntax is ExpressionSyntax right
            && PatternSafety.StableVariable(model, left, cancellation) is { } symbol
            && SymbolEqualityComparer.Default.Equals(
                symbol,
                model.GetSymbolInfo(PatternSafety.Unwrap(right), cancellation).Symbol
            );
    }
}
