namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     Issue #587: two elements glued together are a break point for width, with no space between them when they
///     stay on one line; an element glued to a word is not. Every expected line is <c>jb cleanupcode</c> 2025.2.6's
///     under <c>SkalaFormatOnly</c>; <c>constructs/trivia/doc-comment-glued-elements.cs</c> carries all seven shapes.
/// </summary>
public sealed class GluedElementsIssue587Tests {
    const string Long =
        "Another item, written at enough length that the list cannot fit on any single line of its own.";

    [Fact]
    public void GluedItems_StayTogetherWhenTheyFit_AndBreakWhenTheyDoNot() {
        Assert.Equal(
            ["/// <remarks>", "///     <list>", "///         <item>A.</item><item>B.</item>", "///     </list>", "/// </remarks>"],
            Doc("/// <remarks>", "/// <list><item>A.</item><item>B.</item></list>", "/// </remarks>")
        );
        Assert.Equal(
            [
                "/// <remarks>", "///     <list>", "///         <item>One item.</item>", "///         <item>" + Long + "</item>",
                "///     </list>", "/// </remarks>"
            ],
            Doc("/// <remarks>", "/// <list>", "/// <item>One item.</item><item>" + Long + "</item>", "/// </list>", "/// </remarks>")
        );
    }

    [Fact]
    public void GluedInlineElements_BreakBetweenThemAtTheMargin() =>
        Assert.Equal(
            [
                "/// <summary>",
                "///     Prose that runs along for quite some distance before it reaches two glued codes <c>alphaalpha</c>",
                "///     <c>betabetabeta</c> end.", "/// </summary>"
            ],
            Doc(
                "/// <summary>",
                "/// Prose that runs along for quite some distance before it reaches two glued codes <c>alphaalpha</c><c>betabetabeta</c> end.",
                "/// </summary>"
            )
        );

    [Fact]
    public void AnElementGluedToAWord_StillRidesPastTheMargin() =>
        Assert.Equal(
            [
                "/// <summary>",
                "///     Prose that runs along for quite some distance before it reaches a glued tail <c>alphaalphaalpha</c>betabeta end.",
                "/// </summary>"
            ],
            Doc(
                "/// <summary>",
                "/// Prose that runs along for quite some distance before it reaches a glued tail <c>alphaalphaalpha</c>betabeta end.",
                "/// </summary>"
            )
        );

    static string[] Doc(params string[] lines) {
        var once = XmlDoc.Text(XmlDoc.InClass(lines));
        Assert.Equal(once, XmlDoc.Text(once));
        return [..XmlDoc.DocLines(once)];
    }
}
