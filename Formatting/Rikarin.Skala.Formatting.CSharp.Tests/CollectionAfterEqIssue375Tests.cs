namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     Issue #375: a break the author wrote after an <c>=</c> whose value is a collection expression is
///     kept exactly when the collection fits flat on the line below, and otherwise the bracket takes
///     the break — <c>x = [</c>, elements one level in, <c>]</c> on the owner's indent. Every expected
///     string is <c>jb cleanupcode</c>'s own output for the input, measured 2026-09-18 with
///     <c>Testing ask</c>; <c>constructs/breaks/collection-after-eq.cs</c> holds the same shapes as a
///     fixture.
/// </summary>
/// <remarks>
///     ⚠ The Nightly fuzzer's seed 5209185227727739433 found the shape the first class pins: pass one
///     kept the <c>=</c> break and chopped the list around a multi-line lambda, because the exemption
///     was read off the source and the source had no break at the list's own gaps; pass two, reading
///     the chopped list as the author's, joined <c>= [</c>. The issue guessed pass two wrong on the
///     strength of SK-DIV-0103's "the `=` break is kept for a declarator", and the oracle refutes the
///     guess: given the original input it writes <c>= [</c>, and it writes <c>= [</c> for every
///     collection that will not fit flat on the continuation line, whatever the reason. The
///     deconstruction was incidental — <c>var x =</c> with the same element does the same.
/// </remarks>
public sealed class CollectionAfterEqIssue375Tests {
    /// <summary>
    ///     The fuzzer's four lines. One pass equals two, and the <c>=</c> line and the closing bracket
    ///     are the oracle's. ⚠ The three lines between them are not, for two reasons that are not this
    ///     issue's and are recorded beside it: the oracle keeps <c>null!, ((</c> together, because a
    ///     collection element with a certain break inside keeps its head on the comma's line
    ///     (SK-DIV-0117), and it puts the lambda's parameters one level past the element and the
    ///     <c>)</c> on the element's column, because a grouping parenthesis is transparent to the
    ///     parameter list it wraps (SK-DIV-0118). Skala's fixed point is pinned here exactly, so that
    ///     either of those moving is visible.
    /// </summary>
    [Fact]
    public void TheFuzzersInput_SettlesInOnePass_OnTheOraclesBracket() {
        const string source = """
                              namespace P;

                              public class C {
                                  void M() {
                                    var (a58, b59) =
                              [null!, ((x,
                              y) => {
                                })];
                                  }
                              }
                              """;

        const string oracle = """
                              namespace P;

                              public class C {
                                  void M() {
                                      var (a58, b59) = [
                                          null!, ((
                                              x,
                                              y
                                          ) => { })
                                      ];
                                  }
                              }
                              """;

        const string skala = """
                             namespace P;

                             public class C {
                                 void M() {
                                     var (a58, b59) = [
                                         null!,
                                         ((
                                                 x,
                                                 y
                                             ) => { })
                                     ];
                                 }
                             }
                             """;

        var once = Format.Text(source);
        Assert.Equal(skala, once.TrimEnd('\n'));
        Assert.Equal(once, Format.Text(once));

        // The `=` line and the closing bracket, counted from each end: the oracle's answer is one line
        // shorter than Skala's, by the `null!, ((` head it keeps together.
        var oracleLines = oracle.Split('\n');
        var skalaLines = once.TrimEnd('\n').Split('\n');
        Assert.Equal("        var (a58, b59) = [", oracleLines[4]);
        Assert.Equal(oracleLines[4], skalaLines[4]);
        Assert.Equal("        ];", oracleLines[^3]);
        Assert.Equal(oracleLines[^3], skalaLines[^3]);
    }

    /// <summary>
    ///     Pass one's output from before the fix, given as input, reaches the same fixed point: the
    ///     kept <c>=</c> break is given back to the bracket because the list below it is chopped.
    /// </summary>
    [Fact]
    public void TheOldPassOne_ReachesTheSameFixedPoint() {
        const string passOne = """
                               namespace P;

                               public class C {
                                   void M() {
                                               var (a58, b59) =
                                                   [
                                                       null!,
                                                       ((
                                                               x,
                                                               y
                                                           ) => { })
                                                   ];
                                   }
                               }
                               """;

        const string skala = """
                             namespace P;

                             public class C {
                                 void M() {
                                     var (a58, b59) = [
                                         null!,
                                         ((
                                                 x,
                                                 y
                                             ) => { })
                                     ];
                                 }
                             }
                             """;

        var once = Format.Text(passOne);
        Assert.Equal(skala, once.TrimEnd('\n'));
        Assert.Equal(once, Format.Text(once));
    }

    /// <summary>
    ///     The same element under <c>var x =</c>: the designation had nothing to do with it.
    /// </summary>
    [Fact]
    public void ALocalWithTheSameElement_TakesTheSameShape() {
        var once = Format.Text(
            """
            namespace P;

            public class C {
                void M() {
                    var x =
            [null!, ((x,
            y) => {
              })];
                }
            }
            """
        );

        Assert.Contains("        var x = [\n            null!,\n", once, StringComparison.Ordinal);
        Assert.EndsWith("        ];\n    }\n}\n", once, StringComparison.Ordinal);
        Assert.Equal(once, Format.Text(once));
    }

    [Fact]
    public void ADeconstruction_KeepsTheBreak_WhenTheCollectionFits() =>
        Oracle.Agrees(
            """
            namespace P;

            public class C {
                void M() {
                    var (a, b) =
            [1, 2];
                }
            }
            """,
            """
            namespace P;

            public class C {
                void M() {
                    var (a, b) =
                        [1, 2];
                }
            }
            """
        );

    [Fact]
    public void ADeconstruction_KeepsTheBreak_BeforeATuple() =>
        Oracle.Agrees(
            """
            namespace P;

            public class C {
                void M() {
                    var (a, b) =
            (1, 2);
                }
            }
            """,
            """
            namespace P;

            public class C {
                void M() {
                    var (a, b) =
                        (1, 2);
                }
            }
            """
        );

    [Fact]
    public void ADeconstruction_KeepsTheBreak_BeforeAnInvocation() =>
        Oracle.Agrees(
            """
            namespace P;

            public class C {
                void M() {
                    var (a, b) =
            Make();
                }
            }
            """,
            """
            namespace P;

            public class C {
                void M() {
                    var (a, b) =
                        Make();
                }
            }
            """
        );

    [Fact]
    public void ALocal_KeepsTheBreak_WhenTheCollectionFits() =>
        Oracle.Agrees(
            """
            namespace P;

            public class C {
                void M() {
                    int[] x =
            [1, 2];
                }
            }
            """,
            """
            namespace P;

            public class C {
                void M() {
                    int[] x =
                        [1, 2];
                }
            }
            """
        );

    [Fact]
    public void ADeconstructionAssignment_KeepsTheBreak_WhenTheCollectionFits() =>
        Oracle.Agrees(
            """
            namespace P;

            public class C {
                void M() {
                    int a, b;
                    (a, b) =
            [1, 2];
                }
            }
            """,
            """
            namespace P;

            public class C {
                void M() {
                    int a, b;
                    (a, b) =
                        [1, 2];
                }
            }
            """
        );

    [Fact]
    public void ADeconstructionAssignment_KeepsTheBreak_BeforeATuple() =>
        Oracle.Agrees(
            """
            namespace P;

            public class C {
                void M() {
                    int a, b;
                    (a, b) =
            (1, 2);
                }
            }
            """,
            """
            namespace P;

            public class C {
                void M() {
                    int a, b;
                    (a, b) =
                        (1, 2);
                }
            }
            """
        );

    [Fact]
    public void ADeconstruction_BrokenAfterTheBracket_ChopsTheBracket() =>
        Oracle.Agrees(
            """
            namespace P;

            public class C {
                void M() {
                    var (a, b) = [
            1, 2];
                }
            }
            """,
            """
            namespace P;

            public class C {
                void M() {
                    var (a, b) = [
                        1, 2
                    ];
                }
            }
            """
        );

    [Fact]
    public void ADeconstruction_BrokenAfterTheParenthesis_KeepsIt() =>
        Oracle.Agrees(
            """
            namespace P;

            public class C {
                void M() {
                    var (a, b) = (
            1, 2);
                }
            }
            """,
            """
            namespace P;

            public class C {
                void M() {
                    var (a, b) = (
                        1, 2);
                }
            }
            """
        );

    [Fact]
    public void ALocal_BrokenAfterTheBracket_ChopsTheBracket() =>
        Oracle.Agrees(
            """
            namespace P;

            public class C {
                void M() {
                    int[] x = [
            1, 2];
                }
            }
            """,
            """
            namespace P;

            public class C {
                void M() {
                    int[] x = [
                        1, 2
                    ];
                }
            }
            """
        );

    [Fact]
    public void ADeconstructionAssignment_BrokenAfterTheBracket_ChopsTheBracket() =>
        Oracle.Agrees(
            """
            namespace P;

            public class C {
                void M() {
                    int a, b;
                    (a, b) = [
            1, 2];
                }
            }
            """,
            """
            namespace P;

            public class C {
                void M() {
                    int a, b;
                    (a, b) = [
                        1, 2
                    ];
                }
            }
            """
        );

    [Fact]
    public void AMultiLineElement_GivesTheBreakToTheBracket() =>
        Oracle.Agrees(
            """
            namespace P;

            public class C {
                void M() {
                    object[] x =
            [1, () => {
              A();
              B();
            }];
                }
            }
            """,
            """
            namespace P;

            public class C {
                void M() {
                    object[] x = [
                        1, () => {
                            A();
                            B();
                        }
                    ];
                }
            }
            """
        );

    [Fact]
    public void ACollectionTooWideForTheLineBelow_GivesTheBreakToTheBracket() =>
        Oracle.Agrees(
            """
            namespace P;

            public class C {
                void M() {
                    int[] x =
            [1000000, 2000000, 3000000, 4000000, 5000000, 6000000, 7000000, 8000000, 9000000, 10000000, 11000000, 12000000, 13];
                }
            }
            """,
            """
            namespace P;

            public class C {
                void M() {
                    int[] x = [
                        1000000, 2000000, 3000000, 4000000, 5000000, 6000000, 7000000, 8000000, 9000000, 10000000, 11000000,
                        12000000, 13
                    ];
                }
            }
            """
        );

    [Fact]
    public void ACollectionThatFitsTheLineBelow_KeepsTheBreak() =>
        Oracle.Agrees(
            """
            namespace P;

            public class C {
                void M() {
                    int[] x =
            [1000000, 2000000, 3000000, 4000000, 5000000, 6000000, 7000000, 8000000, 9000000, 10000000, 11000000, 123];
                }
            }
            """,
            """
            namespace P;

            public class C {
                void M() {
                    int[] x =
                        [1000000, 2000000, 3000000, 4000000, 5000000, 6000000, 7000000, 8000000, 9000000, 10000000, 11000000, 123];
                }
            }
            """
        );

    [Fact]
    public void AContinuationLineOfExactlyOneHundredAndTwenty_KeepsTheBreak() =>
        Oracle.Agrees(
            """
            namespace P;

            public class C {
                void M() {
                    int[] x =
            [1000000, 2000000, 3000000, 4000000, 5000000, 6000000, 7000000, 8000000, 9000000, 10000000, 11000000, 1234];
                }
            }
            """,
            """
            namespace P;

            public class C {
                void M() {
                    int[] x =
                        [1000000, 2000000, 3000000, 4000000, 5000000, 6000000, 7000000, 8000000, 9000000, 10000000, 11000000, 1234];
                }
            }
            """
        );

    [Fact]
    public void AContinuationLineOfOneHundredAndTwentyOne_GivesTheBreakToTheBracket() =>
        Oracle.Agrees(
            """
            namespace P;

            public class C {
                void M() {
                    int[] x =
            [1000000, 2000000, 3000000, 4000000, 5000000, 6000000, 7000000, 8000000, 9000000, 10000000, 11000000, 12345];
                }
            }
            """,
            """
            namespace P;

            public class C {
                void M() {
                    int[] x = [
                        1000000, 2000000, 3000000, 4000000, 5000000, 6000000, 7000000, 8000000, 9000000, 10000000, 11000000, 12345
                    ];
                }
            }
            """
        );

    [Fact]
    public void AKeptBreakBeforeTheClosingBracket_GivesTheBreakToTheBracket() =>
        Oracle.Agrees(
            """
            namespace P;

            public class C {
                void M() {
                    int[] x =
            [1, F(
            2)
            ];
                }
            }
            """,
            """
            namespace P;

            public class C {
                void M() {
                    int[] x = [
                        1, F(2)
                    ];
                }
            }
            """
        );

    [Fact]
    public void ACommentInsideTheCollection_GivesTheBreakToTheBracket() =>
        Oracle.Agrees(
            """
            namespace P;

            public class C {
                void M() {
                    int[] x =
            [1, // c
            2];
                }
            }
            """,
            """
            namespace P;

            public class C {
                void M() {
                    int[] x = [
                        1, // c
                        2
                    ];
                }
            }
            """
        );

    [Fact]
    public void AParameterDefault_GivesTheBreakToTheBracket_AndTheListChops() =>
        Oracle.Agrees(
            """
            namespace P;

            public class C {
                void M2(object[] a =
            [1, () => {
              A();
              B();
            }]) { }
            }
            """,
            """
            namespace P;

            public class C {
                void M2(
                    object[] a = [
                        1, () => {
                            A();
                            B();
                        }
                    ]
                ) { }
            }
            """
        );

    [Fact]
    public void AUsingHeader_GivesTheBreakToTheBracket() =>
        Oracle.Agrees(
            """
            namespace P;

            public class C {
                void M() {
                    using (var d =
            [1, () => {
              A();
              B();
            }]) { }
                }
            }
            """,
            """
            namespace P;

            public class C {
                void M() {
                    using (var d = [
                               1, () => {
                                   A();
                                   B();
                               }
                           ]) { }
                }
            }
            """
        );

    [Fact]
    public void AForHeader_GivesTheBreakToTheBracket() =>
        Oracle.Agrees(
            """
            namespace P;

            public class C {
                void M() {
                    for (object[] i =
            [1, () => {
              A();
              B();
            }]; i != null; i = null) { }
                }
            }
            """,
            """
            namespace P;

            public class C {
                void M() {
                    for (object[] i = [
                             1, () => {
                                 A();
                                 B();
                             }
                         ];
                         i != null;
                         i = null) { }
                }
            }
            """
        );

    [Fact]
    public void AnInitializerElement_GivesTheBreakToTheBracket() =>
        Oracle.Agrees(
            """
            namespace P;

            public class C {
                void M() {
                    var o = new C { X =
            [1, () => {
              A();
              B();
            }], Y = 2 };
                }
            }
            """,
            """
            namespace P;

            public class C {
                void M() {
                    var o = new C {
                        X = [
                            1, () => {
                                A();
                                B();
                            }
                        ],
                        Y = 2
                    };
                }
            }
            """
        );

    [Fact]
    public void AnInitializerElement_KeepsTheBreak_WhenTheCollectionFits() =>
        Oracle.Agrees(
            """
            namespace P;

            public class C {
                void M() {
                    var o = new C { X =
            [1], Y = 2 };
                }
            }
            """,
            """
            namespace P;

            public class C {
                void M() {
                    var o = new C {
                        X =
                            [1],
                        Y = 2
                    };
                }
            }
            """
        );

    [Fact]
    public void ANamedAttributeArgument_GivesTheBreakToTheBracket() =>
        Oracle.Agrees(
            """
            namespace P;

            public class C {
                [System.Obsolete(Message =
            [1, () => {
              A();
              B();
            }])]
                void M3() { }
            }
            """,
            """
            namespace P;

            public class C {
                [System.Obsolete(
                    Message = [
                        1, () => {
                            A();
                            B();
                        }
                    ]
                )]
                void M3() { }
            }
            """
        );

    [Fact]
    public void ABreakBeforeTheEquals_IsKept_AndTheBracketStillBreaks() =>
        Oracle.Agrees(
            """
            namespace P;

            public class C {
                void M() {
                    int[] x
            = [1, () => {
              A();
              B();
            }];
                }
            }
            """,
            """
            namespace P;

            public class C {
                void M() {
                    int[] x
                        = [
                            1, () => {
                                A();
                                B();
                            }
                        ];
                }
            }
            """
        );

    [Fact]
    public void AnArrayInitializer_KeepsTheBreak_AroundTheSameElement() =>
        Oracle.Agrees(
            """
            namespace P;

            public class C {
                void M() {
                    var t =
            new[] { 1, () => {
              A();
              B();
            } };
                }
            }
            """,
            """
            namespace P;

            public class C {
                void M() {
                    var t =
                        new[] {
                            1, () => {
                                A();
                                B();
                            }
                        };
                }
            }
            """
        );

    [Fact]
    public void ATuple_KeepsTheBreak_AroundTheSameElement() =>
        Oracle.Agrees(
            """
            namespace P;

            public class C {
                void M() {
                    var t =
            (1, () => {
              A();
              B();
            });
                }
            }
            """,
            """
            namespace P;

            public class C {
                void M() {
                    var t =
                        (1, () => {
                            A();
                            B();
                        });
                }
            }
            """
        );

    [Fact]
    public void AUsingHeader_KeepsTheBreak_BeforeAFlatValue() =>
        Oracle.Agrees(
            """
            namespace P;

            public class C {
                void M() {
                    using (var d =
            default(System.IDisposable)) { }
                }
            }
            """,
            """
            namespace P;

            public class C {
                void M() {
                    using (var d =
                           default(System.IDisposable)) { }
                }
            }
            """
        );

    [Fact]
    public void AUsingHeader_KeepsTheBreak_WhenTheCollectionFits() =>
        Oracle.Agrees(
            """
            namespace P;

            public class C {
                void M() {
                    using (var d =
            [1, 2]) { }
                }
            }
            """,
            """
            namespace P;

            public class C {
                void M() {
                    using (var d =
                           [1, 2]) { }
                }
            }
            """
        );
}
