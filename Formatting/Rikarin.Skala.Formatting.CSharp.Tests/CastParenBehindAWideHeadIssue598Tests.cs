namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     Round six of #598: <c>(T)(a + b…).M()</c> behind a head of twelve columns or more, a typed local's, a
///     <c>var</c>'s or an assignment's. Every expected string is <c>jb cleanupcode</c>'s own output, measured
///     2026-10-14 with <c>Testing ask</c> (SK-DIV-0575).
/// </summary>
public sealed class CastParenBehindAWideHeadIssue598Tests {
    static string B(int n) => "B" + new string('b', n - 1);

    /// <summary>
    ///     One column over the dot takes the break; two or three over the <c>=</c> does; past that the dot again while
    ///     the receiver fits beside the <c>=</c>, or the <c>=</c> while the measured comparison keeps the value below
    ///     whole; then the <c>=</c> and the dot; a long first operand keeps the <c>=</c> and breaks inside; a receiver
    ///     too wide below breaks inside and at its dot. Before the fix every one but the fifth broke inside the
    ///     parentheses beside the <c>=</c>.
    /// </summary>
    [Fact]
    public void ACastOverAParenthesisedCall_BehindAWideHead_ComparesItsLayouts() =>
        Oracle.Agrees(
            $$"""
              class C {
                  void M1() {
                      string value = (T)(aaaa + {{B(81)}}).M();
                  }

                  void M2() {
                      string value = (T)(aaaa + {{B(82)}}).M();
                  }

                  void M3() {
                      string value = (T)(aaaa + {{B(84)}}).M();
                  }

                  void M4() {
                      string value = (T)(aaaa + {{B(93)}}).M();
                  }

                  void M5() {
                      string value = (T)(aaaaaaaaaaaaaaaaaaaaaaaaaaaa + {{B(66)}}).M();
                  }

                  void M6() {
                      string value = (T)(aa + {{B(101)}}).M();
                  }

                  void M7() {
                      var valueeeeeeee = (string)(aaaa + {{B(63)}}).ToUpperInvariant();
                  }

                  void M8() {
                      var valueeeeeeee = (string)(aaaa + {{B(65)}}).ToUpperInvariant();
                  }

                  void M9() {
                      Dictionary<int, string> vvvvv = (List<int>)(aaaa + {{B(89)}}).M();
                  }
              }
              """,
            $$"""
              class C {
                  void M1() {
                      string value = (T)(aaaa + {{B(81)}})
                          .M();
                  }

                  void M2() {
                      string value =
                          (T)(aaaa + {{B(82)}}).M();
                  }

                  void M3() {
                      string value = (T)(aaaa + {{B(84)}})
                          .M();
                  }

                  void M4() {
                      string value =
                          (T)(aaaa + {{B(93)}})
                          .M();
                  }

                  void M5() {
                      string value = (T)(aaaaaaaaaaaaaaaaaaaaaaaaaaaa
                          + {{B(66)}}).M();
                  }

                  void M6() {
                      string value =
                          (T)(aa
                              + {{B(101)}})
                          .M();
                  }

                  void M7() {
                      var valueeeeeeee =
                          (string)(aaaa + {{B(63)}}).ToUpperInvariant();
                  }

                  void M8() {
                      var valueeeeeeee = (string)(aaaa + {{B(65)}})
                          .ToUpperInvariant();
                  }

                  void M9() {
                      Dictionary<int, string> vvvvv =
                          (List<int>)(aaaa
                              + {{B(89)}}).M();
                  }
              }
              """
        );
}
