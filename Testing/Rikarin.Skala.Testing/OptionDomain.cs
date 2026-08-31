using Rikarin.Skala.Options;
using System.Globalization;

namespace Rikarin.Skala.Testing;

/// <summary>
///     What an option accepts, and what it must not, derived from the registry entry.
/// </summary>
/// <remarks>
///     ⚠ <b>One copy.</b> This logic existed five times — <c>OptionCoverageTests</c>,
///     <c>OptionObservabilityTests</c>, <c>SweepPlan</c>, <c>DefaultsProbe</c> and the harness's own
///     <c>probe</c> command — with a comment in one of them saying it was "kept deliberately identical"
///     to another. It was not a hypothetical hazard: giving <c>int</c> options a minimum broke four of
///     the five at once, because every copy offered <c>0</c> as a probe value for keys whose floor is
///     now 1. Five places to keep in step is five chances to update four.
///     <para>
///         ⚠ <see cref="Probes" /> and <see cref="EveryLegalValue" /> answer different questions and are
///         not interchangeable. A probe set asks "can this option be observed at all" and wants two or
///         three values that are cheap to format at; the legal set asks "does the tool accept everything
///         it says it accepts" and has to be exhaustive, aliases included.
///     </para>
/// </remarks>
public static class OptionDomain {
    /// <summary>A value guaranteed to be in no enum's domain, and to parse as no integer.</summary>
    public const string NotAValue = "sideways_and_upside_down";

    /// <summary>
    ///     Flags members that name the whole set or the empty one, and so combine with nothing.
    /// </summary>
    /// <remarks>
    ///     ⚠ A name-based heuristic, and it is stated as one. These are the three spellings the registry's
    ///     four flags enums actually use for "everything" and "nothing" — <c>all</c> and <c>none</c> in
    ///     <c>NewLineBeforeOpenBrace</c>, <c>none</c> in <c>BinaryOperationGroup</c>, and <c>false</c> in
    ///     <c>SpaceBetweenParentheses</c>, which spells the empty set as a bool.
    ///     <para>
    ///         ⚠ <b>It does not know about hierarchical aggregates and must not pretend to.</b>
    ///         <c>BinaryOperationGroup</c> has <c>arithmetic</c>, <c>bitwise</c> and <c>conditional</c>,
    ///         each of which subsumes several leaves, and no field in <c>options.json</c> says so. A
    ///         combination drawn from those would be legal but degenerate, and the sweep would report it
    ///         as an ordinary agreement rather than as a probe that could not discriminate. The
    ///         declaration-order pick below avoids them on today's registry; a fifth flags enum could
    ///         defeat it, and the fix then is to record aggregation in the registry rather than to grow
    ///         this list.
    ///     </para>
    /// </remarks>
    static readonly string[] Aggregates = ["all", "none", "false"];

    /// <summary>True when every string is a legal value, so nothing can be refused.</summary>
    public static bool IsFreeForm(OptionInfo info) => info.Kind == OptionValueKind.String;

    /// <summary>
    ///     The values an observability test flips one option across.
    /// </summary>
    /// <remarks>
    ///     For a bool, both values. For an enum, its whole domain. For an int, the configured value and
    ///     one that is definitely different — an int's domain is unbounded above and the point is
    ///     observability, not exhaustiveness.
    ///     <para>
    ///         ⚠ Clamped into the option's declared bounds. The probe set used to offer <c>0</c>
    ///         unconditionally, which is a legal width (<c>max_line_length = 0</c> means 120) and an
    ///         illegal indent (an indent of zero is not a narrower indent), and the difference is only
    ///         visible from the registry.
    ///     </para>
    /// </remarks>
    public static IEnumerable<string> Probes(OptionInfo info) {
        switch (info.Kind) {
            case OptionValueKind.Bool:
                yield return "true";
                yield return "false";
                break;

            case OptionValueKind.Enum:
                foreach (var value in OptionEnums.ValuesOf(info.EnumName!)) {
                    yield return value;
                }

                break;

            case OptionValueKind.Flags:
                var flags = OptionEnums.ValuesOf(info.EnumName!);
                foreach (var value in flags) {
                    yield return value;
                }

                // ⚠ One real combination, not the join of everything. See CombinationProbe.
                if (CombinationProbe(flags) is { } combination) {
                    yield return combination;
                }

                break;

            case OptionValueKind.Int:
                var current = Number(info.Default);
                var seen = new HashSet<int>();

                // ⚠ A third value, because two are not enough for a counter whose configured value
                // is a stand-in for "no cap". `max_invocation_arguments_on_line = 10000` against 0
                // is observable — 0 chops — but `max_line_length = 120` against 0 is not, because 0
                // means 120 and the pair is the same number twice. One is a cap on a count and the
                // other is a width; they have no common "obviously different" second value.
                foreach (var candidate in new[] { current, current == 0 ? 3 : current == 1 ? 2 : 0, 1 }) {
                    var clamped = Clamp(info, candidate);
                    if (seen.Add(clamped)) {
                        yield return clamped.ToString(CultureInfo.InvariantCulture);
                    }
                }

                break;

            default:
                yield return info.Default ?? string.Empty;
                yield return info.Default is null or "" ? "x" : info.Default + "x";
                break;
        }
    }

    /// <summary>
    ///     Every value the option's declared domain contains, aliases and severity suffixes included.
    /// </summary>
    /// <remarks>
    ///     ⚠ The alias half is not decoration. <c>TryParse</c> accepts <c>true</c> for
    ///     <c>PlacementStyle.always</c> and <c>DoNotTouch</c> for <c>do_not_touch</c>, so a suite that
    ///     sweeps <see cref="OptionEnums.ValuesOf" /> alone tests less than the tool promises. A
    ///     severity-suffixed key is the same case in another spelling: <c>value:warning</c> is one
    ///     value and one severity in one assignment, and the value half is the domain.
    /// </remarks>
    /// <summary>
    ///     One genuine multi-flag value for a flags option, or <see langword="null" /> when the domain
    ///     has fewer than two members that can combine.
    /// </summary>
    /// <remarks>
    ///     ⚠ <b>What this replaced, and why it was worth replacing.</b> The probe set used to end with the
    ///     join of <em>every</em> member. For <c>csharp_new_line_before_open_brace</c> that is
    ///     <c>all, none, accessors, …, types</c> — and <c>all</c> is in it, so both engines parse the
    ///     combination and <c>all</c> dominates every other member. The probe was therefore a second copy
    ///     of the <c>all</c> singleton wearing fourteen names, and on a fixture already written in that
    ///     layout it scored as an agreement. Measured at <c>603fbd3</c>: the oracle's output at that value
    ///     is byte-identical to the fixture, and one of the option's three agreements out of fifteen values
    ///     was this.
    ///     <para>
    ///         ⚠ <b>The gap it left is the real defect.</b> Fourteen singletons and one value equivalent to
    ///         a singleton means the probe set never tested a combination at all — and combinations are what
    ///         a flags option is <em>for</em>. A formatter that honours <c>methods</c> and honours
    ///         <c>types</c> and mishandles <c>methods, types</c> passed every probe. This is the same hole
    ///         docs/plan/12 names across two keys, one key inwards.
    ///     </para>
    ///     <para>
    ///         The two last-declared combinable members, so the value is deterministic and reviewable in a
    ///         diff. Declaration order is JetBrains', which puts the aggregates first in every enum here.
    ///     </para>
    ///     <para>
    ///         ⚠ <see cref="EveryLegalValue" /> still emits the all-members join and should: accepting it is
    ///         a real requirement of the parser, and that is a different question from whether it is worth
    ///         formatting at.
    ///     </para>
    /// </remarks>
    public static string? CombinationProbe(IReadOnlyList<string> members) {
        var combinable = members
            .Where(static member => !Aggregates.Contains(member, StringComparer.Ordinal))
            .ToArray();

        return combinable.Length < 2 ? null : string.Join(", ", combinable[^2..]);
    }

    public static IEnumerable<string> EveryLegalValue(OptionInfo info) {
        foreach (var value in BareLegalValues(info)) {
            yield return value;
            if (info.SeveritySuffix) {
                yield return value + ":warning";
            }
        }

        if (info.TabMeans is not null) {
            yield return "tab";
        }
    }

    static IEnumerable<string> BareLegalValues(OptionInfo info) {
        switch (info.Kind) {
            case OptionValueKind.Bool:
                // ⚠ ReSharper writes both spellings, and TrySet accepts both.
                yield return "true";
                yield return "false";
                yield return "always";
                yield return "never";
                break;

            case OptionValueKind.Enum:
                foreach (var value in OptionEnums.ValuesOf(info.EnumName!)) {
                    yield return value;
                }

                foreach (var alias in OptionEnums.AliasesOf(info.EnumName!)) {
                    yield return alias;
                }

                break;

            case OptionValueKind.Flags:
                var members = OptionEnums.ValuesOf(info.EnumName!);
                foreach (var value in members) {
                    yield return value;
                }

                foreach (var alias in OptionEnums.AliasesOf(info.EnumName!)) {
                    yield return alias;
                }

                yield return string.Join(", ", members);
                break;

            case OptionValueKind.Int:
                foreach (var value in new[] {
                             info.Min ?? -1_000, Clamp(info, Number(info.Default)), Clamp(info, 1),
                             info.Max ?? 1_000_000
                         }.Distinct()) {
                    yield return value.ToString(CultureInfo.InvariantCulture);
                }

                break;

            default:
                // Every string is legal, which is the claim `freeFormBecause` has to justify — so
                // the "domain" is anything at all, and the arbitrary values are the test.
                if (info.Default is { Length: > 0 } text) {
                    yield return text;
                }

                yield return NotAValue;
                yield return "x";
                break;
        }
    }

    /// <summary>
    ///     Values the option must refuse, and empty for a free-form string, which refuses nothing.
    /// </summary>
    /// <remarks>
    ///     ⚠ The out-of-bounds integers matter more than the unparseable one. <c>max_line_length = -1</c>
    ///     and <c>indent_size = 0</c> both parse; both were accepted and clamped away in silence, which
    ///     is SK9017's whole reason for existing.
    /// </remarks>
    public static IEnumerable<string> IllegalValues(OptionInfo info) {
        switch (info.Kind) {
            case OptionValueKind.Bool:
            case OptionValueKind.Enum:
            case OptionValueKind.Flags:
                yield return NotAValue;
                break;

            case OptionValueKind.Int:
                yield return NotAValue;
                if (info.Min is { } min) {
                    yield return (min - 1).ToString(CultureInfo.InvariantCulture);
                }

                if (info.Max is { } max) {
                    yield return (max + 1).ToString(CultureInfo.InvariantCulture);
                }

                break;
        }
    }

    static int Clamp(OptionInfo info, int value) =>
        Math.Min(info.Max ?? int.MaxValue, Math.Max(info.Min ?? int.MinValue, value));

    static int Number(string? text) =>
        int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number) ? number : 0;
}
