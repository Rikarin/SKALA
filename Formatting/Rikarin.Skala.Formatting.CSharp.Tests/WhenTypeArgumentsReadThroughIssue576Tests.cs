using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Core.Configuration;

namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     Issue #576, round fifteen (SK-DIV-0399). An arm whose <c>when</c> condition breaks only at type argument lists
///     never reads its body through, whatever the pattern and however short the body: where the condition fits through
///     its <c>()</c>, beside the <c>when</c> or below a broken one, the arrow breaks and the type arguments stay whole
///     (<c>BreakPlan.ArmWhenHoldsTypeArguments</c>). Cells from the fresh probes. Every expected string is jb cleanupcode
///     2025.2.6's own output for the input.
/// </summary>
public sealed class WhenTypeArgumentsReadThroughIssue576Tests {
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

    /// <summary>The arrow breaks, before or after the <c>=&gt;</c>; the type arguments stay on the condition's line.</summary>
    [Theory]
    [InlineData("f576b:r0990_i12_atom_w23_K")]
    [InlineData("f576b:r0888_i24_atom_w99_F")]
    [InlineData("f576g:r0256_i12_atom_w73_K")]
    [InlineData("f576g:r0808_i12_atom_w105_F")]
    [InlineData("f576h:r0767_i12_atom_w90_F")]
    [InlineData("f576a:r0109_i16_atom_w100_F")]
    [InlineData("f576b:r0500_i16_atom_w91_K")]
    [InlineData("f576d:r0580_i12_atom_w70_F")]
    public void TheArrowBreaksAndTheTypeArgumentsStay(string cell) {
        var (source, expected) = cell switch {
            "f576b:r0990_i12_atom_w23_K" => (
                """
                class C {
                    object M() {
                        return state switch {
                            { Kind: not null } when Resolve<List<bool>, IReadOnlyList<Ttttttttttttttttttttttttttttttttt>>() => Abcdefghijklm,
                            _ => 0
                        };
                    }
                }
                """,
                """
                class C {
                    object M() {
                        return state switch {
                            { Kind: not null } when Resolve<List<bool>, IReadOnlyList<Ttttttttttttttttttttttttttttttttt>>() =>
                                Abcdefghijklm,
                            _ => 0
                        };
                    }
                }
                """
            ),
            "f576b:r0888_i24_atom_w99_F" => (
                """
                class C {
                    class D0 {
                        class D1 {
                            class D2 {
                                object M() {
                                    return state switch { { Kind: not null } when Resolve<List<bool>, IReadOnlyList<Ttttttttttttttttttttttttttttttttt>>() => Abcdefghijklm, _ => 0 };
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
                                        { Kind: not null } when Resolve<List<bool>, IReadOnlyList<Ttttttttttttttttttttttttttttttttt>>()
                                            => Abcdefghijklm,
                                        _ => 0
                                    };
                                }
                            }
                        }
                    }
                }
                """
            ),
            "f576g:r0256_i12_atom_w73_K" => (
                """
                class C {
                    object M() {
                        return state switch {
                            Ab when Convert<Bbbbbbbb, IReadOnlyList<Tttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttt>>() => 1,
                            _ => 0
                        };
                    }
                }
                """,
                """
                class C {
                    object M() {
                        return state switch {
                            Ab when Convert<Bbbbbbbb, IReadOnlyList<Tttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttt>>()
                                => 1,
                            _ => 0
                        };
                    }
                }
                """
            ),
            "f576g:r0808_i12_atom_w105_F" => (
                """
                class C {
                    object M() {
                        return state switch { null when M<A, IReadOnlyList<Ttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttt>>() => 1, _ => 0 };
                    }
                }
                """,
                """
                class C {
                    object M() {
                        return state switch {
                            null when M<A, IReadOnlyList<Ttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttt>>()
                                => 1,
                            _ => 0
                        };
                    }
                }
                """
            ),
            "f576h:r0767_i12_atom_w90_F" => (
                """
                class C {
                    object M() {
                        return state switch { { Kind: not null } when CreateInstance<int, IReadOnlyList<Tttttttttttttttttttttttttttttttttttttttttttttt>>() => Short.Value12, _ => 0 };
                    }
                }
                """,
                """
                class C {
                    object M() {
                        return state switch {
                            { Kind: not null } when CreateInstance<int, IReadOnlyList<Tttttttttttttttttttttttttttttttttttttttttttttt>>()
                                => Short.Value12,
                            _ => 0
                        };
                    }
                }
                """
            ),
            "f576a:r0109_i16_atom_w100_F" => (
                """
                class C {
                    class D0 {
                        object M() {
                            return state switch { { Kind: not null } when Pick<HashSet<long>, IReadOnlyList<Tttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttt>>() => Abcdefghijklm, _ => 0 };
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
                                    Pick<HashSet<long>,
                                        IReadOnlyList<Tttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttt>>() =>
                                    Abcdefghijklm,
                                _ => 0
                            };
                        }
                    }
                }
                """
            ),
            "f576b:r0500_i16_atom_w91_K" => (
                """
                class C {
                    class D0 {
                        object M() {
                            return state switch {
                                Ab when Make<HashSet<long>, IReadOnlyList<Ttttttttttttttttttttttttttttttttttttttttttttttttttttttttttt>>() => 1,
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
                                Ab when
                                    Make<HashSet<long>, IReadOnlyList<Ttttttttttttttttttttttttttttttttttttttttttttttttttttttttttt>>() =>
                                    1,
                                _ => 0
                            };
                        }
                    }
                }
                """
            ),
            "f576d:r0580_i12_atom_w70_F" => (
                """
                class C {
                    object M() {
                        return state switch { { Kind: not null } when Resolve<(int, string), IReadOnlyList<Ttttttttttttttttttttttttttttttttttttttttt>>() => "ss", _ => 0 };
                    }
                }
                """,
                """
                class C {
                    object M() {
                        return state switch {
                            { Kind: not null } when Resolve<(int, string), IReadOnlyList<Ttttttttttttttttttttttttttttttttttttttttt>>()
                                => "ss",
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
