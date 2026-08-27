using System.Collections.Immutable;
using Rikarin.Skala.Core.Diagnostics;

namespace Rikarin.Skala.Reporting.Tests;

/// <summary>
/// docs/plan/09's lifecycle: the fingerprint, the baseline, the new-code definition and the gate.
/// </summary>
/// <remarks>
/// ⚠ These are the tests that decide whether the analysis half is adoptable. A rule that
/// over-fires costs a team some triage; a fingerprint that moves costs them the baseline, every
/// commit, permanently — and the failure is silent, because a baseline that matches nothing looks
/// exactly like a repository where everything is new.
/// </remarks>
public sealed class LifecycleTests {
    static readonly string Root = Path.GetFullPath("/tmp/repo");

    static Finding Finding(
        string ruleId = "SK1010",
        int line = 12,
        int start = 300,
        string symbol = "Vixen.Core.Foo.Bar(int, string)",
        string snippet = "source != null",
        string file = "Core/Foo.cs",
        SkalaSeverity severity = SkalaSeverity.Info
    ) =>
        new() {
            RuleId = ruleId,
            Severity = severity,
            Message = "Use `is not null` instead of `!= null`",
            Path = Path.Combine(Root, file.Replace('/', Path.DirectorySeparatorChar)),
            Line = line,
            Column = 9,
            EndLine = line,
            EndColumn = 24,
            Start = start,
            Length = 15,
            EnclosingSymbol = symbol,
            Snippet = snippet
        };

    static RunReport Report(params Finding[] findings) =>
        new() {
            RepositoryRoot = Root,
            Mode = LoadMode.Loose,
            Findings = Fingerprints.Assign([.. findings]),
            ConfigurationFingerprint = "abcdef0123456789",
            Duration = TimeSpan.FromSeconds(1)
        };

    // ---------------------------------------------------------------- the fingerprint

    /// <summary>
    /// ⚠ The property the whole baseline mechanism rests on.
    /// </summary>
    /// <remarks>
    /// doc 09: "No line numbers. A fingerprint that moves when a line moves is a baseline that
    /// expires every commit."
    /// </remarks>
    [Fact]
    public void FingerprintV2_SurvivesTheFindingMovingDownTheFile() =>
        Assert.Equal(
            Fingerprints.V2(Finding(line: 12, start: 300)),
            Fingerprints.V2(Finding(line: 4801, start: 191_204))
        );

    /// <summary>⚠ And a file being renamed, which is the other half of "stable across file moves".</summary>
    [Fact]
    public void FingerprintV2_SurvivesTheFileBeingRenamed() =>
        Assert.Equal(
            Fingerprints.V2(Finding(file: "Core/Foo.cs")),
            Fingerprints.V2(Finding(file: "Engine/Renamed/Foo.cs"))
        );

    [Fact]
    public void FingerprintV2_DiffersWhenTheEnclosingSymbolDoes() =>
        Assert.NotEqual(
            Fingerprints.V2(Finding(symbol: "Vixen.Core.Foo.Bar(int, string)")),
            Fingerprints.V2(Finding(symbol: "Vixen.Core.Foo.Baz(int, string)"))
        );

    [Fact]
    public void FingerprintV2_DiffersWhenTheSnippetDoes() =>
        Assert.NotEqual(Fingerprints.V2(Finding(snippet: "a != null")), Fingerprints.V2(Finding(snippet: "b != null")));

    /// <summary>
    /// ⚠ Two identical findings in one method are two findings.
    /// </summary>
    /// <remarks>
    /// Without the ordinal they share a fingerprint, and a baseline that accepts one accepts both —
    /// so fixing one of them silently keeps the other suppressed forever.
    /// </remarks>
    [Fact]
    public void Ordinal_SeparatesTwoIdenticalFindingsInOneSymbol() {
        var report = Report(Finding(line: 10, start: 100), Finding(line: 20, start: 200));

        Assert.Equal([0, 1], report.Findings.Select(static f => f.OrdinalWithinSymbol).Order());
        Assert.NotEqual(Fingerprints.V2(report.Findings[0]), Fingerprints.V2(report.Findings[1]));
    }

    /// <summary>
    /// ⚠ The ordinal is assigned by position, not by the order the analyzers happened to finish in.
    /// </summary>
    /// <remarks>
    /// Analyzers run concurrently (doc 07 § "Parallelism"). If the ordinal followed arrival order,
    /// the same tree would fingerprint differently between two runs and the baseline would expire
    /// at random.
    /// </remarks>
    [Fact]
    public void Ordinal_IsIndependentOfTheOrderFindingsArriveIn() {
        var forwards = Report(Finding(line: 10, start: 100), Finding(line: 20, start: 200));
        var backwards = Report(Finding(line: 20, start: 200), Finding(line: 10, start: 100));

        Assert.Equal(
            forwards.Findings.Select(Fingerprints.V2).Order(),
            backwards.Findings.Select(Fingerprints.V2).Order()
        );
    }

    /// <summary>⚠ Both versions are emitted, so a baseline written before M6 still reads.</summary>
    [Fact]
    public void BothFingerprintVersions_AreEmitted() {
        var prints = Fingerprints.For(Finding());
        Assert.Equal(Fingerprints.V1(Finding()), prints[Fingerprints.Version1]);
        Assert.Equal(Fingerprints.V2(Finding()), prints[Fingerprints.Version2]);
        Assert.NotEqual(prints[Fingerprints.Version1], prints[Fingerprints.Version2]);
    }

    // ---------------------------------------------------------------- the baseline

    [Fact]
    public void Baseline_RoundTripsThroughSarifAndMatchesNothingAsNew() {
        var report = Report(Finding(), Finding("SK1030", line: 40, start: 900, snippet: "x = x ?? y"));
        var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".sarif");

        try {
            Baseline.Write(path, report, report.Findings);
            var comparison = Baseline.Read(path).Compare(report.Findings);

            Assert.Equal(0, comparison.NewCount);
            Assert.Empty(comparison.Fixed);
            Assert.All(comparison.Findings, static f => Assert.Equal(BaselineBucket.Existing, f.Bucket));
        } finally {
            File.Delete(path);
        }
    }

    [Fact]
    public void Baseline_PartitionsIntoNewExistingAndFixed() {
        var accepted = Report(Finding(), Finding("SK1030", line: 40, start: 900, snippet: "x = x ?? y"));
        var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".sarif");

        try {
            Baseline.Write(path, accepted, accepted.Findings);

            // One of the two still fires; a third has appeared.
            var now = Report(Finding(), Finding("SK1034", line: 70, start: 1500, snippet: "items.Count() > 0"));
            var comparison = Baseline.Read(path).Compare(now.Findings);

            Assert.Equal(1, comparison.NewCount);
            Assert.Single(comparison.Fixed);
            Assert.Equal("SK1030", comparison.Fixed[0].RuleId);
        } finally {
            File.Delete(path);
        }
    }

    /// <summary>⚠ An absent baseline is empty; an unreadable one throws. See <see cref="Baseline.Read"/>.</summary>
    [Fact]
    public void Baseline_AbsentIsEmptyAndCorruptThrows() {
        Assert.Equal(0, Baseline.Read(Path.Combine(Path.GetTempPath(), "nothing-here.sarif")).Count);

        var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".sarif");
        try {
            File.WriteAllText(path, "{ this is not sarif");
            Assert.ThrowsAny<Exception>(() => Baseline.Read(path));
        } finally {
            File.Delete(path);
        }
    }

    // ---------------------------------------------------------------- the gate

    /// <summary>
    /// ⚠ <c>newIssues</c> with nothing to define "new" is a configuration error, not a pass.
    /// </summary>
    /// <remarks>
    /// Counting every finding in the repository as new would make <c>newIssues: 0</c> mean "the
    /// repository is perfect", which nobody who wrote it meant.
    /// </remarks>
    [Fact]
    public void Gate_NewIssuesWithoutABaselineOrSince_Fails() {
        var result = Gate.Evaluate(
            new GateDefinition { Name = "ci", MaxNewIssues = 0 },
            Report(Finding()),
            formattingClean: true
        );

        Assert.False(result.Passed);
        Assert.Contains(
            result.Failures,
            static f => f.Contains("needs a baseline or --since", StringComparison.Ordinal)
        );
    }

    /// <summary>
    /// ⚠ With a baseline in play, <c>maxSeverity</c> is about the new findings.
    /// </summary>
    /// <remarks>
    /// Read literally, doc 09's own `ci` gate — a baseline plus `maxSeverity: warning` — is
    /// unsatisfiable on any repository that has ever had a warning, which contradicts the
    /// adoption story § "New-code definition" is built on. Measured on Vixen's Core: 994 accepted,
    /// 0 new, and a literal reading still failing on 308 of the accepted ones.
    /// </remarks>
    [Fact]
    public void Gate_MaxSeverityIsScopedToNewFindingsWhenABaselineIsInPlay() {
        var warning = Finding(severity: SkalaSeverity.Warning);
        var definition = new GateDefinition { Name = "ci", MaxSeverity = SkalaSeverity.Warning };

        var unscoped = Report(warning);
        Assert.False(Gate.Evaluate(definition, unscoped, formattingClean: true).Passed);

        var accepted = unscoped with {
            HasBaseline = true,
            Findings = [.. unscoped.Findings.Select(static f => f with { Bucket = BaselineBucket.Existing })]
        };

        Assert.True(Gate.Evaluate(definition, accepted, formattingClean: true).Passed);
    }

    /// <summary>⚠ "New" is the intersection of the scopings, never the union.</summary>
    [Fact]
    public void New_IsTheIntersectionOfBaselineAndSince() {
        var outside = Finding(line: 10, start: 100) with { Bucket = BaselineBucket.New, IsInChangedCode = false };
        var inside = Finding(line: 20, start: 200) with { Bucket = BaselineBucket.New, IsInChangedCode = true };
        var accepted = Finding(line: 30, start: 300) with { Bucket = BaselineBucket.Existing, IsInChangedCode = true };

        var report = Report(outside, inside, accepted) with {
            HasBaseline = true, ChangedCodeReference = "origin/main", Findings = [outside, inside, accepted]
        };

        Assert.Single(report.New);
        Assert.Equal(20, report.New.Single().Line);
    }

    /// <summary>⚠ A metric a gate names and the run did not measure fails, rather than passing.</summary>
    [Fact]
    public void Gate_AMetricThatWasNotMeasured_Fails() {
        var definition = new GateDefinition {
            Name = "ci", Metrics = ImmutableDictionary<string, double>.Empty.Add("duplication", 3.0)
        };

        var result = Gate.Evaluate(definition, Report(Finding()), formattingClean: true);
        Assert.False(result.Passed);
        Assert.Contains(result.Failures, static f => f.Contains("was not measured", StringComparison.Ordinal));
    }

    [Fact]
    public void Gate_AnUnknownMetricName_Fails() {
        var definition = new GateDefinition {
            Name = "ci", Metrics = ImmutableDictionary<string, double>.Empty.Add("coverage", 80)
        };

        Assert.False(Gate.Evaluate(definition, Report(Finding()), formattingClean: true).Passed);
    }

    /// <summary>⚠ <c>commentDensity</c> is a floor; everything else is a ceiling.</summary>
    [Fact]
    public void Gate_CommentDensityIsAFloorAndDuplicationIsACeiling() {
        var metrics = new MetricsSummary {
            MemberCount = 100,
            TotalLines = 1000,
            DuplicatedLines = 20,
            Duplication = 2.0,
            CommentDensity = 40
        };

        var report = Report(Finding()) with { Metrics = metrics };

        Assert.True(
            Gate.Evaluate(
                new GateDefinition {
                    Name = "g", Metrics = ImmutableDictionary<string, double>.Empty.Add("duplication", 3.0)
                },
                report,
                formattingClean: true
            ).Passed
        );

        Assert.False(
            Gate.Evaluate(
                new GateDefinition {
                    Name = "g", Metrics = ImmutableDictionary<string, double>.Empty.Add("commentDensity", 60)
                },
                report,
                formattingClean: true
            ).Passed
        );
    }

    [Fact]
    public void Gate_RuleOverridesMatchAPrefixGlobAndAnExactId() {
        var report = Report(Finding("SK5001"), Finding("SK1010", line: 40, start: 900));

        Assert.False(
            Gate.Evaluate(
                new GateDefinition {
                    Name = "g", RuleOverrides = ImmutableDictionary<string, int>.Empty.Add("SK5*", 0)
                },
                report,
                formattingClean: true
            ).Passed
        );

        Assert.True(
            Gate.Evaluate(
                new GateDefinition {
                    Name = "g", RuleOverrides = ImmutableDictionary<string, int>.Empty.Add("SK9001", 0)
                },
                report,
                formattingClean: true
            ).Passed
        );
    }

    /// <summary>⚠ A condition this build does not understand fails the gate rather than being ignored.</summary>
    [Fact]
    public void Gate_AnUnsupportedCondition_FailsRatherThanBeingDropped() {
        var result = Gate.Evaluate(
            new GateDefinition { Name = "ci", Unsupported = ["coverage"] },
            Report(),
            formattingClean: true
        );

        Assert.False(result.Passed);
        Assert.Contains(result.Failures, static f => f.Contains("coverage", StringComparison.Ordinal));
    }

    /// <summary>
    /// ⚠ Doc 09's exit codes are a contract hooks, CI and agents depend on. 2 is distinct from 1.
    /// </summary>
    [Fact]
    public void ExitCodes_AreTheDocumentedValues() {
        Assert.Equal(0, ExitCodes.Ok);
        Assert.Equal(1, ExitCodes.GateFailed);
        Assert.Equal(2, ExitCodes.FormattingNeeded);
        Assert.Equal(3, ExitCodes.ConfigurationError);
        Assert.Equal(4, ExitCodes.LoadFailure);
        Assert.Equal(5, ExitCodes.InternalError);
        Assert.Equal(130, ExitCodes.Cancelled);
        Assert.NotEqual(ExitCodes.GateFailed, ExitCodes.FormattingNeeded);
    }

    // ---------------------------------------------------------------- suppressions

    /// <summary>⚠ "Did not audit" and "audited and found nothing" are different facts.</summary>
    [Fact]
    public void SuppressionAudit_OffDoesNotFailTheGate() {
        Assert.False(SuppressionAudit.Off.Enforced);
        Assert.True(Gate.Evaluate(GateDefinition.Local, Report(), formattingClean: true).Passed);
    }

    [Fact]
    public void Gate_ANewSuppression_Fails() {
        var report = Report() with {
            Suppressions = new SuppressionAudit {
                Enforced = true,
                Reference = "origin/main",
                Added = [new SuppressionEntry(SuppressionSource.EditorConfig, "SK3002", ".editorconfig [*.cs]", "none")]
            }
        };

        var result = Gate.Evaluate(GateDefinition.Local, report, formattingClean: true);
        Assert.False(result.Passed);

        // ⚠ The .editorconfig form specifically: a grep for `#pragma` is not a constraint.
        Assert.Contains(result.Failures, static f => f.Contains(".editorconfig", StringComparison.Ordinal));
    }

    // ---------------------------------------------------------------- history

    [Fact]
    public void History_RoundTripsAndSkipsATornLine() {
        var directory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(directory);

        try {
            History.Append(directory, History.Entry(Report(Finding()), "abc1234", "main"));
            File.AppendAllText(History.PathFor(directory), "{ not json\n");
            History.Append(directory, History.Entry(Report(Finding()), "def5678", "main"));

            var entries = History.Read(directory);
            Assert.Equal(2, entries.Length);
            Assert.Equal("abc1234", entries[0].Sha);
            Assert.Equal("def5678", entries[1].Sha);
            Assert.Contains("findings", History.Render(entries, 20), StringComparison.Ordinal);
        } finally {
            Directory.Delete(directory, recursive: true);
        }
    }

    // ---------------------------------------------------------------- percentiles

    /// <summary>⚠ Nearest-rank, so every reported number is some member's actual score.</summary>
    [Fact]
    public void Percentile_IsNearestRankAndNeverInterpolates() {
        int[] sorted = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10];
        Assert.Equal(10, MetricsSummary.Percentile(sorted, 0.95));
        Assert.Equal(5, MetricsSummary.Percentile(sorted, 0.5));
        Assert.Equal(0, MetricsSummary.Percentile([], 0.95));
    }
}
