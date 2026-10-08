namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     Issue #435, SK-DIV-0200: after a block comment that spans lines, the break point of an <c>=</c>,
///     a compound assignment, a lambda's or a switch arm's arrow and a named argument's colon is not
///     planned, so <c>int y = /* gap</c> / <c>gap2 */ 1;</c> keeps <c>1</c> after the comment. #409 planned
///     it past the comment, and the comment's unbounded width broke it. Every expected string is
///     <c>jb cleanupcode</c>'s own output for the input, and <see cref="Oracle.Agrees" /> asserts the
///     second pass too. Long identifiers are spelled through constants so no line here is over-wide.
/// </summary>
public sealed class MultiLineCommentPointIssue435Tests {
    const string A16 = "aaaaaaaaaaaaaaaa";
    const string A31 = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    const string A34 = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    const string A39 = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    const string A48 = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    const string A50 = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    const string A51 = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    const string A56 = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    const string B43 = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
    const string B45 = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
    const string B50 = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
    const string B57 = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
    const string B68 = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb" + "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
    const string B69 = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb" + "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
    const string B76 = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb" + "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
    const string C21 = "ccccccccccccccccccccc";
    const string C32 = "cccccccccccccccccccccccccccccccc";
    const string D54 = "dddddddddddddddddddddddddddddddddddddddddddddddddddddd";
    const string E15 = "eeeeeeeeeeeeeee";
    const string Y31 = "yyyyyyyyyyyyyyyyyyyyyyyyyyyyyyy";

    /// <summary>
    ///     The issue's case and its family on one-line values: after <c>=</c> in a local, a field, a
    ///     property initializer, an assignment and a parameter default, after a lambda's arrow and an
    ///     expression body's, after <c>return</c>, the value stays on the comment's last line. The
    ///     chopped list (<c>M2(1, /* gap</c> / <c>gap2 */</c> / <c>2</c>) and the operator break
    ///     (<c>a /* gap</c> / <c>gap2 */</c> / <c>+ b</c>) still break after the comment, and a fill
    ///     keeps <c>2</c> after it; a break the author wrote before or after the comment is kept.
    /// </summary>
    [Fact]
    public void AfterAnEquals_TheValueStaysAfterTheComment() =>
        Oracle.Agrees(
            """
            class C {
                void M() {
                    int   y = /* gap
                                 gap2 */ 1;
                    int y2 = /* gap */ 1;
                    int y3 = /** gap */ 1;
                    int y4 = /* gap
                      gap2 */ a + b;
                    int y5 = a + /* gap
                      gap2 */ b;
                    int y6 = a /* gap
                      gap2 */ + b;
                    Func<int> f = () => /* gap
                      gap2 */ 1;
                    M2(1, /* gap
                      gap2 */ 2);
                    M2(1 /* gap
                      gap2 */, 2);
                    M2(1, 2 /* gap
                      gap2 */);
                    var z = new int[] { 1, /* c
                      d */ 2 };
                    y = /* gap
                      gap2 */ 1;
                    x = 1 /* gap
                      gap2 */;
                    int y7 = /* gap
                      gap2 */
                      1;
                    int y8 =
                      /* gap
                      gap2 */ 1;
                }
                int R() {
                    return /* gap
                      gap2 */ 1;
                }
                int P => /* gap
                  gap2 */ 1;
                int Q = /* gap
                  gap2 */ 1;
                void M3(int a = /* gap
                  gap2 */ 1) { }
                int T(bool c) => c ? /* gap
                  gap2 */ 1 : 2;
                int U(int[] a) => a[/* gap
                  gap2 */ 0];
            }
            """,
            """
            class C {
                void M() {
                    int y = /* gap
                                 gap2 */ 1;
                    int y2 = /* gap */ 1;
                    int y3 = /** gap */ 1;
                    int y4 = /* gap
                      gap2 */ a + b;
                    int y5 = a
                        + /* gap
                          gap2 */ b;
                    int y6 = a /* gap
                      gap2 */
                        + b;
                    Func<int> f = () => /* gap
                      gap2 */ 1;
                    M2(
                        1, /* gap
                          gap2 */
                        2
                    );
                    M2(
                        1 /* gap
                          gap2 */,
                        2
                    );
                    M2(
                        1,
                        2 /* gap
                          gap2 */
                    );
                    var z = new int[] {
                        1, /* c
                          d */ 2
                    };
                    y = /* gap
                      gap2 */ 1;
                    x = 1 /* gap
                      gap2 */;
                    int y7 = /* gap
                      gap2 */
                        1;
                    int y8 =
                        /* gap
                        gap2 */ 1;
                }

                int R() {
                    return /* gap
                      gap2 */ 1;
                }

                int P => /* gap
                  gap2 */ 1;

                int Q = /* gap
                  gap2 */ 1;

                void M3(
                    int a = /* gap
                      gap2 */ 1
                ) { }

                int T(bool c) =>
                    c
                        ? /* gap
                          gap2 */ 1
                        : 2;

                int U(int[] a) =>
                    a[ /* gap
                      gap2 */ 0];
            }
            """
        );

    /// <summary>
    ///     Values that cannot fit: the oracle wraps inside the value — a binary operator, a chopped
    ///     call nested from the statement as if the comment were one line — and never after the comment.
    ///     Also a switch arm's arrow, a named argument's colon, <c>throw</c>, <c>yield return</c>, an
    ///     attribute's named argument, a second declarator and a <c>/** */</c> comment.
    /// </summary>
    [Fact]
    public void AValueTooLongForTheLine_WrapsInsideItself() =>
        Oracle.Agrees(
            $$"""
            class C {
                void M() {
                    int {{Y31}}2 = /* gap
                      gap2 */ {{A39}} + {{B76}};
                    int {{Y31}}3 = /* gap
                      gap2 */ Compute({{A39}}, {{B76}});
                    Func<int> f = () => /* gap
                      gap2 */ Compute({{A39}}, {{B76}});
                    int y9 = M2(1, /* a
                      b */ 2);
                    int y10 = /* a */ M2(1, /* a
                      b */ 2);
                    var s = x switch {
                        1 => /* a
                          b */ 2,
                        _ => 3
                    };
                    M2(a: /* a
                      b */ 1);
                    throw /* a
                      b */ new Exception();
                }
                IEnumerable<int> Y() {
                    yield return /* a
                      b */ 1;
                }
                int R() {
                    return /* gap
                      gap2 */ Compute({{A39}}, {{B76}});
                }
                int P { get; } = /* a
                  b */ 1;
                [A(X = /* a
                  b */ 1)]
                int Q = /* a
                  b */ 1, Q2 = /* c
                  d */ 2;
                int Z = /** a
                  b */ 1;
            }
            """,
            $$"""
            class C {
                void M() {
                    int {{Y31}}2 = /* gap
                      gap2 */ {{A39}}
                        + {{B76}};
                    int {{Y31}}3 = /* gap
                      gap2 */ Compute(
                        {{A39}},
                        {{B76}}
                    );
                    Func<int> f = () => /* gap
                      gap2 */ Compute(
                        {{A39}},
                        {{B76}}
                    );
                    int y9 = M2(
                        1, /* a
                          b */
                        2
                    );
                    int y10 = /* a */ M2(
                        1, /* a
                          b */
                        2
                    );
                    var s = x switch {
                        1 => /* a
                          b */ 2,
                        _ => 3
                    };
                    M2(
                        a: /* a
                          b */ 1
                    );
                    throw /* a
                      b */ new Exception();
                }

                IEnumerable<int> Y() {
                    yield return /* a
                      b */ 1;
                }

                int R() {
                    return /* gap
                      gap2 */ Compute(
                        {{A39}},
                        {{B76}}
                    );
                }

                int P { get; } = /* a
                  b */ 1;

                [A(
                    X = /* a
                      b */ 1
                )]
                int Q = /* a
                  b */ 1,
                    Q2 = /* c
                    d */ 2;

                int Z = /** a
                  b */ 1;
            }
            """
        );

    /// <summary>
    ///     A one-line comment still lets the <c>=</c> break after it when the value does not fit
    ///     (SK-DIV-0165); a comment that spans lines does not, and a chopped list breaks after either.
    /// </summary>
    [Fact]
    public void OnlyACommentThatSpansLines_KeepsTheValue() =>
        Oracle.Agrees(
            $$"""
            class C {
                void M() {
                    int {{Y31}} = /* gap */ {{A39}} + {{B43}};
                    int {{Y31}}2 = /* gap
                      gap2 */ {{A39}} + {{B43}};
                    int {{Y31}}3 = {{A39}} + /* gap
                      gap2 */ {{B43}} + {{C32}};
                    M2(1, /* gap
                      gap2 */ 2, {{A51}}, {{B50}});
                }
            }
            """,
            $$"""
            class C {
                void M() {
                    int {{Y31}} = /* gap */
                        {{A39}} + {{B43}};
                    int {{Y31}}2 = /* gap
                      gap2 */ {{A39}} + {{B43}};
                    int {{Y31}}3 = {{A39}}
                        + /* gap
                          gap2 */ {{B43}}
                        + {{C32}};
                    M2(
                        1, /* gap
                          gap2 */
                        2,
                        {{A51}},
                        {{B50}}
                    );
                }
            }
            """
        );

    /// <summary>
    ///     Compound assignments, an anonymous object's and a <c>with</c>'s member, a <c>for</c> header, a
    ///     <c>using</c> declaration, a lambda's arrow, a run of two comments either way round, and the lists
    ///     that do break after the comment: a collection initializer, an element access, a chopped call.
    /// </summary>
    [Fact]
    public void EveryEqualsAndArrow_KeepsTheValueAfterTheComment() =>
        Oracle.Agrees(
            """
            class C {
                void M() {
                    x += /* a
                      b */ 1;
                    x ??= /* a
                      b */ 1;
                    var w = new List<int> { /* a
                      b */ 1 };
                    var v = a[1, /* a
                      b */ 2];
                    var u = c ? /* a
                      b */ 1 : /* c
                      d */ 2;
                    var q = a /* a
                      b */ .B();
                    var r = a ?? /* a
                      b */ b;
                    var o = new { X = /* a
                      b */ 1 };
                    var p = o with { X = /* a
                      b */ 1 };
                    for (int i = /* a
                      b */ 0; i < 1; i++) { }
                    using var d = /* a
                      b */ Open();
                    int y7 = /* gap
                      gap2 */
                      1;
                    int y8 =
                      /* gap
                      gap2 */ 1;
                    int y11 = /* a */ /* b
                      c */ 1;
                    int y12 = /* a
                      b */ /* c */ 1;
                    Func<int, int> g = x => /* a
                      b */ x;
                    M2(1, /* a
                      b */ 2, 3);
                    var lst = new List<int> { 1, /* a
                      b */ 2 };
                }
            }
            """,
            """
            class C {
                void M() {
                    x += /* a
                      b */ 1;
                    x ??= /* a
                      b */ 1;
                    var w = new List<int> { /* a
                      b */
                        1
                    };
                    var v = a[1, /* a
                      b */
                        2];
                    var u = c
                        ? /* a
                          b */ 1
                        : /* c
                        d */ 2;
                    var q = a /* a
                      b */.B();
                    var r = a
                        ?? /* a
                          b */ b;
                    var o = new {
                        X = /* a
                          b */ 1
                    };
                    var p = o with {
                        X = /* a
                          b */ 1
                    };
                    for (int i = /* a
                      b */ 0;
                         i < 1;
                         i++) { }

                    using var d = /* a
                      b */ Open();
                    int y7 = /* gap
                      gap2 */
                        1;
                    int y8 =
                        /* gap
                        gap2 */ 1;
                    int y11 = /* a */ /* b
                      c */ 1;
                    int y12 = /* a
                      b */ /* c */ 1;
                    Func<int, int> g = x => /* a
                      b */ x;
                    M2(
                        1, /* a
                          b */
                        2,
                        3
                    );
                    var lst = new List<int> {
                        1, /* a
                          b */
                        2
                    };
                }
            }
            """
        );

    /// <summary>
    ///     What follows the comment nests from the statement's line, as if the comment were one line: a
    ///     switch's arms, a chain broken at its dots, a chopped call's arguments and its <c>)</c>.
    /// </summary>
    [Fact]
    public void WhatFollowsTheComment_NestsFromTheStatementsLine() =>
        Oracle.Agrees(
            $$"""
            class C {
                void M() {
                    if (a) /* x
                      y */ {
                        Foo();
                    }
                    Run(/* a
                      b */ () => {
                        Foo();
                    });
                    Run(1, /* a
                      b */ () => {
                        Foo();
                    });
                    var q = /* a
                      b */ new List<int> {
                        1, 2
                    };
                    var s = /* a
                      b */ x switch {
                        1 => 2,
                        _ => 3
                    };
                    var t = /* a
                      b */ Compute({{A31}}).Then({{B45}}).Then({{C21}});
                    Foo({{A16}}, /* a
                      b */ Bar({{C32}}, {{D54}}, {{E15}}));
                    int u = 1 /* a
                      b */ + Compute({{A48}}, {{B57}});
                    /* a
                       b */ Foo({{A50}}, {{B69}});
                    var v = new {
                        A = /* a
                          b */ Compute({{A56}}, {{B57}})
                    };
                }
                /* a
                   b */ void N(int {{A34}}, int {{B68}}) { }
            }
            """,
            $$"""
            class C {
                void M() {
                    if (a) /* x
                      y */ {
                        Foo();
                    }

                    Run( /* a
                      b */ () => { Foo(); }
                    );
                    Run(
                        1, /* a
                          b */
                        () => { Foo(); }
                    );
                    var q = /* a
                      b */ new List<int> { 1, 2 };
                    var s = /* a
                      b */ x switch {
                        1 => 2,
                        _ => 3
                    };
                    var t = /* a
                      b */ Compute({{A31}})
                        .Then({{B45}})
                        .Then({{C21}});
                    Foo(
                        {{A16}}, /* a
                          b */
                        Bar(
                            {{C32}},
                            {{D54}},
                            {{E15}}
                        )
                    );
                    int u = 1 /* a
                      b */
                        + Compute(
                            {{A48}},
                            {{B57}}
                        );
                    /* a
                       b */
                    Foo(
                        {{A50}},
                        {{B69}}
                    );
                    var v = new {
                        A = /* a
                          b */ Compute(
                            {{A56}},
                            {{B57}}
                        )
                    };
                }

                /* a
                   b */
                void N(
                    int {{A34}},
                    int {{B68}}
                ) { }
            }
            """
        );
}
