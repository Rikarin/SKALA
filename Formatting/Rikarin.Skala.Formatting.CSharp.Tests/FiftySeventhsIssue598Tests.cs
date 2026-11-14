namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     #598 round 14, SK-DIV-1030 to SK-DIV-1033: the wide head's <c>=</c> before a parenthesised operator, past the
///     margin
///     and beside the receiver, the bare value's, and the cast breaks before one, in 57ths of a column. Every expected
///     string is <c>jb cleanupcode</c>'s own output, measured 2026-11-14 with <c>Testing ask</c>; round 13 (6ae7fa830)
///     differed from it on every method here.
/// </summary>
public sealed class FiftySeventhsIssue598Tests {
    [Fact]
    public void AWideHead_PastTheMargin_KeepsItsEqualsByFiftySevenths() =>
        Oracle.Agrees(
            """
            class C {
                void M1() {
                    string value = (aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa && Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb).Take(10);
                }

                void M2() {
                    string value = (T)(aaaaaaaa + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb).M();
                }

                void M3() {
                    string value = (List<int>)(aaaaaaaa * Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb).Trim();
                }

                void M4() {
                    string value = (List<int>)(aaaaaaaaaaa && Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb).Count;
                }
            }
            """,
            """
            class C {
                void M1() {
                    string value = (aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa
                        && Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb).Take(10);
                }

                void M2() {
                    string value = (T)(aaaaaaaa
                        + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb).M();
                }

                void M3() {
                    string value = (List<int>)(aaaaaaaa
                        * Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb).Trim();
                }

                void M4() {
                    string value = (List<int>)(aaaaaaaaaaa
                        && Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb).Count;
                }
            }
            """
        );

    [Fact]
    public void AWideHead_PastTheMargin_BreaksItsEqualsByFiftySevenths() =>
        Oracle.Agrees(
            """
            class C {
                void M1() {
                    if (c) {
                        string value = ((aaaaaaaaaaaaaaaaaaaaaaaaaaaa - Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb)).Length;
                    }
                }

                void M2() {
                    string value = ((aaaaaaaaaaaaaaaaaaaaaaaa && Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb)).Count;
                }

                void M3() {
                    string value = (aaaaaaaaaaaaaaa - Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb).Value;
                }

                void M4() {
                    if (c) {
                        string value = (aaaaaaaaaaaaaa * Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb).Value;
                    }
                }
            }
            """,
            """
            class C {
                void M1() {
                    if (c) {
                        string value =
                            ((aaaaaaaaaaaaaaaaaaaaaaaaaaaa - Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb)).Length;
                    }
                }

                void M2() {
                    string value =
                        ((aaaaaaaaaaaaaaaaaaaaaaaa && Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb)).Count;
                }

                void M3() {
                    string value =
                        (aaaaaaaaaaaaaaa - Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb).Value;
                }

                void M4() {
                    if (c) {
                        string value =
                            (aaaaaaaaaaaaaa * Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb).Value;
                    }
                }
            }
            """
        );

    [Fact]
    public void AWideHead_Beside_AnswersByTheValuesEndBelow() =>
        Oracle.Agrees(
            """
            class C {
                void M1() {
                    if (c) {
                        string value = (aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb).Replace(oldValue, newValue);
                    }
                }

                void M2() {
                    string value = (List<int>)(aaaaaaaaaaaaaaaa || Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb).Trim();
                }

                void M3() {
                    string value = (List<int>)((aaa && Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb)).Trim();
                }

                void M4() {
                    if (c) {
                        string value = (List<int>)((aaaaa ?? Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb)).Trim();
                    }
                }
            }
            """,
            """
            class C {
                void M1() {
                    if (c) {
                        string value =
                            (aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb).Replace(
                                oldValue,
                                newValue
                            );
                    }
                }

                void M2() {
                    string value = (List<int>)(aaaaaaaaaaaaaaaa || Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb)
                        .Trim();
                }

                void M3() {
                    string value = (List<int>)((aaa && Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb))
                        .Trim();
                }

                void M4() {
                    if (c) {
                        string value = (List<int>)((aaaaa ?? Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb))
                            .Trim();
                    }
                }
            }
            """
        );

    [Fact]
    public void AWideHead_OneColumnOver_KeepsItsEqualsBeforeOneShortArgument() =>
        Oracle.Agrees(
            """
            class C {
                void M1() {
                    if (c) {
                        string value = (aaaaaaaaaaaaaa - Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb).Select(x);
                    }
                }

                void M2() {
                    if (c) {
                        string value = (aaaaaaaaaaaa - Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb).Where(y);
                    }
                }

                void M3() {
                    string value = (aaaaaaaaaaaaaaaaaaaaaaaa ?? Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb).M(q);
                }
            }
            """,
            """
            class C {
                void M1() {
                    if (c) {
                        string value = (aaaaaaaaaaaaaa - Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb)
                            .Select(x);
                    }
                }

                void M2() {
                    if (c) {
                        string value = (aaaaaaaaaaaa - Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb)
                            .Where(y);
                    }
                }

                void M3() {
                    string value = (aaaaaaaaaaaaaaaaaaaaaaaa ?? Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb)
                        .M(q);
                }
            }
            """
        );

    [Fact]
    public void ABareValue_KeepsOrBreaksItsEqualsByFiftySevenths() =>
        Oracle.Agrees(
            """
            class C {
                void M1() {
                    int[] items = (IReadOnlyList<int>)(aaaa + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb);
                }

                void M2() {
                    List<int> s = (IDictionary<int, string>)(aaaaaaaaaaaaaa * Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb);
                }

                void M3() {
                    string value = (List<int>)(aaaaaaaa + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb);
                }
            }
            """,
            """
            class C {
                void M1() {
                    int[] items =
                        (IReadOnlyList<int>)(aaaa
                            + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb);
                }

                void M2() {
                    List<int> s =
                        (IDictionary<int, string>)(aaaaaaaaaaaaaa
                            * Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb);
                }

                void M3() {
                    string value =
                        (List<int>)(aaaaaaaa
                            + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb);
                }
            }
            """
        );

    [Fact]
    public void ACastOverABareValue_BreaksBehindACompoundAssignmentAndDoubled() =>
        Oracle.Agrees(
            """
            class C {
                void M1() {
                    if (c) {
                        y += (IDictionary<int, string>)(aaaaaaaaaaaa * Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb);
                    }
                }

                void M2() {
                    var valueeeeeeee = (IDictionary<int, string>)((aaaaaaa ?? Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb));
                }

                void M3() {
                    if (c) {
                        return (IReadOnlyList<int>)((aaa + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb));
                    }
                }
            }
            """,
            """
            class C {
                void M1() {
                    if (c) {
                        y += (IDictionary<int, string>)
                            (aaaaaaaaaaaa * Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb);
                    }
                }

                void M2() {
                    var valueeeeeeee =
                        (IDictionary<int, string>)
                        ((aaaaaaa ?? Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb));
                }

                void M3() {
                    if (c) {
                        return (IReadOnlyList<int>)
                            ((aaa + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb));
                    }
                }
            }
            """
        );

    [Fact]
    public void ACastBeforeAParenthesisedMember_BreaksAtTheOperatorsParenthesis() =>
        Oracle.Agrees(
            """
            class C {
                void M1() {
                    return (List<int>)(aaaaaa + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb).Select(x);
                }

                void M2() {
                    return (IList<string>)(aaaaaaaaaaaaaaaaaaaaaaaa + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb).M(aaaa, bbbb);
                }

                void M3() {
                    x = (List<int>)(aaaaaa + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb).M(aaaa, bbbb);
                }

                void M4() {
                    x = (Tzzzzzzzzzzz)(aaaaaa + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb).Select(x);
                }
            }
            """,
            """
            class C {
                void M1() {
                    return (List<int>)
                        (aaaaaa + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb).Select(x);
                }

                void M2() {
                    return (IList<string>)
                        (aaaaaaaaaaaaaaaaaaaaaaaa + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb).M(aaaa, bbbb);
                }

                void M3() {
                    x = (List<int>)
                        (aaaaaa + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb).M(
                            aaaa,
                            bbbb
                        );
                }

                void M4() {
                    x = (Tzzzzzzzzzzz)
                        (aaaaaa + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb).Select(x);
                }
            }
            """
        );

    [Fact]
    public void ACastBelowABrokenEquals_BreaksWhereTheValueFitsItsLine() =>
        Oracle.Agrees(
            """
            class C {
                void M1() {
                    string value = (IReadOnlyList<int>)(aaaaaaaaaaaaa + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb).ToList();
                }

                void M2() {
                    string value = (IReadOnlyList<int>)(aaaaaaaaaa + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb).Length;
                }

                void M3() {
                    List<int> s = (IReadOnlyList<int>)(aaaaaaaaaaaaa + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb).ToList();
                }
            }
            """,
            """
            class C {
                void M1() {
                    string value =
                        (IReadOnlyList<int>)
                        (aaaaaaaaaaaaa + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb).ToList();
                }

                void M2() {
                    string value =
                        (IReadOnlyList<int>)
                        (aaaaaaaaaa + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb).Length;
                }

                void M3() {
                    List<int> s =
                        (IReadOnlyList<int>)
                        (aaaaaaaaaaaaa + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb).ToList();
                }
            }
            """
        );

    [Fact]
    public void AWideArgumentList_OnTheOperatorsLastLine_OneColumnOver_BreaksTheDot() =>
        Oracle.Agrees(
            """
            class C {
                void M1() {
                    if (c) {
                        string value = (T)(aaaaaaaaaaaaaaaaaaaaaaaaaaaa * Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb).Replace(oldValue, newValue);
                    }
                }

                void M2() {
                    if (c) {
                        string value = (T)((aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa * Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb)).Replace(oldValue, newValue);
                    }
                }
            }
            """,
            """
            class C {
                void M1() {
                    if (c) {
                        string value =
                            (T)(aaaaaaaaaaaaaaaaaaaaaaaaaaaa
                                * Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb)
                            .Replace(oldValue, newValue);
                    }
                }

                void M2() {
                    if (c) {
                        string value =
                            (T)((aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa
                                * Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb))
                            .Replace(oldValue, newValue);
                    }
                }
            }
            """
        );
}
