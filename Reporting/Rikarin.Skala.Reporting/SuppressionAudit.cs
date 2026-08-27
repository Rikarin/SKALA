using System.Collections.Immutable;

namespace Rikarin.Skala.Reporting;

/// <summary>
/// The four ways a finding can be made to go away without being fixed.
/// </summary>
/// <remarks>
/// ⚠ docs/plan/09 § "Gates": <c>--no-new-suppressions</c> has to cover all of them. A grep for
/// <c>#pragma</c> is not a constraint — it catches the one form that is visible in review and
/// misses the three that are not. An <c>.editorconfig</c> line turning a rule down to
/// <c>suggestion</c> for a whole directory suppresses far more than a pragma ever does, and a
/// baseline addition suppresses a specific finding forever with no marker in the source at all.
/// </remarks>
public enum SuppressionSource {
    /// <summary><c>#pragma warning disable SK1010</c>, in the source.</summary>
    Pragma,

    /// <summary><c>[SuppressMessage("Skala.Async", "SK3002")]</c>, on a symbol.</summary>
    Attribute,

    /// <summary>⚠ An <c>.editorconfig</c> severity turned down. The widest and the least visible.</summary>
    EditorConfig,

    /// <summary>⚠ A finding added to <c>.skala/baseline.sarif</c>. Invisible in the source entirely.</summary>
    Baseline
}

/// <summary>One suppression, wherever it came from.</summary>
/// <param name="Source">Which mechanism.</param>
/// <param name="RuleId">The rule suppressed, or <c>*</c> where the mechanism does not name one.</param>
/// <param name="Where">The file, and a section or a symbol where there is one.</param>
/// <param name="Detail">The severity, the justification, or the finding's message.</param>
public sealed record SuppressionEntry(SuppressionSource Source, string RuleId, string Where, string Detail) {
    public string Describe() =>
        Source switch {
            SuppressionSource.Pragma => "#pragma " + RuleId + " in " + Where,
            SuppressionSource.Attribute => "[SuppressMessage] " + RuleId + " in " + Where,
            SuppressionSource.EditorConfig => RuleId + " → " + Detail + " in " + Where,
            _ => "baseline " + RuleId + " in " + Where
        };

    /// <summary>The identity used to tell an added suppression from one that was already there.</summary>
    public (SuppressionSource, string, string, string) Key => (Source, RuleId, Where, Detail);
}

/// <summary>
/// What <c>--no-new-suppressions</c> found, comparing the working tree to a git ref.
/// </summary>
/// <remarks>
/// ⚠ <see cref="Enforced"/> is separate from "the list is empty". A run that did not audit and a
/// run that audited and found nothing are different facts, and the gate must not treat the first as
/// the second — the failure mode where the flag is misspelled and the build goes green.
/// </remarks>
public sealed record SuppressionAudit {
    public static SuppressionAudit Off { get; } = new();

    /// <summary>Whether the audit ran and the gate should act on it.</summary>
    public bool Enforced { get; init; }

    /// <summary>The git ref the comparison was against.</summary>
    public string Reference { get; init; } = string.Empty;

    /// <summary>Suppressions present now and absent at the ref. ⚠ What fails the gate.</summary>
    public ImmutableArray<SuppressionEntry> Added { get; init; } = [];

    /// <summary>Suppressions present at the ref and absent now — good news, reported and never gated.</summary>
    public ImmutableArray<SuppressionEntry> Removed { get; init; } = [];

    /// <summary>Everything the working tree currently suppresses, for <c>skala report</c>.</summary>
    public ImmutableArray<SuppressionEntry> Current { get; init; } = [];
}
