namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     Issue #598: an <c>=</c> whose value opens with a parenthesised expression breaks when that expression's
///     <c>)</c> lands exactly one column past the margin, and the parentheses break inside at every other width.
///     Every expected string is <c>jb cleanupcode</c>'s own output, measured 2026-10-10 with <c>Testing ask</c>
///     on 1 860 rows (see <c>BreakPlan.ParenCloseEndOf</c>).
/// </summary>
public sealed class ParenOnePastTheMarginIssue598Tests {
    static readonly string B95 = "B" + new string('b', 94);
    static readonly string B96 = "B" + new string('b', 95);
    static readonly string B97 = "B" + new string('b', 96);
    static readonly string B88 = "B" + new string('b', 87);
    static readonly string B99 = "B" + new string('b', 98);
    static readonly string L88 = new('b', 88);
    static readonly string L94 = new('b', 94);

    /// <summary>
    ///     Before the fix: <c>(aaaa</c> / <c>+ B…);</c> at 122 columns too. 121 and 123 are the controls, and
    ///     <c>+=</c> breaks inside at 122.
    /// </summary>
    [Fact]
    public void AParenthesisClosingOnColumn121_BreaksTheEquals() =>
        Oracle.Agrees(
            $$"""
              class C {
                  void M3() {
                      var x = (aaaa + {{B95}});
                  }

                  void M4() {
                      var x = (aaaa + {{B96}});
                  }

                  void M5() {
                      var x = (aaaa + {{B97}});
                  }

                  void M40() {
                      x += (aaaa + {{B99}});
                  }
              }
              """,
            $$"""
              class C {
                  void M3() {
                      var x = (aaaa
                          + {{B95}});
                  }

                  void M4() {
                      var x =
                          (aaaa + {{B96}});
                  }

                  void M5() {
                      var x = (aaaa
                          + {{B97}});
                  }

                  void M40() {
                      x += (aaaa
                          + {{B99}});
                  }
              }
              """
        );

    /// <summary>A cast, a member access after the <c>)</c>, a field, a <c>!</c> and a binary operand after it.</summary>
    [Fact]
    public void TheSameBehindACastAMemberAPrefixAndBeforeAnOperator() =>
        Oracle.Agrees(
            $$"""
              class C {
                  void M10() {
                      var x = (string)(aaaa + {{B88}});
                  }

                  void M24() {
                      var x = (aaaa + {{B96}}).L;
                  }

                  void M28() {
                      _field = (aaaa + {{B95}});
                  }

                  void M44() {
                      var x = !(aaaa && {{L94}});
                  }

                  void M41() {
                      var x = (string)(aaaa + {{L88}}) + c;
                  }
              }
              """,
            $$"""
              class C {
                  void M10() {
                      var x =
                          (string)(aaaa + {{B88}});
                  }

                  void M24() {
                      var x =
                          (aaaa + {{B96}}).L;
                  }

                  void M28() {
                      _field =
                          (aaaa + {{B95}});
                  }

                  void M44() {
                      var x =
                          !(aaaa && {{L94}});
                  }

                  void M41() {
                      var x =
                          (string)(aaaa + {{L88}})
                          + c;
                  }
              }
              """
        );
}
