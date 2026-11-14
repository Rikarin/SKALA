using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Core.Configuration;

namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     Issue #576, round fourteen (SK-DIV-0399). A parenthesised query after an arm's arrow whose <c>(from item</c> does
///     not fit beside it keeps <c>=&gt; (</c> and moves the query below by two regimes of the arrow's end and line start
///     (<c>Fitter.ArmParenBreaks</c>); an atom body's <c>when</c> behind a head of twelve or more follows three regimes,
///     the third where the fill beside the <c>when</c> takes the inner <c>&lt;</c> (<c>Fitter.AtomWhenBreaks</c>); and a
///     wide body's <c>when</c> asks which point the fill would take by the outer list's own rule
///     (<c>Fitter.OuterCommaTakesTheFill</c>). Cells from the structure grids p2, a5, a6, e4 and the fresh probes. Every
///     expected string is jb cleanupcode 2025.2.6's own output for the input.
/// </summary>
public sealed class ArmParenAndAtomWhenIssue576Tests {
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
    ///     The arrow stays, <c>(</c> ends its line and the query moves a level past the arm, its clauses on <c>from</c>'s
    ///     column.
    /// </summary>
    [Theory]
    [InlineData("p2:i12_a110_w95")]
    [InlineData("p2:i16_a110_w92")]
    [InlineData("p2:i12_a116_w123")]
    [InlineData("p2:i16_a117_w130")]
    [InlineData("f576c:r0709_i12_q_w128_F")]
    [InlineData("f576f:r0062_i12_q_w97_F")]
    [InlineData("f576j:r0458_i16_q_w130_F")]
    public void TheQueryMovesBelowAnOpenParenthesis(string cell) {
        var (source, expected) = cell switch {
            "p2:i12_a110_w95" => (
                """
                class C {
                    object M() {
                        return state switch { Xyyy when M<Faaaaa, IReadOnlyList<Ttttttttttttttttttttttttttttttttttttttttttttttttttttttttt>>() => (from item in items where "qqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqq" select item), _ => 0 };
                    }
                }
                """,
                """
                class C {
                    object M() {
                        return state switch {
                            Xyyy when M<Faaaaa, IReadOnlyList<Ttttttttttttttttttttttttttttttttttttttttttttttttttttttttt>>() => (
                                from item in items where "qqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqq" select item),
                            _ => 0
                        };
                    }
                }
                """
            ),
            "p2:i16_a110_w92" => (
                """
                class C {
                    class D0 {
                        object M() {
                            return state switch { Xyyy when M<Faaaaa, IReadOnlyList<Ttttttttttttttttttttttttttttttttttttttttttttttttttttt>>() => (from item in items where "qqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqq" select item), _ => 0 };
                        }
                    }
                }
                """,
                """
                class C {
                    class D0 {
                        object M() {
                            return state switch {
                                Xyyy when M<Faaaaa, IReadOnlyList<Ttttttttttttttttttttttttttttttttttttttttttttttttttttt>>() => (
                                    from item in items where "qqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqq" select item),
                                _ => 0
                            };
                        }
                    }
                }
                """
            ),
            "p2:i12_a116_w123" => (
                """
                class C {
                    object M() {
                        return state switch { Xyyy when M<Faaaaa, IReadOnlyList<Ttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttt>>() => (from item in items where "qqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqq" select item), _ => 0 };
                    }
                }
                """,
                """
                class C {
                    object M() {
                        return state switch {
                            Xyyy when M<Faaaaa, IReadOnlyList<Ttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttt>>() => (
                                from item in items
                                where "qqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqq"
                                select item),
                            _ => 0
                        };
                    }
                }
                """
            ),
            "p2:i16_a117_w130" => (
                """
                class C {
                    class D0 {
                        object M() {
                            return state switch { Xyyy when M<Faaaaa, IReadOnlyList<Tttttttttttttttttttttttttttttttttttttttttttttttttttttttttttt>>() => (from item in items where "qqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqq" select item), _ => 0 };
                        }
                    }
                }
                """,
                """
                class C {
                    class D0 {
                        object M() {
                            return state switch {
                                Xyyy when M<Faaaaa, IReadOnlyList<Tttttttttttttttttttttttttttttttttttttttttttttttttttttttttttt>>() => (
                                    from item in items
                                    where "qqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqq"
                                    select item),
                                _ => 0
                            };
                        }
                    }
                }
                """
            ),
            "f576c:r0709_i12_q_w128_F" => (
                """
                class C {
                    object M() {
                        return state switch { _ when Pick<A, IReadOnlyList<Tttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttt>>() => (from item in items where "qqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqq" select item), _ => 0 };
                    }
                }
                """,
                """
                class C {
                    object M() {
                        return state switch {
                            _ when Pick<A, IReadOnlyList<Tttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttt>>() => (
                                from item in items
                                where "qqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqq"
                                select item),
                            _ => 0
                        };
                    }
                }
                """
            ),
            "f576f:r0062_i12_q_w97_F" => (
                """
                class C {
                    object M() {
                        return state switch { _ when Convert<HashSet<long>, IReadOnlyList<Tttttttttttttttttttttttttttttttttttttttttttttttttttt>>() => (from item in items where "qqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqq" select item), _ => 0 };
                    }
                }
                """,
                """
                class C {
                    object M() {
                        return state switch {
                            _ when Convert<HashSet<long>, IReadOnlyList<Tttttttttttttttttttttttttttttttttttttttttttttttttttt>>() => (
                                from item in items where "qqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqq" select item),
                            _ => 0
                        };
                    }
                }
                """
            ),
            "f576j:r0458_i16_q_w130_F" => (
                """
                class C {
                    class D0 {
                        object M() {
                            return state switch { > 7 when Of<IEnumerable<KeyValuePair<int, string>>, IReadOnlyList<Ttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttt>>() => (from item in items where "qqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqq" select item), _ => 0 };
                        }
                    }
                }
                """,
                """
                class C {
                    class D0 {
                        object M() {
                            return state switch {
                                > 7 when Of<IEnumerable<KeyValuePair<int, string>>, IReadOnlyList<
                                    Ttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttt>>() => (
                                    from item in items
                                    where "qqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqq"
                                    select item),
                                _ => 0
                            };
                        }
                    }
                }
                """
            ),
            _ => throw new ArgumentOutOfRangeException(nameof(cell))
        };

        Agrees(source, expected);
    }

    /// <summary>Short of either bar, outside the first bar's region, or where <c>=&gt; (from</c> keeps the body, as before.</summary>
    [Theory]
    [InlineData("p2:i12_a110_w94")]
    [InlineData("p2:i16_a112_w93")]
    [InlineData("p2:i12_a116_w122")]
    [InlineData("p2:i12_a110_w98")]
    public void TheArrowOrTheFromClauseTakesTheBreak(string cell) {
        var (source, expected) = cell switch {
            "p2:i12_a110_w94" => (
                """
                class C {
                    object M() {
                        return state switch { Xyyy when M<Faaaaa, IReadOnlyList<Ttttttttttttttttttttttttttttttttttttttttttttttttttttttttt>>() => (from item in items where "qqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqq" select item), _ => 0 };
                    }
                }
                """,
                """
                class C {
                    object M() {
                        return state switch {
                            Xyyy when M<Faaaaa, IReadOnlyList<Ttttttttttttttttttttttttttttttttttttttttttttttttttttttttt>>() =>
                                (from item in items where "qqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqq" select item),
                            _ => 0
                        };
                    }
                }
                """
            ),
            "p2:i16_a112_w93" => (
                """
                class C {
                    class D0 {
                        object M() {
                            return state switch { Xyyy when M<Faaaaa, IReadOnlyList<Ttttttttttttttttttttttttttttttttttttttttttttttttttttttt>>() => (from item in items where "qqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqq" select item), _ => 0 };
                        }
                    }
                }
                """,
                """
                class C {
                    class D0 {
                        object M() {
                            return state switch {
                                Xyyy when M<Faaaaa, IReadOnlyList<Ttttttttttttttttttttttttttttttttttttttttttttttttttttttt>>() =>
                                    (from item in items where "qqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqq" select item),
                                _ => 0
                            };
                        }
                    }
                }
                """
            ),
            "p2:i12_a116_w122" => (
                """
                class C {
                    object M() {
                        return state switch { Xyyy when M<Faaaaa, IReadOnlyList<Ttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttt>>() => (from item in items where "qqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqq" select item), _ => 0 };
                    }
                }
                """,
                """
                class C {
                    object M() {
                        return state switch {
                            Xyyy when M<Faaaaa, IReadOnlyList<Ttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttt>>() =>
                                (from item in items
                                    where "qqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqq"
                                    select item),
                            _ => 0
                        };
                    }
                }
                """
            ),
            "p2:i12_a110_w98" => (
                """
                class C {
                    object M() {
                        return state switch { Xyyy when M<Faaaaa, IReadOnlyList<Ttttttttttttttttttttttttttttttttttttttttttttttttttttttttt>>() => (from item in items where "qqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqq" select item), _ => 0 };
                    }
                }
                """,
                """
                class C {
                    object M() {
                        return state switch {
                            Xyyy when M<Faaaaa, IReadOnlyList<Ttttttttttttttttttttttttttttttttttttttttttttttttttttttttt>>() => (from
                                    item in items
                                where "qqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqq"
                                select item),
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

    /// <summary>
    ///     An atom body behind a head of twelve or more: the condition moves below, fills at the outer comma or fills at
    ///     the inner <c>&lt;</c> by its three regimes.
    /// </summary>
    [Theory]
    [InlineData("a6:i12_x164_h18_b30_b6")]
    [InlineData("a6:i12_x164_h18_b18_b6")]
    [InlineData("a6:i12_x164_h18_b40_b6")]
    [InlineData("a6:i12_x152_h12_b20_b2")]
    [InlineData("a6:i12_x152_h12_b40_b2")]
    [InlineData("a5:i12_x158_h23_b22_b6")]
    [InlineData("a5:i20_x158_h23_b22_b6")]
    [InlineData("a6:i12_x164_h12_b36_b14")]
    [InlineData("f576b:r0244_i16_atom_w75_F")]
    [InlineData("f576b:r0676_i20_atom_w42_K")]
    public void TheAtomWhenFollowsItsThreeRegimes(string cell) {
        var (source, expected) = cell switch {
            "a6:i12_x164_h18_b30_b6" => (
                """
                class C {
                    object M() {
                        return state switch { Xyyyyyyyyyyyy when M<Faaaaaaaaaaaaaaaaaaaaaaaaaa, IReadOnlyList<Tttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttt>>() => Value, _ => 0 };
                    }
                }
                """,
                """
                class C {
                    object M() {
                        return state switch {
                            Xyyyyyyyyyyyy when M<Faaaaaaaaaaaaaaaaaaaaaaaaaa, IReadOnlyList<
                                Tttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttt>>() => Value,
                            _ => 0
                        };
                    }
                }
                """
            ),
            "a6:i12_x164_h18_b18_b6" => (
                """
                class C {
                    object M() {
                        return state switch { Xyyyyyyyyyyyy when M<Faaaaaaaaaaaaaa, IReadOnlyList<Tttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttt>>() => Value, _ => 0 };
                    }
                }
                """,
                """
                class C {
                    object M() {
                        return state switch {
                            Xyyyyyyyyyyyy when
                                M<Faaaaaaaaaaaaaa, IReadOnlyList<
                                    Tttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttt>>()
                                => Value,
                            _ => 0
                        };
                    }
                }
                """
            ),
            "a6:i12_x164_h18_b40_b6" => (
                """
                class C {
                    object M() {
                        return state switch { Xyyyyyyyyyyyy when M<Faaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa, IReadOnlyList<Tttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttt>>() => Value, _ => 0 };
                    }
                }
                """,
                """
                class C {
                    object M() {
                        return state switch {
                            Xyyyyyyyyyyyy when
                                M<Faaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa, IReadOnlyList<
                                    Tttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttt>>() => Value,
                            _ => 0
                        };
                    }
                }
                """
            ),
            "a6:i12_x152_h12_b20_b2" => (
                """
                class C {
                    object M() {
                        return state switch { Xyyyyyy when M<Faaaaaaaaaaaaaaaa, IReadOnlyList<Tttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttt>>() => 1, _ => 0 };
                    }
                }
                """,
                """
                class C {
                    object M() {
                        return state switch {
                            Xyyyyyy when M<Faaaaaaaaaaaaaaaa, IReadOnlyList<
                                Tttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttt>>() => 1,
                            _ => 0
                        };
                    }
                }
                """
            ),
            "a6:i12_x152_h12_b40_b2" => (
                """
                class C {
                    object M() {
                        return state switch { Xyyyyyy when M<Faaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa, IReadOnlyList<Tttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttt>>() => 1, _ => 0 };
                    }
                }
                """,
                """
                class C {
                    object M() {
                        return state switch {
                            Xyyyyyy when M<Faaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa,
                                IReadOnlyList<Tttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttt>>() => 1,
                            _ => 0
                        };
                    }
                }
                """
            ),
            "a5:i12_x158_h23_b22_b6" => (
                """
                class C {
                    object M() {
                        return state switch { Xyyyyyyyyyyyyyyyyy when M<Faaaaaaaaaaaaaaaaaa, IReadOnlyList<Ttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttt>>() => Value, _ => 0 };
                    }
                }
                """,
                """
                class C {
                    object M() {
                        return state switch {
                            Xyyyyyyyyyyyyyyyyy when M<Faaaaaaaaaaaaaaaaaa, IReadOnlyList<
                                Ttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttt>>() => Value,
                            _ => 0
                        };
                    }
                }
                """
            ),
            "a5:i20_x158_h23_b22_b6" => (
                """
                class C {
                    class D0 {
                        class D1 {
                            object M() {
                                return state switch { Xyyyyyyyyyyyyyyyyy when M<Faaaaaaaaaaaaaaaaaa, IReadOnlyList<Ttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttt>>() => Value, _ => 0 };
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
                                    Xyyyyyyyyyyyyyyyyy when M<Faaaaaaaaaaaaaaaaaa, IReadOnlyList<
                                        Ttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttt>>() => Value,
                                    _ => 0
                                };
                            }
                        }
                    }
                }
                """
            ),
            "a6:i12_x164_h12_b36_b14" => (
                """
                class C {
                    object M() {
                        return state switch { Xyyyyyy when M<Faaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa, IReadOnlyList<Tttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttt>>() => Abcdefghijklm, _ => 0 };
                    }
                }
                """,
                """
                class C {
                    object M() {
                        return state switch {
                            Xyyyyyy when
                                M<Faaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa, IReadOnlyList<
                                    Tttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttt>>() =>
                                Abcdefghijklm,
                            _ => 0
                        };
                    }
                }
                """
            ),
            "f576b:r0244_i16_atom_w75_F" => (
                """
                class C {
                    class D0 {
                        object M() {
                            return state switch { Tyyyyyyyyyyyy when Of<List<bool>, IReadOnlyList<Ttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttt>>() => Value, _ => 0 };
                        }
                    }
                }
                """,
                """
                class C {
                    class D0 {
                        object M() {
                            return state switch {
                                Tyyyyyyyyyyyy when
                                    Of<List<bool>, IReadOnlyList<
                                        Ttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttt>>() => Value,
                                _ => 0
                            };
                        }
                    }
                }
                """
            ),
            "f576b:r0676_i20_atom_w42_K" => (
                """
                class C {
                    class D0 {
                        class D1 {
                            object M() {
                                return state switch {
                                    not null when Resolve<(int, string), IReadOnlyList<Tttttttttttttttttttttttttttttttttttttttttttttttttt>>() => "ss",
                                    _ => 0
                                };
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
                                        Resolve<(int, string), IReadOnlyList<Tttttttttttttttttttttttttttttttttttttttttttttttttt>>() =>
                                        "ss",
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

    /// <summary>
    ///     A wide body behind a head under twelve, where the outer list gives its break to the inner <c>&lt;</c>: the
    ///     inner bar, with its margin.
    /// </summary>
    [Theory]
    [InlineData("e4:i12_h7_b38_c134_c0_w124")]
    [InlineData("e4:i12_h7_b38_c134_c0_w130")]
    [InlineData("e4:i12_h9_b38_c134_c0_w76")]
    [InlineData("f576e:r0720_i12_c2_w109_K")]
    [InlineData("coordQ:r0774_i12_c2_w89_F")]
    [InlineData("f576i:r0404_i12_q_w78_K")]
    public void TheWhenAsksTheListsOwnFill(string cell) {
        var (source, expected) = cell switch {
            "e4:i12_h7_b38_c134_c0_w124" => (
                """
                class C {
                    object M() {
                        return state switch { Xy when M<Faaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa, IReadOnlyList<Ttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttt>>() => Handle(value, "ccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc"), _ => 0 };
                    }
                }
                """,
                """
                class C {
                    object M() {
                        return state switch {
                            Xy when M<Faaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa, IReadOnlyList<
                                Ttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttt>>() => Handle(
                                value,
                                "ccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc"
                            ),
                            _ => 0
                        };
                    }
                }
                """
            ),
            "e4:i12_h7_b38_c134_c0_w130" => (
                """
                class C {
                    object M() {
                        return state switch { Xy when M<Faaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa, IReadOnlyList<Ttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttt>>() => Handle(value, "ccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc"), _ => 0 };
                    }
                }
                """,
                """
                class C {
                    object M() {
                        return state switch {
                            Xy when
                                M<Faaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa, IReadOnlyList<
                                    Ttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttt>>() => Handle(
                                    value,
                                    "ccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc"
                                ),
                            _ => 0
                        };
                    }
                }
                """
            ),
            "e4:i12_h9_b38_c134_c0_w76" => (
                """
                class C {
                    object M() {
                        return state switch { Xyyy when M<Faaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa, IReadOnlyList<Ttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttt>>() => Handle(value, "ccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc"), _ => 0 };
                    }
                }
                """,
                """
                class C {
                    object M() {
                        return state switch {
                            Xyyy when M<Faaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa, IReadOnlyList<
                                Ttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttt>>() => Handle(
                                value,
                                "ccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc"
                            ),
                            _ => 0
                        };
                    }
                }
                """
            ),
            "f576e:r0720_i12_c2_w109_K" => (
                """
                class C {
                    object M() {
                        return state switch {
                            Xy when Resolve<IReadOnlyCollection<Guid>, IReadOnlyList<Tttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttt>>() => Compute("kkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkk", 2, flag),
                            _ => 0
                        };
                    }
                }
                """,
                """
                class C {
                    object M() {
                        return state switch {
                            Xy when Resolve<IReadOnlyCollection<Guid>, IReadOnlyList<
                                Tttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttt>>() => Compute(
                                "kkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkk",
                                2,
                                flag
                            ),
                            _ => 0
                        };
                    }
                }
                """
            ),
            "coordQ:r0774_i12_c2_w89_F" => (
                """
                class C {
                    object M() {
                        return state switch { > 7 when Materialise<Dictionary<int, string>, IReadOnlyList<Tttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttt>>() => Compute("kkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkk", 2, flag), _ => 0 };
                    }
                }
                """,
                """
                class C {
                    object M() {
                        return state switch {
                            > 7 when Materialise<Dictionary<int, string>, IReadOnlyList<
                                Tttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttt>>() => Compute(
                                "kkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkk",
                                2,
                                flag
                            ),
                            _ => 0
                        };
                    }
                }
                """
            ),
            "f576i:r0404_i12_q_w78_K" => (
                """
                class C {
                    object M() {
                        return state switch {
                            null when Materialise<IReadOnlyCollection<Guid>, IReadOnlyList<Ttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttt>>() => (from item in items where "qqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqq" select item),
                            _ => 0
                        };
                    }
                }
                """,
                """
                class C {
                    object M() {
                        return state switch {
                            null when Materialise<IReadOnlyCollection<Guid>, IReadOnlyList<
                                    Ttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttt>>() =>
                                (from item in items where "qqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqq" select item),
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
