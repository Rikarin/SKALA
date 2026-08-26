namespace Rikarin.Skala.Formatting;

/// <summary>How a group resolved.</summary>
public enum ResolvedMode {
    Flat,
    Broken
}

/// <summary>
/// The fitting pass: resolve every group's mode against the width budget.
/// </summary>
/// <remarks>
/// Wadler-shaped and iterative rather than recursive (C# has no TCO and files nest 30 deep).
/// <para>
/// ⚠ It is driven by <see cref="LayoutWriter"/> rather than run ahead of it, and that is a
/// correction to docs/plan/04 § "The pipeline", which had fitting and emitting as separate steps.
/// Whether a group fits is <c>column + flatWidth &lt;= width</c>, and the column is a function of
/// the indentation stack, of pending spaces and of every break taken so far — that is, of exactly
/// the state the writer maintains. A standalone fitting pass has to reproduce that state, and two
/// implementations of an indentation model that must agree to the column is the kind of duplication
/// that produces a wrap that moves when nothing moved. Resolving on entry, at the column the writer
/// is actually at, has one model and no drift.
/// </para>
/// <para>
/// ⚠ The second pass docs/plan/04 describes is not a second traversal. A
/// <see cref="GroupMode.Owner"/> group's owner is always its syntactic ancestor — all five
/// <c>if_owner_is_single_line</c> keys in the export name a declaration as the owner of something
/// inside that declaration — so a depth-first walk already resolves owners before children. That
/// gives every property the second pass was there to give: owners first, children read the owner's
/// resolved mode, a child may only move Flat → Broken, and termination is a property of the walk
/// order rather than of a convergence argument. <see cref="OwnerUnresolved"/> counts the cases
/// where the invariant does not hold, so that a front end which breaks it is visible rather than
/// silently mis-laid.
/// </para>
/// </remarks>
public sealed class Fitter {
    /// <summary>A group containing a hard break can never be flat; this is its flat width.</summary>
    public const int Unbounded = Document.Unbounded;

    readonly Document _document;
    readonly ResolvedMode[] _modes;
    readonly bool[] _resolved;
    readonly int _width;

    public Fitter(Document document, int width) {
        _document = document;
        _modes = new ResolvedMode[Math.Max(1, document.GroupCount)];
        _resolved = new bool[Math.Max(1, document.GroupCount)];
        _width = width;
    }

    /// <summary>The mode table, indexed by group id.</summary>
    public ResolvedMode[] Modes => _modes;

    /// <summary>
    /// How many <see cref="GroupMode.Owner"/> groups were reached before their owner.
    /// </summary>
    /// <remarks>
    /// ⚠ Zero for every file the C# front end produces, and the formatting tests assert it. A
    /// non-zero count means a front end emitted an owner-dependent group outside its owner, where
    /// the only monotone answer is Broken and the layout is a guess.
    /// </remarks>
    public int OwnerUnresolved { get; private set; }

    /// <summary>Resolves the group at <paramref name="node"/>, which the walk has just entered.</summary>
    /// <param name="column">The column the group's first character will land on.</param>
    public ResolvedMode Enter(int node, int column) {
        ref var slot = ref _document.Nodes[node];
        var id = slot.Arg1;
        var facts = _document.FactsOf(id);
        var mode = Decide(
            (GroupMode)slot.Arg0,
            facts,
            column,
            _document.FlatWidthOf(node),
            facts.MeasuresHead ? _document.HeadWidthOf(node) : _document.FlatWidthOf(node));
        _modes[id] = mode;
        _resolved[id] = true;
        return mode;
    }

    /// <summary>The mode a group resolved to. Flat until the walk reaches it.</summary>
    public ResolvedMode ModeOf(int group) => _modes[group];

    /// <param name="flatWidth">The whole group laid on one line: the test for joining.</param>
    /// <param name="breakWidth">
    /// What the group adds to the current line before its first unavoidable break: the test for
    /// breaking. ⚠ The two are not interchangeable and the asymmetry is not an accident. Joining
    /// <c>M() =&gt;\n from x in y\n select x;</c> needs the whole body to fit, because the join puts
    /// all of it on one line; breaking after the <c>=&gt;</c> of
    /// <c>P =&gt; new Thing {\n … };</c> needs only the head, because the line was going to end at
    /// the brace whatever happens. Using the head width for both re-joins queries and lambdas whose
    /// first line fits and whose body does not, which costs 0.5 points of line fidelity.
    /// </param>
    ResolvedMode Decide(GroupMode mode, in GroupFacts facts, int column, int flatWidth, int breakWidth) {
        var owner = facts.Owner;
        switch (mode) {
            case GroupMode.Flat:
                return ResolvedMode.Flat;

            case GroupMode.Break:
                return ResolvedMode.Broken;

            case GroupMode.Auto:
                return Fits(column, breakWidth) ? ResolvedMode.Flat : ResolvedMode.Broken;

            case GroupMode.Owner:
                if (owner < 0 || !_resolved[owner]) {
                    // ⚠ Broken is the only monotone answer when the owner is unknown, and an owner
                    // that is unknown at this point is a front-end bug rather than a layout.
                    OwnerUnresolved++;
                    return ResolvedMode.Broken;
                }

                return _modes[owner] == ResolvedMode.Broken ? ResolvedMode.Broken : ResolvedMode.Flat;

            default:
                // ⚠ Preserve does not re-flow the author's breaks away by default. Whether it may
                // join one that fits, and whether it may add one that the author did not write, are
                // per-construct facts — see GroupFacts for why one rule is not enough.
                if (facts.SourceBroken) {
                    return facts.JoinsIfFits && Fits(column, flatWidth) ? ResolvedMode.Flat : ResolvedMode.Broken;
                }

                return facts.BreaksIfTooLong && !Fits(column, breakWidth) ? ResolvedMode.Broken : ResolvedMode.Flat;
        }
    }

    bool Fits(int column, int flatWidth) => flatWidth < Unbounded && column + flatWidth <= _width;
}
