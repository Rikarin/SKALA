namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     Issue #448, SK-DIV-0006 and SK-DIV-0079: a tag header past the margin is wrapped at its attributes,
///     and a break the author wrote inside a header is kept. Every expected value is <c>jb cleanupcode</c>
///     2025.2.6's under <c>OracleProfile.DocComments</c>; <c>constructs/trivia/doc-comment-tag-header-wrap.cs</c>
///     carries these and more with the oracle's fixture beside them.
/// </summary>
public sealed class XmlDocHeaderWrapIssue448Tests {
    const string Five =
        "<customElement alphaAttribute=\"1\" betaAttribute=\"2\" gammaAttribute=\"3\" deltaAttribute=\"4\" "
        + "epsilonAttribute=\"5\"";

    const string Cref =
        "<see cref=\"System.Collections.Generic.Dictionary{TKeyOfSomeVeryLongName,TValueOfSomeVeryLongName}\"";

    const string Href = "href=\"https://example.invalid/a/very/long/documentation/link/that/will/not/fit\" />";

    [Fact]
    public void AHeaderPastTheMargin_BreaksBeforeTheFirstAttributeThatDoesNotFit() =>
        Assert.Equal(
            [
                "/// <summary>Text.</summary>",
                "/// " + Five,
                "///     zetaAttribute=\"6\">",
                "///     Body.",
                "/// </customElement>"
            ],
            Doc("/// <summary>Text.</summary>" + Five + " zetaAttribute=\"6\">Body.</customElement>")
        );

    /// <summary>The <c>&gt;</c> after the last attribute is not counted: at 120 with it, the header stays whole.</summary>
    [Fact]
    public void TheClosingAngle_IsNotCounted() =>
        Assert.Equal(
            ["/// <summary>Text.</summary>", "/// " + Five + " z=\"xxxx\">Body.</customElement>"],
            Doc("/// <summary>Text.</summary>" + Five + " z=\"xxxx\">Body.</customElement>")
        );

    [Fact]
    public void ANestedTag_ContinuesOneIndentPastItsOwnColumn() =>
        Assert.Equal(
            ["/// <remarks>", "///     " + Cref, "///         " + Href, "/// </remarks>"],
            Doc("/// <remarks>", "/// " + Cref + " " + Href, "/// </remarks>")
        );

    /// <summary>
    ///     ⚠ An author's break inside a header is kept, though the header would fit, and re-indented to the
    ///     tag's column plus one indent; a break before <c>/&gt;</c> is not a break between attributes and is
    ///     joined.
    /// </summary>
    [Fact]
    public void AnAuthorsBreak_IsKept_ButNotOneBeforeTheCloser() {
        Assert.Equal(
            [
                "/// <summary>Text.</summary>",
                "/// <customElement alphaAttribute=\"1\"",
                "///     betaAttribute=\"2\">",
                "///     Body.",
                "/// </customElement>"
            ],
            Doc(
                "/// <summary>Text.</summary>",
                "/// <customElement alphaAttribute=\"1\"",
                "///  betaAttribute=\"2\">Body.</customElement>"
            )
        );

        Assert.Equal(
            ["/// <remarks>", "///     <see cref=\"System.String\" href=\"https://short.invalid/\" />", "/// </remarks>"],
            Doc("/// <remarks>", "/// <see cref=\"System.String\" href=\"https://short.invalid/\"", "///  />", "/// </remarks>")
        );
    }

    /// <summary>
    ///     ⚠ Content that is one unbreakable word is not opened however long: opening cannot move the word
    ///     left, because the first content line is filled from the start tag's closing column. One more word
    ///     and it is.
    /// </summary>
    [Fact]
    public void OneLongWord_IsNotOpened_TwoAre() {
        var word = new string('w', 115);
        Assert.Equal(["/// <summary>" + word + "</summary>"], Doc("/// <summary>" + word + "</summary>"));
        Assert.Equal(
            ["/// <summary>", "///     " + new string('w', 112), "///     b", "/// </summary>"],
            Doc("/// <summary>" + new string('w', 112) + " b</summary>")
        );
    }

    static string[] Doc(params string[] lines) {
        var once = XmlDoc.Text(XmlDoc.InClass(lines));
        Assert.Equal(once, XmlDoc.Text(once));
        return [.. XmlDoc.DocLines(once)];
    }
}
