namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     Issue #584: a comment interrupting a pattern chain leaves every <c>or</c> on one column. Every expected
///     string is <c>jb cleanupcode</c>'s own output for the input, and each is checked on a second pass;
///     <c>constructs/syntax/comment-in-a-pattern-chain.cs</c> holds the wider set.
/// </summary>
public sealed class CommentInAPatternChainIssue584Tests {
    [Fact]
    public void AnOrAfterAComment_StaysOnTheChainsColumn() =>
        Oracle.Agrees(
            """
            class C {
                bool A(object o) =>
                    o is int
                        or long
                        // a comment
                        or string;

                bool F(object o) {
                    if (o is int
                        // c
                        or long) {
                        return true;
                    }

                    return false;
                }
            }
            """,
            """
            class C {
                bool A(object o) =>
                    o is int
                        or long
                        // a comment
                        or string;

                bool F(object o) {
                    if (o is int
                        // c
                        or long) {
                        return true;
                    }

                    return false;
                }
            }
            """
        );
}
