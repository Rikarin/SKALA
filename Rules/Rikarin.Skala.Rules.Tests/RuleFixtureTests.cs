using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Rikarin.Skala.Rules.Metadata;
using Rikarin.Skala.Rules.Async;
using Rikarin.Skala.Rules.Correctness;
using Rikarin.Skala.Rules.Design;
using Rikarin.Skala.Rules.Maintainability;
using Rikarin.Skala.Rules.Modernization;
using Rikarin.Skala.Rules.Performance;
using Rikarin.Skala.Rules.Security;
using Rikarin.Skala.Rules.TestQuality;

namespace Rikarin.Skala.Rules.Tests;

/// <summary>
/// Every shipped rule, against its positive and its "should not fire" fixture set.
/// </summary>
/// <remarks>
/// ⚠ The negative direction is the one that decides whether a rule ships. docs/plan/16 § R3: a 5 %
/// false-positive rate on a corpus producing 5 000 findings is 250 wrong findings, which is where
/// the analysis half gets switched off — and the rules most likely to over-fire are exactly the
/// ones with the most value.
/// </remarks>
public sealed class RuleFixtureTests {
    static readonly ImmutableArray<DiagnosticAnalyzer> Analyzers = [
        new FileScopedNamespaceAnalyzer(), new NullPatternAnalyzer(), new ThrowIfNullAnalyzer(),
        new NullCoalescingAssignmentAnalyzer(),
        new CountPropertyAnalyzer(), new EnumGetValuesAnalyzer(), new DiscardedExceptionAnalyzer(),
        new RethrowAnalyzer(),
        new AsyncVoidAnalyzer(), new BlockingOnAsyncAnalyzer(), new MetricsAnalyzer(),
        new WhereBeforeOperatorAnalyzer(), new AbstractTypeConstructorAnalyzer(),
        new ThreadSleepInTestAnalyzer(),
        new SqlInjectionAnalyzer(), new ProcessArgumentInjectionAnalyzer(), new WeakCipherAnalyzer(),
        new CertificateValidationAnalyzer(), new XmlExternalEntityAnalyzer(),
        new CollectionExpressionAnalyzer(), new UsingDeclarationAnalyzer(), new TypePatternAnalyzer(),
        new NullConditionalAssignmentAnalyzer(), new DictionaryLookupAnalyzer()
    ];

    public static TheoryData<RuleFixture> Fixtures {
        get {
            var data = new TheoryData<RuleFixture>();
            foreach (var fixture in RuleFixtures.All()) {
                data.Add(fixture);
            }

            return data;
        }
    }

    [Theory]
    [MemberData(nameof(Fixtures))]
    public void Rule_FiresExactlyWhereTheFixtureSaysItShould(RuleFixture fixture) {
        var source = File.ReadAllText(fixture.Path);
        var compilation = RuleFixtures.Compile(source, fixture.Path);

        // ⚠ A fixture that does not compile is a fixture that proves nothing: a rule reading an
        // error type answers "no finding" for the wrong reason, and the negative case passes.
        var errors = compilation.GetDiagnostics(TestContext.Current.CancellationToken)
            .Where(static d => d.Severity == DiagnosticSeverity.Error)
            .ToArray();

        Assert.True(
            errors.Length == 0,
            $"{fixture}: the fixture does not compile, so it proves nothing: "
            + string.Join("; ", errors.Take(3).Select(static d => d.ToString()))
        );

        var produced = RuleFixtures
            .Analyze(compilation, Analyzers, TestContext.Current.CancellationToken)
            .Where(diagnostic => diagnostic.Id == fixture.RuleId)
            .ToArray();

        if (fixture.ShouldFire) {
            Assert.True(
                produced.Length > 0,
                $"{fixture}: {fixture.RuleId} did not fire on a positive fixture."
            );
        } else {
            Assert.True(
                produced.Length == 0,
                $"{fixture}: {fixture.RuleId} fired {produced.Length} time(s) on a fixture that documents why it must not:\n"
                + string.Join(
                    "\n",
                    produced.Select(static d => "  " + d.Location.GetLineSpan() + ": " + d.GetMessage())
                )
            );
        }
    }

    [Theory]
    [MemberData(nameof(Fixtures))]
    public void EveryFix_ProducesTextThatStillParses(RuleFixture fixture) {
        // ⚠ Only rules the catalogue says have a fix. docs/plan/08 § SK7000: the metric rules carry
        // `hasFix: false`, because there is no edit that makes a 300-statement method shorter — the
        // finding is a measurement and the fix is a design decision a person makes. Asserting a fix
        // on those would be asserting the catalogue is wrong.
        if (!fixture.ShouldFire || RuleCatalog.Find(fixture.RuleId) is not { HasFix: true }) {
            return;
        }

        var source = File.ReadAllText(fixture.Path);
        var compilation = RuleFixtures.Compile(source, fixture.Path);
        var produced = RuleFixtures
            .Analyze(compilation, Analyzers, TestContext.Current.CancellationToken)
            .Where(diagnostic => diagnostic.Id == fixture.RuleId)
            .ToArray();

        foreach (var diagnostic in produced) {
            var edits = ReadEdits(diagnostic);
            Assert.True(
                edits.Count > 0,
                $"{fixture}: {diagnostic.Id} carries no fix, but the catalogue says it has one."
            );

            var text = source;
            foreach (var (start, length, replacement) in edits.OrderByDescending(static edit => edit.Start)) {
                text = text[..start] + replacement + text[(start + length)..];
            }

            var after = CSharpSyntaxTree.ParseText(
                text,
                new CSharpParseOptions(LanguageVersion.Preview),
                cancellationToken: TestContext.Current.CancellationToken
            );
            var errors = after.GetDiagnostics(TestContext.Current.CancellationToken)
                .Where(static d => d.Severity == DiagnosticSeverity.Error)
                .ToArray();

            Assert.True(
                errors.Length == 0,
                $"{fixture}: applying {diagnostic.Id}'s fix produced text that does not parse:\n"
                + string.Join("\n", errors.Take(3).Select(static d => "  " + d))
                + "\n---\n"
                + text
            );
        }
    }

    /// <summary>
    /// ⚠ docs/plan/08: every modernization rule declares its floor and is silent below it, checked
    /// against the compilation's effective LangVersion and not the SDK's. A rule that suggests C# 12
    /// syntax to a project pinned at C# 10 produces uncompilable fixes.
    /// </summary>
    [Fact]
    public void ARuleWithALanguageFloor_IsSilentBelowIt() {
        var source = File.ReadAllText(Path.Combine(RuleFixtures.Root, "SK1005", "positive", "simple.cs"));

        var above = RuleFixtures.Analyze(
            RuleFixtures.Compile(source, "simple.cs", LanguageVersion.CSharp10),
            Analyzers,
            TestContext.Current.CancellationToken
        );

        var below = RuleFixtures.Analyze(
            RuleFixtures.Compile(source, "simple.cs", LanguageVersion.CSharp9),
            Analyzers,
            TestContext.Current.CancellationToken
        );

        Assert.Contains(above, diagnostic => diagnostic.Id == RuleIds.FileScopedNamespace);
        Assert.DoesNotContain(below, diagnostic => diagnostic.Id == RuleIds.FileScopedNamespace);
    }

    [Fact]
    public void EveryRule_HasMoreNegativeFixturesThanPositive() {
        var fixtures = RuleFixtures.All();
        var shipped = Analyzers.SelectMany(static analyzer => analyzer.SupportedDiagnostics)
            .Select(static descriptor => descriptor.Id)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal);

        foreach (var ruleId in shipped) {
            var positive = fixtures.Count(f => f.RuleId == ruleId && f.ShouldFire);
            var negative = fixtures.Count(f => f.RuleId == ruleId && !f.ShouldFire);

            Assert.True(positive > 0, $"{ruleId} ships with no positive fixture.");
            Assert.True(
                negative >= positive,
                $"{ruleId} has {positive} positive fixture(s) and {negative} \"should not fire\" fixture(s). "
                + "docs/plan/16 § R3: the negative set must be at least as large as the positive one."
            );
        }
    }

    [Fact]
    public void EveryShippedAnalyzer_IsInTheCatalogue() {
        foreach (var descriptor in Analyzers.SelectMany(static analyzer => analyzer.SupportedDiagnostics)) {
            var rule = RuleCatalog.Find(descriptor.Id);
            Assert.True(rule is not null, $"{descriptor.Id} is reported by an analyzer and is not in rules.json.");
            Assert.Equal(rule!.Title, descriptor.Title.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }
    }

    static List<(int Start, int Length, string Text)> ReadEdits(Diagnostic diagnostic) {
        var result = new List<(int, int, string)>();
        if (!diagnostic.Properties.TryGetValue(FixEdits.CountKey, out var countText)
            || !int.TryParse(countText, out var count)) {
            return result;
        }

        for (var i = 0; i < count; i++) {
            result.Add(
                (
                    int.Parse(
                        diagnostic.Properties[FixEdits.StartKey(i)]!,
                        System.Globalization.CultureInfo.InvariantCulture
                    ),
                    int.Parse(
                        diagnostic.Properties[FixEdits.LengthKey(i)]!,
                        System.Globalization.CultureInfo.InvariantCulture
                    ),
                    diagnostic.Properties[FixEdits.TextKey(i)] ?? string.Empty
                )
            );
        }

        return result;
    }
}
