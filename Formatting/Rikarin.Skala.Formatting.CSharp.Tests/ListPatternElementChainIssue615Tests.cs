namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     Issue #615 (fuzz 7321373205094285321): a pattern chain inside a list pattern's element or a subpattern's
///     value. The oracle keeps a fill's element head beside the comma when the chain inside it is broken, puts the
///     chain's links on the element's column (no level of their own, the parenthesis's neither), and starts the
///     element after a multi-line one on a line of its own. Every expected string is <c>jb cleanupcode</c>'s own
///     output, measured 2026-10-10 with <c>Testing ask</c>.
/// </summary>
public sealed class ListPatternElementChainIssue615Tests {
    /// <summary>
    ///     The seed's shape, flat and as pass one wrote it. Before the fix the `or`s sat a level deeper and pass two,
    ///     reading the chain's breaks as kept, moved `not (0` to a line of its own.
    /// </summary>
    [Fact]
    public void AnElementsBrokenChain_KeepsItsHeadAndItsColumn() =>
        Oracle.Agrees(
            """
            class C {
                void A() {
                    foreach (var e43 in source is [null, not (0 or 1
             or 2)]) {
                        value++;
                    }
                }
                void B() {
                    foreach (var e43 in source is [
                                 null, not (0
                                     or 1
                                     or 2)
                             ]) {
                        value++;
                    }
                }
                void D() {
                    var x = source is [null, not (0 or 1
             or 2)];
                }
                void E() {
                    if (source is [null, not (0 or 1
             or 2)]) { }
                }
            }
            """,
            """
            class C {
                void A() {
                    foreach (var e43 in source is [
                                 null, not (0
                                 or 1
                                 or 2)
                             ]) {
                        value++;
                    }
                }

                void B() {
                    foreach (var e43 in source is [
                                 null, not (0
                                 or 1
                                 or 2)
                             ]) {
                        value++;
                    }
                }

                void D() {
                    var x = source is [
                        null, not (0
                        or 1
                        or 2)
                    ];
                }

                void E() {
                    if (source is [
                            null, not (0
                            or 1
                            or 2)
                        ]) { }
                }
            }
            """
        );

    /// <summary>
    ///     A list element, bare and parenthesised, a subpattern's value, and the element after a multi-line one.
    ///     `x is not (0` / `or 1)` and the positional pattern are the controls, unchanged.
    /// </summary>
    [Fact]
    public void ElementsAndSubpatterns_PutTheLinksOnTheElementsColumn() =>
        Oracle.Agrees(
            """
            class C {
                void A() {
                    var a = x is not (0
                        or 1);
                    var b = x is [not (0
                        or 1)];
                    var c = x is (1, not (0
                        or 1));
                    var d = x is { P: not (0
                        or 1) };
                    var e = x is [1, (0
                        or 1)];
                    var f = x is [1, 0
                        or 1];
                    var g = x is [1, 2, not (0
                        or 1), 3, 4];
                    var h = x is [
                        1, not (0 or 1), 3
                    ];
                }
            }
            """,
            """
            class C {
                void A() {
                    var a = x is not (0
                        or 1);
                    var b = x is [
                        not (0
                        or 1)
                    ];
                    var c = x is (1, not (0
                        or 1));
                    var d = x is {
                        P: not (0
                        or 1)
                    };
                    var e = x is [
                        1, (0
                        or 1)
                    ];
                    var f = x is [
                        1, 0
                        or 1
                    ];
                    var g = x is [
                        1, 2, not (0
                        or 1),
                        3, 4
                    ];
                    var h = x is [
                        1, not (0 or 1), 3
                    ];
                }
            }
            """
        );
}
