using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Analysis.Loading;
using Rikarin.Skala.Core.Configuration;
using Rikarin.Skala.Formatting.CSharp;
using Rikarin.Skala.Formatting.CSharp.Arrangement;
using Rikarin.Skala.Rules.Metadata;
using Rikarin.Skala.Testing;

namespace Rikarin.Skala.Conformance.Tests;

public sealed class PropertyPatternArrangementTests {
    const string Declaration = "sealed record Rule(bool Retired, bool RequiresSemantics, bool Enabled);";

    [Theory]
    [InlineData("!rule.Retired && rule.RequiresSemantics", "Retired: false, RequiresSemantics: true")]
    [InlineData("rule.Retired && !rule.RequiresSemantics", "Retired: true, RequiresSemantics: false")]
    [InlineData(
        "(!rule.Retired) && (rule.RequiresSemantics) && rule.Enabled",
        "Retired: false, RequiresSemantics: true, Enabled: true"
    )]
    public void BooleanMembers_AreCombined(string condition, string clauses) {
        var result = Arrange(Declaration + "class C { bool M(Rule rule) => " + condition + "; }");
        Assert.Equal(ArrangementOutcome.Arranged, result.Outcome);
        Assert.Contains("rule is { " + clauses + " }", result.Text);
        Assert.Empty(result.Diagnostics);
        Assert.Equal(ArrangementOutcome.Unchanged, Arrange(result.Text).Outcome);
    }

    [Fact]
    public void RealRuleInfoMetadata_IsSupportedByTheDefaultPipeline() {
        const string source =
            "using Rikarin.Skala.Rules.Metadata; class C { void M(RuleInfo rule) { if "
            + "(!rule.Retired && rule.RequiresSemantics) { System.Console.WriteLine(rule.Id); } } }";
        var (text, compilation) = Compile(source);
        var options = Options();
        var result = ArrangementPipeline.Run(
            Path,
            text,
            new PhaseOneOptions(options),
            new ArrangementOptions(options),
            compilation,
            cancellation: TestContext.Current.CancellationToken
        );
        Assert.True(result.Converged);
        Assert.Contains(ArrangeIds.PropertyPattern, result.Applied);
        Assert.Contains("if (rule is { Retired: false, RequiresSemantics: true })", result.Text);
        Assert.DoesNotContain(
            result.Diagnostics,
            static d => d.Id is ArrangeIds.Reverted or ArrangeIds.SymbolChanged or ArrangeIds.RuleThrew
        );
    }

    [Theory]
    [InlineData("class Rule { public bool Retired { get; set; } public bool RequiresSemantics { get; set; } }")]
    [InlineData("struct Rule { public bool Retired; public bool RequiresSemantics; }")]
    public void AutoPropertiesAndFields_AreSupported(string declaration) {
        Assert.Equal(
            ArrangementOutcome.Arranged,
            Arrange(declaration + "class C { bool M(Rule rule) => !rule.Retired && rule.RequiresSemantics; }").Outcome
        );
    }

    [Theory]
    [InlineData("Rule? rule", "!rule.Retired && rule.RequiresSemantics")]
    [InlineData("Rule rule, Rule other", "!rule.Retired && other.RequiresSemantics")]
    [InlineData("Rule rule", "!rule.Retired && rule.Retired")]
    [InlineData("Rule rule", "!rule.Retired /* preserve */ && rule.RequiresSemantics")]
    [InlineData("dynamic rule", "!rule.Retired && rule.RequiresSemantics")]
    [InlineData("ref Rule rule", "!rule.Retired && rule.RequiresSemantics")]
    public void UnprovenOrUnsupportedConditions_AreLeftAlone(string parameters, string condition) {
        var source = Declaration + "class C { bool M(" + parameters + ") => " + condition + "; }";
        AssertUnchanged(source);
    }

    [Theory]
    [InlineData("public bool Retired => System.DateTime.Now.Ticks % 2 == 0;")]
    [InlineData("public virtual bool Retired { get; set; }")]
    [InlineData("public volatile bool Retired;")]
    public void GettersWithEffectsOrDispatch_AreLeftAlone(string member) {
        AssertUnchanged(
            "class Rule { "
            + member
            + " public bool RequiresSemantics { get; set; } }"
            + "class C { bool M(Rule rule) => !rule.Retired && rule.RequiresSemantics; }"
        );
    }

    [Fact]
    public void CallsAsReceivers_AreLeftAlone() =>
        AssertUnchanged(
            Declaration
            + "class C { Rule Get() => new(false, true, true); bool M() => !Get().Retired && Get().RequiresSemantics; }"
        );

    [Fact]
    public void ExpressionTrees_AreLeftAlone() =>
        AssertUnchanged(
            Declaration
            + "class C { System.Linq.Expressions.Expression<System.Func<Rule, bool>> P => "
            + "rule => !rule.Retired && rule.RequiresSemantics; }"
        );

    [Fact]
    public void FormatterOff_IsRespected() =>
        AssertUnchanged(
            Declaration
            + "class C { bool M(Rule rule) {\n// @formatter:off\nreturn !rule.Retired && "
            + "rule.RequiresSemantics;\n// @formatter:on\n} }"
        );

    [Fact]
    public void NullableFlowGuard_AllowsTheFollowingMemberTests() {
        var result = Arrange(
            Declaration
            + "class C { bool M(Rule? rule) { if (rule is null) return false; return "
            + "!rule.Retired && rule.RequiresSemantics; } }"
        );
        Assert.Equal(ArrangementOutcome.Arranged, result.Outcome);
        Assert.Contains("return rule is { Retired: false, RequiresSemantics: true };", result.Text);
    }

    [Fact]
    public void SurroundingNegation_KeepsItsMeaning() {
        var result = Arrange(
            Declaration + "class C { bool M(Rule rule) => !(!rule.Retired && rule.RequiresSemantics); }"
        );
        Assert.Equal(ArrangementOutcome.Arranged, result.Outcome);
        Assert.Contains("!(rule is { Retired: false, RequiresSemantics: true })", result.Text);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public void LooseMode_DoesNotGuessMemberTypes() {
        var source = Declaration + "class C { bool M(Rule rule) => !rule.Retired && rule.RequiresSemantics; }";
        var result = Arranger.Arrange(
            Path,
            SourceText.From(source),
            new ArrangementOptions(Options()),
            filter: new([ArrangeIds.PropertyPattern], []),
            cancellation: TestContext.Current.CancellationToken
        );
        Assert.Equal(ArrangementOutcome.Unchanged, result.Outcome);
    }

    [Fact]
    public void CSharp7_DoesNotIntroducePropertyPatterns() {
        const string source =
            "struct Rule { public bool Retired; public bool RequiresSemantics; } class C { "
            + "bool M(Rule rule) => !rule.Retired && rule.RequiresSemantics; }";
        Assert.Equal(ArrangementOutcome.Unchanged, Arrange(source, LanguageVersion.CSharp7_3).Outcome);
    }

    static void AssertUnchanged(string source) {
        var result = Arrange(source);
        Assert.Equal(ArrangementOutcome.Unchanged, result.Outcome);
        Assert.Equal(source, result.Text);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public void QueryExpressions_AreLeftAlone() =>
        AssertUnchanged(
            "using System.Linq; "
            + Declaration
            + "class C { IQueryable<Rule> M(IQueryable<Rule> rules) => from rule in rules "
            + "where !rule.Retired && rule.RequiresSemantics select rule; }"
        );

    const string Path = "/property-pattern/Probe.cs";

    static Rikarin.Skala.Options.FormattingOptions Options() =>
        OptionResolver.Resolve(System.IO.Path.Combine(Corpus.RepositoryRoot, "Probe.cs")).Options;

    static (SourceText, CSharpCompilation) Compile(string source, LanguageVersion language = LanguageVersion.Preview) {
        var text = SourceText.From(source);
        var tree = CSharpSyntaxTree.ParseText(
            text,
            new CSharpParseOptions(language),
            Path,
            TestContext.Current.CancellationToken
        );
        var compilation = CSharpCompilation.Create(
            "probe",
            [tree],
            [.. SharedFrameworkReferences.Value, MetadataReference.CreateFromFile(typeof(RuleInfo).Assembly.Location)],
            new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: language >= LanguageVersion.CSharp8
                    ? NullableContextOptions.Enable
                    : NullableContextOptions.Disable
            )
        );
        return (text, compilation);
    }

    static ArrangementResult Arrange(string source, LanguageVersion language = LanguageVersion.Preview) {
        var (text, compilation) = Compile(source, language);
        return Arranger.Arrange(
            Path,
            text,
            new ArrangementOptions(Options()),
            compilation,
            filter: new([ArrangeIds.PropertyPattern], []),
            cancellation: TestContext.Current.CancellationToken
        );
    }
}
