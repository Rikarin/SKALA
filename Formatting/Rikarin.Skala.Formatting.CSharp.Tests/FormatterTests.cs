using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Core.Diagnostics;
using Rikarin.Skala.Options;

namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>Formats a string with the repository's own configuration, which is the Rider export.</summary>
/// <remarks>
///     ⚠ The repository's <c>.editorconfig</c>, resolved for real, and not
///     <c>FormattingOptions.Defaults</c>. The two were interchangeable while every registry default was
///     the export's own value; milestone 3 derived ReSharper's actual defaults from the oracle, and they
///     are Allman-braced with <c>wrap_if_long</c> chains — a different formatter, correctly. These tests
///     are about the export's behaviour, so they have to say so.
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
    ///     How many owner-dependent groups the document put outside their owner.
    /// </summary>
    /// <remarks>
    ///     ⚠ Must be zero. It is the invariant that makes docs/plan/04's "second pass" a walk order
    ///     rather than an iteration to a fixed point, and the fitter counts violations rather than
    ///     hiding them behind a guess.
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

    /// <summary>
    ///     ⚠ SK-FUZZ-0010. A member's trailing comment shares its line, so it is part of its width.
    /// </summary>
    /// <remarks>
    ///     "Single line" is a property of the <em>output</em>, and the width that decides it has to be
    ///     the width the fitter will see. <c>OutputWidth</c> measured the gaps *between* a member's
    ///     tokens and there is no gap after the last one, so the trailing comment was missing from the
    ///     comparison: the second member below is 108 columns on its own and 123 with its comment. It
    ///     was therefore called single-line, <c>blank_lines_around_single_line_invocable = 0</c>
    ///     declined the blank line above it — and then the fitter, which does count the comment,
    ///     chopped the member across three lines. The second pass read a multi-line member, asked
    ///     <c>blank_lines_around_invocable = 1</c> instead, and inserted the blank line the first pass
    ///     had refused.
    ///     <para>
    ///         ⚠ The widths above are the whole test, so the assertion below pins them: a member that
    ///         fits without its comment and does not fit with it. Both are asserted, because a fixture
    ///         that drifted to the wrong side of the margin would pass this test while asserting nothing.
    ///     </para>
    ///     <para>
    ///         ⚠ Asserted as idempotency rather than as "there is a blank line here". Which of the two
    ///         answers is right is a question for the oracle; that the two passes must give the *same* one
    ///         is not.
    ///     </para>
    /// </remarks>
    [Fact]
    public void AMemberWideOnlyBecauseOfItsTrailingComment_ConvergesInOnePass() {
        const string wide =
            "    internal IEnumerable<TimeSpan> M97<T95, T96>(out decimal p98) => [(static x => 3_000_000L), items[1..]];";

        const string comment = " // over the margin";
        Assert.True(wide.Length <= Format.Options.MaxLineLength, $"the member alone is {wide.Length} columns");
        Assert.True(
            wide.Length + comment.Length > Format.Options.MaxLineLength,
            $"the member and its comment are {wide.Length + comment.Length} columns"
        );

        var source = "class T {\n    public static byte M21() {\n    }" + comment + "\n" + wide + comment + "\n}\n";
        var once = Format.Text(source);
        var twice = Format.Text(once);
        Assert.Equal(once, twice);
    }
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
        // ⚠ Two things happen and both are wanted. The comment is re-indented with the member it
        // documents, which is what this test has always measured; and the sub-formatter keeps the
        // author's line break — `keep_user_linebreaks` is true in the export, so a break the author
        // wrote is a break — while indenting the text inside the element, which is
        // `indent_text = one_indent`.
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
            "class C {\n    /// <summary>\n    ///     Docs.\n    /// </summary>\n    void M() { }\n}\n",
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

    /// <summary>
    ///     ⚠ SK-FUZZ-0009. C# ends a line at a lone <c>\r</c>, and so must the formatter's own count.
    /// </summary>
    /// <remarks>
    ///     <c>CSharpDocumentBuilder.CountNewLines</c> counted <c>'\n'</c> and nothing else, so the gap
    ///     <c>}   &lt;CR&gt;#endif</c> reported zero newlines; <c>EmitGap</c> then reasoned about the
    ///     brace and the directive as though they shared a line and joined them, a <c>#</c> that is not
    ///     first on its line is not a directive to Roslyn, and the <c>#endif</c> became a *skipped
    ///     token*. The safety net caught it — <c>SK9099</c>, nothing written — which means the visible
    ///     symptom was not corruption but a file the tool could not format at all.
    ///     <para>
    ///         ⚠ The assertion is <c>Formatted</c>, not the absence of a diagnostic. A future regression
    ///         that produced a *different* wrong output would still trip <c>SK9099</c> and would still be
    ///         wrong; what this pins is that the directive survives as a directive.
    ///     </para>
    /// </remarks>
    [Fact]
    public void ADirectiveAfterALoneCarriageReturn_StaysADirective() {
        const string source = "class C { // fuzz\r\n#if true\n} \r#endif";
        var result = Format.Run(source);

        Assert.NotEqual(FormatOutcome.VerificationFailed, result.Outcome);
        Assert.DoesNotContain(result.Diagnostics, static d => d.Id == FormatDiagnosticIds.TokenStreamChanged);

        // The `#endif` must still begin a line of its own in the output, whichever terminator ends
        // the line above it.
        var lines = result.Formatted.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split('\n');

        Assert.Contains(lines, static line => line.TrimStart().StartsWith("#endif", StringComparison.Ordinal));
    }

    /// <summary>
    ///     ⚠ SK-FUZZ-0016. The inactive branch of a <c>#if</c> is opaque, and a directive Roslyn still
    ///     reports inside it is part of that branch.
    /// </summary>
    /// <remarks>
    ///     Roslyn does <em>not</em> fold directives into <c>DisabledTextTrivia</c>: a <c>#region</c>, a
    ///     <c>#pragma</c>, a nested <c>#if</c> in a branch that is not compiled all arrive as ordinary
    ///     directive trivia, so the piece stream could not tell them from directives that govern real
    ///     code. <c>blank_lines_around_region</c> then wrote a blank line between <c>#if HAVE_ASYNC</c>
    ///     and the <c>#region</c> below it — and re-parsed, that line is a <c>DisabledTextTrivia</c>
    ///     that was not there before, so the safety net aborted the file with <c>SK9099</c> and it could
    ///     not be formatted at all under the empty symbol set. <see cref="Piece.Inactive" /> is the flag
    ///     that says otherwise, and <c>EmitGap</c> copies the gap byte-for-byte on either side of it.
    ///     <para>
    ///         ⚠ The assertion is <c>Formatted</c> and the output, not the absence of a diagnostic: a
    ///         regression that produced a <em>different</em> wrong output would still trip <c>SK9099</c>
    ///         and would still be wrong. What this pins is that nothing is written into the branch.
    ///     </para>
    /// </remarks>
    [Fact]
    public void ARegionInsideAnInactiveBranch_TakesNoBlankLines() {
        const string source = "#if HAVE_ASYNC\n#region fuzz\n#endregion\n#endif\n";
        var result = Format.Run(source);

        Assert.DoesNotContain(result.Diagnostics, static d => d.Id == FormatDiagnosticIds.TokenStreamChanged);
        Assert.Equal(FormatOutcome.Formatted, result.Outcome);
        Assert.Equal(source, result.Formatted);
    }

    /// <summary>
    ///     ⚠ SK-FUZZ-0016 again, in the shape a second seed found it: the inactive arm of an
    ///     <c>#if</c> that has a live <c>#else</c>, so there is real code on both sides of the branch.
    /// </summary>
    /// <remarks>
    ///     ⚠ And the control, in the same test: a <c>#region</c> that governs compiled code still gets
    ///     <c>blank_lines_around_region</c>. The fix is "the inactive branch is opaque", not "regions
    ///     stopped taking blank lines".
    ///     <para>
    ///         ⚠ The outcome is asserted before the text, and it has to be: a file the safety net refuses
    ///         comes back as its own input, so every assertion about what is <em>not</em> in the output
    ///         passes trivially on the broken formatter. Asserted the other way round this test was green
    ///         against the defect it exists for.
    ///     </para>
    /// </remarks>
    [Fact]
    public void ARegionInTheInactiveArm_KeepsItsGapsWhileALiveRegionKeepsItsBlankLines() {
        var inactive = Format.Run(
            "class C {\n#if DEBUG\n#region fuzz\n#endregion\n  int _a;\n#else\n  int _b;\n#endif\n}\n"
        );

        Assert.Equal(FormatOutcome.Formatted, inactive.Outcome);
        Assert.DoesNotContain("#if DEBUG\n\n", inactive.Formatted, StringComparison.Ordinal);
        Assert.DoesNotContain("#endregion\n\n", inactive.Formatted, StringComparison.Ordinal);

        var live = Format.Run(
            "class C {\n    int _a;\n    #region fuzz\n    int _b;\n    #endregion\n    int _c;\n}\n"
        );

        Assert.Equal(FormatOutcome.Formatted, live.Outcome);
        Assert.Contains("int _a;\n\n    #region fuzz\n", live.Formatted, StringComparison.Ordinal);
        Assert.Contains("    #endregion\n\n    int _c;\n", live.Formatted, StringComparison.Ordinal);
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
///     Phase 2: which gaps may hold a break, and which side of a token it lands on.
/// </summary>
/// <remarks>
///     ⚠ Every expectation here was read off <c>jb cleanupcode</c>, not off an option name. Where the
///     name and the behaviour disagree the behaviour wins, and the two disagree more often than the
///     documentation admits.
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
        const string source =
            "class C {\n    /// <param name=\"a\">A.</param>\n    /// <param name=\"b\">B.</param>\n    void M(int a, int b) { }\n}\n";

        Assert.DoesNotContain(
            Format.Run(source).Diagnostics,
            static d => d.Id == FormatDiagnosticIds.MalformedXmlDoc
        );
    }

    [Fact]
    public void AWellFormedDocComment_IsRewrapped() {
        // ⚠ SK-DIV-0006, and this assertion is the inverse of what it used to be. The missing space
        // after `///` and the 128-column summary are both fixed, because Rider fixes them: the
        // pinned oracle profile does not run `CSharpFormatDocComments` and Rider's own Full Cleanup
        // does. Matching the profile here would mean diverging from the editor on every doc comment
        // in every repository, which is the larger of the two divergences.
        //
        // ⚠ The subject had to grow, and the growth is a measurement rather than a convenience. It
        // used to be one 115-column line, and `jb cleanupcode` with `CSharpFormatDocComments`
        // enabled returns that line **byte-identical** — no wrap, and no marker space either,
        // because the marker's space arrives with a rebuild and there was nothing to rebuild. The
        // budget is measured from after the `///` (SK-DIV-0019), so 115 file columns is well inside
        // it. A summary that genuinely overflows is what this test is about, so it is one now.
        const string source =
            "class C {\n    ///<summary>A summary line that runs a long way past one hundred and twenty columns in total, easily, and then keeps going for another forty columns so that it certainly cannot fit.</summary>\n    void M() { }\n}\n";
        var formatted = Format.Text(source);

        Assert.DoesNotContain("///<summary>", formatted, StringComparison.Ordinal);

        // ⚠ Measured from the character after the `///`, not from column 0, which is SK-DIV-0019
        // and is what `XmlDocColumnTests` carries the probe for. The oracle's own answer to this
        // very input is a 122-column file line, so a `<= 120` on the whole line asserts a shape
        // `jb cleanupcode` does not produce. A code line is still measured whole.
        Assert.All(
            formatted.Split('\n'),
            line => {
                var slashes = line.IndexOf("///", StringComparison.Ordinal);
                var measured = slashes < 0 ? TextWidth.Measure(line) : TextWidth.Measure(line[(slashes + 3)..]);
                Assert.True(measured <= 120, $"'{line}' measures {measured} columns.");
            }
        );
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

/// <summary>
///     The two <c>use_continuous_indent_inside_*</c> keys, at the multiplier that unmasks them.
/// </summary>
/// <remarks>
///     ⚠ SK-DIV-0085. The key-flip sweep cannot ask about either key, because the export sets
///     <c>resharper_continuous_indent_multiplier = 1</c> and at that multiplier a continuation level and
///     an indent width are the same number — so the oracle is flat at both values and any Skala answer
///     that is not also flat reads <c>SPURIOUS</c>. The oracle's real answer, measured at multiplier 2,
///     is pinned here instead, one assertion per column the oracle produced.
///     <para>
///         ⚠ <c>false</c> means <em>one indent width</em>, not the absence of an indent. Skala suppressed
///         the scope outright, which is a level short at every multiplier and invisible at 1.
///     </para>
/// </remarks>
public sealed class ContinuousIndentInsideTests {
    static string FormatWith(string source, params (string Key, string Value)[] overrides) {
        var options = new PhaseOneOptions(
            Rikarin.Skala.Core.Configuration.OptionResolver.Resolve(
                    Path.Combine(Rikarin.Skala.Testing.Corpus.RepositoryRoot, "Test.cs"),
                    [.. overrides.Select(static o => new KeyValuePair<string, string>(o.Key, o.Value))]
                )
                .Options
        );

        return CSharpFormatter.Format("Test.cs", SourceText.From(source), options).Formatted;
    }

    const string Call = """
                        class C {
                            void M(int a, int b) {
                                M(
                                    a,
                                    b);
                            }
                        }
                        """;

    /// <summary>
    ///     ⚠ The negative control the rest of this class rests on: at the export's own multiplier the
    ///     key decides nothing, in Skala exactly as in the oracle.
    /// </summary>
    [Theory]
    [InlineData("true")]
    [InlineData("false")]
    public void AtTheExportsMultiplierOfOne_TheParenKeyDecidesNothing(string value) {
        var formatted = FormatWith(Call, ("resharper_csharp_use_continuous_indent_inside_parens", value));
        Assert.Contains("\n            a,\n", formatted, StringComparison.Ordinal);
    }

    /// <summary>
    ///     ⚠ Measured: at multiplier 2 the arguments land on <c>8 + 2×4</c> at <c>true</c> and
    ///     <c>8 + 1×4</c> at <c>false</c>. The <c>false</c> column is the finding — it is an indent,
    ///     not the lack of one.
    /// </summary>
    [Theory]
    [InlineData("true", "\n                a,\n")]
    [InlineData("false", "\n            a,\n")]
    public void AtMultiplierTwo_InsideParens_IsOneLevelOrOneWidth(string value, string expected) {
        var formatted = FormatWith(
            Call,
            ("resharper_continuous_indent_multiplier", "2"),
            ("resharper_csharp_use_continuous_indent_inside_parens", value)
        );
        Assert.Contains(expected, formatted, StringComparison.Ordinal);
    }

    /// <summary>
    ///     ⚠ The initializer half, and only its <c>false</c> arm is asserted. The <c>true</c> arm comes
    ///     from an <c>IndentKind.Block</c> scope, which is one indent width whatever the multiplier says
    ///     — a <c>continuous_indent_multiplier</c> defect on braced initializers that SK-DIV-0085 records
    ///     and deliberately does not fix. Asserting it here would pin the defect.
    /// </summary>
    [Fact]
    public void AtMultiplierTwo_InsideInitializerBraces_False_IsOneWidthRatherThanNone() {
        // ⚠ Five elements is over `max_initializer_elements_on_line = 4`, so this one is chopped
        // whatever its width — which is what makes the indent inside the braces observable at all.
        // The same construct `constructs/indentation/…_initializer_braces.cs` uses, for the same reason.
        const string source = """
                              class C {
                                  System.Collections.Generic.List<int> N() =>
                                      new System.Collections.Generic.List<int> { 1, 2, 3, 4, 5 };
                              }
                              """;

        var formatted = FormatWith(
            source,
            ("resharper_continuous_indent_multiplier", "2"),
            ("resharper_csharp_use_continuous_indent_inside_initializer_braces", "false")
        );

        // The `new` lands on one continuation level of 2×4 from the member at 4; the elements take one
        // indent width from it rather than landing on it, which is the whole of the finding.
        Assert.Contains("\n            new System.Collections.Generic.List<int> {\n", formatted, StringComparison.Ordinal);
        Assert.Contains("\n                1,\n", formatted, StringComparison.Ordinal);
    }
}
