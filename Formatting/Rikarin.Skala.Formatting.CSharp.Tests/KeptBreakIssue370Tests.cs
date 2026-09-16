namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     Issue #370: eight further kept-break shapes measured beside #369. Every expected string is
///     <c>jb cleanupcode</c>'s own output for the input under the repository's export, checked through
///     <see cref="Oracle.Agrees" /> for a second-pass fixed point as well; the <c>constructs/breaks/</c>
///     fixture named in each class holds the wider set of shapes.
/// </summary>
/// <remarks>
///     SK-DIV-0108: an indexer's bracketed parameter list takes the declaration keys — chop when an item
///     is multi-line or a delimiter break is kept, <c>]</c> on its own line, the arrow after it broken,
///     a break before a comma joined. It had no plan at all. <c>constructs/breaks/indexer-parameter-list.cs</c>.
/// </remarks>
public sealed class IndexerParameterListTests {
    [Fact]
    public void AKeptBreakAfterADefaultsEquals_ChopsTheList_AndBreaksTheArrow() =>
        Oracle.Agrees(
            """
            class T {
                int this[int a =
                    5] => a;

                int this[object a =
                    null, string b = null] {
                    get => 0;
                }
            }
            """,
            """
            class T {
                int this[
                    int a =
                        5
                ] =>
                    a;

                int this[
                    object a =
                        null,
                    string b = null
                ] {
                    get => 0;
                }
            }
            """
        );

    [Fact]
    public void ABreakBeforeAComma_IsJoined_AndOneAfterIt_Chops() =>
        Oracle.Agrees(
            """
            class T {
                int this[int a
                    , string b] => a;

                int this[int a,
                    string b, object c] => a;
            }
            """,
            """
            class T {
                int this[int a, string b] => a;

                int this[
                    int a,
                    string b,
                    object c
                ] =>
                    a;
            }
            """
        );

    [Fact]
    public void AKeptDelimiterBreak_ChopsTheList() =>
        Oracle.Agrees(
            """
            class T {
                int this[
                    long a] => 0;
            }
            """,
            """
            class T {
                int this[
                    long a
                ] =>
                    0;
            }
            """
        );
}

/// <summary>
///     SK-DIV-0111: a <c>for</c> header is multi-line when a break inside its parentheses
///     <em>survives</em> the constructs inside it, not when the source merely holds one — so a break the
///     declarators, a binary operator or an invocation re-join leaves the header whole.
///     <c>constructs/breaks/for-header-surviving-break.cs</c>.
/// </summary>
public sealed class ForHeaderSurvivingBreakTests {
    [Fact]
    public void ABreakTheDeclaratorsReJoin_LeavesTheHeaderWhole() =>
        Oracle.Agrees(
            """
            class T {
                void M(int n) {
                    for (int i = 0
                        , j = 1; i < n; i++) { }

                    for (int i = 0; i <
                        n; i++) { }

                    for (int i = F(
                        1); i < n; i++) { }
                }

                int F(int a) => a;
            }
            """,
            """
            class T {
                void M(int n) {
                    for (int i = 0, j = 1; i < n; i++) { }

                    for (int i = 0; i < n; i++) { }

                    for (int i = F(1); i < n; i++) { }
                }

                int F(int a) => a;
            }
            """
        );

    [Fact]
    public void ABreakThatIsKept_StillChopsIt() =>
        Oracle.Agrees(
            """
            class T {
                void M(int n) {
                    for (int i = 0,
                        j = 1; i < n; i++) { }

                    for (int i = 0, j = 1; i < n
                        && j > 0; i++) { }
                }
            }
            """,
            """
            class T {
                void M(int n) {
                    for (int i = 0,
                         j = 1;
                         i < n;
                         i++) { }

                    for (int i = 0, j = 1;
                         i < n
                         && j > 0;
                         i++) { }
                }
            }
            """
        );
}
