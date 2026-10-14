namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     #617: two non-idempotencies the fuzzer and #598's fresh probe found. Every expected string is
///     <c>jb cleanupcode</c>'s own output, measured 2026-10-14 with <c>Testing ask</c>.
/// </summary>
public sealed class ParenthesisedReceiverIssue617Tests {
    /// <summary>
    ///     A dot broken after a parenthesised operator puts the operator a level past the dot: behind <c>x =</c>, a cast and a
    ///     <c>return</c>, for a property and a call, and as an argument one level past the argument's line. Before the
    ///     fix pass one wrote the operator on the dot's column and pass two nested it; M7 broke only inside.
    /// </summary>
    [Fact]
    public void AParenthesisedReceiversOperator_NestsPastTheDotItBrokeBefore() =>
        Oracle.Agrees(
            """
            class C {
                void M1() {
                    if (c) {
                        x = (T)(aaaaaaa && Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb).Length;
                    }
                }

                void M2() {
                    x = (aaaaaaa && Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb).Length;
                }

                void M3() {
                    x = (aaaaaaa && Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb).Length;
                }

                void M6() {
                    Use((aaaaaaa && Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb).Length);
                }

                void M7() {
                    x = (aaaaaaa && Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb).Trim();
                }
            }
            """,
            """
            class C {
                void M1() {
                    if (c) {
                        x = (T)(aaaaaaa
                                && Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb)
                            .Length;
                    }
                }

                void M2() {
                    x = (aaaaaaa
                            && Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb)
                        .Length;
                }

                void M3() {
                    x = (aaaaaaa && Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb)
                        .Length;
                }

                void M6() {
                    Use(
                        (aaaaaaa
                            && Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb)
                        .Length
                    );
                }

                void M7() {
                    x = (aaaaaaa
                            && Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb)
                        .Trim();
                }
            }
            """
        );

    /// <summary>
    ///     A lone argument the author put below the call's <c>(</c> keeps the chop and the <c>=</c> (fuzz
    ///     17150111611241313074). Before the fix M5 and M7 re-joined the argument and broke the <c>=</c>.
    /// </summary>
    [Fact]
    public void AOneArgumentCallTheAuthorChopped_KeepsItsEquals() =>
        Oracle.Agrees(
            """
            class T2 {
                void M4() {
                    var (a34_wwwwwwwwwwwwwww, b35) = Convert<decimal, Lazy<StringBuilder>>(
                        (aaaaaaaaaaaaaaaaaaaaaaa + bbbbbbbbbbbbbbbbbbbbbb)
                    );
                }

                void M5() {
                    var a34_wwwwwwwwwwwwwwwwwwwwwwwww = Convert<decimal, Lazy<StringBuilder>>(
                        (aaaaaaaaaaaaaaaaaaaaaaa + bbbbbbbbbbbbbbbbbbbbbb)
                    );
                }

                void M7() {
                    var a34_wwwwwwwwwwwwwwwwwwwwwwwww = Convert<decimal, Lazy<StringBuilder>>(
                        aaaaaaaaaaaaaaaaaaaaaaabbbbbbbbbbbbbbbbbbbbbbbbbbb
                    );
                }

                void M8() {
                    var a34_wwwwwwwwwwwwwwwwwwwwwwwww = Convert<decimal, Lazy<StringBuilder>>(
                        aaaaaaaaaaaa
                    );
                }
            }
            """,
            """
            class T2 {
                void M4() {
                    var (a34_wwwwwwwwwwwwwww, b35) = Convert<decimal, Lazy<StringBuilder>>(
                        (aaaaaaaaaaaaaaaaaaaaaaa + bbbbbbbbbbbbbbbbbbbbbb)
                    );
                }

                void M5() {
                    var a34_wwwwwwwwwwwwwwwwwwwwwwwww = Convert<decimal, Lazy<StringBuilder>>(
                        (aaaaaaaaaaaaaaaaaaaaaaa + bbbbbbbbbbbbbbbbbbbbbb)
                    );
                }

                void M7() {
                    var a34_wwwwwwwwwwwwwwwwwwwwwwwww = Convert<decimal, Lazy<StringBuilder>>(
                        aaaaaaaaaaaaaaaaaaaaaaabbbbbbbbbbbbbbbbbbbbbbbbbbb
                    );
                }

                void M8() {
                    var a34_wwwwwwwwwwwwwwwwwwwwwwwww = Convert<decimal, Lazy<StringBuilder>>(aaaaaaaaaaaa);
                }
            }
            """
        );
}
