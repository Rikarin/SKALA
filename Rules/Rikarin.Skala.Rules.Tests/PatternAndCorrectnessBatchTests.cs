using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Rules.Correctness;
using Rikarin.Skala.Rules.Modernization;
using System.Collections.Immutable;
using System.Reflection;
using System.Runtime.Loader;

namespace Rikarin.Skala.Rules.Tests;

public sealed class PatternAndCorrectnessBatchTests {
    static readonly ImmutableArray<DiagnosticAnalyzer> Analyzers = [
        new ReturningSwitchExpressionAnalyzer(), new ListPatternAnalyzer(), new Utf8LiteralAnalyzer(),
        new ConstantRangeComparisonAnalyzer(), new SelfPropertyOperationAnalyzer()
    ];

    static readonly object?[][] ListCases = [
        [null], [Array.Empty<int>()], [new[] { 1 }],
        [new[] { 1, 2, 7 }], [new[] { 1, 2, 8 }], [new[] { 2, 2, 7 }], [new[] { 1, 2, 7, 9 }]
    ];

    public static TheoryData<RuleFixture> Fixtures {
        get {
            var result = new TheoryData<RuleFixture>();
            foreach (var fixture in RuleFixtures.All()
                         .Where(static fixture => fixture.RuleId is
                             "SK1012" or "SK1013" or "SK1026" or "SK2001" or "SK2012"
                         )) {
                result.Add(fixture);
            }

            return result;
        }
    }

    [Theory]
    [MemberData(nameof(Fixtures))]
    public void Fixtures_HaveExactCountsAndExpectedFixAvailability(RuleFixture fixture) {
        var diagnostics = Analyze(RuleFixtures.Compile(File.ReadAllText(fixture.Path), fixture.Path))
            .Where(diagnostic => diagnostic.Id == fixture.RuleId)
            .ToArray();
        Assert.Equal(fixture.ShouldFire ? 1 : 0, diagnostics.Length);
        Assert.All(
            diagnostics,
            diagnostic => Assert.Equal(
                fixture.RuleId.StartsWith("SK1", StringComparison.Ordinal),
                diagnostic.Properties.ContainsKey(FixEdits.CountKey)
            )
        );
    }

    [Theory]
    [InlineData("SK1012", "integers", LanguageVersion.CSharp8, LanguageVersion.CSharp7_3)]
    [InlineData("SK1013", "array", LanguageVersion.CSharp11, LanguageVersion.CSharp10)]
    [InlineData("SK1026", "readonly-span", LanguageVersion.CSharp11, LanguageVersion.CSharp10)]
    public void Modernizations_HonorLanguageFloors(
        string rule,
        string fixture,
        LanguageVersion floor,
        LanguageVersion below
    ) {
        var source = File.ReadAllText(Path.Combine(RuleFixtures.Root, rule, "positive", fixture + ".cs"));
        var earlier = RuleFixtures.Compile(source, "test.cs", below);
        if (below < LanguageVersion.CSharp8) {
            earlier = earlier.WithOptions(earlier.Options.WithNullableContextOptions(NullableContextOptions.Disable));
        }

        Assert.DoesNotContain(Analyze(earlier), diagnostic => diagnostic.Id == rule);
        Assert.Single(Analyze(RuleFixtures.Compile(source, "test.cs", floor)), diagnostic => diagnostic.Id == rule);
    }

    [Theory]
    [InlineData("sbyte")]
    [InlineData("byte")]
    [InlineData("short")]
    [InlineData("ushort")]
    [InlineData("int")]
    [InlineData("uint")]
    [InlineData("long")]
    [InlineData("ulong")]
    [InlineData("char")]
    public void AllFixedWidthRanges_AreExactAtBothEndpoints(string type) {
        var source = $$"""
                     class C {
                         bool Minimum({{type}} value) => value >= {{type}}.MinValue;
                         bool Maximum({{type}} value) => value <= {{type}}.MaxValue;
                         bool Underflow({{type}} value) => value < {{type}}.MinValue;
                         bool Overflow({{type}} value) => value > {{type}}.MaxValue;
                     }
                     """;
        var diagnostics = Analyze(RuleFixtures.Compile(source, "test.cs")).Where(static d => d.Id == "SK2001")
            .ToArray();
        Assert.Equal(4, diagnostics.Length);
        Assert.Equal(
            2,
            diagnostics.Count(static d => d.GetMessage().Contains("always true", StringComparison.Ordinal))
        );
        Assert.Equal(
            2,
            diagnostics.Count(static d => d.GetMessage().Contains("always false", StringComparison.Ordinal))
        );
    }

    [Theory]
    [InlineData("Limits.Min")]
    [InlineData("Alias")]
    public void CrossFileConstantInitializers_AreNotPerFileFacts(string bound) {
        var compilation = RuleFixtures.Compile(
            $"class C {{ const int Alias = Limits.Min; bool M(byte x) => x >= {bound}; }}",
            "use.cs"
        );
        compilation = AddFile(compilation, "class Limits { public const int Min = 0; }", "limits.cs");
        Assert.DoesNotContain(Analyze(compilation), static d => d.Id == "SK2001");
    }

    [Fact]
    public void OtherFileAutoProperties_AreNotAssumedToStayAutomatic() {
        var compilation = RuleFixtures.Compile("class C { bool M(Item item) => item.Value == item.Value; }", "use.cs");
        compilation = AddFile(compilation, "class Item { public int Value { get; set; } }", "item.cs");
        Assert.DoesNotContain(Analyze(compilation), static d => d.Id == "SK2012");
    }

    [Fact]
    public void CrossFileConstants_AreExcludedFromAllThreeRewrites() {
        const string source = """
                              using System;
                              using System.Text;
                              class C {
                                  int Switch(int x) { if (x == Constants.First) return 1; else if (x == 2) return 2; else return 3; }
                                  bool List(int[]? a) => a != null && a.Length == 1 && a[0] == Constants.First;
                                  void Consume(ReadOnlySpan<byte> data) { }
                                  void Bytes() => Consume(Encoding.UTF8.GetBytes(Constants.Text));
                              }
                              """;
        var compilation = AddFile(
            RuleFixtures.Compile(source, "use.cs"),
            "class Constants { public const int First = 1; public const string Text = \"text\"; }",
            "constants.cs"
        );
        Assert.Empty(Analyze(compilation));
    }

    [Theory]
    [InlineData("_")]
    [InlineData("not")]
    [InlineData("and")]
    [InlineData("or")]
    public void ConstantsWithPatternMeanings_AreNotCopiedIntoPatterns(string name) {
        var source = $$"""
                       class C {
                           const int {{name}} = 1;
                           int Switch(int x) { if (x == {{name}}) return 1; else if (x == 2) return 2; else return 3; }
                           bool List(int[]? a) => a != null && a.Length == 1 && a[0] == {{name}};
                       }
                       """;
        Assert.Empty(Analyze(RuleFixtures.Compile(source, "test.cs")));
    }

    [Fact]
    public void CallerArgumentExpressionText_IsNotChangedByAnyRewrite() {
        const string source = """
                              using System;
                              using System.Text;
                              using System.Runtime.CompilerServices;
                              class C {
                                  static void Bytes(ReadOnlySpan<byte> value, [CallerArgumentExpression("value")] string? text = null) { }
                                  static void Boolean(bool value, [CallerArgumentExpression("value")] string? text = null) { }
                                  static void Callback(Func<int, int> value, [CallerArgumentExpression("value")] string? text = null) { }
                                  void M(int[]? a) {
                                      Bytes(Encoding.UTF8.GetBytes("OK"));
                                      Boolean(a != null && a.Length == 1 && a[0] == 1);
                                      Callback(x => { if (x == 1) return 1; else if (x == 2) return 2; else return 3; });
                                  }
                              }
                              """;
        Assert.Empty(Analyze(RuleFixtures.Compile(source, "test.cs")));
    }

    [Fact]
    public void ListPattern_RemainsAvailableAfterPropertyPatternFix() {
        const string source = "class C { bool M(int[]? a) => a != null && a.Length == 2 && a[0] == 1; }";
        var before = RuleFixtures.Compile(source, "test.cs");
        var property = Assert.Single(
            RuleFixtures.Analyze(before, [new PropertyPatternAnalyzer()], TestContext.Current.CancellationToken)
        );
        var normalized = Apply(source, [property]);
        Assert.Contains("Length: 2", normalized, StringComparison.Ordinal);
        Assert.Single(Analyze(RuleFixtures.Compile(normalized, "test.cs")), static d => d.Id == "SK1013");
    }

    [Fact]
    public void GeneratedCode_IsIgnored() {
        var source =
            "// <auto-generated/>\nclass C { int P { get; set; } bool M(byte x) => x >= 0; void A() { P = P; } }";
        Assert.Empty(Analyze(RuleFixtures.Compile(source, "generated.cs")));
    }

    [Theory]
    [InlineData("byte", "300")]
    [InlineData("char", "70000")]
    public void UnrepresentableElementConstants_AreNotRewritten(string type, string value) {
        var source = $"class C {{ bool M({type}[]? a) => a != null && a.Length == 1 && a[0] == {value}; }}";
        Assert.DoesNotContain(Analyze(RuleFixtures.Compile(source, "test.cs")), static d => d.Id == "SK1013");
    }

    [Fact]
    public void SwitchFix_PreservesSelectedBranchEffects() {
        const string source = """
                              public static class Probe {
                                  static int calls;
                                  static string Return(string value) { calls++; return value + calls; }
                                  public static string Run(int x) {
                                      calls = 0;
                                      if (x == 0) return Return("zero");
                                      else if (x == 1) return Return("one");
                                      else return Return("other");
                                  }
                              }
                              """;
        AssertEquivalent(source, "SK1012", [[0], [1], [2], [-1], [int.MaxValue]]);
    }

    [Fact]
    public void ListFix_PreservesNullLengthAndElementResults() {
        const string source = """
                              public static class Probe {
                                  public static bool Run(int[]? a) => a != null && a.Length == 3 && a[2] == 7 && a[0] == 1;
                              }
                              """;
        AssertEquivalent(source, "SK1013", ListCases);
    }

    [Fact]
    public void Utf8Fix_PreservesBytesAndEmbeddedNul() {
        const string source = """
                              using System;
                              using System.Text;
                              public static class Probe {
                                  static string Consume(ReadOnlySpan<byte> bytes) => Convert.ToHexString(bytes);
                                  public static string Run() => Consume(Encoding.UTF8.GetBytes("A\0\r\n\t\"\\\u007f"));
                              }
                              """;
        AssertEquivalent(source, "SK1026", [[]]);
    }

    static CSharpCompilation AddFile(CSharpCompilation compilation, string source, string path) =>
        compilation.AddSyntaxTrees(
            CSharpSyntaxTree.ParseText(
                source,
                (CSharpParseOptions)compilation.SyntaxTrees.First().Options,
                path,
                cancellationToken: TestContext.Current.CancellationToken
            )
        );

    static ImmutableArray<Diagnostic> Analyze(CSharpCompilation compilation) {
        Assert.DoesNotContain(
            compilation.GetDiagnostics(TestContext.Current.CancellationToken),
            static d => d.Severity == DiagnosticSeverity.Error
        );
        var diagnostics = RuleFixtures.Analyze(compilation, Analyzers, TestContext.Current.CancellationToken);
        Assert.DoesNotContain(diagnostics, static d => d.Id == "AD0001");
        return diagnostics;
    }

    static string Apply(string source, IEnumerable<Diagnostic> diagnostics) =>
        SourceText.From(source)
            .WithChanges(
                diagnostics.Select(diagnostic => new TextChange(
                        new TextSpan(
                            int.Parse(
                                diagnostic.Properties[FixEdits.StartKey(0)]!,
                                System.Globalization.CultureInfo.InvariantCulture
                            ),
                            int.Parse(
                                diagnostic.Properties[FixEdits.LengthKey(0)]!,
                                System.Globalization.CultureInfo.InvariantCulture
                            )
                        ),
                        diagnostic.Properties[FixEdits.TextKey(0)]!
                    )
                )
            )
            .ToString();

    static void AssertEquivalent(string source, string rule, object?[][] cases) {
        var original = RuleFixtures.Compile(source, "probe.cs");
        var finding = Assert.Single(Analyze(original), diagnostic => diagnostic.Id == rule);
        var replacement = RuleFixtures.Compile(Apply(source, [finding]), "probe.cs");
        Assert.DoesNotContain(Analyze(replacement), diagnostic => diagnostic.Id == rule);
        Assert.Equal(Evaluate(original, cases), Evaluate(replacement, cases));
    }

    static string[] Evaluate(CSharpCompilation compilation, object?[][] cases) {
        using var image = new MemoryStream();
        var result = compilation.Emit(image, cancellationToken: TestContext.Current.CancellationToken);
        Assert.True(result.Success, string.Join("\n", result.Diagnostics));
        image.Position = 0;
        var context = new AssemblyLoadContext(Guid.NewGuid().ToString(), true);
        try {
            var method = context.LoadFromStream(image).GetType("Probe")!.GetMethod("Run")!;
            return cases.Select(arguments => {
                    try {
                        return "result:" + method.Invoke(null, arguments);
                    } catch (TargetInvocationException exception) {
                        return exception.InnerException!.GetType().FullName!;
                    }
                }
            )
                .ToArray();
        } finally {
            context.Unload();
        }
    }
}
