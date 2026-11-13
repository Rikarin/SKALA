namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     An <c>=</c> before a cast over a parenthesised cast of an atom — <c>(A)(((B)x)</c> — measures what would move
///     below it up to the inner cast's <c>)</c>, the last resort a line there takes, and an author's break there alike.
///     Expected strings are <c>jb cleanupcode</c>'s own output (measured 2026-10-31 with <c>Testing ask</c>) except where
///     a remark says otherwise.
/// </summary>
public sealed class CastOverAParenthesisedCastTests {
    /// <summary>
    ///     Fuzz 3254860842460080159, minimised: at 135 columns pass one kept the <c>=</c>, measuring the whole value
    ///     too wide below, and broke after <c>((object First, int Second))</c> past the margin; pass two, the tail
    ///     ending at that break, broke the <c>=</c>. The <c>=</c> now breaks on both — as the oracle does on the second
    ///     input, which is this test's expected output. ⚠ On the first, flat input the oracle also breaks after the
    ///     outer cast, a point Skala does not have; that remains a divergence, and is pinned here at Skala's settled
    ///     answer.
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

    /// <summary>
    ///     Fuzz 18236595362986456582 (generated, mutations indent and join-line), minimised: the <c>=</c> measured its
    ///     tail to the inner cast's last-resort <c>)</c> (GroupFacts.TailStopsAtLastResort), found it fitting below and
    ///     broke, but the line below then broke first inside <c>(IReadOnlyList&lt;</c>, a type argument fill that comes
    ///     before the last resort. Pass two read that break as the author's, ended the tail there and joined the
    ///     <c>=</c>. The cast types of this shape no longer fill, so the line below ends where the tail said it would.
    ///     ⚠ Skala's settled answer, not the oracle's: on the flat input the oracle breaks after the outer cast instead
    ///     (SK-DIV-0608).
    /// </summary>
    [Fact]
    public void ATypeArgumentFillNeverTakesTheLineTheEqualsMeasuredToTheLastResort() =>
        Settles(
            """
            namespace N {
                class C {
                    async Task M136() {
                        try { (CancellationToken? First, Dictionary<bool, List<bool>> Second) v141 = (IReadOnlyList<Nullable<char>>)((Dictionary<Memory<StringBuilder>, List<Memory<StringBuilder>>>)"ss");
                        } finally {
                        }
                    }
                }
            }
            """,
            """
            namespace N {
                class C {
                    async Task M136() {
                        try {
                            (CancellationToken? First, Dictionary<bool, List<bool>> Second) v141 =
                                (IReadOnlyList<Nullable<char>>)((Dictionary<Memory<StringBuilder>, List<Memory<StringBuilder>>>)
                                    "ss");
                        } finally { }
                    }
                }
            }
            """
        );

    /// <summary>Skala's output is <paramref name="expected" />, and a second pass changes nothing.</summary>
    static void Settles(string source, string expected) {
        var once = Format.Text(source);
        Assert.Equal(expected, once.TrimEnd('\n'));
        Assert.Equal(once, Format.Text(once));
    }

    /// <summary>
    ///     The oracle never breaks inside either cast's type arguments in this shape (0 of 1 167 rows, measured
    ///     2026-11-13 with <c>Testing ask</c>); past the margin a <c>return</c> keeps both casts on its line and breaks
    ///     at the inner cast's <c>)</c>, the operand one level in. Skala filled <c>Nullable&lt;</c>,
    ///     <c>Memory&lt;…&gt;,</c> and <c>List&lt;</c> first.
    /// </summary>
    [Fact]
    public void AReturnBreaksAtTheInnerCastAndNeverInsideATypeArgumentList() =>
        Oracle.Agrees(
            """
            class C {
                object A() {
                    return (IReadOnlyList<Nullable<char>>)((Dictionary<Memory<StringBuilder>, List<Memory<StringBuilder>>>)"sssssssssssssssssssssssssssssssssss");
                }

                object B() {
                    return (object)((Dictionary<Memory<StringBuilder>, List<Memory<StringBuilder>>>)"ssssssssssssssssssssssssssssssssssssssssssssssssssssssssss");
                }

                object C() {
                    return (List<Tx>)((string)"ssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssss");
                }
            }
            """,
            """
            class C {
                object A() {
                    return (IReadOnlyList<Nullable<char>>)((Dictionary<Memory<StringBuilder>, List<Memory<StringBuilder>>>)
                        "sssssssssssssssssssssssssssssssssss");
                }

                object B() {
                    return (object)((Dictionary<Memory<StringBuilder>, List<Memory<StringBuilder>>>)
                        "ssssssssssssssssssssssssssssssssssssssssssssssssssssssssss");
                }

                object C() {
                    return (List<Tx>)((string)
                        "ssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssss");
                }
            }
            """
        );
}
