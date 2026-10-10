namespace Rikarin.Skala.Formatting;

/// <summary>How a group resolved.</summary>
public enum ResolvedMode {
    Flat,
    Broken
}

/// <summary>
///     The fitting pass: resolve every group's mode against the width budget.
/// </summary>
/// <remarks>
///     Wadler-shaped and iterative rather than recursive (C# has no TCO and files nest 30 deep).
///     <para>
///         ⚠ It is driven by <see cref="LayoutWriter" /> rather than run ahead of it, and that is a
///         correction to docs/plan/04 § "The pipeline", which had fitting and emitting as separate steps.
///         Whether a group fits is <c>column + flatWidth &lt;= width</c>, and the column is a function of
///         the indentation stack, of pending spaces and of every break taken so far — that is, of exactly
///         the state the writer maintains. A standalone fitting pass has to reproduce that state, and two
///         implementations of an indentation model that must agree to the column is the kind of duplication
///         that produces a wrap that moves when nothing moved. Resolving on entry, at the column the writer
///         is actually at, has one model and no drift.
///     </para>
///     <para>
///         ⚠ The second pass docs/plan/04 describes is not a second traversal. A
///         <see cref="GroupMode.Owner" /> group's owner is always its syntactic ancestor — all five
///         <c>if_owner_is_single_line</c> keys in the export name a declaration as the owner of something
///         inside that declaration — so a depth-first walk already resolves owners before children. That
///         gives every property the second pass was there to give: owners first, children read the owner's
///         resolved mode, a child may only move Flat → Broken, and termination is a property of the walk
///         order rather than of a convergence argument. <see cref="OwnerUnresolved" /> counts the cases
///         where the invariant does not hold, so that a front end which breaks it is visible rather than
///         silently mis-laid.
///     </para>
/// </remarks>
public sealed class Fitter {
    /// <summary>A group containing a hard break can never be flat; this is its flat width.</summary>
    public const int Unbounded = Document.Unbounded;

    readonly Document document;
    readonly ResolvedMode[] modes;
    readonly bool[] resolved;

    /// <summary>
    ///     The output line each group was entered on, for
    ///     <see cref="GroupFacts.BreaksIfOwnerIsMultiLine" />.
    /// </summary>
    readonly int[] enteredOn;

    /// <summary>
    ///     The column each group was entered at, for the head marker a
    ///     <see cref="GroupFacts.MinimumHead" /> group measures its head from.
    /// </summary>
    readonly int[] enteredAt;

    readonly int width;
    readonly int indentWidth;

    /// <summary>
    ///     The groups resolved since the outermost open <see cref="Mark" />, in order, so that a
    ///     speculative walk can be undone group by group rather than by copying three arrays.
    /// </summary>
    readonly List<int> journal = [];

    /// <summary>How many marks are open; the journal is kept only while one is.</summary>
    int marks;

    /// <summary>
    ///     The modes <see cref="Force" /> committed groups to, whatever <see cref="Decide" /> would say; null
    ///     for a group decided the ordinary way.
    /// </summary>
    readonly ResolvedMode?[] forced;

    /// <summary>The groups forced since the outermost open <see cref="Mark" />, for <see cref="Rollback" />.</summary>
    readonly List<int> forcedJournal = [];

    public Fitter(Document document, int width, int indentWidth = 4) {
        this.indentWidth = Math.Max(1, indentWidth);
        this.document = document;
        modes = new ResolvedMode[Math.Max(1, document.GroupCount)];
        resolved = new bool[Math.Max(1, document.GroupCount)];
        enteredOn = new int[Math.Max(1, document.GroupCount)];
        enteredAt = new int[Math.Max(1, document.GroupCount)];
        forced = new ResolvedMode?[Math.Max(1, document.GroupCount)];
        this.width = width;
    }

    /// <summary>The mode table, indexed by group id.</summary>
    public ResolvedMode[] Modes => modes;

    /// <summary>
    ///     How many <see cref="GroupMode.Owner" /> groups were reached before their owner.
    /// </summary>
    /// <remarks>
    ///     ⚠ Zero for every file the C# front end produces, and the formatting tests assert it. A
    ///     non-zero count means a front end emitted an owner-dependent group outside its owner, where
    ///     the only monotone answer is Broken and the layout is a guess.
    /// </remarks>
    public int OwnerUnresolved { get; private set; }

    /// <summary>Resolves the group at <paramref name="node" />, which the walk has just entered.</summary>
    /// <param name="column">The column the group's first character will land on.</param>
    /// <param name="continuationColumn">
    ///     The column a line broken at one of this group's own points would start at. ⚠ Only the writer
    ///     can answer that — it is a function of the indentation stack and of whether this group opened
    ///     a continuation scope of its own — and the ordering rule cannot be stated without it.
    /// </param>
    /// <param name="trailing">
    ///     What still has to be written on this line after the group ends, up to the next break. ⚠ A
    ///     group is not the line it lands on; see <see cref="LayoutWriter" />'s TrailingWidth.
    /// </param>
    /// <param name="line">
    ///     The output line the group is entered on. ⚠ Recorded for every group and read for the owner
    ///     of a <see cref="GroupFacts.BreaksIfOwnerIsMultiLine" /> group: "the owner is not single-line"
    ///     is a fact about the lines the writer has actually written since the owner began, and the
    ///     writer is the only thing that knows it.
    /// </param>
    /// <param name="lineStart">
    ///     The column the current output line's first character landed on. ⚠ For
    ///     <see cref="GroupFacts.MinimumHead" />: a head that already spans lines is measured from the
    ///     line the group is on, not from its owner's first token — the oracle glues
    ///     <c>Dictionary&lt;…,</c> / <c>int&gt; d = [</c> (an eight-column second line) and breaks
    ///     <c>int&gt; dddddd =</c> (thirteen), the same floor counted from the line (#379).
    /// </param>
    public ResolvedMode Enter(int node, int column, int continuationColumn, int trailing, int line, int lineStart) {
        ref var slot = ref document.Nodes[node];
        var id = slot.Arg1;
        var facts = document.FactsOf(id);
        var mode = forced[id]
            ?? Decide(
                (GroupMode)slot.Arg0,
                facts,
                new(
                    column,
                    continuationColumn,
                    document.FlatWidthOf(node),
                    facts.MeasuresThroughTail ? document.ThroughWidthOf(node)
                    : facts.MeasuresHead ? document.HeadWidthOf(node)
                    : document.FlatWidthOf(node),
                    document.PointWidthOf(node),
                    document.AfterPointOf(node),
                    facts.MeasuresThroughTail ? 0 : trailing,
                    line,
                    document.YieldEndOf(node)
                ),
                document.AfterPointRunsToTheEnd(node),
                document.SegmentOf(node),
                lineStart,
                document.FirstPointFlatWidthOf(node)
            );
        modes[id] = mode;
        resolved[id] = true;
        enteredOn[id] = line;
        enteredAt[id] = column;
        if (marks > 0) {
            journal.Add(id);
        }

        return mode;
    }

    /// <summary>A point to roll the fitter back to: see <see cref="MarkForRollback" />.</summary>
    public readonly record struct Mark(int Journal, int OwnerUnresolved, int Forced);

    /// <summary>
    ///     Starts recording the groups resolved from here on, so that <see cref="Rollback" /> can forget
    ///     them again.
    /// </summary>
    /// <remarks>
    ///     ⚠ For <see cref="LayoutWriter" />'s speculative line only. A group is entered once by the
    ///     walk, so the groups a speculation resolves are exactly the ones it entered and none of them
    ///     was resolved before the mark; forgetting them leaves the fitter as it was. Marks nest, and
    ///     the journal is dropped when the last one closes so that the ordinary walk pays nothing.
    /// </remarks>
    public Mark MarkForRollback() {
        marks++;
        return new(journal.Count, OwnerUnresolved, forcedJournal.Count);
    }

    /// <summary>Forgets every group resolved since <paramref name="mark" />.</summary>
    public void Rollback(Mark mark) {
        for (var i = journal.Count - 1; i >= mark.Journal; i--) {
            var id = journal[i];
            modes[id] = ResolvedMode.Flat;
            resolved[id] = false;
            enteredOn[id] = 0;
            enteredAt[id] = 0;
        }

        journal.RemoveRange(mark.Journal, journal.Count - mark.Journal);
        for (var i = forcedJournal.Count - 1; i >= mark.Forced; i--) {
            forced[forcedJournal[i]] = null;
        }

        forcedJournal.RemoveRange(mark.Forced, forcedJournal.Count - mark.Forced);
        OwnerUnresolved = mark.OwnerUnresolved;
        marks--;
    }

    /// <summary>The six numbers a group is resolved against.</summary>
    /// <param name="Column">Where the group's first character lands.</param>
    /// <param name="ContinuationColumn">Where a line broken at one of its own points would start.</param>
    /// <param name="FlatWidth">The whole group on one line: the test for joining.</param>
    /// <param name="BreakWidth">
    ///     What the group adds to the current line before its first unavoidable break: the test for
    ///     breaking. ⚠ The two are not interchangeable and the asymmetry is not an accident. Joining
    ///     <c>M() =&gt;\n from x in y\n select x;</c> needs the whole body to fit, because the join puts
    ///     all of it on one line; breaking after the <c>=&gt;</c> of <c>P =&gt; new Thing {\n … };</c>
    ///     needs only the head, because the line was going to end at the brace whatever happens. Using
    ///     the head width for both re-joins queries and lambdas whose first line fits and whose body
    ///     does not, which costs 0.5 points of line fidelity.
    /// </param>
    /// <param name="PointWidth">The width from the group's start to its own first break point.</param>
    /// <param name="AfterPoint">The width from that point to the next one.</param>
    /// <param name="Line">The output line the group is entered on.</param>
    readonly record struct Measures(
        int Column,
        int ContinuationColumn,
        int FlatWidth,
        int BreakWidth,
        int PointWidth,
        int AfterPoint,
        int Trailing,
        int Line,
        int YieldEnd = 0);

    /// <summary>
    ///     Commits a group the walk has not entered yet to a mode: the writer wrote ahead, saw what it took,
    ///     and lays out what precedes it on that answer, which a later layout must not take back. See
    ///     <see cref="GroupFacts.LiftsIfArrowBreaks" />.
    /// </summary>
    public void Force(int group, ResolvedMode mode) {
        if (forced[group] is not null) {
            return;
        }

        forced[group] = mode;
        if (marks > 0) {
            forcedJournal.Add(group);
        }
    }

    /// <summary>The mode <see cref="Force" /> committed a group to, or null.</summary>
    public ResolvedMode? ForcedOf(int group) => forced[group];

    /// <summary>The mode a group resolved to. Flat until the walk reaches it.</summary>
    public ResolvedMode ModeOf(int group) => modes[group];

    /// <summary>The output line a resolved group was entered on.</summary>
    public int EnteredOn(int group) => enteredOn[group];

    /// <param name="afterPointRunsToTheEnd">
    ///     Whether nothing after the group's own first break point can end a line — no point that is
    ///     not a last-resort one, no required break — so the group's trailing text lands on the line
    ///     the group is on. See <see cref="GroupFlags.AfterPointRunsToTheEnd" />.
    /// </param>
    /// <param name="tail">
    ///     The flat width past the group's own first point — <see cref="Document.SegmentOf" /> on the
    ///     group — for <see cref="GroupFacts.BreaksOnlyIfTailFits" />. ⚠ Not <c>FlatWidth − PointWidth</c>:
    ///     that difference counts the point's own flat space, and the oracle's boundary is exact — a
    ///     kept <c>=\n[…];</c> whose continuation line is 120 columns stays, and 121 gives the break
    ///     to the bracket.
    /// </param>
    /// <param name="lineStart">
    ///     The column the current line's first character landed on; see <see cref="Enter" />.
    /// </param>
    /// <param name="pointSpace">
    ///     What the group's first point renders as when flat; see <see cref="GroupFacts.TailEndsAt" />.
    /// </param>
    ResolvedMode Decide(
        GroupMode mode,
        in GroupFacts facts,
        in Measures m,
        bool afterPointRunsToTheEnd,
        int tail,
        int lineStart,
        int pointSpace
    ) {
        var owner = facts.Owner;
        switch (mode) {
            case GroupMode.Flat:
                return ResolvedMode.Flat;

            case GroupMode.Break:
                return ResolvedMode.Broken;

            case GroupMode.Auto:
                return Fits(m.Column, m.BreakWidth, m.Trailing)
                    ? ResolvedMode.Flat
                    : Worth(facts, m, afterPointRunsToTheEnd, tail, pointSpace);

            case GroupMode.Owner:
                if (owner < 0 || !resolved[owner]) {
                    // ⚠ Broken is the only monotone answer when the owner is unknown, and an owner
                    // that is unknown at this point is a front-end bug rather than a layout.
                    OwnerUnresolved++;
                    return ResolvedMode.Broken;
                }

                return modes[owner] == ResolvedMode.Broken ? ResolvedMode.Broken : ResolvedMode.Flat;

            default:
                // ⚠ A chain's links break together even though each keeps its own group. The owner
                // holds no break points and answers only "does the whole chain fit on one line";
                // when it says no, every link breaks, which is what chop_if_long means for a
                // construct whose points are spread across nested nodes.
                if (facts.BreaksWithOwner && owner >= 0 && resolved[owner] && modes[owner] == ResolvedMode.Broken) {
                    return ResolvedMode.Broken;
                }

                // ⚠ `if_owner_is_single_line`, answered by the output: the owner's marker was entered on
                // an earlier line than this group, so a break before this group was taken — by a chop,
                // by a fill, by a kept break, it does not matter which — and the owner spans lines. The
                // source cannot answer this (a fill's break is not in it until pass two) and neither
                // can any one list's mode (a `where` moved down is no list's decision). See
                // GroupFacts.BreaksIfOwnerIsMultiLine (#372).
                if (facts.BreaksIfOwnerIsMultiLine && owner >= 0 && resolved[owner] && enteredOn[owner] != m.Line) {
                    return ResolvedMode.Broken;
                }

                // ⚠ And a link whose operand holds something certain to break breaks on its own:
                // `a\n&& b || c` chops at the `||` because the `||` contains the broken `&&`, and
                // `F(\na\n&& b)\n|| c` for the same reason — the containment SK-DIV-0109 gives every
                // group, applied to a link that otherwise breaks only with its owner. The owner is
                // measured without its links' own breaks (DocumentBuilder.ownerWidth), so this is the
                // only way the author's break at an inner operator reaches the outer one.
                if (facts.ChainLink && m.FlatWidth >= Unbounded) {
                    return ResolvedMode.Broken;
                }

                // ⚠ A long parameter's one attribute section: by the measured rule, and before the author's
                // break, which is the rule's own answer on pass two (#476). See GroupFacts.ParameterAfterSection.
                if (facts.ParameterAfterSection > 0
                    && ChopsBeforeTheParameter(facts, m, lineStart) is { } sectionMode) {
                    return sectionMode;
                }

                if (facts.SourceBroken) {
                    return KeepOrJoin(facts, m, tail);
                }

                // ⚠ A local's `=` before an `is` over a positional pattern: broken whenever the line overflows
                // (#559). See GroupFacts.BreaksIfTheLineOverflows.
                // ⚠ Not over a pattern the author broke inside: `var q = o is P(1, 2` / `);` keeps its `=`.
                if (facts.BreaksIfTheLineOverflows) {
                    return m.FlatWidth >= Unbounded
                        || m.Trailing >= Unbounded
                        || Fits(m.Column, m.FlatWidth, m.Trailing)
                            ? ResolvedMode.Flat
                            : ResolvedMode.Broken;
                }

                // ⚠ A field's modifiers and its generic type: the type fills on the modifiers' line by the measured
                // rule rather than moving below them. See GroupFacts.ModifierFillHead (#540).
                if (facts.ModifierFillName > 0 && FillsAfterTheModifiers(facts, lineStart)) {
                    return ResolvedMode.Flat;
                }

                // ⚠ A returned type test's operand: the dot takes the break by the measured rule, and the keyword's
                // band in front of it, entered first, stays flat when it does. See GroupFacts.TypeTestTail (#446).
                if (facts.TypeTestTail > 0) {
                    var dot = m.FlatWidth < Unbounded && TheDotTakesTheBreak(facts, m.Column, lineStart);
                    if (facts.KeywordWidth == 0) {
                        return dot ? ResolvedMode.Broken : ResolvedMode.Flat;
                    }

                    if (dot) {
                        return ResolvedMode.Flat;
                    }
                }

                // ⚠ An `=` before a plain member access yields to its dot fill (#482) — unless the receiver
                // itself does not fit beside the `=`, where no dot can take the break and the oracle breaks
                // the `=`: `T v =` / `context.First;`. Pass one kept `T v = context` past the margin and
                // broke at the dot; pass two read that break as the author's and broke the `=` (Nightly
                // `fuzz --seed=909`, case 9552816164132777654). See GroupFacts.MemberHeadWidth.
                if (facts.MemberHeadWidth > 0
                    && m.PointWidth < Unbounded
                    && !Fits(m.Column, m.PointWidth + 1 + facts.MemberHeadWidth)) {
                    return ResolvedMode.Broken;
                }

                // ⚠ And it breaks whenever the fill would leave too short a fragment beside it for the name it
                // assigns, the value then fitting below by the measured limit (#590). See
                // EqualsFloor.BreaksBeforeTheValue.
                // ⚠ Only on a line the `=` group can measure whole: an author's break before the `=` makes the flat
                // width unbounded, and reading that as "the line overflows" broke `Value` / `= property.Value;` after
                // its `=` on pass two (#608, Nightly seed 9342835643250235022).
                if (facts is { MemberHeadWidth: > 0, EqualsName: > 0 }
                    && m.PointWidth < Unbounded
                    && m.FlatWidth < Unbounded
                    && tail < Unbounded
                    && m.Trailing < Unbounded
                    && !Fits(m.Column, m.FlatWidth, m.Trailing)) {
                    return BreaksBeforeAPlainMember(facts, m, tail) ? ResolvedMode.Broken : ResolvedMode.Flat;
                }

                // ⚠ A lambda-valued local's type/name gap breaks when the line through its `=` overflows: pass
                // one chopped the parameter list behind an `=` past the margin, and pass two, reading the chop
                // as the author's, filled the type (Nightly replay 13830403873739157460). See
                // GroupFacts.NameThroughEquals.
                if (facts.NameThroughEquals > 0 && !Fits(m.Column, 1 + facts.NameThroughEquals)) {
                    return ResolvedMode.Broken;
                }

                if (facts is { NameThroughEquals: > 0, OneOverValue: 0 }) {
                    return ResolvedMode.Flat;
                }

                // ⚠ A local's type/name gap one column past the margin (#583, SK-DIV-0127): it breaks where
                // the `=` would, for the names the planner has already let through. See GroupFacts.OneOverValue.
                if (facts.OneOverValue > 0) {
                    if (m.FlatWidth < Unbounded
                        && m.Trailing < Unbounded
                        && m.Column + m.FlatWidth + m.Trailing == width + 1
                        && (facts.OneOverEquals == -1
                            || facts.OneOverEquals >= 0 && LambdaGivesWayOneOver(facts, m)
                        )) {
                        return ResolvedMode.Broken;
                    }

                    if (facts.OneOverEquals != -1) {
                        return ResolvedMode.Flat;
                    }
                }

                // ⚠ The two sides of a switch arm's `=>` are alternatives: once the arrow itself has
                // moved down, the body follows it on the arrow's line and never takes a line of its
                // own, however wide (issue #378). Read after the kept break, so that a break the
                // author wrote after the arrow survives. See GroupFacts.FlatIfOwnerBroke.
                if (facts.FlatIfOwnerBroke && owner >= 0 && resolved[owner] && modes[owner] == ResolvedMode.Broken) {
                    return ResolvedMode.Flat;
                }

                // ⚠ A wide-named local's `=` before a lambda over a call (#453 round 2): broken exactly by the
                // measured reach and floor, read from the planner's widths so that a kept break inside the lambda
                // on pass two does not change the answer. See GroupFacts.EqualsLambdaArguments.
                if (facts.EqualsLambdaArguments > 0 && m.PointWidth < Unbounded) {
                    var paren = m.Column + 1 + facts.EqualsLambdaValueHead + 1;
                    if (paren + facts.EqualsLambdaArguments <= width) {
                        return ResolvedMode.Flat;
                    }

                    return EqualsFloor.BreaksBeforeALambdaCall(
                        facts.EqualsLambdaName,
                        facts.EqualsLambdaType,
                        m.Column,
                        paren,
                        facts.EqualsLambdaArguments
                    )
                        ? ResolvedMode.Broken
                        : ResolvedMode.Flat;
                }

                // ⚠ An `=` before a lambda with a bare name for a body yields to the arrow while the line
                // through `=>` fits. See GroupFacts.YieldsThroughArrow (#453).
                if (facts.YieldsThroughArrow > 0
                    && m.PointWidth < Unbounded
                    && m.FlatWidth < Unbounded
                    && !Fits(m.Column, m.FlatWidth, m.Trailing)) {
                    var decided = EqualsBeforeALambda(
                        facts,
                        m.Column + m.PointWidth,
                        m.FlatWidth - m.PointWidth - 1 + m.Trailing
                    );
                    if (decided is { } lambdaMode) {
                        return lambdaMode;
                    }
                }

                // ⚠ An `=` before `operand is A or B`: a measured table (#446, SK-DIV-0211).
                // ⚠ A line comment in the pattern ends the line, so what follows the value does not land on it.
                if (facts.PatternHead > 0
                    && facts.BreaksIfTooLong
                    && !Fits(m.Column, m.BreakWidth, m.FlatWidth < Unbounded ? m.Trailing : 0)) {
                    // ⚠ The line through the first alternative past the margin breaks the `=`, however wide the
                    // pattern (Nightly `fuzz --seed=7777`, case 16865623964709448456). The table was measured
                    // with short operands; behind a long one it kept `T v = operand is` and broke after the `is`
                    // with the `or`s a level past `A`, and pass two, reading that break as the author's, put them
                    // back on `A`'s column. Measured 2026-10-09 with `Testing ask`: a typed and a `var` local,
                    // operands of 40 to 110 columns, first alternatives of 5, 15 and 30, two to seven
                    // alternatives — every row whose first alternative ends at 121 or further breaks the `=`.
                    if (facts.PatternFirstWidth > 0
                        && m.PointWidth < Unbounded
                        && !Fits(m.Column, m.PointWidth + 1 + facts.PatternFirstWidth)) {
                        return ResolvedMode.Broken;
                    }

                    var end = m.FlatWidth >= Unbounded || m.Trailing >= Unbounded
                        ? int.MaxValue
                        : m.Column + m.FlatWidth + m.Trailing;
                    return EqualsFloor.BreaksBeforeAPattern(facts.PatternHead, facts.PatternWidth, end)
                        ? ResolvedMode.Broken
                        : ResolvedMode.Flat;
                }

                // ⚠ A lambda's parameter list: broken only when the line through its `=>` overflows.
                // See GroupFacts.ThroughWidth (#453).
                if (facts.ThroughWidth > 0) {
                    // ⚠ Except a measured local's, one column past the margin (#572): the `=` ends a space
                    // before the `(`. See EqualsFloor.ChopsOneOver.
                    if (facts.OneOverType > 0
                        && m.Column + m.FlatWidth + facts.ThroughWidth + 1 + facts.OneOverBody + 1 == width + 1
                        && EqualsFloor.ChopsOneOver(facts.OneOverType, facts.OneOverBody, m.Column - 1)) {
                        return ResolvedMode.Broken;
                    }

                    return Fits(m.Column, m.FlatWidth + facts.ThroughWidth) ? ResolvedMode.Flat : ResolvedMode.Broken;
                }

                // ⚠ An `=` before a call: a measured floor on the argument list, at the call's `(`. See
                // GroupFacts.CalleeWidth and EqualsFloor (#446).
                if (facts.CalleeWidth > 0
                    && facts.BreaksIfTooLong
                    && !Fits(m.Column, m.BreakWidth, m.Trailing)
                    && (facts.CalleeOwner != EqualsOwner.Field || m.ContinuationColumn - indentWidth == 4)) {
                    return EqualsBeforeACall(facts, m, lineStart);
                }

                // ⚠ Broken exactly when the keyword is what overflows. See GroupFacts.KeywordWidth.
                if (facts.KeywordWidth > 0) {
                    if (m.FlatWidth >= Unbounded || tail >= Unbounded) {
                        return ResolvedMode.Flat;
                    }

                    var operand = m.FlatWidth - tail - 1;
                    return Fits(m.Column, operand) && !Fits(m.Column, operand + 1 + facts.KeywordWidth)
                        ? ResolvedMode.Broken
                        : ResolvedMode.Flat;
                }

                // ⚠ See GroupFacts.FlatIfHeadOverflows: the head's own point takes the break.
                if (facts.FlatIfHeadOverflows && !Fits(m.Column, m.PointWidth)) {
                    return ResolvedMode.Flat;
                }

                // ⚠ See GroupFacts.BreaksIfItOverflows: a receiver's own dots (#582).
                if (facts.BreaksIfItOverflows) {
                    return Fits(m.Column, m.FlatWidth) ? ResolvedMode.Flat : ResolvedMode.Broken;
                }

                // ⚠ Broken exactly when the receiver fits, the receiver with its call does not, and the
                // call fits on the line below. See GroupFacts.HeldCall.
                if (facts.HeldCall > 0) {
                    if (m.FlatWidth >= Unbounded || tail >= Unbounded) {
                        return ResolvedMode.Flat;
                    }

                    // ⚠ And only a call whose line below is short enough, by the measured table: the
                    // boundary between breaking before the call and holding it to chop its arguments falls
                    // as the held `(` moves right, then rises again (HeldCallLimit, SK-DIV-0331).
                    var receiver = m.FlatWidth - tail;
                    var paren = m.Column + receiver + facts.HeldCallHead;

                    // ⚠ A single call that is a whole `=` value (kinds 3 and 4): moved down whenever its `(`
                    // would land past the margin — chopping below as well if it must — or, with one argument,
                    // when the value overflows by a single column; otherwise its arguments chop (#528).
                    if (facts.HeldCall >= 3) {
                        return Fits(m.Column, receiver)
                            && (paren > width
                                || facts.HeldCall == 3
                                && m.Column + m.FlatWidth + m.Trailing == width + 1)
                                ? ResolvedMode.Broken
                                : ResolvedMode.Flat;
                    }

                    // ⚠ Under `wrap_if_long` a long rest of the chain pushes the limit out: measured on a
                    // 2496-row grid, one and two arguments, heads 40 to 110 (#552, GroupFacts.HeldCallRest).
                    // ⚠ A receiver that overflows by itself breaks inside, and the call breaks too: every
                    // link chops (#582).
                    if (facts.HeldCallOnAPath && !Fits(m.Column, receiver)) {
                        return ResolvedMode.Broken;
                    }

                    var line = m.ContinuationColumn + tail;
                    var limit = HeldCallLimit(paren, facts.HeldCall);
                    return Fits(m.Column, receiver)
                        && !Fits(m.Column, m.FlatWidth)
                        && (line <= limit
                            || facts.HeldCallRest > 0
                            && line <= width
                            && facts.HeldCallRest > 1.5 * (line - limit) + 9)
                            ? ResolvedMode.Broken
                            : ResolvedMode.Flat;
                }

                // ⚠ Broken exactly when only the terminator overflows. See GroupFacts.Terminator.
                if (facts.Terminator > 0) {
                    return !Fits(m.Column, m.BreakWidth) && Fits(m.Column, m.BreakWidth - facts.Terminator)
                        ? ResolvedMode.Broken
                        : ResolvedMode.Flat;
                }

                // ⚠ A switch arm's pattern fill: by the measured table. See GroupFacts.ArmHead.
                if (facts.ArmHead > 0) {
                    return ArmFills(facts, m) ? ResolvedMode.Broken : ResolvedMode.Flat;
                }

                // ⚠ An `=` before a single call: by the measured table. See GroupFacts.HeldValue.
                if (facts.HeldValue > 0) {
                    return HeldValueBreaks(facts, m) ? ResolvedMode.Broken : ResolvedMode.Flat;
                }

                // ⚠ The last operand of a header condition: read from the planner's flat widths, breaks the body
                // already holds aside (#600). See GroupFacts.LambdaOperandBody.
                if (facts.LambdaOperandBody > 0 && facts.LambdaOperandKept) {
                    return Fits(m.Column, facts.LambdaOperandBody) ? ResolvedMode.Flat : ResolvedMode.Broken;
                }

                if (facts.LambdaOperandBody > 0) {
                    // ⚠ A body that fits by itself stays, and the header's `)` moves down instead.
                    var bodyEnd = m.Column + facts.LambdaOperandBody + facts.LambdaOperandTail;
                    return m.Column + facts.LambdaOperandBody > width
                        && EqualsFloor.BreaksTheOperandArrow(
                            m.Column,
                            facts.LambdaOperandParameters,
                            facts.LambdaOperandFirst,
                            bodyEnd,
                            facts.LambdaOperandPatternLeft
                        )
                            ? ResolvedMode.Broken
                            : ResolvedMode.Flat;
                }

                if (!facts.BreaksIfTooLong || Fits(m.Column, m.BreakWidth, m.Trailing)) {
                    return ResolvedMode.Flat;
                }

                // ⚠ `v = X || Y` with `X` too wide beside the `=`: broken from a head floor that a wide `Y` lowers
                // (#579). See EqualsFloor.OrHeadFloor.
                if (facts.OrLeft > 0 && m.PointWidth < Unbounded && !Fits(m.Column, m.PointWidth + 1 + facts.OrLeft)) {
                    return facts.OrHead >= EqualsFloor.OrHeadFloor(facts.OrLeft, facts.OrRight, facts.OrLeftIsPattern)
                        ? ResolvedMode.Broken
                        : ResolvedMode.Flat;
                }

                // ⚠ A switch arm's body that is a cast over an atom: the arrow or the cast's `)`, by the measured
                // table. See GroupFacts.ArmCast (#591).
                if (facts.ArmCast > 0
                    && width == ArmCastMargin
                    && m.FlatWidth < Unbounded
                    && m.Trailing < Unbounded
                    && m.Column > lineStart) {
                    return ArmCastBreaksTheArrow(facts, m, lineStart) ? ResolvedMode.Broken : ResolvedMode.Flat;
                }

                // ⚠ A sole lambda argument over a member-access fill: the arrow or the fill, by the
                // measured line rather than by whether the body fits below. See
                // GroupFacts.LambdaParameters (#557).
                // ⚠ A sole lambda argument over an operand chain or a binary pattern (#578). See
                // GroupFacts.LambdaOperandParameters.
                if (facts.LambdaOperandParameters > 0 && m.FlatWidth < Unbounded) {
                    var end = m.Column + m.FlatWidth + facts.LambdaOperandTail;
                    return EqualsFloor.BreaksTheOperandArrow(
                        m.Column,
                        facts.LambdaOperandParameters,
                        facts.LambdaOperandFirst,
                        end,
                        facts.LambdaOperandPatternLeft
                    )
                        ? ResolvedMode.Broken
                        : ResolvedMode.Flat;
                }

                // ⚠ A sole lambda argument over a chain of calls (#571). See GroupFacts.LambdaChainHead.
                if (facts.LambdaChainHead > 0) {
                    var start = m.Column - facts.LambdaHead;
                    var parameters = facts.LambdaHead - 3;
                    var startLimit = facts.LambdaIsSimple ? 21 : 25;
                    return start >= startLimit
                        || 9 * (m.ContinuationColumn + tail) + 2 * parameters - 2 * start <= 969
                        || m.Column >= 21 && start + facts.LambdaChainHead > width
                            ? ResolvedMode.Broken
                            : ResolvedMode.Flat;
                }

                // ⚠ A local's lambda over a call with two or more arguments (#453): the arrow breaks while the
                // argument list is narrower than the measured floor; otherwise the head rule below decides. See
                // GroupFacts.LambdaCallArguments.
                if (facts.LambdaCallArguments > 0
                    && m.FlatWidth < Unbounded
                    && EqualsFloor.BreaksTheCallArrow(
                        m.Column - facts.LambdaHead - 1 + (facts.LambdaCallShift ? 8 : 0),
                        m.Column + facts.LambdaCallCallee + 2 + (facts.LambdaCallShift ? 4 : 0),
                        facts.LambdaCallArguments,
                        facts.LambdaCallSingle
                    )) {
                    return ResolvedMode.Broken;
                }

                if (facts.LambdaParameters > 0) {
                    var below = m.ContinuationColumn + tail;
                    var start = m.Column - facts.LambdaHead;
                    return 9 * below + 2 * facts.LambdaParameters - 2 * start <= 969
                        || facts.LambdaIsSimple
                        && start >= 21
                            ? ResolvedMode.Broken
                            : ResolvedMode.Flat;
                }

                // ⚠ A break that is one of two alternatives — the `=`'s or the bracket's after it — is
                // added by the same rule it is kept by: exactly when the value fits flat on the line it
                // would move to, and never as a way of making *this* line fit. The oracle writes
                // `T v = [` at 122 columns and lets the bracket chop rather than break after the `=`
                // before a bracket that would not fit below, and moves a bracket that does fit below
                // down whole right up to a 120-column continuation line, where the ordering rule's
                // fitted margin stops eleven columns short. Answering the flat direction with that
                // rule and the kept direction with this one is what made pass two undo pass one:
                // the `=` broke on a flat line and was given back to the bracket as soon as it was
                // read as the author's (#379, the mirror image of #375). See
                // GroupFacts.BreaksOnlyIfTailFits.
                if (facts.BreaksOnlyIfTailFits) {
                    // ⚠ A lambda's arrow over a call chain whose receiver runs past the margin beside it: no dot
                    // can end the line in time (fuzz 16278079796336422477). Kept, pass two read the dot's break
                    // as the author's and broke the arrow. See GroupFacts.BreaksIfReceiverOverflows.
                    if (facts.BreaksIfReceiverOverflows && m.AfterPoint < Unbounded && !Fits(m.Column, m.AfterPoint)) {
                        return ResolvedMode.Broken;
                    }

                    return TailFits(m, tail) && HeadIsWideEnough(facts, m, lineStart)
                        ? ResolvedMode.Broken
                        : ResolvedMode.Flat;
                }

                // ⚠ A conditional's `=` (#553): broken exactly when the condition does not fit beside it
                // and the head through the `=` is twelve columns or more — and, for a call, when the call
                // then fits below or the `=` stands at column 40 or left of it: a call that fits nowhere
                // behind an `=` further right keeps the `=` and chops its arguments. Measured on chain,
                // binary, identifier and call conditions behind heads from 17 to 66 columns. See
                // GroupFacts.ValueHeadWidth.
                if (facts.ValueHeadWidth > 0) {
                    // ⚠ A call whose `(` lands past the margin beside the `=` leaves the `=` nothing to keep (#596,
                    // fuzz 8249044719362511507): pass one kept `T v19 = Select(` at 124 columns and pass two, finding
                    // the arguments chopped, broke the `=`. Measured 2026-10-10 with `Testing ask`: the `(` at 121 to
                    // 127 breaks the `=` for short and long argument lists alike. See GroupFacts.ValueHeadCallee.
                    if (facts.ValueHeadCallee > 0
                        && m.PointWidth < Unbounded
                        && !Fits(m.Column, m.PointWidth + 1 + facts.ValueHeadCallee)) {
                        return ResolvedMode.Broken;
                    }

                    var beside = Fits(m.Column, m.PointWidth + 1 + facts.ValueHeadWidth);
                    var below = Fits(m.ContinuationColumn, facts.ValueHeadWidth);
                    if (beside) {
                        // ⚠ By the name the `=` assigns when it is known (#577): the fragment question of
                        // EqualsFloor.BreaksBeforeTheValue, then for a name of six or more ConditionalMovesDownWhole's
                        // limit with the name in place of the `=`'s column — what that column measured under the `var`
                        // heads it was fitted on — and for a shorter name the member value's limit.
                        if (facts.EqualsName > 0
                            && m.PointWidth < Unbounded
                            && tail < Unbounded
                            && m.Trailing < Unbounded) {
                            var equals = m.Column + m.PointWidth;
                            return EqualsFloor.FragmentIsShort(equals, facts.EqualsName, facts.ValueHeadWidth)
                                && (facts.EqualsName >= 6
                                    ? ConditionalMovesDownWhole(facts, m, tail)
                                    || equals >= 84
                                    && EqualsFloor.FitsBelow(
                                        equals,
                                        facts.EqualsName,
                                        facts.ValueHeadWidth,
                                        m.ContinuationColumn + tail + m.Trailing
                                    )
                                    : EqualsFloor.FitsBelow(
                                        equals,
                                        facts.EqualsName,
                                        facts.ValueHeadWidth,
                                        m.ContinuationColumn + tail + m.Trailing
                                    ))
                                    ? ResolvedMode.Broken
                                    : ResolvedMode.Flat;
                        }

                        return facts.ValueHeadIsWide && ConditionalMovesDownWhole(facts, m, tail)
                            ? ResolvedMode.Broken
                            : ResolvedMode.Flat;
                    }

                    // ⚠ And a call condition that would be long below keeps the `=` and chops instead (#596's residue,
                    // SK-DIV-0447): measured 2026-10-10 on 712 locals — `var` and typed, indents 8 and 12, calls of one
                    // to four arguments, the `=` ending at 18 to 98 and the call 18 to 108 columns wide — the `=` breaks
                    // only while 9 · (the call's end below) + 2 · (the `=`'s end) + 64 · (its argument count) ≤ 1136.
                    // 680 of the 712 rows agree; the rest are one step either side of the boundary.
                    // Only a call that fits below: one that does not is the `=` column's question, as before.
                    if (facts.ValueHeadFitsBelow
                        && below
                        && facts.ValueHeadArguments > 0
                        && 9 * (m.ContinuationColumn + facts.ValueHeadWidth)
                        + 2 * (m.Column + m.PointWidth)
                        + 64 * facts.ValueHeadArguments
                        > 1136) {
                        return ResolvedMode.Flat;
                    }

                    return !beside
                        && facts.ValueHeadIsWide
                        && (!facts.ValueHeadFitsBelow || below || m.Column <= CallConditionColumn)
                            ? ResolvedMode.Broken
                            : ResolvedMode.Flat;
                }

                return Worth(facts, m, afterPointRunsToTheEnd, tail, pointSpace);
        }
    }

    /// <summary>
    ///     A conditional whose condition fits beside its <c>=</c>: whether the oracle moves it down whole
    ///     rather than chopping it on the <c>=</c>'s line (#577).
    /// </summary>
    /// <remarks>
    ///     ⚠ Measured 2026-10-08 with <c>Testing ask</c> on 2 273 rows that do not fit on one line —
    ///     heads through <c>var … =</c> from 9 to 62 columns at a statement indent of 8, conditions of 4
    ///     to 24 columns, the value's line below from 96 to 121 columns, the branches split evenly and
    ///     lopsidedly (the split never mattered). The oracle moves the value down whole when the line
    ///     below is short enough, and the allowance shrinks with the condition and, past the <c>=</c> at
    ///     column 39, with the head: <c>100·below + 38·min(condition, 24) + 24·max(0, column − 39) ≤ 11 364</c>
    ///     — a condition past 24 columns costs no more (measured to 40).
    ///     The fit agrees on 2 174 of the 2 273 rows; ⚠ the 99 it misses are almost all one band the
    ///     measurement could not explain — a condition of four to six columns behind a head of 17 to 28,
    ///     where the oracle moves the value down whatever its width, past the margin included.
    /// </remarks>
    bool ConditionalMovesDownWhole(in GroupFacts facts, in Measures m, int tail) {
        var below = m.ContinuationColumn + tail;
        // ⚠ The `=`'s column was the name: under the `var` heads it was fitted on, column − 39 is name − 26 (#577).
        var past = facts.EqualsName > 0 ? facts.EqualsName - 26 : m.Column - 39;
        return TailFits(m, tail)
            && 100 * below + 38 * Math.Min(facts.ValueHeadWidth, 24) + 24 * Math.Max(0, past) <= 11364;
    }

    /// <summary>See <see cref="GroupFacts.EqualsName" /> and <see cref="EqualsFloor.BreaksBeforeTheValue" />.</summary>
    /// <remarks>
    ///     The <c>=</c> ends the group's point, so its 1-based column is the group's column plus the point's width —
    ///     under a declarator's clause and an assignment, whose group starts at the name, alike. The fragment is the
    ///     dot fill's first line after the <c>=</c>, read from the formatted widths.
    /// </remarks>
    bool BreaksBeforeAPlainMember(in GroupFacts facts, in Measures m, int tail) {
        var equals = m.Column + m.PointWidth;
        var end = equals + 1 + facts.MemberHeadWidth;
        var fragment = facts.MemberHeadWidth;
        var links = facts.MemberLinks ?? [];
        for (var i = 0; i < links.Length; i++) {
            var last = i == links.Length - 1;
            if (end + links[i] + (last ? m.Trailing : 0) > width) {
                break;
            }

            end += links[i];
            fragment += links[i];
        }

        return EqualsFloor.BreaksBeforeTheValue(
            equals,
            facts.EqualsName,
            fragment,
            m.ContinuationColumn + tail + m.Trailing
        );
    }

    /// <summary>See <see cref="GroupFacts.CalleeWidth" />.</summary>
    ResolvedMode EqualsBeforeACall(in GroupFacts facts, in Measures m, int lineStart) {
        if (m.PointWidth >= Unbounded || !HeadIsWideEnough(facts, m, lineStart)) {
            return ResolvedMode.Flat;
        }

        // The value starts one space past the point; its `(` follows the callee. Columns are 1-based in
        // the measured table.
        var paren = m.Column + m.PointWidth + 1 + facts.CalleeWidth + 1;

        // ⚠ A `(` past the margin breaks the `=` whatever the arguments (Nightly seed 37583856628, case
        // 4304693669410283359). The table was measured with the `(` at 53 to 112 and extrapolated past it,
        // and the extrapolation kept `T v = Select(` on a 122-column line; pass two read the chopped
        // arguments as the author's, lost the floor and broke the `=` — the oracle's answer for both
        // passes. Measured 2026-10-09 with `Testing ask`: a typed and a `var` local, arguments of 20, 60
        // and 140 columns, the `(` at 108 to 127 — every row with the `(` at 121 or further breaks.
        // ⚠ Read before the flat width, which the `(` does not need: arguments holding a break that is
        // certain (a switch expression) have no flat width, and this rule returned Flat before it reached
        // the `(` — `T v = TryGet(` on a 123-column line, which pass two broke (Nightly `fuzz --seed=4242`,
        // case 7862808234978504853).
        if (paren > width) {
            return ResolvedMode.Broken;
        }

        // ⚠ The name the `=` assigns has to reach the call, or the arguments chop (#589). See
        // EqualsFloor.NameReachesTheCall.
        // ⚠ Not a field's name of 30 or more, whose own table has a floor at a `(` far left that the gate would refuse.
        if (facts.EqualsName > 0
            && (facts.CalleeOwner != EqualsOwner.Field || facts.EqualsName < 30)
            && !EqualsFloor.NameReachesTheCall(facts.EqualsName, facts.CalleeWidth, paren)) {
            return ResolvedMode.Flat;
        }

        if (m.FlatWidth >= Unbounded) {
            return ResolvedMode.Flat;
        }

        var arguments = m.FlatWidth - m.PointWidth - 1 - facts.CalleeWidth;

        // ⚠ A field's name of 31 or more with the `(` at 76 or left of it: a floor on the arguments that falls a
        // column per three of name from 62 (round 2 of #589, SK-DIV-0400). ⚠ Not at 77–78, where a
        // `private static readonly` field of 32 (equals-before-a-call-floor.cs) chops that a `public` one breaks.
        // See EqualsFloor.LongFieldNameFloor.
        if (facts is { CalleeOwner: EqualsOwner.Field, EqualsName: >= 31, EqualsNameAttributed: false }
            && paren <= 76) {
            return arguments - 2 <= EqualsFloor.LongFieldNameFloor(facts.EqualsName)
                ? ResolvedMode.Broken
                : ResolvedMode.Flat;
        }

        var indent = m.ContinuationColumn - indentWidth;

        return arguments < EqualsFloor.Of(paren, indent, facts.CalleeWidth, facts.CalleeOwner)
            ? ResolvedMode.Broken
            : ResolvedMode.Flat;
    }

    /// <summary>
    ///     Whether a field's generic type fills on its modifiers' line rather than moving below them (#540,
    ///     SK-DIV-0127).
    /// </summary>
    /// <remarks>
    ///     ⚠ Measured with <c>jb cleanupcode</c> 2025.2.6 on 15 000 fields, one column at a time from 121 to 160:
    ///     eight modifier sets, indents 4 and 8, <c>Dictionary</c>, <c>IReadOnlyDictionary</c>, <c>Func</c>,
    ///     <c>ConcurrentDictionary</c> and <c>Action</c> types, heads of 4 to 63 columns through the first comma, names
    ///     of 1 to 8 letters. With <c>X</c> the line the type and the name would make one level below the modifiers,
    ///     <c>L</c> the name and <c>h</c> the type through its first comma, the oracle fills once
    ///     <c>3·X ≥ 5·L + K(h)</c>, <c>K</c> falling from 336 at <c>h</c> = 15 to 324 from 54, and never below 15;
    ///     under 24 only while that threshold is at most <c>113 + h / 3</c>. The modifiers enter only through <c>X</c>:
    ///     round three's <c>public static readonly</c> rows and <c>private readonly</c> ones read off one table once
    ///     measured on the line below rather than the line's end. 11 113 of 11 361 cells agree, and a probe written
    ///     after the rule, with three new modifier sets and two new types at two indents, 4 028 of 4 092.
    /// </remarks>
    bool FillsAfterTheModifiers(in GroupFacts facts, int lineStart) {
        var head = facts.ModifierFillHead;
        var name = facts.ModifierFillName;
        var k = head >= 54 ? 324 :
            head >= 36 ? 325 :
            head >= 24 ? 326 :
            head >= 21 ? 330 :
            head >= 18 ? 334 :
            head >= 15 ? 336 : 0;
        if (k == 0) {
            return false;
        }

        var first = (5 * name + k + 2) / 3;
        if (head < 24 && first > 113 + head / 3) {
            return false;
        }

        var below = lineStart + indentWidth + facts.ModifierFillType + 1 + name + 1;
        return 3 * below >= 5 * name + k;
    }

    /// <summary>
    ///     Whether a returned type test's operand breaks before its dot, <c>return r</c> / <c>.P… as T;</c>, rather
    ///     than at the keyword (#446, SK-DIV-0210). <paramref name="column" /> is where the operand starts.
    /// </summary>
    /// <remarks>
    ///     ⚠ Measured with <c>jb cleanupcode</c> 2025.2.6 on 4 600 cells, receivers of 1 to 40 columns, types of 1 to
    ///     20, indents 8 to 20, <c>is</c> and <c>as</c>, the keyword ending at 110 to 136. With <c>k</c> the line
    ///     through the keyword, <c>d</c> the dot's line one level in and <c>t</c> the type:
    ///     <list type="bullet">
    ///         <item>the dot's line must fit, and nothing else breaks when the whole statement does;</item>
    ///         <item>an operand past the margin by itself always breaks at the dot (194 of 194);</item>
    ///         <item>
    ///             one column over the margin the dot breaks unless the receiver is at most <c>(t − 10) / 5</c>;
    ///         </item>
    ///         <item>
    ///             otherwise — a tie, where the keyword's band could hold the line — the dot breaks when the type is
    ///             at most 12 columns, <c>k − d</c> is past <c>max(5, ⌈(4t + 12) / 5⌉)</c> and <c>k</c> reaches
    ///             <c>width − 2</c>.
    ///         </item>
    ///     </list>
    ///     ⚠ The ties are wired only where every measured cell agrees. The full boundary read from the first 1 608
    ///     cells — the floor reached, and <c>k</c> a column lower for each two columns past it — was refuted by
    ///     the next probe (510 of 2 988 cells): types of 15 and 16 never break at the dot up to <c>k − d</c> of
    ///     23, at indent 20 the floor is a column lower, and the step in <c>k</c> comes sooner for some types. The
    ///     subset kept here is right in every one of the 5 596 tie cells of the three probes and was then checked
    ///     on a fourth. The <c>x</c> layout — neither the keyword's band nor the dot's line fits, and the oracle
    ///     nests the dot two levels in — is not this rule's and stays the keyword's.
    /// </remarks>
    bool TheDotTakesTheBreak(in GroupFacts facts, int column, int lineStart) {
        var operandEnd = column + facts.TypeTestOperand;
        var keywordEnd = operandEnd + 1 + facts.TypeTestKeyword;
        var whole = keywordEnd + 1 + facts.TypeTestType + 1;
        var dotLine = lineStart + indentWidth + facts.TypeTestTail;
        if (whole <= width || dotLine > width) {
            return false;
        }

        if (operandEnd > width) {
            return true;
        }

        if (whole == width + 1) {
            return 5 * facts.TypeTestReceiver > facts.TypeTestType - 10;
        }

        var floor = Math.Max(5, (4 * facts.TypeTestType + 12 + 4) / 5);
        return facts.TypeTestType <= 12 && keywordEnd - dotLine > floor && keywordEnd >= width - 2;
    }

    /// <summary>
    ///     Whether a parameter's one attribute section chops its arguments, for a parameter wider than eleven
    ///     columns behind a section of two or more arguments (#476, SK-DIV-0352); null when the rule does not
    ///     speak.
    /// </summary>
    /// <remarks>
    ///     ⚠ Measured with <c>jb cleanupcode</c> 2025.2.6 one column at a time, the section ending at 96 to 120
    ///     (#476, SK-DIV-0352): parameters of 12 to 39 columns, indents 8 to 24, attribute names putting the
    ///     <c>(</c> 2 to 17 columns past the <c>[</c>, string, integer, <c>typeof</c> and member arguments, two to four
    ///     of them. The arguments chop exactly when the joined line overflows,
    ///     <c>24·E ≥ 1695 + 32·w + 11·i + 12·h</c> and <c>5·w + 2·i − h ≤ 155</c>: 5 757 of 5 773 cells over six
    ///     probes, the last written after the rule and matching 1 284 of 1 288. Neither what the arguments are nor
    ///     what chopping them saves enters it; the <c>(</c>'s column does. The 16 misses are a threshold that rises
    ///     faster than the line next to the second condition's edge, where the oracle keeps the section whole a few
    ///     columns longer. Only for a section that starts its line, which is what was measured: a chopped list.
    /// </remarks>
    ResolvedMode? ChopsBeforeTheParameter(in GroupFacts facts, in Measures m, int lineStart) {
        if (m.Column != lineStart + facts.SectionHead) {
            return null;
        }

        var end = lineStart + facts.SectionWidth;
        var parameter = facts.ParameterAfterSection;
        if (end > width || end + 1 + parameter <= width) {
            return null;
        }

        return 5 * parameter + 2 * lineStart - facts.SectionHead <= 155
            && 24 * end >= 1695 + 32 * parameter + 11 * lineStart + 12 * facts.SectionHead
                ? ResolvedMode.Broken
                : ResolvedMode.Flat;
    }

    /// <summary>What a <see cref="GroupMode.Preserve" /> group whose source was broken does with the break.</summary>
    /// <remarks>
    ///     ⚠ Preserve does not re-flow the author's breaks away by default. Whether it may join one that
    ///     fits, and whether it may add one that the author did not write, are per-construct facts — see
    ///     <see cref="GroupFacts" /> for why one rule is not enough.
    /// </remarks>
    ResolvedMode KeepOrJoin(in GroupFacts facts, in Measures m, int tail) {
        // ⚠ A kept break that is one of two alternatives — the `=`'s or the `[`'s after it — is kept
        // exactly when the value fits flat on the line it would move to. Measured on the flat width
        // and not the head: `= [` always fits, and a bracket that is going to break is the case where
        // the oracle gives the break to the bracket. See GroupFacts.BreaksOnlyIfTailFits (#375).
        if (facts.BreaksOnlyIfTailFits) {
            return TailFits(m, tail) ? ResolvedMode.Broken : ResolvedMode.Flat;
        }

        return facts.JoinsIfFits && Fits(m.Column, m.FlatWidth, m.Trailing)
            ? ResolvedMode.Flat
            : ResolvedMode.Broken;
    }

    /// <summary>
    ///     Whether what follows the group's own point fits flat on the continuation line — the one
    ///     question a <see cref="GroupFacts.BreaksOnlyIfTailFits" /> group is resolved by, from a kept
    ///     break and from a flat line alike.
    /// </summary>
    /// <remarks>
    ///     ⚠ No margin. <see cref="OuterBreakMargin" /> is fitted to right-hand sides the oracle stops
    ///     moving down well before the line below is full; a collection expression is not one of them.
    ///     Measured from a flat source with the <c>=</c> at column 60 and again at 100, one column at a
    ///     time: a bracket whose continuation line would be 120 columns moves down whole, 121 is
    ///     written <c>= [</c> and filled — the same boundary the kept direction has (#375, #379), and
    ///     the same for an expression body's arrow.
    /// </remarks>
    bool TailFits(in Measures m, int tail) => Fits(m.ContinuationColumn, tail, m.Trailing);

    /// <summary>
    ///     Whether the head — from the owner's first token, the marker <see cref="GroupFacts.Owner" />
    ///     names, through the group's own point — reaches <see cref="GroupFacts.MinimumHead" />.
    /// </summary>
    /// <remarks>
    ///     ⚠ Read off the writer's columns and not off the syntax, because the head's width in the
    ///     source is not its width in the output: the fuzzer widens gaps, and a floor measured on the
    ///     source would pass on pass one and fail on pass two. ⚠ And from the line's own start when the
    ///     owner's first token is on an earlier line: measured, the oracle glues <c>int&gt; d = [</c>
    ///     on the second line of a broken type argument list and breaks after <c>int&gt; dddddd =</c>,
    ///     the same eleven-against-twelve counted from the line. The first cut waived the floor for a
    ///     head that spans lines, on the argument that a full first line is not a short head; the
    ///     oracle does not count the first line at all.
    /// </remarks>
    bool HeadIsWideEnough(in GroupFacts facts, in Measures m, int lineStart) {
        if (facts.MinimumHead <= 0) {
            return true;
        }

        // No marker: the group starts at its own head, and the point width is the head.
        var owner = facts.Owner;
        if (owner < 0) {
            return m.PointWidth >= facts.MinimumHead;
        }

        var from = resolved[owner] && enteredOn[owner] == m.Line ? enteredAt[owner] : lineStart;
        return m.Column + m.PointWidth - from >= facts.MinimumHead;
    }

    /// <summary>
    ///     The ordering rule: a group that does not fit still only breaks when its own break is the one
    ///     worth taking.
    /// </summary>
    /// <remarks>
    ///     ⚠ This is milestone 3's substance, and milestone 2 left it out on purpose: it measured that
    ///     breaking at the first available point costs 0.24 points of line fidelity against leaving the
    ///     line alone, because the oracle's break lands one line away (SK-DIV-0002).
    ///     <para>
    ///         Without the fact set, the answer is the old one — too long means broken — and every group
    ///         that does not opt in behaves exactly as it did at M2. With it, two questions decide, in this
    ///         order:
    ///     </para>
    ///     <list type="number">
    ///         <item>
    ///             <b>Does this break alone finish the job?</b> If what follows the group's own first break
    ///             point fits on a continuation line, take it: two lines beat the three that wrapping something
    ///             inside would cost. This is the case for
    ///             <c>JsonObjectContract c =\n    (JsonObjectContract)r.ResolveContract(typeof(T));</c>, where
    ///             chopping the argument list instead produces a third line the oracle does not write.
    ///         </item>
    ///         <item>
    ///             <b>Otherwise, does the line end here anyway?</b> Something inside is going to wrap, so the
    ///             current line runs to the first break point whether this group breaks or not. If that much
    ///             fits, this group's break buys a line and gains nothing:
    ///             <c>schema.Properties = new Dictionary&lt;…&gt; {</c> is the oracle's first line, not
    ///             <c>schema.Properties =</c>. If it does <em>not</em> fit — the call's own name runs past the
    ///             margin — then both breaks are needed and this one is taken.
    ///         </item>
    ///     </list>
    ///     <para>
    ///         ⚠ It is a local rule and not a search. docs/plan/04 § "The fitting algorithm" rules out
    ///         optimal-layout algorithms because ReSharper is not optimal either; this reproduces the
    ///         answer ReSharper gives on the shapes that occur, in one traversal and with no backtracking.
    ///     </para>
    /// </remarks>
    ResolvedMode Worth(in GroupFacts facts, in Measures m, bool afterPointRunsToTheEnd, int segment, int pointSpace) {
        // ⚠ Only at the 120-column margin it was measured at; any other margin keeps the fitted one, which the
        // committed sweep's `max_line_length` rows were taken against.
        if (facts.PrefersOuterBreak && facts.CreationLimit != 0 && width == CreationLimitWidth) {
            // ⚠ A creation with a one-line initializer moves down whole by its own measured limit, and the
            // braces break otherwise (#581). See GroupFacts.CreationLimit.
            if (segment >= Unbounded || facts.CreationLimit < 0) {
                return ResolvedMode.Flat;
            }

            // ⚠ The segment past the point, not FlatWidth − PointWidth, which counts the point's own space.
            var below = m.ContinuationColumn + segment + m.Trailing;
            var indent = m.ContinuationColumn - indentWidth;
            return 40 * below <= facts.CreationLimit - 5 * (indent - 8)
                ? ResolvedMode.Broken
                : ResolvedMode.Flat;
        }

        if (facts.PrefersOuterBreak) {
            // What lands on the continuation line if this group breaks and nothing inside it does.
            var margin = facts.OuterMargin >= 0 ? facts.OuterMargin : OuterBreakMargin(m);
            var tail = m.FlatWidth >= Unbounded ? Unbounded : m.FlatWidth - m.PointWidth + margin;
            var finishes = facts.JoinedOverflow >= 0
                ? m.FlatWidth < Unbounded
                && Fits(m.Column, m.FlatWidth - facts.JoinedOverflow, m.Trailing)
                && (facts.NameWidth < 0
                    || 9 * (m.Column + 1) + 6 * facts.NameWidth + facts.NameFloor
                    >= 8 * (m.Column + m.FlatWidth + m.Trailing))
                : Fits(m.ContinuationColumn, tail, m.Trailing);
            if (!facts.SkipsOuterTail && finishes) {
                return ResolvedMode.Broken;
            }

            // ⚠ The same two questions asked of the segment that ends at another group's first point,
            // with the construct inside it read as no place to break (#501, SK-DIV-0198): the line
            // through that point stays when it fits where the group starts, and moves down whole when
            // it fits on the continuation line. See GroupFacts.TailEndsAt.
            if (facts.TailEndsAt >= 0 && segment < Unbounded) {
                if (Fits(m.Column, m.PointWidth + pointSpace + segment)) {
                    return ResolvedMode.Flat;
                }

                var tailMargin = facts.TailMargin >= 0 ? facts.TailMargin : OuterBreakMargin(m);
                if (Fits(m.ContinuationColumn, segment + tailMargin)) {
                    return ResolvedMode.Broken;
                }
            }
        } else if (!facts.BreaksOnlyIfHeadOverflows) {
            return ResolvedMode.Broken;
        }

        // ⚠ The type argument list after the point overflows by itself, so it fills and this group's
        // break is not the one taken — unless the argument list after it is at least the measured
        // floor. See GroupFacts.YieldsToOverflowingTypeArguments and ColonFloor (#490).
        if (facts.YieldsToOverflowingTypeArguments
            && m.YieldEnd > 0
            && m.PointWidth < Unbounded
            && !Fits(m.Column, m.PointWidth + m.YieldEnd)) {
            var arguments = m.FlatWidth >= Unbounded ? Unbounded : m.FlatWidth - m.PointWidth - m.YieldEnd;
            var floor = facts.ColonFloor + facts.ColonFloorSlope * (m.PointWidth + m.YieldEnd - 118) / 100;
            if (arguments < floor) {
                return ResolvedMode.Flat;
            }
        } else if (facts.YieldsToOverflowingTypeArguments
                   && facts.ColonEdgeFloor > 0
                   && m.YieldEnd > 0
                   && m.PointWidth < Unbounded
                   && Fits(m.Column, m.PointWidth + m.YieldEnd)
                   && !Fits(m.Column, m.PointWidth + m.YieldEnd + 1)) {
            // ⚠ The `>` lands on the margin itself and only the call's `(` is past it.
            var arguments = m.FlatWidth >= Unbounded ? Unbounded : m.FlatWidth - m.PointWidth - m.YieldEnd;
            if (arguments < facts.ColonEdgeFloor) {
                return ResolvedMode.Flat;
            }
        }

        // What lands on *this* line if the group stays flat and the construct inside wraps instead.
        // ⚠ Plus the trailing text when nothing inside can wrap at all, because then the line does
        // not end at an inner point — it ends after the group, `;` included. A type argument list's
        // points are last-resort ones that the point measure reads through (SK-DIV-0114), so
        // `var result = Generic<A, B, int>();` reached here with a 120-column line and a semicolon
        // nobody counted, stayed flat, and filled the list where the oracle breaks at the `=`.
        // ⚠ A switch arm's arrow, a lambda's and a `when` ask only this question (issue #378): the
        // oracle never moves the body down to spare the construct inside it a break, and moves it
        // exactly when the head up to that construct's first point has no room on the line.
        var line = m.PointWidth >= Unbounded ? Unbounded : m.PointWidth + m.AfterPoint + facts.HeadSlack;
        var trailing = afterPointRunsToTheEnd ? m.Trailing : 0;
        return Fits(m.Column, line, trailing) ? ResolvedMode.Flat : ResolvedMode.Broken;
    }

    /// <summary>
    ///     How much room the outer break has to leave before it is judged to have "finished the job".
    /// </summary>
    /// <remarks>
    ///     ⚠ Measured, and it is not zero, which is the surprise: the oracle stops taking the <c>=</c>
    ///     break well before the continuation line reaches 120, and the result it declines fits with
    ///     room to spare. What ReSharper is really computing there is <em>not known</em>, and milestone
    ///     3.1 swept it hard enough to say so with evidence rather than with a shrug — eleven
    ///     right-hand-side shapes, five block depths, both values of <c>wrap_before_eq</c>, one
    ///     character at a time (<c>Testing/Rikarin.Skala.Testing/MarginSweep.cs</c>, run as
    ///     <c>margin</c>). Three things came out of it, and all three contradict the milestone-3 note
    ///     this replaces:
    ///     <list type="number">
    ///         <item>
    ///             <b>The threshold does not depend on the nesting depth.</b> At a flat width of 121 the last
    ///             continuation line the oracle still writes is 112 columns at block depth 2, 3, 4, 5 and 6
    ///             alike. Milestone 3 read a <c>column / indent</c> term off three cells that were confounded
    ///             with the shape's own width.
    ///         </item>
    ///         <item>
    ///             <b>It does depend on the flat width, and not monotonically.</b> Same shape, same depth,
    ///             sweeping the flat width: 122 → 113, 124 → 115, 126 to 140 → 116, then back down, 146 → 112,
    ///             158 → 107. No affine function of the numbers this fitter has reproduces that curve.
    ///         </item>
    ///         <item>
    ///             <b>It depends on the shape.</b> At a flat width of 137 and depth 2 the threshold is 116 for
    ///             <c>Convert.FromBase64String("…")</c>, 117 for a call on an identifier, 118 for a binary
    ///             chain, 120 for a cast, and 107 for a member chain — and an object initializer and an array
    ///             initializer go the other way, 107 and 101.
    ///         </item>
    ///     </list>
    ///     <para>
    ///         ⚠ So the constant stays, and it is now honestly a <em>fitted</em> constant rather than a
    ///         derived one. Fitted against <c>corpus/real/</c>, with everything else in the ordering rule
    ///         held fixed:
    ///     </para>
    ///     <code>
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
    ///     </code>
    ///     <para>
    ///         ⚠ And the two measurements disagree, which is the finding worth carrying forward. The
    ///         isolated sweep says the threshold is depth-independent and near 112 — that is the
    ///         <c>constant 8</c> row, and it is 0.08 points and six files <em>worse</em> on real code than a
    ///         depth-dependent constant the sweep does not support. The margin is therefore absorbing error
    ///         from the rest of the ordering rule rather than reproducing a rule of ReSharper's, and no
    ///         value of it will close the last of this class. SK-DIV-0005 records that as the argument.
    ///     </para>
    /// </remarks>
    /// <summary>
    ///     Whether a lambda-valued local one column past the margin gives the line to its type/name gap
    ///     (#583, SK-DIV-0127): not where its parameter list chops (#572), and where the `=` would keep the
    ///     arrow only while the `=` ends past <c>indent + 41 + (indent − 8) / 4</c> — 49, 54 and 59 at indents
    ///     8, 12 and 16, measured; the arrow's own floors (#558) do not reach that far for a type this wide.
    /// </summary>
    bool LambdaGivesWayOneOver(in GroupFacts facts, in Measures m) {
        var equals = document.FactsOf(facts.OneOverEquals);
        var head = width - facts.OneOverValue;
        if (equals.OneOverType > 0
            && EqualsFloor.ChopsOneOver(equals.OneOverType, facts.OneOverValue - equals.YieldsThroughArrow - 2, head)) {
            return false;
        }

        var indent = m.ContinuationColumn - indentWidth;
        return head > indent + 41 + (indent - 8) / 4
            || EqualsBeforeALambda(equals, head, facts.OneOverValue) == ResolvedMode.Broken;
    }

    /// <summary>
    ///     An `=` before a lambda with a bare name for a body, whose `=` ends at <paramref name="head" /> and whose
    ///     value runs <paramref name="value" /> columns from there through the `;`; null when no rule here decides.
    ///     See GroupFacts.YieldsThroughArrow (#453).
    /// </summary>
    ResolvedMode? EqualsBeforeALambda(in GroupFacts facts, int head, int value) {
        var through = head + 1 + facts.YieldsThroughArrow;

        // ⚠ A local's `=`, past a name wider than the type's gate, yields only to a value at
        // least as wide as the measured floor; a narrower one moves below the `=` whole
        // (#558). See GroupFacts.LambdaLocal and EqualsFloor.LambdaValue.
        // ⚠ One column past the margin the parameter list may chop instead, and then the `=`
        // stays (#572). See EqualsFloor.ChopsOneOver.
        if (facts.OneOverType > 0
            && head + 1 + value == width + 1
            && EqualsFloor.ChopsOneOver(facts.OneOverType, value - facts.YieldsThroughArrow - 2, head)) {
            return ResolvedMode.Flat;
        }

        if (through <= width) {
            return facts.LambdaLocal == LambdaLocal.None
                || facts.LambdaLocal.HasFlag(LambdaLocal.ArrowWhileItFits)
                || value >= EqualsFloor.LambdaValue(head)
                    ? ResolvedMode.Flat
                    : ResolvedMode.Broken;
        }

        // ⚠ A value whose first character lands past the margin leaves the `=` nothing to keep: neither the
        // parameter list nor the arrow can end the line in time. Kept, `T v = (` chopped its parameters past
        // the margin and pass two, reading the chop as the author's, broke the `=` (Nightly replay
        // 13830403873739157460, seed 99991) — the lambda's twin of the `(` rule in EqualsBeforeACall.
        // Measured with `jb cleanupcode`: the `=` at 119 and 120 breaks for `(x, y) =>` and `x =>` alike, at
        // 118 the parameters chop.
        if (head + 2 > width) {
            return ResolvedMode.Broken;
        }

        // ⚠ And once the line through `=>` overflows, the `=` breaks while `(…) =>` reaches no
        // further than three columns past the margin, or, past a name wider than the type's
        // second gate, while the value is narrow enough for its body; otherwise the parameter
        // list chops (#558). See EqualsFloor.BreaksBeforeAnOverflowingLambda.
        if (facts.LambdaLocal != LambdaLocal.None) {
            // ⚠ A lambda without parentheses has no list to chop, so the `=` is the only break that can end the
            // line in time (#595, fuzz 18379797974820457043): `T v13 = static x =>` past the margin kept the `=`, and
            // pass two, reading the arrow's break as the author's, broke the `=` as well. Measured 2026-10-10 with
            // `Testing ask`: `x =>` and `static x =>` with the arrow ending at 121 to 131 break the `=` on every row
            // where the type/name gap does not take the line first.
            if (facts.LambdaIsSimple) {
                return ResolvedMode.Broken;
            }

            return EqualsFloor.BreaksBeforeAnOverflowingLambda(
                head,
                value,
                through - width,
                value - facts.YieldsThroughArrow - 2,
                facts.LambdaLocal.HasFlag(LambdaLocal.ChopsPastTheParenthesis)
            )
                ? ResolvedMode.Broken
                : ResolvedMode.Flat;
        }

        return null;
    }

    int OuterBreakMargin(in Measures m) => 11 + m.ContinuationColumn / indentWidth;

    /// <summary>The margin <see cref="GroupFacts.ArmCast" />'s table was measured at (#591).</summary>
    const int ArmCastMargin = 120;

    /// <summary>
    ///     Whether a switch arm whose body is a cast over an atom breaks after its arrow rather than after the
    ///     cast's <c>)</c> (#591, SK-DIV-0440). See <see cref="GroupFacts.ArmCast" />.
    /// </summary>
    /// <remarks>
    ///     ⚠ Measured with <c>Testing ask</c> on 5 947 rows; the table agrees on 5 817 (always breaking the arrow,
    ///     as Skala did, agrees on 3 651). The residue is one column either side of the first constraint for
    ///     casts of 9 to 15 columns, where the oracle's boundary steps by one over a stretch of heads the way the
    ///     band below does by the whole line. ⚠ The band is not explained, only bounded: a cast of five columns
    ///     keeps the arrow from a head of 13 to one of 29, of eight only from 22 to 25, and none from nine on;
    ///     it is the same at indents 12 and 16.
    /// </remarks>
    bool ArmCastBreaksTheArrow(in GroupFacts facts, in Measures m, int lineStart) {
        // ⚠ The group starts at its point, before the space after the arrow: its column is the arrow's end.
        // A body that would not fit below the arrow either changes nothing: where the table keeps the arrow
        // the cast's `)` breaks as well, and where it does not the cast's `)` alone.
        var head = m.Column - lineStart;
        var cast = facts.ArmCast;
        var end = m.Column + m.FlatWidth + m.Trailing;
        if (end == width + 1 && (head >= 7 || cast <= 6)) {
            return true;
        }

        if (cast <= 8 && head >= 3 * cast - 2 && head + cast <= (cast >= 7 ? 33 : 34)) {
            return true;
        }

        return 3 * end <= 3 * head + 336 - cast && 8 * end <= 6 * head + 954 - 3 * cast;
    }

    /// <summary>The margin <see cref="GroupFacts.CreationLimit" /> was measured at (#581).</summary>
    const int CreationLimitWidth = 120;

    /// <summary>
    ///     The widest line a held first call may take below and still be moved there rather than held and
    ///     chopped, by the column its <c>(</c> would land on held and its argument kind (1 = one argument or
    ///     none, 2 = more). Measured (#528, SK-DIV-0331).
    /// </summary>
    /// <remarks>
    ///     ⚠ A table, because the oracle's boundary is not monotone in the head. On a 280-row grid at the
    ///     export's 120 columns — heads every five columns from 40 to 110, call lines every two columns —
    ///     two arguments break up to a line of <c>max(108.5 − 0.4·paren, 58 + 0.2·paren)</c>, which
    ///     reproduces every row; one argument follows the listed thresholds, each the widest line that broke
    ///     plus one (the next measured width, two wider, held), read at the nearest measured head.
    /// </remarks>
    static double HeldCallLimit(int paren, int kind) {
        if (kind != 1) {
            return Math.Max(108.5 - 0.4 * paren, 58 + 0.2 * paren);
        }

        var row = Math.Clamp((int)Math.Round(paren / 5.0) * 5, 40, 110);
        return row switch {
            40 => 111,
            45 or 50 or 55 => 109,
            60 => 107,
            65 => 105,
            70 => 103,
            75 => 101,
            80 => 99,
            85 or 90 => 97,
            95 or 100 => 99,
            _ => 101
        };
    }

    /// <summary>
    ///     Whether an <c>=</c> before a single call breaks, by the head's kind. See GroupFacts.HeldValue.
    /// </summary>
    /// <remarks>
    ///     ⚠ Measured 2026-10-09 (#528, SK-DIV-0331) on value widths 119 to 126 below, receivers of 11 and
    ///     40 columns, and the synthetic receivers of 10 to 102 columns that h1, h8 and h9 sweep: the short
    ///     head keeps the <c>=</c> only once its <c>(</c> would land within two columns of the margin below,
    ///     and breaks it while the value overflows below by one column (the dot then breaks too); the long
    ///     head keeps it exactly where a chain's held first call would move down at its dot; the typed local
    ///     breaks it only while the value fits below with three columns to spare. An assignment whose value
    ///     starts at the continuation column has nothing to gain from the break and never takes it.
    /// </remarks>
    bool HeldValueBreaks(in GroupFacts facts, in Measures m) {
        var valueColumn = m.Column + m.PointWidth + 1;
        if (valueColumn + facts.HeldValueWidth <= width) {
            return false;
        }

        var continuation = m.ContinuationColumn;
        var below = continuation + facts.HeldValueWidth;
        var parenBelow = continuation + facts.HeldValueReceiver + facts.HeldValueHead;
        var parenBeside = valueColumn + facts.HeldValueReceiver + facts.HeldValueHead;
        switch (facts.HeldValue) {
            case 1:
                // ⚠ Or when the head with the receiver beside it is wider than 87 columns: `T… c = JsonConvert`
                // holds to 87 and breaks from 88, counted from the statement (h12 at indent 8, on one call
                // width; Newtonsoft's 77, 82 and 95 at indents 12 and 20 agree, where an absolute column
                // would not).
                return below <= width - 3
                    || valueColumn + facts.HeldValueReceiver - (continuation - indentWidth) > HeldReceiverEnd;
            case 2:
                // ⚠ One column over beside it, a lone argument's dot breaks instead, as it does below.
                if (valueColumn <= continuation
                    || !facts.HeldValueManyArgs
                    && valueColumn + facts.HeldValueWidth == width + 1) {
                    return false;
                }

                // ⚠ A receiver that runs past the margin beside the `=` breaks it, as the other two heads'
                // rules do. Unmeasured — the sweeps' receivers ended by column 118 — and held, it left `= r…(`
                // past the margin for the second pass to break once the arguments had chopped (Nightly fuzzer,
                // seed 1, replay 7536332154113230584: idempotency).
                if (valueColumn + facts.HeldValueReceiver > width) {
                    return true;
                }

                return facts.HeldValueManyArgs
                    ? parenBeside > width && parenBelow <= width - 3
                    : below <= width + 1 || parenBelow <= width - 3;
            default:
                // ⚠ A value that fits below with three columns to spare moves down whole, as a typed local's
                // does (Newtonsoft's `_genericTemporaryCollectionCreator =`, Serilog's `var methods =` at
                // indent 8; h8's 119-column row and h10's keep the `=`).
                if (below <= width - 3) {
                    return true;
                }

                // ⚠ Two arguments whose `(` fits beside it keep the `=` and chop there (Newtonsoft, Serilog).
                if (facts.HeldValueManyArgs && parenBeside <= width) {
                    return false;
                }

                var moves = valueColumn + facts.HeldValueReceiver <= width
                    && parenBeside > width
                    && continuation + facts.HeldValueWidth - facts.HeldValueReceiver
                    <= HeldCallLimit(parenBeside, facts.HeldValueManyArgs ? 2 : 1);
                return !moves;
        }
    }

    /// <summary>
    ///     The widest a typed local's head and held receiver may be beside its <c>=</c>, from the statement's
    ///     first column (#528, h12).
    /// </summary>
    const int HeldReceiverEnd = 87;

    /// <summary>
    ///     Whether a switch arm's member-access pattern breaks at a dot rather than leave the arm to its
    ///     arrow (#531, SK-DIV-0330).
    /// </summary>
    /// <remarks>
    ///     ⚠ Measured 2026-10-09 on 384 arms (heads through the <c>=&gt;</c> of 102 to 127 columns, bodies of
    ///     2 to 22 with and without a comma, three prefixes): a pattern that overflows by itself breaks its
    ///     dot whatever the body; one whose <c>=&gt;</c> overflows breaks its dot for a body of fourteen
    ///     columns or less and moves the <c>=&gt;</c> down otherwise; and an arm that overflows only by its
    ///     body breaks its dot for a body of fourteen or less once the <c>=&gt;</c> ends at column 111 or
    ///     right of it, twelve or less at 110, and never left of that — the arrow breaks there.
    /// </remarks>
    bool ArmFills(in GroupFacts facts, in Measures m) {
        var headEnd = m.Column + facts.ArmHead;
        var arrowEnd = headEnd + 3;
        if (arrowEnd + 1 + facts.ArmBody <= width) {
            return false;
        }

        if (headEnd > width) {
            return true;
        }

        if (arrowEnd > width) {
            return facts.ArmBody <= ArmBodyLimit;
        }

        return arrowEnd >= width - 9
            ? facts.ArmBody <= ArmBodyLimit
            : arrowEnd == width - 10 && facts.ArmBody <= ArmBodyLimit - 2;
    }

    /// <summary>The widest arm body, comma included, that lets a switch arm's pattern fill (#531).</summary>
    const int ArmBodyLimit = 14;

    /// <summary>
    ///     The column a call condition's <c>=</c> breaks at or left of when the call fits nowhere (#553).
    /// </summary>
    const int CallConditionColumn = 40;

    bool Fits(int column, int flatWidth, int trailing = 0) =>
        flatWidth < Unbounded && trailing < Unbounded && column + flatWidth + trailing <= width;
}
