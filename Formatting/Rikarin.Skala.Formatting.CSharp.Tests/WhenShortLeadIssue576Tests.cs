using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Core.Configuration;

namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     Issue #576, round nine (SK-DIV-0399). A <c>when</c> condition whose lead through its first comma is under 23
///     columns fills beside the <c>when</c> behind a head under twelve columns while <c>head·(body − 6) &lt; 550</c>;
///     an arm's arrow, decided on the unlifted line, breaks before the <c>=&gt;</c> as well once the lift pushes
///     <c>…&gt;&gt;() =&gt;</c> past the margin; a <c>from</c> clause fills, its collection a level past the query's
///     clauses, in every position; and a parenthesised query stays on its arm's arrow line while
///     <c>2·body &gt; arrow + 88</c>. Every expected string is jb cleanupcode 2025.2.6's own output for the input.
/// </summary>
public sealed class WhenShortLeadIssue576Tests {
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

    /// <summary>A short lead below the bar fills beside the <c>when</c> (the fit grid, three leads and two indents).</summary>
    [Theory]
    [InlineData("s576:i12_h11_S10_q_w52")]
    [InlineData("s576:i20_h11_S01_q_w54")]
    [InlineData("s576:i20_h9_S11_q_w66")]
    public void AShortLeadFillsBesideTheWhenBelowTheBar(string cell) {
        var (source, expected) = cell switch {
            "s576:i12_h11_S10_q_w52" => (
                """
                class C {
                    object M() {
                        return state switch { Xyyyyy when Pick<int, IReadOnlyList<Tttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttt>>() => (from item in items where "qqqqqqqqqqq" select item), _ => 0 };
                    }
                }
                """,
                """
                class C {
                    object M() {
                        return state switch {
                            Xyyyyy when Pick<int, IReadOnlyList<
                                    Tttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttt>>() =>
                                (from item in items where "qqqqqqqqqqq" select item),
                            _ => 0
                        };
                    }
                }
                """
            ),
            "s576:i20_h11_S01_q_w54" => (
                """
                class C {
                    class D0 {
                        class D1 {
                            object M() {
                                return state switch { Xyyyyy when M<List<bool>, IReadOnlyList<Tttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttt>>() => (from item in items where "qqqqqqqqqqqqq" select item), _ => 0 };
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
                                    Xyyyyy when M<List<bool>, IReadOnlyList<
                                            Tttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttt>>() =>
                                        (from item in items where "qqqqqqqqqqqqq" select item),
                                    _ => 0
                                };
                            }
                        }
                    }
                }
                """
            ),
            "s576:i20_h9_S11_q_w66" => (
                """
                class C {
                    class D0 {
                        class D1 {
                            object M() {
                                return state switch { Xyyy when Pick<List<bool>, IReadOnlyList<Ttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttt>>() => (from item in items where "qqqqqqqqqqqqqqqqqqqqqqqqq" select item), _ => 0 };
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
                                    Xyyy when Pick<List<bool>, IReadOnlyList<
                                            Ttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttt>>() =>
                                        (from item in items where "qqqqqqqqqqqqqqqqqqqqqqqqq" select item),
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

    /// <summary>The same on a fresh probe written after the bar was set.</summary>
    [Theory]
    [InlineData("f576c:r0008_i24_q_w69_F")]
    [InlineData("f576c:r0021_i16_q_w40_K")]
    [InlineData("f576c:r0028_i16_c0_w20_F")]
    public void AShortLeadFillsOnAFreshProbe(string cell) {
        var (source, expected) = cell switch {
            "f576c:r0008_i24_q_w69_F" => (
                """
                class C {
                    class D0 {
                        class D1 {
                            class D2 {
                                object M() {
                                    return state switch { _ when Pick<(int, string), IReadOnlyList<Ttttttttttttttttttttttttttttttttttttttttttttttttttttttttttt>>() => (from item in items where "qqqqqqqqqqqqqqqqqqqqqqqqqqqq" select item), _ => 0 };
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
                                        _ when Pick<(int, string),
                                                IReadOnlyList<Ttttttttttttttttttttttttttttttttttttttttttttttttttttttttttt>>() =>
                                            (from item in items where "qqqqqqqqqqqqqqqqqqqqqqqqqqqq" select item),
                                        _ => 0
                                    };
                                }
                            }
                        }
                    }
                }
                """
            ),
            "f576c:r0021_i16_q_w40_K" => (
                """
                class C {
                    class D0 {
                        object M() {
                            return state switch {
                                null when Materialise<int, IReadOnlyList<Tttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttt>>() => (from item in items where "q" select item),
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
                                null when Materialise<int, IReadOnlyList<
                                        Tttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttt>>() =>
                                    (from item in items where "q" select item),
                                _ => 0
                            };
                        }
                    }
                }
                """
            ),
            "f576c:r0028_i16_c0_w20_F" => (
                """
                class C {
                    class D0 {
                        object M() {
                            return state switch { null when Pick<int, IReadOnlyList<Ttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttt>>() => Handle(value, "ccc"), _ => 0 };
                        }
                    }
                }
                """,
                """
                class C {
                    class D0 {
                        object M() {
                            return state switch {
                                null when Pick<int, IReadOnlyList<
                                        Ttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttt>>()
                                    => Handle(value, "ccc"),
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

    /// <summary>The arrow alone on its line once the lift pushes <c>…&gt;&gt;() =&gt;</c> past the margin (a fresh probe).</summary>
    [Theory]
    [InlineData("f576c:r0077_i16_atom_w21_F")]
    [InlineData("f576c:r0118_i16_c2_w129_K")]
    [InlineData("f576c:r0249_i20_c0_w20_K")]
    [InlineData("f576c:r0291_i24_c2_w43_F")]
    public void TheArrowBreaksBeforeItselfOnTheLiftedLine(string cell) {
        var (source, expected) = cell switch {
            "f576c:r0077_i16_atom_w21_F" => (
                """
                class C {
                    class D0 {
                        object M() {
                            return state switch { X when Of<IEnumerable<KeyValuePair<int, string>>, IReadOnlyList<Ttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttt>>() => Short.Value12, _ => 0 };
                        }
                    }
                }
                """,
                """
                class C {
                    class D0 {
                        object M() {
                            return state switch {
                                X when Of<IEnumerable<KeyValuePair<int, string>>, IReadOnlyList<
                                        Ttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttt>>()
                                    =>
                                    Short.Value12,
                                _ => 0
                            };
                        }
                    }
                }
                """
            ),
            "f576c:r0118_i16_c2_w129_K" => (
                """
                class C {
                    class D0 {
                        object M() {
                            return state switch {
                                { Kind: not null } when Resolve<int, IReadOnlyList<Ttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttt>>() =>
                                Compute("kkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkk", 2, flag),
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
                                { Kind: not null } when Resolve<int, IReadOnlyList<
                                        Ttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttt>>()
                                    =>
                                    Compute(
                                        "kkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkk",
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
            "f576c:r0249_i20_c0_w20_K" => (
                """
                class C {
                    class D0 {
                        class D1 {
                            object M() {
                                return state switch {
                                    X when Materialise<(int, string), IReadOnlyList<Tttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttt>>() => Handle(value, "ccc"),
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
                                    X when Materialise<(int, string), IReadOnlyList<
                                            Tttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttt>>()
                                        =>
                                        Handle(value, "ccc"),
                                    _ => 0
                                };
                            }
                        }
                    }
                }
                """
            ),
            "f576c:r0291_i24_c2_w43_F" => (
                """
                class C {
                    class D0 {
                        class D1 {
                            class D2 {
                                object M() {
                                    return state switch { int n when CreateInstance<Func<int, bool>, IReadOnlyList<Tttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttt>>() => Compute("kkkkkkkkkkkkkkkkkkkkkkk", 2, flag), _ => 0 };
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
                                        int n when CreateInstance<Func<int, bool>, IReadOnlyList<
                                                Tttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttt>>()
                                            =>
                                            Compute("kkkkkkkkkkkkkkkkkkkkkkk", 2, flag),
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

    /// <summary>A <c>from</c> clause fills where it does not fit, in six positions (a fresh probe).</summary>
    [Theory]
    [InlineData("q2:r0000_arg_d1")]
    [InlineData("q2:r0005_par_d3")]
    [InlineData("q2:r0006_var_d4")]
    [InlineData("q2:r0012_arm_d0")]
    [InlineData("q2:r0023_lam_d1")]
    [InlineData("q2:r0029_ret_d4")]
    public void AFromClauseFillsInEveryPosition(string cell) {
        var (source, expected) = cell switch {
            "q2:r0000_arg_d1" => (
                """
                class C {
                    class D0 {
                        object M() {
                            Use(vaaaaaaaaaaaa, from ittttttttttttttt in soooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooo where "qqqqqqq" select ittttttttttttttt);
                        }
                    }
                }
                """,
                """
                class C {
                    class D0 {
                        object M() {
                            Use(
                                vaaaaaaaaaaaa,
                                from ittttttttttttttt in
                                    soooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooo
                                where "qqqqqqq"
                                select ittttttttttttttt
                            );
                        }
                    }
                }
                """
            ),
            "q2:r0005_par_d3" => (
                """
                class C {
                    class D0 {
                        class D1 {
                            class D2 {
                                object M() {
                                    var vaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa = (from ittttttttttttttttttttttttttttttttttttttt in sooooooooooooooooooooooooooooooooooooooooooooooooooooooo where "qqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqq" select ittttttttttttttttttttttttttttttttttttttt).ToList();
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
                                    var vaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa =
                                        (from ittttttttttttttttttttttttttttttttttttttt in
                                                sooooooooooooooooooooooooooooooooooooooooooooooooooooooo
                                            where "qqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqq"
                                            select ittttttttttttttttttttttttttttttttttttttt).ToList();
                                }
                            }
                        }
                    }
                }
                """
            ),
            "q2:r0006_var_d4" => (
                """
                class C {
                    class D0 {
                        class D1 {
                            class D2 {
                                class D3 {
                                    object M() {
                                        var va = from ittttttttttttttttttttttttttttttttttttt in soooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooo where "qqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqq" select ittttttttttttttttttttttttttttttttttttt;
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
                                        var va =
                                            from ittttttttttttttttttttttttttttttttttttt in
                                                soooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooo
                                            where "qqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqq"
                                            select ittttttttttttttttttttttttttttttttttttt;
                                    }
                                }
                            }
                        }
                    }
                }
                """
            ),
            "q2:r0012_arm_d0" => (
                """
                class C {
                    object M() {
                        return state switch { vaaaaa => (from itttttttttttttttttttttttttttttttttttttttt in soooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooo where "qqqqqqqqqqqqqqqqqqqqqqqqqqq" select itttttttttttttttttttttttttttttttttttttttt), _ => 0 };
                    }
                }
                """,
                """
                class C {
                    object M() {
                        return state switch {
                            vaaaaa => (from itttttttttttttttttttttttttttttttttttttttt in
                                    soooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooo
                                where "qqqqqqqqqqqqqqqqqqqqqqqqqqq"
                                select itttttttttttttttttttttttttttttttttttttttt),
                            _ => 0
                        };
                    }
                }
                """
            ),
            "q2:r0023_lam_d1" => (
                """
                class C {
                    class D0 {
                        object M() {
                            Func<object> vaa = () => from ittttttttttttttttttt in sooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooo where "qqqqqqqqqqqqqqqqqqqqqqqqqqqqq" select ittttttttttttttttttt;
                        }
                    }
                }
                """,
                """
                class C {
                    class D0 {
                        object M() {
                            Func<object> vaa = () =>
                                from ittttttttttttttttttt in
                                    sooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooo
                                where "qqqqqqqqqqqqqqqqqqqqqqqqqqqqq"
                                select ittttttttttttttttttt;
                        }
                    }
                }
                """
            ),
            "q2:r0029_ret_d4" => (
                """
                class C {
                    class D0 {
                        class D1 {
                            class D2 {
                                class D3 {
                                    object M() {
                                        return from ittttttttttttttttttttttttttttttttt in soooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooo where "qqqqqqqqqqqqqqqqqqqqqqqq" select ittttttttttttttttttttttttttttttttt;
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
                                        return from ittttttttttttttttttttttttttttttttt in
                                                soooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooo
                                            where "qqqqqqqqqqqqqqqqqqqqqqqq"
                                            select ittttttttttttttttttttttttttttttttt;
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

    /// <summary>A query body stays on the arrow's line and fills its <c>from</c> clause (a fresh probe).</summary>
    [Theory]
    [InlineData("f576e:r0074_i12_q_w122_F")]
    [InlineData("f576e:r0081_i12_q_w124_F")]
    [InlineData("f576e:r0182_i16_q_w101_K")]
    [InlineData("f576e:r0233_i20_q_w117_K")]
    public void AQueryBodyStaysOnTheArrowsLine(string cell) {
        var (source, expected) = cell switch {
            "f576e:r0074_i12_q_w122_F" => (
                """
                class C {
                    object M() {
                        return state switch { string s when CreateInstance<HashSet<long>, IReadOnlyList<Tttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttt>>() => (from item in items where "qqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqq" select item), _ => 0 };
                    }
                }
                """,
                """
                class C {
                    object M() {
                        return state switch {
                            string s when
                                CreateInstance<HashSet<long>, IReadOnlyList<
                                    Tttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttt>>() => (from item in
                                        items
                                    where "qqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqq"
                                    select item),
                            _ => 0
                        };
                    }
                }
                """
            ),
            "f576e:r0081_i12_q_w124_F" => (
                """
                class C {
                    object M() {
                        return state switch { > 7 when Convert<List<bool>, IReadOnlyList<Tttttttttttttttttttttttttttttttttttttttt>>() => (from item in items where "qqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqq" select item), _ => 0 };
                    }
                }
                """,
                """
                class C {
                    object M() {
                        return state switch {
                            > 7 when Convert<List<bool>, IReadOnlyList<Tttttttttttttttttttttttttttttttttttttttt>>() => (from item in
                                    items
                                where "qqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqq"
                                select item),
                            _ => 0
                        };
                    }
                }
                """
            ),
            "f576e:r0182_i16_q_w101_K" => (
                """
                class C {
                    class D0 {
                        object M() {
                            return state switch {
                                { Kind: not null } when CreateInstance<Func<int, bool>, IReadOnlyList<Ttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttt>>() => (from item in items where "qqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqq" select item),
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
                                { Kind: not null } when
                                    CreateInstance<Func<int, bool>, IReadOnlyList<
                                        Ttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttt>>() => (from item
                                            in items
                                        where "qqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqq"
                                        select item),
                                _ => 0
                            };
                        }
                    }
                }
                """
            ),
            "f576e:r0233_i20_q_w117_K" => (
                """
                class C {
                    class D0 {
                        class D1 {
                            object M() {
                                return state switch {
                                    { Kind: not null } when CreateInstance<HashSet<long>, IReadOnlyList<Tttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttt>>() => (from item in items where "qqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqq" select item),
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
                                    { Kind: not null } when
                                        CreateInstance<HashSet<long>, IReadOnlyList<
                                            Tttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttt>>() => (from item in
                                                items
                                            where "qqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqq"
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
}
