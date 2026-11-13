namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     ⚠ An <c>=</c> before a held lambda call whose line overflows below too (#619, SK-DIV-0510 round 3). Past the
///     margin the oracle reads the held line as the margin: with a long enough rest after the call, the call would move,
///     so group I's <c>=</c> rule breaks the <c>=</c>. <c>r.Where(…)</c> then overflows below and breaks its arrow. Every
///     expected string is <c>jb cleanupcode</c>'s.
/// </summary>
public sealed class EqualsBeforeAnOverflowingHeldCallIssue619Tests {
    static string Wrap(string body) => "class C {\n    void M() {\n" + body + "    }\n}\n";

    static void Agrees(string source, string expected) {
        var once = Format.Text(Wrap(source)).ReplaceLineEndings("\n");
        Assert.Equal(Wrap(expected), once);
        Assert.Equal(once, Format.Text(once).ReplaceLineEndings("\n"));
    }

    const string A31 = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    const string B31 = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
    const string C32 = "cccccccccccccccccccccccccccccccc";

    /// <summary>A rest of 24 columns: the <c>=</c> breaks, though the call overflows below as well.</summary>
    [Fact]
    public void LongRest_BreaksTheEquals() =>
        Agrees(
            $"        var nnnnnnnnnn = r.Where(x => {A31} && {B31} && {C32}).TTTTTTTTTTTTTTTTTTTT();\n",
            "        var nnnnnnnnnn =\n"
            + "            r.Where(x =>\n"
            + $"                    {A31}\n"
            + $"                    && {B31}\n"
            + $"                    && {C32}\n"
            + "                )\n"
            + "                .TTTTTTTTTTTTTTTTTTTT();\n"
        );

    /// <summary>A rest of 20: the held call would not move, so the <c>=</c> stays and the arrow breaks beside it.</summary>
    [Fact]
    public void ShortRest_KeepsTheEquals() =>
        Agrees(
            $"        var nnnnnnnnnn = r.Where(x => {A31} && {B31} && {C32}).TTTTTTTTTTTTTTTT();\n",
            $"        var nnnnnnnnnn = r.Where(x =>\n                {A31} && {B31} && {C32}\n            )\n"
            + "            .TTTTTTTTTTTTTTTT();\n"
        );

    /// <summary>An assignment one level in, a three-column receiver and a rest of 26.</summary>
    [Fact]
    public void Assignment_LongRest_BreaksTheEquals() =>
        Agrees(
            "        if (b) {\n"
            + "            nnnnnnnnnnnnnnn = rrr.Where(x => aaaaaaaaaaaaaaaaaaaaaaaaaaaaa && bbbbbbbbbbbbbbbbbbbbbbbbbbbbb"
            + " && cccccccccccccccccccccccccccccc).TTTTTTTTTTTTTTTTTTTTTT();\n"
            + "        }\n",
            "        if (b) {\n"
            + "            nnnnnnnnnnnnnnn =\n"
            + "                rrr.Where(x =>\n"
            + "                        aaaaaaaaaaaaaaaaaaaaaaaaaaaaa && bbbbbbbbbbbbbbbbbbbbbbbbbbbbb && cccccccccccccccccccccccccccccc\n"
            + "                    )\n"
            + "                    .TTTTTTTTTTTTTTTTTTTTTT();\n"
            + "        }\n"
        );
}
