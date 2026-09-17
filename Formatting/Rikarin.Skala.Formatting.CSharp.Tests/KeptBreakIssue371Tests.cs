namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     Issue #371 (SK-DIV-0114): the eleven list-like kinds <c>BreakPlan.Plan</c> never visited. Every
///     expected string is <c>jb cleanupcode</c>'s own output for the input under the repository's
///     export, checked through <see cref="Oracle.Agrees" /> for a second-pass fixed point as well; the
///     nine <c>constructs/breaks/</c> fixtures hold the wider set of shapes, and
///     <see cref="SeparatedListPlanTests" /> holds the kinds.
/// </summary>
public sealed class ElementAccessArgumentsTests {
    [Fact]
    public void AKeptBreakAfterTheBracket_ChopsTheList_AndOneBeforeAComma_IsJoined() =>
        Oracle.Agrees(
            """
            class T {
                int[,] grid;

                int AfterOpen() => grid[
                    0, 1];

                int BeforeComma() => grid[0
                    , 1];
            }
            """,
            """
            class T {
                int[,] grid;

                int AfterOpen() =>
                    grid[
                        0,
                        1];

                int BeforeComma() => grid[0, 1];
            }
            """
        );

    [Fact]
    public void AnOverflowingList_ChopsWithoutABreakAtEitherBracket() =>
        Oracle.Agrees(
            """
            class T {
                int[,,] cube;

                int M() => cube[SomeVeryLongExpressionNumberOne, SomeVeryLongExpressionNumberTwo, SomeVeryLongExpressionNumberThree, SomeVeryLongExpressionNumberFour];
            }
            """,
            """
            class T {
                int[,,] cube;

                int M() =>
                    cube[SomeVeryLongExpressionNumberOne,
                        SomeVeryLongExpressionNumberTwo,
                        SomeVeryLongExpressionNumberThree,
                        SomeVeryLongExpressionNumberFour];
            }
            """
        );
}

public sealed class TypeArgumentListTests {
    [Fact]
    public void TheEqualsBreaksFirst_AndTheListFillsOnlyWhatStillOverflows() =>
        Oracle.Agrees(
            """
            class T {
                void M() {
                    var created = new Dictionary<SomeVeryLongTypeNameNumberOneForTheMargin, SomeVeryLongTypeNameNumberTwoForTheMargin>();
                    var both = new Dictionary<SomeVeryLongTypeNameNumberOneForTheMargin, SomeVeryLongTypeNameNumberTwoForTheMargin, SomeVeryLongTypeNameNumberTwoForTheMargin>();
                    var result = Generic<SomeVeryLongTypeNameNumberOneForTheMargin, SomeVeryLongTypeNameNumberTwoForTheMargin, int>();
                }
            }
            """,
            """
            class T {
                void M() {
                    var created =
                        new Dictionary<SomeVeryLongTypeNameNumberOneForTheMargin, SomeVeryLongTypeNameNumberTwoForTheMargin>();
                    var both =
                        new Dictionary<SomeVeryLongTypeNameNumberOneForTheMargin, SomeVeryLongTypeNameNumberTwoForTheMargin,
                            SomeVeryLongTypeNameNumberTwoForTheMargin>();
                    var result =
                        Generic<SomeVeryLongTypeNameNumberOneForTheMargin, SomeVeryLongTypeNameNumberTwoForTheMargin, int>();
                }
            }
            """
        );

    [Fact]
    public void ANestedList_KeepsItsHead_AndAKeptBreakAfterTheAngle_Stays() =>
        Oracle.Agrees(
            """
            class T {
                Dictionary<string, Dictionary<string, Dictionary<string, Dictionary<string, Dictionary<string, Dictionary<string, int>>>>>> Nested() => null;

                Dictionary<
                    string, int> AfterOpen() => null;

                void Twin<
                    T1, T2>() { }

                void Local() {
                    List<Dictionary<string,
                        int>> nested = null;
                }
            }
            """,
            """
            class T {
                Dictionary<string, Dictionary<string,
                    Dictionary<string, Dictionary<string, Dictionary<string, Dictionary<string, int>>>>>> Nested() =>
                    null;

                Dictionary<
                    string, int> AfterOpen() =>
                    null;

                void Twin<
                    T1, T2>() { }

                void Local() {
                    List<Dictionary<string,
                        int>> nested = null;
                }
            }
            """
        );
}

public sealed class AttributeSectionTests {
    [Fact]
    public void TheAttributesAlignUnderTheFirst_AndAParameterLeavesAMultiLineSectionsLine() =>
        Oracle.Agrees(
            """
            class T {
                [Obsolete,
                    Serializable]
                void M() { }

                [return: Obsolete,
                    CLSCompliant(true)]
                int R() => 0;

                void P([Obsolete,
                    CLSCompliant(true)] int a) { }

                void Q([Obsolete, Serializable]
                    int a) { }
            }
            """,
            """
            class T {
                [Obsolete,
                 Serializable]
                void M() { }

                [return: Obsolete,
                         CLSCompliant(true)]
                int R() => 0;

                void P(
                    [Obsolete,
                     CLSCompliant(true)]
                    int a
                ) { }

                void Q([Obsolete, Serializable] int a) { }
            }
            """
        );

    [Fact]
    public void AnOverflowingSection_Fills_AndASingleAttribute_BreaksAfterTheBracketBeforeItChopsItsArguments() =>
        Oracle.Agrees(
            """
            class T {
                [Obsolete("aaa", true), Serializable, CLSCompliant(true), Obsolete("bbb"), Serializable, CLSCompliant(false), Obsolete("ccc")]
                void M() { }

                void P([Obsolete("a very long message that runs the line out past the margin of one hundred and twenty columns and on", true)] int a) { }

                void Q([Description("The path `content` will be saved to, so the right .editorconfig section applies.")] string? contentPath = null) { }
            }
            """,
            """
            class T {
                [Obsolete("aaa", true), Serializable, CLSCompliant(true), Obsolete("bbb"), Serializable, CLSCompliant(false),
                 Obsolete("ccc")]
                void M() { }

                void P(
                    [Obsolete(
                        "a very long message that runs the line out past the margin of one hundred and twenty columns and on",
                        true
                    )]
                    int a
                ) { }

                void Q(
                    [Description("The path `content` will be saved to, so the right .editorconfig section applies.")]
                    string? contentPath = null
                ) { }
            }
            """
        );
}

public sealed class TupleShapedFillTests {
    [Fact]
    public void APositionalPatternAndADesignation_Fill_AndKeepANestedItemsHead() =>
        Oracle.Agrees(
            """
            class T {
                bool P(object o) => o is (SomeVeryLongConstantNumberOne, SomeVeryLongConstantNumberTwo, SomeVeryLongConstantNumberThree, SomeVeryLongConstantNumberFour);

                bool N(object o) => o is (1, (2,
                    3));

                void D() {
                    var (a2, (b2,
                        c2)) = (1, (2, 3));
                }
            }
            """,
            """
            class T {
                bool P(object o) =>
                    o is (SomeVeryLongConstantNumberOne, SomeVeryLongConstantNumberTwo, SomeVeryLongConstantNumberThree,
                        SomeVeryLongConstantNumberFour);

                bool N(object o) =>
                    o is (1, (2,
                        3));

                void D() {
                    var (a2, (b2,
                        c2)) = (1, (2, 3));
                }
            }
            """
        );

    [Fact]
    public void ATupleItemWithAKeptBreakInside_KeepsItsHead_AndATooWideOne_MovesWhole() =>
        Oracle.Agrees(
            """
            class T {
                void M() {
                    var e = (1
                        , G<int
                            , int>());
                    var i = (1, Get(SomeVeryLongExpressionNumberOne + SomeVeryLongExpressionNumberTwo + SomeVeryLongExpressionNumberThree, SomeVeryLongExpressionNumberFour + SomeVeryLongExpressionNumberOne));
                }
            }
            """,
            """
            class T {
                void M() {
                    var e = (1
                        , G<int
                            , int>());
                    var i = (1,
                        Get(
                            SomeVeryLongExpressionNumberOne + SomeVeryLongExpressionNumberTwo + SomeVeryLongExpressionNumberThree,
                            SomeVeryLongExpressionNumberFour + SomeVeryLongExpressionNumberOne
                        ));
                }
            }
            """
        );
}
