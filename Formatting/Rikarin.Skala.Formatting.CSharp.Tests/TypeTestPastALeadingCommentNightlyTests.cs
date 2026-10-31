namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     SK-DIV-0602 (Nightly 2026-10-24, <c>fuzz --replay=4378889735950361705
///     --origin=constructs/breaks/multi-line-comment-neighbours.cs</c>): a type test that is an argument list's first
///     argument, kept on the <c>(</c>'s line behind a block comment, breaks before its keyword at the list's level. The
///     expected strings of the kept-break rows are <c>jb cleanupcode</c>'s own output for the input, measured 2026-10-31
///     with <c>Testing ask</c>.
/// </summary>
/// <remarks>
///     ⚠ The list's level is already spent on the <c>(</c>'s line there (the builder's unconditional scope, #521), so
///     #597's stacked level put a kept <c>as</c> two levels past the statement, and #445's level from the line put the
///     <c>as</c> pass one broke before one level past it. Pass two read pass one's break as the author's and moved it.
/// </remarks>
public sealed class TypeTestPastALeadingCommentNightlyTests {
    /// <summary>
    ///     The minimised seed. ⚠ Pass one is not the oracle's answer for this input — the oracle breaks after the
    ///     comment and keeps <c>… as string</c> whole below it (open, SK-DIV-0602) — but it is the oracle's answer
    ///     for pass one's own output, which it leaves as it is.
    /// </summary>
    [Fact]
    public void TheMinimisedCase_IsIdempotent() {
        const string source = """
                              class C {
                                  void M() {
                                      var d1 = Compute( /* f */ someVeryLongReceiverNameXYZWVUTSRQPONMLKJIHGFEDCBA_SomeVeryLongPropertyName_AnotherVer as string);
                                      var d4 = Compute( /* f */ someVeryLongReceiverNameXYZWVUTSRQPONMLKJIHGFEDCBA_SomeVeryLongPropertyName_AnotherVer is string);
                                      var d5 = Compute(/* f */ someVeryLongReceiverNameXYZWVUTSRQPONMLKJIHGFEDCBA_SomeVeryLongPropertyName_AnotherVer as string);
                                  }
                              }
                              """;
        const string expected = """
                                class C {
                                    void M() {
                                        var d1 = Compute( /* f */ someVeryLongReceiverNameXYZWVUTSRQPONMLKJIHGFEDCBA_SomeVeryLongPropertyName_AnotherVer
                                            as string
                                        );
                                        var d4 = Compute( /* f */ someVeryLongReceiverNameXYZWVUTSRQPONMLKJIHGFEDCBA_SomeVeryLongPropertyName_AnotherVer
                                            is string
                                        );
                                        var d5 = Compute( /* f */ someVeryLongReceiverNameXYZWVUTSRQPONMLKJIHGFEDCBA_SomeVeryLongPropertyName_AnotherVer
                                            as string
                                        );
                                    }
                                }
                                """;

        var once = Format.Text(source);
        Assert.Equal(expected, once.TrimEnd('\n'));
        Assert.Equal(once, Format.Text(once));
    }

    /// <summary>
    ///     A break the author wrote before the keyword, behind the comment, at the list's level — written there or two
    ///     levels in. Without the comment the argument starts its own line and the keyword stacks a level past it
    ///     (#597); a binary operator was never stacked; a test that fits joins.
    /// </summary>
    [Fact]
    public void AKeptBreakBeforeTheKeyword_TakesTheListsLevel() =>
        Oracle.Agrees(
            """
            class C {
                void M() {
                    var e1 = Compute( /* f */ someVeryLongReceiverNameXYZWVUTSRQPONMLKJIHGFEDCBA_SomeVeryLongPropertyName_AnotherVer
                        as string
                    );
                    var e2 = Compute(someVeryLongReceiverNameXYZWVUTSRQPONMLKJIHGFEDCBA_SomeVeryLongPropertyName_AnotherVer
                        as string
                    );
                    var e3 = Compute( /* f */ someVeryLongReceiverNameXYZWVUTSRQPONMLKJIHGFEDCBA_SomeVeryLongPropertyName_AnotherVer
                        + strin
                    );
                    var e5 = Compute( /* f */ someVeryLongReceiverNameXYZWVUTSRQPONMLKJIHGFEDCBA_SomeVeryLongPropertyName_AnotherVer
                        as string);
                    var e6 = Compute( /* f */ shortName
                        as string
                    );
                    var e7 = Compute( /* f */ shortName as string
                    );
                    var f1 = Compute( /* f */ someVeryLongReceiverNameXYZWVUTSRQPONMLKJIHGFEDCBA_SomeVeryLongPropertyName_AnotherVer
                            as string
                    );
                }
            }
            """,
            """
            class C {
                void M() {
                    var e1 = Compute( /* f */ someVeryLongReceiverNameXYZWVUTSRQPONMLKJIHGFEDCBA_SomeVeryLongPropertyName_AnotherVer
                        as string
                    );
                    var e2 = Compute(
                        someVeryLongReceiverNameXYZWVUTSRQPONMLKJIHGFEDCBA_SomeVeryLongPropertyName_AnotherVer
                            as string
                    );
                    var e3 = Compute( /* f */ someVeryLongReceiverNameXYZWVUTSRQPONMLKJIHGFEDCBA_SomeVeryLongPropertyName_AnotherVer
                        + strin
                    );
                    var e5 = Compute( /* f */ someVeryLongReceiverNameXYZWVUTSRQPONMLKJIHGFEDCBA_SomeVeryLongPropertyName_AnotherVer
                        as string
                    );
                    var e6 = Compute( /* f */ shortName
                        as string
                    );
                    var e7 = Compute( /* f */ shortName as string);
                    var f1 = Compute( /* f */ someVeryLongReceiverNameXYZWVUTSRQPONMLKJIHGFEDCBA_SomeVeryLongPropertyName_AnotherVer
                        as string
                    );
                }
            }
            """
        );

    /// <summary>
    ///     The neighbours: a test that is a logical operand still stacks (#597); a call operand keeps its arguments two
    ///     levels in with <c>)</c> and <c>as</c> at one; <c>is not</c>, a declaration pattern, an object creation and a
    ///     <c>/** */</c> comment alike; and where the author broke after the comment the argument starts its own line
    ///     and the keyword stacks past it.
    /// </summary>
    [Fact]
    public void TheNeighbours_AgreeWithTheOracle() =>
        Oracle.Agrees(
            """
            class C {
                void M() {
                    var h1 = Compute( /* f */ someVeryLongReceiverNameXYZWVUTSRQPONMLKJIHGFEDCBA_SomeVeryLongPropertyName_AnotherVer
                        is string || other);
                    var h2 = Compute( /* f */ first && someVeryLongReceiverNameXYZWVUTSRQPONMLKJIHGFEDCBA_SomeVeryLongPropertyName_Ano
                        is string);
                    var h3 = Compute( /* f */ someVeryLongReceiverNameXYZWVUTSRQPONMLKJIHGFEDCBA_SomeVeryLongPropertyName_AnotherVer
                        is not string);
                    var h4 = Compute( /* f */ x.Call(alpha,
                            beta)
                        as string);
                    var h5 = Compute( /* f */
                        someVeryLongReceiverNameXYZWVUTSRQPONMLKJIHGFEDCBA_SomeVeryLongPropertyName_AnotherVer
                        as string);
                    var h6 = Compute(
                        /* f */ someVeryLongReceiverNameXYZWVUTSRQPONMLKJIHGFEDCBA_SomeVeryLongPropertyName_AnotherVer
                        as string);
                    var h7 = new Thing( /* f */ someVeryLongReceiverNameXYZWVUTSRQPONMLKJIHGFEDCBA_SomeVeryLongPropertyName_Anot
                        as string);
                    var h8 = Compute( /** f */ someVeryLongReceiverNameXYZWVUTSRQPONMLKJIHGFEDCBA_SomeVeryLongPropertyName_Anoth
                        as string);
                    var h9 = Compute( /* f */ someVeryLongReceiverNameXYZWVUTSRQPONMLKJIHGFEDCBA_SomeVeryLongPropertyName_AnotherVer
                        is string s);
                }
            }
            """,
            """
            class C {
                void M() {
                    var h1 = Compute( /* f */ someVeryLongReceiverNameXYZWVUTSRQPONMLKJIHGFEDCBA_SomeVeryLongPropertyName_AnotherVer
                            is string
                        || other
                    );
                    var h2 = Compute( /* f */ first
                        && someVeryLongReceiverNameXYZWVUTSRQPONMLKJIHGFEDCBA_SomeVeryLongPropertyName_Ano
                            is string
                    );
                    var h3 = Compute( /* f */ someVeryLongReceiverNameXYZWVUTSRQPONMLKJIHGFEDCBA_SomeVeryLongPropertyName_AnotherVer
                        is not string
                    );
                    var h4 = Compute( /* f */ x.Call(
                            alpha,
                            beta
                        )
                        as string
                    );
                    var h5 = Compute( /* f */
                        someVeryLongReceiverNameXYZWVUTSRQPONMLKJIHGFEDCBA_SomeVeryLongPropertyName_AnotherVer
                            as string
                    );
                    var h6 = Compute(
                        /* f */ someVeryLongReceiverNameXYZWVUTSRQPONMLKJIHGFEDCBA_SomeVeryLongPropertyName_AnotherVer
                            as string
                    );
                    var h7 = new Thing( /* f */ someVeryLongReceiverNameXYZWVUTSRQPONMLKJIHGFEDCBA_SomeVeryLongPropertyName_Anot
                        as string
                    );
                    var h8 = Compute( /** f */ someVeryLongReceiverNameXYZWVUTSRQPONMLKJIHGFEDCBA_SomeVeryLongPropertyName_Anoth
                        as string
                    );
                    var h9 = Compute( /* f */ someVeryLongReceiverNameXYZWVUTSRQPONMLKJIHGFEDCBA_SomeVeryLongPropertyName_AnotherVer
                        is string s
                    );
                }
            }
            """
        );
}
