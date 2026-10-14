using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Core.Configuration;

namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     Issue #446 item 1, SK-DIV-0210: a returned type test whose operand fits, where the keyword's band could hold the
///     line and the oracle still breaks before the operand's dot. Every expected string is jb cleanupcode 2025.2.6's own
///     output for the input, from the 2026-10-10 sweeps.
/// </summary>
public sealed class TypeTestTieIssue446Tests {
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
    ///     A type of at most twelve columns, the line through the keyword at 118 or more and that line past the dot's
    ///     line by more than the floor: the dot takes the break, where the keyword's band would have held it.
    /// </summary>
    [Theory]
    [InlineData("ast8_d10_i24_118")]
    [InlineData("ast3_d8_i24_118")]
    [InlineData("ast3_d8_i24_122")]
    [InlineData("t7_d9_i16_118")]
    public void BreaksBeforeTheDotInATie(string cell) {
        var (source, expected) = cell switch {
            "ast8_d10_i24_118" => (
                """
                class C
                {
                    object M()
                    {
                        {
                            {
                                {
                                    {
                                        return zzzzzzzzzzzzzzzzz.QQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQ as List<Ab>;
                                    }
                                }
                            }
                        }
                    }
                }
                """,
                """
                class C {
                    object M() {
                        {
                            {
                                {
                                    {
                                        return zzzzzzzzzzzzzzzzz
                                            .QQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQ as List<Ab>;
                                    }
                                }
                            }
                        }
                    }
                }
                """
            ),
            "ast3_d8_i24_118" => (
                """
                class C
                {
                    object M()
                    {
                        {
                            {
                                {
                                    {
                                        return zzzzzzzzzz.QQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQ as Wxy;
                                    }
                                }
                            }
                        }
                    }
                }
                """,
                """
                class C {
                    object M() {
                        {
                            {
                                {
                                    {
                                        return zzzzzzzzzz
                                            .QQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQ as Wxy;
                                    }
                                }
                            }
                        }
                    }
                }
                """
            ),
            "ast3_d8_i24_122" => (
                """
                class C
                {
                    object M()
                    {
                        {
                            {
                                {
                                    {
                                        return zzzzzzzzzz.QQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQ as Wxy;
                                    }
                                }
                            }
                        }
                    }
                }
                """,
                """
                class C {
                    object M() {
                        {
                            {
                                {
                                    {
                                        return zzzzzzzzzz
                                            .QQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQ as Wxy;
                                    }
                                }
                            }
                        }
                    }
                }
                """
            ),
            "t7_d9_i16_118" => (
                """
                class C
                {
                    object M()
                    {
                        {
                            {
                                return rrrrrrrrrrrrrrr.PPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPP as TTTTTTT;
                            }
                        }
                    }
                }
                """,
                """
                class C {
                    object M() {
                        {
                            {
                                return rrrrrrrrrrrrrrr
                                    .PPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPP as TTTTTTT;
                            }
                        }
                    }
                }
                """
            ),
            _ => throw new ArgumentOutOfRangeException(nameof(cell))
        };

        Agrees(source, expected);
    }

    /// <summary>
    ///     A column short of 118, or a difference within the floor, and the keyword's band answers as before.
    /// </summary>
    [Theory]
    [InlineData("ast8_d10_i24_117")]
    [InlineData("ast11_d10_i24_120")]
    [InlineData("t7_d9_i16_117")]
    public void KeepsTheKeywordBandOtherwise(string cell) {
        var (source, expected) = cell switch {
            "ast8_d10_i24_117" => (
                """
                class C
                {
                    object M()
                    {
                        {
                            {
                                {
                                    {
                                        return zzzzzzzzzzzzzzzzz.QQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQ as List<Ab>;
                                    }
                                }
                            }
                        }
                    }
                }
                """,
                """
                class C {
                    object M() {
                        {
                            {
                                {
                                    {
                                        return zzzzzzzzzzzzzzzzz.QQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQ as
                                            List<Ab>;
                                    }
                                }
                            }
                        }
                    }
                }
                """
            ),
            "ast11_d10_i24_120" => (
                """
                class C
                {
                    object M()
                    {
                        {
                            {
                                {
                                    {
                                        return zzzzzzzzzzzzzzzzzzzz.QQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQ as IList<Wxyz>;
                                    }
                                }
                            }
                        }
                    }
                }
                """,
                """
                class C {
                    object M() {
                        {
                            {
                                {
                                    {
                                        return zzzzzzzzzzzzzzzzzzzz.QQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQ as
                                            IList<Wxyz>;
                                    }
                                }
                            }
                        }
                    }
                }
                """
            ),
            "t7_d9_i16_117" => (
                """
                class C
                {
                    object M()
                    {
                        {
                            {
                                return rrrrrrrrrrrrrrr.PPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPP as TTTTTTT;
                            }
                        }
                    }
                }
                """,
                """
                class C {
                    object M() {
                        {
                            {
                                return rrrrrrrrrrrrrrr.PPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPP as
                                    TTTTTTT;
                            }
                        }
                    }
                }
                """
            ),
            _ => throw new ArgumentOutOfRangeException(nameof(cell))
        };

        Agrees(source, expected);
    }

    /// <summary>
    ///     Round 8: the tie as two linear conditions. The first of each pair takes the dot, the second, a column of
    ///     receiver or of the dot's column short, keeps the keyword's band: a type of 14 behind a dot at 87 and at 86,
    ///     a type of 10 whose receiver reaches the diagonal at 116, a type of 13 whose dot reaches column 100 at 117, a
    ///     keyword line past the margin, and a type of 15 that never takes it.
    /// </summary>
    [Theory]
    [InlineData("t14_i56_r24_118")]
    [InlineData("t14_i56_r23_118")]
    [InlineData("t10_i8_r27_116")]
    [InlineData("t10_i8_r26_116")]
    [InlineData("t13_i40_r53_117")]
    [InlineData("t13_i40_r52_117")]
    [InlineData("t6_i8_r30_122")]
    [InlineData("t15_i40_r50_118")]
    public void TakesTheDotByTheTwoConditions(string cell) {
        var (source, expected) = RoundEight[cell];
        Agrees(source, expected);
    }

    static readonly Dictionary<string, (string Source, string Expected)> RoundEight = new() {
        ["t14_i56_r24_118"] = (
            """
            class C
            {
                object M()
                {
                    {
                        {
                            {
                                {
                                    {
                                        {
                                            {
                                                {
                                                    {
                                                        {
                                                            {
                                                                {
                                                                    return rrrrrrrrrrrrrrrrrrrrrrrr.PPPPPPPPPPPPPPPPPPPPPPPPPPP as TTTTTTTTTTTTTT;
                                                                }
                                                            }
                                                        }
                                                    }
                                                }
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
                object M() {
                    {
                        {
                            {
                                {
                                    {
                                        {
                                            {
                                                {
                                                    {
                                                        {
                                                            {
                                                                {
                                                                    return rrrrrrrrrrrrrrrrrrrrrrrr
                                                                        .PPPPPPPPPPPPPPPPPPPPPPPPPPP as TTTTTTTTTTTTTT;
                                                                }
                                                            }
                                                        }
                                                    }
                                                }
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
        ),
        ["t14_i56_r23_118"] = (
            """
            class C
            {
                object M()
                {
                    {
                        {
                            {
                                {
                                    {
                                        {
                                            {
                                                {
                                                    {
                                                        {
                                                            {
                                                                {
                                                                    return rrrrrrrrrrrrrrrrrrrrrrr.PPPPPPPPPPPPPPPPPPPPPPPPPPPP as TTTTTTTTTTTTTT;
                                                                }
                                                            }
                                                        }
                                                    }
                                                }
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
                object M() {
                    {
                        {
                            {
                                {
                                    {
                                        {
                                            {
                                                {
                                                    {
                                                        {
                                                            {
                                                                {
                                                                    return rrrrrrrrrrrrrrrrrrrrrrr.PPPPPPPPPPPPPPPPPPPPPPPPPPPP as
                                                                        TTTTTTTTTTTTTT;
                                                                }
                                                            }
                                                        }
                                                    }
                                                }
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
        ),
        ["t10_i8_r27_116"] = (
            """
            class C
            {
                object M()
                {
                    return rrrrrrrrrrrrrrrrrrrrrrrrrrr.PPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPP as TTTTTTTTTT;
                }
            }
            """,
            """
            class C {
                object M() {
                    return rrrrrrrrrrrrrrrrrrrrrrrrrrr
                        .PPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPP as TTTTTTTTTT;
                }
            }
            """
        ),
        ["t10_i8_r26_116"] = (
            """
            class C
            {
                object M()
                {
                    return rrrrrrrrrrrrrrrrrrrrrrrrrr.PPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPP as TTTTTTTTTT;
                }
            }
            """,
            """
            class C {
                object M() {
                    return rrrrrrrrrrrrrrrrrrrrrrrrrr.PPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPP as
                        TTTTTTTTTT;
                }
            }
            """
        ),
        ["t6_i8_r30_122"] = (
            """
            class C
            {
                object M()
                {
                    return rrrrrrrrrrrrrrrrrrrrrrrrrrrrrr.PPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPP as TTTTTT;
                }
            }
            """,
            """
            class C {
                object M() {
                    return rrrrrrrrrrrrrrrrrrrrrrrrrrrrrr
                        .PPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPP as TTTTTT;
                }
            }
            """
        ),
        ["t15_i40_r50_118"] = (
            """
            class C
            {
                object M()
                {
                    {
                        {
                            {
                                {
                                    {
                                        {
                                            {
                                                {
                                                    return rrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrr.PPPPPPPPPPPPPPPPP is TTTTTTTTTTTTTTT;
                                                }
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
                object M() {
                    {
                        {
                            {
                                {
                                    {
                                        {
                                            {
                                                {
                                                    return rrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrr.PPPPPPPPPPPPPPPPP is
                                                        TTTTTTTTTTTTTTT;
                                                }
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
        ),
        ["t13_i40_r53_117"] = (
            """
            class C
            {
                object M()
                {
                    {
                        {
                            {
                                {
                                    {
                                        {
                                            {
                                                {
                                                    return rrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrr.PPPPPPPPPPPPP as TTTTTTTTTTTTT;
                                                }
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
                object M() {
                    {
                        {
                            {
                                {
                                    {
                                        {
                                            {
                                                {
                                                    return rrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrr
                                                        .PPPPPPPPPPPPP as TTTTTTTTTTTTT;
                                                }
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
        ),
        ["t13_i40_r52_117"] = (
            """
            class C
            {
                object M()
                {
                    {
                        {
                            {
                                {
                                    {
                                        {
                                            {
                                                {
                                                    return rrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrr.PPPPPPPPPPPPPP as TTTTTTTTTTTTT;
                                                }
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
                object M() {
                    {
                        {
                            {
                                {
                                    {
                                        {
                                            {
                                                {
                                                    return rrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrr.PPPPPPPPPPPPPP as
                                                        TTTTTTTTTTTTT;
                                                }
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
        ),
    };
}
