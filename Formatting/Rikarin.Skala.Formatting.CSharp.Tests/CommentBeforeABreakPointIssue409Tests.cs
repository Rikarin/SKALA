namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     Issue #409, SK-DIV-0165: a break point whose gap holds a block comment breaks <em>after</em> the
///     comment. The builder used to leave every gap with a comment in it unplanned, so the group lost
///     the point: <c>nameof(value), /* f */ name176: …</c> past the margin chopped <c>nameof(</c> on
///     pass one, because the breaks inside the items were the only ones left on the line, and joined it
///     again on pass two. Every expected string is <c>jb cleanupcode</c>'s own output for the input,
///     and <see cref="Oracle.Agrees" /> asserts the second pass too.
/// </summary>
public sealed class CommentBeforeABreakPointIssue409Tests {
    /// <summary>
    ///     The fuzzer's case, minimised, and the oracle's answer for it whole. Until #411 only the
    ///     settling could be asserted: the oracle also breaks after <c>name176:</c> and puts
    ///     <c>Cast&lt;…&gt;(</c> on a line of its own, and a named argument's colon was not a break point.
    /// </summary>
    [Fact]
    public void TheFuzzersCase_SettlesInOnePass_WithTheBreakAfterTheComment() =>
        Oracle.Agrees(
            """
            class C {
                void M() {
                    if (new D(name175: nameof(value), /* f */  name176: Cast<ValueTask<Dictionary<string, Guid?>>, (ImmutableArray<decimal> First, Nullable<Guid?> Second)>($"n={items[0]} and {source?.Value}", (from item in Source where "sss" orderby item.Length descending select false)))) {
                    }
                }
            }
            """,
            """
            class C {
                void M() {
                    if (new D(
                            name175: nameof(value), /* f */
                            name176:
                            Cast<ValueTask<Dictionary<string, Guid?>>, (ImmutableArray<decimal> First, Nullable<Guid?> Second)>(
                                $"n={items[0]} and {source?.Value}",
                                (from item in Source where "sss" orderby item.Length descending select false)
                            )
                        )) { }
                }
            }
            """
        );

    /// <summary>
    ///     An argument after a comma and a block comment, the comment written beside the comma or on a
    ///     line of its own: the break goes after the comment, and one comment or two is the same.
    /// </summary>
    [Fact]
    public void AfterAComma_TheArgumentListBreaksPastTheComment() =>
        Oracle.Agrees(
            """
            class T {
                void A() {
                    var a = new D(name175: nameof(value), /* f */  name176: Cast<ValueTask<Dictionary<string, Guid?>>, int>($"n={items[0]} and {source?.Value}", 12345));
                }

                void B() {
                    var a = new D(name175: nameof(value), /* f */ /* g */ name176: Cast<ValueTask<Dictionary<string, Guid?>>, int>($"n={items[0]} and {source?.Value}", 1));
                }

                void C() {
                    var a = new D(name175: nameof(value), /* f */
                        name176: Cast<ValueTask<Dictionary<string, Guid?>>, int>($"n={items[0]} and {source?.Value}", 12345));
                }

                void D() {
                    var a = new D(name175: nameof(value),
                        /* f */ name176: Cast<ValueTask<Dictionary<string, Guid?>>, int>($"n={items[0]} and {source?.Value}", 12345));
                }

                void E() {
                    M(alpha,
                        /* f */ beta);
                }

                void F() {
                    M(alpha, /* f */
                        beta);
                }

                void G() {
                    Compute(alphaArgumentValue, /* f */ betaArgumentValue, gammaArgumentValue, /* g */ deltaArgumentValue, epsilonArgumentValue);
                }
            }
            """,
            """
            class T {
                void A() {
                    var a = new D(
                        name175: nameof(value), /* f */
                        name176: Cast<ValueTask<Dictionary<string, Guid?>>, int>($"n={items[0]} and {source?.Value}", 12345)
                    );
                }

                void B() {
                    var a = new D(
                        name175: nameof(value), /* f */ /* g */
                        name176: Cast<ValueTask<Dictionary<string, Guid?>>, int>($"n={items[0]} and {source?.Value}", 1)
                    );
                }

                void C() {
                    var a = new D(
                        name175: nameof(value), /* f */
                        name176: Cast<ValueTask<Dictionary<string, Guid?>>, int>($"n={items[0]} and {source?.Value}", 12345)
                    );
                }

                void D() {
                    var a = new D(
                        name175: nameof(value),
                        /* f */
                        name176: Cast<ValueTask<Dictionary<string, Guid?>>, int>($"n={items[0]} and {source?.Value}", 12345)
                    );
                }

                void E() {
                    M(
                        alpha,
                        /* f */
                        beta
                    );
                }

                void F() {
                    M(
                        alpha, /* f */
                        beta
                    );
                }

                void G() {
                    Compute(
                        alphaArgumentValue, /* f */
                        betaArgumentValue,
                        gammaArgumentValue, /* g */
                        deltaArgumentValue,
                        epsilonArgumentValue
                    );
                }
            }
            """
        );

    /// <summary>
    ///     Every other list with a point after its comma: base types, parameters, type arguments,
    ///     attribute arguments, enum members, switch arms and declarators.
    /// </summary>
    [Fact]
    public void EveryListTheOracleReLays_BreaksPastTheComment() =>
        Oracle.Agrees(
            """
            class T : IAlphaInterfaceNameValue, /* f */ IBetaInterfaceNameValue, IGammaInterfaceNameValue, IDeltaInterfaceNameValue1 {
                void A(int alphaParameterValue, /* f */ string betaParameterValue, long gammaParameterValue, double deltaParameterValu) {
                }

                void B() {
                    Dictionary<VeryLongTypeNameNumberOne, /* f */ VeryLongTypeNameNumberTwo<VeryLongTypeNameNumberThree, int>> field = null;
                }

                [Attr("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", /* f */ Name = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb")]
                void C() {
                }

                enum E { A, /* f */ B, C }

                void F() {
                    var s = x switch { 1 => "a", /* f */ 2 => "b", _ => "c" };
                }

                void G() {
                    int alphaArgumentValue = 1, /* f */ betaArgumentValue = 2, gammaArgumentValue = 3, deltaArgumentValue = 444444444;
                }
            }
            """,
            """
            class T : IAlphaInterfaceNameValue, /* f */
                IBetaInterfaceNameValue,
                IGammaInterfaceNameValue,
                IDeltaInterfaceNameValue1 {
                void A(
                    int alphaParameterValue, /* f */
                    string betaParameterValue,
                    long gammaParameterValue,
                    double deltaParameterValu
                ) { }

                void B() {
                    Dictionary<VeryLongTypeNameNumberOne, /* f */
                        VeryLongTypeNameNumberTwo<VeryLongTypeNameNumberThree, int>> field = null;
                }

                [Attr(
                    "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", /* f */
                    Name = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb"
                )]
                void C() { }

                enum E {
                    A, /* f */
                    B,
                    C
                }

                void F() {
                    var s = x switch {
                        1 => "a", /* f */
                        2 => "b",
                        _ => "c"
                    };
                }

                void G() {
                    int alphaArgumentValue = 1, /* f */
                        betaArgumentValue = 2,
                        gammaArgumentValue = 3,
                        deltaArgumentValue = 444444444;
                }
            }
            """
        );

    /// <summary>
    ///     The point before <c>)</c>, <c>]</c>, a binary operator, a <c>.</c> and a <c>?</c>, after
    ///     <c>{</c> and <c>[</c>, and after <c>=</c> and a lambda's arrow where the value cannot fit.
    /// </summary>
    [Fact]
    public void BeforeACloserOrAnOperator_AndAfterAnOpener_TheBreakIsPastTheComment() =>
        Oracle.Agrees(
            """
            class T {
                void A() {
                    Compute(alphaArgumentValue, betaArgumentValue, gammaArgumentValue, deltaArgumentValue, epsilonArgumentValue /* f */);
                }

                void B() {
                    var x = alphaArgumentValue + betaArgumentValue /* f */ + gammaArgumentValue + deltaArgumentValue + epsilonArgumentValue;
                }

                void C() {
                    var x = source.Where(x => x.Alpha).Select(y => y.Beta) /* f */ .OrderBy(z => z.Gamma).ToList().ToArray().Reverse();
                }

                void D() {
                    var x = alphaArgumentValueLongName /* f */ ? betaArgumentValueLongName : gammaArgumentValueLongName + deltaArgumentVal;
                }

                void E() {
                    var xs = new[] { /* f */ "aaaaaaaaaaaaaaaaaaaa", "bbbbbbbbbbbbbbbbbbbbbbbb", "cccccccccccccccccccccc", "dddddddddddddddd" };
                }

                void F() {
                    int[] xs = [/* f */ alphaArgumentValue, betaArgumentValue, gammaArgumentValue, deltaArgumentValue, epsilonArgumentVa];
                }

                void G() {
                    int[] xs = [alphaArgumentValue, betaArgumentValue, gammaArgumentValue, deltaArgumentValue, epsilonArgum /* f */];
                }

                void H() {
                    var alphaArgumentValueLongName = /* f */ "ssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssss";
                }

                void I() {
                    Func<int, string> f = x => /* f */ "ssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssss";
                }
            }
            """,
            """
            class T {
                void A() {
                    Compute(
                        alphaArgumentValue,
                        betaArgumentValue,
                        gammaArgumentValue,
                        deltaArgumentValue,
                        epsilonArgumentValue /* f */
                    );
                }

                void B() {
                    var x = alphaArgumentValue
                        + betaArgumentValue /* f */
                        + gammaArgumentValue
                        + deltaArgumentValue
                        + epsilonArgumentValue;
                }

                void C() {
                    var x = source.Where(x => x.Alpha)
                        .Select(y => y.Beta) /* f */
                        .OrderBy(z => z.Gamma)
                        .ToList()
                        .ToArray()
                        .Reverse();
                }

                void D() {
                    var x = alphaArgumentValueLongName /* f */
                        ? betaArgumentValueLongName
                        : gammaArgumentValueLongName + deltaArgumentVal;
                }

                void E() {
                    var xs = new[] { /* f */
                        "aaaaaaaaaaaaaaaaaaaa", "bbbbbbbbbbbbbbbbbbbbbbbb", "cccccccccccccccccccccc", "dddddddddddddddd"
                    };
                }

                void F() {
                    int[] xs = [ /* f */
                        alphaArgumentValue, betaArgumentValue, gammaArgumentValue, deltaArgumentValue, epsilonArgumentVa
                    ];
                }

                void G() {
                    int[] xs = [
                        alphaArgumentValue, betaArgumentValue, gammaArgumentValue, deltaArgumentValue, epsilonArgum /* f */
                    ];
                }

                void H() {
                    var alphaArgumentValueLongName = /* f */
                        "ssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssss";
                }

                void I() {
                    Func<int, string> f = x => /* f */
                        "ssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssss";
                }
            }
            """
        );

    /// <summary>
    ///     The two tokens whose wrap the oracle does not carry past a comment — <c>(</c> and an expression
    ///     body's <c>=&gt;</c> — and the lists that fit or fill, which no comment changes.
    /// </summary>
    [Fact]
    public void AfterAParenthesisOrAnExpressionBodysArrow_TheWrapStopsAtTheComment() =>
        Oracle.Agrees(
            """
            class T {
                void A() {
                    var a = new D(/* f */ name175: nameof(value), name176: Cast<ValueTask<Dictionary<string, Guid?>>, int>($"n={items[0]} and {source?.Value}", 12345));
                }

                void B() {
                    Compute(/* f */ alphaArgumentValue, betaArgumentValue, gammaArgumentValue, deltaArgumentValue, epsilonArgumentVa);
                }

                int C => /* f */ Compute(alphaArgumentValue, betaArgumentValue, gammaArgumentValue, deltaArgumentValue, epsilonArgum);

                void D() {
                    var a = new D(name175: nameof(value), /* f */ name176: 1);
                    Compute(alphaArgumentValue, /* f */ betaArgumentValue);
                }

                void E() {
                    var t = (alphaArgumentValue, /* f */ betaArgumentValue, gammaArgumentValue, deltaArgumentValue, epsilonArgumentValu1);
                    var xs = new List<string> { "aaaaaaaaaaaaaaaaaaaa", /* f */ "bbbbbbbbbbbbbbbbbbbbbbbb", "cccccccccccccccccccccc", "ddddddddddd" };
                    var k = new K { Alpha = alphaArgumentValue, /* f */ Beta = betaArgumentValue, Gamma = gammaArgumentValue, Delta = 1 };
                }
            }
            """,
            """
            class T {
                void A() {
                    var a = new D( /* f */ name175: nameof(value),
                        name176: Cast<ValueTask<Dictionary<string, Guid?>>, int>($"n={items[0]} and {source?.Value}", 12345)
                    );
                }

                void B() {
                    Compute( /* f */ alphaArgumentValue,
                        betaArgumentValue,
                        gammaArgumentValue,
                        deltaArgumentValue,
                        epsilonArgumentVa
                    );
                }

                int C => /* f */ Compute(
                    alphaArgumentValue,
                    betaArgumentValue,
                    gammaArgumentValue,
                    deltaArgumentValue,
                    epsilonArgum
                );

                void D() {
                    var a = new D(name175: nameof(value), /* f */ name176: 1);
                    Compute(alphaArgumentValue, /* f */ betaArgumentValue);
                }

                void E() {
                    var t = (alphaArgumentValue, /* f */ betaArgumentValue, gammaArgumentValue, deltaArgumentValue,
                        epsilonArgumentValu1);
                    var xs = new List<string> {
                        "aaaaaaaaaaaaaaaaaaaa", /* f */ "bbbbbbbbbbbbbbbbbbbbbbbb", "cccccccccccccccccccccc", "ddddddddddd"
                    };
                    var k = new K {
                        Alpha = alphaArgumentValue, /* f */ Beta = betaArgumentValue, Gamma = gammaArgumentValue, Delta = 1
                    };
                }
            }
            """
        );

    /// <summary>
    ///     The author's break on either side of the comment. A break after it is kept, or taken by the
    ///     chop. A break before it is the gap in front of the comment's to keep, and a fill that keeps
    ///     breaks does not pin its point after the comment as well: <c>/* f */ beta)</c> stays whole.
    /// </summary>
    [Fact]
    public void AnAuthorsBreakAroundTheComment_IsKeptWhereItWasWritten() =>
        Oracle.Agrees(
            """
            class T<TAlpha,
                /* f */ TBeta> {
                void A() {
                    var t = (alpha, /* f */
                        beta);
                    G<int, /* f */
                        string> g = null;
                    var xs = new[] { 1, /* f */
                        2 };
                    int[] ys = [1, /* f */
                        2];
                    var z = a /* f */
                        + b;
                    var w = a /* f */
                        .B();
                    M(a, /* f */
                        b);
                }

                void B() {
                    var t = (alpha,
                        /* f */ beta);
                    G<int,
                        /* f */ string> g = null;
                    var (x,
                        /* f */ y) = t;
                    var u = (alpha, /* f */
                        /* g */ beta);
                    var xs = new[] { 1,
                        /* f */ 2 };
                    var z = a
                        /* f */ + b;
                    M(a,
                        /* f */ b);
                    if (o is [1,
                        /* f */ 2]) { }
                }

                int C => (alpha, /* f */
                    beta).Item1;
            }
            """,
            """
            class T<TAlpha,
                /* f */ TBeta> {
                void A() {
                    var t = (alpha, /* f */
                        beta);
                    G<int, /* f */
                        string> g = null;
                    var xs = new[] { 1, /* f */ 2 };
                    int[] ys = [
                        1, /* f */
                        2
                    ];
                    var z = a /* f */
                        + b;
                    var w = a /* f */
                        .B();
                    M(
                        a, /* f */
                        b
                    );
                }

                void B() {
                    var t = (alpha,
                        /* f */ beta);
                    G<int,
                        /* f */ string> g = null;
                    var (x,
                        /* f */ y) = t;
                    var u = (alpha, /* f */
                        /* g */ beta);
                    var xs = new[] {
                        1,
                        /* f */ 2
                    };
                    var z = a
                        /* f */
                        + b;
                    M(
                        a,
                        /* f */
                        b
                    );
                    if (o is [
                            1,
                            /* f */ 2
                        ]) { }
                }

                int C =>
                    (alpha, /* f */
                        beta).Item1;
            }
            """
        );
}
