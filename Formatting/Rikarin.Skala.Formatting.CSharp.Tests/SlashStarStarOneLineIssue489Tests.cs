namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     Issue #489, SK-DIV-0181: a one-line <c>/** … */</c> above a declaration is rebuilt as a starred block.
///     Every expected string is <c>jb cleanupcode</c> 2025.2.6's output under <c>OracleProfile.DocComments</c>
///     (line endings aside: the oracle writes CRLF inside the block it rebuilds in an LF file), and
///     <c>constructs/trivia/slash-star-star-one-line-above-a-member.cs</c> carries the same shapes.
/// </summary>
public sealed class SlashStarStarOneLineIssue489Tests {
    [Fact]
    public void AOneLineBlockAboveAMember_IsRebuiltStarred() =>
        Agrees(
            "class C {\n    /** <summary>Doc.</summary> */\n    "
            + "public int F;\n\n    /** single */\n    public int G;\n}\n",
            "class C {\n"
            + "    /**\n     * <summary>Doc.</summary>\n     */\n    public int F;\n\n"
            + "    /**\n     * single\n     */\n    public int G;\n}\n"
        );

    [Fact]
    public void TheBodyIsLaidOutAsAnyDocComment() =>
        Agrees(
            "class C {\n    /** <summary>Doc.</summary><param "
            + "name=\"a\">A.</param> */\n    public void M(int a) { }\n}\n",
            "class C {\n    /**\n     * <summary>Doc.</summary>\n     * <param name=\"a\">A.</param>\n     */\n"
            + "    public void M(int a) { }\n}\n"
        );

    /// <summary>Each of these the oracle returns exactly as written.</summary>
    [Theory]
    [InlineData("class C {\n    /** single*/\n    public int F;\n}\n")]
    [InlineData("class C {\n    /**<summary>X</summary>*/\n    public int F;\n}\n")]
    [InlineData("class C {\n    [System.Obsolete] /** <summary>X</summary> */\n    public int F;\n}\n")]
    [InlineData("class C {\n    void M() {\n        /** above a statement */\n        int x = 1;\n    }\n}\n")]
    [InlineData("class C {\n    void M() {\n        /** <summary>X</summary> */\n        void L() { }\n    }\n}\n")]
    public void OutsideTheClass_ItIsLeftAsWritten(string source) => Assert.Equal(source, XmlDoc.Text(source));

    /// <summary>
    ///     ⚠ Two in a row are merged into one block by the oracle, and a line comment after one is moved below
    ///     it; Skala makes neither change, so it leaves both shapes as written rather than half-doing them.
    /// </summary>
    [Theory]
    [InlineData("class C {\n    /** <summary>A</summary> */\n    /** <summary>B</summary> */\n    public int F;\n}\n")]
    [InlineData("class C {\n    /** <summary>A</summary> */ // note\n    public int F;\n}\n")]
    public void ShapesTheOracleRebuildsFurther_AreLeftAsWritten(string source) =>
        Assert.Equal(source, XmlDoc.Text(source));

    static void Agrees(string source, string expected) {
        var once = XmlDoc.Text(source);
        Assert.Equal(expected, once);
        Assert.Equal(once, XmlDoc.Text(once));
    }
}
