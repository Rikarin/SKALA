namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     Round six of #576: an arm whose <c>when</c> condition breaks only at type argument lists. Every expected string
///     is <c>jb cleanupcode</c>'s own output, measured 2026-10-14 with <c>Testing ask</c> (SK-DIV-0399).
/// </summary>
public sealed class WhenHeadReadThroughIssue576Tests {
    /// <summary>
    ///     A condition that fits beside the <c>when</c> keeps its type arguments whole and the arrow takes the break,
    ///     after it while the line through it fits (M1, M3) and before it after that (M2). Its outer list breaks after
    ///     its first comma while the line it leaves is short (M5) and after the inner list's comma when it is not (M4).
    ///     Before the fix M1 to M4 filled the type arguments at their first comma.
    /// </summary>
    [Fact]
    public void AWhenConditionOfTypeArguments_GivesTheBreakToTheArrow() =>
        Oracle.Agrees(
            """
            class C {
                void M1() {
                    return state switch { Tyyyyyyyyyyyyyyy when Materialise<List<bool>, IReadOnlyDictionary<int, Ttttttttttttttttttttttttttttt>>() => 1, _ => 0 };
                }

                void M2() {
                    return state switch { Tyyyyyyyyyyyyyyy when Materialise<List<bool>, IReadOnlyDictionary<int, Tttttttttttttttttttttttttttttttt>>() => 1, _ => 0 };
                }

                void M3() {
                    if (c) {
                        if (c) {
                            return state switch { not null when Materialise<List<bool>, IReadOnlyDictionary<int, Tttttttttttttttttttttttttt>>() => "ss", _ => 0 };
                        }
                    }
                }

                void M4() {
                    if (c) {
                        if (c) {
                            return state switch { byte when Materialise<List<bool>, IReadOnlyDictionary<int, Ttttttttttttttttttttttttttttttttttttttttttttttttttttttttt>>() => 'c', _ => 0 };
                        }
                    }
                }

                void M5() {
                    return state switch { Tyyyyyyyyyyyyyyy when Materialise<List<bool>, IReadOnlyDictionary<int, Ttttttttttttttttttttttttttttttttttttttttttttttttttttt>>() => 1, _ => 0 };
                }
            }
            """,
            """
            class C {
                void M1() {
                    return state switch {
                        Tyyyyyyyyyyyyyyy when Materialise<List<bool>, IReadOnlyDictionary<int, Ttttttttttttttttttttttttttttt>>() =>
                            1,
                        _ => 0
                    };
                }

                void M2() {
                    return state switch {
                        Tyyyyyyyyyyyyyyy when Materialise<List<bool>, IReadOnlyDictionary<int, Tttttttttttttttttttttttttttttttt>>()
                            => 1,
                        _ => 0
                    };
                }

                void M3() {
                    if (c) {
                        if (c) {
                            return state switch {
                                not null when Materialise<List<bool>, IReadOnlyDictionary<int, Tttttttttttttttttttttttttt>>() =>
                                    "ss",
                                _ => 0
                            };
                        }
                    }
                }

                void M4() {
                    if (c) {
                        if (c) {
                            return state switch {
                                byte when Materialise<List<bool>, IReadOnlyDictionary<int,
                                    Ttttttttttttttttttttttttttttttttttttttttttttttttttttttttt>>() => 'c',
                                _ => 0
                            };
                        }
                    }
                }

                void M5() {
                    return state switch {
                        Tyyyyyyyyyyyyyyy when Materialise<List<bool>,
                            IReadOnlyDictionary<int, Ttttttttttttttttttttttttttttttttttttttttttttttttttttt>>() => 1,
                        _ => 0
                    };
                }
            }
            """
        );
}
