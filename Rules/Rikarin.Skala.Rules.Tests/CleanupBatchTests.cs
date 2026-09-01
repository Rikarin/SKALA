using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Rikarin.Skala.Rules.Cleanup;
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
    ];

    static readonly string[] Ids = ["SK0240", "SK0241", "SK0242", "SK0243"];

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

    /// <summary>Each of <c>SK0240</c>'s three shapes, named by the sentence it reports.</summary>
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

    static Diagnostic[] Findings(string path, string id) {
        var source = File.ReadAllText(path);
        return RuleFixtures
            .Analyze(RuleFixtures.Compile(source, path), Analyzers, TestContext.Current.CancellationToken)
            .Where(diagnostic => diagnostic.Id == id)
            .ToArray();
    }

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
