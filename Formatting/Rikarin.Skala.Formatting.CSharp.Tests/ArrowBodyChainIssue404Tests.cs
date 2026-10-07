namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     Issue #404, SK-DIV-0156: a body heading with a parenthesis the author broke after holds the
///     arrow's or the <c>=</c>'s level (SK-DIV-0101) only while the chain after the <c>)</c> stays
///     whole. A chain with a group of its own breaks at its points whenever its head spans lines, and
///     the oracle then puts the <c>(</c> on the continuation — so the answer is the chain's, not the
///     source's break before a dot, which pass one does not have and pass two does. Every expected
///     string is <c>jb cleanupcode</c>'s own output for the input, and <see cref="Oracle.Agrees" />
///     asserts the second pass too.
/// </summary>
public sealed class ArrowBodyChainIssue404Tests {
    /// <summary>
    ///     Seed 11718305405350914591's minimised input: pass one used to hold the level and break the
    ///     chain, pass two to give the level up.
    /// </summary>
    [Fact]
    public void TheFuzzersCase_LandsOnTheOraclesFixedPointInOnePass() =>
        Oracle.Agrees(
            """
            public class ChainAfterParenthesisedHead {
              object AnElementAccess() =>
               (
              a)[0] .C();
            }
            """,
            """
            public class ChainAfterParenthesisedHead {
                object AnElementAccess() =>
                    (
                        a)[0]
                    .C();
            }
            """
        );

    /// <summary>
    ///     Two calls or more after the <c>)</c> — <c>[0]</c> counts as one — is a chain the fitter
    ///     chops, so the <c>(</c> is not held.
    /// </summary>
    [Fact]
    public void AChainWithAGroup_KeepsTheParenthesisOnTheArrowsContinuation() =>
        Oracle.Agrees(
            """
            class T {
                object A() =>
                    (
                        a)[0].C();

                object B =>
                    (
                        a).B().C();

                object C() =>
                    (
                        a)[0]?.C();

                object D() =>
                    (
                        a ?? b)!.B().C();

                object E() =>
                    (
                        a, b).B().C().D();

                object a, b;
            }
            """,
            """
            class T {
                object A() =>
                    (
                        a)[0]
                    .C();

                object B =>
                    (
                        a).B()
                    .C();

                object C() =>
                    (
                        a)[0]
                    ?.C();

                object D() =>
                    (
                        a ?? b)!.B()
                    .C();

                object E() =>
                    (
                        a, b).B()
                    .C()
                    .D();

                object a, b;
            }
            """
        );

    /// <summary>
    ///     The same rule where a frame pays rather than a group: the lambda's arrow and the
    ///     <c>return</c>.
    /// </summary>
    [Fact]
    public void UnderAnEquals_ALambdaAndAReturn_TheSameChainKeepsTheContinuation() =>
        Oracle.Agrees(
            """
            class T {
                void A() {
                    var x =
                        (
                            a)[0].C();
                    System.Func<object> f = () =>
                        (
                            a).B().C();
                }

                object B() {
                    return
                        (
                            a)[0].C();
                }

                object a;
            }
            """,
            """
            class T {
                void A() {
                    var x =
                        (
                            a)[0]
                        .C();
                    System.Func<object> f = () =>
                        (
                            a).B()
                        .C();
                }

                object B() {
                    return
                        (
                            a)[0]
                        .C();
                }

                object a;
            }
            """
        );

    /// <summary>
    ///     The chain found further down the spine — a ternary's condition — and the shapes that never
    ///     held.
    /// </summary>
    [Fact]
    public void AChainHeadingATernaryOrABinaryOrAnArm_KeepsTheContinuation() =>
        Oracle.Agrees(
            """
            class T {
                object A() =>
                    (
                        a)[0].C() ? a : b;

                object B() =>
                    (
                        a).B().C() + b;

                object C(int k) =>
                    k switch {
                        1 =>
                        (
                            a)[0].C(),
                        _ => null
                    };

                object a, b;
            }
            """,
            """
            class T {
                object A() =>
                    (
                        a)[0]
                    .C()
                        ? a
                        : b;

                object B() =>
                    (
                        a).B()
                    .C()
                    + b;

                object C(int k) =>
                    k switch {
                        1 =>
                            (
                                a)[0]
                            .C(),
                        _ => null
                    };

                object a, b;
            }
            """
        );

    /// <summary>
    ///     The boundary: one call, an indexer alone, a property run and a <c>?.</c> call plan no chain
    ///     group, so nothing breaks and the <c>(</c> stays at the member's indent.
    /// </summary>
    [Fact]
    public void AChainWithNoGroup_StillHoldsTheLevel() =>
        Oracle.Agrees(
            """
            class T {
                object A() =>
                    (
                        a).C();

                object B() =>
                    (
                        a)[0];

                object C() =>
                    (
                        a).B.C();

                object D() =>
                    (
                        a)?.B();

                object E() =>
                    (
                        a)[0].C;

                object a;
            }
            """,
            """
            class T {
                object A() =>
                (
                    a).C();

                object B() =>
                (
                    a)[0];

                object C() =>
                (
                    a).B.C();

                object D() =>
                (
                    a)?.B();

                object E() =>
                (
                    a)[0].C;

                object a;
            }
            """
        );

    /// <summary>
    ///     Under <c>wrap_if_long</c> the chain is a fill and its head breaks nothing, so the source's
    ///     answer stands: whole chains hold the level, the author's break before a dot does not. Asked
    ///     of the oracle with <c>resharper_csharp_wrap_chained_method_calls = wrap_if_long</c>.
    /// </summary>
    [Fact]
    public void UnderAFill_AWholeChainStillHoldsTheLevel() {
        var settled = Overridden.Settled(
            """
            class T {
                object A() =>
                    (
                        a)[0].C();

                object B() =>
                    (
                        a).B().C().D();

                object C() =>
                    (
                        a)[0]
                    .C();

                object a;
            }
            """,
            [("skala_wrap_chained_method_calls", "wrap_if_long")]
        );

        Assert.Equal(
            """
            class T {
                object A() =>
                (
                    a)[0].C();

                object B() =>
                (
                    a).B().C().D();

                object C() =>
                    (
                        a)[0]
                    .C();

                object a;
            }
            """,
            settled.TrimEnd('\n')
        );
    }
}
