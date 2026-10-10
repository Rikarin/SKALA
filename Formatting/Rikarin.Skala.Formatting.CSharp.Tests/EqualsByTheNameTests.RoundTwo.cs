using static Rikarin.Skala.Formatting.CSharp.Tests.TestText;

namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <remarks>
///     Round 2 (2026-10-10): rows drawn from a probe made fresh for the round-two rules, each the oracle's own answer.
/// </remarks>
public sealed partial class EqualsByTheNameTests {
    /// <summary>
    ///     Round 2: a conditional behind a name of six or more past an <c>=</c> at column 84 takes the member value's
    ///     limit too.
    /// </summary>
    static readonly string RoundTwoConditionalsSource = $$"""
                                                          class C {
                                                              void M0() {
                                                                  T{{R('y', 78)}} v{{R('w', 14)}} = flllllllll ? {{R('a', 42)}} : {{R('b', 42)}};
                                                              }
                                                              void M1() {
                                                                  T{{R('y', 65)}} v{{R('w', 21)}} = flll ? {{R('a', 47)}} : {{R('b', 47)}};
                                                              }
                                                              void M2() {
                                                                  T{{R('y', 89)}} vw = cx.Aaaaaaaaaaa.B{{R('b', 80)}};
                                                              }
                                                              void M3() {
                                                                  T{{R('y', 50)}} v{{R('w', 20)}} = f{{R('l', 20)}} ? {{R('a', 32)}} : {{R('b', 33)}};
                                                              }
                                                              void M4() {
                                                                  T{{R('y', 58)}} v{{R('w', 24)}} = f{{R('l', 14)}} ? {{R('a', 37)}} : {{R('b', 37)}};
                                                              }
                                                              void M5() {
                                                                  T{{R('y', 71)}} vwwwww = cxx.Aaaaaaaaaaa.B{{R('b', 84)}};
                                                              }
                                                              void M6() {
                                                                  T{{R('y', 51)}} vwwwwww = cxxx.Aaaaaaaaaa.B{{R('b', 70)}};
                                                              }
                                                              void M7() {
                                                                  T{{R('y', 51)}} vw = cx.Aaaaaaaa.B{{R('b', 79)}};
                                                              }
                                                              void M8() {
                                                                  T{{R('y', 62)}} v = cxxxxxxx.Aaaaaaaa.B{{R('b', 66)}};
                                                              }
                                                              void M9() {
                                                                  T{{R('y', 42)}} v{{R('w', 28)}} = f{{R('l', 17)}} ? {{R('a', 30)}} : {{R('b', 31)}};
                                                              }
                                                              void M10() {
                                                                  T{{R('y', 48)}} vwwwwww = fll ? {{R('a', 39)}} : {{R('b', 40)}};
                                                              }
                                                          }
                                                          """;

    /// <summary>Round 2: a short name behind a fragment of 12 to 15 keeps the limit of 109.</summary>
    static readonly string RoundTwoPlainMembersSource = $$"""
                                                          class C {
                                                              void M0() {
                                                                  T{{R('y', 78)}} v{{R('w', 14)}} = flllllllll ? {{R('a', 42)}} : {{R('b', 42)}};
                                                              }
                                                              void M1() {
                                                                  T{{R('y', 65)}} v{{R('w', 21)}} = flll ? {{R('a', 47)}} : {{R('b', 47)}};
                                                              }
                                                              void M2() {
                                                                  T{{R('y', 66)}} vwwwwww = cxxxxxx.Aa.B{{R('b', 91)}};
                                                              }
                                                              void M3() {
                                                                  T{{R('y', 50)}} v{{R('w', 20)}} = f{{R('l', 20)}} ? {{R('a', 32)}} : {{R('b', 33)}};
                                                              }
                                                              void M4() {
                                                                  T{{R('y', 45)}} v{{R('w', 27)}} = f{{R('l', 17)}} ? {{R('a', 33)}} : {{R('b', 33)}};
                                                              }
                                                              void M5() {
                                                                  T{{R('y', 60)}} v = cxxxx.Aaaaaaa.B{{R('b', 67)}};
                                                              }
                                                              void M6() {
                                                                  T{{R('y', 50)}} vwwwww = cx.Aaaaaaaaaa.B{{R('b', 72)}};
                                                              }
                                                          }
                                                          """;

    /// <summary>Round 2: a field's name of 31 or more, the <c>(</c> at 76 or left: <c>EqualsFloor.LongFieldNameFloor</c>.</summary>
    static readonly string RoundTwoLongFieldNamesSource = $$"""
                                                            class C {
                                                                public Tyyyyyyyy v{{R('w', 33)}} = Compute(alphaValue, {{R('d', 48)}});

                                                                public Tyyyyyyyy v{{R('w', 33)}} = Compute(alphaValue, {{R('d', 50)}});

                                                                public Tyyyyyyyy v{{R('w', 39)}} = Compute(alphaValue, {{R('d', 46)}});

                                                                public Tyyyyyyyy v{{R('w', 39)}} = Compute(alphaValue, {{R('d', 48)}});

                                                                public Tyyy v{{R('w', 47)}} = Compute(alphaValue, {{R('d', 44)}});

                                                                public Tyyy v{{R('w', 47)}} = Compute(alphaValue, {{R('d', 46)}});

                                                                public T{{R('y', 28)}} v{{R('w', 19)}} = Compute(alphaValue, {{R('d', 38)}});

                                                                public T{{R('y', 49)}} v{{R('w', 25)}} = Compute(alphaValue, {{R('d', 18)}});

                                                            }
                                                            """;

    static readonly string RoundTwoConditionalsOracle = $$"""
                                                          class C {
                                                              void M0() {
                                                                  T{{R('y', 78)}} v{{R('w', 14)}} =
                                                                      flllllllll ? {{R('a', 42)}} : {{R('b', 42)}};
                                                              }

                                                              void M1() {
                                                                  T{{R('y', 65)}} v{{R('w', 21)}} =
                                                                      flll ? {{R('a', 47)}} : {{R('b', 47)}};
                                                              }

                                                              void M2() {
                                                                  T{{R('y', 89)}} vw =
                                                                      cx.Aaaaaaaaaaa.B{{R('b', 80)}};
                                                              }

                                                              void M3() {
                                                                  T{{R('y', 50)}} v{{R('w', 20)}} =
                                                                      f{{R('l', 20)}} ? {{R('a', 32)}} : {{R('b', 33)}};
                                                              }

                                                              void M4() {
                                                                  T{{R('y', 58)}} v{{R('w', 24)}} =
                                                                      f{{R('l', 14)}} ? {{R('a', 37)}} : {{R('b', 37)}};
                                                              }

                                                              void M5() {
                                                                  T{{R('y', 71)}} vwwwww = cxx.Aaaaaaaaaaa
                                                                      .B{{R('b', 84)}};
                                                              }

                                                              void M6() {
                                                                  T{{R('y', 51)}} vwwwwww =
                                                                      cxxx.Aaaaaaaaaa.B{{R('b', 70)}};
                                                              }

                                                              void M7() {
                                                                  T{{R('y', 51)}} vw =
                                                                      cx.Aaaaaaaa.B{{R('b', 79)}};
                                                              }

                                                              void M8() {
                                                                  T{{R('y', 62)}} v = cxxxxxxx.Aaaaaaaa
                                                                      .B{{R('b', 66)}};
                                                              }

                                                              void M9() {
                                                                  T{{R('y', 42)}} v{{R('w', 28)}} =
                                                                      f{{R('l', 17)}} ? {{R('a', 30)}} : {{R('b', 31)}};
                                                              }

                                                              void M10() {
                                                                  T{{R('y', 48)}} vwwwwww =
                                                                      fll ? {{R('a', 39)}} : {{R('b', 40)}};
                                                              }
                                                          }
                                                          """;

    static readonly string RoundTwoPlainMembersOracle = $$"""
                                                          class C {
                                                              void M0() {
                                                                  T{{R('y', 78)}} v{{R('w', 14)}} =
                                                                      flllllllll ? {{R('a', 42)}} : {{R('b', 42)}};
                                                              }

                                                              void M1() {
                                                                  T{{R('y', 65)}} v{{R('w', 21)}} =
                                                                      flll ? {{R('a', 47)}} : {{R('b', 47)}};
                                                              }

                                                              void M2() {
                                                                  T{{R('y', 66)}} vwwwwww = cxxxxxx.Aa
                                                                      .B{{R('b', 91)}};
                                                              }

                                                              void M3() {
                                                                  T{{R('y', 50)}} v{{R('w', 20)}} =
                                                                      f{{R('l', 20)}} ? {{R('a', 32)}} : {{R('b', 33)}};
                                                              }

                                                              void M4() {
                                                                  T{{R('y', 45)}} v{{R('w', 27)}} =
                                                                      f{{R('l', 17)}} ? {{R('a', 33)}} : {{R('b', 33)}};
                                                              }

                                                              void M5() {
                                                                  T{{R('y', 60)}} v = cxxxx.Aaaaaaa
                                                                      .B{{R('b', 67)}};
                                                              }

                                                              void M6() {
                                                                  T{{R('y', 50)}} vwwwww =
                                                                      cx.Aaaaaaaaaa.B{{R('b', 72)}};
                                                              }
                                                          }
                                                          """;

    static readonly string RoundTwoLongFieldNamesOracle = $$"""
                                                            class C {
                                                                public Tyyyyyyyy v{{R('w', 33)}} =
                                                                    Compute(alphaValue, {{R('d', 48)}});

                                                                public Tyyyyyyyy v{{R('w', 33)}} = Compute(
                                                                    alphaValue,
                                                                    {{R('d', 50)}}
                                                                );

                                                                public Tyyyyyyyy v{{R('w', 39)}} =
                                                                    Compute(alphaValue, {{R('d', 46)}});

                                                                public Tyyyyyyyy v{{R('w', 39)}} = Compute(
                                                                    alphaValue,
                                                                    {{R('d', 48)}}
                                                                );

                                                                public Tyyy v{{R('w', 47)}} =
                                                                    Compute(alphaValue, {{R('d', 44)}});

                                                                public Tyyy v{{R('w', 47)}} = Compute(
                                                                    alphaValue,
                                                                    {{R('d', 46)}}
                                                                );

                                                                public T{{R('y', 28)}} v{{R('w', 19)}} = Compute(
                                                                    alphaValue,
                                                                    {{R('d', 38)}}
                                                                );

                                                                public T{{R('y', 49)}} v{{R('w', 25)}} =
                                                                    Compute(alphaValue, {{R('d', 18)}});
                                                            }
                                                            """;

    [Fact]
    public void RoundTwoConditionals_ComeBackAsTheOracleWritesThem() {
        var formatted = FormatWith(RoundTwoConditionalsSource);
        Assert.Equal(RoundTwoConditionalsOracle + "\n", formatted);
        Assert.Equal(formatted, FormatWith(formatted));
    }

    [Fact]
    public void RoundTwoPlainMembers_ComeBackAsTheOracleWritesThem() {
        var formatted = FormatWith(RoundTwoPlainMembersSource);
        Assert.Equal(RoundTwoPlainMembersOracle + "\n", formatted);
        Assert.Equal(formatted, FormatWith(formatted));
    }

    [Fact]
    public void RoundTwoLongFieldNames_ComeBackAsTheOracleWritesThem() {
        var formatted = FormatWith(RoundTwoLongFieldNamesSource);
        Assert.Equal(RoundTwoLongFieldNamesOracle + "\n", formatted);
        Assert.Equal(formatted, FormatWith(formatted));
    }
}
