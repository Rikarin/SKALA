using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Core.Configuration;

namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     Issue #603, round ten (SK-DIV-0570): a parameter's attribute section of two or more positional arguments outside a
///     default's band. Its second condition is a steep plane in the section's end as well — the last parameter width that
///     chops at all starts chopping between 113 and 120 — and the first condition reads the indent and the head on one
///     coefficient. Every expected string is jb cleanupcode 2025.2.6's own output for the input.
/// </summary>
public sealed class SecondConditionPlaneIssue603Tests {
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
    ///     The last chopping parameter width chops only late (119 keeps the section whole, 120 chops), one column wider
    ///     chops where <c>5·w + 2·i − h ≤ 155</c> said never, and the first condition sits a column later at some widths.
    /// </summary>
    [Theory]
    [InlineData("e603:i8_A_w28_E119")]
    [InlineData("e603:i8_A_w28_E120")]
    [InlineData("e603:i24_A_w22_E117")]
    [InlineData("e603:i24_A_w22_E118")]
    [InlineData("e603:i16_Range_w26_E119")]
    [InlineData("e603:i8_A_w23_E106")]
    [InlineData("e603:i8_A_w23_E107")]
    [InlineData("f603b:r0184_i8_NotNullIfNotNull_f2_w28_E120")]
    [InlineData("f603b:r0211_i16_Column_f2_w26_E117_L")]
    [InlineData("f603b:r0584_i24_Bind_f1_w17_E107")]
    public void TheSecondConditionIsAPlaneInTheSectionsEnd(string cell) {
        var (source, expected) = cell switch {
            "e603:i8_A_w28_E119" => (
                """
                class C {
                    void M(
                        int b,
                        [A(1, "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")] int valueabcdefghijklmnopqrs,
                        int c
                    ) { }
                }
                """,
                """
                class C {
                    void M(
                        int b,
                        [A(1, "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
                        int valueabcdefghijklmnopqrs,
                        int c
                    ) { }
                }
                """
            ),
            "e603:i8_A_w28_E120" => (
                """
                class C {
                    void M(
                        int b,
                        [A(1, "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")] int valueabcdefghijklmnopqrs,
                        int c
                    ) { }
                }
                """,
                """
                class C {
                    void M(
                        int b,
                        [A(
                            1,
                            "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"
                        )]
                        int valueabcdefghijklmnopqrs,
                        int c
                    ) { }
                }
                """
            ),
            "e603:i24_A_w22_E117" => (
                """
                class C {
                    class D0 {
                        class D1 {
                            class D2 {
                                class D3 {
                                    void M(
                                        int b,
                                        [A(1, "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")] int valueabcdefghijklm,
                                        int c
                                    ) { }
                                }
                            }
                        }
                    }
                }
                """,
                """
                class C {
                    class D0 {
                        class D1 {
                            class D2 {
                                class D3 {
                                    void M(
                                        int b,
                                        [A(1, "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
                                        int valueabcdefghijklm,
                                        int c
                                    ) { }
                                }
                            }
                        }
                    }
                }
                """
            ),
            "e603:i24_A_w22_E118" => (
                """
                class C {
                    class D0 {
                        class D1 {
                            class D2 {
                                class D3 {
                                    void M(
                                        int b,
                                        [A(1, "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")] int valueabcdefghijklm,
                                        int c
                                    ) { }
                                }
                            }
                        }
                    }
                }
                """,
                """
                class C {
                    class D0 {
                        class D1 {
                            class D2 {
                                class D3 {
                                    void M(
                                        int b,
                                        [A(
                                            1,
                                            "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"
                                        )]
                                        int valueabcdefghijklm,
                                        int c
                                    ) { }
                                }
                            }
                        }
                    }
                }
                """
            ),
            "e603:i16_Range_w26_E119" => (
                """
                class C {
                    class D0 {
                        class D1 {
                            void M(
                                int b,
                                [Range(1, "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")] int valueabcdefghijklmnopq,
                                int c
                            ) { }
                        }
                    }
                }
                """,
                """
                class C {
                    class D0 {
                        class D1 {
                            void M(
                                int b,
                                [Range(
                                    1,
                                    "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"
                                )]
                                int valueabcdefghijklmnopq,
                                int c
                            ) { }
                        }
                    }
                }
                """
            ),
            "e603:i8_A_w23_E106" => (
                """
                class C {
                    void M(
                        int b,
                        [A(1, "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")] int valueabcdefghijklmn,
                        int c
                    ) { }
                }
                """,
                """
                class C {
                    void M(
                        int b,
                        [A(1, "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
                        int valueabcdefghijklmn,
                        int c
                    ) { }
                }
                """
            ),
            "e603:i8_A_w23_E107" => (
                """
                class C {
                    void M(
                        int b,
                        [A(1, "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")] int valueabcdefghijklmn,
                        int c
                    ) { }
                }
                """,
                """
                class C {
                    void M(
                        int b,
                        [A(
                            1,
                            "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"
                        )]
                        int valueabcdefghijklmn,
                        int c
                    ) { }
                }
                """
            ),
            "f603b:r0184_i8_NotNullIfNotNull_f2_w28_E120" => (
                """
                class C {
                    void M(
                        int b,
                        [NotNullIfNotNull(typeof(int), "ccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc")] long valueabcdefghijklmnopqr,
                        int c
                    ) { }
                }
                """,
                """
                class C {
                    void M(
                        int b,
                        [NotNullIfNotNull(
                            typeof(int),
                            "ccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc"
                        )]
                        long valueabcdefghijklmnopqr,
                        int c
                    ) { }
                }
                """
            ),
            "f603b:r0211_i16_Column_f2_w26_E117_L" => (
                """
                class C {
                    class D0 {
                        class D1 {
                            void M(
                                int b,
                                [Column(typeof(int), "cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc")] string valueabcdefghijklmn
                            ) { }
                        }
                    }
                }
                """,
                """
                class C {
                    class D0 {
                        class D1 {
                            void M(
                                int b,
                                [Column(typeof(int), "cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc")]
                                string valueabcdefghijklmn
                            ) { }
                        }
                    }
                }
                """
            ),
            "f603b:r0584_i24_Bind_f1_w17_E107" => (
                """
                class C {
                    class D0 {
                        class D1 {
                            class D2 {
                                class D3 {
                                    void M(
                                        int b,
                                        [Bind("bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb", 2)] bool valueabcdefg,
                                        int c
                                    ) { }
                                }
                            }
                        }
                    }
                }
                """,
                """
                class C {
                    class D0 {
                        class D1 {
                            class D2 {
                                class D3 {
                                    void M(
                                        int b,
                                        [Bind("bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb", 2)]
                                        bool valueabcdefg,
                                        int c
                                    ) { }
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
}
