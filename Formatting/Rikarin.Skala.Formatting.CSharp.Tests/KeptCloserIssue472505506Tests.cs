namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     Issues #472, #505 and #506 (SK-DIV-0114, SK-DIV-0204): where a closing delimiter the author kept on a line
///     of its own sits, and where a switch governed by one nests its arms. Every expected string is
///     <c>jb cleanupcode</c>'s own output for the input, and each is checked on a second pass;
///     <c>constructs/syntax/kept-closer-continuation.cs</c> holds the wider set.
/// </summary>
public sealed class KeptCloserIssue472505506Tests {
    /// <summary>
    ///     #505: a grouping parenthesis's <c>)</c> is a continuation line of the statement, paid for by the
    ///     statement where nothing else has, and by nothing under an arrow that already broke.
    /// </summary>
    [Fact]
    public void AGroupingCloser_IsTheStatementsContinuationLine() =>
        Oracle.Agrees(
            """
            class C {
                object M() {
                    (a
                    ).B();
                    return (a
                    );
                }

                object A() =>
                    (a + b
                    );
            }
            """,
            """
            class C {
                object M() {
                    (a
                        ).B();
                    return (a
                        );
                }

                object A() =>
                    (a + b
                    );
            }
            """
        );

    /// <summary>
    ///     #472: a list the oracle only fills, a tuple type and a function pointer's parameters keep their closer
    ///     one level past the opener's line, under an arrow and at a member's level too.
    /// </summary>
    [Fact]
    public void AFilledListsCloser_SitsOneLevelPastItsOpenersLine() =>
        Oracle.Agrees(
            """
            unsafe class C {
                (int a, int b
                ) M() => default;
                bool B(object o) => o is (1, 2
                );
                delegate*<int, void
                > F;
            }
            """,
            """
            unsafe class C {
                (int a, int b
                    ) M() =>
                    default;

                bool B(object o) =>
                    o is (1, 2
                        );

                delegate*<int, void
                    > F;
            }
            """
        );

    /// <summary>
    ///     #506: a switch governed by a tuple whose <c>)</c> was kept keeps that <c>)</c> one level past the
    ///     <c>(</c>'s line and nests its arms from it — after <c>=</c>, after <c>return</c> and under an arrow.
    /// </summary>
    [Fact]
    public void ASwitchGovernedByAKeptCloser_NestsFromItsLine() =>
        Oracle.Agrees(
            """
            class C {
                object M() {
                    var t = (1, 2
                    ) switch {
                        _ => 0
                    };
                    return (1, 2
                    ) switch {
                        _ => 0
                    };
                }

                object U() =>
                    (1, 2
                    ) switch {
                        _ => 0
                    };
            }
            """,
            """
            class C {
                object M() {
                    var t = (1, 2
                        ) switch {
                            _ => 0
                        };
                    return (1, 2
                        ) switch {
                            _ => 0
                        };
                }

                object U() =>
                    (1, 2
                        ) switch {
                            _ => 0
                        };
            }
            """
        );

    /// <summary>
    ///     #505's boundary, from the Lint drift its first cut caused: a grouping or a tuple broken after its
    ///     <c>(</c> closes on its opener's level (#443), after <c>return</c> as anywhere.
    /// </summary>
    [Fact]
    public void ACloserAfterABreakAfterTheOpener_ComesBackToTheOpener() =>
        Oracle.Agrees(
            """
            class C {
                object M() {
                    (
                        first
                    ).B();
                    return (
                        first,
                        second
                    );
                }
            }
            """,
            """
            class C {
                object M() {
                    (
                        first
                    ).B();
                    return (
                        first,
                        second
                    );
                }
            }
            """
        );
}
