using Rikarin.Skala.Options;
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
    Realign = 4,

    /// <summary>
    ///     The same uniform shift as <see cref="Realign" />, to a different target:
    ///     <c>indent_raw_literal_string = indent</c> puts the literal's closing delimiter one indent
    ///     level in from its opening line, wherever the opening quotes happen to sit on that line.
    /// </summary>
    /// <remarks>
    ///     ⚠ The two are alternatives and never both. <c>align</c> targets the column of the opening
    ///     quotes and <c>indent</c> targets the opening LINE's indentation plus one level, which is why
    ///     `var a = """` at eight columns puts its content at sixteen under one and twelve under the
    ///     other. The safety argument is <see cref="Realign" />'s entire — a uniform shift leaves the
    ///     stripped value character for character identical — and nothing about it depends on which
    ///     column is the target.
    /// </remarks>
    RealignToIndent = 8,

    /// <summary>
    ///     A starred block comment: every continuation line is re-indented to the opening
    ///     <c>/*</c>'s column plus one. <c>align_multiline_comments = true</c>.
    /// </summary>
    /// <remarks>
    ///     ⚠ The one flag here that is <em>not</em> a uniform shift, and so the one whose safety argument
    ///     is different. <see cref="Realign" /> may move a string literal because moving every line by the
    ///     same amount leaves the value identical; this moves lines by different amounts, and it is safe
    ///     for the unrelated reason that a comment has no value. What it must not do is change the token
    ///     stream, which is why the caller only sets it on a comment whose every continuation line already
    ///     begins with <c>*</c> — see <c>CSharpDocumentBuilder.IsStarredBlockComment</c>.
    /// </remarks>
    AlignStarred = 16,

    /// <summary>
    ///     A multi-line block comment: every continuation line moves by as many columns as the
    ///     <em>line the comment starts on</em> moved, clamped at column 0. The source line's indentation
    ///     is the node's <see cref="DocNode.Arg2" /> string. SK-DIV-0094, issue #428.
    /// </summary>
    /// <remarks>
    ///     ⚠ The delta is the line's indentation and not the opener's column. Measured against
    ///     <c>jb cleanupcode</c> 2025.2.6: <c>int   y = /* gap</c> loses two columns before its <c>/*</c>
    ///     and its continuation does not move, while <c>var x = 1 + /* expr</c> broken before the
    ///     <c>+</c> moves its continuation by the two columns the new line is indented further — not by
    ///     the eight its <c>/*</c> moved left.
    ///     <para>
    ///         Safe for <see cref="AlignStarred" />'s reason: a comment has no value. Only a line's
    ///         leading whitespace changes, which is exactly what <c>TokenEquivalence</c> collapses when it
    ///         compares comment text, so anything else going wrong here is still an SK9099.
    ///     </para>
    /// </remarks>
    ShiftWithLine = 32
}

/// <summary>
///     Walks a resolved document and produces the output text plus the anchor map.
/// </summary>
public sealed class LayoutWriter {
    readonly Document document;
    readonly Fitter fitter;
    readonly StringBuilder output = new();
    readonly List<AnchorPoint> anchors = [];
    readonly string indentUnit;
    readonly string defaultNewLine;
    readonly List<Scope> scopes = [];

    int column;
    int line;
    int? pendingCloserLevel;
    bool atLineStart = true;
    bool pendingSpace;

    /// <summary>The pending gap's own text, when it is preserved rather than rendered as one space.</summary>
    string? pendingSpaceText;

    /// <summary>Whether the break that ended the last line renders as a space when it does not break.</summary>
    bool createdLineSpace;

    SourceSpan pendingAnchorSpan;
    int pendingAnchorToken = -1;
    bool hasPendingAnchor;

    /// <summary>The chain group <see cref="ChainBreaksInside" /> is watching, or -1.</summary>
    int watchedChain = -1;

    /// <summary>Whether <see cref="watchedChain" /> has taken a point since the watch began.</summary>
    bool watchedChainBroke;

    /// <summary>
    ///     The groups the walk is inside that resolved broken and carry <see cref="GroupFacts.Continues" />,
    ///     innermost last, each with the depth of <see cref="scopes" /> and the line it was entered at.
    ///     See <see cref="BrokenInsideOnItsLine" />.
    /// </summary>
    readonly List<(int Group, int Depth, int Line)> brokenConstructs = [];


    readonly int continuousMultiplier;
    readonly int indentWidth;
    readonly int width;

    /// <summary><c>alignment_tab_fill_style</c>: how the whitespace reaching a column is spelled.</summary>
    /// <remarks>⚠ Read by <see cref="WriteIndentTo" /> and only there. SK-DIV-0032.</remarks>
    readonly TabFillStyle tabFill;

    /// <summary>
    ///     The input, and non-null exactly when <c>disable_indenter</c> is on.
    /// </summary>
    /// <remarks>
    ///     ⚠ The one thing that key needs and the writer otherwise never has. Suppressing indentation
    ///     is not "indent to zero": a line that existed in the input keeps the leading whitespace the
    ///     author wrote, which can only be read out of the input, and the null here is what says the
    ///     ordinary path is in force rather than a flag beside a string nobody passed.
    /// </remarks>
    readonly string? source;

    LayoutWriter(
        Document document,
        int width,
        string indentUnit,
        string defaultNewLine,
        int continuousMultiplier,
        string? suppressedIndentSource,
        TabFillStyle tabFill
    ) {
        this.document = document;
        this.width = width;
        indentWidth = indentUnit == "\t" ? TextWidth.TabStop : indentUnit.Length;
        fitter = new(document, width, indentWidth);
        this.indentUnit = indentUnit;
        this.defaultNewLine = defaultNewLine;
        this.continuousMultiplier = Math.Max(1, continuousMultiplier);
        source = suppressedIndentSource;
        this.tabFill = tabFill;
    }

    /// <param name="width"><c>max_line_length</c>: the budget every Auto group is tested against.</param>
    /// <param name="continuousMultiplier">
    ///     <c>continuous_indent_multiplier</c>: how many indent units one continuation level is worth.
    /// </param>
    /// <param name="suppressedIndentSource">
    ///     <c>disable_indenter</c>: the input text, passed only when the key is on. See
    ///     <see cref="WriteSuppressedIndent" />.
    /// </param>
    /// <param name="tabFill">
    ///     <c>alignment_tab_fill_style</c>: how the whitespace reaching an aligned column is spelled when
    ///     the indent unit is a tab. Defaults to the registry's own default, which is also the export's
    ///     value; it has no effect at all on a space-indented file. See <see cref="WriteIndentTo" />.
    /// </param>
    public static Layout Write(
        Document document,
        int width,
        string indentUnit,
        string defaultNewLine,
        int continuousMultiplier = 1,
        string? suppressedIndentSource = null,
        TabFillStyle tabFill = TabFillStyle.UseSpaces
    ) {
        var writer = new LayoutWriter(
            document,
            width,
            indentUnit,
            defaultNewLine,
            continuousMultiplier,
            suppressedIndentSource,
            tabFill
        );
        writer.Walk();
        return new(
            writer.output.ToString(),
            writer.anchors,
            writer.fitter.Modes,
            writer.fitter.OwnerUnresolved
        );
    }

    void Walk() {
        var stack = new Stack<(int Node, int Child)>();
        stack.Push((document.Root, 0));
        Run(stack, int.MaxValue);
    }

    /// <summary>
    ///     Writes the document from the frames on <paramref name="stack" /> until the stack is empty or
    ///     the writer has moved past output line <paramref name="untilLine" />.
    /// </summary>
    /// <remarks>
    ///     ⚠ The whole walk and a speculative one are the same loop. <see cref="NextLineFitsBeside" />
    ///     runs it on a copy of the frames to write one line ahead and then rolls the writer back, and
    ///     that is only sound because there is no second loop with its own reading of a frame: whatever
    ///     the real walk will do with the line, the speculative one did first, with the same code.
    /// </remarks>
    /// <param name="stack">The frames to write from; consumed.</param>
    /// <param name="untilLine">The last output line to write.</param>
    /// <param name="floor">
    ///     How many frames to leave on the stack: <see cref="ChainBreaksInside" /> writes one scope's
    ///     contents and stops when the scope closes.
    /// </param>
    void Run(Stack<(int Node, int Child)> stack, int untilLine, int floor = 0) {
        while (stack.Count > floor && line <= untilLine) {
            var (node, child) = stack.Pop();
            ref var slot = ref document.Nodes[node];

            if (child == 0) {
                switch (slot.Kind) {
                    case DocKind.Text: {
                        var startedOn = line;
                        WritePiece(document.TextOf(node), slot.Source, (VerbatimFlags)slot.Flags);
                        StartFilledElements(startedOn);
                        continue;
                    }

                    case DocKind.Verbatim:
                        WritePiece(
                            document.TextOf(node),
                            slot.Source,
                            (VerbatimFlags)slot.Flags,
                            slot.Arg2 >= 0 ? document.Strings[slot.Arg2] : null
                        );
                        continue;

                    case DocKind.Anchor:
                        pendingAnchorSpan = slot.Source;
                        pendingAnchorToken = slot.Arg0;
                        hasPendingAnchor = true;
                        continue;

                    case DocKind.Space:
                        if ((SpaceKind)slot.Arg0 != SpaceKind.Forbidden) {
                            pendingSpace = true;

                            // ⚠ A payload means the gap is preserved byte for byte rather than
                            // rendered as one space. `disable_space_changes` is the only producer.
                            pendingSpaceText = slot.Payload > 0 ? document.Strings[slot.Payload] : null;
                        }

                        continue;

                    case DocKind.Line:
                        WriteLine(ref slot, node, stack);
                        continue;

                    case DocKind.Indent:
                        if (((IndentFlags)slot.Arg1 & HeldConditions) != 0) {
                            Push(
                                HeldOrSpent(node, (IndentKind)slot.Arg0, (IndentFlags)slot.Arg1, slot.Arg2, stack),
                                IndentFlags.None,
                                0,
                                stack
                            );
                        } else {
                            Push((IndentKind)slot.Arg0, (IndentFlags)slot.Arg1, slot.Arg2, stack, node);
                        }

                        break;

                    case DocKind.Group:
                        // ⚠ Resolved here, at the column the group's first character will actually
                        // land on, and against the rest of the line as well as its own width. See
                        // Fitter's remarks for why this is not a separate pass, and TrailingWidth
                        // for why the group's own width is not the whole measurement.
                        fitter.Enter(
                            node,
                            CurrentColumn(),
                            ContinuationColumn(slot.Arg1),
                            TrailingWidth(stack),
                            line,
                            atLineStart ? CurrentColumn() : CurrentLineIndent()
                        );
                        if (fitter.ModeOf(slot.Arg1) == ResolvedMode.Broken && document.FactsOf(slot.Arg1).Continues) {
                            brokenConstructs.Add((slot.Arg1, scopes.Count, line));
                        }

                        break;

                    default:
                        // Concat, Fill and IfBroken are descended into below rather than written
                        // here — ChildrenOf covers the first two and IfBroken picks its branch —
                        // so this section is the catch-all that says so, not dead control flow.
                        break;
                }
            }

            var children = document.ChildrenOf(node);

            if (slot.Kind == DocKind.IfBroken) {
                var branch = fitter.ModeOf(slot.Arg0) == ResolvedMode.Broken ? 0 : 1;
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
            } else if (slot.Kind == DocKind.Group
                       && brokenConstructs.Count > 0
                       && brokenConstructs[^1].Group == slot.Arg1) {
                brokenConstructs.RemoveAt(brokenConstructs.Count - 1);
            }
        }
    }

    const IndentFlags HeldConditions = IndentFlags.HeldWhileOwnerFlat | IndentFlags.HeldWhileChainWhole;

    /// <summary>
    ///     The kind a conditionally held scope opens as: <see cref="IndentKind.None" /> — a held level —
    ///     while every condition it names holds, its own kind once one fails.
    /// </summary>
    /// <remarks>
    ///     ⚠ The owner's condition is already decided: the scope is the first child of the group that
    ///     owns it, and that group was resolved when the walk entered it (issue #406). The chain's is
    ///     not, so it is asked last and only when the owner's did not already spend the level; see
    ///     <see cref="ChainBreaksInside" /> (issue #407).
    /// </remarks>
    IndentKind HeldOrSpent(
        int node,
        IndentKind kind,
        IndentFlags conditions,
        int chainGroup,
        Stack<(int Node, int Child)> stack
    ) {
        if ((conditions & IndentFlags.HeldWhileOwnerFlat) != 0 && OwnerBroke(stack)) {
            return kind;
        }

        if ((conditions & IndentFlags.HeldWhileChainWhole) != 0
            && chainGroup >= 0
            && ChainBreaksInside(node, chainGroup, stack)) {
            return kind;
        }

        return IndentKind.None;
    }

    /// <summary>Whether the nearest group around the top of <paramref name="stack" /> resolved broken.</summary>
    bool OwnerBroke(Stack<(int Node, int Child)> stack) {
        foreach (var (ancestor, _) in stack) {
            ref var slot = ref document.Nodes[ancestor];
            if (slot.Kind == DocKind.Group) {
                return fitter.ModeOf(slot.Arg1) == ResolvedMode.Broken;
            }
        }

        return false;
    }

    /// <summary>
    ///     Whether <paramref name="chainGroup" /> takes one of its points when the contents of the scope
    ///     at <paramref name="node" /> are written with the scope held — written ahead, watched, and
    ///     rolled back.
    /// </summary>
    /// <remarks>
    ///     ⚠ <see cref="HeldLevel" />'s <c>WhileChainWhole</c>, and the question is a fill's (issue
    ///     #407, SK-DIV-0158): under <c>wrap_if_long</c> a chain breaks before a dot point by point,
    ///     by width, at the writer's own columns, and whether it breaks decides the level the
    ///     columns are counted from. The same reason <see cref="NextLineFitsBeside" /> gives applies:
    ///     a second model of the fill here would disagree a column at a time, so the writer writes
    ///     the scope's contents held, reads the answer off its own decisions, and restores everything.
    ///     <para>
    ///         ⚠ Sound because the answer is monotone in the level. Held is the shallower of the two
    ///         layouts, so a chain that breaks there breaks spent as well, and the real walk then
    ///         writes it broken; one that stays whole held is written held, exactly as speculated.
    ///         Pass two reads a fill's break back as the author's, which disqualifies the hold from the
    ///         source and spends the level — the same layout.
    ///     </para>
    /// </remarks>
    bool ChainBreaksInside(int node, int chainGroup, Stack<(int Node, int Child)> stack) {
        var (watched, broke) = (watchedChain, watchedChainBroke);
        var checkpoint = Checkpoint();
        watchedChain = chainGroup;
        watchedChainBroke = false;

        Push(IndentKind.None, IndentFlags.None, 0, stack);
        var ahead = new Stack<(int Node, int Child)>(stack.Reverse());
        var floor = ahead.Count;
        var children = document.ChildrenOf(node);
        ahead.Push((node, 1));
        if (children.Length > 0) {
            ahead.Push((children[0], 0));
        }

        Run(ahead, int.MaxValue, floor);
        var answer = watchedChainBroke;
        Restore(checkpoint);
        (watchedChain, watchedChainBroke) = (watched, broke);
        return answer;
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
    void Push(
        IndentKind kind,
        IndentFlags flags,
        int columns,
        Stack<(int Node, int Child)> ancestors,
        int node = -1
    ) {
        var unconditional = (flags & IndentFlags.Unconditional) != 0;


        // ⚠ The closing delimiter goes back to the level the scope was opened AT, not to the level
        // of the physical line the opener happened to land on. The two differ whenever a condition
        // or an initializer pushed the opener rightwards:
        // <code>
        // if (first
        //     &amp;&amp; second) {
        //     Body();
        // }               ← the `if`'s level, not the `&amp;&amp; second` line's
        // </code>
        // ⚠ A block — and the anchor a switch expression's block nests from — reads the scopes opened
        // on its own line differently from every other scope. See LevelForBlock.
        var outer = kind is IndentKind.Block or IndentKind.Anchor ? LevelForBlock(ancestors) : LevelForNested();

        // ⚠ A delimited list on the first line of a construct that broke after it nests from that
        // construct's continuation line, and its closer sits on it. See LiftedLevel.
        var lifted = -1;
        if (kind is IndentKind.Continuous or IndentKind.OneLevel
            && (flags & (IndentFlags.Delimiter | IndentFlags.ChainLevel)) != 0) {
            lifted = LiftedLevel(ancestors, outer, (flags & IndentFlags.ChainLevel) != 0, node, kind, columns);
            if (lifted >= 0) {
                outer = lifted;
            }
        }

        // ⚠ An anchored block nests from the line its anchor was pushed on, which is the governing
        // expression's line and not the brace's. See IndentKind.Anchor.
        if (kind is IndentKind.AnchoredBlock or IndentKind.AnchoredBrace) {
            for (var i = scopes.Count - 1; i >= 0; i--) {
                if (scopes[i].IsAnchor) {
                    outer = scopes[i].CloserLevel;
                    break;
                }
            }

            // The brace itself sits on the level its block nests from.
            if (kind == IndentKind.AnchoredBrace) {
                scopes.Add(new(true, outer, line, outer, unconditional));
                return;
            }

            kind = IndentKind.Block;
        }

        scopes.Add(
            kind switch {
                IndentKind.Block => new Scope(true, outer + indentWidth, line, outer, unconditional),

                // ⚠ The level a scope opening here would nest from — `outer` — and not the line's own
                // indentation. The two differ by the delimited scopes opened earlier on this line, and
                // the oracle counts those: `if (member switch {` nests its arms from the condition's
                // aligned column and `.OrderBy(pair => pair switch {` from the argument's level, while
                // `var s = (a,\n b) switch {` nests from the statement's, because the `=`'s continuation
                // opened on this line is conditional and `LevelForNested` never counts one of those
                // (SK-DIV-0107, measured on all three).
                IndentKind.Anchor => new Scope(false, 0, int.MaxValue, outer, IsAnchor: true),
                IndentKind.Continuous =>
                    new Scope(
                        false,
                        continuousMultiplier * indentWidth,
                        line,
                        outer,
                        unconditional,
                        IsGrouping: (flags & IndentFlags.Grouping) != 0,
                        Lifted: lifted
                    ),
                IndentKind.OneLevel =>
                    new Scope(
                        false,
                        indentWidth,
                        line,
                        outer,
                        unconditional,
                        IsGrouping: (flags & IndentFlags.Grouping) != 0,
                        Lifted: lifted
                    ),
                IndentKind.Outdent =>
                    new Scope(true, Math.Max(0, outer - indentWidth), line, outer, unconditional),

                // ⚠ `align_multiline_statement_conditions = true`: an absolute column rather than a
                // level, captured where the scope opens — which is immediately after the condition's
                // `(`, so it is the column the writer is at. It is a Block scope in every other
                // respect, because "absolute, and nothing below it applies" is exactly what a block
                // already means; the only thing alignment adds is that the number is not a multiple
                // of the indent width.
                // ⚠ `IsAlignment` is the one thing that separates this from a block, and it is read by
                // `LevelColumn` alone: `alignment_tab_fill_style = use_spaces` spells the level part of
                // an indent in tabs and the alignment part in spaces, so it has to know which part of
                // this scope's column is which. `CloserLevel` — `outer` — is the level part.
                IndentKind.Align => new Scope(
                    true,
                    CurrentColumn(),
                    line,
                    outer,
                    unconditional,
                    IsAlignment: true,
                    AlignedCloser: (flags & IndentFlags.CloserAtOpener) != 0 ? Math.Max(0, CurrentColumn() - 1) : -1
                ),

                // ⚠ Columns, not a level, and it carries them in a field of its own rather than in
                // `Level` so that the collapse in `Level(bool)` never sees them. `Level` is 0 here:
                // an outdent scope adds nothing and subtracts a column count, which is a different
                // question from "how many levels does this line take".
                IndentKind.OutdentColumns =>
                    new Scope(false, 0, line, outer, unconditional, Math.Max(0, columns)),

                // ⚠ The indentation of the line being written, or of the line about to start when the
                // scope opens right after a break. See IndentKind.FromLine.
                IndentKind.FromLine =>
                    new Scope(
                        false,
                        FromLineBase() + indentWidth,
                        line,
                        outer,
                        unconditional,
                        IsFromLine: true
                    ),
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
            pendingCloserLevel = scopes[^1].AlignedCloser >= 0 ? scopes[^1].AlignedCloser : scopes[^1].CloserLevel;
        }

        scopes.RemoveAt(scopes.Count - 1);
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
    int LevelForNested() => Level(true);

    /// <summary>
    ///     The level a block opening now nests from — and the level its <c>}</c> takes.
    /// </summary>
    /// <remarks>
    ///     ⚠ Not <see cref="LevelForNested" />, and the difference is in the scopes opened on this line.
    ///     Measured with the oracle on some fifty shapes at three indent depths (issue #393,
    ///     SK-DIV-0148), and it is one rule with two consequences. A block nests from where a
    ///     continuation line of the innermost broken construct around it would start, plus the
    ///     delimiters opened between that construct and the block.
    ///     <list type="bullet">
    ///         <item>
    ///             Nothing around the block broke: a grouping parenthesis opened on this line adds
    ///             nothing. <c>var x = (y switch {</c> puts the arms one level past the statement and
    ///             the <c>}</c> on it — as <c>(new T {</c>, <c>(r with {</c>, <c>((T)new T {</c>,
    ///             <c>((y switch {</c>, <c>M((y switch {</c> and <c>(() =&gt; {</c> do — where the
    ///             parenthesis's unconditional scope put them a level deeper. Unconditional is right for
    ///             a continuation line <em>inside</em> it, <c>if ((a</c> / <c>== b))</c>, and that
    ///             is untouched.
    ///         </item>
    ///         <item>
    ///             A binary or a chain around it broke (<see cref="GroupFacts.Continues" />, resolved
    ///             <see cref="ResolvedMode.Broken" />): the block takes the level that construct's own
    ///             continuation line takes, scopes opened on this line included.
    ///             <c>var x = y switch { … }</c> / <c>+ 1;</c> puts the arms two levels in, the
    ///             <c>+ 1</c> at one;
    ///             <c>(y switch { … }</c> / <c>+ 1)</c> the same, the parenthesis paying;
    ///             <c>(…).ToString()</c> / <c>.Length</c> the same for a chain; and
    ///             <c>var ok = items.Any(x =&gt; {</c> … <c>})</c> / <c>&amp;&amp; flag</c> puts the body
    ///             three in — the argument list's level is not collapsed into the chain's. Where the
    ///             continuation line spends nothing, neither does the block: under an arrow
    ///             (<c>=&gt;</c> / <c>y switch { … }</c> / <c>+ 1</c>), in a ternary branch and inside
    ///             <c>M(</c> / <c>y switch { … }</c> / <c>+ 1</c> the arms are one level past their line.
    ///         </item>
    ///     </list>
    ///     The walk's stack is the document nodes the block is inside, innermost first. Each
    ///     <see cref="DocKind.Indent" /> among them is one entry of <see cref="scopes" /> in the same
    ///     order, and a group's continuation scope is its first child, so the ancestor after a scope is
    ///     the group that opened it.
    /// </remarks>
    /// <param name="brokenAt">
    ///     The index in the path of the broken construct to nest from, when the caller has chosen it;
    ///     −2 to look for the innermost one. See <see cref="LiftedLevel" />.
    /// </param>
    int LevelForBlock(Stack<(int Node, int Child)> ancestors, int brokenAt = -2) {
        var path = ancestors.ToArray();

        // ⚠ The pairing of ancestors with scopes is the whole method, so a stack that does not hold
        // one Indent node per open scope is answered the ordinary way rather than misread.
        var indents = path.Count(frame => document.Nodes[frame.Node].Kind == DocKind.Indent);

        if (indents != scopes.Count) {
            return LevelForNested();
        }

        var broken = brokenAt == -2 ? InnermostBrokenConstruct(path) : brokenAt;
        var level = 0;
        var blocked = -1;
        var outside = false;
        for (int a = 0, next = scopes.Count - 1; a < path.Length; a++) {
            // ⚠ A delimiter opened on this line inside the broken construct does not absorb the
            // construct's own level, so its block on this line is lifted at the boundary; a block
            // from an earlier line is the ordinary one-level-per-line rule and stays.
            // ⚠ Once. When the construct pays its own level the boundary is crossed at its scope,
            // one ancestor earlier, and lifting the block again at the group un-collapsed an `=`
            // opened on the same line: `var commands = new[] {` … `}.Where(…)` / `.Select(…)` put the
            // elements a level past the oracle's — Lint drift on CommandParameterNotSuppliedAnalyzer.
            if (a == broken && !outside) {
                outside = true;
                if (blocked == line) {
                    blocked = -1;
                }
            }

            if (document.Nodes[path[a].Node].Kind != DocKind.Indent) {
                continue;
            }

            var index = next;
            var scope = scopes[next--];

            // ⚠ The broken construct's own continuation scope is its first child, so it is met
            // before the group is; it belongs outside, with the group.
            if (!outside
                && broken >= 0
                && a + 1 == broken
                && document.FactsOf(document.Nodes[path[broken].Node].Arg1).SpendsIndent) {
                outside = true;
                if (blocked == line) {
                    blocked = -1;
                }
            }

            if (scope.ColumnOutdent != 0) {
                if (scope.OpenLine < line) {
                    level -= scope.ColumnOutdent;
                }

                continue;
            }

            if (scope.IsBlock) {
                return Math.Max(0, level + scope.Level);
            }

            if (scope.Lifted >= 0 && !BrokenInsideOnItsLine(index, scope)) {
                var counts = outside
                    ? scope.OpenLine <= line && (scope.Unconditional || scope.OpenLine != blocked)
                    : scope.Unconditional
                        ? scope.OpenLine <= line
                        : scope.OpenLine < line && scope.OpenLine != blocked;

                return Math.Max(0, level + scope.Lifted + (counts ? scope.Level : 0));
            }

            // Outside the broken construct: the level a line starting after this one takes.
            if (outside) {
                if (scope.OpenLine <= line && (scope.Unconditional || scope.OpenLine != blocked)) {
                    level += scope.Level;
                    blocked = scope.OpenLine;
                }

                continue;
            }

            if (scope.OpenLine == line && scope.IsGrouping) {
                continue;
            }

            if (scope.Unconditional) {
                if (scope.OpenLine <= line) {
                    level += scope.Level;
                    blocked = scope.OpenLine;
                }

                continue;
            }

            if (scope.OpenLine < line && scope.OpenLine != blocked) {
                level += scope.Level;
                blocked = scope.OpenLine;
            }
        }

        return Math.Max(0, level);
    }

    /// <summary>
    ///     The index in <paramref name="path" /> of the innermost group around the top of the stack that
    ///     resolved <see cref="ResolvedMode.Broken" /> and <see cref="GroupFacts.Continues" />, looked for
    ///     inside the innermost enclosing block only; −1 when there is none.
    /// </summary>
    /// <summary>
    ///     <see cref="InnermostBrokenConstruct" /> for a fill chain: the innermost broken group carrying
    ///     <see cref="GroupFacts.ContinuesIfItBreaks" />, inside the innermost enclosing block; −1 for none.
    /// </summary>
    int InnermostBrokenFill((int Node, int Child)[] path) {
        for (int a = 0, next = scopes.Count - 1; a < path.Length; a++) {
            ref var slot = ref document.Nodes[path[a].Node];
            if (slot.Kind == DocKind.Indent && scopes[next--].IsBlock) {
                break;
            }

            if (slot.Kind == DocKind.Group
                && fitter.ModeOf(slot.Arg1) == ResolvedMode.Broken
                && document.FactsOf(slot.Arg1).ContinuesIfItBreaks) {
                return a;
            }
        }

        return -1;
    }

    /// <summary>
    ///     Whether the fill chain at <paramref name="pathIndex" /> takes one of its points once the scope at
    ///     <paramref name="node" /> is written unlifted — the rest of the chain written ahead, watched, and
    ///     rolled back.
    /// </summary>
    /// <remarks>
    ///     ⚠ <see cref="ChainBreaksInside" />'s technique, run to the end of the chain's group rather than of
    ///     the scope, because the point that decides it lies past the list: `source.Select(x => {` … `}` /
    ///     `).Where(alpha)` / `.ToList(beta)` lifts for the break before <c>.ToList</c>. Sound for the same
    ///     reason: unlifted is the shallower layout, so a chain that breaks there breaks lifted too.
    /// </remarks>
    bool FillBreaksAfter(
        int node,
        IndentKind kind,
        int columns,
        Stack<(int Node, int Child)> ancestors,
        int pathIndex
    ) {
        var group = document.Nodes[ancestors.ToArray()[pathIndex].Node].Arg1;
        var (watched, broke) = (watchedChain, watchedChainBroke);
        var checkpoint = Checkpoint();
        watchedChain = group;
        watchedChainBroke = false;

        Push(kind, IndentFlags.None, columns, ancestors);
        var ahead = new Stack<(int Node, int Child)>(ancestors.Reverse());
        var floor = ancestors.Count - pathIndex - 1;
        var children = document.ChildrenOf(node);
        ahead.Push((node, 1));
        if (children.Length > 0) {
            ahead.Push((children[0], 0));
        }

        Run(ahead, int.MaxValue, floor);
        var answer = watchedChainBroke;
        Restore(checkpoint);
        (watchedChain, watchedChainBroke) = (watched, broke);
        return answer;
    }

    int InnermostBrokenConstruct((int Node, int Child)[] path) {
        for (int a = 0, next = scopes.Count - 1; a < path.Length; a++) {
            ref var slot = ref document.Nodes[path[a].Node];
            if (slot.Kind == DocKind.Indent && scopes[next--].IsBlock) {
                break;
            }

            if (slot.Kind == DocKind.Group
                && fitter.ModeOf(slot.Arg1) == ResolvedMode.Broken
                && document.FactsOf(slot.Arg1).Continues) {
                return a;
            }
        }

        return -1;
    }

    /// <summary>
    ///     Whether a broken construct was entered inside the lifted list at <paramref name="index" />, on
    ///     the line the list opened on. A line inside that construct is the construct's continuation, and
    ///     it continues the ordinary way: the list is read as an ordinary scope.
    /// </summary>
    /// <remarks>
    ///     ⚠ Measured on Skala's own source, the Lint drift of #418:
    ///     <c>found.SelectMany(static d =&gt; Enumerable.Range(0, n)</c> / <c>.Select(…)</c> / <c>)</c> /
    ///     <c>.OrderByDescending(…)</c> keeps the inner <c>.Select</c> two levels past the statement, and
    ///     <c>scope.DescendantNodes(static n =&gt; n is not (A</c> / <c>or B)</c> / <c>)</c> /
    ///     <c>.OfType…</c> keeps the <c>or</c> where it was — while the outer <c>)</c> moves to the
    ///     chain's line in both, and a call opened on that line, <c>source.Select(a =&gt; Foo(</c>, lifts.
    ///     The innermost broken construct around a line decides, as SK-DIV-0148's rule says for a block;
    ///     a grouping parenthesis or a pattern's own parenthesis pays for a construct that opens no
    ///     scope of its own, so the construct is looked for among the groups, not the scopes.
    /// </remarks>
    bool BrokenInsideOnItsLine(int index, in Scope scope) {
        foreach (var (_, depth, opened) in brokenConstructs) {
            if (depth > index && opened == scope.OpenLine) {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    ///     The level a delimited list opening now nests from when it opens on the first line of a
    ///     construct that broke after it — that construct's continuation line — or −1 when the list
    ///     nests the ordinary way from <paramref name="nested" />.
    /// </summary>
    /// <remarks>
    ///     ⚠ <see cref="LevelForBlock" />'s rule (SK-DIV-0148) applied to a list, which SK-DIV-0149
    ///     recorded and issue #418 measured: asked of the oracle on some three hundred shapes — chain
    ///     roots that are a name, a member access, <c>this</c>, a call, a <c>new</c>, a parenthesis, a
    ///     cast, an <c>await</c>, a <c>!</c> and a <c>?.</c>; under <c>var x =</c>, <c>return</c>, an
    ///     assignment, an argument, an expression body and a statement; two indent depths —
    ///     <code>
    /// var x = source.Select(
    ///         aaaa…           ← two levels past the statement, not one
    ///     )                   ← on the chain's continuation line, with the dots
    ///     .Where(beta);
    ///     </code>
    ///     and the same for a <c>new Foo(</c> heading the chain, a <c>Select&lt;int&gt;(</c>, a
    ///     <c>Select(</c> followed by <c>[0]</c> or a property, and the first operand of a broken
    ///     <c>??</c>. Under an argument list the chain's level is spent in the item's line and the
    ///     list nests one past it; where the construct spends nothing — a binary inside a delimiter,
    ///     a chain headed by a parenthesis under an owner that already spent — there is nothing to lift
    ///     and the list's level is the ordinary one.
    ///     <para>
    ///         ⚠ Not for a fill, which carries no <see cref="GroupFacts.Continues" />. Under
    ///         <c>wrap_if_long</c> the chain's group resolves broken whenever the whole chain does not
    ///         fit, and the oracle lifts the list only when the chain then breaks after it:
    ///         <c>source.Select(</c> / arguments / <c>).Where(beta);</c> keeps the arguments one level
    ///         in. Whether a fill breaks is decided point by point after the list is written, and an
    ///         author's break the fill pinned is a required break no group owns, so a fill keeps the
    ///         ordinary level and the case where it breaks after the list is open (SK-DIV-0185).
    ///     </para>
    /// </remarks>
    /// <param name="ownerIsAChain">
    ///     ⚠ <see cref="IndentFlags.ChainLevel" />: the scope is a chain's own, and the chain's group —
    ///     the scope's owner, at the top of <paramref name="ancestors" /> — is not a construct around it.
    ///     Without the exclusion a chain that broke would lift its own dots past an <c>=</c> on its
    ///     line: <c>var x = a.B()</c> / <c>.C()</c> at two levels.
    /// </param>
    /// <param name="node">The scope's own document node, for the fill's lookahead; −1 for none.</param>
    /// <param name="kind">The scope's kind, pushed unlifted while the lookahead writes ahead.</param>
    /// <param name="columns">The scope's column count, likewise.</param>
    int LiftedLevel(
        Stack<(int Node, int Child)> ancestors,
        int nested,
        bool ownerIsAChain = false,
        int node = -1,
        IndentKind kind = IndentKind.Continuous,
        int columns = 0
    ) {
        var path = ancestors.ToArray();
        if (path.Count(frame => document.Nodes[frame.Node].Kind == DocKind.Indent) != scopes.Count) {
            return -1;
        }

        // ⚠ A fill chain around the list (#496, SK-DIV-0185): lifted exactly when the chain then takes a
        // point, which only writing ahead can say. See GroupFacts.ContinuesIfItBreaks.
        if (!ownerIsAChain && node >= 0 && InnermostBrokenConstruct(path) < 0) {
            var fill = InnermostBrokenFill(path);
            if (fill < 0 || !FillBreaksAfter(node, kind, columns, ancestors, fill)) {
                return -1;
            }

            var lifted = LevelForBlock(ancestors, fill);
            return lifted > nested ? lifted : -1;
        }

        var brokenAt = -2;
        if (ownerIsAChain) {
            if (path.Length == 0 || document.Nodes[path[0].Node].Kind != DocKind.Group) {
                return -1;
            }

            var around = InnermostBrokenConstruct(path[1..]);
            if (around < 0) {
                return -1;
            }

            // ⚠ Only a binary operator's: the chain is its left operand. A chain around a chain — a
            // receiver that is a chain of its own — and a block's owner are not this rule's.
            brokenAt = around + 1;
            if (!document.FactsOf(document.Nodes[path[brokenAt].Node].Arg1).ChainLink) {
                return -1;
            }
        } else if (InnermostBrokenConstruct(path) < 0) {
            return -1;
        }

        var level = LevelForBlock(ancestors, brokenAt);
        return level > nested ? level : -1;
    }

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
    int Effective() => Level(false);

    /// <summary>
    ///     The same as <see cref="Effective" />, but counting an alignment scope at the level it replaced
    ///     rather than at the column it chose.
    /// </summary>
    /// <remarks>
    ///     ⚠ Read by <see cref="WriteIndentTo" /> and by nothing else.
    ///     <c>
    /// alignment_tab_fill_style =
    ///     use_spaces
    ///     </c> — the export's own value — writes the level part of a line's indentation in tabs
    ///     and the alignment part in spaces, which needs the two numbers separately; every other value,
    ///     and every space-indented file, needs only <see cref="Effective" />. An
    ///     <see cref="IndentKind.Align" /> scope's <c>CloserLevel</c> is the level it was opened at, which
    ///     is exactly the level the alignment column replaced.
    /// </remarks>
    int LevelColumn() => Level(false, true);

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
    /// <param name="levelsOnly">
    ///     ⚠ <see cref="LevelColumn" />: count an alignment scope at <c>CloserLevel</c>, the level it was
    ///     opened at, rather than at the absolute column it chose. False everywhere the answer is "where
    ///     does this line start"; true only where <c>alignment_tab_fill_style</c> needs to know how much
    ///     of that column is levels.
    /// </param>
    int Level(bool nested, bool levelsOnly = false) {
        var level = 0;
        var blocked = -1;
        for (var i = scopes.Count - 1; i >= 0; i--) {
            var scope = scopes[i];

            // ⚠ Before the block check and before `blocked` is touched, and both are deliberate. A
            // column outdent is not a level: it never satisfies an enclosing scope's collapse, and
            // it applies inside a block as readily as inside a continuation — a chained call whose
            // dots are outdented is outdented from whatever column the block put it on, so the
            // subtraction has to survive the early return below. Its own opening line is exempt,
            // which is what leaves the first operand of a chain where it was.
            if (scope.ColumnOutdent != 0) {
                if (scope.OpenLine < line) {
                    level -= scope.ColumnOutdent;
                }

                continue;
            }

            if (scope.IsBlock) {
                return Math.Max(0, level + (levelsOnly && scope.IsAlignment ? scope.CloserLevel : scope.Level));
            }

            // ⚠ Absolute, as a block is, but only for a line after the one it opened on: the operand's
            // own line, and anything opened on it, is laid out as if the scope were not there. A scope
            // inside it that opened on that same line and already spent its level here takes the
            // operand line's own indentation as its base rather than the scope's level on top:
            // `var a = Compute(` / arguments one level in, `)` back — not two (#445).
            if (scope.IsFromLine) {
                if (scope.OpenLine < line) {
                    return Math.Max(0, level + (blocked == scope.OpenLine ? scope.Level - indentWidth : scope.Level));
                }

                continue;
            }

            // ⚠ Absolute, as a block is: everything outside a lifted list is already in `Lifted`.
            // Unless a broken construct inside the list opened on the list's own line, which is the
            // innermost broken construct around this line and continues the ordinary way.
            if (scope.Lifted >= 0 && !BrokenInsideOnItsLine(i, scope)) {
                var counts = scope.Unconditional
                    ? nested ? scope.OpenLine <= line : scope.OpenLine < line
                    : scope.OpenLine < line && scope.OpenLine != blocked;

                return Math.Max(0, level + scope.Lifted + (counts ? scope.Level : 0));
            }

            if (scope.Unconditional) {
                if (nested ? scope.OpenLine <= line : scope.OpenLine < line) {
                    level += scope.Level;
                    blocked = scope.OpenLine;
                }

                continue;
            }

            if (scope.OpenLine < line && scope.OpenLine != blocked) {
                level += scope.Level;
                blocked = scope.OpenLine;
            }
        }

        // ⚠ Clamped, because a column outdent is the one contribution that can be negative and the
        // file's outermost construct has no level to spend it against.
        return Math.Max(0, level);
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
    /// <param name="ColumnOutdent">
    ///     ⚠ <see cref="IndentKind.OutdentColumns" />' column count, and zero for every other kind. It is
    ///     a separate field rather than a negative <paramref name="Level" /> because the two are read by
    ///     different rules: a level takes part in the one-level-per-opening-line collapse and a column
    ///     shift must not, or an outdent scope opened mid-line would suppress the continuation level of
    ///     whatever opened earlier on the same line.
    /// </param>
    /// <param name="IsAlignment">
    ///     ⚠ <see cref="IndentKind.Align" />, whose <paramref name="Level" /> is an absolute column rather
    ///     than a level. Only <see cref="LevelColumn" /> reads it, for <c>alignment_tab_fill_style</c>.
    /// </param>
    /// <param name="IsGrouping">
    ///     ⚠ <see cref="IndentFlags.Grouping" />: a grouping parenthesis's scope, which
    ///     <see cref="LevelForBlock" /> counts on its own line only when something inside it broke.
    /// </param>
    /// <param name="Lifted">
    ///     ⚠ <see cref="IndentFlags.Delimiter" />: the level a delimited list opened on the first line of
    ///     a broken construct nests from — that construct's continuation line — or −1 for every other
    ///     scope. Such a scope is absolute, as a block is: wherever a walk of the stack reaches it, it
    ///     answers <c>Lifted</c> plus its own level when it counts and <c>Lifted</c> alone when it does
    ///     not, and nothing outside it is read. See <see cref="LiftedLevel" />.
    /// </param>
    /// <param name="AlignedCloser">
    ///     ⚠ <see cref="IndentFlags.CloserAtOpener" />: the column a closing delimiter on a line of its own
    ///     takes — its opener's — or −1, when it takes <paramref name="CloserLevel" /> (#442,
    ///     SK-DIV-0203). A field of its own because <paramref name="CloserLevel" /> is also the level an
    ///     emptied alignment falls back to, which this must not move.
    /// </param>
    /// <param name="IsAnchor">
    ///     ⚠ <see cref="IndentKind.Anchor" />: a marker that adds nothing and whose
    ///     <paramref name="CloserLevel" /> is the indentation of the line it was pushed on. Read by
    ///     <see cref="IndentKind.AnchoredBlock" /> alone.
    /// </param>
    readonly record struct Scope(
        bool IsBlock,
        int Level,
        int OpenLine,
        int CloserLevel,
        bool Unconditional = false,
        int ColumnOutdent = 0,
        bool IsAlignment = false,
        bool IsAnchor = false,
        bool IsGrouping = false,
        int Lifted = -1,
        int AlignedCloser = -1,
        bool IsFromLine = false);

    /// <summary>The indentation already written at the start of the line being built.</summary>
    /// <summary>
    ///     The indentation an <see cref="IndentKind.FromLine" /> scope counts its level from: the line's
    ///     own, or an alignment column opened on this line — a statement condition's, whose content starts
    ///     past the <c>(</c> rather than at the line's indentation.
    /// </summary>
    int FromLineBase() {
        var indent = atLineStart ? pendingCloserLevel ?? Effective() : CurrentLineIndent();
        for (var i = scopes.Count - 1; i >= 0; i--) {
            if (scopes[i].IsAlignment && scopes[i].OpenLine == line) {
                return Math.Max(indent, scopes[i].Level);
            }
        }

        return indent;
    }

    int CurrentLineIndent() {
        var start = output.Length;
        while (start > 0 && output[start - 1] != '\n') {
            start--;
        }

        var indent = 0;
        for (var i = start; i < output.Length && output[i] is ' ' or '\t'; i++) {
            indent = TextWidth.Advance(output[i].ToString(), indent);
        }

        return indent;
    }

    /// <summary>Writes the indentation that reaches <paramref name="column" />.</summary>
    /// <param name="column">The column the first character of the line is to land on.</param>
    /// <param name="levelColumn">
    ///     The same line's indentation expressed in whole <em>levels</em> — the column it would take if
    ///     no alignment scope were open. Equal to <paramref name="column" /> on every ordinary line, and
    ///     smaller exactly where an <see cref="IndentKind.Align" /> scope put the line on a column of its
    ///     own. See <see cref="LevelColumn" />.
    /// </param>
    /// <remarks>
    ///     ⚠ <b><c>alignment_tab_fill_style</c>, and the three layouts are measured rather than derived.</b>
    ///     This method used to write whole indent units and then spaces for the remainder unconditionally,
    ///     with remarks claiming that is "what <c>alignment_tab_fill_style = use_spaces</c> asks for". It is
    ///     not — it is <c>optimal_fill</c>, and the export asks for <c>use_spaces</c>, so Skala wrote the
    ///     wrong one of the three layouts on every aligned continuation line of every tab-indented file
    ///     (SK-DIV-0032).
    ///     <para>
    ///         Re-measured against <c>jb cleanupcode</c> 2025.2.6 under <c>indent_style = tab</c>,
    ///         <c>tab_width = 4</c>, on statement conditions aligned at four different columns inside blocks
    ///         at three different depths. The tab portion is written <c>»</c> and the space portion <c>·</c>:
    ///         <code>
    /// column │ block │ use_spaces    │ use_tabs_only │ optimal_fill
    ///     12 │     8 │ »»····        │ »»»           │ »»»
    ///     14 │     8 │ »»······      │ »»»           │ »»»··
    ///     15 │     8 │ »»·······     │ »»»»          │ »»»···
    ///     18 │    12 │ »»»······     │ »»»»          │ »»»»··
    ///     23 │    16 │ »»»»·······   │ »»»»»»        │ »»»»»···
    ///         </code>
    ///     </para>
    ///     <list type="bullet">
    ///         <item>
    ///             <c>use_spaces</c> — <b>the export's own value</b> — tabs as far as the line's own
    ///             <em>level</em> column and spells the alignment remainder in spaces, which is what makes it
    ///             "look aligned on any tab size". ⚠ It is the level column and not the enclosing block's:
    ///             measured on the same probe, a plain continuation line at column 12 inside a block at 8 is
    ///             written as three whole tabs, while an <em>aligned</em> line at that same column 12 is
    ///             written as two tabs and four spaces. A continuation level is a level and stays tabs; only
    ///             what alignment adds becomes spaces. The two are indistinguishable on any line whose
    ///             alignment column happens to be a multiple of the tab width, which is why nothing caught
    ///             this.
    ///         </item>
    ///         <item>
    ///             <c>use_tabs_only</c> rounds to the <em>nearest</em> tab stop and writes no spaces at all,
    ///             so the column reached is not the column asked for — which is what the option's own summary
    ///             means by "(inaccurate)". ⚠ The recorded model said "rounded <em>down</em>" and that is
    ///             refuted by the table above: 15 goes up to 16 and 23 up to 24, while 14 and 18 go down.
    ///             Ties break downwards (14 and 18 are both exactly half a tab past a stop).
    ///         </item>
    ///         <item><c>optimal_fill</c> divides the whole column by the tab width — the old unconditional body.</item>
    ///     </list>
    ///     <para>
    ///         ⚠ The key applies only when the indent unit is a tab, and that is not a shortcut. With spaces
    ///         the unit <em>is</em> a space, so all three spell the identical column; measured, all three
    ///         values return an 18-file probe byte-identical under <c>indent_style = space</c>. Letting
    ///         <c>use_tabs_only</c>'s rounding run on a space-indented file would move every aligned line to
    ///         a column no configuration asked for.
    ///     </para>
    /// </remarks>
    void WriteIndentTo(int column, int levelColumn) {
        var tabs = indentUnit == "\t";
        var units = tabFill switch {
            TabFillStyle.UseSpaces when tabs => Math.Min(levelColumn, column) / indentWidth,

            // Round to the nearest stop, ties downwards: 15 ⇒ 4 units, 14 ⇒ 3, 23 ⇒ 6, 18 ⇒ 4.
            TabFillStyle.UseTabsOnly when tabs => (2 * column + indentWidth - 1) / (2 * indentWidth),
            _ => column / indentWidth
        };

        for (var i = 0; i < units; i++) {
            output.Append(indentUnit);
        }

        this.column = units * indentWidth;

        // ⚠ `use_tabs_only` stops here. It reaches a tab stop and not the alignment column, and the
        // remainder is deliberately not spelled — filling it with spaces would be `optimal_fill`.
        if (tabs && tabFill == TabFillStyle.UseTabsOnly) {
            return;
        }

        for (var i = this.column; i < column; i++) {
            output.Append(' ');
            this.column++;
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
        if (atLineStart) {
            return pendingCloserLevel ?? Effective();
        }

        return column + PendingWidth;
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
    /// <summary>
    ///     Puts every continuation line of a starred block comment on <paramref name="column" />.
    /// </summary>
    /// <remarks>
    ///     ⚠ <c>align_multiline_comments = true</c>, which is the export's own value, and SK-DIV-0033's
    ///     whole subject. Measured against <c>jb cleanupcode</c> 2025.2.6 under
    ///     <c>OracleProfile.FormatOnly</c>: every line after the first — the closing <c>*/</c>'s line
    ///     included — lands on the opening <c>/*</c>'s column plus one, whatever column it was written at.
    ///     <para>
    ///         ⚠ Whether a comment qualifies is <em>not</em> decided here. The caller only sets the flag on
    ///         a comment whose every continuation line already begins with <c>*</c>, so this method may
    ///         replace each line's leading whitespace unconditionally; the disqualifying shapes are the
    ///         caller's to recognise, because that is where the comment's text is available before layout.
    ///     </para>
    ///     <para>
    ///         ⚠ Spaces past the line's own indentation, and under <c>indent_style = tab</c> that
    ///         indentation's tabs first: <c>»/*</c> is followed by <c>» *</c>, and a comment trailing
    ///         <c>»»M();</c> by <c>»»</c> and spaces. The target is a column one past a delimiter, which is
    ///         not a multiple of anything, so <c>WriteIndentTo</c>'s fill styles do not apply. Measured for
    ///         #428; until then it was spaces throughout, which the oracle never writes in a tab file.
    ///     </para>
    /// </remarks>
    static string AlignStarred(string text, int column, string prefix, bool tabs) {
        var lines = text.Split('\n');
        if (lines.Length < 2) {
            return text;
        }

        var indent = Fill(column, tabs ? LeadingTabs(prefix) : 0);
        var builder = new StringBuilder(text.Length + lines.Length * 2);
        builder.Append(lines[0]);

        for (var i = 1; i < lines.Length; i++) {
            builder.Append('\n');

            // ⚠ A `\r` belongs to the line it ends, so it is carried across rather than trimmed. The
            // input is the token's own bytes and the file's line ending is not this method's to change.
            var line = lines[i];
            var carriage = line.EndsWith('\r');
            var body = carriage ? line[..^1] : line;

            var start = 0;
            while (start < body.Length && body[start] is ' ' or '\t') {
                start++;
            }

            builder.Append(indent).Append(body, start, body.Length - start);
            if (carriage) {
                builder.Append('\r');
            }
        }

        return builder.ToString();
    }

    /// <summary>The leading whitespace of the line being written.</summary>
    string CurrentLinePrefix() {
        var start = output.Length;
        while (start > 0 && output[start - 1] is not ('\n' or '\r')) {
            start--;
        }

        var end = start;
        while (end < output.Length && output[end] is ' ' or '\t') {
            end++;
        }

        return output.ToString(start, end - start);
    }

    /// <summary>
    ///     Moves every continuation line of a multi-line block comment by the columns its first line's
    ///     line moved: from <paramref name="sourceIndent" /> to <paramref name="prefix" />.
    /// </summary>
    /// <remarks>
    ///     ⚠ Issue #428, SK-DIV-0094. Measured against <c>jb cleanupcode</c> 2025.2.6 on about forty
    ///     comments — own-line before a member and a statement, trailing a statement, inside an argument
    ///     list, an initializer and a broken binary, moved right and left, plain, starred <c>/**</c> and
    ///     ragged:
    ///     <list type="bullet">
    ///         <item>
    ///             the shift is uniform and <b>clamps at column 0</b> line by line: a line written two
    ///             columns in under an opener that moves eight left lands at 0, and so does one written at
    ///             7 — the comment's shape is lost on exactly those lines, as the oracle loses it;
    ///         </item>
    ///         <item>an empty line stays empty (the caller has already trimmed whitespace-only ones);</item>
    ///         <item>
    ///             under <c>indent_style = space</c> the whole run is spaces, a tab the author wrote
    ///             included;
    ///         </item>
    ///         <item>
    ///             ⚠ under <c>indent_style = tab</c> the run is the new line's own indentation, then as
    ///             many tabs as the author's run had <em>leading</em> past the old line's indentation, then
    ///             spaces — never past the target. So <c>»»»x</c> under a <c>»»</c> line moved to <c>»</c>
    ///             is <c>»»x</c>, <c>»»··»x</c> is <c>»····x</c>, and a space-indented continuation under a
    ///             line now at three tabs is three tabs and the rest in spaces, as <c>use_spaces</c> fills
    ///             an aligned line. Six shapes, each one distinguishing this from a simpler reading.
    ///         </item>
    ///     </list>
    ///     ⚠ A comment is left byte for byte only when its line kept its indentation <em>string</em>: a line
    ///     that kept its column but went from four spaces to a tab re-spells the continuation too
    ///     (measured, <c>{ /* c</c> / <c>··d */</c> becomes <c>»··d */</c>).
    /// </remarks>
    static string ShiftWithLine(string text, string sourceIndent, string prefix, bool tabs) {
        if (prefix == sourceIndent || text.IndexOf('\n', StringComparison.Ordinal) < 0) {
            return text;
        }

        var delta = TextWidth.Measure(prefix) - TextWidth.Measure(sourceIndent);
        var prefixTabs = LeadingTabs(prefix);
        var lines = text.Split('\n');
        var builder = new StringBuilder(text.Length + lines.Length * Math.Max(0, delta));
        builder.Append(lines[0]);

        for (var i = 1; i < lines.Length; i++) {
            builder.Append('\n');
            var line = lines[i];
            var start = 0;
            while (start < line.Length && line[start] is ' ' or '\t') {
                start++;
            }

            if (start == line.Length || line[start] == '\r') {
                builder.Append(line);
                continue;
            }

            var run = line[..start];
            var tabCount = 0;
            if (tabs) {
                var rest = run.StartsWith(sourceIndent, StringComparison.Ordinal) ? run[sourceIndent.Length..] : run;
                tabCount = prefixTabs + (prefixTabs == prefix.Length ? LeadingTabs(rest) : 0);
            }

            builder.Append(Fill(TextWidth.Measure(run) + delta, tabCount)).Append(line, start, line.Length - start);
        }

        return builder.ToString();
    }

    static int LeadingTabs(string run) {
        var count = 0;
        while (count < run.Length && run[count] == '\t') {
            count++;
        }

        return count;
    }

    /// <summary>
    ///     The whitespace that reaches <paramref name="column" /> (never below 0): up to
    ///     <paramref name="tabs" /> tabs, as many as fit, then spaces.
    /// </summary>
    static string Fill(int column, int tabs) {
        column = Math.Max(0, column);
        tabs = Math.Min(tabs, column / TextWidth.TabStop);
        return new string('\t', tabs) + new string(' ', column - tabs * TextWidth.TabStop);
    }

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
            if (AddRemainingSiblings(node, child, ref total)) {
                return total;
            }
        }

        return total;
    }

    /// <summary>
    ///     Adds the flat width of <paramref name="node" />'s children from <paramref name="child" />
    ///     onwards to <paramref name="total" />, and says whether the line ended inside them.
    /// </summary>
    /// <remarks>
    ///     ⚠ One loop rather than two. <see cref="TrailingWidth" /> and
    ///     <see cref="TrailingAfterGroup" /> carried byte-identical copies of it and <c>SK7020</c>
    ///     reported them as one clone group; the two measures differ in *which frames* they walk, never
    ///     in how a frame is measured, and a change made to one copy and not the other is exactly the
    ///     disagreement described below.
    ///     <para>
    ///         ⚠ A break point's own flat rendering does not count. The measure is "the rest of this line
    ///         if every break point is taken", and if this one is taken the line ends here — the space it
    ///         would have rendered as is never written. Counting it made this measure one column larger
    ///         than the one a fill point uses on the same gap, and the two disagreeing is a
    ///         non-idempotency rather than a rounding error: the fill keeps an item on the line, the
    ///         item's own group then finds itself one column over and breaks, and the second pass sees a
    ///         multi-line item and breaks before it. Two files out of Vixen's 4 708 did exactly that.
    ///     </para>
    /// </remarks>
    /// <returns><c>true</c> when the line ended — a <c>Line</c> node or a taken break.</returns>
    bool AddRemainingSiblings(int node, int child, ref int total) {
        var children = document.ChildrenOf(node);
        for (var i = child; i < children.Length; i++) {
            var sibling = children[i];
            ref var slot = ref document.Nodes[sibling];
            if (slot.Kind == DocKind.Line) {
                // ⚠ A last-resort point is not the end of the line for anything before it: it is
                // measured as its flat rendering and the walk goes on. See LineFlags.LastResort.
                // ⚠ Unless it is a plain point of a group already resolved Broken — a following point
                // whose group has decided — because then it *is* going to break, and what follows it
                // is not on this line. The arguments of `[Description("…")]\n string? p` are measured
                // against the `]` when the kept break stays, and against the parameter when it joins
                // (SK-DIV-0114). A fill point is never read this way: its group being broken does not
                // say whether it breaks.
                // ⚠ A point that only yields to its predecessors (LineFlags.YieldsToPredecessors) is
                // not read through here at all. This walk is reached from *inside* the point's group —
                // a direct sibling on the stack is one the walk is standing beside — and inside the
                // list the point is an ordinary fill point: the nested `List<Guid>` in a type argument
                // list's first argument sees its line end at the outer comma, stays whole, and the
                // comma breaks (issue #377). The `=` in front of the list reads the list through the
                // builder's point width, which still counts the point as not taken.
                var flags = (LineFlags)slot.Flags;
                if ((LineKind)slot.Arg0 == LineKind.Soft && (flags & LineFlags.LastResort) != 0) {
                    if ((flags & LineFlags.FillPoint) == 0 && fitter.ModeOf(slot.Arg2) == ResolvedMode.Broken) {
                        return true;
                    }

                    var rendering = (flags & LineFlags.FlatSpace) != 0 ? 1 : 0;
                    total = total >= Document.Unbounded ? Document.Unbounded : total + rendering;
                    continue;
                }

                return true;
            }

            // ⚠ An arrow whose body has no break point of its own does not end the line for the
            // head before it: `{ … } => 2u,` and `A or B or C => 2u,` are measured with the arrow
            // and the body, and the pattern or the chain breaks rather than the arrow (issue #378).
            // The arrow's own points are still points — the group breaks by its own rule when the
            // head has nothing left to give — but a construct before it reads the body as part of
            // its line, the way it reads a last-resort point. See GroupFlags.ArrowBodyRunsToTheEnd.
            // ⚠ Except that a body with no break point can still hold a hard one: a multi-line raw
            // string has no flat width, and reading it as Unbounded chopped `double { P14: "k" }`
            // before `when _cache => """`. The line ends at the literal's first newline, so the head
            // counts only what precedes it — the oracle writes `{ P14: "k" } when _cache => """`
            // whole. And because the chop then moved the `when` down, pass two found the pattern
            // short and re-joined it: fourteen consecutive Nightly runs, one defect.
            if (document.ArrowBodyRunsToTheEnd(sibling)) {
                var flat = document.FlatWidthOf(sibling);
                if (flat >= Document.Unbounded) {
                    var head = document.HeadWidthOf(sibling);
                    total = total >= Document.Unbounded || head >= Document.Unbounded
                        ? Document.Unbounded
                        : total + head;
                    return true;
                }

                total = total >= Document.Unbounded || flat >= Document.Unbounded
                    ? Document.Unbounded
                    : total + flat;
                continue;
            }

            var width = document.PointWidthOf(sibling);
            total = total >= Document.Unbounded || width >= Document.Unbounded
                ? Document.Unbounded
                : total + width;

            if (document.HasBreak(sibling)) {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    ///     What still has to be written on this line once <paramref name="group" /> has ended.
    /// </summary>
    /// <remarks>
    ///     ⚠ <see cref="TrailingWidth" /> measured from outside the group rather than from here. The
    ///     walk is inside the group when a fill point is written, so the frames up to and including the
    ///     group's own are its interior — which the segment has already measured — and only the frames
    ///     beyond it are the rest of the line. Frames are innermost-first, so the group's is the last
    ///     one skipped.
    ///     <para>
    ///         ⚠ A group with no frame on the stack — which cannot happen for a point the walk is inside
    ///         — measures nothing rather than the whole line, so a front end that lost the frame declines
    ///         a break instead of taking a wrong one.
    ///     </para>
    /// </remarks>
    int TrailingAfterGroup(Stack<(int Node, int Child)> stack, int group) {
        var total = 0;
        var outside = false;
        foreach (var (node, child) in stack) {
            if (!outside) {
                ref var frame = ref document.Nodes[node];
                outside = frame.Kind == DocKind.Group && frame.Arg1 == group;
                continue;
            }

            if (AddRemainingSiblings(node, child, ref total)) {
                return total;
            }
        }

        return outside ? total : 0;
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
        for (var i = scopes.Count - 1; i >= 0; i--) {
            var scope = scopes[i];
            if (scope.IsBlock) {
                level += scope.Level;
                break;
            }

            // ⚠ A construct being entered on the list's own line continues the ordinary way if it
            // breaks, and its continuation column does not matter if it does not.
            if (scope.Lifted >= 0
                && !BrokenInsideOnItsLine(i, scope)
                && !(scope.OpenLine == line && document.FactsOf(group).Continues)) {
                level += scope.Lifted + (scope.OpenLine <= line && scope.OpenLine != counted ? scope.Level : 0);
                break;
            }

            if (scope.OpenLine <= line && scope.OpenLine != counted) {
                level += scope.Level;
                counted = scope.OpenLine;
            }
        }

        if (document.FactsOf(group).SpendsIndent) {
            level += continuousMultiplier * indentWidth;
        }

        return level;
    }

    /// <summary>
    ///     The line each array-initializer fill's latest element <em>started</em> on — its first token's,
    ///     so that a comment the author put on a line of its own in front of the element is not read as
    ///     the element spanning lines (<c>1,</c> / <c>/* f */ 2</c>).
    /// </summary>
    readonly Dictionary<int, int> filledElementLines = [];

    /// <summary>The fills whose element after their latest point has not written a token yet.</summary>
    readonly List<int> awaitingElement = [];

    /// <summary>
    ///     The line the element before this fill point started on: the fill's own record, or for the first
    ///     element the braces' — the group the fill names as its owner — or −1.
    /// </summary>
    int FilledElementStartedOn(int group) {
        if (filledElementLines.TryGetValue(group, out var started)) {
            return started;
        }

        var owner = document.FactsOf(group).Owner;
        return owner >= 0 && filledElementLines.TryGetValue(owner, out started) ? started : -1;
    }

    /// <summary>The line the latest token was written on: an element's last, at the point after it.</summary>
    int lastTokenLine;

    /// <param name="startedOn">The line the token began on: a literal spanning lines ends on another.</param>
    void StartFilledElements(int startedOn) {
        lastTokenLine = line;
        if (awaitingElement.Count == 0) {
            return;
        }

        foreach (var group in awaitingElement) {
            filledElementLines[group] = startedOn;
        }

        awaitingElement.Clear();
    }

    void WriteLine(ref DocNode slot, int node, Stack<(int Node, int Child)> stack) {
        var kind = (LineKind)slot.Arg0;
        if (kind == LineKind.Soft) {
            var flags = (LineFlags)slot.Flags;

            // ⚠ A break point renders as its flat form when its group stayed flat, and the flat
            // form is per-point: nothing after `(`, a space after `,`.
            var flat = slot.Arg2 < 0 || fitter.ModeOf(slot.Arg2) == ResolvedMode.Flat;

            // ⚠ A fill point in a broken group is the one break decision that is not the group's.
            // It breaks when the next item would not fit and stays put otherwise, which is what
            // makes `wrap_if_long` a fill rather than a chop.
            if (!flat && (flags & LineFlags.FillPoint) != 0) {
                flat = FillPointStaysFlat(node, slot.Arg2, flags, stack, out var headStays);

                // ⚠ The element before this point spanned lines, so the next one starts a line of its
                // own whatever fits (LineFlags.ArrayElement). Read off the output: its first token and
                // its last were written on different lines. A comment's own lines do not count, and
                // neither does a break the author kept in the gap before the next element's comment.
                // ⚠ Except before a delimited element that fits nowhere whole and keeps its head on the
                // line: `), [` / … / `],` / `(null ? …)` in `pathological/nested-collection-in-generated-
                // while.cs` keeps the bracket after the multi-line call and breaks after the bracket's
                // own element (#471, SK-DIV-0110). The head rule outranks the after rule.
                if (flat
                    && !headStays
                    && (flags & LineFlags.ArrayElement) != 0
                    && FilledElementStartedOn(slot.Arg2) is var started
                    && started >= 0
                    && started != lastTokenLine) {
                    flat = false;
                }
            }

            if ((flags & LineFlags.ArrayElement) != 0) {
                filledElementLines.Remove(slot.Arg2);
                awaitingElement.Add(slot.Arg2);
            }

            // ⚠ And a point taken only when the line it creates would not have fit beside it is
            // decided by writing that line: see NextLineFitsBeside. ⚠ Only while its group is still
            // on the line it was entered on — a section that spans lines, by a kept break between
            // its attributes or by arguments that chopped, moves the parameter down whatever the
            // parameter's width (SK-DIV-0114), and that is read off the output like
            // GroupFacts.BreaksIfOwnerIsMultiLine is (#372), not off the source.
            if (!flat
                && (flags & LineFlags.BreaksOnlyIfNextLineOverflows) != 0
                && fitter.EnteredOn(slot.Arg2) == line) {
                flat = NextLineFitsBeside(ref slot, flags, stack);
            }

            if (flat) {
                if ((flags & LineFlags.FlatSpace) != 0) {
                    pendingSpace = true;
                }

                return;
            }
        }

        if (watchedChain >= 0 && kind == LineKind.Soft && slot.Arg2 == watchedChain) {
            watchedChainBroke = true;
        }

        TakeBreak(ref slot);
    }

    /// <summary>Writes a break: the line ends here and the next one begins.</summary>
    void TakeBreak(ref DocNode slot) {
        var kind = (LineKind)slot.Arg0;

        // ⚠ A required break in front of an element — after a `//` comment between two elements — starts
        // that element on a line no fill point saw, so every fill on record waits for the next token to
        // say where it starts. Only a break the builder flagged as one: a break kept *inside* an element
        // is the element spanning lines, which is the thing being measured (#444).
        if (kind != LineKind.Soft
            && ((LineFlags)slot.Flags & LineFlags.ArrayElement) != 0
            && filledElementLines.Count > 0) {
            foreach (var group in filledElementLines.Keys) {
                if (!awaitingElement.Contains(group)) {
                    awaitingElement.Add(group);
                }
            }
        }

        // ⚠ An alignment column with nothing on it is a continuation level. `align_multiline_statement_conditions`
        // anchors the condition on the column after the `(` — and when the author broke the line right
        // there, the oracle does not align to a column nothing occupies: `while (\n c)` puts `c` one
        // continuation level in from the statement, not at the `(`'s column plus one, and the same for
        // `switch (`, `foreach (`, `lock (` and `do { } while (`. An `if (` hid it, because `if (` is
        // four columns wide and the two numbers coincide. Measured at `continuous_indent_multiplier = 2`
        // as well: the fallback is the multiplied continuation, eight columns, not one indent width
        // (SK-DIV-0102).
        //
        // Decided here rather than in the builder because only the writer knows whether the break
        // was taken: a Preserve group's kept break and a hard one both arrive as a line written on
        // the scope's own opening line with the column still at the anchor. "Nothing written since
        // the anchor" is the column test below — a piece written after the `(` would have moved
        // `column` past the level the scope captured.
        DemoteEmptyAlignment();

        // ⚠ remove_spaces_on_blank_lines = true: a pending space before a break is never written,
        // which is also what keeps the formatter from producing trailing whitespace at all. ⚠ A gap
        // `disable_space_changes` preserved is discarded here too, and that is exactly why such a
        // gap is a pending space and not text: preserving a run byte for byte must not survive into
        // a line the run no longer ends.
        pendingSpace = false;
        pendingSpaceText = null;

        // ⚠ …except with the indenter off, where it is not discarded but moved: the line this break
        // creates begins with the break point's own flat rendering. See <see cref="WriteSuppressedIndent" />.
        createdLineSpace = kind == LineKind.Soft && ((LineFlags)slot.Flags & LineFlags.FlatSpace) != 0;

        var newLine = slot.Payload > 0 ? document.Strings[slot.Payload] : defaultNewLine;
        output.Append(newLine);
        line++;
        for (var i = 0; i < slot.Arg1; i++) {
            output.Append(newLine);
            line++;
        }

        atLineStart = true;
        column = 0;
    }

    /// <summary>
    ///     Whether the line a break at <paramref name="slot" /> would create fits beside it instead —
    ///     written ahead, measured, and rolled back.
    /// </summary>
    /// <remarks>
    ///     ⚠ The gap after a parameter's single attribute section
    ///     (<see cref="LineFlags.BreaksOnlyIfNextLineOverflows" />, issue #377). The question is "does
    ///     <c>[Obsolete]</c> plus the parameter's <em>first line</em> fit", and the first line is not a
    ///     number the document holds: it is where the type argument
    ///     list's fill, laid out from the continuation column, takes its first break — a decision made
    ///     point by point, at the writer's own columns, with the head-keeping rule and the trailing
    ///     measure in it. A second implementation of that walk here would be the duplicated
    ///     indentation model <see cref="Fitter" />'s remarks warn against, one that disagrees a column
    ///     at a time. So the writer takes the break, writes the line the real walk would write, reads
    ///     its width off the output, and restores everything: the text, the anchors, the scopes, the
    ///     line and column, the pending gap and the fitter's decisions for every group it entered.
    ///     <para>
    ///         ⚠ Sound because every decision on that line is monotone in the column. A group or fill
    ///         point that stayed flat did so against a measure no wider than the line, and the line
    ///         fits at the joined column too; one that broke would break there as well. The real walk
    ///         then re-decides each of them at the joined column and lands on the same first break — or
    ///         an earlier one, whose line is shorter still, so pass two, reading that break as kept,
    ///         reaches the same join.
    ///     </para>
    ///     <para>
    ///         ⚠ Nested: the line written ahead may hold another such point, which speculates in turn.
    ///         Each writes at most one line, so the work stays linear in the line.
    ///     </para>
    /// </remarks>
    bool NextLineFitsBeside(ref DocNode slot, LineFlags flags, Stack<(int Node, int Child)> stack) {
        var gap = pendingSpace ? PendingWidth : (flags & LineFlags.FlatSpace) != 0 ? 1 : 0;
        var column = atLineStart ? pendingCloserLevel ?? Effective() : this.column + gap;

        var checkpoint = Checkpoint();
        TakeBreak(ref slot);
        var lineStart = output.Length;
        Run(new(stack.Reverse()), line);
        var width = LineContentWidth(lineStart);
        Restore(checkpoint);

        return Fits(column, width);
    }

    /// <summary>The width of the output line beginning at <paramref name="start" />, less its indentation.</summary>
    int LineContentWidth(int start) {
        var end = start;
        while (end < output.Length && output[end] != '\n') {
            end++;
        }

        if (end > start && output[end - 1] == '\r') {
            end--;
        }

        var content = start;
        while (content < end && output[content] is ' ' or '\t') {
            content++;
        }

        var indent = TextWidth.Advance(output.ToString(start, content - start), 0);
        return TextWidth.Advance(output.ToString(content, end - content), indent) - indent;
    }

    /// <summary>Everything the walk mutates, for <see cref="NextLineFitsBeside" /> to roll back to.</summary>
    /// <remarks>
    ///     ⚠ Fields and an initializer rather than a positional record: fourteen constructor parameters
    ///     is a <c>SK7005</c> finding of the formatter's own, and the one this replaces would be new.
    /// </remarks>
    struct WriterState {
        public int Output;
        public int Anchors;
        public Scope[] Scopes;
        public int Column;
        public int Line;
        public int? PendingCloserLevel;
        public bool AtLineStart;
        public bool PendingSpace;
        public string? PendingSpaceText;
        public bool CreatedLineSpace;
        public SourceSpan PendingAnchorSpan;
        public int PendingAnchorToken;
        public bool HasPendingAnchor;
        public bool WatchedChainBroke;
        public (int Group, int Depth, int Line)[] BrokenConstructs;
        public Fitter.Mark Fitter;
    }

    WriterState Checkpoint() =>
        new() {
            Output = output.Length,
            Anchors = anchors.Count,
            Scopes = [..scopes],
            Column = column,
            Line = line,
            PendingCloserLevel = pendingCloserLevel,
            AtLineStart = atLineStart,
            PendingSpace = pendingSpace,
            PendingSpaceText = pendingSpaceText,
            CreatedLineSpace = createdLineSpace,
            PendingAnchorSpan = pendingAnchorSpan,
            PendingAnchorToken = pendingAnchorToken,
            HasPendingAnchor = hasPendingAnchor,
            WatchedChainBroke = watchedChainBroke,
            BrokenConstructs = [..brokenConstructs],
            Fitter = fitter.MarkForRollback()
        };

    void Restore(in WriterState state) {
        output.Length = state.Output;
        anchors.RemoveRange(state.Anchors, anchors.Count - state.Anchors);
        scopes.Clear();
        scopes.AddRange(state.Scopes);
        column = state.Column;
        line = state.Line;
        pendingCloserLevel = state.PendingCloserLevel;
        atLineStart = state.AtLineStart;
        pendingSpace = state.PendingSpace;
        pendingSpaceText = state.PendingSpaceText;
        createdLineSpace = state.CreatedLineSpace;
        pendingAnchorSpan = state.PendingAnchorSpan;
        pendingAnchorToken = state.PendingAnchorToken;
        hasPendingAnchor = state.HasPendingAnchor;
        watchedChainBroke = state.WatchedChainBroke;
        brokenConstructs.Clear();
        brokenConstructs.AddRange(state.BrokenConstructs);
        fitter.Rollback(state.Fitter);
    }

    /// <summary>Whether a fill point in a broken group declines its break.</summary>
    /// <remarks>
    ///     ⚠ A fill breaks before an item only when that makes the item fit whole. An item that would
    ///     not fit on a fresh continuation line either — one with a break of its own that is certain,
    ///     or simply too wide — keeps its head on this line and breaks inside, which is what the oracle
    ///     writes for <c>(1\n, (2\n, 3))</c> and for a 110-column collection after a chopped call
    ///     (SK-DIV-0110, #339). An item that fits once moved still moves, whole, as the 104-column
    ///     initializer <see cref="Document.SegmentOf" /> records.
    ///     <para>
    ///         ⚠ Only before an item the front end flagged <see cref="LineFlags.DelimitedItem" /> —
    ///         measured: the oracle breaks before a 133-column binary chain that fits nowhere — and an
    ///         embedded statement is never flagged: one that has no room is pushed off and then chopped,
    ///         never left as <c>if (c) Frobnicate(</c> (SK-DIV-0106). A type argument is flagged
    ///         (SK-DIV-0114): <c>List&lt;Dictionary&lt;string,↵int&gt;&gt;</c> keeps
    ///         <c>List&lt;Dictionary&lt;</c> on its line, and its points are last-resort ones too, so the
    ///         flag is read on those.
    ///     </para>
    /// </remarks>
    /// <param name="headStays">
    ///     Whether the point stays flat because the item fits nowhere whole and its head stays on the
    ///     line, rather than because the item fits.
    /// </param>
    bool FillPointStaysFlat(
        int node,
        int group,
        LineFlags flags,
        Stack<(int Node, int Child)> stack,
        out bool headStays
    ) {
        headStays = false;
        var width = pendingSpace ? PendingWidth : (flags & LineFlags.FlatSpace) != 0 ? 1 : 0;
        var column = atLineStart
            ? pendingCloserLevel ?? Effective()
            : this.column + width;
        var (segment, head) = FillSegment(node, group, flags, stack);
        if (Fits(column, segment)) {
            return true;
        }

        // ⚠ A chain's call link keeps its head and chops its arguments unless, moved down, its line ends
        // well short of the margin (#484, SK-DIV-0129). See LineFlags.ChainCallLink.
        // ⚠ An argument list that is certain to break — the oracle's own chopped answer read back on pass
        // two — keeps the head too, or pass two would move down what pass one kept.
        if ((flags & LineFlags.ChainCallLink) != 0 && segment >= Document.Unbounded && head < segment) {
            headStays = Fits(column, head);
            return headStays;
        }

        if ((flags & LineFlags.ChainCallLink) != 0 && head < segment) {
            var room = (flags & LineFlags.ChainCallOneArgument) != 0 ? 30 : 48;
            // ⚠ The chain's own continuation scope is on the stack by now — the point is inside the
            // group — so ContinuationColumn, written for a group being entered, would count it twice.
            var below = ContinuationColumn(group)
                - (document.FactsOf(group).SpendsIndent ? continuousMultiplier * indentWidth : 0);
            headStays = Fits(column, head) && below + segment > this.width - room;
            return headStays;
        }

        // ⚠ An identifier-headed tuple item keeps its head only when the break inside it is certain
        // — the segment is unbounded — and moves whole when it is merely too wide; a delimited item
        // keeps its head either way. See LineFlags.KeepsHeadWhenCertain (SK-DIV-0114).
        var headMayStay = (flags & LineFlags.DelimitedItem) != 0
            || (flags & LineFlags.KeepsHeadWhenCertain) != 0
            && segment >= Document.Unbounded;

        if (head >= segment || !headMayStay) {
            return false;
        }

        headStays = !Fits(ContinuationColumn(group), segment) && Fits(column, head);
        return headStays;
    }

    bool Fits(int column, int width) => width < Document.Unbounded && column + width <= this.width;

    /// <summary>The whole width after a fill point and the width to the item's first breakable place.</summary>
    /// <remarks>
    ///     ⚠ At the group's last point the segment ends where the group does, and the line does not —
    ///     so what follows the group counts, exactly as it does when a group is resolved on entry.
    ///     Without it a 121-column <c>for</c> header and a 121-column <c>if</c> condition both measure
    ///     118 and decline the break the oracle takes; the missing three columns are the <c>) {</c>. See
    ///     <see cref="LineFlags.LastPoint" />.
    /// </remarks>
    (int Segment, int Head) FillSegment(int node, int group, LineFlags flags, Stack<(int Node, int Child)> stack) {
        // ⚠ An array initializer's element is measured flat with its kept breaks read as spaces and up
        // to a moved comment's first line — the oracle's measure, and the same number on every pass
        // (#444, SK-DIV-0208). See DocumentBuilder's `draft`.
        var segment = (flags & LineFlags.ArrayElement) != 0 ? document.DraftSegmentOf(node) : document.SegmentOf(node);
        var head = document.SegmentHeadOf(node);
        if ((flags & LineFlags.LastPoint) == 0) {
            return (segment, head);
        }

        var trailing = TrailingAfterGroup(stack, group);
        var whole = head == segment;
        segment = segment >= Document.Unbounded || trailing >= Document.Unbounded
            ? Document.Unbounded
            : segment + trailing;

        return (segment, whole ? segment : head);
    }

    /// <summary>
    ///     Turns an <see cref="IndentKind.Align" /> scope that is about to align to an empty column into
    ///     the continuation level it stands in for.
    /// </summary>
    /// <remarks>
    ///     ⚠ Only the innermost scope, only on its own opening line, and only while the column is still
    ///     the anchor's. A piece written after the anchor — the <c>(</c> of <c>while ((</c>, an operand —
    ///     moves <c>column</c> past <see cref="Scope.Level" /> and the alignment stands; a scope opened
    ///     on an earlier line has content under its column already. The pending space is counted on
    ///     both sides because <see cref="CurrentColumn" /> counted it when the scope was pushed.
    ///     <para>
    ///         The replacement is a block at <c>CloserLevel</c> plus one continuation, which is what the
    ///         alignment replaced: <c>CloserLevel</c> is the level the scope opened at, and the closing
    ///         delimiter still returns to it. It is a block and not a relative scope because the alignment
    ///         it replaces was one — "absolute, and nothing below it applies" — and the one measurement
    ///         that separates the two, <c>continuous_indent_multiplier = 2</c>, is satisfied either way.
    ///     </para>
    /// </remarks>
    void DemoteEmptyAlignment() {
        if (scopes.Count == 0) {
            return;
        }

        var scope = scopes[^1];
        if (!scope.IsAlignment || scope.OpenLine != line || scope.Level != column + PendingWidth) {
            return;
        }

        scopes[^1] = scope with {
            Level = scope.CloserLevel + continuousMultiplier * indentWidth, IsAlignment = false, AlignedCloser = -1
        };
    }

    /// <summary>
    ///     <c>disable_indenter</c>: writes the leading whitespace the author gave this piece, if any.
    /// </summary>
    /// <remarks>
    ///     ⚠ Two halves, and only the second makes it a suppression. A piece that began a line in the
    ///     input keeps the run of spaces and tabs that stood in front of it, byte for byte. A piece
    ///     that did <em>not</em> begin a line in the input is on a line the wrapping created and has
    ///     no leading whitespace of its own anywhere — so what it gets instead is the break point's
    ///     own flat rendering, and nothing else. Synthetic pieces, whose span is empty, are that
    ///     second case as well.
    ///     <para>
    ///         ⚠ "The point's flat rendering" is measured, and the first reading of it — column zero —
    ///         was wrong on two thirds of the shapes. Under this key <c>jb cleanupcode</c> writes a created
    ///         line as:
    ///         <code>
    /// var chopped = Compute(
    /// alpha + beta,          ← nothing: the point after `(` renders as nothing
    ///  epsilon + zeta        ← one space: the point after `,` renders as a space
    /// );                     ← nothing: the point before `)` renders as nothing
    ///
    /// var value = Compute(a)
    ///  + Compute(b)          ← one space: the point before a binary operator renders as a space
    ///         </code>
    ///         which is exactly <see cref="LineFlags.FlatSpace" />, the flag the writer already carries
    ///         for the flat case. The indenter being off does not delete the gap the break replaced; it
    ///         deletes the indentation that would otherwise have stood in front of it.
    ///     </para>
    ///     <para>
    ///         ⚠ Only the emission is suppressed. <see cref="Effective" /> keeps answering with the
    ///         indentation the rules <em>would</em> have written, so every group is still fitted against
    ///         it. Which of the two columns <c>jb cleanupcode</c> measures its margin against under this
    ///         key is unmeasured, and the alternative would be a second rule invented from the same probe.
    ///     </para>
    /// </remarks>
    void WriteSuppressedIndent(SourceSpan source) {
        column = 0;
        if (source.Length == 0 || source.Start > this.source!.Length) {
            CreatedLineGap();
            return;
        }

        var start = source.Start;
        while (start > 0 && this.source[start - 1] is ' ' or '\t') {
            start--;
        }

        if (start > 0 && this.source[start - 1] is not ('\n' or '\r')) {
            CreatedLineGap();
            return;
        }

        for (var i = start; i < source.Start; i++) {
            output.Append(this.source[i]);
        }

        column = TextWidth.Advance(this.source[start..source.Start], 0);
        return;

        void CreatedLineGap() {
            if (!createdLineSpace) {
                return;
            }

            output.Append(' ');
            column = 1;
        }
    }

    /// <summary>The columns the pending gap will occupy, which is 1 unless it is a preserved run.</summary>
    int PendingWidth => !pendingSpace ? 0 : pendingSpaceText is null ? 1 : TextWidth.Measure(pendingSpaceText);

    /// <summary>Writes the pending gap — one space, or the author's own run under `disable_space_changes`.</summary>
    void FlushPendingSpace() {
        if (!pendingSpace) {
            return;
        }

        var gap = pendingSpaceText ?? " ";
        output.Append(gap);
        column = TextWidth.Advance(gap, column);
        pendingSpace = false;
        pendingSpaceText = null;
    }

    void WritePiece(string text, SourceSpan source, VerbatimFlags flags, string? sourceLineIndent = null) {
        // ⚠ Not realigned while the indenter is off. A raw literal's interior lines move only to
        // follow its opening quotes, and under this key the opening quotes did not move.
        if ((flags & VerbatimFlags.Realign) != 0 && this.source is null) {
            text = Realign(
                text,
                atLineStart ? pendingCloserLevel ?? Effective() : column + PendingWidth
            );
        } else if ((flags & VerbatimFlags.RealignToIndent) != 0 && this.source is null) {
            // ⚠ The indentation of the line the opening quotes are on, plus one level — not the
            // level the scope stack is at. A literal opened part-way along `var a = """` takes the
            // line's own indent, and the two differ whenever a continuation scope is open.
            text = Realign(
                text,
                (atLineStart ? pendingCloserLevel ?? Effective() : CurrentLineIndent()) + indentWidth
            );
        }

        if (atLineStart) {
            if ((flags & VerbatimFlags.AtColumnZero) == 0 && (flags & VerbatimFlags.SelfIndented) == 0) {
                if (this.source is null) {
                    // ⚠ A closing delimiter's column is its scope's `CloserLevel`, which is a level and
                    // never an alignment column — so the level column and the target coincide and the
                    // whole indent is written in whole units. Only the `Effective` branch can differ.
                    var closer = pendingCloserLevel;
                    WriteIndentTo(closer ?? Effective(), closer ?? LevelColumn());
                } else {
                    WriteSuppressedIndent(source);
                }
            }

            atLineStart = false;
            pendingSpace = false;
            pendingSpaceText = null;
            pendingCloserLevel = null;
        } else if (pendingCloserLevel is not null) {
            pendingCloserLevel = null;
            FlushPendingSpace();
        } else {
            FlushPendingSpace();
        }

        // ⚠ After the indentation, because the target is the line this comment is now on — and that
        // line's leading whitespace is only in the output once it has been written. Not while the
        // indenter is off: the line did not move, so neither does the comment.
        if ((flags & VerbatimFlags.ShiftWithLine) != 0 && sourceLineIndent is not null && this.source is null) {
            text = ShiftWithLine(text, sourceLineIndent, CurrentLinePrefix(), indentUnit == "\t");
        } else if ((flags & VerbatimFlags.AlignStarred) != 0 && this.source is null) {
            // ⚠ The opening delimiter's own column plus one — measured, and it is the *opener's*
            // column rather than the code's indent, which is why a block comment that begins on a
            // code line puts its asterisks 26 columns in rather than 5. Read here, after the
            // indentation and the gap are written, where `column` is the opener's.
            text = AlignStarred(text, column + 1, CurrentLinePrefix(), indentUnit == "\t");
        }

        var start = output.Length;
        output.Append(text);
        column = TextWidth.Advance(text, column);

        if (hasPendingAnchor) {
            anchors.Add(new(pendingAnchorSpan, start, output.Length, pendingAnchorToken));
            hasPendingAnchor = false;
        }

        // A piece that ends with a newline (a multi-line comment written verbatim never does, but a
        // disabled #if block does) leaves the writer at the start of a line.
        if (text.Length > 0 && (text[^1] == '\n' || text[^1] == '\r')) {
            atLineStart = true;
            column = 0;
        }

        // A multi-line piece — a raw string, a disabled block — moves the line counter with it, so
        // that scopes opened before it still know which side of a break they are on.
        // ⚠ …but a block comment's lines do not. The oracle nests what follows `= /* a` / `b */` from the
        // statement's line, as if the comment were one line: `b */ Compute(` chops its arguments one
        // level in and its `)` at the statement, `b */ x switch {` puts the arms one level in, and a
        // chain broken after it lines its dots up one level in. Counted as a new line, the `=`'s
        // continuation scope, opened on the comment's first line, applied to all three (#435). These
        // two flags are the moved block comments; a frozen starred one keeps its column and counts.
        if ((flags & (VerbatimFlags.ShiftWithLine | VerbatimFlags.AlignStarred)) != 0) {
            return;
        }

        line += text.AsSpan().Count('\n');
    }
}
