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
///     One finding. Configuration diagnostics carry a file and line because a configuration complaint
///     without a line is a complaint about a 4 238-line file.
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
///     The SK9000 range. docs/plan/08-rule-catalogue.md § "SK9000 — Tool diagnostics".
///     ⚠ ADR-012: an id is allocated once and never redefined.
/// </summary>
public static class ConfigDiagnosticIds {
    /// <summary>
    ///     Unknown configuration key. Info by default — the export carries ~2 000 keys Skala will never implement.
    /// </summary>
    public const string UnknownKey = "SK9001";

    /// <summary>
    ///     An option Skala owns was set to a value outside the option's domain, so the configured value
    ///     was discarded and something else is in force.
    /// </summary>
    /// <remarks>
    ///     ⚠
    ///     <b>
    ///         Warning, where <see cref="UnknownKey" /> is info, and the difference is whose mistake it
    ///         is.
    ///     </b> SK9001 is info because a Rider export carries some two thousand keys Skala will never
    ///     implement and a tool that warns about all of them on first run gets uninstalled on first run —
    ///     the user wrote nothing wrong. This is the opposite case: the key <em>is</em> in the registry,
    ///     Skala owns it, the user's intent was recorded and then thrown away, and the code is formatted
    ///     against a value nobody chose. Non-negotiable #4 (docs/plan/00) says unknown configuration is a
    ///     diagnostic and never a silent default; an out-of-domain value was a silent default until M9,
    ///     which is the letter of the rule satisfied and its substance missed.
    ///     <para>
    ///         ⚠ It is also the one configuration diagnostic that fails <c>skala config check</c> without
    ///         <c>--strict</c>. Every other warning there describes a configuration that means something
    ///         and might mean the wrong thing; this one describes a line that means nothing at all, and
    ///         there is no reading of it under which the repository is configured as its author intended.
    ///     </para>
    /// </remarks>
    public const string OptionValueOutOfDomain = "SK9017";

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

    /// <summary><c>skala.jsonc</c> is not valid JSON, so no tool configuration could be read.</summary>
    public const string ToolConfigNotJson = "SK9007";

    /// <summary>No solution or project could be found to load, so nothing was analysed.</summary>
    public const string NothingToLoad = "SK9024";

    /// <summary>No binary log was found, so the binlog load mode had nothing to read.</summary>
    public const string NoBinlog = "SK9022";

    /// <summary>The requested paths contain no C# files.</summary>
    public const string NoSourceFiles = "SK9023";

    /// <summary>The requested load mode produced no compilation, so a fallback mode was used.</summary>
    public const string LoadModeFellBack = "SK9025";

    /// <summary>
    ///     <c>--rules</c> names an id no rule in this run can produce, so those ids contribute nothing.
    /// </summary>
    /// <remarks>
    ///     ⚠ It exists because the absence of it read as a clean tree (#278). <c>--rules SK3510,SK3511</c>
    ///     bound a single string containing a comma, matched no rule, and exited 0 with no output — half
    ///     an hour of an agent believing its analyzers were dead while they were fine. The comma is now
    ///     split, but a mistyped id reaches the same false clean by a different route, so the filter is
    ///     checked against what actually loaded. A filter that is unknown <em>in full</em> is refused
    ///     rather than reported under this id: such a run cannot produce a finding, and its zero is not
    ///     a measurement.
    /// </remarks>
    public const string UnknownRuleFilter = "SK9026";

    /// <summary>
    ///     The managed canonical block does not hash to what its own marker says. Somebody edited it.
    ///     This is the gate condition: drift is a finding, not a surprise (docs/plan/03 § "Canonical
    ///     distribution").
    /// </summary>
    public const string CanonicalDrift = "SK9008";

    /// <summary>
    ///     The repository is on an older canonical than the tool carries. Info, never a failure — a
    ///     canonical bump must not turn eighteen repositories red on the day it is published.
    /// </summary>
    public const string CanonicalBehind = "SK9009";

    /// <summary>
    ///     The local block overrides an option the canonical block also sets. Info: this is the
    ///     mechanism working, and the report is the review artefact.
    /// </summary>
    public const string CanonicalLocalOverride = "SK9013";

    /// <summary>The repository's <c>.editorconfig</c> carries no canonical block at all.</summary>
    public const string CanonicalUnmanaged = "SK9014";

    /// <summary>
    ///     Applying the canonical changes a <c>dotnet_diagnostic</c> severity. Warning when it moves a
    ///     <em>compiler</em> diagnostic up, because under <c>TreatWarningsAsErrors</c> that is a build
    ///     failure from a commit that touches no code; info otherwise.
    /// </summary>
    /// <remarks>
    ///     ⚠ <c>SK9013</c> reports the same thing for keys the option registry owns, and
    ///     <c>dotnet_diagnostic</c> keys deliberately are not in it — so until M9 the loudest thing the
    ///     canonical does to a repository was the one thing it did silently. See
    ///     <c>DiagnosticSeverityChange</c>.
    /// </remarks>
    public const string CanonicalSeverityChange = "SK9016";

    /// <summary>
    ///     <c>skala.jsonc</c> tried to pin a canonical version. The pin lives in the
    ///     <c>.editorconfig</c> marker, beside the bytes it describes, because a version recorded away
    ///     from the thing it versions is a version that drifts.
    /// </summary>
    public const string CanonicalVersionInToolConfig = "SK9012";
}
