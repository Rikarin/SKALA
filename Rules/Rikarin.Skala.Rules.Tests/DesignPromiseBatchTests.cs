using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Rules.Design;
using Rikarin.Skala.Rules.Metadata;
using System.Collections.Immutable;
using System.Globalization;

namespace Rikarin.Skala.Rules.Tests;

/// <summary>
///     The declaration rules that report a promise the declaration does not keep, with exact counts.
/// </summary>
/// <remarks>
///     ⚠ The same guard <see cref="DesignDeclarationBatchTests" /> exists for, one range along.
///     <see cref="RuleFixtureTests.Rule_FiresExactlyWhereTheFixtureSaysItShould" /> asks only whether a
///     positive fixture produced <em>anything</em>, and every rule in this batch reads a declaration
///     that a partial type has more than one of — so a rule that reports per declaration where it
///     should report per symbol passes that test while doubling every count in a report.
/// </remarks>
public sealed class DesignPromiseBatchTests {
    static readonly ImmutableArray<DiagnosticAnalyzer> Analyzers = [
        new GlobalNamespaceTypeAnalyzer(), new ReadonlyMutableFieldAnalyzer(),
        new AbstractTypeWithoutAbstractionAnalyzer(),
        new PrivateConstructorOnlyAnalyzer(),
        new PublicConstantAnalyzer()
    ];

    static readonly string[] Ids = ["SK6030", "SK6031", "SK6032", "SK6033", "SK6034"];

    public static TheoryData<RuleFixture> Fixtures {
        get {
            var data = new TheoryData<RuleFixture>();
            foreach (var fixture in RuleFixtures.All()
                         .Where(static fixture => Ids.Contains(fixture.RuleId, StringComparer.Ordinal))) {
                data.Add(fixture);
            }

            return data;
        }
    }

    [Theory]
    [MemberData(nameof(Fixtures))]
    public void EveryFixture_ProducesExactlyTheCountItClaims(RuleFixture fixture) {
        var findings = Analyze(fixture);
        Assert.Equal(fixture.ShouldFire ? 1 : 0, findings.Length);
    }

    /// <summary>
    ///     ⚠ <c>SK6034</c>'s exemption is a claim the project makes, and it is keyed on nothing else.
    /// </summary>
    /// <remarks>
    ///     ⚠ <b>#330 was filed with a remedy that keyed the exemption on this repository's
    ///     <c>allocated-ids.txt</c> and on the type names <c>RuleIds</c>/<c>ExitCodes</c>, and the issue
    ///     then corrected itself: that is one codebase's layout carried inside a rule that ships
    ///     elsewhere.</b> This test is what says the analyzer holds no such knowledge — the same type,
    ///     the same constant, and only the configured value moves.
    /// </remarks>
    [Fact]
    public void TheFrozenConstantList_IsTheOnlyThingThatExemptsAType() {
        const string source = """
                              public static class RuleIds {
                                  public const string PublicConstantField = "SK6034";
                              }
                              """;

        Assert.Single(Findings(source, null));

        // ⚠ The name is `RuleIds` in all three, so a rule that recognised it would be silent in the
        // first as well.
        Assert.Single(Findings(source, "ExitCodes, ProtocolVersions"));
        Assert.Empty(Findings(source, "ExitCodes, RuleIds"));
    }

    /// <summary>⚠ The fully qualified spelling works too, for a consumer with two types of one name.</summary>
    [Fact]
    public void TheFrozenConstantList_AcceptsAFullyQualifiedName() {
        const string source = """
                              namespace Wire.Protocol;

                              public static class Versions {
                                  public const string Current = "v3";
                              }
                              """;

        Assert.Empty(Findings(source, "Wire.Protocol.Versions"));
        Assert.Single(Findings(source, "Other.Versions"));
    }

    /// <summary>
    ///     ⚠ <c>CA1805</c>: the initialiser goes with the keyword when it is already the default (#330).
    /// </summary>
    [Fact]
    public void TheConstantFix_DropsAnInitialiserThatIsAlreadyTheDefault() {
        const string source = """
                              public static class Codes {
                                  public const int Ok = 0;
                              }
                              """;

        Assert.Contains("public static readonly int Ok;", Apply(source), StringComparison.Ordinal);
    }

    /// <summary>⚠ A value that is not the default keeps its initialiser, or the fix would change it.</summary>
    [Fact]
    public void TheConstantFix_KeepsAnInitialiserThatCarriesAValue() {
        const string source = """
                              public static class Codes {
                                  public const int GateFailed = 1;
                              }
                              """;

        Assert.Contains("public static readonly int GateFailed = 1;", Apply(source), StringComparison.Ordinal);
    }

    /// <summary>
    ///     ⚠ <c>null</c> is a reference type's default and the initialiser stays, because CS8618.
    /// </summary>
    /// <remarks>
    ///     Dropping it would leave an uninitialised non-nullable field, which trades this rule's finding
    ///     for a worse one — so the drop is restricted to value types.
    /// </remarks>
    [Fact]
    public void TheConstantFix_KeepsANullInitialiserOnAReferenceType() {
        const string source = """
                              public static class Codes {
                                  public const string Unset = null;
                              }
                              """;

        Assert.Contains("public static readonly string Unset = null;", Apply(source), StringComparison.Ordinal);
    }

    static Diagnostic[] Analyze(RuleFixture fixture) {
        var compilation = RuleFixtures.Compile(File.ReadAllText(fixture.Path), fixture.Path);

        return RuleFixtures
            .Analyze(compilation, Analyzers, TestContext.Current.CancellationToken)
            .Where(diagnostic => diagnostic.Id == fixture.RuleId)
            .ToArray();
    }

    /// <summary>
    ///     <c>SK6034</c> over one source, with the frozen-constant key set or absent.
    /// </summary>
    static Diagnostic[] Findings(string source, string? frozen) {
        var configured = frozen is null
            ? source
            : "// analyzer-option: dotnet_code_quality.SK6034.frozen_constant_types = "
            + frozen
            + Environment.NewLine
            + source;

        return RuleFixtures
            .Analyze(
                RuleFixtures.Compile(configured, "Frozen.cs"),
                Analyzers,
                TestContext.Current.CancellationToken
            )
            .Where(static diagnostic => diagnostic.Id == RuleIds.PublicConstantField)
            .ToArray();
    }

    static string Apply(string source) {
        var diagnostic = Assert.Single(Findings(source, null));
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
