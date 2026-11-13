using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Core.Configuration;

namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     Issue #603, round eleven (SK-DIV-0730): a parameter's attribute section inside a default's band chops by the rule
///     outside it, read on a parameter about 3/13 of its kept part wider, for one argument as for two, and a parameter of
///     eleven columns or fewer takes the measured section rule like any other. Every expected string is jb cleanupcode
///     2025.2.6's own output for the input.
/// </summary>
public sealed class DefaultBandWiderParameterIssue603Tests {
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
    ///     Inside a default's band, two or more positional arguments chop by the rule outside it read on a parameter about
    ///     3/13 of its kept part wider: the first condition one column either side of round nine's, and the second the
    ///     steep plane, which holds a section whole until the band's last column.
    /// </summary>
    [Theory]
    [InlineData("b2:r00005_i16_JsonProperty_fp2b_w19_v12_E112")]
    [InlineData("f1:r00053_i28_ValidateWith_fn2b_w14_v7_E111")]
    [InlineData("f1:r00054_i28_ValidateWith_fn2b_w14_v7_E112")]
    [InlineData("g2:r00836_i24_A_fp2a_w20_v14_E113")]
    [InlineData("g2:r00837_i24_A_fp2a_w20_v14_E114")]
    [InlineData("b2:r00236_i12_A_fp2t_w24_v17_E113")]
    [InlineData("b3:r00012_i28_StringLength_fp2b_w13_v5_E110_L")]
    public void ADefaultsBandIsTheRuleOnAWiderParameter(string cell) {
        var (source, expected) = cell switch {
            "b2:r00005_i16_JsonProperty_fp2b_w19_v12_E112" => (
                """
                class C {
                    class D0 {
                        class D1 {
                            void M(
                                int b,
                                [JsonProperty("sssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssss", 2)] T va = Kind.MMMMMMM,
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
                                [JsonProperty(
                                    "sssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssss",
                                    2
                                )]
                                T va = Kind.MMMMMMM,
                                int c
                            ) { }
                        }
                    }
                }
                """
            ),
            "f1:r00053_i28_ValidateWith_fn2b_w14_v7_E111" => (
                """
                class C {
                    class D0 {
                        class D1 {
                            class D2 {
                                class D3 {
                                    class D4 {
                                        void M(
                                            int b,
                                            [ValidateWith("sssssssssssssssssssssssssssssssssssssssssssssssssssssss", order: 2)] T va = default,
                                            int c
                                        ) { }
                                    }
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
                                    class D4 {
                                        void M(
                                            int b,
                                            [ValidateWith("sssssssssssssssssssssssssssssssssssssssssssssssssssssss", order: 2)] T va =
                                                default,
                                            int c
                                        ) { }
                                    }
                                }
                            }
                        }
                    }
                }
                """
            ),
            "f1:r00054_i28_ValidateWith_fn2b_w14_v7_E112" => (
                """
                class C {
                    class D0 {
                        class D1 {
                            class D2 {
                                class D3 {
                                    class D4 {
                                        void M(
                                            int b,
                                            [ValidateWith("ssssssssssssssssssssssssssssssssssssssssssssssssssssssss", order: 2)] T va = default,
                                            int c
                                        ) { }
                                    }
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
                                    class D4 {
                                        void M(
                                            int b,
                                            [ValidateWith(
                                                "ssssssssssssssssssssssssssssssssssssssssssssssssssssssss",
                                                order: 2
                                            )]
                                            T va = default,
                                            int c
                                        ) { }
                                    }
                                }
                            }
                        }
                    }
                }
                """
            ),
            "g2:r00836_i24_A_fp2a_w20_v14_E113" => (
                """
                class C {
                    class D0 {
                        class D1 {
                            class D2 {
                                class D3 {
                                    void M(
                                        int b,
                                        [A(1, "sssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssss")] T v = "dddddddddddd",
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
                                        [A(1, "sssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssss")] T v =
                                            "dddddddddddd",
                                        int c
                                    ) { }
                                }
                            }
                        }
                    }
                }
                """
            ),
            "g2:r00837_i24_A_fp2a_w20_v14_E114" => (
                """
                class C {
                    class D0 {
                        class D1 {
                            class D2 {
                                class D3 {
                                    void M(
                                        int b,
                                        [A(1, "ssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssss")] T v = "dddddddddddd",
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
                                            "ssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssss"
                                        )]
                                        T v = "dddddddddddd",
                                        int c
                                    ) { }
                                }
                            }
                        }
                    }
                }
                """
            ),
            "b2:r00236_i12_A_fp2t_w24_v17_E113" => (
                """
                class C {
                    class D0 {
                        void M(
                            int b,
                            [A(typeof(int), "sssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssss")] T va = "ddddddddddddddd",
                            int c
                        ) { }
                    }
                }
                """,
                """
                class C {
                    class D0 {
                        void M(
                            int b,
                            [A(typeof(int), "sssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssss")] T va =
                                "ddddddddddddddd",
                            int c
                        ) { }
                    }
                }
                """
            ),
            "b3:r00012_i28_StringLength_fp2b_w13_v5_E110_L" => (
                """
                class C {
                    class D0 {
                        class D1 {
                            class D2 {
                                class D3 {
                                    class D4 {
                                        void M(
                                            int b,
                                            [StringLength("sssssssssssssssssssssssssssssssssssssssssssssssssssssssssssss", 2)] T vab = 10000
                                        ) { }
                                    }
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
                                    class D4 {
                                        void M(
                                            int b,
                                            [StringLength(
                                                "sssssssssssssssssssssssssssssssssssssssssssssssssssssssssssss",
                                                2
                                            )]
                                            T vab = 10000
                                        ) { }
                                    }
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
    ///     A parameter of eleven columns or fewer takes the section rule too: from indent 16 a one-argument section stays
    ///     whole above it, and above a default the section joins and the = breaks, where every joined line that
    ///     overflowed used to chop.
    /// </summary>
    [Theory]
    [InlineData("b4:r00099_i16_StringLength_fp1_w10_v0_E112")]
    [InlineData("b4:r00010_i24_DefaultValue_fp1_w11_v0_E114")]
    [InlineData("b4:r00027_i8_Serialize_fp1_w11_v0_E115")]
    [InlineData("b3:r00064_i28_Describe_fp1pp_w11_v1_E110")]
    [InlineData("b3:r00320_i36_ValidateWith_fp2b_w11_v3_E111")]
    public void AShortParameterTakesTheSameRule(string cell) {
        var (source, expected) = cell switch {
            "b4:r00099_i16_StringLength_fp1_w10_v0_E112" => (
                """
                class C {
                    class D0 {
                        class D1 {
                            void M(
                                int b,
                                [StringLength("ssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssss")] string? va,
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
                                [StringLength("ssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssss")]
                                string? va,
                                int c
                            ) { }
                        }
                    }
                }
                """
            ),
            "b4:r00010_i24_DefaultValue_fp1_w11_v0_E114" => (
                """
                class C {
                    class D0 {
                        class D1 {
                            class D2 {
                                class D3 {
                                    void M(
                                        int b,
                                        [DefaultValue("ssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssss")] double vabc,
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
                                        [DefaultValue("ssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssss")]
                                        double vabc,
                                        int c
                                    ) { }
                                }
                            }
                        }
                    }
                }
                """
            ),
            "b4:r00027_i8_Serialize_fp1_w11_v0_E115" => (
                """
                class C {
                    void M(
                        int b,
                        [Serialize("ssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssss")] decimal vab,
                        int c
                    ) { }
                }
                """,
                """
                class C {
                    void M(
                        int b,
                        [Serialize(
                            "ssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssss"
                        )]
                        decimal vab,
                        int c
                    ) { }
                }
                """
            ),
            "b3:r00064_i28_Describe_fp1pp_w11_v1_E110" => (
                """
                class C {
                    class D0 {
                        class D1 {
                            class D2 {
                                class D3 {
                                    class D4 {
                                        void M(
                                            int b,
                                            [Describe("sssssssssssssssssssssssssssssssssssssssssssss", Order = 2, Name = "z")] bool va = 1,
                                            int c
                                        ) { }
                                    }
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
                                    class D4 {
                                        void M(
                                            int b,
                                            [Describe("sssssssssssssssssssssssssssssssssssssssssssss", Order = 2, Name = "z")] bool va =
                                                1,
                                            int c
                                        ) { }
                                    }
                                }
                            }
                        }
                    }
                }
                """
            ),
            "b3:r00320_i36_ValidateWith_fp2b_w11_v3_E111" => (
                """
                class C {
                    class D0 {
                        class D1 {
                            class D2 {
                                class D3 {
                                    class D4 {
                                        class D5 {
                                            class D6 {
                                                void M(
                                                    int b,
                                                    [ValidateWith("ssssssssssssssssssssssssssssssssssssssssssssssssssssss", 2)] T vab = "d",
                                                    int c
                                                ) { }
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
                    class D0 {
                        class D1 {
                            class D2 {
                                class D3 {
                                    class D4 {
                                        class D5 {
                                            class D6 {
                                                void M(
                                                    int b,
                                                    [ValidateWith("ssssssssssssssssssssssssssssssssssssssssssssssssssssss", 2)] T vab =
                                                        "d",
                                                    int c
                                                ) { }
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
            _ => throw new ArgumentOutOfRangeException(nameof(cell))
        };

        Agrees(source, expected);
    }

    /// <summary>
    ///     A one-argument section does chop inside a default's band, by its own rule read on the same wider parameter, and
    ///     outside a band its first condition puts the indent and the head on one coefficient, as two arguments do, and its
    ///     steep plane is a little shallower.
    /// </summary>
    [Theory]
    [InlineData("s2:r00025_i8_A_fp1_w13_v7_E112")]
    [InlineData("s2:r00026_i8_A_fp1_w13_v7_E113")]
    [InlineData("s2:r00631_i16_A_fp1_w10_v4_E112")]
    [InlineData("s2:r00632_i16_A_fp1_w10_v4_E113")]
    [InlineData("s1:r00975_i16_Describe_fp1_w11_v2_E114")]
    [InlineData("s1:r00976_i16_Describe_fp1_w11_v2_E115")]
    [InlineData("s1:r01917_i24_NotNullIfNotNull_fp1_w9_v0_E119")]
    [InlineData("s1:r01918_i24_NotNullIfNotNull_fp1_w9_v0_E120")]
    [InlineData("c1:r00198_local_i12_Describe_fp1pp_w9_E112")]
    [InlineData("c1:r00276_delegate_i24_ValidateWith_fn1_w9_E117_L")]
    [InlineData("f12:r00994_i8_Bind_fn1_w16_v2_E119_L")]
    [InlineData("f15:r02162_i8_A_fp1_w15_v6_E117")]
    public void AOneArgumentSectionChopsInsideTheBandToo(string cell) {
        var (source, expected) = cell switch {
            "s2:r00025_i8_A_fp1_w13_v7_E112" => (
                """
                class C {
                    void M(
                        int b,
                        [A("sssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssss")] T a = "ddddd",
                        int c
                    ) { }
                }
                """,
                """
                class C {
                    void M(
                        int b,
                        [A("sssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssss")] T a =
                            "ddddd",
                        int c
                    ) { }
                }
                """
            ),
            "s2:r00026_i8_A_fp1_w13_v7_E113" => (
                """
                class C {
                    void M(
                        int b,
                        [A("ssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssss")] T a = "ddddd",
                        int c
                    ) { }
                }
                """,
                """
                class C {
                    void M(
                        int b,
                        [A(
                            "ssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssss"
                        )]
                        T a = "ddddd",
                        int c
                    ) { }
                }
                """
            ),
            "s2:r00631_i16_A_fp1_w10_v4_E112" => (
                """
                class C {
                    class D0 {
                        class D1 {
                            void M(
                                int b,
                                [A("sssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssss")] T a = "dd",
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
                                [A("sssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssss")] T a =
                                    "dd",
                                int c
                            ) { }
                        }
                    }
                }
                """
            ),
            "s2:r00632_i16_A_fp1_w10_v4_E113" => (
                """
                class C {
                    class D0 {
                        class D1 {
                            void M(
                                int b,
                                [A("ssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssss")] T a = "dd",
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
                                [A(
                                    "ssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssss"
                                )]
                                T a = "dd",
                                int c
                            ) { }
                        }
                    }
                }
                """
            ),
            "s1:r00975_i16_Describe_fp1_w11_v2_E114" => (
                """
                class C {
                    class D0 {
                        class D1 {
                            void M(
                                int b,
                                [Describe("ssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssss")] int ab = 10,
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
                                [Describe("ssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssss")]
                                int ab = 10,
                                int c
                            ) { }
                        }
                    }
                }
                """
            ),
            "s1:r00976_i16_Describe_fp1_w11_v2_E115" => (
                """
                class C {
                    class D0 {
                        class D1 {
                            void M(
                                int b,
                                [Describe("sssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssss")] int ab = 10,
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
                                [Describe(
                                    "sssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssss"
                                )]
                                int ab = 10,
                                int c
                            ) { }
                        }
                    }
                }
                """
            ),
            "s1:r01917_i24_NotNullIfNotNull_fp1_w9_v0_E119" => (
                """
                class C {
                    class D0 {
                        class D1 {
                            class D2 {
                                class D3 {
                                    void M(
                                        int b,
                                        [NotNullIfNotNull("sssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssss")] bool flag,
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
                                        [NotNullIfNotNull("sssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssss")]
                                        bool flag,
                                        int c
                                    ) { }
                                }
                            }
                        }
                    }
                }
                """
            ),
            "s1:r01918_i24_NotNullIfNotNull_fp1_w9_v0_E120" => (
                """
                class C {
                    class D0 {
                        class D1 {
                            class D2 {
                                class D3 {
                                    void M(
                                        int b,
                                        [NotNullIfNotNull("ssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssss")] bool flag,
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
                                        [NotNullIfNotNull(
                                            "ssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssss"
                                        )]
                                        bool flag,
                                        int c
                                    ) { }
                                }
                            }
                        }
                    }
                }
                """
            ),
            "c1:r00198_local_i12_Describe_fp1pp_w9_E112" => (
                """
                class C {
                    void M() {
                        void L(
                            int b,
                            [Describe("sssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssss", Order = 2, Name = "z")] int a = 5,
                            int c
                        ) { }
                    }
                }
                """,
                """
                class C {
                    void M() {
                        void L(
                            int b,
                            [Describe(
                                "sssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssss",
                                Order = 2,
                                Name = "z"
                            )]
                            int a = 5,
                            int c
                        ) { }
                    }
                }
                """
            ),
            "c1:r00276_delegate_i24_ValidateWith_fn1_w9_E117_L" => (
                """
                class C {
                    class D0 {
                        class D1 {
                            class D2 {
                                class D3 {
                                    delegate void H(
                                        int b,
                                        [ValidateWith(name: "sssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssss")] int a = 5
                                    );
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
                                    delegate void H(
                                        int b,
                                        [ValidateWith(name: "sssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssss")]
                                        int a = 5
                                    );
                                }
                            }
                        }
                    }
                }
                """
            ),
            "f12:r00994_i8_Bind_fn1_w16_v2_E119_L" => (
                """
                class C {
                    void M(
                        int b,
                        [Bind(name: "sssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssss")] long valuea = ""
                    ) { }
                }
                """,
                """
                class C {
                    void M(
                        int b,
                        [Bind(
                            name: "sssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssss"
                        )]
                        long valuea = ""
                    ) { }
                }
                """
            ),
            "f15:r02162_i8_A_fp1_w15_v6_E117" => (
                """
                class C {
                    void M(
                        int b,
                        [A("ssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssss")] int va = Kind.M,
                        int c
                    ) { }
                }
                """,
                """
                class C {
                    void M(
                        int b,
                        [A("ssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssss")]
                        int va = Kind.M,
                        int c
                    ) { }
                }
                """
            ),
            _ => throw new ArgumentOutOfRangeException(nameof(cell))
        };

        Agrees(source, expected);
    }
}
