using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Core.Configuration;

namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     Issue #446 item 2, SK-DIV-0211 round 6: a typed local whose head overflows by itself breaks between its type and
///     its name, and the value then continues from the name's line — <c>T</c> / <c>name =</c> at +4 / the value at +8.
///     Every expected string is jb cleanupcode 2025.2.6's own output for the input, from the 2026-10-14 probes.
/// </summary>
public sealed class TypeNameValueLevelIssue446Tests {
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
    ///     The value whole at +8, the value's call chopped from +8, and a head that does not break before its name,
    ///     whose value stays one level in.
    /// </summary>
    [Theory]
    [InlineData("h_24_62_40_11_13_100")]
    [InlineData("h_32_54_44_24_16_118")]
    [InlineData("h_8_60_30_10_12_110")]
    public void ContinuesFromTheNamesLine(string cell) {
        var (source, expected) = Cells[cell];
        Agrees(source, expected);
    }

    static readonly Dictionary<string, (string Source, string Expected)> Cells = new() {
        ["h_24_62_40_11_13_100"] = (
            """
            class C
            {
                object M()
                {
                    {
                        {
                            {
                                {
                                    Tyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyy[] daaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa = xxxxxxxxxxx.Mmmmmmmmmmmmm(vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv);
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
                                    Tyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyy[]
                                        daaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa =
                                            xxxxxxxxxxx.Mmmmmmmmmmmmm(vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv);
                                }
                            }
                        }
                    }
                }
            }
            """
        ),
        ["h_32_54_44_24_16_118"] = (
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
                                            Tyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyy daaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa = xxxxxxxxxxxxxxxxxxxxxxxx.Mmmmmmmmmmmmmmmm(vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv);
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
                                            Tyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyy
                                                daaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa =
                                                    xxxxxxxxxxxxxxxxxxxxxxxx.Mmmmmmmmmmmmmmmm(
                                                        vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv
                                                    );
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
        ["h_8_60_30_10_12_110"] = (
            """
            class C
            {
                object M()
                {
                    Tyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyy daaaaaaaaaaaaaaaaaaaaaaaaaaaaa = xxxxxxxxxx.Mmmmmmmmmmmm("ssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssss");
                }
            }
            """,
            """
            class C {
                object M() {
                    Tyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyy daaaaaaaaaaaaaaaaaaaaaaaaaaaaa =
                        xxxxxxxxxx.Mmmmmmmmmmmm("ssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssss");
                }
            }
            """
        )
    };
}
