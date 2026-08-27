using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Rikarin.Skala.Rules.Maintainability;
using Rikarin.Skala.Rules.Metadata;

namespace Rikarin.Skala.Rules.Tests;

/// <summary>
///     The analyzer's two contracts with the outside world: the <c>.editorconfig</c> keys it reads, and
///     the measurement it puts on every diagnostic.
/// </summary>
/// <remarks>
///     ⚠ Both are plumbing, and untested plumbing is the kind that silently does not work: a threshold
///     key that never resolves looks exactly like a repository whose code is fine, and a missing
///     property looks exactly like a reader that forgot to ask.
/// </remarks>
public sealed class MetricsAnalyzerTests {
    const string ModeratelyNested = """
                                    public sealed class Holder {
                                        /// <summary>Three levels of nesting: cognitive complexity 6.</summary>
                                        public static int Walk(int[] values, bool a, bool b) {
                                            var total = 0;
                                            if (a) {
                                                foreach (var value in values) {
                                                    if (b) {
                                                        total += value;
                                                    }
                                                }
                                            }

                                            return total;
                                        }
                                    }
                                    """;

    /// <summary>
    ///     ⚠ docs/plan/07 § "Metrics": <c>dotnet_code_quality.SK7002.threshold = 15</c>, "the standard
    ///     mechanism Roslyn analyzers already use for configuration and therefore needs no invention".
    /// </summary>
    [Fact]
    public void ALoweredThreshold_MakesTheRuleFire() {
        Assert.Empty(Diagnostics(ModeratelyNested, RuleIds.CognitiveComplexity));

        var tightened = Diagnostics(
            ModeratelyNested,
            RuleIds.CognitiveComplexity,
            ("dotnet_code_quality.SK7002.threshold", "5")
        );

        var diagnostic = Assert.Single(tightened);
        Assert.Contains("Cognitive complexity is 6", diagnostic.GetMessage());
    }

    /// <summary>
    ///     ⚠ "A missing or unparseable value falls back to the documented default silently."
    /// </summary>
    /// <remarks>
    ///     A metric rule that fails a build because someone wrote <c>threshold = fifteen</c> is a metric
    ///     rule that gets switched off, and a zero threshold fires on every member in the repository,
    ///     which is indistinguishable from the tool being broken.
    /// </remarks>
    [Theory]
    [InlineData("fifteen")]
    [InlineData("")]
    [InlineData("-1")]
    [InlineData("0")]
    [InlineData("15.5")]
    public void AnUnusableThreshold_FallsBackToTheDefault(string value) =>
        Assert.Empty(
            Diagnostics(
                ModeratelyNested,
                RuleIds.CognitiveComplexity,
                ("dotnet_code_quality.SK7002.threshold", value)
            )
        );

    /// <summary>Whitespace around the value is what a hand-edited `.editorconfig` looks like.</summary>
    [Fact]
    public void AThresholdWithSurroundingWhitespace_IsStillRead() =>
        Assert.Single(
            Diagnostics(
                ModeratelyNested,
                RuleIds.CognitiveComplexity,
                ("dotnet_code_quality.SK7002.threshold", "  5  ")
            )
        );

    /// <summary>
    ///     ⚠ Every <c>SK70xx</c> diagnostic carries its measurement, so a reader sees the number without
    ///     re-deriving it — and therefore cannot re-derive it differently.
    /// </summary>
    [Fact]
    public void EveryMetricDiagnostic_CarriesTheMeasuredValue() {
        var diagnostics = Diagnostics(
            """
            public sealed class Holder {
                /// <summary>Ten parameters, five levels of nesting.</summary>
                public static int Walk(int a0, int a1, int a2, int a3, int a4, int a5, int a6, int a7, int a8, int a9) {
                    if (a0 > 0) {
                        if (a1 > 0) {
                            if (a2 > 0) {
                                if (a3 > 0) {
                                    if (a4 > 0) {
                                        return a5;
                                    }
                                }
                            }
                        }
                    }

                    return 0;
                }
            }
            """,
            ruleId: null,
            ("dotnet_code_quality.SK7006.threshold", "4"),

            // 1 + 2 + 3 + 4 + 5 is 15, which is the default threshold exactly and therefore not over
            // it. Tightened here so that all three metrics report from the same member and the three
            // values can be compared against one another.
            ("dotnet_code_quality.SK7002.threshold", "10")
        );

        var byId = diagnostics
            .Where(static d => d.Id.StartsWith("SK70", StringComparison.Ordinal))
            .ToDictionary(static d => d.Id, static d => d.Properties[MemberMetrics.ValueKey]);

        Assert.Equal("10", byId[RuleIds.ParameterCount]);
        Assert.Equal("5", byId[RuleIds.NestingDepth]);
        Assert.Equal("15", byId[RuleIds.CognitiveComplexity]);
    }

    static ImmutableArray<Diagnostic> Diagnostics(
        string source,
        string? ruleId,
        params (string Key, string Value)[] options
    ) {
        var compilation = RuleFixtures.Compile(source, "metrics.cs");
        var errors = compilation.GetDiagnostics(TestContext.Current.CancellationToken)
            .Where(static d => d.Severity == DiagnosticSeverity.Error)
            .ToArray();

        Assert.True(errors.Length == 0, string.Join("; ", errors.Select(static d => d.ToString())));

        var produced = compilation
            .WithAnalyzers(
                [new MetricsAnalyzer()],
                new CompilationWithAnalyzersOptions(
                    new AnalyzerOptions([], new FixedOptionsProvider(options)),
                    onAnalyzerException: null,
                    concurrentAnalysis: false,
                    logAnalyzerExecutionTime: false,
                    reportSuppressedDiagnostics: true
                )
            )
            .GetAnalyzerDiagnosticsAsync(TestContext.Current.CancellationToken)
            .GetAwaiter()
            .GetResult();

        return ruleId is null
            ? produced
            : [.. produced.Where(diagnostic => diagnostic.Id == ruleId)];
    }

    /// <summary>The `.editorconfig` chain, reduced to the one section a test cares about.</summary>
    sealed class FixedOptionsProvider(params (string Key, string Value)[] values) : AnalyzerConfigOptionsProvider {
        readonly FixedOptions options = new FixedOptions(values);

        public override AnalyzerConfigOptions GlobalOptions => options;

        public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => options;

        public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) => options;
    }

    sealed class FixedOptions : AnalyzerConfigOptions {
        readonly Dictionary<string, string> values;

        public FixedOptions((string Key, string Value)[] values) {
            // ⚠ Roslyn's own `.editorconfig` reader lower-cases keys and compares them case
            // insensitively. A test that used an ordinal comparer would pass on a lookup the real
            // provider would answer differently.
            this.values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var (key, value) in values) {
                this.values[key] = value;
            }
        }

        public override IEnumerable<string> Keys => values.Keys;

        public override bool TryGetValue(string key, [NotNullWhen(true)] out string? value) =>
            values.TryGetValue(key, out value);
    }
}
