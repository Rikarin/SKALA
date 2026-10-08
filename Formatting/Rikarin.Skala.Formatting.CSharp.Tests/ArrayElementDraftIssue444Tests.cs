namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     Issue #444, SK-DIV-0208: an array initializer's fill measures an element flat, with the author's
///     kept breaks read as spaces and up to the first line of a comment or literal that spans lines, and
///     the element after one that spanned lines starts a line of its own. Every expected string is
///     <c>jb cleanupcode</c>'s own output for the input, and <see cref="Oracle.Agrees" /> asserts the
///     second pass too.
/// </summary>
public sealed class ArrayElementDraftIssue444Tests {
    const string Long1 = "FormatDiagnosticIds.FileIoFailed";

    /// <summary>
    ///     An element is measured flat with its kept breaks read as spaces: <c>Compute(</c> / arguments / <c>)</c> goes back
    ///     beside the elements before it when <c>Compute(alpha, beta)</c> fits there, and an element that does not fit flat
    ///     goes below.
    /// </summary>
    [Fact]
    public void AnElementIsMeasuredFlat_KeptBreaksIgnored() =>
        Oracle.Agrees(
            """
            class C {
                void M() {
                    var a1 = new object[] { alphaValue, betaValue, Compute(alphaArgumentValue, betaArgumentValue, gammaArgumentValue, deltaArgumentValue, epsilon), tail };
                    var a2 = new object[] { alphaValue, betaValue, Compute(alphaArgumentValue, betaArgumentValue, gammaArgumentValue, deltaArgumentValue, epsilonArgumentValue, zetaArgumentValue), tail };
                    var a3 = new object[] { alphaValue, betaValue, new[] { alphaArgumentValue, betaArgumentValue, gammaArgumentValue, deltaArgumentValue, epsilonArgumentValue, zeta }, tail };
                    var a4 = new object[] { alphaValue, betaValue, new[] { alphaArgumentValue, betaArgumentValue, gammaArgumentValue, deltaArgumentValue, epsilonArgumentValue, zetaArgumentValue, eta }, tail };
                    var a5 = new object[] { alphaValue, betaValue, new List<int> { alphaArgumentValue, betaArgumentValue, gammaArgumentValue, deltaArgumentValue, epsilonArgumentValue, zetaArgumentValue }, tail };
                    var a6 = new object[] { alphaValue, betaValue, x => Compute(alphaArgumentValue, betaArgumentValue, gammaArgumentValue, deltaArgumentValue, epsilonArgumentValue, zeta), tail };
                    var a7 = new object[] { alphaValue, betaValue, alphaArgumentValue + betaArgumentValue + gammaArgumentValue + deltaArgumentValue + epsilonArgumentValue + zeta, tail };
                    var a8 = new object[] { alphaValue, Compute(alphaArgumentValue, betaArgumentValue, gammaArgumentValue), Compute(alphaArgumentValue, betaArgumentValue, gammaArgumentValue), tail };
                    var a9 = new object[] {
                        alphaValue, betaValue, Compute(
                            alphaArgumentValue,
                            betaArgumentValue
                        ), tail
                    };
                    var b1 = new object[] {
                        alphaValue, betaValue,
                        Compute(
                            alphaArgumentValue,
                            betaArgumentValue
                        ),
                        tail
                    };
                }
            }
            """,
            """
            class C {
                void M() {
                    var a1 = new object[] {
                        alphaValue, betaValue,
                        Compute(alphaArgumentValue, betaArgumentValue, gammaArgumentValue, deltaArgumentValue, epsilon), tail
                    };
                    var a2 = new object[] {
                        alphaValue, betaValue,
                        Compute(
                            alphaArgumentValue,
                            betaArgumentValue,
                            gammaArgumentValue,
                            deltaArgumentValue,
                            epsilonArgumentValue,
                            zetaArgumentValue
                        ),
                        tail
                    };
                    var a3 = new object[] {
                        alphaValue, betaValue,
                        new[] {
                            alphaArgumentValue, betaArgumentValue, gammaArgumentValue, deltaArgumentValue, epsilonArgumentValue,
                            zeta
                        },
                        tail
                    };
                    var a4 = new object[] {
                        alphaValue, betaValue,
                        new[] {
                            alphaArgumentValue, betaArgumentValue, gammaArgumentValue, deltaArgumentValue, epsilonArgumentValue,
                            zetaArgumentValue, eta
                        },
                        tail
                    };
                    var a5 = new object[] {
                        alphaValue, betaValue,
                        new List<int> {
                            alphaArgumentValue,
                            betaArgumentValue,
                            gammaArgumentValue,
                            deltaArgumentValue,
                            epsilonArgumentValue,
                            zetaArgumentValue
                        },
                        tail
                    };
                    var a6 = new object[] {
                        alphaValue, betaValue,
                        x => Compute(
                            alphaArgumentValue,
                            betaArgumentValue,
                            gammaArgumentValue,
                            deltaArgumentValue,
                            epsilonArgumentValue,
                            zeta
                        ),
                        tail
                    };
                    var a7 = new object[] {
                        alphaValue, betaValue,
                        alphaArgumentValue
                        + betaArgumentValue
                        + gammaArgumentValue
                        + deltaArgumentValue
                        + epsilonArgumentValue
                        + zeta,
                        tail
                    };
                    var a8 = new object[] {
                        alphaValue, Compute(alphaArgumentValue, betaArgumentValue, gammaArgumentValue),
                        Compute(alphaArgumentValue, betaArgumentValue, gammaArgumentValue), tail
                    };
                    var a9 = new object[] {
                        alphaValue, betaValue, Compute(
                            alphaArgumentValue,
                            betaArgumentValue
                        ),
                        tail
                    };
                    var b1 = new object[] {
                        alphaValue, betaValue, Compute(
                            alphaArgumentValue,
                            betaArgumentValue
                        ),
                        tail
                    };
                }
            }
            """
        );

    /// <summary>
    ///     The element after one that spans lines starts a line of its own, whatever it is.
    /// </summary>
    [Fact]
    public void TheElementAfterOneThatSpansLines_StartsALine() =>
        Oracle.Agrees(
            """
            class C {
                void M() {
                    var c1 = new object[] { alphaValue, Compute(alphaArgumentValue, betaArgumentValue, gammaArgumentValue, deltaArgumentValue, epsilonArgumentValue, zetaArgumentValue), tail };
                    var c2 = new object[] { alphaValue, Compute(alphaArgumentValue, betaArgumentValue, gammaArgumentValue, deltaArgumentValue, epsilonArgumentValue, zetaArgumentValue), Compute(alphaArgumentValue, betaArgumentValue, gammaArgumentValue, deltaArgumentValue, epsilonArgumentValue, zetaArgumentValue) };
                    var c3 = new object[] { alphaValue, Compute(alphaArgumentValue, betaArgumentValue, gammaArgumentValue, deltaArgumentValue, epsilonArgumentValue, zetaArgumentValue), Compute(alphaArgumentValue, betaArgumentValue, gammaArgumentValue) };
                    var c4 = new object[] { alphaValue, Compute(alphaArgumentValue, betaArgumentValue, gammaArgumentValue, deltaArgumentValue, epsilonArgumentValue, zetaArgumentValue), ["ss", @"verbatim\path", "ssssssssssssss", null, 3_000_000L, alphaArgumentValue, betaArgumentValue, rest] };
                    var c5 = new object[] { alphaValue, Compute(alphaArgumentValue, betaArgumentValue, gammaArgumentValue, deltaArgumentValue, epsilonArgumentValue, zetaArgumentValue), new[] { "ss", @"verbatim\path", "ssssssssssssss", null, 3_000_000L, alphaArgumentValue, betaArgumentValue, rest } };
                    var c6 = new object[] { alphaValue, Compute(alphaArgumentValue, betaArgumentValue, gammaArgumentValue, deltaArgumentValue, epsilonArgumentValue, zetaArgumentValue), [1, 2] };
                    var c7 = new object[] { alphaValue, new[] { alphaArgumentValue, betaArgumentValue, gammaArgumentValue, deltaArgumentValue, epsilonArgumentValue, zetaArgumentValue, eta }, ["ss", @"verbatim\path", "ssssssssssssss", null, 3_000_000L, alphaArgumentValue, betaArgumentValue, rest] };
                    var c8 = new object[] { alphaValue, betaValue, ["ss", @"verbatim\path", "ssssssssssssss", null, 3_000_000L, alphaArgumentValue, betaArgumentValue, rest] };
                }
            }
            """,
            """
            class C {
                void M() {
                    var c1 = new object[] {
                        alphaValue,
                        Compute(
                            alphaArgumentValue,
                            betaArgumentValue,
                            gammaArgumentValue,
                            deltaArgumentValue,
                            epsilonArgumentValue,
                            zetaArgumentValue
                        ),
                        tail
                    };
                    var c2 = new object[] {
                        alphaValue,
                        Compute(
                            alphaArgumentValue,
                            betaArgumentValue,
                            gammaArgumentValue,
                            deltaArgumentValue,
                            epsilonArgumentValue,
                            zetaArgumentValue
                        ),
                        Compute(
                            alphaArgumentValue,
                            betaArgumentValue,
                            gammaArgumentValue,
                            deltaArgumentValue,
                            epsilonArgumentValue,
                            zetaArgumentValue
                        )
                    };
                    var c3 = new object[] {
                        alphaValue,
                        Compute(
                            alphaArgumentValue,
                            betaArgumentValue,
                            gammaArgumentValue,
                            deltaArgumentValue,
                            epsilonArgumentValue,
                            zetaArgumentValue
                        ),
                        Compute(alphaArgumentValue, betaArgumentValue, gammaArgumentValue)
                    };
                    var c4 = new object[] {
                        alphaValue,
                        Compute(
                            alphaArgumentValue,
                            betaArgumentValue,
                            gammaArgumentValue,
                            deltaArgumentValue,
                            epsilonArgumentValue,
                            zetaArgumentValue
                        ),
                        ["ss", @"verbatim\path", "ssssssssssssss", null, 3_000_000L, alphaArgumentValue, betaArgumentValue, rest]
                    };
                    var c5 = new object[] {
                        alphaValue,
                        Compute(
                            alphaArgumentValue,
                            betaArgumentValue,
                            gammaArgumentValue,
                            deltaArgumentValue,
                            epsilonArgumentValue,
                            zetaArgumentValue
                        ),
                        new[] {
                            "ss", @"verbatim\path", "ssssssssssssss", null, 3_000_000L, alphaArgumentValue, betaArgumentValue, rest
                        }
                    };
                    var c6 = new object[] {
                        alphaValue,
                        Compute(
                            alphaArgumentValue,
                            betaArgumentValue,
                            gammaArgumentValue,
                            deltaArgumentValue,
                            epsilonArgumentValue,
                            zetaArgumentValue
                        ),
                        [1, 2]
                    };
                    var c7 = new object[] {
                        alphaValue,
                        new[] {
                            alphaArgumentValue, betaArgumentValue, gammaArgumentValue, deltaArgumentValue, epsilonArgumentValue,
                            zetaArgumentValue, eta
                        },
                        ["ss", @"verbatim\path", "ssssssssssssss", null, 3_000_000L, alphaArgumentValue, betaArgumentValue, rest]
                    };
                    var c8 = new object[] {
                        alphaValue, betaValue,
                        ["ss", @"verbatim\path", "ssssssssssssss", null, 3_000_000L, alphaArgumentValue, betaArgumentValue, rest]
                    };
                }
            }
            """
        );

    /// <summary>
    ///     A comment that spans lines inside an element is measured to its first line; the element after it starts a line of
    ///     its own.
    /// </summary>
    [Fact]
    public void ACommentInsideAnElement_IsMeasuredToItsFirstLine() =>
        Oracle.Agrees(
            """
            class C {
                void M() {
                    var b1 = new[] { 1, Compute(2, /* a
                      b */ 3), 4 };
                    var b2 = new[] { new[] { 1, 2 /* a
                      b */, 3 }, new[] { 4 } };
                    var b3 = new[] {
                        first is string,
                        second
                            is string
                    };
                    var b4 = new[] { 1, Compute(
                        2), 3, 4 };
                    var b5 = new[] { 1, 2, Compute(
                        2), 3, 4, 5 };
                    var b6 = new[] { Compute(
                        2), 3, 4 };
                    int[] b7 = [1, Compute(
                        2), 3];
                }

                void N() {
                    var a = new Func<int>[] { () => 1, () => {
                        return 2;
                    }, () => 3 };
                    var b = new[] { 1, 2,
                        3, 4 };
                    var c = new[] {
                        new[] { 1, 2 },
                        new[] { 3, 4 }, new[] { 5 }
                    };
                    var d = new[] { Compute(alphaArgumentValueNumberOne, betaArgumentValueNumberTwo, gammaArgumentValueNumberThree), 2, 3 };
                    var e = new[] { 1, Compute(alphaArgumentValueNumberOne, betaArgumentValueNumberTwo, gammaArgumentValueNumberThree), 3 };
                    var g = new[] { 1, x switch {
                        1 => 2,
                        _ => 3
                    }, 4 };
                    var h = new object[] { 1, new {
                        A = 1
                    }, 2 };
                }
            }
            """,
            """
            class C {
                void M() {
                    var b1 = new[] {
                        1, Compute(
                            2, /* a
                              b */
                            3
                        ),
                        4
                    };
                    var b2 = new[] {
                        new[] {
                            1, 2 /* a
                              b */,
                            3
                        },
                        new[] { 4 }
                    };
                    var b3 = new[] {
                        first is string, second
                            is string
                    };
                    var b4 = new[] { 1, Compute(2), 3, 4 };
                    var b5 = new[] { 1, 2, Compute(2), 3, 4, 5 };
                    var b6 = new[] { Compute(2), 3, 4 };
                    int[] b7 = [1, Compute(2), 3];
                }

                void N() {
                    var a = new Func<int>[] { () => 1, () => { return 2; }, () => 3 };
                    var b = new[] { 1, 2, 3, 4 };
                    var c = new[] { new[] { 1, 2 }, new[] { 3, 4 }, new[] { 5 } };
                    var d = new[] {
                        Compute(alphaArgumentValueNumberOne, betaArgumentValueNumberTwo, gammaArgumentValueNumberThree), 2, 3
                    };
                    var e = new[] {
                        1, Compute(alphaArgumentValueNumberOne, betaArgumentValueNumberTwo, gammaArgumentValueNumberThree), 3
                    };
                    var g = new[] {
                        1, x switch {
                            1 => 2,
                            _ => 3
                        },
                        4
                    };
                    var h = new object[] { 1, new { A = 1 }, 2 };
                }
            }
            """
        );

    /// <summary>
    ///     Block lambdas, switch expressions, anonymous objects and a raw string as elements: a raw string's first line is its
    ///     measure too.
    /// </summary>
    [Fact]
    public void OtherElementsThatSpanLines() =>
        Oracle.Agrees(
            """"
            class C {
                void M() {
                    var a = new Func<int>[] { () => 1, () => {
                        return 2;
                    }, () => 3 };
                    var b = new[] { 1, 2,
                        3, 4 };
                    var c = new[] {
                        new[] { 1, 2 },
                        new[] { 3, 4 }, new[] { 5 }
                    };
                    var d = new[] { Compute(alphaArgumentValueNumberOne, betaArgumentValueNumberTwo, gammaArgumentValueNumberThree), 2, 3 };
                    var e = new[] { 1, Compute(alphaArgumentValueNumberOne, betaArgumentValueNumberTwo, gammaArgumentValueNumberThree), 3 };
                    var f = new[] { "a", """
                        raw
                        """, "b" };
                    var g = new[] { 1, x switch {
                        1 => 2,
                        _ => 3
                    }, 4 };
                    var h = new object[] { 1, new {
                        A = 1
                    }, 2 };
                }
            }
            """",
            """"
            class C {
                void M() {
                    var a = new Func<int>[] { () => 1, () => { return 2; }, () => 3 };
                    var b = new[] { 1, 2, 3, 4 };
                    var c = new[] { new[] { 1, 2 }, new[] { 3, 4 }, new[] { 5 } };
                    var d = new[] {
                        Compute(alphaArgumentValueNumberOne, betaArgumentValueNumberTwo, gammaArgumentValueNumberThree), 2, 3
                    };
                    var e = new[] {
                        1, Compute(alphaArgumentValueNumberOne, betaArgumentValueNumberTwo, gammaArgumentValueNumberThree), 3
                    };
                    var f = new[] {
                        "a", """
                             raw
                             """,
                        "b"
                    };
                    var g = new[] {
                        1, x switch {
                            1 => 2,
                            _ => 3
                        },
                        4
                    };
                    var h = new object[] { 1, new { A = 1 }, 2 };
                }
            }
            """"
        );

    /// <summary>
    ///     A <c>//</c> comment between elements is a break in front of the next element, not inside the one before it.
    /// </summary>
    [Fact]
    public void ALineCommentBetweenElements_IsNotAnElementSpanningLines() =>
        Oracle.Agrees(
            $$"""
            class C {
                void M() {
                    foreach (var (id, site) in new[] {
                                 ("SK9098", "ArrangementRule"), // ArrangeIds.Reverted
                                 ("SK9015", "FormatDiagnosticIds"), // {{Long1}}
                                 ("SK9099", "FormatDiagnosticIds"), ("SK9001", "SkalaDiagnostic"), // ConfigDiagnosticIds.UnknownKey
                                 ("SK0201", "ArrangementRule")
                             }) {
                    }
                    var x = new[] {
                        1, // a
                        2, 3
                    };
                    foreach (var (name, description) in new[] {
                                 ("create",
                                     "Accept everything that fires now, replacing any existing baseline."),
                                 ("update",
                                     "Accept what fires now in addition to what is already accepted. Never removes."),
                                 ("show", "What the baseline holds.")
                             }) {
                    }
                }
            }
            """,
            $$"""
            class C {
                void M() {
                    foreach (var (id, site) in new[] {
                                 ("SK9098", "ArrangementRule"), // ArrangeIds.Reverted
                                 ("SK9015", "FormatDiagnosticIds"), // {{Long1}}
                                 ("SK9099", "FormatDiagnosticIds"), ("SK9001", "SkalaDiagnostic"), // ConfigDiagnosticIds.UnknownKey
                                 ("SK0201", "ArrangementRule")
                             }) { }

                    var x = new[] {
                        1, // a
                        2, 3
                    };
                    foreach (var (name, description) in new[] {
                                 ("create",
                                     "Accept everything that fires now, replacing any existing baseline."),
                                 ("update",
                                     "Accept what fires now in addition to what is already accepted. Never removes."),
                                 ("show", "What the baseline holds.")
                             }) { }
                }
            }
            """
        );
}
