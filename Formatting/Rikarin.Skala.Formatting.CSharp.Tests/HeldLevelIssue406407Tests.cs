namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     Issues #406 and #407 (SK-DIV-0157, SK-DIV-0158): where SK-DIV-0101's held level is decided. A
///     switch arm's body that opens with a parenthesis the author broke after holds the arm's level
///     while the arrow stays on the pattern's line, and gives it up once the arrow moves down — the
///     group before the arrow owns the level, so it is that group's resolution the writer reads. Under
///     <c>wrap_if_long</c> the level is held while the chain after the <c>)</c> stays whole, which only
///     the writer can know, so it lays the body out held and watches the chain. Every expected string
///     of an <see cref="Oracle.Agrees" /> test is <c>jb cleanupcode</c>'s own output for the input; the
///     fill's, asked with <c>resharper_csharp_wrap_chained_method_calls = wrap_if_long</c>, are noted
///     one by one.
/// </summary>
public sealed class HeldLevelIssue406407Tests {
    const string Args = "argumentNumberOne, argumentNumberTwo, argumentNumberThree";
    const string MidFirst = "SomeMethodName(argumentNumberOne, argumentNumberTwo)";
    const string MidLast = "OtherMethodName(argumentNumberOne, argumentNumberTwoo)";
    const string Mid = MidFirst + "." + MidLast;
    const string Whole = "SomeMethodName(argumentNumberOne).OtherMethodName(argumentNumberOne)";

    // ⚠ Arm patterns sized so that ` =>` ends at column 120 (held), at 121 (the arrow moves down)
    // and ` => (` at 121 (the break goes after the arrow, and the level is held again).
    static readonly string Held = "C." + new string('N', 103);
    static readonly string Moved = "C." + new string('M', 104);
    static readonly string Glued = "C." + new string('G', 102);

    static string UnderAFill(string source) =>
        Overridden.Settled(source, [("skala_wrap_chained_method_calls", "wrap_if_long")]).TrimEnd('\n');

    /// <summary>
    ///     The issue's shape and the bodies SK-DIV-0101 holds under a member's arrow: the <c>(</c> at
    ///     the arm's indent, its contents one level in, a ternary's <c>?</c> one level past the arm.
    /// </summary>
    [Fact]
    public void AnArmsBody_HoldsTheArmsLevel_ForEveryBodyThatHeldUnderAnArrow() =>
        Oracle.Agrees(
            """
            class T {
                object S(int k) =>
                    k switch {
                        1 =>
                            (
                                a).C(),
                        2 =>
                            (
                                a)[0][1],
                        3 =>
                            (
                                a)?.B(),
                        4 =>
                            (
                                a).B.C(),
                        5 =>
                            (
                                a ?? b)!.C(),
                        6 =>
                            (
                                a, b),
                        7 =>
                            (
                                a) ? b : c,
                        _ =>
                            (
                                a).C()
                    };

                object a, b, c;
            }
            """,
            """
            class T {
                object S(int k) =>
                    k switch {
                        1 =>
                        (
                            a).C(),
                        2 =>
                        (
                            a)[0][1],
                        3 =>
                        (
                            a)?.B(),
                        4 =>
                        (
                            a).B.C(),
                        5 =>
                        (
                            a ?? b)!.C(),
                        6 =>
                        (
                            a, b),
                        7 =>
                        (
                            a)
                            ? b
                            : c,
                        _ =>
                        (
                            a).C()
                    };

                object a, b, c;
            }
            """
        );

    /// <summary>
    ///     The same rule wherever the switch sits, and past a <c>when</c> clause.
    /// </summary>
    [Fact]
    public void AnArmsBody_HoldsTheLevel_UnderAWhenANestedSwitchAStatementALambdaAndAnArgument() =>
        Oracle.Agrees(
            """
            using System;

            class T {
                object A(int k) {
                    return k switch {
                        1 when k > 0 =>
                            (
                                a).C(),
                        { } =>
                            (
                                a, b) switch { _ => a },
                        _ => k switch {
                            2 =>
                                (
                                    a)[0],
                            _ => null
                        }
                    };
                }

                object B(int k) {
                    Func<int, object> f = x => x switch {
                        1 =>
                            (
                                a).C(),
                        _ => null
                    };
                    return M(k switch {
                        1 =>
                            (
                                a).C(),
                        _ => null
                    });
                }

                object M(object o) => o;

                object a, b;
            }
            """,
            """
            using System;

            class T {
                object A(int k) {
                    return k switch {
                        1 when k > 0 =>
                        (
                            a).C(),
                        { } =>
                        (
                            a, b) switch {
                            _ => a
                        },
                        _ => k switch {
                            2 =>
                            (
                                a)[0],
                            _ => null
                        }
                    };
                }

                object B(int k) {
                    Func<int, object> f = x => x switch {
                        1 =>
                        (
                            a).C(),
                        _ => null
                    };
                    return M(
                        k switch {
                            1 =>
                            (
                                a).C(),
                            _ => null
                        }
                    );
                }

                object M(object o) => o;

                object a, b;
            }
            """
        );

    /// <summary>
    ///     The control: a break before the arrow puts the arrow one level in and the body with it,
    ///     which is the level the group before the arrow spends once it broke.
    /// </summary>
    [Fact]
    public void AnArrowTheAuthorMovedDown_SpendsTheLevel() =>
        Oracle.Agrees(
            """
            class T {
                object S(int k) =>
                    k switch {
                        1
                        => (
                            a).C(),
                        2
                        =>
                        (
                            a).C(),
                        3 when k > 0
                        =>
                        (
                            a, b),
                        _ => null
                    };

                object a, b;
            }
            """,
            """
            class T {
                object S(int k) =>
                    k switch {
                        1
                            => (
                                a).C(),
                        2
                            =>
                            (
                                a).C(),
                        3 when k > 0
                            =>
                            (
                                a, b),
                        _ => null
                    };

                object a, b;
            }
            """
        );

    /// <summary>
    ///     ⚠ The arrow's break is the fitter's to take, so the hold cannot be read off the source:
    ///     <c> =&gt;</c> ending at 120 holds, at 121 the arrow moves down and the level is spent, and
    ///     <c> =&gt; (</c> at 121 breaks after the arrow instead and holds again.
    /// </summary>
    [Fact]
    public void AnArrowTheMarginMovesDown_SpendsTheLevel_AndOneThatStaysHoldsIt() =>
        Oracle.Agrees(
            $$"""
            class T {
                object S(int k) =>
                    k switch {
                        {{Held}} =>
                            (
                                a).C(),
                        {{Moved}} =>
                            (
                                a).C(),
                        {{Glued}} => (
                                a).C(),
                        _ => null
                    };

                object a;
            }
            """,
            $$"""
            class T {
                object S(int k) =>
                    k switch {
                        {{Held}} =>
                        (
                            a).C(),
                        {{Moved}}
                            =>
                            (
                                a).C(),
                        {{Glued}} =>
                        (
                            a).C(),
                        _ => null
                    };

                object a;
            }
            """
        );

    /// <summary>
    ///     Under <c>wrap_if_long</c> a chain the fill breaks before its last link gives the level up, as
    ///     the author's break does — under an <c>=</c>, a lambda, a <c>return</c> and an arm. Equal to the
    ///     oracle's output for this input.
    /// </summary>
    [Fact]
    public void UnderAFill_AChainTheFillBreaks_GivesTheLevelUpInOnePass() =>
        Assert.Equal(
            $$"""
            using System;

            class T {
                object A() {
                    var x =
                        (
                            a).{{MidFirst}}
                        .{{MidLast}};
                    Func<object> f = () =>
                        (
                            a).{{MidFirst}}
                        .{{MidLast}};
                    return
                        (
                            a).{{MidFirst}}
                        .{{MidLast}};
                }

                object B(int k) =>
                    k switch {
                        1 =>
                            (
                                a).{{MidFirst}}
                            .{{MidLast}},
                        _ => null
                    };

                object a;
            }
            """,
            UnderAFill(
                $$"""
                using System;

                class T {
                    object A() {
                        var x =
                            (
                                a).{{Mid}};
                        Func<object> f = () =>
                            (
                                a).{{Mid}};
                        return
                            (
                                a).{{Mid}};
                    }

                    object B(int k) =>
                        k switch {
                            1 =>
                                (
                                    a).{{Mid}},
                            _ => null
                        };

                    object a;
                }
                """
            )
        );

    /// <summary>
    ///     The other half, under the same four owners: a chain that fits stays whole and the level is
    ///     held. Equal to the oracle's output for this input.
    /// </summary>
    [Fact]
    public void UnderAFill_AChainThatStaysWhole_HoldsTheLevel() =>
        Assert.Equal(
            $$"""
            using System;

            class T {
                object A() {
                    var x =
                    (
                        a).{{Whole}};
                    Func<object> f = () =>
                    (
                        a).{{Whole}};
                    return
                    (
                        a).{{Whole}};
                }

                object B(int k) =>
                    k switch {
                        1 =>
                        (
                            a).{{Whole}},
                        _ => null
                    };

                object a;
            }
            """,
            UnderAFill(
                $$"""
                using System;

                class T {
                    object A() {
                        var x =
                            (
                                a).{{Whole}};
                        Func<object> f = () =>
                            (
                                a).{{Whole}};
                        return
                            (
                                a).{{Whole}};
                    }

                    object B(int k) =>
                        k switch {
                            1 =>
                                (
                                    a).{{Whole}},
                            _ => null
                        };

                    object a;
                }
                """
            )
        );

    /// <summary>
    ///     ⚠ The issue's input. Pass one used to hold the level and break before <c>.OtherMethodName</c>,
    ///     and pass two to read that break back and give the level up. Now pass one writes pass two's
    ///     form, which the oracle returns unchanged. It is not the oracle's answer to the input — that
    ///     keeps <c>.OtherMethodName(</c> on the line and chops its arguments, which is SK-DIV-0129's
    ///     last-link rule and not this one.
    /// </summary>
    [Fact]
    public void TheIssuesCase_SettlesInOnePass() =>
        Assert.Equal(
            $$"""
            class T {
                object M() =>
                    (
                        a).SomeMethodName({{Args}})
                    .OtherMethodName({{Args}});

                void N() {
                    var x =
                        (
                            a).SomeMethodName({{Args}})
                        .OtherMethodName({{Args}});
                }

                object a;
            }
            """,
            UnderAFill(
                $$"""
                class T {
                    object M() =>
                        (
                            a).SomeMethodName({{Args}}).OtherMethodName({{Args}});

                    void N() {
                        var x =
                            (
                                a).SomeMethodName({{Args}}).OtherMethodName({{Args}});
                    }

                    object a;
                }
                """
            )
        );
}
