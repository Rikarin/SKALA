using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Core.Configuration;

namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     Issue #428, SK-DIV-0094 and SK-DIV-0193: a multi-line block comment's continuation lines move with
///     the line its first line is on. Every expected string is <c>jb cleanupcode</c> 2025.2.6's own output
///     for the input under <c>SkalaFormatOnly</c>, at the configuration each test names, and every test
///     asserts that a second pass changes nothing.
/// </summary>
public sealed class BlockCommentShiftIssue428Tests {
    const string PlainSource =
        "class C {\n"
        + "      /*\n"
        + "         moved left two\n"
        + "       */\n"
        + "    void A() { }\n"
        + "\n"
        + "  /*\n"
        + "     moved right two\n"
        + "   */\n"
        + "    void B() { }\n"
        + "\n"
        + "            /*\n"
        + "  left of the shift\n"
        + "       still left of it\n"
        + "\n"
        + "                kept apart\n"
        + "            */\n"
        + "    void D() { }\n"
        + "}\n";

    const string PlainOracle =
        "class C {\n"
        + "    /*\n"
        + "       moved left two\n"
        + "     */\n"
        + "    void A() { }\n"
        + "\n"
        + "    /*\n"
        + "       moved right two\n"
        + "     */\n"
        + "    void B() { }\n"
        + "\n"
        + "    /*\n"
        + "left of the shift\n"
        + "still left of it\n"
        + "\n"
        + "        kept apart\n"
        + "    */\n"
        + "    void D() { }\n"
        + "}\n";

    const string DocSource =
        "class C {\n"
        + "      /**\n"
        + "       * doc starred\n"
        + "       */\n"
        + "    void A() { }\n"
        + "\n"
        + "      /**\n"
        + "         * ragged doc\n"
        + "     * still ragged\n"
        + "       */\n"
        + "    void B() { }\n"
        + "\n"
        + "      /*\n"
        + "         * ragged plain\n"
        + "     * aligned instead\n"
        + "       */\n"
        + "    void D() { }\n"
        + "\n"
        + "      /** doc plain\n"
        + "            second\n"
        + "       */\n"
        + "    void E() { }\n"
        + "}\n";

    const string DocOracle =
        "class C {\n"
        + "    /**\n"
        + "     * doc starred\n"
        + "     */\n"
        + "    void A() { }\n"
        + "\n"
        + "    /**\n"
        + "       * ragged doc\n"
        + "   * still ragged\n"
        + "     */\n"
        + "    void B() { }\n"
        + "\n"
        + "    /*\n"
        + "     * ragged plain\n"
        + "     * aligned instead\n"
        + "     */\n"
        + "    void D() { }\n"
        + "\n"
        + "    /** doc plain\n"
        + "          second\n"
        + "     */\n"
        + "    void E() { }\n"
        + "}\n";

    const string LineSource =
        "class C {\n"
        + "    void A(int a,\n"
        + "              /* c\n"
        + "                 d */ int b) { }\n"
        + "\n"
        + "    void B() {\n"
        + "          M(); /* trailing\n"
        + "                  more */\n"
        + "          var x = 1 + /* expr\n"
        + "                       expr2 */ 2;\n"
        + "        var q = new int[] { 1, /* c\n"
        + "                                  d */ 2 };\n"
        + "        if (true)\n"
        + "        { /* brace\n"
        + "             x */\n"
        + "            M();\n"
        + "        }\n"
        + "    }\n"
        + "\n"
        + "    void M() { }\n"
        + "}\n";

    const string LineOracle =
        "class C {\n"
        + "    void A(\n"
        + "        int a,\n"
        + "        /* c\n"
        + "           d */\n"
        + "        int b\n"
        + "    ) { }\n"
        + "\n"
        + "    void B() {\n"
        + "        M(); /* trailing\n"
        + "                more */\n"
        + "        var x = 1\n"
        + "            + /* expr\n"
        + "                         expr2 */ 2;\n"
        + "        var q = new int[] {\n"
        + "            1, /* c\n"
        + "                                      d */ 2\n"
        + "        };\n"
        + "        if (true) { /* brace\n"
        + "             x */\n"
        + "            M();\n"
        + "        }\n"
        + "    }\n"
        + "\n"
        + "    void M() { }\n"
        + "}\n";

    const string TrimSource =
        "class C {\n"
        + "    /* first   \n"
        + "       second   \n"
        + "    \n"
        + "       */\n"
        + "    int F;\n"
        + "\n"
        + "      /* moved   \n"
        + "         second\t \n"
        + "      */\n"
        + "    int G;\n"
        + "\n"
        + "    /**\n"
        + "     * doc   \n"
        + "     *   \n"
        + "     */\n"
        + "    int H;\n"
        + "\n"
        + "    /*\n"
        + "   * One.\n"
        + "   \n"
        + "        * Two.\n"
        + "     */\n"
        + "    int I;\n"
        + "}\n";

    const string TrimOracle =
        "class C {\n"
        + "    /* first\n"
        + "       second\n"
        + "\n"
        + "       */\n"
        + "    int F;\n"
        + "\n"
        + "    /* moved\n"
        + "       second\n"
        + "    */\n"
        + "    int G;\n"
        + "\n"
        + "    /**\n"
        + "     * doc\n"
        + "     *\n"
        + "     */\n"
        + "    int H;\n"
        + "\n"
        + "    /*\n"
        + "   * One.\n"
        + "\n"
        + "        * Two.\n"
        + "     */\n"
        + "    int I;\n"
        + "}\n";

    const string TabsSource =
        "class T {\n"
        + "\t\t\t/*\n"
        + "\t\t\t\tt3\n"
        + "\t\t\t */\n"
        + "\tvoid A() {\n"
        + "\t/*\n"
        + "\t\t\t\t\tt4\n"
        + "\t */\n"
        + "\t\tM();\n"
        + "\t}\n"
        + "\n"
        + "\t\t/*\n"
        + "\t\t  \tt5\n"
        + "\t\t*/\n"
        + "\tvoid B() { }\n"
        + "\n"
        + "\t\t/*\n"
        + "\t\t\t  t6\n"
        + "\t\t */\n"
        + "\tvoid D() { }\n"
        + "\n"
        + "\t\t/*\n"
        + "      t10 spaces only under a tab opener\n"
        + "\t\t*/\n"
        + "\tvoid E() { }\n"
        + "\n"
        + "  /*\n"
        + "\t  mixed\n"
        + "   */\n"
        + "\tvoid F() { }\n"
        + "\n"
        + "    void G() { /* c\n"
        + "      d */ M(); }\n"
        + "\n"
        + "\t\t/*\n"
        + "\t\t * starred\n"
        + "\t\t */\n"
        + "\tvoid H() {\n"
        + "\t\t  M(); /* trailing\n"
        + "\t\t\t\t * starred\n"
        + "\t\t\t\t */\n"
        + "\t}\n"
        + "\n"
        + "\tvoid M() { }\n"
        + "}\n";

    const string TabsOracle =
        "class T {\n"
        + "\t/*\n"
        + "\t\tt3\n"
        + "\t */\n"
        + "\tvoid A() {\n"
        + "\t\t/*\n"
        + "\t\t\t\t\t\tt4\n"
        + "\t\t */\n"
        + "\t\tM();\n"
        + "\t}\n"
        + "\n"
        + "\t/*\n"
        + "\t    t5\n"
        + "\t*/\n"
        + "\tvoid B() { }\n"
        + "\n"
        + "\t/*\n"
        + "\t\t  t6\n"
        + "\t */\n"
        + "\tvoid D() { }\n"
        + "\n"
        + "\t/*\n"
        + "  t10 spaces only under a tab opener\n"
        + "\t*/\n"
        + "\tvoid E() { }\n"
        + "\n"
        + "\t/*\n"
        + "\t\tmixed\n"
        + "\t */\n"
        + "\tvoid F() { }\n"
        + "\n"
        + "\tvoid G() { /* c\n"
        + "\t  d */\n"
        + "\t\tM();\n"
        + "\t}\n"
        + "\n"
        + "\t/*\n"
        + "\t * starred\n"
        + "\t */\n"
        + "\tvoid H() {\n"
        + "\t\tM(); /* trailing\n"
        + "\t\t      * starred\n"
        + "\t\t      */\n"
        + "\t}\n"
        + "\n"
        + "\tvoid M() { }\n"
        + "}\n";

    const string FrozenSource =
        "class C {\n"
        + "      /*\n"
        + "         plain moves\n"
        + "       */\n"
        + "    int F;\n"
        + "\n"
        + "    /*  \n"
        + "     * frozen   \n"
        + "     */\n"
        + "    int G;\n"
        + "\n"
        + "    void M() {\n"
        + "          M(); /* trailing\n"
        + "                  * frozen\n"
        + "                  */\n"
        + "    }\n"
        + "}\n";

    const string FrozenOracle =
        "class C {\n"
        + "    /*\n"
        + "       plain moves\n"
        + "     */\n"
        + "    int F;\n"
        + "\n"
        + "    /*  \n"
        + "     * frozen   \n"
        + "     */\n"
        + "    int G;\n"
        + "\n"
        + "    void M() {\n"
        + "        M(); /* trailing\n"
        + "                  * frozen\n"
        + "                  */\n"
        + "    }\n"
        + "}\n";

    /// <summary>
    ///     Left two, right two, and an opener moving eight left over lines written two and seven columns
    ///     in: those two land at column 0 and lose their offsets, the empty line stays empty, and the line
    ///     written sixteen in keeps its distance from the opener.
    /// </summary>
    [Fact]
    public void AnUnstarredComment_MovesAsAUnit_AndClampsAtColumnZero() => Agrees(PlainSource, PlainOracle);

    /// <summary>
    ///     ⚠ A <c>/** … */</c> moves with its line even when every continuation line is starred: a ragged
    ///     one stays ragged. A ragged <c>/* … */</c> beside it is aligned instead, by
    ///     <c>skala_align_multiline_comments</c> (SK-DIV-0033).
    /// </summary>
    [Fact]
    public void ASlashStarStarComment_MovesAsAUnit_EvenStarred_WhileAStarredSlashStarAligns() =>
        Agrees(DocSource, DocOracle);

    /// <summary>
    ///     ⚠ The continuation moves by what the <em>line</em> the comment starts on moved, never by what
    ///     its <c>/*</c> moved. <c>if (true)</c> / <c>{ /* brace</c> joins the brace onto a line indented
    ///     the same, so <c>x */</c> stays at 13 although the <c>/*</c> moved ten right; <c>1 + /* expr</c>
    ///     broken before the <c>+</c> moves <c>expr2</c> by the two the new line is indented further, not by the eight its
    ///     <c>/*</c> moved left.
    /// </summary>
    [Fact]
    public void TheShiftIsTheLines_NotTheOpeners() => Agrees(LineSource, LineOracle);

    /// <summary>
    ///     SK-DIV-0193: every line of a block comment loses its trailing whitespace, and a whitespace-only
    ///     line becomes empty — the first comment here does not move at all. The last one is not starred
    ///     (a whitespace-only line disqualifies it) and is otherwise left where it was written.
    /// </summary>
    [Fact]
    public void EveryLineLosesItsTrailingWhitespace_MovedOrNot() => Agrees(TrimSource, TrimOracle);

    /// <summary>
    ///     ⚠ <c>indent_style = tab</c>: the line's own indentation, then the tabs the author's run led with
    ///     past the old line's indentation, then spaces — never past the target. A line that kept its
    ///     column but went from four spaces to a tab is re-spelled (<c>G</c>), and a starred comment's
    ///     asterisks follow the line's tabs too, own-line and trailing.
    /// </summary>
    [Fact]
    public void UnderTabs_TheRunIsTheLinesIndentThenTheAuthorsLeadingTabsThenSpaces() =>
        Agrees(TabsSource, TabsOracle, "indent_style", "tab");

    /// <summary>
    ///     At <c>skala_align_multiline_comments = false</c> a starred comment is the oracle's to freeze: its
    ///     body and its trailing whitespace are left byte for byte. The unstarred one moves as at
    ///     <c>true</c>. (The oracle freezes a starred comment's opener too, which Skala does not —
    ///     SK-DIV-0033's fact 1 — so none here is written off its column.)
    /// </summary>
    [Fact]
    public void AtFalse_AStarredCommentsBodyIsNotTouched_AndAnUnstarredOneStillMoves() =>
        Agrees(FrozenSource, FrozenOracle, "skala_align_multiline_comments", "false");

    static void Agrees(string source, string expected, string? key = null, string? value = null) {
        var once = Format(source, key, value);
        Assert.Equal(expected, once);
        Assert.Equal(once, Format(once, key, value));
    }

    static string Format(string source, string? key, string? value) {
        var options = OptionResolver.Resolve(
                Path.Combine(Rikarin.Skala.Testing.Corpus.RepositoryRoot, "Test.cs"),
                key is null ? [] : [new KeyValuePair<string, string>(key, value!)]
            )
            .Options;
        return CSharpFormatter.Format("Test.cs", SourceText.From(source), options)
            .Formatted.Replace("\r\n", "\n", StringComparison.Ordinal);
    }
}
