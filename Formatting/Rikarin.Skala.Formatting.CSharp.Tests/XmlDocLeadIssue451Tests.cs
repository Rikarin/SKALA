namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     Issue #451, SK-DIV-0020: an element opened only because a child takes a line of its own. ⚠ The oracle
///     keeps the lead on the start tag's line on its first pass and hoists it when given that output back;
///     a third pass changes nothing. Skala writes the fixed point in one pass (#372's rule), and these pin
///     it. <c>constructs/trivia/doc-comment-lead-prose-given-back-is-hoisted.cs</c> carries the oracle's
///     first-pass answers with its fixed point beside them.
/// </summary>
public sealed class XmlDocLeadIssue451Tests {
    /// <summary>The oracle's first pass, given back: hoisted, which is also its second-pass answer.</summary>
    [Fact]
    public void TheOraclesFirstPass_GivenBack_IsHoisted() {
        Assert.Equal(
            ["/// <remarks>", "///     Some leading prose.", "///     <para>Short.</para>", "/// </remarks>"],
            Doc("/// <remarks>Some leading prose.", "///     <para>Short.</para>", "/// </remarks>")
        );
    }

    /// <summary>
    ///     ⚠ Written on one line, where the oracle's first pass keeps <c>Some leading prose.</c> beside
    ///     <c>&lt;remarks&gt;</c>: Skala goes straight to the fixed point, and the fixed point is stable.
    /// </summary>
    [Fact]
    public void WrittenOnOneLine_SkalaWritesTheFixedPoint() {
        Assert.Equal(
            [
                "/// <remarks>",
                "///     Some leading prose.",
                "///     <para>Short.</para>",
                "///     Trailing prose.",
                "/// </remarks>"
            ],
            Doc("/// <remarks>Some leading prose. <para>Short.</para> Trailing prose.</remarks>")
        );
    }

    static string[] Doc(params string[] lines) {
        var once = XmlDoc.Text(XmlDoc.InClass(lines));
        Assert.Equal(once, XmlDoc.Text(once));
        return [..XmlDoc.DocLines(once)];
    }
}
