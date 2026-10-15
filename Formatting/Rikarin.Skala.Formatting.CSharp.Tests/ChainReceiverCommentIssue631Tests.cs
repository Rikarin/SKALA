namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     Issue #631: a chain whose receiver is a path of names, <c>source.A…</c>, with a comment after the receiver. The
///     receiver's own dots break once the receiver's line overflows, the comment counted (#582's rule, SK-DIV-0596).
///     Every expected string is <c>jb cleanupcode</c>'s own output, measured 2026-10-15 with <c>Testing ask</c>.
/// </summary>
public sealed class ChainReceiverCommentIssue631Tests {
    /// <summary>
    ///     Fuzz 10820374509567880048's statement: the receiver ends at 111 and its comment at 120, which stays; one column more
    ///     and the receiver's own dot breaks as well, whatever the margin says of the receiver alone.
    /// </summary>
    [Fact]
    public void TheFuzzShape_CountsTheCommentAfterTheReceiver() =>
        Oracle.Agrees(
            """
            class C {
                void M() {
                    var q = source.AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA /** d */ .Select /** d */ (y => y).Where (z => z.Bb);
                    var q = source.AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA /** d */ .Select /** d */ (y => y).Where (z => z.Bb);
                    var q = source.AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA /** d */ .Select /** d */ (y => y).Where (z => z.Bb);
                }
            }
            """,
            """
            class C {
                void M() {
                    var q = source.AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA /** d */
                        .Select /** d */(y => y)
                        .Where(z => z.Bb);
                    var q = source
                        .AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA /** d */
                        .Select /** d */(y => y)
                        .Where(z => z.Bb);
                    var q = source
                        .AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA /** d */
                        .Select /** d */(y => y)
                        .Where(z => z.Bb);
                }
            }
            """
        );

    /// <summary>
    ///     A shorter block comment moves the boundary by its own width, as a statement and as an <c>=</c> value.
    /// </summary>
    [Fact]
    public void ABlockCommentBeforeTheDot_CountsAsTheReceiversLine() =>
        Oracle.Agrees(
            """
            class C {
                void M() {
                    var q = source.AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA /* d */ .Select(y => y).Where(z => z.Bb);
                    var q = source.AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA /* d */ .Select(y => y).Where(z => z.Bb);
                    source.AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA /** d */ .Select(y => y).Where(z => z.Bb);
                    source.AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA /** d */ .Select(y => y).Where(z => z.Bb);
                }
            }
            """,
            """
            class C {
                void M() {
                    var q = source.AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA /* d */
                        .Select(y => y)
                        .Where(z => z.Bb);
                    var q = source
                        .AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA /* d */
                        .Select(y => y)
                        .Where(z => z.Bb);
                    source.AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA /** d */
                        .Select(y => y)
                        .Where(z => z.Bb);
                    source
                        .AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA /** d */
                        .Select(y => y)
                        .Where(z => z.Bb);
                }
            }
            """
        );

    /// <summary>
    ///     Pass one's own output: the author broke before the call, the comment ending the line or no comment at all. The
    ///     receiver's dots break by the same rule; Skala used to plan no receiver group here and broke the <c>=</c>.
    /// </summary>
    [Fact]
    public void AnAuthorsBreakBeforeTheCall_StillChopsAReceiverThatOverflows() =>
        Oracle.Agrees(
            """
            class C {
                void M() {
                    var q = source.AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA
                        .Select(y => y).Where(z => z.Bb);
                    var q = source.AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA
                        .Select(y => y).Where(z => z.Bb);
                    var q = source.AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA /** d */
                        .Select(y => y)
                        .Where(z => z.Bb);
                    source.AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA // d
                        .Select(y => y).Where(z => z.Bb);
                    source.AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA // d
                        .Select(y => y).Where(z => z.Bb);
                }
            }
            """,
            """
            class C {
                void M() {
                    var q = source.AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA
                        .Select(y => y)
                        .Where(z => z.Bb);
                    var q = source
                        .AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA
                        .Select(y => y)
                        .Where(z => z.Bb);
                    var q = source
                        .AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA /** d */
                        .Select(y => y)
                        .Where(z => z.Bb);
                    source.AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA // d
                        .Select(y => y)
                        .Where(z => z.Bb);
                    source
                        .AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA // d
                        .Select(y => y)
                        .Where(z => z.Bb);
                }
            }
            """
        );

}
