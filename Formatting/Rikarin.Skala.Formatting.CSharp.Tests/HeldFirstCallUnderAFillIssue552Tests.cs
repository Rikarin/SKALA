using static Rikarin.Skala.Formatting.CSharp.Tests.TestText;

namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     Issue #552: under <c>skala_wrap_chained_method_calls = wrap_if_long</c> a chain's held first call
///     that does not fit beside its receiver moves below it past the #528 table's limit when the rest of
///     the chain is long enough: by <c>1.5 · (line − limit) + 9</c> columns of rest, measured on a
///     2496-row grid. Every expected string is <c>jb cleanupcode</c>'s own output at that value, measured
///     2026-10-09 with <c>Testing ask</c>.
/// </summary>
public sealed class HeldFirstCallUnderAFillIssue552Tests {
    const string Long1 = ".Select(alphaArgumentValueNumberOne, betaArgumentValueNumberTwo, gammaArgumentVa"
        + "lueNumberThreeeeeeeeeeeeeee)";

    const string Long2 = "var t1 = source.Select(alphaArgumentValueNumberOne, betaArgumentValueNumberTwo, "
        + "gammaArgumentValueNumberThreeeeeeeeeeeeeee).Where(alphaPredicateValueNumberOneLo"
        + "ngerStill).ToList(betaValueArgumentNumberTwoLonger);";

    const string Long3 = "Outer(first: 1, source.Select(alphaArgumentValueNumberOne, betaArgumentValueNumb"
        + "erTwo, gammaArgumentValueNumberThreeeeeeeeeeeeeee).Where(alpha).ToList(betaaaaaa"
        + "aaaaaaaaaaaaaaaaaaa));";

    static string UnderAFill(string source) =>
        Overridden.Settled(source, [("skala_wrap_chained_method_calls", "wrap_if_long")]).TrimEnd('\n');

    static string Statements(string statements) =>
        $$"""
          class T {
              void M() {
          {{statements}}
              }
          }
          """;

    /// <summary>
    ///     Before the fix the first row held <c>source.Select(</c> and chopped its three arguments. The
    ///     second is the same chain inside an argument list, whose call does not fit below either and is
    ///     held. The last two straddle the boundary at a 60-column head and a 100-column call line: a rest
    ///     of 30 columns holds, 34 breaks.
    /// </summary>
    [Fact]
    public void ALongRest_MovesTheHeldCallDown() =>
        Assert.Equal(
            Statements(
                $$"""
                          var t1 = source
                              {{Long1}}
                              .Where(alphaPredicateValueNumberOneLongerStill).ToList(betaValueArgumentNumberTwoLonger);
                          Outer(
                              first: 1,
                              source.Select(
                                  alphaArgumentValueNumberOne,
                                  betaArgumentValueNumberTwo,
                                  gammaArgumentValueNumberThreeeeeeeeeeeeeee
                              ).Where(alpha).ToList(betaaaaaaaaaaaaaaaaaaaaaaaaa)
                          );
                          var y = ssssssssssssssssssssssssssssssssssss.Select(
                              aaaaaaaaaaaaaaaaaaa,
                              bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb
                          ).Where(wwwwwwwwwwwwwwwwwwwww);
                          var y = ssssssssssssssssssssssssssssssssssss
                              .Select(aaaaaaaaaaaaaaaaaaa, bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb)
                              .Where(wwwwwwwwwwwwwwwwwwwwwwwww);
                  """
            ),
            UnderAFill(
                Statements(
                    $$"""
                              {{Long2}}
                              {{Long3}}
                              var y = {{R('s', 36)}}.Select(aaaaaaaaaaaaaaaaaaa, {{R('b', 58)}}).Where({{R('w', 21)}});
                              var y = {{R('s', 36)}}.Select(aaaaaaaaaaaaaaaaaaa, {{R('b', 58)}}).Where({{R('w', 25)}});
                      """
                )
            )
        );
}
