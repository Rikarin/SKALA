namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     Round seven of #576 (SK-DIV-0399): the band of head widths in which the <c>when</c> breaks before a body that
///     is not read through, where that band ends, the outer type argument list under a broken <c>when</c>, and a
///     member-access body's own dot. Every expected string is <c>jb cleanupcode</c>'s own output, measured 2026-10-14
///     with <c>Testing ask</c>.
/// </summary>
public sealed class WhenBandIssue576Tests {
    /// <summary>
    ///     Before a body of four columns or more with its comma the <c>when</c> breaks while the head through it is
    ///     within the band (here 21 columns): the arrow then breaks before the body. Past the band's limit (38 columns)
    ///     the type arguments fill beside the <c>when</c>. Before the fix both filled the type arguments.
    /// </summary>
    [Fact]
    public void ABodyNotReadThrough_BreaksTheWhenInsideTheBand() =>
        Oracle.Agrees(
            """
            class C {
                object M1() {
                    return state switch { Tyyyyyyyyyyyyyyy when Materialise<List<bool>, IReadOnlyDictionary<int, Ttttttttttttttttttttttttttttttttttttttttttttttt>>() => "ss", _ => 0 };
                }

                object M2() {
                    return state switch { Tyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyy when Materialise<List<bool>, IReadOnlyDictionary<int, Ttttttttttttttttttttttttttttttttttttttttttttttt>>() => "ss", _ => 0 };
                }
            }
            """,
            """
            class C {
                object M1() {
                    return state switch {
                        Tyyyyyyyyyyyyyyy when
                            Materialise<List<bool>, IReadOnlyDictionary<int, Ttttttttttttttttttttttttttttttttttttttttttttttt>>() =>
                            "ss",
                        _ => 0
                    };
                }

                object M2() {
                    return state switch {
                        Tyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyy when Materialise<List<bool>,
                            IReadOnlyDictionary<int, Ttttttttttttttttttttttttttttttttttttttttttttttt>>() => "ss",
                        _ => 0
                    };
                }
            }
            """
        );

    /// <summary>
    ///     Below a broken <c>when</c> the outer type argument list breaks after its first comma only while its second
    ///     argument fits a level past the condition's line with fourteen columns to spare (113 fills the inner list); a
    ///     condition that would run more than 13 columns past the margin on the line below stays beside the <c>when</c>
    ///     (118). Before the fix the first broke the outer list, the second broke the <c>when</c>.
    /// </summary>
    [Fact]
    public void TheBandEndsThirteenColumnsPastTheMargin_AndTheOuterListReadsTheLiftedContinuation() =>
        Oracle.Agrees(
            """
            class C {
                object M3() {
                    return state switch { Tyyyyyyyyyyyyyyy when Materialise<List<bool>, IReadOnlyDictionary<int, Tttttttttttttttttttttttttttttttttttttttttttttttttttttttttttt>>() => Bbbb, _ => 0 };
                }

                object M4() {
                    return state switch { Tyyyyyyyyyyyyyyy when Materialise<List<bool>, IReadOnlyDictionary<int, Ttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttt>>() => Bbbb, _ => 0 };
                }
            }
            """,
            """
            class C {
                object M3() {
                    return state switch {
                        Tyyyyyyyyyyyyyyy when
                            Materialise<List<bool>, IReadOnlyDictionary<int,
                                Tttttttttttttttttttttttttttttttttttttttttttttttttttttttttttt>>() => Bbbb,
                        _ => 0
                    };
                }

                object M4() {
                    return state switch {
                        Tyyyyyyyyyyyyyyy when Materialise<List<bool>, IReadOnlyDictionary<int,
                            Ttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttt>>() => Bbbb,
                        _ => 0
                    };
                }
            }
            """
        );

    /// <summary>
    ///     <c>2·name + 3·receiver ≥ 22</c>: <c>=&gt; Aaaaa</c> / <c>.Bbbbbbbb,</c> behind a plain head and behind a
    ///     property pattern, where Skala broke the arrow or chopped the braces; a receiver line that does not fit chops
    ///     the braces as before, and a body too long to be read through breaks after the arrow.
    /// </summary>
    [Fact]
    public void AMemberAccessBody_BreaksAtItsOwnDot() =>
        Oracle.Agrees(
            """
            class C {
                object M5() {
                    return state switch { Tyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyy => Aaaaa.Bbbbbbbb, _ => 0 };
                }

                object M6() {
                    return state switch { Kinddddd { Alpha: 1, Beta: 2, Gamma: 3, Pp: Vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv } => Aaaaa.Bbbbbb, _ => 0 };
                }

                object M7() {
                    return state switch { Kinddddd { Alpha: 1, Beta: 2, Gamma: 3, Pp: Vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv } => Aaaaaaa.Bb, _ => 0 };
                }

                object M8() {
                    if (c) {
                        return state switch { Aaaa or Bbbb or Ccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc => Rrrrrrrrrr.Nnnnnnnn, _ => 0 };
                    }
                }
            }
            """,
            """
            class C {
                object M5() {
                    return state switch {
                        Tyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyy => Aaaaa
                            .Bbbbbbbb,
                        _ => 0
                    };
                }

                object M6() {
                    return state switch {
                        Kinddddd { Alpha: 1, Beta: 2, Gamma: 3, Pp: Vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv } => Aaaaa
                            .Bbbbbb,
                        _ => 0
                    };
                }

                object M7() {
                    return state switch {
                        Kinddddd {
                            Alpha: 1, Beta: 2, Gamma: 3, Pp: Vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv
                        } => Aaaaaaa.Bb,
                        _ => 0
                    };
                }

                object M8() {
                    if (c) {
                        return state switch {
                            Aaaa or Bbbb or Ccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc =>
                                Rrrrrrrrrr.Nnnnnnnn,
                            _ => 0
                        };
                    }
                }
            }
            """
        );

    /// <summary>
    ///     Under a <c>when</c> Skala breaks itself the dot is a level past the condition's line, on both passes (the
    ///     first cut put it on the condition's own column on pass two).
    /// </summary>
    [Fact]
    public void ADotUnderABrokenWhen_KeepsItsLevelOnPassTwo() =>
        Oracle.Agrees(
            """
            class C {
                object M9() {
                    return state switch { Tyyyyyyyyyyyyyyy when Materialise<List<bool>, IReadOnlyDictionary<int, Ttttttttttttttttttttttttttttttttttt>>() => Short.Value12, _ => 0 };
                }
            }
            """,
            """
            class C {
                object M9() {
                    return state switch {
                        Tyyyyyyyyyyyyyyy when
                            Materialise<List<bool>, IReadOnlyDictionary<int, Ttttttttttttttttttttttttttttttttttt>>() => Short
                                .Value12,
                        _ => 0
                    };
                }
            }
            """
        );
}
