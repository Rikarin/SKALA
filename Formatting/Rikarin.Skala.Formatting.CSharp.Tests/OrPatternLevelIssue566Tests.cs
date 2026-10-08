namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     Issue #566: an <c>or</c> pattern chain continued inside a lambda that is a call's only argument, and
///     one that is the first operand of an <c>&amp;&amp;</c> or <c>||</c> chain. Every expected string is
///     <c>jb cleanupcode</c>'s own output, measured 2026-10-09 with <c>Testing ask</c>.
/// </summary>
public sealed class OrPatternLevelIssue566Tests {
    const string Long1 = "var b = node.ArgumentList.DescendantNodes().Any(static node => node is Anonymous"
        + "FunctionExpressionSyntax or InitializerExpressionSyntax or AnonymousObjectCreati"
        + "onExpressionSyntax);";

    const string Long2 = "Use(x => x is AnonymousFunctionExpressionSyntax or InitializerExpressionSyntax o"
        + "r AnonymousObjectCreationExpressionSyntaxxxx);";

    const string Long3 = "Use(first, x => x is AnonymousFunctionExpressionSyntax or InitializerExpressionS"
        + "yntax or AnonymousObjectCreationExpression);";

    const string Long4 = "var g = token.Kind() is SyntaxKind.DotToken or SyntaxKind.QuestionToken or Synta"
        + "xKind.CloseParenToken or SyntaxKind.CloseBracketToken || previous.Kind() is Synt"
        + "axKind.OpenParenToken or SyntaxKind.OpenBracketToken;";

    const string Long5 = "return token.Kind() is SyntaxKind.DotToken or SyntaxKind.QuestionToken or Syntax"
        + "Kind.CloseParenToken or SyntaxKind.CloseBracketToken || previous.Kind() is Synta"
        + "xKind.OpenParenToken;";

    /// <summary>
    ///     A sole lambda argument kept on the call's line puts its <c>or</c>s one level past that line, as an
    ///     <c>&amp;&amp;</c> body does; among other arguments the lambda's own line is the base. Before the fix the
    ///     first two rows took two levels.
    /// </summary>
    [Fact]
    public void ASoleLambdasPatternChain_TakesOneLevel() =>
        Oracle.Agrees(
            $$"""
              class T {
                  void M() {
                      {{Long1}}
                      {{Long2}}
                      {{Long3}}
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
            $$"""
              class T {
                  bool M(SyntaxToken token, SyntaxToken previous) {
                      {{Long4}}
                      {{Long5}}
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

    /// <summary>
    ///     The level the first fact removes is only held: where the arrow breaks, the <c>or</c>s go one
    ///     level past the body's own line; and where more links follow the call, the chain's level stands
    ///     beside the parenthesis's (Skala's own <c>ConstantBytes</c> and <c>UndisposedLocalAnalyzer</c>,
    ///     which the first cut moved).
    /// </summary>
    [Fact]
    public void TheLevelIsHeldOnlyWhileTheArrowStays() =>
        Oracle.Agrees(
            """
            class T {
                bool M(object[] expressions) {
                    return expressions.Length > 0
                        && expressions.All(static expression =>
                            expression is LiteralExpressionSyntax
                                or PrefixUnaryExpressionSyntax { Operand: LiteralExpressionSyntax }
                        );
                }

                static bool IsIterator(SyntaxNode body) =>
                    body.DescendantNodes(static child => child is not AnonymousFunctionExpressionSyntax
                            and not LocalFunctionStatementSyntax
                        )
                        .Any(static node => node is YieldStatementSyntax);
            }
            """,
            """
            class T {
                bool M(object[] expressions) {
                    return expressions.Length > 0
                        && expressions.All(static expression =>
                            expression is LiteralExpressionSyntax
                                or PrefixUnaryExpressionSyntax { Operand: LiteralExpressionSyntax }
                        );
                }

                static bool IsIterator(SyntaxNode body) =>
                    body.DescendantNodes(static child => child is not AnonymousFunctionExpressionSyntax
                            and not LocalFunctionStatementSyntax
                        )
                        .Any(static node => node is YieldStatementSyntax);
            }
            """
        );
}
