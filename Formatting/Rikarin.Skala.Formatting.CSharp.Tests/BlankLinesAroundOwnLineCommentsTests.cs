using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Core.Configuration;

namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     Issues #494, #497, #498, #499 and #500: blank lines around an own-line comment, at the keys the committed
///     fixtures under <c>constructs/blank-lines/</c> cannot flip. Every expected string is
///     <c>jb cleanupcode</c> 2025.2.6's own output for the input under the repository's export with the
///     overrides named, and each test asserts the second pass too.
/// </summary>
public sealed class BlankLinesAroundOwnLineCommentsTests {
    static void Agrees(string source, string expected, params (string Key, string Value)[] overrides) {
        var options = new PhaseOneOptions(
            OptionResolver.Resolve(
                    Path.Combine(Rikarin.Skala.Testing.Corpus.RepositoryRoot, "Test.cs"),
                    [..overrides.Select(static o => new KeyValuePair<string, string>(o.Key, o.Value))]
                )
                .Options
        );

        var once = CSharpFormatter.Format("Test.cs", SourceText.From(source), options).Formatted;
        Assert.True(
            expected.TrimEnd('\n') == once.TrimEnd('\n'),
            $"Skala's output is not the oracle's.\n--- Skala ---\n{once}\n--- oracle ---\n{expected}"
        );

        var twice = CSharpFormatter.Format("Test.cs", SourceText.From(once), options).Formatted;
        Assert.True(once == twice, $"took two passes to settle:\n{once}\n--- pass two ---\n{twice}");
    }

    /// <summary>#497: an enum member is a field to <c>blank_lines_around_single_line_field</c>.</summary>
    [Fact]
    public void EnumMembers_PayTheSingleLineFieldKey() =>
        Agrees(
            """
            enum E {
                Alpha,

                Beta,
                Gamma
            }
            """,
            """
            enum E {
                Alpha,


                Beta,


                Gamma
            }
            """,
            ("skala_blank_lines_around_field", "0"),
            ("skala_blank_lines_around_single_line_field", "2")
        );

    /// <summary>
    ///     #497: and to <c>blank_lines_around_field</c> — a member with a comment glued above it is multi-line
    ///     for the gap above the comment, so at <c>0</c> nothing goes there and the single-line key's two go
    ///     under it.
    /// </summary>
    [Fact]
    public void AnEnumMemberUnderAComment_PaysTheMultiLineFieldKey() =>
        Agrees(
            """
            enum E {
                Alpha,
                // own
                Beta,
                Gamma
            }
            """,
            """
            enum E {
                Alpha,
                // own
                Beta,


                Gamma
            }
            """,
            ("skala_blank_lines_around_field", "0"),
            ("skala_blank_lines_around_single_line_field", "2")
        );

    /// <summary>
    ///     #497: at the export, a documented enum member takes <c>blank_lines_around_field</c> above its <c>///</c>.
    /// </summary>
    [Fact]
    public void ADocumentedEnumMember_TakesABlankLineAboveItsDocComment() =>
        Agrees(
            """
            enum E {
                Alpha,
                /// <summary>doc</summary>
                Beta
            }
            """,
            """
            enum E {
                Alpha,

                /// <summary>doc</summary>
                Beta
            }
            """
        );

    /// <summary>#498: a top-level local function pays the single-line local-method key on both sides.</summary>
    [Fact]
    public void ATopLevelLocalFunction_PaysTheSingleLineLocalMethodKey() =>
        Agrees(
            """
            System.Console.WriteLine();
            // s3
            System.Console.WriteLine();
            static void Local() { }
            System.Console.WriteLine();
            """,
            """
            System.Console.WriteLine();
            // s3
            System.Console.WriteLine();


            static void Local() { }


            System.Console.WriteLine();
            """,
            ("skala_blank_lines_around_local_method", "0"),
            ("skala_blank_lines_around_single_line_local_method", "2")
        );

    /// <summary>#498: one with a comment glued above it is multi-line for the gap above the comment.</summary>
    [Fact]
    public void ATopLevelLocalFunctionUnderAComment_IsMultiLineAboveTheComment() =>
        Agrees(
            """
            static void A() { }
            // s3
            static void Local() { }
            """,
            """
            static void A() { }


            // s3
            static void Local() { }
            """,
            ("skala_blank_lines_around_local_method", "0"),
            ("skala_blank_lines_around_single_line_local_method", "2")
        );

    /// <summary>
    ///     #499: neither the member's requirement nor <c>blank_lines_inside_namespace</c> is paid under a
    ///     comment on the namespace's <c>{</c> line; the inside key is still paid before the <c>}</c>.
    /// </summary>
    [Fact]
    public void UnderACommentOnTheBraceLine_NothingIsPaid() =>
        Agrees(
            "namespace N2 { /* b3 */ class X { } }",
            """
            namespace N2 { /* b3 */
                class X { }

            }
            """,
            ("skala_blank_lines_inside_namespace", "1"),
            ("skala_blank_lines_inside_type", "1"),
            ("skala_blank_lines_around_single_line_type", "2")
        );

    /// <summary>
    ///     #500: at <c>keep_blank_lines_in_code = 0</c> a <c>//</c> between two statements keeps one blank line
    ///     above it.
    /// </summary>
    [Fact]
    public void AtKeepZero_ALineCommentKeepsOneBlankLine() =>
        Agrees(
            """
            class C {
                void M() {
                    A();



                    // c
                    B();
                }
            }
            """,
            """
            class C {
                void M() {
                    A();

                    // c
                    B();
                }
            }
            """,
            ("skala_keep_blank_lines_in_code", "0")
        );

    /// <summary>#500: and a <c>/* */</c> keeps none.</summary>
    [Fact]
    public void AtKeepZero_ABlockCommentKeepsNone() =>
        Agrees(
            """
            class C {
                void M() {
                    A();



                    /* c */
                    B();
                }
            }
            """,
            """
            class C {
                void M() {
                    A();
                    /* c */
                    B();
                }
            }
            """,
            ("skala_keep_blank_lines_in_code", "0")
        );

    /// <summary>
    ///     #500: the blank above a comment between two statements is capped by <c>keep_blank_lines_in_code</c>,
    ///     not by <c>keep_blank_lines_in_declarations</c>, which a comment's "no token" used to answer.
    /// </summary>
    [Fact]
    public void BetweenStatements_TheCodeKeyCapsTheBlankAboveAComment() =>
        Agrees(
            """
            class C {
                void M() {
                    A();



                    // c
                    B();
                }
            }
            """,
            """
            class C {
                void M() {
                    A();


                    // c
                    B();
                }
            }
            """,
            ("skala_keep_blank_lines_in_declarations", "0")
        );

    /// <summary>
    ///     #500: between two accessors at <c>keep_blank_lines_in_declarations = 0</c>, a <c>//</c> keeps one.
    /// </summary>
    [Fact]
    public void BetweenAccessors_ALineCommentKeepsOne() =>
        Agrees(
            """
            class C {
                int P {
                    get;



                    // c
                    set;
                }
            }
            """,
            """
            class C {
                int P {
                    get;

                    // c
                    set;
                }
            }
            """,
            ("skala_keep_blank_lines_in_declarations", "0")
        );

    /// <summary>#500: and a <c>/* */</c> there keeps none.</summary>
    [Fact]
    public void BetweenAccessors_ABlockCommentKeepsNone() =>
        Agrees(
            """
            class C {
                int P {
                    get;



                    /* c */
                    set;
                }
            }
            """,
            """
            class C {
                int P {
                    get;
                    /* c */
                    set;
                }
            }
            """,
            ("skala_keep_blank_lines_in_declarations", "0")
        );

    /// <summary>
    ///     #494: a comment glued under a member with a blank under it hangs from that member, which is
    ///     multi-line for the gap under the comment — so at <c>keep_blank_lines_in_declarations = 0</c> the
    ///     blank stays, <c>blank_lines_around_field</c>'s. ⚠ Skala used to take it out on pass one, glue the
    ///     comment to <c>_b</c> and then write two blank lines the oracle does not, and a different two on pass
    ///     two.
    /// </summary>
    [Fact]
    public void ACommentWithABlankUnderIt_HangsFromTheMemberAbove() =>
        Agrees(
            """
            class C {
                int _x;
                int _a;
                // c

                int _b;
                int _d;
            }
            """,
            """
            class C {
                int _x;
                int _a;
                // c

                int _b;
                int _d;
            }
            """,
            ("skala_keep_blank_lines_in_declarations", "0")
        );

    /// <summary>#494: the member below's requirement is paid under such a comment, and nothing above it.</summary>
    [Fact]
    public void ACommentWithABlankUnderIt_TakesTheRequirementUnderIt() =>
        Agrees(
            """
            class C {
                int _a;
                // own

                int _b;
                int _c;
            }
            """,
            """
            class C {
                int _a;
                // own


                int _b;


                int _c;
            }
            """,
            ("skala_blank_lines_around_field", "0"),
            ("skala_blank_lines_around_single_line_field", "2")
        );

    /// <summary>#494: a two-line run is one comment, and the requirement goes under its last line.</summary>
    [Fact]
    public void ATwoLineRunWithABlankUnderIt_TakesTheRequirementUnderItsLastLine() =>
        Agrees(
            """
            class C {
                int _a;
                // one
                // two

                void M() {
                    A();
                }
            }
            """,
            """
            class C {
                int _a;
                // one
                // two


                void M() {
                    A();
                }
            }
            """,
            ("skala_keep_blank_lines_in_declarations", "1"),
            ("skala_blank_lines_around_invocable", "2")
        );

    /// <summary>
    ///     #494: of two runs a blank line apart, the first hangs from the member above and the second is the
    ///     member below's, so the requirement goes between them.
    /// </summary>
    [Fact]
    public void TwoRunsABlankApart_TakeTheRequirementBetweenThem() =>
        Agrees(
            """
            class C {
                int _a;
                // one

                // two
                void M() {
                    A();
                }
            }
            """,
            """
            class C {
                int _a;
                // one


                // two
                void M() {
                    A();
                }
            }
            """,
            ("skala_keep_blank_lines_in_declarations", "1"),
            ("skala_blank_lines_around_invocable", "2")
        );

    /// <summary>
    ///     #494 with #441: a member's requirement is not paid above a comment that the body's <c>}</c> follows,
    ///     glued or not.
    /// </summary>
    [Theory]
    [InlineData("    int _a;\n    int _b;\n    // last\n}", "    int _a;\n\n\n    int _b;\n    // last\n}")]
    [InlineData("    int _a;\n    // own\n\n}", "    int _a;\n    // own\n}")]
    public void AboveACommentBeforeTheClosingBrace_NothingIsPaid(string body, string expected) =>
        Agrees(
            "class C {\n" + body + "\n",
            "class C {\n" + expected + "\n",
            ("skala_blank_lines_around_field", "0"),
            ("skala_blank_lines_around_single_line_field", "2")
        );

    /// <summary>
    ///     #497 with #500: an enum member under a comment pays <c>blank_lines_around_field</c> at
    ///     <c>keep_blank_lines_in_declarations = 0</c>.
    /// </summary>
    [Fact]
    public void AnEnumMemberUnderAComment_KeepsItsBlankAtKeepZero() =>
        Agrees(
            """
            enum E {
                A,



                // c
                B
            }
            """,
            """
            enum E {
                A,

                // c
                B
            }
            """,
            ("skala_keep_blank_lines_in_declarations", "0")
        );
}
