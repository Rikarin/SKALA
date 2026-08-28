using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Core.Configuration;

namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     The two <c>disable_*</c> switches the formatter honours, and the shape the whole family has.
/// </summary>
/// <remarks>
///     ⚠ These keys are a different shape from every other option in the registry: each one
///     <em>suppresses</em> a class of edit rather than choosing between two renderings. That makes them
///     testable in a way nothing else here is — a suppressed class must come back byte-identical to the
///     input in that respect — and it makes them easy to get subtly wrong, because "formats less" and
///     "formats nothing" look alike on a file that was nearly right to begin with. Every subject below
///     is therefore wrong in <em>several</em> dimensions at once, so that a switch which suppressed one
///     class too many or too few is visible rather than plausible.
///     <para>
///         ⚠ The behaviour each test asserts was measured against <c>jb cleanupcode</c> under
///         <c>OracleProfile.FormatOnly</c> with this repository's own <c>.editorconfig</c> and the one key
///         appended, not derived from the key's name. See <c>PhaseOneOptions.DisableFormatter</c>,
///         <c>PhaseOneOptions.DisableBlankLineChanges</c>, and SK-DIV-0060 … SK-DIV-0064 for the seven
///         siblings that are measured and not implemented.
///     </para>
/// </remarks>
public sealed class FormatterOffSwitchTests {
    /// <summary>Wrong in spacing, indentation, blank lines and wrapping at once.</summary>
    const string Crooked = """
                           class C {
                               public int Alpha ;



                               public void Method( int one,int two ) {
                                       var sum=one+two;
                                   if(sum>0){
                                   Alpha = sum;
                                   }
                               }
                           }

                           """;

    static string Format(string source, params (string Key, string Value)[] overrides) =>
        CSharpFormatter.Format(
            "Test.cs",
            SourceText.From(source),
            OptionResolver.Resolve(
                Path.Combine(Rikarin.Skala.Testing.Corpus.RepositoryRoot, "Test.cs"),
                [.. overrides.Select(static o => new KeyValuePair<string, string>(o.Key, o.Value))]
            )
                .Options
        )
            .Formatted;

    /// <summary>
    ///     ⚠ Byte-identical, and the negative control is in the same test.
    /// </summary>
    /// <remarks>
    ///     ⚠ Asserting only the first half would pass against a formatter that had been handed an
    ///     already-tidy file, which is exactly how three keys in this family came to be recorded as
    ///     "inert: the oracle returns the file unchanged at both values". The subject has to be shown
    ///     moving before "it did not move" means anything.
    /// </remarks>
    [Fact]
    public void DisableFormatter_ReturnsTheFileByteForByte() {
        Assert.Equal(Crooked, Format(Crooked, ("resharper_disable_formatter", "true")));
        Assert.NotEqual(Crooked, Format(Crooked));
    }

    /// <summary>⚠ Nothing is written either, so <c>skala format</c> reports the file as unchanged.</summary>
    [Fact]
    public void DisableFormatter_EmitsNoEdits() {
        var result = CSharpFormatter.Format(
            "Test.cs",
            SourceText.From(Crooked),
            OptionResolver.Resolve(
                Path.Combine(Rikarin.Skala.Testing.Corpus.RepositoryRoot, "Test.cs"),
                [new KeyValuePair<string, string>("resharper_disable_formatter", "true")]
            )
                .Options
        );

        Assert.False(result.Changed);
        Assert.Equal(FormatOutcome.Formatted, result.Outcome);
    }

    /// <summary>
    ///     ⚠ A file that does not parse is still reported, because the switch is about whitespace.
    /// </summary>
    /// <remarks>
    ///     The check sits <em>after</em> ADR-003's gate rather than before it, and this is the assertion
    ///     that says so. Putting it first would be a formatting option quietly switching off the tool's
    ///     single most important safety report.
    /// </remarks>
    [Fact]
    public void DisableFormatter_StillReportsAFileThatDoesNotParse() {
        var result = CSharpFormatter.Format(
            "Test.cs",
            SourceText.From("class C { void M( { }"),
            OptionResolver.Resolve(
                Path.Combine(Rikarin.Skala.Testing.Corpus.RepositoryRoot, "Test.cs"),
                [new KeyValuePair<string, string>("resharper_disable_formatter", "true")]
            )
                .Options
        );

        Assert.Equal(FormatOutcome.NotParseable, result.Outcome);
    }

    /// <summary>
    ///     ⚠ All three blank-line systems at once — caps, requirements and removals.
    /// </summary>
    /// <remarks>
    ///     The subject exercises one of each and the assertions name them: the three-blank run above
    ///     <c>Method</c> is past <c>keep_blank_lines_in_declarations</c>, the gap between <c>}</c> and
    ///     the statement below it is where <c>blank_lines_around_invocable</c> inserts one, and the
    ///     blank before a closing brace is what <c>remove_blank_lines_near_braces_in_code</c> deletes.
    ///     A one-system implementation passes on one of these and fails on the other two.
    /// </remarks>
    [Fact]
    public void DisableBlankLineChanges_KeepsEveryRunExactlyAsWritten() {
        const string source = """
                              class C {
                                  int _field;



                                  void M() {
                                      var x = 1;

                                  }
                                  void N() { }
                              }

                              """;

        var off = Format(source, ("resharper_disable_blank_line_changes", "true"));

        // The cap did not truncate the run …
        Assert.Contains("int _field;\n\n\n\n    void M()", off, StringComparison.Ordinal);
        // … the removal did not delete the blank above the brace …
        Assert.Contains("var x = 1;\n\n    }", off, StringComparison.Ordinal);
        // … and the requirement did not insert one between the two methods.
        Assert.Contains("}\n    void N()", off, StringComparison.Ordinal);

        // The negative control: at the export's value all three fire on this same file.
        var on = Format(source);
        Assert.Contains("int _field;\n\n\n    void M()", on, StringComparison.Ordinal);
        Assert.Contains("var x = 1;\n    }", on, StringComparison.Ordinal);
        Assert.Contains("}\n\n    void N()", on, StringComparison.Ordinal);
    }

    /// <summary>
    ///     ⚠ Blank lines only. Line breaks that are not blank lines still move, which is what separates
    ///     this key from <c>disable_line_break_changes</c> — measured, and the reason the latter is not
    ///     implemented as an alias of this one.
    /// </summary>
    [Fact]
    public void DisableBlankLineChanges_StillBreaksLines() {
        var off = Format(
            "class C { void M(bool b) { if(b){ M(b); } } }",
            ("resharper_disable_blank_line_changes", "true")
        );

        Assert.Contains("if (b) {\n            M(b);\n        }", off, StringComparison.Ordinal);
    }

    /// <summary>
    ///     ⚠ A <c>///</c> run's blank count is structure, not spacing: 0 → 1 splits one trivia into two
    ///     and the token-stream check abandons the file. Returning the author's own count cannot do
    ///     that, and this is the case that would catch a future implementation that resolved the count
    ///     some other way.
    /// </summary>
    [Fact]
    public void DisableBlankLineChanges_DoesNotSplitADocumentationRun() {
        const string source = """
                              interface I { /// <summary>x</summary>
                                /// <remarks>y</remarks>
                                int M();
                              }

                              """;

        var result = CSharpFormatter.Format(
            "Test.cs",
            SourceText.From(source),
            OptionResolver.Resolve(
                Path.Combine(Rikarin.Skala.Testing.Corpus.RepositoryRoot, "Test.cs"),
                [new KeyValuePair<string, string>("resharper_disable_blank_line_changes", "true")]
            )
                .Options
        );

        Assert.Equal(FormatOutcome.Formatted, result.Outcome);
    }
}
