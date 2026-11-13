namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     ⚠ A held lambda call behind an <c>=</c>, round four of #619 (SK-DIV-0510): the moved call's own layout, the
///     margin's reading under every call head, and the <c>=</c> before a value that fits below by the held chain's own
///     table. Every expected string is <c>jb cleanupcode</c>'s.
/// </summary>
public sealed class HeldLambdaCallLayoutIssue619Tests {
    static string Wrap(string body) => "class C {\n    void M() {\n" + body + "    }\n}\n";

    static void Agrees(string source, string expected) {
        var once = Format.Text(Wrap(source)).ReplaceLineEndings("\n");
        Assert.Equal(Wrap(expected), once);
        Assert.Equal(once, Format.Text(once).ReplaceLineEndings("\n"));
    }

    /// <summary>
    ///     A sole lambda's arrow ending eleven columns past its line's start stays, at indent 12 as at 8: the operands
    ///     chop.
    /// </summary>
    [Fact]
    public void AnArrowElevenColumnsPastTheLine_Stays() =>
        Agrees(
            "        if (b) {\n"
            + "            UUUUUU(x => aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa && bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb);\n"
            + "        }\n",
            "        if (b) {\n"
            + "            UUUUUU(x => aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa\n"
            + "                && bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb\n"
            + "            );\n"
            + "        }\n"
        );

    /// <summary>
    ///     A moved <c>.Where(</c> keeps <c>x =&gt; a</c> and chops the <c>&amp;&amp;</c> operands one level past its
    ///     line.
    /// </summary>
    [Fact]
    public void AMovedCall_KeepsItsArrowAndChopsOneLevelIn() =>
        Agrees(
            "        var nnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnn = rrrrrrrrr.Where(x => aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa && bbbbbbbbbbbbbbbbbbbb && cccccccccccccccccccc && dddddddddddddddddddd).TTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTT();\n",
            "        var nnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnn = rrrrrrrrr\n"
            + "            .Where(x => aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa\n"
            + "                && bbbbbbbbbbbbbbbbbbbb\n"
            + "                && cccccccccccccccccccc\n"
            + "                && dddddddddddddddddddd\n"
            + "            )\n"
            + "            .TTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTT();\n"
        );

    /// <summary>A moved call over a pattern chain: the <c>or</c>s one level past the call's line.</summary>
    [Fact]
    public void AMovedPatternCall_KeepsItsArrowOneLevelIn() =>
        Agrees(
            "        var nnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnn = rrrrrrrrr.Where(x => x is AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA or BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB or CCCCCCCCCCCCCCCCCCCCCCCCC).TTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTT();\n",
            "        var nnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnn = rrrrrrrrr\n"
            + "            .Where(x => x is AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA\n"
            + "                or BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB\n"
            + "                or CCCCCCCCCCCCCCCCCCCCCCCCC\n"
            + "            )\n"
            + "            .TTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTT();\n"
        );

    /// <summary>A moved call whose arrow breaks: the <c>or</c>s one level past the body's line.</summary>
    [Fact]
    public void AMovedPatternCall_BrokenArrow_OneLevelPastTheBody() =>
        Agrees(
            "        var nnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnn = rrrrrrrrr.SkipWhile(x => x is AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA or BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB or CCCCCCCCCCCCCCCCCCCCCCCCC).TTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTT();\n",
            "        var nnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnn = rrrrrrrrr\n"
            + "            .SkipWhile(x =>\n"
            + "                x is AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA\n"
            + "                    or BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB\n"
            + "                    or CCCCCCCCCCCCCCCCCCCCCCCCC\n"
            + "            )\n"
            + "            .TTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTT();\n"
        );

    /// <summary>Pass two's input: behind a dot the author broke, the same as the moved call.</summary>
    [Fact]
    public void ADotTheAuthorBroke_PatternOneLevelIn() =>
        Agrees(
            "        var nnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnn = rrrrrrrrr\n"
            + "            .Where(x => x is AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA or BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB or CCCCCCCCCCCCCCCCCCCCCCCCC).TTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTT();\n",
            "        var nnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnn = rrrrrrrrr\n"
            + "            .Where(x => x is AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA\n"
            + "                or BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB\n"
            + "                or CCCCCCCCCCCCCCCCCCCCCCCCC\n"
            + "            )\n"
            + "            .TTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTT();\n"
        );

    /// <summary><c>.Any(</c> past the margin is read as the margin too: the call moves and keeps its arrow.</summary>
    [Fact]
    public void AnAnyCallPastTheMargin_Moves() =>
        Agrees(
            "        if (b) {\n"
            + "            if (b) {\n"
            + "                var nnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnn = rrrrrr.Any(x => aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa && bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb).TTTTTTTTTTTTTTTTTTTTTTTTTTTTTT();\n"
            + "            }\n"
            + "        }\n",
            "        if (b) {\n"
            + "            if (b) {\n"
            + "                var nnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnn = rrrrrr\n"
            + "                    .Any(x => aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa\n"
            + "                        && bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb\n"
            + "                    )\n"
            + "                    .TTTTTTTTTTTTTTTTTTTTTTTTTTTTTT();\n"
            + "            }\n"
            + "        }\n"
        );

    /// <summary>
    ///     <c>.SkipWhile(</c> three columns over: the line as it is moves the call, where the margin's reading alone did
    ///     not.
    /// </summary>
    [Fact]
    public void AWideHeadNearTheMargin_MovesByTheLineAsItIs() =>
        Agrees(
            "        if (b) {\n"
            + "            nnnn = rrrrrrr.SkipWhile(x => aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa && bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb).TTTTTTTTTTTTTTTTTTTT();\n"
            + "        }\n",
            "        if (b) {\n"
            + "            nnnn = rrrrrrr\n"
            + "                .SkipWhile(x =>\n"
            + "                    aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa && bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb\n"
            + "                )\n"
            + "                .TTTTTTTTTTTTTTTTTTTT();\n"
            + "        }\n"
        );

    /// <summary>Below a broken <c>=</c> the receiver is the line's start: the call stays with it and breaks its arrow.</summary>
    [Fact]
    public void AShortReachBelowABrokenEquals_KeepsTheCall() =>
        Agrees(
            "        if (b) {\n"
            + "            var nnnnnn = r.OrderBy(x => aaaaaaaaaaaaaaaaaaaaa && bbbbbbbbbbbbbbbbbbbbb && cccccccccccccccccccc && dddddddddddddddddddd).TTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTT();\n"
            + "        }\n",
            "        if (b) {\n"
            + "            var nnnnnn =\n"
            + "                r.OrderBy(x =>\n"
            + "                        aaaaaaaaaaaaaaaaaaaaa && bbbbbbbbbbbbbbbbbbbbb && cccccccccccccccccccc && dddddddddddddddddddd\n"
            + "                    )\n"
            + "                    .TTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTT();\n"
            + "        }\n"
        );

    /// <summary>The value fits below at 112, the limit for a 24-column name and a 6-column receiver: the <c>=</c> breaks.</summary>
    [Fact]
    public void FitsBelow_AtTheLimit_BreaksTheEquals() =>
        Agrees(
            "        var nnnnnnnnnnnnnnnnnnnnnnnn = rrrrrr.Where(x => aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa && bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb).ToList();\n",
            "        var nnnnnnnnnnnnnnnnnnnnnnnn =\n"
            + "            rrrrrr.Where(x => aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa && bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb).ToList();\n"
        );

    /// <summary>A column further: the <c>=</c> stays and the call moves.</summary>
    [Fact]
    public void FitsBelow_OnePastTheLimit_MovesTheCall() =>
        Agrees(
            "        var nnnnnnnnnnnnnnnnnnnnnnnn = rrrrrr.Where(x => aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa && bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb).ToList();\n",
            "        var nnnnnnnnnnnnnnnnnnnnnnnn = rrrrrr\n"
            + "            .Where(x => aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa && bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb)\n"
            + "            .ToList();\n"
        );

    /// <summary><c>9·12 + 8·0 − 3·4 ≤ 153</c>: the <c>=</c> never breaks, however short the value.</summary>
    [Fact]
    public void FitsBelow_NarrowHead_NeverBreaks() =>
        Agrees(
            "        nnnnnnnnnnnn = rrrr.Where(x => aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa && bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb).ToList();\n",
            "        nnnnnnnnnnnn = rrrr.Where(x => aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa && bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb)\n"
            + "            .ToList();\n"
        );

    /// <summary>Group I's short fragment: the <c>=</c> breaks with the value ending at 119.</summary>
    [Fact]
    public void FitsBelow_ShortReceiver_BreaksTheEquals() =>
        Agrees(
            "        nnnnnnnnnnnnnnnnnnnnnnnnnnnn = r.Where(x => aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa && bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb).ToList();\n",
            "        nnnnnnnnnnnnnnnnnnnnnnnnnnnn =\n"
            + "            r.Where(x => aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa && bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb).ToList();\n"
        );

    /// <summary>
    ///     Past the margin where the receiver ends far right, the floor rises only 0.15 a column of head: <c>.All(</c>
    ///     behind a 37-column name and a 13-column receiver stays and breaks its arrow.
    /// </summary>
    [Fact]
    public void AFloorPastTheMargin_ShortHead_KeepsTheCall() =>
        Agrees(
            "        if (b) {\n"
            + "            IEnumerable<int> nnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnn = rrrrrrrrrrrrr.All(x => aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa || bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb).TTTTTTTTTTTTTTTTTTTTTTTT();\n"
            + "        }\n",
            "        if (b) {\n"
            + "            IEnumerable<int> nnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnn = rrrrrrrrrrrrr.All(x =>\n"
            + "                    aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa || bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb\n"
            + "                )\n"
            + "                .TTTTTTTTTTTTTTTTTTTTTTTT();\n"
            + "        }\n"
        );
}
