using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Rikarin.Skala.Rules.Metadata;
using Rikarin.Skala.Rules.Modernization;
using System.Collections.Immutable;
using System.Globalization;

namespace Rikarin.Skala.Rules.Tests;

/// <summary>
///     <c>SK1131</c>'s shapes that a fixture cannot hold, because they do not compile, and the two
///     compiler facts its declines rest on.
/// </summary>
/// <remarks>
///     A fixture must compile (<see cref="RuleFixtureTests.Rule_FiresExactlyWhereTheFixtureSaysItShould" />),
///     and every decline here is about a program that does not — either before the rewrite, where the
///     rule must not repair an error by accident, or after it, where the rewrite is the error.
/// </remarks>
public sealed class AnonymousMethodWithParameterListTests {
    static readonly ImmutableArray<DiagnosticAnalyzer> Analyzers = [new AnonymousMethodWithParameterListAnalyzer()];

    /// <summary>
    ///     ⚠ An attribute, a default value or <c>params</c> on an anonymous method's parameter is an
    ///     error at every language version, and legal on a lambda from C# 10 or 12.
    /// </summary>
    /// <remarks>
    ///     The proposal expected these to need a C# 12 floor. Measured, they never compile as anonymous
    ///     methods, so a floor has nothing to gate; what matters is that the rewrite would turn the
    ///     error into a program that compiles and means something nobody wrote. Both halves are asserted:
    ///     the source's error, the lambda's absence of one, and the rule's silence.
    /// </remarks>
    [Theory]
    [InlineData("Func<string, int> f = delegate([NotNull] string s) { return s.Length; };", "CS7014")]
    [InlineData("Defaulted f = delegate(int x = 5) { return x; };", "CS1065")]
    [InlineData("Spread f = delegate(params int[] xs) { return xs.Length; };", "CS1670")]
    public void AParameterOnlyALambdaMayCarry_IsAnErrorAndIsDeclined(string statement, string error) {
        var anonymous = Compile(statement);
        Assert.Contains(
            anonymous.GetDiagnostics(TestContext.Current.CancellationToken),
            diagnostic => diagnostic.Id == error
        );

        var lambda = Compile(
            statement.Replace("delegate(", "(", StringComparison.Ordinal)
                .Replace(") {", ") => {", StringComparison.Ordinal)
        );
        Assert.DoesNotContain(
            lambda.GetDiagnostics(TestContext.Current.CancellationToken),
            static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error
        );

        Assert.Empty(Findings(anonymous));
    }

    /// <summary>
    ///     Converted to an expression tree, or nested inside a lambda that is, the anonymous method is
    ///     already an error, and the lambda is a different one.
    /// </summary>
    [Theory]
    [InlineData("Expression<Func<int, int>> e = delegate(int x) { return x; };", "CS1946")]
    [InlineData("Expression<Func<int, Func<int, int>>> e = y => delegate(int x) { return x + y; };", "CS1945")]
    public void AnExpressionTreeContext_IsDeclined(string statement, string error) {
        var compilation = Compile(statement);
        Assert.Contains(
            compilation.GetDiagnostics(TestContext.Current.CancellationToken),
            diagnostic => diagnostic.Id == error
        );

        Assert.Empty(Findings(compilation));
    }

    /// <summary>
    ///     ⚠ The fact the overload guard rests on: what the rewrite would produce in the two declined
    ///     overload shapes is an error, not merely different.
    /// </summary>
    /// <remarks>
    ///     If a future C# made an anonymous method applicable to an expression tree, or a lambda
    ///     inapplicable, these go red and the guard can be revisited — which is the only way a guard
    ///     whose reason is a compiler rule says so when the rule changes.
    /// </remarks>
    [Theory]
    [InlineData("a-func-and-expression-overload.cs", "CS0121")]
    [InlineData("a-queryable-where.cs", "CS0834")]
    [InlineData("a-constructor-overload.cs", "CS0121")]
    [InlineData("a-collection-initializer-add-overload.cs", "CS0121")]
    public void TheDeclinedOverloadShape_IsAnErrorAsALambda(string fixture, string error) {
        var source = File.ReadAllText(Path.Combine(RuleFixtures.Root, "SK1131", "negative", fixture));
        Assert.DoesNotContain(
            RuleFixtures.Compile(source, fixture).GetDiagnostics(TestContext.Current.CancellationToken),
            static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error
        );

        var rewritten = source.Replace("delegate(int x) {", "(int x) => {", StringComparison.Ordinal)
            .Replace("delegate(int v) {", "(int v) => {", StringComparison.Ordinal);
        Assert.NotEqual(source, rewritten);
        Assert.Contains(
            RuleFixtures.Compile(rewritten, fixture).GetDiagnostics(TestContext.Current.CancellationToken),
            diagnostic => diagnostic.Id == error
        );
    }

    /// <summary>The two edits, applied, are the lambda — modifiers and trivia kept where they were.</summary>
    [Theory]
    [InlineData(
        "Func<int, int> f = delegate(int x){ return x; };",
        "Func<int, int> f = (int x) => { return x; };"
    )]
    [InlineData(
        "Func<int, Task> f = async static delegate (int x) { await Task.Delay(x); };",
        "Func<int, Task> f = async static (int x) => { await Task.Delay(x); };"
    )]
    [InlineData(
        "Func<int, int> f = delegate(int x)\n        {\n            return x;\n        };",
        "Func<int, int> f = (int x) =>\n        {\n            return x;\n        };"
    )]
    public void TheFix_IsTheLambda(string statement, string expected) {
        var compilation = Compile(statement);
        var source = compilation.SyntaxTrees[0].GetText(TestContext.Current.CancellationToken).ToString();
        var finding = Assert.Single(Findings(compilation));

        var properties = finding.Properties;
        var count = int.Parse(properties[FixEdits.CountKey]!, CultureInfo.InvariantCulture);
        var edits = Enumerable.Range(0, count)
            .Select(index => (
                    Start: int.Parse(properties[FixEdits.StartKey(index)]!, CultureInfo.InvariantCulture),
                    Length: int.Parse(properties[FixEdits.LengthKey(index)]!, CultureInfo.InvariantCulture),
                    Text: properties[FixEdits.TextKey(index)]!
                )
            )
            .OrderByDescending(static edit => edit.Start);

        var text = source;
        foreach (var (start, length, replacement) in edits) {
            text = text[..start] + replacement + text[(start + length)..];
        }

        Assert.Equal(Wrap(expected), text);
    }

    static string Wrap(string statement) =>
        "using System;\n"
        + "using System.Diagnostics.CodeAnalysis;\n"
        + "using System.Linq.Expressions;\n"
        + "using System.Threading.Tasks;\n\n"
        + "public delegate int Defaulted(int x = 5);\n\n"
        + "public delegate int Spread(params int[] xs);\n\n"
        + "public static class Probe {\n"
        + "    public static void Run() {\n"
        + "        "
        + statement
        + "\n"
        + "    }\n"
        + "}\n";

    static CSharpCompilation Compile(string statement) => RuleFixtures.Compile(Wrap(statement), "probe.cs");

    static ImmutableArray<Diagnostic> Findings(CSharpCompilation compilation) => [
        ..RuleFixtures.Analyze(compilation, Analyzers, TestContext.Current.CancellationToken)
            .Where(static diagnostic => diagnostic.Id is "AD0001" or RuleIds.AnonymousMethodWithParameterList)
    ];
}
