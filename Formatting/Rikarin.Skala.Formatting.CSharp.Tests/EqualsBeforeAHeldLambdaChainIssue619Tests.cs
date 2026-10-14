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
}
