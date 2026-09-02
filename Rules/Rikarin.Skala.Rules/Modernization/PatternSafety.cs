using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using System.Linq;
using System.Threading;

namespace Rikarin.Skala.Rules.Modernization;

/// <summary>Proofs shared by the two comparison-to-pattern rewrites.</summary>
internal static class PatternSafety {
    public static ExpressionSyntax Unwrap(ExpressionSyntax expression) {
        while (expression is ParenthesizedExpressionSyntax parentheses) {
            expression = parentheses.Expression;
        }

        return expression;
    }

    public static bool IsIntegral(ITypeSymbol? type) =>
        type?.SpecialType is
        SpecialType.System_SByte
            or SpecialType.System_Byte
            or SpecialType.System_Int16
            or SpecialType.System_UInt16
            or SpecialType.System_Int32
            or SpecialType.System_UInt32
            or SpecialType.System_Int64
            or SpecialType.System_UInt64
            or SpecialType.System_Char;

    public static ISymbol? StableVariable(
        SemanticModel model,
        ExpressionSyntax expression,
        CancellationToken cancellation
    ) {
        expression = Unwrap(expression);
        if (expression is not IdentifierNameSyntax) {
            return null;
        }

        var symbol = model.GetSymbolInfo(expression, cancellation).Symbol;
        if (symbol is not (ILocalSymbol { RefKind: RefKind.None, IsConst: false }
                or IParameterSymbol { RefKind: RefKind.None })) {
            return null;
        }

        // A captured variable can change between the original reads, even from another thread.
        // Analyze the outer body, not just the comparison or the innermost lambda.
        SyntaxNode? body = null;
        foreach (var ancestor in expression.Ancestors()) {
            body = ancestor switch {
                BaseMethodDeclarationSyntax method => (SyntaxNode?)method.Body ?? method.ExpressionBody?.Expression,
                AccessorDeclarationSyntax accessor => (SyntaxNode?)accessor.Body ?? accessor.ExpressionBody?.Expression,
                AnonymousFunctionExpressionSyntax lambda => lambda.Body,
                _ => body
            };
        }

        if (body is null
            || model.AnalyzeDataFlow(body) is not { Succeeded: true } flow
            || flow.Captured.Any(captured => SymbolEqualityComparer.Default.Equals(captured, symbol))) {
            return null;
        }

        // A ref alias also allows the storage to escape without a lambda capture.
        if (body.DescendantNodes()
                .OfType<IdentifierNameSyntax>()
                .Any(name =>
                    CanEscape(name)
                    && SymbolEqualityComparer.Default.Equals(model.GetSymbolInfo(name, cancellation).Symbol, symbol)
                )) {
            return null;
        }

        return symbol;
    }

    static bool CanEscape(IdentifierNameSyntax name) {
        SyntaxNode expression = name;
        while (expression.Parent is ParenthesizedExpressionSyntax parentheses) {
            expression = parentheses;
        }

        return expression.Parent is RefExpressionSyntax or MakeRefExpressionSyntax
            || expression.Parent is PrefixUnaryExpressionSyntax unary
            && unary.IsKind(SyntaxKind.AddressOfExpression)
            || expression.Parent is ArgumentSyntax argument
            && !argument.RefKindKeyword.IsKind(SyntaxKind.None);
    }

    public static bool CanRewrite(SemanticModel model, BinaryExpressionSyntax binary, CancellationToken cancellation) =>
        !binary.ContainsDiagnostics
        && !binary.ContainsDirectives
        && !RewriteGuards.ContainsCommentOrDirective(binary.SyntaxTree, binary.Span)
        && model.GetOperation(binary, cancellation) is IBinaryOperation {
            OperatorMethod: null,
            Type.SpecialType: SpecialType.System_Boolean
        }
        && !NullComparison.InsideExpressionTree(model, binary, cancellation);
}
