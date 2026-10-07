namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     Issue #444, SK-DIV-0209: an empty initializer or collection expression that holds nothing but a
///     block comment spanning lines closes on a line of its own, <c>new int[] { /* a</c> / <c>b */</c> /
///     <c>};</c>. Skala wrote <c>b */ };</c>. Every expected string is <c>jb cleanupcode</c>'s own output
///     for the input, and <see cref="Oracle.Agrees" /> asserts the second pass too.
/// </summary>
public sealed class EmptyContainerCommentIssue444Tests {
    /// <summary>
    ///     An empty array, collection, object or anonymous initializer and an empty collection expression
    ///     holding only a block comment that spans lines close on a line of their own; a one-line comment
    ///     stays beside both braces.
    /// </summary>
    [Fact]
    public void AnEmptyContainerWithAMultiLineComment_ClosesOnItsOwnLine() =>
        Oracle.Agrees(
            """
            class C {
                void M() {
                    var a1 = new int[] { /* a
                      b */ };
                    var a2 = new int[] { /* a */ };
                    var a3 = new List<int> { /* a
                      b */ };
                    int[] a4 = [/* a
                      b */];
                    var a5 = new int[] {
                        /* a
                      b */ };
                    var a6 = new Point { /* a
                      b */ };
                    var a7 = new { /* a
                      b */ };
                    var a8 = new int[] { /** a
                      b */ };
                    var a9 = new List<int> {
                        /* a
                         * b
                         */
                    };
                }
            }
            """,
            """
            class C {
                void M() {
                    var a1 = new int[] { /* a
                      b */
                    };
                    var a2 = new int[] { /* a */ };
                    var a3 = new List<int> { /* a
                      b */
                    };
                    int[] a4 = [ /* a
                      b */
                    ];
                    var a5 = new int[] {
                        /* a
                      b */
                    };
                    var a6 = new Point { /* a
                      b */
                    };
                    var a7 = new { /* a
                      b */
                    };
                    var a8 = new int[] { /** a
                      b */
                    };
                    var a9 = new List<int> {
                        /* a
                         * b
                         */
                    };
                }
            }
            """
        );
}
