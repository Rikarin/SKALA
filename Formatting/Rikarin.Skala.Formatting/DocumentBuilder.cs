namespace Rikarin.Skala.Formatting;

/// <summary>
/// Builds a <see cref="Document"/> into pooled buffers.
/// </summary>
/// <remarks>
/// The builder is a stack machine rather than a tree of constructors: an <c>Open*</c> call pushes a
/// container, the leaf methods append to whatever is open, and <see cref="Close"/> pops. That keeps
/// the whole document in three growable arrays and never allocates a node
/// (docs/plan/13 § "The fitting pass").
/// </remarks>
public sealed class DocumentBuilder {
    readonly List<string> _strings = [];

    /// <summary>Children of frames that are still open, innermost last.</summary>
    readonly List<int> _pending = [];

    /// <summary>Children of frames that have closed. Node slices point in here.</summary>
    readonly List<int> _children = [];

    readonly List<Frame> _stack = [];
    DocNode[] _nodes = new DocNode[512];
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
    public int NextGroupId() => _groupCount++;

    public void Text(string value, SourceSpan source, VerbatimFlags flags = VerbatimFlags.None) {
        var index = _pending.Count;
        Leaf(DocKind.Text, 0, TextWidth.Measure(value), source, AddString(value));
        _nodes[_pending[index]].Flags = (int)flags;
    }

    /// <summary>Raw text, copied byte-for-byte and never reindented.</summary>
    public void Verbatim(string value, SourceSpan source, VerbatimFlags flags = VerbatimFlags.None) {
        var index = _pending.Count;
        Leaf(DocKind.Verbatim, 0, TextWidth.Measure(value), source, AddString(value));
        _nodes[_pending[index]].Flags = (int)flags;
    }

    public void Space(SpaceKind kind) => Leaf(DocKind.Space, (int)kind, 0, default, 0);

    /// <summary>
    /// A line break. <paramref name="newLine"/> carries the source's own ending so that a file with
    /// CRLF stays CRLF — <c>enforce_line_ending_style = false</c> means mixed endings are preserved
    /// rather than normalised.
    /// </summary>
    public void Line(LineKind kind, int blankLines = 0, string? newLine = null) =>
        Leaf(DocKind.Line, (int)kind, blankLines, default, newLine is null ? 0 : AddString(newLine));

    /// <summary>A sync point between output and input, emitted immediately before what it introduces.</summary>
    public void Anchor(SourceSpan source, int tokenId) => Leaf(DocKind.Anchor, tokenId, 0, source, 0);

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
        for (var i = start; i < _pending.Count; i++) {
            _children.Add(_pending[i]);
        }

        _pending.RemoveRange(start, count);

        var index = Allocate(frame.Kind, frame.Arg0, frame.Arg1, default, childStart);
        _nodes[index].Count = count;
        _nodes[index].Flags = alignsCloser ? 1 : 0;

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

        return new Document(_nodes, _nodeCount, [.. _children], [.. _strings], _root, _groupCount);
    }

    void Open(DocKind kind, int arg0, int arg1) => _stack.Add(new Frame(kind, arg0, arg1, _pending.Count));

    void Leaf(DocKind kind, int arg0, int arg1, SourceSpan source, int payload) =>
        _pending.Add(Allocate(kind, arg0, arg1, source, payload));

    int Allocate(DocKind kind, int arg0, int arg1, SourceSpan source, int payload) {
        if (_nodeCount == _nodes.Length) {
            Array.Resize(ref _nodes, _nodes.Length * 2);
        }

        ref var node = ref _nodes[_nodeCount];
        node.Kind = kind;
        node.Arg0 = arg0;
        node.Arg1 = arg1;
        node.Payload = payload;
        node.Count = 0;
        node.Flags = 0;
        node.Source = source;
        return _nodeCount++;
    }

    int AddString(string value) {
        _strings.Add(value);
        return _strings.Count - 1;
    }

    readonly record struct Frame(DocKind Kind, int Arg0, int Arg1, int ChildStart);
}
