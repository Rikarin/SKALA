namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     Issue #566: an <c>or</c> pattern chain continued inside a lambda that is a call's only argument, and
///     one that is the first operand of an <c>&amp;&amp;</c> or <c>||</c> chain. Every expected string is
///     <c>jb cleanupcode</c>'s own output, measured 2026-10-09 with <c>Testing ask</c>.
/// </summary>
public sealed class OrPatternLevelIssue566Tests {
    /// <summary>
    ///     A sole lambda argument kept on the call's line puts its <c>or</c>s one level past that line, as an
    ///     <c>&amp;&amp;</c> body does; among other arguments the lambda's own line is the base. Before the fix the
    ///     first two rows took two levels.
    /// </summary>
    [Fact]
    public void ASoleLambdasPatternChain_TakesOneLevel() =>
        Oracle.Agrees(
            """
            class T {
                void M() {
                    var b = node.ArgumentList.DescendantNodes().Any(static node => node is AnonymousFunctionExpressionSyntax or InitializerExpressionSyntax or AnonymousObjectCreationExpressionSyntax);
                    Use(x => x is AnonymousFunctionExpressionSyntax or InitializerExpressionSyntax or AnonymousObjectCreationExpressionSyntaxxxx);
                    Use(first, x => x is AnonymousFunctionExpressionSyntax or InitializerExpressionSyntax or AnonymousObjectCreationExpression);
                }
            }
            """,
            """
            class T {
                void M() {
                    var b = node.ArgumentList.DescendantNodes()
                        .Any(static node => node is AnonymousFunctionExpressionSyntax
                            or InitializerExpressionSyntax
                            or AnonymousObjectCreationExpressionSyntax
                        );
                    Use(x => x is AnonymousFunctionExpressionSyntax
                        or InitializerExpressionSyntax
                        or AnonymousObjectCreationExpressionSyntaxxxx
                    );
                    Use(
                        first,
                        x => x is AnonymousFunctionExpressionSyntax
                            or InitializerExpressionSyntax
                            or AnonymousObjectCreationExpression
                    );
                }
            }
            """
        );

    /// <summary>
    ///     A pattern chain that is an <c>||</c> chain's first operand puts its <c>or</c>s one level past the
    ///     <c>||</c>s. Before the fix they shared the <c>||</c>s' column.
    /// </summary>
    [Fact]
    public void APatternChainBeforeAnOr_TakesALevelPastIt() =>
        Oracle.Agrees(
            """
            class T {
                bool M(SyntaxToken token, SyntaxToken previous) {
                    var g = token.Kind() is SyntaxKind.DotToken or SyntaxKind.QuestionToken or SyntaxKind.CloseParenToken or SyntaxKind.CloseBracketToken || previous.Kind() is SyntaxKind.OpenParenToken or SyntaxKind.OpenBracketToken;
                    return token.Kind() is SyntaxKind.DotToken or SyntaxKind.QuestionToken or SyntaxKind.CloseParenToken or SyntaxKind.CloseBracketToken || previous.Kind() is SyntaxKind.OpenParenToken;
                }
            }
            """,
            """
            class T {
                bool M(SyntaxToken token, SyntaxToken previous) {
                    var g = token.Kind() is SyntaxKind.DotToken
                            or SyntaxKind.QuestionToken
                            or SyntaxKind.CloseParenToken
                            or SyntaxKind.CloseBracketToken
                        || previous.Kind() is SyntaxKind.OpenParenToken or SyntaxKind.OpenBracketToken;
                    return token.Kind() is SyntaxKind.DotToken
                            or SyntaxKind.QuestionToken
                            or SyntaxKind.CloseParenToken
                            or SyntaxKind.CloseBracketToken
                        || previous.Kind() is SyntaxKind.OpenParenToken;
                }
            }
            """
        );
}
