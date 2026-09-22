namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     Issue #379, the flat half of #375's rule: a line too long to keep whole breaks after a
///     collection-valued <c>=</c> exactly when the bracket fits flat on the line below, with no margin,
///     and otherwise glues <c>= [</c> and chops the bracket — even past the margin, the line measured
///     up to the <c>=</c>; and an added break needs a head of twelve columns from the owning construct's
///     first token. Every expected string is <c>jb cleanupcode</c>'s own output for the input, measured
///     2026-09-22 with <c>Testing ask</c> over eleven probe rounds;
///     <c>constructs/breaks/collection-after-eq-at-the-margin.cs</c> holds the same shapes as a fixture.
/// </summary>
/// <remarks>
///     ⚠ The Nightly fuzzer's seed 3296757264995743770 found the first class: a declarator whose
///     <c>=</c> landed at column 120 broke after it on pass one, because the ordering rule's second
///     question took the <c>=</c> break as soon as <c>= [</c> overhung; pass two read that break as
///     the author's and gave it back to the bracket by #375's rule. The oracle writes pass two from
///     either input. The margin class was found beside it: with the <c>=</c> at column 60 the oracle
///     moves a bracket down whole up to a 120-column continuation line, where
///     <c>Fitter.OuterBreakMargin</c> had stopped eleven columns short. Neither class is visible in a
///     kept-break test, which is all #375 had.
/// </remarks>
public sealed class CollectionAfterEqIssue379Tests {
    /// <summary>
    ///     The wide bracket: fifteen elements, filled by the oracle as eleven and four at indent 12 and
    ///     as twelve and three at indent 8. Interpolated so that no line of this file is over 120
    ///     columns while the formatted content is byte for byte the oracle's.
    /// </summary>
    const string Eleven = "1000000, 2000000, 3000000, 4000000, 5000000, 6000000,"
        + " 7000000, 8000000, 9000000, 10000000, 11000000";

    const string Wide = Eleven + ", 12000000, 13000000, 14000000, 15000000";
    const string RestOfFour = "12000000, 13000000, 14000000, 15000000";
    const string Twelve = Eleven + ", 12000000";
    const string RestOfThree = "13000000, 14000000, 15000000";

    /// <summary>A run of <c>T</c>s: an unbreakable type sized to put the <c>=</c> at an exact column.</summary>
    static string T(int width) => new('T', width);

    /// <summary>
    ///     <c>10, 10, …, last</c>: a bracket of an exact width. With twenty-four tens and a nine-letter
    ///     last element it is 107 columns, which at indent 12 with the <c>;</c> is a 120-column line.
    /// </summary>
    /// <summary>The last element of a <see cref="Tens" /> list, at the two widths the boundaries need.</summary>
    const string Six = "aaaaaa";

    const string Nine = "aaaaaaaaa";

    static string Tens(int count, string last) => string.Join(", ", Enumerable.Repeat("10", count).Append(last));

    // ---- the seed's class: the `=` at the margin, the bracket glued past it ----------------------

    /// <summary>
    ///     The seed's shape reduced to its declarator: the <c>=</c> at column 120, the bracket too wide
    ///     for the line below. One pass, and the <c>= [</c> line is 122 columns.
    /// </summary>
    [Fact]
    public void TheEqualsAtOneHundredAndTwenty_GluesTheBracket_AtOneHundredAndTwentyTwo() {
        var source = $$"""
            namespace P;

            public class C {
                void M() {
                    {{T(107)}} v9 = [{{Wide}}];
                }
            }
            """;

        var expected = $$"""
            namespace P;

            public class C {
                void M() {
                    {{T(107)}} v9 = [
                        {{Eleven}},
                        {{RestOfFour}}
                    ];
                }
            }
            """;

        Oracle.Agrees(source, expected);
        Assert.Equal(122, Format.Text(source).Split('\n')[4].Length);
    }

    [Fact]
    public void TheEqualsAtOneHundredAndNineteen_GluesTheBracket_AtOneHundredAndTwentyOne() =>
        Oracle.Agrees(
            $$"""
            namespace P;

            public class C {
                void M() {
                    {{T(106)}} v9 = [{{Wide}}];
                }
            }
            """,
            $$"""
            namespace P;

            public class C {
                void M() {
                    {{T(106)}} v9 = [
                        {{Eleven}},
                        {{RestOfFour}}
                    ];
                }
            }
            """
        );

    [Fact]
    public void TheEqualsAtOneHundredAndTwenty_BreaksAfterItself_WhenTheBracketFitsBelow() =>
        Oracle.Agrees(
            $$"""
            namespace P;

            public class C {
                void M() {
                    {{T(107)}} v9 = [1, 2, 3];
                }
            }
            """,
            $$"""
            namespace P;

            public class C {
                void M() {
                    {{T(107)}} v9 =
                        [1, 2, 3];
                }
            }
            """
        );

    [Fact]
    public void TheEqualsAtOneHundredAndTwenty_GluesTheBracket_AroundAMultiLineElement() =>
        Oracle.Agrees(
            $$"""
            namespace P;

            public class C {
                void M() {
                    {{T(107)}} v9 = [1, () => {
                        A();
                        B();
                    }];
                }
            }
            """,
            $$"""
            namespace P;

            public class C {
                void M() {
                    {{T(107)}} v9 = [
                        1, () => {
                            A();
                            B();
                        }
                    ];
                }
            }
            """
        );

    /// <summary>
    ///     A type argument list ahead of the <c>=</c> is left whole and the bracket glued: the line is
    ///     measured up to the <c>=</c>, and the list's points yield to what precedes them (SK-DIV-0119),
    ///     not to a bracket that overhangs after them. The seed's own type broke at its outer comma for
    ///     its own width; this one does not have to.
    /// </summary>
    [Fact]
    public void ATypeArgumentListBeforeTheEquals_StaysWhole_AndTheBracketIsGlued() =>
        Oracle.Agrees(
            $$"""
            namespace P;

            public class C {
                void M() {
                    Dictionary<{{T(90)}}, int> v9 = [{{Wide}}];
                }
            }
            """,
            $$"""
            namespace P;

            public class C {
                void M() {
                    Dictionary<{{T(90)}}, int> v9 = [
                        {{Eleven}},
                        {{RestOfFour}}
                    ];
                }
            }
            """
        );

    [Fact]
    public void AField_AtOneHundredAndTwenty_GluesTheBracket() =>
        Oracle.Agrees(
            $$"""
            namespace P;

            public class C {
                static readonly {{T(92)}} Field = [{{Wide}}];
            }
            """,
            $$"""
            namespace P;

            public class C {
                static readonly {{T(92)}} Field = [
                    {{Twelve}},
                    {{RestOfThree}}
                ];
            }
            """
        );

    [Fact]
    public void APropertyInitializer_AtOneHundredAndTwenty_GluesTheBracket() =>
        Oracle.Agrees(
            $$"""
            namespace P;

            public class C {
                {{T(96)}} Property { get; } = [{{Wide}}];
            }
            """,
            $$"""
            namespace P;

            public class C {
                {{T(96)}} Property { get; } = [
                    {{Twelve}},
                    {{RestOfThree}}
                ];
            }
            """
        );

    /// <summary>
    ///     Nothing before the <c>=</c> can break: the oracle glues at 125 columns. This is also what
    ///     Skala writes for a declarator whose <c>=</c> passes column 120, where the oracle breaks the
    ///     gap between the type and the name instead — a gap Skala has no plan for (SK-DIV-0024's
    ///     family) — so the glued form is the fixed point of both formatters there.
    /// </summary>
    [Fact]
    public void AnAssignmentWithNothingBeforeTheEquals_GluesTheBracket_PastTheMargin() {
        var source = $$"""
            namespace P;

            public class C {
                void M() {
                    {{new string('a', 113)}} = [{{Wide}}];
                }
            }
            """;

        var expected = $$"""
            namespace P;

            public class C {
                void M() {
                    {{new string('a', 113)}} = [
                        {{Eleven}},
                        {{RestOfFour}}
                    ];
                }
            }
            """;

        Oracle.Agrees(source, expected);
        Assert.Equal(125, Format.Text(source).Split('\n')[4].Length);
    }

    /// <summary>
    ///     Skala's own pass one from before the fix, given back: the kept break is given to the bracket
    ///     (#375's rule) and the line settles glued at 125. ⚠ Not the oracle's answer for this input —
    ///     it breaks the type/name gap, <c>T…T</c> / <c>v9 = [</c> — but the one Skala can give, and
    ///     it is stable, where before the fix pass one and pass two disagreed on it.
    /// </summary>
    [Fact]
    public void TheOldPassOne_PastTheMargin_SettlesGlued() {
        var passOne = $$"""
            namespace P;

            public class C {
                void M() {
                    {{T(110)}} v9 =
                        [
                            {{Eleven}},
                            {{RestOfFour}}
                        ];
                }
            }
            """;

        var skala = $$"""
            namespace P;

            public class C {
                void M() {
                    {{T(110)}} v9 = [
                        {{Eleven}},
                        {{RestOfFour}}
                    ];
                }
            }
            """;

        var once = Format.Text(passOne);
        Assert.Equal(skala, once.TrimEnd('\n'));
        Assert.Equal(once, Format.Text(once));
    }

    // ---- no margin: the continuation line's own 120 decides -----------------------------------

    [Fact]
    public void AContinuationLineOfOneHundredAndTwenty_BreaksAfterTheEquals() =>
        Oracle.Agrees(
            $$"""
            namespace P;

            public class C {
                void M() {
                    {{T(47)}} v9 = [{{Tens(24, Nine)}}];
                }
            }
            """,
            $$"""
            namespace P;

            public class C {
                void M() {
                    {{T(47)}} v9 =
                        [{{Tens(24, Nine)}}];
                }
            }
            """
        );

    [Fact]
    public void AContinuationLineOfOneHundredAndTwentyOne_GluesTheBracket_AndFills() =>
        Oracle.Agrees(
            $$"""
            namespace P;

            public class C {
                void M() {
                    {{T(47)}} v9 = [{{Tens(25, Six)}}];
                }
            }
            """,
            $$"""
            namespace P;

            public class C {
                void M() {
                    {{T(47)}} v9 = [
                        {{Tens(25, Six)}}
                    ];
                }
            }
            """
        );

    /// <summary>The boundary is the continuation line's, wherever the <c>=</c> is: here at column 100.</summary>
    [Fact]
    public void TheBoundary_DoesNotMoveWithTheEquals() {
        Oracle.Agrees(
            $$"""
            namespace P;

            public class C {
                void M() {
                    {{T(87)}} v9 = [{{Tens(24, Nine)}}];
                }
            }
            """,
            $$"""
            namespace P;

            public class C {
                void M() {
                    {{T(87)}} v9 =
                        [{{Tens(24, Nine)}}];
                }
            }
            """
        );

        Oracle.Agrees(
            $$"""
            namespace P;

            public class C {
                void M() {
                    {{T(87)}} v9 = [{{Tens(25, Six)}}];
                }
            }
            """,
            $$"""
            namespace P;

            public class C {
                void M() {
                    {{T(87)}} v9 = [
                        {{Tens(25, Six)}}
                    ];
                }
            }
            """
        );
    }

    // ---- the head floor: twelve columns from the owner's first token through the `=` ----------

    [Fact]
    public void AnElevenColumnHead_GluesTheBracket_ATwelveColumnHeadBreaks() =>
        Oracle.Agrees(
            $$"""
            namespace P;

            public class C {
                void M() {
                    var ddddd = [{{Tens(24, Six)}}];
                    var dddddd = [{{Tens(24, Six)}}];
                    int[] ddd = [{{Tens(24, Six)}}];
                    object[] d = [{{Tens(24, Six)}}];
                }
            }
            """,
            $$"""
            namespace P;

            public class C {
                void M() {
                    var ddddd = [
                        {{Tens(24, Six)}}
                    ];
                    var dddddd =
                        [{{Tens(24, Six)}}];
                    int[] ddd = [
                        {{Tens(24, Six)}}
                    ];
                    object[] d =
                        [{{Tens(24, Six)}}];
                }
            }
            """
        );

    /// <summary>The floor is a width, not a column: the same heads at a nested block's indent.</summary>
    [Fact]
    public void TheHeadFloor_IsTheSameAtADeeperIndent() =>
        Oracle.Agrees(
            $$"""
            namespace P;

            public class C {
                void M() {
                    {
                        var ddddd = [{{Tens(23, Six)}}];
                        var dddddd = [{{Tens(23, Six)}}];
                    }
                }
            }
            """,
            $$"""
            namespace P;

            public class C {
                void M() {
                    {
                        var ddddd = [
                            {{Tens(23, Six)}}
                        ];
                        var dddddd =
                            [{{Tens(23, Six)}}];
                    }
                }
            }
            """
        );

    /// <summary>
    ///     Under <c>using (</c> the head is counted from the parenthesis: <c>(var dddd =</c> is eleven
    ///     and glues, <c>(var ddddd =</c> is twelve and breaks — the same floor, and not the seventeen
    ///     against eighteen it would be from <c>using</c>. The oracle puts a blank line after the
    ///     multi-line statement, and so does Skala.
    /// </summary>
    [Fact]
    public void UnderAUsingHeader_TheHeadIsCountedFromTheParenthesis() =>
        Oracle.Agrees(
            $$"""
            namespace P;

            public class C {
                void M() {
                    using (var dddd = [{{Tens(22, Six)}}]) { }
                    using (var ddddd = [{{Tens(22, Six)}}]) { }
                }
            }
            """,
            $$"""
            namespace P;

            public class C {
                void M() {
                    using (var dddd = [
                               {{Tens(22, Six)}}
                           ]) { }

                    using (var ddddd =
                           [{{Tens(22, Six)}}]) { }
                }
            }
            """
        );

    [Fact]
    public void AFieldsHead_HasTheSameFloor() =>
        Oracle.Agrees(
            $$"""
            namespace P;

            public class C {
                int[] G = [{{Tens(25, Six)}}];

                int[] Gggggg = [{{Tens(25, Six)}}];
            }
            """,
            $$"""
            namespace P;

            public class C {
                int[] G = [
                    {{Tens(25, Six)}}
                ];

                int[] Gggggg =
                    [{{Tens(25, Six)}}];
            }
            """
        );

    /// <summary>
    ///     An assignment's head is its own left side — the group starts at it, so the point width is
    ///     the head and no marker is needed; a marker at the group's own first token would be entered
    ///     after the group it serves, and the first cut skipped the floor for exactly this row.
    /// </summary>
    [Fact]
    public void AnAssignmentsHead_IsItsLeftSide() =>
        Oracle.Agrees(
            $$"""
            namespace P;

            public class C {
                void M() {
                    object[] ddddddddd;
                    ddddddddd = [{{Tens(24, Six)}}];
                }
            }
            """,
            $$"""
            namespace P;

            public class C {
                void M() {
                    object[] ddddddddd;
                    ddddddddd = [
                        {{Tens(24, Six)}}
                    ];
                }
            }
            """
        );

    /// <summary>
    ///     ⚠ The head is measured on the output, not the source. The fuzzer widens gaps: a source head
    ///     of <c>var   ddddd   =</c> is seventeen characters and eleven columns once written, and a
    ///     floor read off the syntax would break on pass one and glue on pass two.
    /// </summary>
    [Fact]
    public void WidenedGaps_DoNotWidenTheHead() {
        var widened = $$"""
            namespace P;

            public class C {
                void M() {
                    var   ddddd   =   [{{Tens(24, Six)}}];
                }
            }
            """;

        var normal = $$"""
            namespace P;

            public class C {
                void M() {
                    var ddddd = [{{Tens(24, Six)}}];
                }
            }
            """;

        var once = Format.Text(widened);
        Assert.Equal(Format.Text(normal), once);
        Assert.Contains("        var ddddd = [\n", once, StringComparison.Ordinal);
        Assert.Equal(once, Format.Text(once));
    }

    /// <summary>
    ///     ⚠ A head that already spans lines is measured from the <c>=</c>'s own line, not from the
    ///     owner's first token: the second line of a broken type argument list, <c>int&gt; d =</c>, is
    ///     eight and glues; <c>int&gt; dddddd =</c> is thirteen and breaks. The first cut waived the
    ///     floor for a multi-line head and would have broken the first of these.
    /// </summary>
    [Fact]
    public void AHeadThatSpansLines_IsMeasuredFromItsOwnLine() =>
        Oracle.Agrees(
            $$"""
            namespace P;

            public class C {
                void M() {
                    Dictionary<{{T(100)}}, int> d = [{{Tens(24, Six)}}];
                    Dictionary<{{T(100)}}, int> dddddd = [{{Tens(24, Six)}}];
                }
            }
            """,
            $$"""
            namespace P;

            public class C {
                void M() {
                    Dictionary<{{T(100)}},
                        int> d = [
                        {{Tens(24, Six)}}
                    ];
                    Dictionary<{{T(100)}},
                        int> dddddd =
                        [{{Tens(24, Six)}}];
                }
            }
            """
        );

    /// <summary>The kept form of the eight-column second line stays: a kept break has no floor there either.</summary>
    [Fact]
    public void AKeptBreak_OnAHeadThatSpansLines_HasNoFloor() =>
        Oracle.Agrees(
            $$"""
            namespace P;

            public class C {
                void M() {
                    Dictionary<{{T(100)}},
                        int> d =
                        [{{Tens(24, Six)}}];
                }
            }
            """,
            $$"""
            namespace P;

            public class C {
                void M() {
                    Dictionary<{{T(100)}},
                        int> d =
                        [{{Tens(24, Six)}}];
                }
            }
            """
        );

    /// <summary>A kept break has no floor: #375's <c>int[] x =</c> is nine columns and stays.</summary>
    [Fact]
    public void AKeptBreak_HasNoFloor() =>
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

    // ---- the arrow follows the bracket's fit the same way -------------------------------------

    [Fact]
    public void TheArrow_GluesTheBracket_AtOneHundredAndTwenty() =>
        Oracle.Agrees(
            $$"""
            namespace P;

            public class C {
                object[] {{new string('P', 102)}} => [{{Wide}}];
            }
            """,
            $$"""
            namespace P;

            public class C {
                object[] {{new string('P', 102)}} => [
                    {{Twelve}},
                    {{RestOfThree}}
                ];
            }
            """
        );

    [Fact]
    public void TheArrow_BreaksForAContinuationLineOfOneHundredAndTwenty_AndGluesAtOneHundredAndTwentyOne() =>
        Oracle.Agrees(
            $$"""
            namespace P;

            public class C {
                object[] {{new string('P', 45)}} => [{{Tens(25, Nine)}}];

                object[] {{new string('Q', 45)}} => [{{Tens(26, Six)}}];
            }
            """,
            $$"""
            namespace P;

            public class C {
                object[] {{new string('P', 45)}} =>
                    [{{Tens(25, Nine)}}];

                object[] {{new string('Q', 45)}} => [
                    {{Tens(26, Six)}}
                ];
            }
            """
        );

    [Fact]
    public void AKeptArrowBreak_IsKeptAtOneHundredAndTwenty_AndGivenToTheBracketAtOneHundredAndTwentyOne() =>
        Oracle.Agrees(
            $$"""
            namespace P;

            public class C {
                object[] Q1 =>
            [{{Tens(25, Nine)}}];

                object[] Q2 =>
            [{{Tens(26, Six)}}];
            }
            """,
            $$"""
            namespace P;

            public class C {
                object[] Q1 =>
                    [{{Tens(25, Nine)}}];

                object[] Q2 => [
                    {{Tens(26, Six)}}
                ];
            }
            """
        );

    /// <summary>
    ///     Skala's old arrow output past the margin, given back: the oracle re-joins it to <c>=&gt; [</c>
    ///     at 122 columns rather than move the name down, and so does Skala now. The fill keeps the
    ///     author's break after the eleventh element, as a fill keeps a break after a comma.
    /// </summary>
    [Fact]
    public void TheOldArrowBreak_PastTheMargin_IsReJoined() {
        var source = $$"""
            namespace P;

            public class C {
                object[] {{new string('V', 104)}} =>
                    [
                        {{Eleven}},
                        {{RestOfFour}}
                    ];
            }
            """;

        var expected = $$"""
            namespace P;

            public class C {
                object[] {{new string('V', 104)}} => [
                    {{Eleven}},
                    {{RestOfFour}}
                ];
            }
            """;

        Oracle.Agrees(source, expected);
        Assert.Equal(122, Format.Text(source).Split('\n')[3].Length);
    }

    /// <summary>
    ///     The rule is the bracket's alone: <c>= new[] {</c> at the same column breaks after the <c>=</c>.
    /// </summary>
    [Fact]
    public void AnArrayInitializer_AtOneHundredAndTwenty_BreaksAfterTheEquals() =>
        Oracle.Agrees(
            $$"""
            namespace P;

            public class C {
                void M() {
                    {{T(107)}} v9 = new[] { {{Wide}} };
                }
            }
            """,
            $$"""
            namespace P;

            public class C {
                void M() {
                    {{T(107)}} v9 =
                        new[] {
                            {{Eleven}},
                            {{RestOfFour}}
                        };
                }
            }
            """
        );
}
