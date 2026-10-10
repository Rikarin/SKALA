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
}
