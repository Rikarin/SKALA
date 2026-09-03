using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Core.Configuration;

namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     The six <c>disable_*</c> switches the formatter honours, and the shape the whole family has.
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
///         appended, not derived from the key's name. See <c>PhaseOneOptions.DisableFormatter</c> and
///         its five siblings, and SK-DIV-0060 … SK-DIV-0064 for the method.
///     </para>
///     <para>
///         ⚠ Four of the six were "measured and not read here" until this file grew the tests below.
///         The three that remain unimplemented are <c>disable_int_align</c>, which the export masks, and
///         the two SK-DIV-0060 records as unreachable at any pairing tried.
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
        Assert.Equal(Crooked, Format(Crooked, ("skala_disable_formatter", "true")));
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
                [new KeyValuePair<string, string>("skala_disable_formatter", "true")]
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
                [new KeyValuePair<string, string>("skala_disable_formatter", "true")]
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
    ///     <c>Method</c> is past <c>skala_keep_blank_lines_in_declarations</c>, the gap between <c>}</c> and
    ///     the statement below it is where <c>skala_blank_lines_around_invocable</c> inserts one, and the
    ///     blank before a closing brace is what <c>skala_remove_blank_lines_near_braces_in_code</c> deletes.
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

        var off = Format(source, ("skala_disable_blank_line_changes", "true"));

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
            ("skala_disable_blank_line_changes", "true")
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
                [new KeyValuePair<string, string>("skala_disable_blank_line_changes", "true")]
            )
                .Options
        );

        Assert.Equal(FormatOutcome.Formatted, result.Outcome);
    }

    // ── The four the register measured and nothing read ──────────────────────────────────────
    //
    // ⚠ Every test below asserts the negative control on the same subject, and not as a formality.
    // A suppression that does nothing and a suppression that works are byte-identical on any file
    // the formatter would not have changed anyway, so "the option came back unchanged" is evidence
    // only once the same file has been shown changing without it. Three keys in this family were
    // recorded "inert at both values" on exactly that mistake.

    /// <summary>Wrong indentation on every line, and one statement that has to wrap.</summary>
    /// <remarks>
    ///     ⚠ The wrap is the half that separates "the indenter is off" from "indent to zero": three of
    ///     the output's lines did not exist in the input at all.
    /// </remarks>
    const string Crooked5Indentation = """
                                       class C {
                                               public void Method() {
                                           var value = Compute(alpha, beta) + Compute(gamma, delta) + Compute(epsilon, zeta) + Compute(eta, theta) + Compute(alpha, theta) + Compute(beta, eta);
                                             var chopped = Compute(alpha + beta + gamma + delta, epsilon + zeta + eta + theta + alpha + beta + gamma + delta + epsilon);
                                             }
                                       }

                                       """;

    /// <summary>
    ///     ⚠ Two halves, and a test that asserted only the first would pass on "indent to zero".
    /// </summary>
    /// <remarks>
    ///     A line that existed in the input keeps the leading whitespace the author wrote — eight
    ///     columns before <c>public</c>, four before <c>var</c>, six before the closing brace, none of
    ///     which is what the rules would have written.
    ///     <para>
    ///         ⚠ A line the <em>wrap</em> created has none to keep, and what stands in front of it is the
    ///         break point's own flat rendering: one space before a binary operator and after a comma,
    ///         nothing after an opening parenthesis or before a closing one. Both polarities are asserted
    ///         because the obvious reading — "a created line starts at column zero" — is right on one and
    ///         wrong on the other, and it was measured wrong against <c>jb cleanupcode</c> before it was
    ///         measured right. Spacing and wrapping are untouched: the same statements break in the same
    ///         places either way. SK-DIV-0061.
    ///     </para>
    /// </remarks>
    [Fact]
    public void DisableIndenter_KeepsWhatTheAuthorWrote_AndGivesACreatedLineTheBreaksOwnFlatForm() {
        var off = Format(Crooked5Indentation, ("skala_disable_indenter", "true"));

        // The lines that existed in the input, with the author's own (wrong) whitespace.
        Assert.Contains("class C {\n        public void Method() {\n    var value =", off, StringComparison.Ordinal);
        Assert.Contains("\n      var chopped = Compute(\n", off, StringComparison.Ordinal);
        Assert.Contains("\n      }\n}\n", off, StringComparison.Ordinal);
        // A created line before a binary operator: one space, not zero.
        Assert.Contains("\n + Compute(gamma, delta)\n", off, StringComparison.Ordinal);
        // A created line after `(` and before `)`: nothing at all.
        Assert.Contains("\nalpha + beta + gamma + delta,\n", off, StringComparison.Ordinal);
        Assert.Contains("\n);\n", off, StringComparison.Ordinal);
        // And a created line after `,`: one space again.
        Assert.Contains("\n epsilon + zeta + eta + theta", off, StringComparison.Ordinal);

        // ⚠ The negative control: every one of those lines is reindented at the export's value, and
        // both statements break in the same places, so what moved between the two is indentation alone.
        var on = Format(Crooked5Indentation);
        Assert.Contains("class C {\n    public void Method() {\n        var value =", on, StringComparison.Ordinal);
        Assert.Contains("\n            + Compute(gamma, delta)\n", on, StringComparison.Ordinal);
        Assert.Contains("\n        var chopped = Compute(\n", on, StringComparison.Ordinal);
        Assert.Contains("\n            alpha + beta + gamma + delta,\n", on, StringComparison.Ordinal);
        Assert.Contains("\n        );\n    }\n}\n", on, StringComparison.Ordinal);
    }

    /// <summary>Wrong in spacing everywhere, and wrong in indentation and wrapping as well.</summary>
    const string Crooked5Spacing = """
                                   class C {
                                       public int Alpha ;
                                       public void Method( int one,int two ) {
                                           var sum=one  +  two;
                                           if(sum>0){ Alpha=sum; }    // note
                                       }
                                   }

                                   """;

    /// <summary>
    ///     ⚠ Byte for byte, not one-bit — <c>one  +  two</c> keeps both double runs.
    /// </summary>
    /// <remarks>
    ///     ⚠ The double run before <c>+</c> is the case a one-bit implementation gets wrong and nothing
    ///     else here would catch: that gap is a wrap point, whose flat rendering is one space or
    ///     nothing, so preserving it takes the run past the break-point model rather than through it.
    ///     <para>
    ///         ⚠ And the last two assertions are the point of the key: it suppresses the <em>gap</em>
    ///         layer and nothing else. The same file is still reindented and the <c>if</c> body is still
    ///         broken onto its own line, which is what makes this a suppression of one class of edit
    ///         rather than a second <c>disable_formatter</c>. SK-DIV-0062.
    ///     </para>
    /// </remarks>
    [Fact]
    public void DisableSpaceChanges_KeepsEveryInterTokenRunByteForByte() {
        var off = Format(Crooked5Spacing, ("skala_disable_space_changes", "true"));

        Assert.Contains("public int Alpha ;", off, StringComparison.Ordinal);
        Assert.Contains("public void Method( int one,int two ) {", off, StringComparison.Ordinal);
        Assert.Contains("var sum=one  +  two;", off, StringComparison.Ordinal);
        Assert.Contains("if(sum>0){", off, StringComparison.Ordinal);
        // ⚠ The gap before a trailing comment too, which `skala_space_before_trailing_comment` governs and
        // the narrow `disable_space_changes_before_trailing_comment` cannot move at either value.
        Assert.Contains("}    // note", off, StringComparison.Ordinal);
        // Indentation and wrapping are untouched: the body was one line in the input.
        Assert.Contains("        if(sum>0){\n            Alpha=sum;\n        }", off, StringComparison.Ordinal);

        // The negative control: every one of those runs is rewritten at the export's value.
        var on = Format(Crooked5Spacing);
        Assert.Contains("public int Alpha;", on, StringComparison.Ordinal);
        Assert.Contains("public void Method(int one, int two) {", on, StringComparison.Ordinal);
        Assert.Contains("var sum = one + two;", on, StringComparison.Ordinal);
        Assert.Contains("if (sum > 0) {", on, StringComparison.Ordinal);
        Assert.Contains("} // note", on, StringComparison.Ordinal);
    }

    /// <summary>
    ///     One file carrying every break the formatter can add and every one it can remove.
    /// </summary>
    /// <remarks>
    ///     Removals: the three-blank run is past <c>skala_keep_blank_lines_in_declarations</c>, the blank
    ///     before <c>}</c> is what <c>skala_remove_blank_lines_near_braces_in_code</c> deletes, and the break
    ///     after <c>=&gt;</c> is the one <c>skala_keep_existing_expr_member_arrangement</c> re-joins.
    ///     Additions: <c>void M() { var x = 1;</c> is a body the brace rules break, and the gap between
    ///     the two methods is where <c>skala_blank_lines_around_invocable</c> inserts one.
    ///     <para>
    ///         ⚠ The same file as <c>constructs/suppression/skala_disable_line_break_changes.cs</c>,
    ///         and deliberately: the oracle reproduces Skala's output on it byte for byte at the export's
    ///         own configuration, so nothing asserted below is standing on a baseline that already
    ///         disagrees.
    ///     </para>
    /// </remarks>
    const string Crooked5Breaks = """
                                  class C {
                                      int _field;



                                      void M() { var x = 1;

                                      }
                                      int P() =>
                                          1;
                                  }

                                  """;

    /// <summary>
    ///     ⚠ Both directions, blank runs included.
    /// </summary>
    /// <remarks>
    ///     The two assertions that matter most are the additions, because a key implemented as an alias
    ///     of <c>disable_blank_line_changes</c> passes every removal assertion here and fails those:
    ///     <c>void M() { var x = 1;</c> keeps its brace on the statement's line and the two methods stay
    ///     adjacent. SK-DIV-0063.
    /// </remarks>
    [Fact]
    public void DisableLineBreakChanges_AddsNoBreakAndRemovesNone() {
        var off = Format(Crooked5Breaks, ("skala_disable_line_break_changes", "true"));

        // Removals, all three suppressed …
        Assert.Contains("int _field;\n\n\n\n    void M()", off, StringComparison.Ordinal);
        Assert.Contains("var x = 1;\n\n    }", off, StringComparison.Ordinal);
        Assert.Contains("int P() =>\n        1;", off, StringComparison.Ordinal);
        // … and the two additions with them.
        Assert.Contains("void M() { var x = 1;", off, StringComparison.Ordinal);
        Assert.Contains("}\n    int P() =>", off, StringComparison.Ordinal);

        // The negative control: at the export's value all five fire on this same file.
        var on = Format(Crooked5Breaks);
        Assert.Contains("int _field;\n\n\n    void M()", on, StringComparison.Ordinal);
        Assert.Contains("var x = 1;\n    }", on, StringComparison.Ordinal);
        Assert.Contains("int P() => 1;", on, StringComparison.Ordinal);
        Assert.Contains("void M() {\n        var x = 1;", on, StringComparison.Ordinal);
        Assert.Contains("}\n\n    int P() => 1;", on, StringComparison.Ordinal);
    }

    /// <summary>
    ///     ⚠ One direction, and this is the assertion that keeps the two keys apart.
    /// </summary>
    /// <remarks>
    ///     Every removal the test above suppresses is suppressed here too — and every addition it
    ///     suppresses still happens. A key implemented as <c>disable_line_break_changes</c> under
    ///     another name passes the first three assertions and fails the last two, which is the shape
    ///     the split document names as the one most likely to be conflated. SK-DIV-0064.
    /// </remarks>
    [Fact]
    public void DisableLineBreakRemoval_RemovesNoBreak_AndStillAddsOne() {
        var off = Format(Crooked5Breaks, ("skala_disable_line_break_removal", "true"));

        // Nothing the author wrote is taken away …
        Assert.Contains("int _field;\n\n\n\n    void M()", off, StringComparison.Ordinal);
        Assert.Contains("var x = 1;\n\n    }", off, StringComparison.Ordinal);
        Assert.Contains("int P() =>\n        1;", off, StringComparison.Ordinal);
        // … and everything the rules want to add is still added.
        Assert.Contains("void M() {\n        var x = 1;", off, StringComparison.Ordinal);
        Assert.Contains("}\n\n    int P() =>", off, StringComparison.Ordinal);
    }

    /// <summary>
    ///     ⚠ The two keys are not the same key, asserted directly rather than inferred.
    /// </summary>
    /// <remarks>
    ///     ⚠ It is the weaker of the two guards and is here as a backstop, not as the assertion that
    ///     separates them. Wiring the removal key to the changes key was tried, and this test still
    ///     passed — the blank-line halves diverge on their own, so the two outputs differ anyway — while
    ///     <see cref="DisableLineBreakRemoval_RemovesNoBreak_AndStillAddsOne" /> failed on the addition
    ///     it is supposed to allow. That test is the one that keeps the pair apart; this one catches the
    ///     coarser mistake of registering both ids to one property.
    /// </remarks>
    [Fact]
    public void TheTwoLineBreakKeys_AreNotTheSameKey() {
        var changes = Format(Crooked5Breaks, ("skala_disable_line_break_changes", "true"));
        var removal = Format(Crooked5Breaks, ("skala_disable_line_break_removal", "true"));
        var neither = Format(Crooked5Breaks);

        Assert.NotEqual(changes, removal);
        Assert.NotEqual(changes, neither);
        Assert.NotEqual(removal, neither);
    }

    /// <summary>
    ///     ⚠ The two properties a leaking suppression breaks, on every key × every subject.
    /// </summary>
    /// <remarks>
    ///     ⚠
    ///     <b>
    ///         This is not a duplicate of the corpus-wide property suites; it is the only place these
    ///         four keys are asked at all.
    ///     </b> <c>PropertyTests</c> and <c>FuzzerTests</c> run at the
    ///     export's configuration, where all four of these are <c>false</c> and every branch they
    ///     control is dead — so a suppression could leak arbitrarily and 19 000 green property cases
    ///     would say nothing about it. The keys have to be turned on by something, and this is it.
    ///     <para>
    ///         Both properties are the ones a half-applied suppression fails. <b>Idempotence</b>: a
    ///         suppression makes the output depend on the input's own whitespace, so a rule that applies
    ///         to some sites and not others converges on nothing — pass two sees the whitespace pass one
    ///         wrote and moves again. <b>Token equivalence</b>: <c>VerificationFailed</c> is what the
    ///         formatter reports when the output's token stream differs from the input's, and preserved
    ///         whitespace is exactly the material that can fuse or split a trivium — a blank line inside a
    ///         <c>///</c> run splits one trivia into two, which is the failure
    ///         <see cref="DisableBlankLineChanges_DoesNotSplitADocumentationRun" /> already pins for the
    ///         implemented sibling.
    ///     </para>
    /// </remarks>
    [Theory]
    [InlineData("skala_disable_indenter")]
    [InlineData("skala_disable_space_changes")]
    [InlineData("skala_disable_line_break_changes")]
    [InlineData("skala_disable_line_break_removal")]
    public void EverySuppression_IsIdempotent_AndKeepsTheTokenStream(string key) {
        foreach (var subject in new[] { Crooked, Crooked5Indentation, Crooked5Spacing, Crooked5Breaks, Documented }) {
            var first = Result(subject, key);
            Assert.Equal(FormatOutcome.Formatted, first.Outcome);

            var second = Result(first.Formatted, key);
            Assert.Equal(FormatOutcome.Formatted, second.Outcome);
            Assert.Equal(first.Formatted, second.Formatted);
        }
    }

    /// <summary>
    ///     A subject whose whitespace is load-bearing for the <em>token stream</em> and not only for
    ///     the layout.
    /// </summary>
    /// <remarks>
    ///     ⚠ Roslyn ends a documentation comment at a blank line, so the gap between two <c>///</c>
    ///     lines is the one piece of whitespace in the language where 0 → 1 splits one trivium into two
    ///     and 1 → 0 fuses two into one. A suppression that preserves the author's whitespace at some
    ///     sites and rewrites it at others is the shape most likely to land a blank there, and the
    ///     answer is <c>VerificationFailed</c> rather than a misplaced blank line — the file is not
    ///     formatted at all. The run starts on the brace line on purpose: that is the placement that
    ///     defeated the guard in SK-FUZZ-0002.
    /// </remarks>
    const string Documented = """
                              interface I { /// <summary>x</summary>
                                /// <remarks>y</remarks>
                                  int  M( ) ;
                                /// <summary>z</summary>
                                int N();
                              }

                              """;

    static FormatResult Result(string source, string key) =>
        CSharpFormatter.Format(
            "Test.cs",
            SourceText.From(source),
            OptionResolver.Resolve(
                Path.Combine(Rikarin.Skala.Testing.Corpus.RepositoryRoot, "Test.cs"),
                [new KeyValuePair<string, string>(key, "true")]
            )
                .Options
        );
}
