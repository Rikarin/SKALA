using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Rikarin.Skala.Rules.Metadata;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace Rikarin.Skala.Rules.Modernization;

/// <summary>SK1012: returning equality chains over one stable input.</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ReturningSwitchExpressionAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.ReturningSwitchExpression);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(static start => {
                if (SkalaRule.MeetsLanguageVersion(start.Compilation, "8.0")) {
                    start.RegisterSyntaxNodeAction(Analyze, SyntaxKind.IfStatement);
                }
            }
        );
    }

    static void Analyze(SyntaxNodeAnalysisContext context) {
        var root = (IfStatementSyntax)context.Node;
        var model = context.SemanticModel;
        var cancellation = context.CancellationToken;
        if (root.Parent is ElseClauseSyntax
            || root.ContainsDiagnostics
            || root.ContainsDirectives
            || RewriteGuards.ContainsCommentOrDirectiveWithinTheEdit(root.SyntaxTree, root.Span)
            || root.Else?.Statement is not IfStatementSyntax
            || PatternSafety.Unwrap(root.Condition) is not BinaryExpressionSyntax first
            || !ConstantPatternSafety.IsEquality(model, first, cancellation)
            || PatternSafety.StableVariable(model, first.Left, cancellation) is not { } input
            || model.GetTypeInfo(first.Left, cancellation).Type is not { } inputType
            || !ConstantPatternSafety.IsSupportedType(inputType)) {
            return;
        }

        var arms = new List<string>();
        var constants = new HashSet<string>(System.StringComparer.Ordinal);
        ITypeSymbol? resultType = null;
        var current = root;
        while (true) {
            if (PatternSafety.Unwrap(current.Condition) is not BinaryExpressionSyntax comparison
                || !ConstantPatternSafety.IsEquality(model, comparison, cancellation)
                || !SymbolEqualityComparer.Default.Equals(
                    input,
                    model.GetSymbolInfo(PatternSafety.Unwrap(comparison.Left), cancellation).Symbol
                )
                || !ConstantPatternSafety.TryConstant(model, comparison.Right, inputType, cancellation, out var key)
                || !constants.Add(key)
                || ReturnExpression(current.Statement) is not { } expression
                || !SameReturnType(model, expression, ref resultType, cancellation)) {
                return;
            }

            arms.Add(comparison.Right + " => " + expression);
            if (current.Else?.Statement is IfStatementSyntax next) {
                current = next;
                continue;
            }

            if (current.Else is null
                || ReturnExpression(current.Else.Statement) is not { } fallback
                || !SameReturnType(model, fallback, ref resultType, cancellation)) {
                return;
            }

            // Both bool constants cover the whole domain and make a discard arm unreachable.
            if (inputType.SpecialType == SpecialType.System_Boolean && constants.Count == 2) {
                return;
            }

            arms.Add("_ => " + fallback);
            break;
        }

        if (CallerArgumentSafety.CapturesText(model, root, cancellation)) {
            return;
        }

        var replacement = "return " + first.Left + " switch { " + string.Join(", ", arms) + " };";
        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                root.IfKeyword.GetLocation(),
                FixEdits.Pack((root.Span, replacement)),
                "Use a switch expression for this returning equality chain"
            )
        );
    }

    static ExpressionSyntax? ReturnExpression(StatementSyntax statement) {
        if (statement is BlockSyntax { Statements.Count: 1 } block) {
            statement = block.Statements[0];
        }

        return statement is ReturnStatementSyntax { Expression: { } expression }
            && expression is not RefExpressionSyntax
                ? expression : null;
    }

    static bool SameReturnType(
        SemanticModel model,
        ExpressionSyntax expression,
        ref ITypeSymbol? type,
        System.Threading.CancellationToken cancellation
    ) {
        var info = model.GetTypeInfo(expression, cancellation);
        // Avoid changing boxing, numeric common-type inference, target typing or user conversions.
        if (info.Type is null
            || info.Type.TypeKind is TypeKind.Error or TypeKind.Dynamic
            || !SymbolEqualityComparer.Default.Equals(info.Type, info.ConvertedType)) {
            return false;
        }

        type ??= info.Type;
        return SymbolEqualityComparer.Default.Equals(type, info.Type);
    }
}
