using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Core.Configuration;
using static Rikarin.Skala.Formatting.CSharp.Tests.TestText;

namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     An <c>=</c> past the margin decided by the name it assigns rather than by its head (#577, #579, #589, #590,
///     SK-DIV-0400 to SK-DIV-0403).
/// </summary>
/// <remarks>
///     ⚠ Every expected text is the oracle's own answer, measured 2026-10-09 with <c>Testing ask</c> on the very rows
///     below: boundary rows of the grids the rules were fitted on and rows the previous rules got wrong, each in its
///     own method so that no row's layout leans on another's.
/// </remarks>
public sealed partial class EqualsByTheNameTests {
    static string FormatWith(string source) {
        var options = new PhaseOneOptions(
            OptionResolver.Resolve(Path.Combine(Rikarin.Skala.Testing.Corpus.RepositoryRoot, "Test.cs"), []).Options
        );

        return CSharpFormatter.Format("Test.cs", SourceText.From(source), options).Formatted;
    }

    /// <summary>#589: a call with two or more arguments — the name and the callee against where the <c>(</c> lands.</summary>
    static readonly string CallsSource = $$"""
                                           class C {
                                               void M0() {
                                                   T{{R('y', 63)}} vwww = Compute(alphaValue, {{R('d', 32)}});
                                               }
                                               void M1() {
                                                   T{{R('y', 47)}} vwwwwwwwwwww = Compute(alphaValue, {{R('d', 36)}});
                                               }
                                               void M2() {
                                                   T{{R('y', 43)}} v{{R('w', 23)}} = Compute(alphaValue, {{R('d', 26)}});
                                               }
                                               void M3() {
                                                   T{{R('y', 71)}} vwwwwwww = Compute(alphaValue, {{R('d', 20)}});
                                               }
                                               void M4() {
                                                   T{{R('y', 35)}} v{{R('w', 15)}} = Compute(alphaValue, {{R('d', 38)}});
                                               }
                                               void M5() {
                                                   Tyyyyyyyyyyy v{{R('w', 39)}} = Compute(alphaValue, {{R('d', 36)}});
                                               }
                                               void M6() {
                                                   T{{R('y', 87)}} vwwwwwwwwwww = Compute(alphaValue, dd);
                                               }
                                               void M7() {
                                                   T{{R('y', 31)}} v{{R('w', 55)}} = Compute(alphaValue, {{R('d', 14)}});
                                               }
                                               void M8() {
                                                   Tyyy v{{R('w', 47)}} = Compute(alphaValue, {{R('d', 36)}});
                                               }
                                               void M9() {
                                                   T{{R('y', 75)}} vwww = Compute(alphaValue, dddddddd);
                                               }
                                               void M10() {
                                                   T{{R('y', 72)}} vwwwwww = Compute(alphaValue, dddddddd);
                                               }
                                               void M11() {
                                                   T{{R('y', 30)}} v{{R('w', 24)}} = Compute(alphaValue, {{R('d', 34)}});
                                               }
                                               void M12() {
                                                   T{{R('y', 33)}} v{{R('w', 33)}} = Compute(alphaValue, {{R('d', 32)}});
                                               }
                                               void M13() {
                                                   T v{{R('w', 54)}} = Compute(alphaValue, {{R('d', 32)}});
                                               }
                                               void M14() {
                                                   Tyyyyyyyyy v{{R('w', 33)}} = Compute(alphaValue, {{R('d', 56)}});
                                               }
                                               void M15() {
                                                   T{{R('y', 57)}} vw = Compute(alphaValue, {{R('d', 32)}});
                                               }
                                               void M16() {
                                                   T{{R('y', 83)}} vwwwwwww = Compute(alphaValue, {{R('d', 14)}});
                                               }
                                               void M17() {
                                                   T{{R('y', 83)}} vwwwwwww = Compute({{R('a', 12)}});
                                               }
                                               void M18() {
                                                   T{{R('y', 27)}} v{{R('w', 23)}} = Compute(alphaValue, {{R('d', 26)}});
                                               }
                                               void M19() {
                                                   T{{R('y', 43)}} v{{R('w', 23)}} = Compute(alphaValue, {{R('d', 22)}});
                                               }
                                           }
                                           """;

    /// <summary>#590: a plain member value — the dot fill's fragment against the name, and the limit below.</summary>
    static readonly string PlainMembersSource = $$"""
                                                  class C {
                                                      void M0() {
                                                          T{{R('y', 79)}} v = cxxxxxxxxx.A{{R('a', 29)}}.B{{R('b', 50)}};
                                                      }
                                                      void M1() {
                                                          T{{R('y', 67)}} v = cxx.Aaa.B{{R('b', 85)}};
                                                      }
                                                      void M2() {
                                                          T{{R('y', 83)}} v = cxxxxxxxx.A{{R('a', 29)}}.B{{R('b', 50)}};
                                                      }
                                                      void M3() {
                                                          T{{R('y', 99)}} v = cxx.Aa.B{{R('b', 80)}};
                                                      }
                                                      void M4() {
                                                          T{{R('y', 35)}} v = cxx.A.B{{R('b', 92)}};
                                                      }
                                                      void M5() {
                                                          T{{R('y', 39)}} v = cxx.A.B{{R('b', 98)}};
                                                      }
                                                      void M6() {
                                                          T{{R('y', 95)}} v = cxx.Aa.B{{R('b', 91)}};
                                                      }
                                                      void M7() {
                                                          T{{R('y', 43)}} v = cxx.A.B{{R('b', 91)}};
                                                      }
                                                      void M8() {
                                                          T{{R('y', 61)}} vwwwwwwwwww = cxxxx.A{{R('a', 17)}}.B{{R('b', 54)}};
                                                      }
                                                      void M9() {
                                                          T{{R('y', 75)}} vww = cxxxx.A{{R('a', 15)}}.B{{R('b', 56)}};
                                                      }
                                                      void M10() {
                                                          T{{R('y', 69)}} vww = cxxxx.Aaaaaaaaaaaa.B{{R('b', 60)}};
                                                      }
                                                      void M11() {
                                                          T{{R('y', 26)}} vw = cxxxx.A{{R('a', 23)}}.B{{R('b', 48)}};
                                                      }
                                                      void M12() {
                                                          T v{{R('w', 27)}} = cxxxx.A{{R('a', 13)}}.B{{R('b', 68)}};
                                                      }
                                                      void M13() {
                                                          T v{{R('w', 43)}} = cxxxx.A{{R('a', 13)}}.B{{R('b', 85)}};
                                                      }
                                                      void M14() {
                                                          T{{R('y', 16)}} v{{R('w', 29)}} = cxxxx.A{{R('a', 19)}}.B{{R('b', 64)}};
                                                      }
                                                  }
                                                  """;

    /// <summary>#577: a conditional whose condition fits beside the <c>=</c>, under names of every width.</summary>
    static readonly string ConditionalsSource = $$"""
                                                  class C {
                                                      void M0() {
                                                          T{{R('y', 61)}} vww = flllllllll ? {{R('a', 40)}} : {{R('b', 40)}};
                                                      }
                                                      void M1() {
                                                          T{{R('y', 71)}} v = f{{R('l', 19)}} ? {{R('a', 27)}} : {{R('b', 27)}};
                                                      }
                                                      void M2() {
                                                          T{{R('y', 85)}} vww = flag ? {{R('a', 40)}} : {{R('b', 40)}};
                                                      }
                                                      void M3() {
                                                          T{{R('y', 76)}} vwwwwwwwwwww = flag ? {{R('a', 45)}} : {{R('b', 45)}};
                                                      }
                                                      void M4() {
                                                          T{{R('y', 39)}} v = f{{R('l', 19)}} ? {{R('a', 32)}} : {{R('b', 32)}};
                                                      }
                                                      void M5() {
                                                          T{{R('y', 47)}} v = f{{R('l', 19)}} ? {{R('a', 31)}} : {{R('b', 31)}};
                                                      }
                                                      void M6() {
                                                          T{{R('y', 82)}} vwwwww = flllllllll ? {{R('a', 39)}} : {{R('b', 39)}};
                                                      }
                                                      void M7() {
                                                          T{{R('y', 55)}} v = f{{R('l', 19)}} ? {{R('a', 26)}} : {{R('b', 26)}};
                                                      }
                                                      void M8() {
                                                          T{{R('y', 77)}} vww = flag ? {{R('a', 39)}} : {{R('b', 39)}};
                                                      }
                                                      void M9() {
                                                          T{{R('y', 52)}} vwwwwwwwwwww = f{{R('l', 19)}} ? {{R('a', 30)}} : {{R('b', 30)}};
                                                      }
                                                      void M10() {
                                                          T{{R('y', 39)}} v = f{{R('l', 28)}} ? {{R('a', 26)}} : {{R('b', 27)}};
                                                      }
                                                      void M11() {
                                                          T{{R('y', 74)}} vwwwww = flllllllll ? {{R('a', 39)}} : {{R('b', 39)}};
                                                      }
                                                      void M12() {
                                                          Tyyyyyyyyyy vwwwww = f{{R('l', 28)}} ? {{R('a', 35)}} : {{R('b', 36)}};
                                                      }
                                                      void M13() {
                                                          T{{R('y', 39)}} v = flag ? {{R('a', 44)}} : {{R('b', 44)}};
                                                      }
                                                      void M14() {
                                                          T{{R('y', 71)}} v = flag ? {{R('a', 48)}} : {{R('b', 48)}};
                                                      }
                                                      void M15() {
                                                          T{{R('y', 44)}} vwwwwwwwwwww = f{{R('l', 28)}} ? {{R('a', 17)}} : {{R('b', 18)}};
                                                      }
                                                      void M16() {
                                                          T{{R('y', 36)}} vwwwwwwwwwww = f{{R('l', 28)}} ? {{R('a', 29)}} : {{R('b', 30)}};
                                                      }
                                                      void M17() {
                                                          T{{R('y', 55)}} v = flllllllll ? {{R('a', 41)}} : {{R('b', 41)}};
                                                      }
                                                      void M18() {
                                                          T{{R('y', 26)}} vwwwww = flllllllll ? {{R('a', 41)}} : {{R('b', 41)}};
                                                      }
                                                      void M19() {
                                                          T{{R('y', 71)}} v = flllllllll ? {{R('a', 27)}} : {{R('b', 27)}};
                                                      }
                                                      void M20() {
                                                          T{{R('y', 40)}} v{{R('w', 23)}} = flllllllll ? {{R('a', 42)}} : {{R('b', 42)}};
                                                      }
                                                      void M21() {
                                                          T{{R('y', 44)}} vwwwwwwwwwww = flllll ? {{R('a', 43)}} : {{R('b', 43)}};
                                                      }
                                                      void M22() {
                                                          T{{R('y', 47)}} v = f{{R('l', 28)}} ? {{R('a', 35)}} : {{R('b', 36)}};
                                                      }
                                                      void M23() {
                                                          T{{R('y', 48)}} v{{R('w', 23)}} = f{{R('l', 19)}} ? {{R('a', 35)}} : {{R('b', 35)}};
                                                      }
                                                  }
                                                  """;

    /// <summary>#579: <c>X || Y</c> with <c>X</c> too wide beside the <c>=</c> — the head floor a wide <c>Y</c> lowers.</summary>
    static readonly string OrsSource = $$"""
                                         class C {
                                             void M0() {
                                                 var vvvvvvv = token.Kind() is SyntaxKind.Kxxxxxxx or SyntaxKind.Kxxxxxxx or SyntaxKind.Kxxxxxxx or SyntaxKind.Kxxxxxxx or SyntaxKind.Kxxxxxxx or Q || {{R('y', 76)}};
                                             }
                                             void M1() {
                                                 T vvvvvvvvvvv = cxxxxxxxxxxx && cxxxxxxxxxxx && cxxxxxxxxxxx && cxxxxxxxxxxx && cxxxxxxxxxxx && cxxxxxxxxxxx && cxxxxxxxxxxx && z || {{R('y', 60)}};
                                             }
                                             void M2() {
                                                 var vvvvvvvvvv = cxxxxxxxxxxx && cxxxxxxxxxxx && cxxxxxxxxxxx && cxxxxxxxxxxx && cxxxxxxxxxxx && cxxxxxxxxxxx && cxxxxxxxxxxx && cxxxxxxxxxxx && z || {{R('y', 100)}};
                                             }
                                             void M3() {
                                                 T vvvvvvv = token.Kind() is SyntaxKind.Kxxxxxxx or SyntaxKind.Kxxxxxxx or SyntaxKind.Kxxxxxxx or SyntaxKind.Kxxxxxxx or QQQQQQQ || {{R('y', 92)}};
                                             }
                                             void M4() {
                                                 T vvvvvvvv = token.Kind() is SyntaxKind.Kxxxxxxx or SyntaxKind.Kxxxxxxx or SyntaxKind.Kxxxxxxx or SyntaxKind.Kxxxxxxx or QQQQQ || {{R('y', 93)}};
                                             }
                                             void M5() {
                                                 {{R('v', 12)}} = cxxxxxxxxxxx && cxxxxxxxxxxx && cxxxxxxxxxxx && cxxxxxxxxxxx && cxxxxxxxxxxx && cxxxxxxxxxxx && zzzz || {{R('y', 100)}};
                                             }
                                             void M6() {
                                                 T vvvvvvvvv = cxxxxxxxxxxx && cxxxxxxxxxxx && cxxxxxxxxxxx && cxxxxxxxxxxx && cxxxxxxxxxxx && cxxxxxxxxxxx && cxxxxxxxxxxx && cxxxxxxxxxxx && z || {{R('y', 92)}};
                                             }
                                             void M7() {
                                                 var vvvvvvvvv = token.Kind() is SyntaxKind.Kxxxxxxx or SyntaxKind.Kxxxxxxx or SyntaxKind.Kxxxxxxx or {{R('Q', 15)}} || {{R('y', 74)}};
                                             }
                                             void M8() {
                                                 {{R('v', 13)}} = token.Kind() is SyntaxKind.Kxxxxxxx or SyntaxKind.Kxxxxxxx or SyntaxKind.Kxxxxxxx or SyntaxKind.Kxxxxxxx or QQQQQQ || {{R('y', 93)}};
                                             }
                                             void M9() {
                                                 T vvvvvvvv = cxxxxxxxxxxx && cxxxxxxxxxxx && cxxxxxxxxxxx && cxxxxxxxxxxx && cxxxxxxxxxxx && cxxxxxxxxxxx && zzzz || {{R('y', 93)}};
                                             }
                                             void M10() {
                                                 vvvvvvv = cxxxxxxxxxxx && cxxxxxxxxxxx && cxxxxxxxxxxx && cxxxxxxxxxxx && cxxxxxxxxxxx && cxxxxxxxxxxx && cxxxxxxxxxxx && cxxxxxxxxxxx && z || {{R('y', 100)}};
                                             }
                                             void M11() {
                                                 T {{R('v', 12)}} = token.Kind() is SyntaxKind.Kxxxxxxx or SyntaxKind.Kxxxxxxx or SyntaxKind.Kxxxxxxx or SyntaxKind.Kxxxxxxx or QQQQQQ || flag;
                                             }
                                             void M12() {
                                                 T vvvv = token.Kind() is SyntaxKind.Kxxxxxxx or SyntaxKind.Kxxxxxxx or SyntaxKind.Kxxxxxxx or SyntaxKind.Kxxxxxxx or QQQQQQQQ || {{R('y', 80)}};
                                             }
                                             void M13() {
                                                 T vvvvvv = token.Kind() is SyntaxKind.Kxxxxxxx or SyntaxKind.Kxxxxxxx or SyntaxKind.Kxxxxxxx or SyntaxKind.Kxxxxxxx or QQ || flag;
                                             }
                                             void M14() {
                                                 var vvvvvvvvv = token.Kind() is SyntaxKind.Kxxxxxxx or SyntaxKind.Kxxxxxxx or SyntaxKind.Kxxxxxxx or SyntaxKind.Kxxxxxxx or {{R('Q', 12)}} || flag;
                                             }
                                             void M15() {
                                                 var vvvvvvvv = token.Kind() is SyntaxKind.Kxxxxxxx or SyntaxKind.Kxxxxxxx or SyntaxKind.Kxxxxxxx or SyntaxKind.Kxxxxxxx or {{R('Q', 12)}} || {{R('y', 23)}};
                                             }
                                             void M16() {
                                                 T vvvvv = token.Kind() is SyntaxKind.Kxxxxxxx or SyntaxKind.Kxxxxxxx or SyntaxKind.Kxxxxxxx or SyntaxKind.Kxxxxxxx or QQQQQQQQ || {{R('y', 100)}};
                                             }
                                             void M17() {
                                                 var vv = cxxxxxxxxxxx && cxxxxxxxxxxx && cxxxxxxxxxxx && cxxxxxxxxxxx && cxxxxxxxxxxx && cxxxxxxxxxxx && cxxxxxxxxxxx && z || {{R('y', 80)}};
                                             }
                                             void M18() {
                                                 T vvvvvv = token.Kind() is SyntaxKind.Kxxxxxxx or SyntaxKind.Kxxxxxxx or SyntaxKind.Kxxxxxxx or SyntaxKind.Kxxxxxxx or QQQQQ || {{R('y', 60)}};
                                             }
                                             void M19() {
                                                 vvvvvvvv = token.Kind() is SyntaxKind.Kxxxxxxx or SyntaxKind.Kxxxxxxx or SyntaxKind.Kxxxxxxx or SyntaxKind.Kxxxxxxx or QQQQQQQ || {{R('y', 100)}};
                                             }
                                         }
                                         """;

    [Fact]
    public void Calls_ComeBackAsTheOracleWritesThem() {
        var formatted = FormatWith(CallsSource);
        Assert.Equal(CallsOracle + "\n", formatted);
        Assert.Equal(formatted, FormatWith(formatted));
    }

    [Fact]
    public void PlainMembers_ComeBackAsTheOracleWritesThem() {
        var formatted = FormatWith(PlainMembersSource);
        Assert.Equal(PlainMembersOracle + "\n", formatted);
        Assert.Equal(formatted, FormatWith(formatted));
    }

    [Fact]
    public void Conditionals_ComeBackAsTheOracleWritesThem() {
        var formatted = FormatWith(ConditionalsSource);
        Assert.Equal(ConditionalsOracle + "\n", formatted);
        Assert.Equal(formatted, FormatWith(formatted));
    }

    [Fact]
    public void Ors_ComeBackAsTheOracleWritesThem() {
        var formatted = FormatWith(OrsSource);
        Assert.Equal(OrsOracle + "\n", formatted);
        Assert.Equal(formatted, FormatWith(formatted));
    }
}
