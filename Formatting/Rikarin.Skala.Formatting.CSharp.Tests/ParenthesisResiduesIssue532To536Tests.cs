using static Rikarin.Skala.Formatting.CSharp.Tests.TestText;

namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     Issues #532–#536, the residues of #470–#509. Every expected string is <c>jb cleanupcode</c>'s own output
///     for the input, and each is checked on a second pass; <c>constructs/syntax/parenthesis-residues.cs</c> holds
///     the wider set.
/// </summary>
public sealed class ParenthesisResiduesIssue532To536Tests {
    /// <summary>#532: a positional pattern in a property pattern keeps <c>X: (2</c> and its items on <c>X</c>'s column.</summary>
    [Fact]
    public void APositionalPatternInAPropertyPattern_StaysBesideItsName() =>
        Oracle.Agrees(
            """
            class C {
                bool D(object o) => o is { X: (2
            , 3), Y: 1 };
            }
            """,
            """
            class C {
                bool D(object o) =>
                    o is {
                        X: (2
                        , 3),
                        Y: 1
                    };
            }
            """
        );

    /// <summary>
    ///     #533: an empty list's comment already on its own line gets a blank line before it; one beside the
    ///     <c>(</c> that ends its line goes to column 0.
    /// </summary>
    [Fact]
    public void AnEmptyListsOwnLineComment_GetsABlankLineBeforeIt() =>
        Oracle.Agrees(
            """
            class C {
                void M() {
                    Foo(
                        /* a */
                    );
                    Foo(/* a */
                    );
                }

                void N(
                    // a
                ) { }
            }
            """,
            """
            class C {
                void M() {
                    Foo(

                        /* a */
                    );
                    Foo(
            /* a */
                    );
                }

                void N(

                    // a
                ) { }
            }
            """
        );

    /// <summary>
    ///     #534, #535 and #536: <c>nameof</c>'s qualified name breaks at the last dot that fits; a chain lifted
    ///     through two binaries and two groupings; an author's break after a member access's dot is joined, one
    ///     after a comment there is kept.
    /// </summary>
    [Fact]
    public void DotsAndLifts_AreTheOracles() =>
        Oracle.Agrees(
            $$"""
              class C {
                  void M() {
                      var n4 = nameof(a.B{{R('b', 70)}}.Cccccccccccccccccccccccccc.Dddddddddd);
                      var e4 = c.
              X.
              Y().
              Z;
                      var e7 = c./* c */
              X;
                  }

                  static bool Near(Vector2 point, float x) =>
                      ((point
              .X
              - x)
              * (point.X - x))
              + 1;
              }
              """,
            """
            class C {
                void M() {
                    var n4 = nameof(a.Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb
                        .Cccccccccccccccccccccccccc.Dddddddddd);
                    var e4 = c.X.Y().Z;
                    var e7 = c. /* c */
                        X;
                }

                static bool Near(Vector2 point, float x) =>
                    ((point
                                .X
                            - x)
                        * (point.X - x))
                    + 1;
            }
            """
        );
}
