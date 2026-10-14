using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Core.Configuration;

namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     Issue #621, SK-DIV-0540: <c>operand is N.T</c>, which the oracle reads as a constant pattern that is a member
///     access and breaks before its own dot past the margin. Every expected string is jb cleanupcode 2025.2.6's own
///     output for the input, from the 2026-10-14 sweeps (<c>Fitter.ThePatternDotTakesTheBreak</c>).
/// </summary>
public sealed class ConstantPatternDotIssue621Tests {
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
    ///     After <c>return</c> the pattern's dot takes the line at indent 24 and the keyword keeps it at indent 8; beside
    ///     <c>var x =</c> the dot takes the joined line, and a column further the <c>=</c> breaks first; a head of five
    ///     keeps its <c>=</c> and the keyword's break.
    /// </summary>
    [Theory]
    [InlineData("ret_i24_t12_110")]
    [InlineData("ret_i8_t12_110")]
    [InlineData("var_i24_t12_106")]
    [InlineData("var_i24_t12_111")]
    [InlineData("h5_i8_t4_114")]
    public void ThePatternDotTakesTheLineByTheMeasuredLimit(string cell) {
        var (source, expected) = Cells[cell];
        Agrees(source, expected);
    }

    static readonly Dictionary<string, (string Source, string Expected)> Cells = new() {
        ["ret_i24_t12_110"] = (
            """
            class C
            {
                object M()
                {
                    {
                        {
                            {
                                {
                                    return rrrrr.PPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPP is N.TTTTTTTTTTTT;
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
                                    return rrrrr.PPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPP is N
                                        .TTTTTTTTTTTT;
                                }
                            }
                        }
                    }
                }
            }
            """
        ),
        ["ret_i8_t12_110"] = (
            """
            class C
            {
                object M()
                {
                    return rrrrr.PPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPP is N.TTTTTTTTTTTT;
                }
            }
            """,
            """
            class C {
                object M() {
                    return rrrrr.PPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPP is
                        N.TTTTTTTTTTTT;
                }
            }
            """
        ),
        ["var_i24_t12_106"] = (
            """
            class C
            {
                object M()
                {
                    {
                        {
                            {
                                {
                                    var x = rrrrr.PPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPP is NNN.TTTTTTTTTTTT;
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
                                    var x = rrrrr.PPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPP is NNN
                                        .TTTTTTTTTTTT;
                                }
                            }
                        }
                    }
                }
            }
            """
        ),
        ["var_i24_t12_111"] = (
            """
            class C
            {
                object M()
                {
                    {
                        {
                            {
                                {
                                    var x = rrrrr.PPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPP is NNN.TTTTTTTTTTTT;
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
                                    var x =
                                        rrrrr.PPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPP is NNN
                                            .TTTTTTTTTTTT;
                                }
                            }
                        }
                    }
                }
            }
            """
        ),
        ["h5_i8_t4_114"] = (
            """
            class C
            {
                object M()
                {
                    T x = rrrrr.PPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPP is N.TTTT;
                }
            }
            """,
            """
            class C {
                object M() {
                    T x = rrrrr.PPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPP is
                        N.TTTT;
                }
            }
            """
        )
    };
}
