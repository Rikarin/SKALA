namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     Issues #541–#544: what follows an element that spans lines or owns its line, an element whose content
///     ends in an element, and the start tag's carry beside an author's break. Every expected value is
///     <c>jb cleanupcode</c> 2025.2.6's under <c>OracleProfile.DocComments</c>;
///     <c>constructs/trivia/doc-comment-what-follows-an-element.cs</c> carries these and more.
/// </summary>
public sealed class XmlDocElementEndIssues541To544Tests {
    /// <summary>#541: prose after an element opened across lines starts a line of its own.</summary>
    [Fact]
    public void ProseAfterAnOpenedElement_StartsALine() =>
        Assert.Equal(
            [
                "/// <remarks>",
                "///     Lead",
                "///     <i>",
                "///         an italic run",
                "///         over two lines",
                "///     </i>",
                "///     and more prose.",
                "/// </remarks>"
            ],
            Doc(
                "/// <remarks>",
                "///     Lead <i>an italic run",
                "///     over two lines</i> and more prose.",
                "/// </remarks>"
            )
        );

    /// <summary>
    ///     #542: a full stop glued to an opened element's end tag, or to a top-level element, goes to the next
    ///     line — the oracle inserts that break and the signature allows exactly it.
    /// </summary>
    [Fact]
    public void AGluedFullStop_AfterAnElementThatEndsItsLine_StartsALine() {
        Assert.Equal(
            [
                "/// <remarks>",
                "///     Lead",
                "///     <i>",
                "///         an italic run",
                "///         over two lines",
                "///     </i>",
                "///     . A plan says what will happen.",
                "/// </remarks>"
            ],
            Doc(
                "/// <remarks>",
                "///     Lead <i>an italic run",
                "///     over two lines</i>. A plan says what will happen.",
                "/// </remarks>"
            )
        );

        Assert.Equal(
            ["/// <summary>Doc.</summary>", "/// <seealso cref=\"System.String\" />", "/// ."],
            Doc("/// <summary>Doc.</summary>", "/// <seealso cref=\"System.String\"/>.")
        );
    }

    /// <summary>A glued comma after an inline element that does not end its line stays glued.</summary>
    [Fact]
    public void AGluedComma_AfterAnInlineElement_Stays() =>
        Assert.Equal(
            ["/// <remarks>Lead <see cref=\"System.String\" />, then words.</remarks>"],
            Doc("/// <remarks>Lead <see cref=\"System.String\"/>, then words.</remarks>")
        );

    /// <summary>
    ///     ⚠ The allowance is one-directional: a word moved below an element's end may have been beside it,
    ///     but a word that was below it may not be glued to it.
    /// </summary>
    [Fact]
    public void TheSignature_AllowsABreakAfterAnElement_ButNotAJoinOntoIt() {
        const string glued = "<e:i>x</e>\u0002.";
        const string spaced = "<e:i>x</e> .";
        const string broken = "<e:i>x</e>\u0001.";

        Assert.True(XmlDocSignature.Matches(glued, broken));
        Assert.True(XmlDocSignature.Matches(spaced, broken));
        Assert.True(XmlDocSignature.Matches(broken, spaced));
        Assert.False(XmlDocSignature.Matches(broken, glued));
        Assert.False(XmlDocSignature.Matches(glued, spaced));
    }

    /// <summary>
    ///     #543: content ending in an element counts the end tag — 119 columns with it stays flat, 120 opens.
    /// </summary>
    [Fact]
    public void ContentEndingInAnElement_CountsTheEndTag() {
        var x = string.Join(" ", Enumerable.Repeat("x", 26));
        Assert.Equal(
            ["/// <exception cref=\"ArgumentException\">When " + x + " yy <c>null</c></exception>"],
            Doc("/// <exception cref=\"ArgumentException\">When " + x + " yy <c>null</c></exception>")
        );
        Assert.Equal(
            [
                "/// <exception cref=\"ArgumentException\">",
                "///     When " + x + " yyy <c>null</c>",
                "/// </exception>"
            ],
            Doc("/// <exception cref=\"ArgumentException\">When " + x + " yyy <c>null</c></exception>")
        );
    }

    /// <summary>#544: an author's break in the content means no carry; without one the carry stands.</summary>
    [Fact]
    public void AnAuthorsBreak_DropsTheCarry() {
        const string Lead =
            "This type is currently internal, while we consider future directions for the logging pipeline, but "
            + "should end up";
        Assert.Equal(
            ["/// <remarks>", "///     " + Lead, "///     public", "///     in future.", "/// </remarks>"],
            Doc("/// <remarks>" + Lead + " public", "/// in future.</remarks>")
        );
        Assert.Equal(
            [
                "/// <remarks>",
                "///     " + Lead[..^3],
                "///     up public in future, and then some more words to wrap again.",
                "/// </remarks>"
            ],
            Doc("/// <remarks>" + Lead + " public in future, and then some more words to wrap again.</remarks>")
        );
    }

    static string[] Doc(params string[] lines) {
        var once = XmlDoc.Text(XmlDoc.InClass(lines));
        Assert.Equal(once, XmlDoc.Text(once));
        return [..XmlDoc.DocLines(once)];
    }
}
