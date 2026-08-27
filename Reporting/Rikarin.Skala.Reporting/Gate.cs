using System.Collections.Immutable;
using System.Globalization;
using System.Text.Json;
using Rikarin.Skala.Core.Diagnostics;

namespace Rikarin.Skala.Reporting;

/// <summary>One named gate from <c>skala.jsonc</c>.</summary>
/// <remarks>
/// docs/plan/09 § "Gates" defines six conditions. M5 evaluates the two that need no baseline
/// infrastructure. ⚠ The other four are <em>rejected</em> rather than ignored: a gate that silently
/// drops the condition someone relies on is a gate that passes for the wrong reason, which is worse
/// than one that says it cannot run.
/// </remarks>
public sealed record GateDefinition {
    public required string Name { get; init; }

    /// <summary>Any finding at or above this level fails the gate.</summary>
    public SkalaSeverity? MaxSeverity { get; init; }

    /// <summary><c>clean</c> ⇒ <c>skala format --check</c> must produce no edits.</summary>
    public bool RequireCleanFormatting { get; init; }

    /// <summary>Conditions this build cannot evaluate. ⚠ Their presence fails the gate loudly.</summary>
    public ImmutableArray<string> Unsupported { get; init; } = [];

    /// <summary>The default when `skala.jsonc` names no gate: errors fail, nothing else does.</summary>
    public static GateDefinition Local { get; } = new() { Name = "local", MaxSeverity = SkalaSeverity.Error };
}

/// <summary>
/// The one place a finding turns into a verdict.
/// </summary>
/// <remarks>
/// ⚠ ADR-009's corollary: renderers read, the gate decides. Nothing downstream of
/// <see cref="Evaluate"/> may look at severities again and reach its own conclusion.
/// </remarks>
public static class Gate {
    public static GateResult Evaluate(GateDefinition definition, RunReport report, bool formattingClean) {
        var failures = ImmutableArray.CreateBuilder<string>();

        foreach (var condition in definition.Unsupported) {
            failures.Add(
                $"gate condition '{condition}' is not implemented in this build (docs/plan/15 § M6); "
                + "the gate fails rather than passing without it"
            );
        }

        if (definition.MaxSeverity is { } max) {
            var offending = report.Reportable.Count(finding => finding.Severity >= max);
            if (offending > 0) {
                failures.Add(
                    offending.ToString(CultureInfo.InvariantCulture)
                    + " finding(s) at or above "
                    + Renderer.Word(max)
                );
            }
        }

        if (definition.RequireCleanFormatting && !formattingClean) {
            failures.Add("formatting is not clean; run `skala format`");
        }

        return new GateResult(definition.Name, failures.Count == 0, failures.ToImmutable());
    }

    /// <summary>Reads the `gates` block of <c>skala.jsonc</c>, or falls back to <c>local</c>.</summary>
    public static GateDefinition Read(string? toolConfigPath, string name) {
        if (toolConfigPath is null || !File.Exists(toolConfigPath)) {
            return name == "local" ? GateDefinition.Local : GateDefinition.Local with { Name = name };
        }

        try {
            using var document = JsonDocument.Parse(
                File.ReadAllText(toolConfigPath),
                new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true }
            );

            if (!document.RootElement.TryGetProperty("gates", out var gates)
                || !gates.TryGetProperty(name, out var gate)) {
                return name == "local" ? GateDefinition.Local : GateDefinition.Local with { Name = name };
            }

            var unsupported = ImmutableArray.CreateBuilder<string>();
            foreach (var property in gate.EnumerateObject()) {
                if (property.Name is "maxSeverity" or "formatting") {
                    continue;
                }

                if (property.Value.ValueKind == JsonValueKind.Null) {
                    continue;
                }

                unsupported.Add(property.Name);
            }

            return new GateDefinition {
                Name = name,
                MaxSeverity = gate.TryGetProperty("maxSeverity", out var severity)
                    ? ParseSeverity(severity.GetString())
                    : null,
                RequireCleanFormatting = gate.TryGetProperty("formatting", out var formatting)
                    && string.Equals(formatting.GetString(), "clean", StringComparison.Ordinal),
                Unsupported = unsupported.ToImmutable()
            };
        } catch (JsonException) {
            // ToolConfiguration already reports SK9007 for an unreadable skala.jsonc; the gate does
            // not need to report it a second time, and refusing to run is not its call.
            return GateDefinition.Local with { Name = name };
        }
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
