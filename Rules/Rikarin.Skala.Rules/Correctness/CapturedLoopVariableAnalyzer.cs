using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using Rikarin.Skala.Rules.Metadata;
using System;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;

namespace Rikarin.Skala.Rules.Correctness;

/// <summary><c>SK2008</c> — a List stores a delegate capturing a changing for-loop variable.</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class CapturedLoopVariableAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.CapturedLoopVariable);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(
            Analyze,
            SyntaxKind.SimpleLambdaExpression,
            SyntaxKind.ParenthesizedLambdaExpression,
            SyntaxKind.AnonymousMethodExpression
        );
    }

    static void Analyze(SyntaxNodeAnalysisContext context) {
        var lambda = (AnonymousFunctionExpressionSyntax)context.Node;
        SyntaxNode argumentExpression = lambda;
        while (argumentExpression.Parent is ParenthesizedExpressionSyntax parenthesized) {
            argumentExpression = parenthesized;
        }

        if (argumentExpression.Parent is not ArgumentSyntax { Parent.Parent: InvocationExpressionSyntax invocation }
            || context.SemanticModel.GetOperation(invocation, context.CancellationToken)
            is not IInvocationOperation {
                TargetMethod.Name: "Add",
                TargetMethod.Parameters.Length: 1,
                Arguments.Length: 1
            } call
            || call.TargetMethod.Parameters[0].Type.TypeKind != TypeKind.Delegate) {
            return;
        }

        var list = context.Compilation.GetTypeByMetadataName("System.Collections.Generic.List`1");
        if (list is null
            || !SymbolEqualityComparer.Default.Equals(call.TargetMethod.ContainingType.OriginalDefinition, list)) {
            return;
        }

        var flow = context.SemanticModel.AnalyzeDataFlow(lambda);
        if (flow is not { Succeeded: true }) {
            return;
        }

        foreach (var loop in lambda.Ancestors().OfType<ForStatementSyntax>()) {
            if (loop.Declaration is null || !StoredOutsideLoop(call.Instance, loop)) {
                continue;
            }

            foreach (var variable in loop.Declaration.Variables) {
                var symbol = context.SemanticModel.GetDeclaredSymbol(variable, context.CancellationToken);
                if (symbol is null
                    || !flow.CapturedInside.Contains(symbol, SymbolEqualityComparer.Default)
                    || !loop.Incrementors.Any(increment => Changes(increment, symbol, context))) {
                    continue;
                }

                context.ReportDiagnostic(
                    Diagnostic.Create(
                        Descriptor,
                        lambda.GetLocation(),
                        "stored delegate captures changing loop variable `"
                        + symbol.Name
                        + "`; capture an iteration-local value instead"
                    )
                );
                return;
            }
        }
    }

    static bool StoredOutsideLoop(IOperation? receiver, ForStatementSyntax loop) =>
        receiver is IFieldReferenceOperation
        || receiver is ILocalReferenceOperation local
        && local.Local.DeclaringSyntaxReferences.All(reference =>
            reference.SyntaxTree != loop.SyntaxTree || !loop.Span.Contains(reference.Span)
        );

    static bool Changes(ExpressionSyntax expression, ISymbol symbol, SyntaxNodeAnalysisContext context) {
        var target = context.SemanticModel.GetOperation(expression, context.CancellationToken) switch {
            IIncrementOrDecrementOperation {
                OperatorMethod: null,
                Target: ILocalReferenceOperation local
            } => local.Local,
            ICompoundAssignmentOperation {
                OperatorMethod: null,
                OperatorKind: BinaryOperatorKind.Add or BinaryOperatorKind.Subtract,
                Target: ILocalReferenceOperation local,
                Value.ConstantValue: { HasValue: true, Value: var step }
            } when IsNonzeroInteger(step) => local.Local,
            _ => null
        };

        return SymbolEqualityComparer.Default.Equals(target, symbol);
    }

    static bool IsNonzeroInteger(object? value) =>
        value is sbyte or byte or short or ushort or int or uint or long or ulong
        && Convert.ToDecimal(value, CultureInfo.InvariantCulture) != 0;
}
