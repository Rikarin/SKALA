using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Core.Configuration;

namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     Issue #576, round fifteen (SK-DIV-0399). Below a broken <c>when</c> the condition's outer list breaks at its comma
///     by its own measured bar (<c>Fitter.BrokenWhenOuterCommaTakesTheFill</c>), and beside or below a <c>when</c> at the
///     comma whenever the condition ends three columns past the margin; a parenthesised query whose from clause does not
///     fit beside the arrow keeps it by two regimes of the arrow's end, its line's start and its width
///     (<c>Fitter.ArmQueryFillStays</c>); and a query one column over moves below. Cells from the structure grids kb1 to
///     kb5 and the fresh probes. Every expected string is jb cleanupcode 2025.2.6's own output for the input.
/// </summary>
public sealed class WhenBodyAndKeptArrowIssue576Tests {
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
    ///     Below a broken <c>when</c> the condition's list breaks at its comma by <c>2·lead + 9·below ≤ 1089</c>, and at
    ///     its comma wherever the condition ends three columns past the margin and the line below ends by 122.
    /// </summary>
    [Theory]
    [InlineData("kb1:A_K_i16_e123_L7_p_c0")]
    [InlineData("kb1:A_K_i16_e123_L6_p_c0")]
    [InlineData("kb1:A_Wd_i20_e123_L12_p_q")]
    [InlineData("kb1:A_K_i12_e124_L14_p_a")]
    [InlineData("kb1:B_K_i12_e143_L36_d_c0")]
    [InlineData("kb1:B_Wd_i20_e146_L40_s_c0")]
    [InlineData("kb1:B_K_i12_e144_L37_p_c0")]
    [InlineData("f576a:r0390_i16_c0_w125_F")]
    [InlineData("f576h:r0221_i12_c2_w43_K")]
    [InlineData("f576b:r0141_i24_c1_w80_F")]
    public void TheConditionBelowABrokenWhenFillsByItsOwnBar(string cell) {
        var (source, expected) = cell switch {
            "kb1:A_K_i16_e123_L7_p_c0" => (
                """
                class C {
                    class D0 {
                        object M() {
                            return state switch {
                                Xyyyyyyy when
                                    M<Faaa, IReadOnlyList<Ttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttt>>() => Handle(value, "ccccccccccccccccccccccccccccccccc"),
                                _ => 0
                            };
                        }
                    }
                }
                """,
                """
                class C {
                    class D0 {
                        object M() {
                            return state switch {
                                Xyyyyyyy when
                                    M<Faaa,
                                        IReadOnlyList<Ttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttt>>()
                                    => Handle(value, "ccccccccccccccccccccccccccccccccc"),
                                _ => 0
                            };
                        }
                    }
                }
                """
            ),
            "kb1:A_K_i16_e123_L6_p_c0" => (
                """
                class C {
                    class D0 {
                        object M() {
                            return state switch {
                                Xyyyyyyy when
                                    M<Faa, IReadOnlyList<Tttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttt>>() => Handle(value, "ccccccccccccccccccccccccccccccccc"),
                                _ => 0
                            };
                        }
                    }
                }
                """,
                """
                class C {
                    class D0 {
                        object M() {
                            return state switch {
                                Xyyyyyyy when
                                    M<Faa, IReadOnlyList<
                                        Tttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttt>>() => Handle(
                                        value,
                                        "ccccccccccccccccccccccccccccccccc"
                                    ),
                                _ => 0
                            };
                        }
                    }
                }
                """
            ),
            "kb1:A_Wd_i20_e123_L12_p_q" => (
                """
                class C {
                    class D0 {
                        class D1 {
                            object M() {
                                return state switch { { Kind: not null } when M<Faaaaaaaa, IReadOnlyList<Tttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttt>>() => (from item in items where "qqqqqqqqqqqqqqqqqqq" select item), _ => 0 };
                            }
                        }
                    }
                }
                """,
                """
                class C {
                    class D0 {
                        class D1 {
                            object M() {
                                return state switch {
                                    { Kind: not null } when
                                        M<Faaaaaaaa,
                                            IReadOnlyList<Tttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttt>>() =>
                                        (from item in items where "qqqqqqqqqqqqqqqqqqq" select item),
                                    _ => 0
                                };
                            }
                        }
                    }
                }
                """
            ),
            "kb1:A_K_i12_e124_L14_p_a" => (
                """
                class C {
                    object M() {
                        return state switch {
                            Xyyyyyyy when
                                M<Faaaaaaaaaa, IReadOnlyList<Ttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttt>>() => 1,
                            _ => 0
                        };
                    }
                }
                """,
                """
                class C {
                    object M() {
                        return state switch {
                            Xyyyyyyy when
                                M<Faaaaaaaaaa, IReadOnlyList<
                                    Ttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttt>>() => 1,
                            _ => 0
                        };
                    }
                }
                """
            ),
            "kb1:B_K_i12_e143_L36_d_c0" => (
                """
                class C {
                    object M() {
                        return state switch {
                            Xyyyyyyy when
                                Maaaaaaaaaa<Dictionary<int, string>, IReadOnlyList<Tttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttt>>() => Handle(value, "ccccccccccccccccccccccccccccccccc"),
                            _ => 0
                        };
                    }
                }
                """,
                """
                class C {
                    object M() {
                        return state switch {
                            Xyyyyyyy when
                                Maaaaaaaaaa<Dictionary<int, string>,
                                    IReadOnlyList<Tttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttt>>() =>
                                Handle(value, "ccccccccccccccccccccccccccccccccc"),
                            _ => 0
                        };
                    }
                }
                """
            ),
            "kb1:B_Wd_i20_e146_L40_s_c0" => (
                """
                class C {
                    class D0 {
                        class D1 {
                            object M() {
                                return state switch { { Kind: not null } when Maaaaaaaaaaaaaaaaaaaaaaaa<HashSet<long>, IReadOnlyList<Ttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttt>>() => Handle(value, "ccccccccccccccccccccccccccccccccc"), _ => 0 };
                            }
                        }
                    }
                }
                """,
                """
                class C {
                    class D0 {
                        class D1 {
                            object M() {
                                return state switch {
                                    { Kind: not null } when
                                        Maaaaaaaaaaaaaaaaaaaaaaaa<HashSet<long>,
                                            IReadOnlyList<Ttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttt>>() => Handle(
                                            value,
                                            "ccccccccccccccccccccccccccccccccc"
                                        ),
                                    _ => 0
                                };
                            }
                        }
                    }
                }
                """
            ),
            "kb1:B_K_i12_e144_L37_p_c0" => (
                """
                class C {
                    object M() {
                        return state switch {
                            Xyyyyyyy when
                                M<Faaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa, IReadOnlyList<Tttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttt>>() => Handle(value, "ccccccccccccccccccccccccccccccccc"),
                            _ => 0
                        };
                    }
                }
                """,
                """
                class C {
                    object M() {
                        return state switch {
                            Xyyyyyyy when
                                M<Faaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa, IReadOnlyList<
                                    Tttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttt>>() => Handle(
                                    value,
                                    "ccccccccccccccccccccccccccccccccc"
                                ),
                            _ => 0
                        };
                    }
                }
                """
            ),
            "f576a:r0390_i16_c0_w125_F" => (
                """
                class C {
                    class D0 {
                        object M() {
                            return state switch { var v when Make<Bbbbbbbb, IReadOnlyList<Tttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttt>>() => Handle(value, "cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc"), _ => 0 };
                        }
                    }
                }
                """,
                """
                class C {
                    class D0 {
                        object M() {
                            return state switch {
                                var v when
                                    Make<Bbbbbbbb,
                                        IReadOnlyList<Tttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttt>>() =>
                                    Handle(
                                        value,
                                        "cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc"
                                    ),
                                _ => 0
                            };
                        }
                    }
                }
                """
            ),
            "f576h:r0221_i12_c2_w43_K" => (
                """
                class C {
                    object M() {
                        return state switch {
                            { Kind: not null } when Resolve<IEnumerable<KeyValuePair<int, string>>, IReadOnlyList<Tttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttt>>() => Compute("kkkkkkkkkkkkkkkkkkkkkkk", 2, flag),
                            _ => 0
                        };
                    }
                }
                """,
                """
                class C {
                    object M() {
                        return state switch {
                            { Kind: not null } when
                                Resolve<IEnumerable<KeyValuePair<int, string>>, IReadOnlyList<
                                    Tttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttt>>() => Compute(
                                    "kkkkkkkkkkkkkkkkkkkkkkk",
                                    2,
                                    flag
                                ),
                            _ => 0
                        };
                    }
                }
                """
            ),
            "f576b:r0141_i24_c1_w80_F" => (
                """
                class C {
                    class D0 {
                        class D1 {
                            class D2 {
                                object M() {
                                    return state switch { { Kind: not null } when Convert<A, IReadOnlyList<Tttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttt>>() => new Holder(1, "nnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnn"), _ => 0 };
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
                                object M() {
                                    return state switch {
                                        { Kind: not null } when
                                            Convert<A,
                                                IReadOnlyList<Tttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttt>>() =>
                                            new Holder(1, "nnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnn"),
                                        _ => 0
                                    };
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
    ///     Where <c>(from item</c> fits beside the arrow and the whole from clause does not, the body stays by
    ///     <c>4·w + 3·s ≥ a + 299</c> (304 where only <c>(from item</c> fits) inside the <c>4·a + 3·s</c> cut.
    /// </summary>
    [Theory]
    [InlineData("kb3:c1_K_s12_a103_w91")]
    [InlineData("kb3:c1_K_s12_a103_w93")]
    [InlineData("kb3:c2_K_s28_a107_w81")]
    [InlineData("kb3:c2_K_s28_a107_w83")]
    [InlineData("kb3:c2_K_s32_a106_w77")]
    [InlineData("kb3:c2_K_s32_a106_w79")]
    [InlineData("kb2:c2_K_s28_a109_w128")]
    [InlineData("kb4:c2_K_s28_a109_w132")]
    [InlineData("f576a:r0362_i20_q_w109_F")]
    [InlineData("f576g:r0315_i16_q_w91_F")]
    [InlineData("coordQ:r0451_i20_q_w89_F")]
    public void TheQueryKeepsItsFromClauseByTwoRegimes(string cell) {
        var (source, expected) = cell switch {
            "kb3:c1_K_s12_a103_w91" => (
                """
                class C {
                    object M() {
                        return state switch { Xyyy when M<Faaaaaaaaaa, IReadOnlyList<Ttttttttttttttttttttttttttttttttttttttttttttt>>() => (from item in items where "qqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqq" select item), _ => 0 };
                    }
                }
                """,
                """
                class C {
                    object M() {
                        return state switch {
                            Xyyy when M<Faaaaaaaaaa, IReadOnlyList<Ttttttttttttttttttttttttttttttttttttttttttttt>>() =>
                                (from item in items where "qqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqq" select item),
                            _ => 0
                        };
                    }
                }
                """
            ),
            "kb3:c1_K_s12_a103_w93" => (
                """
                class C {
                    object M() {
                        return state switch { Xyyy when M<Faaaaaaaaaa, IReadOnlyList<Ttttttttttttttttttttttttttttttttttttttttttttt>>() => (from item in items where "qqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqq" select item), _ => 0 };
                    }
                }
                """,
                """
                class C {
                    object M() {
                        return state switch {
                            Xyyy when M<Faaaaaaaaaa, IReadOnlyList<Ttttttttttttttttttttttttttttttttttttttttttttt>>() => (from item in
                                    items
                                where "qqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqq"
                                select item),
                            _ => 0
                        };
                    }
                }
                """
            ),
            "kb3:c2_K_s28_a107_w81" => (
                """
                class C {
                    class D0 {
                        class D1 {
                            class D2 {
                                object M() {
                                    return state switch {
                                        Xyyyyyyy when
                                            M<Faaaaaaaaaa, IReadOnlyList<Ttttttttttttttttttttttttttttttttttttttttttt>>() => (from item in items where "qqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqq" select item),
                                        _ => 0
                                    };
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
                                object M() {
                                    return state switch {
                                        Xyyyyyyy when
                                            M<Faaaaaaaaaa, IReadOnlyList<Ttttttttttttttttttttttttttttttttttttttttttt>>() =>
                                            (from item in items where "qqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqq" select item),
                                        _ => 0
                                    };
                                }
                            }
                        }
                    }
                }
                """
            ),
            "kb3:c2_K_s28_a107_w83" => (
                """
                class C {
                    class D0 {
                        class D1 {
                            class D2 {
                                object M() {
                                    return state switch {
                                        Xyyyyyyy when
                                            M<Faaaaaaaaaa, IReadOnlyList<Ttttttttttttttttttttttttttttttttttttttttttt>>() => (from item in items where "qqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqq" select item),
                                        _ => 0
                                    };
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
                                object M() {
                                    return state switch {
                                        Xyyyyyyy when
                                            M<Faaaaaaaaaa, IReadOnlyList<Ttttttttttttttttttttttttttttttttttttttttttt>>() => (from item
                                                    in items
                                                where "qqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqq"
                                                select item),
                                        _ => 0
                                    };
                                }
                            }
                        }
                    }
                }
                """
            ),
            "kb3:c2_K_s32_a106_w77" => (
                """
                class C {
                    class D0 {
                        class D1 {
                            class D2 {
                                class D3 {
                                    object M() {
                                        return state switch {
                                            Xyyyyyyy when
                                                M<Faaaaaaaaaa, IReadOnlyList<Tttttttttttttttttttttttttttttttttttttt>>() => (from item in items where "qqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqq" select item),
                                            _ => 0
                                        };
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
                                    object M() {
                                        return state switch {
                                            Xyyyyyyy when
                                                M<Faaaaaaaaaa, IReadOnlyList<Tttttttttttttttttttttttttttttttttttttt>>() =>
                                                (from item in items where "qqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqq" select item),
                                            _ => 0
                                        };
                                    }
                                }
                            }
                        }
                    }
                }
                """
            ),
            "kb3:c2_K_s32_a106_w79" => (
                """
                class C {
                    class D0 {
                        class D1 {
                            class D2 {
                                class D3 {
                                    object M() {
                                        return state switch {
                                            Xyyyyyyy when
                                                M<Faaaaaaaaaa, IReadOnlyList<Tttttttttttttttttttttttttttttttttttttt>>() => (from item in items where "qqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqq" select item),
                                            _ => 0
                                        };
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
                                    object M() {
                                        return state switch {
                                            Xyyyyyyy when
                                                M<Faaaaaaaaaa, IReadOnlyList<Tttttttttttttttttttttttttttttttttttttt>>() => (from item in
                                                        items
                                                    where "qqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqq"
                                                    select item),
                                            _ => 0
                                        };
                                    }
                                }
                            }
                        }
                    }
                }
                """
            ),
            "kb2:c2_K_s28_a109_w128" => (
                """
                class C {
                    class D0 {
                        class D1 {
                            class D2 {
                                object M() {
                                    return state switch {
                                        Xyyyyyyy when
                                            M<Faaaaaaaaaa, IReadOnlyList<Ttttttttttttttttttttttttttttttttttttttttttttt>>() => (from item in items where "qqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqq" select item),
                                        _ => 0
                                    };
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
                                object M() {
                                    return state switch {
                                        Xyyyyyyy when
                                            M<Faaaaaaaaaa, IReadOnlyList<Ttttttttttttttttttttttttttttttttttttttttttttt>>() =>
                                            (from item in items
                                                where
                                                    "qqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqq"
                                                select item),
                                        _ => 0
                                    };
                                }
                            }
                        }
                    }
                }
                """
            ),
            "kb4:c2_K_s28_a109_w132" => (
                """
                class C {
                    class D0 {
                        class D1 {
                            class D2 {
                                object M() {
                                    return state switch {
                                        Xyyyyyyy when
                                            M<Faaaaaaaaaa, IReadOnlyList<Ttttttttttttttttttttttttttttttttttttttttttttt>>() => (from item in items where "qqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqq" select item),
                                        _ => 0
                                    };
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
                                object M() {
                                    return state switch {
                                        Xyyyyyyy when
                                            M<Faaaaaaaaaa, IReadOnlyList<Ttttttttttttttttttttttttttttttttttttttttttttt>>() => (from item
                                                    in items
                                                where
                                                    "qqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqq"
                                                select item),
                                        _ => 0
                                    };
                                }
                            }
                        }
                    }
                }
                """
            ),
            "f576a:r0362_i20_q_w109_F" => (
                """
                class C {
                    class D0 {
                        class D1 {
                            object M() {
                                return state switch { not null when M<Bbbbbbbb, IReadOnlyList<Tttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttt>>() => (from item in items where "qqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqq" select item), _ => 0 };
                            }
                        }
                    }
                }
                """,
                """
                class C {
                    class D0 {
                        class D1 {
                            object M() {
                                return state switch {
                                    not null when
                                        M<Bbbbbbbb, IReadOnlyList<
                                            Tttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttt>>() => (from item
                                                in items
                                            where "qqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqq"
                                            select item),
                                    _ => 0
                                };
                            }
                        }
                    }
                }
                """
            ),
            "f576g:r0315_i16_q_w91_F" => (
                """
                class C {
                    class D0 {
                        object M() {
                            return state switch { string s when Make<List<bool>, IReadOnlyList<Tttttttttttttttttttttttttttttttttttttttt>>() => (from item in items where "qqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqq" select item), _ => 0 };
                        }
                    }
                }
                """,
                """
                class C {
                    class D0 {
                        object M() {
                            return state switch {
                                string s when Make<List<bool>, IReadOnlyList<Tttttttttttttttttttttttttttttttttttttttt>>() => (from item
                                        in items
                                    where "qqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqq"
                                    select item),
                                _ => 0
                            };
                        }
                    }
                }
                """
            ),
            "coordQ:r0451_i20_q_w89_F" => (
                """
                class C {
                    class D0 {
                        class D1 {
                            object M() {
                                return state switch { _ when Resolve<(int, string), IReadOnlyList<Ttttttttttttttttttttttttttttttttttttt>>() => (from item in items where "qqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqq" select item), _ => 0 };
                            }
                        }
                    }
                }
                """,
                """
                class C {
                    class D0 {
                        class D1 {
                            object M() {
                                return state switch {
                                    _ when Resolve<(int, string), IReadOnlyList<Ttttttttttttttttttttttttttttttttttttt>>() => (from item
                                            in items
                                        where "qqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqq"
                                        select item),
                                    _ => 0
                                };
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

    /// <summary>Beside a <c>when</c>, a condition ending three columns past the margin breaks at its outer comma.</summary>
    [Theory]
    [InlineData("f576d:r0105_i16_q_w20_K")]
    [InlineData("f576i:r0923_i16_c2_w38_K")]
    [InlineData("f576l:r0041_i24_atom_w65_K")]
    public void TheConditionThreeColumnsOverBreaksAtItsComma(string cell) {
        var (source, expected) = cell switch {
            "f576d:r0105_i16_q_w20_K" => (
                """
                class C {
                    class D0 {
                        object M() {
                            return state switch {
                                X when Pick<A, IReadOnlyList<Tttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttt>>() => (from item in items where "q" select item),
                                _ => 0
                            };
                        }
                    }
                }
                """,
                """
                class C {
                    class D0 {
                        object M() {
                            return state switch {
                                X when Pick<A,
                                        IReadOnlyList<Tttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttt>>() =>
                                    (from item in items where "q" select item),
                                _ => 0
                            };
                        }
                    }
                }
                """
            ),
            "f576i:r0923_i16_c2_w38_K" => (
                """
                class C {
                    class D0 {
                        object M() {
                            return state switch {
                                _ when M<int, IReadOnlyList<Ttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttt>>() =>
                                Compute("kkkkkkkkkkkkkkkkkk", 2, flag),
                                _ => 0
                            };
                        }
                    }
                }
                """,
                """
                class C {
                    class D0 {
                        object M() {
                            return state switch {
                                _ when M<int,
                                        IReadOnlyList<Ttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttt>>() =>
                                    Compute("kkkkkkkkkkkkkkkkkk", 2, flag),
                                _ => 0
                            };
                        }
                    }
                }
                """
            ),
            "f576l:r0041_i24_atom_w65_K" => (
                """
                class C {
                    class D0 {
                        class D1 {
                            class D2 {
                                object M() {
                                    return state switch {
                                        > 7 when Of<A, IReadOnlyList<Tttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttt>>() =>
                                Short.Value12,
                                        _ => 0
                                    };
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
                                object M() {
                                    return state switch {
                                        > 7 when Of<A,
                                                IReadOnlyList<Tttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttt>>() =>
                                            Short.Value12,
                                        _ => 0
                                    };
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

    /// <summary>A query whose <c>)</c> lands one column past the margin beside the arrow moves below.</summary>
    [Theory]
    [InlineData("kb5:c1_K_s12_a70_w50")]
    [InlineData("kb5:c2_K_s20_a60_w60")]
    [InlineData("kb5:c2_K_s20_a60_w58")]
    [InlineData("f576e:r0422_i12_q_w22_F")]
    public void AQueryOneColumnOverMovesBelow(string cell) {
        var (source, expected) = cell switch {
            "kb5:c1_K_s12_a70_w50" => (
                """
                class C {
                    object M() {
                        return state switch { Xyyy when M<Faaaaaaaaaa, IReadOnlyList<Tttttttttttt>>() => (from item in items where "qqqqqqqqq" select item), _ => 0 };
                    }
                }
                """,
                """
                class C {
                    object M() {
                        return state switch {
                            Xyyy when M<Faaaaaaaaaa, IReadOnlyList<Tttttttttttt>>() =>
                                (from item in items where "qqqqqqqqq" select item),
                            _ => 0
                        };
                    }
                }
                """
            ),
            "kb5:c2_K_s20_a60_w60" => (
                """
                class C {
                    class D0 {
                        object M() {
                            return state switch {
                                Xyyyyyyy when
                                    M<Faaaaaaaaaa, IReadOnlyList<Tttt>>() => (from item in items where "qqqqqqqqqqqqqqqqqqq" select item),
                                _ => 0
                            };
                        }
                    }
                }
                """,
                """
                class C {
                    class D0 {
                        object M() {
                            return state switch {
                                Xyyyyyyy when
                                    M<Faaaaaaaaaa, IReadOnlyList<Tttt>>() =>
                                    (from item in items where "qqqqqqqqqqqqqqqqqqq" select item),
                                _ => 0
                            };
                        }
                    }
                }
                """
            ),
            "kb5:c2_K_s20_a60_w58" => (
                """
                class C {
                    class D0 {
                        object M() {
                            return state switch {
                                Xyyyyyyy when
                                    M<Faaaaaaaaaa, IReadOnlyList<Tttt>>() => (from item in items where "qqqqqqqqqqqqqqqqq" select item),
                                _ => 0
                            };
                        }
                    }
                }
                """,
                """
                class C {
                    class D0 {
                        object M() {
                            return state switch {
                                Xyyyyyyy when
                                    M<Faaaaaaaaaa, IReadOnlyList<Tttt>>() => (from item in items where "qqqqqqqqqqqqqqqqq" select item),
                                _ => 0
                            };
                        }
                    }
                }
                """
            ),
            "f576e:r0422_i12_q_w22_F" => (
                """
                class C {
                    object M() {
                        return state switch { { Kind: not null } when Materialise<IEnumerable<KeyValuePair<int, string>>, IReadOnlyList<Ttttttttttttttttttttttttttttttttttttt>>() => (from item in items where "q" select item), _ => 0 };
                    }
                }
                """,
                """
                class C {
                    object M() {
                        return state switch {
                            { Kind: not null } when
                                Materialise<IEnumerable<KeyValuePair<int, string>>,
                                    IReadOnlyList<Ttttttttttttttttttttttttttttttttttttt>>() =>
                                (from item in items where "q" select item),
                            _ => 0
                        };
                    }
                }
                """
            ),
            _ => throw new ArgumentOutOfRangeException(nameof(cell))
        };

        Agrees(source, expected);
    }
}
