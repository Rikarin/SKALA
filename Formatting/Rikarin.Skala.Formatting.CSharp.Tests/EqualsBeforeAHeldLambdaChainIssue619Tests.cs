namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     ⚠ An <c>=</c> before a held lambda call's chain (#619, SK-DIV-0510): past the margin the oracle breaks the
///     <c>=</c> and keeps the whole value below by the plain member value's rule, with the receiver for the fragment
///     the held call would leave beside the <c>=</c>. Every expected string is <c>jb cleanupcode</c>'s.
/// </summary>
public sealed class EqualsBeforeAHeldLambdaChainIssue619Tests {
    static string Wrap(string statement) => "class C {\n    void M() {\n" + statement + "    }\n}\n";

    static void Agrees(string statement, string expected) {
        var once = Format.Text(Wrap(statement)).ReplaceLineEndings("\n");
        Assert.Equal(Wrap(expected), once);
        Assert.Equal(once, Format.Text(once).ReplaceLineEndings("\n"));
    }

    /// <summary>A 27-column name, a 12-column receiver, the value ending at 109 below: the <c>=</c> breaks.</summary>
    [Fact]
    public void ValueWithinTheLimit_BreaksTheEquals() =>
        Agrees(
            "        var nnnnnnnnnnnnnnnnnnnnnnnnnnn = rrrrrrrrrrrr.Where(x => aaaaaaaaaaaaaaaaaa && bbbbbbbbbbbbbbbbbb && cccccccccccccccccc).ToList();\n",
            "        var nnnnnnnnnnnnnnnnnnnnnnnnnnn =\n"
            + "            rrrrrrrrrrrr.Where(x => aaaaaaaaaaaaaaaaaa && bbbbbbbbbbbbbbbbbb && cccccccccccccccccc).ToList();\n"
        );

    /// <summary>One column wider: the receiver stays and the held call moves down.</summary>
    [Fact]
    public void ValuePastTheLimit_MovesTheCallDown() =>
        Agrees(
            "        var nnnnnnnnnnnnnnnnnnnnnnnnnnn = rrrrrrrrrrrr.Where(x => aaaaaaaaaaaaaaaaaa && bbbbbbbbbbbbbbbbbb && ccccccccccccccccccc).ToList();\n",
            "        var nnnnnnnnnnnnnnnnnnnnnnnnnnn = rrrrrrrrrrrr\n"
            + "            .Where(x => aaaaaaaaaaaaaaaaaa && bbbbbbbbbbbbbbbbbb && ccccccccccccccccccc)\n"
            + "            .ToList();\n"
        );

    /// <summary>Where the line through the held call fits, the chain breaks before its next link instead.</summary>
    [Fact]
    public void FirstCallFits_BreaksBeforeTheNextLink() =>
        Agrees(
            "        var nnnnnnnnnnnnnnnnnn = rrrrrrrrrrrr.Where(x => aaaaaaaaaaaaaaaaa && bbbbbbbbbbbbbbbbb && ccccccccccccccccc).ToList();\n",
            "        var nnnnnnnnnnnnnnnnnn = rrrrrrrrrrrr.Where(x => aaaaaaaaaaaaaaaaa && bbbbbbbbbbbbbbbbb && ccccccccccccccccc)\n"
            + "            .ToList();\n"
        );

    /// <summary>
    ///     ⚠ Group I: the value does not fit below, and the held call would move. A 16-column name and a 5-column
    ///     receiver: three receivers fit in the name less one, so the <c>=</c> breaks and the call stays with its
    ///     receiver.
    /// </summary>
    [Fact]
    public void ValueTooWideBelow_ShortReceiver_BreaksTheEquals() =>
        Agrees(
            "        var nnnnnnnnnnnnnnnn = rrrrr.Where(x => aaaaaaaaaaaaaaaaaaaaaaaaa && bbbbbbbbbbbbbbbbbbbbbbbbb && cccccccccccccccccccccccccc).TTTTTTTTTTTTTTTTTTTT();\n",
            "        var nnnnnnnnnnnnnnnn =\n"
            + "            rrrrr.Where(x => aaaaaaaaaaaaaaaaaaaaaaaaa && bbbbbbbbbbbbbbbbbbbbbbbbb && cccccccccccccccccccccccccc)\n"
            + "                .TTTTTTTTTTTTTTTTTTTT();\n"
        );

    /// <summary>A receiver one column wider than that: the <c>=</c> stays and the call moves below the receiver.</summary>
    [Fact]
    public void ValueTooWideBelow_WiderReceiver_MovesTheCall() =>
        Agrees(
            "        var nnnnnnnnnnnnnnnn = rrrrrr.Where(x => aaaaaaaaaaaaaaaaaaaaaaaaa && bbbbbbbbbbbbbbbbbbbbbbbbb && ccccccccccccccccccccccccc).TTTTTTTTTTTTTTTTTTTT();\n",
            "        var nnnnnnnnnnnnnnnn = rrrrrr\n"
            + "            .Where(x => aaaaaaaaaaaaaaaaaaaaaaaaa && bbbbbbbbbbbbbbbbbbbbbbbbb && ccccccccccccccccccccccccc)\n"
            + "            .TTTTTTTTTTTTTTTTTTTT();\n"
        );

    /// <summary>A 31-column name still breaks the <c>=</c> over a one-column receiver: eight and five names make 163.</summary>
    [Fact]
    public void ValueTooWideBelow_LongName_BreaksTheEquals() =>
        Agrees(
            "        var nnnnnnnnnnnnnnnnnnnnnnnnnnnnnnn = r.Where(x => aaaaaaaaaaaaaaaaaaaaaaa && bbbbbbbbbbbbbbbbbbbbbbb && cccccccccccccccccccccccc).TTTTTTTTTTTTTTTTTTTT();\n",
            "        var nnnnnnnnnnnnnnnnnnnnnnnnnnnnnnn =\n"
            + "            r.Where(x => aaaaaaaaaaaaaaaaaaaaaaa && bbbbbbbbbbbbbbbbbbbbbbb && cccccccccccccccccccccccc)\n"
            + "                .TTTTTTTTTTTTTTTTTTTT();\n"
        );

    /// <summary>A 32-column name does not: 168 is past 165.</summary>
    [Fact]
    public void ValueTooWideBelow_LongerName_MovesTheCall() =>
        Agrees(
            "        var nnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnn = r.Where(x => aaaaaaaaaaaaaaaaaaaaaaa && bbbbbbbbbbbbbbbbbbbbbbb && cccccccccccccccccccccccc).TTTTTTTTTTTTTTTTTTTT();\n",
            "        var nnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnn = r\n"
            + "            .Where(x => aaaaaaaaaaaaaaaaaaaaaaa && bbbbbbbbbbbbbbbbbbbbbbb && cccccccccccccccccccccccc)\n"
            + "            .TTTTTTTTTTTTTTTTTTTT();\n"
        );

    /// <summary>An assignment whose receiver starts twelve columns into the statement keeps the <c>=</c>.</summary>
    [Fact]
    public void ValueTooWideBelow_ShortHead_MovesTheCall() =>
        Agrees(
            "        nnnnnnnnn = r.Where(x => aaaaaaaaaaaaaaaaaaaaaaaaaa && bbbbbbbbbbbbbbbbbbbbbbbbbbb && ccccccccccccccccccccccccccc).TTTTTTTTTTTTTTTTTTTT();\n",
            "        nnnnnnnnn = r\n"
            + "            .Where(x => aaaaaaaaaaaaaaaaaaaaaaaaaa && bbbbbbbbbbbbbbbbbbbbbbbbbbb && ccccccccccccccccccccccccccc)\n"
            + "            .TTTTTTTTTTTTTTTTTTTT();\n"
        );
}
