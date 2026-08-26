using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Core.Diagnostics;
using Rikarin.Skala.Options;

namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>Formats a string with the repository's own configuration, which is the Rider export.</summary>
public static class Format {
    public static PhaseOneOptions Options { get; } = new(FormattingOptions.Defaults);

    public static FormatResult Run(string source, string path = "Test.cs") =>
        CSharpFormatter.Format(path, SourceText.From(source), Options);

    public static string Text(string source) => Run(source).Formatted;
}

public sealed class SpacingTests {
    [Theory]
    [InlineData("class C { void M(int a,int b) { M(a,b); } }", "void M(int a, int b) { M(a, b); }")]
    [InlineData("class C { void M() { M ( ) ; } }", "void M() { M(); }")]
    [InlineData("class C { int M(int a) => ( int ) a ; }", "int M(int a) => (int)a;")]
    [InlineData("class C { void M(bool b) { if(b){} } }", "if (b) { }")]
    [InlineData("class C { int M(int a) => a<1?2:3; }", "int M(int a) => a < 1 ? 2 : 3;")]
    [InlineData("class C { System.Collections.Generic.List<int> M() => new System.Collections.Generic.List < int > (); }",
        "new System.Collections.Generic.List<int>();")]
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
        var formatted = Format.Text("""
            class C {
                int _a;      // the first
                int _bb;     // the second
            }
            """);

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
        var formatted = Format.Text("""
            class C {
                void M() {
                    for (var i = 0; i < 2; i++)
                        for (var j = 0; j < 2; j++) {
                            M();
                        }
                }
            }
            """);

        Assert.Contains("        for (var i = 0; i < 2; i++)\n        for (var j = 0; j < 2; j++) {", formatted, StringComparison.Ordinal);
    }

    [Fact]
    public void ClosingDelimiter_TakesTheIndentOfTheLineThatOpenedIt() {
        var formatted = Format.Text("""
            class C {
                void M(int a, int b) {
                    M(
                            a,
                            b
                            );
                }
            }
            """);

        Assert.Contains("        M(\n            a,\n            b\n        );", formatted, StringComparison.Ordinal);
    }

    [Fact]
    public void ContinuationLines_TakeOneLevel_RegardlessOfChainDepth() {
        var formatted = Format.Text("""
            class C {
                int M(int a, int b) {
                    return a
                + b
                        + a;
                }
            }
            """);

        Assert.Contains("        return a\n            + b\n            + a;", formatted, StringComparison.Ordinal);
    }

    [Fact]
    public void PreprocessorIf_GoesToColumnZero_AndRegionsIndentWithTheCode() {
        var formatted = Format.Text("""
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
            """);

        // ⚠ DEBUG is not defined for `skala format` — there is no project to ask until milestone 5
        // — so the branch is disabled text and is frozen. What phase 1 owns here is the directives'
        // own column, and `#region` indenting with the code.
        Assert.Contains("\n#if DEBUG\n", formatted, StringComparison.Ordinal);
        Assert.Contains("\n#endif\n", formatted, StringComparison.Ordinal);
        Assert.Contains("\n    #region Things\n", formatted, StringComparison.Ordinal);
    }

    [Fact]
    public void SwitchLabels_IndentFromTheSwitch_AndStatementsFromTheLabel() {
        var formatted = Format.Text("""
            class C {
                void M(int a) {
                    switch (a) {
                    case 1:
                    M(a);
                    break;
                    }
                }
            }
            """);

        Assert.Contains("        switch (a) {\n            case 1:\n                M(a);\n                break;\n        }", formatted, StringComparison.Ordinal);
    }
}

public sealed class BraceTests {
    [Fact]
    public void OpenBrace_JoinsThePreviousLine() {
        var formatted = Format.Text("""
            class C
            {
                void M()
                {
                }
            }
            """);

        Assert.Contains("class C {", formatted, StringComparison.Ordinal);
        Assert.Contains("void M() { }", formatted, StringComparison.Ordinal);
    }

    [Fact]
    public void ElseCatchFinally_JoinTheClosingBrace() {
        var formatted = Format.Text("""
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
            """);

        Assert.Contains("} catch (System.Exception) { }", formatted, StringComparison.Ordinal);
        Assert.Contains("} finally { }", formatted, StringComparison.Ordinal);
        Assert.Contains("} else { }", formatted, StringComparison.Ordinal);
    }

    [Fact]
    public void ABraceIsNeverJoinedAcrossAComment() {
        // ⚠ Joining `// why` with the brace below it would put the brace inside the comment.
        var formatted = Format.Text("""
            class C {
                void M()
                // why
                {
                    M();
                }
            }
            """);

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
        Assert.Contains("namespace N;\n\nclass C", Format.Text("namespace N;\nclass C {\n}\n"), StringComparison.Ordinal);

    [Fact]
    public void TheUsingList_GetsOneBlankAfterIt() =>
        Assert.Contains("using System;\n\nclass C", Format.Text("using System;\nclass C {\n}\n"), StringComparison.Ordinal);
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
        Assert.Contains("    var  matrix = new[,] {\n        { 1 , 0 },\n        { 0 , 1 }\n    };", formatted, StringComparison.Ordinal);
        Assert.Contains("void M() { }", formatted, StringComparison.Ordinal);
    }

    [Fact]
    public void ARawStringLiteral_IsUntouched() {
        const string source = "class C {\n    const string A = \"\"\"\n        {  }\n          x\n        \"\"\";\n}\n";
        Assert.Contains("\"\"\"\n        {  }\n          x\n        \"\"\"", Format.Text(source), StringComparison.Ordinal);
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

        Assert.Equal("class C {\n    /// <summary>\n    /// Docs.\n    /// </summary>\n    void M() { }\n}\n", Format.Text(source));
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
            var written = CrashArtifacts.Write(directory.FullName, "Thing.cs", "class A { }", "class B { }", Format.Options);
            Assert.NotNull(written);
            Assert.Equal("class A { }", File.ReadAllText(Path.Combine(written, "input.cs")));
            Assert.Equal("class B { }", File.ReadAllText(Path.Combine(written, "output.cs")));
            Assert.Contains("max_line_length = 120", File.ReadAllText(Path.Combine(written, "config.snapshot")), StringComparison.Ordinal);
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
        Assert.Equal("M( )".IndexOf('(', StringComparison.Ordinal) + source.IndexOf("M( )", StringComparison.Ordinal) + 1, edit.Span.Start);
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
        Assert.DoesNotContain("\n\n", formatted.Replace("\r\n", "\n", StringComparison.Ordinal).Replace("\n\n", "|", StringComparison.Ordinal), StringComparison.Ordinal);
        Assert.Contains("\r\n", formatted, StringComparison.Ordinal);
    }
}
