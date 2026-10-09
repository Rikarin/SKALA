using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Core.Configuration;

namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     Issue #446 item 1, SK-DIV-0210: a returned type test whose operand, one member access, runs past the margin by
///     itself. Every expected string is jb cleanupcode 2025.2.6's own output for the input, from the 2026-10-09 sweep.
/// </summary>
public sealed class TypeTestOperandDotIssue446Tests {
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
    ///     Past the margin the operand breaks before its dot, one level in, with the keyword and the type kept on that
    ///     line, whenever that line fits: as and is, a plain and a generic type, at indents 8 to 20.
    /// </summary>
    [Theory]
    [InlineData("as10_r20_126")]
    [InlineData("as1_r30_135")]
    [InlineData("is6_r14_127")]
    [InlineData("asg_r25_i20_126")]
    [InlineData("iss_r10_i8_124")]
    [InlineData("isg_r25_i12_130")]
    [InlineData("asg_r25_i12_132")]
    [InlineData("iss_r10_i20_124")]
    public void BreaksBeforeTheDotWhenOnlyThatLineFits(string cell) {
        var (source, expected) = cell switch {
            "as10_r20_126" => (
                """
                class C
                {
                    object M()
                    {
                        {
                            return rrrrrrrrrrrrrrrrrrrr.PPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPP as TTTTTTTTTT;
                        }
                    }
                }
                """,
                """
                class C {
                    object M() {
                        {
                            return rrrrrrrrrrrrrrrrrrrr
                                .PPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPP as TTTTTTTTTT;
                        }
                    }
                }
                """
            ),
            "as1_r30_135" => (
                """
                class C
                {
                    object M()
                    {
                        {
                            return rrrrrrrrrrrrrrrrrrrrrrrrrrrrrr.PPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPP as T;
                        }
                    }
                }
                """,
                """
                class C {
                    object M() {
                        {
                            return rrrrrrrrrrrrrrrrrrrrrrrrrrrrrr
                                .PPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPP as T;
                        }
                    }
                }
                """
            ),
            "is6_r14_127" => (
                """
                class C
                {
                    object M()
                    {
                        {
                            return rrrrrrrrrrrrrr.PPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPP is TTTTTT;
                        }
                    }
                }
                """,
                """
                class C {
                    object M() {
                        {
                            return rrrrrrrrrrrrrr
                                .PPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPP is TTTTTT;
                        }
                    }
                }
                """
            ),
            "asg_r25_i20_126" => (
                """
                class C
                {
                    object M()
                    {
                        {
                            {
                                {
                                    return rrrrrrrrrrrrrrrrrrrrrrrrr.PPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPP as List<int>;
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
                                    return rrrrrrrrrrrrrrrrrrrrrrrrr
                                        .PPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPP as List<int>;
                                }
                            }
                        }
                    }
                }
                """
            ),
            "iss_r10_i8_124" => (
                """
                class C
                {
                    object M()
                    {
                        return rrrrrrrrrr.PPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPP is string;
                    }
                }
                """,
                """
                class C {
                    object M() {
                        return rrrrrrrrrr
                            .PPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPP is string;
                    }
                }
                """
            ),
            "isg_r25_i12_130" => (
                """
                class C
                {
                    object M()
                    {
                        {
                            return rrrrrrrrrrrrrrrrrrrrrrrrr.PPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPP is List<int>;
                        }
                    }
                }
                """,
                """
                class C {
                    object M() {
                        {
                            return rrrrrrrrrrrrrrrrrrrrrrrrr
                                .PPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPP is List<int>;
                        }
                    }
                }
                """
            ),
            "asg_r25_i12_132" => (
                """
                class C
                {
                    object M()
                    {
                        {
                            return rrrrrrrrrrrrrrrrrrrrrrrrr.PPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPP as List<int>;
                        }
                    }
                }
                """,
                """
                class C {
                    object M() {
                        {
                            return rrrrrrrrrrrrrrrrrrrrrrrrr
                                .PPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPP as List<int>;
                        }
                    }
                }
                """
            ),
            "iss_r10_i20_124" => (
                """
                class C
                {
                    object M()
                    {
                        {
                            {
                                {
                                    return rrrrrrrrrr.PPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPP is string;
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
                                    return rrrrrrrrrr
                                        .PPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPP is string;
                                }
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
    ///     Where the operand fits on its line the keyword's band answers, as before.
    /// </summary>
    [Theory]
    [InlineData("as1_r1_120")]
    [InlineData("as1_r1_122")]
    [InlineData("iss_r3_i8_120")]
    public void LeavesTheKeywordBandWhereTheOperandFits(string cell) {
        var (source, expected) = cell switch {
            "as1_r1_120" => (
                """
                class C
                {
                    object M()
                    {
                        {
                            return r.PPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPP as T;
                        }
                    }
                }
                """,
                """
                class C {
                    object M() {
                        {
                            return r.PPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPP as
                                T;
                        }
                    }
                }
                """
            ),
            "as1_r1_122" => (
                """
                class C
                {
                    object M()
                    {
                        {
                            return r.PPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPP as T;
                        }
                    }
                }
                """,
                """
                class C {
                    object M() {
                        {
                            return r.PPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPP
                                as T;
                        }
                    }
                }
                """
            ),
            "iss_r3_i8_120" => (
                """
                class C
                {
                    object M()
                    {
                        return rrr.PPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPP is string;
                    }
                }
                """,
                """
                class C {
                    object M() {
                        return rrr.PPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPP is
                            string;
                    }
                }
                """
            ),
            _ => throw new ArgumentOutOfRangeException(nameof(cell))
        };

        Agrees(source, expected);
    }
}
