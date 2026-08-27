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

    /// <summary>Set when the finding is inside the range <c>--since</c> named. M6 gates on it.</summary>
    public bool IsInChangedCode { get; init; }

    public bool HasFix => !Fix.IsEmpty;

    /// <summary>The dedup identity of a finding across target frameworks.</summary>
    public (string, string, int, int, string) MergeKey => (RuleId, Path, Line, Column, Message);
}
