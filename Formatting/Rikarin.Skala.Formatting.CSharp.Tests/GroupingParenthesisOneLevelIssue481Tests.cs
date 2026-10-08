namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     Issue #481, SK-DIV-0118, SK-DIV-0150: a grouping parenthesis spends no level of its own beside whatever
///     opened on its line, and a second level only when a construct broke after it lifts it. Every expected
///     string is <c>jb cleanupcode</c>'s own output for the input, and each is checked on a second pass;
///     <c>constructs/syntax/grouping-parenthesis-one-level.cs</c> holds the wider set.
/// </summary>
public sealed class GroupingParenthesisOneLevelIssue481Tests {
    /// <summary>A ternary, a binary, a tuple and a lambda's parameters in a grouping: one level, not two.</summary>
    [Fact]
    public void AGroupingBesideAnotherScope_SpendsOneLevel() =>
        Oracle.Agrees(
            """
            class C {
                void M() {
                    var f = ((x,
            y) => { });
                    var t = ((
            1, 2));
                    var x = (c
            ? a
            : b);
                    var y = ((a
            + b));
                }
            }
            """,
            """
            class C {
                void M() {
                    var f = ((
                        x,
                        y
                    ) => { });
                    var t = ((
                        1, 2));
                    var x = (c
                        ? a
                        : b);
                    var y = ((a
                        + b));
                }
            }
            """
        );

    /// <summary>
    ///     Lifted by a construct that broke after it, the grouping spends the second level — nested groupings
    ///     lift once each — and a statement condition's own parenthesis is still a level of its own.
    /// </summary>
    [Fact]
    public void ALiftedGrouping_SpendsTheConstructsLevel() =>
        Oracle.Agrees(
            """
            class C {
                void M() {
                    var b = ((
            1 + 2)
            * 3);
                    if ((a
            == b)) { }
                    var d = (((ax * ax)
            + (az
            * az))
            * ((bx * cz) - (cx * bz)))
            - 1;
                    var h = placement.TriangleOffset
            + (meshlet
            .TriangleCount
            * 3);
                }
            }
            """,
            """
            class C {
                void M() {
                    var b = ((
                            1 + 2)
                        * 3);
                    if ((a
                            == b)) { }

                    var d = (((ax * ax)
                                + (az
                                    * az))
                            * ((bx * cz) - (cx * bz)))
                        - 1;
                    var h = placement.TriangleOffset
                        + (meshlet
                                .TriangleCount
                            * 3);
                }
            }
            """
        );

    /// <summary>
    ///     A lifted level never moves the line it opened at the start of: a property fill heading a switch
    ///     arm's <c>or</c> pattern stays on the arm's column (found merging #481's lift with #482's fill).
    /// </summary>
    [Fact]
    public void ALiftedLevel_LeavesItsOwnFirstLine() =>
        Oracle.Agrees(
            """
            class C {
                static int? K(SyntaxKind kind) =>
                    kind switch {
                        SyntaxKind.MultiplyExpression
                            or SyntaxKind.DivideExpression
                            or SyntaxKind.ModuloExpression => 1,
                        _ => null
                    };
            }
            """,
            """
            class C {
                static int? K(SyntaxKind kind) =>
                    kind switch {
                        SyntaxKind.MultiplyExpression
                            or SyntaxKind.DivideExpression
                            or SyntaxKind.ModuloExpression => 1,
                        _ => null
                    };
            }
            """
        );
}
