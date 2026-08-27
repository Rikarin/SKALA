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
    readonly int _indentWidth;

    public Fitter(Document document, int width, int indentWidth = 4) {
        _indentWidth = Math.Max(1, indentWidth);
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
    /// <param name="continuationColumn">
    /// The column a line broken at one of this group's own points would start at. ⚠ Only the writer
    /// can answer that — it is a function of the indentation stack and of whether this group opened
    /// a continuation scope of its own — and the ordering rule cannot be stated without it.
    /// </param>
    /// <param name="trailing">
    /// What still has to be written on this line after the group ends, up to the next break. ⚠ A
    /// group is not the line it lands on; see <see cref="LayoutWriter"/>'s TrailingWidth.
    /// </param>
    public ResolvedMode Enter(int node, int column, int continuationColumn, int trailing) {
        ref var slot = ref _document.Nodes[node];
        var id = slot.Arg1;
        var facts = _document.FactsOf(id);
        var mode = Decide(
            (GroupMode)slot.Arg0,
            facts,
            new Measures(
                column,
                continuationColumn,
                _document.FlatWidthOf(node),
                facts.MeasuresHead ? _document.HeadWidthOf(node) : _document.FlatWidthOf(node),
                _document.PointWidthOf(node),
                _document.AfterPointOf(node),
                trailing
            )
        );
        _modes[id] = mode;
        _resolved[id] = true;
        return mode;
    }

    /// <summary>The six numbers a group is resolved against.</summary>
    /// <param name="Column">Where the group's first character lands.</param>
    /// <param name="ContinuationColumn">Where a line broken at one of its own points would start.</param>
    /// <param name="FlatWidth">The whole group on one line: the test for joining.</param>
    /// <param name="BreakWidth">
    /// What the group adds to the current line before its first unavoidable break: the test for
    /// breaking. ⚠ The two are not interchangeable and the asymmetry is not an accident. Joining
    /// <c>M() =&gt;\n from x in y\n select x;</c> needs the whole body to fit, because the join puts
    /// all of it on one line; breaking after the <c>=&gt;</c> of <c>P =&gt; new Thing {\n … };</c>
    /// needs only the head, because the line was going to end at the brace whatever happens. Using
    /// the head width for both re-joins queries and lambdas whose first line fits and whose body
    /// does not, which costs 0.5 points of line fidelity.
    /// </param>
    /// <param name="PointWidth">The width from the group's start to its own first break point.</param>
    /// <param name="AfterPoint">The width from that point to the next one.</param>
    readonly record struct Measures(
        int Column,
        int ContinuationColumn,
        int FlatWidth,
        int BreakWidth,
        int PointWidth,
        int AfterPoint,
        int Trailing);

    /// <summary>The mode a group resolved to. Flat until the walk reaches it.</summary>
    public ResolvedMode ModeOf(int group) => _modes[group];

    ResolvedMode Decide(GroupMode mode, in GroupFacts facts, in Measures m) {
        var owner = facts.Owner;
        switch (mode) {
            case GroupMode.Flat:
                return ResolvedMode.Flat;

            case GroupMode.Break:
                return ResolvedMode.Broken;

            case GroupMode.Auto:
                return Fits(m.Column, m.BreakWidth, m.Trailing) ? ResolvedMode.Flat : Worth(facts, m);

            case GroupMode.Owner:
                if (owner < 0 || !_resolved[owner]) {
                    // ⚠ Broken is the only monotone answer when the owner is unknown, and an owner
                    // that is unknown at this point is a front-end bug rather than a layout.
                    OwnerUnresolved++;
                    return ResolvedMode.Broken;
                }

                return _modes[owner] == ResolvedMode.Broken ? ResolvedMode.Broken : ResolvedMode.Flat;

            default:
                // ⚠ A chain's links break together even though each keeps its own group. The owner
                // holds no break points and answers only "does the whole chain fit on one line";
                // when it says no, every link breaks, which is what chop_if_long means for a
                // construct whose points are spread across nested nodes.
                if (facts.BreaksWithOwner && owner >= 0 && _resolved[owner] && _modes[owner] == ResolvedMode.Broken) {
                    return ResolvedMode.Broken;
                }

                // ⚠ Preserve does not re-flow the author's breaks away by default. Whether it may
                // join one that fits, and whether it may add one that the author did not write, are
                // per-construct facts — see GroupFacts for why one rule is not enough.
                if (facts.SourceBroken) {
                    return facts.JoinsIfFits && Fits(m.Column, m.FlatWidth, m.Trailing)
                        ? ResolvedMode.Flat
                        : ResolvedMode.Broken;
                }

                if (!facts.BreaksIfTooLong || Fits(m.Column, m.BreakWidth, m.Trailing)) {
                    return ResolvedMode.Flat;
                }

                return Worth(facts, m);
        }
    }

    /// <summary>
    /// The ordering rule: a group that does not fit still only breaks when its own break is the one
    /// worth taking.
    /// </summary>
    /// <remarks>
    /// ⚠ This is milestone 3's substance, and milestone 2 left it out on purpose: it measured that
    /// breaking at the first available point costs 0.24 points of line fidelity against leaving the
    /// line alone, because the oracle's break lands one line away (SK-DIV-0002).
    /// <para>
    /// Without the fact set, the answer is the old one — too long means broken — and every group
    /// that does not opt in behaves exactly as it did at M2. With it, two questions decide, in this
    /// order:
    /// </para>
    /// <list type="number">
    /// <item>
    /// <b>Does this break alone finish the job?</b> If what follows the group's own first break
    /// point fits on a continuation line, take it: two lines beat the three that wrapping something
    /// inside would cost. This is the case for
    /// <c>JsonObjectContract c =\n    (JsonObjectContract)r.ResolveContract(typeof(T));</c>, where
    /// chopping the argument list instead produces a third line the oracle does not write.
    /// </item>
    /// <item>
    /// <b>Otherwise, does the line end here anyway?</b> Something inside is going to wrap, so the
    /// current line runs to the first break point whether this group breaks or not. If that much
    /// fits, this group's break buys a line and gains nothing:
    /// <c>schema.Properties = new Dictionary&lt;…&gt; {</c> is the oracle's first line, not
    /// <c>schema.Properties =</c>. If it does <em>not</em> fit — the call's own name runs past the
    /// margin — then both breaks are needed and this one is taken.
    /// </item>
    /// </list>
    /// <para>
    /// ⚠ It is a local rule and not a search. docs/plan/04 § "The fitting algorithm" rules out
    /// optimal-layout algorithms because ReSharper is not optimal either; this reproduces the
    /// answer ReSharper gives on the shapes that occur, in one traversal and with no backtracking.
    /// </para>
    /// </remarks>
    ResolvedMode Worth(in GroupFacts facts, in Measures m) {
        if (!facts.PrefersOuterBreak) {
            return ResolvedMode.Broken;
        }

        // What lands on the continuation line if this group breaks and nothing inside it does.
        var tail = m.FlatWidth >= Unbounded ? Unbounded : m.FlatWidth - m.PointWidth + OuterBreakMargin(m);
        if (Fits(m.ContinuationColumn, tail, m.Trailing)) {
            return ResolvedMode.Broken;
        }

        // What lands on *this* line if the group stays flat and the construct inside wraps instead.
        var line = m.PointWidth >= Unbounded ? Unbounded : m.PointWidth + m.AfterPoint;
        return Fits(m.Column, line) ? ResolvedMode.Flat : ResolvedMode.Broken;
    }

    /// <summary>
    /// How much room the outer break has to leave before it is judged to have "finished the job".
    /// </summary>
    /// <remarks>
    /// ⚠ Measured, and it is not zero, which is the surprise: the oracle stops taking the <c>=</c>
    /// break well before the continuation line reaches 120, and the result it declines fits with
    /// room to spare. What ReSharper is really computing there is <em>not known</em>, and milestone
    /// 3.1 swept it hard enough to say so with evidence rather than with a shrug — eleven
    /// right-hand-side shapes, five block depths, both values of <c>wrap_before_eq</c>, one
    /// character at a time (<c>Testing/Rikarin.Skala.Testing/MarginSweep.cs</c>, run as
    /// <c>margin</c>). Three things came out of it, and all three contradict the milestone-3 note
    /// this replaces:
    /// <list type="number">
    /// <item>
    /// <b>The threshold does not depend on the nesting depth.</b> At a flat width of 121 the last
    /// continuation line the oracle still writes is 112 columns at block depth 2, 3, 4, 5 and 6
    /// alike. Milestone 3 read a <c>column / indent</c> term off three cells that were confounded
    /// with the shape's own width.
    /// </item>
    /// <item>
    /// <b>It does depend on the flat width, and not monotonically.</b> Same shape, same depth,
    /// sweeping the flat width: 122 → 113, 124 → 115, 126 to 140 → 116, then back down, 146 → 112,
    /// 158 → 107. No affine function of the numbers this fitter has reproduces that curve.
    /// </item>
    /// <item>
    /// <b>It depends on the shape.</b> At a flat width of 137 and depth 2 the threshold is 116 for
    /// <c>Convert.FromBase64String("…")</c>, 117 for a call on an identifier, 118 for a binary
    /// chain, 120 for a cast, and 107 for a member chain — and an object initializer and an array
    /// initializer go the other way, 107 and 101.
    /// </item>
    /// </list>
    /// <para>
    /// ⚠ So the constant stays, and it is now honestly a <em>fitted</em> constant rather than a
    /// derived one. Fitted against <c>corpus/real/</c>, with everything else in the ordering rule
    /// held fixed:
    /// </para>
    /// <code>
    /// margin                  line       file
    /// never take it          99.11 %    76.05 %
    /// 0                      99.02 %    72.37 %
    /// 4  (constant)          99.37 %    78.95 %
    /// 8  (constant)          99.42 %    80.00 %
    /// 8 + column/indent      99.51 %    82.11 %      ← milestone 3's
    /// 11 + column/indent     99.53 %    82.63 %      ← ships
    /// 12 + column/indent     99.52 %    81.84 %
    /// 16 + column/indent     99.51 %    81.84 %
    /// 24 + column/indent     99.48 %    80.79 %
    /// </code>
    /// <para>
    /// ⚠ And the two measurements disagree, which is the finding worth carrying forward. The
    /// isolated sweep says the threshold is depth-independent and near 112 — that is the
    /// <c>constant 8</c> row, and it is 0.08 points and six files <em>worse</em> on real code than a
    /// depth-dependent constant the sweep does not support. The margin is therefore absorbing error
    /// from the rest of the ordering rule rather than reproducing a rule of ReSharper's, and no
    /// value of it will close the last of this class. SK-DIV-0005 records that as the argument.
    /// </para>
    /// </remarks>
    int OuterBreakMargin(in Measures m) => 11 + m.ContinuationColumn / _indentWidth;

    bool Fits(int column, int flatWidth, int trailing = 0) =>
        flatWidth < Unbounded && trailing < Unbounded && column + flatWidth + trailing <= _width;
}
