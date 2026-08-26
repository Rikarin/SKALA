using System.Text;

// CA1711: a [Flags] enum named *Flags is what every reader expects it to be called.
#pragma warning disable CA1711

namespace Rikarin.Skala.Formatting;

/// <summary>Where one input piece landed in the output. The sync points of docs/plan/04 § "Emitting minimal edits".</summary>
public readonly record struct AnchorPoint(SourceSpan Source, int OutputStart, int OutputEnd, int TokenId);

/// <summary>The result of writing a resolved document.</summary>
/// <param name="OwnerUnresolved">
/// ⚠ How many <see cref="GroupMode.Owner"/> groups were reached before their owner. Zero for every
/// document the C# front end produces; see <see cref="Fitter.OwnerUnresolved"/>.
/// </param>
public sealed record Layout(
    string Text,
    IReadOnlyList<AnchorPoint> Anchors,
    IReadOnlyList<ResolvedMode>? Modes = null,
    int OwnerUnresolved = 0);

/// <summary>Flags a <see cref="DocKind.Verbatim"/> node carries in <see cref="DocNode.Arg0"/>.</summary>
[Flags]
public enum VerbatimFlags {
    None = 0,

    /// <summary>
    /// The text sets its own indentation and must start at column 0.
    /// <c>indent_preprocessor_if = no_indent</c>, and disabled <c>#if</c> text, which is never reindented.
    /// </summary>
    AtColumnZero = 1,

    /// <summary>The text already carries its own leading indentation; write it as-is at the line start.</summary>
    SelfIndented = 2
}

/// <summary>
/// Walks a resolved document and produces the output text plus the anchor map.
/// </summary>
public sealed class LayoutWriter {
    readonly Document _document;
    readonly Fitter _fitter;
    readonly StringBuilder _output = new();
    readonly List<AnchorPoint> _anchors = [];
    readonly string _indentUnit;
    readonly string _defaultNewLine;
    readonly List<Scope> _scopes = [];

    int _column;
    int _line;
    int? _pendingCloserLevel;
    bool _atLineStart = true;
    bool _pendingSpace;
    SourceSpan _pendingAnchorSpan;
    int _pendingAnchorToken = -1;
    bool _hasPendingAnchor;

    readonly int _continuousMultiplier;
    readonly int _indentWidth;

    LayoutWriter(Document document, int width, string indentUnit, string defaultNewLine, int continuousMultiplier) {
        _document = document;
        _fitter = new Fitter(document, width);
        _indentUnit = indentUnit;
        _defaultNewLine = defaultNewLine;
        _continuousMultiplier = Math.Max(1, continuousMultiplier);
        _indentWidth = indentUnit == "\t" ? TextWidth.TabStop : indentUnit.Length;
    }

    /// <param name="width"><c>max_line_length</c>: the budget every Auto group is tested against.</param>
    /// <param name="continuousMultiplier">
    /// <c>continuous_indent_multiplier</c>: how many indent units one continuation level is worth.
    /// </param>
    public static Layout Write(
        Document document,
        int width,
        string indentUnit,
        string defaultNewLine,
        int continuousMultiplier = 1
    ) {
        var writer = new LayoutWriter(document, width, indentUnit, defaultNewLine, continuousMultiplier);
        writer.Walk();
        return new Layout(
            writer._output.ToString(),
            writer._anchors,
            writer._fitter.Modes,
            writer._fitter.OwnerUnresolved
        );
    }

    void Walk() {
        var stack = new Stack<(int Node, int Child)>();
        stack.Push((_document.Root, 0));

        while (stack.Count > 0) {
            var (node, child) = stack.Pop();
            ref var slot = ref _document.Nodes[node];

            if (child == 0) {
                switch (slot.Kind) {
                    case DocKind.Text:
                        WritePiece(_document.TextOf(node), slot.Source, (VerbatimFlags)slot.Flags);
                        continue;

                    case DocKind.Verbatim:
                        WritePiece(_document.TextOf(node), slot.Source, (VerbatimFlags)slot.Flags);
                        continue;

                    case DocKind.Anchor:
                        _pendingAnchorSpan = slot.Source;
                        _pendingAnchorToken = slot.Arg0;
                        _hasPendingAnchor = true;
                        continue;

                    case DocKind.Space:
                        if ((SpaceKind)slot.Arg0 != SpaceKind.Forbidden) {
                            _pendingSpace = true;
                        }

                        continue;

                    case DocKind.Line:
                        WriteLine(ref slot, node);
                        continue;

                    case DocKind.Indent:
                        Push((IndentKind)slot.Arg0);
                        break;

                    case DocKind.Group:
                        // ⚠ Resolved here, at the column the group's first character will actually
                        // land on. See Fitter's remarks for why this is not a separate pass.
                        _fitter.Enter(node, CurrentColumn());
                        break;

                    default:
                        break;
                }
            }

            var children = _document.ChildrenOf(node);

            if (slot.Kind == DocKind.IfBroken) {
                var branch = _fitter.ModeOf(slot.Arg0) == ResolvedMode.Broken ? 0 : 1;
                if (child == 0 && branch < children.Length) {
                    stack.Push((children[branch], 0));
                }

                continue;
            }

            if (child < children.Length) {
                stack.Push((node, child + 1));
                stack.Push((children[child], 0));
                continue;
            }

            if (slot.Kind == DocKind.Indent) {
                Pop(slot.Flags != 0);
            }
        }
    }

    /// <summary>
    /// Opens an indentation scope, recording the line it opened on.
    /// </summary>
    /// <remarks>
    /// ⚠ The line matters. A scope contributes nothing to content that begins on its own opening
    /// line, which is what makes
    /// <code>
    /// M(
    ///     arg,
    ///     new Handler(() =&gt; {
    ///         Body();      ← one level from the lambda's line, not two
    ///     })
    /// );
    /// </code>
    /// come out the way ReSharper writes it. A block additionally <em>fixes</em> its level rather
    /// than adding to whatever is open, because a brace resets the continuation context.
    /// </remarks>
    void Push(IndentKind kind) {
        // ⚠ The closing delimiter goes back to the level the scope was opened AT, not to the level
        // of the physical line the opener happened to land on. The two differ whenever a condition
        // or an initializer pushed the opener rightwards:
        // <code>
        // if (first
        //     &amp;&amp; second) {
        //     Body();
        // }               ← the `if`'s level, not the `&amp;&amp; second` line's
        // </code>
        var outer = Effective();
        _scopes.Add(
            kind switch {
                IndentKind.Block => new Scope(true, outer + 1, _line, outer),
                IndentKind.Continuous => new Scope(false, _continuousMultiplier, _line, outer),
                IndentKind.Outdent => new Scope(true, Math.Max(0, outer - 1), _line, outer),
                _ => new Scope(false, 0, int.MaxValue, outer)
            }
        );
    }

    /// <summary>
    /// Closes a scope, and remembers where the line that opened it began.
    /// </summary>
    /// <remarks>
    /// ⚠ A closing delimiter takes the indentation of the line its opener was on, not of the line
    /// the stack happens to be at:
    /// <code>
    /// M(
    ///     new Handler(() =&gt; {
    ///         Body();
    ///     })      ← the lambda's opening line, two scopes below where the stack now stands
    /// );          ← M's opening line
    /// </code>
    /// </remarks>
    void Pop(bool alignsCloser) {
        if (alignsCloser) {
            _pendingCloserLevel = _scopes[^1].CloserLevel;
        }

        _scopes.RemoveAt(_scopes.Count - 1);
    }

    /// <summary>
    /// The indent level for a line starting now.
    /// </summary>
    /// <remarks>
    /// ⚠ One level per opening <em>line</em>, not per scope. Two groups opened on the same line
    /// are one indentation step:
    /// <code>
    /// context.Report(Diagnostic.Create(
    ///     descriptor,      ← one level, though two parentheses are open
    ///     location));
    /// </code>
    /// </remarks>
    int Effective() {
        var level = 0;
        var counted = -1;
        for (var i = _scopes.Count - 1; i >= 0; i--) {
            var scope = _scopes[i];
            if (scope.IsBlock) {
                return level + scope.Level;
            }

            if (scope.OpenLine < _line && scope.OpenLine != counted) {
                level += scope.Level;
                counted = scope.OpenLine;
            }
        }

        return level;
    }

    readonly record struct Scope(bool IsBlock, int Level, int OpenLine, int CloserLevel);

    /// <summary>
    /// The column the next character will land on, which is what a group is measured against.
    /// </summary>
    /// <remarks>
    /// ⚠ At a line start the indentation has not been written yet, so <c>_column</c> is 0 and the
    /// group would look as though it had the whole line. A group at the head of a line 24 columns
    /// deep has 96, and measuring it against 120 is how a formatter produces a wrap that is one
    /// level too optimistic on every nested construct in a file.
    /// </remarks>
    int CurrentColumn() {
        if (_atLineStart) {
            return (_pendingCloserLevel ?? Effective()) * _indentWidth;
        }

        return _column + (_pendingSpace ? 1 : 0);
    }

    void WriteLine(ref DocNode slot, int node) {
        var kind = (LineKind)slot.Arg0;
        if (kind == LineKind.Soft) {
            // ⚠ A break point renders as its flat form when its group stayed flat, and the flat
            // form is per-point: nothing after `(`, a space after `,`.
            if (slot.Arg2 < 0 || _fitter.ModeOf(slot.Arg2) == ResolvedMode.Flat) {
                if (((LineFlags)slot.Flags & LineFlags.FlatSpace) != 0) {
                    _pendingSpace = true;
                }

                return;
            }
        }

        // ⚠ remove_spaces_on_blank_lines = true: a pending space before a break is never written,
        // which is also what keeps the formatter from producing trailing whitespace at all.
        _pendingSpace = false;
        var newLine = slot.Payload > 0 ? _document.Strings[slot.Payload] : _defaultNewLine;
        _output.Append(newLine);
        _line++;
        for (var i = 0; i < slot.Arg1; i++) {
            _output.Append(newLine);
            _line++;
        }

        _atLineStart = true;
        _column = 0;
        _ = node;
    }

    void WritePiece(string text, SourceSpan source, VerbatimFlags flags) {
        if (_atLineStart) {
            if ((flags & VerbatimFlags.AtColumnZero) == 0 && (flags & VerbatimFlags.SelfIndented) == 0) {
                var level = _pendingCloserLevel ?? Effective();
                for (var i = 0; i < level; i++) {
                    _output.Append(_indentUnit);
                    _column += _indentUnit.Length;
                }
            }

            _atLineStart = false;
            _pendingSpace = false;
            _pendingCloserLevel = null;
        } else if (_pendingCloserLevel is not null) {
            _pendingCloserLevel = null;
            if (_pendingSpace) {
                _output.Append(' ');
                _column++;
                _pendingSpace = false;
            }
        } else if (_pendingSpace) {
            _output.Append(' ');
            _column++;
            _pendingSpace = false;
        }

        var start = _output.Length;
        _output.Append(text);
        _column = TextWidth.Advance(text, _column);

        if (_hasPendingAnchor) {
            _anchors.Add(new AnchorPoint(_pendingAnchorSpan, start, _output.Length, _pendingAnchorToken));
            _hasPendingAnchor = false;
        }

        // A piece that ends with a newline (a multi-line comment written verbatim never does, but a
        // disabled #if block does) leaves the writer at the start of a line.
        if (text.Length > 0 && (text[^1] == '\n' || text[^1] == '\r')) {
            _atLineStart = true;
            _column = 0;
        }

        // A multi-line piece — a raw string, a disabled block — moves the line counter with it, so
        // that scopes opened before it still know which side of a break they are on.
        foreach (var c in text) {
            if (c == '\n') {
                _line++;
            }
        }
    }
}
