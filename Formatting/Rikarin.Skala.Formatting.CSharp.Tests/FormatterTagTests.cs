using Microsoft.CodeAnalysis.Text;

namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     The escape hatch, on the paths that are not the document builder's own walk.
/// </summary>
/// <remarks>
///     ⚠ <c>format</c> honoured <c>@formatter:off</c> from milestone 1 and
///     <c>FormatterTests.AFormatterOffSpan_IsCopiedByteForByte</c> has pinned it since. What it pinned
///     was one path. The tag turned out to be missed by three others — arrangement, the xmldoc
///     sub-formatter and <c>skala fix</c> — because each of them produces edits without building a
///     document, and the check lived inside the document builder. These are the cases that fail if any
///     of them stops asking.
///     <para>
///         ⚠ Every one was verified by mutation: the guard was broken deliberately, the test was watched to
///         fail, and the guard restored. A guard that has never been seen to fail is a guard nobody knows is
///         connected.
///     </para>
/// </remarks>
public sealed class FormatterTagTests {
    const string Region = """
                          class C {
                              // @formatter:off
                              static readonly int[,] Table = {
                                  { 1,   2,   3 },
                                  { 700, 800, 900 },
                              };
                              // @formatter:on
                              void   M( )   {
                              }
                          }
                          """;

    /// <summary>The region the tags protect, exactly as written.</summary>
    const string Protected = """
                                 // @formatter:off
                                 static readonly int[,] Table = {
                                     { 1,   2,   3 },
                                     { 700, 800, 900 },
                                 };
                                 // @formatter:on
                             """;

    [Fact]
    public void TheRegion_IsCopiedByteForByte_AndTheRestIsFormatted() {
        var formatted = Format.Text(Region);
        Assert.Contains(Protected, formatted, StringComparison.Ordinal);
        Assert.Contains("void M() { }", formatted, StringComparison.Ordinal);
    }

    /// <summary>
    ///     ⚠ The sub-formatter runs on the formatter's <em>output</em> and re-parses it, so the verbatim
    ///     chunk the document builder had just protected looked to it like any other <c>///</c> comment.
    ///     It re-wrapped one, inside the same process that was honouring the tag one pass earlier.
    /// </summary>
    [Fact]
    public void TheXmlDocSubFormatter_LeavesADocCommentInsideTheRegionAlone() {
        // ⚠ The comment is two crammed elements rather than one long line, and that is a measurement
        // rather than a tidy-up. `jb cleanupcode` with `CSharpFormatDocComments` enabled returns a
        // lone `///<summary>…</summary>` **byte-identical** whenever nothing about it has to change —
        // the marker's space arrives with a rebuild — so the old subject stopped separating "the tag
        // suppressed the sub-formatter" from "the sub-formatter had nothing to do". Two elements on
        // one line have to be split, so the control below is live again.
        const string source = """
                              class D {
                                  // @formatter:off
                                  ///<summary>A hand-laid line.</summary><remarks>Beside it.</remarks>
                                  public void M() { }
                                  // @formatter:on

                                  ///<summary>A hand-laid line.</summary><remarks>Beside it.</remarks>
                                  public void N() { }
                              }
                              """;

        var options = Rikarin.Skala.Core.Configuration.OptionResolver
            .Resolve(Path.Combine(Rikarin.Skala.Testing.Corpus.RepositoryRoot, "Test.cs"))
            .Options;

        var formatted = CSharpFormatter
            .Format("Test.cs", SourceText.From(source), options)
            .Formatted;

        Assert.Contains(
            "    ///<summary>A hand-laid line.</summary><remarks>Beside it.</remarks>\n    public void M() { }",
            formatted,
            StringComparison.Ordinal
        );

        // …and the identical comment outside the region *is* rewritten, so the test is about the tag
        // rather than about the sub-formatter being off.
        Assert.Contains("    /// <summary>", formatted, StringComparison.Ordinal);
    }

    /// <summary>
    ///     ⚠ SK-FUZZ-0001. An unhandled <c>IndexOutOfRangeException</c> out of
    ///     <c>EditEmitter.AddIfDifferent</c>, on 32 bytes, that escaped every handler and killed the
    ///     process. The file-level rules — the trailing-whitespace trim and the inserted final newline —
    ///     run over the output <em>after</em> the writer produced it, and an off-region that reaches the
    ///     end of the file is the only anchor that covers the bytes they remove.
    /// </summary>
    [Fact]
    public void AnOffRegion_ReachingAWhitespaceOnlyEndOfFile_DoesNotThrow() {
        var result = Format.Run("class C {\n// @formatter:off\n}   ");

        // ⚠ The oracle's own answer, byte for byte:
        //   printf 'class C {\n// @formatter:off\n}   ' > Crash.cs
        //   dotnet run --project Testing/Rikarin.Skala.Testing -- ask <dir>
        // gives `class C {\n// @formatter:off\n}\n` — the trailing spaces trimmed and a final
        // newline added, with the tag comment's own line left at column 0.
        Assert.Equal("class C {\n// @formatter:off\n}\n", result.Formatted);
    }

    /// <summary>
    ///     ⚠ SK-FUZZ-0005. Everything after the tag is copied byte-for-byte, so token equivalence ought
    ///     to hold there trivially. It did not: <c>EmitVerbatim</c> — the arm that writes an
    ///     interpolated string from its original span, because a moved space would change the value —
    ///     did not check that it was inside a region already written, wrote the string a second time
    ///     under a second anchor, and the emitter turned the overlap into an edit that deleted the rest
    ///     of the file. <c>SK9099</c> refused the write, so the file could not be formatted at all.
    /// </summary>
    [Fact]
    public void AnInterpolatedStringInsideTheRegion_DoesNotBreakTokenEquivalence() {
        const string source = """
                              class C {
                                  void M() {
                                  // @formatter:off
                                  }
                                  void N() {
                                  X($"a {b} c");
                                  }
                              }
                              """;

        var result = Format.Run(source);
        Assert.DoesNotContain(
            result.Diagnostics,
            diagnostic => string.Equals(diagnostic.Id, "SK9099", StringComparison.Ordinal)
        );

        Assert.Contains("""X($"a {b} c");""", result.Formatted, StringComparison.Ordinal);
    }

    /// <summary>
    ///     ⚠ SK-FUZZ-0011, and SK-FUZZ-0005's guard evaluated one call too early.
    /// </summary>
    /// <remarks>
    ///     The check that fixed SK-FUZZ-0005 sits at the top of <c>EmitVerbatim</c>, but the tag comment
    ///     here is in the node's own <em>leading trivia</em> — so the piece that opens the region is
    ///     emitted by <c>EmitUpTo</c>, which runs after that check has already passed. Traced:
    ///     <c>_verbatimUntil</c> is -1 on entry and the end of the file on return, and the node was
    ///     written a second time over source the tag had covered.
    ///     <para>
    ///         ⚠ Both halves are needed and neither is exotic. The unbalanced <c>#if</c> is what makes
    ///         <c>PreprocessorGuard</c> emit the member verbatim at all; without the tag
    ///         <c>_verbatimUntil</c> never moves. Either one alone formats cleanly.
    ///     </para>
    /// </remarks>
    [Fact]
    public void AnOffTagInTheLeadingTriviaOfAnUnbalancedMember_DoesNotBreakTokenEquivalence() {
        const string source = "class C {\n#if true\n// @formatter:off\nvoid M() {\n#endif\n}\n}\n";

        var result = Format.Run(source);
        Assert.DoesNotContain(
            result.Diagnostics,
            diagnostic => string.Equals(diagnostic.Id, "SK9099", StringComparison.Ordinal)
        );

        // Everything from the tag line on is the author's, byte for byte.
        Assert.Equal(source, result.Formatted);
    }

    /// <summary>
    ///     ⚠ The tag comment's own line is inside the region, so its indentation is the author's.
    /// </summary>
    /// <remarks>
    ///     Measured on the oracle, which leaves a twelve-space <c>off</c> tag at twelve spaces inside a
    ///     class body it would otherwise indent to four. ⚠ It does <em>not</em> do the same for the
    ///     <c>on</c> tag — it normalises that line to four — and Skala deliberately keeps both, because
    ///     a person reading the two lines as the boundary of their own block expects neither to move.
    ///     That half is SK-DIV-0017's second paragraph.
    /// </remarks>
    [Fact]
    public void TheTagCommentsOwnLine_KeepsTheIndentationTheAuthorGaveIt() {
        var formatted = Format.Text(
            "class C {\n            // @formatter:off\n    void  M( ) { }\n            // @formatter:on\n    void  N( ) { }\n}\n"
        );

        Assert.Contains("\n            // @formatter:off\n", formatted, StringComparison.Ordinal);
        Assert.Contains("\n            // @formatter:on\n", formatted, StringComparison.Ordinal);
        Assert.Contains("    void N() { }", formatted, StringComparison.Ordinal);
    }

    /// <summary>
    ///     ⚠ A tag in a <em>trailing</em> comment does not reach backwards over the code on its line.
    /// </summary>
    [Fact]
    public void ATagInATrailingComment_DoesNotProtectTheCodeBeforeIt() {
        var formatted = Format.Text(
            "class C {\n    void  A( )   { } // @formatter:off\n    void  M( )   {\n    }\n}\n"
        );

        // The oracle's answer on the same input, which is the same: `void A() { }` is formatted and
        // `void  M( )   {` is not.
        Assert.Contains("    void A() { } // @formatter:off", formatted, StringComparison.Ordinal);
        Assert.Contains("    void  M( )   {", formatted, StringComparison.Ordinal);
    }

    /// <summary>
    ///     ⚠ SK-DIV-0017: a comment that <em>mentions</em> the tag is prose, not a directive.
    /// </summary>
    /// <remarks>
    ///     The oracle disagrees, and the disagreement is measured rather than assumed —
    ///     <c>// we support @formatter:off here</c> turns formatting off to the end of the file in
    ///     <c>jb cleanupcode</c> 2025.2.6 exactly as a bare tag does. The argument for diverging is in
    ///     <c>docs/divergences.md</c>; the short version is that it fired inside this repository, on four
    ///     files that document the directive, and nothing reported it.
    /// </remarks>
    [Theory]
    [InlineData("// we support @formatter:off here")]
    [InlineData("// see @formatter:off")]
    [InlineData("// ⚠ `@formatter:off`. The finding still stands.")]
    [InlineData("/// The <c>@formatter:off</c> regions of one file.")]
    public void ACommentThatMentionsTheTag_IsNotTheTag(string comment) {
        var formatted = Format.Text($"class C {{\n    {comment}\n    void  M( )   {{ }}\n}}\n");
        Assert.Contains("void M() { }", formatted, StringComparison.Ordinal);
    }

    /// <summary>⚠ …and a reason written after the tag is still the tag.</summary>
    [Theory]
    [InlineData("// @formatter:off")]
    [InlineData("//@formatter:off")]
    [InlineData("// @formatter:off because the table below is hand-aligned")]
    public void ACommentThatStartsWithTheTag_IsTheTag(string comment) {
        var formatted = Format.Text($"class C {{\n    {comment}\n    void  M( )   {{ }}\n}}\n");
        Assert.Contains("void  M( )   { }", formatted, StringComparison.Ordinal);
    }

    // ── The four keys ────────────────────────────────────────────────────────────────────────
    //
    // ⚠ These four are what the key-flip sweep cannot ask about, and the reason is written up as
    // SK-DIV-0089: no value the sweep's probe can generate makes any of them observable, because the
    // probe offers the key's default and the default with an `x` on the end, and the built-in tag is
    // a prefix of both. So the model measured against `jb cleanupcode` 2025.2.6 by hand is pinned
    // here instead, one test per measurement, and the oracle's answer is quoted beside each.

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

    const string Custom = """
                          class C {
                              // @fmt:off
                              void   M( )   {
                              }
                              // @fmt:on
                              void   N( )   {
                              }
                          }
                          """;

    const string Builtin = """
                           class C {
                               // @formatter:off
                               void   M( )   {
                               }
                               // @formatter:on
                               void   N( )   {
                               }
                           }
                           """;

    /// <summary>
    ///     ⚠ The negative control the rest of this section rests on: an unrecognised tag protects
    ///     nothing, so every "preserved" below is a statement about the key rather than about the
    ///     formatter declining to move.
    /// </summary>
    [Fact]
    public void AnUnrecognisedTag_ProtectsNothing() {
        var formatted = FormatWith(Custom);
        Assert.Contains("void M() { }", formatted, StringComparison.Ordinal);
        Assert.DoesNotContain("void   M( )", formatted, StringComparison.Ordinal);
    }

    /// <summary>
    ///     <c>resharper_formatter_off_tag</c> / <c>_on_tag</c>: a configured tag is recognised, and it is
    ///     recognised <em>in addition to</em> the built-in pair rather than instead of it.
    /// </summary>
    /// <remarks>
    ///     ⚠ Measured. With <c>resharper_formatter_off_tag = @zzz:off</c> and
    ///     <c>resharper_formatter_on_tag = @zzz:on</c>, <c>jb cleanupcode</c> returns a region written
    ///     with the <em>built-in</em> <c>@formatter:off</c> untouched — so the configured pair is
    ///     additive. Skala used to replace, which is strictly less protective than the oracle on a
    ///     feature whose whole job is "nothing touches this".
    /// </remarks>
    [Fact]
    public void AConfiguredTag_IsRecognisedBesideTheBuiltInRatherThanInsteadOfIt() {
        var custom = FormatWith(
            Custom,
            ("resharper_formatter_off_tag", "@fmt:off"),
            ("resharper_formatter_on_tag", "@fmt:on")
        );
        Assert.Contains("void   M( )   {", custom, StringComparison.Ordinal);
        Assert.Contains("void N() { }", custom, StringComparison.Ordinal);

        // The built-in pair, with the configured pair pointed somewhere else entirely.
        var builtin = FormatWith(
            Builtin,
            ("resharper_formatter_off_tag", "@zzz:off"),
            ("resharper_formatter_on_tag", "@zzz:on")
        );
        Assert.Contains("void   M( )   {", builtin, StringComparison.Ordinal);
        Assert.Contains("void N() { }", builtin, StringComparison.Ordinal);
    }

    /// <summary>
    ///     <c>resharper_formatter_tags_enabled = false</c> switches off the <em>configured</em> pair and
    ///     leaves the built-in one alone.
    /// </summary>
    /// <remarks>
    ///     ⚠ Measured, and it is the half Skala had backwards: the key used to open the guard outright,
    ///     so a single line of configuration disabled the escape hatch for every file. The oracle keeps
    ///     honouring <c>@formatter:off</c> under it and stops honouring <c>@fmt:off</c>.
    /// </remarks>
    [Fact]
    public void TagsDisabled_StopsTheConfiguredPairAndNotTheBuiltInOne() {
        var custom = FormatWith(
            Custom,
            ("resharper_formatter_tags_enabled", "false"),
            ("resharper_formatter_off_tag", "@fmt:off"),
            ("resharper_formatter_on_tag", "@fmt:on")
        );
        Assert.Contains("void M() { }", custom, StringComparison.Ordinal);

        var builtin = FormatWith(Builtin, ("resharper_formatter_tags_enabled", "false"));
        Assert.Contains("void   M( )   {", builtin, StringComparison.Ordinal);
        Assert.Contains("void N() { }", builtin, StringComparison.Ordinal);
    }

    /// <summary>
    ///     <c>resharper_formatter_tags_accept_regexp = true</c> makes the configured pair patterns.
    /// </summary>
    /// <remarks>
    ///     ⚠ Measured: <c>@f.*:off</c> under <c>accept_regexp = true</c> protects a <c>// @fmt:off</c>
    ///     region and under <c>false</c> does not. Skala used to answer this key by opening the guard —
    ///     "not implemented, in any pass" — which meant the one key a person would set to make their
    ///     tags *more* expressive silently turned the hatch off.
    /// </remarks>
    [Fact]
    public void AcceptRegexp_MakesTheConfiguredTagAPattern() {
        var on = FormatWith(
            Custom,
            ("resharper_formatter_tags_accept_regexp", "true"),
            ("resharper_formatter_off_tag", "@f.*:off"),
            ("resharper_formatter_on_tag", "@f.*:on")
        );
        Assert.Contains("void   M( )   {", on, StringComparison.Ordinal);
        Assert.Contains("void N() { }", on, StringComparison.Ordinal);

        var off = FormatWith(
            Custom,
            ("resharper_formatter_tags_accept_regexp", "false"),
            ("resharper_formatter_off_tag", "@f.*:off"),
            ("resharper_formatter_on_tag", "@f.*:on")
        );
        Assert.Contains("void M() { }", off, StringComparison.Ordinal);
    }

    /// <summary>
    ///     ⚠ A pattern the runtime will not compile matches nothing, and the built-in pair still works.
    /// </summary>
    /// <remarks>
    ///     Not measured against the oracle — an unparsable pattern is not a configuration anyone means —
    ///     but it must not throw out of the formatter, and it must not fall back to a literal comparison,
    ///     which would turn a typo into a silently different rule.
    /// </remarks>
    [Fact]
    public void AnUnparsablePattern_MatchesNothing_AndTheBuiltInPairSurvivesIt() {
        var custom = FormatWith(
            Custom,
            ("resharper_formatter_tags_accept_regexp", "true"),
            ("resharper_formatter_off_tag", "@fmt:off("),
            ("resharper_formatter_on_tag", "@fmt:on(")
        );
        Assert.Contains("void M() { }", custom, StringComparison.Ordinal);

        var builtin = FormatWith(
            Builtin,
            ("resharper_formatter_tags_accept_regexp", "true"),
            ("resharper_formatter_off_tag", "@fmt:off("),
            ("resharper_formatter_on_tag", "@fmt:on(")
        );
        Assert.Contains("void   M( )   {", builtin, StringComparison.Ordinal);
    }

    /// <summary>
    ///     ⚠ SK-DIV-0017's narrowing survives the regexp reading: a pattern is anchored at the start of
    ///     the comment's body, so a comment that mentions a matching tag is still prose.
    /// </summary>
    [Fact]
    public void UnderAcceptRegexp_ACommentThatMentionsAMatchingTag_IsStillProse() {
        var formatted = FormatWith(
            "class C {\n    // we support @fmt:off here\n    void  M( )   { }\n}\n",
            ("resharper_formatter_tags_accept_regexp", "true"),
            ("resharper_formatter_off_tag", "@f.*:off")
        );
        Assert.Contains("void M() { }", formatted, StringComparison.Ordinal);
    }
}
