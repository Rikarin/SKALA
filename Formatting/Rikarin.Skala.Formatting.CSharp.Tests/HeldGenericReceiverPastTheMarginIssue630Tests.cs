using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Core.Configuration;

namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     Issue #630: a typed local before a single generic call whose receiver runs past the margin beside the <c>=</c>.
///     Pass one broke inside the type arguments, <c>= r.M&lt;</c> / <c>G…&gt;(</c>, which the oracle never writes; pass
///     two broke the <c>=</c>, which is the oracle's answer on the first pass. Every expected string is jb cleanupcode
///     2025.2.6's own output for the input.
/// </summary>
public sealed class HeldGenericReceiverPastTheMarginIssue630Tests {
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
    ///     Two cells of #626's seed-61733 probe that took two passes, an identifier and a string argument.
    /// </summary>
    [Theory]
    [InlineData("g_40_50_20_9_5_138_g45")]
    [InlineData("g_48_12_46_15_3_134_g49")]
    public void BreaksTheEqualsOnTheFirstPass(string cell) {
        var (source, expected) = Cells[cell];
        Agrees(source, expected);
    }

    static readonly Dictionary<string, (string Source, string Expected)> Cells = new() {
        ["g_40_50_20_9_5_138_g45"] = (
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
                                                    List<Tyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyy> daaaaaaaaaaaaaaaaaaa = xxxxxxxxx.Mmmmm<Ggggggggggggggggggggggggggggggggggggggggggggg>(vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv);
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
                                                    List<Tyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyy> daaaaaaaaaaaaaaaaaaa =
                                                        xxxxxxxxx.Mmmmm<Ggggggggggggggggggggggggggggggggggggggggggggg>(
                                                            vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv
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
            }
            """
        ),
        ["g_48_12_46_15_3_134_g49"] = (
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
                                                            List<Tyyyyy> daaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa = xxxxxxxxxxxxxxx.Mmm<Ggggggggggggggggggggggggggggggggggggggggggggggggg>("ssssssssssssssssssssssssssssssssssssssssssssssssssssssssss");
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
                                                            List<Tyyyyy> daaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa =
                                                                xxxxxxxxxxxxxxx
                                                                    .Mmm<Ggggggggggggggggggggggggggggggggggggggggggggggggg>(
                                                                        "ssssssssssssssssssssssssssssssssssssssssssssssssssssssssss"
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
                    }
                }
            }
            """
        )
    };
}
