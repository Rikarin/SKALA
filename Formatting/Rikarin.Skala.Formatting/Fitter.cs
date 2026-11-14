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

    /// <summary>See <see cref="BreaksEveryPoint" />.</summary>
    readonly HashSet<int> chops = [];

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

        // ⚠ The type test's own group under the `x` layout: `.P…` / `as` / `T;` puts the type on a line of its own
        // although it would fit beside the keyword (#446).
        if (facts.TypeTestAfterKeyword
            && mode == ResolvedMode.Broken
            && TheDotGoesTwoLevelsIn(facts, column, lineStart)
            && TheTypeLeavesTheKeywordsLine(facts, lineStart)) {
            chops.Add(id);
        } else {
            chops.Remove(id);
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
            chops.Remove(id);
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
                // ⚠ After `yield return` before a returned type test (#446, SK-DIV-0210 round 11). See
                // YieldReturnBreaks.
                if (facts.YieldTestType > 0) {
                    return m.FlatWidth < Unbounded && YieldReturnBreaks(m.Column + m.FlatWidth, facts)
                        ? ResolvedMode.Broken
                        : ResolvedMode.Flat;
                }

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

                // ⚠ A long parameter's default behind its section, before the author's break, which is this rule's own
                // answer on pass two: see GroupFacts.SectionJoinedOverDefault (#603).
                if (facts.SectionJoinedOverDefault > 0
                    && m.Column + facts.SectionJoinedOverDefault > width
                    && !OverflowsItsOwnLine(facts, m.Column)) {
                    var throughEquals = m.Column + facts.SectionJoinedOverDefault - facts.DefaultValueWidth - 1;
                    return m.Column + facts.SectionWidth <= width && SectionChops(facts, m.Column)
                        || throughEquals > width
                            ? ResolvedMode.Broken
                            : ResolvedMode.Flat;
                }

                // ⚠ A break the author wrote after a sole lambda's arrow over a call is kept (#620, SK-DIV-0760): the
                // oracle keeps `Use(x =>` / `Head(…).Name` even where it keeps the arrow over the same body written on
                // one line, and it is pass two's answer to the measured line's break.
                if (facts.SourceBroken && (facts.SoleCallHead > 0 || facts.SolePropertyChain is not null)) {
                    return ResolvedMode.Broken;
                }

                // ⚠ A bare `(a + b…)` whose `=` the measured form breaks stays broken where its value runs past the
                // margin below too, as pass one wrote it (#598, SK-DIV-0770).
                if (facts.SourceBroken
                    && facts.BareParenOperand > 0
                    && HeadIsWideEnough(facts, m, lineStart)
                    && BareParenEqualsBreaks(facts, m) == true) {
                    return ResolvedMode.Broken;
                }

                // ⚠ A parenthesised operator with one member after it, behind a wide head: by its measured form (#598,
                // SK-DIV-0830). A broken `=` the form breaks stays broken on pass two; one it keeps is the author's.
                if (facts.ParenMember.Width > 0
                    && m.PointWidth < Unbounded
                    && ParenMemberEqualsBreaks(facts, m) is { } member
                    && (member || !facts.SourceBroken)) {
                    return member ? ResolvedMode.Broken : ResolvedMode.Flat;
                }

                // ⚠ And a doubled one with a call behind a head of seven to eleven columns (#598, SK-DIV-0872).
                if (facts.ShortParenMember.Width > 0
                    && m.PointWidth < Unbounded
                    && ShortParenMemberEqualsBreaks(facts, m) is { } shortMember
                    && (shortMember || !facts.SourceBroken)) {
                    return shortMember ? ResolvedMode.Broken : ResolvedMode.Flat;
                }

                // ⚠ And a chain body whose first call fits beside the arrow and which fits nowhere whole (#620,
                // SK-DIV-0812): by the measured lines, read from flat widths so that pass two asks what pass one did; a
                // break the author wrote after the arrow is kept.
                if (facts.SoleChainHead > 0 && SoleChainMode(facts, m, lineStart) is { } chainMode) {
                    return facts.SourceBroken ? ResolvedMode.Broken : chainMode;
                }

                // ⚠ A one-argument call under a type test written on one line keeps its arguments whole where the
                // measured line gives the break to the test (#620, SK-DIV-0811). See GroupFacts.TestedCallHead.
                if (facts.TestedCallHead > 0
                    && !facts.SourceBroken
                    && TestedCallMode(facts, m, lineStart) is { } tested) {
                    return tested;
                }

                if (facts.SourceBroken) {
                    return KeepOrJoin(facts, m, tail);
                }

                // ⚠ A `var` local's `=` before a sole lambda call whose value fits below, by the measured line (#620,
                // SK-DIV-0880); elsewhere the older rules decide. See GroupFacts.SoleLambdaEqualsCallee.
                if (facts.SoleLambdaEqualsCallee > 0 && SoleLambdaEqualsBreaks(facts, m, lineStart)) {
                    return ResolvedMode.Broken;
                }

                // ⚠ See GroupFacts.FlatIfHeadOverflows: the head's own point takes the break. ⚠ Read from the
                // planner's width, never the measured point width, which ends at the target's dot once pass one
                // has broken it (#628).
                if (facts.FlatIfHeadOverflows > 0 && !Fits(m.Column, facts.FlatIfHeadOverflows)) {
                    return ResolvedMode.Flat;
                }

                // ⚠ A `when` condition's outer type argument list: see GroupFacts.WhenOuterTail (#576).
                // ⚠ The "stays" half is read off the planner's width, never off the measured one (fuzz
                // 10815522948059258787 and 1019535202291160959): pass one kept the list and broke the inner one after
                // its `<`; pass two, reading that break as the author's, measured this group as unbounded, skipped the
                // rule and broke the list as well.
                // ⚠ On the line of its own a broken `when` gives it, the list's continuation is a level past that line,
                // which the lift adds only after the fact, so the measured continuation is that line's own start (#576
                // round 7, SK-DIV-0399): measured at conditions 100 to 124 below the `when` at indents 8, 12 and 16.
                if (facts.WhenOuterTail > 0) {
                    var continuation = m.ContinuationColumn <= lineStart
                        ? lineStart + indentWidth
                        : m.ContinuationColumn;
                    // ⚠ Round 10: before a second argument holding one plain type argument the bar is three columns
                    // higher, falling a column of line for four of lead (`M<First, IReadOnlyList<T…>>()`): the line
                    // below through `() =>` may run to 114 while `lead + 4·line ≤ 487`, and to 115 behind a lead of 17
                    // or more (a shorter one keeps the list there: `M<List<bool>,` and `Pick<int,`). Measured on the
                    // ten #576 arm grids, 175 rows of the fit grids and 163 of the fresh probes, none the other way.
                    var below = continuation + facts.WhenOuterTail + 5;
                    if (!Fits(continuation, facts.WhenOuterTail + 14)
                        && !(facts.WhenOuterLead > 0
                            && (below <= 114 || below == 115 && facts.WhenOuterLead >= 17)
                            && facts.WhenOuterLead + 4 * below <= 487)) {
                        return ResolvedMode.Flat;
                    }

                    if (m.FlatWidth < Unbounded && m.Trailing < Unbounded) {
                        return Fits(m.Column, m.FlatWidth, m.Trailing) ? ResolvedMode.Flat : ResolvedMode.Broken;
                    }
                }

                // ⚠ One argument the author put below the call's `(` keeps the `=` while the `(` fits (fuzz
                // 17150111611241313074). See GroupFacts.KeptArgumentCallee.
                if (facts.KeptArgumentCallee > 0
                    && m.PointWidth < Unbounded
                    && Fits(m.Column, m.PointWidth + 1 + facts.KeptArgumentCallee + 1)) {
                    return ResolvedMode.Flat;
                }

                // ⚠ A cast's `)` before a parenthesised operator: by the measured rule. See
                // GroupFacts.CastParenFirst (#598).
                if (facts.CastParenFirst > 0) {
                    return BreaksAfterTheCast(facts, m, lineStart) ? ResolvedMode.Broken : ResolvedMode.Flat;
                }

                // ⚠ And before a parenthesised operator with a member: by its measured rule (#598, SK-DIV-0871). See
                // GroupFacts.CastMember.
                if (facts.CastMember.Width > 0) {
                    return m.PointWidth < Unbounded && CastMemberBreaks(facts, m, lineStart)
                        ? ResolvedMode.Broken
                        : ResolvedMode.Flat;
                }

                // ⚠ An `=` whose value's leading parenthesis closes one column past the margin breaks when the
                // value through that `)` fits below, what follows it breaking on its own (#598): `var x =` /
                // `(string)(…)` / `+ c;`. See GroupFacts.ParenCloseEnd.
                // ⚠ Not behind a head of five columns or fewer, which answers as `x =` does, whose value starts on the
                // continuation column (#598, c2: `xx =` and `xxx =` keep the `=` wherever `x =` does).
                if (facts.ParenCloseEnd > 0
                    && m.PointWidth < Unbounded
                    && m.FlatWidth < Unbounded
                    && m.Column + m.PointWidth + 1 > m.ContinuationColumn + 2
                    && m.Column + m.PointWidth + 1 + facts.ParenCloseEnd == width + 1
                    && Fits(
                        m.ContinuationColumn,
                        facts.ParenCloseEnd,
                        facts.ParenCloseEnd == m.FlatWidth - m.PointWidth - 1 ? m.Trailing : 0
                    )) {
                    return ResolvedMode.Broken;
                }

                // ⚠ And so does a doubled parenthesis's inner `)` (#598). See GroupFacts.ParenCloseEndInner.
                if (facts.ParenCloseEndInner > 0
                    && m.PointWidth < Unbounded
                    && m.FlatWidth < Unbounded
                    && m.Column + m.PointWidth + 1 > m.ContinuationColumn + 2
                    && m.Column + m.PointWidth + 1 + facts.ParenCloseEndInner == width + 1
                    && Fits(m.ContinuationColumn, facts.ParenCloseEnd)) {
                    return ResolvedMode.Broken;
                }

                // ⚠ A bare doubled `((a + b…))`, cast or not, by the single parenthesis's measured form two columns
                // tighter (#598, SK-DIV-0770). See BareParenEqualsBreaks.
                if (facts.BareParenDoubled
                    && m.PointWidth < Unbounded
                    && m.Column + m.PointWidth + 1 + facts.BareParenWidth > width
                    && BareParenEqualsBreaks(facts, m) is { } doubled) {
                    return doubled ? ResolvedMode.Broken : ResolvedMode.Flat;
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

                // ⚠ A head of five columns or fewer keeps its `=` once the value's type argument list closes past the
                // margin, and the list fills (#610). See GroupFacts.ShortHeadTypeEnd.
                // ⚠ And on a line one column over, though the value would fit below.
                if (facts.ShortHeadTypeEnd > 0
                    && m.PointWidth < Unbounded
                    && (!Fits(m.Column, m.PointWidth + 1 + facts.ShortHeadTypeEnd)
                        || m.FlatWidth < Unbounded
                        && m.Trailing < Unbounded
                        && m.Column + m.FlatWidth + m.Trailing == width + 1)) {
                    return ResolvedMode.Flat;
                }

                // ⚠ A field's modifiers and its generic type: the type fills on the modifiers' line by the measured
                // rule rather than moving below them. See GroupFacts.ModifierFillHead (#540).
                if (facts.ModifierFillName > 0 && FillsAfterTheModifiers(facts, lineStart, m.Column)) {
                    return ResolvedMode.Flat;
                }

                // ⚠ `operand is N.T`: the pattern's own dot takes the line before every other group of the type test
                // does, which stay flat when it does. See GroupFacts.PatternDotName (#621).
                if (facts.PatternDotName > 0) {
                    // A head of five or less never breaks its `=` before the type test (#621: none of 705 cells past
                    // the margin).
                    if (facts.PatternDotEquals && facts.PatternDotHead <= 5) {
                        return ResolvedMode.Flat;
                    }

                    var patternDot = ThePatternDotTakesTheBreak(facts, m, lineStart);
                    if (facts.PatternDotPoint) {
                        return patternDot ? ResolvedMode.Broken : ResolvedMode.Flat;
                    }

                    if (patternDot) {
                        return ResolvedMode.Flat;
                    }
                }

                // ⚠ A returned type test's operand: the dot takes the break by the measured rule, and the keyword's
                // band in front of it, entered first, stays flat when it does. See GroupFacts.TypeTestTail (#446).
                if (facts.TypeTestTail > 0) {
                    var dot = m.FlatWidth < Unbounded && TheDotTakesTheBreak(facts, m.Column, lineStart);
                    var twoLevels = m.FlatWidth < Unbounded && TheDotGoesTwoLevelsIn(facts, m.Column, lineStart);

                    // ⚠ The type test's own group, the point after the keyword: flat where the dot's line holds the
                    // type, broken where the dot goes two levels in, and the operand nests past it then.
                    if (facts.TypeTestAfterKeyword) {
                        // ⚠ And flat behind a dot the author broke before, the only break TypeTestTail allows: that is
                        // the dot's line one level in, as pass one wrote it.
                        if (dot || m.FlatWidth >= Unbounded) {
                            return ResolvedMode.Flat;
                        }

                        if (twoLevels) {
                            return ResolvedMode.Broken;
                        }
                    } else if (facts.KeywordWidth == 0) {
                        return dot || twoLevels ? ResolvedMode.Broken : ResolvedMode.Flat;
                    } else if (dot) {
                        return ResolvedMode.Flat;
                    } else if (twoLevels) {
                        return TheKeywordLeavesTheDotsLine(facts, lineStart) ? ResolvedMode.Broken : ResolvedMode.Flat;
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

                // ⚠ Where the line through the held call fits and only the rest of the chain overflows, the `=` breaks
                // when the value fits below by a limit of its own, and stays otherwise (#619 round 7): the ordering rule
                // answered with a limit read from the indent. See HeldChainThroughBreaksTheEquals.
                if (facts is { HeldChainReceiver: > 0, EqualsName: > 0 }
                    && m.PointWidth < Unbounded
                    && m.FlatWidth < Unbounded
                    && tail < Unbounded
                    && m.Trailing < Unbounded
                    && Fits(m.Column, m.FlatWidth + m.Trailing - facts.HeldChainRest)
                    && !Fits(m.Column, m.FlatWidth, m.Trailing)) {
                    return HeldChainThroughBreaksTheEquals(
                        facts,
                        m.Column + m.PointWidth,
                        m.FlatWidth + m.Trailing - facts.HeldChainRest - m.PointWidth - 1,
                        m.ContinuationColumn + tail + m.Trailing,
                        lineStart
                    )
                        ? ResolvedMode.Broken
                        : ResolvedMode.Flat;
                }

                // ⚠ An `=` before a held lambda call's chain asks the plain member value's two questions, with the
                // receiver for the fragment the held call would leave beside it (#619). Only where the value fits
                // below whole, and only on a line the group can measure whole, as above. See
                // GroupFacts.HeldChainReceiver.
                if (facts is { HeldChainReceiver: > 0, EqualsName: > 0 }
                    && m.PointWidth < Unbounded
                    && m.FlatWidth < Unbounded
                    && tail < Unbounded
                    && m.Trailing < Unbounded
                    && !Fits(m.Column, m.FlatWidth + m.Trailing - facts.HeldChainRest)
                    && Fits(m.ContinuationColumn, tail, m.Trailing)) {
                    // ⚠ Not the plain member's rule read a column short (round 2): the held chain's own (#619 round 4).
                    // ⚠ And where group I's fragment is short, round 4's edge was group I's plateau seen through the value's
                    // end below: the plateau answers it (#619 round 6, HeldChainBandBreaks), and a line through the held
                    // call one column over its own (HeldChainOneOverBreaks).
                    if (HeldChainFragmentIsShort(
                            facts.HeldChainReceiver,
                            facts.EqualsName,
                            m.Column + m.PointWidth + 1 - lineStart
                        )) {
                        return (HeldChainCallIsOneOver(facts, m)
                            ? HeldChainOneOverBreaks(facts, m)
                            : HeldChainBandBreaks(facts, m))
                            ? ResolvedMode.Broken
                            : ResolvedMode.Flat;
                    }

                    return HeldChainValueBreaksTheEquals(
                        facts,
                        m,
                        m.Column - lineStart,
                        m.Column + m.PointWidth + 1 - lineStart,
                        m.Column + m.PointWidth,
                        m.ContinuationColumn + tail + m.Trailing
                    )
                        ? ResolvedMode.Broken
                        : ResolvedMode.Flat;
                }

                // ⚠ Where the value does not fit below, the `=` still breaks — `=` / `r.Where(…)` / `.ToList();` —
                // when the held call would move below behind it and the receiver is short against the name (#619,
                // group I). See HeldChainBreaksTheEquals.
                if (facts is { HeldChainReceiver: > 0, EqualsName: > 0, HeldChainArrow: > 0, HeldChainCallHead: > 0 }
                    && m.PointWidth < Unbounded
                    && m.FlatWidth < Unbounded
                    && tail < Unbounded
                    && m.Trailing < Unbounded
                    && !Fits(m.Column, m.FlatWidth + m.Trailing - facts.HeldChainRest)
                    && !Fits(m.ContinuationColumn, tail, m.Trailing)
                    && HeldChainBreaksTheEquals(facts, m, lineStart)) {
                    return ResolvedMode.Broken;
                }

                // ⚠ A lambda-valued local's type/name gap breaks when the line through its `=` overflows: pass
                // one chopped the parameter list behind an `=` past the margin, and pass two, reading the chop
                // as the author's, filled the type (Nightly replay 13830403873739157460). See
                // GroupFacts.NameThroughEquals.
                if (facts.NameThroughEquals > 0 && !Fits(m.Column, 1 + facts.NameThroughEquals)) {
                    return ResolvedMode.Broken;
                }

                // ⚠ A local's type/name gap before a lambda over a call, one column past the margin (#453 round 8).
                // See GroupFacts.OneOverCallArguments.
                if (facts.OneOverCallArguments > 0
                    && m.PointWidth < Unbounded
                    && m.FlatWidth < Unbounded
                    && m.Trailing < Unbounded
                    && m.Column + m.FlatWidth + m.Trailing == width + 1
                    && facts.OneOverCallType >= EqualsFloor.TypeNameOneOverType(facts.OneOverCallName)
                    && (facts.OneOverCallSingle
                        || facts.OneOverCallArguments <= 35 + (facts.OneOverCallType - 32) / 12)) {
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
                        && (facts.OneOverParen == 0
                            || m.Column + facts.OneOverParen + 1 - 0.6 * lineStart > 80.2)
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

                // ⚠ A parenthesised query after an arm's arrow stays on the arrow's line, its `from` clause filling,
                // when it is wide against the arrow's line: `2·body > arrow + 88`, the arrow's end read from the start
                // of its own line, and only while `(from item` fits beside it (#576 round 9). See
                // GroupFacts.ArmQueryBody.
                if (facts.ArmQueryBody > 0 && ArmQueryStays(facts, m, lineStart)) {
                    return ResolvedMode.Flat;
                }

                // ⚠ An object creation after an arm's arrow keeps `new` on the arrow's line and breaks after it by a
                // measured rule (#576 round 13). See ArmNewStays.
                // ⚠ Its head reads the line to that point, not to the arguments' `(`, so the rule answers both ways.
                if (facts.ArmNewBody > 0 && ArmNewStays(facts, m, lineStart) is { } stays) {
                    return stays ? ResolvedMode.Flat : ResolvedMode.Broken;
                }

                // ⚠ A wide-named local's `=` before a lambda over a call (#453 round 2): broken exactly by the
                // measured reach and floor, read from the planner's widths so that a kept break inside the lambda
                // on pass two does not change the answer. See GroupFacts.EqualsLambdaArguments.
                if (facts.EqualsLambdaArguments > 0 && m.PointWidth < Unbounded) {
                    // ⚠ The group starts at the `=` in a declaration and at the target in an assignment: the `=`
                    // ends where its point's head does.
                    var equalsEnd = m.Column + m.PointWidth;
                    var paren = equalsEnd + 1 + facts.EqualsLambdaValueHead + 1;
                    if (paren + facts.EqualsLambdaArguments <= width) {
                        return ResolvedMode.Flat;
                    }

                    // ⚠ A local one column past the margin (#453 round 8): under a type narrower than the type/name
                    // gap's threshold the `=` breaks wherever the arrow would, the gate aside.
                    // ⚠ Read from the planner's widths: on pass two a chopped argument list leaves the flat width
                    // unbounded, and the general rule below broke the `=` pass one had kept.
                    if (facts.EqualsLambdaIsLocal && paren + facts.EqualsLambdaArguments == width + 1) {
                        return facts.EqualsLambdaType < EqualsFloor.TypeNameOneOverType(facts.EqualsLambdaName)
                            && facts.EqualsLambdaType <= 8 * (facts.EqualsLambdaName - 10) + 14
                            && (facts.EqualsLambdaSingle
                                || paren
                                >= equalsEnd
                                + 36
                                - 1.6 * (facts.EqualsLambdaName - 11)
                                + 0.25 * (facts.EqualsLambdaType - 14))
                                ? ResolvedMode.Broken
                                : ResolvedMode.Flat;
                    }

                    return EqualsFloor.BreaksBeforeALambdaCall(
                        facts.EqualsLambdaName,
                        facts.EqualsLambdaType,
                        equalsEnd,
                        paren,
                        facts.EqualsLambdaArguments,
                        facts.EqualsLambdaReach > 0,
                        facts.EqualsLambdaSingle,
                        lineStart
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
                    return EqualsFloor.BreaksBeforeAPattern(
                        facts.PatternHead,
                        facts.PatternWidth,
                        end,
                        facts.PatternFirstAlternative,
                        lineStart
                    )
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
                    && (facts.CalleeOwner != EqualsOwner.Field || m.ContinuationColumn - indentWidth == 4)
                    && EqualsBeforeACall(facts, m, lineStart) is { } callMode) {
                    return callMode;
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

                // ⚠ See GroupFacts.BreaksIfItOverflows: a receiver's own dots (#582).
                // ⚠ With a comment after the receiver counted: `source.A… /** d */` / `.Select(…)` chops at 121 as
                // a bare receiver would (#631). Nothing else stands between the receiver and the held call's dot.
                if (facts.BreaksIfItOverflows) {
                    return Fits(m.Column, m.FlatWidth, m.Trailing) ? ResolvedMode.Flat : ResolvedMode.Broken;
                }

                // ⚠ A single call on a parenthesised receiver: broken exactly when the line overflows and the
                // receiver fits on it. See GroupFacts.ParenReceiver (#598).
                // ⚠ A call with arguments leaves the receiver that fits to the held call's rule below: the dot breaks
                // only once its `(` lands past the margin, and the arguments chop otherwise (#598, round 8).
                if (facts.ParenReceiver
                    && !(facts.ParenReceiverHeld
                        && m.FlatWidth < Unbounded
                        && tail < Unbounded
                        && Fits(m.Column, m.FlatWidth - tail))) {
                    if (m.FlatWidth >= Unbounded || tail >= Unbounded || m.Trailing >= Unbounded) {
                        return ResolvedMode.Flat;
                    }

                    // ⚠ And a receiver too wide for its line breaks inside, the dot with it when the receiver's last
                    // line still overflows with the call (#617). The operator then nests a level past the dot's
                    // line (BreakPlan.ReceiverLiftOf). See GroupFacts.ParenReceiverTail.
                    if (!Fits(m.Column, m.FlatWidth - tail)) {
                        // ⚠ With arguments, the receiver's last line keeps the call while its `(` fits there: the
                        // arguments chop, `+ b…).M(` / `x` / `);` (#598, round 8).
                        // ⚠ Except on a line one column over, where the dot breaks, as below a broken `=` (#598,
                        // SK-DIV-0831).
                        return facts.ParenReceiverTail > 0
                            && (facts.ParenReceiverHeld && facts.HeldCallHead > 0
                                ? !Fits(lineStart + indentWidth, facts.ParenReceiverTail + facts.HeldCallHead)
                                || facts.ParenReceiverArguments <= 10
                                && !Fits(lineStart + indentWidth, facts.ParenReceiverTail + tail, m.Trailing)
                                && Fits(lineStart + indentWidth, facts.ParenReceiverTail + tail - 1, m.Trailing)
                                : !Fits(lineStart + indentWidth, facts.ParenReceiverTail + tail, m.Trailing))
                                ? ResolvedMode.Broken
                                : ResolvedMode.Flat;
                    }

                    return Fits(m.Column, m.FlatWidth, m.Trailing) ? ResolvedMode.Flat : ResolvedMode.Broken;
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
                    // ⚠ The single column moves the dot only while the `(` lands far enough right of where the value
                    // starts: `10·paren − 3·column ≥ 871`, the `(` 1-based, the value's column 0-based — beside a kept
                    // `=` and below a broken one alike. Short of it the oracle chops (#446, SK-DIV-0211 round 7).
                    // ⚠ On a parenthesised operator, two arguments move the dot one column over too, while they are ten
                    // columns or less (#598, SK-DIV-0831).
                    if (facts.HeldCall >= 3) {
                        return Fits(m.Column, receiver)
                            && (paren > width
                                || facts.HeldCall >= 3
                                && m.Column + m.FlatWidth + m.Trailing == width + 1
                                && OneOverMovesTheDot(paren, m.Column, facts.HeldCall == 4)
                                || facts.HeldCall == 4
                                && facts.ParenReceiverHeld
                                // ⚠ Wider arguments move it too once the receiver is 71 columns or more (#598, round
                                // 12, d3: 0 of 64 one-over rows either side of it, arguments of 16 and 18 alike).
                                && (facts.ParenReceiverArguments <= 10 || receiver >= 71)
                                && m.Column + m.FlatWidth + m.Trailing == width + 1
                                || facts.HeldCall == 3
                                && facts.ParenReceiverHeld
                                && !Fits(m.Column, m.FlatWidth, m.Trailing)
                                && ShortArgumentMovesTheDot(facts.ParenReceiverArguments, paren, m.Column, lineStart))
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
                    // ⚠ A lambda call whose line overflows by its `)` alone, behind a head and receiver of 16
                    // columns or fewer, stays and puts the `)` on a line of its own (SK-DIV-0420's lone `)`):
                    // measured at indents 8 and 12, heads of 8 to 21, every row switching at 16 exactly.
                    // ⚠ Not with a rest after the call, behind a head and receiver of twelve or more: there the oracle moves
                    // the call by a limit of its own (#619 round 6). See HeldLambdaOneOverMoves.
                    if (facts.HeldCallLambda
                        && facts.HeldCallLambdaRest > 0
                        && m.Column + receiver + tail == width + 1
                        && m.Column - lineStart + receiver >= 12) {
                        return HeldLambdaOneOverMoves(
                            m.Column,
                            facts.HeldCallHead,
                            facts.HeldCallLambdaRest,
                            line
                        )
                            ? ResolvedMode.Broken
                            : ResolvedMode.Flat;
                    }

                    if (facts.HeldCallLambda
                        && Fits(m.Column, m.FlatWidth - 1)
                        && m.Column - lineStart + receiver <= 16) {
                        return ResolvedMode.Flat;
                    }

                    // ⚠ A lambda call stays behind a head and receiver of eleven columns or fewer, whatever the rest (#619):
                    // `r.Where(x =>` below a broken `=` keeps its call and breaks the arrow, where the rest's term moved it
                    // below `r` again. See HeldLambdaLine. ⚠ Within the margin too (round 6): below a broken `=` the oracle
                    // never moved the call in 29 573 statements of the #619 probes and grids, and behind a kept `=` in none
                    // of 57; Skala moved 109 of the first where the line reached the limit.
                    if (facts.HeldCallLambda && m.Column - lineStart + receiver < 12) {
                        return ResolvedMode.Flat;
                    }

                    // ⚠ A lambda call's limit moves with the rest of its chain (#605). See
                    // GroupFacts.HeldCallLambdaRest. ⚠ And falls a fiftieth of a column per column of the lambda's
                    // head past `x =>`'s four (GroupFacts.HeldCallLambdaArrow).
                    var terms = (facts.HeldCallLambdaRest > 0 ? (facts.HeldCallLambdaRest - 10) / 1.5 : 0)
                        - Math.Max(0, facts.HeldCallLambdaArrow - 4) / 50.0;
                    // ⚠ A lambda call with a rest after it, behind a head and receiver of twelve or more, reads its line below
                    // no further than 113 plus its head — where its body line below a kept call would reach column 111 — and
                    // against one limit within the margin and past it (#619 round 6, HeldLambdaSaturation): rounds 3 to 5's
                    // readings past the margin were this saturation seen at `.Where(`'s 120.
                    var saturates = facts.HeldCallLambda
                        && facts.HeldCallLambdaRest > 0
                        && m.Column - lineStart + receiver >= 12;
                    var limit = !facts.HeldCallLambda
                        ? HeldCallLimit(paren, facts.HeldCall)
                        : saturates
                            ? HeldLambdaLimitWithin(m.Column, receiver, facts.HeldCallHead) + terms
                            : HeldLambdaLimitPast(m.Column, receiver, facts.HeldCallHead, line) + terms;
                    var read = saturates
                        ? Math.Min(line, HeldLambdaSaturation(facts.HeldCallHead))
                        : HeldLambdaLine(
                            facts.HeldCallLambda,
                            facts.HeldCallLambdaRest,
                            m.Column - lineStart + receiver,
                            facts.HeldCallHead,
                            line
                        );

                    // ⚠ Behind a local's or an assignment's `=`, a call whose limit leaves the whole value ending before column
                    // 113 below never moves: the `=` breaks or the arrow does (#619 round 7). See
                    // HeldLambdaStaysBesideTheEquals.
                    if (saturates
                        && facts.HeldCallAfterEquals
                        && paren + facts.HeldCallLambdaArrow <= width
                        && HeldLambdaStaysBesideTheEquals(receiver, limit, facts.HeldCallLambdaRest)) {
                        return ResolvedMode.Flat;
                    }

                    return Fits(m.Column, receiver)
                        && !Fits(m.Column, m.FlatWidth)
                        && (read <= limit
                            // ⚠ Past the margin the line is also read as it is, by #605's limit: either reading moves
                            // the call (#619, HeldLambdaMovesAsItIs).
                            || facts.HeldCallLambda
                            && !saturates
                            && HeldLambdaMovesAsItIs(m.Column, receiver, facts.HeldCallHead, line, terms)
                            // ⚠ A lambda call whose held line through the arrow would overflow moves below while
                            // it fits there (GroupFacts.HeldCallLambdaArrow).
                            || facts.HeldCallLambda
                            && facts.HeldCallLambdaArrow > 0
                            && paren + facts.HeldCallLambdaArrow > width
                            && line <= width
                            || facts.HeldCallRest > 0
                            && line <= width
                            && facts.HeldCallRest > 1.5 * (line - limit) + 9)
                            ? ResolvedMode.Broken
                            : ResolvedMode.Flat;
                }

                // ⚠ Broken exactly when only the terminator overflows. See GroupFacts.Terminator.
                if (facts.Terminator > 0) {
                    // ⚠ A field's commented attribute gap declines only up to a measured line. See
                    // GroupFacts.DeclineHead and EqualsFloor.DeclinesTheJoin.
                    if (facts.DeclineHead > 0
                        && m.PointWidth < Unbounded
                        && m.FlatWidth < Unbounded
                        && m.Trailing < Unbounded
                        && !Fits(m.Column, m.FlatWidth, m.Trailing)) {
                        return EqualsFloor.DeclinesTheJoin(
                            m.Column + m.FlatWidth + m.Trailing,
                            facts.DeclinePrefix,
                            facts.DeclineHead,
                            facts.DeclineName
                        )
                            ? ResolvedMode.Broken
                            : ResolvedMode.Flat;
                    }

                    return !Fits(m.Column, m.BreakWidth) && Fits(m.Column, m.BreakWidth - facts.Terminator)
                        ? ResolvedMode.Broken
                        : ResolvedMode.Flat;
                }

                // ⚠ A switch arm's pattern fill: by the measured table. See GroupFacts.ArmHead.
                if (facts.ArmHead > 0) {
                    return ArmFills(facts, m) ? ResolvedMode.Broken : ResolvedMode.Flat;
                }

                // ⚠ An `=` before a single call: by the measured table. See GroupFacts.HeldValue.
                // ⚠ A cast property whose receiver fits beside a wide head's `=` has a measured band of its own; where
                // that band does not decide, master's ordering rule below still answers (#598, SK-DIV-0575 round 8).
                if (facts.HeldValue > 0) {
                    if (!(facts.HeldValueProperty && HeldReceiverFitsBeside(facts, m))) {
                        return HeldValueBreaks(facts, m) ? ResolvedMode.Broken : ResolvedMode.Flat;
                    }

                    if (CastPropertyBesideAWideHeadBreaks(facts, m) is { } property) {
                        return property ? ResolvedMode.Broken : ResolvedMode.Flat;
                    }
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

                // ⚠ A sole lambda over a chain of properties that fits neither beside its arrow nor below it (#620,
                // SK-DIV-0762): by the measured line. One the author broke inside keeps the arrow.
                if (facts.SolePropertyChain is { } chain
                    && m.ContinuationColumn + chain.Sum() + chain.Length - 1 > width) {
                    return !facts.SolePropertyChainKept
                        && m.Column - lineStart >= 12
                        && BreaksThePropertyChainArrow(chain, facts.SoleCallLambda, m)
                            ? ResolvedMode.Broken
                            : ResolvedMode.Flat;
                }

                // ⚠ And where it fits below (#620 round four, SK-DIV-0881): the same line breaks it; past it, as a chain of
                // calls, the arrow stays where the line below would end at 112 or later, and by BelowBandMode short of
                // that. Measured on 32 184 cells (seven parameter lists, four segment widths, three roots, lambdas 4 to
                // 40 columns past the line's start); the segments and the root play no part beyond the fill's width.
                // A chain broken inside — pass one's own kept arrow over a filled chain — keeps it; one the line would
                // break is left to the older rules, as SoleChainMode leaves it.
                if (facts.SolePropertyChain is { } fitting
                    && m.Column - lineStart >= 12
                    && SolePropertyChainBelowMode(fitting, facts.SoleCallLambda, m, lineStart) is { } propertyBelow
                    && !(facts.SolePropertyChainKept && propertyBelow == ResolvedMode.Broken)) {
                    return propertyBelow;
                }

                // ⚠ A sole lambda over a call that does not fit beside its arrow (#620, SK-DIV-0760): by the measured
                // line, ahead of every older rule for the same bodies. One the author broke inside keeps the arrow.
                // ⚠ Except a keep behind an arrow ending sixty columns or more in, over a call that fits on the line
                // below: there the oracle's line bends with the arguments' width in a way not measured, and the
                // older rules agree with it more often (a fresh probe's 21 members newly wrong, none once deferred).
                if (facts.SoleCallHead > 0
                    && m.Column + 1 + facts.SoleCallHead + facts.SoleCallArguments + 1 > width) {
                    if (facts.SoleCallKept) {
                        return ResolvedMode.Flat;
                    }

                    if (BreaksTheSoleCallArrow(facts, m, lineStart)) {
                        return ResolvedMode.Broken;
                    }

                    if (!SoleCallKeepDefers(facts, m)) {
                        return ResolvedMode.Flat;
                    }
                }

                // ⚠ A sole lambda over a one-argument call with links after it (#620): the arrow breaks whenever the
                // call fits below and not beside it. See GroupFacts.LambdaLinkedCall.
                if (facts.LambdaLinkedCall > 0
                    && !facts.LambdaLinkedCallKept
                    && m.Column + 1 + facts.LambdaLinkedCall > width
                    && m.ContinuationColumn + facts.LambdaLinkedCall <= width) {
                    return ResolvedMode.Broken;
                }

                // ⚠ Pass two of a held typed local whose `=` stayed and whose arguments chopped: the first pass's
                // question again, on the flat widths. See GroupFacts.HeldChoppedWidth.
                // ⚠ Behind a head of twelve or more on a named method, a chop pass one kept by the limit or the
                // one-argument gate stays (#446, SK-DIV-0211 round 11): round 4's rule broke the `=` the gate had kept.
                // ⚠ Only that direction. The oracle keeps an author's chop the flat line would have moved below
                // (`PatternSyntax pattern = SyntaxFactory.ConstantPattern(` / … / `);` in Skala's own source), so a
                // chop inside the limit is left to the rule below as before.
                if (facts is { HeldChoppedWidth: > 0, HeldValueName: > 0, HeldValueTypedHead: >= 12 }
                    && m.PointWidth < Unbounded
                    && m.Column + m.PointWidth + 1 + facts.HeldChoppedHead <= width) {
                    var callee = facts.HeldChoppedHead - 1;
                    var margin = ReceiverListMargin(
                        facts.HeldChoppedWidth - facts.HeldChoppedHead - 2,
                        callee,
                        m.ContinuationColumn - indentWidth,
                        Math.Max(0, facts.HeldValueTypedHead - facts.HeldValueName - 2),
                        facts.HeldValueName,
                        true
                    );
                    if (!InsideTheListBand(margin)
                        && (margin < 0
                            || !OneArgumentGateBreaks(
                                m.Column + m.PointWidth + 1 + facts.HeldChoppedHead,
                                callee,
                                facts.HeldValueName
                            ))) {
                        return ResolvedMode.Flat;
                    }
                }

                if (facts.HeldChoppedWidth > 0
                    && m.PointWidth < Unbounded
                    && Fits(m.Column, m.PointWidth + 1 + facts.HeldChoppedHead)
                    && !EqualsFloor.HeldTypedLocalBreaks(
                        facts.HeldValueTypedHead,
                        m.ContinuationColumn + facts.HeldChoppedWidth,
                        facts.HeldValueCallee,
                        m.ContinuationColumn - indentWidth,
                        facts.HeldValueName
                    )) {
                    return ResolvedMode.Flat;
                }

                // ⚠ `v = X || Y` with `X` too wide beside the `=`: broken from a head floor that a wide `Y` lowers
                // (#579). See EqualsFloor.OrHeadFloor.
                if (facts.OrLeft > 0 && m.PointWidth < Unbounded && !Fits(m.Column, m.PointWidth + 1 + facts.OrLeft)) {
                    return facts.OrHead >= EqualsFloor.OrHeadFloor(facts.OrLeft, facts.OrRight, facts.OrLeftIsPattern)
                        ? ResolvedMode.Broken
                        : ResolvedMode.Flat;
                }

                // ⚠ A switch arm whose line runs exactly one column past the margin breaks after its arrow once the
                // head is wide enough for the body's kind (#559, SK-DIV-0449). See GroupFacts.ArmOneOverHead.
                if (facts.ArmOneOverHead > 0
                    && m.Column - lineStart >= facts.ArmOneOverHead
                    && m.Column > lineStart
                    && !afterPointRunsToTheEnd
                    && m.FlatWidth < Unbounded
                    && m.Trailing < Unbounded
                    && m.Column + m.FlatWidth + m.Trailing == width + 1
                    && Fits(m.ContinuationColumn, m.FlatWidth, m.Trailing)) {
                    return ResolvedMode.Broken;
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
                    // ⚠ The floor is twelve columns past the line's start, not column 20 (#619).
                    return EqualsFloor.BreaksTheOperandArrow(
                        m.Column,
                        facts.LambdaOperandParameters,
                        facts.LambdaOperandFirst,
                        end,
                        facts.LambdaOperandPatternLeft,
                        lineStart + 12
                    )
                        ? ResolvedMode.Broken
                        : ResolvedMode.Flat;
                }

                // ⚠ A sole lambda argument over a broken type test of a call (#618). See GroupFacts.LambdaTestCall.
                if (facts.LambdaTestCall > 0) {
                    return BreaksTheTestArrow(facts, m.Column, lineStart) ? ResolvedMode.Broken : ResolvedMode.Flat;
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
                // ⚠ The floor was measured at indent 8 and falls a column per column of indent past it (group B):
                // on `T n = () => C…(xxx, yyy);` at indents 4 (a field), 8, 12 and 16 — head and `(` placed alike, the
                // arguments one column at a time, 7 002 cells — every row's switch sits exactly 8 − indent columns
                // from indent 8's at the same columns. Round 2's field shift (head +8, `(` +4) was this, fitted.
                if (facts.LambdaCallArguments > 0
                    && m.FlatWidth < Unbounded
                    && EqualsFloor.BreaksTheCallArrow(
                        m.Column - facts.LambdaHead - 1,
                        m.Column + facts.LambdaCallCallee + 2,
                        facts.LambdaCallArguments + lineStart - 8,
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
                if (facts.WhenTable) {
                    return WhenBreaks(facts, m, tail, lineStart) ? ResolvedMode.Broken : ResolvedMode.Flat;
                }

                if (facts.BreaksOnlyIfTailFits) {
                    // ⚠ A lambda's arrow over a call chain whose receiver runs past the margin beside it: no dot
                    // can end the line in time (fuzz 16278079796336422477). Kept, pass two read the dot's break
                    // as the author's and broke the arrow. See GroupFacts.BreaksIfReceiverOverflows.
                    if (ReceiverOverflows(facts, m)) {
                        return ResolvedMode.Broken;
                    }

                    // ⚠ A bare `(a + b…)` or `(T)(a + b…)` behind a wide head, by its measured form (#598,
                    // SK-DIV-0770).
                    if (facts.BareParenOperand > 0
                        && m.PointWidth < Unbounded
                        && HeadIsWideEnough(facts, m, lineStart)
                        && BareParenEqualsBreaks(facts, m) is { } bare) {
                        return bare ? ResolvedMode.Broken : ResolvedMode.Flat;
                    }

                    return TailFits(m, tail + facts.TailSpare) && HeadIsWideEnough(facts, m, lineStart)
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
                    // SK-DIV-0447, and #612): see CallConditionChops. Only a call that fits below: one that does not
                    // is the `=` column's question, as before.
                    if (facts.ValueHeadFitsBelow
                        && below
                        && facts.ValueHeadArguments > 0
                        && CallConditionChops(facts, m)) {
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
    ///     A call condition that fits below its conditional's <c>=</c>: whether the oracle keeps the <c>=</c> and chops
    ///     the call instead (#596's residue, SK-DIV-0447; #612).
    /// </summary>
    /// <remarks>
    ///     ⚠ Two measured regimes, split at a callee of ten columns through its <c>(</c>. Up to ten, #596's fit:
    ///     9·(the call's end below) + 2·(the equals sign's end) + 64·(argument count) &gt; 1136, measured on 712
    ///     locals with short callees and long arguments. Past ten its argument count is a proxy for argument width
    ///     (#612): measured 2026-10-10 on 1 444 typed locals with the name held fixed — callee 8 to 64, argument text
    ///     10 to 90 over one to four arguments, the conditional's branches 16 to 56, the <c>=</c> at 44 to 100 — the
    ///     oracle chops while 8·(argument width) − 2·(the equals sign's end) − callee − 7·(branches) + 160·(two or
    ///     more arguments) &gt; 424
    ///     (1 399 of 1 444), and a fresh probe varying the name too agreed on 547 of 572
    ///     against 425 before. A single argument almost never chops; long branches make the call's own line the
    ///     cheaper break.
    /// </remarks>
    static bool CallConditionChops(in GroupFacts facts, in Measures m) {
        var equals = m.Column + m.PointWidth;
        if (facts.ValueHeadCallee <= 10 || facts.ValueHeadTail == 0) {
            return 9 * (m.ContinuationColumn + facts.ValueHeadWidth) + 2 * equals + 64 * facts.ValueHeadArguments
                > 1136;
        }

        var arguments = facts.ValueHeadWidth - facts.ValueHeadCallee - 1;
        return 8 * arguments
            - 2 * equals
            - facts.ValueHeadCallee
            - 7 * facts.ValueHeadTail
            + (facts.ValueHeadArguments >= 2 ? 160 : 0)
            > 424;
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

    /// <summary>
    ///     Whether an <c>=</c> before a held lambda call's chain, whose value does not fit below, breaks and leaves the
    ///     receiver with its call on the line below, <c>=</c> / <c>r.Where(…)</c> / <c>.ToList();</c>, rather than
    ///     staying and moving the call below the receiver (#619, group I).
    /// </summary>
    /// <remarks>
    ///     ⚠ Measured with <c>Testing ask</c> on <c>T n… = r….Where(x =&gt; a &amp;&amp; b &amp;&amp; c).T…();</c>
    ///     under <c>var</c>, <c>IEnumerable&lt;T&gt;</c> and assignment heads, names of 4 to 40 and receivers of 1 to
    ///     9, where the held call always moves (1 998 cells): every cell answers by the name and the receiver alone,
    ///     the same at two widths of the line below, and the head decides only whether the receiver starts 13 columns
    ///     or more into the statement. The <c>=</c> breaks while three receivers fit in the name less one and eight
    ///     receivers and five names in 165 — the receiver allowed rises to six columns at names of 19 to 23 and falls
    ///     to none past 31. Where the held call would break its arrow instead, the <c>=</c> stays (1 260 cells under
    ///     <c>.ToList()</c>).
    ///     ⚠ A column or two past the held limit the oracle still breaks the <c>=</c> under the shortest receivers;
    ///     that band is not taken (SK-DIV-0510).
    ///     ⚠ <c>r.Where(…)</c> need not fit below (#619 round 4): within the limit the oracle breaks the <c>=</c> and then
    ///     the arrow below it, where the moved call's line ends at 119 to 121 and the receiver pushes it past the margin
    ///     — 34 statements of two random probes, none newly wrong.
    /// </remarks>
    bool HeldChainBreaksTheEquals(in GroupFacts facts, in Measures m, int lineStart) {
        var receiver = facts.HeldChainReceiver;
        var column = m.Column + m.PointWidth + 1;
        if (!HeldChainFragmentIsShort(receiver, facts.EqualsName, column - lineStart)) {
            return false;
        }

        // The held call's own decision, as GroupFacts.HeldCallLambda answers it with the `=` kept: its line below
        // runs from the chain's continuation through the call's `)`.
        var callEnd = m.Column + m.FlatWidth + m.Trailing - facts.HeldChainRest;
        var line = m.ContinuationColumn + callEnd - (column + receiver);
        var terms = (facts.HeldChainRest - 10) / 1.5 - Math.Max(0, facts.HeldChainArrow - 4) / 50.0;

        // ⚠ A line through the held call one column over answers by a limit of its own, the held limit playing no part
        // (#619 round 6). See HeldChainOneOverBreaks.
        if (HeldChainCallIsOneOver(facts, m)) {
            return HeldChainOneOverBreaks(facts, m);
        }

        // ⚠ Past the held limit the `=` still breaks, up to a plateau of its own, within the margin and past it alike
        // (#619 round 6). See HeldChainBandBreaks.
        var band = HeldChainBandBreaks(facts, m);

        // ⚠ Rounds 3 to 5 read a held line past the margin as the margin, and as it is. Both were the saturation: the line
        // is read no further than 113 plus the call's head, within the margin and past it (#619 round 6).
        return Math.Min(line, HeldLambdaSaturation(facts.HeldChainCallHead))
            <= HeldLambdaLimitWithin(column, receiver, facts.HeldChainCallHead) + terms
            || band;
    }

    /// <summary>
    ///     Whether group I's <c>=</c> breaks by <see cref="HeldChainBand" />'s plateau: the held call's line below a kept
    ///     <c>=</c>, read no further than 113 plus the call's head, against the plateau.
    /// </summary>
    static bool HeldChainBandBreaks(in GroupFacts facts, in Measures m) {
        var column = m.Column + m.PointWidth + 1;
        var callEnd = m.Column + m.FlatWidth + m.Trailing - facts.HeldChainRest;
        var line = m.ContinuationColumn + callEnd - (column + facts.HeldChainReceiver);
        return Math.Min(line, HeldLambdaSaturation(facts.HeldChainCallHead))
            <= HeldChainBand(facts, column - facts.EqualsName - 3);
    }

    /// <summary>
    ///     Group I's <c>=</c> where the line through the held call, behind a kept <c>=</c>, is one column over (#619 round 6):
    ///     the oracle breaks the <c>=</c>, or keeps everything and gives the <c>)</c> a line of its own.
    /// </summary>
    /// <remarks>
    ///     ⚠ Neither the held limit nor the plateau separates these: the plateau less 3.7 comes within 0.03 and crosses.
    ///     Measured on 4 000 random statements built one column over (six heads, indents 8 to 20, names of 3 to 31, receivers
    ///     of 1 to 6, fourteen callees, four tails, rests of 8 to 42, three bodies) and every such statement of the other
    ///     #619 probes and grids, 4 478 in all, the exact largest-margin line separates every one by 0.03 of a column: the
    ///     <c>=</c> breaks while the held call's line below a kept <c>=</c> is within 110.284, less 0.4076 a column of
    ///     receiver and 0.1187 of the name's start, plus 0.0086 of name, 0.6218 of call head past seven and 0.6512 of rest
    ///     past ten. One column over, that line is 125 less the receiver's reach. The 28 statements where the oracle instead
    ///     moves the call keep the `=` and are <see cref="HeldLambdaOneOverMoves" />'s.
    /// </remarks>
    static bool HeldChainOneOverBreaks(in GroupFacts facts, in Measures m) {
        var column = m.Column + m.PointWidth + 1;
        var callEnd = m.Column + m.FlatWidth + m.Trailing - facts.HeldChainRest;
        var line = m.ContinuationColumn + callEnd - (column + facts.HeldChainReceiver);
        var start = column - facts.EqualsName - 3;
        return line
            <= 110.283966
            - 0.407589 * facts.HeldChainReceiver
            - 0.118727 * start
            + 0.621787 * (facts.HeldChainCallHead - 7)
            + 0.651163 * (facts.HeldChainRest - 10)
            + 0.008568 * facts.EqualsName;
    }

    /// <summary>Whether the line through a held chain's call, behind a kept <c>=</c>, is one column over the margin.</summary>
    bool HeldChainCallIsOneOver(in GroupFacts facts, in Measures m) =>
        m.Column + m.FlatWidth + m.Trailing - facts.HeldChainRest == width + 1;

    /// <summary>
    ///     How far group I's <c>=</c> keeps breaking past the held limit (#619 round 6): the line below up to which the
    ///     oracle breaks the <c>=</c> before a held lambda call's chain whose value does not fit below, where the held
    ///     call itself would stay and break its arrow.
    /// </summary>
    /// <param name="start">The name's 0-based column: the statement's indent and its head (<c>var </c>, a type).</param>
    /// <remarks>
    ///     ⚠ Measured 2026-11-13 with <c>Testing ask</c> on 1 500 random series inside group I's region, each with the moved
    ///     line swept a column at a time across the held limit (10 058 statements: six heads, indents 8 to 20, names of 3
    ///     to 31, receivers of 1 to 6, fourteen callees of 4 to 15 columns, four tails, rests of 8 to 42, three bodies of 2
    ///     to 4 operands), with group I's band grid (22 280 statements) and 800 series swept past the margin to 138 (13 057).
    ///     The boundary is not the held limit read further out: it does not fall with the name the way the limit's column
    ///     term does. On names of 13 to 21 the least-squares residual is 0.29, a uniform column's rounding. Solved exactly
    ///     for the largest margin over every series and every statement of nine random probes (15 359 constraints), it
    ///     separates all of them by 0.0013 of a column: 114.516, less 0.4026 a column of receiver, 0.1394 of start, 0.0167
    ///     of name and 0.3684 of name past 22, plus 0.5974 a column of call head past 7, 0.6667 of rest past 10 and the held
    ///     limit's arrow term. ⚠ No small-denominator integer form exists: searched to a denominator of 100, two steps
    ///     either side of every coefficient, the best missed 28 series, and fixing the head at 0.6 or the receiver at 0.4
    ///     leaves none that separates. The coefficients are kept to six places because the margin is that thin.
    ///     ⚠ And the line is read no further than 113 plus the call's head, where the held call's body line below a kept
    ///     <c>=</c> would reach column 111: past that the oracle answers as it does there, so a plateau that reaches it
    ///     breaks the <c>=</c> to the margin and past it (no series stops past that point).
    ///     The same plateau decides the <c>=</c> where the value fits below (<see cref="HeldChainValueBreaksTheEquals" />).
    ///     The body, its operands and the tail play no part.
    /// </remarks>
    static double HeldChainBand(in GroupFacts facts, int start) {
        var name = facts.EqualsName;
        return 114.516032
            - 0.402578 * facts.HeldChainReceiver
            - 0.139440 * start
            + 0.597422 * (facts.HeldChainCallHead - 7)
            + 0.666650 * (facts.HeldChainRest - 10)
            - Math.Max(0, facts.HeldChainArrow - 4) / 50.0
            - 0.016693 * name
            - 0.368384 * Math.Max(0, name - 22);
    }

    /// <summary>
    ///     Group I's question for an <c>=</c> before a held lambda call's chain (#619): is the receiver short against the
    ///     name — three receivers within the name less one and eight receivers and five names within 165 — with the
    ///     receiver starting <paramref name="reach" /> columns into the statement, thirteen or more.
    /// </summary>
    static bool HeldChainFragmentIsShort(int receiver, int name, int reach) =>
        reach >= 13 && 3 * receiver <= name - 1 && 8 * receiver + 5 * name <= 165;

    /// <summary>
    ///     Whether an <c>=</c> before a held lambda call's chain whose value fits whole on the line below breaks, by the
    ///     name, the receiver, the head in front of the name and the value's end below (#619 round 4).
    /// </summary>
    /// <param name="head">The columns from the statement's start to the name: <c>var </c>'s four, none for an assignment.</param>
    /// <param name="reach">The columns from the statement's start to the receiver.</param>
    /// <param name="equals">The <c>=</c>'s 1-based column.</param>
    /// <param name="below">The 1-based column the value would end at below, its <c>;</c> included.</param>
    /// <remarks>
    ///     ⚠ Measured 2026-11-13 with <c>Testing ask</c> on <c>T n… = r….Where(x =&gt; a &amp;&amp; b).ToList();</c>, the
    ///     value's end below swept a column at a time: names of 3 to 40 and receivers of 1 to 14 under <c>var</c>,
    ///     assignment and <c>IEnumerable&lt;int&gt;</c> heads (12 964 cells), and names of 3 to 40, receivers of 1 to 12,
    ///     <c>.Where(</c> and <c>.SkipWhile(</c>, rests of 10 and 24 (20 800). The oracle's table is three regions, and
    ///     none of them reads the indent, the callee or the rest:
    ///     <list type="bullet">
    ///         <item>
    ///             where group I's fragment is short, the <c>=</c> breaks unless the value ends far right below:
    ///             <c>below + 0.14·equals + 0.24·name − 0.65·receiver − (callHead − 7) &gt; 132.7</c>, the <c>=</c>'s
    ///             1-based column counting the indent and the call's head from its dot through its <c>(</c> (9 708 more
    ///             cells at the edge, names of 18 to 32, receivers of 1 to 6, six heads, indents 8 and 16, with the two
    ///             grids: 5 of 9 747 apart; a column of call head moves the edge a column, measured under heads of 5 to 11);
    ///             the arrow then breaks beside the kept <c>=</c>. ⚠ That edge is group I's plateau seen through the value's
    ///             end, and the plateau answers it now (round 6, <see cref="HeldChainBandBreaks" />): on 6 227 cells
    ///             inside the region, the moved line swept across both, this edge misses 133 and the plateau 5. Only a
    ///             line through the held call one column over still reads this edge;
    ///         </item>
    ///         <item>where <c>9·name + 8·head − 3·receiver ≤ 153</c>, it never breaks;</item>
    ///         <item>
    ///             between them it breaks up to a value ending at <c>112</c>, less 0.382 a column of receiver, and less
    ///             0.245 a column of name past about 22 or 0.12 a column of name short of it.
    ///         </item>
    ///     </list>
    ///     The two grids miss 22 and 4 cells, against the 559 and 486 the plain member's rule read a column short
    ///     missed; two random probes written before the fit (indents 8 to 20, six heads, five tails, nine callees)
    ///     miss 3 of 520 and 1 of 554 such statements, against 23 and 32. ⚠ An assignment's name is its whole target,
    ///     <c>this.n…</c> included (<see cref="GroupFacts.EqualsName" />), and its head none.
    ///     ⚠ Round 7 (2026-11-14): the third region's limit sat 0.002 to 0.02 above a whole column for four pairs of name
    ///     and receiver, and there the oracle stops a column sooner — <c>List&lt;int&gt; n12 = r9</c>'s 111.002 is 110, at
    ///     every head, indent, callee and rest (22 such pairs, 40 random contexts each, 3 520 statements: every pair answers
    ///     alike in all 40). Solved again exactly over those and every third-region statement of the round's probes, each
    ///     piece takes a receiver term of its own: <c>113.0366 + 0.12195·name − 0.39024·receiver</c> and
    ///     <c>121.0965 − 0.24561·name − 0.38597·receiver</c>, by margins of 0.012 and 0.009 (names to 45). It moves five
    ///     pairs a statement can reach, the four above and <c>n42 r2</c> (109 to 110), and a probe of nine pairs around
    ///     them (1 080 statements) agrees; a first solve without that probe moved <c>n20 r14</c>, which the oracle keeps.
    ///     Past names of 45 round 4's pieces stand (names of 53 to 58 behind an <c>=</c> past column 84 answer otherwise).
    ///     ⚠ And where the held call could not move (<see cref="HeldLambdaStaysBesideTheEquals" />) under the held limit's
    ///     cap, the <c>=</c> breaks only within group I's plateau (<see cref="HeldChainBand" />); past it the arrow breaks.
    ///     Under the floor the plateau, fitted on names of 31 or less, runs short for the longest names and is not read.
    /// </remarks>
    static bool HeldChainValueBreaksTheEquals(
        in GroupFacts facts,
        in Measures m,
        int head,
        int reach,
        int equals,
        int below
    ) {
        var name = facts.EqualsName;
        var receiver = facts.HeldChainReceiver;
        if (HeldChainFragmentIsShort(receiver, name, reach)) {
            return below + 0.14 * equals + 0.24 * name - 0.65 * receiver - (facts.HeldChainCallHead - 7) <= 132.7;
        }

        if (9 * name + 8 * head - 3 * receiver <= 153) {
            return false;
        }

        // ⚠ Past names of 45, outside every grid of round 7, round 4's pieces stand: there the oracle's limit is not one
        // line in the name (an `=` past column 84 moves it), and the refit made 12 of 270 probed statements newly wrong.
        var limit = name > 45
            ? Math.Min(121.05 - 0.245 * name, 113 + 0.12 * name) - 0.382 * receiver
            : Math.Min(
                121.096491 - 0.245614 * name - 0.385965 * receiver,
                113.036585 + 0.121951 * name - 0.390244 * receiver
            );
        if (below > Math.Min(112, Math.Floor(limit))) {
            return false;
        }

        // ⚠ Where the held call could not move behind the kept `=` (HeldLambdaStaysBesideTheEquals), the `=` breaks only
        // within group I's plateau while the held limit's cap decides, and the arrow breaks past it (#619 round 7).
        var column = equals + 1;
        var held = HeldLambdaLimitWithin(column, receiver, facts.HeldChainCallHead)
            + (facts.HeldChainRest - 10) / 1.5
            - Math.Max(0, facts.HeldChainArrow - 4) / 50.0;
        return !(HeldLambdaStaysBesideTheEquals(receiver, held, facts.HeldChainRest)
            && HeldLambdaCapDecides(column, receiver, facts.HeldChainCallHead)
            && !HeldChainBandBreaks(facts, m));
    }

    /// <summary>
    ///     Whether an <c>=</c> before a held lambda call's chain breaks where the line through the held call fits and only
    ///     the rest of the chain overflows (#619 round 7): <c>T n… =</c> / <c>r….Where(x =&gt; …).T…();</c> rather than
    ///     <c>T n… = r….Where(x =&gt; …)</c> / <c>.T…();</c>.
    /// </summary>
    /// <param name="equals">The <c>=</c>'s 1-based column.</param>
    /// <param name="fragment">What stays beside a kept <c>=</c>: the receiver and the held call through its <c>)</c>.</param>
    /// <param name="below">The 1-based column the value would end at below, its <c>;</c> included.</param>
    /// <param name="lineStart">The statement's indent.</param>
    /// <remarks>
    ///     ⚠ Measured 2026-11-14 with <c>Testing ask</c> on 1 399 random series (six heads, indents 8 to 20, names of 3 to
    ///     45, receivers of 1 to 14, fourteen callees of 4 to 15 columns, four tails, rests of 5 to 44, two bodies), each
    ///     swept a column at a time with the line through the held call at 120 or less and the whole line past the margin
    ///     (10 490 statements). Round 2 had the oracle keep the <c>=</c> and break before the next link there; it does so
    ///     only past a limit on the value's end below, and every series switches exactly once. The limit has group I's
    ///     shape (<see cref="HeldChainValueBreaksTheEquals" />'s third region, about ten columns lower and without its
    ///     receiver term): it rises 0.119 a column of name to a hinge at 22 and falls 0.243 a column past it, plus a
    ///     sixtieth of a column per column of rest past ten and of indent. Solved exactly for the largest margin over the
    ///     9 876 statements under names of seven or more, it separates every one, by 0.0017 of a column. Under a name of six
    ///     the <c>=</c> breaks only before a fragment #590 calls short; under five, never, as there. The ordering rule had
    ///     answered with a limit read from the indent, 109 less a quarter of the continuation, and missed 1 067 of the 10 490.
    /// </remarks>
    static bool HeldChainThroughBreaksTheEquals(
        in GroupFacts facts,
        int equals,
        int fragment,
        int below,
        int lineStart
    ) {
        var name = facts.EqualsName;
        if (name < 7 && !EqualsFloor.FragmentIsShort(equals, name, fragment)) {
            return false;
        }

        return below
            <= 102.613854
            + 0.118860 * name
            - 0.361478 * Math.Max(0, name - 22.07)
            + 0.017485 * (facts.HeldChainRest - 10 + lineStart);
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

    /// <summary>
    ///     Whether an <c>=</c> before a method on a named receiver with two or more arguments, <c>r.M(a, b…)</c>, breaks
    ///     (#446, SK-DIV-0211 rounds 8 and 9): while the list is at most <see cref="ReceiverListLimit" />, and for a typed
    ///     local only outside its gate (<see cref="TypedGateBreaks" />). See <see cref="GroupFacts.CalleeOnReceiver" />.
    /// </summary>
    /// <remarks>
    ///     ⚠ Round 8's first cut read the plain-name floor (<see cref="EqualsFloor.Of" />) here, which fitted a typed local
    ///     at about 93 % by any linear form. The floor is a function of the <c>(</c>'s column; the oracle's boundary is
    ///     the type's and the name's separately, and the plain-name tables never varied them apart.
    /// </remarks>
    ResolvedMode? ReceiverCallBreaks(in GroupFacts facts, in Measures m, int lineStart, int paren, int arguments) {
        var name = facts.EqualsName;
        var type = Math.Max(0, HeadWidth(facts, m, lineStart) - name - 2);

        // ⚠ A typed local keeps the `=` and chops inside a region of the `(`'s column (#589's gate, remeasured on
        // receivers). See TypedGateBreaks.
        // ⚠ Except within a hair of a line that decides it, where the rule before it does (#446, round 12). See
        // TypedGateNear.
        if (facts.CalleeOwner == EqualsOwner.TypedLocal) {
            if (TypedGateNear(paren, facts.CalleeWidth, name)) {
                return null;
            }

            if (!TypedGateBreaks(paren, facts.CalleeWidth, name)) {
                return ResolvedMode.Flat;
            }
        }

        var margin = ReceiverListMargin(
            arguments - 2,
            facts.CalleeWidth,
            m.ContinuationColumn - indentWidth,
            type,
            name,
            false
        );
        return InsideTheListBand(margin) ? null
            : margin >= 0 ? ResolvedMode.Broken
            : ResolvedMode.Flat;
    }

    /// <summary>
    ///     Whether a typed local's <c>=</c> before <c>r.M(a, b…)</c> may break at all, by the call's <c>(</c> (1-based), the
    ///     callee <c>r.M</c> and the name (#446, SK-DIV-0211 rounds 8 and 9).
    /// </summary>
    /// <remarks>
    ///     ⚠ Two regions, not a threshold: the <c>=</c> breaks while the <c>(</c> lands at or left of one boundary, or
    ///     right of another; between them it keeps the <c>=</c> and chops. With a callee of 40 and a name of 8 the oracle
    ///     breaks at a <c>(</c> of 72 to 83, chops at 84 and 85, and breaks from 86. Round 8 measured the gate a function of
    ///     the <c>(</c>'s column, the name and the callee alone — the same at indents 8 to 24, at every list width inside
    ///     the limit, and for a plain, generic, array and qualified type of the same width — and drew each boundary as one
    ///     line. ⚠ Round 9 mapped the region a column of <c>(</c> at a time for callees of 8 to 56 and names of 1 to 30
    ///     (9 418 cells): each boundary is two lines, its slope in the name changing where the region closes (the left
    ///     boundary's from about 5 to 8 columns per column of name, the right's from about 1.7 to 2.5), so round 8's
    ///     single lines put the region several columns wrong near its apex. 81 of 82 right and 20 of 20 left boundaries.
    ///     ⚠ One argument showed no gate, but for one row (indent 16, a type of 40, a name of 4, a callee of 16), not
    ///     modelled. ⚠ Round 11: that row is a gate of one argument's own (<see cref="OneArgumentGateBreaks" />), and
    ///     these lines are integers now — an LP over 13 317 open and closed points of eleven grids, every one separated
    ///     — where round 9's fitted lines misplaced three.
    ///     ⚠ Round 12 (#446, SK-DIV-0211): round 11's first line, <c>4·p ≤ 21·callee + 32·name − 762</c>, was a quarter of
    ///     a column off its own LP's line (<c>−190.02 + 5.25·callee + 8·name</c>) wherever the callee is one more than a
    ///     multiple of four, which no grid measured (their callees step by four): a fresh <c>List&lt;…&gt; n… =</c> at a
    ///     callee of 37 broke where it chopped. Round 11's evidence also kept only plain and <c>string</c> heads, so no
    ///     <c>List&lt;…&gt;</c>, array or <c>Dictionary&lt;…&gt;</c> head of a random probe reached it. Over all of them and
    ///     this round's six fresh probes (25 676 open and closed points) the left line is
    ///     <c>7·p ≤ 38·callee + 58·name − 1398</c>, the right ones <c>75·p &gt; 12876 − 124·callee − 189·name</c> and
    ///     <c>14·p &gt; 2155 − 23·callee − 24·name</c>, every point separated. ⚠ Round 11's first right line put a fresh
    ///     cell (a callee of 22, a name of 16, the <c>(</c> at 95) exactly on itself; the LP's margin is 0.007 of a column.
    /// </remarks>
    static bool TypedGateBreaks(int paren, int callee, int name) => TypedGateOpen(TypedGateSlacks(paren, callee, name));

    /// <summary>
    ///     The typed gate's four lines as slacks in columns of <c>(</c>, each positive on its open side: the two left
    ///     lines, then the two right ones. See <see cref="TypedGateBreaks" />.
    /// </summary>
    static (double, double, double, double) TypedGateSlacks(int paren, int callee, int name) =>
        ((38 * callee + 58 * name - 1398 - 7 * paren) / 7.0 + 1 / 14.0,
            (42 * callee + 43 * name - 1061 - 8 * paren) / 8.0 + 1 / 16.0,
            (75 * paren - 12876 + 124 * callee + 189 * name) / 75.0 - 1 / 150.0,
            (14 * paren - 2155 + 23 * callee + 24 * name) / 14.0 - 1 / 28.0);

    static bool TypedGateOpen((double L1, double L2, double R1, double R2) s) =>
        s.L1 > 0 && s.L2 > 0 || s.R1 > 0 && s.R2 > 0;

    /// <summary>
    ///     Whether a line that decides the typed gate lies within <see cref="TypedGateBand" /> of a column of the
    ///     <c>(</c>: flipping that line alone flips the gate (#446, SK-DIV-0211 round 12).
    /// </summary>
    /// <remarks>
    ///     ⚠ Each fresh probe of round 12 found another gate point exactly on a line — a callee of 22 and a name of 16
    ///     at a <c>(</c> of 95, a callee of 13 and a name of 26 at 88 — where the integer lines, exact on every point they
    ///     were fitted to, were a hair off: the LP's margin over 25 676 points is 0.007 of a column. Inside the band the
    ///     rule before it decides, as with the list limit's (<see cref="InsideTheListBand" />).
    /// </remarks>
    static bool TypedGateNear(int paren, int callee, int name) {
        var s = TypedGateSlacks(paren, callee, name);
        var open = TypedGateOpen(s);
        return Math.Abs(s.Item1) < TypedGateBand && TypedGateOpen((-s.Item1, s.Item2, s.Item3, s.Item4)) != open
            || Math.Abs(s.Item2) < TypedGateBand && TypedGateOpen((s.Item1, -s.Item2, s.Item3, s.Item4)) != open
            || Math.Abs(s.Item3) < TypedGateBand && TypedGateOpen((s.Item1, s.Item2, -s.Item3, s.Item4)) != open
            || Math.Abs(s.Item4) < TypedGateBand && TypedGateOpen((s.Item1, s.Item2, s.Item3, -s.Item4)) != open;
    }

    /// <summary>
    ///     The typed gate's band, in columns of <c>(</c>: the smallest width that holds both points above (0.007 and 0.036
    ///     of a column from their lines). It gives back 13 of 407 235 cells round 12's lines had right; 0.1 gives back 80.
    /// </summary>
    const double TypedGateBand = 0.04;

    /// <summary>
    ///     Whether <c>yield return r.P as|is T;</c> breaks after <c>yield return</c>, the whole test one level in, by the
    ///     column its line would end at (<paramref name="end" />), the receiver and the type (#446, SK-DIV-0210 round 11):
    ///     exactly one column over, while <c>3·receiver ≤ 13·type − 25</c> or the type is eight columns or more.
    /// </summary>
    /// <remarks>
    ///     ⚠ Measured with <c>Testing ask</c> on 4 512 cells (g40: <c>as</c> and <c>is</c>, types of 1 to 12, receivers of 1
    ///     to 40, the line ending at 120, 121 and 122, indents 8 and 24): the oracle takes the break only on a line of
    ///     121, the same at both indents and for both keywords — to a receiver of 4, 9, 13, 17 and 22 for types of 3 to 7,
    ///     never for 1 and 2, every receiver measured from 8. Otherwise the dot or the keyword's band decides as before.
    ///     Round 9 had read the same table on fr1 and found Skala had no point there.
    /// </remarks>
    bool YieldReturnBreaks(int end, in GroupFacts facts) =>
        end == width + 1
        && (facts.YieldTestType >= 8 || 3 * facts.YieldTestReceiver <= 13 * facts.YieldTestType - 25);

    /// <summary>
    ///     Whether a typed local's <c>=</c> before <c>r.M(a)</c>, one argument, may break, by the call's <c>(</c> (1-based),
    ///     the callee <c>r.M</c> and the name (#446, SK-DIV-0211 round 11): <see cref="TypedGateBreaks" />'s shape, its left
    ///     line 149½ columns of <c>(</c> further right and its right line 48 further left, so it closes only for a short
    ///     callee and a short name.
    /// </summary>
    /// <remarks>
    ///     ⚠ Round 9 measured "no gate for one argument" but for one row of g20 (indent 16, a type of 40, a name of 4, a
    ///     callee of 16: the oracle chops at every argument). That row is this gate. Mapped with <c>Testing ask</c> on
    ///     g32 (12 450 cells: callees of 12 to 40, names of 1 to 30, the type moving the <c>(</c> from 56 to 112, indents 8
    ///     and 16, two argument widths): the same at both indents and both widths, exact on every row. The second right
    ///     line only closes the corner of a callee of 12 and a name of 1, the one row it was read from.
    /// </remarks>
    static bool OneArgumentGateBreaks(int paren, int callee, int name) =>
        4 * paren <= 21 * callee + 32 * name - 164
        || 52 * paren > 6470 - 86 * callee - 131 * name && paren > 185 - 5 * callee - 13 * name;

    /// <summary>
    ///     209 times the widest argument list (inside its parentheses) plus the indent that an <c>=</c> before a method on
    ///     a receiver, <c>r.M(…)</c>, still breaks for (<see cref="ReceiverListMargin" />), by the callee <c>r.M</c>, the
    ///     statement's indent, the head's type and name, and whether the call has one argument (#446, SK-DIV-0211 round
    ///     9). <paramref name="type" /> is the head before the name, its space included: a typed local's type and one,
    ///     <c>var</c>'s four, an assignment's none.
    /// </summary>
    /// <remarks>
    ///     ⚠ Round 9 (2026-11-13, <c>Testing ask</c>): four lines, the larger of two minimums. Along a row of the head the
    ///     limit falls with the callee and then rises; along a row of the callee it is flat, then falls with the name,
    ///     then rises with the type and the name. Round 8's two lines (the larger of two) had the short-head line flat in
    ///     the head, which is wrong once the head passes the callee's turn, and were fitted with one type/name split: on
    ///     the random probes 21 of the 24 cells that round newly lost sat within 1.2 columns of them.
    ///     <list type="bullet">
    ///         <item>
    ///             ⚠ The list's split between its arguments plays no part: with the list fixed and the first argument
    ///             swept a column at a time (8 448 cells), every row is one answer. Nor do the method's share of the
    ///             callee, two arguments against three, an argument that is a call, or a <c>this.</c>, <c>string</c> or
    ///             typed head of the same widths (2 608 cells).
    ///         </item>
    ///         <item>
    ///             ⚠ One argument is the same four lines 21 columns higher. Round 8's one-argument limit (two lines,
    ///             <c>var</c> and assignment heads) and round 4's typed one (three lines in the line below) were each a
    ///             piece of it. 1 013 of 1 027 grid boundaries, two arguments and one, typed and not, indents 8 to 24.
    ///         </item>
    ///     </list>
    ///     Heads under twelve columns through the <c>=</c> are not this rule (<see cref="NarrowHeadBreaks" />).
    ///     ⚠ Round 11 (2026-11-14): one integer rule at scale 209. With <c>s</c> the column the name's head starts on
    ///     (indent plus type), the list breaks while <c>209·(list + indent)</c> is at most the larger of
    ///     <c>min(15800 − 84·callee − 29·s − 3·name, 17515 − 84·callee − 29·s − 81·name)</c> and
    ///     <c>min(7606 + 38·callee + 93·s + 42·name, 5917 + 38·callee + 93·s + 119·name)</c>, 4 399 higher for one
    ///     argument. The indent and the type share one slope (the indent's is the type's less one), each pair shares
    ///     everything but its name term, and both pairs turn at a name of 22 — the dot's V. Found by an LP over
    ///     16 654 boundary keys of twenty grids, then the smallest integer scale inside it.
    ///     <list type="bullet">
    ///         <item>
    ///             ⚠ Refuted: the receiver apart from the method. At a fixed callee the method was swept 2 to 22 (g30, g31,
    ///             115 rows): the limit never moved.
    ///         </item>
    ///         <item>
    ///             ⚠ Refuted: round 10's scale of 38. The callee's falling slope must lie within 0.4011 to 0.4039, which
    ///             no multiple of 1/38 does.
    ///         </item>
    ///         <item>
    ///             ⚠ No planar form of this shape is exact on every key: the LP's best margin with all of them is −0.025
    ///             of a column. Five keys stay a column off, each within 0.03 of the boundary.
    ///         </item>
    ///     </list>
    /// </remarks>
    static int ReceiverListLimit(int callee, int indent, int type, int name, bool single) {
        var start = indent + type;
        var falling = Math.Min(
            15800 - 84 * callee - 29 * start - 3 * name,
            17515 - 84 * callee - 29 * start - 81 * name
        );
        var rising = Math.Min(
            7606 + 38 * callee + 93 * start + 42 * name,
            5917 + 38 * callee + 93 * start + 119 * name
        );
        return Math.Max(falling, rising) + (single ? 4399 : 0);
    }

    /// <summary>
    ///     A list's margin to <see cref="ReceiverListLimit" /> in its own units, 1/209 of a column: at or above zero the
    ///     <c>=</c> breaks, below it the list chops beside the <c>=</c>.
    /// </summary>
    static int ReceiverListMargin(int list, int callee, int indent, int type, int name, bool single) =>
        ReceiverListLimit(callee, indent, type, name, single) - 209 * (list + indent);

    /// <summary>
    ///     Whether a list lies inside the band round 11's planar limit cannot place, within <see cref="ReceiverListBand" />
    ///     of it, where the rule before it decides (#446, SK-DIV-0211 round 12): the <c>=</c> falls through to the held
    ///     call's and the ordering rule's answers, which is what master did with these values.
    /// </summary>
    /// <remarks>
    ///     ⚠ A deliberate deferral, measured. No planar max-min form fits every boundary key (the LP's best margin is
    ///     −0.025 of a column), and seven cells of round 11's probes sat a column off within 2/209 of it: three
    ///     breaking at −1 and −2, four chopping exactly on it. A band of ±1 left one of them (fg1 C928, at −2); ±2 is the
    ///     smallest that leaves none, and gives back 31 of the 319 359 cells round 11 had right (a band of ±6, the 0.03 of a
    ///     column the residue first suggested, gives back 83).
    /// </remarks>
    static bool InsideTheListBand(int margin) => Math.Abs(margin) <= ReceiverListBand;

    /// <summary>The band's half-width, in 1/209 of a column. See <see cref="InsideTheListBand" />.</summary>
    const int ReceiverListBand = 2;

    /// <summary>The widest head #610 measured one argument behind; past it the floor is not used (#617).</summary>
    const int OneArgumentMeasuredHead = 14;

    /// <summary>
    ///     See <see cref="GroupFacts.CalleeWidth" />. Null for a call with one argument behind a head narrower than the
    ///     floor's that the rule for it does not break, or wider than <see cref="OneArgumentMeasuredHead" />, which the
    ///     ordering rule decides as it always did.
    /// </summary>
    /// <summary>
    ///     The head <see cref="EqualsFloor.OneArgumentBreaksTheEquals" /> reads: the statement's, through the <c>=</c>
    ///     (<see cref="GroupFacts.CalleeArgumentHead" />), while it fits on its line; the fitter's own once it cannot —
    ///     a type that fills onto a second line leaves only its last line beside the <c>=</c> (fuzz
    ///     4769102867181174370).
    /// </summary>
    int OneArgumentHead(in GroupFacts facts, in Measures m, int lineStart) =>
        facts.CalleeArgumentHead > 0 && m.ContinuationColumn - indentWidth + facts.CalleeArgumentHead <= width
            ? facts.CalleeArgumentHead
            : HeadWidth(facts, m, lineStart);

    ResolvedMode? EqualsBeforeACall(in GroupFacts facts, in Measures m, int lineStart) {
        // ⚠ A method on a named receiver is read by the floor only behind a head of twelve or more and with its `(`
        // inside the margin beside the `=`: the measured shapes (#446, SK-DIV-0211 round 8). Otherwise its dot can take
        // the break, and #528's held call decides as before (`var w = S…` / `.Select…(alpha, beta);`, held-single-call.cs).
        if (facts.CalleeOnReceiver
            && (m.PointWidth >= Unbounded
                || !HeadIsWideEnough(facts, m, lineStart)
                || m.Column + m.PointWidth + 1 + facts.CalleeWidth + 1 > width)) {
            return null;
        }

        if (m.PointWidth >= Unbounded) {
            return ResolvedMode.Flat;
        }

        // ⚠ A field's one argument was not measured: the ordering rule's, as before.
        if (facts is { CalleeArgument: > 0, CalleeOwner: EqualsOwner.Field }) {
            return null;
        }

        // ⚠ Behind a head under twelve the `=` breaks when the call's `(` lands past the margin, whatever the arguments
        // (#610): measured 2026-10-10 with `Testing ask` on 896 rows of two arguments, heads of 3 to 10, plain and
        // generic calls and creations, the `(` at 100 to 128 — every row with the `(` at 121 or further breaks, where
        // the head rule kept the `=` and chopped. ⚠ A generic value whose `>` is past the margin behind a head of five
        // or fewer has been answered already (GroupFacts.ShortHeadTypeEnd).
        // ⚠ And with one argument, when the head is wide enough for it by the measured line; otherwise the ordering
        // rule decides, as it did before the floor took one argument. See GroupFacts.CalleeArgument.
        // ⚠ Not one argument behind a statement head wider than fourteen whose marker starts later — a deconstruction's
        // `(` — and so reads it narrow: the one-argument boundary below was measured on the whole head (SK-DIV-0501).
        if (!HeadIsWideEnough(facts, m, lineStart)
            && !(facts.CalleeArgument > 0 && OneArgumentHead(facts, m, lineStart) > OneArgumentMeasuredHead)) {
            var open = m.Column + m.PointWidth + 1 + facts.CalleeWidth + 1;
            if (open > width) {
                return ResolvedMode.Broken;
            }

            if (facts.CalleeArgument <= 0) {
                return ResolvedMode.Flat;
            }

            var head = HeadWidth(facts, m, lineStart);
            var statement = m.ContinuationColumn - indentWidth;
            return 24 * (head - 6) - 3 * (statement - 8) >= 4 * facts.CalleeArgument ? ResolvedMode.Broken : null;
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

        // ⚠ One argument behind a head wider than #610 measured is not the floor of two's (#617, fuzz
        // 12392120797706766071; SK-DIV-0501, SK-DIV-0530): that floor was confirmed for one argument on heads of 12 to
        // 14 only, and chopped from an argument of 44 behind `IReadOnlyDictionary<decimal, decimal> v =` where the
        // oracle breaks the `=` into the sixties. Its own measured boundary decides now
        // (EqualsFloor.OneArgumentBreaksTheEquals); an argument with a certain break of its own, which has no flat
        // width, stays with the ordering rule. Pass two meets pass one's chop as the author's and keeps it
        // (GroupFacts.KeptArgumentCallee).
        if (facts.CalleeArgument > 0
            && OneArgumentHead(facts, m, lineStart) is var wideHead
            && wideHead > OneArgumentMeasuredHead) {
            if (facts.CalleeArgumentHead <= 0 || m.FlatWidth >= Unbounded) {
                return null;
            }

            var below = m.ContinuationColumn + facts.CalleeWidth + facts.CalleeArgument + 2 + m.Trailing;
            return EqualsFloor.OneArgumentBreaksTheEquals(
                m.ContinuationColumn - indentWidth,
                wideHead,
                facts.CalleeWidth,
                facts.CalleeArgument,
                below - width
            )
                ? ResolvedMode.Broken
                : ResolvedMode.Flat;
        }

        // ⚠ The name the `=` assigns has to reach the call, or the arguments chop (#589). See
        // EqualsFloor.NameReachesTheCall.
        // ⚠ Not a field's name of 30 or more, whose own table has a floor at a `(` far left that the gate would refuse.
        if (facts.EqualsName > 0
            && (facts.CalleeOwner != EqualsOwner.Field || facts.EqualsName < 30)
            && !facts.CalleeOnReceiver
            && !EqualsFloor.NameReachesTheCall(facts.EqualsName, facts.CalleeWidth, paren)) {
            // ⚠ The gate was measured on two arguments or more; one argument it turns away goes back to the ordering
            // rule, which `T… vwwwwwww = Compute(aaaaaaaaaaaa);` needs to break its `=` (EqualsByTheNameTests).
            return facts.CalleeArgument > 0 ? null : ResolvedMode.Flat;
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

        // ⚠ A method on a named receiver: its own reading of the floor (#446, SK-DIV-0211 round 8).
        if (facts.CalleeOnReceiver) {
            return ReceiverCallBreaks(facts, m, lineStart, paren, arguments);
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
    /// <remarks>
    ///     ⚠ #604, measured 2026-10-10 on 4 680 more fields: three- and four-argument types read off the same table
    ///     once <c>h</c> is the fill's own first line — through the last comma that fits on the modifiers' line, where
    ///     the oracle's fill breaks, not the first. <c>Func&lt;K…, int, List&lt;…&gt;&gt;</c> behind a 16-column
    ///     <c>Func&lt;K…,</c> reads as 21 and <c>Func&lt;K…, int, long, …&gt;</c> as 27, every row then on the table:
    ///     3 347 of 3 456 cells where either side moves the type, against 2 992 of 4 680 overall before.
    /// </remarks>
    bool FillsAfterTheModifiers(in GroupFacts facts, int lineStart, int column) {
        // The group starts at the gap before the type; the type's first column is one past it.
        var head = column + facts.ModifierFillLastHead <= width ? facts.ModifierFillLastHead : facts.ModifierFillHead;
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
    ///             otherwise — a tie, where the keyword's band could hold the line — with <c>c = min(k, width − 2)</c>,
    ///             <c>r</c> the receiver, <c>i</c> the statement's indent and <c>b</c> the dot's column: the dot breaks
    ///             when <c>r ≥ 5</c>, <c>50·c + 20·r + i − 35·t ≥ 5944</c> and
    ///             <c>56·c − (k − c) + b − 38·t ≥ 6158</c> (at a width of 120).
    ///         </item>
    ///     </list>
    ///     ⚠ #446 round 8 (SK-DIV-0210): the ties were measured one variable at a time — types of 1 to 24, receivers
    ///     to 60, indents 8 to 72, and the head widened to <c>yield return</c> to part the indent from the property —
    ///     78 000 cells. The first condition is the diagonal rounds 6 and 7 saw (a receiver of five more per two
    ///     columns of keyword line); the second is the saturation they read as "types of 14 or more never take the
    ///     dot": it moves 56 columns of the dot's column per column of <c>k</c> and 38 per column of type, so a type of
    ///     14 takes it only from a dot at 82, and one of 15 never can. Past <c>width − 2</c> the keyword's line stops
    ///     helping, and costs a column of the dot's per column. A fresh random probe (both keywords, generic and
    ///     qualified types, indents and types off the grid) matched 2 905 of 2 909 tie cells, where the subset rule
    ///     before it matched 2 520. The <c>x</c> layout — neither the keyword's band nor the dot's line fits, and the
    ///     oracle nests the dot two levels in — is not this rule's and stays the keyword's.
    /// </remarks>
    /// <summary>
    ///     Whether <c>operand is N.T</c> breaks before the pattern's dot, <c>… is N</c> / <c>.T;</c> (#621,
    ///     SK-DIV-0540): the oracle reads <c>N.T</c> after <c>is</c> as a constant pattern that is a member access.
    /// </summary>
    /// <remarks>
    ///     ⚠ Measured 2026-10-14 with <c>Testing ask</c> on <c>return r….P… is N….T…;</c> — indents 8 to 40, qualifiers
    ///     of 1, 3, 5 and 8, names of 2 to 24, receivers of 1 to 40, the keyword ending at 104 to 125, 7 432 cells.
    ///     With <c>k</c> the line through <c>is</c>, <c>q</c> the qualifier and <c>t</c> the name, the line past the
    ///     margin breaks there exactly when <c>… is N</c> fits and <c>2·k − 8·t − 6·indent − 11·q ≤ 61</c>: 3 778 of
    ///     3 778 cells where it and the keyword's break compete. The receiver plays no part. One column over the
    ///     margin the operand's own dot keeps the line, as for a type.
    /// </remarks>
    bool ThePatternDotTakesTheBreak(in GroupFacts facts, in Measures m, int lineStart) {
        // The operand's own fill group measures the operand alone; its type test is a returned one, ending at `;`.
        var operandGroup = facts.TypeTestTail > 0 && facts.KeywordWidth == 0 && !facts.TypeTestAfterKeyword
            || facts.PatternDotEquals;
        if (!operandGroup && m.Trailing >= Unbounded || facts.PatternDotEquals && m.PointWidth >= Unbounded) {
            return false;
        }

        var operand = facts.PatternDotEquals ? m.Column + m.PointWidth + 1 : m.Column;
        var keywordEnd = operand + facts.PatternDotOperand + 3;
        var whole = keywordEnd
            + 1
            + facts.PatternDotQualifier
            + 1
            + facts.PatternDotName
            + (operandGroup ? 1 : m.Trailing);
        if (whole <= width + (facts.TypeTestTail > 0 ? 1 : 0) || keywordEnd + 1 + facts.PatternDotQualifier > width) {
            return false;
        }

        // ⚠ Beside a local's `=` the limit is the head's; on a line of its own, or after `return`, it is 61.
        var joined = facts.PatternDotHead > 0 && operand > lineStart;
        var limit = joined ? PatternDotLimit(facts.PatternDotHead) : 61;
        return limit > int.MinValue
            && 2 * keywordEnd - 8 * facts.PatternDotName - 6 * lineStart - 11 * facts.PatternDotQualifier <= limit;
    }

    /// <summary>
    ///     The limit <see cref="ThePatternDotTakesTheBreak" /> holds a joined local's line to, by the head through the
    ///     <c>=</c>; <see cref="int.MinValue" /> where the <c>=</c> always breaks first.
    /// </summary>
    /// <remarks>
    ///     ⚠ Measured 2026-10-14 on 7 040 locals, heads of 5 to 12 (#621): 58, 31, −52, −114, −167, −206 and −240 for
    ///     heads of 5 to 11, and a head of twelve or more never keeps the pattern beside its <c>=</c>; 5 cells of 3 500
    ///     a column off. ⚠ From 6 to 11 they are <see cref="EqualsFloor.BreaksBeforeAPattern" />'s narrow-head limits
    ///     negated, to the column: the same head decides both.
    /// </remarks>
    static int PatternDotLimit(int head) =>
        head switch {
            <= 5 => 58,
            6 => 31,
            7 => -52,
            8 => -114,
            9 => -167,
            10 => -206,
            11 => -240,
            _ => int.MinValue
        };

    /// <summary>
    ///     Whether a returned type test's operand runs past the margin by itself and its dot's line one level in does
    ///     not fit either: the oracle then breaks before the dot two levels in, the type test's own break taken below
    ///     it (#446, round 4's <c>x</c>).
    /// </summary>
    /// <remarks>
    ///     ⚠ Before a constant pattern that is a member access, <c>is N.T</c>, the oracle often keeps the dot one level
    ///     in and breaks the pattern's own dot instead (#621); not modelled, and leaving those to the keyword's band
    ///     was worse on the probe (4 cells right lost, none gained).
    /// </remarks>
    bool TheDotGoesTwoLevelsIn(in GroupFacts facts, int column, int lineStart) =>
        column + facts.TypeTestOperand > width && lineStart + indentWidth + facts.TypeTestTail > width;

    /// <summary>
    ///     With <see cref="TheDotGoesTwoLevelsIn" />: whether the keyword leaves the dot's line too, which it does
    ///     exactly when it would overflow there (#446).
    /// </summary>
    bool TheKeywordLeavesTheDotsLine(in GroupFacts facts, int lineStart) =>
        DotsLineTwoLevelsIn(facts, lineStart) + 1 + facts.TypeTestKeyword > width;

    /// <summary>
    ///     With <see cref="TheKeywordLeavesTheDotsLine" />: whether the keyword and the type then take a line each,
    ///     <c>.P…</c> / <c>as</c> / <c>T;</c>, which they do while the dot's line ends at most a column past the
    ///     margin; past that, <c>as T;</c> share one (#446).
    /// </summary>
    bool TheTypeLeavesTheKeywordsLine(in GroupFacts facts, int lineStart) =>
        TheKeywordLeavesTheDotsLine(facts, lineStart) && DotsLineTwoLevelsIn(facts, lineStart) <= width + 1;

    int DotsLineTwoLevelsIn(in GroupFacts facts, int lineStart) =>
        lineStart + 2 * indentWidth + facts.TypeTestOperand - facts.TypeTestReceiver;

    /// <summary>
    ///     Whether a resolved group's last-resort points all break, wherever what follows them would fit: the type
    ///     test's own group in the <c>x</c> layout once the keyword left the dot's line (#446).
    /// </summary>
    public bool BreaksEveryPoint(int group) => chops.Contains(group);

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

        // ⚠ The tie, as two linear conditions over the keyword's line capped two columns short of the margin (#446
        // round 8): the receiver against the type, and the dot's column against the type. ⚠ Both read the line as if
        // the head were `return`: under `yield return` the keyword's line and the dot's column count six columns
        // short, so the head's width plays no part (#446 round 9).
        var head = facts.TypeTestHead - 7;
        var capped = Math.Min(keywordEnd - head, width - 2);
        var past = keywordEnd - head - capped;
        var receiver = facts.TypeTestReceiver;
        var type = facts.TypeTestType;
        return receiver >= 5
            && 50 * (capped - width + 2) + 20 * receiver + lineStart - 35 * type >= 44
            && 56 * (capped - width + 2) - past + column - head + receiver - 38 * type >= -450;
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
    ///     ⚠ Round ten (#603, SK-DIV-0570): those misses were the second condition, which is a plane in <c>E</c> too,
    ///     <c>11·E ≥ 78·w + 28·i − 19·h − 1053</c>, with the first refitted beside it as
    ///     <c>50·E ≥ 3520 + 67·w + 24·(i + h)</c>. ⚠ Round eleven (SK-DIV-0730): inside a default's band both are
    ///     read on a parameter about 3/13 of its kept part wider, and a parameter of eleven columns or fewer takes
    ///     this rule too, where it was once chopped whenever the joined line overflowed.
    /// </remarks>
    ResolvedMode? ChopsBeforeTheParameter(in GroupFacts facts, in Measures m, int lineStart) {
        if (m.Column != lineStart + facts.SectionHead) {
            return null;
        }

        var end = lineStart + facts.SectionWidth;
        var parameter = facts.ParameterAfterSection;
        if (end > width || end + 1 + parameter <= width || OverflowsItsOwnLine(facts, lineStart)) {
            return null;
        }

        // ⚠ One argument (#603): the same slopes, the threshold 17 columns later and its own second condition.
        // Measured 2026-10-10 on 2 205 cells — `[A(`, `[From(`, `[Description(`, `[JsonPropertyName(`,
        // `[NotNullIfNotNull(`, parameters of 12 to 24 columns, indents 8, 12 and 20 — with round four's `[A(` and
        // `[Description(` rows.
        return SectionChops(facts, lineStart) ? ResolvedMode.Broken : ResolvedMode.Flat;
    }

    /// <summary>
    ///     Whether a one-argument call under a type test written on one line keeps its arguments whole and leaves the
    ///     break to the test (#620, SK-DIV-0811): <c>100·g + 40·h + 116·s − 67·t ≤ 9633</c> at a margin of 120, with
    ///     <c>g</c> the argument's width, <c>h</c> the line from its start <c>s</c> through the call's <c>(</c>, and
    ///     <c>t</c> what follows the call's <c>)</c> on its line.
    /// </summary>
    /// <remarks>
    ///     ⚠ Measured 2026-11-13 with <c>Testing ask</c>, one column at a time: 4 612 cells of four tests, heads of 3
    ///     to 46 columns behind <c>ok =</c>, <c>return</c> and <c>_ =</c>, indents 8 to 20 and lines of 122 to 151 —
    ///     every one agreeing, the nearest a hundredth of a column from the line — and a sole lambda's body under a
    ///     broken arrow on the same line. Only while the line overflows without its <c>;</c> (at 121 columns with it
    ///     the oracle chops) and the call through its <c>)</c> fits. ⚠ Not a call whose name is one character, which
    ///     the oracle reads otherwise.
    /// </remarks>
    /// <summary>
    ///     A sole lambda's arrow over a chain that starts with a call fitting beside it, where the body fits neither
    ///     beside the arrow nor on the line below (#620, SK-DIV-0812); null elsewhere, where the older rules decide.
    /// </summary>
    /// <remarks>
    ///     ⚠ Measured 2026-11-13 with <c>Testing ask</c> one column at a time — the lambda's start <c>s</c>, its
    ///     parameters <c>p</c> (modifiers and parentheses counted) and the first call's width <c>h</c> — on 18 741 cells
    ///     past the line below, under two statements and two indents, first calls bare, behind receivers, with and
    ///     without an argument, later calls with links after them or none: every one agrees. The arrow stays where it
    ///     ends within twelve columns of the line's start, or while both <c>200·h ≥ 72·s − 127·p + 4067</c> and
    ///     <c>200·s − 352·p − 25·h ≤ 1441</c> hold; it breaks otherwise. The later calls, the links and the body's
    ///     width play no part. ⚠ The second line is SK-DIV-0762's property-chain line, <c>f</c> the first call.
    /// </remarks>
    ResolvedMode? SoleChainMode(in GroupFacts facts, in Measures m, int lineStart) =>
        // ⚠ A chain the author broke is left to the older rules where the lines would break the arrow: Skala's own
        // `DotNetRun(settings => settings` / `.SetProjectFile(cli)` / … keeps its arrow in the oracle, where the lines,
        // measured on chains written on one line, break it (Lint, build/Build.cs). Pass one's own kept arrow over a
        // chopped chain is the lines' answer on pass two too.
        SoleChainModeOf(facts, m, lineStart) is { } mode && !(facts.SoleChainKept && mode == ResolvedMode.Broken)
            ? mode
            : null;

    ResolvedMode? SoleChainModeOf(in GroupFacts facts, in Measures m, int lineStart) {
        var body = facts.SoleChainBody;
        var head = facts.SoleChainHead;
        var below = lineStart + indentWidth + body;
        if (width != 120 || m.Column + 1 + body <= width) {
            return null;
        }

        // ⚠ A first call that does not fit beside the arrow, with a later call SK-DIV-0760 does not read (two arguments or
        // more, or a lambda), behind a receiver of eleven columns or fewer through its dot
        // — none, `this.`, `recv.`, `Helpers.`, `this.cache.` — breaks the arrow wherever the lambda starts (#620 round
        // five): 793 cells of five receivers, three parameter lists, first calls of 30 to 75 columns and lambdas
        // starting 12 to 63 columns in, every one broken. Behind a receiver of twenty columns or more the oracle keeps
        // it at times, as it does a chain of properties, and the older rules decide.
        if (m.Column + 1 + head > width) {
            return facts.SoleCallHead == 0 && facts.SoleChainReceiver <= 11 && m.Column - lineStart >= 12
                ? ResolvedMode.Broken
                : null;
        }

        // ⚠ Round three kept the line below to a first call of up to 45 columns followed by a call of two arguments or
        // more, reporting 11 newly wrong behind eighty-column receivers or before a property without it. Refuted
        // 2026-11-14 (#620 round four, SK-DIV-0881): the same grid of 7 085 cells, unrestricted, has none newly wrong
        // (5 459 → 6 887 agreeing), nor has any probe; the link after the first call plays no part.
        if (m.Column - lineStart < 12) {
            return ResolvedMode.Flat;
        }

        var start = m.Column - facts.SoleCallLambda;
        var parameters = facts.SoleCallLambda - 3;
        var keeps = 200 * head >= 72 * start - 127 * parameters + 4067
            && 200 * start - 352 * parameters - 25 * head <= 1441;

        // ⚠ Where the body fits on the line below, the two lines keep it once that line would end at column 112 or
        // later. Below that, behind a first call of 25 columns or more, by one more line (#620 round four,
        // SK-DIV-0881): `100·B + 25·p + 2·h − 13·s` against 11 142, B the line below's end — measured one column at
        // a time on 7 064 cells and checked on round three's 25 589, read only where it stands 0.2 of a column clear of
        // the line (every cell there agrees); within that, and behind a shorter first call, the older rules decide.
        // ⚠ Refuted: "before 108 never" — a 75-column first call behind `static (Node node)` keeps its arrow at 107.
        if (below <= width && keeps) {
            if (below >= width - 8) {
                return ResolvedMode.Flat;
            }

            if (head >= 25) {
                return BelowBandMode(below, parameters, head, start);
            }

            return below <= width - 13 ? ResolvedMode.Broken : null;
        }

        return keeps ? ResolvedMode.Flat : ResolvedMode.Broken;
    }

    /// <summary>
    ///     Whether a <c>var</c> local's <c>=</c> before a sole lambda call breaks (#620, SK-DIV-0880):
    ///     <c>200·E − 119·c + 4·n + 28·i + 4·p ≤ 21095</c> at a margin of 120, with <c>E</c> where the value would end on
    ///     the line below, its <c>;</c> included, <c>c</c> the callee, <c>n</c> the name, <c>i</c> the line's start and
    ///     <c>p</c> the lambda's parameters; ≤ 20438 where the line runs one or two columns past the margin.
    /// </summary>
    /// <remarks>
    ///     ⚠ Measured 2026-11-14 with <c>Testing ask</c> one column at a time: names of 2 to 25, callees of 3 to 36 plain
    ///     and dotted, indents 8 to 20, three parameter lists and three bodies (17 708 cells), and every class and
    ///     parameter list of #620's probes behind <c>var symbols = context.Compilation.Use(</c> (9 749 more). The body,
    ///     its kind and its width play no part; a head under twelve columns through the <c>=</c> never breaks it, and
    ///     past the line the arrow breaks, as before. Where the line is one or two columns over, the other answer is the
    ///     arrow kept and the outer <c>)</c> moved down, and the line sits 3¼ columns tighter.
    /// </remarks>
    bool SoleLambdaEqualsBreaks(in GroupFacts facts, in Measures m, int lineStart) {
        if (width != 120 || m.PointWidth >= Unbounded) {
            return false;
        }

        var value = facts.SoleLambdaEqualsValue;
        var line = m.Column + m.PointWidth + 1 + value;
        var below = m.ContinuationColumn + value;
        if (line <= width || below > width + 1) {
            return false;
        }

        var score = 200 * below
            - 119 * facts.SoleLambdaEqualsCallee
            + 4 * facts.SoleLambdaEqualsName
            + 28 * lineStart
            + 4 * facts.SoleLambdaEqualsParameters;
        return score <= (line <= width + 2 ? 20438 : 21095);
    }

    ResolvedMode? TestedCallMode(in GroupFacts facts, in Measures m, int lineStart) {
        var tail = facts.TestedCallTail;
        if (m.FlatWidth >= Unbounded || m.Column + m.FlatWidth > width || m.Column + m.FlatWidth + tail <= width) {
            return null;
        }

        // The test's points are fill points now, which end the list's own measure: past the margin the list chops
        // wherever the line does not give the break to the test, as it did while they were last-resort points.
        var head = m.Column + 1 - lineStart;
        return width == 120
            && m.Column + m.FlatWidth + tail - facts.TestedCallTerminator > width
            && (!facts.TestedCallSoleBody || head == facts.TestedCallHead)
            && 100 * (m.FlatWidth - 2) + 40 * head + 116 * lineStart - 67 * tail
            <= (facts.TestedCallTerminator > 0 ? 9633 : 9572)
                ? ResolvedMode.Flat
                : ResolvedMode.Broken;
    }

    /// <summary>
    ///     How far a one-argument sole call's arrow is past the column from which it breaks whatever the arguments,
    ///     in twentieths of a column: <c>20·a + 23·L + 6·h − 13·λ − 1680</c> at a margin of 120 (#620, SK-DIV-0760).
    /// </summary>
    int FarRight(in GroupFacts facts, in Measures m) =>
        20 * (m.Column - width)
        + 23 * facts.SoleCallLinks
        + 6 * facts.SoleCallHead
        - 13 * facts.SoleCallLambda
        - (1680 - 20 * 120);

    /// <summary>
    ///     Whether a sole call's arrow that the measured line keeps is left to the older rules instead: where the line
    ///     was not measured and those rules agree with the oracle more often (#620, SK-DIV-0760).
    /// </summary>
    /// <remarks>
    ///     ⚠ Two places, each found by a fresh probe's newly wrong members and none left once deferred: an arrow ending
    ///     sixty columns or more in over a call that fits on the line below, where the oracle's line bends with the
    ///     arguments' width; and a one-argument call that fits nowhere within five columns of the far-right line,
    ///     where a receiver chain in front of the lambda (<c>await this.service.Get(x =&gt; …)</c>) moves it.
    /// </remarks>
    bool SoleCallKeepDefers(in GroupFacts facts, in Measures m) {
        var below = m.ContinuationColumn + facts.SoleCallHead + facts.SoleCallArguments + 1 <= width;
        return below
            ? m.Column >= width - 60
            : facts.SoleCallSingle && FarRight(facts, m) >= -100;
    }

    /// <summary>
    ///     Whether a sole lambda's arrow breaks over a call that does not fit beside it (#620, SK-DIV-0760). See
    ///     <see cref="GroupFacts.SoleCallHead" />.
    /// </summary>
    /// <remarks>
    ///     ⚠ Measured 2026-11-13 with <c>Testing ask</c>, one column at a time, on grids of 2 000 to 10 500 cells each:
    ///     the oracle decides before it lays out the arguments, by the links' width against the lambda's head, the
    ///     call's head and where the arguments would end on one line two levels in. The callee, the statement and the
    ///     argument's kind play no part, and a type test written on one line after the call is one more link. On six
    ///     random probes written after the rule, 15 400 members, it took master's 8 767 agreeing to 13 655, one newly
    ///     wrong (SK-DIV-0760).
    /// </remarks>
    /// <summary>
    ///     Whether a sole lambda's arrow breaks over a chain of properties that fits neither beside it nor below it
    ///     (#620, SK-DIV-0762): <c>100·s − 175·p − 12·f ≥ 776</c>, <c>s</c> the column the lambda starts at, <c>p</c>
    ///     its parameters' width before <c>=&gt;</c> (modifiers included) and <c>f</c> how much of the chain the fill
    ///     would keep beside the arrow.
    /// </summary>
    /// <remarks>
    ///     ⚠ Measured 2026-11-13 with <c>Testing ask</c>: 2 048 cells of eight parameter lists, two segment widths and
    ///     lambdas starting at columns 12 to 47 (2 034 agree), and 780 members of four random probes (774). The line's
    ///     start, the chain's width and what is in front of the lambda play no part — #557's line, which read the
    ///     width below, agreed in 1 334 and 584. ⚠ Not where the chain fits below, which #557's line still decides.
    /// </remarks>
    bool BreaksThePropertyChainArrow(int[] chain, int lambda, in Measures m) {
        var start = m.Column - lambda;
        var parameters = lambda - 3;
        return 100 * start - 175 * parameters - 12 * PropertyChainKept(chain, m) >= 776;
    }

    /// <summary>
    ///     A sole lambda's arrow over a chain of properties that fits on the line below (#620 round four, SK-DIV-0881);
    ///     null where the older rules decide.
    /// </summary>
    ResolvedMode? SolePropertyChainBelowMode(int[] chain, int lambda, in Measures m, int lineStart) {
        var below = lineStart + indentWidth + chain.Sum() + chain.Length - 1;
        if (width != 120 || below > width) {
            return null;
        }

        if (BreaksThePropertyChainArrow(chain, lambda, m)) {
            return ResolvedMode.Broken;
        }

        return below >= width - 8
            ? ResolvedMode.Flat
            : BelowBandMode(below, lambda - 3, PropertyChainKept(chain, m), m.Column - lambda);
    }

    /// <summary>How much of a property chain the fill would keep beside a sole lambda's arrow ending at m.Column.</summary>
    int PropertyChainKept(int[] chain, in Measures m) {
        var column = m.Column + 1 + chain[0];
        var kept = chain[0];
        for (var k = 1; k < chain.Length && column + 1 + chain[k] <= width; k++) {
            column += 1 + chain[k];
            kept += 1 + chain[k];
        }

        return kept;
    }

    /// <summary>
    ///     A sole lambda's arrow over a chain that fits on the line below, where the measured lines keep it, the line
    ///     below ending at <paramref name="below" /> short of column 112 (#620 round four, SK-DIV-0881): kept once
    ///     <c>100·B + 25·p + 2·h − 13·s ≥ 11 162</c>, broken at 11 122 or less, null between — <c>h</c> the first call
    ///     through its <c>)</c>, or how much of a chain of properties the fill keeps beside the arrow.
    /// </summary>
    /// <remarks>
    ///     ⚠ Measured 2026-11-14 with <c>Testing ask</c> one column at a time: 7 064 cells of chains (first calls of 22
    ///     to 99 columns, three kinds of link, seven parameter lists, lambdas starting 4 to 32 columns past the line's
    ///     start), round three's 25 589 and 32 184 of property chains — every cell 0.2 of a column clear of the line
    ///     agrees; the line's own best fit misses one cell in a hundred, which the gap leaves to the older rules.
    /// </remarks>
    static ResolvedMode? BelowBandMode(int below, int parameters, int head, int start) {
        var band = 100 * below + 25 * parameters + 2 * head - 13 * start;
        return band >= 11162 ? ResolvedMode.Flat : band <= 11122 ? ResolvedMode.Broken : null;
    }

    /// <summary>
    ///     Whether a sole lambda's arrow over a call of two arguments or more stays behind a short head, lambda and
    ///     links, <paramref name="lead" /> the lambda through <c>=&gt;</c> and the head through <c>(</c> (#620,
    ///     SK-DIV-0760, SK-DIV-0880).
    /// </summary>
    /// <remarks>
    ///     ⚠ Round five (2026-11-14), with no links, measured one column at a time on 24 912 cells (lambdas of 4 to 24
    ///     columns, heads of 9 to 40, arrows ending at 20 to 101, behind <c>Uss(</c> and <c>var name = context.Use(</c>):
    ///     the short-lead region reaches a lead of 35 for every lambda up to 8 columns and of 34 for every one up to 24,
    ///     <c>16·lead + λ ≤ 568</c>, where round two's <c>6·lead ≤ 207</c> stopped at 34 — the 31-column
    ///     <c>Helpers.LookupTheDeclarationOf(</c> behind <c>x =&gt;</c> is lead 35, and q4's 78 cells and q3's 7 newly
    ///     wrong were that. Inside it the arrow stays while <c>100·a + 204·h + 110·λ ≤ 12 093</c> at a margin of 120,
    ///     which round two's <c>a + 2·h + λ &lt; 120</c> missed by a column for lambdas of 10 columns or more.
    ///     ⚠ At a lead of exactly 35 and a lambda of 9 the oracle keeps only arrows ending at 31–34 and 50–54 and
    ///     breaks between, at two indents, three argument widths and five statements (q3's eight): no line separates
    ///     it, and the bands are read as measured. With links a link weighs three columns of the lead, not round two's
    ///     2.67.
    /// </remarks>
    bool ShortLeadKeepsTheArrow(in GroupFacts facts, in Measures m, int lead) {
        var links = facts.SoleCallLinks;
        var edge = 16 * lead + facts.SoleCallLambda;
        if (links == 0 && width == 120 && edge == 569) {
            // ⚠ A lead of 35 behind a 9-column lambda: two bands of the arrow's column, measured, not a line.
            return m.Column <= 34 || m.Column is >= 50 and <= 54;
        }

        if (links == 0 && width == 120 && edge <= 568) {
            return 100 * m.Column + 204 * facts.SoleCallHead + 110 * facts.SoleCallLambda <= 12093 || 4 * lead < 70;
        }

        // ⚠ With links, a link column weighs three of the lead, not 2.67: `recv.Use(a, b) is null` behind `x =>` (lead
        // 13, links 8) breaks its arrow wherever it ends, and `Helpers.LookupTh(a, b).Name` (lead 21, links 5) keeps it.
        var inside = links == 0 ? 6 * lead <= 207 : lead + 3 * links <= 36;
        return inside
            && (m.Column + 2 * facts.SoleCallHead + facts.SoleCallLambda + 3 * links < width
                || 4 * lead + 5 * links < 70);
    }

    bool BreaksTheSoleCallArrow(in GroupFacts facts, in Measures m, int lineStart) {
        // ⚠ A head that does not fit beside the arrow moves below whatever the rest (measured at arrows ending at
        // columns 56 to 120 behind every head and argument count).
        if (m.Column + 1 + facts.SoleCallHead > width) {
            return true;
        }

        // The arrow ends twelve columns or more past the line's start, as for every other sole-lambda line (#618).
        if (m.Column - lineStart < 12) {
            return false;
        }

        // `10·L ≥ 6·(λ + h) + 15·(E − e)`: L the links, λ the lambda through `=>`, h the head through `(`, and E where
        // the arguments' line would end two levels past the line, all on one line.
        var links = 10 * facts.SoleCallLinks;
        var head = 6 * (facts.SoleCallLambda + facts.SoleCallHead);
        var end = m.ContinuationColumn + indentWidth + facts.SoleCallArguments;
        if (!facts.SoleCallSingle) {
            // ⚠ Two arguments or more behind a short head, lambda and links: the arrow breaks only once it ends far
            // enough to the right, `a + 2·h + λ + 3·L ≥ 120` at a margin of 120, and not at all behind the shortest.
            var lead = facts.SoleCallLambda + facts.SoleCallHead;
            if (ShortLeadKeepsTheArrow(facts, m, lead)) {
                return false;
            }

            // ⚠ At a thousandth of a column (round two): the lambda weighs a little less than the head, as for one
            // argument — 34 059 cells and probe members, every one of them a call of two arguments or more.
            // ⚠ Round five: the arguments' line stops counting at column 114, as the one-argument line's does —
            // `_ => Head(a, b, c).Select(z => z.Value).ToArray().Value` breaks its arrow wherever the arguments end, where
            // the unbounded line kept it past 114 (a fresh probe's member, then 1 060 cells with links of 6 to 44
            // columns, every one agreeing once it stops). ⚠ Not at 113: a 36-column `is IMethodSymbol { … }` behind
            // `x => Find(` keeps its arrow with the arguments ending at 114 (probe G), where 37 columns of links break it.
            return 1000 * facts.SoleCallLinks
                >= 603 * facts.SoleCallHead
                + 576 * (facts.SoleCallLambda - 3)
                + 1500 * (Math.Min(end, width - 6) - (width - 27))
                + 1864;
        }

        // ⚠ One argument: the arguments' line counts only while it ends six columns or more short of the margin; past
        // that the line stands on its own. ⚠ And an argument far past the margin keeps the arrow by a second line,
        // steeper in the head, that stops rising 126 columns past it.
        // ⚠ And behind an arrow far to the right, by the arrow's own column: `20·a + 23·L + 6·h − 13·λ ≥ 1680` at
        // a margin of 120, whatever the arguments and the line's start.
        // ⚠ The first line at a thousandth of a column (round two): `1000·L ≥ 604·h + 577·λ + 1500·min(0, E − 114) + 12`.
        // The head and the lambda do not weigh the same — 3/5 of a column each, as round one had it, missed one cell in
        // twenty past λ = 11 and every wide head — measured exactly on 520 thresholds of a column-at-a-time grid.
        // ⚠ And an arrow far to the right against the arguments' line breaks it too, `20·a + 6·h − 14·λ + 23·L − 34·E ≥
        // −2214`: the far-right line below, with the arguments' line in it, which the first round's grids — heads of
        // 30 or fewer, an arrow before column 45 — never reached (a 42-column head behind a deep indent).
        var first = 1000 * facts.SoleCallLinks
            >= 604 * facts.SoleCallHead
            + 577 * facts.SoleCallLambda
            + 1500 * Math.Min(0, end - (width - 6))
            + 12;
        var longFirstLine = 20 * m.Column
            + 6 * facts.SoleCallHead
            - 14 * facts.SoleCallLambda
            + 23 * facts.SoleCallLinks
            - 34 * end
            >= -2214;
        return FarRight(facts, m) >= 0
            || longFirstLine
            || first && links >= 5 * head - 90 + 15 * Math.Min(end, width + 126) - 3595;
    }

    /// <summary>
    ///     Whether a sole lambda's arrow breaks over a type test of a call, the arrow ending at
    ///     <paramref name="arrowEnd" /> on a line starting at <paramref name="lineStart" /> (#618). See
    ///     <see cref="GroupFacts.LambdaTestCall" />.
    /// </summary>
    /// <remarks>
    ///     ⚠ Measured 2026-10-14 with <c>Testing ask</c> on 1 482 members — callees of 1 to 24 columns, seven parameter
    ///     lists, call heads of 5 to 60 columns, two to five arguments, calls of 92 to 118 columns, indents 8 and 16 —
    ///     20 apart; and on a probe written after it (480 members: five statement kinds, six heads, six parameter
    ///     lists, four tests, indents 8, 12 and 20), 15 apart where the arrow had been kept in all 138 the oracle
    ///     breaks.
    /// </remarks>
    bool BreaksTheTestArrow(in GroupFacts facts, int arrowEnd, int lineStart) {
        var parameters = facts.LambdaHead;
        var callee = arrowEnd - 1 - parameters - lineStart;
        if (facts.LambdaTestKept || arrowEnd + 1 + facts.LambdaTestCall <= width) {
            return false;
        }

        // ⚠ Without a broken test (#620): a type test written on one line, or a body that is a one-argument call.
        switch (facts.LambdaBodyKind) {
            case LambdaBodyKind.InlineTest:
                return true;
            case LambdaBodyKind.InlineTestOfACall:
                return 6 * callee
                    + 2 * parameters
                    + 5 * facts.LambdaTestCallHead
                    - 9 * facts.LambdaTestCall
                    - 12 * lineStart
                    >= -1032;
            case LambdaBodyKind.OneArgumentCall:
                return callee + parameters >= 11
                    && 6 * callee
                    - 8 * parameters
                    + 7 * facts.LambdaTestCallHead
                    - 11 * facts.LambdaTestCall
                    - 10 * lineStart
                    >= -1108;
            case LambdaBodyKind.BrokenTest:
                break;
        }

        // ⚠ Round five: what the kept arrow's first line would hold decides first (SK-DIV-0520). See
        // KeptLineBreaksTheTestArrow; where it does not speak, the rounds below do.
        if (KeptLineBreaksTheTestArrow(facts, arrowEnd, lineStart) is { } kept) {
            return kept;
        }

        // ⚠ An operand ending in a later call of two or more arguments (`open(x).Analyze(a, b)`, SK-DIV-0520 round
        // two): its own line first, then the operand's other line where it has one (−2: none, and the arrow stays).
        // A wider operand, a longer parameter list or a narrower callee keeps the arrow. ⚠ The constant is not the
        // best fit (−175, 16 of 635 apart on probe H): it is the line below which every member the old rules already
        // agreed with stays as it was, on probes S, F and H — the boundary is not clean, and on its near side the
        // other rules keep deciding. Measured 2026-11-13; see GroupFacts.LambdaTestLaterCall.
        // ⚠ Round three: behind a head of six columns or more through the outer call's `(` it breaks whatever the line
        // says — the keeps the line left all sit behind `X(`, `Do(`, `Use(` and `Run(` (7 of 932 members past column
        // twelve, none of them behind a wider head).
        // ⚠ Round four, with the head width the oracle weighs (`c`, through the outer call's `(`), the line's start and
        // the parameters, measured on head-width grids that change nothing else: the arrow stays over a wide first call
        // behind a narrow head, and breaks wherever the head outweighs the parameters and the first call. See
        // GroupFacts.LambdaTestFirstCall.
        var head = callee + 1;
        var overflow = arrowEnd + 1 + facts.LambdaTestCall - width;
        if (facts.LambdaTestLaterCall && callee + parameters >= 11) {
            if (facts.LambdaTestFirstCall >= 22
                && 9 * head + 2 * lineStart - 2 * (parameters - 3) < 28
                && overflow >= 10) {
                return false;
            }

            if (9 * head + 2 * lineStart - 3 * (parameters - 3) - facts.LambdaTestFirstCall >= -5) {
                return true;
            }
        }

        // ⚠ A property chain fills beside the kept arrow behind a narrow head (round four). See
        // GroupFacts.LambdaTestProperties.
        if (facts.LambdaTestProperties && overflow >= 10 && 9 * head + 2 * lineStart - 2 * (parameters - 3) < 35) {
            return false;
        }

        if (facts.LambdaTestLaterCall
            && callee + parameters >= 11
            && (callee + 1 >= 6 || 12 * callee - 5 * parameters + 2 * lineStart - facts.LambdaTestCall >= -112)) {
            return true;
        }

        if (facts.LambdaTestCallHead == -2) {
            return false;
        }

        // ⚠ One call of one argument with only properties after it (SK-DIV-0520 round three): the arrow stays where the
        // argument's line under it reaches column 114 and the head before the lambda with the properties after the call
        // is narrow against the lambda's parameters with the call's head. Measured 2026-11-13; see
        // GroupFacts.LambdaTestArgument.
        // ⚠ Round four: whether or not the operand fits on the line below — the same score separates the members that
        // do (48 keeps below 7, every member from 10 breaking).
        // ⚠ And short of column 114 the bound falls seven points a column (round four): `s − 7·(i + 8 + a) < −795`.
        if (facts.LambdaTestCallHead == -1 && facts.LambdaTestArgument > 0) {
            var tail = facts.LambdaTestCall - facts.LambdaTestArgumentHead - facts.LambdaTestArgument - 1;
            var spread = 5 * (head + tail) - 3 * (parameters - 3 + facts.LambdaTestArgumentHead);
            var argumentEnd = lineStart + 8 + facts.LambdaTestArgument;
            if (argumentEnd >= 114 ? spread < 7 : spread - 7 * argumentEnd < -795) {
                return false;
            }
        }

        // ⚠ A first call of two or more with links after it (round three), behind a head of six columns or more: the
        // arrow also breaks where the first call with its first link fits a level in with room to spare, its arguments
        // are narrow and the head before the lambda with the links is wide against the lambda's parameters with the
        // call's head; or where that difference, less seven times the column the arguments' line under a kept arrow
        // ends at, is at least −630. See GroupFacts.LambdaTestFirstLink.
        // ⚠ Round four: what decides most of it is where the first call would end a level in, `i + 4 + f`, weighed
        // against the head score: below 96 the arrow breaks, and on to 111 it breaks behind a wider head.
        if (facts.LambdaTestLinks && !facts.LambdaTestLaterCall && callee + parameters >= 11) {
            var end = lineStart + 4 + facts.LambdaTestCallHead + facts.LambdaTestArgument + 1;
            var score = 9 * head + 2 * lineStart - 2 * (parameters - 3);
            if (end < 96 && score >= 20
                || end < 100 && score >= 40
                || end < 105 && score >= 160
                || end < 111 && score >= 220) {
                return true;
            }
        }

        if (facts.LambdaTestLinks && !facts.LambdaTestLaterCall && callee + parameters >= 11 && callee + 1 >= 6) {
            var links = facts.LambdaTestCall - facts.LambdaTestCallHead - facts.LambdaTestArgument - 1;
            var spread = 5 * (callee + 1 + links) - 3 * (parameters - 3 + facts.LambdaTestCallHead);
            var arguments = lineStart + 8 + facts.LambdaTestArgument;
            if (lineStart + 5 + facts.LambdaTestCallHead + facts.LambdaTestArgument + facts.LambdaTestFirstLink < 113
                && arguments < 104
                && spread >= 70
                || spread - 7 * arguments >= -630) {
                return true;
            }
        }

        // ⚠ A call of two or more with nothing after it (round four): in the head's terms the line is
        // `3·c − 5·i − 2·p + 3·h − 5·w ≥ −430`, 5 of 1 703 apart where round one's left 40. It decides five points or
        // more from its boundary; nearer, round one's still does.
        if (!facts.LambdaTestLinks
            && !facts.LambdaTestLaterCall
            && facts.LambdaTestCallHead > 0
            && callee + parameters >= 11) {
            var line = 3 * head
                - 5 * lineStart
                - 2 * (parameters - 3)
                + 3 * facts.LambdaTestCallHead
                - 5 * facts.LambdaTestCall;
            if (line >= -425) {
                return true;
            }

            if (line < -435) {
                return false;
            }
        }

        return arrowEnd + 1 + facts.LambdaTestCall > width
            && callee + parameters >= 11
            && (facts.LambdaTestLinks
                ? 6 * callee - 6 * parameters + 8 * facts.LambdaTestCallHead - 6 * facts.LambdaTestCall - 8 * lineStart
                >= -564
                : facts.LambdaTestCallHead < 0
                || 6 * callee - 3 * parameters + 5 * facts.LambdaTestCallHead - 8 * facts.LambdaTestCall - 6 * lineStart
                >= -658);
    }

    /// <summary>
    ///     Round five of #618 (SK-DIV-0520): whether a sole lambda's arrow breaks over a broken type test, read from what
    ///     the kept arrow's first line would have to hold; <see langword="null" /> where this does not decide and the
    ///     earlier rounds' lines still do.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         With <c>c</c> the head through the outer call's <c>(</c>, <c>p</c> the parameters, <c>i</c> the line's
    ///         start, <c>w</c> the operand and <c>f</c> its first call through the <c>)</c>. Where the arrow's line holds
    ///         the operand through the first call that takes an argument (<see cref="GroupFacts.LambdaTestHeldCall" />),
    ///         the kept arrow can only break the operand between links. There the oracle keeps the arrow wherever it ends
    ///         less than twelve columns past the line's start (<c>c + p &lt; 9</c>), and breaks it wherever the operand
    ///         fits a level in with eleven columns to spare. Every member those two cover agrees, of every operand class,
    ///         on twenty-three probes of 2 400. Past them a line per class decides — for parameters of five columns or
    ///         fewer every line breaks, as the oracle does in every such member — and on its near side, by a margin
    ///         measured so that no member the rules before agreed with moves, those rules still do.
    ///     </para>
    ///     <para>
    ///         Where the line does not hold that call, a one-argument call (only properties after it) breaks the arrow
    ///         by <c>5·(c + t) − 3·(p + h) ≥ min(9, 7·e − 790)</c>, with <c>e = i + 8 + a</c> the argument's line under
    ///         the kept arrow, one more where the operand does not fit a level in and a point less for parameters of ten
    ///         columns or more (two from seventeen). Per column of <c>e</c> the members separate at one threshold each.
    ///     </para>
    /// </remarks>
    bool? KeptLineBreaksTheTestArrow(in GroupFacts facts, int arrowEnd, int lineStart) {
        var parameters = facts.LambdaHead - 3;
        var head = arrowEnd - facts.LambdaHead - lineStart;
        var operand = facts.LambdaTestCall;
        var below = lineStart + 4 + operand;
        var fitsBelow = below <= width;
        var later = facts.LambdaTestLaterCall;
        var held = facts.LambdaTestHeldCall;
        if (later || held == 0 || arrowEnd + 1 + held <= width) {
            if (head + parameters < 9) {
                // ⚠ A property run past the margin by its last `()` alone breaks the arrow, the run kept whole.
                return facts.LambdaTestCallHead == -1
                    && facts.LambdaTestArgument == 0
                    && !facts.LambdaTestProperties
                    && facts.LambdaTestFirstCall == operand
                    && arrowEnd + 1 + operand <= width + 2;
            }

            if (below <= width - 11) {
                return true;
            }

            var overflow = Math.Min(arrowEnd + 1 + operand - width, 12);
            var first = facts.LambdaTestFirstCall;
            var (score, margin) = later
                ? (21 * head + 4 * lineStart - 7 * parameters - first - 2 * overflow + 22, 6)
                : facts.LambdaTestLinks
                    ? (10 * head + lineStart - 3 * parameters - first + 67, 11)
                    : facts.LambdaTestProperties
                        ? (14 * head + 3 * lineStart - 4 * parameters - overflow + (fitsBelow ? 1 : 0) - 39, 6)
                        : facts.LambdaTestCallHead == -1 && facts.LambdaTestArgument == 0
                            ? first == operand
                                ? (9 * head + 2 * lineStart - 3 * parameters - 3 * overflow + 23, 11)
                                : (15 * head + 3 * lineStart - 4 * parameters - operand - first + 106, 8)
                            : facts.LambdaTestCallHead == -1 && facts.LambdaTestArgument > 0
                                ? (5 * head + 2 * lineStart - operand + 64, 3)
                                : (0, int.MaxValue);
            return margin == int.MaxValue ? null
                : score >= margin ? true
                : score < -margin ? false
                : null;
        }

        if (facts.LambdaTestCallHead != -1 || facts.LambdaTestArgument <= 0 || facts.LambdaTestLinks) {
            return null;
        }

        if (head + parameters < 9) {
            return false;
        }

        var tail = operand - facts.LambdaTestArgumentHead - facts.LambdaTestArgument - 1;
        var spread = 5 * (head + tail)
            - 3 * (parameters + facts.LambdaTestArgumentHead)
            + (parameters >= 10 ? 1 : 0)
            + (parameters >= 17 ? 1 : 0);
        var argumentEnd = lineStart + 8 + facts.LambdaTestArgument;
        var distance = spread - Math.Min(9, 7 * argumentEnd - 790 + (fitsBelow ? 0 : 1));
        return argumentEnd >= 114 || distance >= 0 ? distance >= 0
            : distance < -2 ? false
            : null;
    }

    /// <summary>
    ///     Whether the parameter behind a section starting at <paramref name="lineStart" /> overflows even a line of
    ///     its own, its <c>,</c> included: then neither section rule speaks, and the arguments and the gap take their
    ///     own fit.
    /// </summary>
    /// <remarks>
    ///     ⚠ Not an approximation of the oracle but a condition of stability (fuzz seed 16667081197111340728): such a
    ///     parameter is broken by pass one, and on pass two the break inside it is read as the author's, which leaves
    ///     the list to its own fit (<c>BreakPlan.PlanSectionBeforeALongParameter</c>), so pass one must already have
    ///     asked it. The oracle agrees: <c>[EditorBrowsable(</c> / <c>EditorBrowsableState</c> / <c>.Never</c> /
    ///     <c>)]</c> in front of a 120-column parameter, chopped by the arguments' own fit.
    /// </remarks>
    bool OverflowsItsOwnLine(in GroupFacts facts, int lineStart) => lineStart + facts.ParameterAfterSection + 1 > width;

    /// <summary>
    ///     The measured chop rule itself, for a section starting at <paramref name="lineStart" /> (#476, #603): asked
    ///     by the arguments' group and, behind a default, by the gap after the section, which must agree.
    /// </summary>
    bool SectionChops(in GroupFacts facts, int lineStart) {
        var end = lineStart + facts.SectionWidth;
        var parameter = facts.ParameterAfterSection;
        var head = facts.SectionHead;
        var value = facts.DefaultValueWidth;

        // ⚠ Inside a default's band — the line through the `=` fits — the competitor is the `=`'s break and not the
        // section alone on its line (#603, SK-DIV-0570): the arguments chop later, the later the more of the
        // parameter stays on the line.
        // ⚠ Round eleven (#603, SK-DIV-0730): each condition is the rule outside a band, read on a parameter wider by
        // about 3/13 of `k`, the part the `=` keeps on the section's line (`w − v`). Two or more arguments: the first
        // is `E ≥ 70.4 + 1.338·w + 0.311·k + 0.480·(i + h)` (outside: 1.34·w, 0.48), the second the steep plane with
        // `18·k = 78·(3/13)·k` added. Round nine's `24·E ≥ 1644 + 42·w + 12·(i + h) − 8·v` crossed it a column either
        // way, and its `5·w + 2·i − h ≤ 155` could not bind in a band short of indent 44: the band's edge is the
        // steep plane, which keeps `[A(1, "…")] T value… = …` whole up to the last column of the band and chops it there.
        // ⚠ One argument: round nine's "never chops in a band" is refuted at indents 8 to 20, mostly behind short heads —
        // `[A("…")] T x = default` chops from 113 at indent 8 — and the same `w + 3·k/13` reads it (below).
        var inBand = value > 0 && end + parameter - value <= width;
        if (inBand && !facts.SectionSingle) {
            var kept = parameter - value;
            return 148 * end >= 10424 + 198 * parameter + 46 * kept + 71 * (lineStart + head)
                && 11 * end >= 78 * parameter + 18 * kept + 28 * lineStart - 19 * head - 1051;
        }

        // ⚠ A one-argument section's second condition depends on E (#603, SK-DIV-0570): a plane steeper than the
        // first, so near the margin the threshold bends up — `[A("…")]` behind 15 columns chops from 118, not 113 —
        // where `2·w + i − h ≤ 36` held it at the first condition until a fixed parameter width and then never.
        // ⚠ Round eleven (SK-DIV-0730), in and out of a band, on `13·w + 3·k` (`k = 0` outside one): the first
        // condition puts the indent and the head on one coefficient, the `(`'s column, as round ten found for two
        // arguments — `48·E ≥ 4196 + 64·w + 23·(i + h)`, where `24·E ≥ 2106 + 32·w + 11·i + 11·h` chopped a column
        // early behind `[Describe(` and `[NotNullIfNotNull(` — and the steep plane is `20·E ≥ 136·w + 49·i − 34·h − 22`
        // (6.8 columns of section end per column of parameter), where `4·E ≥ 28·w + 10·i − 7·h − 16` kept
        // `[Bind(name: "…")] long valuea = ""` at indent 8 whole a column too long and could not separate `i8 w13 E113`
        // (chops) from `i16 w10 E112` (does not) inside a band.
        if (facts.SectionSingle) {
            var wide = 13 * parameter + (inBand ? 3 * (parameter - value) : 0);
            return 260 * end >= 136 * wide + 637 * lineStart - 442 * head - 288
                && 624 * end >= 54548 + 64 * wide + 299 * (lineStart + head);
        }

        // ⚠ Two or more positional arguments outside a band (#603 round ten, SK-DIV-0570): the second condition is a
        // plane in E as well, steep — seven columns of section end per column of parameter — so the last parameter
        // width that chops at all starts chopping anywhere from 113 to 120, where `5·w + 2·i − h ≤ 155` chopped it
        // from the first condition's column. Measured on 3 958 cells; the first condition refitted on them puts the
        // indent and the head on one coefficient, the `(`'s column.
        return 11 * end >= 78 * parameter + 28 * lineStart - 19 * head - 1053
            && 50 * end >= 3520 + 67 * parameter + 24 * (lineStart + head);
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
            // ⚠ The receiver half of the rule too, in the kept direction (fuzz 6497386119263376933): a lambda's arrow
            // over a chain whose receiver runs past the margin beside it broke on pass one for exactly that reason,
            // with the chain below one column too wide for the continuation the tail is measured at; pass two, reading
            // the break as the author's, asked only the tail question, joined the arrow and wrote `=> this.Items(`
            // eighteen columns past the margin. See GroupFacts.BreaksIfReceiverOverflows.
            if (ReceiverOverflows(facts, m)) {
                return ResolvedMode.Broken;
            }

            return TailFits(m, tail + facts.TailSpare) ? ResolvedMode.Broken : ResolvedMode.Flat;
        }

        return facts.JoinsIfFits && Fits(m.Column, m.FlatWidth, m.Trailing)
            ? ResolvedMode.Flat
            : ResolvedMode.Broken;
    }

    /// <summary>
    ///     Whether a lambda's arrow over an unbroken call chain breaks because the line through the chain's receiver
    ///     runs past the margin beside it. See <see cref="GroupFacts.BreaksIfReceiverOverflows" />.
    /// </summary>
    bool ReceiverOverflows(in GroupFacts facts, in Measures m) =>
        facts.BreaksIfReceiverOverflows && m.AfterPoint < Unbounded && !Fits(m.Column, m.AfterPoint);

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
    ///     Whether the <c>=</c> before a bare <c>(a + b…)</c> or <c>(T)(a + b…)</c>, single or doubled, breaks behind a
    ///     head of twelve columns or more; null where the measured form does not decide (#598, SK-DIV-0770).
    /// </summary>
    /// <remarks>
    ///     ⚠ Measured 2026-11-13 with <c>Testing ask</c> on six dense grids of 54 670 statements. The oracle does not answer
    ///     by whether the value fits below, the collection rule this replaces. It answers by two things:
    ///     <list type="bullet">
    ///         <item>
    ///             <b>The fragment the <c>=</c> would keep.</b> Let <c>n</c> be the width of what the <c>=</c> assigns: a
    ///             local's name, or an assignment's whole target, never the type. Its weight is
    ///             <c>w = min(n + 3, 69 − 2n)</c>.
    ///             The <c>=</c> breaks whenever <c>3·min(a, 24) + 2·cast &lt; w</c>, where <c>a</c> is the first operand.
    ///             That holds even when the value then overflows below, <c>=</c> / <c>(aa</c> / <c>+ b…);</c>. The threshold
    ///             is the same at every line width, and the type does not count: <c>List&lt;int&gt; vvvvv =</c> keeps
    ///             <c>(aaaa</c> beside it where <c>var vvvvvvvvvvvv =</c> breaks.
    ///         </item>
    ///         <item>
    ///             <b>Past that, the parenthesis's end below.</b> Let <c>p</c> be the column the parentheses would end on
    ///             below the <c>=</c>, without the cast. The <c>=</c> stays when <c>128·p + 49·min(a, 24) + 34·cast − 16·w</c>
    ///             reaches 14 724. It breaks at 14 695 or less. Between those the collection rule still answers. Over the
    ///             grids, the best single threshold misclassifies 21 rows, all at a fitted width of one column.
    ///         </item>
    ///     </list>
    ///     A doubled parenthesis reads <c>p</c> two columns tighter. Its gate is 3 rather than 0, and its bounds are
    ///     14 749 and 14 767. Where the inner or the outer <c>)</c> lands on column 121, the <c>ParenCloseEnd</c> rules
    ///     answer first, as before.
    /// </remarks>
    static bool? BareParenEqualsBreaks(in GroupFacts facts, in Measures m) {
        if (facts.BareParenWidth <= 0) {
            return null;
        }

        var name = facts.BareParenName;
        var first = Math.Min(facts.BareParenOperand, 24);
        var cast = facts.BareParenCast;
        var weight = Math.Min(name + 3, 69 - 2 * name);
        if (3 * first + 2 * cast - weight < (facts.BareParenDoubled ? 3 : 0)) {
            return true;
        }

        var parenthesis = m.ContinuationColumn + facts.BareParenWidth - cast - (facts.BareParenDoubled ? 2 : 0);
        var v = 128 * parenthesis + 49 * first + 34 * cast - 16 * weight;
        var (breaks, keeps) = facts.BareParenDoubled ? (14749, 14767) : (14695, 14724);
        return v <= breaks ? true : v >= keeps ? false : null;
    }

    /// <summary>
    ///     Whether the <c>=</c> before <c>(a + b…).M(x)</c> or <c>(T)(a + b…).Length</c>, single or doubled, breaks behind
    ///     a head of twelve columns or more; null where the value fits beside it, or inside a band the measurement left to
    ///     the earlier rules (#598, SK-DIV-0830).
    /// </summary>
    /// <remarks>
    ///     ⚠ Measured 2026-11-13 with <c>Testing ask</c> on 220 056 statements. One score answers on both sides of the
    ///     receiver: <c>8·p + 3·min(f, 24) + 2·cast − w</c>, where <c>p</c> is a parenthesis's end on the line below
    ///     without its cast, <c>f</c> is the fragment the kept <c>=</c> would hold beside it, and
    ///     <c>w = min(n + 3, 69 − 2n)</c> is SK-DIV-0770's weight of the name the <c>=</c> assigns.
    ///     <list type="bullet">
    ///         <item>
    ///             Past the margin, the kept <c>=</c> holds the first operand and carries the member below it:
    ///             <c>φ = 3·min(a, 24) + 2·cast − w − 5·member</c>. The <c>=</c> breaks while <c>φ &lt; 0</c> (a call
    ///             with arguments while <c>φ ≤ 1</c>), and past that while <c>8·p + φ ≤ 907</c>, <c>p</c> the receiver's
    ///             <c>)</c> below; it stays from 916. A doubled parenthesis reads 16 more. A receiver whose <c>)</c> lands
    ///             one column past the margin breaks it whenever the value fits below.
    ///         </item>
    ///         <item>
    ///             While the receiver fits beside, the kept <c>=</c> holds the whole receiver and the dot takes the
    ///             break, so the first operand stops counting. With <c>e</c> the value's end below without its cast and
    ///             <c>s = 8·e + 2·cast − w</c>: a call breaks the <c>=</c> one column over when the head through its
    ///             space and the cast are wider than eighteen (round 6's rule), while its <c>(</c> lands on the margin or
    ///             one past it, and past that while <c>s − 13·args ≤ 843</c>, <c>args</c> the width between its
    ///             parentheses; it stays from 848. A property breaks it while <c>s ≤ 816</c> and keeps it from 821;
    ///             uncast behind a name of seven columns or less it never does.
    ///         </item>
    ///         <item>
    ///             ⚠ Names of one column follow neither: uncast, with arguments two columns wide or none, the
    ///             <c>=</c> breaks only while a call's <c>(</c> lands on the margin or one past it. Cast, behind a head of
    ///             21 columns or more through its space, a name of four or less is left to the earlier rules.
    ///         </item>
    ///     </list>
    ///     Between the bounds the earlier rules answer, and no measured statement they had right moves.
    /// </remarks>
    bool? ParenMemberEqualsBreaks(in GroupFacts facts, in Measures m) {
        var value = facts.ParenMember;
        var valueColumn = m.Column + m.PointWidth + 1;
        var end = valueColumn + value.Width;
        if (end <= width) {
            return null;
        }

        var over = end - width;
        var continuation = m.ContinuationColumn;
        var weight = Math.Min(value.Name + 3, 69 - 2 * value.Name);
        var receiverBeside = valueColumn + value.Receiver;
        if (receiverBeside <= width) {
            var score = 8 * (continuation + value.Width - value.Cast) + 2 * value.Cast - weight;
            var property = value.Arguments < 0;
            var head = valueColumn - (continuation - indentWidth);
            if (value.Cast > 0 && value.Name <= 4 && head > 20) {
                // ⚠ A short name behind a wide head (#598, SK-DIV-0873). One column over the gap between the type and the
                // name takes the line (BreakPlan.BreaksBeforeACastParenMemberOneOver). Past that a narrow cast keeps the
                // `=`: a property while `2·cast + 3·w ≤ 27`, a call with no arguments while `≤ 19` and its `(` lands
                // inside the margin. Elsewhere a call answers by the score below, and a property by the earlier rules.
                var narrow = 2 * value.Cast + 3 * weight;
                if (over == 1) {
                    return null;
                }

                if (property) {
                    return narrow <= 27 ? false : null;
                }

                if (value.Arguments == 0 && narrow <= 19 && receiverBeside + value.Head > width + 1) {
                    return false;
                }
            }

            if (value.Cast == 0 && value.Name <= 1 && value.Arguments <= 2) {
                return !property && over >= 2 && receiverBeside + value.Head <= width + 1;
            }

            if (property) {
                return value.Cast == 0 && value.Name <= 7 ? false : score <= 816 ? true : score >= 821 ? false : null;
            }

            if (over == 1) {
                return value.Arguments > 0 || head + value.Cast > 18;
            }

            if (receiverBeside + value.Head <= width + 1) {
                return true;
            }

            var call = score - 13 * value.Arguments;

            // ⚠ A value too wide for the line below breaks its `=` three more (#598, SK-DIV-0874): 844 to 846 break
            // there on every grid, where a value that fits below goes both ways.
            return call <= 843 || call <= 846 && continuation + value.Width > width ? true : call >= 848 ? false : null;
        }

        if (receiverBeside == width + 1 && continuation + value.Width <= width) {
            return true;
        }

        var fragment = 3 * Math.Min(value.Operand, 24) + 2 * value.Cast - weight - 5 * value.Member;
        if (fragment < 0 || fragment == 1 && value.Arguments > 0) {
            return true;
        }

        if (fragment == 0) {
            return null;
        }

        var keeps = 8 * (continuation + value.Receiver - value.Cast) + fragment - (value.Doubled ? 16 : 0);
        return keeps <= 907 ? true : keeps >= 916 ? false : null;
    }

    /// <summary>
    ///     Whether the <c>=</c> before a doubled parenthesis with a call after it, <c>((a + b…)).M(x)</c>, breaks behind a
    ///     head of seven to eleven columns; null where the earlier rules answer (#598, SK-DIV-0872).
    /// </summary>
    /// <remarks>
    ///     ⚠ Measured 2026-11-14 with <c>Testing ask</c> on 21 060 statements behind eight heads of 7 to 11 columns, every
    ///     one alike. Cast, the <c>=</c> breaks wherever the inner <c>)</c> lands one column past the margin, as a single
    ///     parenthesis's <c>)</c> does (Skala kept it for a call). Uncast, it never breaks once the inner <c>)</c> lands
    ///     two columns past the margin or further: the operator breaks (Skala broke the <c>=</c> for a call). A property
    ///     had both right already.
    /// </remarks>
    bool? ShortParenMemberEqualsBreaks(in GroupFacts facts, in Measures m) {
        var value = facts.ShortParenMember;
        if (!value.Doubled || value.Arguments < 0) {
            return null;
        }

        var inner = m.Column + m.PointWidth + 1 + value.Receiver - 1;
        if (value.Cast > 0) {
            return inner == width + 1 && Fits(m.ContinuationColumn, value.Receiver) ? true : null;
        }

        return inner >= width + 2 ? false : null;
    }

    /// <summary>
    ///     Whether a cast's <c>)</c> breaks before a parenthesised operator with one member after it, the whole value of a
    ///     <c>return</c> or an assignment: <c>return (IReadOnlyList&lt;int&gt;)</c> / <c>(a + b…).Count;</c> (#598,
    ///     SK-DIV-0871).
    /// </summary>
    /// <remarks>
    ///     ⚠ Measured 2026-11-14 with <c>Testing ask</c> on 127 080 statements behind <c>return</c> and assignments of 3 to
    ///     7 columns, casts of 3 to 35, five members, single and doubled. The oracle breaks there only at two places,
    ///     and only behind a head that keeps its <c>=</c> (a <c>return</c>, or an assignment of five columns or fewer):
    ///     <list type="bullet">
    ///         <item>
    ///             where the operator's <c>)</c> lands one column past the margin (a doubled one's inner <c>)</c>), while
    ///             <c>8·head + 8·cast − 3·min(a, 24) + 4·member</c> reaches 159 (151 doubled), <c>a</c> the first
    ///             operand and <c>member</c> its whole width;
    ///         </item>
    ///         <item>
    ///             where the receiver fits and a call's <c>(</c> lands one column past the margin, or on it with no
    ///             arguments, while <c>4·head + 3·cast + 2·min(args, 12) + [no arguments]</c> reaches 96; the first
    ///             operand does not count there.
    ///         </item>
    ///     </list>
    ///     Both are safe bounds: every measured row above them breaks, and 4 % of the rows that break lie below them.
    ///     ⚠ The head weighs twice the cast at the call's <c>(</c> and the same at the operator's <c>)</c>.
    /// </remarks>
    bool CastMemberBreaks(in GroupFacts facts, in Measures m, int lineStart) {
        var value = facts.CastMember;
        var head = m.Column - lineStart - 1;
        var receiver = m.Column + value.Receiver;

        // ⚠ Under a broken `=` the cast opens its line, and it breaks only where the operator's `)` lands one column
        // past the margin, by the same score with no head and the member counted at five: `string value =` /
        // `(IReadOnlyList<int>)` / `(a + b…).Count;`, both on the `=`'s continuation column.
        if (m.Column <= lineStart) {
            var below = 8 * value.Cast - 3 * Math.Min(value.Operand, 24) + 5 * value.Member;
            return receiver - (value.Doubled ? 1 : 0) == width + 1 && below >= (value.Doubled ? 159 : 167)
                || value.Doubled && receiver == width + 1 && below >= 151;
        }

        // ⚠ On the statement's line, only behind a head that keeps its `=`: a `return` (Name 0) or an assignment of
        // five columns or fewer. A local's (Name −1) breaks its `=` first.
        if (value.Name < 0 || value.Name > 0 && head > 5) {
            return false;
        }

        var score = 8 * head + 8 * value.Cast - 3 * Math.Min(value.Operand, 24) + 4 * value.Member;
        if (receiver - (value.Doubled ? 1 : 0) == width + 1) {
            return score >= (value.Doubled ? 151 : 159);
        }

        // ⚠ And a doubled parenthesis's outer `)` one column past the margin, its inner one on it.
        if (value.Doubled && receiver == width + 1) {
            return score >= 143;
        }

        if (receiver > width || value.Arguments < 0) {
            return false;
        }

        var paren = receiver + value.Head;
        return (paren == width + 1 || paren == width && value.Arguments == 0)
            && 4 * head + 3 * value.Cast + 2 * Math.Min(value.Arguments, 12) + (value.Arguments == 0 ? 1 : 0) >= 96;
    }

    /// <summary>Whether a cast's <c>)</c> breaks before a parenthesised operator (#598).</summary>
    /// <remarks>
    ///     ⚠ Measured 2026-10-10 with <c>Testing ask</c> on 450 rows at a statement indent of 8 — <c>x =</c> and
    ///     <c>return</c>, casts of 6 to 34 columns, first operands of 2 to 30 — and 576 more at 119 to 130 columns.
    ///     The oracle breaks there only where the operand's <c>)</c> lands one column past the margin (the same
    ///     column at which an <c>=</c> breaks before it, <see cref="GroupFacts.ParenCloseEnd" />), and then while
    ///     <c>3·s − a ≥ 64</c>, with <c>s</c> the width from the line's start through the cast's <c>)</c> and
    ///     <c>a</c> the operator's first operand: a wide cast behind a short first operand. 4 of the 450 miss.
    /// </remarks>
    bool BreaksAfterTheCast(in GroupFacts facts, in Measures m, int lineStart) =>
        m.FlatWidth < Unbounded
        && m.PointWidth < Unbounded
        && m.Column + m.FlatWidth == width + 1
        && Fits(m.ContinuationColumn, m.FlatWidth - m.PointWidth, m.Trailing)
        && 3 * (m.Column + m.PointWidth - lineStart) - facts.CastParenFirst >= 64;

    /// <summary>
    ///     Whether an arm's <c>when</c> breaks before a condition whose only break points are type argument lists
    ///     (#576, SK-DIV-0399). See <see cref="GroupFacts.WhenTable" />.
    /// </summary>
    /// <remarks>
    ///     ⚠ Measured 2026-10-10 with <c>Testing ask</c> on 600 arms — the <c>when</c> ending at columns 33 to 72,
    ///     the line below from 105 to 119 — and 324 more over three pattern heads, two body widths and widths of 60
    ///     to 128. The condition beside the <c>when</c> is never moved while it fits there through its last token:
    ///     the arrow's break is taken instead. Past that, before a body too wide to be read through the oracle always
    ///     breaks after the <c>when</c>, the condition fitting below or not; before one it reads through (a short
    ///     body, its arm's comma included), it breaks while <c>25·t + 6·h ≤ 3191</c>, with <c>t</c> the line below
    ///     and <c>h</c> the column the <c>when</c> ends at. That fit misses 2 of 590 cells. ⚠ A condition with no
    ///     break point at all always breaks once it does not fit beside (#609, fuzz 7256125207651206043): `when` /
    ///     `SomeVeryLongIdentifier… =&gt;` / `1,` — it has nowhere else to go.
    ///     <para>
    ///         ⚠ Round 7 (2026-10-14): the table holds for a body of three columns or fewer with its comma only. A
    ///         wider atom is not read through: the <c>when</c> breaks when the whole arm fits on the line below, and
    ///         otherwise only inside a band of head widths, <c>max(12, 45 − 6·b) ≤ head</c> and
    ///         <c>7·head ≤ 19·b + 105</c>, with <c>head</c> the arm through <c>when</c> and <c>b</c> the body with its
    ///         comma; before a body of fourteen or fewer, also only while the condition on the line below runs at most
    ///         <c>13 + (b − 5)/2</c> columns past the margin. Measured on 5 600 arms (SK-DIV-0399). The band's upper
    ///         limit was fitted on <c>Materialise&lt;List&lt;bool&gt;, IReadOnlyDictionary&lt;int, T…&gt;&gt;()</c>;
    ///         it moves with the condition's shape, which is not modelled.
    ///     </para>
    ///     <para>
    ///         ⚠ Round 8 (2026-11-13): "before a body too wide to be read through the oracle always breaks" holds only
    ///         from a head of twelve columns. Before a call, a creation or a parenthesised query behind a narrower head
    ///         it breaks while <c>head·(body − 6) ≥ 627</c> and fills beside the <c>when</c> otherwise, measured on
    ///         6 240 arms where the condition ends at 120 or past it on the line below and its lead through the first
    ///         comma is 23 columns or more (<see cref="GroupFacts.WhenWideBody" />, <see cref="GroupFacts.WhenLead" />).
    ///     </para>
    /// </remarks>
    /// <summary>
    ///     Whether a parenthesised query after an arm's arrow stays on the arrow's line, its <c>from</c> clause filling,
    ///     rather than moving below (#576 round 9, SK-DIV-0399). See <see cref="GroupFacts.ArmQueryBody" />.
    /// </summary>
    /// <remarks>
    ///     ⚠ Measured on 2 400 arms whose line through <c>where</c> overflows (eight #576 grids and a probe of queries in
    ///     six positions): the oracle keeps the body while <c>2·body &gt; arrow + 88</c>, the arrow's end read from the
    ///     start of its own line — not from the margin and not from the arm — 90 behind a head on one line. ⚠ That
    ///     origin is what made round 5 to 8's line-below limits "irregular across indents". Two gates: <c>(from item</c>
    ///     must fit beside the arrow; and under a broken <c>when</c>, an arrow line of 81 columns or fewer that leaves
    ///     two or fewer beside <c>(from item</c> moves the body below whatever its width (24 rows at indents 20 and 24,
    ///     none kept). No kept row is broken by either gate on any grid; 102 broken rows are kept by the oracle.
    /// </remarks>
    bool ArmQueryStays(in GroupFacts facts, in Measures m, int lineStart) {
        var head = m.Column + 1 + facts.ArmQueryHead;
        if (head > width) {
            // ⚠ #576 round 13: where `(from item` does not fit beside the arrow but `(from` does, the oracle still keeps
            // the body, `=> (from` / `item in items`, by the creation's two regimes (ArmNewStays) on its own constants:
            // measured on two structure grids (p1, p2: 4 608 arms, the arrow ending 100 to 118, lines starting 12 to 24).
            // While `4·a + 3·s ≤ 496` from `w + s ≥ 110` (narrower bodies there take `=> (` / the query, a point Skala
            // does not have, and stay broken), else from `18·w + 6·a − 11·s ≥ 2756` — the creation's second bar with the
            // arrow four columns further on.
            var arrowEnd = m.Column;
            if (arrowEnd + 1 + "(from".Length > width) {
                return false;
            }

            var w = facts.ArmQueryBody;
            return 4 * arrowEnd + 3 * lineStart <= 496
                ? w + lineStart >= 110
                : 18 * w + 6 * arrowEnd - 11 * lineStart >= 2756;
        }

        var arrow = m.Column - lineStart;
        if (WhenGapBroke(facts.ArmQueryWhenGap) && width - head <= 2 && arrow <= 81) {
            return false;
        }

        var oneLine = facts.LiftGroup < 0 || !resolved[facts.LiftGroup] || enteredOn[facts.LiftGroup] == m.Line;
        return 2 * facts.ArmQueryBody > arrow + (oneLine ? 90 : 88);
    }

    /// <summary>
    ///     Whether an object creation after an arm's arrow keeps <c>new</c> on the arrow's line, breaking after it, rather
    ///     than moving below whole (#576 round 13, SK-DIV-0399). See <see cref="GroupFacts.ArmNewBody" />.
    /// </summary>
    /// <remarks>
    ///     ⚠ Measured 2026-11-14 with <c>Testing ask</c> on four structure grids (n1–n4: 14 196 arms, the arrow ending 100
    ///     to 116, lines starting 12 to 28, bodies of 80 to 146, five creation shapes). It is not a point Skala lacked but an
    ///     arrow the oracle leaves flat: <c>=&gt; new</c> / <c>Holder(…)</c>, the type arguments then unlifted. Only once
    ///     <c>new Holder(</c> no longer fits beside the arrow (where it fits, the arguments chop there, as before) and
    ///     <c>=&gt; new</c> does. With <c>a</c> the arrow's end, <c>s</c> its line's start and <c>w</c> the body:
    ///     <list type="bullet">
    ///         <item>
    ///             while <c>4·a + 3·s ≤ 504</c>, it breaks after <c>new</c> from <c>20·w − 5·a + 16·s ≥ 1522</c>, the
    ///             body fitting below in both layouts;
    ///         </item>
    ///         <item>and wherever <c>18·w + 6·a − 11·s ≥ 2732</c>, where the arguments chop in both.</item>
    ///     </list>
    ///     ⚠ Neither the type's name, its type arguments, the arguments' count nor the head before the arrow moves either
    ///     edge: five creation shapes flip at the same body width to the column. ⚠ And the choice is not a cost of the final
    ///     lines: where the arguments chop the two layouts differ only in their first two lines, yet the choice moves with
    ///     the body's width (the second bar).
    /// </remarks>
    bool? ArmNewStays(in GroupFacts facts, in Measures m, int lineStart) {
        var arrow = m.Column;
        if (arrow + 1 + facts.ArmNewHead <= width || Fits(arrow, 1 + facts.ArmNewBody + facts.ArmBodyTrail)) {
            return null;
        }

        if (arrow + 4 > width) {
            return false;
        }

        var w = facts.ArmNewBody;
        return 4 * arrow + 3 * lineStart <= 504 && 20 * w - 5 * arrow + 16 * lineStart >= 1522
            || 18 * w + 6 * arrow - 11 * lineStart >= 2732;
    }

    /// <summary>Whether an arm's planned <c>when</c> gap broke: −2 is one the author broke. See GroupFacts.ArmQueryWhenGap.</summary>
    bool WhenGapBroke(int gap) => gap == -2 || gap >= 0 && resolved[gap] && modes[gap] == ResolvedMode.Broken;

    bool WhenBreaks(in GroupFacts facts, in Measures m, int tail, int lineStart) {
        if (m.FlatWidth >= Unbounded || Fits(m.Column, m.FlatWidth)) {
            return false;
        }

        if (facts.WhenAtom) {
            return true;
        }

        // ⚠ Under an arrow the author broke, the tail rule: `when Materialise<List<bool>,` / `…>>()` / `=> body`
        // stays beside the `when` where the condition does not fit below (measured on 108 arms of the #576 grid).
        // ⚠ Round 12: refuted for a generic call condition under a break after the `=>`, which fills beside the `when` far
        // short of the line below; see KeptArrowWhenBreaks. A break before the `=>` keeps the tail rule, unmeasured.
        if (facts.WhenArrowKept) {
            return facts.WhenArrowKeptAfter && facts.WhenLead > 0 && tail < Unbounded
                ? KeptArrowWhenBreaks(facts, m, tail, lineStart)
                : TailFits(m, tail);
        }

        if (facts.WhenBody <= 0) {
            // ⚠ Before a call, a creation or a parenthesised query, behind a head under twelve columns, the `when`
            // breaks only from a body width that falls as the head widens: `head·(body − 6) ≥ 627` (#576 round 8,
            // SK-DIV-0399). Short of it the type arguments fill beside the `when`, the rest of the arm as it is under a
            // broken `when`. ⚠ Round 9: a lead through the first comma under 23 columns is not "always breaks", as round 8
            // left it — it breaks a few columns of body earlier, and `head·(body − 6) ≥ 550` is the bar no measured break
            // falls under (6 239 arms behind heads 6 to 11: misses 1 112 → 455, none newly wrong). Rounds 8 to 10 applied
            // it only to a condition ending at 120 or past it on the line below; round 11 replaced that guard (below).
            var head = m.Column - lineStart;
            // ⚠ Round 11: "a condition that fits on the line below moves there whatever the body" is refuted. What the
            // oracle reads is the condition beside the `when`: one or two columns past the margin there it moves below
            // (as an atom body's does); three or more and the product bar holds, the line below or not — `h + X ≥ 122`
            // on every grid, heads 7 to 10 at conditions ending 112 to 115 below (53 rows of the fresh probes, none
            // the other way). Where it fits below the bar is the product alone: the fill's first-line raise is for a
            // condition that fills in both layouts.
            if (facts.WhenWideBody > 0
                && facts.WhenLead > 0
                && head < 12
                && tail < Unbounded
                && !Fits(m.Column, m.FlatWidth - 2)) {
                if (head * (facts.WhenWideBody - 6) < (facts.WhenLead >= 23 ? 627 : 550)) {
                    return false;
                }

                if (Fits(m.ContinuationColumn, tail + 1)) {
                    return true;
                }

                // ⚠ Round 10: the bar rises with what the `when`'s line keeps of the condition when it fills there — the
                // lead through the outer comma while the rest, through `() =>`, fits below by 117, else through the last
                // type-argument break point. With the widths held fixed, the shape of the second argument (`T…`,
                // `IReadOnlyList<T…>`, `IReadOnlyDictionary<int, T…>`) moves nothing where the fill takes the outer
                // comma; the first line's width does, a third of a column of body per column up to a first line of 42,
                // and the 1/head law holds with an offset: the `when` breaks only while
                // `6·head·(body + 6) ≥ 3816 + 2·min(first, 42)·head` (three structure grids and 12 000 arms of eleven
                // more, raising only: no row the oracle breaks falls under it). ⚠ Uncapped, the first fresh probe
                // written after the rule found a 47-column lead the oracle breaks behind two columns under the bar.
                // ⚠ Round 11: where the fill takes the outer comma and the condition ends by 146 below, the raise stops
                // at a first line of 34 — the structure grid e1 saturates there (leads 26 to 38 flip at one body width),
                // and 42 broke the `when` too late behind `var v when` for a 38-column lead (the coordinator's probe, seed
                // 5769999) and a 36-column one (f576i). Past 34 the bar is reached strictly past `68·head`: rows of e1, e4
                // and s576 sit on `6·head·(body + 6) − 3816 = 68·head` and fill. Past an end of 146 two rows of f576e
                // with leads of 41 and 54 hold 42; any bound from 140 to 153 is clean on every grid.
                var conditionEnd = m.ContinuationColumn + tail;
                var inner = conditionEnd - facts.WhenLead + 2 > 117 && facts.WhenLastPoint > facts.WhenLead;
                var first = inner ? facts.WhenLastPoint : facts.WhenLead;
                var raise = inner || conditionEnd > 146 ? 2 * Math.Min(first, 42) * head
                    : first > 34 ? 68 * head + 1
                    : 2 * first * head;
                if (6 * head * (facts.WhenWideBody + 6) < 3816 + raise) {
                    return false;
                }

                // ⚠ Round 11: where the fill must take an inner `<` the bar rises with the condition's flat width, a
                // column and a half of body per column of condition and one per column of lead, past a knee that moves
                // with the head: `head·(2·body − 2·lead − 3·X + 658) ≥ 2960`, X the condition's end flat from the arm's
                // own column. Measured on two structure grids (e4, e5: heads 6 to 11, conditions ending 128 to 184,
                // leads 14 to 38, indents 12 and 20, 5 568 arms), where I and the condition's width move the flip
                // identically and every rising edge lies on the line; on no grid does a row the oracle breaks fall
                // under it. This is "how far the type arguments run past the margin" round 10 found on e2.
                if (inner && head * (2 * facts.WhenWideBody - 2 * facts.WhenLead - 3 * conditionEnd + 670) < 2960) {
                    return false;
                }

                // ⚠ And behind a one-column pattern (`X when`, `_ when`): round 10's "sixteen columns short of where
                // the condition ends below" broke up to twenty columns of body too early once the condition ends past
                // 124. Round 11: `2·body ≥ 3·end + 2·last − lead − 214`, the end being where the condition ends on the
                // line below and `last` the condition through its last type-argument break point — the same column and
                // a half of body per column, and a deeper second argument (`IReadOnlyDictionary<int, T…>` against
                // `IReadOnlyList<T…>` against `T…`) raising it a column per column (254 arms behind `X when` and
                // `_ when` on every grid: no row the oracle breaks under it, 20 the oracle fills over it).
                return head > 6
                    || 2 * facts.WhenWideBody >= 3 * conditionEnd + 2 * facts.WhenLastPoint - facts.WhenLead - 214;
            }

            return true;
        }

        // ⚠ A condition one or two columns past the margin beside the `when` moves below, whatever the head or
        // the body (#576 round 7, SK-DIV-0399).
        if (Fits(m.Column, m.FlatWidth - 2)) {
            return true;
        }

        // ⚠ A body of four columns or more with its comma is not read through: the `when` breaks inside a band
        // of head widths, the head being the arm through `when`, from twelve columns (more before the narrowest
        // bodies) to a limit that rises 19/7 of a column per column of body. Past it the type arguments fill
        // beside the `when`. Measured with `Testing ask` on Materialise<List<bool>, IReadOnlyDictionary<int, T…>>()
        // and three more conditions with their first argument 1 to 32 columns wide (#576 round 7, SK-DIV-0399).
        var body = facts.WhenBody - 4;
        if (body >= 4) {
            // The whole arm fits on the line below: it moves there.
            if (tail < Unbounded && m.ContinuationColumn + tail + facts.WhenBody <= width) {
                return true;
            }

            // ⚠ And, before a body of fourteen columns or fewer, only while the condition, flat on the line below, runs
            // at most 13 columns past the margin, half a column more per column of body past five: wider, it fills
            // beside the `when`. Measured on bodies of four and eight; a wider body was always seen to break.
            // ⚠ Round 10: not from a body of eleven columns with its comma, behind a lead under 39 columns: there the
            // `when` breaks however far past the margin the condition runs (the band grid e3, bodies of 9 to 16 and
            // conditions 6 to 60 columns past at three heads, and 28 rows of the fresh probes, bodies of 14). The limit
            // was measured at bodies of 5 and 9, so a body of 9 or 10 keeps it; leads of 39 or more both stop and break.
            if (body <= 14
                && tail < Unbounded
                && 2 * (m.ContinuationColumn + tail - width) > 26 + body - 5
                && !(body >= 11 && facts.WhenLead is > 0 and < 39)) {
                return false;
            }

            var head = m.Column - lineStart;
            return head >= Math.Max(12, 45 - 6 * body) && 7 * head <= 19 * body + 105;
        }

        if (tail >= Unbounded) {
            return false;
        }

        var below = m.ContinuationColumn + tail + facts.WhenBody;
        return 25 * below + 6 * m.Column <= 3191;
    }

    /// <summary>
    ///     Whether an arm's <c>when</c> breaks before a generic call condition when the author broke the arm after its
    ///     arrow (#576 round 12, SK-DIV-0399). The condition does not fit beside the <c>when</c> here.
    /// </summary>
    /// <remarks>
    ///     ⚠ Measured 2026-11-14 with <c>Testing ask</c> on two structure grids (k1: indents 12 and 20, heads 6 to 23, leads
    ///     5 to 34, the condition ending 119 to 136 beside the <c>when</c>, three bodies; k2: indent 12, heads 6 to 28, leads
    ///     4 to 38, ends 121 to 142; 12 726 arms). ⚠ The old rule, "the condition moves below exactly when it fits there", is
    ///     refuted: the oracle fills beside the <c>when</c> at conditions far narrower than the line below. The body plays
    ///     no part (it is on its own line) and neither does the indent once the condition is read where it ends below:
    ///     <list type="bullet">
    ///         <item>
    ///             Behind a head under twelve columns the <c>when</c> breaks only where the condition runs one or two
    ///             columns past the margin beside it — and never behind <c>X when</c> with a lead of 14 or more.
    ///         </item>
    ///         <item>
    ///             From twelve, it breaks while <c>12·below + 4·min(lead, 26) − head ≤ 1405</c>, <c>below</c> being where the
    ///             condition ends on the line below; or, for a lead short against the head (<c>3·lead − head ≤ 23</c>), while
    ///             <c>12·below − 8·lead − head ≤ 1381</c> — such a condition moves below until the fill beside the <c>when</c>
    ///             would have to take its inner <c>&lt;</c>.
    ///         </item>
    ///     </list>
    ///     Both bars were searched over integer coefficients under the constraint that no arm the old rule had right goes
    ///     wrong. The unconstrained best (<c>… ≤ 1401</c>, <c>8·below − 6·lead − head ≤ 911</c>) misses 36 of k2 against
    ///     these bars' 74, all one column off, but four of its misses were arms the old rule had right. On the twelve fresh
    ///     probes these bars miss 3 of 1 821 kept-arrow arms — each one the old rule missed too, two behind a nested first
    ///     argument (<c>Func&lt;int, bool&gt;,</c>), which the grids did not hold.
    /// </remarks>
    bool KeptArrowWhenBreaks(in GroupFacts facts, in Measures m, int tail, int lineStart) {
        var head = m.Column - lineStart;
        var lead = facts.WhenLead;
        var beside = Fits(m.Column, m.FlatWidth - 2);
        if (head < 12) {
            return beside && !(head <= 6 && lead >= 14);
        }

        if (beside) {
            return true;
        }

        var below = m.ContinuationColumn + tail;
        return 12 * below + 4 * Math.Min(lead, 26) - head <= 1405
            || 3 * lead - head <= 23 && 12 * below - 8 * lead - head <= 1381;
    }

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

    /// <summary>The head <see cref="HeadIsWideEnough" /> measures, through the group's own point.</summary>
    int HeadWidth(in GroupFacts facts, in Measures m, int lineStart) {
        var owner = facts.Owner;
        if (owner < 0) {
            return m.PointWidth;
        }

        var from = resolved[owner] && enteredOn[owner] == m.Line ? enteredAt[owner] : lineStart;
        return m.Column + m.PointWidth - from;
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
            // ⚠ Unless the line through the `{` does not fit, which leaves the braces nothing to break for (#623,
            // fuzz 9076957179953969410): the oracle breaks the `=`, and so did Skala's second pass, once the first
            // had kept it with `= new {` at 124 columns and the brace's break made it a broken initializer.
            if (m.PointWidth < Unbounded && !Fits(m.Column, m.PointWidth + m.AfterPoint)) {
                return ResolvedMode.Broken;
            }

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
        // ⚠ Only while the list's own `<` fits (fuzz 354857589735471722; GroupFacts.ColonTypeOpen). Filling a list
        // whose `<` is past the margin leaves the line long. ⚠ Not AfterPoint, which this read first: it reads
        // through the list's points to the call's `(` whenever an argument list after it can break, and so
        // broke colons #490's own fixture fills. Measured 2026-11-13 with `Testing ask`, names of 4, 6 and 9,
        // bare, `ref` and `out`, the `<` at 104 to 128: the oracle breaks the colon — or the keyword after it —
        // from the `<` at 122 on, and at 121 alone moves the `<` itself down, which Skala has no point for and
        // answers as at 122.
        // Pass one filled `name59: ref Cast_w…<` / `byte, object>()` at 125 columns; pass two read the break after
        // the `<` as the author's, had no flat width to compare with the floor, and broke.
        if (facts.YieldsToOverflowingTypeArguments
            && m.YieldEnd > 0
            && m.PointWidth < Unbounded
            && !Fits(m.Column, m.PointWidth + m.YieldEnd)
            && (facts.ColonTypeOpen <= 0 || Fits(m.Column, m.PointWidth + 1 + facts.ColonTypeOpen))) {
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
    ///     Whether a single one-argument call that is a whole <c>=</c> value, its line one column past the margin, moves
    ///     below at its dot (<c>r</c> / <c>.M(a);</c>) rather than chopping its argument (<c>r.M(</c> / <c>a</c> /
    ///     <c>);</c>): by the <c>(</c>'s 1-based column and the 0-based column the value starts on (#446).
    /// </summary>
    /// <remarks>
    ///     ⚠ Measured 2026-11-13 with <c>Testing ask</c> on 4 032 cells: the <c>(</c> swept a column at a time from 84
    ///     to 107, beside a kept <c>=</c> behind <c>var</c>, assignment, <c>this.</c> and <c>string</c> heads of 5 to 12
    ///     columns and below a broken one behind typed locals, <c>var</c> and <c>string</c> heads, methods of 2, 6 and
    ///     14 columns, indents 8 to 24. The method's width plays no part, and the indent and the head only through the
    ///     column the value starts on. #528's rule moved the dot at any <c>(</c>; the oracle chops wherever the
    ///     <c>(</c> lands short of about 92, which leaves the argument a line of 26 columns or more.
    ///     <c>10·paren − 3·column ≥ 871</c> is the bound under which no measured cell moves the dot: 1 362 of the 1 431
    ///     chopped cells lie under it, and the 69 above it stay as #528 had them.
    ///     ⚠ Two or more arguments move it too, by a bound of their own, <c>5·paren − column ≥ 483</c> (#446, SK-DIV-0211
    ///     round 9), where #528 chopped them at any <c>(</c>. On the eleven random probes' 1 734 such cells (heads of 1 to
    ///     63) the oracle chops every one at 480 or under and moves the dot at all 19 from 485. The one-argument form with
    ///     a bound of its own (943) separated those too, and a test cell refuted it (<c>var nmm =</c>, the <c>(</c> at 100).
    ///     ⚠ Round 12 (#446, SK-DIV-0211): one argument's bound is <c>11·paren − 3·column ≥ 966</c>. Behind narrow heads
    ///     (g41, g42 and g43: heads of four to nine, every callee from 61 to 111, indents 8 to 20; 2 518 cells one column
    ///     over) it separates every cell, where 871 put nineteen a column wrong at a <c>(</c> of 94 to 96 — the two
    ///     bounds cross at a value column of 25. ⚠ Round 8's lifted exclusion of a call argument had exposed it: master
    ///     chopped <c>var ndgp = r(63).M(x.Y(z));</c> only because it held no call argument at all.
    /// </remarks>
    static bool OneOverMovesTheDot(int paren, int column, bool many = false) =>
        many ? 5 * paren - column >= 483 : 11 * paren - 3 * column >= 966;

    /// <summary>
    ///     Whether a call of one or two short arguments on a parenthesised operator, its <c>(</c> fitting beside a head
    ///     that keeps its <c>=</c> or <c>return</c>, breaks its dot rather than chop: <c>x = (T)(a + b…)</c> /
    ///     <c>.M(q);</c> (#598, round 12).
    /// </summary>
    /// <remarks>
    ///     ⚠ Measured 2026-11-14 with <c>Testing ask</c> (grid d1, 3 600 cells; c5, c2): five heads of 3 to 6 columns,
    ///     casts of 0 to 13, arguments of 1 to 4 columns, the <c>(</c> on columns 118 to 120, indents 8, 12 and 16, nested
    ///     by <c>if</c> blocks and by namespaces alike. ⚠ The dot breaks only where the statement starts on column 8: at 12
    ///     and 16, by either nesting, every cell chops. There it is a window in the receiver's column <c>r</c> (0-based)
    ///     and the <c>(</c>'s <c>p</c> (1-based), with <c>a</c> the argument's width:
    ///     <c>r + p ≥ 127 + 4·a</c> and <c>3·p − 2·r ≥ 312 + 4·a</c>. Over the measured cells it is exact; from three
    ///     columns of argument the window is empty, and the method's name plays no part.
    /// </remarks>
    /// <summary>
    ///     Whether the <c>=</c> of a local or an assignment of seven to eleven columns stays before one argument on a
    ///     parenthesised operator whose <c>(</c> fits beside it, the arguments chopping: <c>var x = (a + b…).M(</c> /
    ///     <c>q</c> / <c>);</c> (#598, round 12).
    /// </summary>
    /// <remarks>
    ///     ⚠ Measured 2026-11-14 with <c>Testing ask</c> (grid d2, 4 374 cells; c5 and the fresh probes, 10 044 rows in
    ///     all): nine heads of 7 to 11 columns, casts of 0 to 26, arguments of 1 to 10 columns, the <c>(</c> on columns 105
    ///     to 120, indents 8 to 20. ⚠ The decision reads the head, the indent, the argument's width and the <c>(</c>'s
    ///     column only: no row in 4 497 groups changes with the cast, the method's name or the first operand, which is
    ///     why the round-8 forms, built on the cast and the value's end, could not separate it. Behind nine columns or
    ///     more the <c>=</c> always breaks; behind seven and eight,
    ///     <c>16·indent + 22·argument − 170·[head = 8] − 3·name − 4·paren ≥ −226</c> keeps it, exact on every row: a
    ///     column of indent is worth four of the <c>(</c>, and the eighth column of head forty.
    /// </remarks>
    static bool ShortHeadKeepsBeforeOneArgument(int head, int indent, int argument, int name, int paren) =>
        head <= 8 && 16 * indent + 22 * argument - (head == 8 ? 170 : 0) - 3 * name - 4 * paren >= -226;

    /// <summary>
    ///     <see cref="ShortHeadKeepsBeforeOneArgument" /> for two or three arguments: the <c>=</c> of a local or an
    ///     assignment of ten or eleven columns stays and the arguments chop (#598, round 12).
    /// </summary>
    /// <remarks>
    ///     ⚠ Measured 2026-11-14 with <c>Testing ask</c> (grids d3 and d4, 7 284 cells, and every #598 grid with such a
    ///     call: 7 772 rows): heads of 7 to 9 columns always keep it. Behind ten and eleven,
    ///     <c>12·indent + 15·arguments − 60·[head = 11] − 3·name − 4·paren − 5·[three arguments] ≥ −333</c> keeps it,
    ///     exact on every row; again nothing else is read, the cast included.
    /// </remarks>
    static bool ShortHeadKeepsBeforeArguments(int head, int indent, int arguments, int name, int paren, int count) =>
        head <= 9
        || head <= 11
        && 12 * indent + 15 * arguments - (head == 11 ? 60 : 0) - 3 * name - 4 * paren - (count >= 3 ? 5 : 0) >= -333;

    static bool ShortArgumentMovesTheDot(int arguments, int paren, int column, int lineStart) =>
        lineStart == 8
        && arguments is >= 1 and <= 2
        && column + paren >= 127 + 4 * arguments
        && 3 * paren - 2 * column >= 312 + 4 * arguments;

    /// <summary>
    ///     The held lambda call's line below, as its limit reads it: a line past the margin is read as the margin once a
    ///     rest of the chain follows the call (#619).
    /// </summary>
    /// <remarks>
    ///     ⚠ Measured 2026-11-13 with <c>Testing ask</c> on 4 374 cells: <c>var</c>, assignment and
    ///     <c>IEnumerable&lt;int&gt;</c> heads whose names keep the <c>=</c>, receivers of 1, 3 and 6, indents 8 and 16, the
    ///     held line below at 117 to 129, and the rest after the call at every width from 14 to 40. The rest from which
    ///     the oracle moves the call below is the same at every line from 120 to 129, which #605's limit already gives at
    ///     120, and Skala asked from one to eleven columns more of rest for each column of line past the margin. A line
    ///     that fits is read as it is, and without a rest nothing changes: <c>HeldLambdaLimit</c> alone stays short of
    ///     the margin.
    ///     ⚠ Not behind a head and receiver of eleven columns or fewer, <c>nnnnnnn = r</c>: there the oracle never moved
    ///     an overflowing call, at rests up to 40, and from twelve it did (<c>nnnnnnnn = r</c>, <c>nnnnnn = rrrr</c>).
    /// </remarks>
    int HeldLambdaLine(bool lambda, int rest, int reach, int head, int line) =>
        lambda && rest > 0 && reach >= 12 && ReadsPastTheMargin(head, line) ? width : line;

    /// <summary>
    ///     Whether a held lambda call's line is read past the margin (<see cref="HeldLambdaLine" />,
    ///     <see cref="HeldLambdaLimitPast" />): two columns over or more, under any call head.
    /// </summary>
    /// <remarks>
    ///     ⚠ One column over is the lone <c>)</c> #605 measured, which its limit already reads. ⚠ Round 3 scoped this to
    ///     heads of seven to nine because <c>.SkipWhile(</c> and <c>.Any(</c> newly diverged 25 statements on two fresh
    ///     probes. Refuted as a head question (#619 round 4): those were the moved call's own layout — the arrow's floor
    ///     and the operands' level — and with it fixed every head from 4 to 14 reads the margin, together with the line as
    ///     it is (<see cref="HeldLambdaMovesAsItIs" />).
    /// </remarks>
    bool ReadsPastTheMargin(int head, int line) => line > width + 1 && head > 0;

    /// <summary>
    ///     <see cref="HeldLambdaLimit" /> for a held line past the margin, where the call's head turns from raising the
    ///     limit to lowering it: 0.4 a column of head past <c>.Where(</c>'s seven (#619).
    /// </summary>
    /// <remarks>
    ///     ⚠ Found by the first fresh probe of <see cref="HeldLambdaLine" /> (seed 70913, 5 000 statements): read with
    ///     #605's head term, <c>.OrderBy(</c>, <c>.Select(</c> and <c>.SkipWhile(</c> moved overflowing calls the oracle
    ///     keeps beside the <c>=</c> in 78 statements. Measured then on 1 728 cells under <c>.Any(</c>,
    ///     <c>.Select(</c>, <c>.OrderBy(</c> and <c>.SkipWhile(</c>, heads of 5 to 11 columns, the rest at every width
    ///     from 14 to 40: the oracle needs two thirds of a column more rest per column of head, and the term below
    ///     reproduces every one of its 32 thresholds measured on a line of 130.
    ///     ⚠ That term is the cap's alone (#619 round 5). Where the receiver ends far right and the floor decides, past
    ///     the margin the floor sits 0.3 below its own reading at <c>.Where(</c> and rises only 0.15 a column of head:
    ///     on 8 000 cells of names of 20 to 40 and receivers of 6 to 14 under heads of 4 to 15, with seven probes, the
    ///     head term on the whole limit missed 56 decisions past the margin and the two pieces 16 — the fresh probe's
    ///     <c>.All(</c> behind a 37-column name and a 13-column receiver among them.
    /// </remarks>
    double HeldLambdaLimitPast(int column, int receiver, int head, int line) =>
        ReadsPastTheMargin(head, line)
            ? Math.Max(
                HeldLambdaCap(column, receiver) - 0.4 * (head - 7),
                HeldLambdaFloor(column, receiver) - 0.3 + 0.15 * (head - 7)
            )
            : HeldLambdaLimitWithin(column, receiver, head);

    /// <summary>
    ///     Whether a held lambda call whose line runs past the margin moves below by #605's limit read with the line as
    ///     it is (#619): the oracle moves it when either this or <see cref="HeldLambdaLimitPast" />'s reading says so.
    /// </summary>
    /// <remarks>
    ///     ⚠ Measured 2026-11-13 with <c>Testing ask</c> on 6 000 cells under call heads of 4 to 14 columns (<c>.Ab(</c>
    ///     to <c>.Abcdefghijkl(</c>), lines of 121 to 132 and rests of 10 to 42, with two random probes of 11 000: the
    ///     margin's reading alone missed 48 of 10 200 decisions past the margin, 41 of them under heads of 10 to 14 at
    ///     lines of 122 to 125, where the oracle's threshold falls about a column of rest per column of line — #605's
    ///     own slope. With the line also read as it is, 10 miss, none of them more than a column from the boundary.
    ///     Under heads of nine or fewer the margin's reading is the larger and this changes nothing.
    /// </remarks>
    bool HeldLambdaMovesAsItIs(int column, int receiver, int head, int line, double terms) =>
        ReadsPastTheMargin(head, line) && line <= HeldLambdaLimit(column, receiver, head) + terms;

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
    /// <summary>
    ///     The widest line a held first call with a lambda for its one argument may take below, by the receiver's
    ///     column on its line, the receiver's width and the call's head from its dot through its <c>(</c> (#605).
    /// </summary>
    /// <remarks>
    ///     ⚠ Measured with <c>Testing ask</c> on
    ///     <c>var n… = r….Where(x =&gt; a &amp;&amp; b &amp;&amp; c).ToList();</c>,
    ///     receivers of 2 to 60, the line below one column at a time, names of 1 to 60 at indents 8, 12 and 16 —
    ///     16 068 cells, 670 of them with a boundary inside the grid — and then under callees of 3 to 19 columns
    ///     (6 660 cells, 257 boundaries). Every boundary is a whole column, so each piece was solved exactly rather
    ///     than fitted:
    ///     <list type="bullet">
    ///         <item>a cap falling 0.14 a column of the receiver's column;</item>
    ///         <item>under it, a line falling 0.38 a column of receiver past about 22 columns of it;</item>
    ///         <item>
    ///             and above both, where the receiver ends far right, a floor rising 0.55 a column of receiver and 0.42
    ///             of its column, bent to 0.2 and 0.44 once it passes the line the cap gives way to.
    ///         </item>
    ///     </list>
    ///     ⚠ A wider call head raises the cap and the line 0.6 a column and the floor 1.18: <c>.Where(</c>'s seven
    ///     columns are the origin. ⚠ The receiver's column counts the indent. Round 3's rule measured the head from
    ///     the statement's start; on its own grid it missed 258 rows, one column apart, and this one 6 (#605,
    ///     group I).
    /// </remarks>
    static double HeldLambdaLimit(int column, int receiver, int callHead) =>
        Math.Max(
            HeldLambdaCap(column, receiver) + 0.6 * (callHead - 7),
            HeldLambdaFloor(column, receiver) + 1.18 * (callHead - 7)
        );

    /// <summary>
    ///     <see cref="HeldLambdaLimit" /> within the margin, its cap and floor solved again exactly (#619 round 6), the line
    ///     read no further than <see cref="HeldLambdaSaturation" />.
    /// </summary>
    /// <remarks>
    ///     ⚠ #605 solved each piece on its own grid. Measured again on 2 900 random series outside group I's region (six
    ///     heads, indents 8 to 20, names of 3 to 40, receivers of 1 to 14, fourteen callees, four tails, rests of 8 to 42,
    ///     three bodies; the line below swept a column at a time across the limit, within the margin and past it to 140)
    ///     and every held decision of ten random probes, the exact largest-margin cap is 114.9535 − 0.13947 a column of the
    ///     receiver's column − 0.01682 a column of receiver, plus 0.59743 a column of call head past seven, and the floor
    ///     69.9958 + 0.44470 of column + 0.56591 of receiver, plus 1.18106 of call head (#605's far-right cell at column
    ///     75 held as a constraint): every series and cell on the right side, by 0.0010 and 0.0072 of a column. #605's
    ///     pieces missed 81 series of the first grid and 62 statements of the probes, each within a column. The second
    ///     line of each piece was not reached and is kept. Used for a call with a rest after it, behind a reach of twelve
    ///     or more, within the margin and past it alike; elsewhere <see cref="HeldLambdaLimitPast" /> keeps #605's.
    /// </remarks>
    static double HeldLambdaLimitWithin(int column, int receiver, int callHead) =>
        Math.Max(
            Math.Min(114.953511 - 0.139466 * column - 0.016815 * receiver, 123.35 - 0.38 * receiver - 0.13 * column)
            + 0.597428 * (callHead - 7),
            Math.Min(69.995833 + 0.565909 * receiver + 0.444697 * column, 78.94 + 0.2 * receiver + 0.441 * column)
            + 1.181061 * (callHead - 7)
        );

    /// <summary>
    ///     The furthest a held lambda call's line below is read within the margin (#619 round 6): 113 plus the call's head,
    ///     where the body line below a kept call — <c>.Where(x =&gt;</c> held, the body a level in — would reach column 111.
    ///     Past it the oracle answers as it does there, so a limit that reaches it moves the call, and group I's plateau
    ///     breaks the <c>=</c>, to the margin.
    /// </summary>
    /// <remarks>
    ///     ⚠ Found as group I's "jump" (no threshold of 120 under <c>.Where(</c>, none of 118 to 120 under <c>.Any(</c>)
    ///     and confirmed on the held call itself: the refit limit separates every series exactly only with the line read
    ///     this way. ⚠ Past the margin it is the whole story: rounds 3 to 5 read a held line past the margin as the margin
    ///     (120, which is 113 plus <c>.Where(</c>'s seven) and as it is, with the call head's term turned over. With the line
    ///     read at the saturation and the one limit, 20 834 of 20 837 held decisions past the margin in nine random probes
    ///     agree (the three left are within 0.01 of the line), where those readings missed 12.
    /// </remarks>
    static int HeldLambdaSaturation(int callHead) => 113 + callHead;

    /// <summary>
    ///     Whether a held lambda call behind a local's or an assignment's kept <c>=</c> stays on the <c>=</c>'s line whatever
    ///     its line below (#619 round 7): its <paramref name="limit" /> leaves the whole value — receiver, call and rest —
    ///     ending before column 113 on the line below.
    /// </summary>
    /// <remarks>
    ///     ⚠ Measured 2026-11-14 with <c>Testing ask</c> on 1 200 random series outside group I whose value fits below, the
    ///     held call's line swept a column at a time from six under the limit to two past it (8 296 statements: six heads,
    ///     indents 8 to 20, names of 3 to 40, receivers of 1 to 14, fourteen callees, rests of 5 to 14), with round 6's
    ///     probes and two more grids. Of the 11 760 statements behind a kept <c>=</c> whose line is within the limit, the
    ///     oracle moves the call in 11 654 and breaks the arrow in 106 — every one where the receiver, the limit and the rest
    ///     add to 112.98 or less, and none of the moves under 113.06. Only short rests and short receivers behind long names
    ///     get there: a value whose line at the limit would end so far left fits below, and the oracle breaks the <c>=</c>
    ///     or the arrow instead of moving the call (<see cref="HeldChainValueBreaksTheEquals" />). Past the margin below it
    ///     cannot bite: a line within the limit then ends the value past 120.
    /// </remarks>
    static bool HeldLambdaStaysBesideTheEquals(int receiver, double limit, int rest) => receiver + limit + rest < 113;

    /// <summary>Whether <see cref="HeldLambdaLimitWithin" />'s cap decides rather than its floor.</summary>
    static bool HeldLambdaCapDecides(int column, int receiver, int callHead) =>
        Math.Min(114.953511 - 0.139466 * column - 0.016815 * receiver, 123.35 - 0.38 * receiver - 0.13 * column)
        + 0.597428 * (callHead - 7)
        >= Math.Min(69.995833 + 0.565909 * receiver + 0.444697 * column, 78.94 + 0.2 * receiver + 0.441 * column)
        + 1.181061 * (callHead - 7);

    /// <summary>
    ///     Whether a held lambda call whose line overflows by its <c>)</c> alone, a rest after it, moves below its receiver
    ///     rather than staying and giving the <c>)</c> a line of its own (#619 round 6).
    /// </summary>
    /// <remarks>
    ///     ⚠ SK-DIV-0420's lone <c>)</c> switched at a head and receiver of 16 exactly at indents 8 and 12. Over 8 500
    ///     random statements built one column over outside group I and every such statement of the other #619 probes and
    ///     grids (4 685, heads and receivers of 12 to 50, indents 8 to 20, fourteen callees, rests of 8 to 42) the switch is
    ///     mixed from 12 to 19, and no reach separates it. The exact largest-margin line does, by 0.012 of a column: the call
    ///     moves while its line below — one column over, 125 less the reach — is within 110.731, less 0.1209 a column of the
    ///     receiver's column, plus 0.6154 a column of call head past seven and 0.6703 of rest past ten. ⚠ The margin is thin:
    ///     round 6 solved it on 1 933 and then 3 231 statements, each fresh set of about 2 500 finding one statement within
    ///     0.02 of the line on the wrong side.
    ///     ⚠ Round 7 (2026-11-14): two more fresh sets of 3 000 (seeds 61904 and 61905) found four statements 0.012 on the
    ///     wrong side, all kept where round 6's line moved them; solved again over the 9 214 statements of every set, two
    ///     more (seeds 61911 and 61912) found one, 0.003 over. Over all 12 704 the receiver's own term (round 6's 0.0058)
    ///     goes to zero, the head's falls from 0.6243 to 0.6154, and the margin is 0.0055. The column, the head, the rest
    ///     and the line decide alone — no two of 11 186 distinct statements that share them answer differently — but no
    ///     small-denominator form exists:
    ///     the first integer form that separates them has a denominator of 82, and floor terms on the column, the rest or
    ///     the head (an eighth of the column, two thirds of the rest) do worse than the plain line.
    /// </remarks>
    static bool HeldLambdaOneOverMoves(int column, int callHead, int rest, int line) =>
        line <= 110.730769 - 0.120879 * column + 0.615385 * (callHead - 7) + 0.670330 * (rest - 10);

    /// <summary><see cref="HeldLambdaLimit" />'s cap and the line under it, under <c>.Where(</c>'s head.</summary>
    static double HeldLambdaCap(int column, int receiver) =>
        Math.Min(114.92 - 0.14 * column - 0.0125 * receiver, 123.35 - 0.38 * receiver - 0.13 * column);

    /// <summary><see cref="HeldLambdaLimit" />'s floor, where the receiver ends far right, under <c>.Where(</c>'s head.</summary>
    static double HeldLambdaFloor(int column, int receiver) =>
        Math.Min(72.04 + 0.5525 * receiver + 0.42 * column, 78.94 + 0.2 * receiver + 0.441 * column);

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
    ///     Whether the <c>=</c> breaks before <c>(T)(a op b…).Name</c> behind a head of twelve columns or more, while the
    ///     receiver through its <c>)</c> fits beside the <c>=</c> and the value fits below; null where the measurement
    ///     does not decide and master's ordering rule answers (#598, SK-DIV-0575 round 8).
    /// </summary>
    /// <remarks>
    ///     ⚠ Measured 2026-11-13 with <c>Testing ask</c> on 407 such rows of four probes. A property is not a call with
    ///     no arguments: <c>10·s − 7·cast ≤ 1033</c>, the call's rule, broke 28 <c>=</c> the oracle kept. The property's
    ///     own form is <c>10·s − 8·cast − member</c> (<c>s</c> the statement less its head, <c>member</c> the dot through
    ///     the name): every row at 962 or less broke the <c>=</c> and every row at 990 or more kept it. Between them the
    ///     oracle goes both ways, so the rule leaves those rows where they were. 962 is one row tighter than the fit's
    ///     972, after a fresh probe kept an <c>=</c> at 963.
    /// </remarks>
    bool? CastPropertyBesideAWideHeadBreaks(in GroupFacts facts, in Measures m) {
        var continuation = m.ContinuationColumn;
        if (HeldValueColumn(facts, m) + facts.HeldValueWidth <= width) {
            return false;
        }

        if (continuation + facts.HeldValueWidth > width) {
            return null;
        }

        var s = continuation + facts.HeldValueWidth - indentWidth;
        var v = 10 * s - 8 * facts.HeldValueCastWidth - facts.HeldValueMember;
        return v <= 962 ? true : v >= 990 ? false : null;
    }

    /// <summary>Whether a held value's receiver, through its <c>)</c>, fits beside the <c>=</c>.</summary>
    bool HeldReceiverFitsBeside(in GroupFacts facts, in Measures m) =>
        HeldValueColumn(facts, m) + facts.HeldValueReceiver <= width;

    /// <summary>The column a held value starts on beside its <c>=</c>. See <see cref="HeldValueBreaks" />.</summary>
    int HeldValueColumn(in GroupFacts facts, in Measures m) =>
        facts.HeldValueTargetTail > 0 && (facts.HeldValueTargetKept || m.Column + facts.HeldValueTarget > width)
            ? m.ContinuationColumn + facts.HeldValueTargetTail + 1
            : m.Column + Math.Max(m.PointWidth, facts.HeldValueTarget) + 1;

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
        // ⚠ An assignment's target through its `=`, not the point width, which a point inside the target ends early
        // (#614). See GroupFacts.HeldValueTarget.
        // ⚠ And a target that ends on a line of its own — kept, or a chain too wide to stay whole — from the
        // continuation column that line starts on (#624). See GroupFacts.HeldValueTargetTail.
        var valueColumn = facts.HeldValueTargetTail > 0
            && (facts.HeldValueTargetKept || m.Column + facts.HeldValueTarget > width)
                ? m.ContinuationColumn + facts.HeldValueTargetTail + 1
                : m.Column + Math.Max(m.PointWidth, facts.HeldValueTarget) + 1;
        if (valueColumn + facts.HeldValueWidth <= width) {
            return false;
        }

        var continuation = m.ContinuationColumn;
        var below = continuation + facts.HeldValueWidth;
        if (facts.HeldValueCast && facts.HeldValue != 2) {
            return CastParenBehindAWideHeadBreaks(facts, valueColumn, continuation);
        }

        var parenBelow = continuation + facts.HeldValueReceiver + facts.HeldValueHead;
        var parenBeside = valueColumn + facts.HeldValueReceiver + facts.HeldValueHead;
        switch (facts.HeldValue) {
            case 1 when facts.HeldValueTypedHead > 0:
                // ⚠ By the measured limit on the line below, which the callee's width moves (SK-DIV-0005, round 3);
                // or, as before, when the head with the receiver beside it is wider than 87 columns.
                // ⚠ One argument on a method without type arguments, behind a head of twelve or more: by the argument
                // against the receiver call's limit, the type and the name apart, and not by the receiver's end, which
                // the limit already reads through the callee (#446, SK-DIV-0211 round 9). See ReceiverListLimit.
                // ⚠ Behind a head under twelve, one argument whose `(` fits beside it breaks as a `var`'s does: by the
                // head's width, not its type and name (#446, SK-DIV-0211 round 9). See NarrowHeadBreaks.
                // ⚠ And with its `(` two or more columns past the margin beside it, the receiver fitting there, the dot
                // takes the line whatever the argument (#446, SK-DIV-0211 round 9). See NarrowHeadTakesTheDot.
                // ⚠ Only up to a cap in the argument that falls with the head (#446, SK-DIV-0211 round 11); past it the
                // `=` breaks. See NarrowHeadDotCap.
                if (facts is { HeldValueManyArgs: false, HeldValueGeneric: false, HeldValueHead: > 0 }
                    && facts.HeldValueTypedHead < 12
                    && NarrowHeadTakesTheDot(parenBeside, valueColumn + facts.HeldValueReceiver)) {
                    return !NarrowHeadDotCap(facts, valueColumn - (continuation - indentWidth) - 3, continuation);
                }

                // One column over beside it the lone argument's dot breaks instead, as behind a `var` (case 2).
                if (facts is { HeldValueManyArgs: false, HeldValueGeneric: false, HeldValueHead: > 0 }
                    && facts.HeldValueTypedHead < 12
                    && parenBeside <= width) {
                    return valueColumn + facts.HeldValueWidth != width + 1
                        && NarrowHeadBreaks(
                            HeldArgument(facts),
                            continuation - indentWidth,
                            facts.HeldValueReceiver + facts.HeldValueHead - 1,
                            valueColumn - (continuation - indentWidth) - 1,
                            facts.EqualsName
                        );
                }

                if (HeldOnReceiverLimitApplies(facts)) {
                    // ⚠ The `(` past the margin beside it breaks the `=` unless the dot takes the line, as behind a
                    // `var` (#446, SK-DIV-0211 round 9). See HeldDotTakesTheLine.
                    if (parenBeside > width) {
                        return valueColumn + facts.HeldValueReceiver > width
                            || parenBeside == width + 1
                            || !HeldDotTakesTheLine(
                                HeldArgument(facts),
                                below - HeldArgument(facts),
                                facts.HeldValueName,
                                facts.HeldValueReceiver
                            );
                    }

                    // ⚠ One argument has a gate too, a narrower one (#446, SK-DIV-0211 round 11). See
                    // OneArgumentGateBreaks.
                    var margin = ReceiverListMargin(
                        HeldArgument(facts),
                        facts.HeldValueReceiver + facts.HeldValueHead - 1,
                        continuation - indentWidth,
                        Math.Max(0, facts.HeldValueTypedHead - facts.HeldValueName - 2),
                        facts.HeldValueName,
                        true
                    );
                    if (!InsideTheListBand(margin)) {
                        return margin >= 0
                            && OneArgumentGateBreaks(
                                parenBeside,
                                facts.HeldValueReceiver + facts.HeldValueHead - 1,
                                facts.HeldValueName
                            );
                    }
                }

                return EqualsFloor.HeldTypedLocalBreaks(
                        facts.HeldValueTypedHead,
                        below,
                        facts.HeldValueCallee,
                        continuation - indentWidth,
                        facts.HeldValueName
                    )
                    || valueColumn + facts.HeldValueReceiver - (continuation - indentWidth) > HeldReceiverEnd
                    // ⚠ And when the call's `(` would land past the margin beside it (#626). The oracle never keeps
                    // `T name = r.M(` past the margin — 0 of 16 110 lines in #446's grids — and breaks the `=`, in
                    // 4 207 of 4 243 such cells of #626's probes (the rest broke between the type and the name). Pass
                    // two, reading pass one's chop as the author's, broke the `=` that pass one kept.
                    // ⚠ Not before a generic method, whose dot can take the break instead (`T c = JsonConvert` /
                    // `.DeserializeObject<…>(json);`, #528) — unless the receiver itself runs past the margin beside
                    // the `=`, where no dot can (#630). There pass one broke inside the type arguments,
                    // `= r.M<` / `G…>(`, which the oracle never writes, and pass two broke the `=`, the oracle's
                    // answer in every such cell measured (122 of 122, the rest broke between the type and the name).
                    || parenBeside > width
                    && (!facts.HeldValueGeneric || valueColumn + facts.HeldValueReceiver > width);
            case 1:
                // ⚠ Or when the head with the receiver beside it is wider than 87 columns: `T… c = JsonConvert`
                // holds to 87 and breaks from 88, counted from the statement (h12 at indent 8, on one call
                // width; Newtonsoft's 77, 82 and 95 at indents 12 and 20 agree, where an absolute column
                // would not).
                return below <= width - 3
                    || valueColumn + facts.HeldValueReceiver - (continuation - indentWidth) > HeldReceiverEnd;
            case 2:
                // ⚠ One column over beside it, a lone argument's dot breaks instead, as it does below.
                if (valueColumn <= continuation + 2
                    || !facts.HeldValueManyArgs
                    && valueColumn + facts.HeldValueWidth == width + 1) {
                    return false;
                }

                // ⚠ A receiver that runs past the margin beside the `=` breaks it, as the other two heads'
                // rules do. Unmeasured — the sweeps' receivers ended by column 118 — and held, it left `= r…(`
                // past the margin for the second pass to break once the arguments had chopped (Nightly fuzzer,
                // seed 1, replay 7536332154113230584: idempotency).
                if (valueColumn + facts.HeldValueReceiver > width) {
                    // ⚠ A cast over the call keeps the `=` unless the receiver ends one column past the margin
                    // (#598). See GroupFacts.HeldValueCast.
                    return !facts.HeldValueCast || valueColumn + facts.HeldValueReceiver == width + 1;
                }

                // ⚠ A parenthesised receiver that fits beside it: broken on a line two or three columns over
                // (#598). See GroupFacts.HeldValueParen.
                if (facts.HeldValueParen) {
                    var end = valueColumn + facts.HeldValueWidth;
                    return end == width + 2 || end == width + 3;
                }

                // ⚠ A call with arguments on a parenthesised operator, cast or not (#598, SK-DIV-0575 round 8). Measured
                // 2026-11-13 with `Testing ask`: once its `(` lands two columns or more past the margin beside the `=`,
                // the `=` stays and the dot breaks, in all 1 846 such rows of eight probes; on column 121 the old table
                // answers. Where the `(` fits beside, two arguments always chop beside the `=`, and one breaks the `=`
                // by the two measured forms below: every cast row at 640 or more (uncast 633) with the value fitting
                // below moved down, and every cast row at 1 028 or more with it not fitting below did too. Short of
                // those the `=` stays and the argument chops, master's answer for a cast.
                if (facts.HeldValueParenCall && parenBeside >= width + 2) {
                    return false;
                }

                // ⚠ One argument whose `(` fits beside a head of seven columns or more: by the head, the indent, the
                // argument's width and where the `(` lands, nothing else (#598, round 12). See
                // ShortHeadKeepsBeforeOneArgument.
                if (facts.HeldValueParenCall
                    && !facts.HeldValueManyArgs
                    && parenBeside <= width
                    && valueColumn - (continuation - indentWidth) >= 8) {
                    return !ShortHeadKeepsBeforeOneArgument(
                        valueColumn - (continuation - indentWidth) - 1,
                        continuation - indentWidth,
                        facts.HeldValueMember - facts.HeldValueHead - 1,
                        facts.HeldValueNameWidth,
                        parenBeside
                    );
                }

                // ⚠ And two or three, behind ten columns or more: by the same four widths (#598, round 12). See
                // ShortHeadKeepsBeforeArguments.
                if (facts.HeldValueParenCall
                    && facts.HeldValueManyArgs
                    && parenBeside <= width
                    && valueColumn + facts.HeldValueWidth > width + 1
                    && valueColumn - (continuation - indentWidth) >= 11) {
                    return !ShortHeadKeepsBeforeArguments(
                        valueColumn - (continuation - indentWidth) - 1,
                        continuation - indentWidth,
                        facts.HeldValueMember - facts.HeldValueHead - 1,
                        facts.HeldValueNameWidth,
                        parenBeside,
                        facts.HeldValueArgumentCount
                    );
                }

                if (facts.HeldValueParenCall
                    && parenBeside <= width
                    && (facts.HeldValueCastWidth > 0 || below <= width)) {
                    var statement = continuation - indentWidth;
                    var head = valueColumn - statement;
                    var end = valueColumn + facts.HeldValueWidth;
                    if (facts.HeldValueManyArgs) {
                        return false;
                    }

                    return below <= width
                        ? 10 * parenBeside - 8 * end - 7 * statement + 66 * head - facts.HeldValueHead
                        >= (facts.HeldValueCastWidth > 0 ? 640 : 633)
                        : 10 * parenBeside
                        - 6 * end
                        - 8 * statement
                        + 4 * facts.HeldValueHead
                        + 75 * head
                        + Math.Min(facts.HeldValueFirstOperand, 24)
                        >= 1028;
                }

                // ⚠ One argument whose `(` fits beside it: by the measured line (#446, SK-DIV-0211 round 8). See
                // NarrowHeadBreaks.
                if (!facts.HeldValueManyArgs && parenBeside <= width) {
                    return NarrowHeadBreaks(
                        HeldArgument(facts),
                        continuation - indentWidth,
                        facts.HeldValueReceiver + facts.HeldValueHead - 1,
                        valueColumn - (continuation - indentWidth) - 1,
                        facts.EqualsName
                    );
                }

                // ⚠ One argument whose `(` lands two or more columns past the margin beside it keeps the `=` and breaks
                // at its dot, whatever the argument (#446, SK-DIV-0211 round 9). See NarrowHeadTakesTheDot.
                if (!facts.HeldValueManyArgs
                    && facts.HeldValueHead > 0
                    && !facts.HeldValueCast
                    && !facts.HeldValueGeneric
                    && NarrowHeadTakesTheDot(parenBeside, valueColumn + facts.HeldValueReceiver)) {
                    return !NarrowHeadDotCap(facts, valueColumn - (continuation - indentWidth) - 3, continuation);
                }

                return facts.HeldValueManyArgs
                    ? parenBeside > width && parenBelow <= width - 3
                    : below <= width + 1 || parenBelow <= width - 3;
            default:
                // ⚠ One argument wider than the measured limit keeps the `=` and chops beside it, whether the value would
                // fit below or not (#446, SK-DIV-0211 rounds 8 and 9). See ReceiverListLimit.
                if (!facts.HeldValueManyArgs
                    && facts.HeldValueHead > 0
                    && valueColumn + facts.HeldValueReceiver + facts.HeldValueHead <= width) {
                    var head = valueColumn - (continuation - indentWidth) - 1;
                    var name = facts.EqualsName > 0 ? facts.EqualsName : head - 2;
                    var margin = ReceiverListMargin(
                        HeldArgument(facts),
                        facts.HeldValueReceiver + facts.HeldValueHead - 1,
                        continuation - indentWidth,
                        Math.Max(0, head - 2 - name),
                        name,
                        true
                    );
                    if (margin < 0 && !InsideTheListBand(margin)) {
                        return false;
                    }
                }

                // ⚠ One argument whose `(` lands past the margin beside the `=`, the receiver fitting there: the dot takes
                // the line only for a short argument (#446, SK-DIV-0211 round 9). See HeldDotTakesTheLine.
                if (!facts.HeldValueManyArgs
                    && facts.HeldValueHead > 0
                    && !facts.HeldValueGeneric
                    && parenBeside > width
                    && valueColumn + facts.HeldValueReceiver <= width) {
                    var head = valueColumn - (continuation - indentWidth) - 1;
                    var name = facts.EqualsName > 0 ? facts.EqualsName : head - 2;
                    return parenBeside == width + 1
                        || !HeldDotTakesTheLine(
                            HeldArgument(facts),
                            below - HeldArgument(facts),
                            name,
                            facts.HeldValueReceiver
                        );
                }

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
    ///     The held call's argument list inside its parentheses: the value less its receiver, its dot through <c>(</c>,
    ///     its <c>)</c> and its <c>;</c>.
    /// </summary>
    static int HeldArgument(in GroupFacts facts) =>
        facts.HeldValueWidth - facts.HeldValueReceiver - facts.HeldValueHead - 2;

    /// <summary>
    ///     Whether a typed local's held call is read by <see cref="ReceiverListLimit" />: one argument, a method without
    ///     type arguments (a known name), and a head of twelve or more through the <c>=</c> (#446, SK-DIV-0211 round 9).
    /// </summary>
    /// <remarks>
    ///     ⚠ Round 4's rule (<see cref="EqualsFloor.HeldTypedLocalBreaks" />, three lines in the line below) stays for a
    ///     generic method and for two or more arguments, which round 9 did not measure behind a typed head. Measured
    ///     2026-11-13 with <c>Testing ask</c> on one argument a column at a time behind typed, <c>var</c> and assignment
    ///     heads of 5 to 41 columns, callees of 16 to 96 and indents 8 to 24 (16 443 cells): the limit is the two-argument
    ///     list's, 21 columns higher. Round 8's one-argument line for <c>var</c> and assignments and round 4's typed rule
    ///     were each one region of it.
    /// </remarks>
    /// <summary>
    ///     Whether a held one-argument call whose <c>(</c> lands past the margin beside a kept <c>=</c> breaks at its dot
    ///     (<c>name = receiver</c> / <c>.M(a);</c>) rather than below the <c>=</c>, by the argument, the column the value
    ///     would end at below without it (<paramref name="bare" />, its <c>;</c> included), the name the head assigns and
    ///     the receiver (#446, SK-DIV-0211 rounds 9 and 10). Otherwise the <c>=</c> breaks, and the value moves below
    ///     whole, chopped or broken at its dot there. #528's held-call table moved the dot for any argument up to a line of
    ///     83 or so.
    /// </summary>
    /// <remarks>
    ///     ⚠ Round 10 (#446, SK-DIV-0211): one integer inequality, not two fitted lines.
    ///     <c>38·argument − receiver + 57·max(0, width − 5 − bare) ≤ max(428 − 7·name, 14·name − 34)</c>. Round 9's
    ///     rising edge (a column and a half of argument per column of bare line) and its cap (a line falling with the name)
    ///     were the two halves of it, each fitted to within a column, and g22's 183 newly wrong cells sat on them.
    ///     <list type="bullet">
    ///         <item>
    ///             The cap is three widths and nothing else: the argument, the receiver and the name. An exact integer
    ///             search over the indent, the type and the method gives each a coefficient of 0.
    ///         </item>
    ///         <item>
    ///             Short of a bare line ending at <c>width − 5</c> the bound falls 57 per column, which is 38 · 3/2: the
    ///             rising edge round 9 read as a slope of 1.5.
    ///         </item>
    ///         <item>
    ///             ⚠ The name's term is a V. It falls 7 a column to a name of 22 and rises 14 a column past it: a name of
    ///             28 caps like one of 10, one of 38 at 14. Round 9's "falls with the name" had no name past 30.
    ///         </item>
    ///         <item>
    ///             ⚠ The "special" cells this round first set aside are the same rule. A <c>(</c> two or three columns
    ///             over, a first line ending at 115 to 120, a value one column over below (<c>=</c> / <c>r</c> /
    ///             <c>.M(a);</c>) and heads of ten and eleven columns all need nothing of their own.
    ///         </item>
    ///     </list>
    ///     54 180 of 54 180 cells of seven grids: g21, g22, g24 and g25 from round 9, and g26 to g28 written this round with
    ///     names of 1 to 46, receivers of 3 to 97, methods of 4 to 44 and indents of 8 to 24. A head under twelve
    ///     columns through the <c>=</c> is not this rule (<see cref="NarrowHeadTakesTheDot" />).
    /// </remarks>
    bool HeldDotTakesTheLine(int argument, int bare, int name, int receiver) =>
        38 * argument - receiver + 57 * Math.Max(0, width - 5 - bare) <= Math.Max(428 - 7 * name, 14 * name - 34);

    /// <summary>
    ///     Whether a held one-argument call behind a head under twelve columns keeps the <c>=</c> and breaks at its dot:
    ///     its <c>(</c> (<paramref name="paren" />, 1-based) two or more columns past the margin beside the <c>=</c>, and
    ///     its receiver ending inside it (<paramref name="receiverEnd" />) (#446, SK-DIV-0211 round 9).
    /// </summary>
    /// <remarks>
    ///     ⚠ Measured 2026-11-13 with <c>Testing ask</c> on 10 356 cells: <c>var</c>, assignment and typed heads of 4 to 11
    ///     columns, indents 8 and 16, the bare line below at 104 to 128 and the argument from 3 to 70. Every head through
    ///     the <c>=</c> under twelve takes the dot at every argument measured, where #528's rule broke the <c>=</c> and
    ///     chopped below; a head of twelve or thirteen takes it only to an argument of about a dozen, as the wide heads do
    ///     (<see cref="HeldDotTakesTheLine" />). One column over is #528's own rule (<see cref="OneOverMovesTheDot" />).
    ///     ⚠ Round 10 (2026-11-14, measured, not wired): "every argument" holds to about 70 only. Swept from 40 to 100 a
    ///     column at a time (g29), a head of six to nine columns before its <c>=</c> keeps the dot to an argument of about
    ///     91, 82, 75 and 69, and past it the <c>=</c> breaks and the argument chops below. ⚠ Round 11 wired it, on this
    ///     path and the typed head's: <see cref="NarrowHeadDotCap" />. Round 10's table "changed nothing" because it only
    ///     withheld the early return, and the fall-through kept the <c>=</c> anyway; the cap has to break it.
    /// </remarks>
    bool NarrowHeadTakesTheDot(int paren, int receiverEnd) => paren >= width + 2 && receiverEnd <= width;

    /// <summary>
    ///     Whether a narrow head's held one-argument call keeps its dot (<see cref="NarrowHeadTakesTheDot" />) at its
    ///     argument's width: <c>38·argument − receiver + 7·name + 57·max(0, width − 5 − bare)</c>, the wide heads' left-hand
    ///     side, at most a bound of the head before its <c>=</c> (<paramref name="head" />): 3 427, 3 095, 2 829 and 2 610
    ///     for heads of six to nine columns, none up to five (#446, SK-DIV-0211 round 11). Past it the <c>=</c> breaks and
    ///     the call chops or breaks at its dot below.
    /// </summary>
    /// <remarks>
    ///     Measured with <c>Testing ask</c> on round 10's g29 (arguments of 40 to 100 a column at a time, heads of 4 to 9,
    ///     receivers of 76 to 104, methods of 4 and 16, indents 8 and 16): the left-hand side is the wide heads' dot rule
    ///     (<see cref="HeldDotTakesTheLine" />) with its bare line far right, and the bound is exact on every row. A head
    ///     of six or less kept the dot at every argument whose dot line fitted (to 100).
    /// </remarks>
    bool NarrowHeadDotCap(in GroupFacts facts, int head, int continuation) {
        var bound = head switch {
            <= 5 => int.MaxValue,
            6 => 3427,
            7 => 3095,
            8 => 2829,
            _ => 2610,
        };
        var name = facts.EqualsName > 0 ? facts.EqualsName : head;
        var bare = continuation + facts.HeldValueWidth - HeldArgument(facts);
        return 38L * HeldArgument(facts) - facts.HeldValueReceiver + 7 * name + 57 * Math.Max(0, width - 5 - bare)
            <= bound;
    }

    static bool HeldOnReceiverLimitApplies(in GroupFacts facts) =>
        facts is { HeldValueManyArgs: false, HeldValueGeneric: false, HeldValueName: > 0, HeldValueHead: > 0 }
        && facts.HeldValueTypedHead >= 12;

    /// <summary>
    ///     Whether the <c>=</c> breaks before a held one-argument call behind a head under twelve columns through the
    ///     <c>=</c>, its <c>(</c> fitting beside it (#446, SK-DIV-0211 rounds 8 and 9): while the argument is at most the
    ///     smaller of two lines in the callee <c>r.M</c>, the statement's indent, the head and its part before the name.
    ///     <paramref name="head" /> is the statement through the <c>=</c>; <paramref name="name" /> the name it assigns.
    /// </summary>
    /// <remarks>
    ///     ⚠ Measured 2026-11-13 with <c>Testing ask</c> on 5 115 cells: heads of 4 to 11, callees of 56 to 96, indents 8
    ///     to 24, the argument a column at a time from 3 to 49. #528's <c>below ≤ width + 1</c> broke the <c>=</c> on 4 487
    ///     cells the oracle chops beside it (master agreed on 460). Heads of seven or less broke it on no measured cell,
    ///     which is #379's floor again. A head of eight to eleven breaks it only for a narrow argument, the same shape as
    ///     the binary pattern's rule (<see cref="EqualsFloor.BreaksBeforeAPattern" />).
    ///     ⚠ Round 9 (13 503 more cells: <c>var</c>, assignment and typed heads of 6 to 11 columns, callees of 70 to 106 a
    ///     few columns apart): a typed local's narrow head is the same rule — <c>T… nn =</c> and <c>var nn =</c> of equal
    ///     width break at nearly the same argument — and round 8's single line,
    ///     <c>argument + 0.545·indent − 0.198·callee − 5.22·head &lt; −42.62</c>, missed the narrowest heads by up to
    ///     nine columns: the head's slope steepens there. The callee's and the indent's slopes are the wide limit's
    ///     rising lines' (<see cref="ReceiverListLimit" />). 154 of 155 boundaries.
    ///     ⚠ Round 12 (#446, SK-DIV-0211): one integer form, the head a table rather than two lines,
    ///     <c>258·argument ≤ c(head) + 47·callee − 143·indent − 32·type</c> with <c>c</c> −2 050, 35, 1 694, 3 044 and
    ///     4 181 for five to nine columns before the <c>=</c>, none up to four. Round 9's two lines were fitted without a
    ///     head of five (g19 and g23 measured four, six and up), and extrapolated there they broke
    ///     <c>Tdo n = r(78).M(avcvip);</c> where the oracle chops; they also missed boundary rows of their own grids by a
    ///     few hundredths of a column. g41 (6 890 cells: every type/name split of heads of four to eight, callees of 62 to
    ///     110, methods of 6 and 18), g43 (23 242 cells: every callee from 61 to 111, which the older grids stepped by
    ///     four, indents 8, 12 and 20), g42 (below) and round 9's g6, g19 and g23 give 2 282 boundary rows of heads of five
    ///     to nine;
    ///     the form is exact on all of them, and an LP finds no two lines in the head that are (best margin −0.375 of a
    ///     column). ⚠ Cuts at scales 99 and 186, each fitted before the next grid, were a hundredth of a column off on seven
    ///     odd-callee rows of g43 and two rows of g42; the LP's margin is 0.003 of a column, so the form is tight.
    ///     ⚠ An argument that is a call answers as a plain one of the same width: g42, 15 744 cells side by side, 171 of
    ///     171 boundary rows identical.
    /// </remarks>
    static bool NarrowHeadBreaks(int argument, int indent, int callee, int head, int name) {
        var before = head - 2;
        var type = Math.Max(0, before - name);
        int? bound = before switch {
            <= 4 => null,
            5 => -2050,
            6 => 35,
            7 => 1694,
            8 => 3044,
            _ => 4181,
        };
        return bound is { } c && 258 * argument <= c + 47 * callee - 143 * indent - 32 * type;
    }

    /// <summary>
    ///     Whether the <c>=</c> breaks before <c>(T)(a op b…).M()</c> behind a head of twelve columns or more, a typed
    ///     local's or a <c>var</c>'s or an assignment's, on a line it does not fit (#598, SK-DIV-0575).
    /// </summary>
    /// <remarks>
    ///     ⚠ Measured 2026-10-14 with <c>Testing ask</c> on 16 000 rows (heads of 13 to 40 columns through the
    ///     <c>=</c>, casts of 3 to 25, first operands of 2 to 34, seven operators, six calls, indents 8 to 20, lines of
    ///     118 to 160), and checked on fresh random probes built after it was chosen. Here <c>s</c> is the statement's
    ///     width without its head, its indent included: the value's line below less one level.
    ///     <list type="bullet">
    ///         <item>
    ///             While the receiver through its <c>)</c> fits beside the <c>=</c>: one column over, the <c>=</c>
    ///             breaks when the head and the cast are wider than eighteen; two or three over it always does; past
    ///             that while <c>10·s − 7·cast ≤ 1033</c> and the value fits below. Otherwise the dot takes the break.
    ///         </item>
    ///         <item>
    ///             Past that, the <c>=</c> stays and the parentheses break inside only while
    ///             <c>10·s + 4·min(first operand, 24) − 7·cast ≥ 1185</c> and the operator's line fits. The first
    ///             operand stops counting at 24 columns: the oracle's boundary in the line stops moving there, for
    ///             every head and cast measured.
    ///         </item>
    ///     </list>
    ///     ⚠ The second boundary is a safe bound, not the oracle's: every row the oracle kept the <c>=</c> on lies
    ///     above it, and about one row in twelve that it broke does too, which Skala still keeps. No form in these
    ///     widths separates them exactly (SK-DIV-0575).
    /// </remarks>
    bool CastParenBehindAWideHeadBreaks(in GroupFacts facts, int valueColumn, int continuation) {
        var statement = continuation - indentWidth;
        var head = valueColumn - statement;
        var cast = facts.HeldValueCastWidth;
        var end = valueColumn + facts.HeldValueWidth;
        var s = continuation + facts.HeldValueWidth - indentWidth;
        if (valueColumn + facts.HeldValueReceiver <= width) {
            // ⚠ A call with arguments breaks the `=` from one column over, and past three by its own form, where the
            // call's width counts against the break (#598, SK-DIV-0575 round 8): 832 of 851 rows that broke lie at
            // 1 025 or less, and none that kept the `=`.
            if (facts.HeldValueParenCall) {
                return end <= width + 3
                    || 10 * s - 4 * cast - 14 * facts.HeldValueMember + 12 * facts.HeldValueHead <= 1025
                    && continuation + facts.HeldValueWidth <= width;
            }

            if (end == width + 1) {
                return head + cast > 18;
            }

            return end <= width + 3 || 10 * s - 7 * cast <= 1033 && continuation + facts.HeldValueWidth <= width;
        }

        // ⚠ Round 8 (#598): the member after the parenthesis and the head count too. Every row of 1 477 at 1 094 or
        // less broke the `=` (the fit's 1 095 kept one on a fresh probe), and every row at 1 170 or more kept it;
        // between them round 6's safe bound still answers.
        // ⚠ Past the receiver with the value fitting below (#598, round 8): over 3 325 rows, cast or not, every row at
        // 1 065 or less broke the `=` and every row at 1 121 or more kept it.
        if (continuation + facts.HeldValueWidth <= width) {
            var w = 10 * s
                + 3 * facts.HeldValueFirstOperand
                - 8 * cast
                - 19 * facts.HeldValueMember
                - head
                - (cast > 0 ? 7 : 0);
            if (w <= 1065) {
                return true;
            }

            if (w >= 1121) {
                return continuation + facts.HeldValueOperatorTail > width;
            }
        }

        var v = 10 * s
            + 8 * Math.Min(facts.HeldValueFirstOperand, 24)
            - 7 * cast
            - 20 * facts.HeldValueMember
            - 4 * head;
        if (v <= 1094) {
            return true;
        }

        var keeps = (v >= 1170 || 10 * s + 4 * Math.Min(facts.HeldValueFirstOperand, 24) - 7 * cast >= 1185)
            && continuation + facts.HeldValueOperatorTail <= width;
        return !keeps;
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
