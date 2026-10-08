namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     Issue #473, SK-DIV-0114: a positional pattern or a designation nested in another spends no level of its
///     own, and the outermost one spends none directly in an aligned statement condition; a tuple expression
///     spends one per parenthesis. Every expected string is <c>jb cleanupcode</c>'s own output for the input,
///     and each is checked on a second pass; <c>constructs/syntax/nested-positional-pattern.cs</c> holds the
///     wider set.
/// </summary>
public sealed class NestedPositionalPatternIssue473Tests {
    [Fact]
    public void ANestedPatternOrDesignation_SpendsNoLevelOfItsOwn() =>
        Oracle.Agrees(
            """
            class C {
                bool B(object o) => o is (1
            , (2
            , 3));

                void M() {
                    var (a
            , (b
            , c)) = t;
                    if (o is (1
            , (2
            , 3))) { }
                    var t2 = (1
            , (2
            , 3), 4);
                }
            }
            """,
            """
            class C {
                bool B(object o) =>
                    o is (1
                        , (2
                        , 3));

                void M() {
                    var (a
                        , (b
                        , c)) = t;
                    if (o is (1
                        , (2
                        , 3))) { }

                    var t2 = (1
                        , (2
                            , 3), 4);
                }
            }
            """
        );
}
