namespace Rikarin.Skala.Formatting;

/// <summary>
/// Builds a <see cref="Document"/> into pooled buffers.
/// </summary>
/// <remarks>
/// The builder is a stack machine rather than a tree of constructors: an <c>Open*</c> call pushes a
/// container, the leaf methods append to whatever is open, and <see cref="Close"/> pops. That keeps
/// the whole document in three growable arrays and never allocates a node
/// (docs/plan/13 § "The fitting pass").
/// <para>
/// ⚠ The measure pass is fused into it. Every node's flat width is accumulated as the node is
/// appended, and a container's width is the sum its children already deposited into the open frame,
/// so the document arrives measured and the fitter never traverses it to find out
/// (docs/plan/13 § "The fitting pass": "the measure pass is fused into the build pass where a
/// group's contents are already known, which removes one full traversal").
/// </para>
/// </remarks>
public sealed class DocumentBuilder {
    readonly List<string> _strings = [];

    /// <summary>Children of frames that are still open, innermost last.</summary>
    readonly List<int> _pending = [];

    /// <summary>Children of frames that have closed. Node slices point in here.</summary>
    readonly List<int> _children = [];

    readonly List<Frame> _stack = [];

    /// <summary>Group metadata, indexed by group id and filled as ids are handed out.</summary>
    readonly List<GroupFacts> _facts = [];

    DocNode[] _nodes = new DocNode[512];
    int[] _flatWidth = new int[512];
    int[] _headWidth = new int[512];

    /// <summary>Width to the first break point, optional ones included. <see cref="Document.PointWidthOf"/>.</summary>
    int[] _pointWidth = new int[512];

    /// <summary>Width from a group's own first break point to the next. <see cref="Document.AfterPointOf"/>.</summary>
    int[] _afterPoint = new int[512];

    /// <summary>
    /// The groups that own at least one break point.
    /// </summary>
    /// <remarks>
    /// ⚠ It gates <see cref="MeasureSegments"/>'s descent, so that the root group — which owns no
    /// points and contains the file — is never walked and the measure stays linear in practice.
    /// </remarks>
    readonly HashSet<int> _ownPoints = [];

    /// <summary>Flat width from one fill point to the next. <see cref="Document.SegmentOf"/>.</summary>
    int[] _segment = new int[512];

    /// <summary>Whether the subtree holds a break point of any kind. Stops the two measures above.</summary>
    bool[] _breaks = new bool[512];

    int _nodeCount;
    int _groupCount;
    int _root = -1;

    public DocumentBuilder() {
        // Index 0 is reserved so that a zero payload can mean "nothing here".
        _strings.Add(string.Empty);
        OpenConcat();
    }

    /// <summary>The number of groups handed out so far.</summary>
    public int GroupCount => _groupCount;

    /// <summary>Allocates a group id, so that <see cref="OpenIfBroken"/> can reference the group.</summary>
    public int NextGroupId() {
        _facts.Add(new GroupFacts());
        return _groupCount++;
    }

    /// <summary>
    /// Records what the fitter needs to know about a group before it meets it, which only the front
    /// end can answer.
    /// </summary>
    public void DescribeGroup(int groupId, GroupFacts facts) => _facts[groupId] = facts;

    /// <summary>
    /// A token's text.
    /// </summary>
    /// <remarks>
    /// ⚠ A token that spans lines — a raw string literal, a verbatim string, a block comment — has
    /// no flat width, because there is no line it can be laid flat on. <c>chop_if_long</c> reads
    /// "chop if long <em>or multiline</em>" in ReSharper's own summary, and the oracle chops an
    /// argument list around a multi-line string exactly as it chops one that is too wide.
    /// </remarks>
    public void Text(string value, SourceSpan source, VerbatimFlags flags = VerbatimFlags.None) {
        var index = _pending.Count;
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
        _nodes[_pending[index]].Flags = (int)flags;
    }

    /// <summary>Raw text, copied byte-for-byte and never reindented.</summary>
    public void Verbatim(string value, SourceSpan source, VerbatimFlags flags = VerbatimFlags.None) {
        var index = _pending.Count;
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
        _nodes[_pending[index]].Flags = (int)flags;
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
    /// A line break. <paramref name="newLine"/> carries the source's own ending so that a file with
    /// CRLF stays CRLF — <c>enforce_line_ending_style = false</c> means mixed endings are preserved
    /// rather than normalised.
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
    /// A break point: a gap the layout may or may not break at, owned by <paramref name="group"/>.
    /// </summary>
    /// <param name="flatSpace">
    /// What the gap renders as when the group stays flat. ⚠ Not uniform across a construct's own
    /// points: the gap after <c>(</c> is nothing and the gap after <c>,</c> is a space.
    /// </param>
    /// <param name="fill">
    /// The point breaks only when what follows it does not fit, rather than with its group.
    /// <see cref="LineFlags.FillPoint"/>.
    /// </param>
    public void BreakPoint(int group, bool flatSpace, bool fill = false, int blankLines = 0, string? newLine = null) {
        var index = _pending.Count;
        Leaf(
            DocKind.Line,
            (int)LineKind.Soft,
            blankLines,
            default,
            newLine is null ? 0 : AddString(newLine),
            flatSpace ? 1 : 0,
            flatSpace ? 1 : 0
        );
        ref var node = ref _nodes[_pending[index]];
        node.Arg2 = group;
        node.Flags = (flatSpace ? (int)LineFlags.FlatSpace : 0) | (fill ? (int)LineFlags.FillPoint : 0);
        _ownPoints.Add(group);

        // ⚠ A break point stops the point measure, which is what distinguishes it from the head.
        // ⚠ And it contributes nothing to it. "The rest of this line if every break point is taken"
        // ends *before* the space the point would have rendered as, and LayoutWriter.TrailingWidth
        // already says so for a point that is a direct sibling — but a point nested inside a group
        // reached that code through the group's own point width, which was counting it. The two
        // disagreeing is worth a column, and a column is a wrap: an expression-bodied member whose
        // declaration is exactly 120 wide came back with its parameter list chopped, while the same
        // declaration with a block body did not.
        _pointWidth[_pending[index]] = 0;
        _breaks[_pending[index]] = true;
    }

    /// <summary>A sync point between output and input, emitted immediately before what it introduces.</summary>
    public void Anchor(SourceSpan source, int tokenId) => Leaf(DocKind.Anchor, tokenId, 0, source, 0, 0, 0);

    public void OpenGroup(GroupMode mode, int groupId) => Open(DocKind.Group, (int)mode, groupId);

    public void OpenFill() => Open(DocKind.Fill, 0, 0);

    /// <param name="unconditional">
    /// The scope contributes its level even when another scope opened on the same line, which is
    /// otherwise collapsed to one. See <see cref="LayoutWriter"/>'s Effective.
    /// </param>
    public void OpenIndent(IndentKind kind, bool unconditional = false) =>
        Open(DocKind.Indent, (int)kind, unconditional ? 1 : 0);

    public void OpenConcat() => Open(DocKind.Concat, 0, 0);

    /// <summary>Opens an <see cref="DocKind.IfBroken"/> over a group; its two children are Then and Else.</summary>
    public void OpenIfBroken(int groupId) => Open(DocKind.IfBroken, groupId, 0);

    /// <param name="alignsCloser">
    /// The piece immediately after this scope is the scope's own closing delimiter, and takes the
    /// indentation of the line its opener was on.
    /// </param>
    public void Close(bool alignsCloser = false) {
        var frame = _stack[^1];
        _stack.RemoveAt(_stack.Count - 1);

        var start = frame.ChildStart;
        var count = _pending.Count - start;
        var childStart = _children.Count;
        var width = 0;
        var head = 0;
        var point = 0;
        var breaks = false;
        var stopped = false;
        var pointStopped = false;

        for (var i = start; i < _pending.Count; i++) {
            var child = _pending[i];
            _children.Add(child);
            if (width < Document.Unbounded) {
                width += _flatWidth[child];
            }

            // The head stops accumulating at the first child that contains a break of its own.
            if (!stopped) {
                head += _headWidth[child];
                stopped = _flatWidth[child] >= Document.Unbounded;
            }

            // ⚠ The point measure stops at the first *optional* break too, which is the whole
            // difference between it and the head.
            if (!pointStopped) {
                point += _pointWidth[child];
                pointStopped = _breaks[child];
            }

            breaks |= _breaks[child];
        }

        if (width > Document.Unbounded) {
            width = Document.Unbounded;
        }

        if (point > Document.Unbounded) {
            point = Document.Unbounded;
        }

        _pending.RemoveRange(start, count);

        // ⚠ IfBroken's flat width is its Else branch's, not the sum: a flat owner emits one branch.
        if (frame.Kind == DocKind.IfBroken) {
            width = count > 1 ? _flatWidth[_children[childStart + 1]] : 0;
            head = count > 1 ? _headWidth[_children[childStart + 1]] : 0;
            point = count > 1 ? _pointWidth[_children[childStart + 1]] : 0;
            breaks = count > 1 && _breaks[_children[childStart + 1]];
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
        if (frame.Kind == DocKind.Group
            && (GroupMode)frame.Arg0 == GroupMode.Preserve
            && _facts[frame.Arg1] is { SourceBroken: true, JoinsIfFits: false, HidesFlatWidthWhenBroken: true }) {
            width = Document.Unbounded;
        }

        if (frame.Kind == DocKind.Group && (GroupMode)frame.Arg0 == GroupMode.Break) {
            width = Document.Unbounded;
            head = 0;
            breaks = true;
            for (var i = 0; i < count; i++) {
                var child = _children[childStart + i];
                if (_nodes[child].Kind == DocKind.Line && (LineKind)_nodes[child].Arg0 == LineKind.Soft) {
                    break;
                }

                head += _headWidth[child];
            }
        }

        var index = Allocate(frame.Kind, frame.Arg0, frame.Arg1, default, childStart, width, head);
        _pointWidth[index] = point;
        _breaks[index] = breaks;
        _afterPoint[index] = frame.Kind == DocKind.Group ? MeasureSegments(childStart, count, frame.Arg1) : 0;
        _nodes[index].Count = count;
        _nodes[index].Flags = alignsCloser ? 1 : 0;
        _nodes[index].Arg2 = frame.Kind == DocKind.Group ? _facts[frame.Arg1].Owner : -1;

        if (_stack.Count == 0) {
            _root = index;
        } else {
            _pending.Add(index);
        }
    }

    public Document Build() {
        while (_stack.Count > 0) {
            Close();
        }

        return new Document(
            _nodes,
            _nodeCount,
            [.. _children],
            [.. _strings],
            _root,
            _groupCount,
            _flatWidth,
            _headWidth,
            _pointWidth,
            _afterPoint,
            _segment,
            _breaks,
            [.. _facts]
        );
    }

    /// <summary>
    /// Measures the stretch after each of a group's own break points, and returns the first one.
    /// </summary>
    /// <remarks>
    /// Two numbers per point, because two rules ask different questions about the same gap.
    /// <list type="bullet">
    /// <item>
    /// <see cref="Document.SegmentOf"/> is the <em>flat</em> width up to the next point: what a fill
    /// asks, because a fill decides whether the next item goes on this line whole. Verified against
    /// the oracle on a collection initializer whose second element is a 104-column object
    /// initializer: the oracle breaks before it, so the question is the item's whole width and not
    /// the width of its first line.
    /// </item>
    /// <item>
    /// <see cref="Document.AfterPointOf"/> is the <em>point</em> width: what the ordering rule asks,
    /// because it wants to know where the current line would end if this group declined to break
    /// and the construct inside it wrapped instead.
    /// </item>
    /// </list>
    /// ⚠ Linear despite the nested loop: the segments partition the children, so each child is
    /// visited by exactly one of them.
    /// </remarks>
    int MeasureSegments(int childStart, int count, int group) {
        if (!_ownPoints.Contains(group)) {
            return 0;
        }

        var first = -1;
        var current = -1;
        var flat = 0;
        var point = 0;
        var pointStopped = false;

        Walk(childStart, count);
        Flush();
        return first < 0 ? 0 : _afterPoint[first];

        void Flush() {
            if (current >= 0) {
                _segment[current] = flat;
                _afterPoint[current] = point;
            }
        }

        void Walk(int start, int n) {
            for (var i = 0; i < n; i++) {
                var child = _children[start + i];
                if (IsOwnBreakPoint(child, group)) {
                    Flush();
                    current = child;
                    flat = 0;
                    point = 0;
                    pointStopped = false;
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
                ref var node = ref _nodes[child];
                if (node.Count > 0 && node.Kind is DocKind.Concat or DocKind.Group or DocKind.Indent or DocKind.Fill) {
                    Walk(node.Payload, node.Count);
                    continue;
                }

                // ⚠ A break the rules require ends the segment rather than making it infinite. A
                // list pattern whose items the author pinned one per line has hard lines between
                // the fill's own points, and measuring one of those as "infinitely wide" makes the
                // fill point in front of it break — so a byte array written eight per line came back
                // seven and one.
                if (node.Kind == DocKind.Line && _flatWidth[child] >= Document.Unbounded) {
                    Flush();
                    current = -1;
                    continue;
                }

                if (current < 0) {
                    continue;
                }

                flat = flat >= Document.Unbounded || _flatWidth[child] >= Document.Unbounded
                    ? Document.Unbounded
                    : flat + _flatWidth[child];

                if (!pointStopped) {
                    point += _pointWidth[child];
                    pointStopped = _breaks[child];
                }
            }
        }
    }

    /// <summary>Whether this child is a break point belonging to the group being closed.</summary>
    bool IsOwnBreakPoint(int child, int group) {
        ref var node = ref _nodes[child];
        return node.Kind == DocKind.Line && (LineKind)node.Arg0 == LineKind.Soft && node.Arg2 == group;
    }

    void Open(DocKind kind, int arg0, int arg1) => _stack.Add(new Frame(kind, arg0, arg1, _pending.Count));

    void Leaf(DocKind kind, int arg0, int arg1, SourceSpan source, int payload, int width, int head) =>
        _pending.Add(Allocate(kind, arg0, arg1, source, payload, width, head));

    int Allocate(DocKind kind, int arg0, int arg1, SourceSpan source, int payload, int width, int head) {
        if (_nodeCount == _nodes.Length) {
            Array.Resize(ref _nodes, _nodes.Length * 2);
            Array.Resize(ref _flatWidth, _flatWidth.Length * 2);
            Array.Resize(ref _headWidth, _headWidth.Length * 2);
            Array.Resize(ref _pointWidth, _pointWidth.Length * 2);
            Array.Resize(ref _afterPoint, _afterPoint.Length * 2);
            Array.Resize(ref _segment, _segment.Length * 2);
            Array.Resize(ref _breaks, _breaks.Length * 2);
        }

        ref var node = ref _nodes[_nodeCount];
        node.Kind = kind;
        node.Arg0 = arg0;
        node.Arg1 = arg1;
        node.Payload = payload;
        node.Count = 0;
        node.Flags = 0;
        node.Arg2 = -1;
        node.Source = source;
        _flatWidth[_nodeCount] = width;
        _headWidth[_nodeCount] = head;

        // A leaf's point width is its head width and it holds no break; Line and Close override
        // both. Written here so that every allocation site does not have to.
        _pointWidth[_nodeCount] = head;
        _afterPoint[_nodeCount] = 0;
        _segment[_nodeCount] = 0;
        _breaks[_nodeCount] = kind == DocKind.Line && (LineKind)arg0 != LineKind.Soft;
        return _nodeCount++;
    }

    int AddString(string value) {
        _strings.Add(value);
        return _strings.Count - 1;
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
        for (var i = 0; i < value.Length; i++) {
            if (value[i] == '\n') {
                return true;
            }
        }

        return false;
    }

    readonly record struct Frame(DocKind Kind, int Arg0, int Arg1, int ChildStart);
}
