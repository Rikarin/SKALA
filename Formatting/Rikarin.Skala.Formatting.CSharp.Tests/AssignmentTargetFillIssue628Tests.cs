namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     Issue #628: an assignment whose target is a plain member-access chain too wide with its <c>=</c> for the line
///     breaks at the target's dot and keeps the <c>=</c> — on pass one and on pass two, whatever the value. Every
///     expected string is <c>jb cleanupcode</c>'s own output, measured 2026-10-15 with <c>Testing ask</c>.
/// </summary>
public sealed class AssignmentTargetFillIssue628Tests {
    /// <summary>
    ///     Fuzz 10722289349277649158, minimised. Pass one measured the target flat through the <c>=</c>, found it
    ///     overflowing and kept the <c>=</c>. Pass two read the dot break as the author's, so the group's point width
    ///     ended at that dot, the head fitted and the ordering rule broke the <c>=</c>. <c>B</c> is pass one's output,
    ///     which the oracle leaves as it is.
    /// </summary>
    [Fact]
    public void ATargetBrokenAtItsDot_KeepsTheEqualsOnBothPasses() =>
        Oracle.Agrees(
            """
            class C {
                void A() {
                    Aaaaaaaaaaaaaaaa.Bbbbbbbbbbbbbbbbbbbb.Cccccccccccccccccccc_wwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwww.Dddddddddddddddddd.MorexxxxxxxxxxxxxxxValue = yyyyyyyy;
                }

                void B() {
                    Aaaaaaaaaaaaaaaa.Bbbbbbbbbbbbbbbbbbbb.Cccccccccccccccccccc_wwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwww.Dddddddddddddddddd
                        .MorexxxxxxxxxxxxxxxValue = yyyyyyyy;
                }
            }
            """,
            """
            class C {
                void A() {
                    Aaaaaaaaaaaaaaaa.Bbbbbbbbbbbbbbbbbbbb.Cccccccccccccccccccc_wwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwww.Dddddddddddddddddd
                        .MorexxxxxxxxxxxxxxxValue = yyyyyyyy;
                }

                void B() {
                    Aaaaaaaaaaaaaaaa.Bbbbbbbbbbbbbbbbbbbb.Cccccccccccccccccccc_wwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwww.Dddddddddddddddddd
                        .MorexxxxxxxxxxxxxxxValue = yyyyyyyy;
                }
            }
            """
        );

    /// <summary>
    ///     A member access or a call for a value is the same layout: the target's dot takes the break and the value
    ///     stays beside the <c>=</c>. The member-value and callee rules, asked first, read the overflowing head as a
    ///     reason to break the <c>=</c> as well. A target that fits through its <c>=</c> stays whole, and the value
    ///     moves down.
    /// </summary>
    [Fact]
    public void AMemberOrCallValue_StaysBesideTheTargetsLastLink() =>
        Oracle.Agrees(
            """
            class C {
                void C1() {
                    if (c) {
                        Aaaaaaaaaaaaaaaa.Ccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc.MoxxxxxxxxxxValue = oo.Ppp;
                    }
                }

                void D() {
                    Aaaaaaaaaaaaaaaa.Ccccccccccccccccccccccccccccccccccccccccccccccccccccc.Bbbbbbbbbbbbbbbbbbbb.Dddddddddddddddddd.MoxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxValue = M(aaaa, bbbbbbb);
                }

                void E() {
                    Aaaaaaaaaaaaaaaa.Bbbbbbbbbbbbbbbbbbbb.Cccccccccccccccccccc_wwwwwwww.Dddddddddddddddddd.MorexxxxxxxxxValue = yyyyyyyyyyyyyyyyyyyyyyyyy;
                }
            }
            """,
            """
            class C {
                void C1() {
                    if (c) {
                        Aaaaaaaaaaaaaaaa.Ccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc
                            .MoxxxxxxxxxxValue = oo.Ppp;
                    }
                }

                void D() {
                    Aaaaaaaaaaaaaaaa.Ccccccccccccccccccccccccccccccccccccccccccccccccccccc.Bbbbbbbbbbbbbbbbbbbb.Dddddddddddddddddd
                        .MoxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxValue = M(aaaa, bbbbbbb);
                }

                void E() {
                    Aaaaaaaaaaaaaaaa.Bbbbbbbbbbbbbbbbbbbb.Cccccccccccccccccccc_wwwwwwww.Dddddddddddddddddd.MorexxxxxxxxxValue =
                        yyyyyyyyyyyyyyyyyyyyyyyyy;
                }
            }
            """
        );
}
