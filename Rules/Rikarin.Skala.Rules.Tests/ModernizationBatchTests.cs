using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Rules.Async;
using Rikarin.Skala.Rules.Maintainability;
using Rikarin.Skala.Rules.Modernization;
using System.Collections.Immutable;
using System.Reflection;
using System.Runtime.Loader;

namespace Rikarin.Skala.Rules.Tests;

public sealed class ModernizationBatchTests {
    static readonly int[] BoundaryValues = [int.MinValue, -2, -1, 0, 9, 10, int.MaxValue];
    static readonly bool[] MissingValues = [false, true];

    static readonly ImmutableArray<DiagnosticAnalyzer> Analyzers = [
        new RelationalPatternAnalyzer(), new PropertyPatternAnalyzer(), new SpanDecodingAnalyzer(),
        new ConfigureAwaitAnalyzer(), new FileLengthAnalyzer()
    ];

    public static TheoryData<RuleFixture> Fixtures {
        get {
            var data = new TheoryData<RuleFixture>();
            foreach (var fixture in RuleFixtures.All()
                         .Where(static fixture =>
                             fixture.RuleId is "SK1011" or "SK1014" or "SK1028" or "SK3003" or "SK7030"
                         )) {
                data.Add(fixture);
            }

            return data;
        }
    }

    [Theory]
    [MemberData(nameof(Fixtures))]
    public void Fixtures_HaveExactCountsAndNoAnalyzerFailures(RuleFixture fixture) {
        var compilation = RuleFixtures.Compile(File.ReadAllText(fixture.Path), fixture.Path);
        var diagnostics = Analyze(compilation);
        Assert.Equal(fixture.ShouldFire ? 1 : 0, diagnostics.Count(diagnostic => diagnostic.Id == fixture.RuleId));
    }

    [Theory]
    [InlineData("SK1014", "class C { bool M(int x) => x > 0 && x < 10; }")]
    public void PatternRules_RequireCSharp9(string rule, string source) {
        Assert.DoesNotContain(
            Analyze(RuleFixtures.Compile(source, "test.cs", LanguageVersion.CSharp8)),
            d => d.Id == rule
        );
        Assert.Single(
            Analyze(RuleFixtures.Compile(source, "test.cs", LanguageVersion.CSharp9)).Where(d => d.Id == rule)
        );
    }

    [Fact]
    public void PropertyPatterns_RequireCSharp8() {
        const string source = "class I { public int P; } class C { bool M(I i) => i != null && i.P == 2; }";
        var below = RuleFixtures.Compile(source, "test.cs", LanguageVersion.CSharp7_3);
        below = below.WithOptions(below.Options.WithNullableContextOptions(NullableContextOptions.Disable));
        Assert.DoesNotContain(Analyze(below), static diagnostic => diagnostic.Id == "SK1011");
        Assert.Single(
            Analyze(
                RuleFixtures.Compile(source, "test.cs", LanguageVersion.CSharp8)
            ).Where(static diagnostic => diagnostic.Id == "SK1011")
        );
    }

    [Theory]
    [InlineData("x > 10 && x < 0")]
    [InlineData("x >= 0 || x < 10")]
    [InlineData("x > int.MaxValue && x < 10")]
    [InlineData("x >= int.MinValue && x <= int.MaxValue")]
    public void ConstantResultRanges_AreNotRewrittenIntoCompilerDiagnostics(string condition) =>
        Assert.Empty(Analyze(RuleFixtures.Compile($"class C {{ bool M(int x) => {condition}; }}", "test.cs")));

    [Theory]
    [InlineData("disabled", 0)]
    [InlineData("ui", 0)]
    [InlineData("library", 4)]
    [InlineData(" LIBRARY ", 4)]
    [InlineData("invalid", 0)]
    public void ConfigureAwait_RecognizesAllFourFrameworkTaskTypes(string mode, int expected) {
        var source = $$"""
                     // analyzer-option: resharper_configure_await_analysis_mode = {{mode}}
                     using System.Threading.Tasks;
                     class C {
                         async Task M(Task a, Task<int> b, ValueTask c, ValueTask<int> d) {
                             await a; await b; await c; await d;
                             await a.ConfigureAwait(false); await b.ConfigureAwait(true);
                             await c.ConfigureAwait(false); await d.ConfigureAwait(true);
                         }
                     }
                     """;
        var diagnostics = Analyze(RuleFixtures.Compile(source, "test.cs")).Where(static d => d.Id == "SK3003")
            .ToArray();
        Assert.Equal(expected, diagnostics.Length);
        Assert.All(
            diagnostics,
            static diagnostic => Assert.False(diagnostic.Properties.ContainsKey(FixEdits.CountKey))
        );
    }

    [Fact]
    public void Configuration_IsPerTreeEvenInOneCompilation() {
        const string body = "using System.Threading.Tasks; class {0} {{ async Task M(Task task) {{ await task; }} }}";
        var compilation = RuleFixtures.Compile(
            "// analyzer-option: resharper_configure_await_analysis_mode = library\n"
            + string.Format(System.Globalization.CultureInfo.InvariantCulture, body, "Library"),
            "library.cs"
        );
        compilation = compilation.AddSyntaxTrees(
            CSharpSyntaxTree.ParseText(
                "// analyzer-option: resharper_configure_await_analysis_mode = ui\n"
                + string.Format(System.Globalization.CultureInfo.InvariantCulture, body, "Ui"),
                (CSharpParseOptions)compilation.SyntaxTrees.Single().Options,
                "ui.cs",
                cancellationToken: TestContext.Current.CancellationToken
            )
        );
        var diagnostic = Assert.Single(Analyze(compilation).Where(static d => d.Id == "SK3003"));
        Assert.Equal("library.cs", diagnostic.Location.SourceTree!.FilePath);
    }

    [Theory]
    [InlineData("\n")]
    [InlineData("\r\n")]
    [InlineData("\r")]
    public void FileLength_CountsPhysicalLinesNotTerminalEmptyLine(string newline) {
        var source = "// analyzer-option: dotnet_code_quality.SK7030.threshold = 3"
            + newline
            + "class C {"
            + newline
            + "// counted comment"
            + newline
            + "}";
        foreach (var ending in new[] { string.Empty, newline }) {
            var diagnostic = Assert.Single(
                Analyze(RuleFixtures.Compile(source + ending, "test.cs"))
                    .Where(static d => d.Id == "SK7030")
            );
            Assert.Equal("4", diagnostic.Properties[MemberMetrics.ValueKey]);
            Assert.Equal(0, diagnostic.Location.SourceSpan.Start);
            Assert.False(diagnostic.Properties.ContainsKey(FixEdits.CountKey));
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData("0")]
    [InlineData("-3")]
    [InlineData("1.5")]
    [InlineData("9999999999999999")]
    public void InvalidFileThreshold_FallsBackToDefault(string threshold) {
        var source = "// analyzer-option: dotnet_code_quality.SK7030.threshold = "
            + threshold
            + "\n"
            + string.Concat(Enumerable.Repeat("// comment\n", 999));
        Assert.DoesNotContain(Analyze(RuleFixtures.Compile(source, "test.cs")), static d => d.Id == "SK7030");
        var diagnostic = Assert.Single(
            Analyze(RuleFixtures.Compile(source + "// extra", "test.cs"))
                .Where(static d => d.Id == "SK7030")
        );
        Assert.Equal("1001", diagnostic.Properties[MemberMetrics.ValueKey]);
    }

    [Fact]
    public void EmptyFiles_HaveNoLengthFinding() =>
        Assert.Empty(Analyze(RuleFixtures.Compile(string.Empty, "test.cs")));

    [Theory]
    [InlineData("test.g.cs", "")]
    [InlineData("test.cs", "// <auto-generated/>\n")]
    public void GeneratedCode_IsExcluded(string path, string header) {
        var source = header
            + "// analyzer-option: resharper_configure_await_analysis_mode = library\n"
            + "// analyzer-option: dotnet_code_quality.SK7030.threshold = 1\n"
            + "using System.Threading.Tasks; class C { bool M(int x) => x > 0 && x < 10; async Task A(Task t) { await t; } }";
        Assert.Empty(Analyze(RuleFixtures.Compile(source, path)));
    }

    [Fact]
    public void SourceEncodingWithoutSpanOverload_IsIgnored() {
        const string source = """
                              namespace System.Text {
                                  class Encoding {
                                      public static Encoding UTF8 => new Encoding();
                                      public string GetString(byte[] bytes) => "";
                                  }
                              }
                              class C {
                                  string M(System.ReadOnlySpan<byte> bytes) => System.Text.Encoding.UTF8.GetString(bytes.ToArray());
                              }
                              """;
        Assert.Empty(Analyze(RuleFixtures.Compile(source, "test.cs")));
    }

    [Fact]
    public void PatternFixes_PreserveResultsAndSingleGetterEvaluation() {
        const string source = """
                              public static class Probe {
                                  sealed class Item {
                                      public int Calls;
                                      public int P { get { Calls++; return 3; } }
                                  }
                                  public static string Run(int value, bool missing) {
                                      Item? item = missing ? null : new Item();
                                      bool property = item != null && item.P == 3;
                                      bool range = value >= -1 && value < 10;
                                      return property + ":" + range + ":" + (item?.Calls ?? 0);
                                  }
                              }
                              """;
        var cases = (from value in BoundaryValues
            from missing in MissingValues
            select new object?[] { value, missing }).ToArray();
        AssertEquivalent(source, cases, 2);
    }

    [Fact]
    public void SpanFix_PreservesDecodingAndSliceExceptions() {
        const string source = """
                              using System;
                              using System.Text;
                              public static class Probe {
                                  public static string Run(byte[] bytes, int start, int count) =>
                                      Encoding.UTF8.GetString(bytes.AsSpan(start, count).ToArray());
                              }
                              """;
        object?[][] cases = [
            [new byte[] { 65, 66, 67 }, 1, 2], [new byte[] { 255, 192 }, 0, 2],
            [Array.Empty<byte>(), 0, 0], [null, 0, 0], [null, 1, 0],
            [new byte[] { 65 }, -1, 1], [new byte[] { 65 }, 0, 2]
        ];
        AssertEquivalent(source, cases, 1);
    }

    static void AssertEquivalent(string source, object?[][] cases, int expectedFixes) {
        var before = RuleFixtures.Compile(source, "probe.cs");
        var diagnostics = Analyze(before);
        Assert.Equal(expectedFixes, diagnostics.Length);
        var edits = diagnostics.Select(diagnostic => new TextChange(
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
            .ToArray();
        var after = RuleFixtures.Compile(SourceText.From(source).WithChanges(edits).ToString(), "probe.cs");
        Assert.Empty(Analyze(after));
        Assert.Equal(Evaluate(before, cases), Evaluate(after, cases));
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
                        return exception.InnerException!.GetType().FullName
                            + ":"
                            + (exception.InnerException as ArgumentException)?.ParamName;
                    }
                }
            )
                .ToArray();
        } finally {
            context.Unload();
        }
    }

    static ImmutableArray<Diagnostic> Analyze(CSharpCompilation compilation) {
        Assert.DoesNotContain(
            compilation.GetDiagnostics(TestContext.Current.CancellationToken),
            static d => d.Severity == DiagnosticSeverity.Error
        );
        var diagnostics = RuleFixtures.Analyze(compilation, Analyzers, TestContext.Current.CancellationToken);
        Assert.DoesNotContain(diagnostics, static d => d.Id == "AD0001");
        return diagnostics;
    }
}
