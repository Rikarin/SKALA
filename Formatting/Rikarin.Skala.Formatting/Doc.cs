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

/// <summary>What a <see cref="DocKind.Line"/> node carries in <see cref="DocNode.Flags"/>.</summary>
[Flags]
public enum LineFlags {
    None = 0,

    /// <summary>
    /// A <see cref="LineKind.Soft"/> break renders as one space when its group is flat.
    /// </summary>
    /// <remarks>
    /// ⚠ This is the flat half of the break-position model. <c>Foo(a, b)</c> has three break points
    /// and they do not render alike when the group stays flat: the one after <c>(</c> and the one
    /// before <c>)</c> render as nothing, the one after <c>,</c> renders as a space. A soft break
    /// with a single flat rendering produces <c>Foo( a, b )</c> or <c>Foo(a,b)</c> and there is no
    /// third choice.
    /// </remarks>
    FlatSpace = 1,

    /// <summary>
    /// The point belongs to a fill: it breaks only when what follows it would not fit, rather than
    /// with the rest of its group.
    /// </summary>
    /// <remarks>
    /// ⚠ The flag is on the point and not on the group, because a fill's delimiters and its item
    /// separators do not behave alike. <c>wrap_array_initializer_style = wrap_if_long</c> puts the
    /// <c>{</c> at the end of the opening line and the <c>}</c> on a line of its own <em>whenever
    /// the initializer wraps at all</em>, and fills only the gaps between elements:
    /// <code>
    /// var e = new[] {
    ///     "aaaaaaaaaaaaaaa", "bbbbbbbbbbbbbbb", "ccccccccccccccc", "ddddddddddddddd", "eeeeeeeeeeeeeee",
    ///     "fffffffffffffff"
    /// };
    /// </code>
    /// A group-wide fill mode would either put the braces in the fill — producing
    /// <c>new[] { "aaa",</c> — or take the elements out of it. Neither is what the oracle writes.
    /// </remarks>
    FillPoint = 2
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

    /// <summary>
    /// ⚠ The third state: broken iff it was broken in the source, subject to width — with "subject
    /// to width" spelled out per group by <see cref="GroupFacts"/>.
    /// </summary>
    Preserve,

    /// <summary>
    /// ⚠ The fourth: broken iff the group named by <see cref="Document.OwnerOf"/> resolved broken.
    /// </summary>
    /// <remarks>
    /// <c>place_*_on_single_line = if_owner_is_single_line</c>, which five keys in the export use.
    /// The owner resolves first and the child only reads it, so a child may move Flat → Broken and
    /// never back, which is what makes termination a property of the shape rather than of a
    /// convergence argument (docs/plan/04 § "The fitting algorithm").
    /// </remarks>
    Owner
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
    Outdent,

    /// <summary>
    /// A column rather than a level: everything inside starts at the column the scope opened at.
    /// </summary>
    /// <remarks>
    /// ⚠ docs/plan/04 reserves an <c>Align</c> node and milestones 1–3 never produced one, which is
    /// what SK-DIV-0008 recorded. <c>align_multiline_statement_conditions = true</c> is the key that
    /// needs it: a condition broken across lines is laid out from the column just after the
    /// statement's <c>(</c>, which is not a multiple of the indent width.
    /// </remarks>
    Align
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

    /// <summary>
    /// Kind-specific: for <see cref="DocKind.Line"/> the group whose mode decides the break, and
    /// for <see cref="DocKind.Group"/> the owner group of a <see cref="GroupMode.Owner"/> group.
    /// −1 when there is none.
    /// </summary>
    public int Arg2;

    /// <summary>The source span this node came from; <see cref="SourceSpan.Length"/> 0 when synthetic.</summary>
    public SourceSpan Source;
}

/// <summary>
/// A document: a struct arena of nodes plus the side tables they index.
/// </summary>
public sealed class Document {
    readonly int[] _flatWidth;
    readonly int[] _headWidth;
    readonly int[] _pointWidth;
    readonly int[] _afterPoint;
    readonly int[] _segment;
    readonly bool[] _hasBreak;
    readonly GroupFacts[] _facts;

    internal Document(
        DocNode[] nodes,
        int nodeCount,
        int[] children,
        string[] strings,
        int root,
        int groupCount,
        int[] flatWidth,
        int[] headWidth,
        int[] pointWidth,
        int[] afterPoint,
        int[] segment,
        bool[] hasBreak,
        GroupFacts[] facts
    ) {
        Nodes = nodes;
        NodeCount = nodeCount;
        Children = children;
        Strings = strings;
        Root = root;
        GroupCount = groupCount;
        _flatWidth = flatWidth;
        _headWidth = headWidth;
        _pointWidth = pointWidth;
        _afterPoint = afterPoint;
        _segment = segment;
        _hasBreak = hasBreak;
        _facts = facts;
    }

    public DocNode[] Nodes { get; }

    public int NodeCount { get; }

    public int[] Children { get; }

    public string[] Strings { get; }

    public int Root { get; }

    public int GroupCount { get; }

    /// <summary>
    /// The width the subtree at <paramref name="node"/> occupies with every group flat.
    /// </summary>
    /// <remarks>
    /// ⚠ Computed by <see cref="DocumentBuilder"/> as the arena is filled, not by a traversal of
    /// its own: docs/plan/13 § "The fitting pass" — "the measure pass is fused into the build pass
    /// where a group's contents are already known, which removes one full traversal". A subtree
    /// containing a hard break is <see cref="Unbounded"/>.
    /// </remarks>
    public int FlatWidthOf(int node) => _flatWidth[node];

    /// <summary>
    /// The width from the node's start to the first break inside it, or its flat width when there
    /// is none.
    /// </summary>
    /// <remarks>
    /// ⚠ The second of the two measures a group can be fitted against, and the two are not
    /// interchangeable. A list — an argument list, an enum body, a switch expression — is chopped
    /// when it is "long <em>or multiline</em>", which is ReSharper's own wording for
    /// <c>chop_if_long</c>, so its measure is the flat width and a hard break anywhere inside makes
    /// it infinite. A tail — the right-hand side of an <c>=</c>, the body after an <c>=&gt;</c> —
    /// is broken only when what remains of the current line does not fit, because breaking after
    /// the <c>=</c> of <c>Original = new Thing {</c> gains nothing: the line was going to end at the
    /// brace either way. Measuring a tail by its flat width costs 7.6 points of line fidelity, which
    /// is how this distinction was found.
    /// </remarks>
    public int HeadWidthOf(int node) => _headWidth[node];

    /// <summary>
    /// The width from the node's start to the first <em>break point</em> inside it — the one
    /// measure of the three that treats an optional break as though it were taken.
    /// </summary>
    /// <remarks>
    /// ⚠ The third measure, and milestone 3 could not choose a wrap point without it.
    /// <see cref="HeadWidthOf"/> stops only at a break that is certain, so for
    /// <c>= new Dictionary&lt;…&gt; { a, b }</c> — whose only breaks are the initializer's optional
    /// ones — head and flat are the same number and the question "how much of this lands on the
    /// current line if the inner construct wraps" has no answer. This measure answers it:
    /// <c>= new Dictionary&lt;…&gt; {</c>.
    /// <para>
    /// It is the pessimistic reading — every break point taken — which is the correct one for the
    /// question it is asked, because it is only ever consulted once the group is known not to fit
    /// flat, and a group that does not fit flat has some inner break that will be taken.
    /// </para>
    /// </remarks>
    public int PointWidthOf(int node) => _pointWidth[node];

    /// <summary>
    /// The width from a group's <em>own</em> first break point to the next break point after it.
    /// </summary>
    /// <remarks>
    /// ⚠ <see cref="PointWidthOf"/> on a group stops at that group's own first point, which for
    /// <c>schema.Properties = new Dictionary&lt;…&gt; {</c> is <c>schema.Properties = </c> and
    /// answers nothing. The number the ordering rule needs is what follows that point and precedes
    /// the next one — <c>new Dictionary&lt;…&gt; {</c> — because that is the rest of the line when
    /// the group declines to break and lets the construct inside it wrap instead.
    /// </remarks>
    public int AfterPointOf(int node) => _afterPoint[node];

    /// <summary>
    /// The flat width from one break point to the next one of the same group: what a fill puts on
    /// the current line if it declines to break here.
    /// </summary>
    /// <remarks>
    /// ⚠ Flat, not <see cref="PointWidthOf"/>, and the difference is visible on real code. A
    /// collection initializer whose second element is itself a 104-column object initializer is
    /// broken before that element by the oracle — so a fill asks "does the whole next item fit",
    /// not "does the next item's first line fit". Measuring the head instead leaves multi-line items
    /// trailing off the end of a line that already has one on it.
    /// </remarks>
    public int SegmentOf(int node) => _segment[node];

    /// <summary>Whether the subtree holds a break of any kind — a hard line or a break point.</summary>
    public bool HasBreak(int node) => _hasBreak[node];

    /// <summary>What the fitter needs to know about one group beyond its mode and its width.</summary>
    public GroupFacts FactsOf(int group) => _facts[group];

    /// <summary>A subtree that contains a hard break can never be flat; this is its flat width.</summary>
    public const int Unbounded = int.MaxValue / 4;

    public ReadOnlySpan<int> ChildrenOf(int node) {
        ref var slot = ref Nodes[node];
        return new ReadOnlySpan<int>(Children, slot.Payload, slot.Count);
    }

    public string TextOf(int node) => Strings[Nodes[node].Payload];
}


/// <summary>
/// The per-group half of the <see cref="GroupMode.Preserve"/> rule.
/// </summary>
/// <remarks>
/// ⚠ docs/plan/04 states Preserve as one rule — "broken iff it was broken in the source, subject to
/// width" — and one rule is not enough, because "subject to width" runs in two directions and the
/// export wants a different one per construct family.
/// <list type="bullet">
/// <item>
/// An argument list <em>adds</em> breaks for width: <c>chop_if_long</c> chops a call that does not
/// fit even though the author wrote it on one line. <see cref="BreaksIfTooLong"/>.
/// </item>
/// <item>
/// An expression-bodied member <em>removes</em> one: <c>keep_existing_expr_member_arrangement =
/// false</c> re-joins <c>int P =&gt;\n 1;</c>, and leaves the break alone when joining would not
/// fit. <see cref="JoinsIfFits"/>.
/// </item>
/// <item>
/// Neither is the other's default. Giving the arrow the argument list's rule — break after
/// <c>=&gt;</c> whenever the declaration is over 120 — costs 0.7 points of line fidelity on
/// <c>corpus/real/</c>, because the oracle wraps such a line at a different point and Skala's break
/// then lands one line away from the oracle's. Choosing <em>which</em> of a line's candidate points
/// to wrap at is <c>prefer_wrap_around_eq</c>'s job and belongs to milestone 3.
/// </item>
/// </list>
/// </remarks>
/// <param name="SourceBroken">
/// ⚠ Whether the source held a break at one of this group's <em>own</em> break points — not
/// "somewhere inside the group". <c>var n = aaa +\n bbb;</c> and <c>var n = aaa\n + bbb;</c> are
/// both breaks inside the same binary chain; the oracle removes the first and keeps the second,
/// because <c>wrap_before_binary_opsign = true</c> makes only the gap before the operator a break
/// point. A containment test cannot tell them apart.
/// </param>
/// <param name="JoinsIfFits">The group may remove the author's break when the flat form fits.</param>
/// <param name="BreaksIfTooLong">The group may add breaks the author did not write, to fit.</param>
/// <param name="MeasuresHead">
/// Fit against <see cref="Document.HeadWidthOf"/> — what remains of the line — rather than the
/// whole flat width.
/// </param>
/// <param name="PrefersOuterBreak">
/// ⚠ The ordering rule, and the substance of milestone 3. A group with this fact set does not break
/// merely because it is too long: it breaks when its own break is the one worth taking, and
/// otherwise stays flat and lets the construct inside it wrap. Measured against the oracle on four
/// shapes that a "break when too long" rule gets wrong in three different directions:
/// <list type="table">
/// <item>
/// <term><c>JsonObjectContract c = (JsonObjectContract)r.ResolveContract(typeof(T));</c></term>
/// <description>
/// The oracle breaks after the <c>=</c> and leaves the call whole, because that alone fits. Two
/// lines, not the three that chopping the argument list would cost.
/// </description>
/// </item>
/// <item>
/// <term><c>LogEventInfo e = new LogEventInfo { Message = m, Level = l, Exception = x };</c></term>
/// <description>Same: the <c>=</c> break alone fits, so the initializer never wraps.</description>
/// </item>
/// <item>
/// <term><c>schema.Properties = new Dictionary&lt;…&gt; { … };</c></term>
/// <description>
/// The <c>=</c> break does not make it fit, and the line ends at the initializer's <c>{</c> either
/// way — so breaking after the <c>=</c> buys a line and gains nothing. The oracle leaves it.
/// </description>
/// </item>
/// <item>
/// <term><c>ExtensionDataTestClass a = JsonConvert.DeserializeObject&lt;…&gt;(…);</c></term>
/// <description>
/// The <c>=</c> break does not make it fit <em>and</em> the head does not fit either — the call's
/// name alone runs past 120 — so the oracle takes both breaks.
/// </description>
/// </item>
/// </list>
/// </param>
/// <param name="HidesFlatWidthWhenBroken">
/// ⚠ When this group is certain to break, nothing containing it has a flat form either — the same
/// rule <see cref="GroupMode.Break"/> already carries, extended to a
/// <see cref="GroupMode.Preserve"/> group whose source was broken and which may not re-join. The
/// oracle chops <c>Report(Diagnostic.Create(</c> into two lines as soon as the inner call is
/// broken, although the outer call's own flat width is 59 columns.
/// <para>
/// ⚠ Set on delimited lists and not on every Preserve group, and the difference is measured. An
/// expression body's arrow is resolved against its whole flat width — "if owner is single line"
/// means the declaration occupies one line — so an unbreakable body would make every such arrow
/// break, and <c>bool Property(object o) =&gt; o is { … };</c> would lose its first line. Applying
/// it everywhere costs 0.12 points of line fidelity and two of the four preservation corners.
/// </para>
/// </param>
/// <param name="SpendsIndent">
/// The group opens the continuation scope its own break points land in, so the column after one of
/// its breaks is one level deeper than the line it is on. The fitter needs the number, not the flag,
/// but only the writer knows the indentation stack.
/// </param>
/// <param name="BreaksWithOwner">
/// ⚠ A <see cref="GroupMode.Preserve"/> group that additionally breaks whenever the group named by
/// <see cref="Owner"/> broke. It is what lets <c>chop_if_long</c> mean "chop every operator of the
/// chain at once" while each operator still keeps its own preserve behaviour: the chain group holds
/// no break points and decides only whether the whole chain fits, and every operator group reads it.
/// One group cannot do both, because <c>keep_user_linebreaks = true</c> requires a chain the author
/// broke at one operator to come back with exactly that one break.
/// </param>
/// <param name="Owner">
/// The group a <see cref="GroupMode.Owner"/> group reads its mode from, or the chain group a
/// <see cref="BreaksWithOwner"/> group reads, or −1.
/// </param>
public readonly record struct GroupFacts(
    bool SourceBroken = false,
    bool JoinsIfFits = false,
    bool BreaksIfTooLong = false,
    bool MeasuresHead = false,
    bool PrefersOuterBreak = false,
    bool HidesFlatWidthWhenBroken = false,
    bool SpendsIndent = false,
    bool BreaksWithOwner = false,
    int Owner = -1);
