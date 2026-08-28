namespace Rikarin.Skala.Conformance.Sweep;

/// <summary>
///     What the pairwise sweep concluded about one pair of options.
/// </summary>
/// <remarks>
///     ⚠ <see cref="InteractionOnly" /> is the verdict this whole pass exists to produce, and it is the
///     one that cannot be reached by <c>KeyFlipSweep</c> at any effort. That sweep measures each key on
///     <em>that key's own fixture</em> with every other key at the export's value, so on the primary's
///     fixture it visits one line of the grid: the primary's values against the secondary's export
///     value. A pair that agrees along that line and disagrees off it is reported <c>CONFORMANT</c> by
///     an instrument working perfectly — see docs/plan/12 § "Interactions".
/// </remarks>
public enum PairOutcome {
    /// <summary>Every corner agrees. ✅</summary>
    Conformant,

    /// <summary>
    ///     ⚠ Every corner the single sweep can reach agrees, and an interior corner does not.
    /// </summary>
    InteractionOnly,

    /// <summary>A corner disagrees, and the single sweep could have seen it too.</summary>
    Divergent,

    /// <summary>⚠ Neither engine distinguished the corners. Not a pass: the fixture is too weak.</summary>
    Unexercised,

    /// <summary>
    ///     ⚠ The two engines already disagreed on this fixture before either key was set.
    /// </summary>
    /// <remarks>
    ///     ⚠ The grid cannot answer anything about the pair, and calling it <see cref="Divergent" />
    ///     blames the pair for a divergence it inherited. <c>KeyFlipSweep</c> meets the same situation and
    ///     handles it more weakly — it records <c>BaselineAgrees</c> and writes the caveat into the
    ///     reason text beside a <c>DIVERGENT</c> verdict. That is tolerable there, where the row is one of
    ///     283 and a reader is looking down a column. It is not tolerable here, where the entire product
    ///     of the pass is a handful of interior findings and 43 inherited divergences would bury them.
    /// </remarks>
    BaselineDivergent,

    /// <summary>
    ///     ⚠ Every disagreement is one that a key of the pair already owns on its own.
    /// </summary>
    /// <remarks>
    ///     ⚠ <b>The verdict that stops this pass inventing findings.</b> The corrected first run reported
    ///     17 <see cref="InteractionOnly" /> rows across the <c>wrap_*</c> family, and every one of them
    ///     disagreed only where <c>max_line_length</c> was <c>0</c> or <c>1</c> — the two values at which
    ///     <c>conformance-sweep.json</c> records <c>max_line_length</c> disagreeing <em>measured alone, on
    ///     its own fixture</em>. Seventeen findings with one cause, none of it about a pair, and each
    ///     would have been sent to somebody to investigate as a subtle two-key defect.
    ///     <para>
    ///         The rule: a disagreeing corner is evidence about the <em>pair</em> only when both keys are
    ///         recorded as agreeing at the values that corner assigns them. Where the single sweep never
    ///         measured a value, nothing is excused — see <c>SweepArchive.ReadAgreement</c>.
    ///     </para>
    /// </remarks>
    Inherited,

    /// <summary>The pair names no fixture the corpus has.</summary>
    NoFixture
}

/// <summary>What both engines produced at one corner of a two-key grid.</summary>
/// <param name="PrimaryValue">The per-construct key's value.</param>
/// <param name="SecondaryValue">The global key's value.</param>
/// <param name="OracleHash">A short digest of the oracle's output.</param>
/// <param name="SkalaHash">The same for Skala's.</param>
/// <param name="Agree">Whether the two are byte-identical after line-ending normalisation.</param>
/// <param name="AttributableToOneKey">
///     ⚠ Whether the committed single sweep already records one of the two keys disagreeing at the value
///     this corner assigns it. Such a corner says nothing about the pair. Recorded per corner rather than
///     derived later, so the committed table shows which corners were excused and why.
/// </param>
/// <param name="ReachedBySingleSweep">
///     ⚠ Whether <c>KeyFlipSweep</c> visits this corner <em>on this fixture</em>. See
///     <c>PairwiseSweep.ReachedBySingleSweep</c>: it depends on the secondary alone, because the single
///     sweep measures each key on that key's own fixture. This flag is what separates
///     <see cref="PairOutcome.InteractionOnly" /> from an ordinary divergence, and it is therefore
///     recorded per corner rather than derived later.
/// </param>
public sealed record PairCorner(
    string PrimaryValue,
    string SecondaryValue,
    string OracleHash,
    string SkalaHash,
    bool Agree,
    bool ReachedBySingleSweep,
    bool AttributableToOneKey = false);

/// <summary>The pairwise sweep's verdict on one pair.</summary>
public sealed record PairSweep(
    string PrimaryKey,
    string SecondaryKey,
    string Fixture,
    PairOutcome Outcome,
    IReadOnlyList<PairCorner> Corners,
    int OracleDistinct,
    int SkalaDistinct,
    bool BaselineAgrees,
    TimeSpan Cost) {
    public int Agreements => Corners.Count(static corner => corner.Agree);

    /// <summary>Whether this row is evidence the pair is honoured together. Only one verdict is.</summary>
    public bool IsGreen => Outcome == PairOutcome.Conformant;

    /// <summary>
    ///     The three-way classification, from the counts and from which corners disagreed.
    /// </summary>
    /// <remarks>
    ///     ⚠ The <see cref="PairOutcome.Unexercised" /> guard comes first and is not a pass, for exactly
    ///     the reason it is not a pass in <c>OptionSweep.Classify</c>: a grid whose corners are all the
    ///     same output is a grid that measured nothing, and every corner "agrees" in it.
    /// </remarks>
    public static PairOutcome Classify(
        int oracleDistinct,
        int skalaDistinct,
        IReadOnlyList<PairCorner> corners,
        bool baselineAgrees
    ) {
        // ⚠ Before anything else. A fixture the two engines already disagree on cannot report on a
        // pair, and every other verdict below would be a statement about the pair.
        if (!baselineAgrees) {
            return PairOutcome.BaselineDivergent;
        }

        if (oracleDistinct <= 1 && skalaDistinct <= 1) {
            return PairOutcome.Unexercised;
        }

        var disagreeing = corners.Where(static corner => !corner.Agree).ToArray();
        if (disagreeing.Length == 0) {
            return PairOutcome.Conformant;
        }

        // ⚠ Before the other two. A corner where one of the keys is already known to diverge at the
        // value it was given is not evidence about the pair, whether or not the single sweep visited
        // that corner — and 17 of this pass's first findings were exactly that.
        if (disagreeing.All(static corner => corner.AttributableToOneKey)) {
            return PairOutcome.Inherited;
        }

        // ⚠ Divergent when the single sweep could have caught it, InteractionOnly when it could not.
        // The distinction is the whole product of this pass: the second says "two keys that are each
        // conformant alone are wrong together", which is a different defect and a different fix.
        return disagreeing.Any(static corner => corner.ReachedBySingleSweep)
            ? PairOutcome.Divergent
            : PairOutcome.InteractionOnly;
    }
}
