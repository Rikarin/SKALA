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

/// <summary>What a <see cref="DocKind.Group" /> node carries in <see cref="DocNode.Flags" />.</summary>
[Flags]
public enum GroupFlags {
    None = 0,

    /// <summary>
    ///     The measure from the group's first break point ran to the group's end without meeting a
    ///     break: nothing inside can end the line, so what trails the group lands on it too. Bit 1 is
    ///     the closer alignment an <see cref="DocKind.Indent" /> node shares the field with.
    /// </summary>
    AfterPointRunsToTheEnd = 2,

    /// <summary>
    ///     An arrow group — <see cref="GroupFacts.BreaksOnlyIfHeadOverflows" />, not broken in the
    ///     source — whose body holds no break point of its own, so that the constructs <em>before</em>
    ///     the arrow measure their rest-of-line through it, body included, and break in preference to it.
    /// </summary>
    /// <remarks>
    ///     ⚠ Measured on twenty-five switch arms (issue #378). <c>{ … } =&gt; 2u,</c> at 122 columns
    ///     comes back from the oracle with the pattern's braces apart and the arrow untouched, and
    ///     <c>A or B or C or D =&gt; 2u,</c> with every <c>or</c> chopped, where the same heads followed
    ///     by <c>=&gt; Body(…)</c> stay whole and the arrow or the arguments break. A body that can break
    ///     inside ends the line the head is measured against; one that cannot is part of it. ⚠ Not the
    ///     whole of the oracle's rule: with a body that runs to the end but is wide enough — eleven
    ///     characters at 122 columns, more as the overflow grows — the oracle moves the body down and
    ///     leaves the head whole, by a boundary that depends on both widths and is recorded, not
    ///     modelled. Skala reads through the arrow for every such body, which is the reading that
    ///     preserves Rider's own output in both cases: a chopped pattern before a short body is not
    ///     re-joined, and a kept arrow break before a wide one is kept.
    /// </remarks>
    ArrowBodyRunsToTheEnd = 4,

    /// <summary>
    ///     The group's first break point renders as a space when flat. Read with
    ///     <see cref="GroupFacts.TailEndsAt" />: the line that point stays on counts the space, the
    ///     segment after it does not.
    /// </summary>
    FirstPointFlatSpace = 8
}

/// <summary>What a <see cref="DocKind.Line" /> node carries in <see cref="DocNode.Flags" />.</summary>
[Flags]
public enum LineFlags {
    None = 0,

    /// <summary>
    ///     A <see cref="LineKind.Soft" /> break renders as one space when its group is flat.
    /// </summary>
    /// <remarks>
    ///     ⚠ This is the flat half of the break-position model. <c>Foo(a, b)</c> has three break points
    ///     and they do not render alike when the group stays flat: the one after <c>(</c> and the one
    ///     before <c>)</c> render as nothing, the one after <c>,</c> renders as a space. A soft break
    ///     with a single flat rendering produces <c>Foo( a, b )</c> or <c>Foo(a,b)</c> and there is no
    ///     third choice.
    /// </remarks>
    FlatSpace = 1,

    /// <summary>
    ///     The point belongs to a fill: it breaks only when what follows it would not fit, rather than
    ///     with the rest of its group.
    /// </summary>
    /// <remarks>
    ///     ⚠ The flag is on the point and not on the group, because a fill's delimiters and its item
    ///     separators do not behave alike. <c>wrap_array_initializer_style = wrap_if_long</c> puts the
    ///     <c>{</c> at the end of the opening line and the <c>}</c> on a line of its own
    ///     <em>
    ///         whenever
    ///         the initializer wraps at all
    ///     </em>
    ///     , and fills only the gaps between elements:
    ///     <code>
    /// var e = new[] {
    ///     "aaaaaaaaaaaaaaa", "bbbbbbbbbbbbbbb", "ccccccccccccccc", "ddddddddddddddd", "eeeeeeeeeeeeeee",
    ///     "fffffffffffffff"
    /// };
    ///     </code>
    ///     A group-wide fill mode would either put the braces in the fill — producing
    ///     <c>new[] { "aaa",</c> — or take the elements out of it. Neither is what the oracle writes.
    /// </remarks>
    FillPoint = 2,

    /// <summary>
    ///     The last of its group's own break points, so what follows it on the line runs past the end
    ///     of the group.
    /// </summary>
    /// <remarks>
    ///     ⚠ Set by <see cref="DocumentBuilder" /> when the group closes and its segments are measured,
    ///     because only then is it known which point was last.
    ///     <para>
    ///         ⚠ It exists because <see cref="Document.SegmentOf" /> stops at the group's next point, and
    ///         at the last point there is no next one — so the measure ends at the group's own end and
    ///         the fill never sees the <c>) {</c> that follows it. That is not a rounding error: at the
    ///         export's 120-column margin a <c>for</c> header of 121 columns has 118 before its <c>) {</c>
    ///         and an <c>if</c> condition of 121 has 118 before its own, so <em>both</em> fills declined a
    ///         break the oracle takes, and neither was reachable by any width the fixture could choose.
    ///         <see cref="LayoutWriter" />'s TrailingWidth is the same measure a group already gets on
    ///         entry; this is what lets the last point of a fill ask for it too.
    ///     </para>
    /// </remarks>
    LastPoint = 4,

    /// <summary>
    ///     A fill point taken only as a last resort: the rest of the line is measured <em>through</em> it,
    ///     so every construct before it wraps first and the point breaks only when what follows it still
    ///     has no room after they have.
    /// </summary>
    /// <remarks>
    ///     ⚠ The oracle's embedded statement (SK-DIV-0106). A 125-column <c>while (…) n++;</c> whose
    ///     header alone is 120 comes back with every <c>&amp;&amp;</c> of its condition on
    ///     its own line and <c>n++</c> still after the <c>)</c>: the condition chain was resolved
    ///     against a line that included the statement, and only afterwards did the statement's own
    ///     gap ask whether it fits. An ordinary fill point ends <see cref="LayoutWriter" />'s trailing
    ///     measure — "the rest of this line if every break point is taken" — so the chain measured 120,
    ///     stayed whole, and the statement was pushed off instead. A point with this flag contributes
    ///     its flat rendering to that measure and does not end it; its own decision is still the fill's.
    /// </remarks>
    LastResort = 8,

    /// <summary>
    ///     The item after this fill point opens with a delimiter — <c>(</c>, <c>[</c> or <c>{</c> — so
    ///     when it fits nowhere whole its head may stay on the line and the item break inside.
    /// </summary>
    /// <remarks>
    ///     ⚠ SK-DIV-0110, and the boundary is measured rather than derived: the oracle keeps
    ///     <c>, (2</c> and <c>), [</c> on the line before an item that has no flat form anywhere, and
    ///     breaks before <c>SixthConditionValueLong &amp;&amp; …</c>, a 133-column chain that fits
    ///     nowhere either. An opening delimiter may hang at the end of a line; an identifier's item
    ///     starts a fresh one. The front end sets it, because only it knows the token.
    /// </remarks>
    DelimitedItem = 16,

    /// <summary>
    ///     The item after this fill point keeps its head on the line when it is multi-line by a break
    ///     of its own — a kept one, or one a rule requires — and moves whole when it is merely too wide.
    /// </summary>
    /// <remarks>
    ///     ⚠ SK-DIV-0114, the other half of the boundary above, measured on a tuple:
    ///     <c>(1↵, G&lt;int↵, int&gt;())</c> and <c>(1↵, Get(2,↵3))</c> keep <c>, G&lt;int</c> and
    ///     <c>, Get(</c> on the comma's line, while <c>(1, Get(…))</c> and <c>(1, G&lt;A, B, C&gt;())</c>
    ///     over the margin are broken before <c>Get</c> and <c>G</c>. The reason an identifier-headed
    ///     item spans lines decides, and only the segment measure knows it: a certain break inside makes
    ///     the segment unbounded, width alone leaves it finite. A <see cref="DelimitedItem" /> keeps
    ///     its head in both cases.
    /// </remarks>
    KeepsHeadWhenCertain = 32,

    /// <summary>
    ///     A fill point that yields to what stands <em>before its group</em> and to nothing inside it:
    ///     a construct in front of the list is measured through the point, as through a
    ///     <see cref="LastResort" /> one, so an <c>=</c> or an argument list ahead of the list wraps
    ///     first; a construct inside one of the list's own items sees its line end at the point, as at
    ///     any fill point, so the list's comma breaks before anything nested in an item does.
    /// </summary>
    /// <remarks>
    ///     ⚠ The type argument list's points (SK-DIV-0114), and the half of "last resort" they were
    ///     never measured for. Flagged <see cref="LastResort" />, the <c>List&lt;Guid&gt;</c> nested in the
    ///     first argument of <c>Dictionary&lt;(…, List&lt;Guid&gt;&gt; First, …), List&lt;…&gt;&gt;</c> measured
    ///     its rest-of-line through the outer comma, saw the whole 145-column parameter, resolved
    ///     broken and broke at its own <c>&lt;</c> — <c>List&lt;↵Guid&gt;&gt;</c> — where the oracle
    ///     breaks the outer list at that comma and leaves every nested list whole; the same on
    ///     <c>Dictionary&lt;Dictionary&lt;A, List&lt;B&gt;&gt;, List&lt;…&gt;&gt;</c> with no tuple anywhere (issue
    ///     #377). The embedded statement's gap, which the flag was written for, is the other way round:
    ///     the header's own <c>&amp;&amp;</c> chain wraps before the statement is pushed off
    ///     (SK-DIV-0106), so that one keeps <see cref="LastResort" />. <see cref="DocumentBuilder" />
    ///     measures both as not taken; only <see cref="LayoutWriter" />'s rest-of-line walk tells them
    ///     apart.
    /// </remarks>
    YieldsToPredecessors = 64,

    /// <summary>
    ///     A point of a broken group that is still not taken when the line it would create fits beside
    ///     it: the writer lays that line out, measures it, and joins the two when
    ///     <c>column + gap + line ≤ width</c>.
    /// </summary>
    /// <remarks>
    ///     ⚠ The gap after a parameter's single attribute section (issue #377). The oracle decides it
    ///     twice and differently: on a flat <c>[Obsolete] Dictionary&lt;(…), List&lt;…&gt;&gt; p15</c> that runs
    ///     to 156 columns it puts <c>[Obsolete]</c> on its own line and fills the list below; on its
    ///     own output it joins <c>[Obsolete] Dictionary&lt;(…),</c> because the kept comma break now ends
    ///     the measure at 92. The joined form is its fixed point, measured stable; <c>[Obsolete]</c>
    ///     stays alone exactly when the parameter's <em>first line as laid out alone</em> does not fit
    ///     after it — a 107-column three-argument list that fits alone at column 12 and a
    ///     four-argument list whose first line is 103 wide both keep it alone, a first line of 70
    ///     joins. Skala reaches that fixed point from either input in one pass, which the oracle's
    ///     first answer cannot be made to do: a source-shaped rule gives one answer to the flat input
    ///     and another to its own output (#375's shape). The first line is what the writer would write,
    ///     so the writer writes it — speculatively, on a checkpoint it rolls back — rather than a
    ///     second copy of the fill's rules guessing at it.
    /// </remarks>
    BreaksOnlyIfNextLineOverflows = 128,

    /// <summary>
    ///     ⚠ A point in front of an array initializer's element (#444, SK-DIV-0208). Its fill measures the
    ///     element by <see cref="Document.DraftSegmentOf" />, and the element after one that spanned lines
    ///     starts a line of its own.
    /// </summary>
    ArrayElement = 256,

    /// <summary>
    ///     A required line that keeps a break the author wrote, which the draft measure reads as a space;
    ///     with <see cref="ArrayElement" />, one in front of an element.
    /// </summary>
    KeptBreak = 512,

    /// <summary>
    ///     ⚠ A <c>wrap_if_long</c> chain's fill point before a call link (#484, SK-DIV-0129): the link keeps
    ///     its head on the line and chops its arguments when, moved down, its line would end past 72
    ///     columns at the export's 120 — 90 with <see cref="ChainCallOneArgument" />.
    /// </summary>
    ChainCallLink = 1024,

    /// <summary>With <see cref="ChainCallLink" />: the link's call has one argument or none.</summary>
    ChainCallOneArgument = 2048,

    /// <summary>
    ///     ⚠ A <see cref="LastResort" /> point that the rest-of-line measure reads through even once its
    ///     group has resolved Broken: the gap between a parameter's one attribute section and a short
    ///     parameter (#476, SK-DIV-0352). The oracle chops the section's arguments exactly when the joined
    ///     line overflows, and puts the parameter below them, rather than moving the parameter alone.
    /// </summary>
    ReadThroughWhenBroken = 4096,

    /// <summary>
    ///     ⚠ The first point of a fill whose items align under the first one: it breaks when the items that
    ///     would stay on its line before the fill's first wrap are narrower than twelve columns (SK-DIV-0351).
    ///     <c>skala_align_multiline_type_parameter_list = true</c>'s gap after the <c>&lt;</c>.
    /// </summary>
    AlignedListHead = 8192
}

/// <summary>
///     The three-state group model, the concrete form of ADR-002.
/// </summary>
public enum GroupMode {
    /// <summary>Never break.</summary>
    Flat,

    /// <summary>Always break.</summary>
    Break,

    /// <summary>Break iff too wide — the classic Prettier group.</summary>
    Auto,

    /// <summary>
    ///     ⚠ The third state: broken iff it was broken in the source, subject to width — with "subject
    ///     to width" spelled out per group by <see cref="GroupFacts" />.
    /// </summary>
    Preserve,

    /// <summary>
    ///     ⚠ The fourth: broken iff the group named by <see cref="Document.OwnerOf" /> resolved broken.
    /// </summary>
    /// <remarks>
    ///     <c>place_*_on_single_line = if_owner_is_single_line</c>, which five keys in the export use.
    ///     The owner resolves first and the child only reads it, so a child may move Flat → Broken and
    ///     never back, which is what makes termination a property of the shape rather than of a
    ///     convergence argument (docs/plan/04 § "The fitting algorithm").
    /// </remarks>
    Owner
}

/// <summary>What a <see cref="DocKind.Indent" /> node carries in <see cref="DocNode.Arg1" />.</summary>
[Flags]
public enum IndentFlags {
    None = 0,

    /// <summary>The scope counts even when another scope opened on the same line.</summary>
    Unconditional = 1,

    /// <summary>
    ///     ⚠ A grouping parenthesis's scope. It spends its level for a continuation line inside it —
    ///     <c>if ((a</c> / <c>== b))</c> is two levels — but a block that opens on the parenthesis's own
    ///     line nests through it: <c>var x = (y switch {</c> puts the arms one level past the statement
    ///     and the <c>}</c> on it, exactly where they go without the parenthesis. Unless the construct
    ///     inside the parenthesis broke (<see cref="GroupFacts.Continues" />): <c>(y switch { … }</c> /
    ///     <c>+ 1)</c> puts the arms two levels in (issue #393, SK-DIV-0148).
    /// </summary>
    Grouping = 2,

    /// <summary>
    ///     ⚠ A held level while the group that owns the scope stays flat, and a continuation level once
    ///     it broke. The owner is the nearest group around the scope — the scope is its first child —
    ///     and the writer has entered it, and so resolved it, before it reaches the scope. A switch
    ///     arm's group before its <c>=&gt;</c> is the one user (issue #406, SK-DIV-0157).
    /// </summary>
    HeldWhileOwnerFlat = 4,

    /// <summary>
    ///     ⚠ A held level while the chain group whose id the node carries in place of a column count
    ///     takes none of its points, and a continuation level once it takes one. Nothing has decided
    ///     that when the scope opens — the chain is inside it, and a fill decides point by point at
    ///     the writer's columns — so the writer lays the scope's contents out held, watches the chain,
    ///     and rolls back before deciding (issue #407, SK-DIV-0158).
    /// </summary>
    HeldWhileChainWhole = 8,

    /// <summary>
    ///     ⚠ A delimited list's scope — an argument, parameter or bracketed list, not a grouping
    ///     parenthesis. Opened on the first line of a chained call or a binary operator that broke after
    ///     it, the list nests from that construct's continuation line rather than collapsing into it:
    ///     <c>var x = source.Select(</c> / arguments / <c>)</c> / <c>.Where(beta);</c> puts the arguments
    ///     two levels past the statement and the <c>)</c> one, with the dots (issue #418, SK-DIV-0184).
    ///     ⚠ Read by the lift alone, so a grouping parenthesis's scope (<see cref="Grouping" />) and a
    ///     chain frame's continuation lift the same way (#470, #481) without carrying this flag's name.
    ///     The chain's carries it: the document builder sets it on the level a chain frame spends.
    /// </summary>
    Delimiter = 16,

    /// <summary>
    ///     ⚠ An <see cref="IndentKind.Align" /> scope opened just after a one-column opener whose closer,
    ///     on a line of its own, sits under that opener: a statement's condition <c>(</c> under
    ///     <c>align_multiline_statement_conditions = true</c>, where the oracle writes <c>if (a</c> /
    ///     <c>   ) { }</c> with the <c>)</c> in the <c>(</c>'s column, not the statement's (#442,
    ///     SK-DIV-0203).
    /// </summary>
    CloserAtOpener = 32,

    /// <summary>
    ///     ⚠ A chained call's own continuation scope. Opened on the first line of a binary operator that
    ///     broke after it — the chain is the operator's left operand — it nests from the operator's
    ///     continuation line, as a <see cref="Delimiter" /> list does: <c>var w = a.SelfLink()</c> /
    ///     <c>.SelfLink()</c> two levels in / <c>+ other;</c> one (#457, SK-DIV-0068). The chain's own
    ///     group is the scope's owner and is skipped: it is the construct the scope belongs to, not one
    ///     around it.
    /// </summary>
    ChainLevel = 64,

    /// <summary>
    ///     ⚠ A grouping parenthesis heading a chain that the author broke before a dot after its
    ///     <c>)</c>, where no group of the chain's own carries the break (#470, SK-DIV-0112). Its
    ///     contents nest from the line after the <c>(</c>'s — the chain's continuation line — when
    ///     that line is deeper: <c>var z = (</c> / <c>a).B</c> / <c>.C();</c> puts <c>a</c> two levels
    ///     past the statement and <c>.C</c> one. The writer cannot see a frame's break coming, so the
    ///     document builder reads it from the source and says so.
    /// </summary>
    BrokenAfter = 128,

    /// <summary>
    ///     ⚠ An <see cref="IndentKind.Anchor" /> that records the indentation of the line it is pushed
    ///     on as written, rather than the level a block opening there would nest from: a switch
    ///     expression whose governing <c>)</c> was kept on a line of its own nests its arms from that
    ///     line, whatever paid for its indentation (#506).
    /// </summary>
    AnchorAtLine = 256,

    /// <summary>
    ///     ⚠ A <see cref="IndentKind.Block" /> or <see cref="IndentKind.AnchoredBlock" /> whose contents
    ///     are a continuation rather than a body: a braced initializer's elements and a switch
    ///     expression's arms under <c>skala_use_continuous_indent_inside_initializer_braces = true</c>.
    ///     They take <c>skala_continuous_indent_multiplier</c> indent widths where a body takes one;
    ///     the closing brace still returns to the opener's level (#464). At the export's multiplier of 1
    ///     the two are the same number.
    /// </summary>
    Multiplied = 512,

    /// <summary>
    ///     ⚠ An unconditional continuation scope that leaves the scopes outside it on the same line
    ///     counting too: a binary pattern chain that is the left operand of a broken <c>&amp;&amp;</c> or
    ///     <c>||</c> takes its level past that operator's, although both opened on the statement's line —
    ///     <c>var e = n.P is A</c> / <c>or B</c> / <c>&amp;&amp; c;</c> puts the <c>or</c> at 16 and the
    ///     <c>&amp;&amp;</c> at 12 (#560, SK-DIV-0394). Where the operator's level opened on an earlier
    ///     line — under a broken <c>=&gt;</c> — it counts as any scope does, and nothing is added.
    /// </summary>
    Additive = 1024,

    /// <summary>
    ///     ⚠ A held level spent once the group named beside it resolves broken: a sole lambda's arrow, for
    ///     the pattern chain in its body (#566). See <c>HeldLevel.WhileArrowFlat</c>.
    /// </summary>
    HeldWhileGroupFlat = 2048,

    /// <summary>
    ///     ⚠ An <see cref="IndentKind.FromLine" /> scope for a chain that is the body of a sole lambda nested
    ///     in another's: one level more for each enclosing argument list opened on the line beyond the
    ///     innermost (#585).
    /// </summary>
    NestedSoleLambda = 4096
}

/// <summary>The indentation flavours from docs/plan/04 § "Indentation".</summary>
public enum IndentKind {
    /// <summary>One level per <c>{ }</c>, per <c>case</c>, per embedded statement.</summary>
    Block,

    /// <summary>Continuation lines of one expression. <c>continuous_line_indent = single</c>.</summary>
    Continuous,

    /// <summary>
    ///     One indent width of continuation, whatever <c>continuous_indent_multiplier</c> says.
    /// </summary>
    /// <remarks>
    ///     ⚠ <see cref="Continuous" /> with the multiplier forced to 1, and it exists because that is
    ///     what <c>use_continuous_indent_inside_parens = false</c> means. Measured against
    ///     <c>jb cleanupcode</c> 2025.2.6 with <c>continuous_indent_multiplier = 2</c>, which is the only
    ///     configuration that can tell the two apart — under the export's own multiplier of 1 they are
    ///     the same number, and that is why the key read <c>SPURIOUS</c> in the sweep:
    ///     <code>
    /// M(              M(              ← multiplier = 2
    ///         a,          a,
    ///         b           b
    /// );              );
    /// true            false
    /// 8 + 2×4         8 + 1×4
    ///     </code>
    ///     ⚠ Not <see cref="Block" />: a block is absolute and <em>replaces</em> whatever continuation is
    ///     open, and the contents of a parenthesis do not reset the continuation context. This is
    ///     relative, and composes exactly as <see cref="Continuous" /> does.
    /// </remarks>
    OneLevel,

    /// <summary>No change; a scope marker only.</summary>
    None,

    /// <summary>
    ///     No change; a marker that remembers the indentation of the line it opened on, for an
    ///     <see cref="AnchoredBlock" /> inside it to nest from.
    /// </summary>
    /// <remarks>
    ///     ⚠ A switch expression's arms take one level from the line its <em>governing expression</em>
    ///     starts on, not from the line its <c>{</c> lands on (SK-DIV-0107). The two differ whenever the
    ///     governing expression is multi-line under a continuation the statement opened but never wrote
    ///     a break at: <c>var s = (a,\n b) switch {</c> puts the arms at the statement's level plus one,
    ///     where a block nesting from the <c>{</c>'s line — which sits inside the <c>=</c>'s continuation
    ///     — put them a level deeper. The anchor is pushed where the governing expression begins and read
    ///     where the brace opens.
    /// </remarks>
    Anchor,

    /// <summary>
    ///     A <see cref="Block" /> whose outer level is the innermost <see cref="Anchor" />'s recorded
    ///     indentation rather than the level the brace's own line nests from.
    /// </summary>
    AnchoredBlock,

    /// <summary>
    ///     The innermost <see cref="Anchor" />'s recorded indentation itself — the column an
    ///     <see cref="AnchoredBlock" />'s <c>}</c> takes — for the <c>{</c> that opens it when the brace
    ///     is on a line of its own.
    /// </summary>
    /// <remarks>
    ///     ⚠ Measured under <c>csharp_new_line_before_open_brace = all</c> (#465): <c>Action b = () =&gt;</c>
    ///     / <c>{</c>, <c>var m = new List&lt;int&gt;</c> / <c>{</c> and <c>var r = 1 switch</c> / <c>{</c>
    ///     put the brace on the statement's column, where the <c>=</c>'s continuation had put it one
    ///     level in; under an argument list's level the brace is one level in, on its <c>}</c>'s column.
    /// </remarks>
    AnchoredBrace,

    /// <summary>One level less — the nested-statement outdent family.</summary>
    Outdent,

    /// <summary>
    ///     A fixed number of <em>columns</em> less, for every line but the one the scope opened on.
    /// </summary>
    /// <remarks>
    ///     ⚠ Not <see cref="Outdent" /> with a smaller number, and the distinction is the one
    ///     docs/plan/05 § "Indentation" recorded as missing when it filed <c>outdent_binary_ops</c>,
    ///     <c>outdent_dots</c> and <c>outdent_ternary_ops</c> as "observable and not implemented …
    ///     they need a scope kind the IR does not have". <see cref="Outdent" /> is one indent level and
    ///     is absolute — it <em>replaces</em> what is open, because it is a block. This is a relative
    ///     shift of a column count that is not a multiple of the indent width, it composes with
    ///     whatever alignment or continuation is already open, and it takes no part in the
    ///     one-level-per-opening-line collapse: it is not a level.
    ///     <para>
    ///         ⚠ The amount is the width of the operator that starts the line plus the space written after
    ///         it, which is exactly the offset that leaves the <em>operand</em> on the column it would have
    ///         had without the outdent. Measured against <c>jb cleanupcode</c> 2025.2.6 at a 70-column
    ///         margin, one key at a time, and the same arithmetic covers all of them:
    ///         <code>
    /// outdent_binary_ops         `+`    12 → 10      (1 + 1)
    /// outdent_binary_ops         `&amp;&amp;`   12 →  9      (2 + 1)
    /// outdent_binary_pattern_ops `and`  12 →  8      (3 + 1)
    /// outdent_dots               `.`    12 → 11      (1 + 0, space_after_dot = false)
    ///         </code>
    ///     </para>
    ///     <para>
    ///         ⚠ It composes with <see cref="Align" /> rather than replacing it, which is measured rather
    ///         than assumed: with <c>align_multiline_expression = true</c> beside
    ///         <c>outdent_binary_ops = true</c> the oracle writes the operands on the expression's own
    ///         column and the operators two to the left of it, 18 → 16 and 19 → 16.
    ///     </para>
    /// </remarks>
    OutdentColumns,

    /// <summary>
    ///     A column rather than a level: everything inside starts at the column the scope opened at.
    /// </summary>
    /// <remarks>
    ///     ⚠ docs/plan/04 reserves an <c>Align</c> node and milestones 1–3 never produced one, which is
    ///     what SK-DIV-0008 recorded. <c>align_multiline_statement_conditions = true</c> is the key that
    ///     needs it: a condition broken across lines is laid out from the column just after the
    ///     statement's <c>(</c>, which is not a multiple of the indent width.
    /// </remarks>
    Align,

    /// <summary>
    ///     One level past the indentation of the line the scope opens on, whatever else is open there.
    /// </summary>
    /// <remarks>
    ///     ⚠ An <c>is</c> or <c>as</c> breaks one level past its operand's <em>line</em> (#445): under
    ///     <c>|| x</c> it lands four past the <c>||</c> line, and inside a lambda that is an argument —
    ///     <c>nodes.Count(c =&gt; c.Parent</c> / <c>is ArgumentSyntax</c> — four past the
    ///     <c>nodes.Count(</c> line, not eight: the argument list opened on that line adds nothing. A
    ///     continuation level stacked on whatever was open counted the argument list too. Absolute, as a
    ///     block is: a scope opened inside it composes on top of it.
    /// </remarks>
    FromLine
}

/// <summary>
///     One IR node.
/// </summary>
/// <remarks>
///     ⚠ A struct in a per-file arena, indexed by <c>int</c>, not a class
///     (docs/plan/13 § "The fitting pass"): a 1 000-line file produces ~40 000 nodes and the reference
///     corpus produces ~110 M, so class allocation here is several GB of garbage per run. docs/plan/04
///     writes the IR as records; the two documents disagree and performance wins, because doc 13 states
///     its constraints as design constraints rather than as later tuning.
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

    /// <summary>Kind-specific bit flags: <see cref="LineFlags" />, <see cref="VerbatimFlags" />.</summary>
    public int Flags;

    /// <summary>
    ///     Kind-specific: for <see cref="DocKind.Line" /> the group whose mode decides the break, and
    ///     for <see cref="DocKind.Group" /> the owner group of a <see cref="GroupMode.Owner" /> group.
    ///     −1 when there is none.
    /// </summary>
    public int Arg2;

    /// <summary>The source span this node came from; <see cref="SourceSpan.Length" /> 0 when synthetic.</summary>
    public SourceSpan Source;
}

/// <summary>
///     A document: a struct arena of nodes plus the side tables they index.
/// </summary>
public sealed class Document {
    readonly int[] flatWidth;
    readonly int[] headWidth;
    readonly int[] pointWidth;
    readonly int[] afterPoint;
    readonly int[] segment;
    readonly int[] segmentHead;
    readonly int[] draftSegment;
    readonly bool[] hasBreak;
    readonly GroupFacts[] facts;
    readonly IReadOnlyDictionary<int, int> yieldEnds;
    readonly Dictionary<int, int> throughWidth;
    readonly Dictionary<int, int[]> alignedItems;

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
        int[] segmentHead,
        int[] draftSegment,
        bool[] hasBreak,
        GroupFacts[] facts,
        IReadOnlyDictionary<int, int> yieldEnds,
        Dictionary<int, int>? throughWidth = null,
        Dictionary<int, int[]>? alignedItems = null
    ) {
        Nodes = nodes;
        NodeCount = nodeCount;
        Children = children;
        Strings = strings;
        Root = root;
        GroupCount = groupCount;
        this.flatWidth = flatWidth;
        this.headWidth = headWidth;
        this.pointWidth = pointWidth;
        this.afterPoint = afterPoint;
        this.segment = segment;
        this.segmentHead = segmentHead;
        this.draftSegment = draftSegment;
        this.hasBreak = hasBreak;
        this.facts = facts;
        this.yieldEnds = yieldEnds;
        this.throughWidth = throughWidth ?? [];
        this.alignedItems = alignedItems ?? [];
    }

    public DocNode[] Nodes { get; }

    public int NodeCount { get; }

    public int[] Children { get; }

    public string[] Strings { get; }

    public int Root { get; }

    public int GroupCount { get; }

    /// <summary>
    ///     The width the subtree at <paramref name="node" /> occupies with every group flat.
    /// </summary>
    /// <remarks>
    ///     ⚠ Computed by <see cref="DocumentBuilder" /> as the arena is filled, not by a traversal of
    ///     its own: docs/plan/13 § "The fitting pass" — "the measure pass is fused into the build pass
    ///     where a group's contents are already known, which removes one full traversal". A subtree
    ///     containing a hard break is <see cref="Unbounded" />.
    /// </remarks>
    public int FlatWidthOf(int node) => flatWidth[node];

    /// <summary>
    ///     The width from the node's start to the first break inside it, or its flat width when there
    ///     is none.
    /// </summary>
    /// <remarks>
    ///     ⚠ The second of the two measures a group can be fitted against, and the two are not
    ///     interchangeable. A list — an argument list, an enum body, a switch expression — is chopped
    ///     when it is "long <em>or multiline</em>", which is ReSharper's own wording for
    ///     <c>chop_if_long</c>, so its measure is the flat width and a hard break anywhere inside makes
    ///     it infinite. A tail — the right-hand side of an <c>=</c>, the body after an <c>=&gt;</c> —
    ///     is broken only when what remains of the current line does not fit, because breaking after
    ///     the <c>=</c> of <c>Original = new Thing {</c> gains nothing: the line was going to end at the
    ///     brace either way. Measuring a tail by its flat width costs 7.6 points of line fidelity, which
    ///     is how this distinction was found.
    /// </remarks>
    public int HeadWidthOf(int node) => headWidth[node];

    /// <summary>
    ///     The width from the node's start to the first <em>break point</em> inside it — the one
    ///     measure of the three that treats an optional break as though it were taken.
    /// </summary>
    /// <remarks>
    ///     ⚠ The third measure, and milestone 3 could not choose a wrap point without it.
    ///     <see cref="HeadWidthOf" /> stops only at a break that is certain, so for
    ///     <c>= new Dictionary&lt;…&gt; { a, b }</c> — whose only breaks are the initializer's optional
    ///     ones — head and flat are the same number and the question "how much of this lands on the
    ///     current line if the inner construct wraps" has no answer. This measure answers it:
    ///     <c>= new Dictionary&lt;…&gt; {</c>.
    ///     <para>
    ///         It is the pessimistic reading — every break point taken — which is the correct one for the
    ///         question it is asked, because it is only ever consulted once the group is known not to fit
    ///         flat, and a group that does not fit flat has some inner break that will be taken.
    ///     </para>
    /// </remarks>
    public int PointWidthOf(int node) => pointWidth[node];

    /// <summary>
    ///     The width from a group's <em>own</em> first break point to the next break point after it.
    /// </summary>
    /// <remarks>
    ///     ⚠ <see cref="PointWidthOf" /> on a group stops at that group's own first point, which for
    ///     <c>schema.Properties = new Dictionary&lt;…&gt; {</c> is <c>schema.Properties = </c> and
    ///     answers nothing. The number the ordering rule needs is what follows that point and precedes
    ///     the next one — <c>new Dictionary&lt;…&gt; {</c> — because that is the rest of the line when
    ///     the group declines to break and lets the construct inside it wrap instead.
    /// </remarks>
    public int AfterPointOf(int node) => afterPoint[node];

    /// <summary>
    ///     For a group that <see cref="GroupFacts.YieldsToOverflowingTypeArguments" />: the point width from
    ///     its own first point to the end of the first group of yielding points after it — a type argument
    ///     list's <c>&gt;</c>. Zero when there is none.
    /// </summary>
    public int YieldEndOf(int node) => yieldEnds.TryGetValue(node, out var end) ? end : 0;

    /// <summary>
    ///     The flat width from one break point to the next one of the same group: what a fill puts on
    ///     the current line if it declines to break here.
    /// </summary>
    /// <remarks>
    ///     ⚠ Flat, not <see cref="PointWidthOf" />, and the difference is visible on real code. A
    ///     collection initializer whose second element is itself a 104-column object initializer is
    ///     broken before that element by the oracle — so a fill asks "does the whole next item fit",
    ///     not "does the next item's first line fit". Measuring the head instead leaves multi-line items
    ///     trailing off the end of a line that already has one on it.
    ///     <para>
    ///         On a group node it is the group's <em>first</em> point's segment — the same convention
    ///         <see cref="AfterPointOf" /> already follows — which for a group with one point is
    ///         everything past that point: the tail <see cref="GroupFacts.BreaksOnlyIfTailFits" /> is
    ///         measured by. Zero for a group that owns no point.
    ///     </para>
    /// </remarks>
    public int SegmentOf(int node) => segment[node];

    /// <summary>
    ///     The width from a fill point to the first place inside the next item where a break could
    ///     land, or the whole segment when there is none. A fill that cannot make the item fit whole
    ///     by breaking here keeps this much on the line instead (SK-DIV-0110).
    /// </summary>
    public int SegmentHeadOf(int node) => segmentHead[node];

    /// <summary>
    ///     The draft width from a point to the next one of its group: flat with the author's kept breaks
    ///     read as spaces, ending at a moved comment's first line. What an array initializer's fill
    ///     measures an element by (#444, SK-DIV-0208).
    /// </summary>
    public int DraftSegmentOf(int node) => draftSegment[node];

    /// <summary>Whether the subtree holds a break of any kind — a hard line or a break point.</summary>
    public bool HasBreak(int node) => hasBreak[node];

    /// <summary>
    ///     Whether nothing after a group's first break point can end the line — see
    ///     <see cref="GroupFlags.AfterPointRunsToTheEnd" />.
    /// </summary>
    public bool AfterPointRunsToTheEnd(int node) =>
        Nodes[node].Kind == DocKind.Group && (Nodes[node].Flags & (int)GroupFlags.AfterPointRunsToTheEnd) != 0;

    /// <summary>
    ///     The flat width from a group's start to the first point of the group its
    ///     <see cref="GroupFacts.TailEndsAt" /> names — its own points at their flat rendering — or
    ///     <see cref="Unbounded" /> when anything before that point is certain to break. The flat width
    ///     for any other node. See <see cref="GroupFacts.MeasuresThroughTail" />.
    /// </summary>
    public int[] AlignedItemsOf(int group) => alignedItems.TryGetValue(group, out var items) ? items : [];

    /// <summary>
    ///     The flat width from a group's start to the first point of the group its
    ///     <see cref="GroupFacts.TailEndsAt" /> names — see <see cref="ThroughWidthOf" />.
    /// </summary>
    public int ThroughWidthOf(int node) => throughWidth.TryGetValue(node, out var width) ? width : FlatWidthOf(node);

    /// <summary>
    ///     The width the group's first break point renders as when flat — see
    ///     <see cref="GroupFlags.FirstPointFlatSpace" />.
    /// </summary>
    public int FirstPointFlatWidthOf(int node) =>
        Nodes[node].Kind == DocKind.Group && (Nodes[node].Flags & (int)GroupFlags.FirstPointFlatSpace) != 0 ? 1 : 0;

    /// <summary>
    ///     Whether the node is an arrow group whose body cannot break, so that the rest-of-line measure
    ///     of what precedes it runs through it — see <see cref="GroupFlags.ArrowBodyRunsToTheEnd" />.
    /// </summary>
    public bool ArrowBodyRunsToTheEnd(int node) =>
        Nodes[node].Kind == DocKind.Group && (Nodes[node].Flags & (int)GroupFlags.ArrowBodyRunsToTheEnd) != 0;

    /// <summary>What the fitter needs to know about one group beyond its mode and its width.</summary>
    public GroupFacts FactsOf(int group) => facts[group];

    /// <summary>A subtree that contains a hard break can never be flat; this is its flat width.</summary>
    public const int Unbounded = int.MaxValue / 4;

    public ReadOnlySpan<int> ChildrenOf(int node) {
        ref var slot = ref Nodes[node];
        return new(Children, slot.Payload, slot.Count);
    }

    public string TextOf(int node) => Strings[Nodes[node].Payload];
}


/// <summary>
///     The per-group half of the <see cref="GroupMode.Preserve" /> rule.
/// </summary>
/// <remarks>
///     ⚠ docs/plan/04 states Preserve as one rule — "broken iff it was broken in the source, subject to
///     width" — and one rule is not enough, because "subject to width" runs in two directions and the
///     export wants a different one per construct family.
///     <list type="bullet">
///         <item>
///             An argument list <em>adds</em> breaks for width: <c>chop_if_long</c> chops a call that does not
///             fit even though the author wrote it on one line. <see cref="BreaksIfTooLong" />.
///         </item>
///         <item>
///             An expression-bodied member <em>removes</em> one:
///             <c>
///                 keep_existing_expr_member_arrangement =
///                 false
///             </c>
///             re-joins <c>int P =&gt;\n 1;</c>, and leaves the break alone when joining would not
///             fit. <see cref="JoinsIfFits" />.
///         </item>
///         <item>
///             Neither is the other's default. Giving the arrow the argument list's rule — break after
///             <c>=&gt;</c> whenever the declaration is over 120 — costs 0.7 points of line fidelity on
///             <c>corpus/real/</c>, because the oracle wraps such a line at a different point and Skala's break
///             then lands one line away from the oracle's. Choosing <em>which</em> of a line's candidate points
///             to wrap at is <c>prefer_wrap_around_eq</c>'s job and belongs to milestone 3.
///         </item>
///     </list>
/// </remarks>
/// <param name="SourceBroken">
///     ⚠ Whether the source held a break at one of this group's <em>own</em> break points — not
///     "somewhere inside the group". <c>var n = aaa +\n bbb;</c> and <c>var n = aaa\n + bbb;</c> are
///     both breaks inside the same binary chain; the oracle removes the first and keeps the second,
///     because <c>wrap_before_binary_opsign = true</c> makes only the gap before the operator a break
///     point. A containment test cannot tell them apart.
/// </param>
/// <param name="JoinsIfFits">The group may remove the author's break when the flat form fits.</param>
/// <param name="BreaksIfTooLong">The group may add breaks the author did not write, to fit.</param>
/// <param name="MeasuresHead">
///     Fit against <see cref="Document.HeadWidthOf" /> — what remains of the line — rather than the
///     whole flat width.
/// </param>
/// <param name="PrefersOuterBreak">
///     ⚠ The ordering rule, and the substance of milestone 3. A group with this fact set does not break
///     merely because it is too long: it breaks when its own break is the one worth taking, and
///     otherwise stays flat and lets the construct inside it wrap. Measured against the oracle on four
///     shapes that a "break when too long" rule gets wrong in three different directions:
///     <list type="table">
///         <item>
///             <term>
///                 <c>JsonObjectContract c = (JsonObjectContract)r.ResolveContract(typeof(T));</c>
///             </term>
///             <description>
///                 The oracle breaks after the <c>=</c> and leaves the call whole, because that alone fits. Two
///                 lines, not the three that chopping the argument list would cost.
///             </description>
///         </item>
///         <item>
///             <term>
///                 <c>LogEventInfo e = new LogEventInfo { Message = m, Level = l, Exception = x };</c>
///             </term>
///             <description>Same: the <c>=</c> break alone fits, so the initializer never wraps.</description>
///         </item>
///         <item>
///             <term>
///                 <c>schema.Properties = new Dictionary&lt;…&gt; { … };</c>
///             </term>
///             <description>
///                 The <c>=</c> break does not make it fit, and the line ends at the initializer's <c>{</c> either
///                 way — so breaking after the <c>=</c> buys a line and gains nothing. The oracle leaves it.
///             </description>
///         </item>
///         <item>
///             <term>
///                 <c>ExtensionDataTestClass a = JsonConvert.DeserializeObject&lt;…&gt;(…);</c>
///             </term>
///             <description>
///                 The <c>=</c> break does not make it fit <em>and</em> the head does not fit either — the call's
///                 name alone runs past 120 — so the oracle takes both breaks.
///             </description>
///         </item>
///     </list>
/// </param>
/// <param name="HidesFlatWidthWhenBroken">
///     ⚠ When this group is certain to break, nothing containing it has a flat form either — the same
///     rule <see cref="GroupMode.Break" /> already carries, extended to a
///     <see cref="GroupMode.Preserve" /> group whose source was broken and which may not re-join. The
///     oracle chops <c>Report(Diagnostic.Create(</c> into two lines as soon as the inner call is
///     broken, although the outer call's own flat width is 59 columns.
///     <para>
///         ⚠ Set on delimited lists and not on every Preserve group, and the difference is measured. An
///         expression body's arrow is resolved against its whole flat width — "if owner is single line"
///         means the declaration occupies one line — so an unbreakable body would make every such arrow
///         break, and <c>bool Property(object o) =&gt; o is { … };</c> would lose its first line. Applying
///         it everywhere costs 0.12 points of line fidelity and two of the four preservation corners.
///     </para>
/// </param>
/// <param name="SpendsIndent">
///     The group opens the continuation scope its own break points land in, so the column after one of
///     its breaks is one level deeper than the line it is on. The fitter needs the number, not the flag,
///     but only the writer knows the indentation stack.
/// </param>
/// <param name="BreaksWithOwner">
///     ⚠ A <see cref="GroupMode.Preserve" /> group that additionally breaks whenever the group named by
///     <see cref="Owner" /> broke. It is what lets <c>chop_if_long</c> mean "chop every operator of the
///     chain at once" while each operator still keeps its own preserve behaviour: the chain group holds
///     no break points and decides only whether the whole chain fits, and every operator group reads it.
///     One group cannot do both, because <c>keep_user_linebreaks = true</c> requires a chain the author
///     broke at one operator to come back with exactly that one break.
/// </param>
/// <param name="Owner">
///     The group a <see cref="GroupMode.Owner" /> group reads its mode from, or the chain group a
///     <see cref="BreaksWithOwner" /> group reads, or the head marker a
///     <see cref="BreaksIfOwnerIsMultiLine" /> group reads, or −1.
/// </param>
/// <param name="ChainLink">
///     ⚠ One operator of a binary chain: a <see cref="BreaksWithOwner" /> group whose owner is the
///     chain-wide group and nothing else. The two containment rules of SK-DIV-0109 apply to links
///     alone: a link whose operand holds something certain to break breaks on its own, and the owner
///     it reports to is measured without its links' own breaks.
/// </param>
/// <param name="BreaksIfOwnerIsMultiLine">
///     ⚠ A <see cref="GroupMode.Preserve" /> group that breaks whenever the writer has started a new
///     line since it entered the group named by <see cref="Owner" /> — a zero-width
///     <see cref="GroupMode.Flat" /> marker the front end puts at the first token of the construct
///     that owns this one. It is <c>if_owner_is_single_line</c> read off the <em>output</em>: the
///     owner is not single-line when any break before this group was taken, whoever took it — a
///     chopped parameter list, a filled type parameter list, a <c>where</c> clause moved down, or a
///     break the author wrote after a modifier and <c>keep_user_linebreaks</c> kept. Reading any one
///     of those from the source or from one list's group instead is what made pass one keep
///     <c>=&gt; body;</c> on the line a type parameter fill had just broken, and pass two — now
///     seeing the break as the author's — move it (#372). Measured on eleven shapes: the oracle
///     breaks the arrow after every one of them, and after none where only an attribute list
///     precedes the declaration on its own line, which is why the marker sits after the attributes.
/// </param>
/// <param name="BreaksOnlyIfTailFits">
///     ⚠ A group whose break is an <em>alternative</em> to a delimiter's own rather than a pair with it:
///     the break is taken — kept when the author wrote it, added when the line is too long — exactly
///     when what follows the point fits flat on the continuation line, and otherwise the group stays
///     flat and the delimiter breaks instead, <em>whatever that does to the delimiter's line</em>. It
///     is the <c>=</c> and the <c>=&gt;</c> before a collection expression (issues #375 and #379): the
///     oracle keeps <c>int[] x =\n [1, 2];</c> and every <c>=\n[…]</c> whose bracket fits on
///     the line below, breaks a flat <c>T v = […];</c> after the <c>=</c> exactly when the bracket fits
///     there — 120 columns on the continuation line, 121 not, with no margin and however far left the
///     <c>=</c> is — and writes <c>x = [</c> for one that does not, because it is too wide for that
///     line, because the author broke it at one of its own gaps, or because an element inside it spans
///     lines. ⚠ From a flat source the ordering rule used to answer this with
///     <see cref="PrefersOuterBreak" />'s fitted margin, which took the <c>=</c> break for a bracket the
///     oracle glues (<c>T v = [</c> at 122 columns, the bracket overhanging, and the line measured only
///     up to the <c>=</c>) and declined it for one the oracle moves down whole; and the kept path then
///     undid pass one on pass two. Reading the source for the second reason alone had been the earlier
///     instrument that disagreed with itself across passes: a list chopped around a multi-line element
///     has no break at its own gaps on pass one and has them on pass two. The tail is the group's flat
///     width past its point, so the front end must leave that width measurable — the group is still
///     certain to make its container multi-line, whichever of the two breaks is taken, and
///     <see cref="DocumentBuilder" /> keeps its certainty while declining to hide its width.
/// </param>
/// <param name="MinimumHead">
///     ⚠ For a <see cref="BreaksOnlyIfTailFits" /> group on a flat line: the break is not <em>added</em>
///     unless the head — from the first token of the construct that owns the <c>=</c> through the
///     group's own point — is at least this many columns wide; below it the group stays flat and the
///     bracket breaks, whatever fits where. Zero means no such floor. The owner's first token is the
///     marker named by <see cref="Owner" />, and the fitter reads the column it landed on.
///     <para>
///         Measured on the oracle one column at a time (issue #379): <c>var ddddd = […];</c> comes back
///         <c>var ddddd = [</c> and <c>var dddddd = […];</c> comes back <c>var dddddd =\n[…];</c> for the
///         same bracket, which fits the line below either way — an 11-column head glues and a 12-column
///         head breaks, and the same for <c>int[] d =</c> through <c>int[] ddd =</c> against
///         <c>object[] d =</c>, for a field, a <c>for</c> header, an initializer element and a named
///         attribute argument, at <c>indent_size = 2</c> and at a nested block's indent alike. The head
///         starts at the construct's own scope, not at the line: under <c>using (</c> it is measured from
///         the parenthesis — <c>(var dddd =</c> is 11 and glues, <c>(var ddddd =</c> is 12 and breaks —
///         so it is not a column, not a saving against the continuation line, and not tied to the
///         indent. A <em>kept</em> break has no floor: <c>int[] x =\n[1, 2];</c> is kept at nine (#375).
///         The arrow of an expression body has none either down to a head of eight.
///     </para>
/// </param>
/// <param name="BreaksOnlyIfHeadOverflows">
///     ⚠ A group that breaks only when the line up to the first break point <em>inside</em> it
///     overflows — the ordering rule's second question asked alone, never its first. It is a switch
///     expression arm's <c>=&gt;</c>, a lambda's <c>=&gt;</c> and the gap before a <c>when</c> (issue
///     #378): the oracle keeps <c>1 =&gt; Body(</c> and chops the arguments whenever <c>Body(</c> fits on
///     the head's line — even when the whole body would have fitted on the continuation line, which
///     is the case the <c>=</c>'s <see cref="PrefersOuterBreak" /> takes — and moves the body down only
///     when the head up to that point does not fit: <c>… =&gt;</c> / <c>SomeVeryLongIdentifier,</c>, or
///     <c>{ … } when static x =&gt;</c> / <c>Convert&lt;…&gt;(…)</c> where the lambda's arrow point at
///     column 105 is what fits. Measured on eleven body shapes — an argument list, a chain, an
///     initializer, a ternary, a binary chain, a string, an identifier — and on <c>when</c> in a case
///     label and in an arm alike.
/// </param>
/// <param name="FlatIfOwnerBroke">
///     ⚠ A group that stays flat whenever the group named by <see cref="Owner" /> broke: its break and
///     the owner's are alternatives, and the owner's is the one taken first. The gap after a switch
///     arm's <c>=&gt;</c> reads the gap before it (issue #378): when <c> =&gt;</c> itself has no room on
///     the head's line the arrow moves down and the body follows it on the arrow's line, however wide
///     — <c>{ … }</c> / <c>=&gt; SomeVeryLongIdentifier…,</c> at 140 columns is what the oracle
///     writes, never <c>=&gt;</c> alone on a line. A break the author wrote after the arrow is kept
///     regardless; the fact is read after <see cref="SourceBroken" />.
/// </param>
/// <param name="Continues">
///     ⚠ A block opening on the construct's first line nests from the construct's continuation line
///     once the group broke — the arms of a switch in a binary operator's first operand or in a
///     chain's receiver. Read by <see cref="LayoutWriter" /> alone. Set by a binary operator and a
///     chained call, whether or not the group pays for the level itself: under a delimiter the builder
///     refuses the scope and the delimiter pays, which <see cref="SpendsIndent" /> says (issue #393,
///     SK-DIV-0148). ⚠ Not by a ternary, measured: <c>return y switch { … } is 1</c> / <c>? a</c>
///     keeps the arms one level past the statement, as does a property pattern in the condition.
///     A delimited list opening on that line nests from the same continuation line, and its closer
///     sits on it (issue #418, SK-DIV-0184). ⚠ Not by a fill: a <c>wrap_if_long</c> group resolves
///     broken whenever its construct does not fit whole, which says nothing about whether it breaks
///     after the block (SK-DIV-0185).
/// </param>
/// <param name="Terminator">
///     ⚠ The width of what ends the construct's line — <c>;</c>, <c> { }</c>, <c> {</c> — for a group
///     that breaks exactly when its line overflows <em>by no more than that</em>, and stays flat
///     otherwise. It is <c>place_*_attribute_on_same_line = always</c>'s joining half (#438,
///     SK-DIV-0201): the oracle joins <c>[Obsolete] public void M(…) { }</c> when it fits, joins it and
///     chops the parameters when the <c>)</c> itself is past the margin, and declines the join — the
///     attribute on its own line — when only the terminator is. Zero for any other group.
/// </param>
/// <param name="YieldsToOverflowingTypeArguments">
///     ⚠ A <see cref="BreaksOnlyIfHeadOverflows" /> group that stays flat whenever the line up to the end of
///     the first type argument list after its point — the <c>&gt;</c> — does not fit, and leaves that list
///     to fill (#490, SK-DIV-0177). A named argument's colon: the oracle writes
///     <c>name: Cast&lt;SomeVeryLongTypeArgumentNumberOne,</c> / <c>Taaa…&gt;(x, y)</c> whenever the
///     <c>&gt;</c> is past the margin, at every argument list width swept (6 to 80 columns, the
///     arguments chopped below the fill once they do not fit), and never <c>name:</c> alone. The head
///     the colon otherwise asks about reads through the type arguments to the call's <c>(</c>, which is
///     right when only the <c>(</c> overflows. See <see cref="Document.YieldEndOf" />.
/// </param>
/// <param name="ColonFloor">
///     ⚠ With <see cref="YieldsToOverflowingTypeArguments" />: the argument list width from which the
///     colon breaks after all, the type argument list overflowing or not — measured, not derived (#490,
///     SK-DIV-0177). The front end reads it off the measured grid for the argument's name and first type
///     argument; <see cref="ColonFloorSlope" /> moves it by hundredths of a column per column of the head
///     (the width from the argument's start through the <c>&gt;</c>) past 118.
/// </param>
/// <param name="ColonFloorSlope">See <see cref="ColonFloor" />.</param>
/// <param name="ColonEdgeFloor">
///     ⚠ The same floor for the one column where the type argument list's <c>&gt;</c> fits and only the
///     call's <c>(</c> does not: a different table (#490). Zero breaks the colon whatever the width.
/// </param>
/// <param name="CalleeWidth">
///     ⚠ An <c>=</c> before a call with two or more arguments: the callee's width, which turns the ordering
///     rule into a measured one (#446, SK-DIV-0211). With a head of <see cref="MinimumHead" /> or more the
///     <c>=</c> breaks exactly when the argument list is narrower than <see cref="EqualsFloor.Of" /> at the
///     call's <c>(</c> column and the statement's indent — the value then moving down whole, or chopped
///     below when it does not fit there either — and with a narrower head never. Zero for any other value.
/// </param>
/// <param name="CalleeOwner">Which of <see cref="EqualsFloor" />'s measured owners the <c>=</c> belongs to.</param>
/// <param name="ThroughWidth">
///     ⚠ A group that breaks exactly when its flat form and this many columns after it — a lambda's
///     <c> =&gt;</c> — do not fit on its line, whatever follows (#453). Zero for any other group.
/// </param>
/// <param name="LiftsThroughInnerBreaks">
///     ⚠ A <see cref="Continues" /> group whose lifted list keeps its lifted level for the lines of a
///     construct that broke inside it on its own line — a switch arm whose arrow the author kept on a line
///     of its own (#446, SK-DIV-0212): `when x.All(static e => e` / `is T` / `)` puts the `is`, an `&amp;&amp;`
///     and a `.Member` two levels past the arm, where under a broken chain those lines continue the
///     ordinary way (#418).
/// </param>
/// <param name="PatternHead">
///     ⚠ A local's <c>=</c> before <c>operand is A or B</c>: the head's width through the <c>=</c>. With
///     <see cref="PatternWidth" /> it decides the <c>=</c> by <see cref="EqualsFloor.BreaksBeforeAPattern" />
///     (#446, SK-DIV-0211). Zero for any other group.
/// </param>
/// <param name="HeadSlack">
///     ⚠ Columns a <see cref="BreaksOnlyIfHeadOverflows" /> group adds to its head before asking whether it
///     fits — measured, for the gap after an <c>is</c> before a binary pattern whose first operand is short
///     (#446). Zero for any other group.
/// </param>
/// <param name="YieldsThroughArrow">
///     ⚠ An <c>=</c> before a lambda with a bare name for a body: the width from the lambda's start through
///     its <c>=&gt;</c>. The <c>=</c> stays flat while that much fits after it on its line (#453).
/// </param>
/// <param name="OneOverType">
///     ⚠ A measured local's lambda with a bare-name body, on its <c>=</c> and on its parameter list: the
///     declaration type's width, which with <see cref="OneOverBody" /> decides the one line the other rules
///     do not — one column past the margin, where the parameter list chops up to a head that the type and
///     the body set (#572). See <c>EqualsFloor.ChopsOneOver</c>. Zero for any other group.
/// </param>
/// <param name="LambdaOperandParameters">
///     ⚠ The arrow of a sole lambda argument whose body is an operand chain or a binary pattern on one line:
///     the width of everything before ` =&gt;` — modifiers, parentheses and parameters — or zero for any other
///     group (#578). Past the margin the arrow breaks exactly when it ends at or past
///     <c>EqualsFloor.OperandArrowThreshold</c>, read with the line's end and
///     <see cref="LambdaOperandFirst" />.
/// </param>
/// <param name="LambdaOperandFirst">
///     The width of the body's first operand — <c>a</c> in <c>a &amp;&amp; b</c>, <c>x is A</c> in
///     <c>x is A or B</c>. See <see cref="LambdaOperandParameters" />.
/// </param>
/// <param name="LambdaOperandTail">
///     The width from the body's end to its statement's end — <c>);</c> for a call statement — which the line's
///     end is measured with. See <see cref="LambdaOperandParameters" />.
/// </param>
/// <param name="HeldCallOnAPath">
///     ⚠ A held first call whose receiver is a plain path of names, `source.A…`: when the receiver alone
///     overflows, the call breaks too and every link chops (#582). Not behind a call chain the receiver
///     ends with a `!` or a `?.`, which the oracle keeps holding (ChainLinksIssue454Tests).
/// </param>
/// <param name="BreaksIfItOverflows">
///     ⚠ Broken exactly when the group's own flat width, nothing after it counted, overflows its line: a
///     call chain's receiver that is itself a member access, `source.A….Select(…)`, whose dots break only
///     once the receiver alone runs past the margin (#582).
/// </param>
/// <param name="LambdaChainHead">
///     ⚠ The arrow of a sole lambda argument whose body is a chain of calls: the width from the lambda's start
///     to its first call's dot, or zero for any other group (#571). Past the margin the arrow breaks for a
///     lambda without parentheses from column 21 and one with them from column 25; otherwise by
///     <see cref="LambdaParameters" />' measured line; otherwise when the arrow ends at column 21 or later
///     and the chain's head through that dot no longer fits on the arrow's line. ⚠ Not #529's "the chain
///     fits below", which broke the arrow where the oracle keeps it and fills the chain: 1 540 cells, 4
///     of them, at a parenthesised lambda's column 23, differ. See <see cref="LambdaHead" /> and
///     <see cref="LambdaIsSimple" />, which it shares.
/// </param>
/// <param name="OneOverBody">The lambda's body width. See <see cref="OneOverType" />.</param>
/// <param name="LambdaParameters">
///     ⚠ The arrow of a sole lambda argument whose body is a member-access fill: the width of the lambda's
///     parameter text — <c>x</c>, <c>(x)</c>, <c>(A x, B y)</c> — or zero for any other group (#557). Past
///     the margin the arrow breaks exactly when three times the column the body would end at on the
///     continuation line, plus this width, is at most 336, and otherwise the body fills on the arrow's
///     line. Measured over 1 234 cells; see <see cref="LambdaIsSimple" /> for the one exception.
/// </param>
/// <param name="LambdaHead">
///     The width from the lambda's start through its <c>=&gt;</c>. See <see cref="LambdaParameters" />.
/// </param>
/// <param name="LambdaIsSimple">
///     ⚠ A lambda without parentheses: its arrow breaks whenever the lambda starts at column 21 or past it,
///     however wide the body — measured to a 175-column line. Not measured for a parenthesised lambda.
/// </param>
/// <param name="PatternWidth">The binary pattern's width. See <see cref="PatternHead" />.</param>
/// <param name="KeywordWidth">
///     ⚠ The width of the keyword after this group's one point, for the point before an <c>is</c> or an
///     <c>as</c> (#444, SK-DIV-0210): broken exactly when the operand before the point fits on its line
///     and the operand with a space and the keyword does not. The operand is the group's flat width less
///     the segment after its point and the point's own space. Zero for any other group.
/// </param>
/// <param name="TailEndsAt">
///     ⚠ For a <see cref="PrefersOuterBreak" /> group: the group whose first point ends this group's
///     segment, or −1. A primary constructor's base list with interfaces after its base type
///     (#501, SK-DIV-0198): once the whole list does not fit on the continuation line, the oracle asks
///     its two questions about <c>: B(…),</c> alone — through the list's first comma, reading the base
///     type's argument list as no place to break. The line through that comma fits where the
///     declaration reached: the list stays and the interfaces chop. Otherwise it fits on the
///     continuation line, by the fitted margin: the break goes before the <c>:</c>. Otherwise the
///     ordinary second question decides, and <c>: B(</c> stays with its arguments chopped.
/// </param>
/// <param name="SkipsOuterTail">
///     ⚠ For a <see cref="PrefersOuterBreak" /> group: the first question — does everything after the
///     point fit on the continuation line — is not asked. A primary constructor's base list at
///     <c>skala_wrap_before_extends_colon = true</c> (#502): the oracle keeps <c>: B(</c> and chops the
///     arguments of a list that would fit whole below, and breaks before the <c>:</c> only when the
///     head up to <c>B(</c> does not fit, or by <see cref="TailEndsAt" />'s question.
/// </param>
/// <param name="TailMargin">
///     ⚠ <see cref="OuterMargin" /> for <see cref="TailEndsAt" />'s continuation question, or −1 for the fitted one.
/// </param>
/// <param name="StopsAtYieldingPoints">
///     ⚠ The ordering rule's second question ends at the first point after this group's own, a type
///     argument list's yielding points included: a type declaration's keyword/name gap (#539) is not
///     taken for <c>class G : IDictionary&lt;A…, B…,</c> past the margin, where the list fills.
/// </param>
/// <param name="OuterMargin">
///     ⚠ For a <see cref="PrefersOuterBreak" /> group: the margin its first question leaves, in place of the
///     fitted one (<c>Fitter.OuterBreakMargin</c>), or −1. A type's base list with one base type after a
///     primary constructor (SK-DIV-0198): the oracle stops breaking before the <c>:</c> once the
///     continuation line reaches 88 or 89 columns at two depths, where the fitted margin went on to 105.
/// </param>
/// <param name="CreationLimit">
///     ⚠ For a <see cref="PrefersOuterBreak" /> <c>=</c> whose value is a creation with an initializer written on
///     one line (#581), in fortieths of a column, or zero: the widest continuation line the creation moves down
///     whole to, before the column the indent and the margin move it by. The oracle's limit is not the fitted
///     margin's: it grows with the width of <c>new X {</c> and shrinks with the head from the declarator's name
///     through the <c>=</c>, by <c>110.5 + 0.6 · prefix − 0.4 · max(name head, 23) − (indent − 8) / 8</c>
///     columns, two fewer for a field. Otherwise the braces break. Negative: the head through the <c>=</c> is
///     under twelve columns, and the braces always break. ⚠ Only at the 120-column margin it was measured at;
///     any other margin leaves the decision to the fitted one. See <c>Fitter.Worth</c>.
/// </param>
/// <param name="JoinedOverflow">
///     ⚠ For a <see cref="PrefersOuterBreak" /> group: its first question asks whether the <em>joined</em> line
///     overflows by at most this many columns, not whether the tail fits on the continuation line, or −1. A
///     type's name (#539, SK-DIV-0353): the oracle breaks before it up to a 124-column line behind
///     <c>class</c>, <c>public class</c> and <c>internal sealed class</c> alike, so how much the continuation
///     line saves is not the variable.
/// </param>
/// <param name="NameWidth">
///     ⚠ With <see cref="JoinedOverflow" />: the width of the name the group's point stands before, or −1. The
///     first question then also asks <c>9·column + 6·width + <see cref="NameFloor" /> ≥ 8·end</c>, where
///     <c>column</c> is where the name starts and <c>end</c> where the joined line ends — a short name behind a
///     short head stays, and the list after it wraps instead (#539, SK-DIV-0353).
/// </param>
/// <param name="OneOverValue">
///     ⚠ For a local's type/name gap: the width of its value through the <c>;</c>, when the planner has found
///     the type and the name to be ones the oracle breaks between at a line one column past the margin
///     (#583, SK-DIV-0127); zero otherwise. At exactly that line the gap breaks — where 122 breaks the
///     <c>=</c> — if the <c>=</c> would break; see <see cref="OneOverEquals" />.
/// </param>
/// <param name="OneOverEquals">
///     −1 for a value of a bare name, whose <c>=</c> breaks there. Otherwise the value is a lambda and this is
///     its <c>=</c>'s group, whose arrow and parameter-list rules are asked at the head the name gives it
///     (−2 until the planner links the two); the gap then asks nothing else.
/// </param>
/// <param name="NameFloor">
///     The constant of <see cref="NameWidth" />'s rule, which the planner lowers by three per column of the
///     competing list's first item: a longer first item keeps more names on the keyword's line.
/// </param>
/// <param name="MeasuresThroughTail">
///     ⚠ The group is fitted against <see cref="Document.ThroughWidthOf" /> — from its start to the
///     first point of the group <see cref="TailEndsAt" /> names — with nothing trailing it: flat when
///     that much fits, broken otherwise. A parameter's run of two or more attribute sections (#475,
///     SK-DIV-0350): the oracle puts every section and the parameter on lines of their own as soon as
///     the sections do not fit on one line together, or one of them spans lines, and leaves the gap
///     before the parameter to its own rule when they do.
/// </param>
/// <param name="ContinuesIfItBreaks">
///     ⚠ <see cref="Continues" /> for a fill chain, whose group resolving broken does not say it breaks
///     (#496, SK-DIV-0185): a delimited list on the chain's first line lifts exactly when the chain then
///     takes one of its points, which the writer answers by writing the rest of the chain ahead with the
///     list unlifted and watching the group. A chain whose author's breaks the fill pinned lifts outright
///     (<see cref="Continues" />), so the second pass — which reads the fill's break as the author's —
///     gives the same answer as the first.
/// </param>
/// <param name="ValueHeadWidth">
///     ⚠ An <c>=</c> whose value is a conditional (#553): the flat width of the condition. The oracle
///     breaks the <c>=</c> exactly when the condition does not fit beside it and the head through the
///     <c>=</c> reaches <see cref="MinimumHead" /> — whatever the condition's own points could do — or,
///     with <see cref="ValueHeadFitsBelow" />, when the condition then fits below. Zero for any other value.
/// </param>
/// <param name="ValueHeadFitsBelow">
///     With <see cref="ValueHeadWidth" />: the condition is a call, whose <c>=</c> breaks only when the
///     condition fits on the line below.
/// </param>
/// <param name="HeldValue">
///     ⚠ An <c>=</c> whose value is a single call on a receiver (#528, SK-DIV-0331), by its head, or zero:
///     1 a typed local, 2 a <c>var</c> or assignment head under twelve columns, 3 one of twelve or more.
///     When the value does not fit beside it, the <c>=</c> breaks by a measured table — the typed local
///     when the value fits below with three columns to spare; the short head when it overflows below by at
///     most one column or its <c>(</c> lands three short of the margin there; the long head unless the
///     call would move down at its dot as a chain's held first call does — and otherwise the call's own
///     dot takes the break: <c>T c = JsonConvert</c> / <c>.DeserializeObject&lt;…&gt;(json);</c>.
/// </param>
/// <param name="HeldValueWidth">With <see cref="HeldValue" />: the value's flat width with its <c>;</c>.</param>
/// <param name="HeldValueReceiver">With <see cref="HeldValue" />: the receiver's flat width.</param>
/// <param name="HeldValueHead">With <see cref="HeldValue" />: the width from the dot through the <c>(</c>.</param>
/// <param name="ArmHead">
///     ⚠ A switch arm's member-access pattern (#531, SK-DIV-0330): the pattern's flat width, with
///     <see cref="ArmBody" />. The pattern's fill engages by a measured table on the column the arm's
///     <c>=&gt;</c> ends at and the body's width rather than by its own width alone. See
///     <c>Fitter.ArmFills</c>.
/// </param>
/// <param name="ArmBody">With <see cref="ArmHead" />: the arm's body with its comma, if it has one.</param>
/// <param name="HeldValueManyArgs">With <see cref="HeldValue" />: the call has more than one argument.</param>
/// <param name="FlatIfHeadOverflows">
///     ⚠ An assignment's <c>=</c> whose target is a member-access fill (#531, SK-DIV-0330): when the target
///     with its <c>=</c> does not fit on the line, the target's own dot breaks and the <c>=</c> stays —
///     <c>A.B.C.D.More</c> / <c>.Value = 1;</c> — where a break after the <c>=</c> would leave the line it
///     ends as long as it was.
/// </param>
/// <param name="HeldCall">
///     ⚠ A chain's held first call (#528, SK-DIV-0331), as the columns its line has to end short of the
///     margin by, or zero: the point before it breaks exactly when the
///     receiver fits on its line, the receiver with the call does not, and the call fits whole on the
///     continuation line. A receiver that does not fit flat breaks inside itself and leaves the point alone.
///     1 and 2 are a chain's first call with one argument and with more; 3 and 4 a single call that is a
///     whole <c>=</c> value (<see cref="HeldValue" />) with one argument and with more.
/// </param>
/// <param name="HeldCallRest">
///     ⚠ Under <c>wrap_if_long</c> (#552): the flat width of the chain after the held call, through its
///     <c>;</c>, or zero. Past the measured table's limit the held call still breaks before itself when
///     that rest is wider than <c>1.5 · (line − limit) + 9</c> — the longer the rest, the further past the
///     limit the oracle moves the call down rather than chop it.
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
    int Owner = -1,
    bool ChainLink = false,
    bool BreaksIfOwnerIsMultiLine = false,
    bool BreaksOnlyIfTailFits = false,
    int MinimumHead = 0,
    bool BreaksOnlyIfHeadOverflows = false,
    bool FlatIfOwnerBroke = false,
    bool Continues = false,
    int Terminator = 0,
    int KeywordWidth = 0,
    int TailEndsAt = -1,
    bool SkipsOuterTail = false,
    int OuterMargin = -1,
    int CreationLimit = 0,
    int JoinedOverflow = -1,
    int NameWidth = -1,
    int NameFloor = 0,
    int OneOverValue = 0,
    int OneOverEquals = -1,
    int TailMargin = -1,
    bool StopsAtYieldingPoints = false,
    bool MeasuresThroughTail = false,
    bool YieldsToOverflowingTypeArguments = false,
    int ColonFloor = 0,
    int ColonFloorSlope = 0,
    int ColonEdgeFloor = 0,
    int CalleeWidth = 0,
    EqualsOwner CalleeOwner = EqualsOwner.None,
    int ThroughWidth = 0,
    int HeldCall = 0,
    int HeldCallHead = 0,
    int HeldCallRest = 0,
    bool ContinuesIfItBreaks = false,
    bool FlatIfHeadOverflows = false,
    int ValueHeadWidth = 0,
    bool ValueHeadFitsBelow = false,
    bool ValueHeadIsWide = false,
    int HeldValue = 0,
    int HeldValueWidth = 0,
    int HeldValueReceiver = 0,
    int HeldValueHead = 0,
    bool HeldValueManyArgs = false,
    int ArmHead = 0,
    int ArmBody = 0,
    bool LiftsThroughInnerBreaks = false,
    int PatternHead = 0,
    int PatternWidth = 0,
    int HeadSlack = 0,
    int YieldsThroughArrow = 0,
    int LambdaParameters = 0,
    int LambdaHead = 0,
    bool LambdaIsSimple = false,
    LambdaLocal LambdaLocal = LambdaLocal.None,
    int OneOverType = 0,
    int OneOverBody = 0,
    int LambdaChainHead = 0,
    bool BreaksIfItOverflows = false,
    bool HeldCallOnAPath = false,
    int LambdaOperandParameters = 0,
    int LambdaOperandTail = 0,
    int LambdaOperandFirst = 0);

/// <summary>
///     What a local's <c>=</c> before a lambda with a bare-name body knows of its declaration (#558): the
///     two gates on the declarator's name width, decided from the syntax, under which the measured floors
///     do not apply. See <see cref="GroupFacts.YieldsThroughArrow" />.
/// </summary>
/// <remarks>
///     ⚠ Measured on <c>Func&lt;T…&gt; name = (…) =&gt; body;</c> over type widths of 2 to 59 and name widths
///     of 1 to 51, 12 805 cells. The name and the type act separately, which no head-width table can
///     express: a narrow name keeps the arrow at any value, where the same head made of a wider name
///     breaks the <c>=</c> below a floor.
/// </remarks>
[Flags]
public enum LambdaLocal {
    /// <summary>Not a local's <c>=</c>: the arrow while the line through it fits, as measured in round 3.</summary>
    None = 0,

    /// <summary>A measured local: the floors apply past the gates.</summary>
    Measured = 1,

    /// <summary>
    ///     The name is at most <c>10 + ⌊(type + 4) / 12⌋</c> wide, the type measured whole: while the line through
    ///     <c>=&gt;</c>
    ///     fits, the arrow breaks whatever the value's width.
    /// </summary>
    ArrowWhileItFits = 2,

    /// <summary>
    ///     The name is at most <c>⌊(type − 6) / 5⌋ + 1</c> wide: once the <c>)</c> is off the line, the
    ///     parameter list chops whatever the value's width.
    /// </summary>
    ChopsPastTheParenthesis = 4
}
