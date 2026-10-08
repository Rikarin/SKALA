using static Rikarin.Skala.Formatting.CSharp.Tests.TestText;

namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     Issue #551: under <c>skala_wrap_chained_method_calls = wrap_if_long</c> a chain whose first call's
///     lambda body broke keeps <c>}</c> / <c>).Where(</c> on one line, and an argument list chopped there
///     nests one level past that line's <c>)</c>. Every expected string is <c>jb cleanupcode</c>'s own
///     output at that value, measured 2026-10-08 with <c>Testing ask</c>.
/// </summary>
public sealed class ClosingLineListIssue551Tests {
    const string Long1 = "}).Where(alphaPredicateValueNumberOneLongerStill).ToList(betaValueArgumentNumber"
        + "TwoLongerStillxxxxxxxxxxxxxxxxxx);";

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
    ///     Before the fix the chopped argument sat at 16 and its <c>)</c> at 12: the list counted the
    ///     <c>=</c>'s and the chain's levels, both opened on the statement's line, though the line it
    ///     opened on starts at 8. The second and third statements are the controls.
    /// </summary>
    [Fact]
    public void AListOpenedOnAClosersLine_NestsFromThatLine() =>
        Assert.Equal(
            Statements(
                $$"""
                          var r2 = source.Select(x => {
                                  A();
                                  return x;
                              }
                          ).Where(
                              alphaPredicateValueNumberOneLongerStill{{R('x', 69)}}
                          );
                          var r = source.Select(x => {
                                      A();
                                      return x;
                                  }
                              ).Where(alphaPredicateValueNumberOneLongerStill)
                              .ToList(betaValueArgumentNumberTwoLongerStillxxxxxxxxxxxxxxxxxx);
                          var s2 = source.Select(
                              alphaArgumentValueNumberOne,
                              betaArgumentValueNumberTwo
                          ).Where(beta);
                  """
            ),
            UnderAFill(
                Statements(
                    $$"""
                              var r2 = source.Select(x => {
                                  A();
                                  return x;
                              }).Where(alphaPredicateValueNumberOneLongerStill{{R('x', 69)}});
                              var r = source.Select(x => {
                                  A();
                                  return x;
                              {{Long1}}
                              var s2 = source.Select(
                                  alphaArgumentValueNumberOne,
                                  betaArgumentValueNumberTwo
                              ).Where(beta);
                      """
                )
            )
        );
}
