using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Core.Configuration;

namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     Issue #626: a typed local before a single call whose <c>(</c> would land past the margin beside the <c>=</c>. Pass
///     one kept <c>T name = r.M(</c> past the margin and chopped the arguments; pass two, reading the chop as the
///     author's, broke the <c>=</c>. The oracle breaks the <c>=</c> on the first pass. Every expected string is jb
///     cleanupcode 2025.2.6's own output for the input.
/// </summary>
public sealed class HeldParenPastTheMarginIssue626Tests {
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
    ///     The two cells #626 was filed from: the value line 132 and 134 columns, the arguments chopped below.
    /// </summary>
    [Theory]
    [InlineData("p_36_31_48_4_3_132")]
    [InlineData("p_36_36_44_2_13_134")]
    public void BreaksTheEqualsOnTheFirstPass(string cell) {
        var (source, expected) = Cells[cell];
        Agrees(source, expected);
    }

    static readonly Dictionary<string, (string Source, string Expected)> Cells = new() {
        ["p_36_31_48_4_3_132"] = (
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
                                                Tyyyyyyyyyyyyyyyyyyyyyyyyyyyyyy daaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa = xxxx.Mmm(vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv);
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
                                                Tyyyyyyyyyyyyyyyyyyyyyyyyyyyyyy daaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa =
                                                    xxxx.Mmm(
                                                        vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv
                                                    );
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
        ["p_36_36_44_2_13_134"] = (
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
                                                Tyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyy daaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa = xx.Mmmmmmmmmmmmm("sssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssss");
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
                                                Tyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyy daaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa =
                                                    xx.Mmmmmmmmmmmmm(
                                                        "sssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssss"
                                                    );
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
}
