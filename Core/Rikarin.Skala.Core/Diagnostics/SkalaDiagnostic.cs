namespace Rikarin.Skala.Core.Diagnostics;

/// <summary>docs/plan/03-configuration-model.md § "Severities".</summary>
public enum SkalaSeverity {
    /// <summary>ReSharper's <c>hint</c>. Only shown with <c>--include-hints</c>.</summary>
    Hidden,

    /// <summary>ReSharper's <c>suggestion</c>. Shown, dimmed; never fails a gate.</summary>
    Info,

    /// <summary>Fails a gate depending on the gate.</summary>
    Warning,

    /// <summary>Always fails a gate.</summary>
    Error
}

/// <summary>
/// One finding. Configuration diagnostics carry a file and line because a configuration complaint
/// without a line is a complaint about a 4 238-line file.
/// </summary>
public sealed record SkalaDiagnostic(
    string Id,
    SkalaSeverity Severity,
    string Message,
    string? File = null,
    int Line = 0,
    string? Detail = null) {
    public string Location =>
        File is null
        ? string.Empty
        : Line > 0
        ? $"{File}:{Line.ToString(System.Globalization.CultureInfo.InvariantCulture)}"
        : File;

    public override string ToString() {
        var location = Location;
        var prefix = location.Length == 0 ? string.Empty : location + ": ";
        return $"{prefix}{Severity.ToString().ToLowerInvariant()} {Id}: {Message}";
    }
}

/// <summary>
/// The SK9000 range. docs/plan/08-rule-catalogue.md § "SK9000 — Tool diagnostics".
/// ⚠ ADR-012: an id is allocated once and never redefined.
/// </summary>
public static class ConfigDiagnosticIds {
    /// <summary>Unknown configuration key. Info by default — the export carries ~2 000 keys Skala will never implement.</summary>
    public const string UnknownKey = "SK9001";

    /// <summary>The effective configuration draws from a file above the repository root.</summary>
    public const string InheritedFromAbove = "SK9002";

    /// <summary>A style key appeared in <c>skala.jsonc</c>, which cannot set style (ADR-001).</summary>
    public const string StyleKeyInToolConfig = "SK9003";

    /// <summary>Two spellings of one option are set at the same specificity with different values.</summary>
    public const string DuplicateAlias = "SK9004";

    /// <summary>Two settings contradict each other; the report says which one wins.</summary>
    public const string ContradictoryOptions = "SK9005";

    /// <summary>A setting is on that Skala cannot honour, and that makes the IDE and the oracle disagree.</summary>
    public const string UnhonourableSetting = "SK9006";
}
