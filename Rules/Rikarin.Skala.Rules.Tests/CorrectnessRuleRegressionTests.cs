using Microsoft.CodeAnalysis;
using Rikarin.Skala.Rules.Correctness;

namespace Rikarin.Skala.Rules.Tests;

public sealed class CorrectnessRuleRegressionTests {
    [Theory]
    [InlineData("ToLower")]
    [InlineData("ToUpper")]
    public void ParenthesizedCasingComparison_IsReported(string method) {
        var source = "class C { bool M(string a, string b) => ((a." + method + "())) == b; }";
        var compilation = RuleFixtures.Compile(source, "casing.cs");
        Assert.DoesNotContain(
            compilation.GetDiagnostics(TestContext.Current.CancellationToken),
            static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error
        );
        var findings = RuleFixtures.Analyze(
            compilation,
            [new ImplicitStringCultureAnalyzer()],
            TestContext.Current.CancellationToken
        );
        Assert.Single(findings);
        Assert.Equal("SK2010", findings[0].Id);
    }

    [Fact]
    public void PartialEqualityContract_ReportsOnlyOnce() {
        const string source = """
                              using System;
                              partial class Key : IEquatable<Key> { }
                              partial class Key { public bool Equals(Key? other) => other is not null; }
                              """;
        var compilation = RuleFixtures.Compile(source, "partial.cs");
        Assert.DoesNotContain(
            compilation.GetDiagnostics(TestContext.Current.CancellationToken),
            static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error
        );
        var findings = RuleFixtures.Analyze(
            compilation,
            [new IncompleteEqualityContractAnalyzer()],
            TestContext.Current.CancellationToken
        );
        Assert.Single(findings);
        Assert.Equal("SK2004", findings[0].Id);
    }

    [Theory]
    [InlineData("nameof(i)", 0)]
    [InlineData("i", 1)]
    public void StoredDelegate_UsesSemanticCaptureAnalysis(string argument, int expected) {
        var source = "using System; using System.Collections.Generic; class C { "
            + "readonly List<Action> actions = new(); void M() { for (var i = 0; i < 3; i++) { "
            + "actions.Add(() => Console.WriteLine("
            + argument
            + ")); } } }";
        var compilation = RuleFixtures.Compile(source, "captures.cs");
        Assert.DoesNotContain(
            compilation.GetDiagnostics(TestContext.Current.CancellationToken),
            static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error
        );
        var findings = RuleFixtures.Analyze(
            compilation,
            [new CapturedLoopVariableAnalyzer()],
            TestContext.Current.CancellationToken
        );
        Assert.Equal(expected, findings.Length);
        Assert.All(findings, static diagnostic => Assert.Equal("SK2008", diagnostic.Id));
    }
}
