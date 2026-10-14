namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     Round five of #598 and #576: a single call on a parenthesised receiver, cast or not, behind <c>return</c> and
///     a short <c>=</c>; a doubled parenthesis whose inner <c>)</c> lands one column past the margin; and the body
///     under a width break after an arm's <c>when</c>. Every expected string is <c>jb cleanupcode</c>'s own output,
///     measured 2026-10-14 with <c>Testing ask</c> (SK-DIV-0491, SK-DIV-0399).
/// </summary>
public sealed class CastChainAndWhenLiftIssue598And576Tests {
    static string B(int n) => "B" + new string('b', n - 1);

    /// <summary>
    ///     <c>(T)(a + b…).M()</c> breaks before its dot while the receiver fits; behind <c>var x =</c> the <c>=</c>
    ///     breaks instead on a line two or three columns over, and a receiver past the margin keeps the <c>=</c> and
    ///     breaks inside. Before the fix all five broke inside the parentheses.
    /// </summary>
    [Fact]
    public void ACastCallOnAParenthesisedReceiver_BreaksBeforeItsDot() =>
        Oracle.Agrees(
            $$"""
              class C {
                  void M1() {
                      return (string)(aaaa + {{B(85)}}).Trim();
                  }

                  void M2() {
                      x = (string)(aaaa + {{B(85)}}).Trim();
                  }

                  void M3() {
                      var x = (string)(aaaa + {{B(81)}}).Trim();
                  }

                  void M4() {
                      var x = (string)(aaaa + {{B(83)}}).Trim();
                  }

                  void M5() {
                      var x = (string)(aaaa + {{B(90)}}).Trim();
                  }
              }
              """,
            $$"""
              class C {
                  void M1() {
                      return (string)(aaaa + {{B(85)}})
                          .Trim();
                  }

                  void M2() {
                      x = (string)(aaaa + {{B(85)}})
                          .Trim();
                  }

                  void M3() {
                      var x =
                          (string)(aaaa + {{B(81)}}).Trim();
                  }

                  void M4() {
                      var x = (string)(aaaa + {{B(83)}})
                          .Trim();
                  }

                  void M5() {
                      var x = (string)(aaaa
                          + {{B(90)}}).Trim();
                  }
              }
              """
        );

    /// <summary>
    ///     A doubled parenthesis breaks the <c>=</c> where its inner <c>)</c> lands on column 121 as well as its outer
    ///     one. 124 is the control. Before the fix the first broke inside.
    /// </summary>
    [Fact]
    public void ADoubledParenthesis_ReadsItsInnerClose() =>
        Oracle.Agrees(
            $$"""
              class C {
                  void M5() {
                      var x = ((aaaa + {{B(95)}}));
                  }

                  void M6() {
                      var x = ((aaaa + {{B(96)}}));
                  }
              }
              """,
            $$"""
              class C {
                  void M5() {
                      var x =
                          ((aaaa + {{B(95)}}));
                  }

                  void M6() {
                      var x = ((aaaa
                          + {{B(96)}}));
                  }
              }
              """
        );

    /// <summary>
    ///     Under a width break after the <c>when</c>, the body's arguments and query clauses nest from the condition's
    ///     line, with and without braces in the pattern. Before the fix both sat a level shallower.
    /// </summary>
    [Fact]
    public void AWhenBrokenForWidth_LiftsTheBody() =>
        Oracle.Agrees(
            $$"""
              class C {
                  void M23() {
                      return state switch { DateTime { P25: not null } when Materialise<List<bool>, IReadOnlyDictionary<int, T{{new string('t', 28)}}>>() => Handle(argument0, argument1, argument2, argument3, argument4, argument5), _ => 0 };
                  }

                  void M99() {
                      return state switch { T{{new string('y', 40)}} when Materialise<List<bool>, IReadOnlyDictionary<int, T{{new string('t', 24)}}>>() => (from item in items where "{{new string('q', 20)}}" select item), _ => 0 };
                  }
              }
              """,
            $$"""
              class C {
                  void M23() {
                      return state switch {
                          DateTime { P25: not null } when
                              Materialise<List<bool>, IReadOnlyDictionary<int, T{{new string('t', 28)}}>>() => Handle(
                                  argument0,
                                  argument1,
                                  argument2,
                                  argument3,
                                  argument4,
                                  argument5
                              ),
                          _ => 0
                      };
                  }

                  void M99() {
                      return state switch {
                          T{{new string('y', 40)}} when
                              Materialise<List<bool>, IReadOnlyDictionary<int, T{{new string('t', 24)}}>>() => (from item in items
                                  where "{{new string('q', 20)}}"
                                  select item),
                          _ => 0
                      };
                  }
              }
              """
        );
}
