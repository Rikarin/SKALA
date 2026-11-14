namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     ⚠ #619 round 8 (SK-DIV-0510): an <c>=</c> past column 83, the held limit's floor past a receiver's column of
///     84, the <c>=</c> outside group I's region far enough right, the plateau under the floor, and round 4's
///     never-breaks region under short rests. Every expected string is the oracle's, taken from the round's grids.
///     See <c>Fitter.HeldChainEqualsPast83</c>, <c>Fitter.HeldLambdaLimitWithin</c>,
///     <c>Fitter.HeldChainFarBreaksTheEquals</c>, <c>Fitter.HeldLambdaFloorOverCap</c> and
///     <c>Fitter.HeldChainValueBreaksTheEquals</c>.
/// </summary>
public sealed class HeldChainRound8Issue619Tests {
    static string Wrap(string body) => "class C {\n    void M() {\n" + body + "    }\n}\n";

    static void Agrees(string source, string expected) {
        var once = Format.Text(Wrap(source)).ReplaceLineEndings("\n");
        Assert.Equal(Wrap(expected), once);
        Assert.Equal(once, Format.Text(once).ReplaceLineEndings("\n"));
    }

    /// <summary>
    ///     An <c>=</c> at column 85, a name of 48 and a receiver of 8: the value ends at 107 below, past round 7's limit
    ///     (106) and within it raised 0.5088 a column past 83, and the <c>=</c> breaks.
    /// </summary>
    [Fact]
    public void PastColumn83_ThirdRegion_BreaksTheEquals() =>
        Agrees(
            "        if (b) {\n"
            + "            if (b) {\n"
            + "                if (b) {\n"
            + "                    if (b) {\n"
            + "                        if (b) {\n"
            + "                            string nnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnn = rrrrrrrr.Select(x => aaaaaaaaaaaaaaaaaaaa || bbbbbbbbbbbbbbbbbbb).ToList();\n"
            + "                        }\n"
            + "                    }\n"
            + "                }\n"
            + "            }\n"
            + "        }\n",
            "        if (b) {\n"
            + "            if (b) {\n"
            + "                if (b) {\n"
            + "                    if (b) {\n"
            + "                        if (b) {\n"
            + "                            string nnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnn =\n"
            + "                                rrrrrrrr.Select(x => aaaaaaaaaaaaaaaaaaaa || bbbbbbbbbbbbbbbbbbb).ToList();\n"
            + "                        }\n"
            + "                    }\n"
            + "                }\n"
            + "            }\n"
            + "        }\n"
        );

    /// <summary>
    ///     An <c>=</c> at column 105: the raised limit passes 113, so the <c>=</c> breaks before a value ending at 114,
    ///     past round 4's cap of 112.
    /// </summary>
    [Fact]
    public void PastColumn83_LimitPast113_BreaksForAnyValueBelow() =>
        Agrees(
            "        if (b) {\n"
            + "            if (b) {\n"
            + "                if (b) {\n"
            + "                    if (b) {\n"
            + "                        if (b) {\n"
            + "                            if (b) {\n"
            + "                                IEnumerable<int> nnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnn = rrrrrrrrrrrr.Abcdefghijk(x => aaaaaaaaaaaaaaaa && bbbbbbbbbbbbbbbb).ToArray();\n"
            + "                            }\n"
            + "                        }\n"
            + "                    }\n"
            + "                }\n"
            + "            }\n"
            + "        }\n",
            "        if (b) {\n"
            + "            if (b) {\n"
            + "                if (b) {\n"
            + "                    if (b) {\n"
            + "                        if (b) {\n"
            + "                            if (b) {\n"
            + "                                IEnumerable<int> nnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnn =\n"
            + "                                    rrrrrrrrrrrr.Abcdefghijk(x => aaaaaaaaaaaaaaaa && bbbbbbbbbbbbbbbb).ToArray();\n"
            + "                            }\n"
            + "                        }\n"
            + "                    }\n"
            + "                }\n"
            + "            }\n"
            + "        }\n"
        );

    /// <summary>
    ///     A held call that cannot move, under the held limit's floor (its receiver at column 78): past group I's plateau
    ///     raised by the floor's lead over the cap, the arrow breaks, not the <c>=</c>.
    /// </summary>
    [Fact]
    public void StaysUnderTheFloor_PastThePlateau_ArrowBreaks() =>
        Agrees(
            "        if (b) {\n"
            + "            if (b) {\n"
            + "                if (b) {\n"
            + "                    if (b) {\n"
            + "                        if (b) {\n"
            + "                            if (b) {\n"
            + "                                nnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnn = rrr.Where(x => aaaaaaaaaaaaaaa || bbbbbbbbbbbbbb || cccccccccccccc).T();\n"
            + "                            }\n"
            + "                        }\n"
            + "                    }\n"
            + "                }\n"
            + "            }\n"
            + "        }\n",
            "        if (b) {\n"
            + "            if (b) {\n"
            + "                if (b) {\n"
            + "                    if (b) {\n"
            + "                        if (b) {\n"
            + "                            if (b) {\n"
            + "                                nnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnn = rrr.Where(x =>\n"
            + "                                        aaaaaaaaaaaaaaa || bbbbbbbbbbbbbb || cccccccccccccc\n"
            + "                                    )\n"
            + "                                    .T();\n"
            + "                            }\n"
            + "                        }\n"
            + "                    }\n"
            + "                }\n"
            + "            }\n"
            + "        }\n"
        );

    /// <summary>
    ///     Past column 83 the raised limit passes 113, but the held call would not move behind the kept <c>=</c> and its
    ///     line is past the plateau: the arrow breaks.
    /// </summary>
    [Fact]
    public void PastColumn83_CallWouldNotMove_ArrowBreaks() =>
        Agrees(
            "        if (b) {\n"
            + "            if (b) {\n"
            + "                if (b) {\n"
            + "                    if (b) {\n"
            + "                        if (b) {\n"
            + "                            if (b) {\n"
            + "                                List<int> nnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnn = r.Any(x => aaaaaaaaaaaaaaaaa || bbbbbbbbbbbbbbbbb || ccccccccccccccccc).ToList();\n"
            + "                            }\n"
            + "                        }\n"
            + "                    }\n"
            + "                }\n"
            + "            }\n"
            + "        }\n",
            "        if (b) {\n"
            + "            if (b) {\n"
            + "                if (b) {\n"
            + "                    if (b) {\n"
            + "                        if (b) {\n"
            + "                            if (b) {\n"
            + "                                List<int> nnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnn = r.Any(x =>\n"
            + "                                        aaaaaaaaaaaaaaaaa || bbbbbbbbbbbbbbbbb || ccccccccccccccccc\n"
            + "                                    )\n"
            + "                                    .ToList();\n"
            + "                            }\n"
            + "                        }\n"
            + "                    }\n"
            + "                }\n"
            + "            }\n"
            + "        }\n"
        );

    /// <summary>
    ///     A receiver at column 89 behind a kept <c>=</c>: the floor turned over past 84, so a moved line of 108 is past
    ///     the limit and the arrow breaks, where round 6's floor moved the call.
    /// </summary>
    [Fact]
    public void HeldPastColumn84_ArrowBreaks() =>
        Agrees(
            "        if (b) {\n"
            + "            if (b) {\n"
            + "                if (b) {\n"
            + "                    if (b) {\n"
            + "                        if (b) {\n"
            + "                            if (b) {\n"
            + "                                var nnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnn = r.All(x => aaaaaaaaaaaaaaaaaa && bbbbbbbbbbbbbbbbbb && ccccccccccccccccc).TT().UUUU();\n"
            + "                            }\n"
            + "                        }\n"
            + "                    }\n"
            + "                }\n"
            + "            }\n"
            + "        }\n",
            "        if (b) {\n"
            + "            if (b) {\n"
            + "                if (b) {\n"
            + "                    if (b) {\n"
            + "                        if (b) {\n"
            + "                            if (b) {\n"
            + "                                var nnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnn = r.All(x =>\n"
            + "                                        aaaaaaaaaaaaaaaaaa && bbbbbbbbbbbbbbbbbb && ccccccccccccccccc\n"
            + "                                    )\n"
            + "                                    .TT()\n"
            + "                                    .UUUU();\n"
            + "                            }\n"
            + "                        }\n"
            + "                    }\n"
            + "                }\n"
            + "            }\n"
            + "        }\n"
        );

    /// <summary>
    ///     The same past column 84 for a moved line of 121: past the turned-over limit the call stays and the arrow
    ///     breaks.
    /// </summary>
    [Fact]
    public void HeldPastColumn84_OneOverBelow_ArrowBreaks() =>
        Agrees(
            "        if (b) {\n"
            + "            if (b) {\n"
            + "                if (b) {\n"
            + "                    if (b) {\n"
            + "                        if (b) {\n"
            + "                            if (b) {\n"
            + "                                string nnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnn = rrrrrr.SkipWhile(x => aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa || bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb).TTTTTT().UUU();\n"
            + "                            }\n"
            + "                        }\n"
            + "                    }\n"
            + "                }\n"
            + "            }\n"
            + "        }\n",
            "        if (b) {\n"
            + "            if (b) {\n"
            + "                if (b) {\n"
            + "                    if (b) {\n"
            + "                        if (b) {\n"
            + "                            if (b) {\n"
            + "                                string nnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnn = rrrrrr.SkipWhile(x =>\n"
            + "                                        aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa || bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb\n"
            + "                                    )\n"
            + "                                    .TTTTTT()\n"
            + "                                    .UUU();\n"
            + "                            }\n"
            + "                        }\n"
            + "                    }\n"
            + "                }\n"
            + "            }\n"
            + "        }\n"
        );

    /// <summary>
    ///     An <c>=</c> at column 97 before a value ending at the margin: the third region's limit raised past 83 passes
    ///     113, the call would move, and the <c>=</c> breaks.
    /// </summary>
    [Fact]
    public void PastColumn83_ValueEndsAtTheMargin_BreaksTheEquals() =>
        Agrees(
            "        if (b) {\n"
            + "            if (b) {\n"
            + "                if (b) {\n"
            + "                    if (b) {\n"
            + "                        IEnumerable<int> nnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnn = rr.TakeWhile(x => aaaaaaaaaaaaaaaaaaaaaaaaaaaaa && bbbbbbbbbbbbbbbbbbbbbbbbbbbbb).ToArray();\n"
            + "                    }\n"
            + "                }\n"
            + "            }\n"
            + "        }\n",
            "        if (b) {\n"
            + "            if (b) {\n"
            + "                if (b) {\n"
            + "                    if (b) {\n"
            + "                        IEnumerable<int> nnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnn =\n"
            + "                            rr.TakeWhile(x => aaaaaaaaaaaaaaaaaaaaaaaaaaaaa && bbbbbbbbbbbbbbbbbbbbbbbbbbbbb).ToArray();\n"
            + "                    }\n"
            + "                }\n"
            + "            }\n"
            + "        }\n"
        );

    /// <summary>The same at column 104, where the arrow would also overflow beside the kept <c>=</c>.</summary>
    [Fact]
    public void PastColumn83_ArrowPastTheMargin_BreaksTheEquals() =>
        Agrees(
            "        if (b) {\n"
            + "            if (b) {\n"
            + "                if (b) {\n"
            + "                    if (b) {\n"
            + "                        IEnumerable<int> nnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnn = rr.Abcdefgh(x => aaaaaaaaaaaaaaaaaaa || bbbbbbbbbbbbbbbbbbb || cccccccccccccccccc).ToList();\n"
            + "                    }\n"
            + "                }\n"
            + "            }\n"
            + "        }\n",
            "        if (b) {\n"
            + "            if (b) {\n"
            + "                if (b) {\n"
            + "                    if (b) {\n"
            + "                        IEnumerable<int> nnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnn =\n"
            + "                            rr.Abcdefgh(x => aaaaaaaaaaaaaaaaaaa || bbbbbbbbbbbbbbbbbbb || cccccccccccccccccc).ToList();\n"
            + "                    }\n"
            + "                }\n"
            + "            }\n"
            + "        }\n"
        );

    /// <summary>
    ///     An arrow past the margin beside the kept <c>=</c> moves the call even where its line below is one over, the
    ///     <c>)</c> on a line of its own: past column 84 the limit no longer reaches that line.
    /// </summary>
    [Fact]
    public void ArrowPastTheMargin_OneOverBelow_MovesTheCall() =>
        Agrees(
            "        if (b) {\n"
            + "            if (b) {\n"
            + "                if (b) {\n"
            + "                    if (b) {\n"
            + "                        List<int> nnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnn = rrrrrrrr.SkipWhile(x => aaaaaaaaaaaaaaaaaaaaaaa || bbbbbbbbbbbbbbbbbbbbbbb || cccccccccccccccccccccc).TTTTTTTTTTT();\n"
            + "                    }\n"
            + "                }\n"
            + "            }\n"
            + "        }\n",
            "        if (b) {\n"
            + "            if (b) {\n"
            + "                if (b) {\n"
            + "                    if (b) {\n"
            + "                        List<int> nnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnn = rrrrrrrr\n"
            + "                            .SkipWhile(x => aaaaaaaaaaaaaaaaaaaaaaa || bbbbbbbbbbbbbbbbbbbbbbb || cccccccccccccccccccccc\n"
            + "                            )\n"
            + "                            .TTTTTTTTTTT();\n"
            + "                    }\n"
            + "                }\n"
            + "            }\n"
            + "        }\n"
        );

    /// <summary>
    ///     Round 4's never-breaks region under a rest of 9: the value ends at 110, within the third region's limit for a
    ///     name of 21 and a receiver of 14, and the <c>=</c> breaks.
    /// </summary>
    [Fact]
    public void NeverRegion_ShortRest_BreaksTheEquals() =>
        Agrees(
            "        if (b) {\n"
            + "            if (b) {\n"
            + "                nnnnnnnnnnnnnnnnnnnnn = rrrrrrrrrrrrrr.Where(x => aaaaaaaaaaaaaaaaaaaaaaaaa && bbbbbbbbbbbbbbbbbbbbbbbbb).TTTTT();\n"
            + "            }\n"
            + "        }\n",
            "        if (b) {\n"
            + "            if (b) {\n"
            + "                nnnnnnnnnnnnnnnnnnnnn =\n"
            + "                    rrrrrrrrrrrrrr.Where(x => aaaaaaaaaaaaaaaaaaaaaaaaa && bbbbbbbbbbbbbbbbbbbbbbbbb).TTTTT();\n"
            + "            }\n"
            + "        }\n"
        );

    /// <summary>
    ///     Outside group I's region, an <c>=</c> at column 97 for a name of 54 and a receiver of 2 is past the far cut:
    ///     the value ends at 121 below, past the margin, and within the plateau the <c>=</c> breaks and the rest takes a line
    ///     of its own.
    /// </summary>
    [Fact]
    public void FarEquals_ValuePastTheMargin_BreaksTheEquals() =>
        Agrees(
            "        if (b) {\n"
            + "            if (b) {\n"
            + "                if (b) {\n"
            + "                    if (b) {\n"
            + "                        IEnumerable<int> nnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnn = rr.TakeWhile(x => aaaaaaaaaaaaaaaaaaaaaaaaaaaaaa && bbbbbbbbbbbbbbbbbbbbbbbbbbbbb).ToArray();\n"
            + "                    }\n"
            + "                }\n"
            + "            }\n"
            + "        }\n",
            "        if (b) {\n"
            + "            if (b) {\n"
            + "                if (b) {\n"
            + "                    if (b) {\n"
            + "                        IEnumerable<int> nnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnn =\n"
            + "                            rr.TakeWhile(x => aaaaaaaaaaaaaaaaaaaaaaaaaaaaaa && bbbbbbbbbbbbbbbbbbbbbbbbbbbbb)\n"
            + "                                .ToArray();\n"
            + "                    }\n"
            + "                }\n"
            + "            }\n"
            + "        }\n"
        );

    /// <summary>
    ///     Past the far cut, where the arrow would overflow beside the kept <c>=</c>, the <c>=</c> breaks before a value
    ///     that does not fit below.
    /// </summary>
    [Fact]
    public void FarEquals_ArrowPastTheMargin_BreaksTheEquals() =>
        Agrees(
            "        if (b) {\n"
            + "            if (b) {\n"
            + "                if (b) {\n"
            + "                    if (b) {\n"
            + "                        IEnumerable<int> nnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnn = rr.Abcdefgh(x => aaaaaaaaaaaaaaaaaaa || bbbbbbbbbbbbbbbbbbb || ccccccccccccccccccc).ToList();\n"
            + "                    }\n"
            + "                }\n"
            + "            }\n"
            + "        }\n",
            "        if (b) {\n"
            + "            if (b) {\n"
            + "                if (b) {\n"
            + "                    if (b) {\n"
            + "                        IEnumerable<int> nnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnn =\n"
            + "                            rr.Abcdefgh(x => aaaaaaaaaaaaaaaaaaa || bbbbbbbbbbbbbbbbbbb || ccccccccccccccccccc)\n"
            + "                                .ToList();\n"
            + "                    }\n"
            + "                }\n"
            + "            }\n"
            + "        }\n"
        );

    /// <summary>
    ///     A held call that cannot move, under the floor (receiver at column 82): its line of 99 is past group I's
    ///     plateau but within it raised by the floor's lead over the cap, and the <c>=</c> breaks.
    /// </summary>
    [Fact]
    public void StaysUnderTheFloor_WithinTheRaisedPlateau_BreaksTheEquals() =>
        Agrees(
            "        if (b) {\n"
            + "            if (b) {\n"
            + "                if (b) {\n"
            + "                    if (b) {\n"
            + "                        if (b) {\n"
            + "                            if (b) {\n"
            + "                                nnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnn = rrr.Abcd(x => aaaaaaaaaaaaaaaaaaaaaaaa || bbbbbbbbbbbbbbbbbbbbbbb).T();\n"
            + "                            }\n"
            + "                        }\n"
            + "                    }\n"
            + "                }\n"
            + "            }\n"
            + "        }\n",
            "        if (b) {\n"
            + "            if (b) {\n"
            + "                if (b) {\n"
            + "                    if (b) {\n"
            + "                        if (b) {\n"
            + "                            if (b) {\n"
            + "                                nnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnn =\n"
            + "                                    rrr.Abcd(x => aaaaaaaaaaaaaaaaaaaaaaaa || bbbbbbbbbbbbbbbbbbbbbbb).T();\n"
            + "                            }\n"
            + "                        }\n"
            + "                    }\n"
            + "                }\n"
            + "            }\n"
            + "        }\n"
        );

    /// <summary>
    ///     Round 7's held line 0.001 inside round 6's floor: solved again with every series of rounds 6 to 8, the floor
    ///     sits 0.017 lower and the arrow breaks, as the oracle does.
    /// </summary>
    [Fact]
    public void HeldFloorSolvedAgain_ArrowBreaks() =>
        Agrees(
            "        if (b) {\n"
            + "            if (b) {\n"
            + "                IEnumerable<int> nnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnn = rrrr.TakeWhile(x => aaaaaaaaaaaaaaaaaaaaa && bbbbbbbbbbbbbbbbbbbbb && cccccccccccccccccccc).TTTTT();\n"
            + "            }\n"
            + "        }\n",
            "        if (b) {\n"
            + "            if (b) {\n"
            + "                IEnumerable<int> nnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnn = rrrr.TakeWhile(x =>\n"
            + "                        aaaaaaaaaaaaaaaaaaaaa && bbbbbbbbbbbbbbbbbbbbb && cccccccccccccccccccc\n"
            + "                    )\n"
            + "                    .TTTTT();\n"
            + "            }\n"
            + "        }\n"
        );

    /// <summary>
    ///     An arrow past the margin beside the kept <c>=</c> (receiver at column 107) moves the call even where its line
    ///     below ends at 127, past the margin: past column 84 the limit no longer reaches it.
    /// </summary>
    [Fact]
    public void ArrowPastTheMargin_LinePastTheMargin_MovesTheCall() =>
        Agrees(
            "        if (b) {\n"
            + "            if (b) {\n"
            + "                if (b) {\n"
            + "                    if (b) {\n"
            + "                        if (b) {\n"
            + "                            if (b) {\n"
            + "                                List<int> nnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnn = rrrrrrrrrrrrr.TakeWhile(x => aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa && bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb).TTTTTTTTTTTTTT();\n"
            + "                            }\n"
            + "                        }\n"
            + "                    }\n"
            + "                }\n"
            + "            }\n"
            + "        }\n",
            "        if (b) {\n"
            + "            if (b) {\n"
            + "                if (b) {\n"
            + "                    if (b) {\n"
            + "                        if (b) {\n"
            + "                            if (b) {\n"
            + "                                List<int> nnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnn = rrrrrrrrrrrrr\n"
            + "                                    .TakeWhile(x =>\n"
            + "                                        aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa && bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb\n"
            + "                                    )\n"
            + "                                    .TTTTTTTTTTTTTT();\n"
            + "                            }\n"
            + "                        }\n"
            + "                    }\n"
            + "                }\n"
            + "            }\n"
            + "        }\n"
        );
}
