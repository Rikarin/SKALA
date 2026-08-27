using System.Collections.Immutable;
using System.Globalization;

namespace Rikarin.Skala.Reporting;

/// <summary>
/// The aggregate numbers a run produced, and what <c>metrics.*</c> in a gate reads.
/// </summary>
/// <remarks>
/// docs/plan/07 § "Metrics" and docs/plan/09 § "Gates". ⚠ These are <em>aggregates</em>, and they
/// are a different surface from the <c>SK70xx</c> findings: a finding says "this member is over the
/// threshold", an aggregate says "the codebase's p95 is 9". A gate that read the findings instead
/// would be gating on how the thresholds happen to be configured rather than on the code.
/// <para>
/// ⚠ Percentiles rather than means. A mean cognitive complexity is dominated by the thousands of
/// three-line members every codebase has and moves by 0.01 when something terrible is added; p95
/// moves. doc 09's own example line is "cognitive complexity p95 9 (gate 15)".
/// </para>
/// </remarks>
public sealed record MetricsSummary {
    public static MetricsSummary Empty { get; } = new();

    /// <summary>How many members were measured. Zero means the metrics did not run.</summary>
    public int MemberCount { get; init; }

    public int CognitiveComplexityP95 { get; init; }

    public int CognitiveComplexityMax { get; init; }

    public int CyclomaticComplexityP95 { get; init; }

    public int CyclomaticComplexityMax { get; init; }

    public int MethodLengthP95 { get; init; }

    public int NestingDepthMax { get; init; }

    public int ParameterCountMax { get; init; }

    /// <summary>Doc-comment coverage of the publicly visible surface, as a percentage.</summary>
    public double CommentDensity { get; init; }

    /// <summary>⚠ Production duplication only. Test duplication is carried separately and never gated.</summary>
    public double Duplication { get; init; }

    public double TestDuplication { get; init; }

    public int DuplicatedLines { get; init; }

    public int TotalLines { get; init; }

    public int CloneGroupCount { get; init; }

    public bool HasDuplication => TotalLines > 0;

    /// <summary>
    /// Reads a named metric the way a gate spells it.
    /// </summary>
    /// <remarks>
    /// ⚠ An unknown name returns null and the gate reports it as an unknown condition rather than
    /// silently passing. A gate that ignores the condition somebody relies on passes for the wrong
    /// reason, which doc 09 calls out as worse than one that says it cannot run.
    /// </remarks>
    public double? Read(string name) =>
        name switch {
            "duplication" => HasDuplication ? Duplication : null,
            "cognitiveComplexity" => MemberCount > 0 ? CognitiveComplexityP95 : null,
            "cognitiveComplexityMax" => MemberCount > 0 ? CognitiveComplexityMax : null,
            "cyclomaticComplexity" => MemberCount > 0 ? CyclomaticComplexityP95 : null,
            "methodLength" => MemberCount > 0 ? MethodLengthP95 : null,
            "nestingDepth" => MemberCount > 0 ? NestingDepthMax : null,
            "parameterCount" => MemberCount > 0 ? ParameterCountMax : null,
            "commentDensity" => MemberCount > 0 ? CommentDensity : null,
            _ => null
        };

    /// <summary>The names <see cref="Read"/> understands, for the diagnostic on an unknown one.</summary>
    public static ImmutableArray<string> Names { get; } = [
        "duplication", "cognitiveComplexity", "cognitiveComplexityMax", "cyclomaticComplexity", "methodLength", "nestingDepth",
        "parameterCount", "commentDensity"
    ];

    /// <summary>
    /// ⚠ <c>commentDensity</c> is a floor and everything else is a ceiling.
    /// </summary>
    /// <remarks>
    /// A gate saying <c>commentDensity: 60</c> means "at least 60 % documented"; one saying
    /// <c>duplication: 3.0</c> means "at most 3 %". Getting this backwards is a gate that passes
    /// exactly when it should fail, so the direction is a property of the metric rather than
    /// something each gate has to spell out.
    /// </remarks>
    public static bool IsFloor(string name) => name == "commentDensity";

    /// <summary>The one line doc 09's human report ends with, when there is anything to say.</summary>
    public string Render(IReadOnlyDictionary<string, double>? thresholds = null) {
        var parts = new List<string>();
        if (HasDuplication) {
            parts.Add(
                "duplication "
                + Duplication.ToString("0.0", CultureInfo.InvariantCulture)
                + " %"
                + Gate(thresholds, "duplication")
            );
        }

        if (MemberCount > 0) {
            parts.Add(
                "cognitive complexity p95 "
                + CognitiveComplexityP95.ToString(CultureInfo.InvariantCulture)
                + Gate(thresholds, "cognitiveComplexity")
            );
        }

        return string.Join("  ·  ", parts);
    }

    static string Gate(IReadOnlyDictionary<string, double>? thresholds, string name) =>
        thresholds is not null && thresholds.TryGetValue(name, out var threshold)
            ? " (gate " + threshold.ToString("0.#", CultureInfo.InvariantCulture) + ")"
            : string.Empty;

    /// <summary>
    /// Folds a set of per-member measurements into the aggregate.
    /// </summary>
    /// <remarks>
    /// ⚠ The p95 is the nearest-rank one: sort, take the value at <c>ceil(0.95 n)</c>. Not
    /// interpolated, because an interpolated percentile over integers produces a number that is not
    /// any member's actual score, and every one of these numbers is meant to be traceable back to
    /// the member that produced it.
    /// </remarks>
    public static int Percentile(IReadOnlyList<int> sorted, double fraction) {
        if (sorted.Count == 0) {
            return 0;
        }

        var rank = (int)Math.Ceiling(fraction * sorted.Count);
        return sorted[Math.Clamp(rank - 1, 0, sorted.Count - 1)];
    }
}
