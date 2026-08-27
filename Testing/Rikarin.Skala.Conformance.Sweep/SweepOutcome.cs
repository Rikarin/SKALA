using Rikarin.Skala.Options;

namespace Rikarin.Skala.Conformance.Sweep;

/// <summary>
///     What the sweep concluded about one option.
/// </summary>
/// <remarks>
///     ⚠ The outcome is not pass/fail and it is not two-way. Every previous measurement in this project
///     ran at one configuration, which measures the output and not the option: Skala reached 99.70 %
///     fidelity while respecting 205 of the 458 keys the export sets, because an unimplemented key whose
///     configured value happens to coincide with Skala's behaviour costs nothing. Flipping
///     <c>resharper_int_align</c> between <c>false</c> and <c>true</c> produced byte-identical output
///     and no test noticed.
///     <para>
///         ⚠ <see cref="Unexercised" /> is the verdict that makes the harness worth building, and reading it
///         as a pass rebuilds the exact defect the harness exists to detect. It says the fixture could not
///         tell the option's values apart — either the fixture is too weak or the option is inert — and it
///         is never green.
///     </para>
///     <para>
///         ⚠ <see cref="Inert" /> and <see cref="Spurious" /> are the one-sided cases the three-way table does
///         not name. They are divergences, and they are separated from <see cref="Divergent" /> because the
///         diagnosis differs: <see cref="Inert" /> is <c>resharper_int_align</c> — ReSharper honours the key
///         and Skala ignores it — which is precisely the defect a one-configuration measurement cannot see.
///     </para>
/// </remarks>
public enum SweepOutcome {
    /// <summary>Both engines distinguish the values, and every value's output agrees. ✅</summary>
    Conformant,

    /// <summary>Both engines distinguish the values, and at least one value's output disagrees.</summary>
    Divergent,

    /// <summary>ReSharper distinguishes the values and Skala does not. The <c>int_align</c> shape.</summary>
    Inert,

    /// <summary>Skala distinguishes the values and ReSharper does not.</summary>
    Spurious,

    /// <summary>⚠ Neither engine moved. Not a pass: the fixture does not exercise the option.</summary>
    Unexercised,

    /// <summary>The registry names no fixture, or names one that is not in the corpus.</summary>
    NoFixture
}

/// <summary>What both engines produced for one option at one value.</summary>
/// <param name="Value">The value assigned.</param>
/// <param name="OracleHash">A short digest of the oracle's output, so the table can be read as a diff.</param>
/// <param name="SkalaHash">The same for Skala's.</param>
/// <param name="Agree">Whether the two are byte-identical after line-ending normalisation.</param>
public sealed record SweepValue(string Value, string OracleHash, string SkalaHash, bool Agree);

/// <summary>The sweep's verdict on one option.</summary>
/// <param name="Key">The option's canonical spelling.</param>
/// <param name="Tier">The tier the registry claimed when the sweep ran.</param>
/// <param name="Kind">Bool, enum, int — an int verdict is weaker, see <see cref="SweepPlan.LegalValues" />.</param>
/// <param name="Fixture">The fixture the verdict was reached on.</param>
/// <param name="Outcome">The three-way result, with the one-sided cases named.</param>
/// <param name="Values">One row per value.</param>
/// <param name="OracleDistinct">How many distinct outputs the oracle produced across the values.</param>
/// <param name="SkalaDistinct">The same for Skala.</param>
/// <param name="BaselineAgrees">
///     Whether the two engines already agreed on this fixture under the base configuration, with nothing
///     overridden. ⚠ Without it a <see cref="Divergent" /> row cannot be read: a fixture the two disagree
///     on before the key is touched is a pre-existing divergence that this option inherited, and
///     blaming the option for it sends someone to the wrong code.
/// </param>
/// <param name="LineEndingOnly">
///     ⚠ Whether this verdict had to be read off the raw bytes because line-ending normalisation erased
///     the option's entire effect. Every other measurement in this repository compares normalised text,
///     because a committed fixture may have been generated on another OS — but
///     <c>resharper_enforce_line_ending_style</c> and <c>resharper_csharp_insert_final_newline</c>
///     change nothing else, so a normalised comparison reports them <see cref="SweepOutcome.Unexercised" />
///     for a reason that is about the instrument and not about the option. Both sides of this comparison
///     are produced in one run on one machine, so falling back to raw bytes is safe here in a way it is
///     not for a committed fixture.
/// </param>
/// <param name="Cost">Amortised oracle wall-clock attributable to this option.</param>
public sealed record OptionSweep(
    string Key,
    OptionTier Tier,
    OptionValueKind Kind,
    string Fixture,
    SweepOutcome Outcome,
    IReadOnlyList<SweepValue> Values,
    int OracleDistinct,
    int SkalaDistinct,
    bool BaselineAgrees,
    bool LineEndingOnly,
    TimeSpan Cost) {
    public int Agreements => Values.Count(static value => value.Agree);

    /// <summary>Whether this option's row is evidence that the option is honoured. Only one is.</summary>
    public bool IsGreen => Outcome == SweepOutcome.Conformant;

    public static SweepOutcome Classify(int oracleDistinct, int skalaDistinct, int agreements, int values) =>
        (oracleDistinct > 1, skalaDistinct > 1) switch {
            (false, false) => SweepOutcome.Unexercised,
            (true, false) => SweepOutcome.Inert,
            (false, true) => SweepOutcome.Spurious,
            _ => agreements == values ? SweepOutcome.Conformant : SweepOutcome.Divergent
        };
}
