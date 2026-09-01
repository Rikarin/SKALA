using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using Rikarin.Skala.Rules.Metadata;
using System.Collections.Immutable;

namespace Rikarin.Skala.Rules.Modernization;

/// <summary>SK1011: a null guard followed by one member comparison, without reordering getters.</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class PropertyPatternAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.PropertyPattern);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(static start => {
                if (SkalaRule.MeetsLanguageVersion(start.Compilation, "8.0")) {
                    start.RegisterSyntaxNodeAction(Analyze, SyntaxKind.LogicalAndExpression);
                }
            }
        );
    }

    static void Analyze(SyntaxNodeAnalysisContext context) {
        var binary = (BinaryExpressionSyntax)context.Node;
        var model = context.SemanticModel;
        var cancellation = context.CancellationToken;
        var guard = PatternSafety.Unwrap(binary.Left);
        ExpressionSyntax? receiver = guard switch {
            BinaryExpressionSyntax comparison when comparison.IsKind(SyntaxKind.NotEqualsExpression)
                => NullComparison.OperandOf(comparison),
            IsPatternExpressionSyntax {
                Pattern:
                UnaryPatternSyntax {
                    RawKind: (int)SyntaxKind.NotPattern,
                    Pattern: ConstantPatternSyntax { Expression.RawKind: (int)SyntaxKind.NullLiteralExpression }
                }
            } pattern
                => pattern.Expression,
            _ => null
        };
        if (receiver is null
            || !NullComparison.IsRewritable(model, receiver, cancellation)
            || model.GetTypeInfo(receiver, cancellation).Type is not { IsReferenceType: true }
            || PatternSafety.Unwrap(binary.Right) is not BinaryExpressionSyntax comparisonRight
            || !comparisonRight.IsKind(SyntaxKind.EqualsExpression)
            || PatternSafety.Unwrap(comparisonRight.Left) is not MemberAccessExpressionSyntax member
            || !PatternSafety.CanRewrite(model, binary, cancellation)
            || model.GetOperation(comparisonRight, cancellation) is not IBinaryOperation { OperatorMethod: null }) {
            return;
        }

        var symbol = PatternSafety.StableVariable(model, receiver, cancellation);
        if (symbol is null
            || !SymbolEqualityComparer.Default.Equals(
                symbol,
                model.GetSymbolInfo(PatternSafety.Unwrap(member.Expression), cancellation).Symbol
            )
            || model.GetSymbolInfo(member, cancellation).Symbol is not (IPropertySymbol {
                IsStatic: false,
                RefKind: RefKind.None
            }
                or IFieldSymbol { IsStatic: false })) {
            return;
        }

        var constant = model.GetConstantValue(comparisonRight.Right, cancellation);
        var type = model.GetTypeInfo(member, cancellation).Type;
        if (!constant.HasValue
            || type is null
            || !model.ClassifyConversion(comparisonRight.Right, type).IsImplicit
            || (constant.Value is null
                    ? !NullComparison.IsRewritable(model, member, cancellation)
                    : !(PatternSafety.IsIntegral(type)
                        || type.SpecialType is SpecialType.System_Boolean or SpecialType.System_String
                        || type.TypeKind == TypeKind.Enum))) {
            return;
        }

        var replacement = "(" + receiver + " is { " + member.Name + ": " + comparisonRight.Right + " })";
        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                binary.GetLocation(),
                FixEdits.Pack((binary.Span, replacement)),
                "Combine the null guard and member comparison with a property pattern"
            )
        );
    }
}
