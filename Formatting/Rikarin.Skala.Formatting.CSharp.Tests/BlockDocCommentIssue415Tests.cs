using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     Issue #415, SK-DIV-0180: a <c>/** … */</c> comment lost its <c>/**</c> opener, so every file
///     holding one was refused with <c>SK9099</c>. ⚠ Not only inside an expression, which is where the
///     issue found it: before a member, before a statement and at the end of a line too — the corpus holds
///     no <c>/**</c> at all, which is how it went unseen. Every expected string is <c>jb cleanupcode</c>'s
///     own output for the input.
/// </summary>
public sealed class BlockDocCommentIssue415Tests {
    /// <summary>
    ///     ⚠ The Roslyn fact the defect stood on, pinned so that the fix's premise is asserted rather than
    ///     remembered: under <c>DocumentationMode.Parse</c> a <c>/**</c> is documentation wherever it
    ///     stands, and a structured trivia's <see cref="SyntaxTrivia.Span" /> excludes the <c>/**</c>,
    ///     which is the leading exterior trivia of the structure's first token.
    /// </summary>
    [Fact]
    public void UnderParse_ASlashStarStarInAnArgumentList_IsDocumentation_AndItsSpanLacksTheOpener() {
        const string Source = "class C { void M() { M(1 /** e09 */, 2); } }";
        var trivia = Trivia(Source, DocumentationMode.Parse);

        Assert.Equal(SyntaxKind.MultiLineDocumentationCommentTrivia, trivia.Kind());
        Assert.Equal(" e09 */", Source[trivia.Span.Start..trivia.Span.End]);
        Assert.Equal("/** e09 */", trivia.ToFullString());
        Assert.Equal(" e09 */", trivia.ToString());
        var full = SourcePieces.TextOf(trivia);
        Assert.Equal("/** e09 */", Source[full.Start..full.End]);
    }

    /// <summary>
    ///     ⚠ Under <c>DocumentationMode.None</c> the same text is an ordinary block comment, which is why the
    ///     defect needs <c>Parse</c> — and the formatter always parses with it (#388).
    /// </summary>
    [Fact]
    public void UnderNone_ASlashStarStar_IsAnOrdinaryBlockComment() {
        var trivia = Trivia("class C { void M() { M(1 /** e09 */, 2); } }", DocumentationMode.None);

        Assert.Equal(SyntaxKind.MultiLineCommentTrivia, trivia.Kind());
        Assert.Equal(DocumentationMode.Parse, CSharpFormatter.ParseOptions.DocumentationMode);
    }

    public static TheoryData<string> Shapes() =>
        new() {
            "class C {\n    void M(int x, int y) { }\n\n    void T() {\n        M(1 /** e09 */, 2);\n    }\n}\n",
            "class C {\n    void M(int x, int y) { }\n\n    void T() {\n        M(1, /** e10 */2);\n    }\n}\n",
            "class C {\n    void M(int x, int y) { }\n\n    void T() {\n        M(1 /** e09 */ , 2);\n    }\n}\n",
            "class C {\n    void M(int x, int y) { }\n\n    void T() {\n        M(1 /**\n             multi */, 2);\n    }\n}\n",
            "class C {\n    /** <summary>Doc.</summary> */\n    void M() { }\n}\n",
            "class C {\n    /**\n     * <summary>Doc.</summary>\n     */\n    void M() { }\n}\n",
            "class C {\n      /**\n         * <summary>Odd.</summary>\n       */\n    void M() { }\n}\n",
            "class C {\n    void M() {\n        /** before a statement */\n        M();\n    }\n}\n",
            "class C {\n    void M() {\n        M(); /** end of line */\n    }\n}\n",
            "class C {\n    /** single */ int F;\n\n    void M() {\n        /** s1 */ M();\n    }\n}\n",
            "class C {\n    void M() {\n        var a = new[] { 1, /** g */ 2 };\n        var b = 1 /** h */ + 2;\n        if (true) /** i */ { }\n    }\n}\n",
            "class C {\n    void M() {\n        var a = 1 + /**/ 2 + /***/ 3 + /** **/ 4;\n    }\n}\n",
            "/** top */ class C { }\n",
            "class C { void M() { M(/** j */); } }\n"
        };

    /// <summary>
    ///     The property the issue is about, over every position measured: the file is formatted, not
    ///     refused; the comment's text reaches the output byte for byte; the token stream is the input's;
    ///     and a second pass changes nothing.
    /// </summary>
    [Theory]
    [MemberData(nameof(Shapes))]
    public void ASlashStarStarComment_IsNeverRefused_AndKeepsEveryByte(string source) {
        var result = Format.Run(source);

        Assert.NotEqual(FormatOutcome.VerificationFailed, result.Outcome);
        Assert.Equal(FormatOutcome.Formatted, result.Outcome);
        Assert.Null(
            TokenEquivalence.Compare(
                SourceText.From(source),
                SourceText.From(result.Formatted),
                CSharpFormatter.ParseOptions
            )
        );

        foreach (var comment in Comments(source)) {
            Assert.Contains(comment.Split('\n')[0], result.Formatted, StringComparison.Ordinal);
        }

        Assert.Equal(result.Formatted, Format.Text(result.Formatted));
    }

    /// <summary>The oracle breaks after an own-line <c>/** … */</c> before a member and a statement.</summary>
    [Fact]
    public void AnOwnLineSlashStarStar_IsFollowedByABreak_LikeABlockComment() =>
        Oracle.Agrees(
            """
            class C {
                /** single */ public int F;

                /** single */ public class N { }

                public void E() {
                    /** s1 */ E();
                    E(); /** trailing */
                    E();
                }
            }
            """,
            """
            class C {
                /** single */
                public int F;

                /** single */
                public class N { }

                public void E() {
                    /** s1 */
                    E();
                    E(); /** trailing */
                    E();
                }
            }
            """
        );

    /// <summary>
    ///     #409's break point after a block comment holds for a <c>/** … */</c> too, under
    ///     <c>SkalaFormatOnly</c> and <c>SkalaDocComments</c> alike.
    /// </summary>
    [Fact]
    public void ABreakPointAfterASlashStarStar_BreaksAfterTheComment() =>
        Oracle.Agrees(
            """
            class T {
                void A() {
                    var a = new D(name175: nameof(value), /** f */  name176: Cast<ValueTask<Dictionary<string, Guid?>>, int>($"n={items[0]} and {source?.Value}", 12345));
                }

                void G() {
                    Compute(alphaArgumentValue, /** f */ betaArgumentValue, gammaArgumentValue, /** g */ deltaArgumentValue, epsilonArgumentValue);
                }

                void H() {
                    var x = alphaArgumentValue + betaArgumentValue + gammaArgumentValue /** h */ + deltaArgumentValue + epsilonArgument;
                }

                void J() {
                    M(alpha,
                        /** f */ beta);
                }
            }
            """,
            """
            class T {
                void A() {
                    var a = new D(
                        name175: nameof(value), /** f */
                        name176: Cast<ValueTask<Dictionary<string, Guid?>>, int>($"n={items[0]} and {source?.Value}", 12345)
                    );
                }

                void G() {
                    Compute(
                        alphaArgumentValue, /** f */
                        betaArgumentValue,
                        gammaArgumentValue, /** g */
                        deltaArgumentValue,
                        epsilonArgumentValue
                    );
                }

                void H() {
                    var x = alphaArgumentValue
                        + betaArgumentValue
                        + gammaArgumentValue /** h */
                        + deltaArgumentValue
                        + epsilonArgument;
                }

                void J() {
                    M(
                        alpha,
                        /** f */
                        beta
                    );
                }
            }
            """
        );

    /// <summary>The positions where the oracle leaves the comment exactly where it was.</summary>
    [Fact]
    public void WhereTheOracleLeavesIt_SoDoesSkala() =>
        Oracle.Agrees(
            """
            class C {
                /** <summary>Doc.</summary> */
                public void M(int x, int y) { }

                /**
                 * <summary>Doc.</summary>
                 */
                public void T() {
                    /** before a statement */
                    M(1, /** e10 */ 2);
                    var a = new[] { 1, /** g */ 2 };
                    var b = 1 /** h */ + 2;
                    var z = 1 + /**/ 2;
                    var w = 1 + /***/ 2;
                }
            }
            """,
            """
            class C {
                /** <summary>Doc.</summary> */
                public void M(int x, int y) { }

                /**
                 * <summary>Doc.</summary>
                 */
                public void T() {
                    /** before a statement */
                    M(1, /** e10 */ 2);
                    var a = new[] { 1, /** g */ 2 };
                    var b = 1 /** h */ + 2;
                    var z = 1 + /**/ 2;
                    var w = 1 + /***/ 2;
                }
            }
            """
        );

    /// <summary>
    ///     A formatter tag written as <c>/** … */</c> still opens and closes a region, and the region is
    ///     copied byte for byte — the tag reads the comment's whole text now that the text has its opener.
    /// </summary>
    [Fact]
    public void ASlashStarStarFormatterTag_ProtectsItsRegion() {
        const string Protected = """
                                     /** @formatter:off */
                                     void   A( )   {
                                     }
                                     /** @formatter:on */
                                 """;

        var formatted = Format.Text("class C {\n" + Protected + "\n    void   B( )   {\n    }\n}\n");

        Assert.Contains(Protected, formatted, StringComparison.Ordinal);
        Assert.Contains("void B() { }", formatted, StringComparison.Ordinal);
    }

    static SyntaxTrivia Trivia(string source, DocumentationMode mode) =>
        CSharpSyntaxTree.ParseText(
            SourceText.From(source),
            new CSharpParseOptions(LanguageVersion.Preview).WithDocumentationMode(mode)
        )
            .GetRoot()
            .DescendantTrivia()
            .Single(static t => t.ToFullString().StartsWith("/**", StringComparison.Ordinal));

    static IEnumerable<string> Comments(string source) {
        for (var i = source.IndexOf("/*", StringComparison.Ordinal);
             i >= 0;
             i = source.IndexOf("/*", i + 2, StringComparison.Ordinal)) {
            var end = source.IndexOf("*/", i + 2, StringComparison.Ordinal);
            yield return source[i..(end + 2)];
            i = end;
        }
    }
}
