using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Rules.Async;
using Rikarin.Skala.Rules.Metadata;
using System.Collections.Immutable;
using System.Globalization;

namespace Rikarin.Skala.Rules.Tests;

/// <summary>
///     The rules that ask whether a declared disposal contract is actually kept.
/// </summary>
/// <remarks>
///     ⚠ <c>SK3502</c> is in the analyzer list on purpose. <c>SK3530</c> is the half of one ownership
///     predicate that <c>SK3502</c> is the negation of, and "exactly one of the two speaks about a
///     given field" is a property of the pair rather than of either rule — asserting it needs both
///     running at once.
/// </remarks>
public sealed class DisposalContractBatchTests {
    static readonly ImmutableArray<DiagnosticAnalyzer> Analyzers = [
        new UndisposedOwnedFieldAnalyzer(), new OwnedDisposableFieldAnalyzer(), new DisposeAsyncBaseCallAnalyzer(),
    ];

    public static TheoryData<RuleFixture> Fixtures {
        get {
            var data = new TheoryData<RuleFixture>();
            foreach (var fixture in RuleFixtures.All().Where(static f => f.RuleId is "SK3530" or "SK3531")) {
                data.Add(fixture);
            }

            return data;
        }
    }

    [Theory]
    [MemberData(nameof(Fixtures))]
    public void Fixtures_HaveExactCountsAndCarryTheirFix(RuleFixture fixture) {
        var findings = Analyze(RuleFixtures.Compile(File.ReadAllText(fixture.Path), fixture.Path))
            .Where(diagnostic => diagnostic.Id == fixture.RuleId)
            .ToArray();

        Assert.Equal(fixture.ShouldFire ? 1 : 0, findings.Length);

        var fixable = RuleCatalog.Get(fixture.RuleId).HasFix;
        Assert.All(findings, d => Assert.Equal(fixable, d.Properties.ContainsKey(FixEdits.CountKey)));
    }

    /// <summary>
    ///     ⚠ Neither rule ever speaks where the other does, and it is arithmetic rather than luck.
    /// </summary>
    /// <remarks>
    ///     <c>SK3530</c> requires the owner to implement <c>IDisposable</c>; <c>SK3502</c> reports only
    ///     where the owner implements neither contract its field offers. One predicate is the other's
    ///     negation, so no <c>supersedes</c> is involved — which matters, because
    ///     <c>Supersession.Apply</c> suppresses the *superseded* finding and here that would be the one
    ///     carrying the fix.
    /// </remarks>
    [Theory]
    [MemberData(nameof(BothFamilies))]
    public void AFieldIsNeverReportedByBothOwnershipRules(RuleFixture fixture) {
        var findings = Analyze(RuleFixtures.Compile(File.ReadAllText(fixture.Path), fixture.Path));

        Assert.False(
            findings.Any(static d => d.Id == "SK3530") && findings.Any(static d => d.Id == "SK3502"),
            $"{fixture}: SK3530 and SK3502 both reported. Their predicates are each other's negation; "
            + "if both can speak, one of the two owner tests has been widened."
        );
    }

    public static TheoryData<RuleFixture> BothFamilies {
        get {
            var data = new TheoryData<RuleFixture>();
            foreach (var fixture in RuleFixtures.All().Where(static f => f.RuleId is "SK3530" or "SK3502")) {
                data.Add(fixture);
            }

            return data;
        }
    }

    /// <summary>
    ///     ⚠ The guard that stops the rule reporting every faithful <c>CA1063</c> implementation.
    /// </summary>
    /// <remarks>
    ///     Reading only <c>Dispose()</c>'s own body — rather than the whole declaration — makes this
    ///     file report, and the reported type is correct. That is the sabotage this fixture is for, and
    ///     it is the single widening most likely to be made by somebody who reads the rule's title.
    /// </remarks>
    [Fact]
    public void TheDocumentedPattern_IsNotReported() {
        var source = File.ReadAllText(
            Path.Combine(RuleFixtures.Root, "SK3530", "negative", "the-work-is-in-the-dispose-bool-overload.cs")
        );

        Assert.DoesNotContain(Analyze(RuleFixtures.Compile(source, "pattern.cs")), static d => d.Id == "SK3530");
    }

    /// <summary>⚠ The empty body is a different edit, because inserting after `{` of `{ }` is not one.</summary>
    [Fact]
    public void TheFix_FillsAnEmptyDisposeBody() {
        var source = File.ReadAllText(
            Path.Combine(RuleFixtures.Root, "SK3530", "positive", "an-empty-dispose-body.cs")
        );

        Assert.Contains(
            "    public void Dispose() {\n        semaphore.Dispose();\n    }",
            Apply(source, "SK3530"),
            StringComparison.Ordinal
        );
    }

    /// <summary>The disposal goes first, where the pattern puts owned resources.</summary>
    [Fact]
    public void TheFix_PutsTheDisposalAtTheTopOfAnExistingBody() {
        var source = File.ReadAllText(
            Path.Combine(RuleFixtures.Root, "SK3530", "positive", "a-dispose-that-only-records-the-fact.cs")
        );

        Assert.Contains(
            "    public void Dispose() {\n        stream.Dispose();\n        closed = true;\n    }",
            Apply(source, "SK3530"),
            StringComparison.Ordinal
        );
    }

    /// <summary>
    ///     ⚠ The two guards that make every <c>SK3531</c> finding provable rather than probable.
    /// </summary>
    /// <remarks>
    ///     Dropping either one is the widening the rule is most likely to suffer, and each costs a
    ///     different kind of wrong report: without the "the base does work" test the rule asks every
    ///     leaf of every hook-shaped hierarchy to call a no-op, and without the abstract test it asks
    ///     for a call to a method that has no body at all — <c>CS0205</c>, a fix that cannot compile.
    /// </remarks>
    [Theory]
    [InlineData("the-base-does-no-work")]
    [InlineData("the-base-is-abstract")]
    public void ABaseWithNothingToLose_IsNotReported(string name) {
        var source = File.ReadAllText(Path.Combine(RuleFixtures.Root, "SK3531", "negative", name + ".cs"));

        Assert.DoesNotContain(Analyze(RuleFixtures.Compile(source, name + ".cs")), static d => d.Id == "SK3531");
    }

    [Fact]
    public void GeneratedCode_IsIgnored() {
        var source = "// <auto-generated/>\n"
            + File.ReadAllText(Path.Combine(RuleFixtures.Root, "SK3530", "positive", "an-empty-dispose-body.cs"));

        Assert.Empty(Analyze(RuleFixtures.Compile(source, "generated.cs")));
    }

    static ImmutableArray<Diagnostic> Analyze(CSharpCompilation compilation) =>
        RuleFixtures.Analyze(compilation, Analyzers, TestContext.Current.CancellationToken);

    static string Apply(string source, string id) {
        var diagnostic = Assert.Single(Analyze(RuleFixtures.Compile(source, "probe.cs")).Where(d => d.Id == id));

        var count = int.Parse(diagnostic.Properties[FixEdits.CountKey]!, CultureInfo.InvariantCulture);
        var edits = Enumerable.Range(0, count)
            .Select(index => new TextChange(
                    new TextSpan(
                        int.Parse(diagnostic.Properties[FixEdits.StartKey(index)]!, CultureInfo.InvariantCulture),
                        int.Parse(diagnostic.Properties[FixEdits.LengthKey(index)]!, CultureInfo.InvariantCulture)
                    ),
                    diagnostic.Properties[FixEdits.TextKey(index)]!
                )
            );

        return SourceText.From(source).WithChanges(edits).ToString();
    }
}
