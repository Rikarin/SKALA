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
/// Wadler-shaped, iterative rather than recursive (C# has no TCO and files nest 30 deep), and two
/// passes per group tree because of <c>if_owner_is_single_line</c>
/// (docs/plan/04 § "The fitting algorithm").
/// <para>
/// In Milestone 1 no line ever moves, so every group the C# builder emits is
/// <see cref="GroupMode.Flat"/> and the measure pass is the only interesting half. The resolver is
/// written in full anyway, because Milestone 2 and 3 add group kinds, not a new algorithm.
/// </para>
/// </remarks>
public sealed class Fitter {
    /// <summary>A group containing a hard break can never be flat; this is its flat width.</summary>
    public const int Unbounded = int.MaxValue / 4;

    readonly Document _document;
    readonly int[] _flatWidth;
    readonly ResolvedMode[] _modes;
    readonly bool[] _sourceBroken;

    Fitter(Document document) {
        _document = document;
        _flatWidth = new int[document.NodeCount];
        _modes = new ResolvedMode[Math.Max(1, document.GroupCount)];
        _sourceBroken = new bool[Math.Max(1, document.GroupCount)];
    }

    /// <summary>Resolves every group and returns the mode table, indexed by group id.</summary>
    public static ResolvedMode[] Resolve(Document document, int width) {
        var fitter = new Fitter(document);
        fitter.Measure();
        fitter.ResolveGroups(width);
        return fitter._modes;
    }

    /// <summary>
    /// Flat width per node, and whether each Preserve group was broken in the source.
    /// </summary>
    /// <remarks>
    /// Iterative post-order: the node arena is built bottom-up by
    /// <see cref="DocumentBuilder"/>, so a child always has a lower index than its parent and one
    /// forward sweep is a valid post-order.
    /// </remarks>
    void Measure() {
        for (var i = 0; i < _document.NodeCount; i++) {
            ref var node = ref _document.Nodes[i];
            switch (node.Kind) {
                case DocKind.Text:
                case DocKind.Verbatim:
                    _flatWidth[i] = node.Arg1;
                    break;

                case DocKind.Space:
                    _flatWidth[i] = (SpaceKind)node.Arg0 == SpaceKind.Forbidden ? 0 : 1;
                    break;

                case DocKind.Line:
                    _flatWidth[i] = (LineKind)node.Arg0 switch {
                        // A soft line is a space when its group is flat.
                        LineKind.Soft => 1,
                        LineKind.Preserve => node.Arg1 > 0 ? Unbounded : 1,
                        _ => Unbounded
                    };
                    break;

                case DocKind.Anchor:
                    _flatWidth[i] = 0;
                    break;

                case DocKind.IfBroken:
                    // Flat width is the Else branch's, since a flat owner takes it.
                    _flatWidth[i] = node.Count > 1 ? _flatWidth[_document.Children[node.Payload + 1]] : 0;
                    break;

                default:
                    var total = 0;
                    var children = _document.ChildrenOf(i);
                    for (var c = 0; c < children.Length; c++) {
                        total += _flatWidth[children[c]];
                        if (total >= Unbounded) {
                            total = Unbounded;
                            break;
                        }
                    }

                    _flatWidth[i] = total;
                    if (node.Kind == DocKind.Group) {
                        _sourceBroken[node.Arg1] = ContainsSourceBreak(i);
                    }

                    break;
            }
        }
    }

    bool ContainsSourceBreak(int node) {
        var children = _document.ChildrenOf(node);
        for (var c = 0; c < children.Length; c++) {
            ref var child = ref _document.Nodes[children[c]];
            if (child.Kind == DocKind.Line && (LineKind)child.Arg0 is LineKind.Preserve or LineKind.Hard or LineKind.Blank && child.Arg1 > 0) {
                return true;
            }

            if (child.Kind is DocKind.Concat or DocKind.Indent or DocKind.Fill && ContainsSourceBreak(children[c])) {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Depth-first, maintaining a column. A second bounded pass re-resolves owner-dependent groups;
    /// a group may only move Flat → Broken, never back, which is what guarantees termination.
    /// </summary>
    void ResolveGroups(int width) {
        // Iterative, threading one running column through the walk. Recursion is not an option:
        // real files nest thirty deep and C# has no tail calls.
        var stack = new Stack<(int Node, int Child)>();
        stack.Push((_document.Root, 0));
        var column = 0;

        while (stack.Count > 0) {
            var (node, child) = stack.Pop();
            ref var slot = ref _document.Nodes[node];

            if (child == 0) {
                switch (slot.Kind) {
                    case DocKind.Text:
                    case DocKind.Verbatim:
                    case DocKind.Space:
                        column += _flatWidth[node];
                        continue;

                    case DocKind.Anchor:
                        continue;

                    case DocKind.Line:
                        column = 0;
                        continue;

                    case DocKind.Group:
                        var id = slot.Arg1;
                        _modes[id] = (GroupMode)slot.Arg0 switch {
                            GroupMode.Flat => ResolvedMode.Flat,
                            GroupMode.Break => ResolvedMode.Broken,
                            GroupMode.Auto => column + _flatWidth[node] <= width ? ResolvedMode.Flat : ResolvedMode.Broken,
                            // ⚠ Preserve does not re-flow the author's breaks away. It keeps them and
                            // only adds more where a line still does not fit (docs/plan/04).
                            _ => _sourceBroken[id] || column + _flatWidth[node] > width
                                ? ResolvedMode.Broken
                                : ResolvedMode.Flat
                        };

                        break;

                    default:
                        break;
                }
            }

            var children = _document.ChildrenOf(node);
            if (slot.Kind == DocKind.IfBroken) {
                // Both branches are visited by the writer; the fitter only needs the one that will
                // be emitted, and the owner is already resolved because it encloses this node.
                var branch = _modes[slot.Arg0] == ResolvedMode.Broken ? 0 : 1;
                if (child == 0 && branch < children.Length) {
                    stack.Push((children[branch], 0));
                }

                continue;
            }

            if (child < children.Length) {
                stack.Push((node, child + 1));
                stack.Push((children[child], 0));
            }
        }
    }
}
