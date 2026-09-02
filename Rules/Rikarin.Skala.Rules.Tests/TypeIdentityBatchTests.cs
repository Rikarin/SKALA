using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Rikarin.Skala.Rules.Correctness;
using System.Collections.Immutable;

namespace Rikarin.Skala.Rules.Tests;

/// <summary>
///     The batch level for <c>SK2180</c>–<c>SK2184</c>: what the fixture harness cannot ask.
/// </summary>
/// <remarks>
///     ⚠ <b>A crashed analyzer passes every negative fixture.</b> Roslyn catches the exception, reports
///     it as <c>AD0001</c>, and the analyzer then produces nothing at all — so the positives fail,
///     which reads as "the rule needs another condition", and every "should not fire" fixture passes,
///     which reads as a spotless false-positive record. The fixture harness does not look for
///     <c>AD0001</c> (issue #279) and <c>skala check</c> records it only into the SARIF's
///     <c>toolExecutionNotifications</c> without failing the gate (issue #295), so these tests do.
///     <para>
///         ⚠ This batch has a specific reason to worry. Every one of the five rules resolves a symbol
///         and then reads something off it, and one of them calls
///         <c>Compilation.IsSymbolAccessibleWithin</c>, which <b>throws</b> for a <c>within</c>
///         argument that is neither a type nor an assembly — a call that killed a rule for a whole
///         batch before this one.
///     </para>
/// </remarks>
public sealed class TypeIdentityBatchTests {
    static readonly ImmutableArray<string> Ids = ["SK2180", "SK2181", "SK2182", "SK2183", "SK2184"];

    static readonly ImmutableArray<DiagnosticAnalyzer> Analyzers = [
        new ForeachElementDowncastAnalyzer(), new GetTypeOnATypeAnalyzer(),
        new TypeComparedByNameAnalyzer(), new StaticMemberViaDerivedTypeAnalyzer(),
        new HiddenBaseInterfaceOverloadAnalyzer()
    ];

    /// <summary>Every fixture in the batch, asserting only that no analyzer threw.</summary>
    [Theory]
    [MemberData(nameof(Fixtures))]
    public void NoFixture_CrashesAnAnalyzer(string path) {
        var diagnostics = RuleFixtures.Analyze(
            RuleFixtures.Compile(File.ReadAllText(path), path),
            Analyzers,
            TestContext.Current.CancellationToken
        );

        Assert.DoesNotContain(diagnostics, static diagnostic => diagnostic.Id == "AD0001");
    }

    public static TheoryData<string> Fixtures {
        get {
            var data = new TheoryData<string>();
            foreach (var fixture in RuleFixtures.All()) {
                if (Ids.Contains(fixture.RuleId)) {
                    data.Add(fixture.Path);
                }
            }

            return data;
        }
    }

    /// <summary>
    ///     ⚠ Anti-vacuity for the test above: an analyzer set that never runs also never crashes.
    /// </summary>
    [Fact]
    public void TheFixtureSet_ReallyReachesEveryRuleInTheBatch() {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var fixture in RuleFixtures.All()) {
            if (!fixture.ShouldFire || !Ids.Contains(fixture.RuleId)) {
                continue;
            }

            foreach (var diagnostic in RuleFixtures.Analyze(
                         RuleFixtures.Compile(File.ReadAllText(fixture.Path), fixture.Path),
                         Analyzers,
                         TestContext.Current.CancellationToken
                     )) {
                seen.Add(diagnostic.Id);
            }
        }

        Assert.Equal(Ids, seen.Order(StringComparer.Ordinal));
    }

    /// <summary>
    ///     The refutations, as tests rather than as prose in doc 08.
    /// </summary>
    /// <remarks>
    ///     ⚠ Each of these is a shape an issue in this batch asked for and the <b>compiler</b> already
    ///     owns. The claim being pinned is that the source does not build — so the day one of these
    ///     stops being an error, this file goes red and the refutation gets re-examined instead of
    ///     quietly outliving its evidence.
    /// </remarks>
    [Theory]
    [InlineData("CS0030", "class B { } sealed class S { } static class M { static S F(B b) => (S)b; }")]
    [InlineData("CS0030", "interface I { } sealed class S { } static class M { static I F(S s) => (I)s; }")]
    [InlineData(
        "CS0229",
        "interface L { int V { get; } } interface R { int V { get; } } interface B : L, R { } "
        + "static class M { static int F(B b) => b.V; }"
    )]
    [InlineData(
        "CS0121",
        "interface L { void Run(); } interface R { void Run(); } interface B : L, R { } "
        + "static class M { static void F(B b) => b.Run(); }"
    )]
    public void TheCompilerAlreadyOwnsThisShape(string id, string source) {
        var errors = RuleFixtures.Compile(source, "refutation.cs")
            .GetDiagnostics(TestContext.Current.CancellationToken)
            .Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .Select(static diagnostic => diagnostic.Id)
            .ToArray();

        Assert.Contains(id, errors);
    }

    /// <summary>
    ///     ⚠ The binding <c>SK2184</c> exists for, pinned as an executable fact.
    /// </summary>
    /// <remarks>
    ///     The rule's whole premise is that a derived interface's overload takes the name and the base
    ///     interface's better-matching overload becomes unreachable. If Roslyn ever bound this the
    ///     other way, the rule would be reporting correct code and nothing else in the suite would
    ///     notice — the positive fixtures would keep passing, because they assert that the rule fires
    ///     rather than that the premise holds.
    /// </remarks>
    [Fact]
    public void ADerivedInterfaceOverload_HidesTheBetterBaseOverload() {
        const string source = """
                              interface IParent {
                                  string M(string s);
                              }

                              interface IChild : IParent {
                                  string M(object o);
                              }

                              static class Call {
                                  public static string ThroughChild(IChild c) => c.M("literal");

                                  public static string ThroughParent(IParent p) => p.M("literal");
                              }
                              """;

        var compilation = RuleFixtures.Compile(source, "binding.cs");
        var tree = compilation.SyntaxTrees.Single();
        var model = compilation.GetSemanticModel(tree);
        var calls = tree.GetRoot(TestContext.Current.CancellationToken)
            .DescendantNodes()
            .OfType<Microsoft.CodeAnalysis.CSharp.Syntax.InvocationExpressionSyntax>()
            .Select(node => (IMethodSymbol)model.GetSymbolInfo(node, TestContext.Current.CancellationToken).Symbol!)
            .ToArray();

        Assert.Equal(2, calls.Length);
        Assert.Equal("IChild", calls[0].ContainingType.Name);
        Assert.Equal("object", calls[0].Parameters[0].Type.ToDisplayString());
        Assert.Equal("IParent", calls[1].ContainingType.Name);
        Assert.Equal("string", calls[1].Parameters[0].Type.ToDisplayString());
    }
}
