namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     Issue #633 (SK-DIV-0601): a block comment after a sole lambda's first operand is part of that operand's line,
///     so it counts toward whether the line fits beside the arrow. Every expected string is <c>jb cleanupcode</c>'s
///     own output for the input, measured 2026-10-15 with <c>Testing ask</c>.
/// </summary>
public sealed class LambdaFirstOperandCommentIssue633Tests {
    /// <summary>
    ///     Fuzz 10236841228984116565: <c>x is A /* f */</c> ends at 125 with the comment and at 117 without it. The
    ///     arrow breaks; without the comment it stays. A <c>//</c> comment breaks it too, and a comment after the
    ///     <c>or</c> belongs to the next line and does not count.
    /// </summary>
    [Fact]
    public void ACommentAfterTheFirstOperand_CountsBesideTheArrow() =>
        Oracle.Agrees(
            """
            class C {
                void M1() {
                    UUUUUUUU(x_wwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwww => x_wwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwww is Aaaaaaaaaaaa /* f */  or Bbbbbbbbbbbbbbbb or CCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCC);
                }

                void M2() {
                    UUUUUUUU(x_wwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwww => x_wwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwww is Aaaaaaaaaaaa /* f */
                        or Bbbbbbbbbbbbbbbb
                        or CCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCC
                    );
                }

                void M4() {
                    UUUUUUUU(x_wwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwww => x_wwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwww is Aaaaaaaaaaaa or Bbbbbbbbbbbbbbbb or CCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCC);
                }

                void M6() {
                    UUUUUUUU(x_wwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwww => x_wwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwww is Aaaaaaaaaaaa // f
                        or Bbbbbbbbbbbbbbbb
                        or CCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCC
                    );
                }

                void M7() {
                    UUUUUUUU(x_wwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwww => x_wwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwww is Aaaaaaaaaaaa or /* f */ Bbbbbbbbbbbbbbbb or CCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCC);
                }
            }
            """,
            """
            class C {
                void M1() {
                    UUUUUUUU(x_wwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwww =>
                        x_wwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwww is Aaaaaaaaaaaa /* f */
                            or Bbbbbbbbbbbbbbbb
                            or CCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCC
                    );
                }

                void M2() {
                    UUUUUUUU(x_wwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwww =>
                        x_wwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwww is Aaaaaaaaaaaa /* f */
                            or Bbbbbbbbbbbbbbbb
                            or CCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCC
                    );
                }

                void M4() {
                    UUUUUUUU(x_wwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwww => x_wwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwww is Aaaaaaaaaaaa
                        or Bbbbbbbbbbbbbbbb
                        or CCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCC
                    );
                }

                void M6() {
                    UUUUUUUU(x_wwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwww =>
                        x_wwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwww is Aaaaaaaaaaaa // f
                            or Bbbbbbbbbbbbbbbb
                            or CCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCC
                    );
                }

                void M7() {
                    UUUUUUUU(x_wwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwww => x_wwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwww is Aaaaaaaaaaaa
                        or /* f */ Bbbbbbbbbbbbbbbb
                        or CCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCC
                    );
                }
            }
            """
        );

    /// <summary>
    ///     The same miss over an operand chain: <c>t… /* c */</c> ends at 122 with the comment, so the arrow breaks and
    ///     the chain's operands stay on the body's level.
    /// </summary>
    [Fact]
    public void ACommentAfterAnOperandChainsFirstOperand_CountsBesideTheArrow() =>
        Oracle.Agrees(
            """
            class C {
                void M() {
                    UUUUUUUU(p_www => txxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx /* c */ && bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb && cccccccccccccccccccccccccccccccccccccccccccc);
                }
            }
            """,
            """
            class C {
                void M() {
                    UUUUUUUU(p_www =>
                        txxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx /* c */
                        && bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb
                        && cccccccccccccccccccccccccccccccccccccccccccc
                    );
                }
            }
            """
        );
}
