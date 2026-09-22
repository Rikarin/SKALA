namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     Issue #378: a property pattern heading a switch expression arm or a case label, the arm's
///     <c>=&gt;</c>, the gap before a <c>when</c>, and the lambda's <c>=&gt;</c>. Every expected string is
///     <c>jb cleanupcode</c>'s own output for the input, measured 2026-09-22 with <c>Testing ask</c>,
///     except where a remark says otherwise; <c>constructs/breaks/property-pattern-head.cs</c> and
///     <c>constructs/breaks/case-label-when.cs</c> hold the same shapes as fixtures.
/// </summary>
/// <remarks>
///     ⚠ Three Nightly seeds chopped the pattern on pass one and joined it on pass two. Neither side
///     of the arm's arrow had a plan, nor the gap before a <c>when</c>, so the pattern measured its
///     rest-of-line through the arrow into the body — through a type argument list's yielding points
///     to the first argument — and on pass two the body's fill had left a kept break that ended the
///     measure. The oracle decides the pattern by its own extent up to the next movable break, and
///     the arrow by a rule that is <em>not</em> the <c>=</c>'s: the body moves down exactly when the
///     head up to the body's first break point has no room, never to spare the construct inside the
///     body a break. ⚠ The issue's "in a case label the chopped pattern fills, in an arm it chops" was
///     refuted: it is one braces-first rule everywhere, and a bare <c>{</c> at a line start simply
///     never has room on its continuation line when the whole did not fit.
/// </remarks>
public sealed class ArmArrowIssue378Tests {
    const string Ten = "(first, second, third, fourth, fifth, sixth, seventh, eighth, ninth, tenth)";

    const string Six = "(firstArgumentValue, secondArgumentValue, thirdArgumentValue,"
        + " fourthArgumentValue, fifthArgumentValue, sixthArgumentValue)";

    const string Seven = "{ Length: > 3, Name: \"ssssssssssssssssssssssssssssss\", Kind: not null,"
        + " Value.Length: > 2, Other: \"ssssssssssssssssssssssssssssssssss\", Last: 1, Tail: \"ssssssssssssssssss\" }";

    /// <summary>A two-subpattern property pattern whose <c>}</c> lands on <paramref name="column" />.</summary>
    static string Pattern(int column, string before = "", int indent = 12) {
        var head = new string(' ', indent) + before + "{ Length: > 0, Name: \"";
        return head + new string('s', column - head.Length - 3) + "\" }";
    }

    static string Switch(params string[] arms) =>
        "class C {\n    void M() {\n        var r = value switch {\n"
        + string.Join("\n", arms)
        + "\n            _ => 0\n        };\n    }\n}\n";

    static string Cases(params string[] labels) =>
        "class C {\n    void M() {\n        switch (value) {\n"
        + string.Join("\n", labels.Select(static l => l + "\n                break;"))
        + "\n            default:\n                break;\n        }\n    }\n}\n";

    /// <summary>
    ///     The first seed's arm, reduced: the pattern stays whole and the arrow breaks, in one pass.
    ///     ⚠ Not an oracle-equality test: the oracle breaks the type argument list inside its tuple
    ///     type (SK-DIV-0024's family, recorded under SK-DIV-0119), and Skala fills the list at its
    ///     outer comma. The pattern and the arrow — the lines the seed was about — are the oracle's.
    /// </summary>
    [Fact]
    public void TheFirstSeedsArm_KeepsThePattern_AndSettlesInOnePass() {
        const string cast = "Cast<IReadOnlyDictionary<(decimal First, StringBuilder Second),"
            + " (decimal First, StringBuilder Second)>, TimeSpan>";
        const string arguments = "(out var o143, name144: 48278, name145: \"ssssssssssssssssss\","
            + " name146: \"sssssssssssss\")";
        var once = Format.Text(Switch($"            {{ Length: > 0 }} => {cast}{arguments},"));
        Assert.Equal(once, Format.Text(once));
        Assert.Contains("            { Length: > 0 } =>\n                Cast<", once, StringComparison.Ordinal);
        Assert.Equal(0, Format.OwnerUnresolved(once));
    }

    /// <summary>
    ///     Rule 1 and rule 3 together, at the boundary the oracle draws: <c>}</c> at 117 leaves
    ///     <c> =&gt;</c> at 120 and the body goes below the arrow; at 118 and 120 the arrow itself has no
    ///     room and moves down with the body on its line; at 121 the pattern's own extent overflows
    ///     and it chops — one subpattern per line, because two of them do not fit at the continuation
    ///     column either.
    /// </summary>
    [Theory]
    [InlineData(117, "after")]
    [InlineData(118, "before")]
    [InlineData(120, "before")]
    [InlineData(121, "chops")]
    public void TheArmsArrow_IsNeverLeftPastTheMargin_AndThePatternChopsOnlyByItsOwnExtent(int close, string shape) {
        var pattern = Pattern(close);
        var expected = shape switch {
            "after" => $"{pattern} =>\n                Body{Ten},",
            "before" => $"{pattern}\n                => Body{Ten},",
            _ => "            {\n                Length: > 0,\n                Name: \""
                + new string('s', close - 37)
                + $"\"\n            }} => Body{Ten},"
        };

        Oracle.Agrees(Switch($"{pattern} => Body{Ten},"), Switch(expected));
    }

    /// <summary>
    ///     ⚠ The arrow is not the <c>=</c>'s <c>PrefersOuterBreak</c>. <c>Body(first, …, fifth),</c> would
    ///     fit whole on the continuation line — the <c>=</c>'s first question would take the outer break —
    ///     and the oracle keeps <c>=&gt; Body(</c> on the head's line and chops the arguments; the same
    ///     with a short head and six arguments. Only a body whose head does not fit moves down.
    /// </summary>
    [Fact]
    public void TheArrow_StaysWhenTheBodysHeadFits_AndTheArgumentsChop() =>
        Oracle.Agrees(
            Switch(
                $"            1 => Body{Six},",
                "            SomeLongConstantName.SomeMemberValue.AnotherMemberName.YetAnother"
                + " => Body(first, second, third, fourth, fifth),"
            ),
            Switch(
                """
                            1 => Body(
                                firstArgumentValue,
                                secondArgumentValue,
                                thirdArgumentValue,
                                fourthArgumentValue,
                                fifthArgumentValue,
                                sixthArgumentValue
                            ),
                            SomeLongConstantName.SomeMemberValue.AnotherMemberName.YetAnother => Body(
                                first,
                                second,
                                third,
                                fourth,
                                fifth
                            ),
                """
            )
        );

    /// <summary>A body with no break point of its own moves down whole when the head is unbreakable.</summary>
    [Fact]
    public void ABodyWithNoBreakPoint_MovesDown() =>
        Oracle.Agrees(
            Switch(
                "            SomeLongConstantName.SomeMemberValue.AnotherMemberName.YetAnotherOne.AndMore"
                + " => SomeVeryLongIdentifierWithNoBreakPointsInsideItAtAll,"
            ),
            Switch(
                """
                            SomeLongConstantName.SomeMemberValue.AnotherMemberName.YetAnotherOne.AndMore =>
                                SomeVeryLongIdentifierWithNoBreakPointsInsideItAtAll,
                """
            )
        );

    /// <summary>
    ///     ⚠ And a head that can break is measured through such a body: <c>{ … } =&gt; 2u,</c> at 122
    ///     chops the pattern — braces first — and leaves the arrow alone. SK-DIV-0121 records the
    ///     wider bodies for which the oracle moves the body down instead.
    /// </summary>
    [Fact]
    public void AShortBody_ChopsThePatternRatherThanTheArrow() =>
        Oracle.Agrees(
            Switch($"{Pattern(115)} => 2u,"),
            Switch(
                "            {\n                Length: > 0, Name: \""
                + new string('s', 78)
                + "\"\n            } => 2u,"
            )
        );

    /// <summary>A break the author wrote on either side of the arrow is kept even when everything fits.</summary>
    [Fact]
    public void KeptArrowBreaks_Stay() {
        var source = Switch(
            "            1 =>\n                Body(first),",
            "            2\n                => Body(second),"
        );
        Oracle.Agrees(source, source);
    }

    /// <summary>
    ///     A case label's colon cannot move: <c>}</c> at 119 stays and at 120 — the colon at 121 — the
    ///     pattern breaks, braces first, with both subpatterns on the continuation line.
    /// </summary>
    [Fact]
    public void ACaseLabelsColon_IsNotAMovableBreak() =>
        Oracle.Agrees(
            Cases($"{Pattern(119, "case ")}:", $"{Pattern(120, "case ")}:"),
            Cases(
                $"{Pattern(119, "case ")}:",
                "            case {\n                Length: > 0, Name: \""
                + new string('s', 78)
                + "\"\n            }:"
            )
        );

    /// <summary>
    ///     ⚠ Refutes the issue's fill: seven subpatterns whose first four would have shared a line come
    ///     back one per line, in a case label and in an arm alike. The split is braces first, then a
    ///     chop when the subpatterns still do not fit at the continuation column — and the same under
    ///     <c>is</c> and after a type, where two subpatterns that do fit share the line.
    /// </summary>
    [Fact]
    public void ThePropertyPattern_PutsItsBracesApartFirst_AndChopsOnlyWhatStillOverflows() {
        const string chopped = "                Length: > 3,\n"
            + "                Name: \"ssssssssssssssssssssssssssssss\",\n"
            + "                Kind: not null,\n"
            + "                Value.Length: > 2,\n"
            + "                Other: \"ssssssssssssssssssssssssssssssssss\",\n"
            + "                Last: 1,\n"
            + "                Tail: \"ssssssssssssssssss\"\n";
        Oracle.Agrees(Cases($"            case {Seven}:"), Cases($"            case {{\n{chopped}            }}:"));
        Oracle.Agrees(
            Switch($"            SomeType {Seven} => 1,", $"{Pattern(121, "SomeType ")} => 1,"),
            Switch(
                $"            SomeType {{\n{chopped}            }} => 1,",
                "            SomeType {\n                Length: > 0, Name: \""
                + new string('s', 75)
                + "\"\n            } => 1,"
            )
        );
        Oracle.Agrees(
            $"class C {{\n    void M() {{\n{Pattern(120, "var b = value is ", 8)};\n    }}\n}}\n",
            "class C {\n    void M() {\n        var b = value is {\n            Length: > 0, Name: \""
            + new string('s', 70)
            + "\"\n        };\n    }\n}\n"
        );
    }

    /// <summary>
    ///     The <c>when</c> gap is a break point, and it follows the arrow's rule: it moves down when
    ///     <c>when Bind(</c> has no room after a pattern ending at 120, and stays — the arguments chop
    ///     below it — after a declaration pattern although the whole clause would have fitted on the
    ///     continuation line. At 121 the pattern chops and the clause stays on the <c>}</c> line.
    /// </summary>
    [Fact]
    public void TheWhenGap_MovesOnlyWhenItsHeadHasNoRoom() =>
        Oracle.Agrees(
            Cases(
                $"{Pattern(120, "case ")} when Bind{Ten}:",
                $"{Pattern(121, "case ")} when Bind{Ten}:",
                "            case SomeVeryLongTypeName someVeryLongVariableName"
                + " when Bind(first, second, third, fourth, fifth, sixth, seventh):"
            ),
            Cases(
                $"{Pattern(120, "case ")}\n                when Bind{Ten}:",
                "            case {\n                Length: > 0, Name: \""
                + new string('s', 79)
                + $"\"\n            }} when Bind{Ten}:",
                """
                            case SomeVeryLongTypeName someVeryLongVariableName when Bind(
                                first,
                                second,
                                third,
                                fourth,
                                fifth,
                                sixth,
                                seventh
                            ):
                """
            )
        );

    /// <summary>
    ///     The third seed's label: the lambda's arrow is a break point, so the <c>when</c> measures its
    ///     head up to it and stays, and the lambda's body moves down. A kept break before a <c>when</c>
    ///     is kept.
    /// </summary>
    [Fact]
    public void TheLambdasArrow_KeepsTheWhenOnTheLabelsLine() =>
        Oracle.Agrees(
            Cases(
                "            case { Length: > 3, Name: \"sssssssssssssssssssssss\" } when static x =>"
                + " Convert<CancellationToken, CancellationToken>(x, cancellationToken, anotherArgument):",
                "            case 1\n                when x:"
            ),
            Cases(
                """
                            case { Length: > 3, Name: "sssssssssssssssssssssss" } when static x =>
                                Convert<CancellationToken, CancellationToken>(x, cancellationToken, anotherArgument):
                """,
                "            case 1\n                when x:"
            )
        );

    /// <summary>
    ///     The second seed's label, reduced: the declaration pattern stays whole at 61 columns and the
    ///     <c>when</c> moves down with its argument list chopped, as the oracle writes it.
    /// </summary>
    [Fact]
    public void TheSecondSeedsLabel_MovesTheWhenDown() =>
        Oracle.Agrees(
            Cases(
                "            case decimal { P42: Task<double> { P45: null } } when Bind<Lazy<Dictionary<string, bool>>,"
                + " IReadOnlyList<IReadOnlyDictionary<TimeSpan, TimeSpan>>>(name48: x49 => (true ? 60582 : 39311)):"
            ),
            Cases(
                "            case decimal { P42: Task<double> { P45: null } }\n"
                + "                when Bind<Lazy<Dictionary<string, bool>>,"
                + " IReadOnlyList<IReadOnlyDictionary<TimeSpan, TimeSpan>>>(\n"
                + "                    name48: x49 => (true ? 60582 : 39311)\n"
                + "                ):"
            )
        );

    /// <summary>
    ///     A <c>when</c> inside an arm reads the same way, and the arm's arrow after it is never left
    ///     past the margin: <c>=&gt;</c> ending at 120 stays, at 121 it moves down with the body.
    /// </summary>
    [Fact]
    public void AWhenInsideAnArm_AndTheArrowAfterIt() {
        var at120 = "            SomeLongConstant.Value when " + new string('x', 77);
        var at121 = "            SomeLongConstant.Value when " + new string('x', 78);
        const string six = "                    firstArgumentValue,\n"
            + "                    secondArgumentValue,\n"
            + "                    thirdArgumentValue,\n"
            + "                    fourthArgumentValue,\n"
            + "                    fifthArgumentValue,\n"
            + "                    sixthArgumentValue\n";
        Oracle.Agrees(
            Switch(
                $"{Pattern(101)} when Bind(first, second, third, fourth, fifth, sixth, seventh) => Body(first),",
                $"{at120} => Body{Six},",
                $"{at121} => Body{Six},"
            ),
            Switch(
                $"{Pattern(101)} when Bind(\n                first,\n                second,\n                third,\n"
                + "                fourth,\n                fifth,\n                sixth,\n                seventh\n"
                + "            ) => Body(first),",
                $"{at120} =>\n                Body(\n{six}                ),",
                $"{at121}\n                => Body(\n{six}                ),"
            )
        );
    }

    /// <summary>
    ///     The lambda's arrow outside a <c>when</c>: the arguments chop when the body's head fits, in a
    ///     local and in a sole lambda argument. ⚠ The oracle also moves a body down whose head fits —
    ///     from a column that moves with the indent, and always for a one-argument body — by a rule
    ///     SK-DIV-0122 records and this plan does not model; the shapes here are the ones both agree on.
    /// </summary>
    [Fact]
    public void TheLambdasArrow_FollowsTheArmsRule() =>
        Oracle.Agrees(
            "class C {\n    void M() {\n"
            + "        Func<int, int> f = x => Convert(someParameterName, cancellationToken, anotherArgument,"
            + " yetAnotherArgument, andOneMore, last);\n"
            + "        var thrown = Assert.Throws<ArgumentException>(() => FoliageGrowth.Simulate(volume,"
            + " Ground.Flat, Field with { Size = new(0f, 100f) }));\n"
            + "    }\n}\n",
            """
            class C {
                void M() {
                    Func<int, int> f = x => Convert(
                        someParameterName,
                        cancellationToken,
                        anotherArgument,
                        yetAnotherArgument,
                        andOneMore,
                        last
                    );
                    var thrown = Assert.Throws<ArgumentException>(() => FoliageGrowth.Simulate(
                            volume,
                            Ground.Flat,
                            Field with { Size = new(0f, 100f) }
                        )
                    );
                }
            }
            """
        );

    /// <summary>
    ///     ⚠ A named argument is not the single lambda argument the placement key keeps on the call's
    ///     line: <c>(name48: x49 =&gt; …)</c> breaks after the <c>(</c> like <c>(name48: (…))</c> does,
    ///     while <c>(x49 =&gt; …)</c> stays and breaks its arrow. Found on the second seed's label.
    /// </summary>
    [Fact]
    public void ANamedLambdaArgument_IsNotTheSoleLambda() {
        const string label = "            case decimal { P42: Task<double> { P45: null } } when"
            + " Bind<Lazy<Dictionary<string, bool>>, IReadOnlyList<IReadOnlyDictionary<TimeSpan, TimeSpan>>>";
        const string moved = "            case decimal { P42: Task<double> { P45: null } }\n"
            + "                when Bind<Lazy<Dictionary<string, bool>>,"
            + " IReadOnlyList<IReadOnlyDictionary<TimeSpan, TimeSpan>>>";
        Oracle.Agrees(
            Cases($"{label}(x49 => (true ? 60582 : 39311)):", $"{label}(name48: (true ? 60582 : 39311)):"),
            Cases(
                $"{moved}(x49 =>\n                    (true ? 60582 : 39311)\n                ):",
                $"{moved}(\n                    name48: (true ? 60582 : 39311)\n                ):"
            )
        );
    }

    /// <summary>
    ///     Unchanged and kept: an argument list, an initializer and a pattern under <c>is</c> are not
    ///     chopped when only the tail overflows — the operator or the <c>?</c> breaks.
    /// </summary>
    [Fact]
    public void TheOrderingRule_StillHoldsOutsideTheArm() =>
        Oracle.Agrees(
            "class C {\n    void M() {\n"
            + "        var total = Compute(firstArgument, secondArgument) + someOtherRatherLongValueName"
            + " + yetAnotherLongValueName + more;\n"
            + "        var other = new Thing { Alpha = 1, Beta = 2 } + someOtherRatherLongValueName"
            + " + yetAnotherLongValueName + moreValues;\n"
            + "        var third = value is { Length: > 0, Name: \"sssssssssssss\" }"
            + " ? someRatherLongExpressionForTheTrueBranch(first) : other;\n"
            + "    }\n}\n",
            """
            class C {
                void M() {
                    var total = Compute(firstArgument, secondArgument)
                        + someOtherRatherLongValueName
                        + yetAnotherLongValueName
                        + more;
                    var other = new Thing { Alpha = 1, Beta = 2 }
                        + someOtherRatherLongValueName
                        + yetAnotherLongValueName
                        + moreValues;
                    var third = value is { Length: > 0, Name: "sssssssssssss" }
                        ? someRatherLongExpressionForTheTrueBranch(first)
                        : other;
                }
            }
            """
        );
}
