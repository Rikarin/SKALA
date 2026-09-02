using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Rikarin.Skala.Rules.Cleanup;
using Rikarin.Skala.Rules.Correctness;
using System.Collections.Immutable;

namespace Rikarin.Skala.Rules.Tests;

/// <summary>
///     The <c>SK024x</c> cleanup family, one assertion per <em>reported message</em>.
/// </summary>
/// <remarks>
///     ⚠ <see cref="RuleFixtureTests" /> asks only whether the rule fired, which for a rule covering
///     several ReSharper inspections under one id is not enough: a rule matching four shapes and tested
///     for one has three shapes nothing is holding. Every shape here names the sentence it produces, so
///     breaking one branch fails the assertion that describes it rather than an anonymous count.
/// </remarks>
public sealed class CleanupBatchTests {
    static readonly ImmutableArray<DiagnosticAnalyzer> Analyzers = [
        new RedundantControlFlowAnalyzer(), new IneffectiveModifierAnalyzer(),
        new RedundantNullableDirectiveAnalyzer(), new RedundantQualifierAnalyzer(),
        new RedundantDeclarationAnalyzer(),
    ];

    static readonly string[] Ids = ["SK0240", "SK0241", "SK0242", "SK0243", "SK0244"];

    public static TheoryData<RuleFixture> Fixtures {
        get {
            var data = new TheoryData<RuleFixture>();
            foreach (var fixture in RuleFixtures.All()
                         .Where(static fixture => Ids.Contains(fixture.RuleId, StringComparer.Ordinal))) {
                data.Add(fixture);
            }

            return data;
        }
    }

    /// <summary>
    ///     ⚠ Exactly one finding on a positive fixture and exactly none on a negative one.
    /// </summary>
    /// <remarks>
    ///     The fixture harness asserts "at least one" for a positive, which a rule that reports the same
    ///     redundancy twice — once for the clause and once for the statement inside it — satisfies while
    ///     producing a duplicate in every report and two overlapping edits for <c>skala fix</c>.
    /// </remarks>
    [Theory]
    [MemberData(nameof(Fixtures))]
    public void EveryFixture_ProducesTheExactCount(RuleFixture fixture) {
        var findings = Findings(fixture.Path, fixture.RuleId);
        Assert.Equal(fixture.ShouldFire ? 1 : 0, findings.Length);
        Assert.All(findings, static diagnostic => Assert.True(diagnostic.Properties.ContainsKey(FixEdits.CountKey)));
    }

    /// <summary>Each of <c>SK0240</c>'s five shapes, named by the sentence it reports.</summary>
    [Theory]
    [InlineData("catch_sole", "only rethrows")]
    [InlineData("catch_after_another", "only rethrows")]
    [InlineData("catch_with_finally", "only rethrows")]
    [InlineData("catch_general", "only rethrows")]
    [InlineData("continue_for", "control reaches the next iteration")]
    [InlineData("continue_foreach", "control reaches the next iteration")]
    [InlineData("return_void_method", "returns nothing")]
    [InlineData("return_setter", "returns nothing")]
    [InlineData("return_constructor", "returns nothing")]
    [InlineData("default_only_breaks", "`default:` section only breaks")]
    [InlineData("finally_empty_beside_a_catch", "guarantees that nothing happens")]
    [InlineData("finally_empty_sole", "guarantees that nothing happens")]
    [InlineData("case_label_shares_a_default_section", "shares its section with `default:`")]
    [InlineData("default_section_with_extra_labels_only_breaks", "`default:` section only breaks")]
    // ⚠ #302. Both of these carry a comment on the line *above* the construct, which the fix's span
    // begins after. They are positives precisely because the guard that used to withdraw them was
    // protecting text the fix does not touch.
    [InlineData("catch_with_a_comment_above_it", "only rethrows")]
    [InlineData("default_with_a_comment_above_it", "`default:` section only breaks")]
    // ⚠ One finding for a `try` carrying both shapes. Reporting them separately gives `skala fix`
    // two edits whose composition is CS1524, and reporting one per pass leaves a finding standing on
    // the fix's own output.
    [InlineData("finally_empty_beside_a_rethrowing_catch", "only rethrows")]
    public void SK0240_ReportsTheShapeItMatched(string name, string sentence) {
        var finding = Assert.Single(
            Findings(Path.Combine(RuleFixtures.Root, "SK0240", "positive", name + ".cs"), "SK0240")
        );

        Assert.Contains(sentence, finding.GetMessage(), StringComparison.Ordinal);
    }

    /// <summary>
    ///     ⚠ The two fixes are different edits and the choice between them is what the rule decides.
    /// </summary>
    /// <remarks>
    ///     Where a <c>finally</c> or another <c>catch</c> survives, the clause alone is deleted. Where
    ///     the rethrowing clause is the only one, <c>try { … }</c> would be CS1524, so the whole
    ///     statement is replaced by its block's contents — and asserting only "a fix exists" would let
    ///     the wrong one through, which is text that does not compile rather than a worse suggestion.
    /// </remarks>
    [Theory]
    [InlineData("catch_sole", "Write(path, payload);")]
    [InlineData("catch_general", "Run();")]
    public void SK0240_UnwrapsTheTryWhenTheRethrowIsTheOnlyClause(string name, string kept) {
        var path = Path.Combine(RuleFixtures.Root, "SK0240", "positive", name + ".cs");
        var after = Apply(File.ReadAllText(path), Findings(path, "SK0240"));

        Assert.DoesNotContain("try", after, StringComparison.Ordinal);
        Assert.DoesNotContain("catch", after, StringComparison.Ordinal);
        Assert.Contains(kept, after, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("catch_after_another")]
    [InlineData("catch_with_finally")]
    public void SK0240_KeepsTheTryWhenSomethingElseSurvives(string name) {
        var path = Path.Combine(RuleFixtures.Root, "SK0240", "positive", name + ".cs");
        var after = Apply(File.ReadAllText(path), Findings(path, "SK0240"));

        Assert.Contains("try {", after, StringComparison.Ordinal);
        Assert.DoesNotContain("throw;", after, StringComparison.Ordinal);
    }

    /// <summary>
    ///     ⚠ The empty <c>finally</c> takes the same two fixes as the rethrowing <c>catch</c>, for the
    ///     same reason: <c>try { … }</c> on its own is CS1524.
    /// </summary>
    [Fact]
    public void SK0240_DeletesTheEmptyFinallyWhenACatchSurvives() {
        var path = Path.Combine(RuleFixtures.Root, "SK0240", "positive", "finally_empty_beside_a_catch.cs");
        var after = CodeOnly(Apply(File.ReadAllText(path), Findings(path, "SK0240")));

        Assert.Contains("try {", after, StringComparison.Ordinal);
        Assert.Contains("catch (InvalidOperationException e)", after, StringComparison.Ordinal);
        Assert.DoesNotContain("finally", after, StringComparison.Ordinal);
    }

    /// <summary>
    ///     ⚠ A <c>try</c> matching both shapes is one finding whose single edit is the unwrap.
    /// </summary>
    /// <remarks>
    ///     Deleting the rethrowing <c>catch</c> and the empty <c>finally</c> as two edits composes
    ///     into <c>try { Run(); }</c>, which is CS1524. Neither clause would leave anything standing,
    ///     so the whole statement is replaced by its block's contents instead — asserted here because
    ///     "a fix exists" and "the right fix exists" are otherwise the same green.
    /// </remarks>
    [Fact]
    public void SK0240_UnwrapsTheTryWhenTheCatchAndTheFinallyAreBothInert() {
        var path = Path.Combine(
            RuleFixtures.Root,
            "SK0240",
            "positive",
            "finally_empty_beside_a_rethrowing_catch.cs"
        );

        var findings = Findings(path, "SK0240");
        Assert.Single(findings);

        var after = CodeOnly(Apply(File.ReadAllText(path), findings));
        Assert.DoesNotContain("try", after, StringComparison.Ordinal);
        Assert.DoesNotContain("catch", after, StringComparison.Ordinal);
        Assert.DoesNotContain("finally", after, StringComparison.Ordinal);
        Assert.Contains("Run();", after, StringComparison.Ordinal);
    }

    /// <summary>
    ///     ⚠ The whole section, extra labels and all, so the fix's output carries no second finding.
    /// </summary>
    [Fact]
    public void SK0240_DeletesTheWholeSectionWhenItOnlyBreaks() {
        var path = Path.Combine(
            RuleFixtures.Root,
            "SK0240",
            "positive",
            "default_section_with_extra_labels_only_breaks.cs"
        );

        var after = CodeOnly(Apply(File.ReadAllText(path), Findings(path, "SK0240")));

        Assert.DoesNotContain("case 2:", after, StringComparison.Ordinal);
        Assert.DoesNotContain("default:", after, StringComparison.Ordinal);
        Assert.Contains("case 1:", after, StringComparison.Ordinal);
    }

    [Fact]
    public void SK0240_UnwrapsTheTryWhenTheEmptyFinallyIsTheOnlyClause() {
        var path = Path.Combine(RuleFixtures.Root, "SK0240", "positive", "finally_empty_sole.cs");
        var after = CodeOnly(Apply(File.ReadAllText(path), Findings(path, "SK0240")));

        Assert.DoesNotContain("try", after, StringComparison.Ordinal);
        Assert.DoesNotContain("finally", after, StringComparison.Ordinal);
        Assert.Contains("Run();", after, StringComparison.Ordinal);
    }

    /// <summary>
    ///     ⚠ The label goes and the section it shared stays, because the section is what still runs.
    /// </summary>
    [Fact]
    public void SK0240_DeletesOnlyTheCaseLabelAndKeepsTheDefaultSection() {
        var path = Path.Combine(
            RuleFixtures.Root,
            "SK0240",
            "positive",
            "case_label_shares_a_default_section.cs"
        );

        var after = CodeOnly(Apply(File.ReadAllText(path), Findings(path, "SK0240")));

        Assert.DoesNotContain("case 2:", after, StringComparison.Ordinal);
        Assert.Contains("default:", after, StringComparison.Ordinal);
        Assert.Contains("case 1:", after, StringComparison.Ordinal);
    }

    /// <summary>
    ///     ⚠ #302, as an assertion rather than a note: the comment the fix does not delete is still
    ///     there after the fix, and the finding was made anyway.
    /// </summary>
    [Theory]
    [InlineData("catch_with_a_comment_above_it", "// Reviewed 2026-02: nothing to add here yet.")]
    [InlineData("default_with_a_comment_above_it", "// Everything else is handled by the outer dispatcher.")]
    public void SK0240_FiresOverACommentItDoesNotDelete(string name, string comment) {
        var path = Path.Combine(RuleFixtures.Root, "SK0240", "positive", name + ".cs");
        var after = Apply(File.ReadAllText(path), Findings(path, "SK0240"));

        Assert.Contains(comment, after, StringComparison.Ordinal);
    }

    /// <summary>Each of <c>SK0241</c>'s five keywords, named by the sentence it reports.</summary>
    [Theory]
    [InlineData("abstract_interface_method", "an interface member is abstract")]
    [InlineData("abstract_interface_property", "an interface member is abstract")]
    [InlineData("sealed_member_in_sealed_class", "the containing type is `sealed`")]
    [InlineData("sealed_member_in_sealed_record", "the containing type is `sealed`")]
    [InlineData("record_class_keyword", "`record` already means `record class`")]
    [InlineData("enum_underlying_int", "the underlying type an enum has when none is written")]
    [InlineData("readonly_method_in_readonly_struct", "`readonly struct` is already `readonly`")]
    [InlineData("readonly_property_in_readonly_struct", "`readonly struct` is already `readonly`")]
    [InlineData("readonly_accessor_in_readonly_struct", "`readonly struct` is already `readonly`")]
    public void SK0241_ReportsTheKeywordItMatched(string name, string sentence) {
        var finding = Assert.Single(
            Findings(Path.Combine(RuleFixtures.Root, "SK0241", "positive", name + ".cs"), "SK0241")
        );

        Assert.Contains(sentence, finding.GetMessage(), StringComparison.Ordinal);
    }

    /// <summary>
    ///     ⚠ The fix takes the space after the keyword and never the trivia in front of it.
    /// </summary>
    /// <remarks>
    ///     A documentation comment and an attribute list are leading trivia on the first modifier, so a
    ///     deletion that started at the token's <em>full</em> span would take them with it — silently,
    ///     under a fix the catalogue marks safe.
    /// </remarks>
    [Fact]
    public void SK0241_KeepsTheDocumentationCommentAboveTheModifier() {
        const string source = """
                              class Base {
                                  public virtual void Flush() { }
                              }

                              sealed class Writer : Base {
                                  /// <summary>Flushes nothing, on purpose.</summary>
                                  [System.Obsolete("use Close")]
                                  public sealed override void Flush() { }
                              }
                              """;

        var compilation = RuleFixtures.Compile(source, "modifier.cs");
        var findings = RuleFixtures
            .Analyze(compilation, Analyzers, TestContext.Current.CancellationToken)
            .Where(static diagnostic => diagnostic.Id == "SK0241")
            .ToArray();

        var after = Apply(source, findings);
        Assert.Contains("<summary>Flushes nothing, on purpose.</summary>", after, StringComparison.Ordinal);
        Assert.Contains("[System.Obsolete(\"use Close\")]", after, StringComparison.Ordinal);
        Assert.Contains("public override void Flush()", after, StringComparison.Ordinal);
    }

    /// <summary>
    ///     <c>SK0242</c>'s two sentences: a restatement, and a <c>restore</c> that restores nothing.
    /// </summary>
    [Theory]
    [InlineData("restore_at_file_start", "restores the project default the file already had")]
    [InlineData("restore_after_restore", "restores the project default the file already had")]
    [InlineData("enable_twice", "the one already in effect")]
    [InlineData("disable_twice", "the one already in effect")]
    [InlineData("enable_then_enable_annotations", "the one already in effect")]
    [InlineData("disable_warnings_twice", "the one already in effect")]
    public void SK0242_ReportsWhichKindOfNoOpItFound(string name, string sentence) {
        var finding = Assert.Single(
            Findings(Path.Combine(RuleFixtures.Root, "SK0242", "positive", name + ".cs"), "SK0242")
        );

        Assert.Contains(sentence, finding.GetMessage(), StringComparison.Ordinal);
    }

    /// <summary>
    ///     ⚠ The fix takes the whole line, leaving no blank <c>#</c> and no stray indentation.
    /// </summary>
    [Fact]
    public void SK0242_DeletesTheEntireDirectiveLine() {
        var path = Path.Combine(RuleFixtures.Root, "SK0242", "positive", "enable_twice.cs");
        var source = File.ReadAllText(path);
        var after = Apply(source, Findings(path, "SK0242"));

        Assert.Equal(2, source.Split("#nullable enable").Length - 1);
        Assert.Equal(1, after.Split("#nullable enable").Length - 1);
        Assert.DoesNotContain("#\n", after, StringComparison.Ordinal);
    }

    /// <summary><c>SK0243</c>'s two halves, named by the sentence each reports.</summary>
    [Theory]
    [InlineData("qualified_field_type", "already binds to this type here")]
    [InlineData("qualified_return_type", "already binds to this type here")]
    [InlineData("qualified_base_list", "already binds to this type here")]
    [InlineData("qualified_nested_type", "already binds to this type here")]
    [InlineData("qualified_generic_argument", "already binds to this type here")]
    [InlineData("base_call_to_a_non_virtual_member", "reaches the same member as no qualifier")]
    [InlineData("base_call_in_a_sealed_type", "reaches the same member as no qualifier")]
    [InlineData("base_property_access", "reaches the same member as no qualifier")]
    public void SK0243_ReportsWhichQualifierItMatched(string name, string sentence) {
        var finding = Assert.Single(
            Findings(Path.Combine(RuleFixtures.Root, "SK0243", "positive", name + ".cs"), "SK0243")
        );

        Assert.Contains(sentence, finding.GetMessage(), StringComparison.Ordinal);
    }

    /// <summary>Each of <c>SK0244</c>'s six declarations, named by the sentence it reports.</summary>
    [Theory]
    [InlineData("empty_finalizer", "an empty finalizer is not free")]
    [InlineData("empty_public_constructor", "the parameterless constructor is empty and is the only one")]
    [InlineData(
        "empty_protected_constructor_on_an_abstract_class",
        "the parameterless constructor is empty and is the only one"
    )]
    [InlineData("empty_constructor_with_a_base_call", "the parameterless constructor is empty and is the only one")]
    [InlineData("empty_namespace", "the namespace declares nothing")]
    [InlineData("redundant_base_call", "the constructor initializer the compiler supplies")]
    [InlineData("default_int_field", "a field is zero-initialized before any code runs")]
    [InlineData("default_bool_field", "a field is zero-initialized before any code runs")]
    [InlineData("default_nullable_field", "a field is zero-initialized before any code runs")]
    [InlineData("default_expression_field", "a field is zero-initialized before any code runs")]
    [InlineData("default_auto_property", "property's storage is zero-initialized")]
    [InlineData("override_forwards_to_base", "does nothing but call the base implementation")]
    [InlineData("override_forwards_from_a_block", "does nothing but call the base implementation")]
    public void SK0244_ReportsWhichDeclarationItMatched(string name, string sentence) {
        var finding = Assert.Single(
            Findings(Path.Combine(RuleFixtures.Root, "SK0244", "positive", name + ".cs"), "SK0244")
        );

        Assert.Contains(sentence, finding.GetMessage(), StringComparison.Ordinal);
    }

    /// <summary>
    ///     ⚠ The empty finalizer's message names the runtime cost, because that is the whole reason it
    ///     stays in a rule about redundancy instead of moving to the performance range.
    /// </summary>
    [Fact]
    public void SK0244_SaysWhatAnEmptyFinalizerCosts() {
        var finding = Assert.Single(
            Findings(Path.Combine(RuleFixtures.Root, "SK0244", "positive", "empty_finalizer.cs"), "SK0244")
        );

        Assert.Contains("finalization", finding.GetMessage(), StringComparison.Ordinal);
        Assert.Contains("a generation later", finding.GetMessage(), StringComparison.Ordinal);
    }

    /// <summary>
    ///     ⚠ Every negative fixture in this family passes if the analyzer <em>crashed</em>, so the
    ///     crash is asserted against before the count is.
    /// </summary>
    /// <remarks>
    ///     Roslyn turns an exception out of an analyzer into <c>AD0001</c> and carries on, which for a
    ///     "should not fire" fixture is indistinguishable from the rule correctly declining — a whole
    ///     negative set goes green while the rule does nothing at all. The fixture harness does not
    ///     look (#279), so this one does: <c>AD0001</c> is a failure of the run rather than a finding
    ///     about the file.
    /// </remarks>
    /// <summary>
    ///     ⚠ <c>SK0240</c> and <c>SK2009</c> read an empty <c>default:</c> section in opposite
    ///     directions, and this is the assertion that they no longer hand work to each other ([#321]).
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         Both directions in one test, because neither is visible alone. <see cref="Analyzers" />
    ///         holds only the <c>SK024x</c> family and <see cref="RuleFixtureTests" />'s round-trip asks
    ///         only whether <em>the same</em> rule still fires after its own fix — so a fix that creates
    ///         a <em>different</em> rule's finding passes every test this project had. The #321 batch
    ///         measured itself as "9 errors cleared" while quietly adding a tenth, and only a set-diff
    ///         of the before/after SARIF caught it.
    ///     </para>
    ///     <para>
    ///         ⚠ The "after" text is computed here by deleting the <c>default:</c> section rather than
    ///         read from the second fixture, and then <em>compared</em> against it. Reading it would
    ///         let the two fixtures drift into two unrelated files that each pass their own half.
    ///     </para>
    /// </remarks>
    [Fact]
    public void SK0240AndSK2009_DoNotHandTheEnumSwitchBackAndForth() {
        var kept = Path.Combine(
            RuleFixtures.Root,
            "SK0240",
            "negative",
            "default_legitimises_a_nonexhaustive_enum_switch.cs"
        );
        var source = File.ReadAllText(kept);

        // Direction one: as written, the section keeps SK2009 quiet and SK0240 stands down for it.
        Assert.Empty(Interacting(source, kept, "SK0240"));
        Assert.Empty(Interacting(source, kept, "SK2009"));

        // Direction two: SK0240's fix was to delete that section, and this is what it left behind.
        var section = CSharpSyntaxTree.ParseText(source, cancellationToken: TestContext.Current.CancellationToken)
            .GetRoot(TestContext.Current.CancellationToken)
            .DescendantNodes()
            .OfType<SwitchSectionSyntax>()
            .Single(static candidate => candidate.Labels.Any(static label => label is DefaultSwitchLabelSyntax));
        var after = source[..section.Span.Start] + source[section.Span.End..];

        var reported = Assert.Single(Interacting(after, kept, "SK2009"));
        Assert.Contains("omits `Fill`, `IfBroken`", reported.GetMessage(), StringComparison.Ordinal);
        Assert.Empty(Interacting(after, kept, "SK0240"));

        // ⚠ And the second fixture really is that text, so the pair cannot drift apart.
        var fixture = File.ReadAllText(
            Path.Combine(RuleFixtures.Root, "SK2009", "positive", "sk0240_fix_output_omits_members.cs")
        );

        Assert.Equal(Squashed(CodeOnly(after)), Squashed(CodeOnly(fixture)));
    }

    /// <summary>
    ///     ⚠ The stand-down is narrow: an <em>exhaustive</em> enum switch's empty <c>default:</c> is
    ///     still reported, and taking the fix still introduces no <c>SK2009</c>.
    /// </summary>
    /// <remarks>
    ///     Without this half, "<c>SK0240</c> declines an empty default on an enum switch" and
    ///     "<c>SK0240</c> declines an empty default" are the same green run, and the second is a rule
    ///     two thirds switched off.
    /// </remarks>
    [Fact]
    public void SK0240_StillDeletesAnEmptyDefaultWhenTheEnumSwitchIsExhaustive() {
        var path = Path.Combine(
            RuleFixtures.Root,
            "SK0240",
            "positive",
            "default_only_breaks_on_an_exhaustive_enum_switch.cs"
        );
        var source = File.ReadAllText(path);

        var finding = Assert.Single(Interacting(source, path, "SK0240"));
        var after = Apply(source, [finding]);

        Assert.DoesNotContain("default:", CodeOnly(after), StringComparison.Ordinal);
        Assert.Empty(Interacting(after, path, "SK2009"));
    }

    /// <summary>Both sides of the [#321] interaction in one analyzer set, which is the only way to see it.</summary>
    static Diagnostic[] Interacting(string source, string path, string id) =>
        RuleFixtures
            .Analyze(
                RuleFixtures.Compile(source, path),
                [new RedundantControlFlowAnalyzer(), new EnumSwitchExhaustivenessAnalyzer()],
                TestContext.Current.CancellationToken
            )
            .Where(diagnostic => diagnostic.Id == id)
            .ToArray();

    /// <summary>Every run of whitespace as one space, so an indentation difference is not a failure.</summary>
    static string Squashed(string text) =>
        string.Join(" ", text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    static Diagnostic[] Findings(string path, string id) {
        var source = File.ReadAllText(path);
        var all = RuleFixtures.Analyze(
            RuleFixtures.Compile(source, path),
            Analyzers,
            TestContext.Current.CancellationToken
        );

        var crashes = all.Where(static d => d.Id == "AD0001").ToArray();
        Assert.True(
            crashes.Length == 0,
            $"an analyzer threw while reading {Path.GetFileName(path)}; a crashed analyzer reports nothing, "
            + "which every negative fixture accepts:\n"
            + string.Join("\n", crashes.Select(static d => "  " + d.GetMessage()))
        );

        return all.Where(diagnostic => diagnostic.Id == id).ToArray();
    }

    /// <summary>
    ///     ⚠ The fixed text with its <c>//</c> comment lines dropped, for an assertion about code.
    /// </summary>
    /// <remarks>
    ///     A fixture explaining why it is the shape it is names that shape in prose —
    ///     <c>// Deleting only `case 2:` would …</c> — and a bare
    ///     <c>Assert.DoesNotContain("case 2:", after)</c> then finds the sentence rather than the
    ///     label and fails on a correct fix. Three of these assertions were written that way and all
    ///     three went red on their first run, which is the cheap version of the failure: an assertion
    ///     matching a fixture's commentary passes or fails on what the comment says.
    /// </remarks>
    static string CodeOnly(string text) =>
        string.Join(
            "\n",
            text.Split('\n').Where(static line => !line.TrimStart().StartsWith("//", StringComparison.Ordinal))
        );

    static string Apply(string source, IEnumerable<Diagnostic> findings) {
        var text = source;
        foreach (var diagnostic in findings.OrderByDescending(static d => d.Location.SourceSpan.Start)) {
            var count = int.Parse(
                diagnostic.Properties[FixEdits.CountKey]!,
                System.Globalization.CultureInfo.InvariantCulture
            );
            for (var i = count - 1; i >= 0; i--) {
                var start = int.Parse(
                    diagnostic.Properties[FixEdits.StartKey(i)]!,
                    System.Globalization.CultureInfo.InvariantCulture
                );
                var length = int.Parse(
                    diagnostic.Properties[FixEdits.LengthKey(i)]!,
                    System.Globalization.CultureInfo.InvariantCulture
                );
                text = text[..start] + diagnostic.Properties[FixEdits.TextKey(i)] + text[(start + length)..];
            }
        }

        return text;
    }
}
