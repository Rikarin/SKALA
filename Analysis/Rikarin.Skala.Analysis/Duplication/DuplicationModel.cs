using System.Collections.Immutable;

namespace Rikarin.Skala.Analysis.Duplication;

/// <summary>
/// One file offered to the detector.
/// </summary>
/// <remarks>
/// ⚠ The two flags are the caller's answer, not the detector's guess. Whether a path is generated
/// is already decided once, by the loader (<c>BinlogLoader.IsGenerated</c>), and whether it is a
/// test is a repository convention; a detector that re-derived either would be a second opinion
/// that can disagree with the first.
/// </remarks>
/// <param name="Path">Absolute path. It is carried through to <see cref="CloneOccurrence.Path"/> verbatim.</param>
/// <param name="Text">The file's text, as it was read.</param>
/// <param name="IsGenerated">Excluded from the findings <i>and</i> from both halves of the percentage.</param>
/// <param name="IsTest">Measured, but counted and reported separately (docs/plan/09 § "Duplication").</param>
public sealed record DuplicationInput(string Path, string Text, bool IsGenerated, bool IsTest);

/// <summary>
/// One occurrence of a clone.
/// </summary>
/// <param name="Path">The <see cref="DuplicationInput.Path"/> this occurrence is in.</param>
/// <param name="Start">Character offset of the first token, into the file as it was read.</param>
/// <param name="Length">Characters from <paramref name="Start"/> to the end of the last token.</param>
/// <param name="StartLine">1-based, like <c>Finding.Line</c>.</param>
/// <param name="EndLine">1-based and inclusive: the line the last token ends on.</param>
public sealed record CloneOccurrence(string Path, int Start, int Length, int StartLine, int EndLine);

/// <summary>
/// A maximal clone group: the same token run, normalised, in two or more places.
/// </summary>
/// <remarks>
/// Maximal in both directions — no occurrence can be extended by one more token without one of the
/// others disagreeing. <see cref="Occurrences"/> is sorted by path then offset, and
/// <c>Occurrences[0]</c> is the "first occurrence" the finding is reported at.
/// </remarks>
public sealed record CloneGroup(int TokenLength, ImmutableArray<CloneOccurrence> Occurrences);

/// <summary>
/// One duplication measurement over a file set: the groups, and the percentage the
/// <c>metrics.duplication</c> gate reads.
/// </summary>
/// <remarks>
/// ⚠ Production and test are two measurements, not one measurement with a flag. docs/plan/09:
/// "test files are counted separately, because test duplication is often deliberate and gating it
/// drives people to write worse tests". They are also matched separately — a production file is
/// never compared against a test file — so that a group's bucket is never ambiguous.
/// <para>
/// Generated files appear in neither, which is what "excluded from both numerator and denominator"
/// means: they are dropped before anything is counted.
/// </para>
/// </remarks>
public sealed record DuplicationResult {
    /// <summary>Clone groups among production files, in deterministic order.</summary>
    public ImmutableArray<CloneGroup> Groups { get; init; } = [];

    /// <summary>Clone groups among test files. Never mixed with <see cref="Groups"/>.</summary>
    public ImmutableArray<CloneGroup> TestGroups { get; init; } = [];

    /// <summary>
    /// Production lines taking part in at least one clone.
    /// </summary>
    /// <remarks>
    /// ⚠ Each distinct line counts once however many groups it is in. A line counted twice makes a
    /// percentage that can exceed 100, which is a metric nobody will trust again.
    /// </remarks>
    public int DuplicatedLines { get; init; }

    /// <summary>Production lines measured, generated files excluded.</summary>
    public int TotalLines { get; init; }

    /// <summary>Test lines taking part in at least one clone.</summary>
    public int TestDuplicatedLines { get; init; }

    /// <summary>Test lines measured, generated files excluded.</summary>
    public int TestTotalLines { get; init; }

    /// <summary>The gate's number. 0 when nothing was measured — never a division by zero.</summary>
    public double Percentage => TotalLines == 0 ? 0 : DuplicatedLines * 100.0 / TotalLines;

    /// <summary>The same for tests, reported beside the gate's number and never folded into it.</summary>
    public double TestPercentage => TestTotalLines == 0 ? 0 : TestDuplicatedLines * 100.0 / TestTotalLines;
}
