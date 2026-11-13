namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     #598, SK-DIV-0830 and SK-DIV-0831: one parenthesis around a binary operator, or two, with one member after it, cast
///     or not, as the whole value of an <c>=</c> behind a head of twelve columns or more. Every expected string is
///     <c>jb cleanupcode</c>'s own output, measured 2026-11-13 with <c>Testing ask</c>; the start commit differed from it
///     on every method here.
/// </summary>
public sealed class ParenMemberNameIssue598Tests {
    [Fact]
    public void AMemberPastTheMargin_KeepsItsEqualsOnceTheFragmentAndTheLineBelowAreWideEnough() =>
        Oracle.Agrees(
            """
            class C {
                void M1() {
                    var vvvvvvvvvvvvvv = (aaaaaaaaaaaaaaaa + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb).M();
                }

                void M2() {
                    var vvvvvvvvvvvvvv = (aaaaaaaaaaaaaaaaaaaaaaaa + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb).ToList();
                }

                void M3() {
                    var vvvvvvvvvvvvvv = (aaaaaaaaaaaaaaaaaaaaaaaa + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb).Select(x);
                }
            }
            """,
            """
            class C {
                void M1() {
                    var vvvvvvvvvvvvvv = (aaaaaaaaaaaaaaaa
                        + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb).M();
                }

                void M2() {
                    var vvvvvvvvvvvvvv = (aaaaaaaaaaaaaaaaaaaaaaaa
                        + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb).ToList();
                }

                void M3() {
                    var vvvvvvvvvvvvvv = (aaaaaaaaaaaaaaaaaaaaaaaa
                        + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb).Select(x);
                }
            }
            """
        );

    [Fact]
    public void AShortFirstOperandAgainstTheName_BreaksTheEqualsBeforeAMember() =>
        Oracle.Agrees(
            """
            class C {
                void M1() {
                    var vvvvvvvvvvvvvv = (aa + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb).Length;
                }

                void M2() {
                    var vvvvvvvvvvvvvv = (aa + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb).M(aaaa, bbbb);
                }

                void M3() {
                    var vvvvvvvvvvvvvv = (T)(aaaaaa + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb).M();
                }
            }
            """,
            """
            class C {
                void M1() {
                    var vvvvvvvvvvvvvv =
                        (aa + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb).Length;
                }

                void M2() {
                    var vvvvvvvvvvvvvv =
                        (aa + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb)
                        .M(aaaa, bbbb);
                }

                void M3() {
                    var vvvvvvvvvvvvvv =
                        (T)(aaaaaa
                            + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb)
                        .M();
                }
            }
            """
        );

    [Fact]
    public void ACallWhoseReceiverFitsBeside_BreaksTheEqualsWhileTheValueBelowIsShortEnough() =>
        Oracle.Agrees(
            """
            class C {
                void M1() {
                    var vvvvvvvvvvvvvv = (aa + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb).M(aaaa, bbbb);
                }

                void M2() {
                    string vvvvvvvvvvvv = (aa + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb).M(aaaa, bbbb);
                }

                void M3() {
                    this.vvvvvvvvvvvv = (aa + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb).M(aaaa, bbbb);
                }
            }
            """,
            """
            class C {
                void M1() {
                    var vvvvvvvvvvvvvv =
                        (aa + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb).M(aaaa, bbbb);
                }

                void M2() {
                    string vvvvvvvvvvvv =
                        (aa + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb).M(aaaa, bbbb);
                }

                void M3() {
                    this.vvvvvvvvvvvv =
                        (aa + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb).M(aaaa, bbbb);
                }
            }
            """
        );

    [Fact]
    public void ACallWhoseReceiverFitsBeside_KeepsTheEqualsAndBreaksTheDotOnceTheValueBelowRunsLong() =>
        Oracle.Agrees(
            """
            class C {
                void M1() {
                    var vvvvvvvvvvvvvv = (aa + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb).ToList();
                }

                void M2() {
                    var vvvvvvvvvvvvvv = (aa + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb).Select(x);
                }

                void M3() {
                    string vvvvvvvvvvvv = (aa + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb).ToList();
                }
            }
            """,
            """
            class C {
                void M1() {
                    var vvvvvvvvvvvvvv = (aa + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb)
                        .ToList();
                }

                void M2() {
                    var vvvvvvvvvvvvvv = (aa + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb)
                        .Select(x);
                }

                void M3() {
                    string vvvvvvvvvvvv = (aa + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb)
                        .ToList();
                }
            }
            """
        );

    [Fact]
    public void AProperty_BreaksItsEqualsByItsOwnBound() =>
        Oracle.Agrees(
            """
            class C {
                void M1() {
                    var vvvvvvvvvvvvvv = (aa + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb).Length;
                }

                void M2() {
                    string vvvvvvvvvvvv = (aa + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb).Length;
                }

                void M3() {
                    this.vvvvvvvvvvvv = (T)(aa + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb).Length;
                }
            }
            """,
            """
            class C {
                void M1() {
                    var vvvvvvvvvvvvvv = (aa + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb)
                        .Length;
                }

                void M2() {
                    string vvvvvvvvvvvv = (aa + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb)
                        .Length;
                }

                void M3() {
                    this.vvvvvvvvvvvv =
                        (T)(aa + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb).Length;
                }
            }
            """
        );

    [Fact]
    public void ADoubledParenthesisWithAMember_TakesTheSingleForm() =>
        Oracle.Agrees(
            """
            class C {
                void M1() {
                    string value = ((aaaa && Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb)).ToList();
                }

                void M2() {
                    if (c) {
                        int[] items = ((aaaaaaaaaaaaaaaaaaa + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb)).GetHashCode();
                    }
                }

                void M3() {
                    if (c) {
                        List<int> s = ((aaaaaaaaaaaaaaaaaaaaaaaaaaaaa && Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb)).ToList();
                    }
                }
            }
            """,
            """
            class C {
                void M1() {
                    string value = ((aaaa && Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb))
                        .ToList();
                }

                void M2() {
                    if (c) {
                        int[] items = ((aaaaaaaaaaaaaaaaaaa + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb))
                            .GetHashCode();
                    }
                }

                void M3() {
                    if (c) {
                        List<int> s = ((aaaaaaaaaaaaaaaaaaaaaaaaaaaaa && Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb))
                            .ToList();
                    }
                }
            }
            """
        );

    [Fact]
    public void TwoArgumentsOneColumnOver_BreakTheDotRatherThanChop() =>
        Oracle.Agrees(
            """
            class C {
                void M1() {
                    var vvvvvvvvvvvvvv = (aaaaaa + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb).M(aaaa, bbbb);
                }

                void M2() {
                    string vvvvvvvvvvvv = (aa + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb).M(aaaa, bbbb);
                }

                void M3() {
                    this.vvvvvvvvvvvv = (aa + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb).M(aaaa, bbbb);
                }
            }
            """,
            """
            class C {
                void M1() {
                    var vvvvvvvvvvvvvv =
                        (aaaaaa + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb)
                        .M(aaaa, bbbb);
                }

                void M2() {
                    string vvvvvvvvvvvv =
                        (aa + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb)
                        .M(aaaa, bbbb);
                }

                void M3() {
                    this.vvvvvvvvvvvv =
                        (aa + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb)
                        .M(aaaa, bbbb);
                }
            }
            """
        );

    [Fact]
    public void AReceiverBrokenAtItsOperatorOneColumnOver_BreaksTheDotRatherThanChop() =>
        Oracle.Agrees(
            """
            class C {
                void M1() {
                    string value = (aaaaaaaaaaaaa + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb).Select(x);
                }

                void M2() {
                    string value = (aaaaaaaaaaaaaaaaaaaa + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb).Contains(value);
                }

                void M3() {
                    string value = (aa + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb).M(aaaa, bbbb);
                }
            }
            """,
            """
            class C {
                void M1() {
                    string value =
                        (aaaaaaaaaaaaa
                            + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb)
                        .Select(x);
                }

                void M2() {
                    string value =
                        (aaaaaaaaaaaaaaaaaaaa
                            + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb)
                        .Contains(value);
                }

                void M3() {
                    string value =
                        (aa
                            + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb)
                        .M(aaaa, bbbb);
                }
            }
            """
        );

    [Fact]
    public void AnUncastTypedLocalWithTwoArguments_BreaksItsDotBesideTheEquals() =>
        Oracle.Agrees(
            """
            class C {
                void M1() {
                    if (c) {
                        List<int> s = (aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa * Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb).Substring(1, 2);
                    }
                }

                void M2() {
                    if (c) {
                        string value = (aaaaaaaaaaaaaaaaaaaaaaaaa && Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb).Substring(1, 2);
                    }
                }

                void M3() {
                    if (c) {
                        int[] items = (aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa * Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb).M(a, b, c);
                    }
                }
            }
            """,
            """
            class C {
                void M1() {
                    if (c) {
                        List<int> s = (aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa * Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb)
                            .Substring(1, 2);
                    }
                }

                void M2() {
                    if (c) {
                        string value = (aaaaaaaaaaaaaaaaaaaaaaaaa && Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb)
                            .Substring(1, 2);
                    }
                }

                void M3() {
                    if (c) {
                        int[] items = (aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa * Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb)
                            .M(a, b, c);
                    }
                }
            }
            """
        );
}
