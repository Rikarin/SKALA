using Rikarin.Skala.Core.Diagnostics;
using Rikarin.Skala.Rules.Metadata;
using System.Collections.Immutable;
using System.Text.Json;

namespace Rikarin.Skala.Reporting.Tests;

/// <summary>
///     ADR-009 as tests: one serialisation, and every other surface a renderer over it.
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
    ///     ⚠ doc 09: "No line numbers. A fingerprint that moves when a line moves is a baseline that
    ///     expires every commit."
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

    /// <summary>
    ///     ⚠ The <c>ReplaceLineEndings("\n")</c> this assertion used to make first is gone, and its
    ///     absence is the assertion. Normalising before comparing meant the one thing this test could
    ///     not detect was the renderer emitting the wrong line ending, which it did on Windows for as
    ///     long as the test existed. See <see cref="NoRenderer_EmitsACarriageReturn" />.
    /// </summary>
    [Fact]
    public void PlainRenderer_IsTheFormatEveryEditorsErrorParserUnderstands() {
        var text = Renderer.Render(Sample(Modernization()), ReportFormat.Plain);
        Assert.Equal("Core/Foo.cs:12:9: suggestion SK1010: Use `is not null` instead of `!= null`\n", text);
    }

    /// <summary>
    ///     ⚠ docs/plan/10: three buckets, always in this order. Formatting is free and unconditional,
    ///     fixable is mechanical, decisions need the model to think — so an agent reading top-down
    ///     arrives at the hard part with a clean tree.
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

    /// <summary>
    ///     ⚠ Every surface, on every platform, ends its lines with <c>\n</c>.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         <c>Renderers.cs</c> built all seven with <c>StringBuilder.AppendLine</c>, which appends
    ///         <see cref="Environment.NewLine" />. On Windows that is CRLF, so the tool's output changed
    ///         shape with the platform it ran on — and <c>plain</c> is doc 09's greppable, editor-parsed
    ///         format while <c>agent</c> is doc 10's machine report, so this is an output contract and not
    ///         a preference.
    ///     </para>
    ///     <para>
    ///         ⚠ The reason it survived is the shape of the test that caught it:
    ///         <see cref="AgentRenderer_SaysNothingToDoWhenThereIsNothingToDo" /> is the only assertion
    ///         that compared a whole rendered string to a literal, so it failed on Windows and nowhere
    ///         else — a platform leg had to exist and be read before anybody knew. This one asks the
    ///         question directly, of every format, and fails on the platform that has the defect *and* on
    ///         the ones that do not, the moment a renderer reaches for <c>AppendLine</c> again.
    ///     </para>
    ///     <para>
    ///         ⚠ The cases come from <see cref="Enum.GetValues{TEnum}" />, and hand-written
    ///         <c>InlineData</c> is what they replace. The list used to name six of the seven formats;
    ///         the missing one was <see cref="ReportFormat.Json" />, and it had the bug — SARIF is
    ///         serialised through a <see cref="System.IO.TextWriter" /> rather than a
    ///         <c>StringBuilder</c>, so the sweep that fixed the <c>AppendLine</c> renderers never
    ///         reached it and the theory that should have caught it never asked. A list of formats
    ///         maintained by hand beside an enum drifts from the enum; enumerating it cannot.
    ///     </para>
    /// </remarks>
    [Theory]
    [MemberData(nameof(EveryFormat))]
    public void NoRenderer_EmitsACarriageReturn(ReportFormat format) {
        var report = Sample(
            Modernization(),
            Modernization("SK2001", fix: false) with {
                Severity = SkalaSeverity.Warning, Message = "Comparison is always true"
            }
        );

        var text = Renderer.Render(report, format, true);

        // Anti-vacuity: a renderer that returned nothing would pass the assertion below trivially.
        Assert.NotEmpty(text);
        Assert.DoesNotContain('\r', text);
    }

    public static TheoryData<ReportFormat> EveryFormat() {
        var data = new TheoryData<ReportFormat>();
        foreach (var format in Enum.GetValues<ReportFormat>()) {
            data.Add(format);
        }

        return data;
    }

    /// <summary>
    ///     ⚠ The Actions log has to say why the gate failed, because the step summary is a different
    ///     page and the exit code is a number.
    /// </summary>
    /// <remarks>
    ///     The `github` renderer emitted findings and stopped. A failing `Check` step's log was
    ///     therefore an unbroken run of annotations and then `Process completed with exit code 1`,
    ///     with the verdict, its reasons, and the run's own diagnostics about itself all written
    ///     somewhere the log could not reach. Read from the log alone, this repository's master gate
    ///     looked like one rule family misbehaving; it was failing four conditions, and the one that
    ///     mattered most was <c>SK9030</c> — the baseline the gate names does not exist, so every
    ///     finding counts as new. The tool had diagnosed itself and filed the answer out of sight.
    /// </remarks>
    [Fact]
    public void GithubRenderer_PutsTheVerdictAndTheRunsOwnDiagnosticsInTheLog() {
        var report = Sample(Modernization()) with {
            Gate = new GateResult("ci", false, ["3 new finding(s) at or above warning"]),
            Diagnostics = [
                new SkalaDiagnostic(
                    "SK9030",
                    SkalaSeverity.Warning,
                    "the gate names a baseline at .skala/baseline.sarif and there is no such file"
                )
            ]
        };

        var text = Renderer.Render(report, ReportFormat.Github, true);

        Assert.Contains("::notice::SK9030: the gate names a baseline", text, StringComparison.Ordinal);
        Assert.Contains("::error::gate `ci`: FAIL", text, StringComparison.Ordinal);
        Assert.Contains("::error::  3 new finding(s) at or above warning", text, StringComparison.Ordinal);

        // ⚠ Not a second gate. A passing gate says so and raises nothing (doc 09: the gate decides,
        // once), so a renderer that had started deciding for itself fails here.
        var passed = Renderer.Render(
            report with { Gate = new GateResult("ci", true, []) },
            ReportFormat.Github,
            true
        );

        Assert.Contains("::notice::gate `ci`: PASS", passed, StringComparison.Ordinal);
        Assert.DoesNotContain("::error::gate", passed, StringComparison.Ordinal);
    }

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
    ///     ⚠ Determinism is enforced after the fact, not during (docs/plan/07). Parallelism may never be
    ///     observable in output.
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
        var result = Gate.Evaluate(GateDefinition.Local, report, true);

        Assert.False(result.Passed);
        Assert.Contains(result.Failures, failure => failure.Contains("error", StringComparison.Ordinal));
    }

    [Fact]
    public void Gate_PassesWhenEverythingIsBelowTheBar() =>
        Assert.True(Gate.Evaluate(GateDefinition.Local, Sample(Modernization()), true).Passed);

    /// <summary>
    ///     ⚠ A condition this build cannot evaluate fails the gate rather than being dropped. A gate
    ///     that silently loses the condition someone relies on passes for the wrong reason.
    /// </summary>
    [Fact]
    public void Gate_FailsRatherThanIgnoringAConditionItCannotEvaluate() {
        var definition = new GateDefinition {
            Name = "ci", MaxSeverity = SkalaSeverity.Error, Unsupported = ["newIssues", "baseline"]
        };

        var result = Gate.Evaluate(definition, Sample(), true);
        Assert.False(result.Passed);
        Assert.Equal(2, result.Failures.Length);
    }

    [Fact]
    public void Gate_RequiringCleanFormatting_FailsWhenItIsNot() =>
        Assert.False(
            Gate.Evaluate(
                GateDefinition.Local with { RequireCleanFormatting = true },
                Sample(),
                false
            ).Passed
        );

    /// <summary>
    ///     ⚠ <b>`--no-formatting` used to satisfy a gate that names `formatting`.</b> The bit defaulted
    ///     to <c>true</c> when the run never collected formatting, so the flag that suppressed the
    ///     measurement also suppressed the check — the same "passing for the wrong reason" that
    ///     <see cref="Gate_FailsRatherThanIgnoringAConditionItCannotEvaluate" /> already forbids for an
    ///     unrecognized condition. <c>null</c> is "nobody asked", and an unasked question fails.
    /// </summary>
    [Fact]
    public void Gate_RequiringCleanFormatting_FailsWhenTheRunNeverLooked() {
        var result = Gate.Evaluate(
            GateDefinition.Local with { RequireCleanFormatting = true },
            Sample(),
            null
        );

        Assert.False(result.Passed);
        Assert.Contains(result.Failures, failure => failure.Contains("no-formatting", StringComparison.Ordinal));
    }

    /// <summary>A gate that does not name `formatting` does not care that nobody looked.</summary>
    [Fact]
    public void Gate_NotRequiringCleanFormatting_IsUnaffectedByNoFormatting() =>
        Assert.True(Gate.Evaluate(GateDefinition.Local, Sample(Modernization()), null).Passed);

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
