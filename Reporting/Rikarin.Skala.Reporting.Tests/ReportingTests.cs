using System.Collections.Immutable;
using System.Text.Json;
using Rikarin.Skala.Core.Diagnostics;
using Rikarin.Skala.Reporting;
using Rikarin.Skala.Rules.Metadata;

namespace Rikarin.Skala.Reporting.Tests;

/// <summary>
/// ADR-009 as tests: one serialisation, and every other surface a renderer over it.
/// </summary>
public sealed class ReportingTests {
    static RunReport Sample(params Finding[] findings) =>
        new() {
            RepositoryRoot = Path.GetFullPath("/tmp/repo"),
            Mode = LoadMode.Loose,
            Findings = [.. findings],
            LoadSummary = "loose (3 file(s), no project)",
            FileCount = 3,
            LineCount = 120,
            ConfigurationFingerprint = "abcdef0123456789",
            Duration = TimeSpan.FromMilliseconds(420)
        };

    static Finding Modernization(string ruleId = "SK1010", int line = 12, bool fix = true) =>
        new() {
            RuleId = ruleId,
            Severity = SkalaSeverity.Info,
            Message = "Use `is not null` instead of `!= null`",
            Path = Path.Combine(Path.GetFullPath("/tmp/repo"), "Core", "Foo.cs"),
            Line = line,
            Column = 9,
            EndLine = line,
            EndColumn = 24,
            Start = 300,
            Length = 15,
            Fix = fix
                ? [new FixEdit(Path.Combine(Path.GetFullPath("/tmp/repo"), "Core", "Foo.cs"), 300, 15, "x is not null")]
                : [],
            FixIsSafe = fix
        };

    [Fact]
    public void Sarif_IsValidJsonAndCarriesTheThingsAReportIsComparedBy() {
        var report = Sample(Modernization()) with { Gate = GateResult.Pass("local") };
        using var document = JsonDocument.Parse(SarifWriter.Serialize(SarifWriter.Build(report)));

        var run = document.RootElement.GetProperty("runs")[0];
        Assert.Equal("2.1.0", document.RootElement.GetProperty("version").GetString());

        var driver = run.GetProperty("tool").GetProperty("driver");
        Assert.Equal("Skala", driver.GetProperty("name").GetString());

        var properties = driver.GetProperty("properties");
        Assert.Equal("loose", properties.GetProperty("loadMode").GetString());
        Assert.Equal("abcdef0123456789", properties.GetProperty("configurationFingerprint").GetString());
        Assert.False(properties.GetProperty("optionOverridesActive").GetBoolean());

        // ⚠ Every rule that *could* fire, not every rule that did (doc 09).
        Assert.Equal(RuleCatalog.All.Count, driver.GetProperty("rules").GetArrayLength());

        var result = run.GetProperty("results")[0];
        Assert.Equal("SK1010", result.GetProperty("ruleId").GetString());
        Assert.Equal("note", result.GetProperty("level").GetString());
        Assert.Equal(
            "Core/Foo.cs",
            result.GetProperty("locations")[0]
                .GetProperty("physicalLocation")
                .GetProperty("artifactLocation")
                .GetProperty("uri")
                .GetString()
        );

        var replacement = result.GetProperty("fixes")[0]
            .GetProperty("artifactChanges")[0]
            .GetProperty("replacements")[0];

        Assert.Equal(300, replacement.GetProperty("deletedRegion").GetProperty("charOffset").GetInt32());
        Assert.Equal("x is not null", replacement.GetProperty("insertedContent").GetProperty("text").GetString());
    }

    /// <summary>
    /// ⚠ doc 09: "No line numbers. A fingerprint that moves when a line moves is a baseline that
    /// expires every commit."
    /// </summary>
    [Fact]
    public void Fingerprint_SurvivesTheFindingMovingDownTheFile() {
        var before = SarifWriter.Fingerprint(Modernization(line: 12));
        var after = SarifWriter.Fingerprint(Modernization(line: 480));
        Assert.Equal(before, after);
    }

    [Fact]
    public void Fingerprint_DiffersWhenTheRuleOrTheMessageDoes() {
        var baseline = SarifWriter.Fingerprint(Modernization());
        Assert.NotEqual(baseline, SarifWriter.Fingerprint(Modernization("SK1030")));
        Assert.NotEqual(baseline, SarifWriter.Fingerprint(Modernization() with { Message = "something else" }));
    }

    [Fact]
    public void PlainRenderer_IsTheFormatEveryEditorsErrorParserUnderstands() {
        var text = Renderer.Render(Sample(Modernization()), ReportFormat.Plain);
        Assert.Equal(
            "Core/Foo.cs:12:9: suggestion SK1010: Use `is not null` instead of `!= null`\n",
            text.ReplaceLineEndings("\n")
        );
    }

    /// <summary>
    /// ⚠ docs/plan/10: three buckets, always in this order. Formatting is free and unconditional,
    /// fixable is mechanical, decisions need the model to think — so an agent reading top-down
    /// arrives at the hard part with a clean tree.
    /// </summary>
    [Fact]
    public void AgentRenderer_OrdersTheBucketsFormattingThenFixableThenDecisions() {
        var report = Sample(
            Modernization(),
            Modernization("SK2001", fix: false) with {
                Severity = SkalaSeverity.Warning, Message = "Comparison is always true"
            },
            new Finding {
                RuleId = RuleIds.FileIsNotFormatted,
                Severity = SkalaSeverity.Info,
                Message = "the file is not formatted (4 edit(s)); run `skala format`",
                Path = Path.Combine(Path.GetFullPath("/tmp/repo"), "Core", "Bar.cs"),
                Line = 1,
                Column = 1,
                Fix = [new FixEdit(Path.Combine(Path.GetFullPath("/tmp/repo"), "Core", "Bar.cs"), 0, 1, " ")],
                FixIsSafe = true
            }
        );

        var text = Renderer.Render(report, ReportFormat.Agent);
        var format = text.IndexOf("FORMAT", StringComparison.Ordinal);
        var fixable = text.IndexOf("FIXABLE", StringComparison.Ordinal);
        var action = text.IndexOf("ACTION", StringComparison.Ordinal);

        Assert.True(
            format >= 0 && fixable > format && action > fixable,
            "the three buckets are not in order:\n" + text
        );

        // ⚠ The command to run is printed complete, with paths — not "run skala format".
        Assert.Contains("run: skala format Core/Bar.cs", text, StringComparison.Ordinal);
        Assert.Contains("run: skala fix --safe", text, StringComparison.Ordinal);
    }

    [Fact]
    public void AgentRenderer_SaysNothingToDoWhenThereIsNothingToDo() =>
        Assert.Equal("OK  nothing to do.\n", Renderer.Render(Sample(), ReportFormat.Agent));

    [Fact]
    public void AgentRenderer_IsBounded() {
        var many = Enumerable.Range(0, 400).Select(i => Modernization(line: i)).ToArray();
        var text = Renderer.Render(Sample(many), ReportFormat.Agent);

        Assert.True(
            text.Length <= AgentRenderer.MaxCharacters + 200,
            $"the agent report was {text.Length} characters; the cap exists because an unbounded lint dump eats the context window the agent needs to fix things with."
        );
    }

    /// <summary>
    /// ⚠ Determinism is enforced after the fact, not during (docs/plan/07). Parallelism may never be
    /// observable in output.
    /// </summary>
    [Fact]
    public void Renderers_SortIndependentlyOfTheOrderFindingsArrivedIn() {
        var a = Modernization(line: 30);
        var b = Modernization(line: 10);
        var c = Modernization(line: 20);

        Assert.Equal(
            Renderer.Render(Sample(a, b, c), ReportFormat.Plain),
            Renderer.Render(Sample(c, a, b), ReportFormat.Plain)
        );
    }

    [Fact]
    public void Gate_FailsOnAnyFindingAtOrAboveMaxSeverity() {
        var report = Sample(Modernization("SK5001") with { Severity = SkalaSeverity.Error });
        var result = Gate.Evaluate(GateDefinition.Local, report, formattingClean: true);

        Assert.False(result.Passed);
        Assert.Contains(result.Failures, failure => failure.Contains("error", StringComparison.Ordinal));
    }

    [Fact]
    public void Gate_PassesWhenEverythingIsBelowTheBar() =>
        Assert.True(Gate.Evaluate(GateDefinition.Local, Sample(Modernization()), formattingClean: true).Passed);

    /// <summary>
    /// ⚠ A condition this build cannot evaluate fails the gate rather than being dropped. A gate
    /// that silently loses the condition someone relies on passes for the wrong reason.
    /// </summary>
    [Fact]
    public void Gate_FailsRatherThanIgnoringAConditionItCannotEvaluate() {
        var definition = new GateDefinition {
            Name = "ci", MaxSeverity = SkalaSeverity.Error, Unsupported = ["newIssues", "baseline"]
        };

        var result = Gate.Evaluate(definition, Sample(), formattingClean: true);
        Assert.False(result.Passed);
        Assert.Equal(2, result.Failures.Length);
    }

    [Fact]
    public void Gate_RequiringCleanFormatting_FailsWhenItIsNot() =>
        Assert.False(
            Gate.Evaluate(
                GateDefinition.Local with { RequireCleanFormatting = true },
                Sample(),
                formattingClean: false
            ).Passed
        );

    [Fact]
    public void SkippedRules_AreInTheSarifSoTwoCleanRunsAreComparable() {
        var report = Sample() with { SkippedRules = [new SkippedRule("SK1010", "requires a semantic model")] };

        using var document = JsonDocument.Parse(SarifWriter.Serialize(SarifWriter.Build(report)));
        var rules = document.RootElement.GetProperty("runs")[0]
            .GetProperty("tool")
            .GetProperty("driver")
            .GetProperty("rules");

        var skipped = rules.EnumerateArray().Single(rule => rule.GetProperty("id").GetString() == "SK1010");
        Assert.Equal("requires a semantic model", skipped.GetProperty("properties").GetProperty("skipped").GetString());
    }

    [Fact]
    public void ExitCodes_AreTheOnesHooksAndCiDependOn() {
        Assert.Equal(0, ExitCodes.Ok);
        Assert.Equal(1, ExitCodes.GateFailed);

        // ⚠ 2 is distinct from 1 on purpose: a hook that auto-formats on 2 and stops on 1 is a
        // two-line hook.
        Assert.Equal(2, ExitCodes.FormattingNeeded);
        Assert.Equal(3, ExitCodes.ConfigurationError);
        Assert.Equal(4, ExitCodes.LoadFailure);
        Assert.Equal(5, ExitCodes.InternalError);
        Assert.Equal(130, ExitCodes.Cancelled);
    }
}
