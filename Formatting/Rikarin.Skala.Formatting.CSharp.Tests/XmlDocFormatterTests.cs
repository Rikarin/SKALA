using System.Collections.Immutable;
using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Core.Configuration;
using Rikarin.Skala.Core.Diagnostics;
using Rikarin.Skala.Formatting.CSharp;
using Rikarin.Skala.Options;

namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>Runs the pipeline, which formats documentation comments by default.</summary>
/// <remarks>
///     ⚠ These fixtures assert <b>the semantics JetBrains' settings pages state</b> rather than the
///     oracle's behaviour, and they were once the only evidence these keys had. They are not any
///     more: <c>OracleProfile.DocComments</c> enables ReSharper's own <c>CSharpFormatDocComments</c>
///     task, <c>constructs/xmldoc/</c> carries a corpus file per key with the oracle's answer beside
///     it, and 13 of the 22 keys are Tier A on that evidence like every other option in Skala.
///     <para>
///         ⚠ These stay, and they are not redundant. A corpus fixture measures one configuration — the
///         repository's — and these measure the key at both values with the shape isolated, which is what
///         says <em>why</em> a fixture came out the way it did. For the nine keys that disagree with the
///         oracle (SK-DIV-0019 … SK-DIV-0023) they are also the only statement of what Skala does mean by
///         the key.
///     </para>
/// </remarks>
public static class XmlDoc {
    static FormattingOptions Resolve(params (string Key, string Value)[] overrides) =>
        OptionResolver.Resolve(
            Path.Combine(Rikarin.Skala.Testing.Corpus.RepositoryRoot, "Test.cs"),
            [.. overrides.Select(static o => new KeyValuePair<string, string>(o.Key, o.Value))]
        ).Options;

    public static FormatResult Run(string source, params (string Key, string Value)[] overrides) {
        var options = Resolve(overrides);
        return CSharpFormatter.Format("Test.cs", SourceText.From(source), options, null, null, xmlDoc: true);
    }

    public static string Text(string source, params (string Key, string Value)[] overrides) =>
        Run(source, overrides).Formatted;

    /// <summary>Wraps doc-comment lines in a class so the pipeline sees a real declaration.</summary>
    public static string InClass(params string[] lines) =>
        "class C {\n" + string.Join("\n", lines.Select(static l => "    " + l)) + "\n    void M() { }\n}\n";

    /// <summary>The <c>///</c> lines of the output, with the code indentation removed.</summary>
    public static ImmutableArray<string> DocLines(string formatted) => [
        .. formatted.Split('\n')
            .Select(static line => line.TrimStart())
            .Where(static line => line.StartsWith("///", StringComparison.Ordinal))
    ];
}

public sealed class XmlDocSubFormatterTests {
    [Fact]
    public void UnderNoXmlDoc_TheOracleAgreementIsUntouched() {
        // ⚠ The escape hatch, and it is now the only thing that reproduces what the pinned oracle
        // profile does: no marker space, no re-wrap, byte-identical. Asserted rather than assumed,
        // because `--no-xmldoc` is what a tree that wants the old answer reaches for and a kill
        // switch that half-works is worse than none.
        const string source = "class C {\n    ///<summary>A summary line.</summary>\n    void M() { }\n}\n";
        var options = OptionResolver
            .Resolve(Path.Combine(Rikarin.Skala.Testing.Corpus.RepositoryRoot, "Test.cs"))
            .Options;
        var formatted = CSharpFormatter
            .Format("Test.cs", SourceText.From(source), options, null, null, xmlDoc: false)
            .Formatted;

        Assert.Contains("///<summary>A summary line.</summary>", formatted, StringComparison.Ordinal);
    }

    [Fact]
    public void SpaceAfterTripleSlash_IsInserted() {
        var source = XmlDoc.InClass("///<summary>Docs.</summary>");
        Assert.Contains("/// <summary>Docs.</summary>", XmlDoc.Text(source), StringComparison.Ordinal);
    }

    [Fact]
    public void ALongSummary_IsWrappedAtTheConfiguredWidth() {
        var text = string.Join(
            " ",
            Enumerable.Repeat("word", 60)
        );
        var formatted = XmlDoc.Text(XmlDoc.InClass("/// <summary>" + text + "</summary>"));

        foreach (var line in formatted.Split('\n')) {
            Assert.True(TextWidth.Measure(line) <= 120, $"'{line}' is {TextWidth.Measure(line)} columns.");
        }

        // Nothing was lost: every word is still there, in order.
        Assert.Equal(60, XmlDoc.DocLines(formatted).Sum(static l => l.Split("word").Length - 1));
    }

    [Fact]
    public void MaxLineLength_IsTheKeyThatDecidesTheWidth() {
        var text = string.Join(" ", Enumerable.Repeat("word", 40));
        var narrow = XmlDoc.Text(
            XmlDoc.InClass("/// <summary>" + text + "</summary>"),
            ("resharper_xmldoc_max_line_length", "60")
        );

        Assert.All(narrow.Split('\n'), line => Assert.True(TextWidth.Measure(line) <= 60, line));
        Assert.True(
            XmlDoc.DocLines(narrow).Length
            > XmlDoc.DocLines(XmlDoc.Text(XmlDoc.InClass("/// <summary>" + text + "</summary>"))).Length
        );
    }

    [Fact]
    public void WrapLinesFalse_LeavesTheLongLineLong() {
        var text = string.Join(" ", Enumerable.Repeat("word", 60));
        var formatted = XmlDoc.Text(
            XmlDoc.InClass("/// <summary>" + text + "</summary>"),
            ("resharper_xmldoc_wrap_lines", "false")
        );

        Assert.Single(XmlDoc.DocLines(formatted));
    }

    [Fact]
    public void LinebreakBeforeElements_GivesEachListedTagItsOwnLine() {
        var source = XmlDoc.InClass(
            "/// <summary>One.</summary><param name=\"a\">A.</param><param name=\"b\">B.</param>"
        );

        Assert.Equal(
            ["/// <summary>One.</summary>", "/// <param name=\"a\">A.</param>", "/// <param name=\"b\">B.</param>"],
            XmlDoc.DocLines(XmlDoc.Text(source))
        );
    }

    [Fact]
    public void AnElementNotOnTheList_StaysInlineWithTheProse() {
        var source = XmlDoc.InClass("/// <summary>See <see cref=\"C\" /> for details.</summary>");
        Assert.Equal(
            ["/// <summary>See <see cref=\"C\" /> for details.</summary>"],
            XmlDoc.DocLines(XmlDoc.Text(source))
        );
    }

    [Fact]
    public void SpaceBeforeSelfClosing_IsHonoured() {
        var source = XmlDoc.InClass("/// <summary>A <see cref=\"C\"/> b.</summary>");
        Assert.Contains("<see cref=\"C\" />", XmlDoc.Text(source), StringComparison.Ordinal);
        Assert.Contains(
            "<see cref=\"C\"/>",
            XmlDoc.Text(source, ("resharper_xmldoc_space_before_self_closing", "false")),
            StringComparison.Ordinal
        );
    }

    [Fact]
    public void MaxBlankLinesBetweenTags_IsHonoured() {
        var source = XmlDoc.InClass(
            "/// <summary>One.</summary>",
            "///",
            "///",
            "/// <remarks>Two.</remarks>"
        );

        Assert.Equal(
            ["/// <summary>One.</summary>", "/// <remarks>Two.</remarks>"],
            XmlDoc.DocLines(XmlDoc.Text(source))
        );

        Assert.Equal(
            ["/// <summary>One.</summary>", "///", "/// <remarks>Two.</remarks>"],
            XmlDoc.DocLines(XmlDoc.Text(source, ("resharper_xmldoc_max_blank_lines_between_tags", "1")))
        );
    }

    [Fact]
    public void IndentChildElements_IndentsAnElementThatHoldsOnlyElements() {
        var source = XmlDoc.InClass("/// <remarks><para>One.</para><para>Two.</para></remarks>");

        Assert.Equal(
            ["/// <remarks>", "///     <para>One.</para>", "///     <para>Two.</para>", "/// </remarks>"],
            XmlDoc.DocLines(XmlDoc.Text(source))
        );

        Assert.Equal(
            ["/// <remarks>", "/// <para>One.</para>", "/// <para>Two.</para>", "/// </remarks>"],
            XmlDoc.DocLines(XmlDoc.Text(source, ("resharper_xmldoc_indent_child_elements", "zero_indent")))
        );
    }

    [Fact]
    public void IndentSize_IsTheXmlDocsOwn() {
        var source = XmlDoc.InClass("/// <remarks><para>One.</para></remarks>");
        Assert.Contains(
            "///   <para>One.</para>",
            XmlDoc.Text(source, ("resharper_xmldoc_indent_size", "2")),
            StringComparison.Ordinal
        );
    }

    [Fact]
    public void LinebreaksInsideTagsForElementsWithChildElements_False_KeepsThemOnOneLine() {
        // ⚠ `<b>` rather than `<para>`: an element the export lists in
        // `linebreak_before_elements` owns its own line whatever this key says, and asserting on one
        // would be asserting that the other key does not work.
        var source = XmlDoc.InClass("/// <remarks><b>One.</b></remarks>");

        Assert.Equal(
            ["/// <remarks>", "///     <b>One.</b>", "/// </remarks>"],
            XmlDoc.DocLines(XmlDoc.Text(source))
        );

        Assert.Equal(
            ["/// <remarks><b>One.</b></remarks>"],
            XmlDoc.DocLines(
                XmlDoc.Text(
                    source,
                    ("resharper_xmldoc_linebreaks_inside_tags_for_elements_with_child_elements", "false")
                )
            )
        );
    }

    [Fact]
    public void SpacesInsideTags_IsHonoured() {
        var source = XmlDoc.InClass("/// <summary>Docs.</summary>");
        Assert.Contains(
            "<summary> Docs. </summary>",
            XmlDoc.Text(source, ("resharper_xmldoc_spaces_inside_tags", "true")),
            StringComparison.Ordinal
        );
    }

    [Fact]
    public void KeepUserLinebreaks_True_NeverJoinsTwoLinesTheAuthorSeparated() {
        var source = XmlDoc.InClass("/// <summary>", "/// One.", "/// Two.", "/// </summary>");
        Assert.Equal(
            ["/// <summary>", "///     One.", "///     Two.", "/// </summary>"],
            XmlDoc.DocLines(XmlDoc.Text(source))
        );
    }

    [Fact]
    public void KeepUserLinebreaks_False_ReflowsTheParagraph() {
        var source = XmlDoc.InClass("/// <summary>", "/// One.", "/// Two.", "/// </summary>");
        Assert.Equal(
            ["/// <summary>One. Two.</summary>"],
            XmlDoc.DocLines(XmlDoc.Text(source, ("resharper_xmldoc_keep_user_linebreaks", "false")))
        );
    }
}

/// <summary>The two hazards docs/plan/05 § "Phase 4" names, and the third the project's own history adds.</summary>
public sealed class XmlDocHazardTests {
    const string Malformed = "class C {\n    /// <summary>Not closed <b>at all.</summary>\n    void M() { }\n}\n";

    [Fact]
    public void AMalformedDocComment_IsByteIdentical_AndReportedAtHint() {
        // ⚠ Hazard 2. Malformed doc comments are extremely common in real code and "fixing" one is
        // worse than ignoring it. Under the flag as much as without it.
        var result = XmlDoc.Run(Malformed);

        Assert.Equal(Malformed, result.Formatted);
        Assert.Contains(
            result.Diagnostics,
            static d => d.Id == FormatDiagnosticIds.MalformedXmlDoc && d.Severity == SkalaSeverity.Hidden
        );
    }

    [Fact]
    public void AMalformedDocComment_IsNotEvenGivenTheMarkerSpace() {
        // The cheapest possible edit is still an edit, and "left exactly as it is" has no exceptions.
        const string source = "class C {\n    ///<summary>Not closed <b>at all.</summary>\n    void M() { }\n}\n";
        Assert.Equal(source, XmlDoc.Text(source));
    }

    [Theory]
    [InlineData("code")]
    [InlineData("c")]
    public void AMarkerInsideAVerbatimElement_Survives(string tag) {
        // ⚠ The regression this file exists for, and the sub-formatter failed it. `<c>///</c>` — a
        // doc comment talking about the marker, which this repository's own sources do in several
        // places — came back as an empty `<c></c>`. `SourceLines` stripped a `///` from the *first*
        // line of the element's body, which is not a line: it starts immediately after the start
        // tag, in the middle of a physical source line, so it never carries the exterior marker.
        //
        // ⚠ The round trip did not catch it, and that is the more important half. `XmlDocSignature`
        // calls the same function, so both sides erased the same three characters and agreed. A
        // self-check can only catch a rewrite that disagrees with its own reading; it cannot catch a
        // reading that is wrong. This is what having no oracle in this area actually costs, and it
        // was found by turning the sub-formatter on over Skala's own sources — 230 files — rather
        // than by any test.
        var source = XmlDoc.InClass(
            "/// <summary>The <" + tag + ">///</" + tag + "> marker, named in prose.</summary>"
        );
        Assert.Contains("<" + tag + ">///</" + tag + ">", XmlDoc.Text(source), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("code")]
    [InlineData("c")]
    public void TextInsideCodeAndC_IsVerbatim(string tag) {
        // ⚠ Hazard 1. Re-wrapping a code sample changes what it says, and the sample is the part of
        // a doc comment a reader is most likely to copy.
        var source = XmlDoc.InClass(
            "/// <summary>",
            "/// <" + tag + ">",
            "///     if (x) {",
            "///         Do( a ,  b );   // two   spaces",
            "///     }",
            "/// </" + tag + ">",
            "/// </summary>"
        );

        var formatted = XmlDoc.Text(source);
        Assert.Contains("///         Do( a ,  b );   // two   spaces", formatted, StringComparison.Ordinal);
        Assert.Contains("///     if (x) {", formatted, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("code")]
    [InlineData("c")]
    public void AVerbatimElementOpenedOntoItsOwnLines_KeepsTheMarkerSpace(string tag) {
        // ⚠ The defect this pair of tests exists for, found by running `skala format` over this
        // repository's own sources: `///Func&lt;int&gt;` — a `///` line with no space after the
        // marker, in a comment whose every other line has one.
        //
        // ⚠ The shape is an inline verbatim element that runs over a line break. Its body's *first*
        // line began immediately after the start tag, so it is the one line of the region that never
        // carried the marker; opening the element up moves it to a line start, and the verbatim path
        // used to hand it to the writer exactly as captured. `space_after_triple_slash` is Tier A and
        // this is a `///` line like any other, so the space belongs there.
        var source = XmlDoc.InClass(
            "/// <remarks>",
            "///     Found by the fuzzer: <" + tag + ">Func&lt;int&gt; v = new () { P = (from",
            "///     item in items select null) };</" + tag + "> makes the binder throw.",
            "/// </remarks>"
        );

        var lines = XmlDoc.DocLines(XmlDoc.Text(source));
        Assert.DoesNotContain(lines, static line => line.StartsWith("///F", StringComparison.Ordinal));
        Assert.Contains("/// Func&lt;int&gt; v = new () { P = (from", lines);

        // ⚠ And the body's own columns are untouched: the second line kept the four spaces it had
        // relative to the marker. That is the half a naive fix loses — see `XmlDocModel.SourceLines`.
        Assert.Contains("///     item in items select null) };", lines);
    }

    [Theory]
    [InlineData("c")]
    [InlineData("code")]
    public void AVerbatimBodyOfThreeLines_KeepsTheMarkerSpaceOnAllOfThem(string tag) {
        // ⚠ The same shape one line longer, and it is a separate fixture because "multi-line" was the
        // gap: the corpus reaches a one-line verbatim body and nothing reached a body that has to stay
        // several lines. The first line is the one that never carried a marker; the rest carry one and
        // must not gain a second.
        var source = XmlDoc.InClass(
            "/// <remarks>",
            "///     Found by the fuzzer: <" + tag + ">var q = from item in items",
            "///     where item.Enabled",
            "///     select item.Name;</" + tag + "> makes the binder throw.",
            "/// </remarks>"
        );

        var lines = XmlDoc.DocLines(XmlDoc.Text(source));
        Assert.Contains("/// var q = from item in items", lines);
        Assert.Contains("///     where item.Enabled", lines);
        Assert.Contains("///     select item.Name;", lines);
        Assert.DoesNotContain(lines, static line => Marks(line));
    }

    [Fact]
    public void TheProseAroundAnInlineVerbatimElement_DoesNotChangeTheAnswer() {
        // ⚠ Reported as a shape that still failed, at prose long enough that the element cannot stay on
        // the line it started on. It is the same construct, and it is pinned because the report was
        // specific: the wrap that moves `<c>` onto a line of its own is a different code path
        // (`Flush`/`EndLine`) from the one that opens it, and "it works at one prose length" is not an
        // answer about the other.
        var source = XmlDoc.InClass(
            "/// <remarks>",
            "///     Some prose that runs on and on and eventually mentions <c>Func&lt;int&gt; v = new () { P = (from",
            "///     item in items select null) };</c> and then keeps going for a while afterwards too.",
            "/// </remarks>"
        );

        var lines = XmlDoc.DocLines(XmlDoc.Text(source));
        Assert.Contains("/// Func&lt;int&gt; v = new () { P = (from", lines);
        Assert.Contains("///     item in items select null) };", lines);
        Assert.DoesNotContain(lines, static line => Marks(line));
    }

    /// <summary>Whether the line is a <c>///</c> line whose marker is followed by something else.</summary>
    /// <remarks>
    ///     ⚠ <c>////</c> is not a documentation comment — Roslyn does not classify one and neither does
    ///     <c>XmlDocFormatter.Indent</c> — so it is excluded rather than counted.
    /// </remarks>
    static bool Marks(string line) =>
        line.StartsWith("///", StringComparison.Ordinal) && line.Length > 3 && line[3] is not (' ' or '/');

    [Fact]
    public void AVerbatimBlockUnderAMarkerlessConvention_ShiftsWholeRatherThanFlattening() {
        // ⚠ The hazard in giving the verbatim path a marker space, and the reason `SourceLines`
        // removes the old one all-or-nothing. Here the author wrote no space after `///`, so the one
        // column `y();` carries *is* the sample's indentation. Removing a leading space line by line
        // would have taken it, and the signature could not have noticed: it calls the same function.
        var source = XmlDoc.InClass(
            "/// <summary>",
            "/// <code>",
            "///if (x) {",
            "/// y();",
            "///}",
            "/// </code>",
            "/// </summary>"
        );

        var lines = XmlDoc.DocLines(XmlDoc.Text(source));
        Assert.Contains("/// if (x) {", lines);
        Assert.Contains("///  y();", lines);
        Assert.Contains("/// }", lines);
    }

    [Fact]
    public void AnInlineMarkerlessBlockThatMustOpen_StillShiftsWholeRatherThanFlattening() {
        // ⚠ The other direction of the multi-line case, and the one that says the fix did not buy the
        // marker space by giving up the columns. This is the ambiguous shape: the region's first body
        // line begins on the start tag and carries no marker, exactly as a line the old defect damaged
        // does — and its second line's single space is the sample's own indentation rather than the
        // marker's. The two are indistinguishable, so nothing may be taken off either, and the whole
        // region moves by one column together. Relative indentation is the invariant, not absolute.
        var source = XmlDoc.InClass(
            "/// <summary>",
            "///     Guard: <code>if (x) {",
            "/// y();",
            "///}</code> is the shape.",
            "/// </summary>"
        );

        var lines = XmlDoc.DocLines(XmlDoc.Text(source));
        Assert.Contains("/// if (x) {", lines);
        Assert.Contains("///  y();", lines);
        Assert.Contains("/// }", lines);
        Assert.DoesNotContain(lines, static line => Marks(line));
    }

    [Fact]
    public void ABlankLineInsideAVerbatimBlock_IsNeitherACrashNorATrailingSpace() {
        // ⚠ Found by running the fix over this repository's own sources before committing it, and it
        // was a crash rather than bad output: a bare `///` inside a `<code>` block has no marker space
        // to take off, and taking one anyway threw out of `XmlDocModel.SourceLines`. It is also the
        // line that would gain a trailing space if the writer applied the marker unconditionally.
        var source = XmlDoc.InClass(
            "/// <summary>",
            "/// <code>",
            "///     var x = 1;",
            "///",
            "///     var y = 2;",
            "/// </code>",
            "/// </summary>"
        );

        var lines = XmlDoc.DocLines(XmlDoc.Text(source));
        Assert.Contains("///     var x = 1;", lines);
        Assert.Contains("///", lines);
        Assert.Contains("///     var y = 2;", lines);
        Assert.DoesNotContain("/// ", lines);
    }

    [Fact]
    public void AVerbatimBlockUnderNoMarkerSpace_KeepsItsColumnsExactly() {
        // ⚠ The mirror, and the reason `space_after_triple_slash` is carried into the model and into
        // the signature rather than assumed true. With the key off nothing is added on the way out, so
        // nothing may be taken off on the way in — a capture that always removed a column would make
        // every one of these comments fail its own round trip and be refused for a reason reserved
        // for defects.
        var source = XmlDoc.InClass(
            "/// <summary>",
            "/// <code>",
            "///     var x = 1;",
            "///     var y = 2;",
            "/// </code>",
            "/// </summary>"
        );

        var formatted = XmlDoc.Text(source, ("resharper_space_after_triple_slash", "false"));
        var lines = XmlDoc.DocLines(formatted);
        Assert.Contains("///     var x = 1;", lines);
        Assert.Contains("///     var y = 2;", lines);

        // Re-flowed, not refused: the key is off, so the marker of every other line lost its space.
        Assert.Contains("///<summary>", lines);
    }

    [Fact]
    public void ALongLineInsideCode_IsNotWrapped() {
        var line = "///     " + string.Join(" ", Enumerable.Repeat("token", 40)) + ";";
        var source = XmlDoc.InClass("/// <example>", "/// <code>", line, "/// </code>", "/// </example>");

        Assert.Contains(line.TrimStart(), XmlDoc.Text(source), StringComparison.Ordinal);
    }

    [Fact]
    public void AnInlineCTagGluedToAWord_IsNotSeparated() {
        // `<c>Foo</c>s` is one word. A formatter that put a space or a line break in it would have
        // rewritten the sentence.
        var source = XmlDoc.InClass("/// <summary>Two <c>Foo</c>s exist.</summary>");
        Assert.Contains("<c>Foo</c>s exist.", XmlDoc.Text(source), StringComparison.Ordinal);
    }

    [Fact]
    public void AnEntity_IsCopiedAsWritten() {
        var source = XmlDoc.InClass("/// <summary>Use &lt;T&gt; and &amp; here.</summary>");
        Assert.Contains("Use &lt;T&gt; and &amp; here.", XmlDoc.Text(source), StringComparison.Ordinal);
    }

    [Fact]
    public void ACrefAttribute_IsCopiedByteForByte() {
        var source = XmlDoc.InClass("/// <summary>See <see cref=\"System.Collections.Generic.List{T}\" />.</summary>");
        Assert.Contains(
            "cref=\"System.Collections.Generic.List{T}\"",
            XmlDoc.Text(source),
            StringComparison.Ordinal
        );
    }

    [Fact]
    public void TheSafetyNet_StillCatchesALostWord() {
        // ⚠ Hazard 3. The allowance the sub-formatter needs is "where the line breaks fall", and it
        // is not one inch wider: the word sequence is still compared in order.
        var one = SourceText.From("class C {\n    /// <summary>One two three.</summary>\n    void M() { }\n}\n");
        var two = SourceText.From("class C {\n    /// <summary>One three.</summary>\n    void M() { }\n}\n");

        Assert.NotNull(TokenEquivalence.Compare(one, two, CSharpFormatter.ParseOptions, xmlDocReflow: true));
    }

    [Fact]
    public void TheSafetyNet_AcceptsOnlyTheRewrap() {
        var one = SourceText.From("class C {\n    /// <summary>One two three.</summary>\n    void M() { }\n}\n");
        var two = SourceText.From(
            "class C {\n    /// <summary>\n    ///     One two three.\n    /// </summary>\n    void M() { }\n}\n"
        );

        Assert.Null(TokenEquivalence.Compare(one, two, CSharpFormatter.ParseOptions, xmlDocReflow: true));

        // ⚠ And with the flag off it is a hard failure, which is what makes the allowance narrow
        // rather than permanent: nothing but the sub-formatter may move a line break in a comment.
        Assert.NotNull(TokenEquivalence.Compare(one, two, CSharpFormatter.ParseOptions));
    }
}

/// <summary>The properties that stand in for an oracle.</summary>
public sealed class XmlDocPropertyTests {
    public static TheoryData<string> Comments {
        get {
            var data = new TheoryData<string>();
            foreach (var comment in new[] {
                         "/// <summary>Docs.</summary>", "///<summary>No space.</summary>",
                         "/// <summary>" + string.Join(" ", Enumerable.Repeat("word", 60)) + "</summary>",
                         "/// <summary>One.</summary><remarks>Two.</remarks>", "/// <summary>A <c>b</c>c d.</summary>",
                         "/// <summary>Use &lt;T&gt;.</summary>",
                         "/// <remarks><para>One.</para><para>Two.</para></remarks>",
                         "/// <summary>\n    /// <code>\n    ///     var x =  1;\n    /// </code>\n    /// </summary>",
                         "/// <summary>Not closed <b>at all.</summary>",
                         "/// <param name=\"a\">A.</param>\n    /// <param name=\"b\">B.</param>",
                         "/// <summary>\n    ///\n    ///\n    /// Text.\n    /// </summary>",
                         "/// <summary>Trailing space. </summary>", "/// <summary><![CDATA[ raw < > & ]]></summary>",
                         "/// <inheritdoc />",
                         "/// Bare prose with no tags at all, running on for a while so that it has to wrap somewhere."
                     }) {
                data.Add(comment);
            }

            return data;
        }
    }

    [Theory]
    [MemberData(nameof(Comments))]
    public void Idempotency_TheSecondPassWantsNoEdits(string comment) {
        var once = XmlDoc.Text(XmlDoc.InClass(comment));
        Assert.Empty(XmlDoc.Run(once).Edits);
    }

    [Theory]
    [MemberData(nameof(Comments))]
    public void TokenEquivalence_HoldsUnderTheFlag(string comment) {
        Assert.NotEqual(FormatOutcome.VerificationFailed, XmlDoc.Run(XmlDoc.InClass(comment)).Outcome);
    }

    [Theory]
    [MemberData(nameof(Comments))]
    public void RoundTrip_TheWordsSurviveInOrder(string comment) {
        // ⚠ The property that replaces the oracle, asserted here on top of the run-time check that
        // already refuses to write a comment which fails it. Asserting it twice is the point: the
        // run-time check is fail-safe and would hide a regression as a silently untouched comment.
        var source = XmlDoc.InClass(comment);
        var before = Words(source);
        Assert.Equal(before, Words(XmlDoc.Text(source)));
    }

    /// <summary>
    ///     Every non-whitespace character of every <c>///</c> line, in order.
    /// </summary>
    /// <remarks>
    ///     ⚠ Deliberately not <see cref="XmlDocSignature" />: checking the round trip with the same
    ///     function the sub-formatter checks it with would prove only that the function agrees with
    ///     itself. This one knows nothing about XML — it cannot be fooled by a tag the model
    ///     misunderstood — and what it cannot see (whitespace) is covered by the hazard fixtures, which
    ///     assert the bytes of a <c>&lt;code&gt;</c> block and the absence of a space inside
    ///     <c>&lt;c&gt;x&lt;/c&gt;s</c>.
    /// </remarks>
    static string Words(string source) =>
        string.Concat(
            source.Split('\n')
                .Select(static line => line.Trim())
                .Where(static line => line.StartsWith("///", StringComparison.Ordinal))
                .SelectMany(static line => line[3..].Where(static c => !char.IsWhiteSpace(c)))
        );
}

/// <summary>
///     The four keys measured against <c>jb cleanupcode</c> with its doc-comment task switched on.
/// </summary>
/// <remarks>
///     ⚠ <b>Every expectation in this class is the oracle's own output, copied off the bytes.</b> That
///     makes it different in kind from the fixtures above it, which assert the semantics JetBrains'
///     settings pages <em>state</em>. These four were measured: a scratch project, the repository's
///     <c>.editorconfig</c>, and a cleanup profile carrying
///     <c>&lt;CSharpFormatDocComments&gt;True&lt;/CSharpFormatDocComments&gt;</c> beside the usual
///     <c>CSReformatCode</c>, at each value of each key.
///     <para>
///         ⚠ They are still not Tier A, and the reason is a fixable one rather than a fact about
///         documentation comments: <c>CSharpFormatDocComments</c> is a real task in this build of the
///         tool and neither committed profile enables it, so no committed <c>.expected.cs</c> can show
///         the oracle doing any of this. Enabling it in <c>OracleProfile</c> would make all twenty-one
///         honoured keys promotable — and would regenerate every fixture in the corpus, which is a
///         reviewed decision in its own commit rather than a side effect of adding four keys.
///     </para>
///     <para>
///         ⚠ Until then, the measurement lives here. A number copied off a tool and written into a test
///         is worth strictly more than the same number reasoned about in a comment, which is what the
///         old refusals for three of these four keys were.
///     </para>
/// </remarks>
public sealed class XmlDocMeasuredTagHeaderTests {
    [Fact]
    public void SpaceAfterLastAttribute_DefaultsToNoSpace_AndAddsOneWhenAsked() {
        var source = XmlDoc.InClass("/// <param name=\"a\" >Text.</param>");

        // ⚠ The default normalises the author's stray space away rather than preserving it. That is
        // the oracle's answer, and it is the half of this key that changes output at its default.
        Assert.Contains("/// <param name=\"a\">Text.</param>", XmlDoc.Text(source), StringComparison.Ordinal);

        Assert.Contains(
            "/// <param name=\"a\" >Text.</param>",
            XmlDoc.Text(source, ("resharper_xmldoc_space_after_last_attribute", "true")),
            StringComparison.Ordinal
        );
    }

    [Fact]
    public void SpaceAfterLastAttribute_TouchesNeitherASelfClosingTagNorABareOne() {
        // ⚠ Measured, because "the gap before the bracket" is one obvious reading and it is wrong on
        // both counts: a self-closing tag's gap belongs to `space_before_self_closing` alone, and a
        // tag with no attributes has no last attribute to follow.
        var formatted = XmlDoc.Text(
            XmlDoc.InClass("/// <summary>Short.</summary>", "/// <remarks><see cref=\"C\" /></remarks>"),
            ("resharper_xmldoc_space_after_last_attribute", "true")
        );

        Assert.Contains("/// <summary>Short.</summary>", formatted, StringComparison.Ordinal);
        Assert.Contains("<see cref=\"C\" />", formatted, StringComparison.Ordinal);
        Assert.DoesNotContain("<summary >", formatted, StringComparison.Ordinal);
    }

    [Fact]
    public void SpacesAroundEqInAttribute_MovesOnlyTheWhitespace() {
        var source = XmlDoc.InClass("/// <param name = \"b\">Text.</param>");

        Assert.Contains("/// <param name=\"b\">Text.</param>", XmlDoc.Text(source), StringComparison.Ordinal);

        Assert.Contains(
            "/// <param name = \"b\">Text.</param>",
            XmlDoc.Text(source, ("resharper_xmldoc_spaces_around_eq_in_attribute", "true")),
            StringComparison.Ordinal
        );
    }

    [Fact]
    public void ATagHeader_IsNormalisedButItsValuesAreNot() {
        // ⚠ The oracle collapses the runs of spaces between attributes and drops the one before
        // `>`, and it leaves the quote character alone: `name='single'` comes back single quoted.
        // Rebuilding a header from a parsed model is only safe because the value — quotes included —
        // is copied rather than re-emitted, and this is the assertion that says so.
        var formatted = XmlDoc.Text(
            XmlDoc.InClass(
                "/// <param name='single'>One.</param>",
                "/// <param   name=\"double\"    other=\"x\"  >Two.</param>"
            )
        );

        Assert.Contains("/// <param name='single'>One.</param>", formatted, StringComparison.Ordinal);
        Assert.Contains("/// <param name=\"double\" other=\"x\">Two.</param>", formatted, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("123456789012", false)]
    [InlineData("1234567890123", true)]
    public void LinebreaksInsideTagsForElementsLongerThan_ComparesTheFlatContentStrictly(
        string content,
        bool opened
    ) {
        // ⚠ At a threshold of 12: twelve characters of content stay on the line and thirteen do not.
        // Both halves are measured. The old refusal said "what ReSharper measures against it is not
        // stated anywhere", which was true of the documentation and never true of the tool.
        var formatted = XmlDoc.Text(
            XmlDoc.InClass("/// <summary>" + content + "</summary>"),
            ("resharper_xmldoc_linebreaks_inside_tags_for_elements_longer_than", "12")
        );

        Assert.Equal(opened, !formatted.Contains("<summary>" + content + "</summary>", StringComparison.Ordinal));
    }

    [Fact]
    public void LinebreaksInsideTagsForElementsLongerThan_ReadsZeroAsAlways() {
        // ⚠ The registry's bounds note said 0 meant "never". It means "always": the comparison is
        // strictly greater, so every non-empty content crosses 0. "Never" is the export's own
        // int.MaxValue, which is exactly why this key looked like it could not be pinned.
        var formatted = XmlDoc.Text(
            XmlDoc.InClass("/// <summary>ab</summary>"),
            ("resharper_xmldoc_linebreaks_inside_tags_for_elements_longer_than", "0")
        );

        Assert.DoesNotContain("<summary>ab</summary>", formatted, StringComparison.Ordinal);
        Assert.Contains("<summary>", formatted, StringComparison.Ordinal);
    }

    /// <summary>
    ///     ⚠ The marker space <em>is</em> on a processing instruction's own line, and it was not.
    /// </summary>
    /// <remarks>
    ///     This constant used to read <c>///&lt;?display …?&gt;</c> and carried a note saying so: the
    ///     verbatim path did not re-apply the marker space, which was right for a <c>&lt;code&gt;</c>
    ///     block's columns and wrong for the marker, and the note asked to be read the moment somebody
    ///     fixed it. That is SK-DIV-0023's first half, and it is fixed —
    ///     <c>XmlDocModel.SourceLines</c> takes the marker space off on the way in, so
    ///     <c>XmlDocFormatter</c> can write it back on every line without a code block gaining a column.
    ///     The oracle's own <c>constructs/xmldoc/resharper_xmldoc_blank_line_after_pi</c> fixture is what
    ///     says <c>/// &lt;?…?&gt;</c> is the right answer; what still diverges there is the trailing
    ///     space on the blank line after it, which is the entry's second half and a decision.
    /// </remarks>
    const string Pi = "/// <?display mode=\"short\"?>";

    [Fact]
    public void BlankLineAfterPi_IsOnByDefault() {
        // ⚠ A default-true key the sub-formatter had never performed, invisible for as long as the
        // key coverage test excluded the processing-instruction family from its own partition.
        var lines = XmlDoc.DocLines(
            XmlDoc.Text(XmlDoc.InClass("/// <?display mode=\"short\"?>", "/// <summary>After.</summary>"))
        );

        Assert.Equal([Pi, "///", "/// <summary>After.</summary>"], lines);
    }

    [Fact]
    public void BlankLineAfterPi_IsSuppressedWhenTurnedOff() {
        var lines = XmlDoc.DocLines(
            XmlDoc.Text(
                XmlDoc.InClass("/// <?display mode=\"short\"?>", "/// <summary>After.</summary>"),
                ("resharper_xmldoc_blank_line_after_pi", "false")
            )
        );

        Assert.Equal([Pi, "/// <summary>After.</summary>"], lines);
    }

    [Fact]
    public void ATrailingProcessingInstruction_DoesNotLeaveADanglingBlankLine() {
        // ⚠ `Render` trims trailing blank lines, so the last thing in a comment being a processing
        // instruction does not end it on a bare `///`. Asserted because the blank line is emitted
        // unconditionally and this is the one place that has to take it back.
        var lines = XmlDoc.DocLines(
            XmlDoc.Text(XmlDoc.InClass("/// <summary>Before.</summary>", "/// <?display mode=\"short\"?>"))
        );

        Assert.Equal(["/// <summary>Before.</summary>", Pi], lines);
    }
}

/// <summary>The count in the milestone notes, checked against the registry rather than remembered.</summary>
/// <remarks>
///     ⚠ "Twenty-one of thirty-two honoured, eleven refused" is a claim about this repository's option
///     registry, and a claim like that rots the moment somebody adds a key. It is asserted here so that
///     adding a <c>resharper_xmldoc_*</c> key to <c>options.json</c> fails the build until somebody has
///     decided whether the sub-formatter honours it or refuses it, and has written down which.
/// </remarks>
public sealed class XmlDocKeyCoverageTests {
    /// <summary>
    ///     The family: every <c>resharper_xmldoc_*</c> key in the registry, with nothing excluded.
    /// </summary>
    /// <remarks>
    ///     ⚠ The five processing-instruction keys used to be dropped from the count on the grounds that
    ///     "a processing instruction in a C# documentation comment is not a thing that occurs". It is —
    ///     Roslyn parses one, and the oracle acts on one: with <c>CSharpFormatDocComments</c> enabled,
    ///     <c>resharper_xmldoc_blank_line_after_pi</c> puts a blank line after
    ///     <c>&lt;?xml-stylesheet …?&gt;</c> at its default <c>true</c>, which is behaviour Skala was
    ///     missing on the default path for as long as the exclusion stood.
    ///     <para>
    ///         ⚠ An exclusion is a claim, and this one hid a key. Five keys carried no decision at all
    ///         because the partition they would have failed did not look at them; one of the five turned out
    ///         to be implementable. The family is now the whole family, and the four that remain are in
    ///         <c>XmlDocIds.Refused</c> with reasons like every other undone key.
    ///     </para>
    /// </remarks>
    static IEnumerable<string> Family =>
        OptionRegistry.All
            .Select(static info => info.Key)
            .Where(static key => key.StartsWith("resharper_xmldoc_", StringComparison.Ordinal));

    [Fact]
    public void HonouredAndRefused_PartitionTheFamilyExactly() {
        var honoured = XmlDocIds.Honoured
            .Select(static id => OptionRegistry.Get(id).Key)
            .Where(static key => key.StartsWith("resharper_xmldoc_", StringComparison.Ordinal))
            .ToHashSet(StringComparer.Ordinal);
        var refused = XmlDocIds.Refused.Select(static pair => pair.Key).ToHashSet(StringComparer.Ordinal);
        var family = Family.ToHashSet(StringComparer.Ordinal);
        var covered = honoured.Union(refused, StringComparer.Ordinal).ToHashSet(StringComparer.Ordinal);

        Assert.Empty(honoured.Intersect(refused, StringComparer.Ordinal));
        Assert.Empty(family.Except(covered, StringComparer.Ordinal));
        Assert.Empty(covered.Except(family, StringComparer.Ordinal));

        Assert.Equal(32, family.Count);
        Assert.Equal(21, honoured.Count);
        Assert.Equal(11, refused.Count);
    }

    [Fact]
    public void EveryRefusal_CarriesAReason() {
        Assert.All(
            XmlDocIds.Refused,
            static pair => Assert.True(
                pair.Value.Length > 40,
                pair.Key + " is refused with a reason too short to be one."
            )
        );
    }

    /// <summary>
    ///     ⚠ This assertion used to say the opposite, and the opposite was wrong.
    /// </summary>
    /// <remarks>
    ///     It was <c>NothingTheSubFormatterReads_ClaimsTierA</c>: every key the sub-formatter honours
    ///     had to be Tier D and out of <see cref="PhaseOneOptions.Implemented" />, "because the oracle
    ///     has nothing to say about any of these". The oracle has plenty to say —
    ///     <c>OracleProfile.DocComments</c> asks it — and <c>constructs/xmldoc/</c> is what it said. The
    ///     family splits 13 / 9, and both halves are asserted here so that neither can drift silently:
    ///     a promoted key that stops agreeing fails <c>XmlDocOracleTests</c>, and a promoted key that
    ///     loses its <c>Of</c> registration fails this.
    ///     <para>
    ///         ⚠ <c>space_after_triple_slash</c> is checked alongside them even though it is not a
    ///         <c>resharper_xmldoc_*</c> key. It is the one that has been Tier A, then inert, then
    ///         unoracled, and now Tier A again; a key with that history is the one worth pinning by name.
    ///     </para>
    /// </remarks>
    [Fact]
    public void TheSubFormattersKeys_SplitIntoTheTiersTheOracleMeasured() {
        var implemented = PhaseOneOptions.Implemented.ToHashSet();
        var unoracled = Ids.ReadButUnoracled.ToHashSet();
        var unsubstantiated = Rikarin.Skala.Testing.SweepVerdicts.Unsubstantiated();

        foreach (var id in XmlDocIds.Honoured.Add(XmlDocIds.SpaceAfterTripleSlash)) {
            var info = OptionRegistry.Get(id);
            if (implemented.Contains(id)) {
                // ⚠ Implemented is a weaker claim than Tier A, and this test conflated them. A key the
                // sub-formatter reads, whose doc-comment fixture it reproduces, can still diverge at a
                // value the fixture does not reach — six of these did, and the sweep found them. That is
                // the same correction OptionCoverageTests already carries for the C# family.
                Assert.Equal(
                    unsubstantiated.Contains(info.Key) ? OptionTier.D : OptionTier.A,
                    info.Tier
                );

                Assert.False(
                    unoracled.Contains(id),
                    info.Key + " is both implemented and unoracled, which are the two halves of a partition."
                );

                Assert.True(
                    info.Oracle is { Length: > 0 },
                    info.Key
                    + " is implemented and carries no `oracle` glob; both tiers here rest on fixture evidence."
                );

                continue;
            }

            Assert.Equal(OptionTier.D, info.Tier);
            Assert.Contains(id, unoracled);
        }

        // ⚠ The split the oracle measured: 13 of the 22 reproduce their doc-comment fixture and 9 do
        // not. That is a statement about the fixtures and it has not changed. What the sweep changed is
        // how many of the 13 may *claim Tier A* — six of them diverge at a value the fixture does not
        // reach — so the tier count is asserted separately and against the sweep rather than baked in.
        Assert.Equal(
            13,
            XmlDocIds.Honoured.Add(XmlDocIds.SpaceAfterTripleSlash).Count(implemented.Contains)
        );

        Assert.Equal(9, XmlDocIds.Honoured.Count(unoracled.Contains));

        Assert.Equal(
            XmlDocIds.Honoured.Add(XmlDocIds.SpaceAfterTripleSlash)
                .Count(id => implemented.Contains(id) && !unsubstantiated.Contains(OptionRegistry.Get(id).Key)),
            XmlDocIds.Honoured.Add(XmlDocIds.SpaceAfterTripleSlash)
                .Count(id => OptionRegistry.Get(id).Tier == OptionTier.A)
        );
    }
}
