using System.Collections.Immutable;
using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Core.Configuration;
using Rikarin.Skala.Core.Diagnostics;
using Rikarin.Skala.Formatting.CSharp;
using Rikarin.Skala.Options;

namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>Runs the pipeline, which formats documentation comments by default.</summary>
/// <remarks>
///     ⚠ These fixtures assert <b>the semantics JetBrains' settings pages state</b>, not the oracle's
///     behaviour, and the difference is the whole of SK-DIV-0006: the committed <c>.expected.cs</c>
///     fixtures were produced by a profile that does not run ReSharper's own
///     <c>CSharpFormatDocComments</c> task, so every one of them returns its doc comments exactly as
///     written and none of them can pin any of this. Every other option in Skala is pinned the other
///     way. That is why none of these keys is Tier A — not because the behaviour is optional.
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

/// <summary>The count in the milestone notes, checked against the registry rather than remembered.</summary>
/// <remarks>
///     ⚠ "Seventeen of twenty-seven honoured, ten refused" is a claim about this repository's option
///     registry, and a claim like that rots the moment somebody adds a key. It is asserted here so that
///     adding a <c>resharper_xmldoc_*</c> key to <c>options.json</c> fails the build until somebody has
///     decided whether the sub-formatter honours it or refuses it, and has written down which.
/// </remarks>
public sealed class XmlDocKeyCoverageTests {
    /// <summary>
    ///     The family the milestone counted: every <c>resharper_xmldoc_*</c> key that is not about
    ///     processing instructions.
    /// </summary>
    /// <remarks>
    ///     ⚠ The <c>_pi_</c> keys are out of the count rather than refused. A processing instruction in
    ///     a C# documentation comment is not a thing that occurs, and counting five keys as "refused"
    ///     that govern a construct the language does not put there would inflate both halves.
    /// </remarks>
    static IEnumerable<string> Family =>
        OptionRegistry.All
            .Select(static info => info.Key)
            .Where(static key => key.StartsWith("resharper_xmldoc_", StringComparison.Ordinal))
            .Where(static key => !key.Contains("_pi_", StringComparison.Ordinal))
            .Where(static key => !key.EndsWith("_after_pi", StringComparison.Ordinal));

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

        Assert.Equal(27, family.Count);
        Assert.Equal(17, honoured.Count);
        Assert.Equal(10, refused.Count);
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

    [Fact]
    public void NothingTheSubFormatterReads_ClaimsTierA() {
        // The whole argument of SK-DIV-0006 in one assertion. Tier A means "pinned by an oracle
        // fixture", the oracle has nothing to say about any of these, and so any of them appearing
        // in PhaseOneOptions.Implemented would be a claim the corpus cannot support.
        var implemented = PhaseOneOptions.Implemented.ToHashSet();

        // ⚠ `space_after_triple_slash` is checked alongside the seventeen even though it is not one
        // of them. It is the key milestone 3 *demoted* from Tier A after measuring what inserting
        // the space cost, and a milestone that gives it a use is exactly when it would creep back.
        foreach (var id in XmlDocIds.Honoured.Add(XmlDocIds.SpaceAfterTripleSlash)) {
            Assert.DoesNotContain(id, implemented);
            Assert.Equal(OptionTier.D, OptionRegistry.Get(id).Tier);
        }
    }
}
