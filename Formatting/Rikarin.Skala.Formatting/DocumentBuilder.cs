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
            multiline ? FirstLineWidth(value) : TextWidth.Measure(value));
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
            multiline ? FirstLineWidth(value) : TextWidth.Measure(value));
        _nodes[_pending[index]].Flags = (int)flags;
    }

    public void Space(SpaceKind kind) =>
        Leaf(DocKind.Space, (int)kind, 0, default, 0, kind == SpaceKind.Forbidden ? 0 : 1, kind == SpaceKind.Forbidden ? 0 : 1);

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
            kind == LineKind.Soft ? 1 : 0);

    /// <summary>
    /// A break point: a gap the layout may or may not break at, owned by <paramref name="group"/>.
    /// </summary>
    /// <param name="flatSpace">
    /// What the gap renders as when the group stays flat. ⚠ Not uniform across a construct's own
    /// points: the gap after <c>(</c> is nothing and the gap after <c>,</c> is a space.
    /// </param>
    public void BreakPoint(int group, bool flatSpace, int blankLines = 0, string? newLine = null) {
        var index = _pending.Count;
        Leaf(
            DocKind.Line,
            (int)LineKind.Soft,
            blankLines,
            default,
            newLine is null ? 0 : AddString(newLine),
            flatSpace ? 1 : 0,
            flatSpace ? 1 : 0);
        ref var node = ref _nodes[_pending[index]];
        node.Arg2 = group;
        node.Flags = flatSpace ? (int)LineFlags.FlatSpace : (int)LineFlags.None;
    }

    /// <summary>A sync point between output and input, emitted immediately before what it introduces.</summary>
    public void Anchor(SourceSpan source, int tokenId) => Leaf(DocKind.Anchor, tokenId, 0, source, 0, 0, 0);

    public void OpenGroup(GroupMode mode, int groupId) => Open(DocKind.Group, (int)mode, groupId);

    public void OpenFill() => Open(DocKind.Fill, 0, 0);

    public void OpenIndent(IndentKind kind) => Open(DocKind.Indent, (int)kind, 0);

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
        var stopped = false;
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
        }

        if (width > Document.Unbounded) {
            width = Document.Unbounded;
        }

        _pending.RemoveRange(start, count);

        // ⚠ IfBroken's flat width is its Else branch's, not the sum: a flat owner emits one branch.
        if (frame.Kind == DocKind.IfBroken) {
            width = count > 1 ? _flatWidth[_children[childStart + 1]] : 0;
            head = count > 1 ? _headWidth[_children[childStart + 1]] : 0;
        }

        // ⚠ A group that always breaks has no flat form, so nothing that contains it has one either.
        // `int M(int v) => v switch { … }` is a one-line expression body whose body cannot be on one
        // line, and an enclosing group that measures the switch as its flat width concludes the
        // member fits and leaves the arrow where it was.
        if (frame.Kind == DocKind.Group && (GroupMode)frame.Arg0 == GroupMode.Break) {
            width = Document.Unbounded;
            head = 0;
            for (var i = 0; i < count; i++) {
                var child = _children[childStart + i];
                if (_nodes[child].Kind == DocKind.Line && (LineKind)_nodes[child].Arg0 == LineKind.Soft) {
                    break;
                }

                head += _headWidth[child];
            }
        }

        var index = Allocate(frame.Kind, frame.Arg0, frame.Arg1, default, childStart, width, head);
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
            [.. _facts]);
    }

    void Open(DocKind kind, int arg0, int arg1) => _stack.Add(new Frame(kind, arg0, arg1, _pending.Count));

    void Leaf(DocKind kind, int arg0, int arg1, SourceSpan source, int payload, int width, int head) =>
        _pending.Add(Allocate(kind, arg0, arg1, source, payload, width, head));

    int Allocate(DocKind kind, int arg0, int arg1, SourceSpan source, int payload, int width, int head) {
        if (_nodeCount == _nodes.Length) {
            Array.Resize(ref _nodes, _nodes.Length * 2);
            Array.Resize(ref _flatWidth, _flatWidth.Length * 2);
            Array.Resize(ref _headWidth, _headWidth.Length * 2);
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
