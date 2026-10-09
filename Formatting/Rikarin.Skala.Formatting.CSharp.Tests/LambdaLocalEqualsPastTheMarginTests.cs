namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     ⚠ Nightly `fuzz --seed=99991`, replay 13830403873739157460: a lambda-valued local whose <c>=</c> ends past the
///     margin. Skala kept <c>T v = (</c> and chopped the parameter list past the margin; pass two read the chop as
///     the author's and broke inside the type. Every expected line is <c>jb cleanupcode</c>'s, measured on 90 such
///     locals at indent 8: the <c>=</c> ending at 121 or later breaks between the type and the name, and at 119
///     and 120 — where the value's first character would land past the margin — the <c>=</c> breaks.
/// </summary>
public sealed class LambdaLocalEqualsPastTheMarginTests {
    const string Type = "(Span<IReadOnlyDictionary<StringBuilder, StringBuilder>> First, Dictionary<string, long?> Second)";

    static string Wrap(string body) => "class C {\n    void M() {\n" + body + "    }\n}\n";

    static void Agrees(string statement, string expected) {
        var once = Format.Text(Wrap("        " + statement + "\n")).ReplaceLineEndings("\n");
        Assert.Equal(Wrap(expected), once);
        Assert.Equal(once, Format.Text(once).ReplaceLineEndings("\n"));
    }

    /// <summary>The replay's value, its <c>=</c> ending at 124 as the replay's did at indent 20.</summary>
    [Fact]
    public void AnEqualsPastTheMargin_BreaksTheTypeAndTheName() =>
        Agrees(
            Type + " vvvvvvvvvvvvvvvv = (x, y) => Source;",
            "        " + Type + "\n            vvvvvvvvvvvvvvvv = (x, y) => Source;\n"
        );

    /// <summary>One column past the margin, through the <c>=</c>.</summary>
    [Fact]
    public void AnEqualsEndingAt121_BreaksTheTypeAndTheName() =>
        Agrees(
            Type + " vvvvvvvvvvvvv = x => Source;",
            "        " + Type + "\n            vvvvvvvvvvvvv = x => Source;\n"
        );

    /// <summary>The <c>=</c> at 120: its value would start past the margin, so the <c>=</c> breaks.</summary>
    [Theory]
    [InlineData("(x, y) => Source")]
    [InlineData("x => Source")]
    public void AnEqualsEndingAt120_Breaks(string value) =>
        Agrees(
            Type + " vvvvvvvvvvvv = " + value + ";",
            "        " + Type + " vvvvvvvvvvvv =\n            " + value + ";\n"
        );

    /// <summary>The <c>=</c> at 118: the parameter list chops, as it did.</summary>
    [Fact]
    public void AnEqualsEndingAt118_ChopsTheParameters() =>
        Agrees(
            Type + " vvvvvvvvvv = (x, y) => Source;",
            "        " + Type + " vvvvvvvvvv = (\n            x,\n            y\n        ) => Source;\n"
        );
}
