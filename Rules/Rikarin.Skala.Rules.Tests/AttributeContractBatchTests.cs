using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Rikarin.Skala.Rules.Correctness;
using Rikarin.Skala.Rules.Metadata;
using System.Collections.Immutable;

namespace Rikarin.Skala.Rules.Tests;

/// <summary>
///     <c>SK2100</c>–<c>SK2103</c>: four rules that read an attribute and the declaration it is on.
/// </summary>
/// <remarks>
///     ⚠ The fixture harness cannot see the one failure this batch is most exposed to. A Roslyn
///     analyzer that throws is swallowed as <c>AD0001</c> and produces nothing at all, so its positive
///     fixtures fail and <b>every one of its negative fixtures passes</b> (#279) — and attribute
///     resolution throws exactly where a symbol did not resolve.
///     <see cref="NoAnalyzerThrows_OnAnyFixtureInTheBatch" /> is that gap closed for these four.
/// </remarks>
public sealed class AttributeContractBatchTests {
    static readonly ImmutableArray<DiagnosticAnalyzer> Batch = [
        new IneffectiveThreadStaticAnalyzer(), new PureAttributeOnVoidAnalyzer(),
        new DebuggerDisplayMissingMemberAnalyzer(), new DuplicatedAttributeAnalyzer()
    ];

    static readonly string[] Ids = [
        RuleIds.IneffectiveThreadStatic, RuleIds.PureAttributeOnVoid,
        RuleIds.DebuggerDisplayMissingMember, RuleIds.DuplicatedAttribute
    ];

    /// <summary>
    ///     ⚠ The disjointness assertion issue #269 needs, as a measurement rather than a sentence.
    /// </summary>
    /// <remarks>
    ///     <c>SK2103</c> is broad enough on paper to swallow the other three, and the boundary that
    ///     stops it is structural: the other three read the declaration an attribute is on, and
    ///     <c>SK2103</c> only ever compares two applications of one attribute to each other. Each
    ///     fixture below is a shape a reader would expect both rules to claim. Exactly one does.
    /// </remarks>
    [Theory]
    [InlineData("SK2102", "positive", "boundary_only_the_missing_one.cs", "SK2102")]
    [InlineData("SK2101", "positive", "boundary_two_vendors_of_pure.cs", "SK2101")]
    public void ACrossingShape_ProducesExactlyOneFindingFromTheWholeBatch(
        string directory,
        string folder,
        string file,
        string expected
    ) {
        var path = Path.Combine(RuleFixtures.Root, directory, folder, file);
        Assert.True(File.Exists(path), $"{path} is the fixture this assertion is about and it is missing.");

        var produced = Findings(path);

        var only = Assert.Single(produced);
        Assert.Equal(expected, only.Id);
    }

    /// <summary>
    ///     Both halves of <c>SK2100</c> are true of one declaration, and it is one defect.
    /// </summary>
    [Fact]
    public void AnInstanceFieldWithAnInitializer_IsReportedOnceAsAnInstanceField() {
        var path = Path.Combine(
            RuleFixtures.Root,
            RuleIds.IneffectiveThreadStatic,
            "positive",
            "instance_field_with_initializer.cs"
        );

        var only = Assert.Single(Findings(path));

        Assert.Equal(RuleIds.IneffectiveThreadStatic, only.Id);
        Assert.Contains("not `static`", only.GetMessage(System.Globalization.CultureInfo.InvariantCulture));
    }

    /// <summary>
    ///     ⚠ <c>AD0001</c>, the failure that makes every negative fixture pass.
    /// </summary>
    /// <remarks>
    ///     Roslyn catches an analyzer exception and reports it as <c>AD0001</c> rather than letting it
    ///     escape, so an analyzer that throws on every file looks, to a "should not fire" fixture,
    ///     exactly like an analyzer that correctly declined. This runs the four over every fixture in
    ///     the repository — not only their own — because the analyzer that killed a rule last round
    ///     threw on a fixture belonging to a different one.
    /// </remarks>
    [Fact]
    public void NoAnalyzerThrows_OnAnyFixtureInTheBatch() {
        var fixtures = RuleFixtures.All();
        Assert.NotEmpty(fixtures);

        var failures = new List<string>();
        foreach (var fixture in fixtures) {
            var compilation = RuleFixtures.Compile(File.ReadAllText(fixture.Path), fixture.Path);
            foreach (var diagnostic in RuleFixtures.Analyze(
                         compilation,
                         Batch,
                         TestContext.Current.CancellationToken
                     )) {
                if (string.Equals(diagnostic.Id, "AD0001", StringComparison.Ordinal)) {
                    failures.Add(
                        fixture.Path + ": " + diagnostic.GetMessage(System.Globalization.CultureInfo.InvariantCulture)
                    );
                }
            }
        }

        Assert.True(
            failures.Count == 0,
            "An analyzer in SK2100-SK2103 threw. Roslyn swallows this as AD0001, so the positive "
            + "fixtures fail and every negative fixture passes:\n  "
            + string.Join("\n  ", failures)
        );
    }

    /// <summary>
    ///     No fixture in the batch draws a finding from any rule outside it.
    /// </summary>
    /// <remarks>
    ///     ⚠ The complement of the crossing test. A fixture whose scaffolding trips an unrelated rule
    ///     is a fixture that proves something other than what it says, which is one of the two
    ///     sabotages that failed to fail last round.
    /// </remarks>
    [Fact]
    public void NoFixtureInTheBatch_IsClaimedByAnotherRuleInTheBatch() {
        foreach (var fixture in RuleFixtures.All()) {
            if (Array.IndexOf(Ids, fixture.RuleId) < 0) {
                continue;
            }

            foreach (var diagnostic in Findings(fixture.Path)) {
                Assert.True(
                    string.Equals(diagnostic.Id, fixture.RuleId, StringComparison.Ordinal),
                    $"{fixture}: {diagnostic.Id} also fired on it. Disjointness in this batch is by "
                    + "construction, so a second rule claiming a fixture means a boundary moved."
                );
            }
        }
    }

    static ImmutableArray<Diagnostic> Findings(string path) =>
        RuleFixtures.Analyze(
            RuleFixtures.Compile(File.ReadAllText(path), path),
            Batch,
            TestContext.Current.CancellationToken
        )
            .Where(static diagnostic => Array.IndexOf(Ids, diagnostic.Id) >= 0)
            .ToImmutableArray();
}
