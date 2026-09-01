using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Rikarin.Skala.Rules.Correctness;
using System.Collections.Immutable;

namespace Rikarin.Skala.Rules.Tests;

/// <summary>
///     The arithmetic, range and constant-comparison batch: <c>SK2050</c>–<c>SK2054</c>.
/// </summary>
/// <remarks>
///     ⚠ These five sit next to <c>SK2001</c> and none of them is <c>SK2001</c>. SK2001 folds a
///     relational comparison the operand <em>type's</em> range decides; <c>SK2053</c> needs the
///     framework contract that a count is non-negative, which no type range supplies, and the other
///     four are about arithmetic rather than comparison. <see cref="SK2001_AndSK2053_NeverBothFire" />
///     pins the boundary rather than describing it.
/// </remarks>
public sealed class ArithmeticAndRangeBatchTests {
    static readonly ImmutableArray<DiagnosticAnalyzer> Analyzers = [
        new IntegerDivisionFractionAnalyzer(), new FixedResultArithmeticAnalyzer(), new MaskedShiftCountAnalyzer(),
        new NonnegativeSizeComparisonAnalyzer(), new SignedModulusEqualityAnalyzer(),
        new ConstantRangeComparisonAnalyzer()
    ];

    public static TheoryData<RuleFixture> Fixtures {
        get {
            var data = new TheoryData<RuleFixture>();
            foreach (var fixture in RuleFixtures.All()
                         .Where(static fixture => fixture.RuleId is "SK2050"
                                 or "SK2051"
                                 or "SK2052"
                                 or "SK2053"
                                 or "SK2054"
                         )) {
                data.Add(fixture);
            }

            return data;
        }
    }

    [Fact]
    public void TheBatch_HasFixtures() {
        // Anti-vacuity: every theory below is satisfied by an empty set.
        var count = RuleFixtures.All()
            .Count(static fixture => fixture.RuleId is "SK2050" or "SK2051" or "SK2052" or "SK2053" or "SK2054");

        Assert.True(count > 60, $"Only {count} fixture(s) were discovered for this batch.");
    }

    [Theory]
    [MemberData(nameof(Fixtures))]
    public void Fixtures_HaveExactCountsAndExpectedFixes(RuleFixture fixture) {
        var compilation = RuleFixtures.Compile(File.ReadAllText(fixture.Path), fixture.Path);
        var findings = Analyze(compilation).Where(diagnostic => diagnostic.Id == fixture.RuleId).ToArray();
        Assert.Equal(fixture.ShouldFire ? 1 : 0, findings.Length);
        Assert.All(
            findings,
            diagnostic => Assert.Equal(
                fixture.RuleId is "SK2050" or "SK2051",
                diagnostic.Properties.ContainsKey(FixEdits.CountKey)
            )
        );
    }

    /// <summary>
    ///     ⚠ An analyzer that throws is swallowed as <c>AD0001</c> and then produces nothing at all —
    ///     every positive fixture fails and every negative one passes, which reads as a half-working
    ///     rule rather than a dead one.
    /// </summary>
    [Theory]
    [MemberData(nameof(Fixtures))]
    public void NoFixture_CrashesAnAnalyzer(RuleFixture fixture) {
        var crashes = Analyze(RuleFixtures.Compile(File.ReadAllText(fixture.Path), fixture.Path))
            .Where(static diagnostic => diagnostic.Id == "AD0001")
            .Select(static diagnostic => diagnostic.GetMessage())
            .ToArray();

        Assert.True(crashes.Length == 0, $"{fixture}: an analyzer threw:\n  {string.Join("\n  ", crashes)}");
    }

    /// <summary>
    ///     ⚠ The trap that defines <c>SK2054</c>: <c>%</c> takes the sign of its dividend, so
    ///     <c>value % 2 == 1</c> is false for every negative odd value and <c>value % 2 == 0</c> is
    ///     correct for both signs. A rule that reported the second would be wrong about the one
    ///     spelling that works.
    /// </summary>
    [Fact]
    public void TheModulusAsymmetry_IsReal() {
        Assert.Equal(-1, -5 % 2);
#pragma warning disable SK2054 // The defect SK2054 reports, written out so the language fact is asserted.
        Assert.False(-5 % 2 == 1);
        Assert.True(-5 % 2 != 0);
#pragma warning restore SK2054
        Assert.Single(Findings("class C { bool M(int v) => v % 2 == 1; }"), d => d.Id == "SK2054");
        Assert.DoesNotContain(Findings("class C { bool M(int v) => v % 2 == 0; }"), d => d.Id == "SK2054");
    }

    /// <summary>
    ///     ⚠ Shift counts are masked, not clamped, and the mask comes from the <em>promoted</em> width
    ///     of the left operand. The same source text is a defect on <c>int</c> and correct on
    ///     <c>long</c>, which is why the rule cannot be syntactic.
    /// </summary>
    [Fact]
    public void TheShiftMask_FollowsTheOperandWidth() {
        var value = 1;
#pragma warning disable SK2052 // The defect SK2052 reports, written out so the language fact is asserted.
        Assert.Equal(value, value << 32);
#pragma warning restore SK2052
        Assert.Equal(4294967296L, 1L << 32);
        Assert.Single(Findings("class C { int M(int v) => v << 32; }"), d => d.Id == "SK2052");
        Assert.DoesNotContain(Findings("class C { long M(long v) => v << 32; }"), d => d.Id == "SK2052");

        // A byte promotes to int, so it masks with 31 and not with 7.
        Assert.Single(Findings("class C { int M(byte v) => v << 32; }"), d => d.Id == "SK2052");
    }

    /// <summary>
    ///     ⚠ <c>SK2053</c> and <c>SK2001</c> answer different questions and must never answer the same
    ///     expression. Both directions are checked, because a rule that stands down too eagerly is as
    ///     wrong as one that double-reports.
    /// </summary>
    [Fact]
    public void SK2001_AndSK2053_NeverBothFire() {
        var size = Findings("class C { bool M(int[] v) => v.Length >= 0; }");
        Assert.Single(size, static d => d.Id == "SK2053");
        Assert.DoesNotContain(size, static d => d.Id == "SK2001");

        var range = Findings("class C { bool M(byte v) => v >= 0; }");
        Assert.Single(range, static d => d.Id == "SK2001");
        Assert.DoesNotContain(range, static d => d.Id == "SK2053");
    }

    /// <summary>
    ///     ⚠ <c>x + 0.0</c> is not the identity: it turns negative zero into positive zero. The
    ///     integral-only restriction on <c>SK2051</c> is that fact, not tidiness.
    /// </summary>
    [Fact]
    public void NegativeZero_IsWhyFloatingPointIsExcluded() {
        var negativeZero = -0.0;
        Assert.True(double.IsNegative(negativeZero));
        Assert.False(double.IsNegative(negativeZero + 0.0));
        Assert.DoesNotContain(Findings("class C { double M(double v) => v + 0.0; }"), static d => d.Id == "SK2051");
    }

    /// <summary>
    ///     ⚠ <c>SK2050</c>'s fix changes the answer, which is why it is not marked safe. Demonstrated
    ///     rather than asserted in prose, and it is also why the rule stands down on <c>/ 1</c>: there
    ///     the shape is present and no fraction is lost.
    /// </summary>
    [Fact]
    public void TheDivisionFix_ChangesTheAnswer() {
#pragma warning disable SK2050 // The defect SK2050 reports, written out so the truncation is asserted.
        double before = 7 / 2;
#pragma warning restore SK2050
        var after = (double)7 / 2;
        Assert.Equal(3.0, before);
        Assert.Equal(3.5, after);
        Assert.Single(Findings("class C { double M(int h, int t) => h / t; }"), static d => d.Id == "SK2050");
        Assert.DoesNotContain(Findings("class C { double M(int h) => h / 1; }"), static d => d.Id == "SK2050");
    }

    /// <summary>
    ///     ⚠ Cross-file constants are not a file-local proof, the same guard <c>SK2001</c> uses.
    /// </summary>
    [Fact]
    public void CrossFileConstants_AreNotAProof() {
        var source = "class C { int M(int v) => v * Limits.One; }";
        var compilation = RuleFixtures.Compile(source, "test.cs")
            .AddSyntaxTrees(
                CSharpSyntaxTree.ParseText(
                    "class Limits { public const int One = 1; }",
                    new CSharpParseOptions(LanguageVersion.Preview),
                    "limits.cs",
                    cancellationToken: TestContext.Current.CancellationToken
                )
            );

        Assert.DoesNotContain(Analyze(compilation), static diagnostic => diagnostic.Id == "SK2051");
    }

    /// <summary>Generated code is nobody's to fix.</summary>
    [Theory]
    [InlineData("SK2050", "assignment")]
    [InlineData("SK2051", "multiplied_by_one")]
    [InlineData("SK2052", "int_masked_to_zero")]
    [InlineData("SK2053", "count_at_least_zero")]
    [InlineData("SK2054", "parity")]
    public void GeneratedCode_IsIgnored(string id, string name) {
        var source = "// <auto-generated/>\n"
            + File.ReadAllText(Path.Combine(RuleFixtures.Root, id, "positive", name + ".cs"));
        Assert.DoesNotContain(
            Analyze(RuleFixtures.Compile(source, "generated.cs")),
            diagnostic => diagnostic.Id == id
        );
    }

    /// <summary>
    ///     Severity overrides reach these rules like any other, which is what makes a repository able
    ///     to run them at a different level per path.
    /// </summary>
    [Fact]
    public void SeverityOverrides_Apply() {
        var compilation = RuleFixtures.Compile("class C { bool M(int v) => v % 2 == 1; }", "test.cs");
        compilation = compilation.WithOptions(
            compilation.Options.WithSpecificDiagnosticOptions(
                ImmutableDictionary<string, ReportDiagnostic>.Empty.Add("SK2054", ReportDiagnostic.Suppress)
            )
        );

        Assert.DoesNotContain(Analyze(compilation), static diagnostic => diagnostic.Id == "SK2054");
    }

    static ImmutableArray<Diagnostic> Findings(string source) => Analyze(RuleFixtures.Compile(source, "test.cs"));

    static ImmutableArray<Diagnostic> Analyze(CSharpCompilation compilation) =>
        RuleFixtures.Analyze(compilation, Analyzers, TestContext.Current.CancellationToken);
}
