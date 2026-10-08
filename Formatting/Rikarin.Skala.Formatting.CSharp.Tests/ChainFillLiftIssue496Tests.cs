namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     Issue #496: under <c>wrap_if_long</c> a block or a list on a chain's first line nests from the
///     chain's continuation line exactly when the fill then takes one of its points, anywhere to its
///     right. Every expected string is <c>jb cleanupcode</c>'s own output at
///     <c>skala_wrap_chained_method_calls = wrap_if_long</c> and
///     <c>skala_wrap_chained_binary_expressions = wrap_if_long</c>, measured 2026-10-08.
/// </summary>
/// <remarks>
///     ⚠ The writer answers it by writing the rest of the chain ahead with the list unlifted and watching
///     the chain's group (<c>LayoutWriter.FillBreaksAfter</c>), and a chain whose author's break the fill
///     pinned lifts outright — so pass two, which reads pass one's fill break as the author's, agrees. The
///     controls, where the fill takes no point, are
///     <c>ChainFirstCallArgumentsIssue418Tests.AFill_KeepsTheOrdinaryLevel</c>.
/// </remarks>
public sealed class ChainFillLiftIssue496Tests {
    const string Long1 = "}).Where(alphaPredicateValueNumberOneLongerStill).ToList(betaValueArgumentNumber"
        + "TwoLongerStillxxxxxxxxxxxxxxxxxx);";

    static string UnderAFill(string source) =>
        Overridden.Settled(
                source,
                [
                    ("skala_wrap_chained_method_calls", "wrap_if_long"),
                    ("skala_wrap_chained_binary_expressions", "wrap_if_long")
                ]
            )
            .TrimEnd('\n');

    /// <summary>
    ///     The fill's one break is a link past the block's (<c>.ToList</c>), and the block lifts; an
    ///     author's break the fill pins lifts the list. Before the fix neither lifted.
    /// </summary>
    [Fact]
    public void AFillThatBreaksAfterTheList_LiftsIt() =>
        Assert.Equal(
            """
            class T {
                void M() {
                    var r = source.Select(x => {
                                A();
                                return x;
                            }
                        ).Where(alphaPredicateValueNumberOneLongerStill)
                        .ToList(betaValueArgumentNumberTwoLongerStillxxxxxxxxxxxxxxxxxx);
                    var s = source.Select(
                            alphaArgumentValueNumberOne,
                            betaArgumentValueNumberTwo
                        )
                        .Where(beta);
                }
            }
            """,
            UnderAFill(
                $$"""
                  class T {
                      void M() {
                          var r = source.Select(x => {
                              A();
                              return x;
                          {{Long1}}
                          var s = source.Select(
                              alphaArgumentValueNumberOne,
                              betaArgumentValueNumberTwo
                          )
                              .Where(beta);
                      }
                  }
                  """
            )
        );
}
