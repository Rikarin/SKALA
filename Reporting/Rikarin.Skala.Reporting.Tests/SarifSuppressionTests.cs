using Rikarin.Skala.Core.Diagnostics;
using Rikarin.Skala.Rules.Metadata;
using System.Text.Json;

namespace Rikarin.Skala.Reporting.Tests;

/// <summary>
///     What the uploaded SARIF claims, held against what the gate decided.
/// </summary>
/// <remarks>
///     ⚠ The defect these exist for: the baseline governed the verdict and was invisible in the file.
///     Every accepted finding went to code scanning with no <c>suppressions</c> entry, so a page that
///     answers "what is wrong with master" listed 428 long-accepted findings as open alerts, while the
///     gate that read the same run counted 0 new. The page and the verdict were computed from one run
///     and still disagreed by 428.
/// </remarks>
public sealed class SarifSuppressionTests {
    static readonly string Root = Path.GetFullPath("/tmp/repo");

    static string[] Strings(JsonElement array, Func<JsonElement, JsonElement> select) =>
        [.. array.EnumerateArray().Select(element => select(element).GetString() ?? string.Empty)];

    static Finding At(string ruleId, int line, SkalaSeverity severity = SkalaSeverity.Warning) =>
        new() {
            RuleId = ruleId,
            Severity = severity,
            Message = ruleId + " fired at " + line,
            Path = Path.Combine(Root, "Core", "Foo.cs"),
            Line = line,
            Column = 1,
            EndLine = line,
            EndColumn = 9,
            Start = line * 10,
            Length = 8
        };

    static RunReport Baselined(params Finding[] findings) =>
        new() {
            RepositoryRoot = Root,
            Mode = LoadMode.Loose,
            Findings = [.. findings],
            HasBaseline = true,
            BaselineSummary = ".skala/baseline.sarif (2 accepted)"
        };

    static JsonElement Results(RunReport report) =>
        JsonDocument.Parse(SarifWriter.Serialize(SarifWriter.Build(report)))
            .RootElement
                .GetProperty("runs")[0]
                .GetProperty("results");

    /// <summary>
    ///     ⚠ Suppressed, and still present. Both halves are the assertion.
    /// </summary>
    /// <remarks>
    ///     Dropping the accepted findings instead would be a different and false claim — "this run did
    ///     not find them" rather than "this repository has accepted them" — and <c>skala report</c>, the
    ///     PR comment and the stored-verdict path all render from this same file (ADR-009), so the
    ///     numbers on every one of them would move.
    /// </remarks>
    [Fact]
    public void AnAcceptedFinding_IsSuppressedRatherThanDropped() {
        var report = Baselined(
            At("SK2001", 10) with { Bucket = BaselineBucket.Existing },
            At("SK2001", 20) with { Bucket = BaselineBucket.New }
        );

        var results = Results(report);
        Assert.Equal(2, results.GetArrayLength());

        var accepted = results[0].GetProperty("suppressions");
        Assert.Equal(1, accepted.GetArrayLength());
        Assert.Equal("external", accepted[0].GetProperty("kind").GetString());
        Assert.Equal("accepted", accepted[0].GetProperty("status").GetString());

        // ⚠ The justification names the file. "external" alone says a tool outside SARIF dismissed
        // this and not which one, and the answer is a reviewed, committed artefact.
        Assert.Contains(
            Baseline.DefaultRelativePath,
            accepted[0].GetProperty("justification").GetString()!,
            StringComparison.Ordinal
        );

        Assert.Equal(
            SarifWriter.BaselineSuppressionSource,
            accepted[0].GetProperty("properties").GetProperty(SarifWriter.SuppressionSourceProperty).GetString()
        );

        Assert.False(results[1].TryGetProperty("suppressions", out _));
    }

    /// <summary>
    ///     ⚠ <b>The invariant the code-scanning page rests on.</b> An alert is open exactly when its
    ///     result carries no <c>suppressions</c>, so "open on the page" has to be the same set as
    ///     <see cref="RunReport.New" /> — which is what the <c>ci</c> gate's <c>newIssues</c> counts.
    /// </summary>
    /// <remarks>
    ///     It holds by construction rather than by agreement: <c>CheckCommand</c> evaluates the gate
    ///     against one <see cref="RunReport" />, stores the verdict on it and writes the SARIF from the
    ///     same object, and both sides of this assertion are functions of the same
    ///     <see cref="Finding.Bucket" />. This test is what fails if a later change gives either side
    ///     its own opinion.
    /// </remarks>
    [Fact]
    public void TheOpenResults_AreExactlyWhatTheGateCountsAsNew() {
        var report = Baselined(
            At("SK2001", 10) with { Bucket = BaselineBucket.Existing },
            At("SK2001", 20) with { Bucket = BaselineBucket.Existing },
            At("SK3002", 30, SkalaSeverity.Error) with { Bucket = BaselineBucket.New },
            At("SK1010", 40, SkalaSeverity.Info) with {
                Bucket = BaselineBucket.Existing, Suppression = SuppressionKind.Pragma
            }
        );

        var gate = Gate.Evaluate(
            new GateDefinition { Name = "ci", MaxNewIssues = 0, BaselinePath = Baseline.DefaultRelativePath },
            report,
            true
        );

        var open = Results(report with { Gate = gate })
            .EnumerateArray()
            .Where(static result => !result.TryGetProperty("suppressions", out _))
            .Select(static result => result.GetProperty("ruleId").GetString() ?? string.Empty)
            .ToArray();

        Assert.Equal(report.New.Select(static finding => finding.RuleId).ToArray(), open);

        // Anti-vacuity: naming the one result that should be open, so a change that suppressed
        // everything could not satisfy the equality above by emptying both sides.
        Assert.Equal("SK3002", Assert.Single(open));

        // Anti-vacuity: the gate really did fail on that one finding, so a page showing one open
        // alert and a build showing one new finding are the same statement.
        Assert.False(gate.Passed);
        Assert.Contains("1 new finding(s)", Assert.Single(gate.Failures), StringComparison.Ordinal);
    }

    /// <summary>
    ///     ⚠ A finding can be suppressed in the source <em>and</em> accepted by the baseline, and the
    ///     two are separate claims. <c>Baseline.Write</c> writes suppressed findings for exactly this
    ///     reason: removing the pragma must not turn an accepted finding new.
    /// </summary>
    [Fact]
    public void APragmaAndTheBaseline_AreTwoSuppressionsAndNeitherHidesTheOther() {
        var report = Baselined(
            At("SK1010", 10) with { Bucket = BaselineBucket.Existing, Suppression = SuppressionKind.Pragma }
        );

        var suppressions = Results(report)[0].GetProperty("suppressions");
        Assert.Equal(2, suppressions.GetArrayLength());
        Assert.Equal(
            ["pragma", SarifWriter.BaselineSuppressionSource],
            Strings(
                suppressions,
                static s => s.GetProperty("properties").GetProperty(SarifWriter.SuppressionSourceProperty)
            )
        );

        Assert.Equal(["inSource", "external"], Strings(suppressions, static s => s.GetProperty("kind")));
    }

    /// <summary>
    ///     ⚠ <b>The half of the change that is easy to miss.</b> <see cref="SarifReader" /> answered
    ///     "there is at least one suppression" with <see cref="SuppressionKind.Pragma" />, so writing
    ///     baseline suppressions would have made every accepted finding read back as a pragma — and
    ///     <see cref="RunReport.Reportable" /> drops suppressed findings, which is the gate's own input.
    ///     A repository with 428 accepted findings would have re-rendered as one with 18.
    /// </summary>
    [Fact]
    public void ReadingBack_KeepsTheBaselineOutOfTheFindingsOwnSuppression() {
        var report = Baselined(
            At("SK2001", 10) with { Bucket = BaselineBucket.Existing },
            At("SK2001", 20) with { Bucket = BaselineBucket.New },
            At("SK1010", 30) with { Bucket = BaselineBucket.Existing, Suppression = SuppressionKind.Attribute },
            At("SK7001", 40) with { Bucket = BaselineBucket.New, Suppression = SuppressionKind.Superseded }
        );

        var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".sarif");
        try {
            File.WriteAllText(path, SarifWriter.Serialize(SarifWriter.Build(report)));
            var round = SarifReader.Read(path, Root);

            Assert.Equal(
                [SuppressionKind.None, SuppressionKind.None, SuppressionKind.Attribute, SuppressionKind.Superseded],
                round.Findings.Select(static finding => finding.Suppression).ToArray()
            );

            Assert.Equal(
                [
                    BaselineBucket.Existing, BaselineBucket.New, BaselineBucket.Existing,
                    BaselineBucket.New
                ],
                round.Findings.Select(static finding => finding.Bucket).ToArray()
            );

            Assert.Equal(report.Reportable.Count(), round.Reportable.Count());
        } finally {
            File.Delete(path);
        }
    }

    /// <summary>
    ///     ⚠ A suppression from a SARIF that is not Skala's names no mechanism this build knows, and
    ///     the old reading — "something outside the source made this go away" — is still the safe one.
    /// </summary>
    [Fact]
    public void AForeignSuppression_IsStillReadAsASuppression() {
        var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".sarif");
        try {
            File.WriteAllText(
                path,
                """
                {
                  "version": "2.1.0",
                  "runs": [{
                    "tool": { "driver": { "name": "SomeOtherTool" } },
                    "results": [{
                      "ruleId": "X0001",
                      "level": "warning",
                      "message": { "text": "something" },
                      "suppressions": [{ "kind": "external", "justification": "a tool we do not know" }]
                    }]
                  }]
                }
                """
            );

            var finding = Assert.Single(SarifReader.Read(path, Root).Findings);
            Assert.Equal(SuppressionKind.Pragma, finding.Suppression);
        } finally {
            File.Delete(path);
        }
    }
}

/// <summary>
///     Skala's four severities against SARIF's three failure levels — the mapping in
///     <see cref="SarifSeverity" />, as assertions.
/// </summary>
public sealed class SarifSeverityTests {
    static Finding Finding(SkalaSeverity severity) =>
        new() {
            RuleId = "SK0002",
            Severity = severity,
            Message = "m",
            Path = Path.Combine(Path.GetFullPath("/tmp/repo"), "Core", "Foo.cs"),
            Line = 1,
            Column = 1
        };

    static RunReport Report(params Finding[] findings) =>
        new() { RepositoryRoot = Path.GetFullPath("/tmp/repo"), Mode = LoadMode.Loose, Findings = [.. findings] };

    static string[] Strings(JsonElement array, Func<JsonElement, JsonElement> select) =>
        [.. array.EnumerateArray().Select(element => select(element).GetString() ?? string.Empty)];

    static JsonElement Log(RunReport report) =>
        JsonDocument.Parse(SarifWriter.Serialize(SarifWriter.Build(report))).RootElement
        .GetProperty("runs")[0];

    /// <summary>
    ///     ⚠ <b>52 of 446 results carried no <c>level</c> at all.</b> The SARIF SDK declares
    ///     <c>Result.Level</c> with <c>[DefaultValue(FailureLevel.Warning)]</c>, so the one value Skala
    ///     most deliberately chose serialised as nothing. Nothing downstream was wrong about them —
    ///     SARIF and GitHub both default an absent level to <c>warning</c> — but a file that states the
    ///     severity for three of four values and omits the fourth cannot be diffed or grepped, and the
    ///     absence is indistinguishable from a writer that forgot.
    /// </summary>
    [Theory]
    [InlineData(SkalaSeverity.Error, "error", "error")]
    [InlineData(SkalaSeverity.Warning, "warning", "warning")]
    [InlineData(SkalaSeverity.Info, "note", "suggestion")]
    [InlineData(SkalaSeverity.Hidden, "note", "hint")]
    public void EveryResult_CarriesTheLevelAndTheWordSkalaMeans(
        SkalaSeverity severity,
        string level,
        string word
    ) {
        var result = Log(Report(Finding(severity))).GetProperty("results")[0];
        Assert.Equal(level, result.GetProperty("level").GetString());
        Assert.Equal(word, result.GetProperty("properties").GetProperty(SarifSeverity.Property).GetString());
    }

    /// <summary>
    ///     ⚠ <b>249 of 446 results were <c>level: none</c>, which SARIF does not allow them to be.</b>
    ///     § 3.27.10 permits <c>none</c> only where <c>kind</c> is something other than <c>fail</c>, and
    ///     a rule violation's kind is <c>fail</c>. GitHub's documented vocabulary is <c>error</c>,
    ///     <c>warning</c>, <c>note</c>; it has no rendering for the value Skala was sending, so how the
    ///     page displayed a third of its own report was undefined rather than merely wrong.
    /// </summary>
    [Fact]
    public void NoResult_IsEverLevelNone() {
        var results = Log(Report([.. Enum.GetValues<SkalaSeverity>().Select(Finding)]))
            .GetProperty("results");

        Assert.Equal(Enum.GetValues<SkalaSeverity>().Length, results.GetArrayLength());
        Assert.DoesNotContain("none", Strings(results, static r => r.GetProperty("level")));
    }

    /// <summary>
    ///     ⚠ SARIF's failure scale bottoms out at <c>note</c>, so <c>hint</c> and <c>suggestion</c> land
    ///     on the same level. <see cref="SarifSeverity.Property" /> is what stops that being a loss —
    ///     <see cref="History" /> records the hint count as a compared number, and folding it into the
    ///     suggestion count on every round trip would move a recorded metric with nothing saying so.
    /// </summary>
    [Theory]
    [InlineData(SkalaSeverity.Error)]
    [InlineData(SkalaSeverity.Warning)]
    [InlineData(SkalaSeverity.Info)]
    [InlineData(SkalaSeverity.Hidden)]
    public void TheSeverity_SurvivesTheRoundTripEvenWhereTheLevelCannotHoldIt(SkalaSeverity severity) {
        var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".sarif");
        try {
            var report = Report(Finding(severity));
            File.WriteAllText(path, SarifWriter.Serialize(SarifWriter.Build(report)));
            Assert.Equal(severity, Assert.Single(SarifReader.Read(path, report.RepositoryRoot).Findings).Severity);
        } finally {
            File.Delete(path);
        }
    }

    /// <summary>
    ///     ⚠ A rules table whose <c>defaultConfiguration.level</c> disagrees with the level the rule's
    ///     own results carry cannot be used to explain those results. The two mappings are one method
    ///     pair in <see cref="SarifSeverity" /> so they cannot drift; this asserts they have not.
    /// </summary>
    [Fact]
    public void EveryRule_StatesItsLevelAndAgreesWithTheResultsItProduces() {
        var rules = Log(Report()).GetProperty("tool").GetProperty("driver").GetProperty("rules");
        Assert.Equal(RuleCatalog.All.Count, rules.GetArrayLength());

        var expected = new Dictionary<RuleSeverity, string> {
            [RuleSeverity.Error] = "error",
            [RuleSeverity.Warning] = "warning",
            [RuleSeverity.Suggestion] = "note",
            [RuleSeverity.Hint] = "note",

            // ⚠ `None` is "never runs, never reported" — not a level. See the `enabled` assertion below.
            [RuleSeverity.None] = "none"
        };

        foreach (var rule in rules.EnumerateArray()) {
            var catalogued = RuleCatalog.All.Single(entry => entry.Id == rule.GetProperty("id").GetString());
            var configuration = rule.GetProperty("defaultConfiguration");

            Assert.Equal(expected[catalogued.DefaultSeverity], configuration.GetProperty("level").GetString());
            Assert.Equal(
                catalogued.DefaultSeverity.ToString().ToLowerInvariant(),
                rule.GetProperty("properties").GetProperty("defaultSeverity").GetString()
            );

            // SARIF's own way of saying "this rule does not run", rather than a level that means it.
            Assert.Equal(
                catalogued.DefaultSeverity != RuleSeverity.None,
                !configuration.TryGetProperty("enabled", out var enabled) || enabled.GetBoolean()
            );
        }
    }
}
