using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Core.Configuration;

namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     Issue #576, round eight (SK-DIV-0399): behind a head under twelve columns, before a call, a creation or a
///     parenthesised query, an arm's <c>when</c> breaks only from a body width that falls as the head widens,
///     <c>head·(body − 6) ≥ 627</c>; short of it the type arguments fill beside the <c>when</c>. Every expected string is
///     jb cleanupcode 2025.2.6's own output for the input.
/// </summary>
public sealed class WideBodyWhenIssue576Tests {
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
    ///     Each pair straddles the limit (a 62-column query fills behind an 11-column head, 64 breaks; 94 and 98 behind
    ///     7); a call body follows it too, and a condition that ends at 119 on the line below moves there whatever the
    ///     body. The last three are from a probe written after the rule.
    /// </summary>
    [Theory]
    [InlineData("d576:i12_h11_M104_q_w62")]
    [InlineData("d576:i12_h11_M104_q_w64")]
    [InlineData("d576:i12_h7_C104_q_w94")]
    [InlineData("d576:i12_h7_C104_q_w98")]
    [InlineData("w576:i12_h9_M106_c0_w74")]
    [InlineData("f576a:r0893_i16_q_w53_K")]
    [InlineData("f576b:r0046_i12_c0_w32_F")]
    [InlineData("f576b:r0094_i12_c1_w61_F")]
    [InlineData("f576b:r0072_i24_q_w32_F")]
    public void AWideBodyBreaksTheWhenOnlyPastAHeadsLimit(string cell) {
        var (source, expected) = cell switch {
            "d576:i12_h11_M104_q_w62" => (
                """
                class C {
                    object M() {
                        return state switch { Xyyyyy when Materialise<List<bool>, IReadOnlyDictionary<int, Ttttttttttttttttttttttttttttttttttttttttttttttttttt>>() => (from item in items where "qqqqqqqqqqqqqqqqqqqqq" select item), _ => 0 };
                    }
                }
                """,
                """
                class C {
                    object M() {
                        return state switch {
                            Xyyyyy when Materialise<List<bool>,
                                IReadOnlyDictionary<int, Ttttttttttttttttttttttttttttttttttttttttttttttttttt>>() => (from item in items
                                where "qqqqqqqqqqqqqqqqqqqqq"
                                select item),
                            _ => 0
                        };
                    }
                }
                """
            ),
            "d576:i12_h11_M104_q_w64" => (
                """
                class C {
                    object M() {
                        return state switch { Xyyyyy when Materialise<List<bool>, IReadOnlyDictionary<int, Ttttttttttttttttttttttttttttttttttttttttttttttttttt>>() => (from item in items where "qqqqqqqqqqqqqqqqqqqqqqq" select item), _ => 0 };
                    }
                }
                """,
                """
                class C {
                    object M() {
                        return state switch {
                            Xyyyyy when
                                Materialise<List<bool>, IReadOnlyDictionary<int, Ttttttttttttttttttttttttttttttttttttttttttttttttttt>>()
                                => (from item in items where "qqqqqqqqqqqqqqqqqqqqqqq" select item),
                            _ => 0
                        };
                    }
                }
                """
            ),
            "d576:i12_h7_C104_q_w94" => (
                """
                class C {
                    object M() {
                        return state switch { Xy when Convert<Dictionary<int, string>, Tttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttt>() => (from item in items where "qqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqq" select item), _ => 0 };
                    }
                }
                """,
                """
                class C {
                    object M() {
                        return state switch {
                            Xy when Convert<Dictionary<int, string>,
                                Tttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttt>() => (from item in items
                                where "qqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqq"
                                select item),
                            _ => 0
                        };
                    }
                }
                """
            ),
            "d576:i12_h7_C104_q_w98" => (
                """
                class C {
                    object M() {
                        return state switch { Xy when Convert<Dictionary<int, string>, Tttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttt>() => (from item in items where "qqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqq" select item), _ => 0 };
                    }
                }
                """,
                """
                class C {
                    object M() {
                        return state switch {
                            Xy when
                                Convert<Dictionary<int, string>, Tttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttt>()
                                => (from item in items where "qqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqq" select item),
                            _ => 0
                        };
                    }
                }
                """
            ),
            "w576:i12_h9_M106_c0_w74" => (
                """
                class C {
                    object M() {
                        return state switch { Xyyy when Materialise<List<bool>, IReadOnlyDictionary<int, Ttttttttttttttttttttttttttttttttttttttttttttttttttttt>>() => Handle(value, "ccccccccccccccccccccccccccccccccccccccccccccccccccccccccc"), _ => 0 };
                    }
                }
                """,
                """
                class C {
                    object M() {
                        return state switch {
                            Xyyy when Materialise<List<bool>,
                                IReadOnlyDictionary<int, Ttttttttttttttttttttttttttttttttttttttttttttttttttttt>>() => Handle(
                                value,
                                "ccccccccccccccccccccccccccccccccccccccccccccccccccccccccc"
                            ),
                            _ => 0
                        };
                    }
                }
                """
            ),
            "f576a:r0893_i16_q_w53_K" => (
                """
                class C {
                    class D0 {
                        object M() {
                            return state switch {
                                X when Convert<Dictionary<int, string>, IReadOnlyList<Ttttttttttttttttttttttttttttttttttttttttttttttt>>() => (from item in items where "qqqqqqqqqqqq" select item),
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
                                X when
                                    Convert<Dictionary<int, string>, IReadOnlyList<Ttttttttttttttttttttttttttttttttttttttttttttttt>>()
                                    => (from item in items where "qqqqqqqqqqqq" select item),
                                _ => 0
                            };
                        }
                    }
                }
                """
            ),
            "f576b:r0046_i12_c0_w32_F" => (
                """
                class C {
                    object M() {
                        return state switch { null when Resolve<IReadOnlyCollection<Guid>, IReadOnlyList<Tttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttt>>() => Handle(value, "ccccccccccccccc"), _ => 0 };
                    }
                }
                """,
                """
                class C {
                    object M() {
                        return state switch {
                            null when Resolve<IReadOnlyCollection<Guid>,
                                IReadOnlyList<Tttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttt>>() => Handle(
                                value,
                                "ccccccccccccccc"
                            ),
                            _ => 0
                        };
                    }
                }
                """
            ),
            "f576b:r0094_i12_c1_w61_F" => (
                """
                class C {
                    object M() {
                        return state switch { Ab when Resolve<IReadOnlyCollection<Guid>, IReadOnlyList<Ttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttt>>() => new Holder(1, "nnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnn"), _ => 0 };
                    }
                }
                """,
                """
                class C {
                    object M() {
                        return state switch {
                            Ab when Resolve<IReadOnlyCollection<Guid>, IReadOnlyList<
                                    Ttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttt>>() =>
                                new Holder(1, "nnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnn"),
                            _ => 0
                        };
                    }
                }
                """
            ),
            "f576b:r0072_i24_q_w32_F" => (
                """
                class C {
                    class D0 {
                        class D1 {
                            class D2 {
                                object M() {
                                    return state switch { X when Materialise<IEnumerable<KeyValuePair<int, string>>, IReadOnlyList<Tttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttt>>() => (from item in items where "q" select item), _ => 0 };
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
                                        X when Materialise<IEnumerable<KeyValuePair<int, string>>, IReadOnlyList<
                                                Tttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttt>>() =>
                                            (from item in items where "q" select item),
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
