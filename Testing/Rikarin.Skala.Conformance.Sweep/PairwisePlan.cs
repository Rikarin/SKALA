using Rikarin.Skala.Options;
using Rikarin.Skala.Testing;

namespace Rikarin.Skala.Conformance.Sweep;

/// <summary>One pair of options to ask about together, with the grid to ask over.</summary>
/// <param name="Primary">The per-construct key. Its <c>oracle</c> fixture is the one both are asked on.</param>
/// <param name="Secondary">The global key it is believed to interact with.</param>
/// <param name="PrimaryValues">The primary's probe values.</param>
/// <param name="SecondaryValues">The secondary's probe values.</param>
/// <param name="Fixture">The corpus file the grid is measured on.</param>
public sealed record PairCandidate(
    OptionInfo Primary,
    OptionInfo Secondary,
    IReadOnlyList<string> PrimaryValues,
    IReadOnlyList<string> SecondaryValues,
    CorpusFile Fixture) {
    public int Corners => PrimaryValues.Count * SecondaryValues.Count;
}

/// <summary>A pair the sweep cannot ask about, and why not.</summary>
public sealed record PairExclusion(string Primary, string Secondary, string Reason);

/// <summary>What one call to <see cref="PairwisePlan.Build" /> produced.</summary>
public sealed record PairwisePlanResult(
    IReadOnlyList<PairCandidate> Candidates,
    IReadOnlyList<PairExclusion> Excluded);

/// <summary>
///     Chooses which pairs of options are asked about together.
/// </summary>
/// <remarks>
///     ⚠ <b>Why a named list and not every pair.</b> 283 sweepable options is 39 903 pairs, and the
///     interesting ones are not uniformly distributed — they are the families the design already says
///     interact, because someone measured them. docs/plan/12 § "Interactions are out of scope" names
///     three: <c>keep_existing_*</c> × <c>keep_user_linebreaks</c>, <c>wrap_*</c> ×
///     <c>max_line_length</c>, and <c>align_*</c> × <c>indent_*</c>. Those are the three below, and the
///     set is deliberately a hypothesis to be falsified rather than a search.
///     <para>
///         ⚠ The <c>keep</c> family is not a guess: docs/plan/05 § "<c>keep_existing_*</c>" carries a
///         <b>four-way table</b> that M2 measured corner by corner, and its finding is that the two keys
///         govern <em>different gaps of the same construct</em> — the per-construct key the delimiters, the
///         global key the gaps between items. A table like that is a statement about the interior of the
///         grid, and the one-at-a-time sweep visits none of the interior.
///     </para>
///     <para>
///         ⚠ Nothing here holds a hard-coded option list, for the same reason <see cref="SweepPlan" />
///         does not: the registry moves. A family is matched by prefix after the vendor prefix is
///         stripped, and the secondary is resolved by key at run time so that a rename is a loud failure
///         rather than a silently empty plan.
///     </para>
/// </remarks>
public static class PairwisePlan {
    /// <summary>The named interacting families, as docs/plan/12 lists them.</summary>
    /// <remarks>
    ///     ⚠ <c>keep_existing_linebreaks</c> is deliberately not a primary. docs/plan/05 warns that it
    ///     "reads like one of the family and is not" — it is the per-language form of the global
    ///     <c>keep_user_linebreaks</c>, so pairing the two would measure a key against itself and report
    ///     a guaranteed interaction that means nothing.
    /// </remarks>
    public static readonly IReadOnlyList<PairFamily> Families = [
        new("keep", "keep_existing", "resharper_keep_user_linebreaks", ["keep_existing_linebreaks"]),
        new("wrap", "wrap", "resharper_csharp_max_line_length", []),
        new("align", "align", "resharper_csharp_indent_size", [])
    ];

    public static PairwisePlanResult Build(IReadOnlyList<string> families) {
        var candidates = new List<PairCandidate>();
        var excluded = new List<PairExclusion>();
        var arrangement = Rikarin.Skala.Formatting.CSharp.Arrangement.ArrangementOptions.Implemented.ToHashSet();

        foreach (var family in Families) {
            if (families.Count > 0 && !families.Contains(family.Name, StringComparer.Ordinal)) {
                continue;
            }

            if (!OptionRegistry.TryResolve(family.SecondaryKey, out var secondaryId)) {
                excluded.Add(
                    new PairExclusion(
                        family.Prefix + "_*",
                        family.SecondaryKey,
                        "the secondary key is not in options.json — it was renamed, and this family is "
                        + "now measuring nothing"
                    )
                );
                continue;
            }

            var secondary = OptionRegistry.Get(secondaryId);
            var secondaryValues = OptionDomain.Probes(secondary).Distinct(StringComparer.Ordinal).ToArray();

            foreach (var primary in OptionRegistry.All.OrderBy(static info => info.Key, StringComparer.Ordinal)) {
                if (!SweepPlan.InFamily(primary.Key, [family.Prefix])
                    || string.Equals(primary.Key, secondary.Key, StringComparison.Ordinal)) {
                    continue;
                }

                if (family.Skip.Any(skip => SweepPlan.Strip(primary.Key).StartsWith(skip, StringComparison.Ordinal)
                    )) {
                    excluded.Add(new PairExclusion(primary.Key, secondary.Key, "excluded by name: " + family.Name));
                    continue;
                }

                if (!SweepPlan.Languages.Contains(primary.Language, StringComparer.Ordinal)) {
                    excluded.Add(
                        new PairExclusion(primary.Key, secondary.Key, "language is '" + primary.Language + "'")
                    );
                    continue;
                }

                // ⚠ Same exclusion as the single sweep and for the same reason: an arrangement key is
                // read by the arranger and pinned by the cleanup profile, and this pass runs
                // format-only. Sweeping one here would invent a divergence rather than find one.
                if (arrangement.Contains(primary.Id)) {
                    excluded.Add(
                        new PairExclusion(
                            primary.Key,
                            secondary.Key,
                            "arrangement option: needs the cleanup profile, not CSReformatCode"
                        )
                    );
                    continue;
                }

                if (primary.Oracle is not { Length: > 0 } glob) {
                    excluded.Add(new PairExclusion(primary.Key, secondary.Key, "no `oracle` fixture in the registry"));
                    continue;
                }

                var matches = CorpusGlob.Resolve(glob);
                if (matches.Count == 0) {
                    excluded.Add(
                        new PairExclusion(
                            primary.Key,
                            secondary.Key,
                            "`oracle` is '" + glob + "' and no corpus file matches it"
                        )
                    );
                    continue;
                }

                var fixture = matches[0];

                var primaryValues = OptionDomain.Probes(primary).Distinct(StringComparer.Ordinal).ToArray();
                if (primaryValues.Length < 2 || secondaryValues.Length < 2) {
                    excluded.Add(
                        new PairExclusion(primary.Key, secondary.Key, "fewer than two values on one side of the grid")
                    );
                    continue;
                }

                candidates.Add(new PairCandidate(primary, secondary, primaryValues, secondaryValues, fixture));
            }
        }

        return new PairwisePlanResult(candidates, excluded);
    }
}

/// <summary>A family of per-construct keys and the global key they are believed to interact with.</summary>
/// <param name="Name">What <c>--family</c> selects it by.</param>
/// <param name="Prefix">The primary family, matched after the vendor prefix is stripped.</param>
/// <param name="SecondaryKey">The global key, resolved through the registry at run time.</param>
/// <param name="Skip">Primaries excluded by name, each with a recorded reason in the remarks above.</param>
public sealed record PairFamily(
    string Name,
    string Prefix,
    string SecondaryKey,
    IReadOnlyList<string> Skip);
