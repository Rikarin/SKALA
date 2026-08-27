using System.Collections.Immutable;
using Rikarin.Skala.Core.Diagnostics;

namespace Rikarin.Skala.Reporting;

/// <summary>One replacement in one file. The only shape a fix ever takes (ADR-005).</summary>
/// <remarks>
/// Offsets are character offsets into the file as it was read, which is what
/// <c>artifactChanges[].replacements[].deletedRegion.charOffset</c> is in SARIF and what
/// <c>EditEmitter.Apply</c> takes. ⚠ Never line/column: a fix expressed in lines is a fix that has
/// to be re-resolved against a file that may have moved under it.
/// </remarks>
public sealed record FixEdit(string Path, int Start, int Length, string Text) {
    public int End => Start + Length;
}

/// <summary>
/// Which side of the baseline a finding is on (docs/plan/09 § "The baseline").
/// </summary>
/// <remarks>
/// ⚠ <see cref="Unknown"/> exists so that "no baseline was loaded" is not spelled the same way as
/// "the baseline did not have this". A gate with <c>newIssues: 0</c> and no baseline would
/// otherwise fail on every finding in the repository, which is the opposite of what the condition
/// is for.
/// </remarks>
public enum BaselineBucket {
    /// <summary>No baseline took part in this run.</summary>
    Unknown,

    /// <summary>The fingerprint is not in the baseline.</summary>
    New,

    /// <summary>The fingerprint is in the baseline and the finding still fires.</summary>
    Existing
}

/// <summary>Why a finding was reported but not counted.</summary>
public enum SuppressionKind {
    None,

    /// <summary><c>#pragma warning disable</c>, in the source.</summary>
    Pragma,

    /// <summary><c>[SuppressMessage]</c>, on a symbol.</summary>
    Attribute,

    /// <summary>⚠ A higher-precedence rule reported the same span (docs/plan/08 § `supersedes`).</summary>
    Superseded
}

/// <summary>
/// One finding, from any source: a Skala rule, a hosted third-party analyzer, the compiler, or the
/// formatter.
/// </summary>
/// <remarks>
/// ⚠ This is the only representation between analysis and reporting. ADR-009 makes SARIF the
/// canonical serialisation and every other surface a renderer over it; keeping one in-memory shape
/// is what makes "the agent and the human see the same finding" true by construction.
/// </remarks>
public sealed record Finding {
    public required string RuleId { get; init; }

    public required SkalaSeverity Severity { get; init; }

    public required string Message { get; init; }

    /// <summary>Absolute path. Rendered relative to the repository root, never stored that way.</summary>
    public required string Path { get; init; }

    public int Line { get; init; }

    public int Column { get; init; }

    public int EndLine { get; init; }

    public int EndColumn { get; init; }

    /// <summary>Character offsets, for the fix and for the fingerprint.</summary>
    public int Start { get; init; }

    public int Length { get; init; }

    public ImmutableArray<FixEdit> Fix { get; init; } = [];

    /// <summary>⚠ Whether the fix may be applied by <c>--safe</c> without review.</summary>
    public bool FixIsSafe { get; init; }

    /// <summary>
    /// The target frameworks this finding was produced under.
    /// </summary>
    /// <remarks>
    /// ⚠ Multi-targeting produces near-duplicate diagnostics; they are merged on
    /// <c>(ruleId, file, line, column, message)</c> and the list is carried here, so a finding that
    /// only occurs under one target is visibly a one-target finding rather than silently a
    /// deduplicated one.
    /// </remarks>
    public ImmutableArray<string> TargetFrameworks { get; init; } = [];

    public SuppressionKind Suppression { get; init; } = SuppressionKind.None;

    /// <summary>Set when the finding is inside the range <c>--since</c> named. The gate reads it.</summary>
    public bool IsInChangedCode { get; init; }

    /// <summary>
    /// The display string of the symbol the finding sits in — <c>Vixen.Core.Foo.Bar(int, string)</c>.
    /// </summary>
    /// <remarks>
    /// ⚠ docs/plan/09 § "The fingerprint": stable across file moves, which is the whole point. A
    /// lambda or a local function reports its containing member instead of itself, because a
    /// lambda's display string contains its position and a fingerprint built on it would move
    /// whenever anything above it moved — the failure this term exists to prevent.
    /// </remarks>
    public string EnclosingSymbol { get; init; } = string.Empty;

    /// <summary>The finding's own span, whitespace collapsed, identifiers preserved.</summary>
    /// <remarks>
    /// ⚠ Not the message. A message can carry a line number, a count or a path; the source text of
    /// the span cannot, and it is what actually identifies the finding.
    /// </remarks>
    public string Snippet { get; init; } = string.Empty;

    /// <summary>
    /// Which of several otherwise identical findings inside one symbol this is.
    /// </summary>
    /// <remarks>
    /// ⚠ Assigned once, by <c>Fingerprints.Assign</c>, over the whole run in a deterministic order.
    /// Without it two identical findings in one method share a fingerprint, and a baseline that
    /// accepts one accepts both.
    /// </remarks>
    public int OrdinalWithinSymbol { get; init; }

    /// <summary>
    /// The baseline bucket this finding fell into, once a baseline has been applied.
    /// </summary>
    /// <remarks>
    /// ⚠ <see cref="BaselineBucket.Unknown"/> means no baseline was in play, which is a different
    /// statement from "new" and the gate has to be able to tell them apart.
    /// </remarks>
    public BaselineBucket Bucket { get; init; } = BaselineBucket.Unknown;

    public bool HasFix => !Fix.IsEmpty;

    /// <summary>The dedup identity of a finding across target frameworks.</summary>
    public (string, string, int, int, string) MergeKey => (RuleId, Path, Line, Column, Message);
}
