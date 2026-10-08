using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Rikarin.Skala.Rules.Metadata;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;

namespace Rikarin.Skala.Rules.Tests;

/// <summary>
///     <c>SK1133</c>: what the fix writes, and which rule owns a shape two rules can see (#512).
/// </summary>
public sealed class CollectionExpressionSpreadTests {
    const string Header = """
                          // fixture-option: LangVersion = 14
                          using System.Collections.Generic;
                          using System.Linq;

                          public sealed class Site {
                              public int[] Copy(List<int> list, List<int>? other, bool flag) {

                          """;

    const string Footer = """

                                  return copied;
                              }
                          }
                          """;

    /// <summary>
    ///     ⚠ No space after <c>..</c>. The formatter keeps a spread exactly as written (SK-DIV-0009), so
    ///     the fix's spelling is the one that stays; and parentheses go only where the spread reads back
    ///     as the same expression without them.
    /// </summary>
    [Theory]
    [InlineData("int[] copied = list.ToArray();", "int[] copied = [..list];")]
    [InlineData("int[] copied = (list).ToArray();", "int[] copied = [..list];")]
    [InlineData("int[] copied = ((list)).ToArray();", "int[] copied = [..list];")]
    [InlineData(
        "int[] copied = ((IEnumerable<int>)list).ToArray();",
        "int[] copied = [..(IEnumerable<int>)list];"
    )]
    [InlineData(
        "int[] copied = list.Where(value => value > 0).ToArray();",
        "int[] copied = [..list.Where(value => value > 0)];"
    )]
    [InlineData("int[] copied = (other ?? list).ToArray();", "int[] copied = [..other ?? list];")]
    [InlineData("int[] copied = (flag ? list : other!).ToArray();", "int[] copied = [..flag ? list : other!];")]
    [InlineData(
        "int[] copied = (from value in list select value).ToArray();",
        "int[] copied = [..from value in list select value];"
    )]
    [InlineData("int[] copied = (other = list).ToArray();", "int[] copied = [..other = list];")]
    [InlineData(
        "int[] copied = (list as IEnumerable<int>).ToArray();",
        "int[] copied = [..list as IEnumerable<int>];"
    )]
    public void TheFix_WritesTheSpreadGlued(string written, string expected) {
        var source = Header + "        " + written + Footer;

        Assert.Equal(
            Header + "        " + expected + Footer,
            CollectionCallShapeBatchTests.Apply(source, RuleIds.CollectionExpressionSpread, SkalaAnalyzers.All)
        );
    }

    /// <summary>
    ///     ⚠ <c>SK1081</c> owns a copy of a copy. This rule waits for its fix, then takes the shortened call
    ///     — one defect, one finding, at every step.
    /// </summary>
    [Fact]
    public void ACopyOfACopy_IsSK1081s_ThenThisRulesOnTheNextPass() {
        var source = Header + "        int[] copied = list.ToList().ToArray();" + Footer;
        var first = Analyze(source);

        Assert.Single(first, static d => d.Id == RuleIds.RedundantSequenceCall);
        Assert.DoesNotContain(first, static d => d.Id == RuleIds.CollectionExpressionSpread);

        var shortened = CollectionCallShapeBatchTests.Apply(source, RuleIds.RedundantSequenceCall, SkalaAnalyzers.All);
        Assert.Contains("int[] copied = list.ToArray();", shortened, StringComparison.Ordinal);

        var second = Analyze(shortened);
        Assert.Single(second, static d => d.Id == RuleIds.CollectionExpressionSpread);
        Assert.DoesNotContain(second, static d => d.Id == RuleIds.RedundantSequenceCall);
    }

    /// <summary>
    ///     <c>SK4006</c>'s <c>foreach</c> over a copy has no target type, so it is that rule's alone.
    /// </summary>
    [Fact]
    public void AForeachOverACopy_IsSK4006sAlone() {
        const string source = """
                              // fixture-option: LangVersion = 14
                              using System.Collections.Generic;
                              using System.Linq;

                              public static class Loop {
                                  public static int Sum(int[] values) {
                                      var total = 0;
                                      foreach (var value in values.ToArray()) {
                                          total += value;
                                      }

                                      return total;
                                  }
                              }
                              """;
        var found = Analyze(source);

        Assert.Single(found, static d => d.Id == RuleIds.ImmediateMaterialization);
        Assert.DoesNotContain(found, static d => d.Id == RuleIds.CollectionExpressionSpread);
    }

    /// <summary>
    ///     <c>SK1001</c> rewrites a <c>new</c> and never a call; a <c>new</c> copied by <c>ToArray</c> is
    ///     this rule's, and <c>SK1072</c> then takes the array apart.
    /// </summary>
    [Fact]
    public void ACopiedCreation_IsThisRules_ThenSK1072s() {
        var source = Header + "        int[] copied = new[] { 1, 2 }.ToArray();" + Footer;
        var first = Analyze(source);

        Assert.Single(first, static d => d.Id == RuleIds.CollectionExpressionSpread);
        Assert.DoesNotContain(first, static d => d.Id == RuleIds.CollectionExpression);
        Assert.DoesNotContain(first, static d => d.Id == RuleIds.RedundantSpreadElement);

        var spread = CollectionCallShapeBatchTests.Apply(
            source,
            RuleIds.CollectionExpressionSpread,
            SkalaAnalyzers.All
        );
        Assert.Contains("int[] copied = [..new[] { 1, 2 }];", spread, StringComparison.Ordinal);

        var second = Analyze(spread);
        Assert.Single(second, static d => d.Id == RuleIds.RedundantSpreadElement);
        Assert.DoesNotContain(second, static d => d.Id == RuleIds.CollectionExpressionSpread);
    }

    /// <summary>
    ///     ⚠ Two proofs of the compiler and one floor: a written C# 14, or (#515) a .NET 10 reference set —
    ///     and C# 14 as the effective version under either.
    /// </summary>
    /// <remarks>
    ///     The null rows are the test host's own runtime assemblies, whose core library is
    ///     <c>System.Private.CoreLib</c> — <c>--load=loose</c>'s shape, where nothing was built — and there
    ///     only a written number proves anything. The <c>net9.0</c> rows are why the version is read off
    ///     the reference set rather than assumed: SDK 9.0.1xx builds them with Roslyn 4.12.
    /// </remarks>
    [Theory]
    [InlineData(null, LanguageVersion.CSharp14, true)]
    [InlineData(null, LanguageVersion.Latest, false)]
    [InlineData(null, LanguageVersion.LatestMajor, false)]
    [InlineData(null, LanguageVersion.Preview, false)]
    [InlineData(null, LanguageVersion.CSharp13, false)]
    [InlineData("net10.0", LanguageVersion.CSharp14, true)]
    [InlineData("net10.0", LanguageVersion.Latest, true)]
    [InlineData("net10.0", LanguageVersion.LatestMajor, true)]
    [InlineData("net10.0", LanguageVersion.Default, true)]
    [InlineData("net10.0", LanguageVersion.Preview, true)]
    [InlineData("net10.0", LanguageVersion.CSharp13, false)]
    [InlineData("net9.0", LanguageVersion.CSharp14, true)]
    [InlineData("net9.0", LanguageVersion.Latest, false)]
    [InlineData("net9.0", LanguageVersion.Preview, false)]
    [InlineData("net9.0", LanguageVersion.CSharp13, false)]
    public void TheCompiler_IsProvedByAWrittenVersionOrANet10ReferenceSet(
        string? framework,
        LanguageVersion version,
        bool fires
    ) {
        var source = (framework is null ? "" : "// fixture-option: TargetFramework = " + framework + "\n")
            + Header
            + "        int[] copied = list.ToArray();"
            + Footer;
        var compilation = RuleFixtures.Compile(source, "probe.cs", version);

        // The instrument, before the claim: the reference set is the one the row names, or every row
        // measures the test host.
        Assert.Equal(
            framework switch {
                "net10.0" => "System.Runtime 10",
                "net9.0" => "System.Runtime 9",
                _ => "System.Private.CoreLib " + Environment.Version.Major
            },
            CoreLibrary(compilation)
        );

        Assert.Empty(
            compilation.GetDiagnostics(TestContext.Current.CancellationToken)
                .Where(static d => d.Severity == DiagnosticSeverity.Error)
        );

        var found = RuleFixtures.Analyze(compilation, SkalaAnalyzers.All, TestContext.Current.CancellationToken);
        Assert.Equal(fires, found.Any(static d => d.Id == RuleIds.CollectionExpressionSpread));
    }

    /// <summary>
    ///     ⚠ #515: a <c>netstandard2.1;net10.0</c> project at <c>latest</c>. The <c>net10.0</c> leg proves its
    ///     compiler and the <c>netstandard2.1</c> leg does not, so the shared file gets nothing from either.
    /// </summary>
    /// <remarks>
    ///     <c>MultiTargetLanguageFloor</c> cannot see this: both legs are at C# 14 to Skala. The sibling is
    ///     stood in for by a compilation whose core library is not .NET 10's — the test host's own, which
    ///     is exactly as unproved as <c>netstandard 2.1.0.0</c> — and the control row is the same pair
    ///     with the sibling on <c>net10.0</c> references too. The proof asked of the sibling is the whole
    ///     one: a <c>net10.0</c> sibling at C# 13 withholds, and a sibling with a written C# 14 proves.
    /// </remarks>
    [Theory]
    [InlineData(null, LanguageVersion.Latest, false)]
    [InlineData("net9.0", LanguageVersion.Latest, false)]
    [InlineData("net10.0", LanguageVersion.Latest, true)]
    [InlineData("net10.0", LanguageVersion.CSharp13, false)]
    [InlineData(null, LanguageVersion.CSharp14, true)]
    public async Task ASiblingThatDoesNotProveTheCompiler_WithholdsTheSharedFile(
        string? sibling,
        LanguageVersion siblingVersion,
        bool fires
    ) {
        var body = Header + "        int[] copied = list.ToArray();" + Footer;
        var current = RuleFixtures.Compile(
            "// fixture-option: TargetFramework = net10.0\n" + body,
            "shared.cs",
            LanguageVersion.Latest
        );
        var other = RuleFixtures.Compile(
            (sibling is null ? "" : "// fixture-option: TargetFramework = " + sibling + "\n") + body,
            "shared.cs",
            siblingVersion
        );

        var found = await current
            .WithAnalyzers(
                SkalaAnalyzers.All,
                new CompilationWithAnalyzersOptions(
                    new AnalyzerOptions([], new SiblingProvider([other])),
                    null,
                    true,
                    false,
                    true
                )
            )
            .GetAnalyzerDiagnosticsAsync(TestContext.Current.CancellationToken);

        Assert.DoesNotContain(found, static d => d.Id == "AD0001");
        Assert.Equal(fires, found.Any(static d => d.Id == RuleIds.CollectionExpressionSpread));
    }

    static string CoreLibrary(Compilation compilation) {
        var identity = compilation.ObjectType.ContainingAssembly.Identity;
        return identity.Name + " " + identity.Version.Major;
    }

    sealed class SiblingProvider(ImmutableArray<Compilation> siblings) : AnalyzerConfigOptionsProvider,
        ISiblingCompilations {
        public ImmutableArray<Compilation> Siblings { get; } = siblings;

        public override AnalyzerConfigOptions GlobalOptions => Empty.Instance;

        public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => Empty.Instance;

        public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) => Empty.Instance;
    }

    sealed class Empty : AnalyzerConfigOptions {
        public static Empty Instance { get; } = new();

        public override bool TryGetValue(string key, [NotNullWhen(true)] out string? value) {
            value = null;
            return false;
        }
    }

    static ImmutableArray<Diagnostic> Analyze(string source) =>
        RuleFixtures.Analyze(
            RuleFixtures.Compile(source, "probe.cs"),
            SkalaAnalyzers.All,
            TestContext.Current.CancellationToken
        );
}
