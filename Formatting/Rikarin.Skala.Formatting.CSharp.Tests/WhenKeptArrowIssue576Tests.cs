using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Core.Configuration;

namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     Issue #576, round twelve (SK-DIV-0399). Under an arrow the author broke after its <c>=&gt;</c>, the gap after
///     an arm's <c>when</c> before a generic call condition is not "the condition moves below exactly when it fits
///     there": behind a head under twelve columns it breaks only where the condition runs one or two columns past the
///     margin beside the <c>when</c>, and from twelve by two bars on where the condition ends below and its lead.
///     Every expected string is jb cleanupcode 2025.2.6's own output for the input.
/// </summary>
public sealed class WhenKeptArrowIssue576Tests {
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

    /// <summary>Under an arrow the author broke, a condition that would fit below fills beside the <c>when</c>.</summary>
    [Theory]
    [InlineData("f576j:r0016_i12_c1_w124_K")]
    [InlineData("f576j:r0138_i12_q_w50_K")]
    [InlineData("f576j:r0296_i16_q_w80_K")]
    [InlineData("f576j:r0563_i20_c0_w27_K")]
    [InlineData("k2:i12_h6_b14_x121_c0")]
    [InlineData("k2:i12_h18_b20_x127_c0")]
    public void TheConditionFillsBesideTheWhen(string cell) {
        var (source, expected) = cell switch {
            "f576j:r0016_i12_c1_w124_K" => (
                """
                class C {
                    object M() {
                        return state switch {
                            not null when Of<A, IReadOnlyList<Tttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttt>>() =>
                                new Holder(1, "nnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnn"),
                            _ => 0
                        };
                    }
                }
                """,
                """
                class C {
                    object M() {
                        return state switch {
                            not null when Of<A, IReadOnlyList<
                                    Tttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttt>>() =>
                                new Holder(
                                    1,
                                    "nnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnn"
                                ),
                            _ => 0
                        };
                    }
                }
                """
            ),
            "f576j:r0138_i12_q_w50_K" => (
                """
                class C {
                    object M() {
                        return state switch {
                            Xy when Pick<IReadOnlyCollection<Guid>, IReadOnlyList<Tttttttttttttttttttttttttttttttttttttttttttttttttttttt>>() =>
                                (from item in items where "qqqqqqqqq" select item),
                            _ => 0
                        };
                    }
                }
                """,
                """
                class C {
                    object M() {
                        return state switch {
                            Xy when Pick<IReadOnlyCollection<Guid>,
                                    IReadOnlyList<Tttttttttttttttttttttttttttttttttttttttttttttttttttttt>>() =>
                                (from item in items where "qqqqqqqqq" select item),
                            _ => 0
                        };
                    }
                }
                """
            ),
            "f576j:r0296_i16_q_w80_K" => (
                """
                class C {
                    class D0 {
                        object M() {
                            return state switch {
                                { Kind: not null } when Of<(int, string), IReadOnlyList<Ttttttttttttttttttttttttttttttttttttttttttttttttttttttttttt>>() =>
                                (from item in items where "qqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqq" select item),
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
                                { Kind: not null } when Of<(int, string),
                                        IReadOnlyList<Ttttttttttttttttttttttttttttttttttttttttttttttttttttttttttt>>() =>
                                    (from item in items where "qqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqq" select item),
                                _ => 0
                            };
                        }
                    }
                }
                """
            ),
            "f576j:r0563_i20_c0_w27_K" => (
                """
                class C {
                    class D0 {
                        class D1 {
                            object M() {
                                return state switch {
                                    string s when M<HashSet<long>, IReadOnlyList<Ttttttttttttttttttttttttttttttttttttttttttttttttttttttt>>() =>
                                Handle(value, "cccccccccc"),
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
                                    string s when M<HashSet<long>,
                                            IReadOnlyList<Ttttttttttttttttttttttttttttttttttttttttttttttttttttttt>>() =>
                                        Handle(value, "cccccccccc"),
                                    _ => 0
                                };
                            }
                        }
                    }
                }
                """
            ),
            "k2:i12_h6_b14_x121_c0" => (
                """
                class C {
                    object M() {
                        return state switch {
                            X when M<Faaaaaaaaaa, IReadOnlyList<Ttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttt>>() =>
                                Handle(value, "ccccccccccccccccccccccc"),
                            _ => 0
                        };
                    }
                }
                """,
                """
                class C {
                    object M() {
                        return state switch {
                            X when M<Faaaaaaaaaa,
                                    IReadOnlyList<Ttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttt>>() =>
                                Handle(value, "ccccccccccccccccccccccc"),
                            _ => 0
                        };
                    }
                }
                """
            ),
            "k2:i12_h18_b20_x127_c0" => (
                """
                class C {
                    object M() {
                        return state switch {
                            Xyyyyyyyyyyyy when M<Faaaaaaaaaaaaaaaa, IReadOnlyList<Ttttttttttttttttttttttttttttttttttttttttttttttttttttttttt>>() =>
                                Handle(value, "ccccccccccccccccccccccc"),
                            _ => 0
                        };
                    }
                }
                """,
                """
                class C {
                    object M() {
                        return state switch {
                            Xyyyyyyyyyyyy when M<Faaaaaaaaaaaaaaaa,
                                    IReadOnlyList<Ttttttttttttttttttttttttttttttttttttttttttttttttttttttttt>>() =>
                                Handle(value, "ccccccccccccccccccccccc"),
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

    /// <summary>Under an arrow the author broke, the <c>when</c> still breaks inside the measured bounds.</summary>
    [Theory]
    [InlineData("k2:i12_h13_b9_x132_c0")]
    [InlineData("k2:i12_h18_b20_x126_c0")]
    [InlineData("f576d:r0178_i24_c1_w51_K")]
    public void TheWhenStillBreaks(string cell) {
        var (source, expected) = cell switch {
            "k2:i12_h13_b9_x132_c0" => (
                """
                class C {
                    object M() {
                        return state switch {
                            Xyyyyyyy when M<Faaaaa, IReadOnlyList<Tttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttt>>() =>
                                Handle(value, "ccccccccccccccccccccccc"),
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
                                M<Faaaaa, IReadOnlyList<
                                    Tttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttt>>() =>
                                Handle(value, "ccccccccccccccccccccccc"),
                            _ => 0
                        };
                    }
                }
                """
            ),
            "k2:i12_h18_b20_x126_c0" => (
                """
                class C {
                    object M() {
                        return state switch {
                            Xyyyyyyyyyyyy when M<Faaaaaaaaaaaaaaaa, IReadOnlyList<Tttttttttttttttttttttttttttttttttttttttttttttttttttttttt>>() =>
                                Handle(value, "ccccccccccccccccccccccc"),
                            _ => 0
                        };
                    }
                }
                """,
                """
                class C {
                    object M() {
                        return state switch {
                            Xyyyyyyyyyyyy when
                                M<Faaaaaaaaaaaaaaaa, IReadOnlyList<Tttttttttttttttttttttttttttttttttttttttttttttttttttttttt>>() =>
                                Handle(value, "ccccccccccccccccccccccc"),
                            _ => 0
                        };
                    }
                }
                """
            ),
            "f576d:r0178_i24_c1_w51_K" => (
                """
                class C {
                    class D0 {
                        class D1 {
                            class D2 {
                                object M() {
                                    return state switch {
                                        string s when Of<A, IReadOnlyList<Ttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttt>>() =>
                                new Holder(1, "nnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnn"),
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
                                        string s when
                                            Of<A, IReadOnlyList<Ttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttt>>()
                                            =>
                                            new Holder(1, "nnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnn"),
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
