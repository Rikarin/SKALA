using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Rikarin.Skala.Rules.Metadata;
using Rikarin.Skala.Rules.Security;
using System.Collections.Immutable;

namespace Rikarin.Skala.Rules.Tests;

/// <summary>
///     <c>SK5010</c>'s pattern scanner, against many more shapes than a fixture file each would be
///     worth.
/// </summary>
/// <remarks>
///     ⚠ The fixtures carry the cases a reader needs the argument for — Serilog's bounded quantifier,
///     Vixen's group-free assertions, the escaped parentheses. This class is the density behind them:
///     the scanner has to read character classes, escapes, `\p{…}`, nesting and four spellings of a
///     quantifier, and a bug in any of those is a security finding on correct code.
/// </remarks>
public sealed class RegexTimeoutTests {
    static readonly ImmutableArray<DiagnosticAnalyzer> Analyzers = [new RegexTimeoutAnalyzer()];

    /// <summary>Patterns whose shape is the nested unbounded quantifier the rule exists for.</summary>
    [Theory]
    [InlineData("^(a+)+$")]
    [InlineData("^(a*)*$")]
    [InlineData("^(a?)+$")]
    [InlineData("^(a+)*$")]
    [InlineData("([A-Za-z]+)*")]
    [InlineData(@"(\w+)+")]
    [InlineData(@"(\d{2,})+")]
    [InlineData("((ab)+)+")]
    [InlineData("(x+){2,}")]
    [InlineData("(?:a+)+")]
    [InlineData("(?<name>a+)+")]
    [InlineData(@"(\p{L}+)+")]
    [InlineData("prefix(a+)+suffix")]
    [InlineData("(a+)+?")]
    [InlineData("([^x]+)+")]
    public void TheScanner_ReadsTheNestedQuantifier(string pattern) => Assert.Single(Findings(pattern));

    /// <summary>
    ///     Patterns that must stay silent. ⚠ These are the ones that decide whether the rule ships:
    ///     each is a shape a looser detector reports and none of them can backtrack super-linearly.
    /// </summary>
    [Theory]
    [InlineData("(a+)?", "an outer `?` admits one iteration")]
    [InlineData("(a+){1,3}", "an outer `{n,m}` has a ceiling")]
    [InlineData("(a+){0,1}", "Serilog's shape, reduced")]
    [InlineData("(a+){3}", "an exact count is not a repetition to explore")]
    [InlineData("(abc*)+", "the body is three atoms; each iteration must start with `ab`")]
    [InlineData("(a+b)+", "the body is two atoms")]
    [InlineData("(abc)+", "a fixed-width body repeats linearly")]
    [InlineData("(a|b)+", "an alternation is not a quantifier, and these are disjoint")]
    [InlineData(@"^\w+\s*\d+$", "sequential quantifiers, none nested")]
    [InlineData("(?<name>[^:]+)::(?<member>[A-Za-z0-9]*)", "no group is repeated")]
    [InlineData("[(*+]+", "`(` and `*` inside a class are literals")]
    [InlineData(@"\(a+\)+", "escaped parentheses are not a group")]
    [InlineData("[]()]+", "a `]` straight after `[` is a literal, so the class runs to the second one")]
    // ⚠ The three below were added because sabotaging the escape skip, the class skip and the
    // leading-`]` rule each left every other case in this class green. In all three the scanner would
    // read a group that is not there and report a pattern that cannot backtrack. They are the only
    // cases that fail when those clauses are removed, and without them the clauses read as dead code.
    [InlineData(@"\(a+)+", "the `(` is escaped, so the `)` closes nothing")]
    [InlineData("[(]+)+", "the `(` is inside a class, so the `)` closes nothing")]
    [InlineData("[](a+)+]", "a leading `]` is a literal, so the whole pattern is one class")]
    [InlineData("(?>a+)+", "an atomic group cannot be backtracked into")]
    [InlineData("(?=a+)b+", "a lookaround is skipped rather than modelled")]
    [InlineData("^(a+$", "unbalanced: the scanner fails closed")]
    [InlineData("", "the empty pattern")]
    [InlineData("[a-z]+", "no group at all")]
    public void TheScanner_DeclinesWhatCannotBlowUp(string pattern, string why) =>
        Assert.True(Findings(pattern).Length == 0, $"reported `{pattern}`, but {why}.");

    /// <summary>
    ///     What the rule cannot read, it does not report on.
    /// </summary>
    /// <remarks>
    ///     ⚠ Both halves matter and they fail in opposite directions. An unknown *pattern* could be
    ///     anything, and reporting it would be guessing; unknown *options* could carry
    ///     `NonBacktracking`, so reporting would be reporting over the mitigation. The pattern in the
    ///     second case is the one every "reads it" case above is built from, so the only thing keeping
    ///     the rule quiet there is the options guard.
    /// </remarks>
    [Theory]
    [InlineData("public static bool F(string s, string p) => Regex.IsMatch(s, p);")]
    [InlineData("public static bool F(string s, RegexOptions o) => Regex.IsMatch(s, @\"^(a+)+$\", o);")]
    public void TheRule_SaysNothingAboutWhatItCannotRead(string member) {
        var source = "using System.Text.RegularExpressions;\npublic static class Probe {\n    " + member + "\n}\n";
        var compilation = RuleFixtures.Compile(source, "probe.cs");

        Assert.DoesNotContain(
            compilation.GetDiagnostics(TestContext.Current.CancellationToken),
            static d => d.Severity == DiagnosticSeverity.Error
        );
        Assert.DoesNotContain(
            RuleFixtures.Analyze(compilation, Analyzers, TestContext.Current.CancellationToken),
            static d => d.Id == RuleIds.RegexWithoutTimeout
        );
    }

    /// <summary>
    ///     ⚠ A crashed analyzer reports nothing, so every "declines" case above would pass on a rule
    ///     that threw on the first character. This is the only assertion that separates the two.
    /// </summary>
    [Theory]
    [InlineData("(a+)+")]
    [InlineData("^(a+$")]
    [InlineData("[")]
    [InlineData("(")]
    [InlineData("(?")]
    [InlineData("(?<")]
    [InlineData(@"\")]
    [InlineData("(a+){")]
    [InlineData("[^")]
    [InlineData(@"(\p{")]
    public void TheScanner_DoesNotThrow_OnAPatternItCannotParse(string pattern) {
        var crashes = Analyze(pattern).Where(static d => d.Id == "AD0001").ToArray();

        Assert.True(
            crashes.Length == 0,
            $"the analyzer threw on `{pattern}` and Roslyn swallowed it as AD0001:\n"
            + string.Join("\n", crashes.Select(static d => "  " + d.GetMessage()))
        );
    }

    /// <summary>Every SK5010 fixture, swept for a swallowed analyzer exception.</summary>
    [Theory]
    [MemberData(nameof(Fixtures))]
    public void TheAnalyzer_DoesNotThrow_OnAnyFixture(RuleFixture fixture) {
        var source = File.ReadAllText(fixture.Path);
        var compilation = RuleFixtures.Compile(source, fixture.Path);
        var crashes = RuleFixtures.Analyze(compilation, Analyzers, TestContext.Current.CancellationToken)
            .Where(static d => d.Id == "AD0001")
            .ToArray();

        Assert.True(
            crashes.Length == 0,
            $"{fixture}: the analyzer threw and Roslyn swallowed it as AD0001, so this file proves "
            + "nothing:\n"
            + string.Join("\n", crashes.Select(static d => "  " + d.GetMessage()))
        );
    }

    public static TheoryData<RuleFixture> Fixtures {
        get {
            var data = new TheoryData<RuleFixture>();
            foreach (var fixture in RuleFixtures.All().Where(static f => f.RuleId == RuleIds.RegexWithoutTimeout)) {
                data.Add(fixture);
            }

            return data;
        }
    }

    static Diagnostic[] Findings(string pattern) =>
        Analyze(pattern).Where(static d => d.Id == RuleIds.RegexWithoutTimeout).ToArray();

    /// <summary>
    ///     ⚠ The pattern goes in as a verbatim string literal, so a `"` inside it would end the literal
    ///     and change what is being tested. No case here contains one.
    /// </summary>
    static ImmutableArray<Diagnostic> Analyze(string pattern) {
        var source = "using System.Text.RegularExpressions;\n"
            + "public static class Probe {\n"
            + "    public static bool Looks(string input) => Regex.IsMatch(input, @\""
            + pattern
            + "\");\n"
            + "}\n";
        var compilation = RuleFixtures.Compile(source, "probe.cs");

        Assert.DoesNotContain(
            compilation.GetDiagnostics(TestContext.Current.CancellationToken),
            static d => d.Severity == DiagnosticSeverity.Error
        );

        return RuleFixtures.Analyze(compilation, Analyzers, TestContext.Current.CancellationToken);
    }
}
