namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     #598, SK-DIV-0870 to SK-DIV-0874: a parenthesised operator with one member after it, cast or not, as the whole
///     value of an <c>=</c> or a <c>return</c>. Every expected string is <c>jb cleanupcode</c>'s own output, measured
///     2026-11-14 with <c>Testing ask</c>; round 10's commit (afaed3a50) differed from it on every method here.
/// </summary>
public sealed class CastBreakAndAnchorIssue598Tests {
    [Fact]
    public void ACallOnAParenthesisedOperator_NestsItsArgumentsFromTheLineItStartsOn() =>
        Oracle.Agrees(
            """
            class C {
                void M1() {
                    y += (T)((aa && Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb)).Where(y);
                }

                void M2() {
                    x = (T)(aaa * Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb).Where(y);
                }

                void M3() {
                    if (c) {
                        x = (string)((aa + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb)).Contains(value);
                    }
                }
            }
            """,
            """
            class C {
                void M1() {
                    y += (T)((aa
                        && Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb)).Where(
                        y
                    );
                }

                void M2() {
                    x = (T)(aaa
                        * Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb).Where(
                        y
                    );
                }

                void M3() {
                    if (c) {
                        x = (string)((aa
                            + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb)).Contains(
                            value
                        );
                    }
                }
            }
            """
        );

    [Fact]
    public void OneColumnOver_ACastBreaksBeforeTheOperatorBehindAHeadThatKeepsItsEquals() =>
        Oracle.Agrees(
            """
            class C {
                void M1() {
                    return (string)(aa + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb).M(aaaa, bbbb);
                }

                void M2() {
                    return (List<int>)(aa + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb).Select(x);
                }

                void M3() {
                    return (List<int>)(aa + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb).M(aaaa, bbbb);
                }
            }
            """,
            """
            class C {
                void M1() {
                    return (string)
                        (aa + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb).M(
                            aaaa,
                            bbbb
                        );
                }

                void M2() {
                    return (List<int>)
                        (aa + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb).Select(x);
                }

                void M3() {
                    return (List<int>)
                        (aa + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb).M(
                            aaaa,
                            bbbb
                        );
                }
            }
            """
        );

    [Fact]
    public void ADoubledParenthesis_BreaksItsCastWhereEitherCloseLandsOneColumnOver() =>
        Oracle.Agrees(
            """
            class C {
                void M1() {
                    x = (string)((aa + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb)).ToUpperInvariant();
                }

                void M2() {
                    x = (List<int>)((aa + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb)).Select(x);
                }

                void M3() {
                    x = (List<int>)((aa + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb)).M(aaaa, bbbb);
                }
            }
            """,
            """
            class C {
                void M1() {
                    x = (string)
                        ((aa + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb))
                        .ToUpperInvariant();
                }

                void M2() {
                    x = (List<int>)
                        ((aa + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb))
                        .Select(x);
                }

                void M3() {
                    x = (List<int>)
                        ((aa + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb)).M(
                            aaaa,
                            bbbb
                        );
                }
            }
            """
        );

    [Fact]
    public void ACallsParenthesisOnTheMargin_BreaksTheCastBehindAWideCast() =>
        Oracle.Agrees(
            """
            class C {
                void M1() {
                    return (Tzzzzzzzzzzzzzzzz)((aa + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb)).M(aaaa, bbbb);
                }

                void M2() {
                    return (Tzzzzzzzzzzzzzzzzzz)((aa + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb)).M(aaaa, bbbb);
                }

                void M3() {
                    return (Tzzzzzzzzzzzzzzzzzzzz)((aa + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb)).M(aaaa, bbbb);
                }
            }
            """,
            """
            class C {
                void M1() {
                    return (Tzzzzzzzzzzzzzzzz)
                        ((aa + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb)).M(aaaa, bbbb);
                }

                void M2() {
                    return (Tzzzzzzzzzzzzzzzzzz)
                        ((aa + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb)).M(aaaa, bbbb);
                }

                void M3() {
                    return (Tzzzzzzzzzzzzzzzzzzzz)
                        ((aa + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb)).M(aaaa, bbbb);
                }
            }
            """
        );

    [Fact]
    public void BelowABrokenEquals_TheCastTakesALineOfItsOwn() =>
        Oracle.Agrees(
            """
            class C {
                void M1() {
                    string value = (IReadOnlyList<int>)(aa + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb).M();
                }

                void M2() {
                    string value = (IReadOnlyList<int>)(aa + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb).ToList();
                }

                void M3() {
                    string value = (IReadOnlyList<int>)(aa + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb).Length;
                }
            }
            """,
            """
            class C {
                void M1() {
                    string value =
                        (IReadOnlyList<int>)
                        (aa + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb).M();
                }

                void M2() {
                    string value =
                        (IReadOnlyList<int>)
                        (aa + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb).ToList();
                }

                void M3() {
                    string value =
                        (IReadOnlyList<int>)
                        (aa + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb).Length;
                }
            }
            """
        );

    [Fact]
    public void ADoubledParenthesisWithACall_KeepsTheEqualsUncastBehindAShortHead() =>
        Oracle.Agrees(
            """
            class C {
                void M1() {
                    this.x = ((aa + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb)).M();
                }

                void M2() {
                    this.x = ((aa + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb)).Select(x);
                }

                void M3() {
                    this.x = ((aa + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb)).M(aaaa, bbbb);
                }
            }
            """,
            """
            class C {
                void M1() {
                    this.x = ((aa
                        + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb)).M();
                }

                void M2() {
                    this.x = ((aa
                            + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb))
                        .Select(x);
                }

                void M3() {
                    this.x = ((aa
                        + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb)).M(
                        aaaa,
                        bbbb
                    );
                }
            }
            """
        );

    [Fact]
    public void ADoubledParenthesisWithACall_BreaksTheEqualsCastWhereItsInnerCloseLandsOneOver() =>
        Oracle.Agrees(
            """
            class C {
                void M1() {
                    this.x = (T)((aa + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb)).M();
                }

                void M2() {
                    this.x = (T)((aa + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb)).Select(x);
                }

                void M3() {
                    this.x = (T)((aa + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb)).M(aaaa, bbbb);
                }
            }
            """,
            """
            class C {
                void M1() {
                    this.x =
                        (T)((aa + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb))
                        .M();
                }

                void M2() {
                    this.x =
                        (T)((aa + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb))
                        .Select(x);
                }

                void M3() {
                    this.x =
                        (T)((aa + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb)).M(
                            aaaa,
                            bbbb
                        );
                }
            }
            """
        );

    [Fact]
    public void ACallTooWideForTheLineBelow_BreaksItsEqualsThreeLater() =>
        Oracle.Agrees(
            """
            class C {
                void M1() {
                    int[] itemsss = (string)(aa + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb).Contains(value);
                }

                void M2() {
                    if (c) {
                        int[] items = (T)(aaaaaaa * Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb).M(aaaa, bbbb);
                    }
                }

                void M3() {
                    if (c) {
                        List<int> s = (List<int>)((aaaaaaaaaaaa && Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb)).Substring(1, 2);
                    }
                }
            }
            """,
            """
            class C {
                void M1() {
                    int[] itemsss =
                        (string)(aa + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb)
                        .Contains(value);
                }

                void M2() {
                    if (c) {
                        int[] items =
                            (T)(aaaaaaa * Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb).M(
                                aaaa,
                                bbbb
                            );
                    }
                }

                void M3() {
                    if (c) {
                        List<int> s =
                            (List<int>)((aaaaaaaaaaaa && Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb))
                            .Substring(1, 2);
                    }
                }
            }
            """
        );

    [Fact]
    public void AShortNameBehindAWideType_BreaksBeforeTheNameOneColumnOver() =>
        Oracle.Agrees(
            """
            class C {
                void M1() {
                    Dictionary<string, int> d = (T)(aaaaaa + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb).M();
                }

                void M2() {
                    Dictionary<string, int> d = (T)(aaaaaa + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb).Select(x);
                }

                void M3() {
                    Dictionary<string, int> d = (T)(aaaaaa + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb).M(aaaa, bbbb);
                }
            }
            """,
            """
            class C {
                void M1() {
                    Dictionary<string, int>
                        d = (T)(aaaaaa + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb).M();
                }

                void M2() {
                    Dictionary<string, int>
                        d = (T)(aaaaaa + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb).Select(x);
                }

                void M3() {
                    Dictionary<string, int>
                        d = (T)(aaaaaa + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb).M(aaaa, bbbb);
                }
            }
            """
        );

    [Fact]
    public void AShortNameBehindAWideType_KeepsItsEqualsBeforeANarrowCast() =>
        Oracle.Agrees(
            """
            class C {
                void M1() {
                    Dictionary<string, int> d = (T)(aaaaaa + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb).M();
                }

                void M2() {
                    Dictionary<string, int> d = (T)(aaaaaa + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb).Length;
                }

                void M3() {
                    Dictionary<string, int> d = (T)(aaaaaa + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb).SomePropertyName;
                }
            }
            """,
            """
            class C {
                void M1() {
                    Dictionary<string, int> d = (T)(aaaaaa + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb)
                        .M();
                }

                void M2() {
                    Dictionary<string, int> d = (T)(aaaaaa + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb)
                        .Length;
                }

                void M3() {
                    Dictionary<string, int> d = (T)(aaaaaa + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb)
                        .SomePropertyName;
                }
            }
            """
        );
}
