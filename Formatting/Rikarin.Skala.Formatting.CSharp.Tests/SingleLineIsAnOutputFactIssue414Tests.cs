using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Core.Configuration;

namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     Issue #414, SK-DIV-0171 and SK-DIV-0172: whether a member or a statement is single-line is a fact
///     about the output, so a member the writer joins takes <c>blank_lines_around_single_line_*</c> and one
///     the fitter breaks takes <c>blank_lines_around_*</c> — and a plain comment glued between two members
///     makes the oracle count both as multi-line for the gap above them. Every expected string is
///     <c>jb cleanupcode</c>'s own output for the input, and each test asserts the second pass too.
/// </summary>
/// <remarks>
///     The committed fixtures are <c>constructs/blank-lines/a-member-the-writer-joins.cs</c> and
///     <c>constructs/blank-lines/a-comment-glued-between-two-members.cs</c>.
/// </remarks>
public sealed class SingleLineIsAnOutputFactIssue414Tests {
    /// <summary>The accessor key most of these cases set, named once (SK7083).</summary>
    const string AroundAccessor = "skala_blank_lines_around_accessor";

    /// <summary>The oracle's answer under the repository's export with <paramref name="overrides" /> on top.</summary>
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

    /// <summary>The issue's first case: <c>get {</c>↵<c>return _n;</c>↵<c>}</c> is joined, and is then a single-line accessor.</summary>
    [Fact]
    public void AnAccessorTheWriterJoins_TakesTheSingleLineAccessorKey() =>
        Agrees(
            """
            class C {
                int _n;

                public int P {
                    get {
                        return _n;
                    }
                    set { _n = value; }
                }
            }
            """,
            """
            class C {
                int _n;

                public int P {
                    get { return _n; }
                    set { _n = value; }
                }
            }
            """,
            (AroundAccessor, "1")
        );

    /// <summary>
    ///     At <c>keep_blank_lines_in_declarations = 0</c> the source-read blank was a non-idempotency: pass two removed
    ///     it.
    /// </summary>
    [Fact]
    public void AJoinedAccessor_IsIdempotentWhenTheCapIsBelowTheMultiLineValue() =>
        Agrees(
            """
            class C {
                int _n;

                public int P {
                    get {
                        return _n;
                    }
                    set { _n = value; }
                }
            }
            """,
            """
            class C {
                int _n;

                public int P {
                    get { return _n; }
                    set { _n = value; }
                }
            }
            """,
            (AroundAccessor, "1"),
            ("skala_keep_blank_lines_in_declarations", "0")
        );

    /// <summary>The issue's second case, under the reflow-keep preservation corner.</summary>
    [Fact]
    public void ALocalFunctionWhoseArgumentsReJoin_TakesTheSingleLineLocalMethodKey() =>
        Agrees(
            """
            using System;

            class C {
                int _n;

                void M() {
                    _n = 1;
                    void LocalWraps() { _n = Math.Max(
                        _n,
                        1); }
                    _n = 2;
                }
            }
            """,
            """
            using System;

            class C {
                int _n;

                void M() {
                    _n = 1;
                    void LocalWraps() { _n = Math.Max(_n, 1); }
                    _n = 2;
                }
            }
            """,
            ("skala_keep_user_linebreaks", "false"),
            ("skala_keep_user_wrapping", "false"),
            ("skala_keep_existing_attribute_arrangement", "true"),
            ("skala_keep_existing_declaration_block_arrangement", "true"),
            ("skala_keep_existing_declaration_parens_arrangement", "true"),
            ("skala_keep_existing_embedded_arrangement", "true"),
            ("skala_keep_existing_embedded_block_arrangement", "true"),
            ("skala_keep_existing_enum_arrangement", "true"),
            ("skala_keep_existing_expr_member_arrangement", "true"),
            ("skala_keep_existing_invocation_parens_arrangement", "true"),
            ("skala_keep_existing_list_patterns_arrangement", "true"),
            ("skala_keep_existing_primary_constructor_declaration_parens_arrangement", "true"),
            ("skala_keep_existing_property_patterns_arrangement", "true"),
            ("skala_keep_existing_switch_expression_arrangement", "true"),
            ("skala_keep_existing_lambda_and_anonymous_function_parens_arrangement", "true")
        );

    /// <summary>
    ///     <c>keep_existing_expr_member_arrangement = false</c> joins <c>int B() =&gt;</c>↵<c>2;</c>, under the plain
    ///     export.
    /// </summary>
    [Fact]
    public void AnExpressionBodyTheExportJoins_IsSingleLine() =>
        Agrees(
            """
            class C {
                int A() => 1;
                int B() =>
                    2;
                int D() => 3;
            }
            """,
            """
            class C {
                int A() => 1;
                int B() => 2;
                int D() => 3;
            }
            """
        );

    /// <summary><c>place_simple_initializer_on_single_line</c> joins a field's initializer, under the plain export.</summary>
    [Fact]
    public void AFieldInitializerTheExportJoins_IsSingleLine() =>
        Agrees(
            """
            class C {
                int _a = 1;
                int[] _b = new[] {
                    1, 2, 3
                };
                int _c = 3;
            }
            """,
            """
            class C {
                int _a = 1;
                int[] _b = new[] { 1, 2, 3 };
                int _c = 3;
            }
            """
        );

    /// <summary>A local function's expression body, joined under the plain export.</summary>
    [Fact]
    public void ALocalFunctionArrowTheExportJoins_IsSingleLine() =>
        Agrees(
            """
            class C {
                void M() {
                    int A() => 1;
                    int B() =>
                        2;
                    int D() => 3;
                }
            }
            """,
            """
            class C {
                void M() {
                    int A() => 1;
                    int B() => 2;
                    int D() => 3;
                }
            }
            """
        );

    /// <summary>An accessor's expression body and a one-statement block, each written over lines and joined.</summary>
    [Fact]
    public void AccessorArrowsAndBlocksTheWriterJoins_AreSingleLine() =>
        Agrees(
            """
            class C {
                int _n;

                int P {
                    get =>
                        _n;
                    set => _n = value;
                }

                int Q {
                    get {
                        return _n;
                    }
                    set { _n = value; }
                }

                int R {
                    get { return _n; }
                    set { _n = value; }
                }
            }
            """,
            """
            class C {
                int _n;

                int P {
                    get => _n;
                    set => _n = value;
                }

                int Q {
                    get { return _n; }
                    set { _n = value; }
                }

                int R {
                    get { return _n; }
                    set { _n = value; }
                }
            }
            """,
            (AroundAccessor, "1")
        );

    /// <summary><c>class B {</c>↵<c>}</c> comes back <c>class B { }</c> and takes <c>blank_lines_around_single_line_type</c>.</summary>
    [Fact]
    public void EmptyTypesTheWriterJoins_AreSingleLine() =>
        Agrees(
            """
            namespace N {
                class A { }
                class B {
                }
                enum E {
                    X, Y
                }
                class D { }
                interface I {
                }
                class F { }
            }
            """,
            """
            namespace N {
                class A { }
                class B { }

                enum E {
                    X,
                    Y
                }

                class D { }
                interface I { }
                class F { }
            }
            """,
            ("skala_blank_lines_around_single_line_type", "0")
        );

    /// <summary>The reverse direction: one source line, broken after the arrow because it overflows.</summary>
    [Fact]
    public void AOneLineMemberTheFitterBreaks_IsMultiLine() =>
        Agrees(
            """
            class C {
                int A() => 1;
                int B() => System.Math.Max(111111111111111111, 222222222222222222) + System.Math.Max(333333333333333, 4444444444444444);
                int D() => 3;
            }
            """,
            """
            class C {
                int A() => 1;

                int B() =>
                    System.Math.Max(111111111111111111, 222222222222222222) + System.Math.Max(333333333333333, 4444444444444444);

                int D() => 3;
            }
            """
        );

    /// <summary>A lambda block and an initializer joined, an arrow kept broken, under both multi-line statement keys.</summary>
    [Fact]
    public void StatementsTheWriterJoins_AreNotMultiLineStatements() =>
        Agrees(
            """
            using System;

            class C {
                void M(Action a) {
                    M(null);
                    M(() => {
                        M(null);
                    });
                    M(null);
                    var z = new[] {
                        1, 2, 3
                    };
                    M(null);
                    Action f = () =>
                        M(null);
                    M(null);
                }
            }
            """,
            """
            using System;

            class C {
                void M(Action a) {
                    M(null);
                    M(() => { M(null); });
                    M(null);
                    var z = new[] { 1, 2, 3 };
                    M(null);

                    Action f = () =>
                        M(null);

                    M(null);
                }
            }
            """,
            ("skala_blank_lines_after_multiline_statements", "1"),
            ("skala_blank_lines_before_multiline_statements", "1")
        );

    [Fact]
    public void AMemberTheWriterJoinsUnderAComment() =>
        Agrees(
            """
            class C {
                int _a;
                // c
                void M() {
                }
                int _b;
            }
            """,
            """
            class C {
                int _a;

                // c
                void M() { }
                int _b;
            }
            """
        );

    [Fact]
    public void AOneLineMethodUnderAComment() =>
        Agrees(
            """
            class C {
                int _a;
                // c
                void M() { }
                int _b;
            }
            """,
            """
            class C {
                int _a;

                // c
                void M() { }
                int _b;
            }
            """
        );

    [Fact]
    public void AJoinedArrowUnderAComment() =>
        Agrees(
            """
            class C {
                int _a;
                // c
                int P() =>
                    1;
                int _b;
            }
            """,
            """
            class C {
                int _a;

                // c
                int P() => 1;
                int _b;
            }
            """
        );

    [Fact]
    public void AOneLineArrowUnderAComment() =>
        Agrees(
            """
            class C {
                int _a;
                // c
                int P() => 1;
                int _b;
            }
            """,
            """
            class C {
                int _a;

                // c
                int P() => 1;
                int _b;
            }
            """
        );

    [Fact]
    public void AFieldUnderACommentAfterTwoFields() =>
        Agrees(
            """
            class C {
                int _a;
                int _c;
                // c
                int _d;
                int _b;
            }
            """,
            """
            class C {
                int _a;

                int _c;

                // c
                int _d;
                int _b;
            }
            """
        );

    [Fact]
    public void AJoinedInitializerUnderACommentAfterTwoFields() =>
        Agrees(
            """
            class C {
                int _a;
                int _c;
                // c
                int[] _d = new[] {
                    1, 2 };
                int _b;
            }
            """,
            """
            class C {
                int _a;

                int _c;

                // c
                int[] _d = new[] { 1, 2 };
                int _b;
            }
            """
        );

    [Fact]
    public void AFieldUnderACommentAfterAJoinedMethod() =>
        Agrees(
            """
            class C {
                int _a;
                void M() {
                }
                // c
                int _b;
            }
            """,
            """
            class C {
                int _a;

                void M() { }

                // c
                int _b;
            }
            """
        );

    [Fact]
    public void TheGapUnderACommentedMemberIsSingleLine() =>
        Agrees(
            """
            class C {
                int _a;
                // c
                int _b;
                int _d;
            }
            """,
            """
            class C {
                int _a;

                // c
                int _b;
                int _d;
            }
            """
        );

    [Fact]
    public void ACommentOnTheMembersOwnLineGluesNothing() =>
        Agrees(
            """
            class C {
                int _a; // trailing
                int _b;
                int _d;
            }
            """,
            """
            class C {
                int _a; // trailing
                int _b;
                int _d;
            }
            """
        );

    [Fact]
    public void ACommentGluedToTheClosingBrace() =>
        Agrees(
            """
            class C {
                int _a;
                int _b;
                // c
            }
            """,
            """
            class C {
                int _a;

                int _b;
                // c
            }
            """
        );

    [Fact]
    public void TwoCommentLinesGlueLikeOne() =>
        Agrees(
            """
            class C {
                int _x;
                int _a;
                // one
                // two
                int _b;
                int _d;
            }
            """,
            """
            class C {
                int _x;

                int _a;

                // one
                // two
                int _b;
                int _d;
            }
            """
        );

    [Fact]
    public void ABlankUnderTheCommentGluesNothing() =>
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
            """
        );

    [Fact]
    public void MethodsAroundAComment() =>
        Agrees(
            """
            class C {
                void X() { }
                void A() { }
                // c
                void B() { }
                void D() { }
            }
            """,
            """
            class C {
                void X() { }

                void A() { }

                // c
                void B() { }
                void D() { }
            }
            """
        );

    [Fact]
    public void ABlockCommentGluesLikeALineComment() =>
        Agrees(
            """
            class C {
                int _x;
                int _a;
                /* c */
                int _b;
                int _d;
            }
            """,
            """
            class C {
                int _x;

                int _a;

                /* c */
                int _b;
                int _d;
            }
            """
        );

    [Fact]
    public void LocalFunctionsAroundAComment() =>
        Agrees(
            """
            class C {
                void M() {
                    int L() => 1;
                    // c
                    int K() => 2;
                    int J() => 3;
                }
            }
            """,
            """
            class C {
                void M() {
                    int L() => 1;

                    // c
                    int K() => 2;
                    int J() => 3;
                }
            }
            """
        );

    [Fact]
    public void TheMemberUnderTheCommentIsMultiLineForTheGapAboveIt() =>
        Agrees(
            """
            class C {
                int _a;
                // c
                void B() { }
                void D() { }
            }
            """,
            """
            class C {
                int _a;

                // c
                void B() { }
                void D() { }
            }
            """,
            ("skala_blank_lines_around_field", "0")
        );

    [Fact]
    public void ABlankAboveTheCommentGluesNothingToTheMemberAboveIt() =>
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
            ("skala_blank_lines_around_field", "0")
        );

    [Fact]
    public void ACommentBeforeARegionGluesNothing() =>
        Agrees(
            """
            class C {
                int _x;
                int _a;
                // c
                #region R
                int _b;
                #endregion
            }
            """,
            """
            class C {
                int _x;
                int _a;
                // c

                #region R

                int _b;

                #endregion
            }
            """,
            ("skala_blank_lines_around_field", "0")
        );

    [Fact]
    public void AJoinedMethodUnderACommentAtFieldZero() =>
        Agrees(
            """
            class C {
                int _x;
                int _a;
                // c
                void M() {
                }
            }
            """,
            """
            class C {
                int _x;
                int _a;

                // c
                void M() { }
            }
            """,
            ("skala_blank_lines_around_field", "0")
        );

    [Fact]
    public void ADocCommentGluesOnlyToItsOwnMember() =>
        Agrees(
            """
            class C {
                int _x;
                int _a;
                /// <summary>doc</summary>
                int _b;
                int _d;
            }
            """,
            """
            class C {
                int _x;
                int _a;

                /// <summary>doc</summary>
                int _b;

                int _d;
            }
            """,
            (AroundAccessor, "1"),
            ("skala_blank_lines_around_single_line_type", "0")
        );

    [Fact]
    public void AccessorsAroundAComment() =>
        Agrees(
            """
            class C {
                int P {
                    get => 1;
                    // c
                    set { }
                }

                int Q {
                    get => 1;
                    set { }
                    // c
                }
            }
            """,
            """
            class C {
                int P {
                    get => 1;

                    // c
                    set { }
                }

                int Q {
                    get => 1;

                    set { }
                    // c
                }
            }
            """,
            (AroundAccessor, "1"),
            ("skala_blank_lines_around_single_line_type", "0")
        );

    [Fact]
    public void TypesAroundAComment() =>
        Agrees(
            """
            namespace N {
                class X { }
                class A { }
                // c
                class B { }
                class D { }
            }
            """,
            """
            namespace N {
                class X { }

                class A { }

                // c
                class B { }
                class D { }
            }
            """,
            (AroundAccessor, "1"),
            ("skala_blank_lines_around_single_line_type", "0")
        );

    [Fact]
    public void ThreeLocalFunctionsAroundAComment() =>
        Agrees(
            """
            class C {
                void M() {
                    int X() => 0;
                    int L() => 1;
                    // c
                    int K() => 2;
                    int J() => 3;
                }
            }
            """,
            """
            class C {
                void M() {
                    int X() => 0;

                    int L() => 1;

                    // c
                    int K() => 2;
                    int J() => 3;
                }
            }
            """,
            (AroundAccessor, "1"),
            ("skala_blank_lines_around_single_line_type", "0")
        );
}
