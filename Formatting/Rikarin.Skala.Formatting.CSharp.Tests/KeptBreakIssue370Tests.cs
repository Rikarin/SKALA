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
