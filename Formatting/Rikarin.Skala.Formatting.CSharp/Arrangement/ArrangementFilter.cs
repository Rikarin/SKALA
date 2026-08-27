using System.Collections.Immutable;

namespace Rikarin.Skala.Formatting.CSharp.Arrangement;

/// <summary>
/// Which rules of the catalogue may run — <c>skala arrange --include/--exclude</c> (docs/plan/11).
/// </summary>
/// <remarks>
/// ⚠ Also what makes the M4 agreement number honest. Three of doc 06's rewrites have no oracle at
/// all (<c>is not null</c>, the empty-string literal, redundant braces — see
/// <c>docs/oracle-cleanup-profile.md</c>), so the changed-span differential runs with those excluded
/// and measures them against hand-written fixtures separately. Scoring a correct rewrite as a
/// divergence because the oracle declines to perform it would make the number say the opposite of
/// what it means.
/// </remarks>
public sealed record ArrangementFilter(ImmutableHashSet<string> Include, ImmutableHashSet<string> Exclude) {
    public static ArrangementFilter All { get; } = new([], []);

    /// <summary>The rules that have an oracle to be measured against.</summary>
    public static ArrangementFilter OracleComparable { get; } = new(
        [],
        [ArrangeIds.NullCheckingPattern, ArrangeIds.EmptyString, ArrangeIds.RedundantBraces]
    );

    public bool Allows(ArrangementRule rule) =>
        (Include.IsEmpty || Include.Contains(rule.Id)) && !Exclude.Contains(rule.Id);

    public static ArrangementFilter Parse(IEnumerable<string>? include, IEnumerable<string>? exclude) =>
        new(
            [.. include ?? []],
            [.. exclude ?? []]
        );
}
