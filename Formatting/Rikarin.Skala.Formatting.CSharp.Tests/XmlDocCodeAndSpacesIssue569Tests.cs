namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     Issue #569: the last doc-comment hunks of <c>corpus/real</c>, each a rule of its own. Every expected line is
///     <c>jb cleanupcode</c> 2025.2.6's under <c>SkalaFormatOnly</c>;
///     <c>constructs/trivia/doc-comment-code-block-edges.cs</c>,
///     <c>…-inline-code-spanning-lines.cs</c> and <c>…-space-runs.cs</c> carry the wider probes.
/// </summary>
public sealed class XmlDocCodeAndSpacesIssue569Tests {
    /// <summary>
    ///     A <c>&lt;code&gt;</c> spread over lines keeps its edges as written: the end tag's own whitespace, code
    ///     on the start tag's line, and three lines for one line of code. Serilog's <c>LogContext</c>.
    /// </summary>
    [Fact]
    public void ACodeBlock_KeepsItsEdges() {
        Assert.Equal(
            [
                "/// <example>", "///     Text:", "///     <code>", "/// var a = 1;", "///     a++;", "/// </code>",
                "/// </example>"
            ],
            Doc(
                "/// <example>",
                "/// Text:",
                "/// <code>",
                "/// var a = 1;",
                "///     a++;",
                "/// </code>",
                "/// </example>"
            )
        );
        Assert.Equal(
            ["/// <example>", "///     <code>", "/// var a = 1;", "///         </code>", "/// </example>"],
            Doc("/// <example>", "///     <code>", "/// var a = 1;", "///         </code>", "/// </example>")
        );
        Assert.Equal(
            ["/// <remarks>", "///     <code>var a = 1;", "/// a++;", "/// </code>", "/// </remarks>"],
            Doc("/// <remarks>", "/// <code>var a = 1;", "/// a++;", "/// </code>", "/// </remarks>")
        );
    }

    /// <summary>
    ///     A <c>&lt;c&gt;</c> spread over lines is re-indented a line at a time, its inner spaces kept. Vixen's
    ///     <c>UtilityComposition</c>.
    /// </summary>
    [Fact]
    public void AnInlineCodeSpanningLines_IsReindented() =>
        Assert.Equal(
            [
                "/// <remarks>", "///     Lead", "///     <c>", "///         alpha  beta", "///         gamma",
                "///     </c>", "///     trail.", "/// </remarks>"
            ],
            Doc("/// <remarks>Lead <c>alpha  beta", "///         gamma</c> trail.</remarks>")
        );

    /// <summary>
    ///     The author's double space survives, and is a column wide: Serilog's <c>PropertyToken</c> line ends one
    ///     column past the margin because of it, and the oracle wraps it.
    /// </summary>
    [Fact]
    public void ARunOfSpaces_SurvivesAndCounts() {
        Assert.Equal(
            ["/// <summary>End.  Next sentence.</summary>"],
            Doc("/// <summary>End.  Next sentence.</summary>")
        );
        Assert.Equal(
            [
                "/// <returns>",
                "///     <see langword=\"true\" /> if the specified object  is equal to the current object; otherwise,",
                "///     <see langword=\"false\" />.", "/// </returns>"
            ],
            Doc(
                "/// <returns>",
                "/// <see langword=\"true\" /> if the specified object  is equal to the current object; otherwise, <see langword=\"false\" />.",
                "/// </returns>"
            )
        );
    }

    static string[] Doc(params string[] lines) {
        var once = XmlDoc.Text(XmlDoc.InClass(lines));
        Assert.Equal(once, XmlDoc.Text(once));
        return [..XmlDoc.DocLines(once)];
    }
}
