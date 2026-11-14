using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Core.Configuration;

namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     Issue #576, round thirteen (SK-DIV-0399). An object creation after an arm's arrow: once <c>new Holder(</c> no
///     longer
///     fits beside the arrow, the oracle keeps the arrow flat and breaks after <c>new</c> by a measured rule on the body's
///     width, the arrow's end and its line's start (<c>Fitter.ArmNewStays</c>), the type arguments then unlifted; and a
///     parenthesised query whose <c>(from item</c> no longer fits there keeps <c>(from</c> by the same two regimes
///     (<c>Fitter.ArmQueryStays</c>). Cells from the structure grids n1–n4, p2 and the fresh probes. Every expected string
///     is jb cleanupcode 2025.2.6's own output for the input.
/// </summary>
public sealed class ArmNewIssue576Tests {
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

    /// <summary>The arrow stays and the creation breaks after its <c>new</c>.</summary>
    [Theory]
    [InlineData("n1:i12_a110_w94")]
    [InlineData("n1:i20_a109_w88")]
    [InlineData("n3:i20_a112_w127")]
    [InlineData("n4:i16_a112_w100_GEN")]
    [InlineData("n4:i24_a115_w130_LONG")]
    [InlineData("f576j:r0362_i12_c1_w115_F")]
    [InlineData("f576e:r0675_i16_c1_w89_F")]
    public void TheCreationBreaksAfterItsNew(string cell) {
        var (source, expected) = cell switch {
            "n1:i12_a110_w94" => (
                """
                class C {
                    object M() {
                        return state switch { Xyyy when M<Faaaaa, IReadOnlyList<Ttttttttttttttttttttttttttttttttttttttttttttttttttttttttt>>() => new Holder(1, "nnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnn"), _ => 0 };
                    }
                }
                """,
                """
                class C {
                    object M() {
                        return state switch {
                            Xyyy when M<Faaaaa, IReadOnlyList<Ttttttttttttttttttttttttttttttttttttttttttttttttttttttttt>>() => new
                                Holder(1, "nnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnn"),
                            _ => 0
                        };
                    }
                }
                """
            ),
            "n1:i20_a109_w88" => (
                """
                class C {
                    class D0 {
                        class D1 {
                            object M() {
                                return state switch { Xyyy when M<Faaaaa, IReadOnlyList<Tttttttttttttttttttttttttttttttttttttttttttttttt>>() => new Holder(1, "nnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnn"), _ => 0 };
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
                                    Xyyy when M<Faaaaa, IReadOnlyList<Tttttttttttttttttttttttttttttttttttttttttttttttt>>() => new
                                        Holder(1, "nnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnn"),
                                    _ => 0
                                };
                            }
                        }
                    }
                }
                """
            ),
            "n3:i20_a112_w127" => (
                """
                class C {
                    class D0 {
                        class D1 {
                            object M() {
                                return state switch { Xyyy when M<Faaaaa, IReadOnlyList<Ttttttttttttttttttttttttttttttttttttttttttttttttttt>>() => new Holder(1, "nnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnn"), _ => 0 };
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
                                    Xyyy when M<Faaaaa, IReadOnlyList<Ttttttttttttttttttttttttttttttttttttttttttttttttttt>>() => new
                                        Holder(
                                            1,
                                            "nnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnn"
                                        ),
                                    _ => 0
                                };
                            }
                        }
                    }
                }
                """
            ),
            "n4:i16_a112_w100_GEN" => (
                """
                class C {
                    class D0 {
                        object M() {
                            return state switch { Xyyy when M<Faaaaa, IReadOnlyList<Ttttttttttttttttttttttttttttttttttttttttttttttttttttttt>>() => new Holder<int>(1, "nnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnn"), _ => 0 };
                        }
                    }
                }
                """,
                """
                class C {
                    class D0 {
                        object M() {
                            return state switch {
                                Xyyy when M<Faaaaa, IReadOnlyList<Ttttttttttttttttttttttttttttttttttttttttttttttttttttttt>>() => new
                                    Holder<int>(1, "nnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnn"),
                                _ => 0
                            };
                        }
                    }
                }
                """
            ),
            "n4:i24_a115_w130_LONG" => (
                """
                class C {
                    class D0 {
                        class D1 {
                            class D2 {
                                object M() {
                                    return state switch { Xyyy when M<Faaaaa, IReadOnlyList<Tttttttttttttttttttttttttttttttttttttttttttttttttt>>() => new HolderOfThings123(1, "nnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnn"), _ => 0 };
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
                                        Xyyy when M<Faaaaa, IReadOnlyList<Tttttttttttttttttttttttttttttttttttttttttttttttttt>>() => new
                                            HolderOfThings123(
                                                1,
                                                "nnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnn"
                                            ),
                                        _ => 0
                                    };
                                }
                            }
                        }
                    }
                }
                """
            ),
            "f576j:r0362_i12_c1_w115_F" => (
                """
                class C {
                    object M() {
                        return state switch { Tyyyyyyyyyyyy when Of<int, IReadOnlyList<Tttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttt>>() => new Holder(1, "nnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnn"), _ => 0 };
                    }
                }
                """,
                """
                class C {
                    object M() {
                        return state switch {
                            Tyyyyyyyyyyyy when
                                Of<int, IReadOnlyList<
                                    Tttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttt>>() => new
                                    Holder(
                                        1,
                                        "nnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnn"
                                    ),
                            _ => 0
                        };
                    }
                }
                """
            ),
            "f576e:r0675_i16_c1_w89_F" => (
                """
                class C {
                    class D0 {
                        object M() {
                            return state switch { Ab when M<(int, string), IReadOnlyList<Tttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttt>>() => new Holder(1, "nnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnn"), _ => 0 };
                        }
                    }
                }
                """,
                """
                class C {
                    class D0 {
                        object M() {
                            return state switch {
                                Ab when M<(int, string),
                                    IReadOnlyList<Tttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttt>>() => new
                                    Holder(1, "nnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnn"),
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

    /// <summary>
    ///     Short of either bar, outside the first bar's region, or where <c>new Holder(</c> fits beside the arrow, the
    ///     arrow breaks or the arguments chop, as before.
    /// </summary>
    [Theory]
    [InlineData("n1:i12_a110_w93")]
    [InlineData("n3:i20_a112_w126")]
    [InlineData("n1:i12_a107_w100")]
    [InlineData("n2:i16_a115_w95_h4")]
    public void TheArrowOrTheArgumentsTakeTheBreak(string cell) {
        var (source, expected) = cell switch {
            "n1:i12_a110_w93" => (
                """
                class C {
                    object M() {
                        return state switch { Xyyy when M<Faaaaa, IReadOnlyList<Ttttttttttttttttttttttttttttttttttttttttttttttttttttttttt>>() => new Holder(1, "nnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnn"), _ => 0 };
                    }
                }
                """,
                """
                class C {
                    object M() {
                        return state switch {
                            Xyyy when M<Faaaaa, IReadOnlyList<Ttttttttttttttttttttttttttttttttttttttttttttttttttttttttt>>() =>
                                new Holder(1, "nnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnn"),
                            _ => 0
                        };
                    }
                }
                """
            ),
            "n3:i20_a112_w126" => (
                """
                class C {
                    class D0 {
                        class D1 {
                            object M() {
                                return state switch { Xyyy when M<Faaaaa, IReadOnlyList<Ttttttttttttttttttttttttttttttttttttttttttttttttttt>>() => new Holder(1, "nnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnn"), _ => 0 };
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
                                    Xyyy when M<Faaaaa, IReadOnlyList<Ttttttttttttttttttttttttttttttttttttttttttttttttttt>>() =>
                                        new Holder(
                                            1,
                                            "nnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnn"
                                        ),
                                    _ => 0
                                };
                            }
                        }
                    }
                }
                """
            ),
            "n1:i12_a107_w100" => (
                """
                class C {
                    object M() {
                        return state switch { Xyyy when M<Faaaaa, IReadOnlyList<Tttttttttttttttttttttttttttttttttttttttttttttttttttttt>>() => new Holder(1, "nnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnn"), _ => 0 };
                    }
                }
                """,
                """
                class C {
                    object M() {
                        return state switch {
                            Xyyy when M<Faaaaa, IReadOnlyList<Tttttttttttttttttttttttttttttttttttttttttttttttttttttt>>() => new Holder(
                                1,
                                "nnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnn"
                            ),
                            _ => 0
                        };
                    }
                }
                """
            ),
            "n2:i16_a115_w95_h4" => (
                """
                class C {
                    class D0 {
                        object M() {
                            return state switch { Xyyy when M<Faaaaa, IReadOnlyList<Tttttttttttttttttttttttttttttttttttttttttttttttttttttttttt>>() => new Holder(1, "nnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnn"), _ => 0 };
                        }
                    }
                }
                """,
                """
                class C {
                    class D0 {
                        object M() {
                            return state switch {
                                Xyyy when M<Faaaaa, IReadOnlyList<Tttttttttttttttttttttttttttttttttttttttttttttttttttttttttt>>() =>
                                    new Holder(1, "nnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnn"),
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

    /// <summary>
    ///     A parenthesised query where <c>(from item</c> does not fit beside the arrow but <c>(from</c> does: the oracle keeps
    ///     the body, <c>=&gt; (from</c> / <c>item in items</c>, by the creation's two regimes on the query's own constants.
    /// </summary>
    [Theory]
    [InlineData("p2:i12_a112_w98")]
    [InlineData("p2:i16_a112_w94")]
    [InlineData("p2:i20_a112_w128")]
    [InlineData("p2:i24_a110_w132")]
    [InlineData("f576e:r0317_i12_q_w104_F")]
    [InlineData("f576b:r0579_i12_q_w128_F")]
    public void TheQueryKeepsItsFromBesideTheArrow(string cell) {
        var (source, expected) = cell switch {
            "p2:i12_a112_w98" => (
                """
                class C {
                    object M() {
                        return state switch { Xyyy when M<Faaaaa, IReadOnlyList<Ttttttttttttttttttttttttttttttttttttttttttttttttttttttttttt>>() => (from item in items where "qqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqq" select item), _ => 0 };
                    }
                }
                """,
                """
                class C {
                    object M() {
                        return state switch {
                            Xyyy when M<Faaaaa, IReadOnlyList<Ttttttttttttttttttttttttttttttttttttttttttttttttttttttttttt>>() => (from
                                    item in items
                                where "qqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqq"
                                select item),
                            _ => 0
                        };
                    }
                }
                """
            ),
            "p2:i16_a112_w94" => (
                """
                class C {
                    class D0 {
                        object M() {
                            return state switch { Xyyy when M<Faaaaa, IReadOnlyList<Ttttttttttttttttttttttttttttttttttttttttttttttttttttttt>>() => (from item in items where "qqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqq" select item), _ => 0 };
                        }
                    }
                }
                """,
                """
                class C {
                    class D0 {
                        object M() {
                            return state switch {
                                Xyyy when M<Faaaaa, IReadOnlyList<Ttttttttttttttttttttttttttttttttttttttttttttttttttttttt>>() => (from
                                        item in items
                                    where "qqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqq"
                                    select item),
                                _ => 0
                            };
                        }
                    }
                }
                """
            ),
            "p2:i20_a112_w128" => (
                """
                class C {
                    class D0 {
                        class D1 {
                            object M() {
                                return state switch { Xyyy when M<Faaaaa, IReadOnlyList<Ttttttttttttttttttttttttttttttttttttttttttttttttttt>>() => (from item in items where "qqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqq" select item), _ => 0 };
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
                                    Xyyy when M<Faaaaa, IReadOnlyList<Ttttttttttttttttttttttttttttttttttttttttttttttttttt>>() => (from
                                            item in items
                                        where "qqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqq"
                                        select item),
                                    _ => 0
                                };
                            }
                        }
                    }
                }
                """
            ),
            "p2:i24_a110_w132" => (
                """
                class C {
                    class D0 {
                        class D1 {
                            class D2 {
                                object M() {
                                    return state switch { Xyyy when M<Faaaaa, IReadOnlyList<Ttttttttttttttttttttttttttttttttttttttttttttt>>() => (from item in items where "qqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqq" select item), _ => 0 };
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
                                        Xyyy when M<Faaaaa, IReadOnlyList<Ttttttttttttttttttttttttttttttttttttttttttttt>>() => (from
                                                item in items
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
            "f576e:r0317_i12_q_w104_F" => (
                """
                class C {
                    object M() {
                        return state switch { X when M<HashSet<long>, IReadOnlyList<Ttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttt>>() => (from item in items where "qqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqq" select item), _ => 0 };
                    }
                }
                """,
                """
                class C {
                    object M() {
                        return state switch {
                            X when M<HashSet<long>, IReadOnlyList<
                                Ttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttt>>() => (from
                                    item in items
                                where "qqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqq"
                                select item),
                            _ => 0
                        };
                    }
                }
                """
            ),
            "f576b:r0579_i12_q_w128_F" => (
                """
                class C {
                    object M() {
                        return state switch { { Kind: not null } when Make<int, IReadOnlyList<Tttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttt>>() => (from item in items where "qqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqq" select item), _ => 0 };
                    }
                }
                """,
                """
                class C {
                    object M() {
                        return state switch {
                            { Kind: not null } when
                                Make<int, IReadOnlyList<
                                    Tttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttt>>() => (from
                                        item in items
                                    where "qqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqq"
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

    /// <summary>One column of body short of either bar the query moves below the arrow.</summary>
    [Theory]
    [InlineData("p2:i20_a112_w127")]
    [InlineData("p2:i16_a112_w93")]
    public void TheQueryMovesBelowShortOfTheBar(string cell) {
        var (source, expected) = cell switch {
            "p2:i20_a112_w127" => (
                """
                class C {
                    class D0 {
                        class D1 {
                            object M() {
                                return state switch { Xyyy when M<Faaaaa, IReadOnlyList<Ttttttttttttttttttttttttttttttttttttttttttttttttttt>>() => (from item in items where "qqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqq" select item), _ => 0 };
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
                                    Xyyy when M<Faaaaa, IReadOnlyList<Ttttttttttttttttttttttttttttttttttttttttttttttttttt>>() =>
                                        (from item in items
                                            where
                                                "qqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqq"
                                            select item),
                                    _ => 0
                                };
                            }
                        }
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
            _ => throw new ArgumentOutOfRangeException(nameof(cell))
        };

        Agrees(source, expected);
    }
}
