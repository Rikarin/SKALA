namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     #598, SK-DIV-0770: a bare <c>(a op b…)</c> or <c>(T)(a op b…)</c>, single or doubled, as the whole value of an
///     <c>=</c> behind a head of twelve columns or more. Every expected string is <c>jb cleanupcode</c>'s own output,
///     measured 2026-11-13 with <c>Testing ask</c> on dense grids; the start commit differed from it on every method
///     here but one in the last test, which shows the name's width deciding between two otherwise equal values.
/// </summary>
public sealed class BareParenNameIssue598Tests {
    [Fact]
    public void ABareParenthesisBehindAWideHead_KeepsItsEqualsOnceTheLineBelowWouldRunLong() =>
        Oracle.Agrees(
            """
            class C {
                void M1() {
                    string value = (aaaaaa + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb);
                }

                void M2() {
                    this.something.name = (aaaaaaaa + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb);
                }

                void M3() {
                    var valueeeeeeee = (aaaaaa + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb);
                }
            }
            """,
            """
            class C {
                void M1() {
                    string value = (aaaaaa
                        + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb);
                }

                void M2() {
                    this.something.name = (aaaaaaaa
                        + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb);
                }

                void M3() {
                    var valueeeeeeee = (aaaaaa
                        + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb);
                }
            }
            """
        );

    [Fact]
    public void ACastBareParenthesisBehindAWideHead_KeepsItsEqualsOnceTheLineBelowWouldRunLong() =>
        Oracle.Agrees(
            """
            class C {
                void M1() {
                    string value = (T)(aa + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb);
                }

                void M2() {
                    this.something.name = (T)(aaaaaa + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb);
                }

                void M3() {
                    var valueeeeeeee = (T)(aaaa + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb);
                }
            }
            """,
            """
            class C {
                void M1() {
                    string value = (T)(aa
                        + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb);
                }

                void M2() {
                    this.something.name = (T)(aaaaaa
                        + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb);
                }

                void M3() {
                    var valueeeeeeee = (T)(aaaa
                        + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb);
                }
            }
            """
        );

    [Fact]
    public void AFirstOperandShortAgainstTheName_BreaksTheEqualsThoughTheValueOverflowsBelowToo() =>
        Oracle.Agrees(
            """
            class C {
                void M1() {
                    var vvvvvvvvvvvvvv = (aa + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb);
                }

                void M2() {
                    string vvvvvvvvvvvv = (aa + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb);
                }

                void M3() {
                    this.vvvvvvvvvvvv = (aa + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb);
                }
            }
            """,
            """
            class C {
                void M1() {
                    var vvvvvvvvvvvvvv =
                        (aa
                            + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb);
                }

                void M2() {
                    string vvvvvvvvvvvv =
                        (aa
                            + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb);
                }

                void M3() {
                    this.vvvvvvvvvvvv =
                        (aa
                            + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb);
                }
            }
            """
        );

    [Fact]
    public void ADoubledBareParenthesis_TakesTheSameFormTwoColumnsTighter() =>
        Oracle.Agrees(
            """
            class C {
                void M1() {
                    var vvvvvvvvvvvvvv = ((aa + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb));
                }

                void M2() {
                    string vvvvvvvvvvvv = ((aa + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb));
                }
            }
            """,
            """
            class C {
                void M1() {
                    var vvvvvvvvvvvvvv =
                        ((aa + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb));
                }

                void M2() {
                    string vvvvvvvvvvvv =
                        ((aa
                            + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb));
                }
            }
            """
        );

    [Fact]
    public void TheSameOperandBehindTwoNames_KeepsTheEqualsOnlyBehindTheShortName() =>
        Oracle.Agrees(
            """
            class C {
                void M1() {
                    var vvvvvvvvvvvv = (aaaa + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb);
                }

                void M2() {
                    List<int> vvvvv = (aaaa + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb);
                }
            }
            """,
            """
            class C {
                void M1() {
                    var vvvvvvvvvvvv =
                        (aaaa
                            + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb);
                }

                void M2() {
                    List<int> vvvvv = (aaaa
                        + Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb);
                }
            }
            """
        );
}
