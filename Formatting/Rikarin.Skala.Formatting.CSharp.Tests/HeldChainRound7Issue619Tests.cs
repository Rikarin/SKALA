namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     ⚠ #619 round 7 (SK-DIV-0510): the <c>=</c> where the line through the held call fits, the held call that never
///     moves while its limit leaves the value ending before column 113 below, the third region's whole-column edge, and
///     both one-column-over lines. Every expected string is the oracle's, taken from the #619 probes and grids. See
///     <c>Fitter.HeldChainThroughBreaksTheEquals</c>, <c>Fitter.HeldLambdaStaysBesideTheEquals</c>,
///     <c>Fitter.HeldChainValueBreaksTheEquals</c>, <c>Fitter.HeldChainOneOverBreaks</c> and
///     <c>Fitter.HeldLambdaOneOverMoves</c>.
/// </summary>
public sealed class HeldChainRound7Issue619Tests {
    static string Wrap(string body) => "class C {\n    void M() {\n" + body + "    }\n}\n";

    static void Agrees(string source, string expected) {
        var once = Format.Text(Wrap(source)).ReplaceLineEndings("\n");
        Assert.Equal(Wrap(expected), once);
        Assert.Equal(once, Format.Text(once).ReplaceLineEndings("\n"));
    }

    /// <summary>
    ///     The line through the held call fits; the value ends at 105 below, within the limit for a name of 22: the
    ///     <c>=</c> breaks, where the ordering rule kept it.
    /// </summary>
    [Fact]
    public void ThroughFits_ValueFitsBelow_BreaksTheEquals() =>
        Agrees(
            "        if (b) {\n"
            + "            string nnnnnnnnnnnnnnnnnnnnnn = rrrr.Count(x => aaaaaaaaaaaaaaaaaaaaaa || bbbbbbbbbbbbbbbbbbbbb).TTTTTTTTTTTTTTTTTTTTT();\n"
            + "        }\n",
            "        if (b) {\n"
            + "            string nnnnnnnnnnnnnnnnnnnnnn =\n"
            + "                rrrr.Count(x => aaaaaaaaaaaaaaaaaaaaaa || bbbbbbbbbbbbbbbbbbbbb).TTTTTTTTTTTTTTTTTTTTT();\n"
            + "        }\n"
        );

    /// <summary>
    ///     The same past the limit for a 37-column target: the <c>=</c> stays and the rest breaks below, where the
    ///     ordering rule broke the <c>=</c>.
    /// </summary>
    [Fact]
    public void ThroughFits_LongName_KeepsTheEquals() =>
        Agrees(
            "        if (b) {\n"
            + "            this.nnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnn = rrrrrr.TakeWhile(x => aaaaaaaaa || bbbbbbbbb || ccccccccc).TTTTTTTTTTTTTTTTTTTTTTTTTT();\n"
            + "        }\n",
            "        if (b) {\n"
            + "            this.nnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnn = rrrrrr.TakeWhile(x => aaaaaaaaa || bbbbbbbbb || ccccccccc)\n"
            + "                .TTTTTTTTTTTTTTTTTTTTTTTTTT();\n"
            + "        }\n"
        );

    /// <summary>A name of four never breaks the <c>=</c> there: the fragment is not short against it.</summary>
    [Fact]
    public void ThroughFits_ShortName_KeepsTheEquals() =>
        Agrees(
            "        IEnumerable<int> nnnn = r.Where(x => aaaaaaaaaaaaaaa || bbbbbbbbbbbbbbb || ccccccccccccccc).TTTTTTTTTTTTTTTTTTTT();\n",
            "        IEnumerable<int> nnnn = r.Where(x => aaaaaaaaaaaaaaa || bbbbbbbbbbbbbbb || ccccccccccccccc)\n"
            + "            .TTTTTTTTTTTTTTTTTTTT();\n"
        );

    /// <summary>
    ///     A value whose line at the limit would end before column 113: the call stays though its line is within the
    ///     limit, and the arrow breaks.
    /// </summary>
    [Fact]
    public void NoMove_ArrowBreaksWithinTheLimit() =>
        Agrees(
            "        string nnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnn = rrrr.Abcd(x => aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa && bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb).T();\n",
            "        string nnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnn = rrrr.Abcd(x =>\n"
            + "                aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa && bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb\n"
            + "            )\n"
            + "            .T();\n"
        );

    /// <summary>Under the cap, past group I's plateau and within the third region's bound: the arrow breaks, not the <c>=</c>.</summary>
    [Fact]
    public void NoMove_PastThePlateau_ArrowBreaks() =>
        Agrees(
            "        IEnumerable<int> nnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnn = rrr.First(x => aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa || bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb).T();\n",
            "        IEnumerable<int> nnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnn = rrr.First(x =>\n"
            + "                aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa || bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb\n"
            + "            )\n"
            + "            .T();\n"
        );

    /// <summary>The same a column further left, within the plateau: the <c>=</c> breaks.</summary>
    [Fact]
    public void NoMove_WithinThePlateau_BreaksTheEquals() =>
        Agrees(
            "        IEnumerable<int> nnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnn = rrr.First(x => aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa || bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb).T();\n",
            "        IEnumerable<int> nnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnn =\n"
            + "            rrr.First(x => aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa || bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb).T();\n"
        );

    /// <summary>
    ///     The fresh probe's rest of eight a column inside the limit (seed 20261117): the call stays and the arrow
    ///     breaks.
    /// </summary>
    [Fact]
    public void NoMove_RestOfEight_ArrowBreaks() =>
        Agrees(
            "        if (b) {\n"
            + "            if (b) {\n"
            + "                if (b) {\n"
            + "                    var nnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnn = r.Sum(x => aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa || bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb).TTTT();\n"
            + "                }\n"
            + "            }\n"
            + "        }\n",
            "        if (b) {\n"
            + "            if (b) {\n"
            + "                if (b) {\n"
            + "                    var nnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnn = r.Sum(x =>\n"
            + "                            aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa || bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb\n"
            + "                        )\n"
            + "                        .TTTT();\n"
            + "                }\n"
            + "            }\n"
            + "        }\n"
        );

    /// <summary>
    ///     <c>IEnumerable&lt;int&gt; n12 = r9</c>: round 4's limit read 111.002; the oracle keeps the <c>=</c> at a value
    ///     ending at 111.
    /// </summary>
    [Fact]
    public void ThirdRegion_WholeColumnEdge_MovesTheCall() =>
        Agrees(
            "        IEnumerable<int> nnnnnnnnnnnn = rrrrrrrrr.All(x => aaaaaaaaaaaaaaaaaaaa && bbbbbbbbbbbbbbbbbbbb && cccccccccccccccccccc).ToArray();\n",
            "        IEnumerable<int> nnnnnnnnnnnn = rrrrrrrrr\n"
            + "            .All(x => aaaaaaaaaaaaaaaaaaaa && bbbbbbbbbbbbbbbbbbbb && cccccccccccccccccccc)\n"
            + "            .ToArray();\n"
        );

    /// <summary>One column over, 0.012 past round 6's line: the oracle keeps the call and gives the <c>)</c> a line.</summary>
    [Fact]
    public void OneOver_RefitLine_KeepsTheCall() =>
        Agrees(
            "        this.nnnn = rrrr.Where(x => aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa && bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb).ToArray();\n",
            "        this.nnnn = rrrr.Where(x => aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa && bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb\n"
            + "            )\n"
            + "            .ToArray();\n"
        );

    /// <summary>One column over behind a reach of 18, past the line: the call stays.</summary>
    [Fact]
    public void OneOver_FarReach_KeepsTheCall() =>
        Agrees(
            "        string nnnnnn = rr.Ab(x => aaaaaaaaaaaaaaaaaaa || bbbbbbbbbbbbbbbbbb || cccccccccccccccccc || dddddddddddddddddd).TTTTT();\n",
            "        string nnnnnn = rr.Ab(x => aaaaaaaaaaaaaaaaaaa || bbbbbbbbbbbbbbbbbb || cccccccccccccccccc || dddddddddddddddddd\n"
            + "            )\n"
            + "            .TTTTT();\n"
        );

    /// <summary>Group I, one column over, within its own line: the <c>=</c> breaks.</summary>
    [Fact]
    public void OneOver_GroupI_BreaksTheEquals() =>
        Agrees(
            "        nnnnnnnnnnn = rrr.TakeWhile(x => aaaaaaaaaaaaaaaaa && bbbbbbbbbbbbbbbbb && ccccccccccccccccc && dddddddddddddddd).ToList();\n",
            "        nnnnnnnnnnn =\n"
            + "            rrr.TakeWhile(x => aaaaaaaaaaaaaaaaa && bbbbbbbbbbbbbbbbb && ccccccccccccccccc && dddddddddddddddd)\n"
            + "                .ToList();\n"
        );

    /// <summary>Group I, one column over, past its own line: the <c>=</c> stays and the <c>)</c> takes a line.</summary>
    [Fact]
    public void OneOver_GroupI_KeepsTheEquals() =>
        Agrees(
            "        this.nnnnnnn = r.All(x => aaaaaaaaaaaaaaaaaaaaaaaaaa || bbbbbbbbbbbbbbbbbbbbbbbbbb || cccccccccccccccccccccccccc).ToList();\n",
            "        this.nnnnnnn = r.All(x => aaaaaaaaaaaaaaaaaaaaaaaaaa || bbbbbbbbbbbbbbbbbbbbbbbbbb || cccccccccccccccccccccccccc\n"
            + "            )\n"
            + "            .ToList();\n"
        );
}
