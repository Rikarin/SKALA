// CA1051: DocNode's fields are public on purpose. It is a mutable struct in a per-file arena, and
// the arena is the design (docs/plan/13 § "The fitting pass"); property accessors on the hot path
// of a structure that exists to avoid allocation would be a joke at the reader's expense.
// CA1711: a [Flags] enum named *Flags is what every reader expects it to be called.
#pragma warning disable CA1051, CA1711

namespace Rikarin.Skala.Formatting;

/// <summary>The node kinds of the document IR (docs/plan/04 § "The document IR").</summary>
public enum DocKind {
    /// <summary>A token's text. Width is columns, not characters.</summary>
    Text,

    /// <summary>An ordered sequence of children.</summary>
    Concat,

    /// <summary>An inter-token gap that stays on one line.</summary>
    Space,

    /// <summary>An inter-token gap that is a line break, possibly with blank lines.</summary>
    Line,

    /// <summary>A wrapping unit with a three-state mode (ADR-002).</summary>
    Group,

    /// <summary>Fill: break only where the line runs out. <c>wrap_if_long</c>.</summary>
    Fill,

    /// <summary>An indentation scope.</summary>
    Indent,

    /// <summary>Emits one branch or the other depending on a group's resolved mode.</summary>
    IfBroken,

    /// <summary>Raw text copied byte-for-byte: disabled <c>#if</c> regions, raw strings, off-tag spans.</summary>
    Verbatim,

    /// <summary>Maps a point in the output back to the input, for minimal edits and for verification.</summary>
    Anchor
}

/// <summary>Whether an inter-token gap must, must not, or may hold a space.</summary>
public enum SpaceKind {
    Required,
    Forbidden,

    /// <summary>Leave whatever the author wrote. Used for gaps no rule governs.</summary>
    Preserve
}

/// <summary>The flavours of line break.</summary>
public enum LineKind {
    /// <summary>Always a break.</summary>
    Hard,

    /// <summary>A break only when the enclosing group is broken.</summary>
    Soft,

    /// <summary>A break plus <c>n</c> blank lines.</summary>
    Blank,

    /// <summary>Broken iff the source was broken here.</summary>
    Preserve
}

/// <summary>
/// The three-state group model, the concrete form of ADR-002.
/// </summary>
public enum GroupMode {
    /// <summary>Never break.</summary>
    Flat,

    /// <summary>Always break.</summary>
    Break,

    /// <summary>Break iff too wide — the classic Prettier group.</summary>
    Auto,

    /// <summary>⚠ The third state: broken iff it was broken in the source, subject to width.</summary>
    Preserve
}

/// <summary>The indentation flavours from docs/plan/04 § "Indentation".</summary>
public enum IndentKind {
    /// <summary>One level per <c>{ }</c>, per <c>case</c>, per embedded statement.</summary>
    Block,

    /// <summary>Continuation lines of one expression. <c>continuous_line_indent = single</c>.</summary>
    Continuous,

    /// <summary>No change; a scope marker only.</summary>
    None,

    /// <summary>One level less — the nested-statement outdent family.</summary>
    Outdent
}

/// <summary>
/// One IR node.
/// </summary>
/// <remarks>
/// ⚠ A struct in a per-file arena, indexed by <c>int</c>, not a class
/// (docs/plan/13 § "The fitting pass"): a 1 000-line file produces ~40 000 nodes and the reference
/// corpus produces ~110 M, so class allocation here is several GB of garbage per run. docs/plan/04
/// writes the IR as records; the two documents disagree and performance wins, because doc 13 states
/// its constraints as design constraints rather than as later tuning.
/// </remarks>
public struct DocNode {
    public DocKind Kind;

    /// <summary>Kind-specific: SpaceKind, LineKind, GroupMode, IndentKind, or a group id.</summary>
    public int Arg0;

    /// <summary>Kind-specific: blank count, text width, or the referenced group id.</summary>
    public int Arg1;

    /// <summary>Index into the child arena, or a string-table index.</summary>
    public int Payload;

    /// <summary>Child count, for the child arena slice.</summary>
    public int Count;

    /// <summary>Kind-specific bit flags: <see cref="LineFlags"/>, <see cref="VerbatimFlags"/>.</summary>
    public int Flags;

    /// <summary>The source span this node came from; <see cref="SourceSpan.Length"/> 0 when synthetic.</summary>
    public SourceSpan Source;
}

/// <summary>
/// A document: a struct arena of nodes plus the side tables they index.
/// </summary>
public sealed class Document {
    internal Document(
        DocNode[] nodes,
        int nodeCount,
        int[] children,
        string[] strings,
        int root,
        int groupCount) {
        Nodes = nodes;
        NodeCount = nodeCount;
        Children = children;
        Strings = strings;
        Root = root;
        GroupCount = groupCount;
    }

    public DocNode[] Nodes { get; }

    public int NodeCount { get; }

    public int[] Children { get; }

    public string[] Strings { get; }

    public int Root { get; }

    public int GroupCount { get; }

    public ReadOnlySpan<int> ChildrenOf(int node) {
        ref var slot = ref Nodes[node];
        return new ReadOnlySpan<int>(Children, slot.Payload, slot.Count);
    }

    public string TextOf(int node) => Strings[Nodes[node].Payload];
}
