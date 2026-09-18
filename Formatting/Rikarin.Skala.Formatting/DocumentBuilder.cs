namespace Rikarin.Skala.Formatting;

/// <summary>
///     Builds a <see cref="Document" /> into pooled buffers.
/// </summary>
/// <remarks>
///     The builder is a stack machine rather than a tree of constructors: an <c>Open*</c> call pushes a
///     container, the leaf methods append to whatever is open, and <see cref="Close" /> pops. That keeps
///     the whole document in three growable arrays and never allocates a node
///     (docs/plan/13 § "The fitting pass").
///     <para>
///         ⚠ The measure pass is fused into it. Every node's flat width is accumulated as the node is
///         appended, and a container's width is the sum its children already deposited into the open frame,
///         so the document arrives measured and the fitter never traverses it to find out
///         (docs/plan/13 § "The fitting pass": "the measure pass is fused into the build pass where a
///         group's contents are already known, which removes one full traversal").
///     </para>
/// </remarks>
public sealed class DocumentBuilder {
    readonly List<string> strings = [];

    /// <summary>Children of frames that are still open, innermost last.</summary>
    readonly List<int> pending = [];

    /// <summary>Children of frames that have closed. Node slices point in here.</summary>
    readonly List<int> children = [];

    readonly List<Frame> stack = [];

    /// <summary>Group metadata, indexed by group id and filled as ids are handed out.</summary>
    readonly List<GroupFacts> facts = [];

    DocNode[] nodes = new DocNode[512];
    int[] flatWidth = new int[512];
    int[] headWidth = new int[512];

    /// <summary>Width to the first break point, optional ones included. <see cref="Document.PointWidthOf" />.</summary>
    int[] pointWidth = new int[512];

    /// <summary>Width from a group's own first break point to the next. <see cref="Document.AfterPointOf" />.</summary>
    int[] afterPoint = new int[512];

    /// <summary>
    ///     The groups that own at least one break point.
    /// </summary>
    /// <remarks>
    ///     ⚠ It gates <see cref="MeasureSegments" />'s descent, so that the root group — which owns no
    ///     points and contains the file — is never walked and the measure stays linear in practice.
    /// </remarks>
    readonly HashSet<int> ownPoints = [];

    /// <summary>Flat width from one fill point to the next. <see cref="Document.SegmentOf" />.</summary>
    int[] segment = new int[512];

    /// <summary>
    ///     The width from a fill point to the first place inside the next item where a break could be
    ///     taken — a hard line, or a point of a nested group that can break — or the whole segment when
    ///     there is none. What the fill keeps on the line when the item cannot fit whole anywhere.
    /// </summary>
    /// <remarks>
    ///     ⚠ SK-DIV-0110. <see cref="segment" /> asks "does the whole next item fit", and the oracle
    ///     asks it too — a 104-column object initializer is broken before, whole — but not when the
    ///     answer would be no on a fresh line as well: <c>(1\n, (2\n, 3))</c> keeps <c>, (2</c>
    ///     together although the item has no flat form, and <c>Resolve(\n…\n), [</c> keeps a
    ///     110-column collection's <c>[</c> on the <c>)</c> line and chops it inside. So a fill breaks
    ///     before an item exactly when that makes the item fit; otherwise the item's head stays — for
    ///     an item that opens with a delimiter (<see cref="LineFlags.DelimitedItem" />): the oracle
    ///     still breaks before a 133-column binary chain that fits nowhere. That is what makes the rule
    ///     idempotent: on pass one an item too wide for any line keeps its head and breaks inside, and
    ///     on pass two the same item, now certain, is measured the same way.
    /// </remarks>
    int[] segmentHead = new int[512];

    /// <summary>
    ///     Each group's mode, by id, for <see cref="segmentHead" /> to know which nested points can break.
    /// </summary>
    readonly Dictionary<int, GroupMode> modes = [];

    /// <summary>Whether the subtree holds a break point of any kind. Stops the two measures above.</summary>
    bool[] breaks = new bool[512];

    /// <summary>
    ///     Whether the node is certain to hold a line break once laid out: a hard line, a group that
    ///     always breaks, a preserve group whose source was broken at its own points and which may not
    ///     re-join — or anything containing one of those.
    /// </summary>
    /// <remarks>
    ///     ⚠ The containment fact SK-DIV-0007 and SK-DIV-0050 recorded as missing: "a construct that
    ///     spans lines makes its container span lines". A group whose child is certain has no flat form,
    ///     which is what makes <c>Use(a &gt; 0\n &amp;&amp; b &gt; 0)</c> chop its argument list around
    ///     the operator break the author wrote, <c>c ? 1\n + n : 2</c> chop its ternary, and
    ///     <c>a\n &amp;&amp; b || c</c> chop at the <c>||</c> as well — every one of them the oracle's
    ///     answer, measured (SK-DIV-0109). It is kept apart from <see cref="flatWidth" /> because a
    ///     nested group's certainty must reach its container without the nested group's own width
    ///     becoming unbounded for every measure: the head and point measures stop at an unbounded child,
    ///     and an <c>=</c> whose value holds a kept operator break still keeps the value on its line.
    /// </remarks>
    bool[] certain = new bool[512];

    /// <summary>
    ///     Where a node's certainty comes from: 0 for none, 1 when it comes only from
    ///     <see cref="GroupFacts.BreaksWithOwner" /> links whose source was broken, 2 when a hard line, a
    ///     group that always breaks or a broken group that is not a link is involved.
    /// </summary>
    /// <remarks>
    ///     ⚠ Read by the chain-wide owner alone. The owner answers "does the whole chain fit on one
    ///     line" and must not read its own links' breaks as its answer — but it must still read a
    ///     multi-line lambda or a chopped list inside an operand, which chops the whole chain today and
    ///     in the oracle. Measured three ways: <c>a &gt; 0 &amp;&amp; a &lt; 10\n || a == 20</c> comes
    ///     back unchanged; <c>a &gt; 0\n &amp;&amp; a &lt; 10 || a == 20</c> comes back chopped at both
    ///     operators, because the <c>||</c> link contains the broken <c>&amp;&amp;</c>; and
    ///     <c>row is null\n || line != n\n || run.Count &gt; 0 &amp;&amp; !Joins(…)</c> keeps its
    ///     <c>&amp;&amp;</c> — the links the author broke are certain, the link that contains them is
    ///     certain, and the owner that contains all three is not, or every link would chop. A width
    ///     the links' certainty had flowed into by summation was what made it chop (SK-DIV-0109).
    /// </remarks>
    byte[] certainOrigin = new byte[512];

    /// <summary>
    ///     The node's flat width with only strong certainty (<see cref="certainOrigin" /> 2) counted as
    ///     unbounded: what a chain-wide owner sums its children by.
    /// </summary>
    int[] ownerWidth = new int[512];

    /// <summary>
    ///     The groups that own <see cref="GroupFacts.BreaksWithOwner" /> links — a binary chain's
    ///     chain-wide group — which answer "does the whole chain fit on one line" and are the one kind of
    ///     container a certain child does not make unbounded.
    /// </summary>
    /// <remarks>
    ///     ⚠ Measured both ways, and the asymmetry is the oracle's: a chain broken at its outer
    ///     <c>||</c> comes back unchanged, while one broken at its inner <c>&amp;&amp;</c> comes back
    ///     chopped at both operators. The second is the <c>||</c> link containing the broken
    ///     <c>&amp;&amp;</c> — a child, so it chops. The first would only chop if the chain-wide owner,
    ///     which contains every link, read the <c>||</c>'s break as its own and broke the whole chain,
    ///     which is exactly what SK-DIV-0007 measured the obvious fix doing to two committed fixtures.
    ///     See <see cref="certainOrigin" /> for the width the owner sums instead.
    /// </remarks>
    readonly HashSet<int> chainOwners = [];

    int nodeCount;
    int groupCount;
    int root = -1;

    public DocumentBuilder() {
        // Index 0 is reserved so that a zero payload can mean "nothing here".
        strings.Add(string.Empty);
        OpenConcat();
    }

    /// <summary>The number of groups handed out so far.</summary>
    public int GroupCount => groupCount;

    /// <summary>Allocates a group id, so that <see cref="OpenIfBroken" /> can reference the group.</summary>
    public int NextGroupId() {
        facts.Add(new GroupFacts());
        return groupCount++;
    }

    /// <summary>
    ///     Records what the fitter needs to know about a group before it meets it, which only the front
    ///     end can answer.
    /// </summary>
    public void DescribeGroup(int groupId, GroupFacts facts) {
        this.facts[groupId] = facts;

        // ⚠ A link is described before its owner closes — inside it — so the set is complete by
        // the time Close() asks. See `chainOwners`.
        if (facts.ChainLink && facts.Owner >= 0) {
            chainOwners.Add(facts.Owner);
        }
    }

    /// <summary>
    ///     A token's text.
    /// </summary>
    /// <remarks>
    ///     ⚠ A token that spans lines — a raw string literal, a verbatim string, a block comment — has
    ///     no flat width, because there is no line it can be laid flat on. <c>chop_if_long</c> reads
    ///     "chop if long <em>or multiline</em>" in ReSharper's own summary, and the oracle chops an
    ///     argument list around a multi-line string exactly as it chops one that is too wide.
    /// </remarks>
    public void Text(string value, SourceSpan source, VerbatimFlags flags = VerbatimFlags.None) {
        var index = pending.Count;
        var multiline = ContainsNewLine(value);
        Leaf(
            DocKind.Text,
            0,
            TextWidth.Measure(value),
            source,
            AddString(value),
            multiline ? Document.Unbounded : TextWidth.Measure(value),
            multiline ? FirstLineWidth(value) : TextWidth.Measure(value)
        );
        nodes[pending[index]].Flags = (int)flags;
    }

    /// <summary>Raw text, copied byte-for-byte and never reindented.</summary>
    public void Verbatim(string value, SourceSpan source, VerbatimFlags flags = VerbatimFlags.None) {
        var index = pending.Count;
        var multiline = ContainsNewLine(value);
        Leaf(
            DocKind.Verbatim,
            0,
            TextWidth.Measure(value),
            source,
            AddString(value),
            multiline ? Document.Unbounded : TextWidth.Measure(value),
            multiline ? FirstLineWidth(value) : TextWidth.Measure(value)
        );
        nodes[pending[index]].Flags = (int)flags;
    }

    public void Space(SpaceKind kind) =>
        Leaf(
            DocKind.Space,
            (int)kind,
            0,
            default,
            0,
            kind == SpaceKind.Forbidden ? 0 : 1,
            kind == SpaceKind.Forbidden ? 0 : 1
        );

    /// <summary>
    ///     An inter-token gap preserved byte for byte rather than normalised to one space.
    /// </summary>
    /// <remarks>
    ///     ⚠ <c>disable_space_changes</c> is the only thing that produces one, and it is a
    ///     <see cref="DocKind.Space" /> rather than a <see cref="DocKind.Text" /> for one reason: a
    ///     gap is allowed to sit immediately before a break point, and a space is the only node the
    ///     writer discards when the break is taken. Written as text, a preserved run before a taken
    ///     break would be trailing whitespace — which nothing else in this formatter can emit, and
    ///     which the idempotence property would then fail on.
    /// </remarks>
    public void Space(string text) {
        var width = TextWidth.Measure(text);
        Leaf(DocKind.Space, (int)SpaceKind.Required, 0, default, AddString(text), width, width);
    }

    /// <summary>
    ///     A line break. <paramref name="newLine" /> carries the source's own ending so that a file with
    ///     CRLF stays CRLF — <c>enforce_line_ending_style = false</c> means mixed endings are preserved
    ///     rather than normalised.
    /// </summary>
    public void Line(LineKind kind, int blankLines = 0, string? newLine = null) =>
        Leaf(
            DocKind.Line,
            (int)kind,
            blankLines,
            default,
            newLine is null ? 0 : AddString(newLine),
            kind == LineKind.Soft ? 1 : Document.Unbounded,
            kind == LineKind.Soft ? 1 : 0
        );

    /// <summary>
    ///     A break point: a gap the layout may or may not break at, owned by <paramref name="group" />.
    /// </summary>
    /// <param name="flatSpace">
    ///     What the gap renders as when the group stays flat. ⚠ Not uniform across a construct's own
    ///     points: the gap after <c>(</c> is nothing and the gap after <c>,</c> is a space.
    /// </param>
    /// <param name="fill">
    ///     The point breaks only when what follows it does not fit, rather than with its group.
    ///     <see cref="LineFlags.FillPoint" />.
    /// </param>
    /// <param name="lastResort">
    ///     The point does not end the rest-of-line measure of anything before it.
    ///     <see cref="LineFlags.LastResort" />.
    /// </param>
    /// <param name="delimitedItem">
    ///     The item after the point opens with a delimiter. <see cref="LineFlags.DelimitedItem" />.
    /// </param>
    /// <param name="keepsHeadWhenCertain">
    ///     The item after the point keeps its head when a break inside it is certain.
    ///     <see cref="LineFlags.KeepsHeadWhenCertain" />.
    /// </param>
    public void BreakPoint(
        int group,
        bool flatSpace,
        bool fill = false,
        int blankLines = 0,
        string? newLine = null,
        bool lastResort = false,
        bool delimitedItem = false,
        bool keepsHeadWhenCertain = false
    ) {
        var index = pending.Count;
        Leaf(
            DocKind.Line,
            (int)LineKind.Soft,
            blankLines,
            default,
            newLine is null ? 0 : AddString(newLine),
            flatSpace ? 1 : 0,
            flatSpace ? 1 : 0
        );
        ref var node = ref nodes[pending[index]];
        node.Arg2 = group;
        node.Flags = (flatSpace ? (int)LineFlags.FlatSpace : 0)
            | (fill ? (int)LineFlags.FillPoint : 0)
            | (lastResort ? (int)LineFlags.LastResort : 0)
            | (delimitedItem ? (int)LineFlags.DelimitedItem : 0)
            | (keepsHeadWhenCertain ? (int)LineFlags.KeepsHeadWhenCertain : 0);

        ownPoints.Add(group);

        // ⚠ A last-resort point is measured as *not* taken: it counts as its flat rendering and stops
        // nothing, so a construct before it on the line sees what follows the point as still to come.
        if (lastResort) {
            pointWidth[pending[index]] = flatSpace ? 1 : 0;
            breaks[pending[index]] = false;
            return;
        }

        // ⚠ A break point stops the point measure, which is what distinguishes it from the head.
        // ⚠ And it contributes nothing to it. "The rest of this line if every break point is taken"
        // ends *before* the space the point would have rendered as, and LayoutWriter.TrailingWidth
        // already says so for a point that is a direct sibling — but a point nested inside a group
        // reached that code through the group's own point width, which was counting it. The two
        // disagreeing is worth a column, and a column is a wrap: an expression-bodied member whose
        // declaration is exactly 120 wide came back with its parameter list chopped, while the same
        // declaration with a block body did not.
        pointWidth[pending[index]] = 0;
        breaks[pending[index]] = true;
    }

    /// <summary>A sync point between output and input, emitted immediately before what it introduces.</summary>
    public void Anchor(SourceSpan source, int tokenId) => Leaf(DocKind.Anchor, tokenId, 0, source, 0, 0, 0);

    public void OpenGroup(GroupMode mode, int groupId) {
        modes[groupId] = mode;
        Open(DocKind.Group, (int)mode, groupId);
    }

    public void OpenFill() => Open(DocKind.Fill, 0, 0);

    /// <param name="unconditional">
    ///     The scope contributes its level even when another scope opened on the same line, which is
    ///     otherwise collapsed to one. See <see cref="LayoutWriter" />'s Effective.
    /// </param>
    /// <param name="columns">
    ///     ⚠ <see cref="IndentKind.OutdentColumns" /> only: how many columns to the left every line but
    ///     the scope's opening one moves. Ignored by every other kind, which measure in levels.
    /// </param>
    public void OpenIndent(IndentKind kind, bool unconditional = false, int columns = 0) =>
        Open(DocKind.Indent, (int)kind, unconditional ? 1 : 0, columns);

    public void OpenConcat() => Open(DocKind.Concat, 0, 0);

    /// <summary>Opens an <see cref="DocKind.IfBroken" /> over a group; its two children are Then and Else.</summary>
    public void OpenIfBroken(int groupId) => Open(DocKind.IfBroken, groupId, 0);

    /// <param name="alignsCloser">
    ///     The piece immediately after this scope is the scope's own closing delimiter, and takes the
    ///     indentation of the line its opener was on.
    /// </param>
    public void Close(bool alignsCloser = false) {
        var frame = stack[^1];
        stack.RemoveAt(stack.Count - 1);

        var start = frame.ChildStart;
        var count = pending.Count - start;
        var childStart = children.Count;
        var width = 0;
        var head = 0;
        var point = 0;
        var breaks = false;
        var stopped = false;
        var pointStopped = false;
        var childCertain = false;
        byte childOrigin = 0;
        var owned = 0;

        for (var i = start; i < pending.Count; i++) {
            var child = pending[i];
            children.Add(child);
            childCertain |= certain[child];
            childOrigin = Math.Max(childOrigin, certainOrigin[child]);
            if (width < Document.Unbounded) {
                width += flatWidth[child];
            }

            if (owned < Document.Unbounded) {
                owned += ownerWidth[child];
            }

            // The head stops accumulating at the first child that contains a break of its own.
            if (!stopped) {
                head += headWidth[child];
                stopped = flatWidth[child] >= Document.Unbounded;
            }

            // ⚠ The point measure stops at the first *optional* break too, which is the whole
            // difference between it and the head.
            if (!pointStopped) {
                point += pointWidth[child];
                pointStopped = this.breaks[child];
            }

            breaks |= this.breaks[child];
        }

        if (width > Document.Unbounded) {
            width = Document.Unbounded;
        }

        if (owned > Document.Unbounded) {
            owned = Document.Unbounded;
        }

        if (point > Document.Unbounded) {
            point = Document.Unbounded;
        }

        pending.RemoveRange(start, count);

        // ⚠ IfBroken's flat width is its Else branch's, not the sum: a flat owner emits one branch.
        if (frame.Kind == DocKind.IfBroken) {
            width = count > 1 ? flatWidth[children[childStart + 1]] : 0;
            head = count > 1 ? headWidth[children[childStart + 1]] : 0;
            point = count > 1 ? pointWidth[children[childStart + 1]] : 0;
            breaks = count > 1 && this.breaks[children[childStart + 1]];
            childCertain = count > 1 && certain[children[childStart + 1]];
            childOrigin = count > 1 ? certainOrigin[children[childStart + 1]] : (byte)0;
            owned = count > 1 ? ownerWidth[children[childStart + 1]] : 0;
        }

        // ⚠ A group with a certain child has no flat form — "chop if long *or multiline*", one level
        // up, for every container and not only the delimited lists HidesFlatWidthWhenBroken names.
        // Except the group that owns a chain's links, whose question is whether the whole chain fits
        // and whose own links' breaks are not its to take: it is measured by `ownerWidth`, which
        // only strong certainty makes unbounded, so the links' own breaks cannot reach it by
        // summation either. See `certain`, `certainOrigin` and `chainOwners`.
        var isGroup = frame.Kind == DocKind.Group;
        if (isGroup && chainOwners.Contains(frame.Arg1)) {
            width = owned;
        } else if (isGroup && childCertain) {
            width = Document.Unbounded;
        }

        if (isGroup && childOrigin == 2) {
            owned = Document.Unbounded;
        }

        // ⚠ A group that always breaks has no flat form, so nothing that contains it has one either.
        // `int M(int v) => v switch { … }` is a one-line expression body whose body cannot be on one
        // line, and an enclosing group that measures the switch as its flat width concludes the
        // member fits and leaves the arrow where it was.
        // ⚠ A group that is going to break has no flat form either, and "going to break" is not only
        // GroupMode.Break. A Preserve group whose source was broken at its own points and which may
        // not re-join is just as certain, and the construct around it has to know: the oracle chops
        // `Report(Diagnostic.Create(` into two lines as soon as the inner call is broken, although
        // the outer call's own flat width is 59 columns and fits with room to spare. That is the
        // "chop if long *or multiline*" half of chop_if_long, one level up.
        // ⚠ Except a group whose kept break may yet yield to the delimiter after it
        // (GroupFacts.KeptOnlyIfTailFits): the fitter decides that by this very width, so it stays
        // measurable. The group's certainty still reaches its container through `certain` above —
        // the value is multi-line whichever of the two breaks is taken — so the container chops
        // exactly as before; only the group's own number is left honest.
        if (frame.Kind == DocKind.Group
            && (GroupMode)frame.Arg0 == GroupMode.Preserve
            && facts[frame.Arg1] is {
                SourceBroken: true, JoinsIfFits: false, HidesFlatWidthWhenBroken: true, KeptOnlyIfTailFits: false
            }) {
            width = Document.Unbounded;
            owned = Document.Unbounded;
        }

        if (frame.Kind == DocKind.Group && (GroupMode)frame.Arg0 == GroupMode.Break) {
            width = Document.Unbounded;
            owned = Document.Unbounded;
            head = 0;
            breaks = true;
            for (var i = 0; i < count; i++) {
                var child = children[childStart + i];
                if (nodes[child].Kind == DocKind.Line && (LineKind)nodes[child].Arg0 == LineKind.Soft) {
                    break;
                }

                head += headWidth[child];
            }
        }

        var index = Allocate(frame.Kind, frame.Arg0, frame.Arg1, default, childStart, width, head);
        pointWidth[index] = point;
        this.breaks[index] = breaks;
        var selfOrigin = OwnCertainty(frame);
        certain[index] = childCertain || selfOrigin > 0;
        certainOrigin[index] = Math.Max(childOrigin, selfOrigin);
        ownerWidth[index] = owned;
        var afterPointRuns = false;
        afterPoint[index] = frame.Kind == DocKind.Group
            ? MeasureSegments(childStart, count, frame.Arg1, out afterPointRuns, out segment[index])
            : 0;

        nodes[index].Count = count;
        nodes[index].Flags = (alignsCloser ? 1 : 0)
            | (afterPointRuns ? (int)GroupFlags.AfterPointRunsToTheEnd : 0);
        nodes[index].Arg2 = frame.Kind == DocKind.Group ? facts[frame.Arg1].Owner : frame.Arg2;

        if (stack.Count == 0) {
            root = index;
        } else {
            pending.Add(index);
        }
    }

    /// <summary>
    ///     The certainty a group brings of its own — 2 for a group that always breaks or a broken
    ///     group that is not a link, 1 for a broken link, 0 for anything else. See
    ///     <see cref="certainOrigin" />.
    /// </summary>
    byte OwnCertainty(in Frame frame) {
        if (frame.Kind != DocKind.Group) {
            return 0;
        }

        var mode = (GroupMode)frame.Arg0;
        if (mode == GroupMode.Break) {
            return 2;
        }

        if (mode != GroupMode.Preserve || facts[frame.Arg1] is not { SourceBroken: true, JoinsIfFits: false }) {
            return 0;
        }

        return facts[frame.Arg1].ChainLink ? (byte)1 : (byte)2;
    }

    public Document Build() {
        while (stack.Count > 0) {
            Close();
        }

        return new Document(
            nodes,
            nodeCount,
            [.. children],
            [.. strings],
            root,
            groupCount,
            flatWidth,
            headWidth,
            pointWidth,
            afterPoint,
            segment,
            segmentHead,
            breaks,
            [.. facts]
        );
    }

    /// <summary>
    ///     Measures the stretch after each of a group's own break points, and returns the first one.
    /// </summary>
    /// <remarks>
    ///     Two numbers per point, because two rules ask different questions about the same gap.
    ///     <list type="bullet">
    ///         <item>
    ///             <see cref="Document.SegmentOf" /> is the <em>flat</em> width up to the next point: what a fill
    ///             asks, because a fill decides whether the next item goes on this line whole. Verified against
    ///             the oracle on a collection initializer whose second element is a 104-column object
    ///             initializer: the oracle breaks before it, so the question is the item's whole width and not
    ///             the width of its first line.
    ///         </item>
    ///         <item>
    ///             <see cref="Document.AfterPointOf" /> is the <em>point</em> width: what the ordering rule asks,
    ///             because it wants to know where the current line would end if this group declined to break
    ///             and the construct inside it wrapped instead.
    ///         </item>
    ///     </list>
    ///     ⚠ Linear despite the nested loop: the segments partition the children, so each child is
    ///     visited by exactly one of them.
    /// </remarks>
    int MeasureSegments(int childStart, int count, int group, out bool firstRunsToTheEnd, out int firstSegment) {
        firstRunsToTheEnd = false;
        firstSegment = 0;
        if (!ownPoints.Contains(group)) {
            return 0;
        }

        var first = -1;
        var current = -1;
        var flat = 0;
        var point = 0;
        var pointStopped = false;
        var pointDepth = 0;
        var head = 0;
        var headStopped = false;

        Walk(childStart, count, 0);

        // ⚠ The point still open when the walk ends is the group's last, and it is flagged here
        // because this is the first moment anything knows which one that was. A fill's last point is
        // the only one whose segment does not end at another point of the same group, so it is the
        // only one that has to ask what follows the group — see LineFlags.LastPoint.
        var last = current;
        Flush();
        if (last >= 0) {
            nodes[last].Flags |= (int)LineFlags.LastPoint;
        }

        // ⚠ Whether the first point's measure reached the group's end without meeting a break —
        // no point of a nested group that can break, no required line. Then nothing inside the group
        // will end the line the group is on, and the ordering rule has to count what trails the
        // group on that line (SK-DIV-0114). Only the *first* point's answer is the group's, because
        // that is the one AfterPointOf reports.
        firstRunsToTheEnd = first >= 0 && first == last && !pointStopped;

        // ⚠ The first point's flat segment is the group's too, beside its point width: for a group
        // with one point it is everything past that point, which is the tail a kept break that may
        // yield to the delimiter after it is measured by (GroupFacts.KeptOnlyIfTailFits). Flat, and
        // not the point width, because the question is whether the whole value fits on the line it
        // would move to — the point measure stops at the bracket's own first point, one column in.
        if (first >= 0) {
            firstSegment = segment[first];
        }

        return first < 0 ? 0 : afterPoint[first];

        void Flush() {
            if (current >= 0) {
                segment[current] = flat;
                afterPoint[current] = point;
                segmentHead[current] = Math.Min(head, flat);
            }
        }

        // Whether a nested group's own points are places a break could land: it breaks always, on
        // width, or because its source was broken there and it may not re-join.
        bool CanBreak(int nestedGroup) =>
            modes.TryGetValue(nestedGroup, out var mode)
            && (mode is GroupMode.Break or GroupMode.Auto
                || mode == GroupMode.Preserve
                && (facts[nestedGroup].BreaksIfTooLong
                    || facts[nestedGroup] is { SourceBroken: true, JoinsIfFits: false }));

        void Walk(int start, int n, int depth) {
            for (var i = 0; i < n; i++) {
                var child = children[start + i];
                if (IsOwnBreakPoint(child, group)) {
                    Flush();
                    current = child;
                    pointDepth = depth;
                    flat = 0;
                    point = 0;
                    pointStopped = false;
                    head = 0;
                    headStopped = false;
                    if (first < 0) {
                        first = child;
                    }

                    continue;
                }

                // ⚠ A container is spliced rather than measured, because a group's own break points
                // are not always its direct children: a group that spends a continuation level opens
                // the indent scope *inside* itself, so every one of its points is a grandchild.
                // Measuring the container as one child leaves both numbers below at zero for the
                // whole `=` family and for every delimited list that spends a level — which makes a
                // fill never break and the ordering rule's second question answer "yes"
                // unconditionally.
                // ⚠ `IfBroken` is not spliced: its flat width is one branch's rather than the sum,
                // so splicing it would count both.
                ref var node = ref nodes[child];
                if (node.Count > 0 && node.Kind is DocKind.Concat or DocKind.Group or DocKind.Indent or DocKind.Fill) {
                    // A nested group can require a break using only soft points (for example,
                    // a switch expression). Splicing its children must not restore a flat form
                    // the group itself has already ruled out.
                    if (current >= 0 && node.Kind == DocKind.Group && flatWidth[child] >= Document.Unbounded) {
                        flat = Document.Unbounded;
                    }

                    Walk(node.Payload, node.Count, depth + (node.Kind == DocKind.Group ? 1 : 0));
                    continue;
                }

                // ⚠ A break the rules require ends the segment rather than making it infinite. A
                // list pattern whose items the author pinned one per line has hard lines between
                // the fill's own points, and measuring one of those as "infinitely wide" makes the
                // fill point in front of it break — so a byte array written eight per line came back
                // seven and one.
                if (node.Kind == DocKind.Line && flatWidth[child] >= Document.Unbounded) {
                    // A hard break inside a nested item does not end the enclosing fill's
                    // segment. That item has no flat form. Otherwise a break created on pass
                    // one and preserved on pass two shortens its measured width (#337, #339).
                    // ⚠ It does end the item's *head*, which is the other measure a fill reads —
                    // see segmentHead — and that is what keeps the two passes agreeing now.
                    if (current >= 0 && depth > pointDepth) {
                        flat = Document.Unbounded;
                        pointStopped = true;
                        headStopped = true;
                    } else {
                        Flush();
                        current = -1;
                    }

                    continue;
                }

                if (current < 0) {
                    continue;
                }

                // A nested group's point that can break ends the head; one that cannot renders flat
                // and counts as its flat rendering.
                if (node.Kind == DocKind.Line
                    && (LineKind)node.Arg0 == LineKind.Soft
                    && !headStopped
                    && CanBreak(node.Arg2)) {
                    headStopped = true;
                }

                flat = flat >= Document.Unbounded || flatWidth[child] >= Document.Unbounded
                    ? Document.Unbounded
                    : flat + flatWidth[child];

                if (!headStopped) {
                    head = head >= Document.Unbounded || flatWidth[child] >= Document.Unbounded
                        ? Document.Unbounded
                        : head + flatWidth[child];
                }

                if (!pointStopped) {
                    point += pointWidth[child];
                    pointStopped = breaks[child];
                }
            }
        }
    }

    /// <summary>Whether this child is a break point belonging to the group being closed.</summary>
    bool IsOwnBreakPoint(int child, int group) {
        ref var node = ref nodes[child];
        return node.Kind == DocKind.Line && (LineKind)node.Arg0 == LineKind.Soft && node.Arg2 == group;
    }

    void Open(DocKind kind, int arg0, int arg1, int arg2 = -1) =>
        stack.Add(new Frame(kind, arg0, arg1, pending.Count, arg2));

    void Leaf(DocKind kind, int arg0, int arg1, SourceSpan source, int payload, int width, int head) =>
        pending.Add(Allocate(kind, arg0, arg1, source, payload, width, head));

    int Allocate(DocKind kind, int arg0, int arg1, SourceSpan source, int payload, int width, int head) {
        if (nodeCount == nodes.Length) {
            Array.Resize(ref nodes, nodes.Length * 2);
            Array.Resize(ref flatWidth, flatWidth.Length * 2);
            Array.Resize(ref headWidth, headWidth.Length * 2);
            Array.Resize(ref pointWidth, pointWidth.Length * 2);
            Array.Resize(ref afterPoint, afterPoint.Length * 2);
            Array.Resize(ref segment, segment.Length * 2);
            Array.Resize(ref segmentHead, segmentHead.Length * 2);
            Array.Resize(ref breaks, breaks.Length * 2);
            Array.Resize(ref certain, certain.Length * 2);
            Array.Resize(ref certainOrigin, certainOrigin.Length * 2);
            Array.Resize(ref ownerWidth, ownerWidth.Length * 2);
        }

        ref var node = ref nodes[nodeCount];
        node.Kind = kind;
        node.Arg0 = arg0;
        node.Arg1 = arg1;
        node.Payload = payload;
        node.Count = 0;
        node.Flags = 0;
        node.Arg2 = -1;
        node.Source = source;
        flatWidth[nodeCount] = width;
        headWidth[nodeCount] = head;

        // A leaf's point width is its head width and it holds no break; Line and Close override
        // both. Written here so that every allocation site does not have to.
        pointWidth[nodeCount] = head;
        afterPoint[nodeCount] = 0;
        segment[nodeCount] = 0;
        segmentHead[nodeCount] = 0;
        breaks[nodeCount] = kind == DocKind.Line && (LineKind)arg0 != LineKind.Soft;
        certain[nodeCount] = width >= Document.Unbounded;
        certainOrigin[nodeCount] = width >= Document.Unbounded ? (byte)2 : (byte)0;
        ownerWidth[nodeCount] = width;
        return nodeCount++;
    }

    int AddString(string value) {
        strings.Add(value);
        return strings.Count - 1;
    }

    /// <summary>Columns up to the first newline: what a multi-line token contributes to its line.</summary>
    static int FirstLineWidth(string value) {
        for (var i = 0; i < value.Length; i++) {
            if (value[i] is '\n' or '\r') {
                return TextWidth.Measure(value[..i]);
            }
        }

        return TextWidth.Measure(value);
    }

    static bool ContainsNewLine(string value) {
        // ⚠ No LINQ and no allocation: this runs once per verbatim piece and there are millions of
        // them over the reference corpus (docs/plan/13 § "The fitting pass").
        foreach (var item in value) {
            if (item == '\n') {
                return true;
            }
        }

        return false;
    }

    /// <param name="Arg2">
    ///     The node's <see cref="DocNode.Arg2" /> for kinds that carry one of their own. A group
    ///     overwrites it with its owner in <see cref="Close" />; an <see cref="DocKind.Indent" /> keeps
    ///     what was opened with, which is <see cref="IndentKind.OutdentColumns" />' column count.
    /// </param>
    readonly record struct Frame(DocKind Kind, int Arg0, int Arg1, int ChildStart, int Arg2 = -1);
}
