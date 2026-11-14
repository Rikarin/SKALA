using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Core.Configuration;

namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     Issue #576, round ten (SK-DIV-0399). Behind a head under twelve columns the bar for breaking an arm's
///     <c>when</c> before a wide body rises with what the <c>when</c>'s line would keep of the condition, and behind
///     <c>X when</c> also with where the condition ends below; a <c>when</c> condition's outer type argument list before
///     a plain second argument breaks at its comma three columns further than before; and an atom body of eleven
///     columns or more no longer stops the <c>when</c> breaking however far past the margin the condition runs. Every
///     expected string is jb cleanupcode 2025.2.6's own output for the input.
/// </summary>
public sealed class WhenFirstLineIssue576Tests {
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
    ///     The <c>when</c> breaks only while <c>6·head·(body + 6) ≥ 3816 + 2·min(first, 42)·head</c>, <c>first</c> what
    ///     the <c>when</c>'s line keeps of the condition (structure grids e1, e2 and the fit grids).
    /// </summary>
    [Theory]
    [InlineData("e1:i12_h9_b38_c0_w76")]
    [InlineData("e1:i12_h7_c8_c0_w92")]
    [InlineData("e2:i12_h9_bc122_c0_w72")]
    [InlineData("s576:i12_h7_S00_q_w86")]
    [InlineData("f576e:r0040_i20_q_w89_F")]
    public void TheBarRisesWithTheFirstLine(string cell) {
        var (source, expected) = cell switch {
            "e1:i12_h9_b38_c0_w76" => (
                """
                class C {
                    object M() {
                        return state switch { Xyyy when M<Faaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa, IReadOnlyList<Ttttttttttttttttttttttttttttttttttttttttttttttttttttt>>() => Handle(value, "ccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc"), _ => 0 };
                    }
                }
                """,
                """
                class C {
                    object M() {
                        return state switch {
                            Xyyy when M<Faaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa,
                                IReadOnlyList<Ttttttttttttttttttttttttttttttttttttttttttttttttttttt>>() => Handle(
                                value,
                                "ccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc"
                            ),
                            _ => 0
                        };
                    }
                }
                """
            ),
            "e1:i12_h7_c8_c0_w92" => (
                """
                class C {
                    object M() {
                        return state switch { Xy when M<Faaaa, IReadOnlyDictionary<int, Tttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttt>>() => Handle(value, "ccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc"), _ => 0 };
                    }
                }
                """,
                """
                class C {
                    object M() {
                        return state switch {
                            Xy when M<Faaaa, IReadOnlyDictionary<int,
                                Tttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttt>>() => Handle(
                                value,
                                "ccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc"
                            ),
                            _ => 0
                        };
                    }
                }
                """
            ),
            "e2:i12_h9_bc122_c0_w72" => (
                """
                class C {
                    object M() {
                        return state switch { Xyyy when M<Faaaaaaaaaaaaaaaa, IReadOnlyList<Ttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttt>>() => Handle(value, "ccccccccccccccccccccccccccccccccccccccccccccccccccccccc"), _ => 0 };
                    }
                }
                """,
                """
                class C {
                    object M() {
                        return state switch {
                            Xyyy when M<Faaaaaaaaaaaaaaaa, IReadOnlyList<
                                Ttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttt>>() => Handle(
                                value,
                                "ccccccccccccccccccccccccccccccccccccccccccccccccccccccc"
                            ),
                            _ => 0
                        };
                    }
                }
                """
            ),
            "s576:i12_h7_S00_q_w86" => (
                """
                class C {
                    object M() {
                        return state switch { Xy when M<int, IReadOnlyList<Ttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttt>>() => (from item in items where "qqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqq" select item), _ => 0 };
                    }
                }
                """,
                """
                class C {
                    object M() {
                        return state switch {
                            Xy when M<int, IReadOnlyList<
                                    Ttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttt>>() =>
                                (from item in items where "qqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqq" select item),
                            _ => 0
                        };
                    }
                }
                """
            ),
            "f576e:r0040_i20_q_w89_F" => (
                """
                class C {
                    class D0 {
                        class D1 {
                            object M() {
                                return state switch { Ab when Materialise<A, IReadOnlyList<Tttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttt>>() => (from item in items where "qqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqq" select item), _ => 0 };
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
                                    Ab when Materialise<A, IReadOnlyList<
                                            Tttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttt>>()
                                        => (from item in items where "qqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqq" select item),
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
    ///     Behind <c>X when</c> the <c>when</c> breaks only before a body at most sixteen columns short of where the
    ///     condition ends below.
    /// </summary>
    [Theory]
    [InlineData("d576:i12_h6_P110_q_w100")]
    [InlineData("f576e:r0403_i12_c0_w106_F")]
    [InlineData("f576e:r0418_i20_q_w101_K")]
    public void AOneColumnPatternBreaksOnlyBeforeAWideBody(string cell) {
        var (source, expected) = cell switch {
            "d576:i12_h6_P110_q_w100" => (
                """
                class C {
                    object M() {
                        return state switch { X when Pick<int, Func<string, Ttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttt>>() => (from item in items where "qqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqq" select item), _ => 0 };
                    }
                }
                """,
                """
                class C {
                    object M() {
                        return state switch {
                            X when Pick<int, Func<string,
                                Ttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttt>>() => (from item in
                                    items
                                where "qqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqq"
                                select item),
                            _ => 0
                        };
                    }
                }
                """
            ),
            "f576e:r0403_i12_c0_w106_F" => (
                """
                class C {
                    object M() {
                        return state switch { _ when M<A, IReadOnlyList<Ttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttt>>() => Handle(value, "ccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc"), _ => 0 };
                    }
                }
                """,
                """
                class C {
                    object M() {
                        return state switch {
                            _ when M<A, IReadOnlyList<
                                Ttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttt>>() => Handle(
                                value,
                                "ccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc"
                            ),
                            _ => 0
                        };
                    }
                }
                """
            ),
            "f576e:r0418_i20_q_w101_K" => (
                """
                class C {
                    class D0 {
                        class D1 {
                            object M() {
                                return state switch {
                                    X when CreateInstance<int, IReadOnlyList<Ttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttt>>() => (from item in items where "qqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqq" select item),
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
                                    X when CreateInstance<int, IReadOnlyList<
                                            Ttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttt>>() =>
                                        (from item in items
                                            where "qqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqq"
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

    /// <summary>
    ///     Before <c>IReadOnlyList&lt;T…&gt;</c> the outer comma takes the break while the line below through
    ///     <c>() =&gt;</c> is 114 or less and <c>lead + 4·line ≤ 487</c>.
    /// </summary>
    [Theory]
    [InlineData("e1:i12_h9_b20_c0_w80")]
    [InlineData("e1:i12_h7_b14_c0_w40")]
    [InlineData("f576e:r0015_i12_c2_w64_F")]
    [InlineData("f576e:r0017_i12_q_w45_K")]
    public void APlainSecondArgumentBreaksAtTheOuterComma(string cell) {
        var (source, expected) = cell switch {
            "e1:i12_h9_b20_c0_w80" => (
                """
                class C {
                    object M() {
                        return state switch { Xyyy when M<Faaaaaaaaaaaaaaaa, IReadOnlyList<Ttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttt>>() => Handle(value, "ccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc"), _ => 0 };
                    }
                }
                """,
                """
                class C {
                    object M() {
                        return state switch {
                            Xyyy when
                                M<Faaaaaaaaaaaaaaaa,
                                    IReadOnlyList<Ttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttt>>() => Handle(
                                    value,
                                    "ccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc"
                                ),
                            _ => 0
                        };
                    }
                }
                """
            ),
            "e1:i12_h7_b14_c0_w40" => (
                """
                class C {
                    object M() {
                        return state switch { Xy when M<Faaaaaaaaaa, IReadOnlyList<Ttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttt>>() => Handle(value, "ccccccccccccccccccccccc"), _ => 0 };
                    }
                }
                """,
                """
                class C {
                    object M() {
                        return state switch {
                            Xy when M<Faaaaaaaaaa,
                                    IReadOnlyList<Ttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttt>>() =>
                                Handle(value, "ccccccccccccccccccccccc"),
                            _ => 0
                        };
                    }
                }
                """
            ),
            "f576e:r0015_i12_c2_w64_F" => (
                """
                class C {
                    object M() {
                        return state switch { { Kind: not null } when M<IReadOnlyCollection<Guid>, IReadOnlyList<Ttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttt>>() => Compute("kkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkk", 2, flag), _ => 0 };
                    }
                }
                """,
                """
                class C {
                    object M() {
                        return state switch {
                            { Kind: not null } when
                                M<IReadOnlyCollection<Guid>,
                                    IReadOnlyList<Ttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttt>>() =>
                                Compute("kkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkk", 2, flag),
                            _ => 0
                        };
                    }
                }
                """
            ),
            "f576e:r0017_i12_q_w45_K" => (
                """
                class C {
                    object M() {
                        return state switch {
                            Ab when CreateInstance<HashSet<long>, IReadOnlyList<Tttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttt>>() =>
                                (from item in items where "qqqq" select item),
                            _ => 0
                        };
                    }
                }
                """,
                """
                class C {
                    object M() {
                        return state switch {
                            Ab when CreateInstance<HashSet<long>,
                                    IReadOnlyList<Tttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttt>>() =>
                                (from item in items where "qqqq" select item),
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

    /// <summary>At 115 columns the outer comma takes the break behind a lead of 17 or more.</summary>
    [Theory]
    [InlineData("f576f:r0183_i20_c1_w29_F")]
    [InlineData("f576f:r0777_i16_c0_w69_F")]
    public void ALeadOfSeventeenTakesTheOuterCommaAt115(string cell) {
        var (source, expected) = cell switch {
            "f576f:r0183_i20_c1_w29_F" => (
                """
                class C {
                    class D0 {
                        class D1 {
                            object M() {
                                return state switch { not null when Of<(int, string), IReadOnlyList<Tttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttt>>() => new Holder(1, "nnnnnnnnnnnn"), _ => 0 };
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
                                        Of<(int, string),
                                            IReadOnlyList<Tttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttt>>() =>
                                        new Holder(1, "nnnnnnnnnnnn"),
                                    _ => 0
                                };
                            }
                        }
                    }
                }
                """
            ),
            "f576f:r0777_i16_c0_w69_F" => (
                """
                class C {
                    class D0 {
                        object M() {
                            return state switch { not null when CreateInstance<A, IReadOnlyList<Tttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttt>>() => Handle(value, "cccccccccccccccccccccccccccccccccccccccccccccccccccc"), _ => 0 };
                        }
                    }
                }
                """,
                """
                class C {
                    class D0 {
                        object M() {
                            return state switch {
                                not null when
                                    CreateInstance<A,
                                        IReadOnlyList<Tttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttt>>() =>
                                    Handle(value, "cccccccccccccccccccccccccccccccccccccccccccccccccccc"),
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
    ///     Before a body of eleven columns or more with its comma the <c>when</c> breaks however far past the margin
    ///     the condition runs (the band grid e3).
    /// </summary>
    [Theory]
    [InlineData("e3:i16_h13_bb14_atom_w24")]
    [InlineData("f576e:r0115_i12_atom_w88_K")]
    [InlineData("f576e:r0051_i16_atom_w64_K")]
    public void AWideAtomBreaksTheWhenFarPastTheMargin(string cell) {
        var (source, expected) = cell switch {
            "e3:i16_h13_bb14_atom_w24" => (
                """
                class C {
                    class D0 {
                        object M() {
                            return state switch { Xyyyyyyy when M<Faaaaaaaaaaaaaaaa, IReadOnlyList<Ttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttt>>() => Abbbbbbbbbbbb, _ => 0 };
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
                                    M<Faaaaaaaaaaaaaaaa, IReadOnlyList<
                                        Ttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttt>>() =>
                                    Abbbbbbbbbbbb,
                                _ => 0
                            };
                        }
                    }
                }
                """
            ),
            "f576e:r0115_i12_atom_w88_K" => (
                """
                class C {
                    object M() {
                        return state switch {
                            Tyyyyyyyyyyyy when Materialise<Bbbbbbbb, IReadOnlyList<Tttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttt>>() => Abcdefghijklm,
                            _ => 0
                        };
                    }
                }
                """,
                """
                class C {
                    object M() {
                        return state switch {
                            Tyyyyyyyyyyyy when
                                Materialise<Bbbbbbbb, IReadOnlyList<
                                    Tttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttt>>() =>
                                Abcdefghijklm,
                            _ => 0
                        };
                    }
                }
                """
            ),
            "f576e:r0051_i16_atom_w64_K" => (
                """
                class C {
                    class D0 {
                        object M() {
                            return state switch {
                                string s when Make<(int, string), IReadOnlyList<Tttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttt>>() => Abcdefghijklm,
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
                                string s when
                                    Make<(int, string), IReadOnlyList<
                                        Tttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttt>>() =>
                                    Abcdefghijklm,
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
