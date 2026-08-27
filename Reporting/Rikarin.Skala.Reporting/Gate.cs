using System.Collections.Immutable;
using System.Globalization;
using System.Text.Json;
using Rikarin.Skala.Core.Diagnostics;

namespace Rikarin.Skala.Reporting;

/// <summary>One named gate from <c>skala.jsonc</c>.</summary>
/// <remarks>
///     docs/plan/09 § "Gates" defines six conditions and M6 implements all six. ⚠ A condition this
///     build does not understand is still <em>rejected</em> rather than ignored, for the reason M5
///     wrote down and that has not changed: a gate that silently drops the condition someone relies on
///     passes for the wrong reason, which is worse than one that says it cannot run.
/// </remarks>
public sealed record GateDefinition {
    public required string Name { get; init; }

    /// <summary>Any finding at or above this level fails the gate.</summary>
    public SkalaSeverity? MaxSeverity { get; init; }

    /// <summary><c>clean</c> ⇒ <c>skala format --check</c> must produce no edits.</summary>
    public bool RequireCleanFormatting { get; init; }

    /// <summary>
    ///     The maximum number of <em>new</em> findings, relative to the baseline and/or <c>--since</c>.
    /// </summary>
    public int? MaxNewIssues { get; init; }

    /// <summary>The git ref the gate scopes to, when the gate rather than the command line names one.</summary>
    public string? Since { get; init; }

    /// <summary>The baseline path the gate names, relative to the repository root.</summary>
    public string? BaselinePath { get; init; }

    /// <summary>⚠ Ceilings, except <c>commentDensity</c>. See <see cref="MetricsSummary.IsFloor" />.</summary>
    public ImmutableDictionary<string, double> Metrics { get; init; } =
        ImmutableDictionary<string, double>.Empty;

    /// <summary>
    ///     Per-rule tightening, e.g. <c>"SK5*": 0</c> — at most this many findings for rules matching
    ///     the pattern, regardless of the rest of the gate.
    /// </summary>
    public ImmutableDictionary<string, int> RuleOverrides { get; init; } =
        ImmutableDictionary<string, int>.Empty;

    /// <summary>⚠ Conditions this build cannot evaluate. Their presence fails the gate loudly.</summary>
    public ImmutableArray<string> Unsupported { get; init; } = [];

    /// <summary>The default when `skala.jsonc` names no gate: errors fail, nothing else does.</summary>
    public static GateDefinition Local { get; } = new() { Name = "local", MaxSeverity = SkalaSeverity.Error };

    /// <summary>Whether the gate needs a baseline loaded to be evaluable.</summary>
    public bool NeedsBaseline => BaselinePath is not null || MaxNewIssues is not null;
}

/// <summary>
///     The one place a finding turns into a verdict.
/// </summary>
/// <remarks>
///     ⚠ ADR-009's corollary: renderers read, the gate decides. Nothing downstream of
///     <see cref="Evaluate" /> may look at severities again and reach its own conclusion.
/// </remarks>
public static class Gate {
    /// <param name="formattingClean">
    ///     ⚠ Three states, not two. <c>true</c> and <c>false</c> are the answers to "would
    ///     <c>format --check</c> edit anything"; <c>null</c> means the run never asked — <c>--no-formatting</c>
    ///     — and a gate that names <c>formatting</c> must fail rather than pass on an unasked question.
    ///     Before M9 this was a <c>bool</c> defaulting to <c>true</c>, so <c>--no-formatting</c> turned a
    ///     red gate green without saying it had dropped the condition.
    /// </param>
    public static GateResult Evaluate(GateDefinition definition, RunReport report, bool? formattingClean) {
        var failures = ImmutableArray.CreateBuilder<string>();

        foreach (var condition in definition.Unsupported) {
            failures.Add(
                $"gate condition '{condition}' is not implemented in this build; "
                + "the gate fails rather than passing without it"
            );
        }

        if (definition.MaxSeverity is { } max) {
            // ⚠ <b>Scoped, when the gate is scoped.</b> docs/plan/09's table says "any finding at or
            // above this level fails", and read literally that makes the document's own `ci` gate —
            // `baseline` plus `maxSeverity: warning` — unsatisfiable on every repository that has
            // ever had a warning. Measured on Vixen's Core: 994 findings accepted into a baseline,
            // 0 new, and a literal `maxSeverity` still failing on 308 of the accepted ones. A
            // baseline whose entries keep failing the gate has not accepted anything, and the
            // "adoptable on a tree with existing findings" claim in § "New-code definition" is the
            // whole point of the section.
            //
            // So: with no baseline and no `--since`, every reportable finding counts, which is
            // exactly M5's behaviour and what the `local` gate still gets. With either scoping in
            // play, the condition applies to the findings that scoping calls new. The severity bar
            // and the new-code bar then compose rather than contradicting each other.
            var considered = report.HasBaseline || report.ChangedCodeReference is not null
                ? report.New
                : report.Reportable;

            var offending = considered.Count(finding => finding.Severity >= max);
            if (offending > 0) {
                failures.Add(
                    offending.ToString(CultureInfo.InvariantCulture)
                    + (report.HasBaseline || report.ChangedCodeReference is not null ? " new" : string.Empty)
                    + " finding(s) at or above "
                    + Renderer.Word(max)
                );
            }
        }

        if (definition.RequireCleanFormatting) {
            if (formattingClean is null) {
                failures.Add(
                    "the gate requires `formatting: clean` but this run was given `--no-formatting`, "
                    + "so the condition could not be evaluated; the gate fails rather than "
                    + "passing without it"
                );
            } else if (formattingClean is false) {
                failures.Add("formatting is not clean; run `skala format`");
            }
        }

        EvaluateNewIssues(definition, report, failures);
        EvaluateMetrics(definition, report, failures);
        EvaluateRuleOverrides(definition, report, failures);
        EvaluateSuppressions(report, failures);

        return new GateResult(definition.Name, failures.Count == 0, failures.ToImmutable());
    }

    /// <summary>
    ///     <c>newIssues</c> — the condition that makes adoption possible.
    /// </summary>
    /// <remarks>
    ///     ⚠ "New" is the intersection of the two scopings that are in play, not the union. With a
    ///     baseline, new means "not in the baseline"; with <c>--since</c>, new means "on a line this
    ///     branch touched"; with both, it means both, because a gate that fired on either would fail a
    ///     PR for a pre-existing finding that happens to sit near an edit.
    ///     <para>
    ///         ⚠ A gate naming <c>newIssues</c> with neither scoping in play is a configuration error and
    ///         is reported as one. Counting every finding in the repository as new would make
    ///         <c>newIssues: 0</c> mean "the repository is perfect", which is not what anybody who wrote it
    ///         meant.
    ///     </para>
    /// </remarks>
    static void EvaluateNewIssues(
        GateDefinition definition,
        RunReport report,
        ImmutableArray<string>.Builder failures
    ) {
        if (definition.MaxNewIssues is not { } maximum) {
            return;
        }

        if (!report.HasBaseline && report.ChangedCodeReference is null) {
            failures.Add(
                "gate condition 'newIssues' needs a baseline or --since to say what 'new' means; "
                + "the gate has neither, and counting every finding as new is not what the condition means"
            );

            return;
        }

        var count = report.New.Count();
        if (count > maximum) {
            failures.Add(
                count.ToString(CultureInfo.InvariantCulture)
                + " new finding(s) against a limit of "
                + maximum.ToString(CultureInfo.InvariantCulture)
                + " ("
                + Scope(report)
                + ")"
            );
        }
    }

    static string Scope(RunReport report) {
        var parts = new List<string>();
        if (report.HasBaseline) {
            parts.Add("baseline " + report.BaselineSummary);
        }

        if (report.ChangedCodeReference is { } reference) {
            parts.Add("since " + reference);
        }

        return string.Join(", ", parts);
    }

    static void EvaluateMetrics(
        GateDefinition definition,
        RunReport report,
        ImmutableArray<string>.Builder failures
    ) {
        foreach (var (name, threshold) in definition.Metrics.OrderBy(static pair => pair.Key, StringComparer.Ordinal)) {
            if (!MetricsSummary.Names.Contains(name)) {
                failures.Add(
                    "gate condition 'metrics."
                    + name
                    + "' names no metric this build computes (known: "
                    + string.Join(", ", MetricsSummary.Names)
                    + ")"
                );

                continue;
            }

            if (report.Metrics.Read(name) is not { } value) {
                // ⚠ Not silently passed. A gate asking about duplication in a run where duplication
                // was not measured has not been satisfied; it has been skipped, and the two must not
                // look the same from outside.
                failures.Add(
                    "gate condition 'metrics." + name + "' was not measured in this run, so it cannot be evaluated"
                );

                continue;
            }

            var failed = MetricsSummary.IsFloor(name) ? value < threshold : value > threshold;
            if (failed) {
                failures.Add(
                    "metrics."
                    + name
                    + " is "
                    + value.ToString("0.##", CultureInfo.InvariantCulture)
                    + (MetricsSummary.IsFloor(name) ? ", below the floor of " : ", over the limit of ")
                    + threshold.ToString("0.##", CultureInfo.InvariantCulture)
                );
            }
        }
    }

    /// <summary>
    ///     <c>ruleOverrides</c> — per-rule tightening, e.g. <c>"SK5*": 0</c>.
    /// </summary>
    /// <remarks>
    ///     ⚠ The pattern is a prefix glob rather than a regular expression, because the only shape doc
    ///     09 asks for is a range (<c>SK5*</c>) or an id, and a regular expression in a configuration
    ///     file is a thing people get wrong silently.
    /// </remarks>
    static void EvaluateRuleOverrides(
        GateDefinition definition,
        RunReport report,
        ImmutableArray<string>.Builder failures
    ) {
        foreach (var (pattern, maximum) in
                 definition.RuleOverrides.OrderBy(static pair => pair.Key, StringComparer.Ordinal)) {
            var count = report.Reportable.Count(finding => Matches(pattern, finding.RuleId));
            if (count > maximum) {
                failures.Add(
                    count.ToString(CultureInfo.InvariantCulture)
                    + " finding(s) matching '"
                    + pattern
                    + "' against a limit of "
                    + maximum.ToString(CultureInfo.InvariantCulture)
                );
            }
        }
    }

    internal static bool Matches(string pattern, string ruleId) =>
        pattern.EndsWith('*')
            ? ruleId.StartsWith(pattern[..^1], StringComparison.OrdinalIgnoreCase)
            : string.Equals(pattern, ruleId, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    ///     <c>--no-new-suppressions</c>, evaluated wherever the suppression audit ran.
    /// </summary>
    /// <remarks>
    ///     ⚠ The audit is computed in <c>Rikarin.Skala.Analysis</c> because it needs the source and the
    ///     configuration; the gate only reads its verdict, so that "renderers read, the gate decides"
    ///     keeps holding and there is exactly one place a suppression turns into a failure.
    /// </remarks>
    static void EvaluateSuppressions(RunReport report, ImmutableArray<string>.Builder failures) {
        if (report.Suppressions is not { Enforced: true } audit || audit.Added.IsEmpty) {
            return;
        }

        failures.Add(
            audit.Added.Length.ToString(CultureInfo.InvariantCulture)
            + " new suppression(s) since "
            + audit.Reference
            + ": "
            + string.Join("; ", audit.Added.Take(5).Select(static entry => entry.Describe()))
            + (audit.Added.Length > 5 ? "; …" : string.Empty)
        );
    }

    /// <summary>Reads the `gates` block of <c>skala.jsonc</c>, or falls back to <c>local</c>.</summary>
    public static GateDefinition Read(string? toolConfigPath, string name) {
        if (toolConfigPath is null || !File.Exists(toolConfigPath)) {
            return Fallback(name);
        }

        try {
            using var document = JsonDocument.Parse(
                File.ReadAllText(toolConfigPath),
                new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true }
            );

            if (!document.RootElement.TryGetProperty("gates", out var gates)
                || !gates.TryGetProperty(name, out var gate)) {
                return Fallback(name);
            }

            return Parse(name, gate);
        } catch (JsonException) {
            // ToolConfiguration already reports SK9007 for an unreadable skala.jsonc; the gate does
            // not need to report it a second time, and refusing to run is not its call.
            return GateDefinition.Local with { Name = name };
        }
    }

    static GateDefinition Fallback(string name) =>
        name == "local" ? GateDefinition.Local : GateDefinition.Local with { Name = name };

    static GateDefinition Parse(string name, JsonElement gate) {
        var unsupported = ImmutableArray.CreateBuilder<string>();
        var metrics = ImmutableDictionary.CreateBuilder<string, double>(StringComparer.Ordinal);
        var overrides = ImmutableDictionary.CreateBuilder<string, int>(StringComparer.Ordinal);

        foreach (var property in gate.EnumerateObject()) {
            // ⚠ An explicit null is "this condition does not apply", which is how doc 09's own `pr`
            // gate spells `"coverage": null`. It is not an unsupported condition.
            if (property.Value.ValueKind == JsonValueKind.Null) {
                continue;
            }

            switch (property.Name) {
                case "maxSeverity":
                case "formatting":
                case "newIssues":
                case "since":
                case "baseline":
                    continue;

                case "metrics":
                    foreach (var metric in property.Value.EnumerateObject()) {
                        if (metric.Value.ValueKind is JsonValueKind.Number) {
                            metrics[metric.Name] = metric.Value.GetDouble();
                        }
                    }

                    continue;

                case "ruleOverrides":
                    foreach (var rule in property.Value.EnumerateObject()) {
                        if (rule.Value.ValueKind is JsonValueKind.Number) {
                            overrides[rule.Name] = rule.Value.GetInt32();
                        }
                    }

                    continue;

                default:
                    unsupported.Add(property.Name);
                    continue;
            }
        }

        return new GateDefinition {
            Name = name,
            MaxSeverity = gate.TryGetProperty("maxSeverity", out var severity)
                ? ParseSeverity(severity.GetString())
                : null,
            RequireCleanFormatting = gate.TryGetProperty("formatting", out var formatting)
                && string.Equals(formatting.GetString(), "clean", StringComparison.Ordinal),
            MaxNewIssues = gate.TryGetProperty("newIssues", out var issues)
                && issues.ValueKind == JsonValueKind.Number
                    ? issues.GetInt32()
                    : null,
            Since = gate.TryGetProperty("since", out var since) && since.ValueKind == JsonValueKind.String
                ? since.GetString()
                : null,
            BaselinePath = gate.TryGetProperty("baseline", out var baseline)
                && baseline.ValueKind == JsonValueKind.String
                    ? baseline.GetString()
                    : null,
            Metrics = metrics.ToImmutable(),
            RuleOverrides = overrides.ToImmutable(),
            Unsupported = unsupported.ToImmutable()
        };
    }

    static SkalaSeverity? ParseSeverity(string? value) =>
        value switch {
            "error" => SkalaSeverity.Error,
            "warning" => SkalaSeverity.Warning,
            "suggestion" => SkalaSeverity.Info,
            "hint" => SkalaSeverity.Hidden,
            _ => null
        };
}
