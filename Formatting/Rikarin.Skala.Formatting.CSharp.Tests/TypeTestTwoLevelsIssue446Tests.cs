using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Core.Configuration;

namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     Issue #446 item 1, SK-DIV-0210 round 9: a returned type test whose operand overflows by itself and whose dot's line
///     one level in does not fit either — round 4's <c>x</c> layout. The dot goes two levels in, the type test breaks
///     after its keyword below it, and the keyword moves down too when it alone overflows the dot's line. Every expected
///     string is jb cleanupcode 2025.2.6's own output for the input, from the 2026-10-14 probes.
/// </summary>
public sealed class TypeTestTwoLevelsIssue446Tests {
    /// <summary>The oracle's answer under the repository's export, and a second pass that changes nothing.</summary>
    static void Agrees(string source, string expected) {
        var options = new PhaseOneOptions(
            OptionResolver.Resolve(Path.Combine(Rikarin.Skala.Testing.Corpus.RepositoryRoot, "Test.cs"), []).Options
        );

        var once = CSharpFormatter.Format("Test.cs", SourceText.From(source), options).Formatted;
        Assert.True(
            expected.TrimEnd('\n') == once.TrimEnd('\n'),
            $"Skala's output is not the oracle's.\n--- Skala ---\n{once}\n--- oracle ---\n{expected}"
        );

        var twice = CSharpFormatter.Format("Test.cs", SourceText.From(once), options).Formatted;
        Assert.True(once == twice, $"took two passes to settle:\n{once}\n--- pass two ---\n{twice}");
    }

    /// <summary>
    ///     A receiver of 7 keeps the dot's line one level in at 120 columns; a receiver of 6 makes it 121, and the dot
    ///     goes two levels in. Then the dot's line two levels in ends at 117 (the keyword stays), 118 and 121 (the keyword
    ///     and the type take a line each) and 122 (they share one). And the same under <c>yield return</c>.
    /// </summary>
    [Theory]
    [InlineData("as_i8_r7_t4_124")]
    [InlineData("as_i8_r6_t4_124")]
    [InlineData("as_i8_r6_t4_125")]
    [InlineData("as_i8_r6_t4_126")]
    [InlineData("as_i8_r6_t4_129")]
    [InlineData("as_i8_r6_t4_130")]
    [InlineData("is_i36_r1_t17_126_y")]
    public void PutsTheDotTwoLevelsIn(string cell) {
        var (source, expected) = Cells[cell];
        Agrees(source, expected);
    }

    /// <summary>
    ///     The same layouts written by the author, which the oracle keeps with the dot two levels in: a break after the
    ///     keyword, and under <c>yield return</c> a break before it, which stacks the type test's level as it does after
    ///     <c>return</c> (#597).
    /// </summary>
    [Theory]
    [InlineData("x3_short")]
    [InlineData("y4_short")]
    [InlineData("y_call_is")]
    public void KeepsTheAuthorsLayout(string cell) {
        var (source, expected) = Kept[cell];
        Agrees(source, expected);
    }

    static readonly Dictionary<string, (string Source, string Expected)> Cells = new() {
        ["as_i8_r7_t4_124"] = (
            """
            class C
            {
                object M()
                {
                    return rrrrrrr.PPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPP as TTTT;
                }
            }
            """,
            """
            class C {
                object M() {
                    return rrrrrrr
                        .PPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPP as TTTT;
                }
            }
            """
        ),
        ["as_i8_r6_t4_124"] = (
            """
            class C
            {
                object M()
                {
                    return rrrrrr.PPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPP as TTTT;
                }
            }
            """,
            """
            class C {
                object M() {
                    return rrrrrr
                            .PPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPP as
                        TTTT;
                }
            }
            """
        ),
        ["as_i8_r6_t4_125"] = (
            """
            class C
            {
                object M()
                {
                    return rrrrrr.PPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPP as TTTT;
                }
            }
            """,
            """
            class C {
                object M() {
                    return rrrrrr
                            .PPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPP as
                        TTTT;
                }
            }
            """
        ),
        ["as_i8_r6_t4_126"] = (
            """
            class C
            {
                object M()
                {
                    return rrrrrr.PPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPP as TTTT;
                }
            }
            """,
            """
            class C {
                object M() {
                    return rrrrrr
                            .PPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPP
                        as
                        TTTT;
                }
            }
            """
        ),
        ["as_i8_r6_t4_129"] = (
            """
            class C
            {
                object M()
                {
                    return rrrrrr.PPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPP as TTTT;
                }
            }
            """,
            """
            class C {
                object M() {
                    return rrrrrr
                            .PPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPP
                        as
                        TTTT;
                }
            }
            """
        ),
        ["as_i8_r6_t4_130"] = (
            """
            class C
            {
                object M()
                {
                    return rrrrrr.PPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPP as TTTT;
                }
            }
            """,
            """
            class C {
                object M() {
                    return rrrrrr
                            .PPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPP
                        as TTTT;
                }
            }
            """
        ),
        ["is_i36_r1_t17_126_y"] = (
            """
            class C
            {
                System.Collections.Generic.IEnumerable<object> M()
                {
                    {
                        {
                            {
                                {
                                    {
                                        {
                                            {
                                                yield return r.PPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPP is TTTTTTTTTTTTTTTTT;
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            """,
            """
            class C {
                System.Collections.Generic.IEnumerable<object> M() {
                    {
                        {
                            {
                                {
                                    {
                                        {
                                            {
                                                yield return r
                                                        .PPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPP is
                                                    TTTTTTTTTTTTTTTTT;
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            """
        )
    };

    static readonly Dictionary<string, (string Source, string Expected)> Kept = new() {
        ["x3_short"] = (
            """
            class C
            {
                object M()
                {
                    return rrr
                    .PPPP is
                    TT;
                }
            }
            """,
            """
            class C {
                object M() {
                    return rrr
                            .PPPP is
                        TT;
                }
            }
            """
        ),
        ["y4_short"] = (
            """
            class C
            {
                System.Collections.Generic.IEnumerable<object> M()
                {
                    yield return rrr
                    .PPPP
                    is
                    TT;
                }
            }
            """,
            """
            class C {
                System.Collections.Generic.IEnumerable<object> M() {
                    yield return rrr
                            .PPPP
                        is
                        TT;
                }
            }
            """
        ),
        ["y_call_is"] = (
            """
            class C
            {
                System.Collections.Generic.IEnumerable<object> M()
                {
                    yield return x.M(
                    aaaa,
                    bbbb
                    )
                    is T;
                }
            }
            """,
            """
            class C {
                System.Collections.Generic.IEnumerable<object> M() {
                    yield return x.M(
                            aaaa,
                            bbbb
                        )
                        is T;
                }
            }
            """
        )
    };
}
