namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     SK-DIV-0381: the tag-header keys at their non-export values. Every expected value is
///     <c>jb cleanupcode</c> 2025.2.6's under <c>OracleProfile.DocComments</c> with the one key flipped;
///     <c>constructs/xmldoc/skala_xmldoc_attribute_style.cs</c>, <c>…_attribute_indent.cs</c> and
///     <c>…_wrap_tags_and_pi.cs</c> carry the export's answers.
/// </summary>
public sealed class XmlDocHeaderKeysSkDiv0381Tests {
    const string Five =
        """<customElementName alphaAttribute="1" betaAttribute="2" gammaAttribute="3" deltaAttribute="4" """
        + "epsilonAttribute=\"5\"";

    static readonly string[] PastTheMargin = ["/// <summary>Text.</summary>" + Five + """ zetaAttribute="6" />"""];

    [Fact]
    public void AtWrapTagsAndPiFalse_NoBreakIsIntroduced_AndAnAuthorsIsKept() {
        Assert.Equal(
            ["/// <summary>Text.</summary>", "/// " + Five + " zetaAttribute=\"6\" />"],
            Doc(PastTheMargin, ("skala_xmldoc_wrap_tags_and_pi", "false"))
        );
        Assert.Equal(
            [
                "/// <remarks>", "///     <see cref=\"System.String\"",
                "///         href=\"https://short.invalid/\" />", "/// </remarks>"
            ],
            Doc(
                [
                    "/// <remarks>", "/// <see cref=\"System.String\"", "///     href=\"https://short.invalid/\" />",
                    "/// </remarks>"
                ],
                ("skala_xmldoc_wrap_tags_and_pi", "false")
            )
        );
    }

    [Fact]
    public void AttributeIndent_SingleDoubleAndAligned() {
        Assert.Equal(
            ["/// <summary>Text.</summary>", "/// " + Five, """///         zetaAttribute="6" />"""],
            Doc(PastTheMargin, ("skala_xmldoc_attribute_indent", "double_indent"))
        );
        Assert.Equal(
            ["/// <summary>Text.</summary>", "/// " + Five, """///                    zetaAttribute="6" />"""],
            Doc(PastTheMargin, ("skala_xmldoc_attribute_indent", "align_by_first_attribute"))
        );
    }

    /// <summary>
    ///     ⚠ An alignment column at two thirds of the margin or past it falls back to two indents: 79 aligns,
    ///     80 does not, at 120.
    /// </summary>
    [Fact]
    public void AlignedPastTwoThirdsOfTheMargin_FallsBackToTwoIndents() {
        var attributes = string.Join(" ", Enumerable.Range(1, 29).Select(static i => $"a{i}=\"{i}\""));
        var near = Doc(
            ["/// <summary>Text.</summary><" + new string('n', 77) + " " + attributes + " />"],
            ("skala_xmldoc_attribute_indent", "align_by_first_attribute")
        );
        var far = Doc(
            ["/// <summary>Text.</summary><" + new string('n', 78) + " " + attributes + " />"],
            ("skala_xmldoc_attribute_indent", "align_by_first_attribute")
        );

        Assert.StartsWith("/// " + new string(' ', 79) + "a", near[2], StringComparison.Ordinal);
        Assert.StartsWith("/// " + new string(' ', 8) + "a", far[2], StringComparison.Ordinal);
    }

    [Fact]
    public void AttributeStyle_EveryValue() {
        string[] broken = [
            "/// <remarks>", "/// <see cref=\"System.String\"", """///     href="https://short.invalid/" />""",
            "/// </remarks>"
        ];
        Assert.Equal(
            [
                "/// <remarks>", """///     <see cref="System.String" href="https://short.invalid/" />""",
                "/// </remarks>"
            ],
            Doc(broken, ("skala_xmldoc_attribute_style", "on_single_line"))
        );

        string[] param = ["""/// <param name="a">Single attribute.</param>"""];
        Assert.Equal(
            ["/// <param", """///     name="a">""", "///     Single attribute.", "/// </param>"],
            Doc(param, ("skala_xmldoc_attribute_style", "on_different_lines"))
        );
        Assert.Equal(
            ["""/// <param name="a">Single attribute.</param>"""],
            Doc(param, ("skala_xmldoc_attribute_style", "first_attribute_on_single_line"))
        );

        string[] two = [
            """/// <summary>Text.</summary><customElement alphaAttribute="1" betaAttribute="2">Body.</customElement>"""
        ];
        Assert.Equal(
            [
                "/// <summary>Text.</summary>",
                "/// <customElement alphaAttribute=\"1\"",
                "///     betaAttribute=\"2\">",
                "///     Body.",
                "/// </customElement>"
            ],
            Doc(two, ("skala_xmldoc_attribute_style", "first_attribute_on_single_line"))
        );
    }

    static string[] Doc(string[] lines, params (string Key, string Value)[] overrides) {
        var once = XmlDoc.Text(XmlDoc.InClass(lines), overrides);
        Assert.Equal(once, XmlDoc.Text(once, overrides));
        return [..XmlDoc.DocLines(once)];
    }
}
