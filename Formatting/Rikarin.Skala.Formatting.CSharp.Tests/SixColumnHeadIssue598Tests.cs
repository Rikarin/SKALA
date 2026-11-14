namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     #598 round 13, SK-DIV-0990 and SK-DIV-0991: the <c>=</c> of a six-column head (<c>name =</c>,
///     <c>xxxx =</c>) before a call or a property on a parenthesised operator, and the break after a cast where a
///     call's <c>(</c> lands on the margin or one past it. Every expected string is <c>jb cleanupcode</c>'s own output,
///     measured 2026-11-14 with <c>Testing ask</c>; round 12's merge (35b7bff86) differed from it on every method here.
/// </summary>
public sealed class SixColumnHeadIssue598Tests {
    [Fact]
    public void ASixColumnHead_KeepsItsEqualsBeforeANarrowCast() =>
        Oracle.Agrees(
            """
            class C {
                void M1() {
                    name = (aa + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb).M();
                }

                void M2() {
                    if (c) {
                        name = (aa + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb).M();
                    }
                }

                void M3() {
                    name = (aa + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb).Select(x);
                }

                void M4() {
                    if (c) {
                        name = (aa + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb).Select(x);
                    }
                }
            }
            """,
            """
            class C {
                void M1() {
                    name = (aa + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb)
                        .M();
                }

                void M2() {
                    if (c) {
                        name = (aa + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb)
                            .M();
                    }
                }

                void M3() {
                    name = (aa + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb)
                        .Select(x);
                }

                void M4() {
                    if (c) {
                        name = (aa + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb)
                            .Select(x);
                    }
                }
            }
            """
        );

    [Fact]
    public void ASixColumnHead_BreaksItsEqualsBeforeAWideCast() =>
        Oracle.Agrees(
            """
            class C {
                void M1() {
                    name = (Tzzzzzzzzzzzz)(aa + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb).Take(10);
                }

                void M2() {
                    if (c) {
                        name = (Tzzzzzzzzzzzz)(aa + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb).Take(10);
                    }
                }

                void M3() {
                    xxxx = (Tzzzzzzzzzzzz)(aa + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb).Take(10);
                }

                void M4() {
                    if (c) {
                        xxxx = (Tzzzzzzzzzzzz)(aa + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb).Take(10);
                    }
                }
            }
            """,
            """
            class C {
                void M1() {
                    name =
                        (Tzzzzzzzzzzzz)(aa + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb).Take(
                            10
                        );
                }

                void M2() {
                    if (c) {
                        name =
                            (Tzzzzzzzzzzzz)(aa + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb).Take(
                                10
                            );
                    }
                }

                void M3() {
                    xxxx =
                        (Tzzzzzzzzzzzz)(aa + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb).Take(
                            10
                        );
                }

                void M4() {
                    if (c) {
                        xxxx =
                            (Tzzzzzzzzzzzz)(aa + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb).Take(
                                10
                            );
                    }
                }
            }
            """
        );

    [Fact]
    public void ASixColumnHead_OneColumnOver_KeepsItsEqualsBehindALongFirstOperand() =>
        Oracle.Agrees(
            """
            class C {
                void M1() {
                    name = (aaaaaaaaaaaaaa + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb).M();
                }

                void M2() {
                    name = (aaaaaaaaaaaaaaaaaa + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb).Count;
                }

                void M3() {
                    name = (aaaaaaaaaaaaaaaaaaaa + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb).Length;
                }

                void M4() {
                    name = (aaaaaaaaaaaaaaaa + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb).M(q);
                }
            }
            """,
            """
            class C {
                void M1() {
                    name = (aaaaaaaaaaaaaa
                        + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb).M();
                }

                void M2() {
                    name = (aaaaaaaaaaaaaaaaaa
                        + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb).Count;
                }

                void M3() {
                    name = (aaaaaaaaaaaaaaaaaaaa
                        + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb).Length;
                }

                void M4() {
                    name = (aaaaaaaaaaaaaaaa
                        + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb).M(q);
                }
            }
            """
        );

    [Fact]
    public void ASixColumnHead_Doubled_OneColumnOver_BreaksTheEqualsAndTheDot() =>
        Oracle.Agrees(
            """
            class C {
                void M1() {
                    name = (T)((aa + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb)).M();
                }

                void M2() {
                    name = (T)((aa + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb)).M(q);
                }

                void M3() {
                    name = (T)((aa + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb)).Take(10);
                }
            }
            """,
            """
            class C {
                void M1() {
                    name =
                        (T)((aa + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb))
                        .M();
                }

                void M2() {
                    name =
                        (T)((aa + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb))
                        .M(q);
                }

                void M3() {
                    name =
                        (T)((aa + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb))
                        .Take(10);
                }
            }
            """
        );

    [Fact]
    public void ACast_BreaksBeforeACallWhoseParenthesisLandsOnTheMargin() =>
        Oracle.Agrees(
            """
            class C {
                void M1() {
                    y += (IDictionary<int, string>)(aa + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb).M();
                }

                void M2() {
                    return (Tzzzzzzzzzzzzzzzzzzzz)((aa + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb)).M();
                }

                void M3() {
                    return (Tzzzzzzzzzzzzzzzzzzzz)((aa + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb)).Select(x);
                }

                void M4() {
                    return (Tzzzzzzzzzzzzzzzzzzzz)((aa + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb)).ToUpperInvariant();
                }
            }
            """,
            """
            class C {
                void M1() {
                    y += (IDictionary<int, string>)
                        (aa + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb).M();
                }

                void M2() {
                    return (Tzzzzzzzzzzzzzzzzzzzz)
                        ((aa + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb)).M();
                }

                void M3() {
                    return (Tzzzzzzzzzzzzzzzzzzzz)
                        ((aa + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb)).Select(x);
                }

                void M4() {
                    return (Tzzzzzzzzzzzzzzzzzzzz)
                        ((aa + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb)).ToUpperInvariant();
                }
            }
            """
        );
}
