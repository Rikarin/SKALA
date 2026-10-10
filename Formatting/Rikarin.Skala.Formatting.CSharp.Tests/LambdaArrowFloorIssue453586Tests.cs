namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     ⚠ A lambda's arrow against the construct in its body (#453, #586; SK-DIV-0050, SK-DIV-0377): a local's lambda
///     over a call breaks its arrow below a measured floor on the argument list, and a sole lambda argument over a
///     binary pattern decides its arrow by the pattern's own constants, weighing the tested expression apart; a
///     lambda call that is the receiver of a further link reads its line to its own <c>)</c>. Every expected
///     string is <c>jb cleanupcode</c>'s, one column either side of a measured boundary.
/// </summary>
public sealed class LambdaArrowFloorIssue453586Tests {
    static string Wrap(string statement) => "class C {\n    void M() {\n" + statement + "    }\n}\n";

    static void Agrees(string statement, string expected) {
        var once = Format.Text(Wrap(statement)).ReplaceLineEndings("\n");
        Assert.Equal(Wrap(expected), once);
        Assert.Equal(once, Format.Text(once).ReplaceLineEndings("\n"));
    }

    static string WrapInAClass(string member) => "class C {\n" + member + "}\n";

    static void AgreesInAClass(string member, string expected) {
        var once = Format.Text(WrapInAClass(member)).ReplaceLineEndings("\n");
        Assert.Equal(WrapInAClass(expected), once);
        Assert.Equal(once, Format.Text(once).ReplaceLineEndings("\n"));
    }

    /// <summary>Head 20, the <c>(</c> at 86: a 53-column argument list moves below the arrow.</summary>
    [Fact]
    public void TwoArguments_UnderTheFloor_BreakTheArrow() =>
        Agrees(
            "        Func<TT> n = () => CCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCC(xxxxxxxxxxxxxxxxxxxxxxxx, yyyyyyyyyyyyyyyyyyyyyyyyy);\n",
            "        Func<TT> n = () =>\n"
            + "            CCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCC(\n"
            + "                xxxxxxxxxxxxxxxxxxxxxxxx,\n"
            + "                yyyyyyyyyyyyyyyyyyyyyyyyy\n"
            + "            );\n"
        );

    /// <summary>One column wider, at the floor of 54: the arguments chop.</summary>
    [Fact]
    public void TwoArguments_AtTheFloor_Chop() =>
        Agrees(
            "        Func<TT> n = () => CCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCC(xxxxxxxxxxxxxxxxxxxxxxxxx, yyyyyyyyyyyyyyyyyyyyyyyyy);\n",
            "        Func<TT> n = () => CCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCC(\n"
            + "            xxxxxxxxxxxxxxxxxxxxxxxxx,\n"
            + "            yyyyyyyyyyyyyyyyyyyyyyyyy\n"
            + "        );\n"
        );

    /// <summary>Head 40, the <c>(</c> at 77, nearer the head than 38 columns: the arguments always chop.</summary>
    [Fact]
    public void TwoArguments_BeforeTheDrop_Chop() =>
        Agrees(
            "        Func<TTTTTTTTTTTTTTTTTTTTTT> n = () => CCCCCCCCCCCCCCCCCCCCCCCCCCCCC(xxxxxxxxxxxxxxxxxxxxxxx, yyyyyyyyyyyyyyyyyyyyyyy);\n",
            "        Func<TTTTTTTTTTTTTTTTTTTTTT> n = () => CCCCCCCCCCCCCCCCCCCCCCCCCCCCC(\n"
            + "            xxxxxxxxxxxxxxxxxxxxxxx,\n"
            + "            yyyyyyyyyyyyyyyyyyyyyyy\n"
            + "        );\n"
        );

    /// <summary>Head 40, the <c>(</c> at 78: the floor is 65, and a 50-column list moves below the arrow.</summary>
    [Fact]
    public void TwoArguments_PastTheDrop_BreakTheArrow() =>
        Agrees(
            "        Func<TTTTTTTTTTTTTTTTTTTTTT> n = () => CCCCCCCCCCCCCCCCCCCCCCCCCCCCCC(xxxxxxxxxxxxxxxxxxxxxxx, yyyyyyyyyyyyyyyyyyyyyyy);\n",
            "        Func<TTTTTTTTTTTTTTTTTTTTTT> n = () =>\n"
            + "            CCCCCCCCCCCCCCCCCCCCCCCCCCCCCC(xxxxxxxxxxxxxxxxxxxxxxx, yyyyyyyyyyyyyyyyyyyyyyy);\n"
        );

    /// <summary>One argument, head 20, the <c>(</c> at 86: its floor is 75.</summary>
    [Fact]
    public void OneArgument_UnderItsFloor_BreaksTheArrow() =>
        Agrees(
            "        Func<TT> n = () => CCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCC(xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx);\n",
            "        Func<TT> n = () =>\n"
            + "            CCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCC(\n"
            + "                xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx\n"
            + "            );\n"
        );

    /// <summary>One column wider: it chops.</summary>
    [Fact]
    public void OneArgument_AtItsFloor_Chops() =>
        Agrees(
            "        Func<TT> n = () => CCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCC(xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx);\n",
            "        Func<TT> n = () => CCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCC(\n"
            + "            xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx\n"
            + "        );\n"
        );

    /// <summary>
    ///     <c>static node =&gt; x is A…</c>, a 32-column first operand, on a 200-column line: the arrow ending at 53
    ///     stays.
    /// </summary>
    [Fact]
    public void PatternArrow_BelowTheCeiling_Stays() =>
        Agrees(
            "        UUUUUUUUUUUUUUUUUUUUUUUUUUUUUU(static node => x is AAAAAAAAAAAAAAAAAAAAAAAAAAA or BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB or CCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCC);\n",
            "        UUUUUUUUUUUUUUUUUUUUUUUUUUUUUU(static node => x is AAAAAAAAAAAAAAAAAAAAAAAAAAA\n"
            + "            or BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB\n"
            + "            or CCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCC\n"
            + "        );\n"
        );

    /// <summary>Ending at 54, the pattern's ceiling, it breaks (an operand chain's ceiling would be 45).</summary>
    [Fact]
    public void PatternArrow_AtTheCeiling_Breaks() =>
        Agrees(
            "        UUUUUUUUUUUUUUUUUUUUUUUUUUUUUUU(static node => x is AAAAAAAAAAAAAAAAAAAAAAAAAAA or BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB or CCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCC);\n",
            "        UUUUUUUUUUUUUUUUUUUUUUUUUUUUUUU(static node =>\n"
            + "            x is AAAAAAAAAAAAAAAAAAAAAAAAAAA\n"
            + "                or BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB\n"
            + "                or CCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCC\n"
            + "        );\n"
        );

    /// <summary>
    ///     A twelve-column tested expression, <c>abcdefghijkl is A…</c>, a 28-column first operand: the arrow ending at
    ///     40 stays.
    /// </summary>
    [Fact]
    public void WideTestedExpression_LowersTheCeiling_Stays() =>
        Agrees(
            "        UUUUUUUUUUUUUUUUU(static node => abcdefghijkl is AAAAAAAAAAAA or BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB or CCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCC);\n",
            "        UUUUUUUUUUUUUUUUU(static node => abcdefghijkl is AAAAAAAAAAAA\n"
            + "            or BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB\n"
            + "            or CCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCC\n"
            + "        );\n"
        );

    /// <summary>Ending at 41 it breaks, ten columns before <c>x is A…</c> of the same width would.</summary>
    [Fact]
    public void WideTestedExpression_LowersTheCeiling_Breaks() =>
        Agrees(
            "        UUUUUUUUUUUUUUUUUU(static node => abcdefghijkl is AAAAAAAAAAAA or BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB or CCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCC);\n",
            "        UUUUUUUUUUUUUUUUUU(static node =>\n"
            + "            abcdefghijkl is AAAAAAAAAAAA\n"
            + "                or BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB\n"
            + "                or CCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCC\n"
            + "        );\n"
        );

    /// <summary>
    ///     A lambda call that is the receiver of <c>.ToList()</c>: the line is read to the call's <c>)</c>, and the arrow
    ///     breaks.
    /// </summary>
    [Fact]
    public void ReceiverOfAFurtherLink_BreaksTheArrow() =>
        Agrees(
            "        var g = iiiiiiiiiiiiiiiii.Where(static node => x is AAAAAAAAAAAAAAAAAAAAAAAAAAA or BBBBBBBBBBBBBBBBBBBB or CCCCCCCCCCCCCCCCCCCC).ToList();\n",
            "        var g = iiiiiiiiiiiiiiiii.Where(static node =>\n"
            + "                x is AAAAAAAAAAAAAAAAAAAAAAAAAAA or BBBBBBBBBBBBBBBBBBBB or CCCCCCCCCCCCCCCCCCCC\n"
            + "            )\n"
            + "            .ToList();\n"
        );

    /// <summary>A 26-column first operand and the arrow ending at 39, under the ceiling: the pattern chops beside it.</summary>
    [Fact]
    public void ReceiverOfAFurtherLink_BelowTheCeiling_Chops() =>
        Agrees(
            "        var g = ii.Where(static node => x is AAAAAAAAAAAAAAAAAAAAA or BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB or CCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCC).ToList();\n",
            "        var g = ii.Where(static node => x is AAAAAAAAAAAAAAAAAAAAA\n"
            + "                or BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB\n"
            + "                or CCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCC\n"
            + "            )\n"
            + "            .ToList();\n"
        );

    /// <summary>A 22-column name under a 32-column type: the `=` reaches 31 columns past the margin.</summary>
    [Fact]
    public void WideName_WithinTheReach_BreaksTheEquals() =>
        Agrees(
            "        Func<TTTTTTTTTTTTTTTTTTTTTTTTTT> nnnnnnnnnnnnnnnnnnnnnn = () => CCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCC(xxxxxxxxxxxxxxx, yyyyyyyyyyyyyyyy);\n",
            "        Func<TTTTTTTTTTTTTTTTTTTTTTTTTT> nnnnnnnnnnnnnnnnnnnnnn =\n"
            + "            () => CCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCC(xxxxxxxxxxxxxxx, yyyyyyyyyyyyyyyy);\n"
        );

    /// <summary>One column further, and the `=` stays; the arrow decides.</summary>
    [Fact]
    public void WideName_PastTheReach_KeepsTheEquals() =>
        Agrees(
            "        Func<TTTTTTTTTTTTTTTTTTTTTTTTTT> nnnnnnnnnnnnnnnnnnnnnn = () => CCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCC(xxxxxxxxxxxxxxxx, yyyyyyyyyyyyyyyy);\n",
            "        Func<TTTTTTTTTTTTTTTTTTTTTTTTTT> nnnnnnnnnnnnnnnnnnnnnn = () =>\n"
            + "            CCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCC(xxxxxxxxxxxxxxxx, yyyyyyyyyyyyyyyy);\n"
        );

    /// <summary>A ten-column name: the `=` never breaks before a lambda over a call.</summary>
    [Fact]
    public void TenColumnName_NeverBreaksTheEquals() =>
        Agrees(
            "        Func<TTTTTTTTTTTTTT> nnnnnnnnnn = () => CCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCC(xxxx, yyyyy);\n",
            "        Func<TTTTTTTTTTTTTT> nnnnnnnnnn = () =>\n"
            + "            CCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCC(xxxx, yyyyy);\n"
        );

    /// <summary>A field: the `=` stays and the arrow breaks below its floor.</summary>
    [Fact]
    public void Field_KeepsTheEquals_AndBreaksTheArrow() =>
        AgreesInAClass(
            "    Func<TTTTTTTTTTTTTTTTTTTTTTTTT> nnnnnnnnn = () => CCCCCCCCCCCCC(ppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppp);\n",
            "    Func<TTTTTTTTTTTTTTTTTTTTTTTTT> nnnnnnnnn = () =>\n"
            + "        CCCCCCCCCCCCC(ppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppp);\n"
        );

    /// <summary>An assignment to a target of three columns or fewer chops the arguments, whatever their width.</summary>
    [Fact]
    public void ShortAssignmentTarget_AlwaysChops() =>
        Agrees(
            "        nn = () => CCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCC(pppppppppppppppp, qqqqqqqqqqqqqqqqq);\n",
            "        nn = () => CCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCC(\n"
            + "            pppppppppppppppp,\n"
            + "            qqqqqqqqqqqqqqqqq\n"
            + "        );\n"
        );

    /// <summary>A receiver lambda's <c>&amp;&amp;</c> chain chopped beside the arrow: one level past the `)`.</summary>
    [Fact]
    public void ReceiverLambda_OperandChain_TakesItsOwnLevel() =>
        Agrees(
            "        var g = ii.Where(nnnnnnnnnnnnnnnnnnnn => aaaaaaaaaaaaaaaaaaaa && bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb && ccccccccccccccccccccccccccccccccccccc).ToList();\n",
            "        var g = ii.Where(nnnnnnnnnnnnnnnnnnnn => aaaaaaaaaaaaaaaaaaaa\n"
            + "                && bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb\n"
            + "                && ccccccccccccccccccccccccccccccccccccc\n"
            + "            )\n"
            + "            .ToList();\n"
        );
}
