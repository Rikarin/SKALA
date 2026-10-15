namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     Issue #632 (SK-DIV-0600): a cast's <c>)</c> the author kept on a line of its own is a continuation line of
///     the statement whether or not a break follows it. Every expected string is <c>jb cleanupcode</c>'s own
///     output for the input, measured 2026-10-15 with <c>Testing ask</c>.
/// </summary>
public sealed class CastCloserIssue632Tests {
    /// <summary>
    ///     Under <c>return</c>, <c>throw</c> and <c>yield return</c>, where the statement's frame pays for the level.
    ///     Skala used to put the <c>)</c> a level in only when its operand shared its line.
    /// </summary>
    [Fact]
    public void ACastsKeptCloser_IsTheStatementsContinuationLine() =>
        Oracle.Agrees(
            """
            class C {
                object M1() {
                    return (DateTime?
            )x;
                }
                object M2() {
                    return (DateTime?
            )
            x;
                }
                object M3() {
                    throw (Exception
            )
            x;
                }
                object M4() {
                    return (int
            )
            (long
            )
            x;
                }
                object M5() {
                    return (DateTime?
            )x.Y
            .Z();
                }
                object M6() {
                    yield return (DateTime?
            )
            x;
                }
                object M7() {
                    return (DateTime?)
                        x;
                }
            }
            """,
            """
            class C {
                object M1() {
                    return (DateTime?
                        )x;
                }

                object M2() {
                    return (DateTime?
                        )
                        x;
                }

                object M3() {
                    throw (Exception
                        )
                        x;
                }

                object M4() {
                    return (int
                        )
                        (long
                        )
                        x;
                }

                object M5() {
                    return (DateTime?
                        )x.Y
                            .Z();
                }

                object M6() {
                    yield return (DateTime?
                        )
                        x;
                }

                object M7() {
                    return (DateTime?)
                        x;
                }
            }
            """
        );

    /// <summary>
    ///     The fuzz seed's own shape: pass one breaks after the <c>)</c>, before the multi-line raw literal, and the
    ///     <c>)</c> must sit where pass two will read it. ⚠ Not an oracle comparison: the oracle keeps <c>)"""</c>
    ///     on one line, and that break is a separate, settled divergence; only the second pass is asserted.
    /// </summary>
    [Fact]
    public void ACastsCloserBeforeARawLiteral_SettlesInOnePass() {
        const string source = """"
                              class C {
                                  object M() {
                                      return (DateTime?
                              )"""
                                          line of text
                                          """;
                                  }
                              }
                              """";
        var once = Format.Text(source);
        Assert.Contains("return (DateTime?\n            )\n", once.Replace("\r\n", "\n"));
        Assert.Equal(once, Format.Text(once));
    }
}
