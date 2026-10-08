namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     Issue #568, SK-DIV-0380: which multi-line <c>/** … */</c> blocks are rebuilt as starred blocks. Every expected
///     value is <c>jb cleanupcode</c> 2025.2.6's output for the input under <c>SkalaDocComments</c>;
///     <c>constructs/trivia/doc-comment-multi-line-block.cs</c> carries all twenty-eight shapes.
/// </summary>
public sealed class MultiLineDocBlockIssue568Tests {
    [Theory]
    // Content on the opener's line and another line before the closer, starred or not: rebuilt.
    [InlineData("/** a\n     * b\n     */", "/**\n     * a\n     * b\n     */")]
    [InlineData("/** a\n       b\n     */", "/**\n     * a\n     * b\n     */")]
    [InlineData("/** a\n     * b */", "/**\n     * a\n     * b\n     */")]
    [InlineData(
        "/** <summary>Doc.</summary>\n        <remarks>Unstarred second.</remarks> */",
        "/**\n     * <summary>Doc.</summary>\n     * <remarks>Unstarred second.</remarks>\n     */"
    )]
    // `/**` alone and every line starred on the opener's column plus one: rebuilt, and formatted.
    [InlineData("/**\n     * text */", "/**\n     * text\n     */")]
    [InlineData(
        "/**\n     * <summary>\n     * Doc.\n     * </summary>\n     */",
        "/**\n     * <summary>\n     *     Doc.\n     * </summary>\n     */"
    )]
    [InlineData("/**\n     *\n     * <summary>blank first</summary>\n     */", "/**\n     * <summary>blank first</summary>\n     */")]
    // Left as written by the oracle, and by Skala.
    [InlineData("/** text\n     */", "/** text\n     */")]
    [InlineData("/**\n     *no space\n     */", "/**\n     *no space\n     */")]
    [InlineData("/**\n      * a\n     */", "/**\n      * a\n     */")]
    [InlineData("/**\n     * a\n    */", "/**\n     * a\n    */")]
    public void TheBlock(string written, string expected) =>
        Oracle.Agrees(
            "class C {\n    " + written + "\n    void M() { }\n}\n",
            "class C {\n    " + expected + "\n    void M() { }\n}\n"
        );
}
