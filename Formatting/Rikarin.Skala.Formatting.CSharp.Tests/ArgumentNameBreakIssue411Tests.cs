namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     Issue #411, SK-DIV-0177: a named argument's <c>name:</c> is a break point, and the value lands on
///     the argument's own column. Skala had none there, so <c>name176: Cast&lt;…&gt;(</c> past the margin
///     filled the type argument list and a string too wide for its line stayed beside its name. Every
///     expected string is <c>jb cleanupcode</c>'s own output for the input, and
///     <see cref="Oracle.Agrees" /> asserts the second pass too.
/// </summary>
public sealed class ArgumentNameBreakIssue411Tests {
    /// <summary>
    ///     A string or an identifier too wide for the argument's line moves below its name, on the
    ///     argument's own column and without a continuation level — at 121 columns, not at 120, with a
    ///     trailing comma counted, at a nested block's depth, and when it does not fit below either.
    /// </summary>
    [Fact]
    public void AValueThatCannotBreak_MovesBelowItsName() =>
        Oracle.Agrees(
            """
            class T {
                void A() {
                    Outer(first: 1, name176: "ssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssss");
                }

                void B() {
                    Outer(first: 1, name176: "sssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssss");
                }

                void C() {
                    Outer(first: 1, name176: "sssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssss", last: 2);
                }

                void D() {
                    Outer(first: 1, name176: aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa);
                }

                void E() {
                    {
                        Outer(name176: "ssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssss");
                    }
                }
            }
            """,
            """
            class T {
                void A() {
                    Outer(
                        first: 1,
                        name176:
                        "ssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssss"
                    );
                }

                void B() {
                    Outer(
                        first: 1,
                        name176: "sssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssss"
                    );
                }

                void C() {
                    Outer(
                        first: 1,
                        name176:
                        "sssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssss",
                        last: 2
                    );
                }

                void D() {
                    Outer(
                        first: 1,
                        name176:
                        aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa
                    );
                }

                void E() {
                    {
                        Outer(
                            name176:
                            "ssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssss"
                        );
                    }
                }
            }
            """
        );

    /// <summary>
    ///     The ordering rule's second question alone, the arrow's (#378): a binary operand, a call's
    ///     own name past the margin move the value down; <c>Compute(</c>, <c>source.Select(</c>,
    ///     <c>new Widget(</c> and <c>x =&gt; Compute(</c> stay beside the name although the whole value
    ///     would have fitted below.
    /// </summary>
    [Fact]
    public void TheColonBreaks_OnlyWhenTheLineToTheValuesFirstPointHasNoRoom() =>
        Oracle.Agrees(
            """
            class T {
                void A() {
                    Outer(first: 1, name176: aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa + beta);
                }

                void B() {
                    Outer(first: 1, name176: aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa + beta);
                }

                void C() {
                    Outer(first: 1, name176: Compute(aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa, beta));
                }

                void D() {
                    Outer(first: 1, name176: source.Select(aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa).Where(beta));
                }

                void E() {
                    Outer(first: 1, name176: new Widget(aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa, beta));
                }

                void F() {
                    Outer(first: 1, name176: x => Compute(aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa, beta));
                }

                void G() {
                    Outer(first: 1, name176: Computeaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa(alpha, beta));
                }
            }
            """,
            """
            class T {
                void A() {
                    Outer(
                        first: 1,
                        name176:
                        aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa + beta
                    );
                }

                void B() {
                    Outer(
                        first: 1,
                        name176: aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa
                        + beta
                    );
                }

                void C() {
                    Outer(
                        first: 1,
                        name176: Compute(
                            aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa,
                            beta
                        )
                    );
                }

                void D() {
                    Outer(
                        first: 1,
                        name176: source.Select(aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa)
                            .Where(beta)
                    );
                }

                void E() {
                    Outer(
                        first: 1,
                        name176: new Widget(
                            aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa,
                            beta
                        )
                    );
                }

                void F() {
                    Outer(
                        first: 1,
                        name176: x => Compute(
                            aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa,
                            beta
                        )
                    );
                }

                void G() {
                    Outer(
                        first: 1,
                        name176:
                        Computeaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa(
                            alpha,
                            beta
                        )
                    );
                }
            }
            """
        );

    /// <summary>
    ///     An attribute's <c>name:</c>, an object creation's argument, and the issue's own shape: a
    ///     generic call whose <c>(</c> runs past the margin and whose arguments chop anyway breaks after
    ///     the colon rather than filling its type argument list (SK-DIV-0177 for where it does not).
    /// </summary>
    [Fact]
    public void AnAttributesAnObjectCreationsAndAGenericCallsArgument_BreakTheSameWay() =>
        Oracle.Agrees(
            """
            class T {
                [Attr(first: 1, name176: "ssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssss")]
                void A() { }

                void B() {
                    var created = new Widget(first: 1, name176: aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa);
                }

                void C() {
                    Outer(first: 1, name176: Cast<Dictionary<string, Guid?>, AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA>(xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx, yyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyy));
                }
            }
            """,
            """
            class T {
                [Attr(
                    first: 1,
                    name176:
                    "ssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssss"
                )]
                void A() { }

                void B() {
                    var created = new Widget(
                        first: 1,
                        name176:
                        aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa
                    );
                }

                void C() {
                    Outer(
                        first: 1,
                        name176:
                        Cast<Dictionary<string, Guid?>, AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA>(
                            xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx,
                            yyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyy
                        )
                    );
                }
            }
            """
        );

    /// <summary>
    ///     An author's break after the colon is kept even when the line fits joined, and a chopped list
    ///     the author wrote without one gets one when the argument overflows.
    /// </summary>
    [Fact]
    public void TheAuthorsBreakAfterTheColon_IsKept() =>
        Oracle.Agrees(
            """
            class T {
                void A() {
                    Outer(
                        first: 1,
                        name176:
                        Compute(alpha, beta)
                    );
                }

                void B() {
                    Outer(
                        first: 1,
                        name176: "ssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssss"
                    );
                }
            }
            """,
            """
            class T {
                void A() {
                    Outer(
                        first: 1,
                        name176:
                        Compute(alpha, beta)
                    );
                }

                void B() {
                    Outer(
                        first: 1,
                        name176:
                        "ssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssss"
                    );
                }
            }
            """
        );
}
