using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Threading;

namespace Rikarin.Skala.Rules.Async;

/// <summary>
///     What a <c>using</c> owns, shared by the three rules that ask that question from three angles.
/// </summary>
/// <remarks>
///     <c>SK3510</c> asks "is this variable already owned, so the explicit <c>Dispose</c> is
///     redundant"; <c>SK3511</c> asks "is the thing the <c>using</c> owns constructed in a way that
///     leaves a window where it is not owned yet"; <c>SK3512</c> asks "does something else take the
///     variable out of the scope that owns it". One answer serves all three.
///     <para>
///         ⚠ <b>Ownership is read from the declaration, never from a scope walk.</b> The earlier draft
///         walked ancestors from the use site looking for an enclosing <c>using</c> that named the same
///         identifier, which is the same shape <c>SK3007</c> uses and is wrong here: it matches on text,
///         so a shadowing local of the same name in an inner scope reads as owned. Going the other way —
///         from the symbol to its own declarator and asking what that declarator's parent is — is exact,
///         needs no scope reasoning at all, and gets <c>await using</c> for free.
///     </para>
/// </remarks>
static class UsingResource {
    /// <summary>
    ///     The <c>using</c> that owns <paramref name="local" />, or null when nothing does.
    /// </summary>
    /// <returns>
    ///     A <see cref="UsingStatementSyntax" /> for <c>using (var x = …)</c>, or a
    ///     <see cref="LocalDeclarationStatementSyntax" /> for <c>using var x = …;</c>.
    /// </returns>
    public static StatementSyntax? OwnerOf(ILocalSymbol local, CancellationToken cancellation) {
        if (local.DeclaringSyntaxReferences.Length != 1) {
            return null;
        }

        // VariableDeclarator -> VariableDeclaration -> the `using` that owns it.
        return local.DeclaringSyntaxReferences[0].GetSyntax(cancellation) is VariableDeclaratorSyntax {
            Parent: VariableDeclarationSyntax { Parent: { } owner }
        }
                ? owner switch {
                    UsingStatementSyntax use => use,
                    LocalDeclarationStatementSyntax { UsingKeyword.RawKind: not (int)SyntaxKind.None } declaration =>
                        declaration,
                    _ => null
                }
                : null;
    }

    /// <summary>
    ///     The single variable a <c>using</c> declares, or null for the resource-expression form and
    ///     for the multi-declarator form neither fix knows how to rewrite.
    /// </summary>
    public static VariableDeclaratorSyntax? DeclaredVariable(StatementSyntax owner) {
        var declaration = owner switch {
            UsingStatementSyntax use => use.Declaration,
            LocalDeclarationStatementSyntax {
                UsingKeyword.RawKind: not (int)SyntaxKind.None
            } local => local.Declaration,
            _ => null
        };

        return declaration is { Variables.Count: 1 } ? declaration.Variables[0] : null;
    }

    /// <summary>
    ///     ⚠ Whether a lambda or a local function stands between <paramref name="node" /> and
    ///     <paramref name="ancestor" />.
    /// </summary>
    /// <remarks>
    ///     A reference to the resource from inside a nested function may run at a time the enclosing
    ///     <c>using</c> says nothing about — after the scope has ended, on another thread, or never.
    ///     Every one of these three rules withdraws rather than reasoning about when.
    /// </remarks>
    public static bool CrossesAFunctionBoundary(SyntaxNode node, SyntaxNode ancestor) {
        for (var current = node;
             current is not null && !ReferenceEquals(current, ancestor);
             current = current.Parent) {
            if (current is AnonymousFunctionExpressionSyntax or LocalFunctionStatementSyntax) {
                return true;
            }
        }

        return false;
    }

    /// <summary>Strips the wrappers that do not change what an expression refers to.</summary>
    public static ExpressionSyntax Unwrap(ExpressionSyntax expression) {
        while (true) {
            switch (expression) {
                case ParenthesizedExpressionSyntax parenthesized:
                    expression = parenthesized.Expression;
                    continue;
                case PostfixUnaryExpressionSyntax {
                    RawKind: (int)SyntaxKind.SuppressNullableWarningExpression
                } suppression:
                    expression = suppression.Operand;
                    continue;
                default:
                    return expression;
            }
        }
    }

    public static bool Implements(ITypeSymbol? type, INamedTypeSymbol? contract) {
        if (type is null || contract is null || type.TypeKind == TypeKind.Error) {
            return false;
        }

        if (SymbolEqualityComparer.Default.Equals(type, contract)) {
            return true;
        }

        foreach (var candidate in type.AllInterfaces) {
            if (SymbolEqualityComparer.Default.Equals(candidate, contract)) {
                return true;
            }
        }

        return false;
    }

    /// <summary>The whitespace the line containing <paramref name="node" /> starts with.</summary>
    public static string IndentOf(SyntaxNode node) {
        var text = node.SyntaxTree.GetText();
        var line = text.Lines.GetLineFromPosition(node.SpanStart);
        var prefix = text.ToString(TextSpan.FromBounds(line.Start, node.SpanStart));
        foreach (var c in prefix) {
            if (!char.IsWhiteSpace(c)) {
                return string.Empty;
            }
        }

        return prefix;
    }
}
