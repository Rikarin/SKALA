namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     Issue #484: under <c>skala_wrap_chained_method_calls = wrap_if_long</c> a call link keeps its head
///     on the line and chops its arguments unless, moved down, its line ends well short of the margin.
///     Every expected string is <c>jb cleanupcode</c>'s own output at that value, measured 2026-10-08
///     with <c>Testing ask</c>; the 144-row grid behind the widths is in SK-DIV-0129.
/// </summary>
public sealed class ChainFillHeadIssue484Tests {
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
    ///     A last link and a middle one alike keep their head when the link is wide; a narrow link moves
    ///     down. Before the fix every link that fitted on the continuation line moved down.
    /// </summary>
    [Fact]
    public void AWideLink_KeepsItsHead_ANarrowOneMovesDown() =>
        Assert.Equal(
            Statements(
                """
                        var a3 = SomeMethod(aaaaaaaaaaaaaaaaaa, bbbbbbbbbbbbbbbbbbbbb, ccccccccccccccc).Other(
                            ddddddddddddddddddddddddddddddddddd,
                            eeeeeeeeeeeeeeeeeeeeee,
                            ffffffff
                        );
                        var a7 = SomeMethod(aaaaaaaaaaaaaaaaaa, bbbbbbbbbbbbbbbbbbbbb, ccccccccccccccc).Other(
                            ddddddddddddddddddddddddddddddddddd,
                            eeeeeeeeeeeeeeeeeeeeee,
                            ffffffff
                        ).Third(gg);
                        var b5 = alpha.SomeMethod(aaaaaaaaaaaaaaaaaa, bbbbbbbbbbbbbbbbbbbbb).Other(ccccccccccccccc).Third(dddd)
                            .Fourth(eeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee, ff);
                        var b6 = alpha.SomeMethod(aaaaaaaaaaaaaaaaaa, bbbbbbbbbbbbbbbbbbbbb).Other(ccccccccccccccc)
                            .Third(ddddddddddddddddddddddddd, eeeeeeeeeeeeeee).Fourth(ff);
                """
            ),
            UnderAFill(
                Statements(
                    """
                            var a3 = SomeMethod(aaaaaaaaaaaaaaaaaa, bbbbbbbbbbbbbbbbbbbbb, ccccccccccccccc).Other(ddddddddddddddddddddddddddddddddddd, eeeeeeeeeeeeeeeeeeeeee, ffffffff);
                            var a7 = SomeMethod(aaaaaaaaaaaaaaaaaa, bbbbbbbbbbbbbbbbbbbbb, ccccccccccccccc).Other(ddddddddddddddddddddddddddddddddddd, eeeeeeeeeeeeeeeeeeeeee, ffffffff).Third(gg);
                            var b5 = alpha.SomeMethod(aaaaaaaaaaaaaaaaaa, bbbbbbbbbbbbbbbbbbbbb).Other(ccccccccccccccc).Third(dddd).Fourth(eeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee, ff);
                            var b6 = alpha.SomeMethod(aaaaaaaaaaaaaaaaaa, bbbbbbbbbbbbbbbbbbbbb).Other(ccccccccccccccc).Third(ddddddddddddddddddddddddd, eeeeeeeeeeeeeee).Fourth(ff);
                    """
                )
            )
        );
}
