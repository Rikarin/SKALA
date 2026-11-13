namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     ⚠ A held lambda call's limit solved again exactly, its line read no further than 113 plus its head, within the
///     margin and past it (#619 round 6, SK-DIV-0510); a line one column over by a limit of its own; a reach under twelve
///     never moving. Every expected string is the oracle's, taken from the #619 probes and grids. See
///     <c>Fitter.HeldLambdaSaturation</c>, <c>Fitter.HeldLambdaLimitWithin</c> and <c>Fitter.HeldLambdaOneOverMoves</c>.
/// </summary>
public sealed class HeldLambdaSaturationIssue619Tests {
    static string Wrap(string body) => "class C {\n    void M() {\n" + body + "    }\n}\n";

    static void Agrees(string source, string expected) {
        var once = Format.Text(Wrap(source)).ReplaceLineEndings("\n");
        Assert.Equal(Wrap(expected), once);
        Assert.Equal(once, Format.Text(once).ReplaceLineEndings("\n"));
    }

    /// <summary>
    ///     Group I's line one column over behind a 14-column reach: the oracle keeps the <c>=</c> and gives the <c>)</c>
    ///     a line, past the one-over limit.
    /// </summary>
    [Fact]
    public void OneOver_ShortReach_KeepsTheEquals() =>
        Agrees(
            "        var nnnnnn = r.Where(x => aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa && bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb).ToList();\n",
            "        var nnnnnn = r.Where(x => aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa && bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb\n"
            + "            )\n"
            + "            .ToList();\n"
        );

    /// <summary>
    ///     Below a broken <c>=</c> a six-column receiver keeps its call and breaks the arrow: a reach under twelve never
    ///     moves, within the margin too.
    /// </summary>
    [Fact]
    public void BrokenEquals_ShortReceiver_KeepsTheCall() =>
        Agrees(
            "        var nnnnnnnnnnnnnnnnnnnn = rrrrrr.Where(x => aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa && bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb).TTTTTTTTTTTTTTTTTTTT();\n",
            "        var nnnnnnnnnnnnnnnnnnnn =\n"
            + "            rrrrrr.Where(x =>\n"
            + "                    aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa && bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb\n"
            + "                )\n"
            + "                .TTTTTTTTTTTTTTTTTTTT();\n"
        );

    /// <summary>Behind <c>nnnnnn = r</c>, eleven columns, the call stays though its line is within the limit.</summary>
    [Fact]
    public void ShortReach_NeverMovesWithinTheMargin() =>
        Agrees(
            "        nnnnnn = r.Where(x => aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa && bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb).TTTTTTTTTTTTTTTTTTTT();\n",
            "        nnnnnn = r.Where(x =>\n"
            + "                aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa && bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb\n"
            + "            )\n"
            + "            .TTTTTTTTTTTTTTTTTTTT();\n"
        );

    /// <summary>A line of 120 under a four-column call head is read as 117, within the limit: the call moves.</summary>
    [Fact]
    public void Saturated_MovesTheCall() =>
        Agrees(
            "        this.nnnnnnnnnnnnnnnnnnnn = rrrrrrrrrrrrrr.Ab(x => aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa || bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb).TTTTTTTTTTTTTTTTTTTTTT();\n",
            "        this.nnnnnnnnnnnnnnnnnnnn = rrrrrrrrrrrrrr\n"
            + "            .Ab(x => aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa || bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb)\n"
            + "            .TTTTTTTTTTTTTTTTTTTTTT();\n"
        );

    /// <summary>
    ///     A line one column over with a rest after it, behind a reach of 13: the one-over limit moves the call, where
    ///     the lone <c>)</c> rule kept it.
    /// </summary>
    [Fact]
    public void OneOver_WithARest_MovesTheCall() =>
        Agrees(
            "        string nnn = rrr.Abcdefghijklm(x => aaaaaaaaaaaaaaaa && bbbbbbbbbbbbbbbb && cccccccccccccccc && dddddddddddddddd).ToArray();\n",
            "        string nnn = rrr\n"
            + "            .Abcdefghijklm(x => aaaaaaaaaaaaaaaa && bbbbbbbbbbbbbbbb && cccccccccccccccc && dddddddddddddddd)\n"
            + "            .ToArray();\n"
        );

    /// <summary>Past the margin group I reads the line at the saturation, 120 under <c>.Where(</c>: the <c>=</c> breaks.</summary>
    [Fact]
    public void PastTheMargin_ReadAtTheSaturation() =>
        Agrees(
            "        this.nnnnnnnnn = r.Where(x => x is AAAAAAAAAAAAAAAAAAAAAAA or BBBBBBBBBBBBBBBBBBBBBBB or CCCCCCCCCCCCCCCCCCCCCC or DDDDDDDDDDDDDDDDDDDDDD).TTTTTTTTTTTTTTTTTT();\n",
            "        this.nnnnnnnnn =\n"
            + "            r.Where(x =>\n"
            + "                    x is AAAAAAAAAAAAAAAAAAAAAAA\n"
            + "                        or BBBBBBBBBBBBBBBBBBBBBBB\n"
            + "                        or CCCCCCCCCCCCCCCCCCCCCC\n"
            + "                        or DDDDDDDDDDDDDDDDDDDDDD\n"
            + "                )\n"
            + "                .TTTTTTTTTTTTTTTTTT();\n"
        );

    /// <summary>The refit cap: a line of 117 inside the limit moves the call, where the earlier cap kept it.</summary>
    [Fact]
    public void RefitCap_MovesTheCall() =>
        Agrees(
            "        IEnumerable<int> nnnnnnnnnnnnnnnnnn = rrrrrrrr.Select(x => aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa || bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb).TTTTTTTTTTTTTTTTTT();\n",
            "        IEnumerable<int> nnnnnnnnnnnnnnnnnn = rrrrrrrr\n"
            + "            .Select(x => aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa || bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb)\n"
            + "            .TTTTTTTTTTTTTTTTTT();\n"
        );
}
