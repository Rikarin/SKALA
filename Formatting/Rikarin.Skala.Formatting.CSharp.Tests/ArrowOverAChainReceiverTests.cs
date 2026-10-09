namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     ⚠ Fuzz 16278079796336422477: a lambda among a chopped tuple's elements whose body is a call chain that
///     fits neither below the arrow nor beside it. Skala kept <c>=&gt; builder</c> past the margin and broke at
///     the first dot; pass two read that break as the author's and broke the arrow. Every expected string is
///     <c>jb cleanupcode</c>'s: of 135 such lambdas (among arguments, in a tuple, in a chopped tuple; the arrow
///     ending at 50 to 117) the arrow breaks in exactly the rows where <c>=&gt; builder</c> ends past 120.
/// </summary>
public sealed class ArrowOverAChainReceiverTests {
    const string Body =
        "builder.Length(\"sssss\").First(\"ssssssssssssssssssssssss\").Items(\"ssssssssssssssssssssssssssssssssssssssssssssssssssssssssssss\")";

    static readonly string Head = "        return (" + new string('a', 100) + ",\n";

    static string Wrap(string statement) => "class C {\n    object M() {\n" + statement + "    }\n}\n";

    static string Source(int parameter) =>
        Wrap(
            "        return ("
            + new string('a', 100)
            + ", ("
            + new string('T', parameter)
            + " x) => "
            + Body
            + ", bbbb);\n"
        );

    static void Agrees(string source, string expected) {
        var once = Format.Text(source).ReplaceLineEndings("\n");
        Assert.Equal(expected, once);
        Assert.Equal(once, Format.Text(once).ReplaceLineEndings("\n"));
    }

    /// <summary>The arrow at 113: <c>=&gt; builder</c> would end at 121, so the arrow breaks.</summary>
    [Fact]
    public void AReceiverPastTheMargin_BreaksTheArrow() =>
        Agrees(
            Source(94),
            Wrap(
                Head
                + "            ("
                + new string('T', 94)
                + " x) =>\n"
                + "                builder.Length(\"sssss\")\n"
                + "                    .First(\"ssssssssssssssssssssssss\")\n"
                + "                    .Items(\"ssssssssssssssssssssssssssssssssssssssssssssssssssssssssssss\"), bbbb);\n"
            )
        );

    /// <summary>The arrow at 110: <c>=&gt; builder</c> ends at 118, and the chain breaks at its dots.</summary>
    [Fact]
    public void AReceiverThatFits_KeepsTheArrow() =>
        Agrees(
            Source(91),
            Wrap(
                Head
                + "            ("
                + new string('T', 91)
                + " x) => builder\n"
                + "                .Length(\"sssss\")\n"
                + "                .First(\"ssssssssssssssssssssssss\")\n"
                + "                .Items(\"ssssssssssssssssssssssssssssssssssssssssssssssssssssssssssss\"), bbbb);\n"
            )
        );
}
