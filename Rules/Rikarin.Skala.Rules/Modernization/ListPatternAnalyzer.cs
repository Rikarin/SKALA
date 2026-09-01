using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Rikarin.Skala.Rules.Metadata;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Rikarin.Skala.Rules.Modernization;

/// <summary>SK1013: guarded fixed-length array/string element tests.</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ListPatternAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.ListPattern);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(static start => {
                if (SkalaRule.MeetsLanguageVersion(start.Compilation, "11.0")) {
                    start.RegisterSyntaxNodeAction(Analyze, SyntaxKind.LogicalAndExpression);
                }
            }
        );
    }

    static void Analyze(SyntaxNodeAnalysisContext context) {
        var binary = (BinaryExpressionSyntax)context.Node;
        SyntaxNode outer = binary;
        while (outer.Parent is ParenthesizedExpressionSyntax parentheses) {
            outer = parentheses;
        }

        if (outer.Parent is BinaryExpressionSyntax parent
            && parent.IsKind(SyntaxKind.LogicalAndExpression)
            || !PatternSafety.CanRewrite(context.SemanticModel, binary, context.CancellationToken)) {
            return;
        }

        var terms = new List<ExpressionSyntax>();
        Flatten(binary, terms);
        var model = context.SemanticModel;
        var cancellation = context.CancellationToken;
        if (terms.Count < 2) {
            return;
        }

        // SK1011 may already have combined the null and Length guards. Keep the list candidate
        // alive after that safe fix rather than making the two modernizations order-dependent.
        var receiver = ConstantPatternSafety.NonNullReceiver(terms[0]);
        ExpressionSyntax? lengthExpression = null;
        var elementStart = 2;
        if (terms[0] is IsPatternExpressionSyntax {
                Pattern:
                RecursivePatternSyntax {
                    Type: null,
                    Designation: null,
                    PositionalPatternClause: null,
                    PropertyPatternClause.Subpatterns.Count: 1
                } recursive
            } combined
            && recursive.PropertyPatternClause!.Subpatterns[0] is {
                NameColon.Name.Identifier.ValueText: "Length",
                Pattern: ConstantPatternSyntax constant
            }) {
            receiver = combined.Expression;
            lengthExpression = constant.Expression;
            elementStart = 1;
        }

        if (receiver is null
            || terms.Count <= elementStart
            || PatternSafety.StableVariable(model, receiver, cancellation) is not { } symbol) {
            return;
        }

        var type = model.GetTypeInfo(receiver, cancellation).Type;
        var elementType = type switch {
            IArrayTypeSymbol { Rank: 1, IsSZArray: true } array => array.ElementType,
            { SpecialType: SpecialType.System_String } => model.Compilation.GetSpecialType(SpecialType.System_Char),
            _ => null
        };
        if (elementType is null
            || !ConstantPatternSafety.IsSupportedType(elementType)
            || !NullComparison.IsRewritable(model, receiver, cancellation)) {
            return;
        }

        if (lengthExpression is null) {
            if (terms[1] is not BinaryExpressionSyntax lengthTest
                || !ConstantPatternSafety.IsEquality(model, lengthTest, cancellation)
                || PatternSafety.Unwrap(lengthTest.Left) is not MemberAccessExpressionSyntax {
                    Name.Identifier.ValueText: "Length"
                } length
                || !SymbolEqualityComparer.Default.Equals(
                    symbol,
                    model.GetSymbolInfo(PatternSafety.Unwrap(length.Expression), cancellation).Symbol
                )) {
                return;
            }

            lengthExpression = lengthTest.Right;
        }

        if (!IntegralDomain.TryConstant(model, lengthExpression, cancellation, out var lengthValue)
            || lengthValue < 1
            || lengthValue > 8) {
            return;
        }

        var patterns = Enumerable.Repeat("_", (int)lengthValue).ToArray();
        var seen = new HashSet<int>();
        foreach (var term in terms.Skip(elementStart)) {
            if (term is not BinaryExpressionSyntax comparison
                || !ConstantPatternSafety.IsEquality(model, comparison, cancellation)
                || PatternSafety.Unwrap(comparison.Left) is not ElementAccessExpressionSyntax {
                    ArgumentList.Arguments.Count: 1
                } element
                || !SymbolEqualityComparer.Default.Equals(
                    symbol,
                    model.GetSymbolInfo(PatternSafety.Unwrap(element.Expression), cancellation).Symbol
                )
                || !IntegralDomain.TryConstant(
                    model,
                    element.ArgumentList.Arguments[0].Expression,
                    cancellation,
                    out var index
                )
                || index < 0
                || index >= lengthValue
                || !seen.Add((int)index)
                || !ConstantPatternSafety.TryConstant(model, comparison.Right, elementType, cancellation, out _)) {
                return;
            }

            patterns[(int)index] = comparison.Right.ToString();
        }

        if (CallerArgumentSafety.CapturesText(model, binary, cancellation)) {
            return;
        }

        var replacement = "(" + receiver + " is [" + string.Join(", ", patterns) + "])";
        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                binary.GetLocation(),
                FixEdits.Pack((binary.Span, replacement)),
                "Use a list pattern for the null, length and element checks"
            )
        );
    }

    static void Flatten(ExpressionSyntax expression, List<ExpressionSyntax> terms) {
        var pending = new Stack<ExpressionSyntax>();
        pending.Push(expression);
        while (pending.Count > 0 && terms.Count <= 10) {
            var current = PatternSafety.Unwrap(pending.Pop());
            if (current is BinaryExpressionSyntax binary && binary.IsKind(SyntaxKind.LogicalAndExpression)) {
                pending.Push(binary.Right);
                pending.Push(binary.Left);
            } else {
                terms.Add(current);
            }
        }
    }
}
