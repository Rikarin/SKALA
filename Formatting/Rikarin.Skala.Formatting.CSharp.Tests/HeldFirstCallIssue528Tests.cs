using static Rikarin.Skala.Formatting.CSharp.Tests.TestText;

namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     Issue #528: a chain's held first call breaks too when it does not fit on the receiver's line and
///     its line below ends well short of the margin. Every expected string is <c>jb cleanupcode</c>'s own
///     output, measured 2026-10-08 with <c>Testing ask</c>; <c>constructs/breaks/held-first-call.cs</c>
///     holds the wider set.
/// </summary>
public sealed class HeldFirstCallIssue528Tests {
    const string Long1 = "var x = source.Select(alphaArgumentValueNumberOne, betaArgumentValueNumberTwo, g"
        + "ammaArgumentValuexxxxx)!.Where(beta).ToList();";

    const string Long2 = "var x = source.Select(alphaArgumentValueNumberOne, betaArgumentValueNumberTwo, g"
        + "ammaArgumentValuexxxxx)!";

    /// <summary>Before the fix: <c>….Select(</c> / <c>alpha</c> / <c>)</c> / <c>.Where(b);</c>.</summary>
    [Fact]
    public void AFirstCallThatDoesNotFit_MovesDown() =>
        Oracle.Agrees(
            $$"""
              class T {
                  void M() {
                      var y6 = sourceWithAVeryLongNam{{R('e', 70)}}.Select(alpha).Where(b);
                      {{Long1}}
                  }
              }
              """,
            $$"""
              class T {
                  void M() {
                      var y6 = sourceWithAVeryLongNam{{R('e', 70)}}
                          .Select(alpha)
                          .Where(b);
                      {{Long2}}
                          .Where(beta)
                          .ToList();
                  }
              }
              """
        );

    /// <summary>The control: a call whose line below would be wide is held and its arguments chopped.</summary>
    [Fact]
    public void AWideFirstCall_IsHeldAndChopped() =>
        Oracle.Agrees(
            $$"""
              class T {
                  void M() {
                      var y = {{R('s', 56)}}.Select(aaaaaaaaaaaaaaaaaaa, bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb).Where(b);
                  }
              }
              """,
            """
            class T {
                void M() {
                    var y = ssssssssssssssssssssssssssssssssssssssssssssssssssssssss.Select(
                            aaaaaaaaaaaaaaaaaaa,
                            bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb
                        )
                        .Where(b);
                }
            }
            """
        );

    /// <summary>
    ///     The five grid rows the 76/96 constants held and chopped (round three of #528): two arguments at
    ///     a call line of 82 behind a 60-column head and 78 behind 100, and one argument at 100 to 106
    ///     behind heads of 40 to 60. Each breaks before the call by <c>Fitter.HeldCallLimit</c>.
    /// </summary>
    [Fact]
    public void TheFiveResidueGridRows_BreakBeforeTheCall() =>
        Oracle.Agrees(
            $$"""
              class T {
                  void M() {
                      var y = ssssssssssssssss.Select({{R('b', 85)}}).Where(b);
                      var y = ssssssssssssssssssssssssssssssssssss.Select(aaaaaaaaaaaaaaaaaaa, {{R('b', 40)}}).Where(b);
                      var y = ssssssssssssssssssssssssssssssssssss.Select({{R('b', 79)}}).Where(b);
                      var y = ssssssssssssssssssssssssssssssssssss.Select({{R('b', 85)}}).Where(b);
                      var y = {{R('s', 76)}}.Select(aaaaaaaaaaaaaaaaaaa, bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb).Where(b);
                  }
              }
              """,
            """
            class T {
                void M() {
                    var y = ssssssssssssssss
                        .Select(bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb)
                        .Where(b);
                    var y = ssssssssssssssssssssssssssssssssssss
                        .Select(aaaaaaaaaaaaaaaaaaa, bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb)
                        .Where(b);
                    var y = ssssssssssssssssssssssssssssssssssss
                        .Select(bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb)
                        .Where(b);
                    var y = ssssssssssssssssssssssssssssssssssss
                        .Select(bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb)
                        .Where(b);
                    var y = ssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssss
                        .Select(aaaaaaaaaaaaaaaaaaa, bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb)
                        .Where(b);
                }
            }
            """
        );
}
