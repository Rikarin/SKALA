using System.Text;

// CA1711: a [Flags] enum named *Flags is what every reader expects it to be called.
#pragma warning disable CA1711

namespace Rikarin.Skala.Formatting;

/// <summary>
///     Where one input piece landed in the output. The sync points of docs/plan/04 § "Emitting minimal edits".
/// </summary>
public readonly record struct AnchorPoint(SourceSpan Source, int OutputStart, int OutputEnd, int TokenId);

/// <summary>The result of writing a resolved document.</summary>
/// <param name="OwnerUnresolved">
///     ⚠ How many <see cref="GroupMode.Owner" /> groups were reached before their owner. Zero for every
///     document the C# front end produces; see <see cref="Fitter.OwnerUnresolved" />.
/// </param>
public sealed record Layout(
    string Text,
    IReadOnlyList<AnchorPoint> Anchors,
    IReadOnlyList<ResolvedMode>? Modes = null,
    int OwnerUnresolved = 0);

/// <summary>Flags a <see cref="DocKind.Verbatim" /> node carries in <see cref="DocNode.Arg0" />.</summary>
[Flags]
public enum VerbatimFlags {
    None = 0,

    /// <summary>
    ///     The text sets its own indentation and must start at column 0.
    ///     <c>indent_preprocessor_if = no_indent</c>, and disabled <c>#if</c> text, which is never reindented.
    /// </summary>
    AtColumnZero = 1,

    /// <summary>The text already carries its own leading indentation; write it as-is at the line start.</summary>
    SelfIndented = 2,

    /// <summary>
    ///     A multi-line raw string literal: its interior lines and its closing delimiter move with the
    ///     opening one. <c>indent_raw_literal_string = align</c>.
    /// </summary>
    /// <remarks>
    ///     ⚠ The one re-indentation in the formatter that could change a string's value, and the reason
    ///     it does not is that it is a <em>uniform shift</em>. C# strips the closing delimiter's own
    ///     whitespace prefix from every line of a raw literal, so moving every interior line and the
    ///     closing delimiter by the same number of columns leaves the stripped result identical —
    ///     character for character, and the token-equivalence check would abort the file if it did not.
    ///     Re-indenting the lines independently, or moving the content without the delimiter, changes
    ///     what the program prints.
    /// </remarks>
    Realign = 4
}

/// <summary>
///     Walks a resolved document and produces the output text plus the anchor map.
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
    readonly int _width;

    LayoutWriter(Document document, int width, string indentUnit, string defaultNewLine, int continuousMultiplier) {
        _document = document;
        _width = width;
        _indentWidth = indentUnit == "\t" ? TextWidth.TabStop : indentUnit.Length;
        _fitter = new Fitter(document, width, _indentWidth);
        _indentUnit = indentUnit;
        _defaultNewLine = defaultNewLine;
        _continuousMultiplier = Math.Max(1, continuousMultiplier);
    }

    /// <param name="width"><c>max_line_length</c>: the budget every Auto group is tested against.</param>
    /// <param name="continuousMultiplier">
    ///     <c>continuous_indent_multiplier</c>: how many indent units one continuation level is worth.
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
                        Push((IndentKind)slot.Arg0, slot.Arg1 != 0);
                        break;

                    case DocKind.Group:
                        // ⚠ Resolved here, at the column the group's first character will actually
                        // land on, and against the rest of the line as well as its own width. See
                        // Fitter's remarks for why this is not a separate pass, and TrailingWidth
                        // for why the group's own width is not the whole measurement.
                        _fitter.Enter(node, CurrentColumn(), ContinuationColumn(slot.Arg1), TrailingWidth(stack));
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
    ///     Opens an indentation scope, recording the line it opened on.
    /// </summary>
    /// <remarks>
    ///     ⚠ The line matters. A scope contributes nothing to content that begins on its own opening
    ///     line, which is what makes
    ///     <code>
    /// M(
    ///     arg,
    ///     new Handler(() =&gt; {
    ///         Body();      ← one level from the lambda's line, not two
    ///     })
    /// );
    ///     </code>
    ///     come out the way ReSharper writes it. A block additionally <em>fixes</em> its level rather
    ///     than adding to whatever is open, because a brace resets the continuation context.
    /// </remarks>
    void Push(IndentKind kind, bool unconditional) {
        // ⚠ The closing delimiter goes back to the level the scope was opened AT, not to the level
        // of the physical line the opener happened to land on. The two differ whenever a condition
        // or an initializer pushed the opener rightwards:
        // <code>
        // if (first
        //     &amp;&amp; second) {
        //     Body();
        // }               ← the `if`'s level, not the `&amp;&amp; second` line's
        // </code>
        var outer = LevelForNested();
        _scopes.Add(
            kind switch {
                IndentKind.Block => new Scope(true, outer + _indentWidth, _line, outer, unconditional),
                IndentKind.Continuous =>
                    new Scope(false, _continuousMultiplier * _indentWidth, _line, outer, unconditional),
                IndentKind.Outdent =>
                    new Scope(true, Math.Max(0, outer - _indentWidth), _line, outer, unconditional),

                // ⚠ `align_multiline_statement_conditions = true`: an absolute column rather than a
                // level, captured where the scope opens — which is immediately after the condition's
                // `(`, so it is the column the writer is at. It is a Block scope in every other
                // respect, because "absolute, and nothing below it applies" is exactly what a block
                // already means; the only thing alignment adds is that the number is not a multiple
                // of the indent width.
                IndentKind.Align => new Scope(true, CurrentColumn(), _line, outer, unconditional),
                _ => new Scope(false, 0, int.MaxValue, outer, unconditional)
            }
        );
    }

    /// <summary>
    ///     Closes a scope, and remembers where the line that opened it began.
    /// </summary>
    /// <remarks>
    ///     ⚠ A closing delimiter takes the indentation of the line its opener was on, not of the line
    ///     the stack happens to be at:
    ///     <code>
    /// M(
    ///     new Handler(() =&gt; {
    ///         Body();
    ///     })      ← the lambda's opening line, two scopes below where the stack now stands
    /// );          ← M's opening line
    ///     </code>
    /// </remarks>
    void Pop(bool alignsCloser) {
        if (alignsCloser) {
            _pendingCloserLevel = _scopes[^1].CloserLevel;
        }

        _scopes.RemoveAt(_scopes.Count - 1);
    }

    /// <summary>
    ///     The level a scope opening now nests from, and the level its closing delimiter takes.
    /// </summary>
    /// <remarks>
    ///     ⚠ Not <see cref="Effective" />, and the difference is one scope. Effective answers "what
    ///     level does a line starting now take" and therefore ignores everything opened on the current
    ///     line; a scope opening now is opening <em>inside</em> those, so an unconditional one counts.
    ///     <code>
    /// messages.Any(message => message.Contains(
    ///         "…"
    ///     )        ← Contains' closer, at Any's level, not at the statement's
    /// );
    ///     </code>
    /// </remarks>
    int LevelForNested() => Level(nested: true);

    /// <summary>
    ///     The indent level for a line starting now.
    /// </summary>
    /// <remarks>
    ///     ⚠ One level per opening <em>line</em>, not per scope. Two groups opened on the same line
    ///     are one indentation step:
    ///     <code>
    /// context.Report(Diagnostic.Create(
    ///     descriptor,      ← one level, though two parentheses are open
    ///     location));
    ///     </code>
    /// </remarks>
    int Effective() => Level(nested: false);

    /// <summary>
    ///     Walks the scope stack and adds up the levels that apply.
    /// </summary>
    /// <param name="nested">
    ///     True for a scope opening now rather than a line starting now: an unconditional scope opened
    ///     earlier on <em>this</em> line is one the new scope is nesting inside.
    /// </param>
    /// <remarks>
    ///     ⚠ Two rules, and the second is milestone 3's correction to the first.
    ///     <list type="number">
    ///         <item>
    ///             A <b>delimited</b> scope — a parenthesis, a bracket — always spends its level. Verified
    ///             against the oracle: an operand broken onto its own line inside <c>if ((… == …))</c> lands two
    ///             levels in, one for each parenthesis, although both opened on the same line.
    ///         </item>
    ///         <item>
    ///             An <b>undelimited continuation</b> — the level a group spends for its own break points,
    ///             docs/plan/04's second row — spends at most one level per line, and none at all on a line
    ///             where a delimited scope inside it already spent one. That second clause is what keeps
    ///             <c>using var d = Drawn(</c> with its arguments under it at one level: the <c>=</c> would
    ///             otherwise pay for a continuation the parenthesis is already paying for. Dropping either half costs
    ///             1.9 points of
    ///             line fidelity on <c>corpus/real/</c>, in opposite directions.
    ///         </item>
    ///     </list>
    ///     ⚠ The single <c>blocked</c> variable is enough because scopes are visited innermost-first and
    ///     an outer scope never opened on a later line than an inner one.
    /// </remarks>
    int Level(bool nested) {
        var level = 0;
        var blocked = -1;
        for (var i = _scopes.Count - 1; i >= 0; i--) {
            var scope = _scopes[i];
            if (scope.IsBlock) {
                return level + scope.Level;
            }

            if (scope.Unconditional) {
                if (nested ? scope.OpenLine <= _line : scope.OpenLine < _line) {
                    level += scope.Level;
                    blocked = scope.OpenLine;
                }

                continue;
            }

            if (scope.OpenLine < _line && scope.OpenLine != blocked) {
                level += scope.Level;
                blocked = scope.OpenLine;
            }
        }

        return level;
    }

    /// <param name="Unconditional">
    ///     ⚠ The scope counts even when another scope opened on the same line. One level per opening
    ///     <em>line</em> is the general rule and it is right —
    ///     <c>context.Report(Diagnostic.Create(\n    descriptor,</c> takes one level, not two, and
    ///     removing the collapse costs 1.7 points of line fidelity on <c>corpus/real/</c>. The
    ///     exception is the parenthesis of a call whose sole argument is a lambda, which
    ///     <c>place_single_method_argument_lambda_on_same_line = true</c> keeps on the call's line:
    ///     <code>
    /// messages.Any(message => message.Contains(
    ///         "…"          ← two levels, from `Any(` and from `Contains(`
    ///     )                ← one, back to `Contains(`'s opener
    /// );
    ///     </code>
    ///     The lambda is not a break the layout chose, so the parenthesis it hides behind still spends
    ///     its level. docs/plan/05 § "place_* and if_owner_is_single_line" records the closing half of
    ///     the same rule.
    /// </param>
    /// <param name="Level">
    ///     ⚠ A <em>column</em>, not a level count, and it has been one since milestone 3.1. Alignment
    ///     puts a line at a column that is not a multiple of the indent width — the column just after a
    ///     statement's condition `(` — so a stack of levels cannot express it and a stack of columns
    ///     can express both.
    /// </param>
    readonly record struct Scope(bool IsBlock, int Level, int OpenLine, int CloserLevel, bool Unconditional = false);

    /// <summary>Writes the indentation that reaches <paramref name="column" />.</summary>
    /// <remarks>
    ///     ⚠ Whole indent units first and spaces for the remainder, which is what
    ///     `alignment_tab_fill_style = use_spaces` asks for and is also the only thing that can be right
    ///     when the unit is a tab: a column of 25 is six tabs and a space, never twenty-five tabs.
    /// </remarks>
    void WriteIndentTo(int column) {
        var units = column / _indentWidth;
        for (var i = 0; i < units; i++) {
            _output.Append(_indentUnit);
        }

        _column = units * _indentWidth;
        for (var i = _column; i < column; i++) {
            _output.Append(' ');
            _column++;
        }
    }

    /// <summary>
    ///     The column the next character will land on, which is what a group is measured against.
    /// </summary>
    /// <remarks>
    ///     ⚠ At a line start the indentation has not been written yet, so <c>_column</c> is 0 and the
    ///     group would look as though it had the whole line. A group at the head of a line 24 columns
    ///     deep has 96, and measuring it against 120 is how a formatter produces a wrap that is one
    ///     level too optimistic on every nested construct in a file.
    /// </remarks>
    int CurrentColumn() {
        if (_atLineStart) {
            return _pendingCloserLevel ?? Effective();
        }

        return _column + (_pendingSpace ? 1 : 0);
    }

    /// <summary>
    ///     Shifts a multi-line raw string literal so that its closing delimiter lands at
    ///     <paramref name="column" />.
    /// </summary>
    /// <remarks>
    ///     ⚠ <c>indent_raw_literal_string = align</c> aligns to the column of the opening quotes, which
    ///     is where this token starts. Established against the oracle, including the detail that an
    ///     interpolated opener aligns to its quotes and not to its dollar sign — the content lands one
    ///     column further right than for a plain literal in the same place.
    ///     <para>
    ///         ⚠ Whitespace-only lines are left exactly as they are. C# treats a line whose whitespace is
    ///         shorter than the closing delimiter's as empty rather than as an error, so shifting one is
    ///         both unnecessary and, when the shift is negative, impossible.
    ///     </para>
    /// </remarks>
    static string Realign(string text, int column) {
        var lines = text.Split('\n');
        if (lines.Length < 2) {
            return text;
        }

        var closer = lines[^1];
        var current = 0;
        while (current < closer.Length && closer[current] is ' ' or '\t') {
            current++;
        }

        var shift = column - current;
        if (shift == 0) {
            return text;
        }

        var builder = new StringBuilder(text.Length + (Math.Abs(shift) + 1) * lines.Length);
        builder.Append(lines[0]);
        for (var i = 1; i < lines.Length; i++) {
            builder.Append('\n');
            var line = lines[i];
            var body = line.EndsWith('\r') ? line[..^1] : line;
            if (body.AsSpan().TrimStart(" \t").IsEmpty) {
                builder.Append(line);
                continue;
            }

            if (shift > 0) {
                builder.Append(' ', shift).Append(line);
                continue;
            }

            var removable = 0;
            while (removable < -shift && removable < body.Length && body[removable] is ' ' or '\t') {
                removable++;
            }

            builder.Append(line, removable, line.Length - removable);
        }

        return builder.ToString();
    }

    /// <summary>
    ///     How much of the current line is still to come after this node, up to the next break.
    /// </summary>
    /// <remarks>
    ///     ⚠ A group's own width is not the length of the line it lands on, and milestone 3 found that
    ///     out on <c>var f = new Thing { A = 1, B = 2, C = 3 };</c> at 121 columns. The initializer's
    ///     group covers <c>{ … }</c> and stops there: it is entered at column 26, measures 94, concludes
    ///     120 and stays flat — and then the semicolon that is not in it makes the line 121. The oracle
    ///     wraps it. Every construct that ends before its statement does has the same blind spot, so the
    ///     error is not rare: closing parentheses, semicolons, commas and closing braces are exactly what
    ///     follows the constructs that wrap.
    ///     <para>
    ///         The answer is Prettier's <c>fits(next, restCommands)</c>: measure the group plus whatever
    ///         remains of the line. The walk's own stack already holds it — every ancestor frame names the
    ///         sibling the walk will return to — so this is a read of state that exists rather than a second
    ///         traversal, and it stops at the first break point, which is normally one or two nodes away.
    ///     </para>
    /// </remarks>
    int TrailingWidth(Stack<(int Node, int Child)> stack) {
        var total = 0;
        foreach (var (node, child) in stack) {
            var children = _document.ChildrenOf(node);
            for (var i = child; i < children.Length; i++) {
                var sibling = children[i];

                // ⚠ A break point's own flat rendering does not count. The measure is "the rest of
                // this line if every break point is taken", and if this one is taken the line ends
                // here — the space it would have rendered as is never written. Counting it made this
                // measure one column larger than the one a fill point uses on the same gap, and the
                // two disagreeing is a non-idempotency rather than a rounding error: the fill keeps
                // an item on the line, the item's own group then finds itself one column over and
                // breaks, and the second pass sees a multi-line item and breaks before it. Two files
                // out of Vixen's 4 708 did exactly that.
                if (_document.Nodes[sibling].Kind == DocKind.Line) {
                    return total;
                }

                var width = _document.PointWidthOf(sibling);
                total = total >= Document.Unbounded || width >= Document.Unbounded
                    ? Document.Unbounded
                    : total + width;

                if (_document.HasBreak(sibling)) {
                    return total;
                }
            }
        }

        return total;
    }

    /// <summary>
    ///     The column a line broken at one of this group's own points would start at.
    /// </summary>
    /// <remarks>
    ///     ⚠ Not <see cref="Effective" />. That function answers "what level does a line starting
    ///     <em>now</em> take", and it deliberately ignores a scope opened on the current line — one
    ///     level per opening line is the rule. A break inside such a scope lands on the <em>next</em>
    ///     line, where the scope does count, so the two answers differ by exactly one level on every
    ///     construct whose delimiter opened on this line, which is most of them.
    ///     <para>
    ///         The group's own continuation scope, when it has one, is not on the stack yet: the writer
    ///         resolves the group on entry and the <see cref="DocKind.Indent" /> node is its first child.
    ///         <see cref="GroupFacts.SpendsIndent" /> is how the front end says so.
    ///     </para>
    /// </remarks>
    int ContinuationColumn(int group) {
        var level = 0;
        var counted = -1;
        for (var i = _scopes.Count - 1; i >= 0; i--) {
            var scope = _scopes[i];
            if (scope.IsBlock) {
                level += scope.Level;
                break;
            }

            if (scope.OpenLine <= _line && scope.OpenLine != counted) {
                level += scope.Level;
                counted = scope.OpenLine;
            }
        }

        if (_document.FactsOf(group).SpendsIndent) {
            level += _continuousMultiplier * _indentWidth;
        }

        return level;
    }

    void WriteLine(ref DocNode slot, int node) {
        var kind = (LineKind)slot.Arg0;
        if (kind == LineKind.Soft) {
            var flags = (LineFlags)slot.Flags;

            // ⚠ A break point renders as its flat form when its group stayed flat, and the flat
            // form is per-point: nothing after `(`, a space after `,`.
            var flat = slot.Arg2 < 0 || _fitter.ModeOf(slot.Arg2) == ResolvedMode.Flat;

            // ⚠ A fill point in a broken group is the one break decision that is not the group's.
            // It breaks when the next item would not fit and stays put otherwise, which is what
            // makes `wrap_if_long` a fill rather than a chop.
            if (!flat && (flags & LineFlags.FillPoint) != 0) {
                var space = _pendingSpace || (flags & LineFlags.FlatSpace) != 0;
                var column = _atLineStart
                    ? _pendingCloserLevel ?? Effective()
                    : _column + (space ? 1 : 0);
                var segment = _document.SegmentOf(node);
                flat = segment < Document.Unbounded && column + segment <= _width;
            }

            if (flat) {
                if ((flags & LineFlags.FlatSpace) != 0) {
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
    }

    void WritePiece(string text, SourceSpan source, VerbatimFlags flags) {
        if ((flags & VerbatimFlags.Realign) != 0) {
            text = Realign(
                text,
                _atLineStart ? _pendingCloserLevel ?? Effective() : _column + (_pendingSpace ? 1 : 0)
            );
        }

        if (_atLineStart) {
            if ((flags & VerbatimFlags.AtColumnZero) == 0 && (flags & VerbatimFlags.SelfIndented) == 0) {
                WriteIndentTo(_pendingCloserLevel ?? Effective());
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
