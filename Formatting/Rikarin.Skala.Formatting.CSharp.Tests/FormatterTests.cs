using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Core.Diagnostics;
using Rikarin.Skala.Options;

namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>Formats a string with the repository's own configuration, which is the Rider export.</summary>
/// <remarks>
/// ⚠ The repository's <c>.editorconfig</c>, resolved for real, and not
/// <c>FormattingOptions.Defaults</c>. The two were interchangeable while every registry default was
/// the export's own value; milestone 3 derived ReSharper's actual defaults from the oracle, and they
/// are Allman-braced with <c>wrap_if_long</c> chains — a different formatter, correctly. These tests
/// are about the export's behaviour, so they have to say so.
/// </remarks>
public static class Format {
    public static PhaseOneOptions Options { get; } = new(
        Rikarin.Skala.Core.Configuration.OptionResolver
            .Resolve(Path.Combine(Rikarin.Skala.Testing.Corpus.RepositoryRoot, "Test.cs"))
            .Options
    );

    public static FormatResult Run(string source, string path = "Test.cs") =>
        CSharpFormatter.Format(path, SourceText.From(source), Options);

    public static string Text(string source) => Run(source).Formatted;

    /// <summary>
    /// How many owner-dependent groups the document put outside their owner.
    /// </summary>
    /// <remarks>
    /// ⚠ Must be zero. It is the invariant that makes docs/plan/04's "second pass" a walk order
    /// rather than an iteration to a fixed point, and the fitter counts violations rather than
    /// hiding them behind a guess.
    /// </remarks>
    public static int OwnerUnresolved(string source) {
        var text = SourceText.From(source);
        var tree = CSharpSyntaxTree.ParseText(text, CSharpFormatter.ParseOptions);
        var built = CSharpDocumentBuilder.Build("Test.cs", text, tree.GetRoot(), Options);
        return LayoutWriter.Write(built.Document, Options.MaxLineLength, "    ", "\n").OwnerUnresolved;
    }
}

public sealed class SpacingTests {
    [Theory]
    // ⚠ The body is on its own line since milestone 3, and these two rows say so on purpose. Every
    // statement gets a line of its own — `csharp_preserve_single_line_blocks = true` is in the export
    // and ReSharper ignores it (BreakPlan.PlanOnePerLine) — so a one-line method with a body in it
    // is three lines, and asserting the spacing on one line was asserting the wrong shape.
    [InlineData("class C { void M(int a,int b) { M(a,b); } }", "void M(int a, int b) {\n        M(a, b);\n    }")]
    [InlineData("class C { void M() { M ( ) ; } }", "void M() {\n        M();\n    }")]
    [InlineData("class C { int M(int a) => ( int ) a ; }", "int M(int a) => (int)a;")]
    [InlineData("class C { void M(bool b) { if(b){} } }", "if (b) { }")]
    [InlineData("class C { int M(int a) => a<1?2:3; }", "int M(int a) => a < 1 ? 2 : 3;")]
    [InlineData(
        "class C { System.Collections.Generic.List<int> M() => new System.Collections.Generic.List < int > (); }",
        "new System.Collections.Generic.List<int>();"
    )]
    [InlineData("class C { public int X{get;set;} }", "public int X { get; set; }")]
    [InlineData("class C { void M() { for(var i=0;i<2;i++){} } }", "for (var i = 0; i < 2; i++) { }")]
    [InlineData("class C { int M(int[] xs) => xs [ 0 ] ; }", "int M(int[] xs) => xs[0];")]
    [InlineData("class C { void M() { var a = new [ ] { 1 , 2 } ; } }", "var a = new[] { 1, 2 };")]
    [InlineData("class C<T> where T:struct { }", "class C<T> where T : struct { }")]
    [InlineData("class C { void M(System.Func<int,int> f) => M(x=>x+1); }", "M(x => x + 1);")]
    public void Format_ResolvesTheGap(string source, string expected) =>
        Assert.Contains(expected, Format.Text(source), StringComparison.Ordinal);

    [Fact]
    public void ExtraSpaces_CollapseEvenWhereTheAuthorAlignedThem() {
        // ⚠ disable_space_changes_before_trailing_comment = false, so hand-built trailing-comment
        // alignment IS collapsed. It is correct, it is what Rider does, and it is the change people
        // most often mistake for a bug on a first run (docs/plan/05 § "Spaces").
        var formatted = Format.Text(
            """
            class C {
                int _a;      // the first
                int _bb;     // the second
            }
            """
        );

        Assert.Contains("int _a; // the first", formatted, StringComparison.Ordinal);
        Assert.Contains("int _bb; // the second", formatted, StringComparison.Ordinal);
    }

    [Fact]
    public void CommentText_IsLeftAlone() {
        // space_before_trailing_comment_text = false leaves `//x` alone.
        Assert.Contains("M(); //x", Format.Text("class C { void M() { M();    //x\n } }"), StringComparison.Ordinal);
    }
}

public sealed class IndentationTests {
    [Fact]
    public void NestedLoops_StayFlush() {
        // indent_nested_for_stmt = false — a real transformation, and one of the few places the
        // formatter removes indentation the author wrote.
        var formatted = Format.Text(
            """
            class C {
                void M() {
                    for (var i = 0; i < 2; i++)
                        for (var j = 0; j < 2; j++) {
                            M();
                        }
                }
            }
            """
        );

        Assert.Contains(
            "        for (var i = 0; i < 2; i++)\n        for (var j = 0; j < 2; j++) {",
            formatted,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public void ClosingDelimiter_TakesTheIndentOfTheLineThatOpenedIt() {
        var formatted = Format.Text(
            """
            class C {
                void M(int a, int b) {
                    M(
                            a,
                            b
                            );
                }
            }
            """
        );

        Assert.Contains("        M(\n            a,\n            b\n        );", formatted, StringComparison.Ordinal);
    }

    [Fact]
    public void ContinuationLines_TakeOneLevel_RegardlessOfChainDepth() {
        var formatted = Format.Text(
            """
            class C {
                int M(int a, int b) {
                    return a
                + b
                        + a;
                }
            }
            """
        );

        Assert.Contains("        return a\n            + b\n            + a;", formatted, StringComparison.Ordinal);
    }

    [Fact]
    public void PreprocessorIf_GoesToColumnZero_AndRegionsIndentWithTheCode() {
        var formatted = Format.Text(
            """
            class C {
                void M() {
                    #if DEBUG
                    M();
                    #endif
                }

                #region Things
                void N() {
                }
                #endregion
            }
            """
        );

        // ⚠ DEBUG is not defined for `skala format` — there is no project to ask until milestone 5
        // — so the branch is disabled text and is frozen. What phase 1 owns here is the directives'
        // own column, and `#region` indenting with the code.
        Assert.Contains("\n#if DEBUG\n", formatted, StringComparison.Ordinal);
        Assert.Contains("\n#endif\n", formatted, StringComparison.Ordinal);
        Assert.Contains("\n    #region Things\n", formatted, StringComparison.Ordinal);
    }

    [Fact]
    public void SwitchLabels_IndentFromTheSwitch_AndStatementsFromTheLabel() {
        var formatted = Format.Text(
            """
            class C {
                void M(int a) {
                    switch (a) {
                    case 1:
                    M(a);
                    break;
                    }
                }
            }
            """
        );

        Assert.Contains(
            "        switch (a) {\n            case 1:\n                M(a);\n                break;\n        }",
            formatted,
            StringComparison.Ordinal
        );
    }
}

public sealed class BraceTests {
    [Fact]
    public void OpenBrace_JoinsThePreviousLine() {
        var formatted = Format.Text(
            """
            class C
            {
                void M()
                {
                }
            }
            """
        );

        Assert.Contains("class C {", formatted, StringComparison.Ordinal);
        Assert.Contains("void M() { }", formatted, StringComparison.Ordinal);
    }

    [Fact]
    public void ElseCatchFinally_JoinTheClosingBrace() {
        var formatted = Format.Text(
            """
            class C {
                void M(bool b) {
                    try {
                        M(b);
                    }
                    catch (System.Exception) {
                    }
                    finally {
                    }

                    if (b) {
                    }
                    else {
                    }
                }
            }
            """
        );

        Assert.Contains("} catch (System.Exception) { }", formatted, StringComparison.Ordinal);
        Assert.Contains("} finally { }", formatted, StringComparison.Ordinal);
        Assert.Contains("} else { }", formatted, StringComparison.Ordinal);
    }

    [Fact]
    public void ABraceIsNeverJoinedAcrossAComment() {
        // ⚠ Joining `// why` with the brace below it would put the brace inside the comment.
        var formatted = Format.Text(
            """
            class C {
                void M()
                // why
                {
                    M();
                }
            }
            """
        );

        Assert.Contains("// why\n", formatted, StringComparison.Ordinal);
        Assert.DoesNotContain("// why {", formatted, StringComparison.Ordinal);
    }
}

public sealed class BlankLineTests {
    [Fact]
    public void Caps_TruncateButNeverExtend() {
        var formatted = Format.Text("class C {\n    int _a;\n\n\n\n\n    int _b;\n}\n");
        Assert.Contains("int _a;\n\n\n    int _b;", formatted, StringComparison.Ordinal);
    }

    [Fact]
    public void Removals_BeatRequirements() {
        // remove_blank_lines_near_braces_in_declarations wins over around_field.
        var formatted = Format.Text("class C {\n\n    int _a;\n\n}\n");
        Assert.Contains("class C {\n    int _a;\n}", formatted, StringComparison.Ordinal);
    }

    [Fact]
    public void AdjacentSingleLineFields_StayTight() {
        var formatted = Format.Text("class C {\n    int _a;\n    int _b;\n}\n");
        Assert.Contains("int _a;\n    int _b;", formatted, StringComparison.Ordinal);
    }

    [Fact]
    public void AMethodGetsOne_EvenWhereTheAuthorWroteNone() {
        var formatted = Format.Text("class C {\n    int _a;\n    void M() {\n        M();\n    }\n}\n");
        Assert.Contains("int _a;\n\n    void M()", formatted, StringComparison.Ordinal);
    }

    [Fact]
    public void StickComment_PutsTheBlankAboveTheComment() {
        var formatted = Format.Text("class C {\n    int _a;\n    // about M\n    void M() {\n        M();\n    }\n}\n");
        Assert.Contains("int _a;\n\n    // about M\n    void M()", formatted, StringComparison.Ordinal);
    }

    [Fact]
    public void AFileScopedNamespace_GetsOneBlankAfterIt() =>
        Assert.Contains(
            "namespace N;\n\nclass C",
            Format.Text("namespace N;\nclass C {\n}\n"),
            StringComparison.Ordinal
        );

    [Fact]
    public void TheUsingList_GetsOneBlankAfterIt() =>
        Assert.Contains(
            "using System;\n\nclass C",
            Format.Text("using System;\nclass C {\n}\n"),
            StringComparison.Ordinal
        );
}

public sealed class TriviaTests {
    [Fact]
    public void DisabledText_IsNeverReindented() {
        const string source = """
                              class C {
                              #if NEVER
                                              int   _a  ;
                                int _b;
                              #endif
                                  int _c;
                              }
                              """;

        var formatted = Format.Text(source);
        Assert.Contains("                int   _a  ;\n  int _b;", formatted, StringComparison.Ordinal);
    }

    [Fact]
    public void AFormatterOffSpan_IsCopiedByteForByte() {
        const string source = """
                              class C {
                                  // @formatter:off
                                  var  matrix = new[,] {
                                      { 1 , 0 },
                                      { 0 , 1 }
                                  };
                                  // @formatter:on
                                  void   M( )   {
                                  }
                              }
                              """;

        var formatted = Format.Text(source);
        Assert.Contains(
            "    var  matrix = new[,] {\n        { 1 , 0 },\n        { 0 , 1 }\n    };",
            formatted,
            StringComparison.Ordinal
        );
        Assert.Contains("void M() { }", formatted, StringComparison.Ordinal);
    }

    [Fact]
    public void ARawStringLiteral_MovesAsOnePiece() {
        // ⚠ `indent_raw_literal_string = align` since milestone 3, and this test asserted the
        // opposite until then: SK-DIV-0003 recorded raw literals as emitted verbatim because
        // re-indenting one wrongly changes what the program prints. The transformation that is safe
        // is a *uniform shift* — every interior line and the closing delimiter by the same number of
        // columns — because C# strips the closing delimiter's own prefix from every line, so the
        // stripped result is identical. The interior's own relative indentation is preserved: the
        // `x` line stays two columns further in than the `{  }` line, and the braces inside the
        // string are untouched.
        const string source = "class C {\n    const string A = \"\"\"\n        {  }\n          x\n        \"\"\";\n}\n";
        Assert.Contains(
            "\"\"\"\n                     {  }\n                       x\n                     \"\"\"",
            Format.Text(source),
            StringComparison.Ordinal
        );
    }

    [Fact]
    public void ADocComment_IsReindentedLineByLine() {
        const string source = """
                              class C {
                                      /// <summary>
                                      /// Docs.
                                      /// </summary>
                                  void M() {
                                  }
                              }
                              """;

        Assert.Equal(
            "class C {\n    /// <summary>\n    /// Docs.\n    /// </summary>\n    void M() { }\n}\n",
            Format.Text(source)
        );
    }
}

public sealed class SafetyTests {
    [Fact]
    public void AFileThatDoesNotParse_IsLeftByteIdentical() {
        // ⚠ ADR-003, the single most important safety property in the tool.
        const string source = "class C { void M( {\n";
        var result = Format.Run(source);

        Assert.Equal(FormatOutcome.NotParseable, result.Outcome);
        Assert.Empty(result.Edits);
        Assert.Equal(source, result.Formatted);
        Assert.Contains(result.Diagnostics, static d => d.Id == FormatDiagnosticIds.NotParseable);
    }

    [Fact]
    public void AMemberWhoseBracesAreSplitAcrossAnIf_IsEmittedVerbatimWithSK9011() {
        const string source = """
                              class C {
                              #if DEBUG
                                  void M(int a) {
                              #else
                                  void M(int a, int b) {
                              #endif
                                      M(a);
                                  }
                              }
                              """;

        var result = Format.Run(source);
        Assert.Contains(result.Diagnostics, static d => d.Id == FormatDiagnosticIds.UnbalancedPreprocessor);
    }

    [Fact]
    public void GeneratedCode_IsSkipped() {
        var byName = Format.Run("class   C{}", "Thing.g.cs");
        Assert.Equal(FormatOutcome.Generated, byName.Outcome);

        var byHeader = Format.Run("// <auto-generated />\nclass   C{}", "Thing.cs");
        Assert.Equal(FormatOutcome.Generated, byHeader.Outcome);
    }

    [Fact]
    public void TokenEquivalence_NoticesALostToken() {
        var before = SourceText.From("class C { void M() { M(); } }");
        var after = SourceText.From("class C { void M() { } }");
        Assert.NotNull(TokenEquivalence.Compare(before, after, CSharpFormatter.ParseOptions));
    }

    [Fact]
    public void TokenEquivalence_AcceptsAReindentedDocComment() {
        var before = SourceText.From("class C {\n        /// <summary>A.</summary>\n    void M() { }\n}");
        var after = SourceText.From("class C {\n    /// <summary>A.</summary>\n    void M() { }\n}");
        Assert.Null(TokenEquivalence.Compare(before, after, CSharpFormatter.ParseOptions));
    }

    [Fact]
    public void TokenEquivalence_NoticesADroppedComment() {
        var before = SourceText.From("class C { // note\n }");
        var after = SourceText.From("class C { }");
        Assert.NotNull(TokenEquivalence.Compare(before, after, CSharpFormatter.ParseOptions));
    }

    [Fact]
    public void TokenEquivalence_NoticesAChangedDisabledBlock() {
        var before = SourceText.From("class C {\n#if NEVER\n    int   a;\n#endif\n}");
        var after = SourceText.From("class C {\n#if NEVER\n    int a;\n#endif\n}");
        Assert.NotNull(TokenEquivalence.Compare(before, after, CSharpFormatter.ParseOptions));
    }

    [Fact]
    public void ACrashArtefact_IsAReadyMadeRegressionTest() {
        var directory = Directory.CreateTempSubdirectory("skala-crash-test-");
        try {
            var written = CrashArtifacts.Write(
                directory.FullName,
                "Thing.cs",
                "class A { }",
                "class B { }",
                Format.Options
            );
            Assert.NotNull(written);
            Assert.Equal("class A { }", File.ReadAllText(Path.Combine(written, "input.cs")));
            Assert.Equal("class B { }", File.ReadAllText(Path.Combine(written, "output.cs")));
            Assert.Contains(
                "max_line_length = 120",
                File.ReadAllText(Path.Combine(written, "config.snapshot")),
                StringComparison.Ordinal
            );
        } finally {
            directory.Delete(recursive: true);
        }
    }
}

public sealed class EditTests {
    [Fact]
    public void AlreadyFormattedCode_ProducesNoEdits() {
        const string source = """
                              class C {
                                  int _a;

                                  void M() {
                                      M();
                                  }
                              }

                              """;

        Assert.Empty(Format.Run(source).Edits);
    }

    [Fact]
    public void TheEditsCoverOnlyTheRegionsThatDiffer() {
        const string source = "class C {\n    void M() {\n        M( );\n    }\n}\n";
        var result = Format.Run(source);
        var edit = Assert.Single(result.Edits);
        Assert.Equal(
            "M( )".IndexOf('(', StringComparison.Ordinal) + source.IndexOf("M( )", StringComparison.Ordinal) + 1,
            edit.Span.Start
        );
        Assert.Equal(string.Empty, edit.NewText);
    }

    [Fact]
    public void Range_FiltersEditsAfterTheWholeFileWasFitted() {
        const string source = "class C {\n    void M( ) {\n    }\n\n    void N( ) {\n    }\n}\n";
        var result = Format.Run(source);
        var second = source.IndexOf("void N", StringComparison.Ordinal);

        var restricted = EditEmitter.Restrict(result.Edits, SourceSpan.FromBounds(second, source.Length));
        Assert.NotEmpty(restricted);
        Assert.All(restricted, edit => Assert.True(edit.Span.Start >= second));
        Assert.True(restricted.Count < result.Edits.Length);
    }

    [Fact]
    public void FinalNewline_IsAddedEvenThoughTheGenericKeySaysOtherwise() {
        // ⚠ resharper_csharp_insert_final_newline = true beats [*] insert_final_newline = false by
        // language specificity (docs/plan/03, hazard 3).
        Assert.EndsWith("}\n", Format.Text("class C {\n}"), StringComparison.Ordinal);
    }

    [Fact]
    public void CrlfInput_StaysCrlf() {
        // enforce_line_ending_style = false: mixed endings are preserved, not normalised.
        var formatted = Format.Text("class C {\r\n    void M( ) {\r\n    }\r\n}\r\n");
        Assert.DoesNotContain(
            "\n\n",
            formatted.Replace("\r\n", "\n", StringComparison.Ordinal).Replace("\n\n", "|", StringComparison.Ordinal),
            StringComparison.Ordinal
        );
        Assert.Contains("\r\n", formatted, StringComparison.Ordinal);
    }
}

/// <summary>
/// Phase 2: which gaps may hold a break, and which side of a token it lands on.
/// </summary>
/// <remarks>
/// ⚠ Every expectation here was read off <c>jb cleanupcode</c>, not off an option name. Where the
/// name and the behaviour disagree the behaviour wins, and the two disagree more often than the
/// documentation admits.
/// </remarks>
public sealed class BreakPositionTests {
    [Fact]
    public void ABreakOnTheWrongSideOfABinaryOperator_IsRemoved() {
        // wrap_before_binary_opsign = true: the gap before the operator is the break point and the
        // gap after it is not, so one of these two survives and the other does not.
        var formatted = Format.Text(
            """
            class C {
                void M() {
                    var afterTheSign = first +
                        second;
                    var beforeTheSign = first
                        + second;
                }
            }
            """
        );

        Assert.Contains("var afterTheSign = first + second;", formatted, StringComparison.Ordinal);
        Assert.Contains("var beforeTheSign = first\n            + second;", formatted, StringComparison.Ordinal);
    }

    [Fact]
    public void ABreakOnTheWrongSideOfATernaryOperator_IsRemoved() {
        var formatted = Format.Text(
            """
            class C {
                void M() {
                    var after = condition ?
                        whenTrue :
                        whenFalse;
                }
            }
            """
        );

        Assert.Contains("var after = condition ? whenTrue : whenFalse;", formatted, StringComparison.Ordinal);
    }

    [Fact]
    public void AnInvocationBrokenBetweenItsArguments_IsChoppedAtEveryPoint() {
        // ⚠ chop_if_long is "chop if long OR multiline": one break between two arguments puts every
        // argument on its own line and the closing parenthesis on one of its own.
        var formatted = Format.Text(
            """
            class C {
                void M() {
                    Foo(first,
                        second);
                }
            }
            """
        );

        Assert.Contains(
            "Foo(\n            first,\n            second\n        );",
            formatted,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public void AnInvocationBrokenOnlyAtItsParenthesis_IsRejoined() {
        // keep_existing_invocation_parens_arrangement = false, and there is no break between items
        // for keep_user_linebreaks to protect. The asymmetry between this and the test above is the
        // whole content of docs/plan/05's four-way table.
        var formatted = Format.Text(
            """
            class C {
                void M() {
                    Foo(
                        first);
                }
            }
            """
        );

        Assert.Contains("Foo(first);", formatted, StringComparison.Ordinal);
    }

    [Fact]
    public void ADeclarationBrokenOnlyAtItsParenthesis_IsKept() {
        // …and the same shape on a declaration is kept, because
        // keep_existing_declaration_parens_arrangement is true where the invocation one is false.
        var formatted = Format.Text(
            """
            class C {
                void M(
                    int first) { }
            }
            """
        );

        Assert.Contains("void M(\n        int first\n    ) { }", formatted, StringComparison.Ordinal);
    }

    [Fact]
    public void AnEnumBody_PutsOneMemberPerLine_Always() {
        var formatted = Format.Text("enum E { First, Second, Third }");
        Assert.Contains("enum E {\n    First,\n    Second,\n    Third\n}", formatted, StringComparison.Ordinal);
    }

    [Fact]
    public void ASwitchExpression_ChopsEveryArm_Always() {
        var formatted = Format.Text(
            """
            class C {
                int M(int v) => v switch { 1 => 10, _ => 0 };
            }
            """
        );

        Assert.Contains(
            "v switch {\n            1 => 10,\n            _ => 0\n        }",
            formatted,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public void AnAttributeSection_NeverSharesALineWithWhatFollowsIt() {
        var formatted = Format.Text(
            """
            class C {
                [First] [Second] void M() { }
            }
            """
        );

        Assert.Contains("[First]\n    [Second]\n    void M() { }", formatted, StringComparison.Ordinal);
    }

    [Fact]
    public void AShortExpressionBodiedMember_IsRejoined_AndALongOneIsNot() {
        // place_expr_property_on_single_line = if_owner_is_single_line, both halves of it.
        var formatted = Format.Text(
            """
            class C {
                int Short =>
                    1;
                int TheLongOne => Helper.Compute(firstArgumentName, secondArgumentName, thirdArgumentName, fourthArgumentName, fifth);
            }
            """
        );

        Assert.Contains("int Short => 1;", formatted, StringComparison.Ordinal);
        Assert.Contains("int TheLongOne =>\n        Helper.Compute(", formatted, StringComparison.Ordinal);
    }

    [Fact]
    public void ACallWhoseOnlyArgumentIsALambda_KeepsItsOpeningParenthesis_ButNotItsClosingOne() {
        // ⚠ place_single_method_argument_lambda_on_same_line governs the opening parenthesis only,
        // which is not what the name suggests and is what the oracle does.
        var formatted = Format.Text(
            """
            class C {
                void M() {
                    Run(() => {
                        FirstStatement();
                        SecondStatement();
                    });
                }
            }
            """
        );

        Assert.Contains("Run(() => {", formatted, StringComparison.Ordinal);

        // ⚠ Only the presence of the break is asserted. Where the closing parenthesis and the
        // lambda's closing brace land relative to each other is an indentation question that the
        // oracle answers differently for a lambda inside a broken call than for one inside a
        // collection expression, and neither shape is milestone 2's to settle.
        Assert.DoesNotContain("});", formatted, StringComparison.Ordinal);
    }

    [Fact]
    public void NoDocument_EverPutsAnOwnerDependentGroupOutsideItsOwner() {
        // ⚠ The invariant that makes the second pass of docs/plan/04 § "The fitting algorithm" a
        // walk order rather than an iteration. If a front end ever breaks it, the only monotone
        // answer is Broken and the layout is a guess; this counts the guesses and there are none.
        foreach (var source in new[] {
                "class C { int P => 1; }", "class C { void M(bool f) { if (f) G(); } }",
                "class C { void M(int v) { switch (v) { case 1: G(); break; } } }",
                "class C { int P { get => 1; set => _f = value; } }"
            }) {
            Assert.Equal(0, Format.OwnerUnresolved(source));
        }
    }
}

/// <summary>Phase 4, and the shape of it that is evidence-led rather than schedule-led.</summary>
public sealed class XmlDocTests {
    [Fact]
    public void AMalformedDocComment_IsReportedAtHint_AndLeftExactlyAsWritten() {
        // ⚠ docs/plan/05 § "Phase 4": "A doc comment that is not well-formed XML — extremely common
        // in real code — must be left exactly as it is and reported at hint (SK0003), not 'fixed'."
        const string source = "class C {\n    /// <summary>Not closed <b>at all.</summary>\n    void M() { }\n}\n";
        var result = Format.Run(source);

        Assert.Contains(
            result.Diagnostics,
            d => d.Id == FormatDiagnosticIds.MalformedXmlDoc && d.Severity == SkalaSeverity.Hidden
        );

        Assert.Contains("/// <summary>Not closed <b>at all.</summary>", result.Formatted, StringComparison.Ordinal);
    }

    [Fact]
    public void TwoSiblingParamTags_AreWellFormed() {
        // ⚠ A doc comment is a fragment, not a document. Two <param> siblings are ordinary, and
        // judging them by document rules would report most of the corpus.
        const string source = "class C {\n    /// <param name=\"a\">A.</param>\n    /// <param name=\"b\">B.</param>\n    void M(int a, int b) { }\n}\n";

        Assert.DoesNotContain(
            Format.Run(source).Diagnostics,
            static d => d.Id == FormatDiagnosticIds.MalformedXmlDoc
        );
    }

    [Fact]
    public void AWellFormedDocComment_IsNotRewrappedEither() {
        // ⚠ SK-DIV-0006. `jb cleanupcode` does not format documentation comments at all — not the
        // missing space after `///`, not a 128-column summary, not two tags on one line — so Skala
        // does not either. A formatter that re-wrapped them would diverge from the oracle on every
        // doc comment in the corpus, with no oracle to check itself against while doing it.
        const string source = "class C {\n    ///<summary>A summary line that runs a long way past one hundred and twenty columns in total, easily.</summary>\n    void M() { }\n}\n";

        Assert.Contains("///<summary>", Format.Text(source), StringComparison.Ordinal);
    }

    [Fact]
    public void ALineNothingCouldBreak_IsLeftLongAndReportedAtHint() {
        // docs/plan/04 § "The fitting algorithm": "Unfittable lines are left long. […] never emits a
        // diagnostic for it by default (SK0002 at hint for the audit)."
        var source = "class C {\n    const string S = \"" + new string('x', 200) + "\";\n}\n";
        var result = Format.Run(source);

        Assert.Contains(
            result.Diagnostics,
            static d => d.Id == FormatDiagnosticIds.LineTooLong && d.Severity == SkalaSeverity.Hidden
        );

        Assert.Contains(new string('x', 200), result.Formatted, StringComparison.Ordinal);
    }
}
