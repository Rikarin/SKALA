namespace Rikarin.Skala.Release;

/// <summary>Whether a detector ran, and if not, why not.</summary>
/// <remarks>
/// ⚠ The distinction is the anti-vacuity mechanism. A detector that cannot see a baseline reports
/// <see cref="Unmeasured"/> and says so in the notes; it never reports "no change", because "no
/// change" and "I did not look" produce the same version number and must not produce the same
/// sentence. Three of this repository's four previous guard mechanisms failed by conflating them.
/// </remarks>
public enum DetectorState {
    Measured,
    Unmeasured
}

/// <summary>One compatibility surface's verdict, with the measurement behind it.</summary>
public sealed record DetectorResult(
    string Surface,
    DetectorState State,
    BumpKind Bump,
    string Headline,
    IReadOnlyList<string> Details
) {
    public static DetectorResult Unmeasured(string surface, string why) =>
        new(surface, DetectorState.Unmeasured, BumpKind.Patch, why, []);

    public static DetectorResult Measured(
        string surface,
        BumpKind bump,
        string headline,
        IReadOnlyList<string>? details = null
    ) =>
        new(surface, DetectorState.Measured, bump, headline, details ?? []);

    /// <summary>
    /// The bump this result contributes. ⚠ An unmeasured surface contributes nothing rather than
    /// <see cref="BumpKind.Patch"/>: it has no opinion, and a floor it did not measure is a claim.
    /// </summary>
    public BumpKind? Contribution => State == DetectorState.Measured ? Bump : null;
}
