namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     Round eight of #598: <c>(T)(a op b…)</c> followed by a call with arguments or a property, and the shapes round
///     seven left on the ordering rule. Every expected string is <c>jb cleanupcode</c>'s own output, measured 2026-11-13
///     with <c>Testing ask</c> on random probes (SK-DIV-0575 round 8); master differed from it on every method here.
/// </summary>
public sealed class CastParenMemberIssue598Tests {
    [Fact]
    public void ACastCallWithOneArgument_BehindAShortHead_BreaksItsDotOnceTheParenIsTwoPast() =>
        Oracle.Agrees(
            """
            class C {
                void M1() {
                    this.x = (T)(aaaaaaaaaaaaaaa && Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb).Take(10);
                }

                void M2() {
                    this.x = (T)(aaaa + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb).Take(10);
                }

                void M3() {
                    this.x = (List<int>)(aaaaaaaaaaaaaaaaaa && Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb).Take(10);
                }
            }
            """,
            """
            class C {
                void M1() {
                    this.x = (T)(aaaaaaaaaaaaaaa && Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb)
                        .Take(10);
                }

                void M2() {
                    this.x = (T)(aaaa + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb)
                        .Take(10);
                }

                void M3() {
                    this.x = (List<int>)(aaaaaaaaaaaaaaaaaa && Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb)
                        .Take(10);
                }
            }
            """
        );

    [Fact]
    public void ACastCallWithArguments_AfterReturn_BreaksItsDotOrChopsOnTheReceiversLastLine() =>
        Oracle.Agrees(
            """
            class C {
                void M1() {
                    return (T)(aaaaaaaaaaaaaa * Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb).Substring(1, 2);
                }

                void M2() {
                    return (IReadOnlyList<int>)(aaaaaaaaaaaaaaaaaaaaaaaaa + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb).M(aaaa, bbbb);
                }
            }
            """,
            """
            class C {
                void M1() {
                    return (T)(aaaaaaaaaaaaaa * Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb)
                        .Substring(1, 2);
                }

                void M2() {
                    return (IReadOnlyList<int>)(aaaaaaaaaaaaaaaaaaaaaaaaa + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb)
                        .M(aaaa, bbbb);
                }
            }
            """
        );

    [Fact]
    public void ACastCall_AfterACompoundAssignment_BreaksItsDot() =>
        Oracle.Agrees(
            """
            class C {
                void M1() {
                    y += (IDictionary<int, string>)(aaaaaaaaaaaaaa * Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb).Trim();
                }

                void M2() {
                    y += (string)(aaaa ?? Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb).GetHashCode();
                }
            }
            """,
            """
            class C {
                void M1() {
                    y += (IDictionary<int, string>)(aaaaaaaaaaaaaa * Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb)
                        .Trim();
                }

                void M2() {
                    y += (string)(aaaa ?? Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb)
                        .GetHashCode();
                }
            }
            """
        );

    [Fact]
    public void ACastCall_BehindAShortTypedHead_TakesTheShortHeadsTable() =>
        Oracle.Agrees(
            """
            class C {
                void M1() {
                    int k = (List<int>)(aaaaaaaaaaaaaaaaaaaaaaaaaaa ?? Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb).Take(10);
                }

                void M2() {
                    long n = (T)(aaaaaaaaaaaaaaaaaaaaaaa ?? Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb).Append(itemmmm);
                }

                void M3() {
                    long n = (T)(aaaaaaaaaaaaaaaaaa ?? Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb).Select(x);
                }
            }
            """,
            """
            class C {
                void M1() {
                    int k = (List<int>)(aaaaaaaaaaaaaaaaaaaaaaaaaaa ?? Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb)
                        .Take(10);
                }

                void M2() {
                    long n =
                        (T)(aaaaaaaaaaaaaaaaaaaaaaa ?? Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb).Append(itemmmm);
                }

                void M3() {
                    long n =
                        (T)(aaaaaaaaaaaaaaaaaa ?? Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb).Select(x);
                }
            }
            """
        );

    [Fact]
    public void ACastCallWithOneArgument_WhoseParenFitsBeside_MovesDownOrChopsByTheMeasuredForms() =>
        Oracle.Agrees(
            """
            class C {
                void M1() {
                    var zzzz = (string)(aaaa ?? Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb).M(q);
                }

                void M2() {
                    var zzzz = (List<int>)(aaaaaaaa ?? Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb).Append(itemmmm);
                }

                void M3() {
                    this.x = (string)(aaaaa + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb).Select(xxxxxxxxxx);
                }

                void M4() {
                    var yy = (IDictionary<int, string>)(aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb).Select(xxxxxxxxxx);
                }
            }
            """,
            """
            class C {
                void M1() {
                    var zzzz =
                        (string)(aaaa ?? Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb).M(q);
                }

                void M2() {
                    var zzzz =
                        (List<int>)(aaaaaaaa ?? Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb).Append(itemmmm);
                }

                void M3() {
                    this.x =
                        (string)(aaaaa + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb).Select(
                            xxxxxxxxxx
                        );
                }

                void M4() {
                    var yy =
                        (IDictionary<int, string>)(aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb).Select(
                            xxxxxxxxxx
                        );
                }
            }
            """
        );

    [Fact]
    public void ACastCallWithArguments_BehindAWideHead_MovesDown() =>
        Oracle.Agrees(
            """
            class C {
                void M1() {
                    var valueeeeeeee = (List<int>)(aaa * Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb).Replace(oldValue, newValue);
                }

                void M2() {
                    string value = (T)(aaaaaaa ?? Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb).Append(itemmmm);
                }

                void M3() {
                    var resultttttttttttt = (List<int>)(aaaaaaaaaaaaaa * Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb).M(aaaa, bbbb);
                }

                void M4() {
                    List<int> s = (T)(aaaaaaaaaaaaaaaaaaa ?? Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb).M(aaaa, bbbb);
                }
            }
            """,
            """
            class C {
                void M1() {
                    var valueeeeeeee =
                        (List<int>)(aaa * Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb).Replace(oldValue, newValue);
                }

                void M2() {
                    string value =
                        (T)(aaaaaaa ?? Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb).Append(itemmmm);
                }

                void M3() {
                    var resultttttttttttt =
                        (List<int>)(aaaaaaaaaaaaaa * Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb).M(
                            aaaa,
                            bbbb
                        );
                }

                void M4() {
                    List<int> s =
                        (T)(aaaaaaaaaaaaaaaaaaa ?? Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb).M(
                            aaaa,
                            bbbb
                        );
                }
            }
            """
        );

    [Fact]
    public void ACastProperty_BehindAWideHead_MovesDownOrBreaksItsDot() =>
        Oracle.Agrees(
            """
            class C {
                void M1() {
                    var valueeeeeeee = (T)(aa - Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb).Count;
                }

                void M2() {
                    Dictionary<string, List<int>> map = (string)(aaaaaaaaaaaaaaaaaaaaaaaaaaaa + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb).SomePropertyName;
                }

                void M3() {
                    int[] items = (IReadOnlyList<int>)(aaaaaaaaaaa * Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb).SomePropertyName;
                }

                void M4() {
                    string value = (IReadOnlyList<int>)(aaaaaaaaaaaaaaaaaaaaaaaaaaaaa ?? Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb).Value;
                }
            }
            """,
            """
            class C {
                void M1() {
                    var valueeeeeeee =
                        (T)(aa - Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb)
                        .Count;
                }

                void M2() {
                    Dictionary<string, List<int>> map =
                        (string)(aaaaaaaaaaaaaaaaaaaaaaaaaaaa + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb).SomePropertyName;
                }

                void M3() {
                    int[] items =
                        (IReadOnlyList<int>)(aaaaaaaaaaa * Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb).SomePropertyName;
                }

                void M4() {
                    string value =
                        (IReadOnlyList<int>)(aaaaaaaaaaaaaaaaaaaaaaaaaaaaa ?? Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb)
                        .Value;
                }
            }
            """
        );

    [Fact]
    public void ACastValue_WhoseReceiverRunsPastTheMarginBesideAWideHead_MovesDownAndBreaksItsDot() =>
        Oracle.Agrees(
            """
            class C {
                void M1() {
                    int[] items = (IDictionary<int, string>)(aaaaaaaaaa ?? Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb).Select(x);
                }

                void M2() {
                    int[] items = (IDictionary<int, string>)(aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb).GetHashCode();
                }

                void M3() {
                    string value = (string)(aaaaaaaaaaaaaaaaaaaaaaaaa ?? Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb).Take(10);
                }
            }
            """,
            """
            class C {
                void M1() {
                    int[] items =
                        (IDictionary<int, string>)(aaaaaaaaaa ?? Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb)
                        .Select(x);
                }

                void M2() {
                    int[] items =
                        (IDictionary<int, string>)(aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb)
                        .GetHashCode();
                }

                void M3() {
                    string value =
                        (string)(aaaaaaaaaaaaaaaaaaaaaaaaa ?? Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb).Take(
                            10
                        );
                }
            }
            """
        );

    [Fact]
    public void ADoubledParenthesis_TakesTheSingleOnesRules() =>
        Oracle.Agrees(
            """
            class C {
                void M1() {
                    if (c) {
                        IReadOnlyList<string> result = (List<int>)((aaaaaaaaaaaaaaaa * Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb)).Select(x);
                    }
                }

                void M2() {
                    List<int> s = (string)((aaaaaa ?? Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb)).ToList();
                }

                void M3() {
                    if (c) {
                        x = (IDictionary<int, string>)((aaaaa + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb)).Where(y);
                    }
                }

                void M4() {
                    var yy = (List<int>)((aaaaaaaaaaaaaaaaaaaaaa + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb)).Append(itemmmm);
                }
            }
            """,
            """
            class C {
                void M1() {
                    if (c) {
                        IReadOnlyList<string> result =
                            (List<int>)((aaaaaaaaaaaaaaaa * Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb)).Select(x);
                    }
                }

                void M2() {
                    List<int> s =
                        (string)((aaaaaa ?? Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb))
                        .ToList();
                }

                void M3() {
                    if (c) {
                        x = (IDictionary<int, string>)((aaaaa + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb))
                            .Where(y);
                    }
                }

                void M4() {
                    var yy =
                        (List<int>)((aaaaaaaaaaaaaaaaaaaaaa + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb)).Append(
                            itemmmm
                        );
                }
            }
            """
        );

    [Fact]
    public void AnUncastCall_BehindAShortHead_TakesTheCastTable() =>
        Oracle.Agrees(
            """
            class C {
                void M1() {
                    long n = (aaaa && Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb).ToList();
                }

                void M2() {
                    if (c) {
                        int k = (aaaaaaaaaaaaaaaaaaaa * Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb).Where(y);
                    }
                }

                void M3() {
                    if (c) {
                        long n = (aa * Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb).Take(10);
                    }
                }

                void M4() {
                    if (c) {
                        var x = (aaa && Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb).M();
                    }
                }
            }
            """,
            """
            class C {
                void M1() {
                    long n = (aaaa && Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb)
                        .ToList();
                }

                void M2() {
                    if (c) {
                        int k = (aaaaaaaaaaaaaaaaaaaa * Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb)
                            .Where(y);
                    }
                }

                void M3() {
                    if (c) {
                        long n = (aa
                                * Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb)
                            .Take(10);
                    }
                }

                void M4() {
                    if (c) {
                        var x = (aaa
                                && Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb)
                            .M();
                    }
                }
            }
            """
        );

    [Fact]
    public void AParenthesisedValue_ThatTookTwoPasses_SettlesOnPassOne() =>
        Oracle.Agrees(
            """
            class C {
                void M1() {
                    if (c) {
                        if (c) {
                            y += (T)((aaa * Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb)).Value;
                        }
                    }
                }

                void M2() {
                    if (c) {
                        if (c) {
                            long n = (aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa && Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb);
                        }
                    }
                }
            }
            """,
            """
            class C {
                void M1() {
                    if (c) {
                        if (c) {
                            y += (T)((aaa
                                    * Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb))
                                .Value;
                        }
                    }
                }

                void M2() {
                    if (c) {
                        if (c) {
                            long n =
                                (aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa && Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb);
                        }
                    }
                }
            }
            """
        );
}
