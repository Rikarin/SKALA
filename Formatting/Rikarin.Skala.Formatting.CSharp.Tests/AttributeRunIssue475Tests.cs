using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Core.Configuration;

namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     Issue #475, SK-DIV-0350: a parameter&apos;s run of two or more attribute sections is one line, or every section and the parameter on lines of their own. Every expected string is <c>jb cleanupcode</c> 2025.2.6's own output for the input under
///     <c>SkalaFormatOnly</c>, and each test asserts the second pass too.
/// </summary>
public sealed class AttributeRunIssue475Tests {
    /// <summary>The oracle's answer under the repository's export with <paramref name="overrides" /> on top.</summary>
    static void Agrees(string source, string expected, params (string Key, string Value)[] overrides) {
        var options = new PhaseOneOptions(
            OptionResolver.Resolve(
                Path.Combine(Rikarin.Skala.Testing.Corpus.RepositoryRoot, "Test.cs"),
                [.. overrides.Select(static o => new KeyValuePair<string, string>(o.Key, o.Value))]
            )
                .Options
        );

        var once = CSharpFormatter.Format("Test.cs", SourceText.From(source), options).Formatted;
        Assert.True(
            expected.TrimEnd('\n') == once.TrimEnd('\n'),
            $"Skala's output is not the oracle's.\n--- Skala ---\n{once}\n--- oracle ---\n{expected}"
        );

        var twice = CSharpFormatter.Format("Test.cs", SourceText.From(once), options).Formatted;
        Assert.True(once == twice, $"took two passes to settle:\n{once}\n--- pass two ---\n{twice}");
    }

    /// <summary>#475: once one section spans lines, by an author&apos;s break or by width, first, last or in the middle, every gap after a section breaks.</summary>
    [Fact]
    public void ASectionSpanningLines_PutsEverySectionAndTheParameterOnItsOwnLine() =>
        Agrees(
            """
            class C {
                void M([Description("aaaaaaaaaaaaaaaaaaaaaaa",
            "bbbbbbbbbbbbbbbbb")] [Obsolete] int a) { }
            }

            class C {
                void M([Obsolete] [Description("aaaaaaaaaaaaaaaaaaaaaaa",
            "bbbbbbbbbbbbbbbbb")] int a) { }
            }

            class C {
                void M([Description("aaaaaaaaaaaaaaaaaaaaaaa",
            "bbbbbbbbbbbbbbbbb")] [Obsolete] [Serializable] int a) { }
            }

            class C {
                void M(int b, [Description("aaaaaaaaaaaaaaaaaaaaaaa",
            "bbbbbbbbbbbbbbbbb")] [Obsolete] int a) { }
            }

            class C {
                void M([Description("aaaaaaaaaaaaaaaaaaaaaaa",
            "bbbbbbbbbbbbbbbbb")] [Obsolete] ref int a, int b) { }
            }

            class C {
                void M([Description("aaaaaaaaaaaaaaaaaaaaaaa",
            "bbbbbbbbbbbbbbbbb")][Obsolete] int a) { }
            }

            class C {
                void M([Description("aaaaaaaaaaaaaaaaaaaaaaa",
            "bbbbbbbbbbbbbbbbb")] [Obsolete]
             int a) { }
            }

            class C {
                void M(int b, [Description("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")] [Obsolete] int a) { }
            }

            class C {
                void M(int b, [Obsolete] [Description("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")] int a) { }
            }

            class C {
                void M([Obsolete] [Description("aaaaaaaaaaaaaaaaaaaaaaa",
            "bbbbbbbbbbbbbbbbb")] [Serializable] int a) { }
            }

            class C {
                void M() {
                    var f = ([Description("aaaaaaaaaaaaaaaaaaaaaaa",
            "bbbbbbbbbbbbbbbbb")] [Obsolete] int a) => a;
                }
            }
            """,
            """
            class C {
                void M(
                    [Description(
                        "aaaaaaaaaaaaaaaaaaaaaaa",
                        "bbbbbbbbbbbbbbbbb"
                    )]
                    [Obsolete]
                    int a
                ) { }
            }

            class C {
                void M(
                    [Obsolete]
                    [Description(
                        "aaaaaaaaaaaaaaaaaaaaaaa",
                        "bbbbbbbbbbbbbbbbb"
                    )]
                    int a
                ) { }
            }

            class C {
                void M(
                    [Description(
                        "aaaaaaaaaaaaaaaaaaaaaaa",
                        "bbbbbbbbbbbbbbbbb"
                    )]
                    [Obsolete]
                    [Serializable]
                    int a
                ) { }
            }

            class C {
                void M(
                    int b,
                    [Description(
                        "aaaaaaaaaaaaaaaaaaaaaaa",
                        "bbbbbbbbbbbbbbbbb"
                    )]
                    [Obsolete]
                    int a
                ) { }
            }

            class C {
                void M(
                    [Description(
                        "aaaaaaaaaaaaaaaaaaaaaaa",
                        "bbbbbbbbbbbbbbbbb"
                    )]
                    [Obsolete]
                    ref int a,
                    int b
                ) { }
            }

            class C {
                void M(
                    [Description(
                        "aaaaaaaaaaaaaaaaaaaaaaa",
                        "bbbbbbbbbbbbbbbbb"
                    )]
                    [Obsolete]
                    int a
                ) { }
            }

            class C {
                void M(
                    [Description(
                        "aaaaaaaaaaaaaaaaaaaaaaa",
                        "bbbbbbbbbbbbbbbbb"
                    )]
                    [Obsolete]
                    int a
                ) { }
            }

            class C {
                void M(
                    int b,
                    [Description(
                        "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"
                    )]
                    [Obsolete]
                    int a
                ) { }
            }

            class C {
                void M(
                    int b,
                    [Obsolete]
                    [Description(
                        "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"
                    )]
                    int a
                ) { }
            }

            class C {
                void M(
                    [Obsolete]
                    [Description(
                        "aaaaaaaaaaaaaaaaaaaaaaa",
                        "bbbbbbbbbbbbbbbbb"
                    )]
                    [Serializable]
                    int a
                ) { }
            }

            class C {
                void M() {
                    var f = (
                        [Description(
                            "aaaaaaaaaaaaaaaaaaaaaaa",
                            "bbbbbbbbbbbbbbbbb"
                        )]
                        [Obsolete]
                        int a
                    ) => a;
                }
            }
            """
        );

    /// <summary>Two sections that do not fit together break apart and the parameter follows; two that fit stay together, an author&apos;s break between them is joined, and the gap before a long parameter keeps its own rule.</summary>
    [Fact]
    public void SectionsThatFitTogether_StayTogether() =>
        Agrees(
            """
            class C {
                void M(int b, [Obsolete("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")] [Description("dddddddddddddddddddddddddddddddddddddddddddddddddd")] int a) { }
            }

            class C {
                void M(int b, [Obsolete] [Serializable] Dictionary<string, List<int>> pppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppp) { }
            }

            class C {
                void M(int b, [Obsolete("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")] [Serializable] int a) { }
            }

            class C {
                void M([Obsolete]
            [Serializable] int a) { }
            }

            class C {
                void M([Obsolete] [Serializable]
            int a) { }
            }

            class C {
                void M([Obsolete] [Serializable] int a) { }
            }
            """,
            """
            class C {
                void M(
                    int b,
                    [Obsolete("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
                    [Description("dddddddddddddddddddddddddddddddddddddddddddddddddd")]
                    int a
                ) { }
            }

            class C {
                void M(
                    int b,
                    [Obsolete] [Serializable]
                    Dictionary<string, List<int>> pppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppp
                ) { }
            }

            class C {
                void M(
                    int b,
                    [Obsolete("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")] [Serializable] int a
                ) { }
            }

            class C {
                void M([Obsolete] [Serializable] int a) { }
            }

            class C {
                void M([Obsolete] [Serializable] int a) { }
            }

            class C {
                void M([Obsolete] [Serializable] int a) { }
            }
            """
        );
}
