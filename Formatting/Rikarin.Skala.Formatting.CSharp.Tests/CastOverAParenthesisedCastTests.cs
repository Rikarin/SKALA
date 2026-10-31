namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     An <c>=</c> before a cast over a parenthesised cast of an atom — <c>(A)(((B)x)</c> — measures what would move
///     below it up to the inner cast's <c>)</c>, the last resort a line there takes, and an author's break there alike.
///     Expected strings are <c>jb cleanupcode</c>'s own output (measured 2026-10-31 with <c>Testing ask</c>) except where
///     a remark says otherwise.
/// </summary>
public sealed class CastOverAParenthesisedCastTests {
    /// <summary>
    ///     Fuzz 3254860842460080159, minimised: at 135 columns pass one kept the <c>=</c>, measuring the whole value too
    ///     wide below, and broke after <c>((object First, int Second))</c> past the margin; pass two, the tail ending at
    ///     that break, broke the <c>=</c>. The <c>=</c> now breaks on both — as the oracle does on the second input,
    ///     which is this test's expected output. ⚠ On the first, flat input the oracle also breaks after the outer cast,
    ///     a point Skala does not have; that remains a divergence, and is pinned here at Skala's settled answer.
    /// </summary>
    [Fact]
    public void ATailTooWideBelow_StillBreaksTheEqualsBeforeTheInnerCast() =>
        Oracle.Agrees(
            """
            class C {
                void M() {
               Dictionary<string, bool> v285 = ((CancellationToken First, (double First, object Second) Second))((   (object First   , int Second))$"value {Compute(value):F2}");
                }

                void A2() {
                    Dictionary<string, bool> v285 = ((CancellationToken First, (double First, object Second) Second))(((object First, int Second))
                        $"value {Compute(value):F2}");
                }
            }
            """,
            """
            class C {
                void M() {
                    Dictionary<string, bool> v285 =
                        ((CancellationToken First, (double First, object Second) Second))(((object First, int Second))
                            $"value {Compute(value):F2}");
                }

                void A2() {
                    Dictionary<string, bool> v285 =
                        ((CancellationToken First, (double First, object Second) Second))(((object First, int Second))
                            $"value {Compute(value):F2}");
                }
            }
            """
        );

    /// <summary>
    ///     A value that fits whole on the line below moves there, the inner cast's <c>)</c> no break at all — the
    ///     oracle's answer, and Skala's before and after the fix: the tail's end changes nothing for it.
    /// </summary>
    [Fact]
    public void AValueThatFitsBelow_MovesDownWhole() =>
        Oracle.Agrees(
            """
            class C {
                void A5() {
                    Dictionary<string, bool> v285 = ((CancellationToken First, (double First, object Second) Second))((object)$"value {Compute(value):F2}");
                }

                void A7() {
                    Dictionary<string, bool> v285 = ((CancellationToken First, (double First, object Second) Second))(((object First, int Second))$"v");
                }
            }
            """,
            """
            class C {
                void A5() {
                    Dictionary<string, bool> v285 =
                        ((CancellationToken First, (double First, object Second) Second))((object)$"value {Compute(value):F2}");
                }

                void A7() {
                    Dictionary<string, bool> v285 =
                        ((CancellationToken First, (double First, object Second) Second))(((object First, int Second))$"v");
                }
            }
            """
        );
}
