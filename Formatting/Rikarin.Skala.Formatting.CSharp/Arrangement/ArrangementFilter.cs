using System.Collections.Immutable;

namespace Rikarin.Skala.Formatting.CSharp.Arrangement;

/// <summary>
///     Which rules of the catalogue may run — <c>skala arrange --include/--exclude</c> (docs/plan/11).
/// </summary>
/// <remarks>
///     ⚠ Also what makes the M4 agreement number honest. Three of doc 06's rewrites have no oracle at
///     all (<c>is not null</c>, the empty-string literal, redundant braces — see
///     <c>docs/oracle-cleanup-profile.md</c>), so the changed-span differential runs with those excluded
///     and measures them against hand-written fixtures separately. Scoring a correct rewrite as a
///     divergence because the oracle declines to perform it would make the number say the opposite of
///     what it means.
/// </remarks>
public sealed record ArrangementFilter(ImmutableHashSet<string> Include, ImmutableHashSet<string> Exclude) {
    public static ArrangementFilter All { get; } = new([], []);

    /// <summary>
    ///     The rules that have an oracle to be measured against.
    /// </summary>
    /// <remarks>
    ///     ⚠ Four exclusions, and they are not the same kind of thing.
    ///     <para>
    ///         Three — <see cref="ArrangeIds.NullCheckingPattern" />, <see cref="ArrangeIds.EmptyString" />,
    ///         <see cref="ArrangeIds.RedundantBraces" /> — the oracle will not perform at all, under any
    ///         profile (<c>docs/oracle-cleanup-profile.md</c>).
    ///     </para>
    ///     <para>
    ///         ⚠ The fourth, <see cref="ArrangeIds.Usings" />, is excluded for a different and more
    ///         uncomfortable reason: over <c>corpus/real/</c> the oracle's answer is <em>wrong</em>, and
    ///         predictably so. "Is this using needed" is a question about the references a project has, and
    ///         the oracle's scratch project has none but the shared framework — so cleanupcode deletes
    ///         <c>using NUnit.Framework;</c> from a file full of <c>[Test]</c> attributes, because
    ///         <c>NUnit.Framework</c> does not resolve there. Skala keeps it, because
    ///         <see cref="UsingsRule.Unused" /> removes only what the compiler reports as <c>CS8019</c> and an
    ///         unresolvable using is <c>CS0246</c>. Scoring Skala against that would reward deleting usings
    ///         whose packages are missing, which is the opposite of the rule's contract. The rule is pinned
    ///         by <c>constructs/arrangement/usings/</c>, where every namespace resolves inside the corpus
    ///         itself and the oracle's answer is trustworthy.
    ///     </para>
    /// </remarks>
    public static ArrangementFilter OracleComparable { get; } = new(
        [],
        [
            ArrangeIds.NullCheckingPattern,
            ArrangeIds.EmptyString,
            ArrangeIds.RedundantBraces,
            ArrangeIds.Usings
        ]
    );

    public bool Allows(ArrangementRule rule) =>
        (Include.IsEmpty || Include.Contains(rule.Id)) && !Exclude.Contains(rule.Id);

    public static ArrangementFilter Parse(IEnumerable<string>? include, IEnumerable<string>? exclude) =>
        new(
            [.. include ?? []],
            [.. exclude ?? []]
        );
}
