using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Core.Configuration;

namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     Issue #576, round eleven (SK-DIV-0399). Behind a head under twelve columns, before a wide body: a condition that
///     fits on the line below but runs three or more columns past the margin beside the <c>when</c> takes the product
///     bar; where the fill must take an inner <c>&lt;</c> the bar rises a column and a half of body per column of the
///     condition's flat end; the outer comma's raise stops at a first line of 34; and behind <c>X when</c> the bar reads
///     where the condition ends below. Every expected string is jb cleanupcode 2025.2.6's own output for the input.
/// </summary>
public sealed class WhenConditionEndIssue576Tests {
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
    ///     A condition that fits on the line below but runs three or more columns past the margin beside the <c>when</c>
    ///     takes the product bar.
    /// </summary>
    [Theory]
    [InlineData("f576j:r0924_i20_q_w56_F")]
    [InlineData("b576:i12_h3_c0_w20_F")]
    [InlineData("f576h:r0363_i16_c2_w33_F")]
    public void TheConditionBesideTheWhenDecidesNotTheLineBelow(string cell) {
        var (source, expected) = cell switch {
            "f576j:r0924_i20_q_w56_F" => (
                """
                class C {
                    class D0 {
                        class D1 {
                            object M() {
                                return state switch { null when Convert<A, IReadOnlyList<Tttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttt>>() => (from item in items where "qqqqqqqqqqqqqqq" select item), _ => 0 };
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
                                    null when Convert<A,
                                            IReadOnlyList<Tttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttt>>() =>
                                        (from item in items where "qqqqqqqqqqqqqqq" select item),
                                    _ => 0
                                };
                            }
                        }
                    }
                }
                """
            ),
            "b576:i12_h3_c0_w20_F" => (
                """
                class C {
                    object M() {
                        return state switch { var v when Convert<Dictionary<int, string>, Ttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttt>() => Handle(value, "ccc"), _ => 0 };
                    }
                }
                """,
                """
                class C {
                    object M() {
                        return state switch {
                            var v when Convert<Dictionary<int, string>,
                                Ttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttt>() => Handle(value, "ccc"),
                            _ => 0
                        };
                    }
                }
                """
            ),
            "f576h:r0363_i16_c2_w33_F" => (
                """
                class C {
                    class D0 {
                        object M() {
                            return state switch { null when M<HashSet<long>, IReadOnlyList<Tttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttt>>() => Compute("kkkkkkkkkkkkk", 2, flag), _ => 0 };
                        }
                    }
                }
                """,
                """
                class C {
                    class D0 {
                        object M() {
                            return state switch {
                                null when M<HashSet<long>,
                                    IReadOnlyList<Tttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttt>>() => Compute(
                                    "kkkkkkkkkkkkk",
                                    2,
                                    flag
                                ),
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
    ///     Where the fill must take an inner <c>&lt;</c>, the <c>when</c> breaks only while
    ///     <c>head·(2·body − 2·lead − 3·X + 658) ≥ 2960</c>.
    /// </summary>
    [Theory]
    [InlineData("e5:i20_h9_b14_c156_c0_w104")]
    [InlineData("e4:i12_h7_b14_c134_c0_w106")]
    [InlineData("f576h:r0889_i20_c0_w79_K")]
    public void AnInnerFillRaisesTheBarWithTheConditionsWidth(string cell) {
        var (source, expected) = cell switch {
            "e5:i20_h9_b14_c156_c0_w104" => (
                """
                class C {
                    class D0 {
                        class D1 {
                            object M() {
                                return state switch { Xyyy when M<Faaaaaaaaaa, IReadOnlyList<Ttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttt>>() => Handle(value, "ccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc"), _ => 0 };
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
                                    Xyyy when M<Faaaaaaaaaa, IReadOnlyList<
                                            Ttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttt>>()
                                        => Handle(
                                            value,
                                            "ccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc"
                                        ),
                                    _ => 0
                                };
                            }
                        }
                    }
                }
                """
            ),
            "e4:i12_h7_b14_c134_c0_w106" => (
                """
                class C {
                    object M() {
                        return state switch { Xy when M<Faaaaaaaaaa, IReadOnlyList<Ttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttt>>() => Handle(value, "ccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc"), _ => 0 };
                    }
                }
                """,
                """
                class C {
                    object M() {
                        return state switch {
                            Xy when M<Faaaaaaaaaa, IReadOnlyList<
                                    Ttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttt>>()
                                => Handle(
                                    value,
                                    "ccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc"
                                ),
                            _ => 0
                        };
                    }
                }
                """
            ),
            "f576h:r0889_i20_c0_w79_K" => (
                """
                class C {
                    class D0 {
                        class D1 {
                            object M() {
                                return state switch {
                                    null when Resolve<IReadOnlyCollection<Guid>, IReadOnlyList<Ttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttt>>() => Handle(value, "cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc"),
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
                                    null when Resolve<IReadOnlyCollection<Guid>, IReadOnlyList<
                                            Ttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttt>>()
                                        =>
                                        Handle(value, "cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc"),
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

    /// <summary>At the outer comma the raise stops at a first line of 34, reached strictly past <c>68·head</c>.</summary>
    [Theory]
    [InlineData("coordQ:r0981_i16_c1_w70_K")]
    [InlineData("f576i:r0607_i16_q_w69_K")]
    [InlineData("s576:i12_h9_S22_q_w76")]
    public void AnOuterFillsRaiseStopsAtThirtyFour(string cell) {
        var (source, expected) = cell switch {
            "coordQ:r0981_i16_c1_w70_K" => (
                """
                class C {
                    class D0 {
                        object M() {
                            return state switch {
                                var v when Materialise<IReadOnlyCollection<Guid>, IReadOnlyList<Tttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttt>>() => new Holder(1, "nnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnn"),
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
                                var v when
                                    Materialise<IReadOnlyCollection<Guid>,
                                        IReadOnlyList<Tttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttt>>() => new Holder(
                                        1,
                                        "nnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnn"
                                    ),
                                _ => 0
                            };
                        }
                    }
                }
                """
            ),
            "f576i:r0607_i16_q_w69_K" => (
                """
                class C {
                    class D0 {
                        object M() {
                            return state switch {
                                int n when Materialise<Dictionary<int, string>, IReadOnlyList<Tttttttttttttttttttttttttttttttttttttttttttttttttttttttttttt>>() => (from item in items where "qqqqqqqqqqqqqqqqqqqqqqqqqqqq" select item),
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
                                int n when
                                    Materialise<Dictionary<int, string>,
                                        IReadOnlyList<Tttttttttttttttttttttttttttttttttttttttttttttttttttttttttttt>>() =>
                                    (from item in items where "qqqqqqqqqqqqqqqqqqqqqqqqqqqq" select item),
                                _ => 0
                            };
                        }
                    }
                }
                """
            ),
            "s576:i12_h9_S22_q_w76" => (
                """
                class C {
                    object M() {
                        return state switch { Xyyy when Materialise<Dictionary<int, string>, IReadOnlyList<Ttttttttttttttttttttttttttttttttttttttttttttttttttt>>() => (from item in items where "qqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqq" select item), _ => 0 };
                    }
                }
                """,
                """
                class C {
                    object M() {
                        return state switch {
                            Xyyy when Materialise<Dictionary<int, string>,
                                IReadOnlyList<Ttttttttttttttttttttttttttttttttttttttttttttttttttt>>() => (from item in items
                                where "qqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqq"
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

    /// <summary>Behind <c>X when</c> the <c>when</c> breaks only while <c>2·body ≥ 3·end + 2·last − lead − 214</c>.</summary>
    [Theory]
    [InlineData("f576i:r0303_i12_c0_w109_K")]
    [InlineData("f576e:r0950_i16_c2_w127_K")]
    [InlineData("d576:i20_h6_M116_q_w124")]
    [InlineData("f576c:r0824_i20_c1_w111_F")]
    public void AOneColumnPatternReadsTheConditionsEnd(string cell) {
        var (source, expected) = cell switch {
            "f576i:r0303_i12_c0_w109_K" => (
                """
                class C {
                    object M() {
                        return state switch {
                            X when Make<List<bool>, IReadOnlyList<Ttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttt>>() => Handle(value, "cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc"),
                            _ => 0
                        };
                    }
                }
                """,
                """
                class C {
                    object M() {
                        return state switch {
                            X when
                                Make<List<bool>, IReadOnlyList<
                                    Ttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttt>>() => Handle(
                                    value,
                                    "cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc"
                                ),
                            _ => 0
                        };
                    }
                }
                """
            ),
            "f576e:r0950_i16_c2_w127_K" => (
                """
                class C {
                    class D0 {
                        object M() {
                            return state switch {
                                _ when Resolve<IEnumerable<KeyValuePair<int, string>>, IReadOnlyList<Ttttttttttttttttttttttttttttttttttttttttttttttttttt>>() => Compute("kkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkk", 2, flag),
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
                                _ when Resolve<IEnumerable<KeyValuePair<int, string>>,
                                    IReadOnlyList<Ttttttttttttttttttttttttttttttttttttttttttttttttttt>>() => Compute(
                                    "kkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkk",
                                    2,
                                    flag
                                ),
                                _ => 0
                            };
                        }
                    }
                }
                """
            ),
            "d576:i20_h6_M116_q_w124" => (
                """
                class C {
                    class D0 {
                        class D1 {
                            object M() {
                                return state switch { X when Materialise<List<bool>, IReadOnlyDictionary<int, Ttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttt>>() => (from item in items where "qqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqq" select item), _ => 0 };
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
                                    X when Materialise<List<bool>, IReadOnlyDictionary<int,
                                        Ttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttt>>() => (from item in items
                                        where "qqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqq"
                                        select item),
                                    _ => 0
                                };
                            }
                        }
                    }
                }
                """
            ),
            "f576c:r0824_i20_c1_w111_F" => (
                """
                class C {
                    class D0 {
                        class D1 {
                            object M() {
                                return state switch { _ when Pick<int, IReadOnlyList<Ttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttt>>() => new Holder(1, "nnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnn"), _ => 0 };
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
                                    _ when Pick<int, IReadOnlyList<
                                        Ttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttt>>() => new Holder(
                                        1,
                                        "nnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnn"
                                    ),
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

    /// <summary>A <c>where</c> whose literal runs past the margin breaks after the keyword, the literal a level in.</summary>
    [Theory]
    [InlineData("d576:i20_h9_M110_q_w128")]
    [InlineData("f576j:r0372_i20_q_w127_F")]
    [InlineData("f576h:r0047_i24_q_w124_F")]
    public void AWhereLiteralMovesBelowItsClause(string cell) {
        var (source, expected) = cell switch {
            "d576:i20_h9_M110_q_w128" => (
                """
                class C {
                    class D0 {
                        class D1 {
                            object M() {
                                return state switch { Xyyy when Materialise<List<bool>, IReadOnlyDictionary<int, Ttttttttttttttttttttttttttttttttttttttttttttttttttttttttt>>() => (from item in items where "qqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqq" select item), _ => 0 };
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
                                    Xyyy when
                                        Materialise<List<bool>, IReadOnlyDictionary<int,
                                            Ttttttttttttttttttttttttttttttttttttttttttttttttttttttttt>>() => (from item in items
                                            where
                                                "qqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqq"
                                            select item),
                                    _ => 0
                                };
                            }
                        }
                    }
                }
                """
            ),
            "f576j:r0372_i20_q_w127_F" => (
                """
                class C {
                    class D0 {
                        class D1 {
                            object M() {
                                return state switch { not null when CreateInstance<A, IReadOnlyList<Tttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttt>>() => (from item in items where "qqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqq" select item), _ => 0 };
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
                                        CreateInstance<A, IReadOnlyList<
                                            Tttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttt>>() =>
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
            "f576h:r0047_i24_q_w124_F" => (
                """
                class C {
                    class D0 {
                        class D1 {
                            class D2 {
                                object M() {
                                    return state switch { Ab when CreateInstance<Dictionary<int, string>, IReadOnlyList<Tttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttt>>() => (from item in items where "qqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqq" select item), _ => 0 };
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
                                        Ab when CreateInstance<Dictionary<int, string>, IReadOnlyList<
                                                Tttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttt>>()
                                            => (from item in items
                                                where
                                                    "qqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqq"
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
            _ => throw new ArgumentOutOfRangeException(nameof(cell))
        };
        Agrees(source, expected);
    }
}
