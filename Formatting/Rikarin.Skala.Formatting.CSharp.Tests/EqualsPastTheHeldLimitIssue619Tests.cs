namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     ⚠ Group I's <c>=</c> past the held limit (#619 round 6, SK-DIV-0510). Where the receiver is short against the name
///     and the value does not fit below, the oracle breaks the <c>=</c> — <c>=</c> / <c>r.Where(…)</c> / <c>.T…();</c> —
///     not only while the held call would move, but up to a plateau of its own: the indent, head, receiver, call head and
///     rest set it, and the name lowers it only past 22. Past 113 plus the call's head the line is read as that, so a
///     plateau reaching it breaks the <c>=</c> to the margin and past it. Every expected string is the oracle's, taken
///     from the band grids. See <c>Fitter.HeldChainBand</c>.
/// </summary>
public sealed class EqualsPastTheHeldLimitIssue619Tests {
    static string Wrap(string body) => "class C {\n    void M() {\n" + body + "    }\n}\n";

    static void Agrees(string source, string expected) {
        var once = Format.Text(Wrap(source)).ReplaceLineEndings("\n");
        Assert.Equal(Wrap(expected), once);
        Assert.Equal(once, Format.Text(once).ReplaceLineEndings("\n"));
    }

    /// <summary>A line below of 112, a column past the held limit's 111.1: the plateau's 112.7 breaks the <c>=</c>.</summary>
    [Fact]
    public void PastTheHeldLimit_BreaksTheEquals() =>
        Agrees(
            "        nnnnnnnnnnnnnnnn = r.Where(x => aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa && bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb).ToList();\n",
            "        nnnnnnnnnnnnnnnn =\n"
            + "            r.Where(x => aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa && bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb)\n"
            + "                .ToList();\n"
        );

    /// <summary>The same a column further, past the plateau: the <c>=</c> stays and the arrow breaks beside it.</summary>
    [Fact]
    public void PastThePlateau_KeepsTheEquals() =>
        Agrees(
            "        nnnnnnnnnnnnnnnn = r.Where(x => aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa && bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb).ToList();\n",
            "        nnnnnnnnnnnnnnnn = r.Where(x =>\n"
            + "                aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa && bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb\n"
            + "            )\n"
            + "            .ToList();\n"
        );

    /// <summary>A name of 28 lowers the plateau to 117.3; a line below of 117 still breaks the <c>=</c>.</summary>
    [Fact]
    public void LongName_BreaksTheEquals() =>
        Agrees(
            "        IEnumerable<int> nnnnnnnnnnnnnnnnnnnnnnnnnnnn = r.Where(x => aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa && bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb).TTTTTTTTTTTTTTTTTTTT();\n",
            "        IEnumerable<int> nnnnnnnnnnnnnnnnnnnnnnnnnnnn =\n"
            + "            r.Where(x => aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa && bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb)\n"
            + "                .TTTTTTTTTTTTTTTTTTTT();\n"
        );

    /// <summary>And 118 keeps it: where the plateau alone (119.5) would still break.</summary>
    [Fact]
    public void LongName_PastItsCut_KeepsTheEquals() =>
        Agrees(
            "        IEnumerable<int> nnnnnnnnnnnnnnnnnnnnnnnnnnnn = r.Where(x => aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa && bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb).TTTTTTTTTTTTTTTTTTTT();\n",
            "        IEnumerable<int> nnnnnnnnnnnnnnnnnnnnnnnnnnnn = r.Where(x =>\n"
            + "                aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa && bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb\n"
            + "            )\n"
            + "            .TTTTTTTTTTTTTTTTTTTT();\n"
        );

    /// <summary>
    ///     A plateau of 119.9 under <c>.Any(</c>: past 118 the line is read as 118, so a line of 121 breaks the <c>=</c>
    ///     and chops the operands below.
    /// </summary>
    [Fact]
    public void PastTheBodyLine_BreaksToTheMargin() =>
        Agrees(
            "        var nnnnnnnnnnnnnn = rr.Any(x => aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa && bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb).TTTTTTTTTTTTTTTTTTTT();\n",
            "        var nnnnnnnnnnnnnn =\n"
            + "            rr.Any(x => aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa\n"
            + "                    && bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb\n"
            + "                )\n"
            + "                .TTTTTTTTTTTTTTTTTTTT();\n"
        );

    /// <summary>
    ///     Where the value fits below, the same plateau (108.5) breaks the <c>=</c> at a line of 107, where round 4's edge
    ///     on the value's end (133.7 against 132.7) kept it and moved the call.
    /// </summary>
    [Fact]
    public void FitsBelow_LongName_BreaksTheEquals() =>
        Agrees(
            "        var nnnnnnnnnnnnnnnnnnnnnnnnnnnnn = rr.Any(x => aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa || bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb).ToArray();\n",
            "        var nnnnnnnnnnnnnnnnnnnnnnnnnnnnn =\n"
            + "            rr.Any(x => aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa || bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb).ToArray();\n"
        );

    /// <summary>The same reading past the margin: a line of 124 below a 21-column rest of two calls.</summary>
    [Fact]
    public void PastTheMargin_BreaksTheEquals() =>
        Agrees(
            "        var nnnnnnnnnnnnnnnnnnnn = r.Any(x => aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa && bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb && ccccccccccccccccccccccccccccccc).TTT().UUUUUUUUUUU();\n",
            "        var nnnnnnnnnnnnnnnnnnnn =\n"
            + "            r.Any(x => aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa\n"
            + "                    && bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb\n"
            + "                    && ccccccccccccccccccccccccccccccc\n"
            + "                )\n"
            + "                .TTT()\n"
            + "                .UUUUUUUUUUU();\n"
        );
}
