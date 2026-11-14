using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Core.Configuration;

namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     Issue #576, round fifteen (SK-DIV-0399). An atom body's <c>when</c> behind a generic call condition whose first
///     type argument nests two deep (<c>IEnumerable&lt;KeyValuePair&lt;int, string&gt;&gt;,</c>) is decided by
///     <c>Fitter.AtomWhenBreaks</c>, whose line-below clause is now the long-lead bars' edge; beside the <c>when</c> such
///     a list takes its outer comma by <c>Fitter.NestedAtomOuterCommaTakesTheFill</c>. Cells from the structure grids a7,
///     a8, a9 and the fresh probes. Every expected string is jb cleanupcode 2025.2.6's own output for the input.
/// </summary>
public sealed class AtomWhenNestedFirstIssue576Tests {
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


    /// <summary>A first argument nesting two deep follows the atom rule's regimes.</summary>
    [Theory]
    [InlineData("f576a:r0788_i20_atom_w30_F")]
    [InlineData("f576b:r0062_i24_atom_w38_K")]
    [InlineData("f576h:r0346_i24_atom_w85_K")]
    [InlineData("f576m:r0889_i16_atom_w33_K")]
    [InlineData("f576m:r0581_i20_atom_w77_F")]
    [InlineData("f576m:r0222_i20_atom_w70_F")]
    [InlineData("a7:i12_h15_L47_x145_b6")]
    [InlineData("a7:i12_h20_L45_x163_b14")]
    public void TheNestedFirstArgumentFollowsTheAtomRegimes(string cell) {
        var (source, expected) = cell switch {
            "f576a:r0788_i20_atom_w30_F" => (
                """
                class C {
                    class D0 {
                        class D1 {
                            object M() {
                                return state switch { string s when Make<IEnumerable<KeyValuePair<int, string>>, IReadOnlyList<Tttttttttttttttttttttttttttttttttttttttttttttttttttttttttttt>>() => Abcdefghijklm, _ => 0 };
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
                                    string s when
                                        Make<IEnumerable<KeyValuePair<int, string>>,
                                            IReadOnlyList<Tttttttttttttttttttttttttttttttttttttttttttttttttttttttttttt>>() =>
                                        Abcdefghijklm,
                                    _ => 0
                                };
                            }
                        }
                    }
                }
                """
            ),
            "f576b:r0062_i24_atom_w38_K" => (
                """
                class C {
                    class D0 {
                        class D1 {
                            class D2 {
                                object M() {
                                    return state switch {
                                        string s when M<IEnumerable<KeyValuePair<int, string>>, IReadOnlyList<Tttttttttttttttttttttttttttttttttttttttttt>>() => Value,
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
                                        string s when M<IEnumerable<KeyValuePair<int, string>>,
                                            IReadOnlyList<Tttttttttttttttttttttttttttttttttttttttttt>>() => Value,
                                        _ => 0
                                    };
                                }
                            }
                        }
                    }
                }
                """
            ),
            "f576h:r0346_i24_atom_w85_K" => (
                """
                class C {
                    class D0 {
                        class D1 {
                            class D2 {
                                object M() {
                                    return state switch {
                                        Tyyyyyyyyyyyy when Of<IEnumerable<KeyValuePair<int, string>>, IReadOnlyList<Ttttttttttttttttttttttttttttttttttttttttttt>>() => "ss",
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
                                        Tyyyyyyyyyyyy when Of<IEnumerable<KeyValuePair<int, string>>,
                                            IReadOnlyList<Ttttttttttttttttttttttttttttttttttttttttttt>>() => "ss",
                                        _ => 0
                                    };
                                }
                            }
                        }
                    }
                }
                """
            ),
            "f576m:r0889_i16_atom_w33_K" => (
                """
                class C {
                    class D0 {
                        object M() {
                            return state switch {
                                string s when M<IEnumerable<KeyValuePair<int, string>>, IReadOnlyList<Ttttttttttttttttttttttttttttttttttttttttt>>() => Value,
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
                                string s when M<IEnumerable<KeyValuePair<int, string>>,
                                    IReadOnlyList<Ttttttttttttttttttttttttttttttttttttttttt>>() => Value,
                                _ => 0
                            };
                        }
                    }
                }
                """
            ),
            "f576m:r0581_i20_atom_w77_F" => (
                """
                class C {
                    class D0 {
                        class D1 {
                            object M() {
                                return state switch { { Kind: not null } when M<IEnumerable<KeyValuePair<int, string>>, IReadOnlyList<Tttttttttttttttttttttttttttttttttttttttttttttttttttttt>>() => Value, _ => 0 };
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
                                        M<IEnumerable<KeyValuePair<int, string>>,
                                            IReadOnlyList<Tttttttttttttttttttttttttttttttttttttttttttttttttttttt>>() => Value,
                                    _ => 0
                                };
                            }
                        }
                    }
                }
                """
            ),
            "f576m:r0222_i20_atom_w70_F" => (
                """
                class C {
                    class D0 {
                        class D1 {
                            object M() {
                                return state switch { { Kind: not null } when M<IEnumerable<KeyValuePair<int, string>>, IReadOnlyList<Tttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttt>>() => Abcdefghijklm, _ => 0 };
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
                                        M<IEnumerable<KeyValuePair<int, string>>, IReadOnlyList<
                                            Tttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttt>>() =>
                                        Abcdefghijklm,
                                    _ => 0
                                };
                            }
                        }
                    }
                }
                """
            ),
            "a7:i12_h15_L47_x145_b6" => (
                """
                class C {
                    object M() {
                        return state switch { Xyyyyyyyyy when Maaaaaa<IEnumerable<KeyValuePair<int, string>>, IReadOnlyList<Ttttttttttttttttttttttttttttttttttttttttttttttttttt>>() => Value, _ => 0 };
                    }
                }
                """,
                """
                class C {
                    object M() {
                        return state switch {
                            Xyyyyyyyyy when Maaaaaa<IEnumerable<KeyValuePair<int, string>>,
                                IReadOnlyList<Ttttttttttttttttttttttttttttttttttttttttttttttttttt>>() => Value,
                            _ => 0
                        };
                    }
                }
                """
            ),
            "a7:i12_h20_L45_x163_b14" => (
                """
                class C {
                    object M() {
                        return state switch { Xyyyyyyyyyyyyyy when Maaaa<IEnumerable<KeyValuePair<int, string>>, IReadOnlyList<Tttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttt>>() => Abcdefghijklm, _ => 0 };
                    }
                }
                """,
                """
                class C {
                    object M() {
                        return state switch {
                            Xyyyyyyyyyyyyyy when
                                Maaaa<IEnumerable<KeyValuePair<int, string>>,
                                    IReadOnlyList<Tttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttt>>() =>
                                Abcdefghijklm,
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

    /// <summary>The line below a broken <c>when</c> is the long-lead bars' edge, not a bound of its own.</summary>
    [Theory]
    [InlineData("coordQ:r0051_i12_atom_w105_K")]
    [InlineData("a8:i12_h15_L26_e112_n1")]
    [InlineData("a8:i12_h18_L30_e113_n2")]
    [InlineData("a8:i12_h18_L45_e113_n2")]
    [InlineData("a8:i12_h15_L45_e114_n4")]
    public void TheLineBelowIsTheLongLeadBarsEdge(string cell) {
        var (source, expected) = cell switch {
            "coordQ:r0051_i12_atom_w105_K" => (
                """
                class C {
                    object M() {
                        return state switch {
                            string s when Pick<IReadOnlyCollection<Guid>, IReadOnlyList<Ttttttttttttttttttttttttttttttttttttttttttttttt>>() => 1,
                            _ => 0
                        };
                    }
                }
                """,
                """
                class C {
                    object M() {
                        return state switch {
                            string s when Pick<IReadOnlyCollection<Guid>,
                                IReadOnlyList<Ttttttttttttttttttttttttttttttttttttttttttttttt>>() => 1,
                            _ => 0
                        };
                    }
                }
                """
            ),
            "a8:i12_h15_L26_e112_n1" => (
                """
                class C {
                    object M() {
                        return state switch { Xyyyyyyyyy when M<Faaaaaaaaaaaaaaaaaaaaaa, IReadOnlyList<Ttttttttttttttttttttttttttttttttttttttttttttttttttt>>() => 1, _ => 0 };
                    }
                }
                """,
                """
                class C {
                    object M() {
                        return state switch {
                            Xyyyyyyyyy when M<Faaaaaaaaaaaaaaaaaaaaaa,
                                IReadOnlyList<Ttttttttttttttttttttttttttttttttttttttttttttttttttt>>() => 1,
                            _ => 0
                        };
                    }
                }
                """
            ),
            "a8:i12_h18_L30_e113_n2" => (
                """
                class C {
                    object M() {
                        return state switch { Xyyyyyyyyyyyy when M<Faaaaaaaaaaaaaaaaaaaaaaaaaa, IReadOnlyList<Tttttttttttttttttttttttttttttttttttttttttttttttt>>() => 12, _ => 0 };
                    }
                }
                """,
                """
                class C {
                    object M() {
                        return state switch {
                            Xyyyyyyyyyyyy when M<Faaaaaaaaaaaaaaaaaaaaaaaaaa,
                                IReadOnlyList<Tttttttttttttttttttttttttttttttttttttttttttttttt>>() => 12,
                            _ => 0
                        };
                    }
                }
                """
            ),
            "a8:i12_h18_L45_e113_n2" => (
                """
                class C {
                    object M() {
                        return state switch { Xyyyyyyyyyyyy when Maaaa<IEnumerable<KeyValuePair<int, string>>, IReadOnlyList<Ttttttttttttttttttttttttttttttttt>>() => 12, _ => 0 };
                    }
                }
                """,
                """
                class C {
                    object M() {
                        return state switch {
                            Xyyyyyyyyyyyy when Maaaa<IEnumerable<KeyValuePair<int, string>>,
                                IReadOnlyList<Ttttttttttttttttttttttttttttttttt>>() => 12,
                            _ => 0
                        };
                    }
                }
                """
            ),
            "a8:i12_h15_L45_e114_n4" => (
                """
                class C {
                    object M() {
                        return state switch { Xyyyyyyyyy when Maaaa<IEnumerable<KeyValuePair<int, string>>, IReadOnlyList<Tttttttttttttttttttttttttttttttttt>>() => 1234, _ => 0 };
                    }
                }
                """,
                """
                class C {
                    object M() {
                        return state switch {
                            Xyyyyyyyyy when Maaaa<IEnumerable<KeyValuePair<int, string>>,
                                IReadOnlyList<Tttttttttttttttttttttttttttttttttt>>() => 1234,
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

    /// <summary>Beside the <c>when</c>, a first argument nesting two deep fills at the inner <c>&lt;</c> by its own bar.</summary>
    [Theory]
    [InlineData("f576h:r0892_i16_atom_w29_F")]
    [InlineData("f576i:r0259_i20_atom_w24_K")]
    [InlineData("f576m:r0800_i16_atom_w130_F")]
    [InlineData("a9:i12_h15_L54_x175_b2")]
    [InlineData("a9:i24_h15_L50_x172_b2")]
    public void TheNestedFirstArgumentsFillPoint(string cell) {
        var (source, expected) = cell switch {
            "f576h:r0892_i16_atom_w29_F" => (
                """
                class C {
                    class D0 {
                        object M() {
                            return state switch { _ when CreateInstance<IEnumerable<KeyValuePair<int, string>>, IReadOnlyList<Tttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttt>>() => Value, _ => 0 };
                        }
                    }
                }
                """,
                """
                class C {
                    class D0 {
                        object M() {
                            return state switch {
                                _ when CreateInstance<IEnumerable<KeyValuePair<int, string>>, IReadOnlyList<
                                    Tttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttt>>() => Value,
                                _ => 0
                            };
                        }
                    }
                }
                """
            ),
            "f576i:r0259_i20_atom_w24_K" => (
                """
                class C {
                    class D0 {
                        class D1 {
                            object M() {
                                return state switch {
                                    var v when Materialise<IEnumerable<KeyValuePair<int, string>>, IReadOnlyList<Ttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttt>>() => Abcdefghijklm,
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
                                    var v when Materialise<IEnumerable<KeyValuePair<int, string>>, IReadOnlyList<
                                        Ttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttt>>() => Abcdefghijklm,
                                    _ => 0
                                };
                            }
                        }
                    }
                }
                """
            ),
            "f576m:r0800_i16_atom_w130_F" => (
                """
                class C {
                    class D0 {
                        object M() {
                            return state switch { Xy when Resolve<IEnumerable<KeyValuePair<int, string>>, IReadOnlyList<Tttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttt>>() => "ss", _ => 0 };
                        }
                    }
                }
                """,
                """
                class C {
                    class D0 {
                        object M() {
                            return state switch {
                                Xy when Resolve<IEnumerable<KeyValuePair<int, string>>, IReadOnlyList<
                                    Tttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttt>>() => "ss",
                                _ => 0
                            };
                        }
                    }
                }
                """
            ),
            "a9:i12_h15_L54_x175_b2" => (
                """
                class C {
                    object M() {
                        return state switch { Xyyyyyyyyy when Maaaaaaaaaaaaa<IEnumerable<KeyValuePair<int, string>>, IReadOnlyList<Tttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttt>>() => 1, _ => 0 };
                    }
                }
                """,
                """
                class C {
                    object M() {
                        return state switch {
                            Xyyyyyyyyy when Maaaaaaaaaaaaa<IEnumerable<KeyValuePair<int, string>>, IReadOnlyList<
                                Tttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttt>>() => 1,
                            _ => 0
                        };
                    }
                }
                """
            ),
            "a9:i24_h15_L50_x172_b2" => (
                """
                class C {
                    class D0 {
                        class D1 {
                            class D2 {
                                object M() {
                                    return state switch { Xyyyyyyyyy when Maaaaaaaaa<IEnumerable<KeyValuePair<int, string>>, IReadOnlyList<Ttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttt>>() => 1, _ => 0 };
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
                                        Xyyyyyyyyy when Maaaaaaaaa<IEnumerable<KeyValuePair<int, string>>,
                                            IReadOnlyList<Ttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttt>>() => 1,
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

    /// <summary>Past the inner bar, half a column lower behind a first argument nesting two deep.</summary>
    [Theory]
    [InlineData("coordQ:r0055_i16_atom_w70_K")]
    public void TheNestedInnerBarSitsLower(string cell) {
        var (source, expected) = cell switch {
            "coordQ:r0055_i16_atom_w70_K" => (
                """
                class C {
                    class D0 {
                        object M() {
                            return state switch {
                                { Kind: not null } when Pick<IEnumerable<KeyValuePair<int, string>>, IReadOnlyList<Ttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttt>>() => Short.Value12,
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
                                { Kind: not null } when Pick<IEnumerable<KeyValuePair<int, string>>, IReadOnlyList<
                                    Ttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttt>>() => Short.Value12,
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
