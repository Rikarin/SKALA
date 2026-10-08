using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Core.Diagnostics;
using Rikarin.Skala.Options;
using System.Text;

namespace Rikarin.Skala.Formatting.CSharp;

/// <summary>The document, plus what building it had to say.</summary>
/// <param name="LineQuestions">
///     Every node a blank-line rule asked "does this occupy one output line", with the answer it went on;
///     see <see cref="OutputLines" />.
/// </param>
public sealed record BuiltDocument(
    Document Document,
    IReadOnlyList<SkalaDiagnostic> Diagnostics,
    IReadOnlyList<LineQuestion>? LineQuestions = null);

/// <summary>
///     Turns a parsed C# file into the language-agnostic document IR.
/// </summary>
/// <remarks>
///     The walk is structural — one pass over the syntax tree — with an ordered piece stream
///     (<see cref="SourcePieces" />) threaded through it, so that a comment between two tokens is
///     emitted at the nesting the tokens around it establish rather than at whichever token Roslyn
///     happened to attach it to. That is what docs/plan/04 means by "which token owns this comment must
///     be a decision Skala makes, not one it inherits".
///     <para>
///         ⚠ Milestone 1 never moves a line to fit a width. Every gap that held a line break still holds
///         one and every gap that did not, does not — except where a brace rule joins one, which is a
///         decision rather than a fit.
///     </para>
/// </remarks>
public sealed partial class CSharpDocumentBuilder {
    readonly SourceText text;
    readonly string source;
    readonly Piece[] pieces;
    readonly SyntaxToken[] tokens;
    readonly PhaseOneOptions options;
    readonly DocumentBuilder doc = new();
    readonly List<SkalaDiagnostic> diagnostics = [];
    readonly List<int> blockStack = [];

    /// <summary>One per open statement, member, accessor or call chain, and per block scope.</summary>
    readonly List<Frame> frames = [];

    readonly HashSet<int> verbatimMembers = [];

    /// <summary>
    ///     The expressions a <c>[CallerArgumentExpression]</c> parameter may capture, emitted
    ///     byte-for-byte (#432). See <see cref="CapturedArguments" />.
    /// </summary>
    readonly HashSet<TextSpan> captured;

    readonly string path;
    BreakPlan plan = null!;

    int cursor;
    int lastPiece = -1;

    /// <summary>Where <see cref="EmitLeadingGap" /> already wrote a gap, so it is not written twice.</summary>
    int gapEmittedAt = -1;

    int verbatimUntil = -1;
    int continuousDepth;

    /// <summary>
    ///     How many continuation levels the groups of the node being entered opened themselves — the
    ///     count <see cref="VisitPlanned" /> hands <see cref="VisitInner" /> so that a chain frame can
    ///     tell its own group's level from the depth outside the chain. See <see cref="Frame.EntryDepth" />.
    /// </summary>
    int levelsOpenedByOwnGroups;

    /// <summary>
    ///     How many <c>skala_outdent_dots</c> chain scopes are open, each pulling its wrapped lines back by
    ///     a <c>.</c>'s width. See <see cref="ExtraOutdentFor" />.
    /// </summary>
    int dotOutdents;

    /// <summary>Group id to the plan that created it, built on first use by <c>GuessesSpansLines</c>.</summary>
    Dictionary<int, GroupPlan>? groupPlans;

    /// <summary>Whether each own-line comment run, by its first piece, is detached from the code under it (#494).</summary>
    Dictionary<int, bool>? detachedRuns;

    /// <summary>The run <see cref="RunIsDetached" /> is resolving the gap under, or −1.</summary>
    int probingRun = -1;

    /// <summary>
    ///     Whether each node occupies one line within the margin, read off an earlier layout of this same
    ///     file, or null on the first build. See <c>OccupiesOneLine</c>.
    /// </summary>
    readonly IReadOnlyDictionary<TextSpan, bool>? outputLines;

    readonly List<LineQuestion> lineQuestions = [];

    CSharpDocumentBuilder(
        string path,
        SourceText text,
        SyntaxNode root,
        in PhaseOneOptions options,
        IReadOnlyDictionary<TextSpan, bool>? outputLines
    ) {
        this.path = path;
        this.text = text;
        source = text.ToString();
        this.options = options;
        this.outputLines = outputLines;
        (pieces, tokens) = SourcePieces.Split(root, text);
        captured = [..CapturedArguments.Find(root)];
    }

    /// <param name="path">The file's path, for diagnostics.</param>
    /// <param name="text">The file.</param>
    /// <param name="root">Its parsed tree.</param>
    /// <param name="options">The resolved configuration.</param>
    /// <param name="outputLines">
    ///     ⚠ The answers an earlier layout of the same document gave to its
    ///     <see cref="BuiltDocument.LineQuestions" />, keyed by node span; null on the first build. See
    ///     <see cref="OutputLines" />.
    /// </param>
    public static BuiltDocument Build(
        string path,
        SourceText text,
        SyntaxNode root,
        in PhaseOneOptions options,
        IReadOnlyDictionary<TextSpan, bool>? outputLines = null
    ) {
        var builder = new CSharpDocumentBuilder(path, text, root, options, outputLines);
        builder.Run(root);
        return new(builder.doc.Build(), builder.diagnostics, builder.lineQuestions);
    }

    void Run(SyntaxNode root) {
        PreprocessorGuard.MarkUnbalancedMembers(root, text, verbatimMembers, diagnostics, path);

        // ⚠ The break plan is built before the walk, not during it: a gap can belong to two
        // constructs at once and only a pass that sees both can decide which one owns it
        // (see BreakPlan's remarks). Ids are handed out here so that the plan's numbering and the
        // document's agree.
        plan = BreakPlan.Build(root, source, options, captured);
        for (var i = 0; i < plan.GroupCount; i++) {
            doc.NextGroupId();
        }

        foreach (var planned in plan.Groups) {
            doc.DescribeGroup(planned.Id, planned.Facts);
        }

        var group = doc.NextGroupId();
        doc.OpenGroup(GroupMode.Flat, group);
        Visit(root);
        EmitUpTo(int.MaxValue);
        doc.Close();
    }

    // ── The structural walk ──────────────────────────────────────────────────────────────────

    /// <summary>
    ///     Visits a node, opening a continuation frame for the constructs a continuation break can be
    ///     attributed to.
    /// </summary>
    /// <remarks>
    ///     ⚠ The frame is what turns the one continuous indent level into a <em>scope</em> rather than a
    ///     per-line adjustment, and the difference is not cosmetic:
    ///     <code>
    /// void N() =>
    ///     Q(          ← the arrow's continuation level, +1
    ///         a,      ← and the parenthesis's, +2 — which only composes if the first is a scope
    ///     );
    ///     </code>
    /// </remarks>
    void Visit(SyntaxNode node) {
        // ⚠ #432: the compiler hands this expression's source text to a `[CallerArgumentExpression]`
        // parameter, so a moved space or a re-wrapped line changes what the program prints — the
        // interpolated string's reason for being verbatim, reached through a call instead of a
        // literal. Before every scope this node would open: its groups were never planned
        // (BreakPlan.Walk stops here too), and the gaps around it still belong to the list it sits in.
        if (IsCaptured(node)) {
            EmitVerbatim(node);
            return;
        }

        if (!AlignsFromOwnColumn(node)) {
            VisitPlanned(node);
            return;
        }

        // ⚠ The gap before the anchor is emitted *before* the scope opens, and that is the whole of
        // what makes the column right. A break in that gap belongs to whatever encloses the
        // construct, so the column to align to is the one after it — and the writer only knows that
        // column once the gap has been resolved.
        EmitLeadingGapAt(AlignAnchor(node));
        OpenIndent(IndentKind.Align, true);
        VisitPlanned(node);
        EmitUpTo(node.Span.End);
        CloseIndent(IndentKind.Align);
    }

    /// <summary>Whether <paramref name="node" /> is a captured argument's whole expression.</summary>
    /// <remarks>
    ///     ⚠ Not when a formatter tag sits inside it. The tag's region runs past the expression's end,
    ///     and only <see cref="EmitPiece" /> reaching the tag opens it; a verbatim chunk would swallow
    ///     the tag and format everything after it that the author switched off. The text before the tag
    ///     is then formatted — the narrower of the two holes, and one nobody has written.
    /// </remarks>
    bool IsCaptured(SyntaxNode node) {
        if (captured.Count == 0 || node is not ExpressionSyntax || !captured.Contains(node.Span)) {
            return false;
        }

        // Reached only for a captured argument, so the iterator is not on the per-node path.
        foreach (var trivia in node.DescendantTrivia(node.Span).Where(IsLineOrBlockComment)) {
            var comment = trivia.ToString();
            if (FormatterTagGuard.IsOffTag(comment, options.Tags) || FormatterTagGuard.IsOnTag(comment, options.Tags)) {
                return false;
            }
        }

        return true;
    }

    static bool IsLineOrBlockComment(SyntaxTrivia trivia) =>
        trivia.IsKind(SyntaxKind.SingleLineCommentTrivia) || trivia.IsKind(SyntaxKind.MultiLineCommentTrivia);

    /// <summary>The position the alignment column is read at.</summary>
    /// <remarks>
    ///     ⚠ The construct's own first token for every kind but one, and the exception is measured. An
    ///     anonymous object's node starts at <c>new</c> and the oracle aligns it to the <c>{</c>:
    ///     <code>
    /// var v = new {
    ///                 A = 1     ← the brace's column plus one level, not `new`'s
    ///             };
    ///     </code>
    ///     Every other braced construct here <em>is</em> its brace — an <c>InitializerExpressionSyntax</c>
    ///     and a <c>PropertyPatternClauseSyntax</c> both start at one — so the distinction only ever
    ///     shows on this node.
    /// </remarks>
    static int AlignAnchor(SyntaxNode node) =>
        node switch {
            AnonymousObjectCreationExpressionSyntax anonymous => anonymous.OpenBraceToken.SpanStart,

            // ⚠ The first base type, two columns past the base list's own node, and measured: with
            // `skala_align_multiline_extends_list = true` the oracle puts the second interface under the
            // first one rather than under the `:`.
            //
            //     public class Alpha : System.Collections.Generic.IReadOnlyCollection<int>,
            //                          System.IDisposable,        ← the first base type's column
            //
            // The anchor is a *position* rather than the node's start for exactly this reason;
            // EmitLeadingGapAt writes the `:` and the gap after it before the scope opens, so the
            // column the scope reads is the one the first base type lands on.
            BaseListSyntax { Types: [{ } first, ..] } => first.SpanStart,

            // ⚠ And the first type parameter, one column past the list's own node, which is the `<`.
            // `align_multiline_type_parameter_list = true`:
            //
            //     public void ManyParams<TFirstParameterName, TSecondParameterName,
            //                            TThirdParameterName>(int a) { }
            TypeParameterListSyntax { Parameters: [{ } parameter, ..] } => parameter.SpanStart,

            // ⚠ The first declarator, past the type. A VariableDeclarationSyntax starts at its type
            // and the oracle aligns the second declarator under the first one's name.
            VariableDeclarationSyntax { Variables: [{ } declarator, ..] } => declarator.SpanStart,

            _ => node.SpanStart
        };

    /// <summary>
    ///     Whether an <c>align_multiline_*</c> key anchors this construct to the column its own first
    ///     token lands on, rather than to an indent level of the line it is on.
    /// </summary>
    /// <remarks>
    ///     ⚠ One rule for six constructs, and the anchor is the <em>node's</em> start rather than the
    ///     opening delimiter's — which is the same token for four of them and is not for the other two.
    ///     A switch expression starts at its governing expression and an initializer starts at its
    ///     brace, and the oracle aligns each to its own node:
    ///     <code>
    /// var r = v switch {              var v = new SomeType {
    ///             1 => "a",                                    A = 1,
    ///             _ => "b"                                     B = 2
    ///         };                                           };
    ///     </code>
    ///     Both columns fall out of "the construct's first token" and neither falls out of "the brace".
    ///     <para>
    ///         ⚠ Every key here is <c>false</c> in the export, so this returns false for every file the
    ///         fidelity number is measured over. That is not an argument for guessing at the shape: the
    ///         columns above are the oracle's, asked at a 70-column margin with one key flipped at a time.
    ///     </para>
    /// </remarks>
    bool AlignsFromOwnColumn(SyntaxNode node) =>
        node switch {
            InitializerExpressionSyntax or AnonymousObjectCreationExpressionSyntax =>
                options.AlignMultilineArrayAndObjectInitializer,
            CollectionExpressionSyntax or ListPatternSyntax => options.AlignMultilineListPattern,
            PropertyPatternClauseSyntax => options.AlignMultilinePropertyPattern,
            SwitchExpressionSyntax => options.AlignMultilineSwitchExpression,
            QueryExpressionSyntax => options.AlignLinqQuery,
            BinaryExpressionSyntax =>
                options.AlignMultilineBinaryExpressionsChain && BreakPlan.IsChainRootOperator(node),
            BinaryPatternSyntax => options.AlignMultilineBinaryPatterns && BreakPlan.IsChainRootOperator(node),
            BaseListSyntax { Types.Count: > 0 } => options.AlignMultilineExtendsList,

            // ⚠ `align_multiline_calls_chain` is deliberately absent, and the reason is a *layout*
            // dependency rather than a missing scope. Its anchor is the column the chain's first `.`
            // lands on, which is not a position in the source: at 120 columns
            // `skala_wrap_before_first_method_call = false` keeps `.Where(…)` on the head line and the rest
            // align under that dot, 26 columns past the receiver's start; at 70 the first call no
            // longer fits, the layout breaks before that dot too, and the anchor becomes the
            // receiver's own column. AlignAnchor is a source position resolved before the fitter
            // runs, so it can produce one of those two answers and never the other. Measured on both
            // margins, and the first reading of it — "the anchor is the chain's own first token" —
            // was a 70-column measurement mistaken for the rule. See PhaseOneOptions.
            //
            // ⚠ A local declaration's declarators, and not a field's. The oracle moves
            // `System.Int32 a = 1,\n             b = 2` to the first declarator's column and leaves
            // the identical field declaration on its continuation indent, at both values.
            VariableDeclarationSyntax { Variables.Count: > 1, Parent: not FieldDeclarationSyntax } =>
                options.AlignMultipleDeclaration,

            // ⚠ A type parameter list is not here: its anchor is past its own first break point, the gap
            // after the `<`, so the scope opens inside the list's group — see AlignsTypeParameters.
            _ => false
        };

    /// <summary>
    ///     Several attributes in one section line up under the first one, and no key says so
    ///     (SK-DIV-0114): <c>[Obsolete,\n Serializable]</c> one column past the bracket,
    ///     <c>[return: Obsolete,\n CLSCompliant(true)]</c> under <c>Obsolete</c>, at every value of every
    ///     <c>align_*</c> key in the export. See <c>BreakPlan.PlanAttributeList</c>.
    /// </summary>
    /// <remarks>
    ///     ⚠ Not after <c>[\n</c>: there is nothing on the bracket's line to align to and the oracle
    ///     gives the attributes the bracket's continuation level. The source is the right witness,
    ///     because nothing plans a break after a <c>[</c> — a kept one is the only kind there is.
    ///     <para>
    ///         ⚠ The scope is the tuple-components one — opened inside <see cref="VisitDelimited" /> once
    ///         the first attribute's column is known, and closed before the <c>]</c> — rather than
    ///         <see cref="Visit" />'s, which wraps the whole node: the oracle puts a <c>]</c> the author
    ///         gave a line of its own back on the owner's indent, and a scope around the node cannot
    ///         let the bracket out.
    ///     </para>
    /// </remarks>
    /// <summary>
    ///     Three of the lists BreakPlan.PlanFilledList plans — filled, with no wrap style of their own —
    ///     and the three whose kept closer was measured (#443). An array rank and a function pointer's
    ///     parameter list are filled the same way and were not asked.
    /// </summary>
    static bool IsOnlyFilled(SyntaxNode node) =>
        node is TupleExpressionSyntax or PositionalPatternClauseSyntax or ParenthesizedVariableDesignationSyntax;

    static bool IsAnAlignedAttributeSection(SyntaxNode node, string source) =>
        node is AttributeListSyntax { Attributes: [{ } first, ..] } attributes
        && attributes.Attributes.Count > 1
        && !BreakPlan.BreaksBeforeIn(source, first.GetFirstToken());

    void VisitPlanned(SyntaxNode node) {
        var planned = plan.GroupsOf(node);
        if (planned.Count == 0) {
            levelsOpenedByOwnGroups = 0;
            VisitInner(node);
            return;
        }

        var aligned = AlignsFromOwnColumn(node);

        // ⚠ The gap *before* the construct is emitted first, outside the group. It belongs to
        // whatever encloses the construct, and a group that swallows it is measured wrong twice
        // over: the break makes its flat width infinite, so it can never be flat, and the column it
        // is entered at is the one before that break rather than the one after it. The symptom is a
        // statement-level group that never joins and never fits — `if (flag)\n First();` measured as
        // starting at column 23 on the previous line.
        //
        // ⚠ Unless the group says otherwise: a construct whose own first break point *is* that gap
        // has to own it. See GroupPlan.LeadingGapInside.
        //
        // ⚠ And the claim is per plan rather than per node, which is what lets two groups over one
        // construct ask their question at two different columns. A base list's outer group owns the
        // gap before the `:` and asks "does the whole list fit where the declaration reached"; its
        // inner group opens *after* that gap and asks "do the types fit on the line the first one
        // lands on". Emitting the gap before both would enter the inner one at the column before the
        // break, and `skala_wrap_before_extends_colon = true` would chop every comma of a list the oracle
        // leaves whole. Plans are outermost-first, so the claimants are a prefix of them.
        var gapAfter = 0;
        while (gapAfter < planned.Count && planned[gapAfter].LeadingGapInside) {
            gapAfter++;
        }

        if (gapAfter == 0) {
            EmitLeadingGap(node);
        }

        // ⚠ Outermost first, and every one of them opened before the body. Two constructs can start
        // and end at the same token — a binary chain and its outermost operator — and the outer one
        // has to be the outer group or the fitter resolves the inner first and the ordering is
        // inverted.
        var indented = new int[planned.Count];
        var heldLevels = new bool[planned.Count];
        for (var i = 0; i < planned.Count; i++) {
            var plan = planned[i];
            doc.OpenGroup(plan.Mode, plan.Id);

            // ⚠ The continuation scope a group's own break points need is opened here, inside the
            // group and closed inside it, rather than lazily at the break. Lazily is what milestone
            // 1 did and it is fine while the document stack holds nothing but indent scopes; a group
            // on the same stack closes before the statement that owns the frame does, and pops the
            // indent instead of itself.
            // ⚠ An aligned construct spends no level of its own: the Align scope around it is an
            // absolute column and its contents start there. `OwnLevel` has to go too — a binary
            // pattern chain takes an extra level everywhere else, and under alignment the oracle
            // puts its operands on the pattern's own column and not one indent past it.
            indented[i] = aligned
                ? 0
                : (plan.SpendsIndent && CanSpendAContinuationLevel(node, plan.SpendsUnderDelimiters) ? 1 : 0)
                + (plan.OwnLevel && !HeaderPaysForTheOwnLevel() ? 1 : 0);

            // ⚠ A held level is spent, as zero columns: the scope is a marker the writer adds nothing
            // for, counted as a continuation so that no frame further in spends the level the group
            // has taken. See GroupPlan.HoldsLevel. The fitter is told the level is not spent, because
            // the column a break lands on is the owner's.
            var held = plan.HoldsLevel == HeldLevel.Always && indented[i] > 0;
            if (held) {
                indented[i] = 0;
            }

            // ⚠ Whether the level is actually spent is decided here and not in the plan, and the
            // fitter needs the answer: the ordering rule asks what column a break inside this group
            // lands on, and that is one level deeper only when this group is the one paying for it.
            doc.DescribeGroup(
                plan.Id,
                plan.Facts with {
                    SpendsIndent = indented[i] > 0 && plan.HoldsLevel != HeldLevel.WhileChainWhole,
                    Continues = plan.Facts.Continues && !aligned
                }
            );

            if (held) {
                HoldContinuationLevel();
                heldLevels[i] = true;
            }

            for (var level = 0; level < indented[i]; level++) {
                // ⚠ Every own level lifts — a chain's and a property fill's (#482) alike. The property
                // fill was a frame's level before #482 gave it a group, and the frame's lifted (#481):
                // `+ (meshlet` / `.TriangleCount` / `* 3)` puts the dot two levels past the `+`.
                OpenContinuation(plan, level, plan.OwnLevel);
            }

            // ⚠ One level past the operand's line, not stacked on what that line opened (#445).
            if (plan.FromLine && !aligned) {
                OpenIndent(IndentKind.FromLine);
            }

            if (i + 1 == gapAfter) {
                EmitLeadingGap(node);
            }
        }

        // ⚠ Innermost of everything this node opens, and that is load-bearing rather than tidy.
        // LayoutWriter.Level walks the stack innermost-first and returns at the first block, and an
        // Align scope is a block — so an outdent opened *outside* one would never be reached. Inside
        // it, the two compose, which is what the oracle does: with `align_multiline_expression` and
        // `skala_outdent_binary_ops` both on, the operands take the expression's own column and the
        // operators sit two to the left of it.
        var outdent = OutdentColumnsFor(node);
        var dotOutdent = outdent > 0 && BreakPlan.IsChainRoot(node);
        if (outdent > 0) {
            OpenIndent(IndentKind.OutdentColumns, columns: outdent);
        }

        if (dotOutdent) {
            dotOutdents++;
        }

        levelsOpenedByOwnGroups = indented.Sum() + heldLevels.Count(static held => held);
        SpaceIfTheCollectionBreaks(node, planned);
        VisitInner(node);
        EmitUpTo(node.Span.End);

        if (dotOutdent) {
            dotOutdents--;
        }

        if (outdent > 0) {
            CloseIndent(IndentKind.OutdentColumns);
        }

        for (var i = planned.Count - 1; i >= 0; i--) {
            if (planned[i].FromLine && !aligned) {
                CloseIndent(IndentKind.FromLine);
            }

            for (var level = 0; level < indented[i]; level++) {
                CloseIndent(IndentKind.Continuous);
            }

            if (heldLevels[i]) {
                ReleaseContinuationLevel();
            }

            doc.Close();
        }
    }

    /// <summary>
    ///     One space in front of a collection expression's <c>[</c> behind a cast's <c>)</c> or a
    ///     parenthesis's <c>(</c>, exactly when the collection breaks.
    /// </summary>
    /// <remarks>
    ///     ⚠ #450 and #485 (SK-DIV-0012, SK-DIV-0150). `(Kind[])[a, b]` and `([1, 2])` stay closed at the
    ///     export, and the same collections chopped come back `(Kind[]) [` and `( [`, at both values of
    ///     <c>space_after_cast</c> and <c>space_within_parentheses</c> — the gap is the key's while the
    ///     collection is flat and one space once it breaks. The space rules cannot see a resolved mode, and
    ///     the gap is written before the collection's group opens, so the space is an
    ///     <see cref="DocKind.IfBroken" /> placed as the group's first child, where the writer has already
    ///     resolved it. ⚠ A space written there after a break the gap took is dropped by the writer, which
    ///     never writes a pending space at a line's start. Only the innermost parenthesis: `(([` gives
    ///     `(( [`.
    /// </remarks>
    void SpaceIfTheCollectionBreaks(SyntaxNode node, IReadOnlyList<GroupPlan> planned) {
        if (node is not CollectionExpressionSyntax { Elements.Count: > 0 } collection
            || options.DisableSpaceChanges
            || planned.Count == 0) {
            return;
        }

        var before = collection.OpenBracketToken.GetPreviousToken();
        if (!(before.IsKind(SyntaxKind.OpenParenToken)
                && before.Parent is ParenthesizedExpressionSyntax
                || before.IsKind(SyntaxKind.CloseParenToken)
                && before.Parent is CastExpressionSyntax)) {
            return;
        }

        doc.OpenIfBroken(planned[^1].Id);
        doc.Space(SpaceKind.Required);
        doc.OpenConcat();
        doc.Close();
        doc.Close();
    }

    /// <summary>
    ///     Spends a continuation level as zero columns: a <see cref="IndentKind.None" /> scope the writer
    ///     adds nothing for, counted in <c>continuousDepth</c> so that nothing further in spends the
    ///     level again. See <see cref="GroupPlan.HoldsLevel" />.
    /// </summary>
    void HoldContinuationLevel() {
        doc.OpenIndent(IndentKind.None);
        continuousDepth++;
    }

    void ReleaseContinuationLevel() {
        continuousDepth--;
        doc.Close();
    }

    /// <summary>
    ///     How many columns left the <c>outdent_*</c> family moves every wrapped line of this
    ///     construct, or zero.
    /// </summary>
    /// <remarks>
    ///     ⚠ One arithmetic for three keys: the width of the operator that starts a wrapped line, plus
    ///     the space written after it. That is the offset which leaves the <em>operand</em> on the
    ///     column it would have taken unmoved, and it is what the oracle writes at a 70-column margin
    ///     with one key flipped at a time — <c>+</c> 12 → 10, <c>&amp;&amp;</c> 12 → 9, <c>and</c>
    ///     12 → 8, <c>.</c> 12 → 11.
    ///     <para>
    ///         ⚠ The space is asked of <see cref="SpaceRules" /> rather than assumed. It is the whole of why
    ///         the dot moves by one and the operators by their width plus one:
    ///         <c>space_after_dot = false</c> in this export, and a configuration that sets it true moves
    ///         the dots by two. Hard-coding "width plus one" would be right three times out of four and
    ///         silently wrong on the fourth.
    ///     </para>
    ///     <para>
    ///         ⚠ Guarded on the key that decides which side of the operator the break lands on.
    ///         <c>skala_wrap_before_binary_opsign = false</c> leaves the operator at the end of the previous line
    ///         and there is then nothing at the head of a line to outdent; the oracle agrees, and returns
    ///         such a file byte-identical at both values.
    ///     </para>
    /// </remarks>
    int OutdentColumnsFor(SyntaxNode node) {
        var op = OutdentToken(node);
        if (op.IsKind(SyntaxKind.None)) {
            return 0;
        }

        return op.Text.Length
            + (SpaceRules.Decide(op, op.GetNextToken(), options) == SpaceKind.Required ? 1 : 0);
    }

    /// <summary>The operator a wrapped line of this construct starts with, or <c>default</c>.</summary>
    /// <remarks>
    ///     ⚠ One token for the whole scope, and the assumption that makes that sound is that a chain's
    ///     links are the same width. C# has no two same-precedence binary operators of different widths
    ///     — a relational chain does not type-check, and <c>and</c>/<c>or</c> are different precedences
    ///     and so are different chains — so a chain-wide amount is a chain-wide fact. The exception the
    ///     grammar does allow is a mixed <c>a?.B().C()</c>, whose first dot is two columns and whose
    ///     rest are one; the chain root's own dot is what is taken, which is the outermost link.
    /// </remarks>
    SyntaxToken OutdentToken(SyntaxNode node) {
        switch (node) {
            case BinaryExpressionSyntax binary
                when options.OutdentBinaryOps
                && options.WrapBeforeBinaryOpsign
                && BreakPlan.IsChainRootOperator(binary):
                return binary.OperatorToken;

            case BinaryPatternSyntax pattern
                when options.OutdentBinaryPatternOps
                && options.WrapBeforeBinaryPatternOp
                && BreakPlan.IsChainRootOperator(pattern):
                return pattern.OperatorToken;

            case InvocationExpressionSyntax or MemberAccessExpressionSyntax or ConditionalAccessExpressionSyntax
                when options.OutdentDots
                && !options.WrapAfterDotInMethodCalls
                && BreakPlan.IsChainRoot(node):
                return node switch {
                    InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax access } =>
                        access.OperatorToken,
                    MemberAccessExpressionSyntax property => property.OperatorToken,
                    InvocationExpressionSyntax { Expression: MemberBindingExpressionSyntax binding } =>
                        binding.OperatorToken,
                    ConditionalAccessExpressionSyntax conditional => conditional.OperatorToken,
                    _ => default
                };

            default:
                return default;
        }
    }

    /// <summary>
    ///     Whether a continuation level is this group's to spend.
    /// </summary>
    /// <remarks>
    ///     ⚠ <c>continuous_line_indent = single</c> as docs/plan/04 § "Indentation" corrects it: a
    ///     delimited group already open supplies the level, and an undelimited continuation that is
    ///     already inside another one adds nothing. <c>M(\n a\n + b)</c> takes the parenthesis's level
    ///     and not a second one.
    /// </remarks>
    /// <param name="underDelimiters">
    ///     <see cref="GroupPlan.SpendsUnderDelimiters" />: an open delimited scope does not refuse the
    ///     level. The frame test below still applies — a frame that has already spent its level does
    ///     not spend it twice.
    /// </param>
    bool CanSpendAContinuationLevel(SyntaxNode? node = null, bool underDelimiters = false) {
        if (node is not null && IsBinaryChainElement(node)) {
            return false;
        }

        if (continuousDepth != 0 && !underDelimiters) {
            return false;
        }

        for (var i = frames.Count - 1; i >= 0; i--) {
            if (!frames[i].Started) {
                continue;
            }

            return !frames[i].Activated;
        }

        return true;
    }

    /// <summary>
    ///     A binary chain that <em>is</em> a braced initializer's or a collection expression's element.
    /// </summary>
    /// <remarks>
    ///     ⚠ SK-DIV-0040. A binary expression chain has no continuation level of its own — it lands on
    ///     the one the construct around it opened, which is what <c>PlanChainWide</c>'s
    ///     <c>spendsIndent: pattern</c> says and what <see cref="CanSpendAContinuationLevel" /> normally
    ///     enforces through <c>_continuousDepth</c>. Inside braces that test cannot see it:
    ///     <see cref="VisitBraced" /> opens an <see cref="IndentKind.Block" /> scope, which resets the
    ///     depth, so a chain that <em>is</em> an element believes it is the first continuation on the
    ///     line and takes a second one. Measured against the oracle, with the element already at 12:
    ///     <code>
    /// var flags = new[] {
    ///     FirstCondition
    ///     &amp;&amp; SecondCondition     ← 12, and Skala wrote 16
    /// };
    ///     </code>
    ///     ⚠ The test is the chain's <em>root</em>, not the operator node. Refusing only the outermost
    ///     operator hands the level to the next one down, which puts every operator but the last at 16.
    ///     <para>
    ///         ⚠ Elements only. A chain the element merely contains — <c>Name = a &amp;&amp; b</c>,
    ///         <c>1 =&gt; a &amp;&amp; b</c> — does take a level, because there the element's own column is
    ///         not where the chain begins; and a call chain or a pattern chain in the same position takes
    ///         one too, which is why this is a test on <see cref="BinaryExpressionSyntax" /> and not on the
    ///         position alone.
    ///     </para>
    /// </remarks>
    static bool IsBinaryChainElement(SyntaxNode node) {
        // ⚠ A collection expression's element has the same span as the expression inside it, and
        // groups are keyed by span — so the chain's own groups are reached through both nodes and
        // the test has to answer alike for the two. Reached through the element and answered `false`,
        // the level is spent there and the chain never gets the chance to decline it.
        var expression = node is ExpressionElementSyntax element ? element.Expression : node;
        if (expression is not BinaryExpressionSyntax) {
            return false;
        }

        var root = expression;
        while (!BreakPlan.IsChainRootOperator(root) && root.Parent is not null) {
            root = root.Parent;
        }

        SyntaxNode? owner = root.Parent is CollectionElementSyntax outer ? outer.Parent : root.Parent;
        return owner is InitializerExpressionSyntax or CollectionExpressionSyntax;
    }

    /// <summary>Emits everything before <paramref name="node" />'s first piece, its gap included.</summary>
    void EmitLeadingGap(SyntaxNode node) => EmitLeadingGapAt(node.SpanStart);

    /// <summary>The first element inside a braced construct, or the closing brace when it is empty.</summary>
    static int FirstElementStart(SyntaxNode node) {
        var seenOpen = false;
        foreach (var child in node.ChildNodesAndTokens()) {
            if (!seenOpen) {
                seenOpen = child.IsToken && child.AsToken().IsKind(SyntaxKind.OpenBraceToken);
                continue;
            }

            return child.SpanStart;
        }

        return node.Span.End;
    }

    void EmitLeadingGapAt(int position) {
        EmitUpTo(position);
        if (cursor >= pieces.Length || lastPiece < 0) {
            return;
        }

        var piece = pieces[cursor];

        // ⚠ Nested groups can start at the same token — a binary chain and its leftmost operand,
        // an invocation and the member access inside it — and the gap before that token is one gap.
        // Emitting it twice writes two breaks and, worse, opens a continuation scope that is closed
        // once.
        if (piece.Span.Start < verbatimUntil || piece.Span.Start == gapEmittedAt) {
            return;
        }

        EmitGap(
            cursor,
            piece.Kind,
            piece.Span.Start,
            piece.Kind == PieceKind.Token ? tokens[piece.TokenIndex] : default
        );
        gapEmittedAt = piece.Span.Start;
    }

    void VisitInner(SyntaxNode node) {
        // ⚠ A chained method call takes a continuation level of its own; a binary chain in the same
        // position does not. Verified against the oracle, because the two look identical on paper:
        //   Q(                         Q(
        //       a                          new[] { … }
        //       + b,          vs               .Select(…)   ← one level deeper
        //       c);                            .ToArray(),
        //                              c);
        // The level is spent lazily, at the first break before a `.`, so a chain that does not
        // break costs nothing and an argument list inside one is not pushed twice.
        // ⚠ A binary PATTERN chain takes a level of its own; a binary EXPRESSION chain does not.
        // `skala_wrap_chained_binary_patterns` and `skala_wrap_chained_binary_expressions` are separate keys and
        // ReSharper treats them differently:
        //   x is A            a
        //       or B    vs    + b     ← one level, not two
        if (IsChainRoot(node) || IsPatternChainRoot(node)) {
            frames.Add(
                new(
                    IsPatternChainRoot(node) ? FrameKind.Pattern : FrameKind.Chain,
                    false,
                    // ⚠ An aligned chain spends no continuation level of its own. The Align scope is
                    // an absolute column and everything under it starts there; adding the level the
                    // chain would otherwise pay for puts the operands one indent past the column the
                    // oracle writes them at.
                    Aligned: AlignsFromOwnColumn(node),

                    // ⚠ And neither does a call chain whose head is a parenthesised expression or a
                    // tuple: `(\n a).B\n.C()` puts `.C()` on the `(`'s own column (SK-DIV-0112). The
                    // group half of the same rule is BreakPlan.PlanChainedCalls' HoldsLevel; this is
                    // the frame half, for an author's break before a dot that is not a point.
                    HoldsLevel: IsChainRoot(node) && BreakPlan.HeadSharesTheLevelAroundIt(node),

                    // ⚠ And a chain pays its level once. The group half — PlanChainedCalls' OwnLevel —
                    // opens a continuation scope over the whole chain when the chain has points, and
                    // it is already open here, because VisitPlanned opens the node's groups before it
                    // calls VisitInner; the frame half must then not spend a second one for an
                    // author's break before a dot that is not a point. The depth *outside* the
                    // node's own groups is what tells the two apart at the break: see FrameToSpend.
                    EntryDepth: continuousDepth - levelsOpenedByOwnGroups
                )
            );
            Dispatch(node);
            if (frames[^1].Activated) {
                CloseIndent(IndentKind.Continuous);
            }

            frames.RemoveAt(frames.Count - 1);
            return;
        }

        if (!OwnsAContinuationFrame(node)) {
            // ⚠ An object creation's initializer nests from the line the creation starts on, the
            // anonymous function's rule below: `_t = new T(` / `a,` / `b` / `) {` puts the members at
            // the `=`'s level plus one and `};` on it (SK-DIV-0164).
            var anchors = AnchorsItsBlock(node);
            if (anchors) {
                EmitLeadingGap(node);
                OpenIndent(IndentKind.Anchor);
            }

            Dispatch(node);
            if (anchors) {
                EmitUpTo(node.Span.End);
                CloseIndent(IndentKind.Anchor);
            }

            return;
        }

        // ⚠ A lambda body is its own continuation context. Without the reset, a chain broken inside
        // a lambda that is itself inside an argument list sees a delimited scope already open and
        // declines the level ReSharper gives it.
        //
        // The reset is deferred to the frame's first piece, not applied here: the break that lands
        // just before the lambda is still the enclosing member's to pay for, and zeroing the depth
        // early lets the member's own level be spent inside the lambda's frame instead.
        //
        // ⚠ The depth to restore lives on the frame rather than in a local, and that is what makes
        // the deferral work at all. A lambda's own parameter opens a frame of its own, and it is the
        // parameter's first token that starts the lambda's frame and triggers the reset — so a local
        // captured before the parameter was visited puts the enclosing scope's depth back the moment
        // the parameter ends, and the body never sees the reset. Measured: `M(\n a,\n x => p\n
        // && q\n)` came out with `&&` at the argument's own level where the oracle gives it one more.
        frames.Add(
            new(
                FrameKind.Unit,
                false,
                ResetsDepth: node is AnonymousFunctionExpressionSyntax && !IsSoleLambdaArgument(node),
                SavedDepth: continuousDepth,

                // ⚠ The one break a sole lambda argument's frame pays for although a delimited scope
                // is open: the arrow, broken before, of a lambda with a block body (#488,
                // SK-DIV-0169). The oracle writes `Use((int first)` / `=> {` with the arrow two levels
                // past the statement — the parenthesis's unconditional level and the arrow's own — and
                // `=> first + 1` under the same call at one. The block keeps its anchor either way.
                PaysAt: IsSoleLambdaArgument(node) && node is LambdaExpressionSyntax { Block: not null } blockLambda
                    ? blockLambda.ArrowToken.SpanStart
                    : -1,

                // ⚠ A `where` clause's continuation lines take no level: `where T : class\n, new()`
                // puts the next constraint on the `where`'s own column, at every value of every key
                // measured (SK-DIV-0105). The frame stays — it bounds what the clause's own breaks
                // may spend — and pays for nothing, which is what an aligned frame already means.
                Aligned: node is TypeParameterConstraintClauseSyntax,
                PaysNotBefore: node is VariableDeclaratorSyntax declarator && IsFirstDeclaratorBehindItsType(declarator)
                    ? node.SpanStart
                    : -1
            )
        );

        // ⚠ An anonymous function's block nests from the line the function starts on, not from the
        // line its `{` lands on — the switch expression's rule (SK-DIV-0107), measured for #413
        // (SK-DIV-0164). The two differ when the parameter list or the arrow broke under a
        // continuation the statement opened on the function's own line: `_f = delegate(` /
        // `int first` / `) {` puts the statement at the `=`'s level plus one and `};` on it, as
        // `_f = (int first)` / `=> {` does, while a block nesting from the `) {` line counted the
        // `=`'s continuation it was inside. The anchor is pushed after the gap before the function,
        // so it records the function's own line.
        var anchored = AnchorsItsBlock(node);
        var activatedOutside = false;
        if (anchored) {
            EmitLeadingGap(node);
            activatedOutside = frames[^1].Activated;
            OpenIndent(IndentKind.Anchor);
        }

        Dispatch(node);
        if (anchored) {
            // A continuation the frame spent inside the anchor closes inside it, so the document's
            // scopes stay nested.
            EmitUpTo(node.Span.End);
            if (frames[^1].Activated && !activatedOutside) {
                CloseIndent(IndentKind.Continuous);
                frames[^1] = frames[^1] with { Activated = false };
            }

            CloseIndent(IndentKind.Anchor);
        }

        if (frames[^1].Activated) {
            CloseIndent(IndentKind.Continuous);
        }

        var restored = frames[^1].SavedDepth;
        frames.RemoveAt(frames.Count - 1);
        continuousDepth = restored;
    }

    /// <summary>
    ///     Whether a pattern's parenthesis sits under an <c>is</c> the author broke before, which has spent
    ///     the level already: <c>next.Parent</c> / <c>is not (Alpha</c> / <c>or Beta);</c> keeps the
    ///     <c>or</c> on the <c>is</c>'s column (#520).
    /// </summary>
    bool FollowsABrokenIs(SyntaxNode node) {
        for (var current = node.Parent; current is not null; current = current.Parent) {
            switch (current) {
                case PatternSyntax:
                    continue;
                case IsPatternExpressionSyntax test:
                    return HasLineBreak(test.IsKeyword.GetPreviousToken().Span.End, test.IsKeyword.SpanStart);
                default:
                    return false;
            }
        }

        return false;
    }

    /// <summary>
    ///     An anonymous function or an object creation whose block nests from the line the construct
    ///     starts on.
    /// </summary>
    /// <remarks>See VisitInner and SK-DIV-0164.</remarks>
    bool AnchorsItsBlock(SyntaxNode node) => AnchoredBlockOf(node) is not null;

    /// <summary>The block <see cref="AnchorsItsBlock" /> anchors, or null.</summary>
    /// <remarks>
    ///     ⚠ Not an initializer that aligns to its own column or takes one level rather than a block:
    ///     an <see cref="IndentKind.AnchoredBlock" /> is a block, and either of those is another scope.
    /// </remarks>
    SyntaxNode? AnchoredBlockOf(SyntaxNode node) =>
        node switch {
            AnonymousFunctionExpressionSyntax { Block: { } block } => block,
            BaseObjectCreationExpressionSyntax { Initializer: { } initializer }
                when options.UseContinuousIndentInsideInitializerBraces && !AlignsFromOwnColumn(initializer) =>
                initializer,
            // ⚠ And a `with` initializer, from the line the `with` expression starts on (#487,
            // SK-DIV-0168): `_r = Make(` / `a,` / `b` / `) with {` puts the members at the statement's
            // level plus one and `};` on it, as an object creation's does.
            WithExpressionSyntax { Initializer: { } initializer }
                when options.UseContinuousIndentInsideInitializerBraces && !AlignsFromOwnColumn(initializer) =>
                initializer,
            _ => null
        };

    /// <summary>
    ///     The lambda that <c>place_single_method_argument_lambda_on_same_line = true</c> keeps on the
    ///     call's own line.
    /// </summary>
    /// <remarks>
    ///     ⚠ Its body is not a continuation context of its own, and every other lambda's is. The
    ///     difference is that the call's parenthesis has already spent a level
    ///     <em>
    ///         on the lambda's own
    ///         line
    ///     </em> — which is why <see cref="VisitDelimited" /> opens that one unconditionally — so a
    ///     second level for the body is the one-level-per-opening-line rule being paid twice:
    ///     <code>
    /// var b = new Func&lt;int, bool&gt;(x =&gt; x &gt; 0
    ///     &amp;&amp; x &lt; 10          ← one level, not the two an argument on its own line takes
    /// );
    ///     </code>
    /// </remarks>
    bool IsSoleLambdaArgument(SyntaxNode node) =>
        options.PlaceSingleMethodArgumentLambdaOnSameLine
        // ⚠ And not a named argument, which the oracle lays out like any other argument — the same
        // exclusion BreakPlan.IsLambdaArgument makes (issue #378).
        && node is LambdaExpressionSyntax
        && node.Parent is ArgumentSyntax { NameColon: null, Parent: ArgumentListSyntax { Arguments.Count: 1 } };

    /// <summary>
    ///     A break is attributed to the innermost statement, member or accessor, because those are the
    ///     units whose continuation lines the option is about. A block resets the count, so the frame a
    ///     break lands on is always inside the nearest brace.
    /// </summary>
    /// <summary>
    ///     The outermost link of a <c>a.B().C()</c> chain — the one whose level the whole chain hangs
    ///     from.
    /// </summary>
    static bool IsChainRoot(SyntaxNode node) =>
        // ⚠ The root is the outermost link, and for `a.B().C()` that is the invocation, not the
        // member access inside it. Testing only for a member access finds the wrong node and the
        // chain never spends its level.
        node is InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax or MemberBindingExpressionSyntax }
            or MemberAccessExpressionSyntax
            or ConditionalAccessExpressionSyntax
        && node.Parent is not (InvocationExpressionSyntax
            or MemberAccessExpressionSyntax
            or ElementAccessExpressionSyntax
            or ConditionalAccessExpressionSyntax
            or MemberBindingExpressionSyntax);

    static bool IsPatternChainRoot(SyntaxNode node) =>
        node is BinaryPatternSyntax && node.Parent is not BinaryPatternSyntax;

    /// <summary>
    ///     The constructs a continuation level is attributed to.
    /// </summary>
    /// <remarks>
    ///     ⚠ List elements are on this list, and they have to be. A level spent inside one arm of a
    ///     switch expression must not still be open when the next arm starts — the leak shifts every
    ///     following arm right by four and is invisible until a long file has one wrapped arm in the
    ///     middle of it.
    /// </remarks>
    static bool OwnsAContinuationFrame(SyntaxNode node) =>
        node is StatementSyntax
            or MemberDeclarationSyntax
            or AccessorDeclarationSyntax
            or AnonymousFunctionExpressionSyntax
            or SwitchExpressionArmSyntax
            or ArgumentSyntax
            or AttributeArgumentSyntax
            or ParameterSyntax
            or AnonymousObjectMemberDeclaratorSyntax
            or VariableDeclaratorSyntax
            or SubpatternSyntax
            or CollectionElementSyntax
            or SwitchLabelSyntax
            or BaseTypeSyntax
            or TypeParameterConstraintClauseSyntax;

    void Dispatch(SyntaxNode node) {
        if (verbatimMembers.Contains(node.SpanStart) && node is MemberDeclarationSyntax) {
            EmitVerbatim(node);
            return;
        }

        switch (NodeLayouts.Classify(node.Kind())) {
            case NodeLayout.Unknown:
            case NodeLayout.Verbatim:
                // ⚠ R5: a kind this Skala was never told about is emitted from its original span
                // rather than guessed at. The same path serves interpolated strings, where a moved
                // space changes the value.
                EmitVerbatim(node);
                return;

            case NodeLayout.BracedBlock:
            case NodeLayout.BracedInitializer:
                VisitBraced(node);
                return;

            case NodeLayout.Parens:
            case NodeLayout.Brackets:
            case NodeLayout.Angles:
                VisitDelimited(node, NodeLayouts.Classify(node.Kind()));
                return;

            case NodeLayout.Embedded:
                VisitEmbedded(node);
                return;

            case NodeLayout.SwitchBody:
                VisitSwitch((SwitchStatementSyntax)node);
                return;

            // ⚠ A `catch … when (…)` filter is a statement condition too, and the only one that is
            // not reached through VisitEmbedded — a catch clause has no embedded statement, so it
            // was `Transparent` and its parentheses opened no scope at all. The oracle aligns it
            // like every other condition:
            //     } catch (Exception exception) when (exception is YamlBindingException
            //                                             or YamlParseException) {
            case NodeLayout.Transparent when node is CatchFilterClauseSyntax filter:
                EmitToken(filter.WhenKeyword);
                if (filter.OpenParenToken.IsKind(SyntaxKind.None)) {
                    VisitChildren(node);
                    return;
                }

                EmitToken(filter.OpenParenToken);
                var filterScopes = OpenConditionScopes();
                Visit(filter.FilterExpression);
                EmitUpTo(filter.CloseParenToken.SpanStart);
                var filterPending = CloseConditionScopesBeforeRparen(filterScopes);
                EmitToken(filter.CloseParenToken);
                CloseConditionScopesAfterRparen(filterPending);
                return;

            case NodeLayout.SwitchSection:
                VisitSwitchSection((SwitchSectionSyntax)node);
                return;

            case NodeLayout.Continuation when node is ConditionalExpressionSyntax ternary:
                // ⚠ A ternary's arms take a level of their own, on top of whatever continuation the
                // expression already sits in — `outdent_ternary_ops = false`. A binary chain does
                // not, which is why the two are not the same case.
                // ⚠ Except when the ternary is another one's else-arm. `align_ternary =
                // align_not_nested` says a *chain* of conditionals is not nested, and the oracle
                // writes it flat:
                //     OperatingSystem.IsWindows() ? "win"
                //     : OperatingSystem.IsMacOS() ? "osx"
                //     : "linux";
                // One level per link turns six lines into a staircase six levels deep.
                // ⚠ And except directly inside a grouping parenthesis, whose level the arms land on
                // (#546). Measured 2026-10-08: `b ? (a > 0` / `? a` / `: c) : c`, `= (a > 0` / `? a`,
                // the same as an argument, in an `if` condition, as a binary operand and as a
                // `WhenFalse` all put the signs one level past the parenthesis's line, where the two
                // scopes, both opened on that line, counted twice. A conditional that is itself an
                // argument keeps its level: `F(a > 0` / `? a` is two levels past `F(`'s line.
                var nested = ternary.Parent is ConditionalExpressionSyntax outer
                    && outer.WhenFalse == ternary
                    || ternary.WhenFalse is ConditionalExpressionSyntax
                    || ternary.Parent is ParenthesizedExpressionSyntax;
                // ⚠ Opened on the condition's *first* line unless the condition is a binary chain
                // (#530, SK-DIV-0333). A condition that spans lines as a chain or an argument list —
                // `var t = a.B()` / `.C()` / `? x` / `: y`, `Compute(` / … / `)` / `? x` — puts the
                // signs one level past the statement, the same level as the dots; opened after the
                // condition, the scope began on the condition's last line and the signs went a level
                // deeper. A broken `&&` chain is the opposite and measured too: `&& c` at one level,
                // `? x` at two, which is what opening the scope on that last line gives.
                // ⚠ Nor a chain headed by a parenthesis, which shares the level around it (SK-DIV-0112):
                // `=>` / `(` / `a)[0]` / `.C()` on the `(`'s column, `? a` one level in.
                var early = !nested
                    && ternary.Condition is not BinaryExpressionSyntax
                    && (ternary.Condition is ParenthesizedExpressionSyntax
                        || !BreakPlan.ChainHeadIsParenthesised(ternary.Condition));
                if (early) {
                    // After the gap before the condition, so that a break there — `=` / `cond` — puts
                    // the scope on the condition's own line.
                    EmitLeadingGap(ternary.Condition);
                    OpenIndent(IndentKind.Continuous);
                }

                Visit(ternary.Condition);
                if (!nested && !early) {
                    OpenIndent(IndentKind.Continuous);
                }

                EmitToken(ternary.QuestionToken);
                Visit(ternary.WhenTrue);
                EmitToken(ternary.ColonToken);
                Visit(ternary.WhenFalse);
                EmitUpTo(ternary.Span.End);
                if (!nested) {
                    CloseIndent(IndentKind.Continuous);
                }

                return;

            // skala_indent_type_constraints: a `where` clause on its own line is a continuation of
            // the declaration, and the option says whether it takes a level.
            // ⚠ Only without a constraint run, and this arm used to open the scope under a run too.
            // Under `skala_place_type_constraints_on_same_line = true` the gap before the `where` is
            // emitted before the clause is entered and the run's group spends the level for it; a
            // scope opened here then began on the `where`'s own line and reached only the lines
            // *inside* the clause, one indent past the keyword. The oracle indents none of them:
            // `where T : class\n, new()` puts `new()` on the `where`'s own column at both values of
            // this key, at both values of the placement key, and at a multiplier of 2 (SK-DIV-0105).
            // Without a run the gap is emitted here, under the scope, which is what puts the
            // `where` a level in — and the clause's lines after it land on that same level because
            // the clause's frame pays for nothing (see VisitInner).
            case NodeLayout.Continuation when node is TypeParameterConstraintClauseSyntax clause:
                VisitConstraintClause(clause);
                return;

            case NodeLayout.Transparent when node is FileScopedNamespaceDeclarationSyntax fileScoped:
                VisitFileScopedNamespace(fileScoped);
                return;

            default:
                VisitChildren(node);
                return;
        }
    }

    /// <summary>A <c>where</c> clause: its level, if it takes one here, and its constraints' fill.</summary>
    void VisitConstraintClause(TypeParameterConstraintClauseSyntax clause) {
        var indents = options.IndentTypeConstraints && !options.PlaceTypeConstraintsOnSameLine;
        if (indents) {
            OpenIndent(IndentKind.Continuous);
        }

        // The constraints' own fill (BreakPlan.PlanConstraintList), opened inside the clause so
        // that the gap before the `where` stays outside it.
        var hasConstraintList = plan.TryInnerGroup(clause, out var constraintList);
        if (hasConstraintList) {
            EmitUpTo(clause.Constraints[0].SpanStart);
            doc.DescribeGroup(constraintList.Id, constraintList.Facts);
            doc.OpenGroup(constraintList.Mode, constraintList.Id);
        }

        VisitChildren(clause);
        if (hasConstraintList) {
            EmitUpTo(clause.Span.End);
            doc.Close();
        }

        if (indents) {
            CloseIndent(IndentKind.Continuous);
        }
    }

    /// <summary>
    ///     A file-scoped namespace: the continuation its own name may have spent ends at the <c>;</c>.
    /// </summary>
    /// <remarks>
    ///     ⚠ This is <see cref="VisitChild" />'s rule — "a body indents from its declaration's level" —
    ///     for the one declaration whose body has no braces. A file-scoped namespace is a
    ///     <see cref="MemberDeclarationSyntax" />, so it owns a continuation frame, and the whole rest of
    ///     the file is its children rather than its siblings. So a wrapped namespace name spent a
    ///     continuation level that nothing closed until the end of the file:
    ///     <code>
    /// namespace Serilog
    ///     .Configuration;      ← the break before `.` spends the level
    ///
    ///     public class Foo {   ← and every line after it is +4, to the end of the file
    ///         public int Bar { get; set; }
    ///     }
    ///     </code>
    ///     A braced namespace never showed it because <see cref="VisitBraced" /> closes the frame at the
    ///     <c>{</c>, and no file in <c>corpus/real/</c> showed it either, because nobody writes a
    ///     wrapped namespace name by hand. It took the unformat differential — 204 of the 380 scrambled
    ///     files contain one, and they scored 38.00 % against the other 176's 88.93 %.
    /// </remarks>
    void VisitFileScopedNamespace(FileScopedNamespaceDeclarationSyntax node) {
        var semicolon = node.SemicolonToken;
        foreach (var child in node.ChildNodesAndTokens()) {
            if (child.AsNode() is { } inner) {
                VisitChild(node, inner);
                continue;
            }

            var token = child.AsToken();
            EmitToken(token);

            // ⚠ After the token and before the gap that follows it, which is the order VisitBraced
            // uses at the `{` and for the same reason: the break belongs to the level the closing
            // leaves behind, not to the one it is closing.
            if (!semicolon.IsKind(SyntaxKind.None)
                && token.SpanStart == semicolon.SpanStart
                && frames.Count > 0
                && frames[^1].Activated) {
                CloseIndent(IndentKind.Continuous);
                frames[^1] = frames[^1] with { Activated = false };
            }
        }
    }

    /// <remarks>
    ///     ⚠ The one construct whose group is opened here rather than around a node is a run of
    ///     <c>where</c> clauses: they are siblings with nothing in the tree spanning them, and the two
    ///     questions ReSharper asks about them — see <see cref="ConstraintRun" /> — are asked at two
    ///     different columns, one before the break that precedes the first clause and one after it. So
    ///     the outer group opens, the gap is written, the inner group opens, and both close after the
    ///     last clause and before the body.
    /// </remarks>
    void VisitChildren(SyntaxNode node) {
        var run = BeginConstraintRun(node);
        List<(int Indented, bool Held)>? opened = null;

        foreach (var child in node.ChildNodesAndTokens()) {
            // ⚠ A group that begins at a child of this node rather than at a node of its own — a
            // switch arm's `=>` and the body after it (issue #378). Opened before the gap that
            // precedes the child, so that the gap is the group's first point, and closed after the
            // last child; the same shape as the constraint run's. See BreakPlan.TryOpenedAt.
            // ⚠ A group whose first point is not that gap opens after it (#420): a declarator list whose
            // first name a comment has already broken onto its own line must not count that break as
            // its own, or `int /* c */` / `a, b;` chops into `a,` / `b;`.
            if (plan.TryOpenedAt(node, child.SpanStart, out var plans)) {
                opened ??= [];
                foreach (var planned in plans) {
                    if (planned.LeadingGapInside) {
                        opened.Add(OpenGroupAt(planned, node));
                    }
                }

                EmitLeadingGapAt(child.SpanStart);
                foreach (var planned in plans) {
                    if (!planned.LeadingGapInside) {
                        opened.Add(OpenGroupAt(planned, node));
                    }
                }
            }

            if (child.IsToken) {
                EmitToken(child.AsToken());
                continue;
            }

            if (child.AsNode() is { } inner) {
                VisitConstrainedChild(node, inner, ref run);
            }
        }

        if (opened is null) {
            return;
        }

        EmitUpTo(node.Span.End);
        for (var i = opened.Count - 1; i >= 0; i--) {
            CloseGroupAt(opened[i]);
        }
    }

    /// <summary>
    ///     Opens one of <see cref="BreakPlan.TryOpenedAt" />'s groups with the continuation level it
    ///     spends, or holds, exactly as <see cref="VisitPlanned" /> opens a group described on a node.
    /// </summary>
    (int Indented, bool Held) OpenGroupAt(GroupPlan planned, SyntaxNode node) {
        doc.OpenGroup(planned.Mode, planned.Id);
        var indented = planned.SpendsIndent && CanSpendAContinuationLevel(node, planned.SpendsUnderDelimiters) ? 1 : 0;
        var held = planned.HoldsLevel == HeldLevel.Always && indented > 0;
        if (held) {
            indented = 0;
        }

        doc.DescribeGroup(
            planned.Id,
            planned.Facts with { SpendsIndent = indented > 0 && planned.HoldsLevel != HeldLevel.WhileChainWhole }
        );
        if (held) {
            HoldContinuationLevel();
        }

        for (var level = 0; level < indented; level++) {
            OpenContinuation(planned, level);
        }

        return (indented, held);
    }

    /// <summary>
    ///     Opens one of the continuation levels a group spends — the first as a held level while the
    ///     group stays flat when the plan asks for <see cref="HeldLevel.WhileFlat" />.
    /// </summary>
    /// <remarks>
    ///     ⚠ Spent, for the builder, either way: <c>continuousDepth</c> counts it, so nothing further
    ///     in spends the level the group has taken, and the fitter is told the group spends it,
    ///     because a break at the group's own point lands one level in. Only the writer, which knows
    ///     whether the group broke, decides whether the level has columns (issue #406, SK-DIV-0157).
    /// </remarks>
    /// <param name="chainLevel">
    ///     The level is a chained call's own (<see cref="GroupPlan.OwnLevel" /> on a chain root), which
    ///     nests from a broken binary operator's continuation line when the chain is the operator's left
    ///     operand: <see cref="IndentFlags.ChainLevel" />.
    /// </param>
    void OpenContinuation(in GroupPlan planned, int level, bool chainLevel = false) {
        var conditions = (planned.HoldsLevel & HeldLevel.WhileFlat) != 0
            ? IndentFlags.HeldWhileOwnerFlat
            : IndentFlags.None;

        var chain = (planned.HoldsLevel & HeldLevel.WhileChainWhole) != 0 ? plan.ChainHeldAgainst(planned.Id) : -1;
        if (chain >= 0) {
            conditions |= IndentFlags.HeldWhileChainWhole;
        }

        if (level == 0 && conditions != IndentFlags.None) {
            doc.OpenHeldIndent(IndentKind.Continuous, conditions, chain);
            continuousDepth++;
            return;
        }

        OpenIndent(
            IndentKind.Continuous,
            planned.UnconditionalLevel,
            chainLevel ? IndentFlags.ChainLevel : IndentFlags.None
        );
    }

    void CloseGroupAt((int Indented, bool Held) opened) {
        for (var level = 0; level < opened.Indented; level++) {
            CloseIndent(IndentKind.Continuous);
        }

        if (opened.Held) {
            ReleaseContinuationLevel();
        }

        doc.Close();
    }

    /// <summary>What a constraint run needs while the declaration's children are being written.</summary>
    struct ConstraintRunState {
        public SyntaxNode? Last;
        public ConstraintRun Run;
        public bool Open;
        public int IndentedOuter;
        public int IndentedInner;
    }

    ConstraintRunState BeginConstraintRun(SyntaxNode node) =>
        plan.TryConstraintRun(node, out var run)
            ? new ConstraintRunState { Last = LastConstraintClause(node), Run = run }
            : default;

    /// <summary>
    ///     Writes one child of a declaration, opening and closing the constraint run around the
    ///     <c>where</c> clauses it contains.
    /// </summary>
    /// <remarks>
    ///     ⚠ Both walks over a declaration's children need this — a method's is
    ///     <see cref="VisitChildren" /> and a type's is <see cref="VisitBraced" /> — and the run must
    ///     close before the body's <c>{</c>, which is why it closes at the last clause rather than at the
    ///     end of the walk.
    /// </remarks>
    void VisitConstrainedChild(SyntaxNode owner, SyntaxNode inner, ref ConstraintRunState state) {
        if (state.Last is not null && inner is TypeParameterConstraintClauseSyntax) {
            if (!state.Open) {
                state.Open = true;
                state.IndentedOuter = OpenRunGroup(state.Run.Outer);
                if (state.Run.OwnsLeadingGap) {
                    EmitLeadingGapAt(inner.SpanStart);
                }

                state.IndentedInner = OpenRunGroup(state.Run.Inner);
            } else {
                // ⚠ Here rather than inside the clause, and that is the whole of where the
                // indentation comes from. A clause is a NodeLayout.Continuation, so it opens a level
                // of its own around its children; the gap before its `where` is written by that arm,
                // which puts the break *inside* the level and lands the second clause one step past
                // the first. The gap belongs to the run.
                EmitLeadingGapAt(inner.SpanStart);
            }
        }

        VisitChild(owner, inner);

        if (state.Open && inner == state.Last) {
            EmitUpTo(inner.Span.End);
            CloseRunGroup(state.IndentedInner);
            CloseRunGroup(state.IndentedOuter);
            state.Open = false;
            state.Last = null;
        }
    }

    /// <summary>
    ///     Opens one of a constraint run's groups, spending a continuation level for it if it is the
    ///     one paying.
    /// </summary>
    /// <remarks>
    ///     ⚠ Both groups ask for the level and at most one of them gets it, which is
    ///     <see cref="CanSpendAContinuationLevel" />'s answer rather than a rule of this construct's.
    ///     Whichever opens first while nothing else is spending takes it, and the breaks of the other
    ///     land inside it — it is the same level either way, and asking for it twice would put a
    ///     wrapped clause two indents in.
    /// </remarks>
    int OpenRunGroup(GroupPlan plan) {
        doc.OpenGroup(plan.Mode, plan.Id);
        var spends = plan.SpendsIndent && CanSpendAContinuationLevel();
        doc.DescribeGroup(plan.Id, plan.Facts with { SpendsIndent = spends });
        if (spends) {
            OpenIndent(IndentKind.Continuous);
        }

        return spends ? 1 : 0;
    }

    void CloseRunGroup(int indented) {
        for (var level = 0; level < indented; level++) {
            CloseIndent(IndentKind.Continuous);
        }

        doc.Close();
    }

    static SyntaxNode? LastConstraintClause(SyntaxNode node) {
        SyntaxNode? last = null;
        foreach (var child in node.ChildNodes()) {
            if (child is TypeParameterConstraintClauseSyntax) {
                last = child;
            }
        }

        return last;
    }

    /// <summary>
    ///     Visits a child, spending the owner's continuation level first when the child is the owner's
    ///     own body.
    /// </summary>
    /// <remarks>
    ///     ⚠ A body indents from its declaration's level, not from whatever line the brace ended up on:
    ///     <code>
    /// protected C(int a) :
    ///     base(a) {        ← the initializer's continuation level
    ///     Body();          ← but the body is one from the CONSTRUCTOR, not two
    /// }
    ///     </code>
    ///     The test is that the block is the frame owner's own child; a lambda's block nested inside an
    ///     expression keeps the continuation, because there the level is real.
    /// </remarks>
    void VisitChild(SyntaxNode owner, SyntaxNode child) {
        if (child is BlockSyntax or AccessorListSyntax
            && OwnsAContinuationFrame(owner)
            && frames.Count > 0
            && frames[^1].Activated) {
            CloseIndent(IndentKind.Continuous);
            frames[^1] = frames[^1] with { Activated = false };
        }

        Visit(child);
    }

    /// <summary>A <c>{ }</c> body: everything between the braces takes one block indent.</summary>
    internal void VisitBraced(SyntaxNode node) {
        var (open, close) = BraceTokens(node);
        var opened = false;

        // ⚠ A group opened *inside* the braces, at the column its contents land on. An initializer's
        // elements are measured against the continuation column, not against the column the
        // construct starts at, and a group opened around the node is entered at the latter. See
        // BreakPlan's `_inner` for why the two cannot be the same group.
        var hasInner = plan.TryInnerGroup(node, out var elements);

        // csharp_indent_braces: the braces themselves take the inner level rather than the outer.
        //
        // ⚠ Only where the brace goes on a line of its own, which is measured and was missing. Under
        // the export's `csharp_new_line_before_open_brace = none` the oracle is flat at both values —
        // indenting a brace that is welded to the end of the previous line is not a thing it can do —
        // and Skala applied it anyway. The opening brace was joined and so could not move, the closing
        // one moved, and the result was a shape neither value of either key produces:
        //
        //   class C {              class C            ← indent_braces = true, and the oracle's answer
        //       void M() {             {                 only at `new_line_before_open_brace = all`
        //           if (true) {        void M()
        //               M();               {
        //               }                  if (true)
        //           }                          {
        //       }                              M();
        //                                      }
        //                                  }
        //                              }
        var indentBraces = options.IndentBraces
            && (options.NewLineBeforeOpenBraceOwners & BraceOwnerSet.Of(open)) != 0;

        // skala_indent_inside_namespace = false flattens a block namespace's members.
        var suppress = node is NamespaceDeclarationSyntax && !options.IndentInsideNamespace;

        // ⚠ `skala_use_continuous_indent_inside_initializer_braces = false` used to suppress the scope
        // outright — the initializer's contents landed on the level of the construct that owns them —
        // and that is measured wrong in the same way its `_parens` sibling was: the oracle gives them
        // ONE INDENT WIDTH. With `skala_continuous_indent_multiplier = 2`:
        //
        //   new List<int> {          new List<int> {
        //           1,                   1,          ← 12 + 1×4 at false, 12 + 2×4 at true
        //   };                       };
        //
        // Under the export's own multiplier of 1 the two are the same number, which is why the sweep
        // called this key `SPURIOUS`: the oracle could not move and Skala did.
        //
        // ⚠ The `true` arm is `IndentKind.Block` and stays that way in this pass. Block is one indent
        // width, so it is *also* the `false` answer at multiplier 1, and at any other multiplier the
        // `true` arm is a level short. That is a `skala_continuous_indent_multiplier` defect on braced
        // initializers rather than this key's, it is recorded at the key in options.json, and moving
        // it here would mean turning an absolute scope into a relative one under every initializer in
        // `corpus/real` on the strength of a row that does not ask about it.
        var singleInsideInitializer = node is InitializerExpressionSyntax
            or AnonymousObjectCreationExpressionSyntax
            && !options.UseContinuousIndentInsideInitializerBraces;

        // ⚠ A generic type's `where` clauses come before its `{`, so the run belongs to this walk as
        // much as to VisitChildren's. Without it a constrained class declaration has the plan and no
        // group to hang it on, and its constraints stay on a 200-column line.
        var run = BeginConstraintRun(node);

        // ⚠ A switch expression's arms nest from the line its governing expression starts on, not
        // from the line the `{` lands on (SK-DIV-0107). Measured: `var s = (a,\n b) switch {` puts the
        // arms at 12 and the `}` at 8 — the statement's level plus one — while a block opened at the
        // brace, inside the `=`'s continuation, put them at 16. The same for `(a\n + b) switch`, for a
        // chain broken before `.Length switch`, for `F(a,\n b) switch`, for `int s =`, for `s =` and
        // at a multiplier of 2; under an arrow the two agree already, because the arrow's level is
        // written before the governing expression begins. The anchor is pushed here, after
        // VisitPlanned has emitted the gap before the node, so it records the governing expression's
        // own line.
        // ⚠ Not under `skala_align_multiline_switch_expression`, whose Align scope is already an absolute
        // column the arms nest from.
        var anchored = node is SwitchExpressionSyntax && !AlignsFromOwnColumn(node);

        // ⚠ And an anonymous function's block and an object creation's initializer nest from the
        // anchor VisitInner pushed where the construct begins (SK-DIV-0164).
        var nestsFromAnchor = anchored || node.Parent is { } owner && AnchoredBlockOf(owner) == node;

        // ⚠ Unless the governing expression is a grouping parenthesis or a tuple whose `)` the author
        // kept on a line of its own (#506): the arms nest from that `)`'s line — `var t = (1, 2` /
        // `    ) switch {` / `        _ => 0` / `    };` — wherever VisitDelimited put it. An argument
        // list's `)` comes back to its opener's level and the arms nest from the statement as before.
        // So the anchor is pushed at the `switch` keyword and records that line's own indentation.
        // ⚠ And so do the arms of a switch over a chain whose head is parenthesised, once the chain
        // broke: they nest from the dots' line, `(` / `a).B()` / `.C() switch {` / arms two levels in /
        // `}` one, where a chain with an ordinary head keeps them at the statement's (SK-DIV-0158).
        var anchorAtKeyword = anchored
            && node is SwitchExpressionSyntax { GoverningExpression: var governing }
            && (KeepsAClosingParenthesisBeforeASwitch(governing)
                || IsChainRoot(governing)
                && BreakPlan.ChainHeadIsParenthesised(governing)
                && HasLineBreak(governing.SpanStart, governing.Span.End));
        if (anchored && !anchorAtKeyword) {
            OpenIndent(IndentKind.Anchor);
        }

        foreach (var child in node.ChildNodesAndTokens()) {
            if (child.IsToken) {
                var token = child.AsToken();
                if (anchorAtKeyword && token.IsKind(SyntaxKind.SwitchKeyword)) {
                    EmitUpTo(token.SpanStart);
                    OpenIndent(IndentKind.Anchor, false, IndentFlags.AnchorAtLine);
                }

                if (opened && !close.IsKind(SyntaxKind.None) && token.SpanStart == close.SpanStart) {
                    EmitUpTo(close.SpanStart);
                    if (hasInner) {
                        doc.Close();
                    }

                    var closeIndent = singleInsideInitializer ? IndentKind.OneLevel : IndentKind.Block;
                    if (!indentBraces) {
                        CloseIndent(closeIndent, true);
                    }

                    EmitToken(token);
                    if (indentBraces) {
                        CloseIndent(closeIndent);
                    }

                    opened = false;
                    continue;
                }

                if (!opened && !open.IsKind(SyntaxKind.None) && token.SpanStart == open.SpanStart && !suppress) {
                    // ⚠ Same rule as VisitChild's: a body indents from its declaration's level.
                    // `class C\n    : B {` puts the base list on a continuation line, and the members
                    // still take one level from the class rather than two.
                    // ⚠ Not when the switch's anchor was pushed at its keyword, inside the level its own
                    // frame spent at the `)` (`return (1, 2` / `    ) switch {`): closing it here would
                    // pop the anchor instead, and the anchored block nests from that anchor anyway.
                    if (!anchorAtKeyword && OwnsAContinuationFrame(node) && frames.Count > 0 && frames[^1].Activated) {
                        CloseIndent(IndentKind.Continuous);
                        frames[^1] = frames[^1] with { Activated = false };
                    }

                    var braceIndent = singleInsideInitializer ? IndentKind.OneLevel
                        : nestsFromAnchor ? IndentKind.AnchoredBlock
                        : IndentKind.Block;

                    // ⚠ A braced initializer's elements and a switch expression's arms are a continuation
                    // inside braces, and take `skala_continuous_indent_multiplier` widths, not one (#464):
                    // at a multiplier of 2 the oracle puts them 8 + 2 × 4 = 16 with the `}` at 8, for
                    // a collection, array, object, anonymous and `with` initializer and a switch
                    // expression alike, and at 3 likewise. A block body keeps one width, and so does all
                    // of it under `skala_use_continuous_indent_inside_initializer_braces = false` — the
                    // switch's arms included, which that key governs too (measured at multiplier 2).
                    var multiplied = options.UseContinuousIndentInsideInitializerBraces
                        && node is InitializerExpressionSyntax
                            or AnonymousObjectCreationExpressionSyntax
                            or SwitchExpressionSyntax
                            ? IndentFlags.Multiplied
                            : IndentFlags.None;

                    if (indentBraces) {
                        OpenIndent(braceIndent, false, multiplied);
                        EmitToken(token);
                    } else if (nestsFromAnchor) {
                        // ⚠ A brace on a line of its own takes its `}`'s column, not the `=`'s
                        // continuation the gap before it sits in (#465). See IndentKind.AnchoredBrace.
                        OpenIndent(IndentKind.AnchoredBrace);
                        EmitToken(token);
                        CloseIndent(IndentKind.AnchoredBrace);
                        OpenIndent(braceIndent, false, multiplied);
                    } else {
                        EmitToken(token);
                        OpenIndent(braceIndent, false, multiplied);
                    }

                    if (hasInner) {
                        // ⚠ The gap after the brace is emitted *before* the group opens, and the
                        // order is the whole point. That gap holds the outer group's break point, so
                        // emitting it first is what puts the elements group's first character on the
                        // continuation line — which is the column its contents have to be measured
                        // against. Opening the group first measures the elements from the column
                        // just after the `{`, four columns and one line too optimistic, and every
                        // initializer that would have fitted on one continuation line comes out with
                        // one element per line instead.
                        EmitLeadingGapAt(FirstElementStart(node));
                        doc.DescribeGroup(elements.Id, elements.Facts);
                        doc.OpenGroup(elements.Mode, elements.Id);
                    }

                    opened = true;
                    continue;
                }

                EmitToken(token);
            } else if (child.AsNode() is { } inner) {
                VisitConstrainedChild(node, inner, ref run);
            }
        }

        if (opened) {
            EmitUpTo(close.IsKind(SyntaxKind.None) ? int.MaxValue : close.SpanStart);
            if (hasInner) {
                doc.Close();
            }

            CloseIndent(singleInsideInitializer ? IndentKind.OneLevel : IndentKind.Block);
        }

        if (anchored) {
            EmitUpTo(node.Span.End);
            CloseIndent(IndentKind.Anchor);
        }
    }

    /// <summary>
    ///     Everything a delimited group decides before it writes a token: which indent its contents
    ///     take, whether the scope is spent unconditionally, whether its children are elements, and the
    ///     two <c>indent_*_pars</c> numbers.
    /// </summary>
    /// <remarks>
    ///     ⚠ Extracted from <see cref="VisitDelimited" /> and nothing more: five pure functions of the
    ///     node, the layout and the options, none of which reads or writes the emitter's state. The
    ///     token loop below is where the state lives, and keeping the decisions out of it is what lets
    ///     each one be read against the measurement that produced it.
    /// </remarks>
    (IndentKind Inner, bool Unconditional, bool Element, int Inside, int Closer, bool Marker) PlanDelimited(
        SyntaxNode node,
        NodeLayout layout
    ) {
        // ⚠ And an aligned construct spends no GROUP level of its own — the same rule VisitPlanned
        // applies, for the same reason: the Align scope is an absolute column and its contents start
        // there.
        // ⚠ But whether it spends a DELIMITER level depends on where its anchor is, and that is
        // measured rather than uniform. A type parameter list anchors on its first parameter, which
        // is already past the `<`, so its continuation lines land on the anchor exactly:
        //     public void ManyParams<TFirstParameterName, TSecondParameterName,
        //                            TThirdParameterName>(int a) { }
        // A list pattern and a collection expression anchor on their own `[`, and there the oracle
        // puts the elements one level PAST the anchor and brings the `]` back to it:
        //     var matched = candidate is [
        //                                    firstElementPatternName, secondElementPatternName,
        //                                    fourthElementPatternName
        //                                ];
        // — which is the same relationship a braced initializer already gets from its own path, and
        // is why `skala_align_multiline_array_and_object_initializer` and
        // `skala_align_multiline_switch_expression` were conformant while `skala_align_multiline_list_pattern`
        // was not. Reading "aligned" as "no level at all" put the elements on the bracket's column.
        var aligned = AlignsFromOwnColumn(node);
        var anchorsOnTheDelimiter = aligned && AlignAnchor(node) <= node.SpanStart;

        // ⚠ `skala_use_continuous_indent_inside_parens = false` used to suppress the scope outright, and
        // that was measured wrong: the oracle gives the contents ONE INDENT WIDTH, not none. The two
        // readings are indistinguishable under the export's `skala_continuous_indent_multiplier = 1` — which
        // is exactly why the sweep called this key `SPURIOUS`, with Skala moving where the oracle
        // could not — and separate at any other multiplier. See IndentKind.OneLevel.
        var singleInsideParens = layout == NodeLayout.Parens && !options.UseContinuousIndentInsideParens;

        // ⚠ A positional pattern or a designation nested in another spends no level of its own (#473):
        // the oracle puts `, 3` of `o is (1` / `, (2` / `, 3))` under `, (2`, three deep and through a
        // property pattern alike, and a nested `)` on its own line with them — where a tuple
        // *expression* spends one per parenthesis. So the nested list opens no scope at all, as an
        // aligned one does.
        // ⚠ And so does the outermost one directly in an aligned statement condition: `if (o is (1` /
        // `, (2` puts `, (2` on the condition's column, which already pays the statement's level.
        // ⚠ And a pattern's own parenthesis inside an aligned statement condition spends nothing: the
        // oracle writes `if (o is not (Alpha` / `or Beta))` with `or` on the condition's column, and
        // `while (` / `or` at 15 — where a grouping parenthesis around an *expression* there is a
        // level of its own (`if ((a` / `== b))`). Under `var b = o is not (Alpha` / `or Beta);` the
        // parenthesis keeps its level (#520).
        var suppress = aligned
            || IsNestedPositionalList(node)
            || node is PositionalPatternClauseSyntax
            && DirectlyInAnAlignedHeader()
            || node is ParenthesizedPatternSyntax
            && (options.AlignMultilineStatementConditions
                && BreakPlan.IsStatementCondition(node)
                || FollowsABrokenIs(node));

        // ⚠ `skala_align_tuple_components = true`: the column *after* the tuple's `(`, which is a
        // different anchor from every key AlignsFromOwnColumn answers and needs a different place
        // to open the scope. Measured —
        //
        //     var tuple = (FirstComponentName: a, SecondComponentName: b,
        //                  ThirdOne: c);          ← the `(`'s column plus one, not plus an indent
        //
        // The scope opens after the `(` has been written, so `CurrentColumn` is already the first
        // component's. Opening it around the node — the way `Visit` does — would read the `(`'s own
        // column and land one to the left.
        var innerIndent = node is TupleExpressionSyntax
            && options.AlignTupleComponents
            || IsAnAlignedAttributeSection(node, source)
            ? IndentKind.Align
            : singleInsideParens
                ? IndentKind.OneLevel
                : IndentKind.Continuous;

        // ⚠ Which delimited scopes spend their level unconditionally — that is, even when another
        // scope opened on the same line — and which are collapsed with it. Both answers come from
        // the oracle and neither is guessable:
        //
        //   if ((expr           ← two levels: the condition's parenthesis is unconditional, and a
        //           == value))    grouping parenthesis inside it spends its own on top.
        //   [Attr(              ← one. The bracket and the argument list's parenthesis are one step.
        //       argument
        //   )]
        //   var d = Drawn(      ← one. The `=` does not pay for what the parenthesis pays for.
        //       argument
        //   );
        //
        // The sole-lambda case is the third: `place_single_method_argument_lambda_on_same_line`
        // keeps the lambda on the call's line, so that parenthesis never gets a line of its own and
        // would otherwise be collapsed into whatever the lambda's body opens.
        // ⚠ A grouping parenthesis is NOT unconditional, and was until #481: `var x = (c` / `? a`,
        // `var y = ((a` / `+ b))`, `var t = ((` / `1, 2))`, `var f = ((x,` / `y) => { })` and
        // `int[] z = ([` / `1,` all put the contents one level past the statement — the grouping, the
        // `=` and whatever opened beside it are one line's one level. Where a grouping does spend a
        // second level it is lifted by a construct that broke after it (`var b = ((` / `1 + 2)` /
        // `* 3);`, LayoutWriter.LiftedLevel), which is what the unconditional scope used to stand in
        // for, and did wrongly everywhere else.
        // ⚠ A type parameter list's angle brackets are a level of their own too (#538): an attribute
        // whose arguments chop on the `<`'s line, `class C<[Description(`, puts them two levels in and
        // `)]` one, on a class, a method and after a first parameter — the `(` alone paid one level.
        // ⚠ And an argument list whose first argument stays behind a comment after its `(` (#521): the
        // oracle writes `Compute( /* f */ Inner(` / `"…"` two levels in / `)` one / `);` — the outer
        // list's level counts although the inner one opened on the same line. See
        // BreakPlan.PlanPastLeadingComments.
        var unconditional = node is TypeParameterListSyntax
            || options.PlaceSingleMethodArgumentLambdaOnSameLine
            && node is ArgumentListSyntax { Arguments: [{ Expression: LambdaExpressionSyntax }] }
            || node is ArgumentListSyntax { Arguments.Count: > 0 } commented
            && plan.PlansPastALeadingComment(commented.Arguments[0].SpanStart);

        // ⚠ A collection expression's elements are elements, like an initializer's: a chain broken
        // inside one takes its own continuation level rather than living off the bracket's.
        var element = node is CollectionExpressionSyntax or ListPatternSyntax;

        // ⚠ The `indent_*_pars` family, and it is two numbers rather than one. An aligned construct
        // keeps the single scope it always had — the Align scope is an absolute column, and a second
        // one of those is not a deeper indent but the same column twice.
        var (inside, closer) = innerIndent == IndentKind.Align || suppress
            ? (innerIndent == IndentKind.Align || anchorsOnTheDelimiter ? 1 : 0, 0)
            : DelimiterLevels(ParenthesesStyleFor(node));

        // ⚠ `outside_and_inside`'s outer level belongs to the closing delimiter, and a construct
        // whose closing delimiter is not a break point of its own never realises it. Measured, one
        // key at a time, at that value:
        //
        //   Consume(                        ← skala_wrap_before_invocation_rpar: the `)` is a point
        //           argument                  contents 2 levels, `)` 1
        //   );
        //   class Primary(                  ← skala_wrap_before_primary_constructor_declaration_rpar is
        //       parameter) { }                false; the `)` is not a point — contents 1 level
        //   class Wide<TFirst, TSecond,     ← a `>` is never a point — contents 1 level
        //       TThird> { }
        //
        // ⚠ NOT "the closer takes a line": `arr[a + b\n]` has its `]` on a line of its own, because
        // `keep_user_linebreaks = true` keeps the author's break there, and the oracle still gives
        // that bracket's contents one level at `outside_and_inside`. Being *kept* on a line is not
        // the same as being a break point of the construct, and only the second answers here.
        if (inside > 1 && !ClosesAtABreakPoint(node, layout)) {
            inside = 1;
        }

        // ⚠ `none` is the one value of the family that asks for *zero* levels inside, and zero levels
        // used to mean no scope at all — which left the construct's closing delimiter wherever the
        // ambient continuation had got to, instead of on its opener's line. A `None` scope is the
        // marker that fixes it: no level, and something for `alignsCloser` to be measured against.
        // The suppressed arms above also produce zero, and they must not get one — theirs is
        // "this construct has no delimiter scope", not "its scope is worth nothing".
        var marker = inside == 0 && closer == 0 && !suppress && innerIndent != IndentKind.Align;

        return (innerIndent, unconditional, element, inside, closer, marker);
    }

    /// <summary>A <c>( )</c>, <c>[ ]</c> or <c>&lt; &gt;</c> group: one continuous indent inside.</summary>
    void VisitDelimited(SyntaxNode node, NodeLayout layout) {
        var (open, close) = DelimiterTokens(node, layout);
        if (open.IsKind(SyntaxKind.None) || close.IsKind(SyntaxKind.None)) {
            VisitChildren(node);
            return;
        }

        var opened = 0;
        var (innerIndent, unconditional, element, inside, closer, marker) = PlanDelimited(node, layout);

        // ⚠ At `none` the scope is a marker of no level, and the whole loop below is written in
        // terms of one scope kind and a count. So the kind becomes a variable and the count becomes
        // one: the marker opens and closes exactly where a real scope would, and the closing
        // delimiter reaches `alignsCloser` instead of falling through to the ambient continuation.
        var scopeKind = marker ? IndentKind.None : innerIndent;
        var levels = marker ? 1 : inside;

        var savedDepth = continuousDepth;
        var pending = 0;

        // ⚠ An alignment scope inside the delimiter's own, opened past the list's first break point.
        // See AlignsTypeParameters.
        var alignedInside = false;

        // ⚠ A nested positional list opens no scope, but its contents are still inside a list: a break
        // between its items is not a continuation any frame around it pays for. Held at zero columns, as
        // GroupPlan.HoldsLevel holds one — inside a property pattern's braces nothing else is open, and
        // the subpattern's frame paid a level for `, 3` (#532).
        var holding = false;
        foreach (var child in node.ChildNodesAndTokens()) {
            if (child.IsToken) {
                var token = child.AsToken();
                if (holding && token.SpanStart == close.SpanStart) {
                    EmitUpTo(close.SpanStart);
                    ReleaseContinuationLevel();
                    holding = false;
                }

                if (opened > 0 && token.SpanStart == close.SpanStart) {
                    // ⚠ A grouping parenthesis's or a tuple's `)` on a line of its own is a continuation
                    // line like any other (#442, SK-DIV-0203): measured, `p = (a + b` / `    );`,
                    // `return (a` / `    );` and `(a` / `    ).B();` take the statement's continuation
                    // level, and inside an argument list the item's line — while `typeof(int` / `);`
                    // comes back to its opener's line as an argument list's `)` does. So its scopes
                    // close before the gap, where the break can spend the statement's level.
                    var governsASwitch = KeepsAClosingParenthesisBeforeASwitch(node);
                    var continues = closer == 0
                        && node is ParenthesizedExpressionSyntax or TupleExpressionSyntax
                        && !governsASwitch;
                    if (continues) {
                        for (var i = opened; i > 0; i--) {
                            CloseIndent(scopeKind);
                        }

                        opened = 0;
                    }

                    EmitUpTo(close.SpanStart);
                    if (element) {
                        if (frames[^1].Activated) {
                            doc.Close();
                        }

                        frames.RemoveAt(frames.Count - 1);
                        continuousDepth = savedDepth;
                    }

                    // ⚠ The scopes the closing delimiter itself is inside stay open across it, and
                    // that is the whole of what `outside` means. `alignsCloser` is the `inside` and
                    // `none` shape — the closer takes the level of the line its opener was on — and
                    // it is exactly wrong for the other two, where the closer takes one more.
                    // ⚠ Except a list the oracle only fills whose first item shares its opener's line
                    // (#443): `(1, 2` / `    );`, `P(1, 2` / `    );` and `var (a, b` / `    ) = …` keep the
                    // closer one level in, where the author's break left it. A tuple that broke after
                    // its `(` still closes on its opener's level, `(` / `    a,` / `)`.
                    if (alignedInside) {
                        CloseIndent(IndentKind.Align);
                        alignedInside = false;
                    }

                    // ⚠ And it is one level past the opener's *line*, not the ambient level after the
                    // scope closes (#472): the closer is written inside the list's own scope. The two
                    // agree under an `=` or a statement, which is all #443 measured; they part where
                    // nothing else pays — `o is (1, 2` / `    );` under an arrow that already broke,
                    // `(int a, int b` / `    ) M()` and `delegate*<int, void` / `    > F` at a member's
                    // level, and `foreach (var (k, v` / `) in d)` four past the aligned column.
                    // ⚠ And so does a grouping parenthesis's or a tuple's `)` before a `switch`, which is
                    // not the statement's continuation line there but one level past the `(`'s, under an
                    // arrow and inside an argument list too (#506).
                    var keepsCloserIn = governsASwitch
                        || !continues
                        && (IsOnlyFilled(node) || node is TupleTypeSyntax or FunctionPointerParameterListSyntax)
                        && !HasLineBreak(open.Span.End, open.GetNextToken().SpanStart);
                    if (keepsCloserIn) {
                        pending = opened;
                    } else {
                        for (var i = opened; i > closer; i--) {
                            CloseIndent(scopeKind, closer == 0 && i == closer + 1);
                        }

                        pending = continues ? 0 : closer;
                    }

                    opened = 0;
                }

                EmitToken(token);

                for (var i = 0; i < pending; i++) {
                    CloseIndent(scopeKind);
                }

                pending = 0;

                if (levels == 0
                    && token.SpanStart == open.SpanStart
                    && IsNestedPositionalList(node)
                    && !AlignsFromOwnColumn(node)) {
                    HoldContinuationLevel();
                    holding = true;
                }

                if (opened == 0 && levels > 0 && token.SpanStart == open.SpanStart) {
                    EmitUpToTheAlignmentAnchor(node, scopeKind);
                    var brokenAfter = node is ParenthesizedExpressionSyntax grouping
                        && PrepayTheLevelOfAChainBrokenAfter(grouping);

                    for (var i = 0; i < levels; i++) {
                        // ⚠ Both scopes are unconditional when there are two, and it has to be both.
                        // `outside_and_inside` means "the contents take two levels" and both open on
                        // the opener's own line, where the writer's one-level-per-opening-line rule
                        // collapses them into one — and marking only the inner one unconditional
                        // does not help, because it then *blocks* the outer one at the same line.
                        // Measured: the oracle's `outside_and_inside` puts a chopped call's
                        // arguments eight columns in and its `)` four, and Skala wrote four and four
                        // under both of the other spellings.
                        OpenIndent(
                            scopeKind,
                            unconditional || inside > 1,
                            node is ParenthesizedExpressionSyntax
                                ? IndentFlags.Grouping | (brokenAfter ? IndentFlags.BrokenAfter : IndentFlags.None)
                                : IndentFlags.Delimiter
                        );
                    }

                    opened = levels;
                    if (AlignsTypeParameters(node)
                        && node is TypeParameterListSyntax { Parameters: [{ } parameter, ..] }) {
                        EmitLeadingGapAt(parameter.SpanStart);
                        OpenIndent(IndentKind.Align, true);
                        alignedInside = true;
                    }

                    if (element) {
                        savedDepth = continuousDepth;
                        continuousDepth = 0;
                        frames.Add(new(FrameKind.Unit, false));
                    }
                }
            } else if (child.AsNode() is { } inner) {
                VisitChild(node, inner);
            }
        }

        if (holding) {
            EmitUpTo(close.SpanStart);
            ReleaseContinuationLevel();
        }

        if (opened > 0) {
            EmitUpTo(close.SpanStart);
            if (alignedInside) {
                CloseIndent(IndentKind.Align);
            }

            if (element) {
                if (frames[^1].Activated) {
                    doc.Close();
                }

                frames.RemoveAt(frames.Count - 1);
                continuousDepth = savedDepth;
            }

            for (var i = 0; i < opened; i++) {
                CloseIndent(scopeKind);
            }
        }
    }

    /// <summary>
    ///     Whether <paramref name="node" /> is a positional pattern's or a designation's parentheses inside
    ///     another such list of the same pattern or declaration (#473).
    /// </summary>
    static bool IsNestedPositionalList(SyntaxNode node) {
        if (node is not (PositionalPatternClauseSyntax or ParenthesizedVariableDesignationSyntax)) {
            return false;
        }

        for (var ancestor = node.Parent; ancestor is not null; ancestor = ancestor.Parent) {
            switch (ancestor) {
                // ⚠ A property pattern's braces are a level the list nests inside too: `{ X: (2` / `, 3) }`
                // puts `, 3` on `X`'s column (#532).
                case PositionalPatternClauseSyntax
                    or ParenthesizedVariableDesignationSyntax
                    or PropertyPatternClauseSyntax:
                    return true;
                case PatternSyntax or SubpatternSyntax or VariableDesignationSyntax:
                    continue;
                default:
                    return false;
            }
        }

        return false;
    }

    /// <summary>
    ///     Whether <paramref name="node" /> is a grouping parenthesis or a tuple governing a switch
    ///     expression, with its first item on the <c>(</c>'s line and its <c>)</c> kept on a line of its
    ///     own (#506).
    /// </summary>
    bool KeepsAClosingParenthesisBeforeASwitch(SyntaxNode node) {
        if (node is not (ParenthesizedExpressionSyntax or TupleExpressionSyntax)
            || node.Parent is not SwitchExpressionSyntax owner
            || owner.GoverningExpression != node
            || AlignsFromOwnColumn(owner)
            || DelimiterLevels(ParenthesesStyleFor(node)).Closer != 0) {
            return false;
        }

        var open = node.GetFirstToken();
        var close = node.GetLastToken();
        return HasLineBreak(close.GetPreviousToken().Span.End, close.SpanStart)
            && !HasLineBreak(open.Span.End, open.GetNextToken().SpanStart);
    }

    /// <summary>
    ///     Whether a grouping parenthesis heads a chain the author broke before a dot after its
    ///     <c>)</c>, with no group of the chain's own to carry the break — and if so, spends now the
    ///     continuation level a frame would otherwise spend lazily at that dot.
    /// </summary>
    /// <remarks>
    ///     ⚠ Measured on fourteen shapes (#470, SK-DIV-0112, SK-DIV-0148): the oracle writes
    ///     <code>
    /// var z = (
    ///         a).B         ← two levels past the statement, not one
    ///     .C();
    /// var x = (y switch {
    ///         1 => 2,      ← two
    ///     }).Length        ← one, on the chain's continuation line
    ///     .Length;
    ///     </code>
    ///     and the same after <c>return</c>, as a bare statement, after an assignment, for a binary
    ///     inside the parenthesis, a property chain and a single <c>.B</c>. Where the break before the
    ///     dot spends nothing — under an arrow that already broke, inside an argument list — the
    ///     contents stay one level past the <c>(</c>'s line, on both sides. So the parenthesis's
    ///     scope is flagged <see cref="IndentFlags.BrokenAfter" /> and the writer lifts it to the next
    ///     line's level when that is deeper; and where that level is a frame's, paid lazily at the
    ///     dot, it has to exist before the parenthesis opens for the writer to see it. The frame
    ///     <see cref="FrameToSpend" /> names for the dot's break pays here instead, which leaves the
    ///     break itself nothing more to spend.
    ///     <para>
    ///         ⚠ Frames only. A chain with break points of its own has a group, and a group resolved
    ///         broken lifts the parenthesis through the writer's <c>LiftedLevel</c>, which reads the
    ///         fitter's answer rather than the author's — `(` / `a).B()` / `.C()` was already right.
    ///         An author's break before a dot that is not a point is kept by <c>keep_user_linebreaks</c>
    ///         and nothing else, so the source decides here.
    ///     </para>
    /// </remarks>
    bool PrepayTheLevelOfAChainBrokenAfter(ParenthesizedExpressionSyntax node) {
        if (!options.KeepUserLinebreaks) {
            return false;
        }

        SyntaxNode head = node;
        SyntaxToken broken = default;
        while (head.Parent is { } parent && IsReceiverOf(parent, head)) {
            if (broken.IsKind(SyntaxKind.None)) {
                var dot = parent switch {
                    MemberAccessExpressionSyntax access => access.OperatorToken,
                    ConditionalAccessExpressionSyntax conditional => conditional.OperatorToken,
                    _ => default
                };

                if (!dot.IsKind(SyntaxKind.None) && HasLineBreak(dot.GetPreviousToken().Span.End, dot.SpanStart)) {
                    broken = dot;
                }
            }

            head = parent;
        }

        if (broken.IsKind(SyntaxKind.None) || head == node || plan.ChainGroupOf(head) >= 0) {
            return false;
        }

        var frame = FrameToSpend(-1, broken);
        if (frame >= 0) {
            OpenIndent(IndentKind.Continuous);
            frames[frame] = frames[frame] with { Activated = true };
        }

        return true;

        static bool IsReceiverOf(SyntaxNode parent, SyntaxNode child) =>
            parent switch {
                MemberAccessExpressionSyntax access => access.Expression == child,
                InvocationExpressionSyntax invocation => invocation.Expression == child,
                ElementAccessExpressionSyntax element => element.Expression == child,
                ConditionalAccessExpressionSyntax conditional => conditional.Expression == child,
                PostfixUnaryExpressionSyntax postfix => postfix.Operand == child,
                _ => false
            };
    }

    /// <summary>
    ///     An attribute section's alignment column is the first attribute's, which is past a
    ///     <c>return:</c> target when there is one; the scope opens once that much is written. See
    ///     <see cref="IsAnAlignedAttributeSection" />.
    /// </summary>
    void EmitUpToTheAlignmentAnchor(SyntaxNode node, IndentKind scopeKind) {
        if (scopeKind == IndentKind.Align && node is AttributeListSyntax { Attributes: [{ } first, ..] }) {
            EmitLeadingGapAt(first.SpanStart);
        }
    }

    /// <summary>
    ///     <c>skala_align_multiline_type_parameter_list = true</c>: the parameters line up under the first
    ///     one, wherever it landed.
    /// </summary>
    /// <remarks>
    ///     ⚠ The scope opens inside the list, once the gap after the <c>&lt;</c> is written — and that gap
    ///     is the list's own first break point (#452, SK-DIV-0024). Opened around the node, the way
    ///     <see cref="Visit" /> opens every other alignment, the gap was written before the list's group
    ///     existed, and a group not yet entered renders its point flat: a single type parameter wider
    ///     than the margin stayed on a 125-column line. Measured with <c>jb cleanupcode</c> 2025.2.6 from
    ///     121 to 124 columns on a method's list of one and of two parameters: the oracle breaks after the
    ///     <c>&lt;</c> exactly as at <c>false</c>, the parameters one level in, and they align under the
    ///     first one there. Not under <c>skala_wrap_before_type_parameter_langle</c>, where the break is
    ///     before the <c>&lt;</c> and the list has no interior point to align.
    /// </remarks>
    bool AlignsTypeParameters(SyntaxNode node) =>
        node is TypeParameterListSyntax { Parameters.Count: > 0 }
        && options.AlignMultilineTypeParameterList
        && !options.WrapBeforeTypeParameterLangle;

    /// <summary>
    ///     How many levels a delimited construct's contents take, and how many its closing delimiter
    ///     takes, from the line its opening delimiter is on.
    /// </summary>
    /// <remarks>
    ///     ⚠ Measured, one key at a time, on a chopped call and a chopped parameter list whose closing
    ///     delimiter this export's wrap keys put on a line of its own. See
    ///     <see cref="PhaseOneOptions.IndentInvocationPars" /> for the table and for why three of the
    ///     seven keys were recorded inert and are not.
    /// </remarks>
    static (int Inside, int Closer) DelimiterLevels(ParenthesesIndentStyle style) =>
        style switch {
            ParenthesesIndentStyle.None => (0, 0),
            ParenthesesIndentStyle.Outside => (1, 1),
            ParenthesesIndentStyle.OutsideAndInside => (2, 1),
            _ => (1, 0)
        };

    /// <summary>
    ///     Whether this construct's closing delimiter is a break point of its own — a
    ///     <c>wrap_before_*_rpar</c> the export switched on — rather than a token that merely happens
    ///     to keep a line the author gave it.
    /// </summary>
    bool ClosesAtABreakPoint(SyntaxNode node, NodeLayout layout) {
        var (_, close) = DelimiterTokens(node, layout);
        return !close.IsKind(SyntaxKind.None)
            && plan.TryGap(close.SpanStart, out var spec)
            && spec.Rule == GapRule.Point;
    }

    /// <summary>Which of the family's keys governs this construct's delimiters.</summary>
    /// <remarks>
    ///     ⚠ A record's or class's parameter list is the primary constructor's and has a key of its
    ///     own, so the test is the parent and not the node. `skala_indent_pars` is the default arm — every
    ///     bracket, every grouping and tuple parenthesis, every pattern and attribute list.
    /// </remarks>
    /// <remarks>
    ///     ⚠ Two exemptions from <c>skala_indent_pars</c>, measured at <c>outside</c> and <c>none</c> (#508):
    ///     a grouping parenthesis is laid out as <c>inside</c> at every value — `Call(1, (a + b` / `)` keeps
    ///     its `)` on the item's line and `var g = (a` / `+ b);` its contents one level in — and a positional
    ///     pattern and a tuple ignore <c>none</c> (`o is (1,` / `2)` one level past the `is` line, `var u =
    ///     (1,` / `2);` one level in). <c>nameof(…)</c> is
    ///     the <c>typeof</c> family's, not an invocation's (#507): it follows <c>skala_indent_pars</c>.
    /// </remarks>
    ParenthesesIndentStyle ParenthesesStyleFor(SyntaxNode node) =>
        node switch {
            TypeArgumentListSyntax => options.IndentTypeargAngles,
            TypeParameterListSyntax => options.IndentTypeparamAngles,
            ParenthesizedExpressionSyntax => ParenthesesIndentStyle.Inside,
            PositionalPatternClauseSyntax or TupleExpressionSyntax when options.IndentPars
                == ParenthesesIndentStyle.None =>
                ParenthesesIndentStyle.Inside,
            ArgumentListSyntax { Parent: InvocationExpressionSyntax invocation } when BreakPlan.IsNameOf(invocation) =>
                options.IndentPars,
            ArgumentListSyntax => options.IndentInvocationPars,
            ParameterListSyntax { Parent: TypeDeclarationSyntax } => options.IndentPrimaryConstructorDeclPars,
            ParameterListSyntax => options.IndentMethodDeclPars,
            _ => options.IndentPars
        };

    /// <summary>
    ///     A statement whose embedded statement indents when it is not a block, and whose condition
    ///     parentheses are a continuation scope of their own.
    /// </summary>
    void VisitEmbedded(SyntaxNode node) {
        if (node is LabeledStatementSyntax labeled) {
            VisitLabeled(labeled);
            return;
        }

        var embedded = EmbeddedStatement(node);
        var (open, close) = ConditionParentheses(node);
        var parenScopes = 0;
        var parenPending = 0;

        // ⚠ A group opened *inside* the parentheses, at the column the header's clauses land on. The
        // one construct that asks for it is a `for` header under `skala_wrap_for_stmt_header_style`, and it
        // has to be an inner group for the reason BreakPlan.PlanForHeader records: a group around the
        // statement spans the body too, so its flat width is unbounded and it would break every time.
        var hasHeader = plan.TryInnerGroup(node, out var header);
        var headerOpen = false;

        foreach (var child in node.ChildNodesAndTokens()) {
            if (child.IsToken) {
                var token = child.AsToken();
                if (parenScopes > 0 && token.SpanStart == close.SpanStart) {
                    EmitUpTo(close.SpanStart);
                    if (headerOpen) {
                        doc.Close();
                        headerOpen = false;
                    }

                    parenPending = CloseConditionScopesBeforeRparen(parenScopes);
                    parenScopes = 0;
                }

                EmitToken(token);
                CloseConditionScopesAfterRparen(parenPending);
                parenPending = 0;

                if (parenScopes == 0 && !open.IsKind(SyntaxKind.None) && token.SpanStart == open.SpanStart) {
                    parenScopes = OpenConditionScopes();

                    if (hasHeader) {
                        // ⚠ The gap after the `(` is emitted before the group opens, the same order
                        // VisitBraced uses at the `{`: that gap belongs to whatever encloses the
                        // header, and a group that swallows it can never be flat.
                        EmitUpTo(open.GetNextToken().SpanStart);
                        doc.DescribeGroup(header.Id, header.Facts);
                        doc.OpenGroup(header.Mode, header.Id);
                        headerOpen = true;
                    }
                }

                continue;
            }

            if (child.AsNode() is not { } inner) {
                continue;
            }

            if (embedded is not null && inner == embedded && NeedsEmbeddedIndent(node, embedded)) {
                OpenIndent(IndentKind.Block);
                Visit(inner);
                CloseIndent(IndentKind.Block);
                continue;
            }

            VisitChild(node, inner);
        }

        if (parenScopes > 0) {
            EmitUpTo(close.SpanStart);
            if (headerOpen) {
                doc.Close();
            }

            for (var i = 0; i < parenScopes; i++) {
                CloseIndent(ConditionIndent);
            }
        }
    }

    /// <summary>
    ///     <c>Finish:</c> and the statement it labels, which sit at the same level.
    /// </summary>
    /// <remarks>
    ///     ⚠ Not an embedded statement, although <see cref="EmbeddedStatement" /> calls it one and every
    ///     other owner in that list really does indent its body. The oracle writes
    ///     <code>
    /// goto Finish;
    /// Finish:
    /// Console.Write(matched);
    ///     </code>
    ///     with all three lines flush, and Skala put the labelled statement one level in — a
    ///     divergence that was invisible because <c>goto</c> occurs a handful of times in the corpus.
    ///     <para>
    ///         <c>skala_outdent_statement_labels = true</c> then moves the label alone one level out, which is
    ///         the C-style <c>label:</c> convention, and is measured rather than inferred: it takes the
    ///         label from column 8 to column 4 and leaves the statement at 8.
    ///     </para>
    /// </remarks>
    void VisitLabeled(LabeledStatementSyntax node) {
        var outdented = options.OutdentStatementLabels;
        if (outdented) {
            OpenIndent(IndentKind.Outdent);
        }

        EmitToken(node.Identifier);
        EmitToken(node.ColonToken);

        // ⚠ The label's continuation ends at its colon: `d` / `    :` puts the statement on the label's
        // own column, not one level in with the colon (#433). The order VisitFileScopedNamespace uses.
        if (frames.Count > 0 && frames[^1].Activated) {
            CloseIndent(IndentKind.Continuous);
            frames[^1] = frames[^1] with { Activated = false };
        }

        if (outdented) {
            CloseIndent(IndentKind.Outdent);
        }

        Visit(node.Statement);
        EmitUpTo(node.Span.End);
    }

    /// <summary>
    ///     What a statement's condition parentheses open.
    /// </summary>
    /// <remarks>
    ///     ⚠ <c>skala_align_multiline_statement_conditions = true</c> — the export's value — lays the
    ///     condition out from the column just after the <c>(</c> rather than from an indent level:
    ///     <code>
    /// else if (ReflectionUtils.ImplementsGenericDefinition(
    ///              NonNullableUnderlyingType,      ← the `(`'s column plus one level
    ///              typeof(IEnumerable&lt;&gt;),
    ///              out tempCollectionType
    ///          )) {                                ← the `(`'s column
    ///     </code>
    ///     It is the one thing <see cref="IndentKind.Align" /> exists for, and SK-DIV-0008 recorded it
    ///     as unimplemented from milestone 1 until 3.1.
    /// </remarks>
    IndentKind ConditionIndent =>
        options.AlignMultilineStatementConditions
            ? IndentKind.Align
            : IndentKind.Continuous;

    /// <summary>
    ///     A statement condition's parenthesis levels: <c>skala_indent_statement_pars</c>, unless alignment
    ///     owns the column.
    /// </summary>
    /// <remarks>
    ///     ⚠ This is the whole of why <c>skala_indent_statement_pars</c> is inert under this export, and it
    ///     is a mask rather than a gap. <c>skala_align_multiline_statement_conditions = true</c> makes the
    ///     scope an <see cref="IndentKind.Align" /> one — an absolute column — and a level count has
    ///     nothing to say about a column. Measured: all four values return the same file while that key
    ///     is on. Turn it off and the family's table applies here like anywhere else.
    /// </remarks>
    (int Inside, int Closer) ConditionLevels =>
        ConditionIndent == IndentKind.Align ? (1, 0) : DelimiterLevels(options.IndentStatementPars);

    /// <summary>
    ///     The <c>_continuousDepth</c> each open statement header sits at, while its parentheses are
    ///     an <see cref="IndentKind.Continuous" /> scope rather than an aligned column.
    /// </summary>
    /// <remarks>
    ///     ⚠ It exists so that a construct with a level of its OWN — a chained call, a binary pattern
    ///     chain — does not add one on top of the header's when the header is paying a level rather
    ///     than naming a column. Measured at `skala_align_multiline_statement_conditions = false`, one key
    ///     flipped:
    ///     <code>
    /// foreach (var item in registry.Where(…)
    ///     .Select(Project)) {                       ← one level from the statement, not two
    /// } catch (Exception e) when (e is BindingResolutionException
    ///     or DocumentParseException) {
    ///     </code>
    ///     and Skala wrote both continuations at column 16. At <c>true</c> the header names an
    ///     absolute column and the chain's own level lands on top of it — the oracle puts
    ///     <c>.Select</c> four past the <c>(</c>'s column — so this applies to the
    ///     <see cref="IndentKind.Continuous" /> case alone.
    ///     <para>
    ///         ⚠ The depth and not a flag, because a construct further in is a different question. A
    ///         chain inside an argument list inside the header has a delimited scope between it and the
    ///         header, which raises <c>_continuousDepth</c>, and the chain's own level is spent again —
    ///         which is what the `if (Call(` case in the same fixture pins. <see cref="CanSpendAContinuationLevel" />
    ///         cannot answer this: it is false inside a header at BOTH values of the key, because
    ///         <see cref="OpenIndent" /> counts an aligned scope as a continuation too.
    ///     </para>
    /// </remarks>
    readonly List<int> continuousHeaders = [];

    /// <summary>Whether a group's <see cref="GroupPlan.OwnLevel" /> is the header's to pay.</summary>
    bool HeaderPaysForTheOwnLevel() => continuousHeaders.Count > 0 && continuousHeaders[^1] == continuousDepth;

    /// <summary>
    ///     <see cref="continuousHeaders" />' twin for an aligned header: the depth each open statement
    ///     condition's <see cref="IndentKind.Align" /> scope sits at, or −1 for a continuous one.
    /// </summary>
    readonly List<int> alignedHeaders = [];

    /// <summary>Whether nothing has opened a scope between the innermost aligned condition and here.</summary>
    bool DirectlyInAnAlignedHeader() => alignedHeaders.Count > 0 && alignedHeaders[^1] == continuousDepth;

    int OpenConditionScopes() {
        var (inside, _) = ConditionLevels;
        for (var i = 0; i < inside; i++) {
            // ⚠ An aligned condition's `)` on a line of its own goes under its `(` (#442, SK-DIV-0203).
            OpenIndent(
                ConditionIndent,
                true,
                ConditionIndent == IndentKind.Align ? IndentFlags.CloserAtOpener : IndentFlags.None
            );
        }

        // ⚠ Pushed only when a scope was actually opened, and popped by
        // CloseConditionScopesBeforeRparen on the same condition, which is the one close call that
        // runs exactly once per header. CloseConditionScopesAfterRparen runs on every token of the
        // walk with nothing pending and cannot own the pop.
        if (inside > 0) {
            continuousHeaders.Add(ConditionIndent == IndentKind.Continuous ? continuousDepth : -1);
            alignedHeaders.Add(ConditionIndent == IndentKind.Align ? continuousDepth : -1);
        }

        return inside;
    }

    /// <summary>
    ///     Closes the scopes the closing <c>)</c> is <em>not</em> inside, and returns how many it is —
    ///     those close after the token.
    /// </summary>
    int CloseConditionScopesBeforeRparen(int opened) {
        if (opened > 0 && continuousHeaders.Count > 0) {
            continuousHeaders.RemoveAt(continuousHeaders.Count - 1);
            alignedHeaders.RemoveAt(alignedHeaders.Count - 1);
        }

        var (_, closer) = ConditionLevels;
        for (var i = opened; i > closer; i--) {
            CloseIndent(ConditionIndent, closer == 0 && i == closer + 1);
        }

        return Math.Min(opened, closer);
    }

    void CloseConditionScopesAfterRparen(int pending) {
        for (var i = 0; i < pending; i++) {
            CloseIndent(ConditionIndent);
        }
    }

    void VisitSwitch(SwitchStatementSyntax node) {
        EmitToken(node.SwitchKeyword);
        if (!node.OpenParenToken.IsKind(SyntaxKind.None)) {
            EmitToken(node.OpenParenToken);
            var scopes = OpenConditionScopes();
            Visit(node.Expression);
            EmitUpTo(node.CloseParenToken.SpanStart);
            var pending = CloseConditionScopesBeforeRparen(scopes);
            EmitToken(node.CloseParenToken);
            CloseConditionScopesAfterRparen(pending);
        } else {
            Visit(node.Expression);
        }

        EmitToken(node.OpenBraceToken);

        // skala_indent_switch_labels = true: the labels take one indent from the switch.
        var labelled = options.IndentSwitchLabels;
        if (labelled) {
            OpenIndent(IndentKind.Block);
        }

        foreach (var section in node.Sections) {
            Visit(section);
        }

        EmitUpTo(node.CloseBraceToken.SpanStart);
        if (labelled) {
            CloseIndent(IndentKind.Block, true);
        }

        EmitToken(node.CloseBraceToken);
    }

    void VisitSwitchSection(SwitchSectionSyntax node) {
        // A simple section's group (BreakPlan.PlanCaseStatements) opens at the last label, so that
        // the required breaks between stacked labels stay outside it and do not make it unbounded.
        // ⚠ The label's own leading gap — the section's required break — has to be emitted before the
        // group opens too; `EmitUpTo` stops short of it, and inside the group it is a hard line that
        // made every section measure as unbounded and break at its label.
        var hasGroup = plan.TryInnerGroup(node, out var section);
        foreach (var label in node.Labels) {
            if (hasGroup && label == node.Labels[^1]) {
                EmitLeadingGap(label);
                doc.DescribeGroup(section.Id, section.Facts);
                doc.OpenGroup(section.Mode, section.Id);
            }

            Visit(label);
        }

        if (node.Statements.Count == 0) {
            return;
        }

        // ⚠ A case whose whole body is a block already gets its level from the braces. Adding the
        // section's own level too puts `case X: {` two levels above its contents, which is the
        // shape ReSharper does not produce.
        var braced = node.Statements is [BlockSyntax];
        if (braced) {
            foreach (var statement in node.Statements) {
                Visit(statement);
            }

            EmitUpTo(node.Span.End);
            return;
        }

        // The statements of a case take one indent from the label.
        OpenIndent(IndentKind.Block);
        foreach (var statement in node.Statements) {
            // ⚠ A block among several statements sits on the label's column, with its contents one level
            // in and the statements after it back on the section's level (#478, SK-DIV-0115). Measured
            // 2026-10-08: `case 1: { M(); } break;` comes back `case 1: {` / `M();` / `}` / `break;` and
            // `case 2: M(); { M(); } break;` puts the `{` and `}` on `case`'s column — the same place a
            // section that is only a block puts them.
            if (statement is BlockSyntax) {
                OpenIndent(IndentKind.Outdent);
                Visit(statement);
                CloseIndent(IndentKind.Outdent);
                continue;
            }

            // ⚠ skala_indent_break_from_case = false puts the control transfer back at the
            // label's own level, which is a different shape and not a rounding error.
            if (!options.IndentBreakFromCase
                && statement is BreakStatementSyntax or ContinueStatementSyntax or GotoStatementSyntax) {
                OpenIndent(IndentKind.Outdent);
                Visit(statement);
                CloseIndent(IndentKind.Outdent);
                continue;
            }

            Visit(statement);
        }

        EmitUpTo(node.Span.End);
        CloseIndent(IndentKind.Block);
        if (hasGroup) {
            doc.Close();
        }
    }

    // ── Indent scopes ────────────────────────────────────────────────────────────────────────

    void OpenIndent(IndentKind kind, bool unconditional = false, int columns = 0) =>
        OpenIndent(kind, unconditional, IndentFlags.None, columns);

    /// <param name="shape">
    ///     A delimited construct's scope: <see cref="IndentFlags.Grouping" /> or
    ///     <see cref="IndentFlags.Delimiter" />.
    /// </param>
    void OpenIndent(IndentKind kind, bool unconditional, IndentFlags shape, int columns = 0) {
        doc.OpenIndent(kind, unconditional, columns, shape);

        // ⚠ Neither an outdent kind is a continuation and neither is a block, so neither touches the
        // frame machinery. `OutdentColumns` shifts a column and spends no level at all, which is the
        // whole of what distinguishes it from the continuation scopes below.
        // ⚠ `None` joins them: it is a scope marker that changes no level, so it must not touch the
        // frame machinery either. It exists so that a construct whose contents take *zero* levels
        // still has a scope for its closing delimiter to be aligned against.
        // ⚠ `Anchor` is a marker too, and `AnchoredBlock` is a block in every respect this
        // bookkeeping cares about; only the writer reads the difference.
        if (kind is IndentKind.Outdent
            or IndentKind.OutdentColumns
            or IndentKind.None
            or IndentKind.Anchor
            or IndentKind.AnchoredBrace) {
            return;
        }

        // ⚠ `Single` belongs here and not below: it is a continuation scope whose width happens not
        // to be multiplied, so it composes and does not reset the continuation context.
        if (kind is IndentKind.Continuous or IndentKind.Align or IndentKind.OneLevel or IndentKind.FromLine) {
            continuousDepth++;
            return;
        }

        blockStack.Add(continuousDepth);
        continuousDepth = 0;

        // ⚠ A block is a frame boundary. A continuation level spent inside it must be closed inside
        // it too, or the document builder's Close pops the wrong container and the whole brace
        // structure of the file shifts by one.
        frames.Add(new(FrameKind.Unit, false));
    }

    /// <param name="alignsCloser">
    ///     The next piece is this scope's own closing delimiter and takes its opener's line level.
    /// </param>
    void CloseIndent(IndentKind kind, bool alignsCloser = false) {
        if (kind is IndentKind.Outdent
            or IndentKind.OutdentColumns
            or IndentKind.None
            or IndentKind.Anchor
            or IndentKind.AnchoredBrace) {
            doc.Close(alignsCloser);
            return;
        }

        if (kind is IndentKind.Continuous or IndentKind.Align or IndentKind.OneLevel or IndentKind.FromLine) {
            continuousDepth--;
        } else {
            if (frames[^1].Activated) {
                doc.Close();
            }

            frames.RemoveAt(frames.Count - 1);
            continuousDepth = blockStack[^1];
            blockStack.RemoveAt(blockStack.Count - 1);
        }

        doc.Close(alignsCloser);
    }

    // ── The piece stream ─────────────────────────────────────────────────────────────────────

    void EmitToken(SyntaxToken token) {
        if (token.IsKind(SyntaxKind.None) || token.IsMissing && token.Span.Length == 0) {
            return;
        }

        EmitUpTo(token.SpanStart);

        // ⚠ The piece has to be *this* token's, and a start position is not enough to say so.
        // `SourcePieces.Split` makes no piece for a zero-width token — the omitted sizes of
        // `int[,]`, a missing token in a partial tree — so such a token arrives here with the
        // *next* token's piece under the cursor, and it shares that token's start whenever no
        // trivia separates them. `byte[\n] f;` is exactly that: the omitted size sits at the `]`,
        // which then gets emitted here, one caller too early, from inside the bracket's
        // continuation scope instead of after it has been closed. The `]` came out at eight
        // columns and at four on the second pass, because a space before it in the source moves the
        // `]` off the omitted token's position and the collision stops happening (SK-FUZZ-0004).
        if (cursor < pieces.Length
            && pieces[cursor].Span.Start == token.SpanStart
            && pieces[cursor].Span.Length == token.Span.Length
            && pieces[cursor].Kind == PieceKind.Token) {
            EmitPiece(cursor++);
        }
    }

    void EmitUpTo(int position) {
        while (cursor < pieces.Length && pieces[cursor].Span.Start < position) {
            EmitPiece(cursor++);
        }
    }

    /// <summary>A whole node written from its original span: never reindented, never respaced.</summary>
    void EmitVerbatim(SyntaxNode node) {
        // ⚠ Inside a `@formatter:off` span everything was written as one raw chunk already — the
        // same check EmitPiece makes at the top, and for a sharper reason. The tree walk still
        // reaches every node between the tags, and this is the one arm that writes a node's *text*
        // instead of skipping it. An interpolated string between the tags was therefore written
        // twice, under a second anchor covering source the emitter had already covered, and
        // EditEmitter turned the overlap into an edit that deleted the rest of the file. The
        // token-stream check refused the write, so nothing was ever lost on disk — but the file
        // could not be formatted at all until the tag was taken out.
        if (node.SpanStart < verbatimUntil) {
            return;
        }

        EmitUpTo(node.SpanStart);

        // ⚠ And again, because `EmitUpTo` is what opens the span. The tag comment sits in this
        // node's *leading trivia*, so the piece that calls `EmitFormatterOffSpan` is emitted by the
        // line above — after the check at the top of this method has already passed. `_verbatimUntil`
        // was -1 on entry and covers the rest of the file on return, so the one check could never
        // see it and the node was written a second time over source the tag had already covered.
        // SK-FUZZ-0011: a member marked verbatim by PreprocessorGuard whose leading trivia carries
        // `@formatter:off` — `#if` … `// @formatter:off` … `void M() {` … `#endif`. Both halves are
        // needed: without the unbalanced `#if` this node is never emitted verbatim, and without the
        // tag `_verbatimUntil` never moves.
        if (node.SpanStart < verbatimUntil) {
            return;
        }

        var span = node.Span;
        if (span.Start != gapEmittedAt) {
            EmitGap(cursor, PieceKind.Token, span.Start, node.GetFirstToken());
        }

        MarkFramesStarted();

        var source = new SourceSpan(span.Start, span.Length);
        doc.Anchor(source, -1);
        // The node's first line takes the code's indentation; its interior lines are never
        // reindented, because the writer only indents at a line start and this text is one piece.
        // ⚠ An interpolated string on one line has its holes respaced (#492); its text never moves.
        doc.Verbatim(RespacedInterpolatedString(node) ?? this.source[span.Start..span.End], source);

        while (cursor < pieces.Length && pieces[cursor].Span.Start < span.End) {
            lastPiece = cursor;
            cursor++;
        }
    }

    void EmitPiece(int index) {
        var piece = pieces[index];

        // Inside a `@formatter:off` span everything was written as one raw chunk already.
        if (piece.Span.Start < verbatimUntil) {
            lastPiece = index;
            return;
        }

        if (piece.IsComment && FormatterTagGuard.IsOffTag(piece.Text, options.Tags)) {
            EmitFormatterOffSpan(index);
            return;
        }

        var token = piece.Kind == PieceKind.Token ? tokens[piece.TokenIndex] : default;

        // ⚠ Opened before the gap, so that a break in it lands inside, and closed after the token, so
        // that it covers that one line and no other.
        var extraOutdent = ExtraOutdentFor(token);
        if (extraOutdent > 0) {
            OpenIndent(IndentKind.OutdentColumns, columns: extraOutdent);
        }

        if (piece.Span.Start != gapEmittedAt) {
            EmitGap(index, piece.Kind, piece.Span.Start, token);
        }

        // ⚠ After the gap and before the text: a head marker records the line its token lands on,
        // and the gap before the token is where a break that moves it would be. Before the gap, a
        // declaration written on its own line under a kept break would read the previous line as
        // its own. See BreakPlan.TryMarker.
        if (piece.Kind == PieceKind.Token && plan.TryMarker(piece.Span.Start, out var marker)) {
            doc.OpenGroup(GroupMode.Flat, marker);
            doc.Close();
        }

        MarkFramesStarted();

        var span = new SourceSpan(piece.Span.Start, piece.Span.Length);
        doc.Anchor(span, piece.TokenIndex);

        switch (piece.Kind) {
            case PieceKind.Token:
                // ⚠ `skala_indent_raw_literal_string`, all three values. A multi-line raw literal's
                // interior and its closing delimiter move together, and the shift is uniform, so
                // the string's value is unchanged; see VerbatimFlags.Realign.
                doc.Text(piece.Text, span, RawLiteralFlags(piece));
                break;

            case PieceKind.DisabledText:
            case PieceKind.Skipped:
                // ⚠ Never reindented. Silently doing something clever here is how formatters
                // destroy code (docs/plan/04 § "Trivia").
                doc.Verbatim(piece.Text, span, VerbatimFlags.SelfIndented);
                break;

            // ⚠ Trimmed on the right. A directive's trailing whitespace is never part of anything,
            // the oracle removes it, and `#if HAVE_DYNAMIC` followed by twenty-eight spaces is real
            // code in the corpus — 71 lines across 14 files of `corpus/real/` were nothing but this.
            // The rest of a verbatim piece is still byte-for-byte.
            case PieceKind.ConditionalDirective:
                EmitDirective(piece, span, options.IndentPreprocessorIf);
                break;

            case PieceKind.OtherDirective:
                EmitDirective(piece, span, options.IndentPreprocessorOther);
                break;

            case PieceKind.RegionDirective:
                EmitDirective(piece, span, options.IndentPreprocessorRegion);
                break;

            case PieceKind.BlockComment:
            case PieceKind.BlockDocComment:
                EmitBlockComment(piece, span, LoneCommentAt(index) == LoneComment.ColumnZero);
                break;

            case PieceKind.DocCommentLine:
                // ⚠ `space_after_triple_slash` is read and deliberately not applied. Milestone 1
                // inserted the space; the oracle does not, on its own dedicated fixture
                // (`constructs/trivia/skala_space_after_triple_slash.cs` comes back with
                // `///<summary>` untouched) and nowhere else either. Applying it costs 79 lines
                // across 15 files of `corpus/real/`. See SK-DIV-0006: `jb cleanupcode`'s
                // CSReformatCode does not format doc comments at all.
                doc.Text(piece.Text, span, CommentFlags(piece));
                break;

            // ⚠ Not trimmed, and not respaced either. A comment's own text is the author's, and the
            // oracle leaves the trailing space on `// … during and ` exactly where it is. Only
            // directives are trimmed; see the ConditionalDirective arm above.
            //
            // ⚠ `space_before_trailing_comment_text` used to be read here and is not, because the C#
            // formatter does not answer to it. Measured on `M(); //x`, `M(); // y`, an own-line
            // `//z`, an own-line `// w` and a trailing `/*t*/` in one file, one key flipped at a time
            // over the export: at `true` and at `false` alike the oracle returns every one of them
            // byte-identical — `//x` never grows its space. The key sits in the export's unprefixed
            // block beside the C++ comment settings; the C# formatter has
            // `skala_space_before_trailing_comment`, which is the gap between the code and the `//` and is
            // a different key with its own fixture.
            case PieceKind.LineComment:
                doc.Text(piece.Text, span, CommentFlags(piece));
                break;

            default:
                doc.Text(piece.Text, span);
                break;
        }

        if (extraOutdent > 0) {
            CloseIndent(IndentKind.OutdentColumns);
        }

        lastPiece = index;
    }

    /// <summary>
    ///     The columns a wrapped line beginning with <paramref name="token" /> is pulled back past what its
    ///     chain's <c>skala_outdent_dots</c> scope already pulls it: the rest of a <c>?.</c>'s width.
    /// </summary>
    /// <remarks>
    ///     ⚠ The scope's amount is one chain-wide number, a <c>.</c>'s width, and the oracle outdents
    ///     each line by its own leading operator (#458, SK-DIV-0069): <c>?.SelectName(…)</c> at column 10
    ///     where the <c>.</c> lines sit at 11. The option's own meaning — pull the line back by the width
    ///     of the operator that starts it — says the same without the oracle.
    /// </remarks>
    int ExtraOutdentFor(SyntaxToken token) {
        if (dotOutdents == 0
            || !token.IsKind(SyntaxKind.QuestionToken)
            || token.Parent is not ConditionalAccessExpressionSyntax
            || !token.GetNextToken().IsKind(SyntaxKind.DotToken)) {
            return 0;
        }

        var dot = token.GetNextToken();
        return token.Text.Length + dot.Text.Length - 1;
    }

    /// <summary>
    ///     <c>skala_indent_raw_literal_string</c>: where a multi-line raw literal's closing delimiter — and
    ///     with it every interior line — is put.
    /// </summary>
    /// <remarks>
    ///     ⚠ Three values, and Skala used to write two: <c>indent</c> fell through to
    ///     <see cref="VerbatimFlags.None" /> and was therefore an alias of <c>do_not_change</c>, which
    ///     is what the sweep saw as the oracle producing three distinct outputs to Skala's two.
    ///     Measured on four literals in one file — one already aligned, one flush left, one
    ///     over-indented, one carrying a blank line — with the statement at eight columns:
    ///     <code>
    /// align          content at 16   the column of the opening quotes
    /// indent         content at 12   the opening LINE's indent, plus one level
    /// do_not_change  unmoved         every literal exactly as the author left it
    ///     </code>
    ///     ⚠ <c>indent</c> is a fact about the line and not about the quotes: all four literals land
    ///     at twelve however far right their opening quotes sit and however they were indented in the
    ///     source, which is what separates it from <c>align</c>.
    /// </remarks>
    VerbatimFlags RawLiteralFlags(Piece piece) {
        if (!tokens[piece.TokenIndex].IsKind(SyntaxKind.MultiLineRawStringLiteralToken)) {
            return VerbatimFlags.None;
        }

        return options.IndentRawLiteralString switch {
            RawStringIndentStyle.Align => VerbatimFlags.Realign,
            RawStringIndentStyle.Indent => VerbatimFlags.RealignToIndent,
            _ => VerbatimFlags.None
        };
    }

    /// <summary>
    ///     <c>skala_stick_comment = true</c> — "Don't indent comments started at first column": a comment the
    ///     author wrote hard against the left margin stays there; every other comment is indented with
    ///     the code around it.
    /// </summary>
    /// <remarks>
    ///     ⚠ This flag used to hang off <c>skala_place_comments_at_first_column</c>, and it was the wrong key
    ///     in both directions. That key is <em>inert</em> under <c>cleanupcode</c> — it governs the
    ///     editor's comment-out action, and the oracle returns the probe below byte-identical at
    ///     <c>true</c> and at <c>false</c> — while Skala at <c>true</c> pinned <em>every</em>
    ///     line-starting comment to column 0, which is a comment moved to the left margin in real code
    ///     and no oracle behaviour behind it. The sweep called that row <c>SPURIOUS</c> and it was
    ///     right.
    ///     <para>
    ///         ⚠ The key that does govern it is this one, measured on the same probe, one flip:
    ///         <code>
    /// void M() {
    /// // already at column zero          → stays at column zero at `true`, indented to 8 at `false`
    ///     // indented as its owner       → unmoved at both
    ///   // badly indented                → indented to 8 at both
    ///         </code>
    ///         So "at the first column" is literal and is a fact about the <em>source</em>, not about
    ///         `StartsLine`: a comment with any whitespace before it on its line is ordinary and gets
    ///         the code's indent. The oracle applies it to block comments as well as line comments.
    ///     </para>
    /// </remarks>
    /// <summary>
    ///     <c>skala_align_multiline_comments</c>: whether this block comment's asterisks are the formatter's to
    ///     move. SK-DIV-0033.
    /// </summary>
    /// <remarks>
    ///     ⚠ <b>The qualifying shape is narrow and it was measured rather than guessed, twice.</b> The
    ///     first reading — "a comment whose lines begin with <c>*</c>" — is right only if "whose lines"
    ///     means <em>every</em> continuation line. Asked of <c>jb cleanupcode</c> 2025.2.6 under
    ///     <c>OracleProfile.FormatOnly</c> at <c>true</c>:
    ///     <list type="bullet">
    ///         <item>every continuation line starred, however ragged — realigned to the opener's column + 1;</item>
    ///         <item>first line starred, second not — <b>returned exactly as written</b>;</item>
    ///         <item>first line unstarred, second starred — returned exactly as written;</item>
    ///         <item>
    ///             ⚠ every line starred but with an <em>empty</em> line among them — returned exactly as
    ///             written. An empty line is a line that does not begin with <c>*</c>, and it disqualifies the
    ///             comment like any other. This is the case a "does it look like a javadoc block" heuristic
    ///             would get wrong, and it is why the loop below has no exemption for blank lines.
    ///         </item>
    ///     </list>
    ///     The closing <c>*/</c>'s line counts as a starred line and is realigned with the rest, which is
    ///     what makes a comment with an unstarred body — whose last line is still <c>*/</c> — come out
    ///     unqualified rather than half-moved.
    /// </remarks>
    /// <summary>
    ///     A <c>/* … */</c> or <c>/** … */</c> comment: its first line takes the code's indentation, and
    ///     what its other lines do is one of three answers, all measured (issue #428).
    /// </summary>
    /// <remarks>
    ///     Asked of <c>jb cleanupcode</c> 2025.2.6 under <c>SkalaFormatOnly</c>, at both values of
    ///     <c>skala_align_multiline_comments</c>, with <c>indent_style</c> space and tab:
    ///     <list type="number">
    ///         <item>
    ///             ⚠ <b>Frozen</b> — a comment whose every continuation line begins with <c>*</c>, at
    ///             <c>false</c>, <c>/*</c> and <c>/**</c> alike. The oracle leaves it byte for byte, its
    ///             opener's column and its trailing whitespace included. Skala still moves the opener
    ///             (SK-DIV-0033, fact 1) and leaves the rest verbatim, which is the nearest it comes.
    ///         </item>
    ///         <item>
    ///             <b>Aligned</b> — the same shape, <c>/*</c> only, at <c>true</c>: every continuation line
    ///             on the opener's column plus one (<see cref="StarredFlag" />).
    ///         </item>
    ///         <item>
    ///             <b>Shifted</b> — everything else, ⚠ a starred <c>/**</c> at <c>true</c> included: a
    ///             ragged one keeps its raggedness and moves with its line
    ///             (<see cref="VerbatimFlags.ShiftWithLine" />). SK-DIV-0094.
    ///         </item>
    ///     </list>
    ///     ⚠ Outside the frozen class every line of the comment loses its trailing whitespace, and a
    ///     whitespace-only line becomes empty — whether or not the comment moved (SK-DIV-0193). A <c>//</c>
    ///     or <c>///</c> comment keeps its trailing whitespace, so this is a block-comment rule and not
    ///     <c>trim_trailing_whitespace</c>.
    /// </remarks>
    /// <param name="lone">
    ///     ⚠ The comment is all an empty argument or parameter list holds (#509): the oracle writes it at
    ///     column 0 on a line of its own, its other lines moving with that line.
    /// </param>
    void EmitBlockComment(Piece piece, SourceSpan span, bool lone = false) {
        var text = piece.Text;
        var starred = IsStarredBlockComment(text);
        var flags = CommentFlags(piece) | (lone ? VerbatimFlags.AtColumnZero : VerbatimFlags.None);
        if (text.IndexOf('\n', StringComparison.Ordinal) < 0 || starred && !options.AlignMultilineComments) {
            doc.Verbatim(text, span, flags);
            return;
        }

        text = TrimLineEnds(text);
        if (piece.Kind == PieceKind.BlockComment && starred) {
            doc.Verbatim(text, span, flags | StarredFlag(piece));
            return;
        }

        doc.Verbatim(text, span, flags | VerbatimFlags.ShiftWithLine, SourceLineIndent(piece.Span.Start));
    }

    /// <summary>
    ///     How a block comment spanning lines that is all an empty argument or parameter list holds is
    ///     laid out — <c>Foo(/* a</c> / <c>b */)</c> — or <see cref="LoneComment.None" /> for any other piece.
    /// </summary>
    /// <remarks>
    ///     ⚠ Measured (#509, SK-DIV-0209): the oracle writes <c>Foo(</c> / <c>/* a</c> at column 0 /
    ///     <c>b */</c> moved by as much as the comment's line moved / <c>)</c> on the opener's level, for a
    ///     call, an object creation, a constructor initializer and a method's or a constructor's
    ///     parameters alike. Column 0 looks like a quirk, and it is the oracle's answer: every other
    ///     placement differs from it on two lines rather than none. ⚠ Narrow, and each edge measured: a
    ///     <c>/** */</c> comment takes a line of its own at the list's level instead; a lambda's parameter
    ///     list keeps the comment after its <c>(</c> and puts only the <c>)</c> on a line of its own; a
    ///     comment the author already put on a line of its own stays there (the oracle adds a blank line
    ///     before it, which Skala does not); and a comment beside an argument, <c>Foo(/* a</c> /
    ///     <c>b */ x)</c>, stays after the <c>(</c>. An initializer keeps it after its <c>{</c> (SK-DIV-0209).
    /// </remarks>
    LoneComment LoneCommentAt(int index) {
        if (index <= 0
            || index + 1 >= pieces.Length
            || pieces[index].Kind is not (PieceKind.BlockComment or PieceKind.BlockDocComment or PieceKind.LineComment)
            || pieces[index - 1].Kind != PieceKind.Token
            || pieces[index + 1].Kind != PieceKind.Token) {
            return LoneComment.None;
        }

        var open = tokens[pieces[index - 1].TokenIndex];
        var close = tokens[pieces[index + 1].TokenIndex];
        if (!open.IsKind(SyntaxKind.OpenParenToken)
            || !close.IsKind(SyntaxKind.CloseParenToken)
            || open.Parent != close.Parent
            || open.Parent is not (ArgumentListSyntax { Arguments.Count: 0 }
                or ParameterListSyntax { Parameters.Count: 0 })) {
            return LoneComment.None;
        }

        var lambda = open.Parent.Parent is ParenthesizedLambdaExpressionSyntax;

        // ⚠ A comment the author already put on a line of its own gets a blank line before it (#533):
        // `Foo(` / `` / `/* a */` / `)`, for a block, a doc and a line comment, one line or several, in an
        // argument and a parameter list. Unmeasured in a lambda's parameter list, which is left alone.
        // ⚠ Except a block comment already at column 0, which is this rule's own output: the oracle is not
        // idempotent there — given its answer back it adds the blank line — and Skala must be, so the
        // column-0 layout is kept as the fixed point of the first pass, whose answer it is.
        // ⚠ And a `/** */` comment on its own line, for the same reason: it is where the doc comment
        // beside a `(` goes, and the blank line the oracle adds on its second pass would be Skala's. That
        // one row of #533 — an author's own-line `/** */` — keeps no blank line, and differs.
        if (pieces[index].StartsLine) {
            return lambda ? LoneComment.None
                : pieces[index].Kind == PieceKind.BlockComment
                    && LineStart(pieces[index].Span.Start) == pieces[index].Span.Start
                    ? LoneComment.ColumnZero
                    : pieces[index].Kind == PieceKind.BlockDocComment
                        ? LoneComment.OwnLine
                        : LoneComment.BlankLineBefore;
        }

        // ⚠ Beside the `(`, a comment that ends its line — spanning lines, or with the `)` on the next —
        // goes to column 0: `Foo(/* a */` / `)` as much as `Foo(/* a` / `b */)` (#533). One that does not
        // end its line, `Foo(/* a */)`, stays.
        var endsItsLine = pieces[index].Text.Contains('\n')
            || HasLineBreak(pieces[index].Span.End, pieces[index + 1].Span.Start);
        if (pieces[index].Kind == PieceKind.LineComment || !endsItsLine) {
            return LoneComment.None;
        }

        return lambda ? LoneComment.CloserOnly
            : pieces[index].Kind == PieceKind.BlockDocComment ? LoneComment.OwnLine
            : LoneComment.ColumnZero;
    }

    /// <summary>The three layouts <see cref="LoneCommentAt" /> answers with.</summary>
    enum LoneComment {
        None,

        /// <summary>A lambda's parameter list: the comment stays beside the <c>(</c>, the <c>)</c> moves down.</summary>
        CloserOnly,

        /// <summary>A <c>/** */</c> comment: a line of its own at the list's level, and the <c>)</c> on another.</summary>
        OwnLine,

        /// <summary>A <c>/* */</c> comment: a line of its own at column 0, and the <c>)</c> on another.</summary>
        ColumnZero,

        /// <summary>A comment already on a line of its own: a blank line before it, and the <c>)</c> on another.</summary>
        BlankLineBefore
    }

    /// <summary>The leading whitespace of the source line <paramref name="position" /> is on.</summary>
    string SourceLineIndent(int position) {
        var start = position;
        while (start > 0 && source[start - 1] is not ('\n' or '\r')) {
            start--;
        }

        var end = start;
        while (end < position && source[end] is ' ' or '\t') {
            end++;
        }

        return source[start..end];
    }

    /// <summary>Removes the spaces and tabs that end each line of a multi-line comment but its last.</summary>
    static string TrimLineEnds(string text) {
        var builder = new StringBuilder(text.Length);
        var lineStart = 0;
        for (var i = 0; i < text.Length; i++) {
            if (text[i] != '\n') {
                continue;
            }

            var end = i > lineStart && text[i - 1] == '\r' ? i - 1 : i;
            var trimmed = end;
            while (trimmed > lineStart && text[trimmed - 1] is ' ' or '\t') {
                trimmed--;
            }

            builder.Append(text, lineStart, trimmed - lineStart).Append(text, end, i + 1 - end);
            lineStart = i + 1;
        }

        return builder.Append(text, lineStart, text.Length - lineStart).ToString();
    }

    VerbatimFlags StarredFlag(Piece piece) =>
        options.AlignMultilineComments && IsStarredBlockComment(piece.Text)
            ? VerbatimFlags.AlignStarred
            : VerbatimFlags.None;

    /// <summary>Whether every continuation line of a block comment begins with <c>*</c>.</summary>
    static bool IsStarredBlockComment(string text) {
        var newLine = text.IndexOf('\n', StringComparison.Ordinal);
        if (newLine < 0) {
            return false;
        }

        for (var i = newLine; i < text.Length; i = text.IndexOf('\n', i + 1)) {
            if (i < 0) {
                break;
            }

            var start = i + 1;
            while (start < text.Length && text[start] is ' ' or '\t') {
                start++;
            }

            // ⚠ End of text, an empty line and a whitespace-only line all fail here, and all three are
            // meant to: none of them begins with `*`.
            if (start >= text.Length || text[start] != '*') {
                return false;
            }
        }

        return true;
    }

    VerbatimFlags CommentFlags(Piece piece) =>
        options.StickComment && piece.StartsLine && LineStart(piece.Span.Start) == piece.Span.Start
            ? VerbatimFlags.AtColumnZero
            : VerbatimFlags.None;

    /// <summary>
    ///     Whether either side of the gap is a directive Roslyn reports from inside an inactive
    ///     <c>#if</c> branch — see <see cref="Piece.Inactive" />.
    /// </summary>
    bool TouchesInactiveBranch(Piece previous, int nextPieceIndex) =>
        previous.Inactive
        || nextPieceIndex >= 0 && nextPieceIndex < pieces.Length && pieces[nextPieceIndex].Inactive;

    /// <summary>
    ///     One preprocessor directive, at the column <c>indent_preprocessor_{if,other,region}</c> asks
    ///     for.
    /// </summary>
    /// <remarks>
    ///     ⚠ Four values, and until the key-flip sweep Skala wrote only two of them: everything that
    ///     was not <c>usual_indent</c> went to column 0. That made <c>outdent</c> and
    ///     <c>do_not_change</c> both aliases of <c>no_indent</c>, and the sweep saw it as the oracle
    ///     producing three distinct outputs to Skala's two.
    ///     <list type="bullet">
    ///         <item><c>no_indent</c> — column 0.</item>
    ///         <item><c>usual_indent</c> — the code's own indent.</item>
    ///         <item>
    ///             ⚠ <c>outdent</c> — the code's indent less one level, and NOT column 0. Measured:
    ///             with the guarded statement at eight columns the oracle returns <c>#if</c> and
    ///             <c>#endif</c> at four. It is <see cref="IndentKind.Outdent" /> exactly, so it is
    ///             spelled as that scope rather than as arithmetic here.
    ///         </item>
    ///         <item>
    ///             ⚠ <c>do_not_change</c> — the column the author wrote, which is not the same claim as
    ///             column 0 and was only passing because the fixture's directives were already there.
    ///             Measured on one file carrying <c>#if</c> at eight columns and another at zero: the
    ///             oracle returns each where it found it.
    ///         </item>
    ///     </list>
    /// </remarks>
    void EmitDirective(Piece piece, SourceSpan span, PreprocessorIndentStyle style) {
        var text = piece.Text.TrimEnd();

        switch (style) {
            case PreprocessorIndentStyle.UsualIndent:
                doc.Verbatim(text, span);
                return;

            case PreprocessorIndentStyle.Outdent:
                OpenIndent(IndentKind.Outdent);
                doc.Verbatim(text, span);
                CloseIndent(IndentKind.Outdent);
                return;

            case PreprocessorIndentStyle.DoNotChange when piece.StartsLine:
                // The author's own indentation, carried in the text so that the writer adds none.
                var start = LineStart(piece.Span.Start);
                doc.Verbatim(source[start..piece.Span.Start] + text, span, VerbatimFlags.SelfIndented);
                return;

            default:
                doc.Verbatim(text, span, VerbatimFlags.AtColumnZero);
                return;
        }
    }

    /// <summary>Every open frame has now seen a piece of its own.</summary>
    void MarkFramesStarted() {
        for (var i = frames.Count - 1; i >= 0 && !frames[i].Started; i--) {
            frames[i] = frames[i] with { Started = true };
            if (!frames[i].ResetsDepth) {
                continue;
            }

            continuousDepth = 0;

            // ⚠ Every frame the same walk has just started sits inside this one, so it restores to
            // the reset value rather than to the depth it captured before the reset happened.
            for (var j = i + 1; j < frames.Count; j++) {
                frames[j] = frames[j] with { SavedDepth = 0 };
            }
        }
    }

    void EmitFormatterOffSpan(int index) {
        // The escape hatch. It must work on the first attempt or people stop trusting the tool.
        //
        // ⚠ The region starts at the beginning of the tag comment's own *line*, not at the comment.
        // Measured: `jb cleanupcode` leaves a twelve-space `// @formatter:off` at twelve spaces
        // inside a class body it would otherwise indent to four. Starting at the comment re-indented
        // the line the author wrote the tag on, which is the one line they can be certain they meant.
        // A tag in a *trailing* comment does not extend backwards — the oracle formats the code
        // before it on that line, and so does this.
        var piece = pieces[index];
        var start = piece.StartsLine ? LineStart(piece.Span.Start) : piece.Span.Start;
        var end = source.Length;
        for (var i = index + 1; i < pieces.Length; i++) {
            if (pieces[i].IsComment && FormatterTagGuard.IsOnTag(pieces[i].Text, options.Tags)) {
                end = pieces[i].Span.End;
                break;
            }
        }

        EmitGap(index, PieceKind.LineComment, start, default);
        var span = new SourceSpan(start, end - start);
        doc.Anchor(span, -1);

        // The chunk now carries the tag line's own indentation, so the writer must not add its own.
        doc.Verbatim(
            source[start..end],
            span,
            piece.StartsLine ? VerbatimFlags.SelfIndented : VerbatimFlags.None
        );

        verbatimUntil = end;
        lastPiece = index;
    }

    /// <summary>The offset of the first character of the line <paramref name="position" /> is on.</summary>
    int LineStart(int position) {
        var start = position;
        while (start > 0 && source[start - 1] is ' ' or '\t') {
            start--;
        }

        // ⚠ Only whitespace is walked back over, and only to a line boundary. A tag comment that
        // follows something other than indentation on its line is not at the start of a line, and
        // `Piece.StartsLine` has already said so — this is the second half of the same statement.
        return start > 0 && source[start - 1] is not ('\n' or '\r') ? position : start;
    }

    // ⚠ "Which comment is a tag" used to be answered here too, by a private `ContainsTag`. It is
    // `FormatterTagGuard.IsOffTag` / `IsOnTag` now and nowhere else: the four keys turned out to
    // compose in a way — the built-in tags surviving `tags_enabled = false`, the configured pair
    // being additive — that two spellings of the answer would have got wrong in two ways.

    // ── Gaps ─────────────────────────────────────────────────────────────────────────────────

    void EmitGap(int nextPieceIndex, PieceKind nextKind, int nextStart, SyntaxToken nextToken) {
        if (lastPiece < 0) {
            // Anything before the first piece is the file's prologue: a BOM, leading blank lines.
            // No anchor precedes it, so the emitter keeps it byte-for-byte.
            return;
        }

        var previous = pieces[lastPiece];
        var gap = source[previous.Span.End..nextStart];
        var newLines = CountNewLines(gap);

        // ⚠ A gap that touches disabled text is copied byte-for-byte. Roslyn's DisabledTextTrivia
        // begins immediately after the opening directive's newline and swallows every blank line
        // next to it, so adding or removing one here does not move whitespace — it rewrites the
        // inactive branch, which Skala never does (docs/plan/04 § "Trivia").
        // ⚠ …and so is a gap that touches a directive *inside* the inactive branch (SK-FUZZ-0016).
        // Roslyn does not fold those into DisabledTextTrivia — `#region`, `#pragma`, a nested `#if`
        // in a branch that is not compiled all stay structured — so without Piece.Inactive the gap
        // rules see two ordinary directives and `skala_blank_lines_around_region` writes a blank line
        // between them. That line is not spacing: re-parsed, it is a DisabledTextTrivia that was not
        // there before, and the safety net aborts the file with SK9099. The branch is opaque whether
        // or not Roslyn kept its contents structured.
        if (nextKind == PieceKind.DisabledText
            || previous.Kind == PieceKind.DisabledText
            && newLines > 0
            || TouchesInactiveBranch(previous, nextPieceIndex)) {
            if (gap.Length > 0) {
                doc.Verbatim(gap, new(previous.Span.End, gap.Length), VerbatimFlags.AtColumnZero);
            }

            return;
        }

        // A disabled block always ends with its own newline, so the whitespace between it and the
        // directive that closes the branch is not part of it and the directive indents normally.
        if (previous.Kind == PieceKind.DisabledText) {
            return;
        }

        // ── The two line-break suppressions ──────────────────────────────────────────────────
        //
        // ⚠ Both are resolved here, above the break plan, because this method is the one funnel: a
        // line break is added or removed at a gap and nowhere else, so answering here is the whole
        // implementation rather than the first of several sites. It is the same argument
        // `ResolveBlankLines` makes for the blank-line half, and the reason the family shares one
        // mechanism instead of acquiring a suppression mode in the writer.
        //
        // ⚠ The two are a superset and a subset, and conflating them would leave one of them green
        // and unimplemented. `disable_line_break_changes` decides the gap in *both* directions from
        // the source — the author wrote a break, so there is one; the author wrote none, so there is
        // none — which is what "no line break is added and none removed" means (SK-DIV-0063).
        // `disable_line_break_removal` decides only the first: a gap the author broke is broken, and
        // a gap the author did not break falls through to every rule below, so the wrapping still
        // adds breaks the author never wrote (SK-DIV-0064).
        // ⚠ Both arms are unreachable for disabled text, which the two returns above have already
        // taken: an inactive `#if` branch is copied byte for byte and no key here reaches inside it.
        if (options.DisableLineBreakChanges && newLines == 0) {
            EmitFlatGap(previous, nextKind, nextToken, gap);
            return;
        }

        // ⚠ A lone comment spanning lines in an empty argument or parameter list takes a line of its
        // own, and so does the `)` after it (#509). See LoneCommentAt.
        if (!options.DisableLineBreakChanges
            && (nextPieceIndex >= 0
                && LoneCommentAt(nextPieceIndex) is LoneComment.OwnLine
                    or LoneComment.ColumnZero
                    or LoneComment.BlankLineBefore
                || LoneCommentAt(lastPiece) != LoneComment.None)) {
            var blankBefore = nextPieceIndex >= 0 && LoneCommentAt(nextPieceIndex) == LoneComment.BlankLineBefore;
            Break(nextPieceIndex, nextToken, blankBefore ? 1 : 0, DefaultNewLine());
            return;
        }

        if ((options.DisableLineBreakChanges || options.DisableLineBreakRemoval) && newLines > 0) {
            Break(
                nextPieceIndex,
                nextToken,
                ResolveBlankLines(previous, nextPieceIndex, nextToken, newLines - 1),
                options.EnforceLineEndingStyle ? DefaultNewLine() : FirstNewLine(gap) ?? DefaultNewLine()
            );

            return;
        }

        // ⚠ The break plan only ever governs a gap between two tokens. A comment or a directive in
        // the gap makes it untouchable: joining `a + // note` with `b` puts `b` inside the comment,
        // and breaking before a directive moves code across it.
        // ⚠ …except for the one comment that can stand between a point and its token: a block comment
        // after the token before the point. See PointSurvivesComments for what the oracle does there.
        // ⚠ A labelled statement starts its own line whatever the plan or the brace rules say, and in
        // both directions: `a: { M(); }` and `a: { }` are broken here before a block's plan could keep
        // the `{` flat, and `a:` / `{ }` is never joined back by the brace placement (#433). See
        // StartsALabelledStatement.
        if (previous.Kind == PieceKind.Token
            && nextKind == PieceKind.Token
            && tokens[previous.TokenIndex] is { RawKind: (int)SyntaxKind.ColonToken, Parent: LabeledStatementSyntax }
            && StartsALabelledStatement(nextToken)) {
            Break(
                nextPieceIndex,
                nextToken,
                ResolveBlankLines(previous, nextPieceIndex, nextToken, Math.Max(0, newLines - 1)),
                newLines == 0
                ? DefaultNewLine()
                : options.EnforceLineEndingStyle ? DefaultNewLine() : FirstNewLine(gap) ?? DefaultNewLine()
            );

            return;
        }

        var spec = default(GapSpec);
        var planned = (previous.Kind == PieceKind.Token || PointSurvivesComments(lastPiece, nextStart))
            && nextKind == PieceKind.Token
            && plan.TryGap(nextStart, out spec);

        // ⚠ An array initializer's first element joins a block comment on its own line above it (#522):
        // `new[] {` / `/* c */` / `1` comes back `/* c */ 1`. The `{`'s break is already taken before the
        // comment, so the point after it has nothing left to break. Measured for `/* */` and `/** */`, one
        // comment or two on a line, an implicit, explicit and field-initializer array, a nested element;
        // not after `{ /* c */` on the brace's line, and not in a collection, object, anonymous or `with`
        // initializer, a collection expression or a property pattern, which all keep the break.
        if (planned && previous.Kind != PieceKind.Token && FirstArrayElementUnderAnOwnLineComment(lastPiece)) {
            EmitFlatGap(previous, nextKind, nextToken, gap);
            return;
        }

        if (planned) {
            // ⚠ A preserved run goes *before* the point rather than into its flat rendering, because
            // a point's flat form is one space or nothing and the run may be wider. The writer
            // discards a pending gap at a break it takes, so the run appears when the point stays
            // flat and vanishes when it breaks — the same answer `flatSpace` would have given, at
            // the width the author wrote.
            var preserved = PreservedRun(gap);

            switch (spec.Rule) {
                case GapRule.Point:
                case GapRule.FillPoint:
                case GapRule.LastResortPoint:
                case GapRule.YieldingFillPoint:
                case GapRule.FollowingPoint:
                    if (preserved is not null) {
                        doc.Space(preserved);
                    }

                    doc.BreakPoint(
                        spec.Group,
                        PointFlags(spec.Rule, previous, nextKind, nextToken, gap, preserved is null),
                        ResolveBlankLines(previous, nextPieceIndex, nextToken, Math.Max(0, newLines - 1)),
                        newLines == 0
                        ? DefaultNewLine()
                        : options.EnforceLineEndingStyle ? DefaultNewLine() : FirstNewLine(gap) ?? DefaultNewLine()
                    );
                    return;

                case GapRule.Flat:
                    EmitFlatGap(previous, nextKind, nextToken, gap);
                    return;

                default:
                    // ⚠ The requirement is resolved even when the source gap held no newline, and
                    // that is a correction rather than a tidy-up. A break the *rules* introduce —
                    // one member per line — creates a gap the blank-line requirements have an
                    // opinion about, and skipping them because the author wrote no newline there
                    // makes the first pass emit no blank and the second emit one:
                    // `int A => 1;    int B => 2;` is not idempotent under the old reading.
                    Break(
                        nextPieceIndex,
                        nextToken,
                        ResolveBlankLines(previous, nextPieceIndex, nextToken, Math.Max(0, newLines - 1)),
                        newLines == 0
                        ? DefaultNewLine()
                        : options.EnforceLineEndingStyle ? DefaultNewLine() : FirstNewLine(gap) ?? DefaultNewLine()
                    );
                    return;
            }
        }

        if (newLines == 0) {
            if (MustBreak(previous, nextKind, nextToken)) {
                Break(nextPieceIndex, nextToken, 0, DefaultNewLine());
                return;
            }

            EmitFlatGap(previous, nextKind, nextToken, gap);
            return;
        }

        if (ShouldJoin(previous, nextKind, nextToken) || JoinsWithoutKeptBreaks(previous, nextKind, nextToken)) {
            EmitFlatGap(previous, nextKind, nextToken, gap);
            return;
        }

        // ⚠ enforce_line_ending_style = false means an existing ending is kept, mixed endings
        // included; true normalises every break to end_of_line.
        Break(
            nextPieceIndex,
            nextToken,
            ResolveBlankLines(previous, nextPieceIndex, nextToken, newLines - 1),
            options.EnforceLineEndingStyle ? DefaultNewLine() : FirstNewLine(gap) ?? DefaultNewLine()
        );

        // An author's break nothing planned: the draft measure reads it as a space (SK-DIV-0208).
        doc.FlagLastLine(LineFlags.KeptBreak);
    }

    /// <summary>
    ///     Whether the block comments ending at <paramref name="lastPieceIndex" /> follow a non-empty array
    ///     initializer's <c>{</c> and at least one of them starts a line. See #522 in <see cref="EmitGap" />.
    /// </summary>
    bool FirstArrayElementUnderAnOwnLineComment(int lastPieceIndex) {
        var startsLine = false;
        for (var i = lastPieceIndex; i >= 0; i--) {
            var piece = pieces[i];
            switch (piece.Kind) {
                case PieceKind.BlockComment or PieceKind.BlockDocComment:
                    startsLine |= piece.StartsLine;
                    continue;
                case PieceKind.Token:
                    return startsLine
                        && tokens[piece.TokenIndex] is { RawKind: (int)SyntaxKind.OpenBraceToken } open
                        && open.Parent is InitializerExpressionSyntax { Expressions.Count: > 0 } array
                        && array.IsKind(SyntaxKind.ArrayInitializerExpression);
                default:
                    return false;
            }
        }

        return false;
    }

    string DefaultNewLine() =>
        options.LineEnding switch {
            LineEnding.Crlf => "\r\n",
            LineEnding.Cr => "\r",
            _ => "\n"
        };

    /// <summary>
    ///     Whether the break point planned before the next token is still a point when the gap holds
    ///     block comments — the run of pieces back from <paramref name="lastPieceIndex" /> to the
    ///     previous token is block comments and nothing else.
    /// </summary>
    /// <remarks>
    ///     ⚠ The oracle puts the break <em>after</em> the comments, never before them: given
    ///     <c>name175: nameof(value), /* f */ name176: …</c> past the margin it writes
    ///     <c>name175: nameof(value), /* f */</c> / <c>name176: …</c>, and the same for every list it
    ///     re-lays (arguments, parameters, attribute arguments, type arguments, base types,
    ///     declarators, enum members, switch arms), for a break before <c>)</c> or <c>]</c>, after
    ///     <c>{</c> or <c>[</c>, after <c>=</c> and a lambda's arrow, and before a binary operator, a
    ///     <c>?</c> and a <c>.</c>. A comment on a line of its own (<c>alpha,</c> / <c>/* f */ beta</c>)
    ///     is no different: the oracle breaks after it too. Skala used to leave every such gap
    ///     unplanned, so the group lost the point: the line overflowed, the only breaks left on it were
    ///     inside the items, and pass two — reading those breaks back — re-decided them (#409).
    ///     <para>
    ///         ⚠ Two tokens are the exception, and both are measured, not assumed. After <c>(</c>:
    ///         <c>Compute( /* f */ alpha, beta, …)</c> chopped stays <c>Compute( /* f */ alpha,</c> with
    ///         only the later items on lines of their own. After an expression body's <c>=&gt;</c>:
    ///         <c>int P =&gt; /* f */ Compute(alpha, …)</c> chops the call where the same line without
    ///         the comment breaks after the arrow. In both the oracle's wrap stops at the comment, and
    ///         breaks past it only when the item cannot fit any other way (<c>Compute( /* f */</c> /
    ///         <c>"…"</c>, <c>=&gt; /* f */</c> / <c>"…"</c>), which Skala does not (SK-DIV-0165).
    ///         Leaving those two gaps unplanned keeps the common case.
    ///     </para>
    ///     <para>
    ///         ⚠ A third: the gap after a declaration's last attribute section. The placement keys'
    ///         break does not survive a comment there in either direction (#434,
    ///         <see cref="EndsAnAttributeRun" />).
    ///     </para>
    ///     <para>
    ///         ⚠ And when a comment in the run spans lines, the tokens that head a single value — an
    ///         <c>=</c>, an arrow, a named argument's colon — do not carry their point past it (#435,
    ///         <see cref="StopsAtAMultiLineComment" />).
    ///     </para>
    ///     <para>
    ///         A line comment is never in the run: the gap after it holds a newline the point would be
    ///         free to join, and joining puts the token inside the comment. Neither is a directive, a
    ///         <c>///</c> documentation comment or a formatter tag.
    ///     </para>
    ///     <para>
    ///         ⚠ A <c>/** … */</c> comment is in the run (#415, SK-DIV-0180). Roslyn lexes <c>/**</c> as
    ///         documentation wherever it stands, inside an argument list included, and the oracle treats
    ///         it as the block comment it looks like: <c>/** single */ public int F;</c> and
    ///         <c>/** s1 */ E();</c> are broken after the comment exactly as <c>/* … */</c> is, under
    ///         <c>SkalaFormatOnly</c> and <c>SkalaDocComments</c> alike.
    ///     </para>
    /// </remarks>
    bool PointSurvivesComments(int lastPieceIndex, int nextStart) {
        var lineComment = false;
        var spansLines = false;
        for (var i = lastPieceIndex; i >= 0; i--) {
            var piece = pieces[i];
            switch (piece.Kind) {
                case PieceKind.BlockComment or PieceKind.BlockDocComment
                    when !FormatterTagGuard.IsOffTag(piece.Text, options.Tags)
                    && !FormatterTagGuard.IsOnTag(piece.Text, options.Tags):
                    spansLines |= piece.Text.AsSpan().IndexOfAny('\n', '\r') >= 0;
                    continue;
                case PieceKind.LineComment when i != lastPieceIndex:
                    // Only on the way to the file's start, below: a `//` header above the run.
                    lineComment = true;
                    continue;
                case PieceKind.Token:
                    return !lineComment
                        && i != lastPieceIndex
                        && (!StopsAtAComment(tokens[piece.TokenIndex]) || plan.PlansPastALeadingComment(nextStart))
                        && (!EndsAnAttributeRun(tokens[piece.TokenIndex])
                            || plan.PlansPastAnAttributeComment(nextStart))
                        && !(spansLines && StopsAtAMultiLineComment(tokens[piece.TokenIndex]));
                default:
                    return false;
            }
        }

        // ⚠ The run reached the file's start: `/* fs */ namespace FS;` on the first line, or under a
        // `//` header. Nothing but comments precede the token, and the planned gap is after the last
        // block comment, never after a `//` — so it is the same gap as after one (#429), and the oracle
        // breaks after the comment there as anywhere.
        return true;
    }

    /// <summary>
    ///     The two tokens whose wrap the oracle does not carry past a comment after them: <c>(</c> and
    ///     an expression body's <c>=&gt;</c>. See <see cref="PointSurvivesComments" />.
    /// </summary>
    /// <remarks>
    ///     ⚠ The list's or the arrow's own wrap stops there; a point of its own past the comment does not,
    ///     and <see cref="BreakPlan.PlansPastALeadingComment" /> names it (#486).
    /// </remarks>
    static bool StopsAtAComment(SyntaxToken token) =>
        token.IsKind(SyntaxKind.OpenParenToken)
        || token.IsKind(SyntaxKind.EqualsGreaterThanToken)
        && token.Parent is ArrowExpressionClauseSyntax;

    /// <summary>
    ///     The tokens whose break point does not survive a block comment that spans lines (#435): an
    ///     <c>=</c> or a compound assignment, a lambda's or a switch arm's <c>=&gt;</c>, and a named
    ///     argument's <c>:</c>.
    /// </summary>
    /// <remarks>
    ///     ⚠ Measured: <c>int y = /* a</c> / <c>b */ 1;</c> keeps <c>1</c> after the comment, and so does
    ///     every <c>=</c> asked — a local, a field, a property initializer, an assignment, <c>+=</c> and
    ///     <c>??=</c>, a parameter default, an attribute's named argument, an anonymous object's and a
    ///     <c>with</c>'s member, a <c>for</c> header, a <c>using</c> declaration — and a lambda's and a
    ///     switch arm's arrow and a named argument's colon. ⚠ Not even when the value cannot fit: the
    ///     oracle wraps inside the value instead (<c>b */ Compute(</c> / the arguments chopped,
    ///     <c>b */ aaa</c> / <c>+ bbb</c>), where after a one-line comment it breaks after the comment
    ///     (SK-DIV-0165). A break the author wrote after the comment is kept, and so is one before it.
    ///     <para>
    ///         ⚠ Not every point does this, so it is not "never break after a multi-line comment". A
    ///         chopped list still breaks after one — <c>M(</c> / <c>1, /* a</c> / <c>b */</c> / <c>2</c>
    ///         — and so does a chopped binary operator or closer whose operand ends in one; a fill
    ///         (<c>new int[] { 1, /* c</c> / <c>d */ 2 }</c>) decides by what fits after it, as Skala's fill
    ///         already did. Each of those is a point of a list that breaks at every point, which is
    ///         where the comment's own lines make the oracle chop; these tokens head a single value.
    ///     </para>
    /// </remarks>
    static bool StopsAtAMultiLineComment(SyntaxToken token) =>
        token.IsKind(SyntaxKind.EqualsToken)
        || token.Parent is AssignmentExpressionSyntax assignment
        && assignment.OperatorToken == token
        || token.IsKind(SyntaxKind.EqualsGreaterThanToken)
        || token.IsKind(SyntaxKind.IsKeyword)
        && token.Parent is BinaryExpressionSyntax or IsPatternExpressionSyntax
        || token.IsKind(SyntaxKind.OpenBraceToken)
        && token.Parent is InitializerExpressionSyntax { Expressions.Count: > 0 } array
        && array.IsKind(SyntaxKind.ArrayInitializerExpression)
        || token.IsKind(SyntaxKind.OpenBracketToken)
        && token.Parent is CollectionExpressionSyntax { Elements.Count: > 0 }
        || token.IsKind(SyntaxKind.AsKeyword)
        && token.Parent is BinaryExpressionSyntax
        || token.IsKind(SyntaxKind.ColonToken)
        && token.Parent is NameColonSyntax;

    /// <summary>
    ///     Whether <paramref name="token" /> is the <c>]</c> of the last attribute section before what the
    ///     sections decorate, so that a block comment after it leaves the gap to the author (#434).
    /// </summary>
    /// <remarks>
    ///     ⚠ Measured on every owner the placement keys name — method, constructor, destructor, operator,
    ///     indexer, property, event, field, accessor, type, delegate, local function, record parameter —
    ///     and under every value: <c>[Obsolete] /* c */ public void M() { }</c> stays on one line at
    ///     <c>never</c>, and <c>[Obsolete] /* c */</c> / <c>public void M() { }</c> stays on two at
    ///     <c>always</c>, <c>if_owner_is_single_line</c> and with
    ///     <c>skala_keep_existing_attribute_arrangement = true</c>. A comment the author put on the next
    ///     line (<c>[Obsolete]</c> / <c>/* c */ public void M()</c>) keeps both its breaks the same way.
    ///     The attribute's own point does not survive the comment, so nothing is added and nothing taken
    ///     away; a declaration too long for the line chops its parameters with the attribute still on it.
    ///     <para>
    ///         ⚠ Only the <em>last</em> section's gap. Between two sections the point does survive:
    ///         <c>[Obsolete] /* c */ [Serializable] public void M()</c> comes back as
    ///         <c>[Obsolete] /* c */</c> / <c>[Serializable]</c> / <c>public void M()</c> at <c>never</c>
    ///         and joined at <c>always</c>, so the token after the comment decides, not the comment.
    ///         <c>[assembly: A] /* c */ [assembly: B]</c> is the same between-sections case at the top level.
    ///     </para>
    /// </remarks>
    static bool EndsAnAttributeRun(SyntaxToken token) =>
        token.IsKind(SyntaxKind.CloseBracketToken)
        && token.Parent is AttributeListSyntax
        && token.GetNextToken() is var next
        && !(next.IsKind(SyntaxKind.OpenBracketToken) && next.Parent is AttributeListSyntax);

    /// <summary>
    ///     Emits a break, spending the statement's one continuous indent level if this is the break
    ///     that needs it.
    /// </summary>
    /// <remarks>
    ///     ⚠ <c>continuous_line_indent = single</c>: one level, and only where no delimited group is
    ///     already providing one. <c>if (a &amp;&amp;\n b)</c> takes the parenthesis's level and not a
    ///     second one; <c>var y = a\n + b</c> has no parenthesis and takes the statement's.
    /// </remarks>
    void Break(int nextPieceIndex, SyntaxToken nextToken, int blanks, string newLine) {
        var frame = FrameToSpend(nextPieceIndex, nextToken);
        if (frame >= 0) {
            // ⚠ A parenthesis the author broke the line after, heading a body, is laid out like a
            // brace: `f = () =>\n(\n    1, 2);` and `return\n(\n    1, 2);` put the `(` at the
            // statement's own indent. The frame's level is held at zero rather than left unspent —
            // the same answer BreakPlan.PlanExpressionBody and PlanAroundEquals give through
            // GroupPlan.HoldsLevel, for the breaks a frame pays for: a lambda's arrow and a
            // statement's own continuation, which never go through a group. The frame closes it
            // as it closes any level it spent (SK-DIV-0101).
            // ⚠ And under `wrap_if_long` held only while the chain after the `)` stays whole, which
            // the writer decides (HeldLevel.WhileChainWhole, issue #407).
            if (nextToken.IsKind(SyntaxKind.OpenParenToken)
                && HeadsABodyWithAChoppedParenthesis(nextToken, out var fillChain)) {
                var chain = fillChain is null ? -1 : plan.ChainGroupOf(fillChain);
                if (chain >= 0) {
                    doc.OpenHeldIndent(IndentKind.Continuous, IndentFlags.HeldWhileChainWhole, chain);
                    continuousDepth++;
                } else {
                    HoldContinuationLevel();
                }
            } else {
                // ⚠ A chain's level is lifted like a delimited list's when it opens on the first line
                // of a construct that broke after it (#481): `((point` / `.X` / `- x)` / `* …)` puts
                // `.X` a level past the `- x` line, as SK-DIV-0148's rule says for a block. Without it
                // the chain collapsed into the grouping it opened beside, which the grouping's
                // unconditional scope used to hide.
                OpenIndent(
                    IndentKind.Continuous,
                    false,
                    frames[frame].Kind == FrameKind.Chain ? IndentFlags.Delimiter : IndentFlags.None
                );
            }

            frames[frame] = frames[frame] with { Activated = true };
        }

        doc.Line(LineKind.Hard, blanks, newLine);

        // ⚠ A required break in front of an array initializer's element — after a `//` comment — is
        // where that element starts, and no fill point saw it (#444, SK-DIV-0208).
        if (StartsAFilledElement(nextToken)) {
            doc.FlagLastLine(LineFlags.ArrayElement);
        }
    }

    /// <summary>
    ///     Which open frame, if any, pays for this break's continuation level.
    /// </summary>
    /// <remarks>
    ///     ⚠ The walk goes outward, because a chain frame answers only for a break before its own
    ///     <c>.</c>: in
    ///     <code>
    /// public int M() =&gt;
    ///     Helper.Compute(x);
    ///     </code>
    ///     the innermost frame at the break is the chain, and the level is the <em>member's</em> to
    ///     spend. Stopping at the innermost frame leaves the body flush with its declaration.
    /// </remarks>
    int FrameToSpend(int nextPieceIndex, SyntaxToken nextToken) {
        // ⚠ A comment run that introduces a chain link is that link's break (#523): the chain pays its
        // level at the comment, and the link under it on the comment's column pays nothing more. Measured:
        // `var y = a` / `// c` / `.B();` puts `.B()` on the comment's column, one level in, blank line above
        // the comment or not, `//` or `/* */`, `.` or `?.`; Skala paid the statement's level for the
        // comment and the chain's again for the dot. In an argument (`Call(a` / `// c` / `.B())`) it was
        // the comment that came out a level short.
        if (nextToken.IsKind(SyntaxKind.None) && nextPieceIndex >= 0) {
            for (var i = nextPieceIndex; i < pieces.Length; i++) {
                if (pieces[i].Kind == PieceKind.Token) {
                    var under = tokens[pieces[i].TokenIndex];
                    if (IsChainLinkStart(under)) {
                        nextToken = under;
                    }

                    break;
                }

                if (!pieces[i].IsComment) {
                    break;
                }
            }
        }

        var beforeDot = IsChainLinkStart(nextToken);

        for (var i = frames.Count - 1; i >= 0; i--) {
            if (!frames[i].Started) {
                continue;
            }

            // ⚠ A first declarator's frame does not pay for the break before its own name (#420). A
            // comment behind the type is written inside the declarator's leading gap, which starts
            // the frame early; paying here left the statement's level unspent and the declarator's
            // spent, so the `=` after the name could spend nothing: `string /* c */` / `    s =` /
            // `    "…";` where the oracle writes the value two levels in.
            if (frames[i].PaysNotBefore == nextToken.SpanStart) {
                continue;
            }

            if (frames[i].Activated) {
                return -1;
            }

            // ⚠ An aligned frame pays for nothing, and it stops the walk rather than passing the
            // break outward: the Align scope under it is an absolute column, so a level spent by an
            // enclosing frame would be discarded by the writer anyway and only the bookkeeping
            // would differ.
            if (frames[i].Aligned) {
                return -1;
            }

            if (frames[i].Kind == FrameKind.Chain) {
                // ⚠ A chain that holds its level pays nothing for its dots and passes the break
                // outward, so a statement whose own continuation is still unspent pays for it and an
                // arrow or `=` that has already spent does not: `(\n a).B\n.C();` as a statement puts
                // `.C()` one level in, and under `=>` on the `(`'s own column. See Frame.HoldsLevel.
                if (beforeDot && !frames[i].HoldsLevel) {
                    // ⚠ And a chain whose group has already opened its level (PlanChainedCalls'
                    // OwnLevel, a continuation scope over the whole chain) pays nothing more for an
                    // author's break before a dot that is not a point. Measured on #380: the oracle
                    // keeps `alpha.Foo(a)` / `.Bar()` / `.Baz` / `.Qux()` at one level throughout,
                    // and `Get()["k"]` / `.Members` / `.Select(…)` / `.OrderBy(…)` likewise; this
                    // frame used to spend a second level at `.Qux` and `.Select`, which the writer's
                    // same-line collapse hid only while no point break came before the kept one
                    // (`alpha.Members` / `.Select(…)` / `.OrderBy(…)` was fine, `alpha.Foo(a)` /
                    // `.Bar()` / `.Baz` / `.Qux()` was not). The level is paid; nothing outside
                    // this chain pays either.
                    return continuousDepth > frames[i].EntryDepth ? -1 : i;
                }

                continue;
            }

            if (frames[i].Kind == FrameKind.Pattern) {
                if (nextToken.Parent is BinaryPatternSyntax pattern && pattern.OperatorToken == nextToken) {
                    return i;
                }

                continue;
            }

            if (frames[i].PaysAt == nextToken.SpanStart) {
                return i;
            }

            // ⚠ A break before an `=` spends the level inside a delimited scope too — the frame-side
            // half of GroupPlan.SpendsUnderDelimiters. `void D(int a\n = 5)` chops the list and the
            // oracle puts `= 5` one level past `int a`; the depth rule alone left it flush.
            var spendsUnderDelimiters = nextToken.IsKind(SyntaxKind.EqualsToken)
                && nextToken.Parent is EqualsValueClauseSyntax { Parent: ParameterSyntax };
            return (continuousDepth == 0 || spendsUnderDelimiters) && IsContinuation(nextPieceIndex, nextToken)
                ? i
                : -1;
        }

        return -1;
    }

    /// <summary>
    ///     The call a chain link starting at <paramref name="token" /> ends in — <c>.Other(…)</c>, or a
    ///     property run feeding one, <c>.Count.ToString()</c> — or null when the link is a property.
    /// </summary>
    static InvocationExpressionSyntax? CallLinkAt(SyntaxToken token) {
        if (!token.IsKind(SyntaxKind.DotToken) || token.Parent is not MemberAccessExpressionSyntax access) {
            return null;
        }

        SyntaxNode link = access;
        while (link.Parent is MemberAccessExpressionSyntax outer && outer.Expression == link) {
            link = outer;
        }

        return link.Parent is InvocationExpressionSyntax call && call.Expression == link ? call : null;
    }

    static bool IsChainLinkStart(SyntaxToken token) =>
        token.IsKind(SyntaxKind.DotToken)
        || token.IsKind(SyntaxKind.QuestionToken)
        && token.Parent is ConditionalAccessExpressionSyntax;

    enum FrameKind {
        Unit,
        Chain,
        Pattern
    }

    /// <param name="Started">
    ///     ⚠ False until the frame's own first piece is emitted. A break that lands <em>before</em> a
    ///     construct belongs to whatever encloses it, not to the construct: the break after
    ///     <c>M() =&gt;</c> is the member's to pay for even though the lambda that follows has already
    ///     been entered.
    /// </param>
    /// <param name="HoldsLevel">
    ///     A chain frame that spends nothing for a break before one of its dots and hands the break to
    ///     the frames outside it: a call chain headed by a parenthesised expression or a tuple
    ///     (SK-DIV-0112). The frame's other duties are unchanged.
    /// </param>
    /// <param name="EntryDepth">
    ///     The continuation depth when the frame was pushed. A chain frame spends for a break before
    ///     one of its dots only while the depth is still this one — once the chain's own group has
    ///     opened its level inside the frame, the level is paid.
    /// </param>
    readonly record struct Frame(
        FrameKind Kind,
        bool Activated,
        bool Started = false,
        bool ResetsDepth = false,
        int SavedDepth = 0,
        bool Aligned = false,
        bool HoldsLevel = false,
        int EntryDepth = 0,
        int PaysNotBefore = -1,
        int PaysAt = -1);

    /// <summary>
    ///     Whether the break continues an expression rather than starting a new statement, member or
    ///     list element.
    /// </summary>
    bool IsContinuation(int nextPieceIndex, SyntaxToken nextToken) {
        if (nextToken.IsKind(SyntaxKind.None)) {
            // A comment or a directive takes the indentation of whatever it introduces.
            if (nextPieceIndex < 0) {
                return false;
            }

            for (var i = nextPieceIndex + 1; i < pieces.Length; i++) {
                if (pieces[i].Kind == PieceKind.Token) {
                    return IsContinuation(-1, tokens[pieces[i].TokenIndex]);
                }
            }

            return false;
        }

        // ⚠ A grouping parenthesis's or a tuple's `)` kept on a line of its own is a continuation line
        // of the statement, and the statement's frame pays for it where nothing has yet (#505):
        // `return (a` / `    );` and `(a` / `    ).B();` take one level, as `p = (a + b` / `    );`
        // already did through the `=`. Under an arrow that already broke, the `)` stays on the `(`'s
        // line's level, because the frame has paid. VisitDelimited's `continues` closes the
        // parenthesis's scopes before this break for the same reason.
        // ⚠ Only where the first item shares the `(`'s line: a list broken after its `(` closes on its
        // opener's level, `return (` / `    a,` / `);` (#443) — Lint drift on Skala's own source.
        if (nextToken.IsKind(SyntaxKind.CloseParenToken)
            && nextToken.Parent is ParenthesizedExpressionSyntax or TupleExpressionSyntax
            && DelimiterLevels(ParenthesesStyleFor(nextToken.Parent)).Closer == 0
            && nextToken.Parent.GetFirstToken() is var opener
            && !HasLineBreak(opener.Span.End, opener.GetNextToken().SpanStart)) {
            return true;
        }

        return !StartsAUnit(nextToken);
    }

    /// <summary>
    ///     Whether this <c>(</c> opens the parenthesis <see cref="BreakPlan.HeadsWithAChoppedParenthesis" />
    ///     describes, for a body whose break is a frame's rather than a group's: a lambda's expression
    ///     body, or the expression of a <c>return</c>, <c>throw</c> or <c>yield return</c>.
    /// </summary>
    /// <remarks>
    ///     ⚠ The walk goes up from the token to the body root along the same left spine the plan's
    ///     predicate walks down, and asks that predicate at the top — so the two cannot disagree about
    ///     which shapes qualify. An arrow clause's or an equals clause's body is excluded here because
    ///     those are the group's to decide and their break never reaches <see cref="Break" />.
    /// </remarks>
    bool HeadsABodyWithAChoppedParenthesis(SyntaxToken open, out ExpressionSyntax? fillChain) {
        fillChain = null;
        if (open.Parent is not (ParenthesizedExpressionSyntax or TupleExpressionSyntax)) {
            return false;
        }

        var body = (ExpressionSyntax)open.Parent;
        while (body.Parent is ExpressionSyntax parent && IsLeftSpineOf(body, parent)) {
            body = parent;
        }

        var owned = body.Parent switch {
            AnonymousFunctionExpressionSyntax lambda => lambda.ExpressionBody == body,
            ReturnStatementSyntax statement => statement.Expression == body,
            ThrowStatementSyntax statement => statement.Expression == body,
            YieldStatementSyntax statement => statement.Expression == body,
            _ => false
        };

        return owned && BreakPlan.HeadsWithAChoppedParenthesis(body, source, options, out fillChain);
    }

    static bool IsLeftSpineOf(ExpressionSyntax child, ExpressionSyntax parent) =>
        parent switch {
            MemberAccessExpressionSyntax access => access.Expression == child,
            InvocationExpressionSyntax invocation => invocation.Expression == child,
            ElementAccessExpressionSyntax element => element.Expression == child,
            ConditionalAccessExpressionSyntax conditional => conditional.Expression == child,
            SwitchExpressionSyntax switchExpression => switchExpression.GoverningExpression == child,
            ConditionalExpressionSyntax ternary => ternary.Condition == child,
            PostfixUnaryExpressionSyntax postfix => postfix.Operand == child,
            _ => false
        };

    /// <summary>
    ///     True when the token begins something the layout treats as its own line: a statement, a
    ///     member, a list element, a label, a clause — or a closing delimiter, which has already
    ///     outdented by the time it is written.
    /// </summary>
    static bool StartsAUnit(SyntaxToken token) {
        if (token.Kind() is SyntaxKind.CloseBraceToken
            or SyntaxKind.CloseParenToken
            or SyntaxKind.CloseBracketToken
            or SyntaxKind.GreaterThanToken
            or SyntaxKind.OpenBraceToken) {
            return true;
        }

        // ⚠ A property-pattern subpattern's value lands on the subpattern's OWN column when the break
        // after its `:` is taken — no continuation level, unlike every other undelimited continuation
        // here. SK-DIV-0081, and it was left open until a second measurement agreed with the first:
        // three now do, aligned at the export's margin, un-aligned at a 60-column margin and nested
        // inside another property pattern. See BreakPlan.PlanSubpattern.
        //
        // ⚠ The loop stops as soon as the token is no longer the node's first token, so it is bounded
        // by the depth of the value expression rather than by the file's — the same shape the walk
        // below uses.
        for (var node = token.Parent; node is not null && node.GetFirstToken() == token; node = node.Parent) {
            if (node.Parent is SubpatternSyntax subpattern && subpattern.Pattern == node) {
                return true;
            }

            // ⚠ So does the operand after a spread's or a slice pattern's `..` when the author broke
            // after it: `1, ..` / `a` puts `a` on the element's column, not one level in (#439).
            if (node.Parent is SpreadElementSyntax spread
                && spread.Expression == node
                || node.Parent is SlicePatternSyntax slice
                && slice.Pattern == node) {
                return true;
            }
        }

        // ⚠ A `do` statement's trailing `while` starts its own line at the `do`'s level, exactly as
        // `else`, `catch` and `finally` do below — but those three have a clause node whose first
        // token they are, and this one is a keyword sitting directly in `DoStatementSyntax`. So the
        // walk read it as a continuation of the `do` and spent a level on it. Measured: at
        // `skala_new_line_before_while = true` the oracle puts `while (b);` flush with its `do` and Skala
        // had it four columns in — the whole of that key's sweep row.
        if (token.IsKind(SyntaxKind.WhileKeyword) && token.Parent is DoStatementSyntax) {
            return true;
        }

        // ⚠ A kept break before a subpattern's or a named argument's colon puts the colon on its name's
        // own column, with no level of its own: `X` / `: 1` in a property pattern, `a` / `: 1` in a
        // chopped argument list (#436).
        if (token.IsKind(SyntaxKind.ColonToken) && token.Parent is BaseExpressionColonSyntax) {
            return true;
        }

        SyntaxNode? child = null;
        for (var node = token.Parent; node is not null; node = node.Parent) {
            if (node.GetFirstToken() != token) {
                // ⚠ Two cases the "first token of a node" test misses, and both are common enough
                // to move the fidelity number on their own:
                //   an element of an initializer, whose enclosing node starts at the brace; and
                //   `[Test]` then `public void M()`, where the member's first token is the
                //   attribute's bracket but `public` still starts a line of its own. The second
                //   keeps walking rather than answering, because the node that carries the
                //   attributes may itself be an element of something.
                if (child is not null && IsListElement(child)) {
                    return true;
                }

                if (FirstTokenAfterAttributes(node) != token) {
                    return false;
                }
            }

            if (IsUnit(node)) {
                return true;
            }

            child = node;
        }

        return false;
    }

    /// <summary>
    ///     ⚠ A list pattern's elements are list elements too, and leaving it out of this test is only
    ///     visible once something forces a break between them: <c>o is [\n 1,\n 2\n]</c> put the
    ///     second element one level deeper than the first, because the break before it was read as a
    ///     continuation of an expression rather than as the start of an element.
    /// </summary>
    static bool IsListElement(SyntaxNode child) =>
        child.Parent is InitializerExpressionSyntax
            or CollectionExpressionSyntax
            or ListPatternSyntax
            or PropertyPatternClauseSyntax
            or BaseListSyntax;

    static SyntaxToken FirstTokenAfterAttributes(SyntaxNode node) {
        foreach (var element in node.ChildNodesAndTokens()) {
            if (element.IsNode && element.AsNode() is AttributeListSyntax) {
                continue;
            }

            return element.IsToken ? element.AsToken() : element.AsNode()!.GetFirstToken();
        }

        return node.GetFirstToken();
    }

    /// <summary>
    ///     The things the layout treats as starting their own line: a statement, a member, a list
    ///     element, a label, a clause.
    /// </summary>
    static bool IsUnit(SyntaxNode node) =>
        node switch {
            StatementSyntax => true,
            MemberDeclarationSyntax => true,
            AccessorDeclarationSyntax => true,
            SwitchLabelSyntax => true,
            UsingDirectiveSyntax => true,
            ExternAliasDirectiveSyntax => true,
            AttributeListSyntax => true,
            ArgumentSyntax => true,
            AttributeArgumentSyntax => true,
            // ⚠ A parameter is a list element only in a real list. A simple lambda's single parameter
            // is the lambda's own first token, and treating it as an element makes `M() =>\n value =>`
            // sit flush with the member.
            ParameterSyntax { Parent: BaseParameterListSyntax } => true,
            TypeParameterSyntax => true,
            BaseTypeSyntax => true,
            TypeParameterConstraintClauseSyntax => true,
            SwitchExpressionArmSyntax => true,
            CatchClauseSyntax => true,
            FinallyClauseSyntax => true,
            ElseClauseSyntax => true,
            // ⚠ Every query clause starts a line of its own — except the first. `item =\n from p in xs`
            // is a continuation of the assignment and takes its level; `where` and `select` under it are
            // siblings of that `from` and take none. Treating the leading `from` as a unit too leaves the
            // whole query flush with the `=` and is 349 lines of the corpus's indentation divergence.
            FromClauseSyntax { Parent: QueryExpressionSyntax query } from when query.FromClause == from => false,
            QueryClauseSyntax => true,
            SelectOrGroupClauseSyntax => true,
            AnonymousObjectMemberDeclaratorSyntax => true,
            SubpatternSyntax => true,
            // ⚠ Except the first, behind its type: a break there is the declaration's continuation and
            // takes its level, `int /* c */` / `    v = 1;` (#420). The later declarators keep theirs.
            VariableDeclaratorSyntax declarator => !IsFirstDeclaratorBehindItsType(declarator),
            InitializerExpressionSyntax => true,
            CollectionElementSyntax => true,
            _ => false
        };

    /// <summary>
    ///     A gap that stays on one line, with <c>disable_space_changes</c> resolved.
    /// </summary>
    /// <remarks>
    ///     ⚠ Byte for byte, which is one step past what <see cref="SpaceKind" /> can say. The oracle
    ///     preserves the whole run — <c>a  +  b</c> keeps both double spaces — and a
    ///     <see cref="SpaceKind.Required" /> renders exactly one space, so a run wider than that is
    ///     emitted as text. Its width is measured like any other text, so the wrapping the key does
    ///     <em>not</em> suppress still sees the columns the run occupies.
    ///     <para>
    ///         ⚠ Only where the source gap held no newline. The other call sites reach here having
    ///         decided to <em>join</em> a break the author wrote, and there is no run to preserve: what
    ///         the author wrote at that gap was a line ending. One space or none, from
    ///         <see cref="FlatGapSpace" />, is the whole of the answer there.
    ///     </para>
    /// </remarks>
    void EmitFlatGap(Piece previous, PieceKind nextKind, SyntaxToken nextToken, string gap) {
        if (PreservedRun(gap) is { } preserved) {
            doc.Space(preserved);
            return;
        }

        // ⚠ The one gap measured to come back two spaces wide (#493): `E( /*f*/)` with the
        // parameter-list key on is the key's space plus the author's bit, when the trailing-comment
        // key also asks for one. ⚠ Two whatever the author wrote, which is the oracle's fixed point and
        // not its first answer: `F(/*f*/)` comes back `F( /*f*/ )`, and that comes back `F(  /*f*/ )`
        // on the next run, so copying the first answer would make Skala fail its own idempotence.
        // See SpaceRules.OpensAnEmptyPairWithItsSpaceOn.
        if (nextKind is PieceKind.BlockComment or PieceKind.BlockDocComment
            && previous.Kind == PieceKind.Token
            && !options.DisableSpaceChanges
            && options.SpaceBeforeTrailingComment
            && SpaceRules.OpensAnEmptyPairWithItsSpaceOn(tokens[previous.TokenIndex], options)) {
            doc.Space("  ");
            return;
        }

        doc.Space(FlatGapSpace(previous, nextKind, nextToken, gap));
    }

    /// <summary>
    ///     The author's own inter-token run, when <c>disable_space_changes</c> asks for it verbatim.
    /// </summary>
    /// <remarks>
    ///     ⚠ Null in three cases, and each is a case <see cref="FlatGapSpace" /> already answers. The
    ///     key is off. The run is one character or none, which one <see cref="SpaceKind" /> bit says
    ///     exactly. Or the run holds a newline, which means the caller is *joining* a break the author
    ///     wrote and there is no horizontal run to preserve — what the author wrote there was a line
    ///     ending, and this key has no opinion about line endings.
    /// </remarks>
    string? PreservedRun(string gap) =>
        options.DisableSpaceChanges && gap.Length > 1 && CountNewLines(gap) == 0 ? gap : null;

    /// <summary>
    ///     <c>disable_space_changes</c>: the one-bit half of the run, which every caller needs.
    /// </summary>
    /// <remarks>
    ///     ⚠ It sits above <see cref="GapSpace" />'s trailing-comment branch rather than inside it, so
    ///     the gap before a trailing comment is preserved too. That is measured: the narrow sibling
    ///     <c>disable_space_changes_before_trailing_comment</c> cannot move that gap at either of
    ///     <c>skala_space_before_trailing_comment</c>'s values, and this key can (SK-DIV-0060, SK-DIV-0062).
    /// </remarks>
    SpaceKind FlatGapSpace(Piece previous, PieceKind nextKind, SyntaxToken nextToken, string gap) {
        if (!options.DisableSpaceChanges) {
            return GapSpace(previous, nextKind, nextToken);
        }

        return gap.AsSpan().IndexOfAny(' ', '\t') >= 0 ? SpaceKind.Required : SpaceKind.Forbidden;
    }

    SpaceKind GapSpace(Piece previous, PieceKind nextKind, SyntaxToken nextToken) {
        // A trailing comment gets exactly one space before it (skala_space_before_trailing_comment), and
        // its own text is left alone (space_before_trailing_comment_text = false).
        if (nextKind is PieceKind.LineComment
            or PieceKind.BlockComment
            or PieceKind.DocCommentLine
            or PieceKind.BlockDocComment) {
            // ⚠ A `/** … */` inside an expression is the same comment to the oracle as a `/* … */`, on
            // both sides of it (#491): `M(1 /** f */ , 2)` comes back `M(1 /** f */, 2)` and
            // `M(1, /** f */2)` as written, exactly as their `/* */` twins do, where Skala answered
            // every gap after one with a space.
            var governed = nextKind is PieceKind.BlockComment or PieceKind.BlockDocComment
                && previous.Kind == PieceKind.Token
                    ? SpaceRules.OpensAnEmptyPairWithItsSpaceOn(tokens[previous.TokenIndex], options)
                        ? true
                        : SpaceRules.BeforeBlockComment(tokens[previous.TokenIndex], options)
                    : null;

            return governed ?? options.SpaceBeforeTrailingComment ? SpaceKind.Required : SpaceKind.Forbidden;
        }

        if (previous.Kind is PieceKind.BlockComment or PieceKind.BlockDocComment
            && nextKind == PieceKind.Token
            && TokenBeforeTheComments(previous) is { } before) {
            // ⚠ A gap holding a line break is a break being joined, and what the author wrote there
            // was the line ending, not a space: it keeps the one space it always had rather than
            // reading the next line's indentation as the author's bit.
            var afterComment = SpaceRules.AfterBlockComment(before, nextToken, options);
            return afterComment == SpaceKind.Preserve
                ? HasSpace(previous.Span.End, nextToken.SpanStart)
                || HasLineBreak(previous.Span.End, nextToken.SpanStart)
                    ? SpaceKind.Required
                    : SpaceKind.Forbidden
                : afterComment;
        }

        if (previous.Kind != PieceKind.Token || nextKind != PieceKind.Token) {
            return SpaceKind.Required;
        }

        var kind = SpaceRules.Decide(tokens[previous.TokenIndex], nextToken, options);

        // ⚠ A gap no rule governs is resolved against the source, here rather than in the writer.
        // `extra_spaces = remove_all` collapses a run to one space and inserts none, so "preserve"
        // is a one-bit question — did the author write any horizontal space — and the answer is
        // still Required or Forbidden by the time the document is built. Keeping the third state
        // alive all the way to the writer would mean carrying the source into it for one construct.
        // ⚠ A gap being joined reads its bit from the indentation of the line it ends on, and from
        // nothing before the last line break (#436). Measured at `keep_user_linebreaks = false`, where
        // the oracle joins all of them: `X` / `: 1` gives `X: 1` and `X` / `    : 1` gives `X : 1`, and a
        // trailing space before the line ending counts for nothing (`X  ` / `: 1` gives `X: 1`); the
        // range operator, the positional pattern's `(` and a label's colon answer the same way. #419's
        // follow-up closed every joined gap, which the fuzzer had asked for on seed
        // 7764980540680690061 only because Skala joined a break the oracle keeps; with that break kept
        // at the defaults, the indentation no longer reaches the output there.
        return kind == SpaceKind.Preserve
            ? HasSpace(AfterTheLastLineBreak(previous.Span.End, nextToken.SpanStart), nextToken.SpanStart)
                ? SpaceKind.Required
                : SpaceKind.Forbidden
            : kind;
    }

    int AfterTheLastLineBreak(int start, int end) {
        for (var i = Math.Min(end, source.Length) - 1; i >= start; i--) {
            if (source[i] is '\n' or '\r') {
                return i + 1;
            }
        }

        return start;
    }

    bool HasSpace(int start, int end) {
        for (var i = start; i < end && i < source.Length; i++) {
            if (source[i] is ' ' or '\t') {
                return true;
            }
        }

        return false;
    }

    bool HasLineBreak(int start, int end) {
        for (var i = start; i < end && i < source.Length; i++) {
            if (source[i] is '\n' or '\r') {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    ///     The token in front of the run of block comments that ends with <paramref name="comment" />,
    ///     when the run shares that token's line; null otherwise.
    /// </summary>
    /// <remarks>
    ///     ⚠ Only the gaps <em>between</em> the pieces are asked about a line break, never a comment's
    ///     own text: <c>1 /* f</c> / <c>f */, 2</c> comes back <c>f */,</c> from the oracle exactly as a
    ///     one-line comment does. A run that starts a line is a different case — the oracle moves what
    ///     follows it onto a line of its own — and it keeps the one space Skala always gave it. So does
    ///     a formatter tag, and a run broken by anything that is not a block comment.
    /// </remarks>
    SyntaxToken? TokenBeforeTheComments(Piece comment) {
        if (lastPiece < 0 || pieces[lastPiece].Span != comment.Span) {
            return null;
        }

        for (var i = lastPiece; i >= 0; i--) {
            var piece = pieces[i];
            if (piece.Kind == PieceKind.Token) {
                return i == lastPiece ? null : tokens[piece.TokenIndex];
            }

            if (piece.Kind is not (PieceKind.BlockComment or PieceKind.BlockDocComment)
                || FormatterTagGuard.IsOffTag(piece.Text, options.Tags)
                || FormatterTagGuard.IsOnTag(piece.Text, options.Tags)
                || i == 0
                || HasLineBreak(pieces[i - 1].Span.End, piece.Span.Start)) {
                return null;
            }
        }

        return null;
    }

    /// <summary>
    ///     The brace rules, the one place phase 1 removes a line break the author wrote.
    /// </summary>
    /// <remarks>
    ///     ⚠ Never across a comment or a directive. Joining <c>// note</c> with the <c>{</c> below it
    ///     would put the brace inside the comment.
    /// </remarks>
    /// <summary>
    ///     An author's break in a gap no rule governs, which only <c>keep_user_linebreaks</c> was keeping.
    /// </summary>
    /// <remarks>
    ///     ⚠ At <c>keep_user_linebreaks = false</c> (and <c>keep_existing_linebreaks = false</c>, which
    ///     answers the same) the oracle joins a break on either side of a range's or a spread's
    ///     <c>..</c>, after a slice pattern's <c>..</c>, before a positional pattern's <c>(</c> and before a
    ///     label's colon (#439). No plan owns those gaps, so Skala kept every one. Joined, each takes its
    ///     bit from the indentation of the line it ended on (<see cref="FlatGapSpace" />): <c>a[1</c> /
    ///     <c>..2]</c> gives <c>a[1..2]</c>, <c>a[1</c> / <c>    ..2]</c> gives <c>a[1 ..2]</c>, and the
    ///     slice pattern's governed gap gives <c>.. var rest</c> as it would written flat.
    /// </remarks>
    bool JoinsWithoutKeptBreaks(Piece previous, PieceKind nextKind, SyntaxToken nextToken) {
        if (options.KeepsUserBreaksBetweenItems || previous.Kind != PieceKind.Token || nextKind != PieceKind.Token) {
            return false;
        }

        // ⚠ And after a type test's or a pattern's keyword (#443): `o is` / `(1, 2)`, `o is` / `null`,
        // `o is not` / `null`, `o as` / `P` and `case` / `(1, 2):` are all kept at the defaults and
        // joined here.
        var previousToken = tokens[previous.TokenIndex];
        // ⚠ The oracle's predicate, not Skala's: a spread's `..` is governed since #513 and the oracle
        // still joins the break behind it here.
        return SpaceRules.OracleKeepsTheAuthorsGap(previousToken, nextToken)
            || previousToken.IsKind(SyntaxKind.DotDotToken)
            && previousToken.Parent is SlicePatternSyntax
            || previousToken.Kind() is SyntaxKind.IsKeyword or SyntaxKind.AsKeyword
            || previousToken.IsKind(SyntaxKind.NotKeyword)
            && previousToken.Parent is UnaryPatternSyntax
            || previousToken.IsKind(SyntaxKind.CaseKeyword)
            && previousToken.Parent is SwitchLabelSyntax;
    }

    bool ShouldJoin(Piece previous, PieceKind nextKind, SyntaxToken nextToken) {
        if (previous.Kind != PieceKind.Token || nextKind != PieceKind.Token) {
            return false;
        }

        var previousToken = tokens[previous.TokenIndex];

        if (previousToken.IsKind(SyntaxKind.OpenBraceToken) && nextToken.IsKind(SyntaxKind.CloseBraceToken)) {
            // ⚠ `together_same_line` joins the pair too, and this read `== Together` — so Skala gave
            // it `multiline`'s answer and disagreed with the oracle at one of the key's three values.
            // Measured, the three are genuinely distinct and the difference between the two
            // `together`s only shows once the brace would be on its own line:
            //
            //   multiline           together                together_same_line
            //   void M()            void M()                void M() { }
            //   {                   { }
            //   }
            //
            // (under `csharp_new_line_before_open_brace = all`; under the export's `none` the second
            // and third are the same bytes.) ⚠ `together_same_line`'s second half — pulling the pair
            // back onto the declaration's line against `new_line_before_open_brace` — is NOT
            // implemented: it needs the brace-split direction Skala does not have. SK-DIV-0091.
            // ⚠ An empty accessor, lambda, anonymous method or initializer is joined at `multiline` too:
            // `() =>` / `{` / `}` comes back `() => { }` at all three values (#465).
            return OpensAJoinableBody(previousToken)
                && (options.EmptyBlockStyle is EmptyBlockStyle.Together or EmptyBlockStyle.TogetherSameLine
                    || EmptyBodyStaysJoined(previousToken));
        }

        if (nextToken.IsKind(SyntaxKind.OpenBraceToken)) {
            // ⚠ Per construct, not per file. This read `NewLineBeforeOpenBrace is "none"` — so every
            // one of the key's twelve members behaved as `all`, and two of its fifteen values agreed
            // with the oracle. See BraceOwners for the seven groups the C# formatter actually has and
            // the probe that established them.
            // ⚠ And an empty body the key would put on a line of its own is pulled back in two cases,
            // both measured (#465): `together_same_line`, which is that value's whole meaning, and an
            // empty accessor, lambda, anonymous method or initializer, which stays `{ }` on its owner's
            // line at every value of the empty-block key. See BreakPlan.SettleOpenBraces.
            return OpensAJoinableBody(nextToken)
                && ((options.NewLineBeforeOpenBraceOwners & BraceOwnerSet.Of(nextToken)) == 0
                    || IsEmptyBody(nextToken)
                    && (options.EmptyBlockStyle == EmptyBlockStyle.TogetherSameLine
                        || EmptyBodyStaysJoined(nextToken)));
        }

        if (previousToken.IsKind(SyntaxKind.ElseKeyword)) {
            return nextToken.IsKind(SyntaxKind.IfKeyword) && options.SpecialElseIfTreatment;
        }

        if (!previousToken.IsKind(SyntaxKind.CloseBraceToken)) {
            return false;
        }

        return nextToken.Kind() switch {
            SyntaxKind.ElseKeyword => !options.NewLineBeforeElse,
            SyntaxKind.CatchKeyword => !options.NewLineBeforeCatch,
            SyntaxKind.FinallyKeyword => !options.NewLineBeforeFinally,
            SyntaxKind.WhileKeyword => nextToken.Parent is DoStatementSyntax && !options.NewLineBeforeWhile,
            _ => false
        };
    }

    /// <summary>
    ///     A break the rules require at a gap the author left flat: the other direction of
    ///     <see cref="ShouldJoin" />.
    /// </summary>
    /// <remarks>
    ///     ⚠ Two arms, and the second is deliberately narrower than <see cref="ShouldJoin" />'s
    ///     mirror image would be. <c>ShouldJoin</c> returning <c>false</c> does not mean "break": it
    ///     means "this rule has no opinion", and every gap in the file passes through it. A break may
    ///     only be *added* where a rule positively asks for one, so the arms here are written out
    ///     rather than derived by negation.
    ///     <para>
    ///         ⚠ The placement family's split direction is otherwise not implemented — a brace is never
    ///         moved onto a line of its own, so <c>new_line_before_open_brace</c>,
    ///         <c>skala_new_line_before_else</c> and their siblings only ever decide whether to *keep* the
    ///         break the author wrote. That gap is recorded in SK-DIV-0091 and is invisible to their
    ///         sweep rows, whose fixtures are all written with the break already there. The one arm
    ///         added here is the one whose row needs it and whose shape is a keyword rather than a
    ///         brace.
    ///     </para>
    /// </remarks>
    bool MustBreak(Piece previous, PieceKind nextKind, SyntaxToken nextToken) {
        if (previous.Kind is PieceKind.BlockComment or PieceKind.BlockDocComment) {
            return nextKind == PieceKind.Token
                && (FollowsItsDeclarationsType(nextToken) || StartsALabelledStatement(nextToken))
                && !FormatterTagGuard.IsOffTag(previous.Text, options.Tags)
                && !FormatterTagGuard.IsOnTag(previous.Text, options.Tags);
        }

        if (previous.Kind != PieceKind.Token) {
            return false;
        }

        var previousToken = tokens[previous.TokenIndex];

        // ⚠ A documentation comment starts its own line whatever `skala_allow_comment_after_lbrace`
        // says, and this arm is deliberately unconditional. Measured on 2026-09-04 under the
        // configuration in force (`allow_comment_after_lbrace = true`, sha256:9bf4b7e7193c5da3): given
        // `class C { /// <summary>…</summary>` the oracle moves the `///` down to its own line, under
        // `SkalaFormatOnly` and `SkalaDocComments` alike. The key is Braces Layout's "allow comment
        // after '{'" and it governs a *trailing* comment; a `///` is documentation for the member
        // below it, not a trailing remark about the brace.
        //
        // ⚠ It was folded into the arm below until the default flipped to `true`, and the cost of
        // that was invisible: this option's fixture
        // (`constructs/braces/skala_allow_comment_after_lbrace.cs`) exercises only `// why`, so
        // nothing in the corpus pinned the doc-comment half of the pair. What caught it was
        // `McpServerTests.Format_FormatsDocumentationCommentsToo` — a doc comment left on the brace's
        // line is not re-anchored by the xmldoc sub-formatter either, so an agent was told its draft
        // was formatted while the comment was untouched.
        if (nextKind == PieceKind.DocCommentLine && previousToken.IsKind(SyntaxKind.OpenBraceToken)) {
            return true;
        }

        // `skala_allow_comment_after_lbrace = false`: a comment may not sit on the brace's line.
        if (!options.AllowCommentAfterLbrace
            && nextKind == PieceKind.LineComment
            && previousToken.IsKind(SyntaxKind.OpenBraceToken)) {
            return true;
        }

        // ⚠ `skala_new_line_before_else`, `_catch`, `_finally` and `_while` at `true` split a `} else` the
        // author wrote joined, and Skala only ever kept a break the author wrote — `ShouldJoin`'s arm
        // below is the other direction. Measured 2026-10-08 (#480) with all four keys `true` on a K&R
        // input: `}` / `else {`, `}` / `catch {`, `}` / `finally {`, `}` / `while (b);` and `}` /
        // `else M();`.
        if (nextKind == PieceKind.Token
            && previousToken.IsKind(SyntaxKind.CloseBraceToken)
            && nextToken.Kind() switch {
                SyntaxKind.ElseKeyword => options.NewLineBeforeElse,
                SyntaxKind.CatchKeyword => options.NewLineBeforeCatch,
                SyntaxKind.FinallyKeyword => options.NewLineBeforeFinally,
                SyntaxKind.WhileKeyword => nextToken.Parent is DoStatementSyntax && options.NewLineBeforeWhile,
                _ => false
            }) {
            return true;
        }

        // ⚠ `skala_empty_block_style = multiline` splits an empty body the author wrote `{ }`, and Skala
        // only ever kept a split one. Measured 2026-10-08 (#465) under `csharp_new_line_before_open_brace`
        // `none` and `all` alike: a type's, a namespace's, a method's, a local function's, a control
        // block's and a switch's `{ }` comes back `{` / `}`, and an accessor's, a lambda's, an anonymous
        // method's and an initializer's stays `{ }`.
        if (nextKind == PieceKind.Token
            && previousToken.IsKind(SyntaxKind.OpenBraceToken)
            && nextToken.IsKind(SyntaxKind.CloseBraceToken)
            && options.EmptyBlockStyle == EmptyBlockStyle.Multiline
            && OpensAJoinableBody(previousToken)
            && IsEmptyBody(previousToken)
            && !EmptyBodyStaysJoined(previousToken)) {
            return true;
        }

        // ⚠ `skala_special_else_if_treatment = false` splits `else if` and lets the `if` become what it
        // structurally is — the `else`'s embedded statement, one level in. Measured, and symmetric:
        // the oracle splits a joined `else if` at `false` and joins a split one at `true`, so
        // `ShouldJoin`'s arm alone covered one direction of a two-directional key.
        //
        //   } else                     ← false
        //       if (a == 2) {
        //           M(a);
        //       }
        return nextKind == PieceKind.Token
            && previousToken.IsKind(SyntaxKind.ElseKeyword)
            && nextToken.IsKind(SyntaxKind.IfKeyword)
            && !options.SpecialElseIfTreatment;
    }

    /// <summary>
    ///     The name of a declaration's first declarator, right behind its type: <c>v</c> in
    ///     <c>int /* c */ v = 1;</c>, for a local, a field, an event field or a <c>using</c> statement's
    ///     resource.
    /// </summary>
    /// <remarks>
    ///     ⚠ A block comment between the type and the name breaks the line after the comment, and the
    ///     name goes one continuation level in (#420). Measured with <c>Testing ask</c> on about sixty
    ///     shapes, each written closed and spaced: <c>var /* c */v = 1;</c>, <c>int? /* c */ v</c>,
    ///     <c>(int, string) /* c */ v</c>, <c>ref int /* c */ v</c>, <c>const int /* c */ v</c>,
    ///     <c>await using var /* c */ x</c>, two comments in a row, a field, an event field and a
    ///     declaration with a second declarator all come back <c>… /* c */</c> / <c>    v = 1;</c>, and an
    ///     author's break before the comment puts the comment on a continuation line of its own too. A
    ///     <c>using</c> statement's resource breaks the same way. ⚠ The issue called it "a local
    ///     declaration's shape only"; it is the declaration's, and a field breaks as a local does.
    ///     <para>
    ///         Measured not to break, and so not here: a comment between modifiers, before the type, after
    ///         the name, before a second declarator, in a <c>for</c> or <c>fixed</c> statement's
    ///         declaration, after <c>foreach (var</c>, in an <c>out var</c> or a declaration pattern, and
    ///         in front of a property's, a method's, a local function's or a parameter's name.
    ///     </para>
    /// </remarks>
    /// <summary>
    ///     The first token of the statement a label labels, unless that statement is empty.
    /// </summary>
    /// <remarks>
    ///     ⚠ A labelled statement starts a line of its own (#433). Measured with <c>Testing ask</c>: the
    ///     oracle writes <c>a: M(1);</c> as <c>a:</c> / <c>M(1);</c>, at the label's own indent, for an
    ///     expression statement, a declaration, an <c>if</c>, a <c>for</c>, a block (<c>a:</c> /
    ///     <c>{ }</c>), a second label (<c>b6: b7: M();</c> takes three lines) and inside a switch section;
    ///     with a block comment after the colon the break comes after the comment, <c>a: /* c */</c> /
    ///     <c>M();</c>. Only an empty statement stays: <c>e:;</c> comes back <c>e: ;</c>. At
    ///     <c>skala_outdent_statement_labels = true</c> the empty statement moves out with its label and
    ///     every other statement stays in.
    /// </remarks>
    static bool StartsALabelledStatement(SyntaxToken token) =>
        token.Parent is not null
        && token.Parent.AncestorsAndSelf()
            .TakeWhile(node => node.GetFirstToken() == token)
            .Any(static node => node is StatementSyntax and not EmptyStatementSyntax
                && node.Parent is LabeledStatementSyntax
            );

    static bool FollowsItsDeclarationsType(SyntaxToken token) =>
        token.IsKind(SyntaxKind.IdentifierToken)
        && token.Parent is VariableDeclaratorSyntax declarator
        && IsFirstDeclaratorBehindItsType(declarator);

    internal static bool IsFirstDeclaratorBehindItsType(VariableDeclaratorSyntax declarator) =>
        declarator.Parent is VariableDeclarationSyntax {
            Parent:
            LocalDeclarationStatementSyntax
                or FieldDeclarationSyntax
                or EventFieldDeclarationSyntax
                or UsingStatementSyntax
        } declaration
        && declaration.Variables[0] == declarator;

    /// <summary>
    ///     Whether the brace opens a body with nothing in it — not a statement, a member, an element nor
    ///     a comment.
    /// </summary>
    internal static bool IsEmptyBody(SyntaxToken open) {
        var next = open.GetNextToken();
        return next.IsKind(SyntaxKind.CloseBraceToken)
            && next.Parent == open.Parent
            && !open.TrailingTrivia.Any(IsLineOrBlockComment)
            && !next.LeadingTrivia.Any(static trivia => IsLineOrBlockComment(trivia) || trivia.IsDirective);
    }

    /// <summary>
    ///     An empty body that stays <c>{ }</c> on its owner's line whatever the brace keys say.
    /// </summary>
    /// <remarks>
    ///     ⚠ Measured 2026-10-08 under <c>csharp_new_line_before_open_brace = all</c> at all three
    ///     <c>skala_empty_block_style</c> values, written K&amp;R and Allman (#465): an accessor's
    ///     <c>get { }</c>, a lambda's <c>() =&gt; { }</c>, an anonymous method's <c>delegate { }</c>, a
    ///     collection initializer's <c>new List&lt;int&gt; { }</c> and an anonymous type's <c>new { }</c>
    ///     come back joined every time, and <c>() =&gt;</c> / <c>{</c> / <c>}</c> is joined back. A
    ///     type's, a namespace's, a method's, a local function's, a control block's and a switch's are
    ///     the ones the empty-block key moves.
    /// </remarks>
    internal static bool EmptyBodyStaysJoined(SyntaxToken open) =>
        BraceOwnerSet.Of(open) is BraceOwners.Accessors or BraceOwners.Lambdas or BraceOwners.Initializers;

    internal static bool OpensAJoinableBody(SyntaxToken brace) =>
        brace.Parent is BlockSyntax
            or AccessorListSyntax
            or BaseTypeDeclarationSyntax
            or NamespaceDeclarationSyntax
            or SwitchStatementSyntax
            or InitializerExpressionSyntax
            or AnonymousObjectCreationExpressionSyntax
            or SwitchExpressionSyntax
            or PropertyPatternClauseSyntax;

    // ── Structure helpers ────────────────────────────────────────────────────────────────────

    static (SyntaxToken Open, SyntaxToken Close) BraceTokens(SyntaxNode node) =>
        node switch {
            BlockSyntax block => (block.OpenBraceToken, block.CloseBraceToken),
            BaseTypeDeclarationSyntax type => (type.OpenBraceToken, type.CloseBraceToken),
            NamespaceDeclarationSyntax ns => (ns.OpenBraceToken, ns.CloseBraceToken),
            AccessorListSyntax accessors => (accessors.OpenBraceToken, accessors.CloseBraceToken),
            InitializerExpressionSyntax initializer => (initializer.OpenBraceToken, initializer.CloseBraceToken),
            AnonymousObjectCreationExpressionSyntax anonymous => (anonymous.OpenBraceToken, anonymous.CloseBraceToken),
            PropertyPatternClauseSyntax pattern => (pattern.OpenBraceToken, pattern.CloseBraceToken),
            SwitchExpressionSyntax switchExpression => (switchExpression.OpenBraceToken,
                switchExpression.CloseBraceToken),
            _ => FindDelimiters(node, SyntaxKind.OpenBraceToken, SyntaxKind.CloseBraceToken)
        };

    static (SyntaxToken Open, SyntaxToken Close) DelimiterTokens(SyntaxNode node, NodeLayout layout) {
        var (openKind, closeKind) = layout switch {
            NodeLayout.Parens => (SyntaxKind.OpenParenToken, SyntaxKind.CloseParenToken),
            NodeLayout.Brackets => (SyntaxKind.OpenBracketToken, SyntaxKind.CloseBracketToken),
            _ => (SyntaxKind.LessThanToken, SyntaxKind.GreaterThanToken)
        };

        return FindDelimiters(node, openKind, closeKind);
    }

    static (SyntaxToken Open, SyntaxToken Close) FindDelimiters(
        SyntaxNode node,
        SyntaxKind openKind,
        SyntaxKind closeKind
    ) {
        var open = default(SyntaxToken);
        var close = default(SyntaxToken);
        foreach (var child in node.ChildNodesAndTokens()) {
            if (!child.IsToken) {
                continue;
            }

            var token = child.AsToken();
            if (open.IsKind(SyntaxKind.None) && token.IsKind(openKind)) {
                open = token;
            } else if (token.IsKind(closeKind)) {
                close = token;
            }
        }

        return (open, close);
    }

    static StatementSyntax? EmbeddedStatement(SyntaxNode node) =>
        node switch {
            IfStatementSyntax statement => statement.Statement,
            ElseClauseSyntax clause => clause.Statement,
            WhileStatementSyntax statement => statement.Statement,
            DoStatementSyntax statement => statement.Statement,
            ForStatementSyntax statement => statement.Statement,
            ForEachStatementSyntax statement => statement.Statement,
            ForEachVariableStatementSyntax statement => statement.Statement,
            UsingStatementSyntax statement => statement.Statement,
            FixedStatementSyntax statement => statement.Statement,
            LockStatementSyntax statement => statement.Statement,
            LabeledStatementSyntax statement => statement.Statement,
            _ => null
        };

    static (SyntaxToken Open, SyntaxToken Close) ConditionParentheses(SyntaxNode node) =>
        node switch {
            IfStatementSyntax statement => (statement.OpenParenToken, statement.CloseParenToken),
            WhileStatementSyntax statement => (statement.OpenParenToken, statement.CloseParenToken),
            DoStatementSyntax statement => (statement.OpenParenToken, statement.CloseParenToken),
            ForStatementSyntax statement => (statement.OpenParenToken, statement.CloseParenToken),
            ForEachStatementSyntax statement => (statement.OpenParenToken, statement.CloseParenToken),
            ForEachVariableStatementSyntax statement => (statement.OpenParenToken, statement.CloseParenToken),
            UsingStatementSyntax statement => (statement.OpenParenToken, statement.CloseParenToken),
            FixedStatementSyntax statement => (statement.OpenParenToken, statement.CloseParenToken),
            LockStatementSyntax statement => (statement.OpenParenToken, statement.CloseParenToken),
            _ => (default, default)
        };

    /// <summary>The flags a planned point carries into the document, from its rule and its gap.</summary>
    /// <param name="renders">
    ///     Whether the gap renders as the space rules say when the point stays flat — false when a
    ///     preserved run already stands in front of the point and is that rendering.
    /// </param>
    LineFlags PointFlags(
        GapRule rule,
        Piece previous,
        PieceKind nextKind,
        SyntaxToken nextToken,
        string gap,
        bool renders
    ) {
        var flags = LineFlags.None;
        if (renders && FlatGapSpace(previous, nextKind, nextToken, gap) != SpaceKind.Forbidden) {
            flags |= LineFlags.FlatSpace;
        }

        if (rule is GapRule.FillPoint or GapRule.LastResortPoint or GapRule.YieldingFillPoint) {
            flags |= LineFlags.FillPoint;
        }

        if (rule is GapRule.LastResortPoint or GapRule.FollowingPoint) {
            flags |= LineFlags.LastResort;
        }

        if (rule == GapRule.YieldingFillPoint) {
            flags |= LineFlags.YieldsToPredecessors;
        }

        if (rule is GapRule.FillPoint
            && nextToken.Parent is TypeParameterSyntax { Parent: TypeParameterListSyntax list }
            && list.Parameters.Count > 1
            && list.Parameters[0].GetFirstToken() == nextToken
            && AlignsTypeParameters(list)) {
            flags |= LineFlags.AlignedListHead;
        }

        if (rule == GapRule.FollowingPoint) {
            flags |= LineFlags.BreaksOnlyIfNextLineOverflows;
            if (IsAShortParameterBehindItsSection(nextToken)) {
                flags |= LineFlags.ReadThroughWhenBroken;
            }
        }

        if (IsADelimitedTupleItem(nextToken) || StartsATypeArgument(nextToken)) {
            flags |= LineFlags.DelimitedItem;
        }

        if (StartsATupleItem(nextToken)) {
            flags |= LineFlags.KeepsHeadWhenCertain;
        }

        if (rule == GapRule.FillPoint && CallLinkAt(nextToken) is { } link) {
            flags |= LineFlags.ChainCallLink;
            if (link.ArgumentList.Arguments.Count <= 1) {
                flags |= LineFlags.ChainCallOneArgument;
            }
        }

        // ⚠ Every point in front of an element carries the flag, the opener's too, so that the line the
        // first element starts on is on record for the second element's point. A collection expression
        // keeps its `[` on the line when it fits nowhere whole: `), [` in
        // `pathological/nested-collection-in-generated-*.cs`, where `new[] {` in the same place moves down.
        // ⚠ Not a parenthesised element, which the fuzzer refuted (seed 11833788308239883143): its head
        // ends at a binary operator only once that operator's break is the author's, so pass two kept on
        // the line what pass one had moved down. A collection's head ends at its own `[` on both passes.
        if (StartsAFilledElement(nextToken)) {
            flags |= LineFlags.ArrayElement;
            if (nextToken.IsKind(SyntaxKind.OpenBracketToken) && nextToken.Parent is CollectionExpressionSyntax) {
                flags |= LineFlags.DelimitedItem;
            }

            // ⚠ And a collection expression's element keeps an identifier head when the break inside it
            // is certain, the tuple's rule: `1, F(() => {` with a block that cannot join stays on the
            // comma's line, while `F("…131 columns…", 2)` moves whole (#471, SK-DIV-0117).
            if (StartsACollectionElement(nextToken)) {
                flags |= LineFlags.KeepsHeadWhenCertain;
            }
        }

        return flags;
    }

    /// <summary>
    ///     Whether the token starts a parameter of at most eleven columns behind its one attribute section.
    /// </summary>
    /// <remarks>
    ///     ⚠ Measured with <c>jb cleanupcode</c> 2025.2.6 (#476, SK-DIV-0352), the section's last column swept
    ///     one at a time: behind <c>[Obsolete("…", true)]</c>, <c>[Description("…")]</c>, <c>[A("…")]</c> and
    ///     <c>[A("…", 1)]</c>, <c>int a</c>, <c>string a</c>, <c>ref int a</c>, <c>int a = 5</c> and
    ///     <c>List&lt;int&gt; a</c> chop the section's arguments exactly when the joined line overflows, and
    ///     never stand alone below a whole section — at two indents. Longer parameters do not follow one
    ///     rule: a 16- or 17-column parameter behind <c>[A("…")]</c> stands alone below the section at
    ///     every width, behind <c>[Obsolete("…", true)]</c> it chops from the joined line's overflow up to
    ///     17 columns and from a threshold that rises with the parameter from 18, and never at 30. So the
    ///     rule is wired where every cell agrees, and the gap is left to the section's own answer above
    ///     eleven columns. ⚠ A parameter with a default value is flagged and not helped: its `=` is a
    ///     point of its own that ends the arguments' measure first, and `[…] int a =` / `5` past the
    ///     margin is what Skala writes with or without the flag, where the oracle chops (SK-DIV-0352).
    /// </remarks>
    static bool IsAShortParameterBehindItsSection(SyntaxToken token) =>
        token.Parent?.AncestorsAndSelf().OfType<ParameterSyntax>().FirstOrDefault() is {
            AttributeLists: [{ Attributes.Count: 1 } section]
        } parameter
        && token == section.CloseBracketToken.GetNextToken()
        && parameter.Span.End - token.SpanStart <= 11
        && !parameter.SyntaxTree.GetText()
            .ToString(TextSpan.FromBounds(token.SpanStart, parameter.Span.End))
            .Contains('\n');

    /// <summary>
    ///     Whether the token opens a tuple's item with a delimiter — the one fill whose head the oracle
    ///     keeps on the line when the item fits nowhere whole (SK-DIV-0110).
    /// </summary>
    /// <remarks>
    ///     ⚠ The tuple and not every fill, and the boundary is measured. A tuple has no wrap style of
    ///     its own: `(1\n, (2\n, 3), 4)` keeps `, (2` and keeps `, 4` after the multi-line item. An
    ///     array initializer keeps a delimited head too (`), [` in `pathological/nested-collection-in-
    ///     generated-while.cs`) but then breaks *after* the multi-line item (`],\n(null ? …`), a rule
    ///     Skala does not have; applying the head rule there alone moved `new[] { ("a", …,\n "…"),
    ///     ("b", …` in Skala's own GateCommands.cs onto the previous item's line, away from the oracle.
    /// </remarks>
    static bool IsADelimitedTupleItem(SyntaxToken token) =>
        token.Kind() is SyntaxKind.OpenParenToken or SyntaxKind.OpenBracketToken or SyntaxKind.OpenBraceToken
        && token.Parent?.Parent is { } item
        && IsATupleShapedItem(item);

    /// <summary>
    ///     Whether the token is the first of an array initializer's element — the fill whose elements were
    ///     measured (#444, SK-DIV-0208) — or of a collection expression's.
    /// </summary>
    /// <remarks>
    ///     ⚠ The collection expression was excluded on a reading of CollectionAfterEqIssue375Tests that
    ///     had it backwards: Skala put <c>((</c> on a line of its own and the oracle keeps
    ///     <c>null!, ((</c>. Measured for #471 (SK-DIV-0117): a collection expression's fill keeps the
    ///     head of a multi-line element — <c>1, () =&gt; {</c>, <c>1, o switch {</c>, <c>[1], [</c>,
    ///     <c>1, 2, Call(</c> — and starts the element after one on a line of its own, exactly as an
    ///     array initializer's does.
    /// </remarks>
    static bool StartsAFilledElement(SyntaxToken token) {
        for (SyntaxNode? node = token.Parent; node is not null && node.GetFirstToken() == token; node = node.Parent) {
            if (node.Parent is InitializerExpressionSyntax initializer
                && initializer.IsKind(SyntaxKind.ArrayInitializerExpression)) {
                return true;
            }
        }

        return StartsACollectionElement(token);
    }

    /// <summary>Whether the token is the first of a collection expression's element.</summary>
    static bool StartsACollectionElement(SyntaxToken token) {
        for (SyntaxNode? node = token.Parent; node is not null && node.GetFirstToken() == token; node = node.Parent) {
            if (node is CollectionElementSyntax && node.Parent is CollectionExpressionSyntax) {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    ///     Whether the token is the first of any tuple-shaped item — the fills whose identifier-headed
    ///     items keep their head when a break inside them is certain (SK-DIV-0114).
    /// </summary>
    static bool StartsATupleItem(SyntaxToken token) {
        for (SyntaxNode? node = token.Parent; node is not null && node.GetFirstToken() == token; node = node.Parent) {
            if (IsATupleShapedItem(node)) {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    ///     An item of a tuple expression, of a positional pattern or of a deconstruction designation —
    ///     the three parenthesised fills the oracle lays out alike (SK-DIV-0114).
    /// </summary>
    /// <remarks>
    ///     ⚠ Measured on the two that are not tuples: <c>o is (1, (2,\n 3))</c>, <c>o is (1\n, (2\n, 3))</c>,
    ///     <c>o is (1, Get(2,\n 3))</c>, <c>var (a, (b,\n c))</c> and <c>var (a\n, (b\n, c))</c> all keep the
    ///     nested item's head on the outer item's line. An array rank and a function pointer's lists are
    ///     filled the same way but were not measured on this shape and are left out.
    ///     <para>
    ///         ⚠ An attribute in a section of several is one (#537): `[Obsolete, Description("…",` / `"…")]`
    ///         comes back from the oracle as `[Obsolete, Description(` with the arguments chopped below,
    ///         after two attributes or three, on a parameter and on a method, where Skala broke after
    ///         `[Obsolete,`. An attribute that is merely too wide still moves to a line of its own —
    ///         `[Obsolete,` / `Description("…110 columns…")` is the oracle's too.
    ///     </para>
    /// </remarks>
    static bool IsATupleShapedItem(SyntaxNode node) =>
        node is ArgumentSyntax { Parent: TupleExpressionSyntax }
            or SubpatternSyntax { Parent: PositionalPatternClauseSyntax }
            or VariableDesignationSyntax { Parent: ParenthesizedVariableDesignationSyntax }
            or AttributeSyntax { Parent: AttributeListSyntax { Attributes.Count: > 1 } };

    /// <summary>
    ///     Whether the token is the first of a type argument — the other fill whose head stays on the
    ///     line when the item fits nowhere whole (SK-DIV-0114).
    /// </summary>
    /// <remarks>
    ///     ⚠ Measured on a nested generic with a kept break inside the inner list:
    ///     <c>List&lt;Dictionary&lt;string,↵int&gt;&gt; nested</c> comes back exactly as written, and a
    ///     six-deep <c>Dictionary&lt;string, Dictionary&lt;…&gt;&gt;</c> over the margin is broken inside the
    ///     second list and nowhere outside it. The general fill rule — break before an item that has no
    ///     flat form — put <c>Dictionary&lt;</c> on a line of its own in both.
    /// </remarks>
    static bool StartsATypeArgument(SyntaxToken token) {
        for (SyntaxNode? node = token.Parent; node is not null && node.GetFirstToken() == token; node = node.Parent) {
            if (node.Parent is TypeArgumentListSyntax) {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    ///     ⚠ <c>indent_nested_{for,foreach,while,using,lock,fixed}_stmt = false</c>: a loop directly
    ///     inside another loop of the same kind stays flush rather than stair-stepping. One of the few
    ///     places the formatter <em>removes</em> indentation the author wrote.
    /// </summary>
    bool NeedsEmbeddedIndent(SyntaxNode owner, SyntaxNode embedded) {
        if (embedded is BlockSyntax) {
            return false;
        }

        // ⚠ skala_special_else_if_treatment = true: `else if` is one line, not a nested block, so the
        // inner if takes no indent of its own.
        if (owner is ElseClauseSyntax && embedded is IfStatementSyntax && options.SpecialElseIfTreatment) {
            return false;
        }

        var flush = owner switch {
            ForStatementSyntax => embedded is ForStatementSyntax && !options.IndentNestedForStmt,
            ForEachStatementSyntax or ForEachVariableStatementSyntax =>
                embedded is ForEachStatementSyntax or ForEachVariableStatementSyntax
                && !options.IndentNestedForeachStmt,
            WhileStatementSyntax => embedded is WhileStatementSyntax && !options.IndentNestedWhileStmt,
            UsingStatementSyntax => embedded is UsingStatementSyntax && !options.IndentNestedUsingsStmt,
            LockStatementSyntax => embedded is LockStatementSyntax && !options.IndentNestedLockStmt,
            FixedStatementSyntax => embedded is FixedStatementSyntax && !options.IndentNestedFixedStmt,
            _ => false
        };

        return !flush;
    }

    /// <summary>
    ///     How many lines a gap ends, counting the line terminators C# actually recognises.
    /// </summary>
    /// <remarks>
    ///     ⚠ A lone <c>\r</c> ends a line, and this counted only <c>\n</c> until SK-FUZZ-0009. The
    ///     consequence was not a cosmetic one: <c>}   &lt;CR&gt;#endif</c> reported zero newlines, so
    ///     <see cref="EmitGap" /> reasoned about the brace and the directive as though they shared a
    ///     line and joined them — and a <c>#</c> that is no longer first on its line is not a
    ///     directive to Roslyn, so the <c>#endif</c> became a skipped token, token equivalence failed
    ///     and the file could not be formatted at all (SK9099). <see cref="FirstNewLine" /> beside it
    ///     had always read a lone <c>\r</c> correctly, which is what made the disagreement invisible:
    ///     the *style* of the break was right, there just was not one.
    ///     <para>
    ///         ⚠ <c>\r\n</c> is one line ending, not two. Counting the <c>\r</c> and the <c>\n</c>
    ///         separately would report one blank line between every pair of lines in a CRLF file.
    ///     </para>
    /// </remarks>
    internal static int CountNewLines(string gap) {
        var count = 0;
        for (var i = 0; i < gap.Length; i++) {
            if (gap[i] == '\n') {
                count++;
            } else if (gap[i] == '\r') {
                count++;
                if (i + 1 < gap.Length && gap[i + 1] == '\n') {
                    i++;
                }
            }
        }

        return count;
    }

    static string? FirstNewLine(string gap) {
        for (var i = 0; i < gap.Length; i++) {
            if (gap[i] == '\r') {
                return i + 1 < gap.Length && gap[i + 1] == '\n' ? "\r\n" : "\r";
            }

            if (gap[i] == '\n') {
                return "\n";
            }
        }

        return null;
    }
}
