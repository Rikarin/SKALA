using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using System.Linq;
using System.Threading;

namespace Rikarin.Skala.Rules.Modernization;

/// <summary>Proofs shared by the comparison-to-pattern and call-to-pattern rewrites.</summary>
internal static class PatternSafety {
    /// <summary>
    ///     ⚠ Whether an <c>is</c> expression may be dropped into this position without parentheses.
    /// </summary>
    /// <remarks>
    ///     A pattern's grammar is not an expression's. <c>!(x is T)</c> rewritten bare as
    ///     <c>!x is not T</c> is <c>(!x) is not T</c>, and <c>a is object == b</c> rewritten as
    ///     <c>a is not null == b</c> hands <c>null == b</c> to a grammar that parses constant patterns.
    ///     Rather than inventing parentheses the author did not write — which the formatter is not
    ///     allowed to remove again — every rule here declines every position outside this list.
    ///     <para>
    ///         ⚠ Shared rather than duplicated: <c>SK1050</c> and <c>SK1130</c> both move an expression
    ///         into the left operand of an <c>is</c>, so the two copies had to agree about the grammar
    ///         and nothing made them.
    ///     </para>
    ///     <para>
    ///         ⚠ <c>ExpressionStatementSyntax</c> was on this list and is not a safe position (#327).
    ///         The grammar accepts <c>x is "abc";</c> and the language does not — <c>CS0201</c>, only an
    ///         assignment, call, increment, decrement, <c>await</c> or <c>new</c> may be a statement —
    ///         so a pattern can never stand there whatever the parentheses say. It survived because the
    ///         one caller that could reach it on <em>compiling</em> code is the one that shipped last:
    ///         <c>SK1050</c> rewrites a comparison, and a bare <c>x != null;</c> is already
    ///         <c>CS0201</c> before any rewrite, so the entry could only ever be reached on code that
    ///         did not compile. <c>SK1130</c> rewrites an invocation, and <c>span.SequenceEqual("abc");</c>
    ///         is perfectly legal — which turned a dead entry into a fix that emits <c>CS0201</c>.
    ///         Removed here rather than excluded at <c>SK1130</c>'s call site, because a position no
    ///         caller may use is this helper's answer to give, and a per-rule exception is the
    ///         divergence sharing it was meant to prevent.
    ///     </para>
    /// </remarks>
    public static bool IsPatternSafeContext(ExpressionSyntax expression) {
        var parent = expression.Parent;
        return parent switch {
            ParenthesizedExpressionSyntax => true,
            IfStatementSyntax or WhileStatementSyntax or DoStatementSyntax => true,
            ReturnStatementSyntax or ArrowExpressionClauseSyntax => true,
            ArgumentSyntax or AttributeArgumentSyntax or EqualsValueClauseSyntax => true,
            AssignmentExpressionSyntax assignment => assignment.Right == expression,
            ConditionalExpressionSyntax conditional => conditional.Condition == expression,
            BinaryExpressionSyntax binary => binary.IsKind(SyntaxKind.LogicalAndExpression)
                || binary.IsKind(SyntaxKind.LogicalOrExpression),
            _ => false
        };
    }

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
        && !RewriteGuards.ContainsCommentOrDirectiveWithinTheEdit(binary.SyntaxTree, binary.Span)
        && model.GetOperation(binary, cancellation) is IBinaryOperation {
            OperatorMethod: null,
            Type.SpecialType: SpecialType.System_Boolean
        }
        && !NullComparison.InsideExpressionTree(model, binary, cancellation);
}
