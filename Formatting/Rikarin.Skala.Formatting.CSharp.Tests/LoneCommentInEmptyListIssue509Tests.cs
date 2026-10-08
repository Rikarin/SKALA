namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     Issue #509, SK-DIV-0209: a block comment spanning lines that is all an empty argument or parameter list
///     holds goes to column 0 on a line of its own, with the <c>)</c> on another; a <c>/** */</c> comment takes the
///     list's level and a lambda's parameter list moves only the <c>)</c>. Every expected string is
///     <c>jb cleanupcode</c>'s own output for the input, and each is checked on a second pass;
///     <c>constructs/syntax/lone-comment-in-empty-list.cs</c> holds the wider set.
/// </summary>
public sealed class LoneCommentInEmptyListIssue509Tests {
    [Fact]
    public void ALoneCommentInAnEmptyList_TakesColumnZero() =>
        Oracle.Agrees(
            """
            class C {
                void M() {
                    Foo(/* a
                       b */);
                    Foo(/** a
                       b */);
                    Action f = (/* a
                       b */) => { };
                }

                void N(/* a
                       b */) { }
            }
            """,
            """
            class C {
                void M() {
                    Foo(
            /* a
               b */
                    );
                    Foo(
                        /** a
                           b */
                    );
                    Action f = ( /* a
                       b */
                    ) => { };
                }

                void N(
            /* a
                   b */
                ) { }
            }
            """
        );
}
