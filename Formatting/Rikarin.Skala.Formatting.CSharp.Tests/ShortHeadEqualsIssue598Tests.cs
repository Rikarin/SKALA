namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     #598 round 12, SK-DIV-0920 to SK-DIV-0924: the <c>=</c>, the dot and the argument list of a call on a parenthesised
///     operator behind heads of three to eleven columns. Every expected string is <c>jb cleanupcode</c>'s own output,
///     measured 2026-11-14 with <c>Testing ask</c>; round 11's merge (007053a82) differed from it on every method here.
/// </summary>
public sealed class ShortHeadEqualsIssue598Tests {
    [Fact]
    public void AShortAssignment_KeepsItsEqualsAsXDoes() =>
        Oracle.Agrees(
            """
            class C {
                void M1() {
                    xx = (T)(aa + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb).M();
                }

                void M2() {
                    xx = (T)(aa + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb).Count;
                }

                void M3() {
                    xx = (T)(aa + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb).Select(x);
                }

                void M4() {
                    xx = (T)(aa + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb).M(aaaa, bbbb);
                }
            }
            """,
            """
            class C {
                void M1() {
                    xx = (T)(aa + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb)
                        .M();
                }

                void M2() {
                    xx = (T)(aa
                        + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb).Count;
                }

                void M3() {
                    xx = (T)(aa + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb)
                        .Select(x);
                }

                void M4() {
                    xx = (T)(aa
                        + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb).M(
                        aaaa,
                        bbbb
                    );
                }
            }
            """
        );

    [Fact]
    public void ACompoundAssignmentOfSeven_AnswersAsAPlainOne() =>
        Oracle.Agrees(
            """
            class C {
                void M1() {
                    yyyy += (T)(aa + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb).M();
                }

                void M2() {
                    yyyy += (T)(aa + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb).Count;
                }

                void M3() {
                    yyyy += (T)(aa + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb).Select(x);
                }

                void M4() {
                    yyyy += (T)(aa + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb).M(aaaa, bbbb);
                }
            }
            """,
            """
            class C {
                void M1() {
                    yyyy +=
                        (T)(aa + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb).M();
                }

                void M2() {
                    yyyy +=
                        (T)(aa + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb)
                        .Count;
                }

                void M3() {
                    yyyy +=
                        (T)(aa + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb).Select(x);
                }

                void M4() {
                    yyyy +=
                        (T)(aa + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb).M(
                            aaaa,
                            bbbb
                        );
                }
            }
            """
        );

    [Fact]
    public void AtIndentEight_OneShortArgumentMovesTheDot() =>
        Oracle.Agrees(
            """
            class C {
                void M1() {
                    x = (aaaa + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb).M(q);
                }

                void M2() {
                    x = (aaaa + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb).Select(x);
                }

                void M3() {
                    x = (T)(aaaa + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb).M(qq);
                }

                void M4() {
                    x = (T)(aaaa + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb).Take(10);
                }
            }
            """,
            """
            class C {
                void M1() {
                    x = (aaaa + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb)
                        .M(q);
                }

                void M2() {
                    x = (aaaa + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb)
                        .Select(x);
                }

                void M3() {
                    x = (T)(aaaa + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb)
                        .M(qq);
                }

                void M4() {
                    x = (T)(aaaa + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb)
                        .Take(10);
                }
            }
            """
        );

    [Fact]
    public void BehindSevenOrEight_OneArgumentKeepsTheEqualsByTheIndentAndTheArgument() =>
        Oracle.Agrees(
            """
            class C {
                void M1() {
                    if (c) {
                        var x = (aaaa + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb).M(qqq);
                    }
                }

                void M2() {
                    var x = (aaaa + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb).M(qqqqq);
                }

                void M3() {
                    if (c) {
                        var x = (aaaa + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb).M(qqqqq);
                    }
                }

                void M4() {
                    var x = (aaaa + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb).M(qqqqqqq);
                }
            }
            """,
            """
            class C {
                void M1() {
                    if (c) {
                        var x = (aaaa + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb).M(
                            qqq
                        );
                    }
                }

                void M2() {
                    var x = (aaaa + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb).M(
                        qqqqq
                    );
                }

                void M3() {
                    if (c) {
                        var x = (aaaa + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb).M(
                            qqqqq
                        );
                    }
                }

                void M4() {
                    var x = (aaaa + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb).M(
                        qqqqqqq
                    );
                }
            }
            """
        );

    [Fact]
    public void BehindSevenOrEight_OneArgumentBreaksTheEqualsByTheIndentAndTheArgument() =>
        Oracle.Agrees(
            """
            class C {
                void M1() {
                    var x = (T)(aaaa + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb).M(qqq);
                }

                void M2() {
                    var x = (T)(aaaa + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb).M(qqqqq);
                }

                void M3() {
                    var x = (T)(aaaa + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb).Select(qqqqq);
                }

                void M4() {
                    int k = (T)(aaaa + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb).M(qqq);
                }
            }
            """,
            """
            class C {
                void M1() {
                    var x =
                        (T)(aaaa + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb)
                        .M(qqq);
                }

                void M2() {
                    var x =
                        (T)(aaaa + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb)
                        .M(qqqqq);
                }

                void M3() {
                    var x =
                        (T)(aaaa + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb)
                        .Select(qqqqq);
                }

                void M4() {
                    int k =
                        (T)(aaaa + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb)
                        .M(qqq);
                }
            }
            """
        );

    [Fact]
    public void BehindTenOrEleven_TwoArgumentsBreakTheEqualsAtSmallIndents() =>
        Oracle.Agrees(
            """
            class C {
                void M1() {
                    var zzzz = (aaaa + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb).M(a, b);
                }

                void M2() {
                    var zzzz = (aaaa + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb).Substring(1, 2);
                }

                void M3() {
                    int total = (aaaa + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb).M(a, b);
                }

                void M4() {
                    if (c) {
                        int total = (aaaa + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb).M(a, b);
                    }
                }
            }
            """,
            """
            class C {
                void M1() {
                    var zzzz =
                        (aaaa + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb).M(a, b);
                }

                void M2() {
                    var zzzz =
                        (aaaa + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb).Substring(1, 2);
                }

                void M3() {
                    int total =
                        (aaaa + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb).M(a, b);
                }

                void M4() {
                    if (c) {
                        int total =
                            (aaaa + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb).M(a, b);
                    }
                }
            }
            """
        );

    [Fact]
    public void OneColumnOver_WideArgumentsMoveTheDotBehindAWideReceiver() =>
        Oracle.Agrees(
            """
            class C {
                void M1() {
                    x = (aaaa + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb).Replace(oldValue, newValue);
                }

                void M2() {
                    if (c) {
                        x = (aaaa + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb).Replace(oldValue, newValue);
                    }
                }

                void M3() {
                    x = (aaaa + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb).M(aaaaaaa, bbbbbbb);
                }
            }
            """,
            """
            class C {
                void M1() {
                    x = (aaaa + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb)
                        .Replace(oldValue, newValue);
                }

                void M2() {
                    if (c) {
                        x = (aaaa + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb)
                            .Replace(oldValue, newValue);
                    }
                }

                void M3() {
                    x = (aaaa + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb)
                        .M(aaaaaaa, bbbbbbb);
                }
            }
            """
        );
}
