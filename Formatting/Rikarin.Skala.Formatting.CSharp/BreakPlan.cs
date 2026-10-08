using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Rikarin.Skala.Options;

namespace Rikarin.Skala.Formatting.CSharp;

/// <summary>What the layout is allowed to do with one inter-token gap.</summary>
public enum GapRule {
    /// <summary>A break point of a group: broken when the group is, its flat form otherwise.</summary>
    Point,

    /// <summary>
    ///     ⚠ Not a break point of the construct that encloses it, so it never holds a break — even if
    ///     the author put one there.
    /// </summary>
    /// <remarks>
    ///     This is the half of the break-position model that removes lines. With
    ///     <c>skala_wrap_before_binary_opsign = true</c> the gap before the operator is a break point and the
    ///     gap after it is not, so <c>a +\n b</c> is re-joined and <c>a\n + b</c> is kept. Milestone 1
    ///     had nowhere to express the difference and kept both.
    /// </remarks>
    Flat,

    /// <summary>A break the rules require, whatever the source did.</summary>
    Mandatory,

    /// <summary>
    ///     A break point of a <em>fill</em>: it breaks when what follows would not fit on the line, and
    ///     not merely because its group broke. <c>wrap_if_long</c>.
    /// </summary>
    FillPoint,

    /// <summary>
    ///     A fill point taken last: the constructs before it on the line wrap first, because the
    ///     rest-of-line measure they are resolved against runs through this gap rather than ending at
    ///     it. An embedded statement's gap under <c>keep_existing_embedded_arrangement</c>
    ///     (SK-DIV-0106). See <see cref="LineFlags.LastResort" />.
    /// </summary>
    LastResortPoint,

    /// <summary>
    ///     A fill point that yields to what stands before its <em>list</em> and to nothing inside it: a
    ///     type argument list's (SK-DIV-0114). The <c>=</c> or the argument list in front of the list is
    ///     resolved against a line that runs through this gap, as with <see cref="LastResortPoint" />;
    ///     a list nested in one of the arguments sees its line end here, as at any fill point, so the
    ///     outer comma breaks before anything inside an argument does: the break lands after
    ///     <c>…Second),</c> and never inside <c>List&lt;Guid&gt;</c> (issue #377). See
    ///     <see cref="LineFlags.YieldsToPredecessors" />.
    /// </summary>
    YieldingFillPoint,

    /// <summary>
    ///     A point of a group that lies <em>after</em> the group's own last token — the gap between an
    ///     attribute section's <c>]</c> and the parameter it decorates (SK-DIV-0114). It breaks with the
    ///     group like any point, but the rest-of-line measure the group is resolved against runs through
    ///     it: the gap breaks only if the group does, so a group asking "do I fit flat?" must count what
    ///     follows the gap as still on its line. Measured as an ordinary point, a 126-column
    ///     <c>[Obsolete("…", true)] int a</c> saw its line end at the <c>]</c> and left the arguments
    ///     whole.
    ///     <para>
    ///         ⚠ And it is not taken merely because the group broke: the writer lays out the line the
    ///         break would create and joins the two when that line fits beside the section
    ///         (<see cref="LineFlags.BreaksOnlyIfNextLineOverflows" />, issue #377). "The whole
    ///         parameter fits" and "the parameter's first line fits" are the oracle's two answers to the
    ///         same gap, one per pass; the second is the one it returns unchanged.
    ///     </para>
    /// </summary>
    FollowingPoint
}

/// <summary>One gap's rule.</summary>
public readonly record struct GapSpec(GapRule Rule, int Group);

// ⚠ A break point carries no "what it looks like when flat". Its flat form is whatever the ninety
// space rules say about that pair of tokens, and asking the plan instead means the plan has to know
// skala_space_after_comma, skala_space_within_parentheses, skala_space_before_ternary_quest and the rest — which is
// how three Tier A spacing keys silently stopped being observable the first time this was written
// with a bool.

/// <summary>A group the builder opens around one syntax node.</summary>
/// <param name="SpendsIndent">
///     ⚠ The group's break points are not inside a delimiter of their own, so the group opens the
///     continuation scope itself, around its own body. Milestone 1 opened that scope lazily at the break
///     and closed it at the enclosing statement, which was fine while the document had nothing but
///     indent scopes on its stack; with groups on the same stack the two interleave and the group's
///     close pops the indent instead of the group. The symptom is the second operand of
///     <c>a\n + b\n + c</c> landing a level short of the first.
/// </param>
/// <param name="LeadingGapInside">
///     ⚠ The group's first break point is the gap <em>before</em> the node, so the group has to open
///     before that gap is written or the point is emitted outside the group it belongs to and the
///     writer, finding the group unresolved, renders it flat. The one construct that needs it is a base
///     list under <c>skala_wrap_before_extends_colon = true</c>, whose only break point is the colon that
///     starts the node. Every other group's first point is at a token in its interior.
/// </param>
/// <param name="OwnLevel">
///     ⚠ A second continuation level, on top of whatever the construct around it already spends. Only a
///     binary <em>pattern</em> chain asks for it, and docs/plan/04 § "Indentation" is where the
///     asymmetry is recorded: a binary expression chain spends no level of its own and a binary pattern
///     chain spends one. It looks arbitrary and it is what the oracle writes —
///     <c>return x is A\n        or B;</c> puts the operand two levels in and
///     <c>return a\n    + b;</c> puts it one.
/// </param>
/// <param name="SpendsUnderDelimiters">
///     ⚠ The group spends its level even while a delimited scope is open around it, which no other
///     undelimited continuation does. Only an <c>=</c> asks for it, and it is measured: inside a chopped
///     parameter list the oracle writes <c>int a =\n        5</c> with the value one level past the
///     parameter, and the same one level past an object initializer's <c>X =</c> and an attribute's
///     <c>Message =</c> — where a binary chain in the same position, <c>M(\n a\n + b)</c>, lands its
///     operand on the argument's own column. The builder's depth rule exists for the second shape and
///     was refusing the first (SK-DIV-0103). The writer's one-level-per-opening-line rule still applies,
///     so an <c>=</c> on the delimiter's own line spends nothing extra.
/// </param>
/// <param name="HoldsLevel">
///     ⚠ The group spends its continuation level as <em>zero</em> columns: the body it opens is a
///     continuation context — nothing further in may spend the member's or statement's level — and yet
///     its first line lands on the owner's own indent. That is what a parenthesis the author broke
///     after asks for when it heads an arrow's or an <c>=</c>'s body (SK-DIV-0101): the <c>(</c> sits
///     where a <c>{</c> would, its contents one level in from <em>there</em>, and a ternary's <c>?</c>
///     or a chain's <c>.</c> after the <c>)</c> takes its own level from the owner's indent rather than
///     from an arrow level that was never written. Declining the level outright reproduced the first
///     line and not the rest — the frame was left unspent and the ternary's break took it.
///     <see cref="HeldLevel.WhileFlat" /> is the same hold for a group whose own point lies <em>before</em>
///     the body rather than at it, and <see cref="HeldLevel.WhileChainWhole" /> the same hold under a fill
///     that may yet break the chain after the <c>)</c>; see there.
/// </param>
/// <param name="UnconditionalLevel">
///     ⚠ The group's continuation level counts on every line after the one it opened on, even where a
///     scope opened on that same line already counted (<c>IndentFlags.Unconditional</c>). Only a
///     multi-declarator list asks for it (#468, SK-DIV-0109): a declarator's own continuation lands one
///     level past the <em>list's</em> level, the first declarator's included — <c>int x = a</c> /
///     <c>+ 1,</c> at 16 and <c>y = 2;</c> at 12 — although the list and the <c>=</c> both opened on the
///     declaration's first line.
/// </param>
/// <param name="AdditiveLevel">
///     ⚠ The group's continuation level counts beside one already counted on its line without standing
///     in for that line (<c>IndentFlags.Additive</c>): a pattern chain that is an <c>&amp;&amp;</c> or
///     <c>||</c> chain's first operand, whose <c>or</c>s go one level past the operators (#566).
/// </param>
public readonly record struct GroupPlan(
    int Id,
    GroupMode Mode,
    GroupFacts Facts,
    bool SpendsIndent = false,
    bool LeadingGapInside = false,
    bool OwnLevel = false,
    bool SpendsUnderDelimiters = false,
    HeldLevel HoldsLevel = HeldLevel.None,
    bool FromLine = false,
    bool UnconditionalLevel = false,
    bool AdditiveLevel = false);

/// <summary>
///     Whether a group spends its continuation level as zero columns. See <see cref="GroupPlan.HoldsLevel" />.
/// </summary>
/// <remarks>
///     <see cref="WhileFlat" /> and <see cref="WhileChainWhole" /> combine: the level is held while every
///     condition named holds, and spent as columns once one fails.
/// </remarks>
[Flags]
public enum HeldLevel {
    /// <summary>The level is spent as columns, as every other group spends it.</summary>
    None = 0,

    /// <summary>The level is spent as zero columns whatever the group resolves to.</summary>
    Always = 1,

    /// <summary>
    ///     ⚠ The level is spent as zero columns while the group stays flat, and as columns once it
    ///     breaks. A switch arm's group before its <c>=&gt;</c> owns the arm's level, and the body's
    ///     group inside it can spend nothing (issue #406, SK-DIV-0157). The oracle holds the level for a
    ///     body that opens with a parenthesis the author broke after only while the arrow stays on
    ///     the pattern's line — <c>1 =&gt;</c> / <c>(</c> at the arm's indent — and once the arrow moves
    ///     down, <c>1</c> / <c>=&gt;</c> / <c>(</c> puts both one level in, by width or by the author's
    ///     break alike. Whether the arrow broke is the fitter's answer, known when the group is entered
    ///     and before any line inside it starts, so the writer reads it there
    ///     (<see cref="IndentFlags.WhileOwnerBroken" />) rather than the plan guessing it from the
    ///     source.
    /// </summary>
    WhileFlat = 2,

    /// <summary>
    ///     ⚠ The level is spent as zero columns while a fill chain on the body's spine takes none of its
    ///     points, and as columns once it takes one (issue #407, SK-DIV-0158). Under
    ///     <c>wrap_if_long</c> whether the chain breaks is a width question asked at the writer's
    ///     columns, so the writer lays the body out held, watches the chain's group
    ///     (<see cref="BreakPlan.ChainHeldAgainst" />), rolls back, and spends the level if the chain
    ///     broke (<see cref="IndentFlags.WhileChainWhole" />). Pass two reads the fill's break back as
    ///     an author's and disqualifies the hold from the source, which is the same answer.
    /// </summary>
    WhileChainWhole = 4,

    /// <summary>
    ///     ⚠ The level is spent as zero columns while the arrow of the lambda the group is the body of stays
    ///     flat (#566): <c>Use(x =&gt; x is A</c> / <c>or B</c> puts the <c>or</c>s one level past the call's line,
    ///     the parenthesis having spent it, and <c>All(x =&gt;</c> / <c>x is A</c> / <c>or B</c> one level past
    ///     the body's line. See <see cref="BreakPlan.ArrowHeldAgainst" />.
    /// </summary>
    WhileArrowFlat = 8
}

/// <summary>
///     The groups a run of sibling <c>where</c> clauses needs, and where the builder opens each.
/// </summary>
/// <param name="Outer">
///     Opened before the gap that precedes the first <c>where</c>, so it is entered at the column the
///     declaration has reached. It answers <em>does the whole constraint list fit on this line</em>, and
///     it owns the break before the first clause when
///     <c>skala_wrap_before_first_type_parameter_constraint</c> says the first clause is part of that answer.
/// </param>
/// <param name="Inner">
///     Opened <em>after</em> that gap, so it is entered at the column the first clause actually lands
///     on — one continuation level in and a line down when <paramref name="OwnsLeadingGap" /> broke.
///     It answers the second question, <em>do the clauses fit on the line the first one is on</em>, and
///     owns the breaks before every clause after the first.
///     <para>
///         ⚠ Two groups rather than one, and it is the oracle's shape rather than a convenience. Given a
///         declaration whose constraints overflow, ReSharper breaks before the first <c>where</c> and then
///         stops if that alone made them fit — a single chop group would have chopped every clause, and a
///         single fill would have filled the ones a <c>chop_if_long</c> list must not fill.
///     </para>
/// </param>
/// <param name="OwnsLeadingGap">
///     Whether <paramref name="Outer" /> holds the break before the first <c>where</c>, which is what
///     tells the builder to write that gap between the two groups rather than before both.
/// </param>
public readonly record struct ConstraintRun(GroupPlan Outer, GroupPlan Inner, bool OwnsLeadingGap);

/// <summary>
///     Decides, before a token is emitted, which gaps of a construct may break and which may not.
/// </summary>
/// <remarks>
///     ⚠ This is the model milestone 1 did not have. M1 decided <em>whether</em> a gap holds a break by
///     copying the source; M2 has to decide <em>which side of a token</em> a break lands on, because
///     that is what <c>skala_wrap_before_binary_opsign</c>, <c>skala_wrap_after_invocation_lpar</c>,
///     <c>skala_wrap_before_invocation_rpar</c>, <c>skala_wrap_after_dot_in_method_calls</c> and
///     <c>skala_wrap_before_comma</c> configure, and a gap model with only "break / do not break" has nowhere
///     to put the answer.
///     <para>
///         It is a pre-pass over the syntax tree rather than a decision taken during the walk, for one
///         reason: a gap can be at the structural level of two constructs at once. In
///         <c>Foo(\n a + b, c)</c> the gap before <c>a</c> is the argument list's first break point and the
///         binary chain's first non-point, and only a pass that sees both can let the point win. During the
///         walk the innermost open construct is the binary chain, and it gives the wrong answer.
///     </para>
///     <para>
///         The rules are established against the oracle, not read off the option names. The three that
///         matter, and that the option documentation does not state:
///     </para>
///     <list type="number">
///         <item>
///             A break <em>between two items</em> of a list is preserved iff <c>keep_user_linebreaks</c>. A
///             break <em>right after the opening delimiter or before the closing one</em> is preserved iff that
///             construct's <c>keep_existing_*_arrangement</c> — which is what makes those keys observable, and
///             it is why <c>Foo1(\n a)</c> re-joins where <c>Foo2(\n a,\n b)</c> does not.
///         </item>
///         <item>
///             Once a construct is broken at all, a <c>chop_*</c> style breaks <em>every</em> one of its points,
///             the two at the delimiters included. That is why the oracle's output over <c>corpus/real/</c> has
///             1 006 lines that are nothing but a closing parenthesis and milestone 1's had 573.
///         </item>
///         <item>
///             <c>keep_user_wrapping</c> has no observable effect in this export. Both values produce identical
///             output on every shape tried; <c>keep_user_linebreaks</c> is the key that governs.
///         </item>
///     </list>
/// </remarks>
public sealed class BreakPlan {
    readonly Dictionary<int, GapSpec> gaps = [];

    /// <summary>The positions <see cref="PlanPastLeadingComments" /> planned. See <see cref="PlansPastALeadingComment" />.</summary>
    readonly HashSet<int> pastLeadingComments = [];

    /// <summary>The positions <see cref="PlanCommentedAttributeGap" /> planned past a block comment.</summary>
    readonly HashSet<int> pastAttributeComments = [];

    /// <summary>
    ///     The groups opened around one node, outermost first.
    /// </summary>
    /// <remarks>
    ///     ⚠ A list rather than one plan, because two constructs can start and end at the same token and
    ///     need two groups. A binary chain is the case that forces it: the operators keep their own
    ///     groups so that <c>a &amp;&amp; b\n || c</c> comes back unchanged, and the chain needs a group
    ///     of its own on the same node so that <c>chop_if_long</c> can break <em>all</em> of them at once
    ///     when the whole chain is too wide. One group cannot be both.
    /// </remarks>
    readonly Dictionary<long, List<GroupPlan>> groups = [];

    /// <summary>The chain-wide group of a binary chain, keyed by its root node.</summary>
    readonly Dictionary<long, int> chainOwner = [];

    /// <summary>The group <see cref="PlanChainedCalls" /> opened over each chain root.</summary>
    readonly Dictionary<long, int> chainGroups = [];

    /// <summary>
    ///     A group holding its level while a fill chain stays whole, to that chain root's key. See
    ///     <see cref="HeldLevel.WhileChainWhole" />.
    /// </summary>
    readonly Dictionary<int, long> heldAgainst = [];

    /// <summary>The arrow group <see cref="PlanArrowBody" /> opened for each lambda, by the lambda's key.</summary>
    readonly Dictionary<long, int> arrowGroups = [];

    /// <summary>The arrow group each <see cref="HeldLevel.WhileArrowFlat" /> hold is decided by.</summary>
    readonly Dictionary<int, int> arrowHeldAgainst = [];

    /// <summary>
    ///     The chain roots whose <c>wrap_chained_binary_*</c> style is <c>wrap_if_long</c>, so that
    ///     every operator of them plans a fill point rather than an ordinary one.
    /// </summary>
    /// <remarks>
    ///     ⚠ Beside <see cref="chainOwner" /> rather than folded into it, because the two are read at
    ///     different times: the owner is written by <see cref="PlanChainWide" /> from the chain root and
    ///     read by <see cref="PlanOperator" /> from a link, and a link cannot see which of the two
    ///     <c>wrap_chained_binary_*</c> keys governs its chain without repeating the precedence walk
    ///     that found the root.
    /// </remarks>
    readonly HashSet<long> chainFills = [];

    /// <summary>
    ///     The operator tokens a <c>force_chop_compound_*</c> key requires a break at, by position.
    /// </summary>
    /// <remarks>
    ///     ⚠ Positions rather than a group, and the reason is that the forced chop runs along a
    ///     <em>finer</em> chain than <see cref="SameChain" />. That test puts <c>&amp;&amp;</c> and
    ///     <c>||</c> at one precedence on purpose, because <c>skala_wrap_chained_binary_expressions</c> chops
    ///     <c>a &amp;&amp; b || c</c> at both operators; the forced chop takes only the root operator's
    ///     own kind, so <c>a.P &amp;&amp; b.P || c.P</c> comes back broken at the <c>||</c> and whole at
    ///     the <c>&amp;&amp;</c>. Reusing the chain-wide group would break both.
    /// </remarks>
    readonly HashSet<int> forcedChop = [];

    /// <summary>
    ///     The group opened <em>inside</em> a construct's delimiters rather than around them.
    /// </summary>
    /// <remarks>
    ///     ⚠ It cannot be one of <see cref="groups" />, because those are opened around the node and are
    ///     therefore entered at the column the node starts at. The elements of a braced initializer are
    ///     measured against the column they land on <em>after</em> the brace has broken, which is one
    ///     continuation level in and one line down, so the group has to be opened where the elements
    ///     begin. <see cref="CSharpDocumentBuilder.VisitBraced" /> opens it.
    /// </remarks>
    readonly Dictionary<long, GroupPlan> inner = [];

    /// <summary>
    ///     The last attribute section of a parameter's run, by node: the group its gap belongs to, allocated
    ///     by <see cref="PlanAttributeRun" />, and the run's own group, which it breaks with (#475).
    /// </summary>
    readonly Dictionary<long, (int Section, int Run)> attributeRuns = [];

    /// <summary>
    ///     The two groups a run of sibling <c>where</c> clauses needs, keyed by the declaration.
    /// </summary>
    /// <remarks>
    ///     ⚠ Not one of <see cref="groups" />, and the reason is the one <see cref="inner" /> already
    ///     gives for braced elements: a group opened around a node is entered at the column that node
    ///     starts at, and a constraint list is not a node. Its clauses are siblings of the parameter list
    ///     and of the body, with nothing in the tree spanning them, so a group over the run has to be
    ///     opened by the builder as it walks the declaration's children.
    /// </remarks>
    readonly Dictionary<long, ConstraintRun> constraints = [];

    /// <summary>Every group described, by id — what <see cref="SourceBreakSurvives" /> reads.</summary>
    readonly Dictionary<int, GroupPlan> byId = [];

    /// <summary>
    ///     Zero-width <see cref="GroupMode.Flat" /> groups the builder opens and closes at a token, by the
    ///     token's position: the head markers a <see cref="GroupFacts.BreaksIfOwnerIsMultiLine" /> group
    ///     reads its owner's line from.
    /// </summary>
    /// <remarks>
    ///     ⚠ Not one of <see cref="groups" /> either, and for the mirror of the constraint run's reason:
    ///     a group opened around a node is entered at the node's first token, and the head of a
    ///     declaration begins at a token no node starts at — the first modifier, after the attribute
    ///     lists. A marker on the declaration itself would be entered before <c>[Attribute]\n</c> and
    ///     read the attribute's own line as the head's.
    /// </remarks>
    readonly Dictionary<int, int> markers = [];

    /// <summary>
    ///     Groups the builder opens before the gap that precedes one child of a node — a token or a
    ///     nested node — and closes at the end of that node, keyed by the node and the child's
    ///     position. Outermost first.
    /// </summary>
    /// <remarks>
    ///     ⚠ The constraint run's mechanism, generalised: a switch expression arm's <c>=&gt;</c> is a
    ///     token, so no node begins at it, and the group that owns the gap before it — the arrow moves
    ///     down when it has no room (issue #378) — has to be opened by the walk over the arm's children
    ///     and closed after the body. The body's own group is opened the same way rather than described
    ///     on the body node, because <see cref="CSharpDocumentBuilder" /> emits the gap before an
    ///     <em>aligned</em> node before it opens that node's groups, and a point emitted outside its
    ///     group is rendered flat; a group opened here owns the gap whatever the body is.
    /// </remarks>
    readonly Dictionary<(long Node, int Position), List<GroupPlan>> openedAt = [];

    /// <summary>
    ///     The <c>for</c> headers whose "is the header multi-line" answer waits for the walk to finish.
    /// </summary>
    /// <remarks>
    ///     ⚠ The header is planned before the clauses inside it, and its answer depends on what those
    ///     plans do with the author's breaks: <c>for (int i = 0\n, j = 1; …)</c> holds a break the
    ///     declarators re-join, and reading the source at plan time counted it (SK-DIV-0111). So the
    ///     header records itself here and <see cref="SettleForHeaders" /> asks the finished gap table.
    /// </remarks>
    readonly List<ForStatementSyntax> forHeaders = [];

    readonly string source;
    readonly PhaseOneOptions options;
    IReadOnlySet<Microsoft.CodeAnalysis.Text.TextSpan>? captured;
    int[] forced = [];
    int nextGroup;

    BreakPlan(string source, in PhaseOneOptions options) {
        this.source = source;
        this.options = options;
    }

    /// <summary>Group ids handed out; the builder pre-allocates that many on the document.</summary>
    public int GroupCount => nextGroup;

    /// <param name="captured">
    ///     The captured-argument expressions the builder emits verbatim (#432). Nothing inside one is
    ///     planned: a group it would open is never opened, and a break point in it is never a gap.
    /// </param>
    public static BreakPlan Build(
        SyntaxNode root,
        string source,
        in PhaseOneOptions options,
        IReadOnlySet<Microsoft.CodeAnalysis.Text.TextSpan>? captured = null
    ) {
        var plan = new BreakPlan(source, options) { captured = captured };
        plan.Walk(root);
        plan.PlanPastLeadingComments(root);
        plan.SettleForHeaders();
        plan.SettleOpenBraces(root);
        plan.SettleParenthesisedCollections(root);
        plan.CollectForcedBreaks();
        return plan;
    }

    /// <summary>The rule for the gap immediately before <paramref name="position" />, if any.</summary>
    public bool TryGap(int position, out GapSpec spec) => gaps.TryGetValue(position, out spec);

    /// <summary>
    ///     Whether the gap before <paramref name="position" /> is planned past the block comment after an
    ///     argument list's <c>(</c> or an expression body's <c>=&gt;</c>. See <see cref="PlanPastLeadingComments" />.
    /// </summary>
    public bool PlansPastALeadingComment(int position) => pastLeadingComments.Contains(position);

    /// <summary>
    ///     Whether the gap before <paramref name="position" /> — after a field's last attribute section and a
    ///     block comment — is planned past that comment. See <see cref="PlanCommentedAttributeGap" />.
    /// </summary>
    public bool PlansPastAnAttributeComment(int position) => pastAttributeComments.Contains(position);

    /// <summary>The groups the builder opens around <paramref name="node" />, outermost first.</summary>
    public IReadOnlyList<GroupPlan> GroupsOf(SyntaxNode node) =>
        groups.TryGetValue(Key(node), out var plans) ? plans : [];

    /// <summary>The group the builder opens just inside <paramref name="node" />'s delimiters, if any.</summary>
    public bool TryInnerGroup(SyntaxNode node, out GroupPlan plan) => inner.TryGetValue(Key(node), out plan);

    /// <summary>The two groups the builder opens around this declaration's <c>where</c> clauses.</summary>
    public bool TryConstraintRun(SyntaxNode node, out ConstraintRun run) => constraints.TryGetValue(Key(node), out run);

    /// <summary>
    ///     The zero-width marker group the builder opens at the token starting at
    ///     <paramref name="position" />, if any.
    /// </summary>
    public bool TryMarker(int position, out int group) => markers.TryGetValue(position, out group);

    /// <summary>
    ///     The groups the builder opens before the gap preceding the child of <paramref name="node" />
    ///     that starts at <paramref name="position" />, outermost first, and closes at the node's end.
    /// </summary>
    public bool TryOpenedAt(SyntaxNode node, int position, out IReadOnlyList<GroupPlan> plans) {
        if (openedAt.TryGetValue((Key(node), position), out var found)) {
            plans = found;
            return true;
        }

        plans = [];
        return false;
    }

    /// <summary>Every group the plan created, so the builder can describe them to the document.</summary>
    public IEnumerable<GroupPlan> Groups {
        get {
            foreach (var plans in groups.Values) {
                foreach (var plan in plans) {
                    yield return plan;
                }
            }

            foreach (var plan in inner.Values) {
                yield return plan;
            }

            foreach (var run in constraints.Values) {
                yield return run.Outer;
                yield return run.Inner;
            }

            foreach (var plans in openedAt.Values) {
                foreach (var plan in plans) {
                    yield return plan;
                }
            }
        }
    }

    /// <summary>
    ///     Whether anything between <paramref name="start" /> and <paramref name="end" /> is certain to
    ///     break, whatever the source did.
    /// </summary>
    /// <remarks>
    ///     ⚠ The blank-line rules need this, which is not obvious until it bites. Whether a member takes
    ///     <c>skala_blank_lines_around_field</c> or <c>skala_blank_lines_around_single_line_field</c> depends on
    ///     whether it is single-line — in the <em>output</em>. A one-line field the formatter is about to
    ///     chop is not single-line, and reading the input instead makes the first pass emit no blank line
    ///     and the second pass emit one. That is a non-idempotency the corpus does not contain, because
    ///     milestone 1 chopped nothing.
    /// </remarks>
    public bool HasForcedBreakIn(int start, int end) {
        var index = Array.BinarySearch(forced, start);
        if (index < 0) {
            index = ~index;
        }

        return index < forced.Length && forced[index] < end;
    }

    /// <summary>
    ///     The positions at which a break is certain: a <see cref="GroupMode.Break" /> group's points and
    ///     every <see cref="GapRule.Mandatory" /> gap. Sorted once so the blank-line rules can ask about a
    ///     member's span without walking the whole plan per member.
    /// </summary>
    void CollectForcedBreaks() {
        var forced = new List<int>();
        foreach (var (key, plans) in groups) {
            foreach (var plan in plans) {
                if (plan.Mode == GroupMode.Break) {
                    forced.Add((int)(key >> 32));
                }
            }
        }

        foreach (var (position, spec) in gaps) {
            if (spec.Rule == GapRule.Mandatory) {
                forced.Add(position);
            }
        }

        forced.Sort();
        this.forced = [..forced];
    }

    // ── The walk ─────────────────────────────────────────────────────────────────────────────

    /// <remarks>
    ///     ⚠ Outer nodes are planned before inner ones, and a point never overwrites a point but always
    ///     overwrites a <see cref="GapRule.Flat" />. That ordering is the conflict rule: the enclosing
    ///     construct's break point wins over the nested construct's non-point.
    /// </remarks>
    void Walk(SyntaxNode node) {
        if (captured is { Count: > 0 } && node is ExpressionSyntax && captured.Contains(node.Span)) {
            return;
        }

        Plan(node);
        foreach (var child in node.ChildNodes()) {
            Walk(child);
        }
    }

    void Plan(SyntaxNode node) {
        PlanAttributes(node);
        PlanEmbeddedStatement(node, EmbeddedStatementOf(node));
        PlanStackedUsing(node);
        PlanClauseAfterAnEmbeddedStatement(node);
        PlanOnePerLine(node);
        PlanConstraints(node);
        PlanConstraintList(node);

        // ⚠ Before the switch and before the condition's own operators are walked. The walk is
        // pre-order, so the statement is planned first and `PlanOperator` reads what this recorded.
        PlanForcedChopCondition(node);
        PlanJoinAfterADot(node);
        PlanCastBeforeACollection(node);

        switch (node) {
            case EnumDeclarationSyntax enumeration:
                PlanEnum(enumeration);
                return;

            case SwitchExpressionSyntax switchExpression:
                PlanSwitchExpression(switchExpression);
                return;

            // ⚠ `nameof(…)` is laid out as the `typeof` family, not as a call (#507): the oracle never
            // chops its parentheses, keeps every break the author wrote inside them — `nameof(a` /
            // `);`, `nameof(` / `a);` — and puts a kept `)` back on the opener's line, as `typeof(int` /
            // `);` does. Measured on five shapes. Syntactic, as the oracle's formatter is: a method of
            // that name called with one argument reads the same.
            case ArgumentListSyntax { Parent: InvocationExpressionSyntax invocation } when IsNameOf(invocation):
                return;

            case ArgumentListSyntax arguments:
                PlanList(
                    node,
                    arguments.OpenParenToken,
                    arguments.CloseParenToken,
                    arguments.Arguments,
                    arguments.Arguments.GetSeparators(),
                    InvocationKeeps(arguments),
                    options.WrapArgumentsStyle,
                    options.WrapAfterInvocationLpar,
                    options.WrapBeforeInvocationRpar,
                    options.MaxInvocationArgumentsOnLine,
                    wrapBeforeOpen: options.WrapBeforeInvocationLpar
                );
                return;

            case AttributeArgumentListSyntax attributeArguments: {
                PlanList(
                    node,
                    attributeArguments.OpenParenToken,
                    attributeArguments.CloseParenToken,
                    attributeArguments.Arguments,
                    attributeArguments.Arguments.GetSeparators(),
                    options.KeepExistingInvocationParensArrangement,
                    options.WrapArgumentsStyle,
                    options.WrapAfterInvocationLpar,
                    options.WrapBeforeInvocationRpar,
                    options.MaxInvocationArgumentsOnLine,
                    wrapBeforeOpen: options.WrapBeforeInvocationLpar
                );

                return;
            }

            case ParameterListSyntax { Parent: TypeDeclarationSyntax } primaryParameters:
                // ⚠ A primary constructor has its own four keys, and they do not agree with the
                // declaration ones: skala_wrap_before_primary_constructor_declaration_rpar is false where
                // skala_wrap_before_declaration_rpar is true, so `record R(\n int Y,\n int Z);` keeps the
                // closing parenthesis on the last parameter's line.
                PlanList(
                    node,
                    primaryParameters.OpenParenToken,
                    primaryParameters.CloseParenToken,
                    primaryParameters.Parameters,
                    primaryParameters.Parameters.GetSeparators(),
                    options.KeepExistingPrimaryConstructorParensArrangement,
                    options.WrapPrimaryConstructorParametersStyle,
                    options.WrapAfterPrimaryConstructorLpar,
                    options.WrapBeforePrimaryConstructorRpar,
                    options.MaxPrimaryConstructorParametersOnLine,
                    wrapBeforeOpen: options.WrapBeforePrimaryConstructorLpar
                );
                return;

            case ParameterListSyntax parameters:
                PlanDeclarationParameters(
                    node,
                    parameters.OpenParenToken,
                    parameters.CloseParenToken,
                    parameters.Parameters
                );

                // ⚠ A lambda's parameter list with an expression body stays whole while the line through
                // its `=>` fits, and the arrow takes the break instead (#453, SK-DIV-0050): the oracle
                // writes `C2((SomeType first, OtherType second) =>` / `body` / `);` with the `=>` as far
                // out as column 120, and chops the parameters only once `) =>` itself is past the margin.
                if (parameters.Parent is ParenthesizedLambdaExpressionSyntax {
                        ExpressionBody: not null
                    } parenthesized) {
                    ReviseFacts(node, facts => facts with { ThroughWidth = 1 + parenthesized.ArrowToken.Span.Length });
                }

                return;

            // ⚠ An indexer's parameter list had no plan at all (SK-DIV-0108): `int this[int a =\n 5]`
            // kept the break and chopped nothing, and `int this[int a\n, int b]` kept a comma the
            // oracle re-lays. The oracle gives the brackets exactly the declaration keys — measured
            // beside a method twin of every shape: the list chops when an item is multi-line or a
            // delimiter break is kept, `]` takes a line of its own, the arrow after it breaks, and a
            // break before a comma is joined. There is no indexer-specific key in the registry to
            // read instead.
            case BracketedParameterListSyntax indexerParameters:
                PlanDeclarationParameters(
                    node,
                    indexerParameters.OpenBracketToken,
                    indexerParameters.CloseBracketToken,
                    indexerParameters.Parameters
                );

                return;

            // ⚠ An element access's arguments — `grid[i, j]`, and the `[key] = value` of an implicit
            // element access in an initializer — had no plan at all (SK-DIV-0114, issue #371). The
            // oracle chops them like an invocation's arguments: a kept break after a comma or after
            // the `[` chops the list, a break before a comma is joined, a list that overflows the
            // margin chops one per line. But the brackets are not the parentheses: there is no
            // `wrap_after_*_lbracket`, the oracle keeps `grid[\n0` and `1\n]` where it re-lays `F(\n0`
            // under `skala_keep_existing_invocation_parens_arrangement = false`, and it never adds a
            // break at either bracket — `cube[a,\n b,\n c,\n d]`, not `cube[\n a,\n …\n]`. So the
            // delimiters are planned as kept rather than as points, and the items take the argument
            // keys. Measured beside an invocation twin of every shape.
            case BracketedArgumentListSyntax elementArguments:
                PlanList(
                    node,
                    elementArguments.OpenBracketToken,
                    elementArguments.CloseBracketToken,
                    elementArguments.Arguments,
                    elementArguments.Arguments.GetSeparators(),
                    true,
                    options.WrapArgumentsStyle,
                    false,
                    false,
                    options.MaxInvocationArgumentsOnLine
                );

                return;

            case TupleExpressionSyntax tuple:
                PlanFilledList(node, tuple.OpenParenToken, tuple.CloseParenToken, tuple.Arguments);
                return;

            // ⚠ Five more lists the oracle fills exactly as it fills a tuple, and which had no plan at
            // all (SK-DIV-0114, issue #371): a kept break inside any of them was left as written and an
            // overflowing one was never wrapped. See PlanFilledList for the measurements. A tuple *type*
            // is deliberately absent — the oracle never breaks one at its commas.
            case PositionalPatternClauseSyntax positional:
                PlanFilledList(node, positional.OpenParenToken, positional.CloseParenToken, positional.Subpatterns);
                return;

            case ParenthesizedVariableDesignationSyntax designation:
                PlanFilledList(node, designation.OpenParenToken, designation.CloseParenToken, designation.Variables);
                return;

            case ArrayRankSpecifierSyntax rank when HasASize(rank):
                PlanFilledList(node, rank.OpenBracketToken, rank.CloseBracketToken, rank.Sizes);
                return;

            case FunctionPointerParameterListSyntax pointerParameters:
                PlanFilledList(
                    node,
                    pointerParameters.LessThanToken,
                    pointerParameters.GreaterThanToken,
                    pointerParameters.Parameters
                );

                return;

            case FunctionPointerUnmanagedCallingConventionListSyntax conventions:
                PlanFilledList(
                    node,
                    conventions.OpenBracketToken,
                    conventions.CloseBracketToken,
                    conventions.CallingConventions
                );

                return;

            case AttributeListSyntax { Attributes.Count: > 1 } attributes:
                PlanAttributeList(attributes);
                return;

            // ⚠ A parameter's single attribute and the parameter share a line exactly when they fit
            // on one (SK-DIV-0114): `[Obsolete]\n int a` comes back as `[Obsolete] int a`, a type
            // parameter's and a lambda parameter's too; `[Description("…96 columns…")] string? p`
            // — written on one line or two — comes back on two, the arguments whole; and only a
            // section that overflows on its own chops its arguments, with the parameter below.
            // Measured on Skala's own McpServer.cs. So the gap after the `]` is a point of a group
            // that joins if it fits and breaks if it does not, and the arguments in front of it
            // measure through the point when it stays and stop at the `]` when it breaks — see
            // LayoutWriter.AddRemainingSiblings.
            case AttributeListSyntax { Attributes.Count: 1 } single: {
                var after = OwnerTokenAfter(single);
                if (after.IsKind(SyntaxKind.None)) {
                    return;
                }

                var inRun = attributeRuns.TryGetValue(Key(single), out var run);
                var section = inRun ? run.Section : NewGroup();
                FollowingPoint(after, section);
                Describe(
                    node,
                    section,
                    GroupMode.Preserve,
                    new(
                        options.KeepsUserBreaksBetweenItems && BreaksBefore(after),
                        true,
                        true,
                        BreaksWithOwner: inRun,
                        Owner: inRun ? run.Run : -1
                    )
                );

                return;
            }

            case ParameterSyntax { AttributeLists: [_, _, ..] lists }:
                PlanAttributeRun(node, lists);
                return;

            case ParameterSyntax parameter:
                PlanParameterTypeNameGap(parameter);
                return;

            case TypeParameterSyntax { AttributeLists: [_, _, ..] lists }:
                PlanAttributeRun(node, lists);
                return;

            case ForStatementSyntax forStatement:
                PlanForHeader(forStatement);
                return;

            case InitializerExpressionSyntax initializer:
                PlanInitializer(initializer);
                return;

            case AnonymousObjectCreationExpressionSyntax anonymous:
                PlanAnonymousObject(anonymous);
                return;

            case CollectionExpressionSyntax { Elements.Count: 0 } empty:
                CloseAfterAMultiLineComment(empty.OpenBracketToken, empty.CloseBracketToken);
                return;

            case CollectionExpressionSyntax collection:
                PlanList(
                    node,
                    collection.OpenBracketToken,
                    collection.CloseBracketToken,
                    collection.Elements,
                    collection.Elements.GetSeparators(),
                    options.KeepExistingListPatternsArrangement,
                    options.WrapListPattern,
                    true,
                    true,
                    placeOnSingleLine: options.PlaceSimpleListPatternOnSingleLine,
                    keepOutranksChopAlways: true,
                    keepIsTheConstructsAlone: true
                );
                return;

            case ListPatternSyntax listPattern:
                PlanList(
                    node,
                    listPattern.OpenBracketToken,
                    listPattern.CloseBracketToken,
                    listPattern.Patterns,
                    listPattern.Patterns.GetSeparators(),
                    options.KeepExistingListPatternsArrangement,
                    options.WrapListPattern,
                    true,
                    true,
                    placeOnSingleLine: options.PlaceSimpleListPatternOnSingleLine,
                    keepOutranksChopAlways: true,
                    keepIsTheConstructsAlone: true
                );
                return;

            case PropertyPatternClauseSyntax propertyPattern:
                PlanPropertyPattern(propertyPattern);
                return;

            case OrderByClauseSyntax { Orderings.Count: > 1 } orderBy:
                PlanOrderings(orderBy);
                return;

            case SwitchExpressionArmSyntax arm:
                PlanArmArrow(arm);
                return;

            case WhenClauseSyntax whenClause:
                PlanWhenClause(whenClause);
                return;

            case LambdaExpressionSyntax { ExpressionBody: { } lambdaBody } lambda:
                PlanLambdaArrow(lambda, lambdaBody);
                return;

            case SubpatternSyntax subpattern:
                PlanSubpattern(subpattern);
                return;

            case ArgumentSyntax { NameColon: { } argumentName, Expression: { } argumentValue } argument:
                PlanArgumentName(argument, argumentName.ColonToken, argumentValue);
                return;

            case AttributeArgumentSyntax { NameColon: { } attributeName, Expression: { } attributeValue } named:
                PlanArgumentName(named, attributeName.ColonToken, attributeValue);
                return;

            case BaseListSyntax baseList:
                PlanBaseList(baseList);
                return;

            case VariableDeclarationSyntax declaration:
                PlanTypeNameGap(declaration);
                if (declaration.Variables.Count > 1) {
                    PlanDeclarators(declaration);
                }

                return;

            case BinaryExpressionSyntax binary when IsTypeTest(binary):
                PlanTypeTest(binary, binary.OperatorToken, binary.Right);
                return;

            case IsPatternExpressionSyntax isPattern when IsUnbreakablePattern(isPattern.Pattern):
                PlanTypeTest(isPattern, isPattern.IsKeyword, isPattern.Pattern);
                return;

            // ⚠ An `is` the author broke before or after, ahead of a pattern that can break: the `is` line
            // (or the pattern's) takes a level past the operand's own line, as a type test's does
            // (SK-DIV-0206, SK-DIV-0392). Two measurements meet here: group J's (`token.Kind()` / `is A` /
            // `or B` under an expression body at 12, where Skala wrote 8) and group L's (#550: `keyword is`
            // / `A` / `or B`, and property patterns, whose braces nest from the `is`'s line since
            // LayoutWriter.LevelForBlock reads a from-the-line scope as absolute).
            case IsPatternExpressionSyntax isPattern when BreaksAroundTheIs(isPattern):
                PlanBrokenTypeTest(isPattern);
                return;

            // ⚠ Only as a local's value, where it was measured: under a lambda or an argument the gap
            // after `is` is not a point, and planning it there moved Skala's own `child is not A` /
            // `and not B` chains off the oracle's column.
            case IsPatternExpressionSyntax {
                Pattern: BinaryPatternSyntax,
                Parent:
                EqualsValueClauseSyntax {
                    Parent:
                    VariableDeclaratorSyntax {
                        Parent: VariableDeclarationSyntax { Parent: LocalDeclarationStatementSyntax }
                    }
                }
            } chainTest:
                PlanAfterIs(chainTest);
                return;

            case BinaryExpressionSyntax binary:
                if (IsChainRootOperator(binary)) {
                    PlanChainWide(binary, options.WrapChainedBinaryExpressions);
                }

                PlanOperator(binary, binary.OperatorToken, binary.Right, options.WrapBeforeBinaryOpsign);
                return;

            case BinaryPatternSyntax pattern:
                if (IsChainRootOperator(pattern)) {
                    PlanChainWide(pattern, options.WrapChainedBinaryPatterns);
                }

                PlanOperator(pattern, pattern.OperatorToken, pattern.Right, options.WrapBeforeBinaryPatternOp);
                return;

            case InvocationExpressionSyntax or MemberAccessExpressionSyntax or ConditionalAccessExpressionSyntax
                when IsChainRoot(node):
                PlanChainedCalls(node);
                return;

            case ConditionalExpressionSyntax ternary:
                PlanConditional(ternary);
                return;

            case QueryExpressionSyntax query:
                PlanQuery(query);
                return;

            case AssignmentExpressionSyntax assignment:
                PlanAroundEquals(assignment, assignment.OperatorToken, assignment.Right);
                return;

            case EqualsValueClauseSyntax { Value: not null } initializer:
                PlanAroundEquals(initializer, initializer.EqualsToken, initializer.Value);
                return;

            // ⚠ `[LoggerMessage(Message = "…" + "…")]` — a named attribute argument's `=` is neither
            // an assignment nor an equals-value clause, so it had no plan at all and its right-hand
            // side sat at the attribute list's level rather than one continuation in. The oracle
            // treats it like every other `=`.
            case AttributeArgumentSyntax { NameEquals: { } nameEquals, Expression: not null } attributeArgument:
                PlanAroundEquals(attributeArgument, nameEquals.EqualsToken, attributeArgument.Expression);
                return;

            // ⚠ A `using` alias's `=` is the `=` of every other declaration (#467): the oracle breaks
            // after it when the line through the first break point of the type does not fit, and fills
            // the type below — `using L =` / `    Dictionary<…,` / `        …>;` — where Skala, with no
            // plan here, filled the type argument list on the first line. Measured on a generic alias,
            // a tuple alias, an array and a pointer alias, flat and with the author's break kept, from
            // 118 to 124 columns. ⚠ The group's own continuation level is also what puts a kept
            // `using Y =` / `(int A, int B);` one level in: outside a namespace no frame was open to
            // pay for it, and the type came back at column 0.
            case UsingDirectiveSyntax { Alias: { } alias } usingAlias:
                PlanAroundEquals(usingAlias, alias.EqualsToken, usingAlias.NamespaceOrType);
                return;

            case ArrowExpressionClauseSyntax { Expression: not null } arrow:
                PlanExpressionBody(arrow);
                return;


            case SwitchSectionSyntax section:
                PlanCaseStatements(section);
                return;

            // ⚠ Two shapes and not a guard. Until T5a this arm ran only under
            // `skala_wrap_before_type_parameter_langle`, with the note that giving a type parameter list a
            // group unconditionally "would change where a long generic declaration wraps at the
            // export's own values" — which was true, and was the divergence rather than the reason
            // to keep it. At the export's `false` the oracle wraps the list itself; see
            // PlanTypeParameters.
            case TypeParameterListSyntax typeParameters:
                if (options.WrapBeforeTypeParameterLangle) {
                    PlanBreakBefore(typeParameters, typeParameters.LessThanToken);
                } else {
                    PlanTypeParameters(node, typeParameters.Parameters, typeParameters.GreaterThanToken);
                }

                return;

            // ⚠ A type *argument* list had no plan at all (SK-DIV-0114, issue #371), and the oracle
            // lays it out exactly as it lays out the type parameter list: a fill at the commas when it
            // runs past the margin — `Generic<A, B,\n C>()` — a kept break after a comma, after the
            // `<` or before the `>` kept as written with the next argument one level in, a break
            // before a comma kept too, and an arrow after any of them broken. There is no
            // `wrap_before_type_argument_langle`, so the fill is the only arm. Measured on a return
            // type, a local's type, `new Dictionary<…>()` and a generic invocation.
            case TypeArgumentListSyntax typeArguments:
                PlanTypeParameters(node, typeArguments.Arguments, typeArguments.GreaterThanToken);
                return;

            // ⚠ `skala_place_type_constraints_on_same_line = false`: the constraints leave the
            // DECLARATION's line, and that is one break before the first `where` — not one before
            // every `where`, which is what this used to plan. Measured, one key flipped:
            //     class SameLine<T, U>
            //         where T : struct where U : class {     ← the clauses stay together
            //     void M<V>(V v)
            //         where V : notnull { }
            // What separates the clauses from one another is
            // `skala_wrap_multiple_type_parameter_constraints_style` and the author's own breaks, which
            // survive here because this arm plans no gap between them at all: the same file's
            // `class OwnLines<T, U>` keeps the `where`s the author put on separate lines.
            case TypeParameterConstraintClauseSyntax constraint
                when !options.PlaceTypeConstraintsOnSameLine
                && ConstraintsOf(constraint.Parent).FirstOrDefault() == constraint:
                Mandatory(constraint.WhereKeyword);
                return;

            case ConstructorInitializerSyntax initializer when !options.PlaceConstructorInitializerOnSameLine:
                Mandatory(initializer.ColonToken);
                return;

            // ⚠ The break goes before the `:`, not before the base type — the same side the ordinary
            // constructor initializer's arm above takes, and it used to take the other one:
            //     class Primary(int a)        class Primary(int a) :
            //         : Base(a);                  Base(a);
            //     ↑ the oracle                ↑ Skala, breaking after the colon
            // `PrimaryConstructorBaseTypeSyntax` is `Base(a)` and the `:` is its base list's, so the
            // node's own first token is one token too late.
            case PrimaryConstructorBaseTypeSyntax { Parent: BaseListSyntax list }
                when !options.PlacePrimaryConstructorInitializerOnSameLine:
                Mandatory(list.ColonToken);
                return;

            default:
                return;
        }
    }

    // ── Constructs ───────────────────────────────────────────────────────────────────────────

    /// <summary>
    ///     A method's, a local function's, a lambda's or an indexer's parameter list, under the
    ///     declaration keys. See <see cref="DeclarationKeeps" /> for why a lambda's is here too.
    /// </summary>
    void PlanDeclarationParameters(
        SyntaxNode node,
        SyntaxToken open,
        SyntaxToken close,
        SeparatedSyntaxList<ParameterSyntax> parameters
    ) =>
        PlanList(
            node,
            open,
            close,
            parameters,
            parameters.GetSeparators(),
            DeclarationKeeps(),
            options.WrapParametersStyle,
            options.WrapAfterDeclarationLpar,
            options.WrapBeforeDeclarationRpar,
            options.MaxFormalParametersOnLine,
            wrapBeforeOpen: options.WrapBeforeDeclarationLpar
        );

    /// <summary>
    ///     <c>skala_wrap_enum_declaration = chop_always</c> with <c>skala_max_enum_members_on_line = 1</c>: one
    ///     member per line, always, whatever the source did.
    /// </summary>
    /// <remarks>
    ///     ⚠ docs/plan/05 names <c>resharper_new_line_before_enumerators</c> for this. That key is in
    ///     the export template and is <em>not</em> in <c>options.json</c> — the M0 importer dropped it
    ///     along with about forty other C#-relevant unprefixed keys — so the mechanism here is the two
    ///     keys that are registered and that produce the same layout. See the M2 report.
    /// </remarks>
    void PlanEnum(EnumDeclarationSyntax node) {
        if (node.Members.Count == 0) {
            return;
        }

        // ⚠ `skala_keep_existing_enum_arrangement` is the ONLY key of the three that governs this, and the
        // other two are masked rather than partners. Measured, one key flipped at a time over the
        // export: the oracle returns `constructs/breaks/enum-members.cs` with every member on its own
        // line at `skala_wrap_enum_declaration = wrap_if_long`, at `chop_if_long`, at `chop_always`, at
        // `skala_max_enum_members_on_line = 1` and at `= 2` — five configurations, one output — and puts
        // `enum Compact { First, Second, Third, Fourth }` back on its line the moment
        // `skala_keep_existing_enum_arrangement` is true. Reading the wrap style and the counter as the
        // forcing condition made Skala the only engine that varied, which is what SPURIOUS means, on
        // both of their rows at once.
        // ⚠ The rule the oracle is really applying is `resharper_new_line_before_enumerators`, which
        // the remarks above already name: it is in the export template, it is not in options.json,
        // and its value there is the one both engines now produce. Neither registered key stands in
        // for it — an enum whose arrangement is not kept is chopped whatever they say.
        var always = !options.KeepExistingEnumArrangement;
        var group = NewGroup();
        Point(FirstToken(node.Members[0]), group);
        var broken = BreaksBefore(FirstToken(node.Members[0]));

        foreach (var separator in node.Members.GetSeparators()) {
            var next = separator.GetNextToken();
            if (!next.IsKind(SyntaxKind.None) && next.SpanStart < node.CloseBraceToken.SpanStart) {
                Point(next, group);
                broken |= BreaksBefore(next);
                JoinBeforeComma(separator);
            }
        }

        Point(node.CloseBraceToken, group);
        broken |= BreaksBefore(node.CloseBraceToken);

        Describe(
            node,
            group,
            always ? GroupMode.Break : GroupMode.Preserve,
            new(
                options.KeepExistingEnumArrangement && broken,
                BreaksIfTooLong: options.WrapEnumDeclaration == WrapStyle.ChopIfLong
            )
        );
    }

    /// <summary>
    ///     <c>skala_wrap_switch_expression = chop_always</c> and
    ///     <c>skala_place_simple_switch_expression_on_single_line = false</c>: every arm, always.
    /// </summary>
    void PlanSwitchExpression(SwitchExpressionSyntax node) {
        if (node.Arms.Count == 0) {
            return;
        }

        var style = options.WrapSwitchExpression;

        // ⚠ `wrap_if_long` fills the arms; `chop_*` takes them together. Measured, one key flipped,
        // on a switch whose arms cannot share a continuation line:
        //     v switch {
        //         1 => "aaaa…", 2 => "bbbb…", 3 => "cccc…",
        //         4 => "dddd…", _ => "e"
        //     };
        var fill = style == WrapStyle.WrapIfLong;

        var group = NewGroup();
        Point(FirstToken(node.Arms[0]), group);
        var broken = BreaksBefore(FirstToken(node.Arms[0]));

        // ⚠ The arms are the INNER group's and the braces the outer one's, which is the split a
        // braced initializer already has and for the same measured reason. The export sets
        // `skala_place_simple_switch_expression_on_single_line = false`, and at that value the oracle puts
        // the braces on their own lines WHATEVER `skala_wrap_switch_expression` says:
        //     int Compact(int v) =>
        //         v switch {
        //             1 => 10, 2 => 20, _ => 0     ← at wrap_if_long and at chop_if_long
        //         };
        // One group holding both meant the arms' style decided the braces too, so at either of those
        // two values Skala returned the whole declaration flat — and the arrow break went with it,
        // because "the owner is a single line" is answered by whether this construct broke.
        var arms = NewGroup();
        var armsBroken = false;
        foreach (var separator in node.Arms.GetSeparators()) {
            var next = separator.GetNextToken();
            if (!next.IsKind(SyntaxKind.None) && next.SpanStart < node.CloseBraceToken.SpanStart) {
                Point(next, arms, fill);
                armsBroken |= BreaksBefore(next);
                JoinBeforeComma(separator);
            }
        }

        broken |= armsBroken;
        Point(node.CloseBraceToken, group);
        broken |= BreaksBefore(node.CloseBraceToken);

        // ⚠ `skala_keep_existing_switch_expression_arrangement` outranks `chop_always`, which the option
        // names do not suggest and the oracle settles: with it on, `value switch { 1 => 1, _ => 0 }`
        // comes back on one line although the wrap style says every arm gets one of its own. With it
        // off — the export's value — the same expression is chopped.
        //
        // ⚠ `chop_always` outranks `skala_place_simple_switch_expression_on_single_line`, and this used to
        // say the reverse. Measured, one key flipped from the export at a time:
        //
        //   chop_always + place = true    every arm on its own line — the placement key does nothing
        //   wrap_if_long + place = true   `value switch { 1 => 1, _ => 0 }` on one line
        //   wrap_if_long + place = false  the braces open; the arms fill `1 => 1, _ => 0`
        //
        // The old ordering was the whole of the key's `SPURIOUS` row: under the export's own
        // `chop_always` Skala flattened a chopped switch expression onto one line at `true` and the
        // oracle left it chopped.
        //
        // The third line is the braces/arms split above: `forced` opens the braces and `fill` lets
        // the arms flow, so all three rows come out of the same two groups.
        var keep = options.KeepExistingSwitchExpressionArrangement;
        var always = style == WrapStyle.ChopAlways && !keep;

        // ⚠ The braces break when the placement key says so, whatever the arms' style, AND whenever
        // the arms are certain to chop — a switch whose arms each take a line cannot have its braces
        // joined around them. `always` gates the arms; `|| always` here is what stops
        // `skala_place_simple_switch_expression_on_single_line = true` from joining the braces back
        // together under `chop_always`, which is the precedence the row turned on.
        var forced = !keep && (always || !options.PlaceSimpleSwitchExpressionOnSingleLine);

        Describe(
            node,
            group,
            forced ? GroupMode.Break : GroupMode.Preserve,
            new(
                broken,
                options.PlaceSimpleSwitchExpressionOnSingleLine && !keep && !always,
                true
            )
        );

        DescribeInner(
            node,
            arms,

            // ⚠ And the arms re-flow when `skala_keep_existing_switch_expression_arrangement` is off, which
            // is the export's value. Measured at `skala_wrap_switch_expression = chop_if_long`: a switch
            // the author wrote one arm per line comes back with the three arms on one continuation
            // line, byte for byte what the same switch written flat gets at that value. The braces
            // are still apart — that is the placement key's doing and not the author's — so the two
            // halves of "what the source did" belong to the two groups separately, the same split
            // `PlanBracedElements` makes.
            always ? GroupMode.Break : GroupMode.Preserve,
            new(
                keep && armsBroken,
                !keep,
                true
            )
        );
    }

    /// <summary>
    ///     A parenthesised list: <c>wrap_after_*_lpar</c>, <c>wrap_before_*_rpar</c>,
    ///     <c>skala_wrap_before_comma</c>, and a <c>chop_*</c> or <c>wrap_if_long</c> style.
    /// </summary>
    /// <remarks>
    ///     ⚠ The two delimiter points and the inter-item points are preserved by different keys, and
    ///     conflating them makes the <c>keep_existing_*</c> family unobservable. Measured against the
    ///     oracle: with <c>skala_keep_existing_invocation_parens_arrangement = false</c>, <c>Foo1(\n a)</c>
    ///     re-joins and <c>Foo2(\n a,\n b)</c> does not, because the first has no inter-item break to
    ///     keep and the second has one.
    /// </remarks>
    /// <param name="maxOnLine">
    ///     <c>max_*_on_line</c>. ⚠ A hard chop and not a fill: measured against the oracle,
    ///     <c>new List&lt;int&gt; { 1, 2, 3, 4, 5 }</c> comes back with one element per line under
    ///     <c>skala_max_initializer_elements_on_line = 4</c> although it is 41 columns wide, while
    ///     <c>new[] { 1, 2, 3, 4, 5 }</c> — governed by
    ///     <c>
    /// skala_max_array_initializer_elements_on_line =
    ///  10000
    ///     </c>
    ///     — does not move. The counter is not a width and does not consult one.
    /// </param>
    /// <param name="placeOnSingleLine">
    ///     A <c>place_simple_*_on_single_line</c> key, or null where the construct has none.
    /// </param>
    /// <remarks>
    ///     ⚠ The key runs in both directions and the name only suggests one of them. At <c>true</c> it
    ///     joins, overriding <c>keep_user_linebreaks</c>: a four-line
    ///     <c>new Thing\n{\n A = 1,\n B = 2\n}</c> comes back as <c>new Thing { A = 1, B = 2 }</c>. At
    ///     <c>false</c> it <em>forces</em> the delimiters apart however short the construct is —
    ///     <c>xs is [1, 2, 3]</c> becomes three lines. Measured against the oracle, because "place on
    ///     single line = false" reads like permission withheld rather than a break required.
    /// </remarks>
    /// <param name="keepsBreakOnEitherSideOfComma">
    ///     ⚠ Whether a break the author wrote on the side of the comma that
    ///     <c>skala_wrap_before_comma</c> does <em>not</em> choose is kept rather than joined. It is the
    ///     one thing that separates a construct with a wrap style of its own from one the oracle merely
    ///     fills, and it is measured on both sides of the line (SK-DIV-0104). Under
    ///     <c>wrap_before_comma = false</c>, <c>F(1\n, 2)</c>, <c>void M(int a\n, int b)</c>,
    ///     <c>[1\n, 2]</c>, <c>new[] { 1\n, 2 }</c>, <c>new T { X = 1\n, Y = 2 }</c>, a switch
    ///     expression's arms, an enum's members, a declaration's declarators and an attribute's arguments
    ///     all come back joined — the construct's own wrap style re-lays the commas. A tuple
    ///     <c>(1\n, 2)</c> and a type parameter list <c>G&lt;T\n, U&gt;</c> come back exactly as written,
    ///     <c>, 2</c> one continuation level in, and so do the constructs this file does not plan at all —
    ///     a tuple type, a type argument list, a positional pattern — because for those the gap is
    ///     nobody's but <c>keep_user_linebreaks</c>'. The tuple is planned here only so that it
    ///     <em>fills</em> when it does not fit, and <c>Flat</c> on the comma took a line the fill had no
    ///     business taking. A break kept this way counts as a break between items: the owner is
    ///     multi-line, and an arrow above it breaks exactly as it does for <c>(1,\n 2)</c>.
    /// </param>
    int PlanList<T>(
        SyntaxNode node,
        SyntaxToken open,
        SyntaxToken close,
        SeparatedSyntaxList<T> items,
        IEnumerable<SyntaxToken> separators,
        bool keepExisting,
        WrapStyle style,
        bool wrapAfterOpen,
        bool wrapBeforeClose,
        int maxOnLine = int.MaxValue,
        bool? placeOnSingleLine = null,
        bool wrapBeforeOpen = false,
        bool keepOutranksChopAlways = false,
        bool keepsBreakOnEitherSideOfComma = false,
        bool keepIsTheConstructsAlone = false
    )
        where T : SyntaxNode {
        // ⚠ For a collection expression and a list pattern the construct's own keep_existing_* key is
        // the whole answer, and keep_user_linebreaks does not gate it (#443). Measured: at
        // keep_user_linebreaks = false (and keep_existing_linebreaks = false) the oracle still chops
        // `[1,` / `2]`, `[` / `..a]` and `a is [1,` / `2]` and keeps `[` / `1, 2` / `]` exactly as at the
        // defaults, and only skala_keep_existing_list_patterns_arrangement = false joins them — at either
        // value of the global key. The table below holds for the constructs it was measured on.
        var keepsUserBreaks = options.KeepsUserBreaksBetweenItems || keepIsTheConstructsAlone;

        if (open.IsKind(SyntaxKind.None) || close.IsKind(SyntaxKind.None) || items.Count == 0) {
            return -1;
        }

        // ⚠ The counter wins over everything else, joining included: over the cap the construct is
        // chopped whatever its width and whatever the author wrote.
        var overCap = items.Count > maxOnLine;

        // ⚠ And the per-construct `keep_existing_*` key outranks the placement key in both
        // directions: with keep on, neither the join at `true` nor the forced break at `false`
        // happens at all.
        var joins = placeOnSingleLine == true && !keepExisting;
        var forced = placeOnSingleLine == false && !keepExisting;

        // ⚠ `wrap_if_long` is a fill: the delimiters break together with the group and the gaps
        // between items break one at a time, as the line runs out. Milestone 2 declined to plan
        // these constructs at all rather than chop them, which is why an over-long initializer came
        // back untouched.
        // ⚠ And so is a construct the placement key forced apart, at whatever style. Measured, one
        // key flipped: `skala_place_simple_property_pattern_on_single_line = false` with
        // `skala_wrap_property_pattern = chop_if_long` returns
        //     o is Thing {
        //         Alpha: 1, Beta: 2
        //     };
        // and keeps a three-subpattern clause of 98 columns together on its continuation line too.
        // `false` puts the BRACES on their own lines; it does not chop what is between them, and
        // reading it as a chop gave every subpattern a line of its own. It also overrides the
        // author's own break at an item gap in the joining direction — a clause written one
        // subpattern per line comes back re-flowed — which falls out of the fill: the group is
        // broken either way, and each gap then answers for itself.
        var fill = style == WrapStyle.WrapIfLong || forced;

        // ⚠ place_single_method_argument_lambda_on_same_line = true governs the OPENING parenthesis
        // only. `Assert.Throws(() => {` keeps the lambda on the call's line however long its body
        // is — and the oracle still moves the closing parenthesis to a line of its own, so the body
        // gains a continuation level and the call ends `}\n);`. Flattening both sides is the
        // intuitive reading of the option name and it is wrong.
        var soleLambda = items.Count == 1
            && options.PlaceSingleMethodArgumentLambdaOnSameLine
            && IsLambdaArgument(items[0]);

        var group = NewGroup();
        var first = FirstToken(items[0]);
        var delimiterBroken = !soleLambda && BreaksBefore(first) || BreaksBefore(close);

        // ⚠ `wrap_after_X_lpar = false` DOES join a break the author put there — but only where the
        // construct's own `keep_existing_*_arrangement` is not preserving it, and that correction is
        // measured. The reading this replaces was "do not put the first item on a line of its own,
        // and do not join one the author wrote", taken from `record R(\n a,\n b\n)`, which the
        // oracle returns unmoved. It does — and the key holding it there is
        // `skala_keep_existing_declaration_parens_arrangement = true`, not this one. The export sets the
        // invocation half of that pair to FALSE, and there the same flip joins:
        //   skala_wrap_after_invocation_lpar = false    Call(firstArgument,\n    secondArgument\n);
        //   skala_wrap_before_invocation_rpar = false   Call(\n    firstArgument,\n    secondArgument);
        // one gap each, and neither touches the other's. Attributing the declaration's answer to
        // this key made both invocation keys diverge at their non-export value.
        // ⚠ The sole-lambda case below joins regardless, and it says so with its own key.
        // ⚠ `wrap_before_X_lpar = true` gives the opening parenthesis a line of its own, and it is a
        // point of the *list's* group rather than a break of its own: when the list chops, the
        // parenthesis goes with it. Asked directly at a 70-column margin, `void Decl(int a, …)`
        // comes back as `void Decl` / `(` / one parameter per line / `) { }`, so the parenthesis
        // breaks exactly when the parameters do.
        // ⚠ Registered before the gap after the parenthesis, because `_gaps` is keyed by position
        // and the two are different positions — the opening token's own start, and the first item's.
        if (wrapBeforeOpen && !soleLambda) {
            Point(open, group);
        } else if (open.IsKind(SyntaxKind.OpenBracketToken)) {
            // ⚠ An opening *bracket* is not a break point of anybody's, and a break the author wrote
            // in front of one is joined rather than kept. MEASURED on
            // constructs/wrapping/patterns.cs, whose `return xs is\n[\n 1,\n 2\n];` comes back from
            // the oracle as `return xs is [\n 1,\n 2\n];` — the items keep their breaks, which is
            // `skala_keep_existing_list_patterns_arrangement = true`, and the gap before the `[` does not,
            // because that key governs the arrangement *inside* the brackets. Without this the gap
            // falls through to `keep_user_linebreaks = true` and stays broken, and the items then
            // sit a continuation level deeper than the oracle puts them.
            // ⚠ Brackets only. `CSharpDocumentBuilder.ShouldJoin` already does the same for the `{`
            // of a joinable body — a property pattern's among them — and a parenthesis is left
            // alone because `skala_wrap_before_declaration_lpar` and its siblings are real keys that put
            // one on a line of its own.
            // ⚠ And not when the bracket follows an opening parenthesis, because that gap is the
            // parenthesis's and not the bracket's (issue #368). A tuple, a parenthesised expression,
            // an `if (` and a positional pattern all keep a break the author wrote after their `(`
            // under `keep_user_linebreaks`, and the oracle keeps it whether the next token is an
            // identifier or a `[`: `=> (\n[1, 2], 3)` comes back with the break exactly where
            // `=> (\n1, 2)` does, and an invocation's `F(\n[1, 2])` joins for the same reason
            // `F(\nx)` does — `skala_keep_existing_invocation_parens_arrangement = false`, the
            // argument list's own rule on the same gap. Flattening it here was the one place a `[`
            // decided a gap it did not own, and it was not idempotent: the tuple's group was planned
            // as broken from the source, the arrow saw a multi-line body and broke, and the writer
            // then joined the only break that had made it multi-line — so pass two, finding a body
            // that fits, re-joined the arrow. Measured against the oracle on twelve shapes; the
            // conformance corpus has no `(\n[` outside an argument list, which is why the fuzzer
            // found it and the sweep did not.
            // ⚠ And not for an element access's bracket or an attribute list's (SK-DIV-0114). The
            // gap before `grid[0, 1]`'s bracket is the receiver's, and the oracle keeps
            // `grid\n[0, 1]` with the bracket one level in; the gap before an attribute list's `[`
            // is its owner's — a member's, a parameter's — and no list plan owns it.
            // ⚠ Nor a one-line list pattern straight after an `is` (#562, SK-DIV-0396): `xs is` /
            // `[1, 2]` keeps its break, one level past the operand's line, as an expression body, after
            // `return` and `var b =` and as an argument, written indented or flush — it is the list
            // pattern the author broke *inside* that the oracle joins to its `is`. The existing pin of
            // `=> xs is` / `[1, 2]` as joined was not the oracle's answer (re-measured 2026-10-08).
            if (!open.GetPreviousToken().IsKind(SyntaxKind.OpenParenToken)
                && open.Parent is not (BracketedArgumentListSyntax or AttributeListSyntax)
                && !IsAOneLineListPatternAfterIs(open)) {
                Flat(open);
            }
        }

        // ⚠ `!keepsUserBreaks` too: a construct whose keep_existing_* key is true, or which has none and
        // keeps by measurement (PlanFilledList), still re-joins its delimiters at
        // keep_user_linebreaks = false, as the table below says. Measured for #443 on a positional
        // pattern and a tuple: `P(` / `1, 2)`, `(` / `1, 2)` and `P(1, 2` / `)` all come back whole,
        // where the unplanned gap had kept the author's break at both values.
        if (wrapAfterOpen && !soleLambda) {
            Point(first, group);
        } else if (soleLambda || !keepExisting || !keepsUserBreaks) {
            Flat(first);
        }

        // ⚠ A fill re-flows every gap it owns, and one construct family will not have that.
        // `skala_keep_existing_list_patterns_arrangement = true` preserves the author's break at each
        // *individual* item gap, so a collection expression the author wrote one element per line
        // comes back one element per line however well two of them would have shared. Measured, and
        // the distinction is between the two constructs rather than between two widths:
        // <code>
        // static readonly int[] A = [        static readonly int[] A = new[] {
        //     1,                                 1, 2,                    ← re-filled
        //     2,                             };
        // ];                                 ← kept
        // </code>
        // The array initializer has no `keep_existing_*` key of its own and the oracle re-fills it;
        // the list pattern has one and the oracle does not. A per-group flag cannot say this — the
        // preserved gaps and the filled ones are siblings — so the preserved ones become ordinary
        // required breaks and the rest stay fill points.
        var pinsItemBreaks = fill && keepExisting && keepsUserBreaks;

        var interBroken = false;
        foreach (var comma in separators) {
            var next = comma.GetNextToken();
            if (next.IsKind(SyntaxKind.None) || next.SpanStart >= close.SpanStart) {
                continue;
            }

            // skala_wrap_before_comma = false puts the break after the comma, which is the gap before the
            // next item; true puts it before the comma.
            var gap = options.WrapBeforeComma ? comma : next;
            var other = options.WrapBeforeComma ? next : comma;
            interBroken |= PlanItemGap(gap, group, fill, pinsItemBreaks);

            // ⚠ The other side is joined for every construct with a wrap style of its own, and kept
            // where the construct is only filled — see keepsBreakOnEitherSideOfComma.
            interBroken |= PlanOtherSideOfComma(
                other,
                keepsBreakOnEitherSideOfComma && keepsUserBreaks
            );
        }

        if (wrapBeforeClose) {
            Point(close, group);
        } else if (!keepExisting || !keepsUserBreaks) {
            Flat(close);
        }

        // ⚠ An author's break between a comment and the closer is an item break, whatever the
        // construct's keep_existing_* key says about its delimiters (#421). The oracle joins
        // `M(1, 2` / `);` and chops `M(1, 2 /* e */` / `);` — it keeps the line a comment ends, as it
        // does after `// e`. Before #409 the gap was unplanned and its kept break forced the chop;
        // since #409 the point before `)` is planned past the comment (PointSurvivesComments), so the
        // break has to be read here or the group stays flat and joins it.
        interBroken |= BreaksAfterACommentBefore(source, close);

        // ⚠ Two keys, two kinds of gap, and the second is gated by the first. Measured against the
        // oracle in all four corners of docs/plan/05's table (constructs/preservation/*):
        //   keep_user_linebreaks | keep_existing_X | delimiters | between items
        //   true                 | true            | kept       | kept
        //   true                 | false           | re-joined  | kept
        //   false                | true            | re-joined  | re-joined
        //   false                | false           | re-joined  | re-joined
        // The global switch turns the per-construct one off; the per-construct one does not turn the
        // global one on.
        // ⚠ `chop_always` is gated on the construct's own `keep_existing_*_arrangement` — for the
        // constructs where that was measured, and for those alone. `skala_wrap_list_pattern = chop_always`
        // leaves `xs is [1, 2, 3]` on its line, and a 113-column list pattern with it, because
        // `skala_keep_existing_list_patterns_arrangement = true` in this export; the same flip with that
        // key turned OFF chops both. It is the keep key and not the placement key —
        // `skala_place_simple_list_pattern_on_single_line = false` beside `chop_always` still leaves the
        // pattern whole.
        // ⚠ And it is NOT general, which the committed sweep settles without another oracle run:
        // `skala_wrap_parameters_style`, `skala_wrap_primary_constructor_parameters_style` and
        // `skala_wrap_arguments_style` are all conformant with THREE distinct oracle outputs on their
        // fixtures, so those lists do chop at `chop_always` although
        // `skala_keep_existing_declaration_parens_arrangement` and its primary-constructor sibling are
        // true. Applying the gate to every caller made all three of them stop varying.
        var chopsAlways = style == WrapStyle.ChopAlways && !(keepOutranksChopAlways && keepExisting);

        var broken = chopsAlways
            || overCap
            || forced
            || keepsUserBreaks
            && interBroken
            || keepsUserBreaks
            && keepExisting
            && delimiterBroken;

        // ⚠ The per-construct `keep_existing_*` key outranks `place_simple_*_on_single_line`, and the
        // oracle is the only place that says so. With
        // `skala_keep_existing_list_patterns_arrangement = true` — the export's value — a list pattern the
        // author split over three lines stays split, although `skala_place_simple_list_pattern_on_single_line`
        // is also true and would otherwise join it; flipping the keep key to false joins it. Reading
        // the placement key as the stronger of the two makes both of them unobservable at once.
        Describe(
            node,
            group,
            chopsAlways || overCap || forced ? GroupMode.Break : GroupMode.Preserve,
            new(
                broken,
                joins && !overCap,
                true,
                HidesFlatWidthWhenBroken: true
            ),
            // ⚠ The list's node starts *at* its opening parenthesis, so a break point registered on
            // that parenthesis is written before the group is opened and the writer, finding the
            // group unresolved, renders it flat. This is the same correction a base list needs
            // under `skala_wrap_before_extends_colon`; see GroupPlan.LeadingGapInside.
            leadingGapInside: wrapBeforeOpen
        );

        return group;
    }

    /// <summary>
    ///     An initializer's braces: <c>skala_wrap_array_initializer_style = wrap_if_long</c> plus the two
    ///     element counters and <c>skala_place_simple_initializer_on_single_line</c>.
    /// </summary>
    /// <remarks>
    ///     ⚠ Two counters, and which one applies is the syntax kind rather than the option name.
    ///     Measured against the oracle: <c>new List&lt;int&gt; { 1, 2, 3, 4, 5 }</c> comes back with one
    ///     element per line and <c>new[] { 1, 2, 3, 4, 5 }</c> does not, because the first is a
    ///     collection initializer (<c>skala_max_initializer_elements_on_line = 4</c>) and the second an array
    ///     initializer (<c>skala_max_array_initializer_elements_on_line = 10000</c>). Reading
    ///     "array initializer" as "any initializer of a collection" gets both wrong at once.
    /// </remarks>
    void PlanInitializer(InitializerExpressionSyntax node) =>
        PlanBracedElements(
            node,
            node.OpenBraceToken,
            node.CloseBraceToken,
            node.Expressions,
            node.Expressions.GetSeparators(),
            node.IsKind(SyntaxKind.ArrayInitializerExpression)
        );

    void PlanAnonymousObject(AnonymousObjectCreationExpressionSyntax node) =>
        PlanBracedElements(
            node,
            node.OpenBraceToken,
            node.CloseBraceToken,
            node.Initializers,
            node.Initializers.GetSeparators(),
            false
        );

    /// <summary>
    ///     A braced initializer: two groups, because it has three layouts and one group has two.
    /// </summary>
    /// <remarks>
    ///     ⚠ Established against the oracle, and it is not what the option names suggest. The three
    ///     layouts an initializer can take are
    ///     <code>
    /// new Thing { A = 1, B = 2 }                          — everything on the owner's line
    /// new Thing {
    ///     A = 1, B = 2, C = 3                             — braces broken, elements together
    /// }
    /// new Thing {
    ///     A = "…", B = "…", C = "…", D = "…"              — braces broken, one element per line
    /// }                                                     (written one per line)
    ///     </code>
    ///     and the second and third are one group's decision while the first is another's. A single
    ///     group can only offer two of the three, which is why milestone 3's first attempt filled
    ///     <c>Title = "Episode VII", Description = "…", Categories =\n    new List&lt;string&gt; {…}</c>
    ///     where the oracle writes four lines.
    ///     <para>
    ///         ⚠ The inner group is a <em>fill</em> for an array initializer and a chop for an object or
    ///         collection one, and that distinction is real:
    ///         <c>
    /// new[] { six, long, string, literals, here,
    ///  again }
    ///         </c>
    ///         comes back with five on one line and one on the next, while
    ///         <c>new List&lt;string&gt; { four, long, string, literals }</c> comes back with one per line
    ///         even though two of them would have shared. It matches the two counters —
    ///         <c>skala_max_array_initializer_elements_on_line = 10000</c> against
    ///         <c>skala_max_initializer_elements_on_line = 4</c> — being separate keys.
    ///     </para>
    /// </remarks>
    void PlanBracedElements<T>(
        SyntaxNode node,
        SyntaxToken open,
        SyntaxToken close,
        SeparatedSyntaxList<T> items,
        IEnumerable<SyntaxToken> separators,
        bool array
    )
        where T : SyntaxNode {
        if (open.IsKind(SyntaxKind.None) || close.IsKind(SyntaxKind.None)) {
            return;
        }

        if (items.Count == 0) {
            CloseAfterAMultiLineComment(open, close);
            return;
        }

        var style = options.WrapArrayInitializerStyle;
        var cap = array ? options.MaxArrayInitializerElementsOnLine : options.MaxInitializerElementsOnLine;
        var overCap = items.Count > cap;
        var joins = options.PlaceSimpleInitializerOnSingleLine && !overCap;
        var forced = !options.PlaceSimpleInitializerOnSingleLine;

        var outer = NewGroup();
        var first = FirstToken(items[0]);
        var broken = BreaksBefore(first) || BreaksBefore(close);

        // ⚠ Unconditional, and `wrap_after_expression_lbrace` / `wrap_before_expression_rbrace` are
        // not consulted: measured at both values of each and in both spellings, the oracle returns
        // this file byte-identical, while the array initializer's own wrap key rewrites it. See the
        // property-pattern call site and PhaseOneOptions.Ids.
        Point(first, outer);
        Point(close, outer);

        // ⚠ A fill only for an array initializer; an object or collection initializer chops.
        // ⚠ …and never over the cap, which is what "a hard chop and not a fill" means on the
        // `maxOnLine` parameter above. Measured, one key flipped: at
        // `skala_max_array_initializer_elements_on_line = 1` the oracle puts `new[] { 1, 2, 3, 4, 5 }` one
        // element per line, and at `0` it does the same, so the counter is not a width and does not
        // defer to one. The cap already forced the braces apart here; without this it left the five
        // elements filled on the continuation line, so the key moved the output and moved it wrong.
        var fill = array && style == WrapStyle.WrapIfLong && !overCap;
        var inner = NewGroup();
        var interBroken = false;
        foreach (var comma in separators) {
            var next = comma.GetNextToken();
            if (next.IsKind(SyntaxKind.None) || next.SpanStart >= close.SpanStart) {
                continue;
            }

            if (options.WrapBeforeComma) {
                Point(comma, inner, fill);
                Flat(next);
                interBroken |= BreaksBefore(comma);
            } else if (fill && EndsInAMultiLineComment(next)) {
                Flat(comma);
                Mandatory(next);
                interBroken |= BreaksBefore(next);
            } else {
                Flat(comma);
                Point(next, inner, fill);
                interBroken |= BreaksBefore(next);
            }
        }

        broken |= interBroken;

        // ⚠ `chop_always` is the ARRAY initializer's, and an object or collection one does not read
        // it. Measured, one key flipped: at `skala_wrap_array_initializer_style = chop_always` the oracle
        // returns `new List<int> { 1, 2, 3 }`, `new Thing { Alpha = 1, Beta = 2 }` and a
        // three-member `new { … }` exactly as written, and chops only `new[] { … }`. Skala read the
        // one key for every braced initializer and gave all four a line per element.
        // ⚠ The other two values need no such guard, because at neither of them does the style force
        // anything: `wrap_if_long` and `chop_if_long` both leave the mode `Preserve` for a non-array
        // initializer, and what separates them there is `fill`, which is already array-only. The key
        // that governs an object or collection initializer is
        // `csharp_wrap_object_and_collection_initializer_style`, which this registry does not carry;
        // the export's answer is unchanged either way, so this narrows a wrong reading rather than
        // standing in for the missing key.
        // ⚠ `skala_place_simple_initializer_on_single_line = false` forces the BRACES apart and not the
        // elements, which is the outer group and not the inner one. Measured, one key flipped: the
        // oracle returns
        //     var a = new List<int> {
        //         1, 2, 3
        //     };
        // and keeps a three-member `new Thing { … }` of 100 columns together on its continuation
        // line too, chopping only the four-member one that does not fit there. Skala gave every
        // element a line of its own, which reads the key as "and chop it" — the same conflation the
        // outer/inner split exists to prevent. The inner group is left to decide on width, which is
        // what `BreaksIfTooLong` already asks of it.
        var chops = style == WrapStyle.ChopAlways && array || overCap;
        var mode = chops || forced ? GroupMode.Break : GroupMode.Preserve;
        var facts = new GroupFacts(
            options.KeepUserLinebreaks && broken || forced,
            joins,
            true
        );

        // ⚠ A `with` initializer breaks open whenever the expression before `with` spans lines, and an
        // object creation's initializer does not (#487, SK-DIV-0168). Measured: `Make(` / `a,` / `b` /
        // `) with { P = 2 };`, `(a` / `+ b) with { P = 2 };` and `x.F()` / `.G() with { P = 2 };` all come
        // back as `with {` / `P = 2` / `};`, while `new R(` / `a,` / `b` / `) { P = 2 };`,
        // `Make(() => { A(); }) with { P = 2 };` and a `with` that is one line inside a chopped argument
        // list stay whole. It is `if_owner_is_single_line` read off the output, so a list the fitter
        // chopped counts as much as one the author broke.
        var withOwner = node.Parent is WithExpressionSyntax with ? MarkerAt(FirstToken(with)) : -1;
        Describe(
            node,
            outer,
            mode,
            withOwner >= 0 ? facts with { Owner = withOwner, BreaksIfOwnerIsMultiLine = true } : facts
        );
        DescribeInner(
            node,
            inner,
            chops ? GroupMode.Break : GroupMode.Preserve,
            facts with {
                SourceBroken = options.KeepsUserBreaksBetweenItems && interBroken,

                // ⚠ And when the placement key forced the braces apart it re-flows the elements as
                // well, overriding `keep_user_linebreaks` in the joining direction. Measured, one
                // key flipped: at `skala_place_simple_initializer_on_single_line = false` a `new Thing`
                // the author wrote one member per line comes back as
                //     var c = new Thing {
                //         Alpha = 1, Beta = 2
                //     };
                // — byte for byte what the same initializer unbroken in the source gets at that
                // value — while the four-member one that does not fit on its continuation line is
                // still chopped. `false` has already decided this construct's shape, so the
                // author's arrangement inside it no longer governs; that is the same direction the
                // outer group's own `SourceBroken: … || forced` already reads the key in.
                JoinsIfFits = joins || forced,

                // ⚠ Read by the writer alone, and no fact of the inner group consults it otherwise: the
                // braces' group is where the line the first element starts on is recorded, because the
                // point in front of that element is the braces' and not the fill's (#444, SK-DIV-0208).
                Owner = array ? outer : -1
            }
        );
    }

    /// <summary>
    ///     <c>skala_wrap_extends_list_style = chop_if_long</c>: a long base list puts one base type per line.
    /// </summary>
    /// <remarks>
    ///     ⚠ Neither delimiter is a break point. <c>skala_wrap_before_extends_colon = false</c> keeps the
    ///     <c>:</c> and the first base type on the declaration's line, and there is no closing delimiter
    ///     to move, so the only points are the commas:
    ///     <code>
    /// class C : Base,
    ///     IFirst,
    ///     ISecond {
    ///     </code>
    ///     The list therefore opens its own continuation scope rather than living inside a delimiter's.
    ///     <para>
    ///         ⚠ With <em>one</em> base type there is no comma either, and a declaration that overflows
    ///         has to break somewhere: the gap after the colon is the point, and it is planned only in
    ///         that case. See the comment on the branch.
    ///     </para>
    ///     <para>
    ///         ⚠ <b>Two groups, and the split is the oracle's rather than a convenience</b> — the same
    ///         shape <see cref="ConstraintRun" /> records for a run of <c>where</c> clauses, and reached
    ///         here by the same measurement. At <c>skala_wrap_before_extends_colon = true</c> the oracle breaks
    ///         at the <c>:</c> and then <em>stops</em>, although <c>skala_wrap_extends_list_style</c> is
    ///         <c>chop_if_long</c> and the list is what did not fit:
    ///         <code>
    /// class LongBaseClassNameHereOkAndMore
    ///     : SomeVeryLongBaseClassNameIndeed, IFirstInterfaceName, ISecondInterfaceName, IThirdName { }
    ///         </code>
    ///         One group holding both the colon and the commas cannot write that — a group resolved
    ///         Broken breaks every point it owns, so Skala chopped all three commas of a list that fits
    ///         on the line the colon break created. The outer group is entered before that gap and asks
    ///         whether the whole list fits where the declaration reached; the inner one is entered after
    ///         it, at the column the first base type actually lands on, and asks whether the types fit
    ///         there. <see cref="GroupPlan.LeadingGapInside" /> is per plan for this.
    ///     </para>
    ///     <para>
    ///         ⚠ <c>wrap_if_long</c> is a <em>fill</em> and not "never wrap". Measured, one key flipped:
    ///         the oracle returns
    ///         <code>
    /// class LongBaseClassNameHereOkAndMore : SomeVeryLongBaseClassNameIndeed, IFirstInterfaceName, ISecondInterfaceName,
    ///     IThirdName { }
    ///         </code>
    ///         — the last comma that still fits, exactly as <see cref="PlanList" /> already fills a
    ///         delimited list. Until this was measured the style reached the group as
    ///         <c>BreaksIfTooLong: style != WrapIfLong</c>, which left the declaration flat past the
    ///         margin. The same misreading was in the chain and the base list alike; see
    ///         <see cref="PlanChainWide" />.
    ///     </para>
    ///     <para>
    ///         ⚠ <c>chop_always</c> is the <em>inner</em> group's mode and never the outer one's, which
    ///         is also measured: <c>class ShortBase : IFirstInterfaceName { }</c> comes back untouched at
    ///         <c>chop_always</c>. A single-base-type list has no comma to chop, and the point after the
    ///         colon exists only so that a declaration too wide for the margin has somewhere to break —
    ///         spending it on a style is how a 41-column declaration acquired a continuation line.
    ///     </para>
    /// </remarks>
    void PlanBaseList(BaseListSyntax node) {
        if (node.Types.Count == 0) {
            return;
        }

        var style = options.WrapExtendsListStyle;
        var outer = NewGroup();
        var broken = false;

        // ⚠ A primary constructor's base type with arguments is an initializer, not a base type, to the
        // oracle (#427, SK-DIV-0197): its point is before the `:`, as a constructor initializer's is, and
        // it is the `=`'s ordering rule. See the branch.
        // ⚠ At `skala_wrap_before_extends_colon = true` too, and there it skips the first question
        // (#502, SK-DIV-0198): the oracle keeps `: B(` and chops the arguments of a list that would
        // fit whole on the continuation line, where `false` breaks before the `:`. Measured from 121
        // columns up on five shapes at two depths; the break before the `:` is taken only when the
        // head up to `B(` does not fit, or by the interfaces' question below.
        // ⚠ And not only a base type with arguments: the base list of any type with a parameter list,
        // `()` included, measured on a class, a record and a `record struct` whose first base type has
        // no arguments, is generic, or is an interface (SK-DIV-0198). `class X(int a)` / `    : IFirst,
        // ISecond { }` is the oracle's answer where Skala broke after the colon or chopped the commas.
        var primaryBase = node.Parent is TypeDeclarationSyntax { ParameterList: not null };
        var initializer = !options.WrapBeforeExtendsColon && primaryBase;

        // ⚠ `skala_wrap_before_extends_colon = true` makes the `:` itself a break point, which is the only
        // way a base list with a single base type can wrap at all. At `false` — the export's value —
        // the gap is left unplanned rather than marked flat: a `false` placement key is permissive
        // and does not remove a break the author wrote, which is the correction docs/plan/05 records
        // for the whole `place_*_on_same_line` family.
        if (options.WrapBeforeExtendsColon) {
            Point(node.ColonToken, outer);
            broken |= BreaksBefore(node.ColonToken);
        } else if (initializer) {
            // ⚠ Measured with `jb cleanupcode` 2025.2.6, at `skala_wrap_before_extends_colon = false`, on a
            // class, a record and a struct, with one base type and with interfaces after it, at two
            // indent depths. The oracle breaks BEFORE the colon — `class C(int a, …)` /
            // `    : Base(a, …) { }` — exactly when the list then fits on the continuation line, and
            // otherwise keeps `: Base(` on the declaration's line and chops the arguments, which is
            // what an argument list chopped by a comment or an author's break always gets:
            // `class L(int a, int b) : B(` / `    a,` / `    b // e` / `) { }`. Skala broke after the
            // colon, as for an ordinary single base type, and so moved a chopped `B(` a level in.
            Point(node.ColonToken, outer);
            broken |= BreaksBefore(node.ColonToken);
        } else if (node.Types.Count == 1) {
            // ⚠ The gap *after* the colon, and only when there is no comma to carry the break. A
            // single-base-type declaration that overflows is wrapped by the oracle —
            //   class ASingleBaseTypeButAVeryLongDeclarationIndeed :
            //       SomeVeryLongBaseClassNameIndeed { }
            // — and until 06caa62f Skala planned nothing here and let the line run past the margin.
            // ⚠ Deliberately restricted to a one-type list rather than made a general point before
            // the first base type: MEASURED, on the same fixture, the oracle leaves the first base
            // type on the declaration's line when the list has commas and breaks at those instead,
            //   class LongBaseClassNameHereOkAndMore : SomeVeryLongBaseClassNameIndeed,
            //       IFirstInterfaceName,
            // so a point here in the multi-type case would chop with the commas and move a line the
            // oracle does not. The colon's own gap stays unplanned for the reason above.
            Point(node.Types[0].GetFirstToken(), outer);
            broken |= BreaksBefore(node.Types[0].GetFirstToken());
        }

        // ⚠ With interfaces after a primary constructor's base type, the oracle's questions end at the
        // list's first comma and read the base type's argument list as no place to break (#501,
        // SK-DIV-0198): `class M3(…)` / `    : B(a, b),` / `        IFirst,` where Skala kept `: B(` and
        // chopped the arguments. See GroupFacts.TailEndsAt.
        var inner = node.Types.SeparatorCount > 0 ? NewGroup() : -1;
        Describe(
            node,
            outer,
            GroupMode.Preserve,
            new(
                options.KeepsUserBreaksBetweenItems && broken,
                BreaksIfTooLong: true,
                MeasuresHead: primaryBase,
                PrefersOuterBreak: primaryBase,
                TailEndsAt: primaryBase ? inner : -1,
                SkipsOuterTail: primaryBase && options.WrapBeforeExtendsColon,

                // ⚠ One base type and nothing after it: measured one column at a time on five shapes, the
                // oracle breaks before the `:` up to a continuation line of 88 columns (nested two deep),
                // 89 (top level), 88 with a second argument, 86 with a one-letter base and 83 behind a
                // 32-column longer head (SK-DIV-0198): a margin of 30 to 36, the tail counting the colon's
                // own space, where the fitted one is 14 and went on to 105. 31 is exact on two, and one,
                // two and five columns lenient on the others. With interfaces after it the whole-list
                // question keeps the fitted margin, which matched there.
                OuterMargin: primaryBase && inner < 0 ? SingleBaseTypeMargin : 0
            ),
            true,

            // ⚠ Always, and not only when the gap is this group's point: an author's break before the
            // `:` that `keep_user_linebreaks` keeps is written inside the group too, so that the commas'
            // scope opens on the colon's line and spends its level there (#503) — `class C` / `    : I1,`
            // / `        I2`, as the oracle writes it.
            true
        );

        if (inner < 0) {
            return;
        }

        // ⚠ `wrap_if_long` fills the commas one at a time; `chop_*` takes them together.
        var fill = style == WrapStyle.WrapIfLong;
        var innerBroken = false;
        foreach (var comma in node.Types.GetSeparators()) {
            var next = comma.GetNextToken();
            if (next.IsKind(SyntaxKind.None)) {
                continue;
            }

            // ⚠ `skala_wrap_before_comma`, the general key, and NOT
            // `wrap_before_comma_in_base_clause`. Measured, one key at a time on this fixture: the
            // base-clause-specific spelling moves nothing at either value — neither the unprefixed
            // one the export writes nor a `csharp_`-prefixed one — while
            // `skala_wrap_before_comma = true` returns
            //     class C : Base
            //         , IFirst
            //         , ISecond { }
            // so the base clause's comma side IS governed, by the key that governs every other
            // comma. Skala read the dead key and was the only engine that varied.
            if (options.WrapBeforeComma) {
                Point(comma, inner, fill);
                Flat(next);
                innerBroken |= BreaksBefore(comma);
            } else {
                Flat(comma);
                Point(next, inner, fill);
                innerBroken |= BreaksBefore(next);
            }
        }

        Describe(
            node,
            new(
                inner,
                style == WrapStyle.ChopAlways ? GroupMode.Break : GroupMode.Preserve,
                new(
                    options.KeepsUserBreaksBetweenItems && innerBroken,
                    BreaksIfTooLong: true,

                    // ⚠ A base type whose arguments chop on the declaration's line nests them from the
                    // list's continuation line once the commas break: `class C(int a) : B(` / two levels
                    // in / `    ),` / `    I1,` (#427, SK-DIV-0197) — #418's lift, measured at two
                    // depths and under `chop_always`. Not for a fill, for #418's reason.
                    Continues: !fill
                ),
                true,

                // ⚠ A level of its own on top of the outer group's, and the writer's one level per
                // opening line decides whether it counts (#503, SK-DIV-0198). On the declaration's line
                // the two collapse into one: `class C : Base,` / `    IFirst`. After a break before the
                // `:` the commas' scope opens on the colon's line, and the oracle puts the types one level
                // past it — `class C` / `    : IFirst,` / `        ISecond` — at every value of
                // `skala_wrap_before_extends_colon` and of
                // `skala_place_primary_constructor_initializer_on_same_line`, the author's break kept or
                // the fitter's taken alike; Skala put them on the colon's column.
                SpendsUnderDelimiters: true
            )
        );
    }

    /// <summary>The first question's margin for a primary constructor's lone base type (SK-DIV-0198).</summary>
    const int SingleBaseTypeMargin = 31;

    /// <summary>
    ///     A tuple's components, <c>(A: 1, B: 2,\n C: 3)</c> — and every other delimited list the oracle
    ///     fills without a wrap-style key: a positional pattern's, a deconstruction designation's, an
    ///     array rank's, a function pointer's parameters and its calling conventions, and an attribute
    ///     list's attributes (SK-DIV-0114).
    /// </summary>
    /// <remarks>
    ///     ⚠ The delimited constructs in this file with no wrap-style key of their own, and that is
    ///     measured rather than assumed. The oracle <em>fills</em> a tuple that does not fit — the
    ///     components run to the margin and the rest go to the next line — and
    ///     <c>skala_wrap_arguments_style = chop_always</c> does not change it, so the style is
    ///     <see cref="WrapStyle.WrapIfLong" /> unconditionally rather than borrowed from the argument
    ///     list's key:
    ///     <code>
    /// var tuple = (FirstComponentName: 1, SecondComponentName: 2, AThirdComponentName: 3, FourthName: 4,
    ///     FifthComponentName: 5);
    ///     </code>
    ///     ⚠ Neither delimiter is a break point either: a tuple too wide even for the continuation line
    ///     keeps <c>(</c> on the first line and <c>)</c> on the last. <c>skala_wrap_before_comma</c> does apply
    ///     — at <c>true</c> the oracle writes <c>…: 3\n, FourthName: 4</c> — which is why the gap is
    ///     chosen by the same key here as everywhere else.
    ///     <para>
    ///         <c>skala_align_tuple_components</c> then decides which column the continuation lands on;
    ///         <see cref="CSharpDocumentBuilder.VisitDelimited" /> opens that scope. Until this plan existed
    ///         there was no break for it to govern.
    ///     </para>
    ///     <para>
    ///         ⚠ A tuple <em>type</em> is not planned. Asked with one too wide for its line the oracle does
    ///         not break it at its commas at either value of the alignment key — it breaks between an
    ///         element's type and its name — so a plan here would pin a line the oracle does not write.
    ///         Re-measured for SK-DIV-0114 on a return type, a local's type and a type argument: the same,
    ///         and a break the author wrote inside one is kept on both sides of the comma and at both
    ///         parentheses, which <c>keep_user_linebreaks</c> already does without a plan.
    ///     </para>
    ///     <para>
    ///         ⚠ The five other kinds routed here were measured one by one (issue #371), each on a kept
    ///         break after a comma, before a comma, after the opening delimiter, before the closing one,
    ///         and on a list past the margin: every kept break comes back as written with the next item
    ///         one level in, and the overflowing list fills at its commas —
    ///         <c>o is (A, B, C,\n D)</c>, <c>var (a, b, c,\n d) = …</c>, <c>new int[a + b,\n c + d]</c>,
    ///         <c>delegate*&lt;A, B,\n C, void&gt;</c>, <c>unmanaged[Cdecl, …,\n SuppressGCTransition]</c>.
    ///         An array rank whose sizes are all omitted (<c>int[,]</c>) has nothing to fill and is left
    ///         to <c>keep_user_linebreaks</c>.
    ///     </para>
    ///     <para>
    ///         ⚠ It fills like a list pattern rather than like an array initializer, which is the
    ///         <c>keepExisting</c> argument and is measured. The two differ on what happens to a break the
    ///         author already wrote: the oracle re-fills an array initializer's and leaves a tuple's where
    ///         it is, even when the whole tuple would fit on one line. It still fills the <em>tail</em> —
    ///         a tuple broken once by hand whose remainder runs past the margin gains a second break — so
    ///         "kept" is per gap and not per construct, which is exactly what
    ///         <c>pinsItemBreaks</c> expresses. There is no <c>keep_existing_*</c> key for a tuple to read
    ///         it off; the argument is the oracle's answer written down.
    ///     </para>
    ///     <para>
    ///         ⚠ And a break the author wrote <em>before</em> a comma is kept too, under
    ///         <c>skala_wrap_before_comma = false</c>, where an argument list's is joined. Same reason: the
    ///         tuple has no wrap style re-laying its commas, so both sides of every comma are the author's.
    ///         Measured on <c>(1\n, 2)</c>, <c>(a: 1\n, b: 2)</c>, a nested <c>(1\n, (2\n, 3))</c> and a
    ///         tuple broken on both sides of one comma, which comes back with the comma on a line of its
    ///         own; the same inputs with <c>F(</c> for <c>(</c> all come back joined (SK-DIV-0104).
    ///     </para>
    /// </remarks>
    int PlanFilledList<T>(SyntaxNode node, SyntaxToken open, SyntaxToken close, SeparatedSyntaxList<T> items)
        where T : SyntaxNode =>
        PlanList(
            node,
            open,
            close,
            items,
            items.GetSeparators(),
            true,
            WrapStyle.WrapIfLong,
            false,
            false,
            keepsBreakOnEitherSideOfComma: true
        );

    /// <summary>A rank with at least one written size: <c>[1, 2]</c> and not <c>[,]</c>.</summary>
    static bool HasASize(ArrayRankSpecifierSyntax rank) =>
        rank.Sizes.Any(static size => size is not OmittedArraySizeExpressionSyntax);

    /// <summary>
    ///     Several attributes in one section, <c>[A, B,\n C]</c>: a fill, and an owner that leaves the
    ///     section's line once the section spans lines.
    /// </summary>
    /// <remarks>
    ///     ⚠ Had no plan at all (SK-DIV-0114, issue #371). Measured on a method, a type, a field, a
    ///     property, an accessor, a <c>return:</c> target, a parameter, a type parameter and a lambda's
    ///     parameter: a kept break after a comma, before one, after the <c>[</c> or before the <c>]</c>
    ///     comes back as written, a section past the margin fills at its commas, and the attributes
    ///     after the first line up under the <em>first attribute</em> — <c>[Obsolete,\n Serializable]</c>
    ///     with <c>Serializable</c> one column past the bracket, <c>[return: Obsolete,\n CLSCompliant]</c>
    ///     with it under <c>Obsolete</c>. That column is
    ///     <see cref="CSharpDocumentBuilder.AlignsFromOwnColumn" />'s, without a key: no
    ///     <c>align_*</c> option in the export changes it.
    ///     <para>
    ///         ⚠ A parameter or a type parameter leaves the section's line exactly when the section
    ///         spans lines — kept or filled, the same — and stays on it otherwise, joining even a break the
    ///         author wrote after the <c>]</c>: <c>[Obsolete, Serializable]\n int a</c> comes back on one
    ///         line, <c>[Obsolete,\n Serializable] int a</c> with <c>int a</c> below the <c>]</c>. So the
    ///         gap after the <c>]</c> is a point of the section's own group: flat with the group, broken
    ///         with it. A member's gap belongs to <see cref="PlanAttributes" /> and its placement key,
    ///         registered first, and a point never overrides one.
    ///     </para>
    /// </remarks>
    void PlanAttributeList(AttributeListSyntax node) {
        var group = PlanFilledList(node, node.OpenBracketToken, node.CloseBracketToken, node.Attributes);
        if (group >= 0) {
            FollowingPoint(OwnerTokenAfter(node), group);
        }
    }

    /// <summary>
    ///     A parameter's two or more attribute sections: one line together, or every section and the
    ///     parameter on lines of their own.
    /// </summary>
    /// <remarks>
    ///     ⚠ Measured with <c>jb cleanupcode</c> 2025.2.6 (#475, SK-DIV-0350) on a method's, a lambda's and a
    ///     type's parameter, with two and three sections, the multi-line one first, in the middle or last,
    ///     by an author's break or by width, one of them holding two attributes: once a section spans lines,
    ///     or the sections do not fit on one line together, every gap after a section breaks —
    ///     <c>[Obsolete]</c> / <c>[Description(</c> / … / <c>)]</c> / <c>int a</c>, never
    ///     <c>[Obsolete] [Description(</c> or <c>)]</c> / <c>[Obsolete] int a</c>, which is what Skala wrote,
    ///     each section deciding its own gap. Two sections that fit together stay together and the gap
    ///     before the parameter keeps its own rule (<c>[Obsolete] [Serializable]</c> / a long
    ///     <c>Dictionary&lt;…&gt; p</c>), and an author's break between two sections that fit is joined.
    ///     The gaps between sections are the run's points; the gap after the last is its section's, which
    ///     breaks with the run. A run whose last section holds several attributes is left to the
    ///     sections, as before: its gap is the fill's and was not measured here.
    /// </remarks>
    void PlanAttributeRun(SyntaxNode owner, SyntaxList<AttributeListSyntax> sections) {
        var last = sections[^1];
        if (last.Attributes.Count != 1 || OwnerTokenAfter(last).IsKind(SyntaxKind.None)) {
            return;
        }

        var run = NewGroup();
        for (var i = 1; i < sections.Count; i++) {
            Point(sections[i].OpenBracketToken, run);
        }

        var section = NewGroup();
        attributeRuns[Key(last)] = (section, run);
        Describe(
            owner,
            run,
            GroupMode.Preserve,
            new(JoinsIfFits: true, BreaksIfTooLong: true, TailEndsAt: section, MeasuresThroughTail: true)
        );
    }

    /// <summary>
    ///     The token after an attribute section whose gap the section's own layout decides — a
    ///     parameter's or a type parameter's — or none where <see cref="PlanAttributes" /> and a
    ///     placement key own it: a member's, an accessor's, a statement's, a primary constructor's.
    /// </summary>
    static SyntaxToken OwnerTokenAfter(AttributeListSyntax section) =>
        section.Parent is ParameterSyntax { Parent.Parent: not TypeDeclarationSyntax } or TypeParameterSyntax
            ? section.CloseBracketToken.GetNextToken()
            : default;

    /// <summary>
    ///     <c>skala_wrap_for_stmt_header_style = chop_if_long</c>: a <c>for</c> header that does not fit puts
    ///     the initializer, the condition and the incrementor each on a line of its own.
    /// </summary>
    /// <remarks>
    ///     ⚠ The break is after the <c>;</c> and there is no key that moves it to the other side — the
    ///     <c>skala_wrap_before_comma</c> family has no member for a semicolon. Measured at the export's
    ///     120-column margin, one key flipped at a time:
    ///     <code>
    /// chop_if_long                              wrap_if_long
    /// for (var i = 0;                           for (var i = 0; i &lt; xs.Count;
    ///      i &lt; xs.Count;                             i += 1) {
    ///      i += 1) {
    ///     </code>
    ///     <para>
    ///         ⚠ The group is an <em>inner</em> one, opened inside the header's parentheses rather than
    ///         around the statement. A group around the statement is the whole <c>for</c>, body included,
    ///         so its flat width is unbounded and it would break every time however short the header is.
    ///         See <see cref="inner" />; <see cref="CSharpDocumentBuilder.VisitEmbedded" /> opens it, just
    ///         inside the scope <c>skala_align_multiline_statement_conditions</c> already puts on the <c>(</c>'s
    ///         column — which is the column the oracle writes the clauses on, at both values of
    ///         <c>align_multiline_for_stmt</c>.
    ///     </para>
    ///     <para>
    ///         ⚠ An empty clause is not a point — a break there would be a line holding nothing — but an
    ///         empty clause beside a full one still is: <c>for (;</c> on a line of its own is what the
    ///         oracle writes for <c>for (; cond;)</c> once the header is multiline, so the point is
    ///         before each clause that exists rather than after each semicolon.
    ///     </para>
    ///     <para>
    ///         ⚠ "Multiline" means <em>any</em> break inside the parentheses and not only one at a
    ///         <c>;</c>, which is the half of the rule <c>corpus/real/</c> had to supply. A header the
    ///         author broke inside its <em>condition</em> comes back from <c>chop_if_long</c> with the
    ///         semicolons broken as well:
    ///         <code>
    /// for (; ((a != b)              for (;
    ///         &amp;&amp; (c == d));)      →         ((a != b)
    ///                                       &amp;&amp; (c == d));)
    ///         </code>
    ///         and from <c>wrap_if_long</c> unchanged. A group whose flat width is measured statically
    ///         cannot see that break — the condition's own break point is soft and its flat form is
    ///         narrow — so the header reads the source for it.
    ///     </para>
    /// </remarks>
    void PlanForHeader(ForStatementSyntax node) {
        if (node.OpenParenToken.IsKind(SyntaxKind.None) || node.CloseParenToken.IsKind(SyntaxKind.None)) {
            return;
        }

        var clauses = new List<SyntaxToken>(2);
        if (node.Condition is { } condition) {
            clauses.Add(FirstToken(condition));
        }

        if (node.Incrementors.Count > 0) {
            clauses.Add(FirstToken(node.Incrementors[0]));
        }

        if (clauses.Count == 0) {
            return;
        }

        var style = options.WrapForStmtHeaderStyle;
        var fill = style == WrapStyle.WrapIfLong;

        // ⚠ A fill keeps the author's own breaks gap by gap; a chop takes all of them together. Both
        // halves are the oracle's, on one header the author broke at a single `;` and that fits:
        //   wrap_if_long   for (var i = 0;\n     i < 10; i++)    ← only the gap the author broke
        //   chop_if_long   for (var i = 0;\n     i < 10;\n     i++)
        // "Chop if long *or multiline*" is what the value means, and it is why a fill's preserved
        // gaps become ordinary required breaks rather than points — the same correction PlanList's
        // `pinsItemBreaks` makes for a list pattern.
        var pinsClauseBreaks = fill && options.KeepsUserBreaksBetweenItems;
        var group = NewGroup();

        // ⚠ Any break inside the parentheses, not only one at a `;`: `chop_if_long` reads a header the
        // author broke inside its condition as multiline and chops the semicolons too.
        // ⚠ Any break that *survives*. The clauses inside are planned after the header, and some of
        // them re-join what the author wrote — a break before a declarator's comma, after a binary
        // operator, inside an invocation's parentheses — so the answer here is provisional and
        // SettleForHeaders replaces it once the gap table is complete (SK-DIV-0111).
        var broken = Holds('\n', node.OpenParenToken.Span.End, node.CloseParenToken.SpanStart);
        forHeaders.Add(node);

        Flat(node.FirstSemicolonToken);
        Flat(node.SecondSemicolonToken);
        foreach (var clause in clauses) {
            if (pinsClauseBreaks && BreaksBefore(clause)) {
                Mandatory(clause);
            } else {
                Point(clause, group, fill);
            }
        }

        DescribeInner(
            node,
            group,
            style == WrapStyle.ChopAlways ? GroupMode.Break : GroupMode.Preserve,
            new(
                options.KeepsUserBreaksBetweenItems && broken,
                BreaksIfTooLong: true
            )
        );
    }

    /// <summary>
    ///     A type parameter list at <c>skala_wrap_before_type_parameter_langle = false</c>, and a type
    ///     argument list always: a fill inside the angle brackets.
    /// </summary>
    /// <remarks>
    ///     ⚠ A fill and not a chop, and no key selects between them — there is no
    ///     <c>wrap_type_parameters_style</c> in the export or in ReSharper. The shape is the oracle's,
    ///     asked at 120 columns on a list wider than the margin:
    ///     <code>
    /// public void WiderThanTheMargin&lt;TFirstParameterName, TSecondParameterName, TThirdParameterName,
    ///     TFourthParameterName&gt;() { }                          ← at the last comma that fits
    ///     </code>
    ///     The gap after the <c>&lt;</c> is a fill point like every gap between parameters: it breaks
    ///     when what follows it does not fit and not merely because the list is being wrapped, which is
    ///     what keeps the first parameter on the declaration's line above and what puts it on its own
    ///     line when one parameter alone runs past the margin. The closing <c>&gt;</c> is no point of
    ///     the group — the oracle never gives it a line of its own — but a break the author wrote before
    ///     it is kept (SK-DIV-0114), so the gap is left to <c>keep_user_linebreaks</c>.
    ///     <para>
    ///         ⚠ It spends no continuation level, and that is deliberate rather than an omission: the level
    ///         is the angle brackets', opened by <see cref="CSharpDocumentBuilder.VisitDelimited" /> inside
    ///         the <c>&lt;</c>.
    ///     </para>
    ///     <para>
    ///         ⚠ Armed by the <em>list's</em> own width, so it wraps a list that runs past the margin and not
    ///         a declaration that does. ReSharper wraps both — given
    ///         <c>void ManyParams&lt;T1, …, T5&gt;(int a) { }</c> whose list ends at column 116 and whose
    ///         line ends at 131, the oracle moves <c>T5</c> down rather than chopping <c>(int a)</c> — and
    ///         Skala chops the parameter list instead. Arming it by the declaration's head, which is the
    ///         obvious fix and was measured, reproduces that shape and loses a worse one: ReSharper does
    ///         <em>not</em> wrap <c>&lt;T0, T1, T2&gt;</c> when what overflows is a four-parameter list after
    ///         it, and Skala then does. Over <c>corpus/real/</c> that trade is −0.14 points of line fidelity
    ///         (99.53 % → 99.39 %; adding <see cref="GroupFacts.PrefersOuterBreak" /> recovers it only to
    ///         99.50 %), against 0.00 for arming by the list. Which of two constructs on one declaration
    ///         ReSharper wraps is the ordering rule's question and no fact this fitter has answers it; the
    ///         narrower arming is the one that costs nothing while the answer is unknown.
    ///     </para>
    /// </remarks>
    void PlanTypeParameters<T>(SyntaxNode node, SeparatedSyntaxList<T> items, SyntaxToken close)
        where T : SyntaxNode {
        if (items.Count == 0 || close.IsKind(SyntaxKind.None)) {
            return;
        }

        var group = NewGroup();
        var first = FirstToken(items[0]);
        var keeps = options.KeepsUserBreaksBetweenItems;

        // ⚠ A type argument list's points yield to what precedes the list (SK-DIV-0114): everything
        // *around* the list wraps first, and the list fills only when what follows still has no
        // room. Measured against a tuple, which the oracle fills *instead* of breaking at the `=` in
        // front of it (`var t = (a, b,\n c);`) — the type argument list is the other way round:
        // `var created = new Dictionary<A, B>();` at 121 columns comes back as
        // `var created =\n new Dictionary<A, B>();`, and only a list that still overflows on the
        // continuation line is filled there (`var both =\n new Dictionary<A, B,\n C>();`).
        // An ordinary fill point ends the `=`'s "does the line end here anyway?" measure at the `<`,
        // so the `=` stayed and the list filled on the first line. A type *parameter* list has no
        // `=` before it and keeps its ordinary points — its competitor is the parameter list, and
        // that trade is measured in the remarks above.
        // ⚠ "Around" and not "before": the points were last-resort ones until issue #377, and a
        // last-resort point is read through by the list's own arguments too, so a list nested in the
        // first argument saw the whole line, broke first, and `Dictionary<(Dictionary<Guid,
        // List<Guid>> First, …), List<…>>` came back as `List<\nGuid>>` where the oracle breaks the
        // outer comma — with or without the tuple. The outer list decides before anything inside an
        // argument does, which is the same rule every other list already has.
        var yields = node is TypeArgumentListSyntax;

        // ⚠ A break the author wrote at a comma is kept, on either side of it, and it is a required
        // break rather than a fill point. A fill point re-decides by width, so `G<T,\n U>()` came
        // back as `G<T, U>()` although the group was planned as broken from the source — the same
        // per-gap pin `PlanList` makes for a list pattern (`pinsItemBreaks`), and the oracle keeps
        // the type parameter list's break exactly as it keeps that one. The other side of the comma
        // is kept too, as a tuple's is and an argument list's is not: the list is filled, never
        // re-laid, so `G<T\n, U>()` comes back as written with `, U` one level in. Measured under
        // `skala_wrap_before_comma = false` on both sides; no corpus input had an author's break in a
        // type parameter list, which is how both halves survived (SK-DIV-0104). The arrow after such
        // a list breaks, because the owner is no longer one line — see PlanExpressionBody.
        // ⚠ And the gap after the `<` is pinned the same way (SK-DIV-0114): it was the one fill point
        // left unpinned, so `void M<\nT, U>()` and `Dictionary<\nstring, int>` came back joined
        // although the group was planned as broken. The oracle keeps both exactly as written.
        var broken = PlanItemGap(first, group, true, keeps, yields);
        foreach (var comma in items.GetSeparators()) {
            var next = comma.GetNextToken();
            if (next.IsKind(SyntaxKind.None) || next.SpanStart >= close.SpanStart) {
                continue;
            }

            var gap = options.WrapBeforeComma ? comma : next;
            var other = options.WrapBeforeComma ? next : comma;
            broken |= PlanItemGap(gap, group, true, keeps, yields);
            broken |= PlanOtherSideOfComma(other, keeps);
        }

        // ⚠ The closing `>` is nobody's point — the oracle never gives it a line of its own — and it
        // used to be `Flat` here, which joined a break the author wrote before it. Measured
        // (SK-DIV-0114): `void M<T, U\n>() { }` and `Dictionary<string, int\n> P()` both come back
        // with the `>` exactly where the author put it, on the declaration's own indent. So the gap
        // is left to `keep_user_linebreaks`, as a type argument list's always was, and a kept one
        // makes the owner multi-line.
        broken |= keeps && BreaksBefore(close);

        Describe(
            node,
            group,
            GroupMode.Preserve,
            new(
                options.KeepsUserBreaksBetweenItems && broken,
                BreaksIfTooLong: true,
                // ⚠ Armed by the list's own width for a type parameter list — the trade the remarks
                // above measure — and by the whole line for a type argument list, whose competitors
                // are an `=` and an argument list rather than a declaration's parameter list.
                // Measured (SK-DIV-0114): `var created = new Dictionary<A, B>();` at 121 columns
                // comes back as `var created =\n new Dictionary<A, B>();` with the list whole, and
                // arming the list by its own width filled it on the first line instead; a list that
                // still overflows after the `=` break is filled on the second line.
                MeasuresHead: node is TypeParameterListSyntax
            )
        );
    }

    /// <summary>
    ///     The <c>where</c> clauses of a generic declaration:
    ///     <c>skala_wrap_before_first_type_parameter_constraint</c> and
    ///     <c>skala_wrap_multiple_type_parameter_constraints_style</c>.
    /// </summary>
    /// <remarks>
    ///     ⚠ Planned from the <em>declaration</em> and not from the clause, because the construct being
    ///     laid out is the whole run and a clause cannot see its siblings. The two groups and why there
    ///     are two are in <see cref="ConstraintRun" />; what is decided here is which gap belongs to
    ///     which of them.
    ///     <para>
    ///         ⚠ At <c>skala_wrap_before_first_type_parameter_constraint = false</c> the first <c>where</c> is
    ///         still a break point — it is simply one measured against the first clause alone rather than
    ///         against the whole list. The oracle does move it when the declaration and its first clause do
    ///         not fit together, so reading <c>false</c> as "never break there" loses a break ReSharper
    ///         takes; and <c>chop_always</c> at <c>false</c> leaves the first clause on the declaration's
    ///         line while giving every other clause one of its own, which is the shape that reaches the key.
    ///     </para>
    /// </remarks>
    void PlanConstraints(SyntaxNode node) {
        var clauses = ConstraintsOf(node);

        // skala_place_type_constraints_on_same_line = false makes every `where` a mandatory break, and the
        // arm below plans that. Two rules over one gap is one rule too many.
        if (clauses.Count == 0 || !options.PlaceTypeConstraintsOnSameLine) {
            return;
        }

        var style = options.WrapMultipleTypeParameterConstraintsStyle;
        var fill = style == WrapStyle.WrapIfLong;
        var wrapsBeforeFirst = options.WrapBeforeFirstTypeParameterConstraint;
        var firstWhere = clauses[0].WhereKeyword;

        var outer = NewGroup();
        if (wrapsBeforeFirst) {
            Point(firstWhere, outer);
        }

        var inner = NewGroup();
        var innerBroken = false;
        for (var i = 1; i < clauses.Count; i++) {
            var where = clauses[i].WhereKeyword;
            Point(where, inner, fill);
            innerBroken |= BreaksBefore(where);
        }

        // ⚠ `skala_indent_type_constraints` and not an unconditional level. The clause's own
        // NodeLayout.Continuation arm is what spends it everywhere else, and the run takes the gaps
        // before the `where`s away from that arm — so if the run spent the level unconditionally the
        // key would stop being observable on exactly the shape its fixture pins.
        var indents = options.IndentTypeConstraints;
        var firstBroken = options.KeepsUserBreaksBetweenItems && BreaksBefore(firstWhere);
        constraints[Key(node)] = new(
            new GroupPlan(
                outer,
                style == WrapStyle.ChopAlways && wrapsBeforeFirst ? GroupMode.Break : GroupMode.Preserve,
                new(wrapsBeforeFirst && firstBroken, BreaksIfTooLong: true),
                indents
            ),
            new GroupPlan(
                inner,
                style == WrapStyle.ChopAlways ? GroupMode.Break : GroupMode.Preserve,
                new(
                    options.KeepsUserBreaksBetweenItems && innerBroken,
                    BreaksIfTooLong: true
                ),
                indents
            ),
            wrapsBeforeFirst
        );

        // ⚠ The first clause's own group, and only when the run's outer group did not take that gap.
        // It spans one clause, so it is entered at the column the declaration reached and measured
        // against that clause alone — which is exactly the question `false` asks.
        if (!wrapsBeforeFirst) {
            var head = NewGroup();
            Point(firstWhere, head);
            Describe(
                clauses[0],
                head,
                GroupMode.Preserve,
                new(firstBroken, BreaksIfTooLong: true),
                indents,
                true
            );
        }
    }

    /// <summary>
    ///     The constraints inside one <c>where</c> clause: a fill at the clause's own column, keeping a
    ///     break the author wrote on either side of a comma.
    /// </summary>
    /// <remarks>
    ///     ⚠ Measured on both sides (SK-DIV-0105), and the level is the finding: a clause broken before
    ///     the comma and one broken after it both put the next constraint on the
    ///     <c>where</c>'s own column — at <c>skala_indent_type_constraints</c> true and false, at
    ///     <c>skala_place_type_constraints_on_same_line</c> true and false, and at
    ///     <c>skala_continuous_indent_multiplier = 2</c>, where the <c>where</c> moves and the constraint
    ///     moves with it. A list too wide for the margin wraps at the last comma that fits, on the same
    ///     column. Skala had no plan for these gaps, so <c>keep_user_linebreaks</c> kept them and the
    ///     clause's frame spent a level on each: one indent past the <c>where</c>, two at the multiplier.
    ///     <para>
    ///         Both sides of the comma are kept, as a tuple's are (SK-DIV-0104): there is no
    ///         <c>wrap_*</c> style over the constraints of one clause for the oracle to re-lay them with.
    ///         The group spends no indent, and <see cref="CSharpDocumentBuilder" /> gives the clause a
    ///         frame that pays for nothing, so a pinned break lands on the column the clause started at.
    ///     </para>
    /// </remarks>
    void PlanConstraintList(SyntaxNode node) {
        if (node is not TypeParameterConstraintClauseSyntax { Constraints.Count: > 1 } clause) {
            return;
        }

        var group = NewGroup();
        var keeps = options.KeepsUserBreaksBetweenItems;
        var broken = false;
        foreach (var comma in clause.Constraints.GetSeparators()) {
            var next = comma.GetNextToken();
            if (next.IsKind(SyntaxKind.None) || next.SpanStart >= clause.Span.End) {
                continue;
            }

            broken |= PlanItemGap(next, group, true, keeps);
            broken |= PlanOtherSideOfComma(comma, keeps);
        }

        // ⚠ An inner group, opened by the builder after the `where`, and not one around the clause:
        // a group around the node makes VisitPlanned emit the gap before the `where` outside the
        // clause's own scope, and that gap is what `skala_indent_type_constraints` indents when there
        // is no constraint run to spend the level.
        DescribeInner(node, group, GroupMode.Preserve, new(keeps && broken, BreaksIfTooLong: true));
    }

    /// <summary>A declaration's <c>where</c> clauses, whichever of the four kinds it is.</summary>
    static SyntaxList<TypeParameterConstraintClauseSyntax> ConstraintsOf(SyntaxNode? node) =>
        node switch {
            TypeDeclarationSyntax type => type.ConstraintClauses,
            MethodDeclarationSyntax method => method.ConstraintClauses,
            DelegateDeclarationSyntax declaration => declaration.ConstraintClauses,
            LocalFunctionStatementSyntax function => function.ConstraintClauses,
            _ => default
        };

    /// <summary>
    ///     The gap between a field's or a local's type and its first name, broken when the line through
    ///     the name and its <c>=</c> does not fit.
    /// </summary>
    /// <remarks>
    ///     ⚠ Measured with <c>jb cleanupcode</c> 2025.2.6 (#474, SK-DIV-0127): a field
    ///     <c>IReadOnlyDictionary&lt;string, IReadOnlyList&lt;string&gt;&gt; Overflowing;</c> past the margin and a
    ///     tuple-typed field come back with the name one level in on a line of its own, the type whole —
    ///     where Skala filled the type's argument list, <c>Dictionary&lt;Guid,</c> / <c>List&lt;Guid&gt;&gt; First, …</c>;
    ///     a local whose <c>=</c> lands past 120 the same, <c>T…T</c> / <c>    v9 = [</c>, and
    ///     <c>Dictionary&lt;T…T, int&gt;</c> / <c>    v9 = [</c> rather than a break at the type argument
    ///     list's comma. When the line through the <c>=</c> fits, the <c>=</c>'s own rule decides, as it did.
    ///     So the gap is a group of its own whose one question is the ordering rule's second: does the line
    ///     run past the margin before the next place it could end — the <c>=</c>'s point, or the end of
    ///     the declaration. The type's own argument lists see their line end at this point and stay whole.
    ///     ⚠ One shape stays divergent: at exactly 121 columns, with a bracket that fits below, the oracle
    ///     breaks here where 122 and wider break the <c>=</c>, and Skala breaks the <c>=</c> at 121 too.
    ///     Not a method's return type or a property's, which the oracle answers with the arrow or the
    ///     accessor list, and not under a block comment behind the type, whose break #420 already takes.
    ///     A break the author wrote here is kept, as it was.
    /// </remarks>
    void PlanTypeNameGap(VariableDeclarationSyntax node) {
        if (node.Parent is not (LocalDeclarationStatementSyntax or FieldDeclarationSyntax)
            || node.Variables.Count == 0
            || node.Type.IsVar) {
            return;
        }

        // ⚠ Not when a comment sits inside the type: the oracle breaks a type argument list past a block
        // comment (#409) — `Dictionary<A, /* f */` / `B<C, int>> field = null;` — where the same type
        // without it fits whole and gives the break to the name.
        // ⚠ Nor before a lambda: its arrow and a parenthesised body hold the statement's level at zero
        // (SK-DIV-0101, #406), and this group's level, spent on the declaration's line, would show
        // under the hold — `f = () =>` / `    (`. Not measured with a type long enough to break here.
        var name = node.Variables[0].Identifier;
        if (HasBlockCommentBefore(name)
            || node.Type.DescendantTrivia()
                .Any(static trivia => trivia.IsKind(SyntaxKind.MultiLineCommentTrivia)
                    || trivia.IsKind(SyntaxKind.SingleLineCommentTrivia)
                )
            || node.Variables[0].Initializer?.Value is AnonymousFunctionExpressionSyntax) {
            return;
        }

        // ⚠ And the gap between the modifiers and the type, by the same question (#540): the oracle
        // writes `public static readonly` / `    IReadOnlyDictionary<…>` / `    Overflowing;` when the
        // type does not fit after the modifiers, the name below it by the gap that follows, and fills a
        // type too long for its own line there — `    IReadOnlyDictionary<…,` / `        …>> Overflowing;`.
        // Skala filled the type on the modifiers' line. A group around the type owning the gap before
        // it; the type's argument lists yield to it as they yield to everything before them.
        // ⚠ A field's, and not a `const` local's: measured on `const IReadOnlyDictionary<…> local = null;`
        // from 121 to 123, the oracle fills the type on the `const` line. Nor, measured and not modelled,
        // a field whose name is one letter, which the oracle fills from a 133-column line up where every
        // longer name moves the type below (SK-DIV-0127).
        if (node.Parent is FieldDeclarationSyntax { Modifiers.Count: > 0 }) {
            var modifiers = NewGroup();
            Point(node.Type.GetFirstToken(), modifiers);
            Describe(
                node.Type,
                new(
                    modifiers,
                    GroupMode.Preserve,
                    new(
                        options.KeepsUserBreaksBetweenItems && BreaksBefore(node.Type.GetFirstToken()),
                        BreaksIfTooLong: true,
                        PrefersOuterBreak: true,
                        SkipsOuterTail: true
                    ),
                    true,
                    true
                )
            );
        }

        // ⚠ Around the declarator and owning the gap before it, so that the group is entered once the
        // type has been written: a type too long for any line fills its own argument list first, and
        // the name then stays on the type's last line when it fits there —
        // `Dictionary<(…),` / `    List<(…)>> local = null;` is the oracle's, and a group entered at
        // the type's first token measured the whole type and moved `local` down.
        var group = NewGroup();
        Point(name, group);
        Describe(
            node.Variables[0],
            new(
                group,
                GroupMode.Preserve,
                // ⚠ The ordering rule's second question alone, asked through PrefersOuterBreak and
                // SkipsOuterTail rather than BreaksOnlyIfHeadOverflows: that fact also makes the type's
                // own argument list read through a name with nothing breakable after it, which filled
                // `IReadOnlyDictionary<string,` / `…>> Overflowing;` where the oracle moves the name.
                new(
                    options.KeepsUserBreaksBetweenItems && BreaksBefore(name),
                    BreaksIfTooLong: true,
                    PrefersOuterBreak: true,
                    SkipsOuterTail: true
                ),
                true,
                true
            )
        );
    }

    /// <summary>
    ///     The gap between a parameter's type and its name, broken when the line through the name does not
    ///     fit (#545) — <see cref="PlanTypeNameGap" />'s rule, on a parameter.
    /// </summary>
    /// <remarks>
    ///     ⚠ Measured with <c>jb cleanupcode</c> 2025.2.6 one column at a time on a chopped parameter list:
    ///     <c>int</c> / <c>    aaa…</c> from a 121-column parameter line up, unchanged to 120; the same for a
    ///     <c>Dictionary&lt;string, List&lt;string&gt;&gt;</c>, a second parameter, a <c>ref</c> or a <c>params</c>
    ///     one (the break after the whole type), a record's primary constructor and a lambda's parameter
    ///     list. A parameter with a default value breaks at its <c>=</c> instead (<c>int bbb… =</c> /
    ///     <c>    1</c>), which is that gap's own rule, so the group opens at the name and the line it asks
    ///     about ends at the <c>=</c>. Not when a comment sits in the type or before the name.
    /// </remarks>
    void PlanParameterTypeNameGap(ParameterSyntax node) {
        // ⚠ Not behind an attribute section: the attribute runs' own break (#475, #476, #537) is the one
        // the oracle takes there, and a parameter with attributes and a name past the margin was not
        // measured.
        if (node.Type is null
            || node.AttributeLists.Count > 0
            || node.Identifier.IsMissing
            || node.Parent is not (ParameterListSyntax or BracketedParameterListSyntax)
            || HasBlockCommentBefore(node.Identifier)
            || node.Type.DescendantTrivia()
                .Any(static trivia => trivia.IsKind(SyntaxKind.MultiLineCommentTrivia)
                    || trivia.IsKind(SyntaxKind.SingleLineCommentTrivia)
                )) {
            return;
        }

        var group = NewGroup();
        Point(node.Identifier, group);
        OpenAt(
            node,
            node.Identifier.SpanStart,
            new(
                group,
                GroupMode.Preserve,
                new(
                    options.KeepsUserBreaksBetweenItems && BreaksBefore(node.Identifier),
                    BreaksIfTooLong: true,
                    PrefersOuterBreak: true,
                    SkipsOuterTail: true
                ),
                true,
                true,
                // ⚠ One level past the parameter inside its list, as the `=` of a default value spends one
                // (SK-DIV-0103): measured, `int` / `            aaa…` at a parameter on column 8.
                SpendsUnderDelimiters: true
            )
        );
    }

    /// <summary>
    ///     <c>skala_wrap_multiple_declaration_style = chop_if_long</c>: <c>int a = 1, b = 2, c = 3;</c> puts
    ///     one declarator per line when it does not fit.
    /// </summary>
    void PlanDeclarators(VariableDeclarationSyntax node) {
        var group = NewGroup();
        var broken = false;
        foreach (var comma in node.Variables.GetSeparators()) {
            var next = comma.GetNextToken();
            if (next.IsKind(SyntaxKind.None)) {
                continue;
            }

            Flat(comma);
            Point(next, group);
            broken |= BreaksBefore(next);
        }

        var plan = new GroupPlan(
            group,
            options.WrapMultipleDeclarationStyle == WrapStyle.ChopAlways ? GroupMode.Break : GroupMode.Preserve,
            new(
                options.KeepsUserBreaksBetweenItems && broken,
                BreaksIfTooLong: options.WrapMultipleDeclarationStyle != WrapStyle.WrapIfLong
            ),
            true,
            UnconditionalLevel: true
        );

        // ⚠ A block comment behind the type breaks the line before the first name (#420), and that
        // break is the declaration's, not the list's: the oracle writes `int /* c */` / `a, b;` and
        // keeps the names together. Around the whole declaration the group would hold the break and
        // could never be flat, so it opens after the gap instead.
        if (CSharpDocumentBuilder.IsFirstDeclaratorBehindItsType(node.Variables[0])
            && HasBlockCommentBefore(node.Variables[0].Identifier)) {
            OpenAt(node, node.Variables[0].SpanStart, plan);
            return;
        }

        Describe(node, plan);
    }

    static bool HasBlockCommentBefore(SyntaxToken token) =>
        token.LeadingTrivia.Concat(token.GetPreviousToken().TrailingTrivia)
            .Any(static trivia => trivia.IsKind(SyntaxKind.MultiLineCommentTrivia)
                || trivia.IsKind(SyntaxKind.MultiLineDocumentationCommentTrivia)
            );

    /// <summary>
    ///     <c>skala_wrap_chained_method_calls = chop_if_long</c>: every <c>.</c> of a chain that does not fit
    ///     starts a line, and the first call does not.
    /// </summary>
    /// <remarks>
    ///     ⚠ Three keys decide which dots are points, and the answer is not "all of them".
    ///     <c>skala_wrap_before_first_method_call = false</c> keeps <c>source.Where(…)</c> together, so the
    ///     first invoked dot is not a point; <c>skala_wrap_after_property_in_chained_method_calls = false</c>
    ///     means a dot that reaches a property rather than a method is not one either. Verified against
    ///     the oracle, which writes
    ///     <code>
    /// var q = source.Where(x => x.IsActive)
    ///     .OrderBy(x => x.Name)
    ///     .Select(x => x.Id);
    ///     </code>
    ///     and not a break before <c>.Where</c>.
    ///     <para>
    ///         ⚠ "The first call" is counted in calls, not in dots, and the first call need not have a
    ///         dot at all. <c>SomeMethod(a, b).Other(c, d)</c> is a chain of <em>two</em> calls to the
    ///         oracle — at <c>chop_always</c> it chops before <c>.Other</c> where <c>x.Other(c, d)</c>
    ///         stays whole — so its only dot is a point, and the same holds for a generic
    ///         <c>F&lt;T&gt;(…)</c>, a delegate invocation <c>handler(a)(b)</c> and an element access
    ///         <c>arr[0]</c> or <c>x.Items[0]</c> at the head. Counting dots alone made
    ///         <c>F(…).Other(…)</c> no chain, so the last argument list took the break instead
    ///         (issue #380, SK-DIV-0128). ⚠ A parenthesised call <c>(F(…)).Other(…)</c>, an object
    ///         creation <c>new T(…).Other(…)</c> and a bare <c>x?.Other(…)</c> are <em>not</em> calls at
    ///         the head: the oracle chops their argument list, measured. ⚠ The <c>wrap_if_long</c> fill
    ///         below is a separate and open matter (SK-DIV-0129): the oracle keeps a <em>last</em>
    ///         link's <c>.Other(</c> on the line and chops its arguments even when the whole link would
    ///         fit on the continuation line, and breaks before a middle link; this fill measures the
    ///         whole link, on a property root as much as on a call root.
    ///     </para>
    /// </remarks>
    void PlanChainedCalls(SyntaxNode root) {
        var first = ChainPointCount(root, options);
        if (first == 0) {
            PlanPropertyFill(root);
            return;
        }

        var (dots, _, _) = ChainLinks(root, options);
        var group = NewGroup();
        chainGroups[Key(root)] = group;
        var broken = false;

        // ⚠ `wrap_if_long` is a fill and not "leave the chain alone". Measured, one key flipped, at
        // the export's 120-column margin:
        //   var b = source.Where(…).OrderBy(…).Select(…).ToList().AsReadOnly()
        //       .Count();
        // — the last dot that still fits, and one break rather than one per link. The style used to
        // reach the group as `BreaksIfTooLong: style != WrapIfLong`, which is "never break", and a
        // 129-column chain came back whole. Same misreading as the base list's; see PlanBaseList.
        var fill = options.WrapChainedMethodCalls == WrapStyle.WrapIfLong;

        // ⚠ And a fill keeps the author's own breaks gap by gap rather than re-flowing them, which
        // is the same correction `PlanList`'s `pinsItemBreaks` and `PlanForHeader`'s
        // `pinsClauseBreaks` make: a chain the author already broke at every dot comes back from
        // `wrap_if_long` with every one of those breaks, although the fill would have re-joined all
        // but the last. A per-group flag cannot say it — the preserved gaps and the filled ones are
        // siblings — so a preserved gap becomes an ordinary required break and the rest stay points.
        var pinsLinkBreaks = fill && options.KeepsUserBreaksBetweenItems;
        for (var i = 0; i < first; i++) {
            if (options.WrapAfterDotInMethodCalls) {
                // ⚠ "After the dot" has to mean after *both* tokens of a `?.`. `ChainDot` registers
                // the `?`, because that is where a break before the link belongs, and the token
                // after the `?` is the `.` rather than the name — so pointing at it blindly gives
                // `…(more)?\n.Where(…)` where the oracle writes `…(more)?.\n Where(…)`. Measured:
                // it took `skala_wrap_after_dot_in_method_calls`, Tier A and Conformant,
                // to Divergent at its non-export value.
                var dot = dots[i];
                Flat(dot);
                if (dot.IsKind(SyntaxKind.QuestionToken)) {
                    dot = dot.GetNextToken();
                    Flat(dot);
                }

                var next = dot.GetNextToken();
                broken |= Link(next);
            } else {
                broken |= Link(dots[i]);
            }
        }

        Describe(
            root,
            new(
                group,
                options.WrapChainedMethodCalls == WrapStyle.ChopAlways ? GroupMode.Break : GroupMode.Preserve,
                new(
                    options.KeepsUserBreaksBetweenItems && broken,
                    BreaksIfTooLong: true,
                    HidesFlatWidthWhenBroken: true,
                    // ⚠ A block in the chain's receiver nests from the chain's continuation line once
                    // the chain broke after it: `(y switch { … }).ToString()` / `.Length…` (SK-DIV-0148),
                    // and so does an argument list on its first line (#418, SK-DIV-0184).
                    // ⚠ Not a fill's. A `wrap_if_long` chain resolves broken whenever it does not fit
                    // whole, and the oracle lifts only when the fill then breaks after the block or the
                    // list — `source.Select(x => {` … `}).Where(beta);` keeps the body one level in.
                    // That is an output fact the group does not have when the block opens; SK-DIV-0185.
                    // ⚠ So the writer answers it, for a list: it writes the rest of the chain ahead and
                    // lifts when the fill takes a point (#496). An author's break the fill pins is taken
                    // whatever the width, so such a chain lifts outright — which is what keeps pass two,
                    // reading pass one's fill break as the author's, on pass one's answer.
                    Continues: !fill || pinsLinkBreaks && broken,
                    ContinuesIfItBreaks: fill && !(pinsLinkBreaks && broken)
                ),
                // ⚠ The chain opens its own continuation scope. Milestone 2 spent that level lazily, in
                // `Break`, at the first break landing before a `.` — and a group's break point never
                // goes through `Break`, so a chain that the fitter chops comes out flush with its
                // receiver:
                //     text.AppendLine("…")
                //     .AppendLine("…")
                // The frame machinery still serves breaks the author wrote; this serves the ones the
                // fitter adds.
                // ⚠ `ownLevel` rather than `spendsIndent`, which is the difference between "a level if
                // no other continuation is open" and "a level, always". A chained call takes one even
                // inside another continuation and a binary chain does not — the asymmetry
                // CSharpDocumentBuilder.VisitInner records — and the shape that shows it is an
                // expression-bodied member whose arrow has already broken:
                //     static void Member(Packer packer) =>
                //         packer.Enum(a)
                //             .Enum(b);      ← two levels, not one
                // The one-level-per-opening-line collapse in LayoutWriter.Level is what keeps
                // `var x = a.B()\n    .C();` at one: there the `=`'s scope and the chain's open on the
                // same line.
                // ⚠ Except when the chain's head is a parenthesised expression or a tuple, which
                // takes the level only if nothing else is spending one — `spendsIndent`'s rule, not
                // `ownLevel`'s (SK-DIV-0112). Under an arrow, an `=` or a `return` that has already
                // spent, the dots of such a chain land on the parenthesis's own column:
                //     object A() =>            object B() =>           var x =
                //         (                        (a                      (
                //             a).B                     + b).C                  a).B
                //         .C();                    .D();                   .C();
                // — a single-line head `(a + b).C\n.D()` included, and a tuple, `?.` and `[0]` after
                // the `)` alike — while the same chain as a statement, with nothing spent yet, puts
                // them one level in. An invocation head keeps `ownLevel`: `F(\n a\n)\n.B` puts `.B`
                // one level past the arrow's. Measured on seventeen shapes. The frame half of the
                // same rule — an author's break before a dot that is not a point — is
                // CSharpDocumentBuilder's Frame.HoldsLevel.
                SharesTheLevelAroundIt(root),
                OwnLevel: !SharesTheLevelAroundIt(root)
            )
        );

        PlanHeldFirstCall(dots, first, fill ? root : null);

        bool Link(SyntaxToken gap) {
            var broke = BreaksBefore(gap);
            if (pinsLinkBreaks && broke) {
                Mandatory(gap);
            } else {
                Point(gap, group, fill);
            }

            return broke;
        }
    }

    /// <summary>
    ///     The held first call of a chain (<c>skala_wrap_before_first_method_call = false</c>) breaks too
    ///     when it does not fit on the receiver's line and does fit whole on the continuation line.
    /// </summary>
    /// <remarks>
    ///     ⚠ Measured for #528 (SK-DIV-0331), a column at a time on two-call chains at indent 8:
    ///     <c>var y = S….Select(alpha)</c> / <c>.Where(b);</c> holds the call while it ends at 120 and
    ///     breaks before it from 121 — <c>S…</c> / <c>.Select(alpha)</c> / <c>.Where(b);</c> — even with
    ///     <c>.Select(</c> itself at 117; with two 19-column arguments the call is broken before at every
    ///     head from 80 to 113 columns, its arguments whole. Where the call does not fit on the
    ///     continuation line either, the oracle holds it and chops its arguments (#418's rows,
    ///     <c>source.Select(</c> / … / <c>)</c> / <c>.Where(beta)</c>) — the other half of the same
    ///     rule, and <see cref="GroupFacts.BreaksOnlyIfTailFits" />'s own question.
    /// </remarks>
    void PlanHeldFirstCall(List<SyntaxToken> dots, int points, SyntaxNode? fillRoot) {
        if (points >= dots.Count || dots[^1] is not { Parent: MemberAccessExpressionSyntax access } dot) {
            return;
        }

        if (BreaksBefore(dot)) {
            return;
        }

        SyntaxNode link = access;
        while (link.Parent is MemberAccessExpressionSyntax outer && outer.Expression == link) {
            link = outer;
        }

        if (link.Parent is not InvocationExpressionSyntax call || call.Expression != link) {
            return;
        }

        // ⚠ The argument count picks the measured table (one argument or none, or more), and the call's
        // head — dot to `(` — places the column the held `(` would land on; see Fitter.HeldCallLimit and
        // SK-DIV-0331.
        var kind = call.ArgumentList.Arguments.Count <= 1 ? 1 : 2;
        var callHead = call.ArgumentList.OpenParenToken.Span.End - dot.SpanStart;

        // ⚠ Under `wrap_if_long` the rest of the chain after the held call weighs in too (#552); see
        // GroupFacts.HeldCallRest.
        var rest = fillRoot is null || call == fillRoot ? 0 : RestWidth(call, fillRoot);
        var group = NewGroup();
        Point(dot, group);
        Describe(
            call,
            group,
            GroupMode.Preserve,
            new(BreaksIfTooLong: true, HeldCall: kind, HeldCallHead: callHead, HeldCallRest: rest)
        );
    }

    /// <summary>
    ///     A single call on a receiver that is the whole value of an <c>=</c> — no chain, one dot: its dot is
    ///     a point, and the <c>=</c> before it answers by the measured table in GroupFacts.HeldValue (#528,
    ///     SK-DIV-0331). Returns the call, or null where nothing was planned.
    /// </summary>
    /// <remarks>
    ///     ⚠ Measured 2026-10-09 against <c>T c = JsonConvert</c> / <c>.DeserializeObject&lt;…&gt;(json);</c>
    ///     (Newtonsoft) and synthetic receivers of 10 to 102 columns, behind <c>var y</c>, <c>y</c>,
    ///     <c>var zzzzzzzzzzzz</c>, a twenty-column assignment target and typed heads of 15 and 38 columns.
    ///     ⚠ A typed local with more than one argument was not measured and plans nothing.
    /// </remarks>
    InvocationExpressionSyntax? PlanHeldSingleCall(ExpressionSyntax value, int kind) {
        if (value is not InvocationExpressionSyntax {
                Expression: MemberAccessExpressionSyntax { OperatorToken: var dot, Expression: var receiver }
            } call
            || kind == 1
            && call.ArgumentList.Arguments.Count > 1
            // ⚠ Measured on plain arguments only: an argument that breaks inside itself — a lambda's body,
            // an initializer — is #529's and #378's layout, not this table's.
            || call.ArgumentList.DescendantNodes()
                .Any(static node => node is AnonymousFunctionExpressionSyntax
                    or InitializerExpressionSyntax
                    or AnonymousObjectCreationExpressionSyntax
                    or SwitchExpressionSyntax
                    or CollectionExpressionSyntax
                    or WithExpressionSyntax
                )
            // ⚠ And behind a `var` or an assignment, on arguments with no call or creation of their own:
            // `var bottom = device.CreateAccelerationStructure(new(…));` and `var listener =
            // fleet.World.Create(AiPerception.Sensing(…), …);` keep the `=` and chop where the table would
            // break it (Vixen). A typed local's `EnumInfo e = Values.Get(new StructMultiKey<…>(…));` does
            // follow it (Newtonsoft).
            || kind != 1
            && call.ArgumentList.DescendantNodes()
                .Any(static node => node is InvocationExpressionSyntax or BaseObjectCreationExpressionSyntax)
            || IsChainRoot(value)
            && ChainPointCount(value, options) > 0
            || receiver is InvocationExpressionSyntax or ElementAccessExpressionSyntax
            || BreaksBefore(dot)
            || source.AsSpan(value.SpanStart, value.Span.Length).IndexOfAny('\r', '\n') >= 0) {
            return null;
        }

        var group = NewGroup();
        Point(dot, group);
        Describe(
            call,
            group,
            GroupMode.Preserve,
            new(
                BreaksIfTooLong: true,
                HeldCall: call.ArgumentList.Arguments.Count > 1 ? 4 : 3,
                HeldCallHead: call.ArgumentList.OpenParenToken.Span.End - dot.SpanStart
            ),
            // ⚠ A level of its own, as a chain's: `var y =` / `R` / `.Call(…)` puts the dot one level past
            // the receiver, and LayoutWriter's one-level-per-line collapse keeps `var y = R` / `.Call(…)` at one.
            ownLevel: true
        );
        return call;
    }

    /// <summary>
    ///     A member-access expression that is no chain of calls — <c>A.B.C.D.Value</c> — breaks at the last
    ///     of its dots that still fits, once.
    /// </summary>
    /// <remarks>
    ///     ⚠ Measured (#482, SK-DIV-0124): past the margin the oracle writes
    ///     <c>Aaaa.Bbbb.Cccc.Dddd</c> / <c>.MoreValue =&gt; 2u,</c> in a switch arm, rather than breaking
    ///     the arrow; <c>var v = Aaaa.Bbbb.Cccc.Dddd</c> / <c>.MoreValue;</c> rather than breaking the
    ///     <c>=</c>; <c>….Dddd</c> / <c>.MoreValue.Rest;</c> — the <em>last dot that fits</em>, with what
    ///     follows it kept on the continuation line, not the first of a property run as in a chain of
    ///     calls; the same past an indexer (<c>a.B[0].C.D</c> / <c>.E</c>), after a <c>?.</c>, on an
    ///     assignment's target, after <c>return</c>, and inside an argument the list has already chopped.
    ///     So it is a fill over every dot. It is not taken while a cheaper break is on the line:
    ///     <c>if (A.B.C.D.E</c> / <c>== x)</c>. And it is only the shape that ends in a property: property
    ///     dots before a single call — <c>alpha.Beta.Gamma.Method(…)</c> — chop the call's arguments
    ///     instead.
    /// </remarks>
    void PlanPropertyFill(SyntaxNode root) {
        if (TrailingProperty(root) is null || !PlansTheFill(root)) {
            return;
        }

        var dots = new List<SyntaxToken>();
        Walk(root);
        if (dots.Count == 0) {
            return;
        }

        var group = NewGroup();
        foreach (var dot in dots) {
            if (options.KeepsUserBreaksBetweenItems && BreaksBefore(dot)) {
                Mandatory(dot);
            } else {
                Point(dot, group, true, IsAssignmentTarget(root));
            }
        }

        // ⚠ As a sole lambda argument's body the fill is one level past the line it starts on (#557,
        // measured): `Use(x => x.Alpha…Papa` / `.Quebec` one level in while the arrow stays, and
        // `Use(x =>` / `x.Alpha…` / `.Quebec` one past the body once it breaks — a level of its own would
        // be two on the arrow's line, and a shared one none below it. Among other arguments it keeps its
        // own level: `x => x.Alpha…` / `.Quebec` two past the chopped list.
        var fromLine = !HeadSharesTheLevelAroundIt(root)
            && options.PlaceSingleMethodArgumentLambdaOnSameLine
            && IsTheBodyOfASoleLambda(root);

        Describe(
            root,
            new(
                group,
                GroupMode.Preserve,
                new(
                    BreaksIfTooLong: true,
                    HidesFlatWidthWhenBroken: !IsAssignmentTarget(root),
                    ArmHead: root.Parent is ConstantPatternSyntax { Parent: SwitchExpressionArmSyntax }
                        ? FlatSourceWidth(root)
                        : 0,
                    ArmBody: root.Parent is ConstantPatternSyntax { Parent: SwitchExpressionArmSyntax arm }
                        ? FlatSourceWidth(arm.Expression)
                        + (arm.GetLastToken().GetNextToken().IsKind(SyntaxKind.CommaToken) ? 1 : 0)
                        : 0
                ),
                HeadSharesTheLevelAroundIt(root),
                OwnLevel: !HeadSharesTheLevelAroundIt(root) && !fromLine,
                FromLine: fromLine
            )
        );

        void Walk(SyntaxNode node) {
            switch (node) {
                case MemberAccessExpressionSyntax member:
                    dots.Add(member.OperatorToken);
                    Walk(member.Expression);
                    return;

                case MemberBindingExpressionSyntax binding:
                    dots.Add(ChainDot(binding));
                    return;

                case ConditionalAccessExpressionSyntax conditional:
                    Walk(conditional.WhenNotNull);
                    Walk(conditional.Expression);
                    return;

                case ElementAccessExpressionSyntax element:
                    Walk(element.Expression);
                    return;

                default:
                    return;
            }
        }
    }

    /// <summary>Whether the property fill is planned in the position <paramref name="root" /> stands in.</summary>
    /// <remarks>
    ///     ⚠ Measured, and the position decides, not the expression. As an assignment's target the
    ///     <c>=</c> breaks first — <c>A.B.C.D.Value =</c> / <c>yyyyyyyy;</c> — and the target's dots only
    ///     when the target with its <c>=</c> overflows by itself (<c>A.B.C.D.More</c> / <c>.Value = 1;</c>,
    ///     #531): planned there as last-resort points, with the <c>=</c> told to stay
    ///     (<see cref="GroupFacts.FlatIfHeadOverflows" />). As the operand of <c>is</c>
    ///     or <c>as</c>, the type test's own break, an <c>=</c> or a lambda's arrow is taken instead
    ///     (#440, #444, #445). As a switch arm's pattern it is always planned, and whether it outranks the
    ///     arrow is the Fitter's: it depends on the column the <c>=&gt;</c> ends at (GroupFacts.ArmHead,
    ///     SK-DIV-0330).
    /// </remarks>
    static bool IsAssignmentTarget(SyntaxNode root) =>
        root.Parent is AssignmentExpressionSyntax assignment && assignment.Left == root;

    static bool PlansTheFill(SyntaxNode root) =>
        root.Parent switch {
            AssignmentExpressionSyntax assignment when assignment.Left == root => true,
            BinaryExpressionSyntax binary when IsTypeTest(binary) && binary.Left == root => false,
            IsPatternExpressionSyntax test when test.Expression == root => false,
            // ⚠ A switch arm's pattern is always planned, and the Fitter answers by the arm's table
            // (GroupFacts.ArmHead, #531). ⚠ A body short enough for the pattern to fill is not: its own
            // dot, `=> yyyyyyyyyyy.Z,`, is a point the pattern's fill looked ahead to and stopped at, and
            // the arrow broke where the oracle breaks the pattern's dot.
            SwitchExpressionArmSyntax arm when arm.Expression == root =>
                FlatSourceWidth(root) + (arm.GetLastToken().GetNextToken().IsKind(SyntaxKind.CommaToken) ? 1 : 0) > 14,
            _ => true
        };

    /// <summary>
    ///     The outermost link of a chain when it is a property — a member access or a <c>?.</c> binding
    ///     that nothing invokes or indexes — as its break token and the receiver left of it.
    /// </summary>
    static (SyntaxToken Dot, ExpressionSyntax Receiver)? TrailingProperty(SyntaxNode node) {
        while (node is ConditionalAccessExpressionSyntax conditional) {
            node = conditional.WhenNotNull;
        }

        return PropertyLink(node);
    }

    /// <summary>
    ///     The dots of a chain rooted at <paramref name="root" />, outermost first, and whether its first
    ///     call is a dot-less head — the walk <see cref="PlanChainedCalls" /> registers its points from.
    /// </summary>
    static (List<SyntaxToken> Dots, bool HeadIsACall, bool IsAChain) ChainLinks(
        SyntaxNode root,
        in PhaseOneOptions options
    ) {
        var dots = new List<SyntaxToken>();
        var headIsACall = false;
        var headIsAnInvocation = false;
        var calls = 0;
        var wrapAfterProperty = options.WrapAfterPropertyInChainedMethodCalls;

        // ⚠ A chain that *ends* in a property run is a chain too, and the run is its last link: the
        // oracle writes `.ToList()` / `.Count;` and `.Where(beta)` / `.Count.Value;` and
        // `.Count?.Value;` — one break, before the run's first dot (#454, SK-DIV-0066, SK-DIV-0184).
        // The walk below registers a property's dot only for the call it feeds, so a trailing run
        // has to be taken here, before it.
        if (!wrapAfterProperty && TrailingProperty(root) is var (trailingDot, trailingReceiver)) {
            dots.Add(PropertyRun(trailingDot, trailingReceiver).Dot);
        }

        Collect(root);

        // ⚠ An invocation at the head counts as a call even before a property; an indexer at the
        // head does only before a call (ChainPointCount).
        return (dots, headIsACall, (calls > 0 || headIsAnInvocation) && (dots.Count >= 2 || headIsACall));

        void Collect(SyntaxNode node) {
            switch (node) {
                case InvocationExpressionSyntax invocation:
                    if (invocation.Expression is MemberAccessExpressionSyntax access) {
                        // ⚠ `skala_wrap_after_property_in_chained_method_calls = false` does not mean "a
                        // property's dot is not a break point"; it means the break lands *before*
                        // the property rather than after it, so the property travels with the call
                        // it feeds. The oracle writes
                        //     .ToList()
                        //     .Count.ToString()
                        // and not `.ToList().Count` followed by `.ToString()`. Registering the
                        // invoked dot and skipping the property's gives exactly the wrong one of the
                        // two.
                        //
                        // ⚠ And the run does not stop at a `?`. `X(…)?.Value.Trim()` is one run —
                        // `?.Value` feeding `.Trim()` — and the oracle writes `.FirstOrDefault(…)` /
                        // `?.Value.Trim();` (measured on Skala's own `VersionSources.Value`); but so is
                        // `X.Self().Inner?.Children.Where(…)`, whose run begins left of the `?`, and the
                        // oracle writes `.Self()` / `.Inner?.Children.Where(…)` — with two properties
                        // left of it, two `?` inside it, or a `?.` at its start alike (#456,
                        // SK-DIV-0067). The run used to end on the `?` and leave `.Inner` behind.
                        if (wrapAfterProperty) {
                            dots.Add(access.OperatorToken);
                            (headIsACall, headIsAnInvocation) = (false, false);
                            calls++;
                            Collect(access.Expression);
                            return;
                        }

                        var run = PropertyRun(access.OperatorToken, access.Expression);
                        dots.Add(run.Dot);
                        (headIsACall, headIsAnInvocation) = (false, false);
                        calls++;
                        if (run.Recurse is not null) {
                            Collect(run.Recurse);
                        }

                        return;
                    }

                    if (invocation.Expression is MemberBindingExpressionSyntax binding) {
                        // ⚠ No recursion past the `?`, here or in a run that crossed one: the
                        // conditional access the binding hangs from walks its own receiver.
                        dots.Add(
                            !wrapAfterProperty && ConditionalOf(binding) is { } owner
                                ? PropertyRun(ChainDot(binding), owner.Expression, true).Dot
                                : ChainDot(binding)
                        );

                        (headIsACall, headIsAnInvocation) = (false, false);
                        calls++;
                        return;
                    }

                    // A call with no dot of its own — `F(…)`, `F<T>(…)`, `handler(a)(b)` — is the
                    // chain's first call. ⚠ Not a parenthesised one: `(F(…)).Other(…)` reaches the
                    // parenthesis below and stops, and the oracle chops `.Other`'s arguments there.
                    // ⚠ Every call arm assigns the flag and the walk is outermost-first, so the value
                    // left standing is the innermost call's: `a.B()[0].C()`'s first call is `a.B()`,
                    // and its `.B` stays with `a`.
                    (headIsACall, headIsAnInvocation) = (true, true);
                    Collect(invocation.Expression);
                    return;

                case ConditionalAccessExpressionSyntax conditional:
                    // ⚠ `a?.B().C()` splits into two tokens and two subtrees: the `?` is this node's
                    // own operator and every dot of the chain — the `.` of `.B` included, as the
                    // `MemberBindingExpression`'s operator — hangs off `WhenNotNull`. Walking only
                    // `Expression` therefore collects the *receiver*'s dots and none of the chain's,
                    // which for the common shape (`a?.Where(…).Select(…)`, where the receiver is a
                    // bare identifier) left `dots` empty, planned no group, and gave the chain no
                    // break points at all — SK-DIV-0030, whose recorded symptom was the last call's
                    // argument list taking the break instead and leaving a dangling `)`.
                    //
                    // ⚠ `WhenNotNull` first and `Expression` second, because the list is
                    // outermost-first and `WhenNotNull` holds the dots to the *right* of the `?`.
                    // Reaching this arm twice is not double-counting: the invocation arm's
                    // `MemberBindingExpressionSyntax` branch returns without recursing, so a
                    // conditional access is only ever entered from the chain root, from a `!` on its
                    // way out, or from an enclosing conditional's `WhenNotNull` — `a?.B()?.C().D()`,
                    // which nests and whose inner node is reached exactly once.
                    Collect(conditional.WhenNotNull);
                    Collect(conditional.Expression);
                    return;

                case MemberAccessExpressionSyntax member:
                    // ⚠ A dot that reaches a property is not a point in this export
                    // (`skala_wrap_after_property_in_chained_method_calls = false`), but it is still part
                    // of the chain and its receiver still has to be walked.
                    if (wrapAfterProperty) {
                        dots.Add(member.OperatorToken);
                    }

                    Collect(member.Expression);
                    return;

                case ElementAccessExpressionSyntax element:
                    // ⚠ An indexer is a call to the oracle. `arr[0].Other(c, d)` and
                    // `x.Items[0].Other(c, d)` both chop before `.Other` at `chop_always`, exactly as
                    // `F(a).Other(c, d)` does and `x.Other(c, d)` does not; so the `[0]` is the
                    // chain's first call and the dot after it is a point. Measured on #380.
                    //
                    // ⚠ Except an indexed property after a call, which is a link of its own: the
                    // oracle writes `source.Make()` / `.Items[0]` / `.Select(…)`, where
                    // `source.Items[0]` / `.Select(…)` keeps the indexed property as the head.
                    if (!wrapAfterProperty && PropertyLink(element.Expression) is var (elementDot, elementReceiver)) {
                        var run = PropertyRun(
                            elementDot,
                            elementReceiver,
                            element.Expression is MemberBindingExpressionSyntax
                        );

                        if (run.Final is InvocationExpressionSyntax or ElementAccessExpressionSyntax) {
                            dots.Add(run.Dot);
                            (headIsACall, headIsAnInvocation) = (false, false);
                            calls++;
                            if (run.Recurse is not null) {
                                Collect(run.Recurse);
                            }

                            return;
                        }
                    }

                    (headIsACall, headIsAnInvocation) = (true, false);
                    Collect(element.Expression);
                    return;

                // ⚠ `receiver?[0].Children.Where(…)` has an indexer at the head as much as
                // `receiver[0]` does: the oracle writes `receiver?[0]` / `.Children.Where(…)` (#455,
                // SK-DIV-0068). The binding is the `?[0]`, reached as the receiver of the first dot.
                case ElementBindingExpressionSyntax:
                    (headIsACall, headIsAnInvocation) = (true, false);
                    return;

                // ⚠ A `!` ends the chain: everything up to and through it is the receiver, and the
                // call after it is the chain's first. The oracle writes
                // `receiver.SelfLink()!.SelfLink()` / `.SelfLink()` …, keeps
                // `source.Select(…)!.Where(beta)` whole as a one-call chain, and at
                // wrap_before_first_method_call = true writes `receiver?.SelfLink()!` / `.SelfLink()`
                // (#455, SK-DIV-0066, SK-DIV-0184). Walking through the `!` made `!.SelfLink` a second
                // call and broke there. The operand is a chain of its own — IsChainRoot does not stop
                // at a `!` — and breaks by itself: `X.Select(…)` / `.Where(gamma)!.Where(beta)` /
                // `.ToList()`.
                case PostfixUnaryExpressionSyntax:
                    return;

                default:
                    return;
            }
        }
    }

    /// <summary>
    ///     A property link — a member access or a <c>?.</c> binding — as the token a break before it
    ///     lands on and the receiver left of it; <see langword="null" /> for anything else.
    /// </summary>
    static (SyntaxToken Dot, ExpressionSyntax Receiver)? PropertyLink(SyntaxNode node) =>
        node switch {
            MemberAccessExpressionSyntax member => (member.OperatorToken, member.Expression),
            MemberBindingExpressionSyntax binding when ConditionalOf(binding) is { } owner =>
                (ChainDot(binding), owner.Expression),
            _ => null
        };

    /// <summary>
    ///     The run of property links ending at <paramref name="dot" />, walked left over member accesses
    ///     and <c>?.</c> bindings alike.
    /// </summary>
    /// <returns>
    ///     The run's first break token; the node left of the run when the walk crossed no <c>?</c>, for
    ///     the caller to recurse into — past a <c>?</c> the conditional access walks its own receiver,
    ///     and recursing as well would count its dots twice; and that node regardless.
    /// </returns>
    static (SyntaxToken Dot, ExpressionSyntax? Recurse, ExpressionSyntax Final) PropertyRun(
        SyntaxToken dot,
        ExpressionSyntax receiver,
        bool crossed = false
    ) {
        while (PropertyLink(receiver) is var (next, left)) {
            crossed |= receiver is MemberBindingExpressionSyntax;
            (dot, receiver) = (next, left);
        }

        return (dot, crossed ? null : receiver, receiver);
    }

    /// <summary>The conditional access whose <c>?</c> stands right before a binding.</summary>
    static ConditionalAccessExpressionSyntax? ConditionalOf(MemberBindingExpressionSyntax binding) =>
        binding.OperatorToken.GetPreviousToken() is {
            RawKind: (int)SyntaxKind.QuestionToken, Parent: ConditionalAccessExpressionSyntax owner
        }
            ? owner
            : null;

    /// <summary>
    ///     How many of a chain's dots <see cref="PlanChainedCalls" /> makes break points of — zero when it
    ///     plans no group at all.
    /// </summary>
    static int ChainPointCount(SyntaxNode root, in PhaseOneOptions options) {
        var (dots, headIsACall, isAChain) = ChainLinks(root, options);

        // A chain is two calls or more. The dots count the calls reached through one; a dot-less
        // call at the head is the other. ⚠ A trailing property run counts as a link, but not as a
        // call: `SomeMethod(…)` / `.Property` and `alpha.SomeMethod(…)` / `.Property` break as chains,
        // while `(\n a)[0].C` — an indexer head and a property — is left whole where `(\n a)[0]` /
        // `.C()` chops, and a run of nothing but properties is no chain at all.
        if (!isAChain) {
            return 0;
        }

        // skala_wrap_before_first_method_call = false: the first invoked dot stays with its receiver
        // — unless the first call is the dot-less head, in which case there is no dot to keep.
        // ⚠ The list is built outermost-first by the walk, so the *last* entry is the first dot of
        // the chain.
        return options.WrapBeforeFirstMethodCall || headIsACall ? dots.Count : dots.Count - 1;
    }

    /// <summary>
    ///     The gap after a member access's dot — or a <c>?.</c>'s — is joined: the name follows its dot.
    /// </summary>
    /// <remarks>
    ///     ⚠ Measured (#536) on a property, a call, a <c>?.</c>, a run of them, <c>this.</c>, a generic call
    ///     and a statement's head: the oracle joins every author's break after the dot, <c>c.</c> / <c>X</c>
    ///     comes back <c>c.X</c>, under <c>keep_user_linebreaks = true</c>. A break *before* the dot is the
    ///     chain's and is kept; a comment after the dot keeps the break, which the gap rules already do. Not
    ///     a qualified name: <c>using System.</c> / <c>Text;</c> is kept. Only where a dot is never a break
    ///     point of its own — <c>skala_wrap_after_dot_in_method_calls = false</c>, the export's value; the
    ///     other value plans the points after the dots in <see cref="PlanChainedCalls" />, which is walked
    ///     first and wins.
    /// </remarks>
    void PlanJoinAfterADot(SyntaxNode node) {
        if (options.WrapAfterDotInMethodCalls) {
            return;
        }

        var name = node switch {
            MemberAccessExpressionSyntax access => access.Name.GetFirstToken(),
            MemberBindingExpressionSyntax binding => binding.Name.GetFirstToken(),
            _ => default
        };

        if (!name.IsKind(SyntaxKind.None)
            && !name.LeadingTrivia.Concat(name.GetPreviousToken().TrailingTrivia)
                .Any(static trivia => trivia.IsKind(SyntaxKind.SingleLineCommentTrivia)
                    || trivia.IsKind(SyntaxKind.MultiLineCommentTrivia)
                )) {
            Flat(name);
        }
    }

    /// <summary>Whether <paramref name="invocation" /> is <c>nameof(…)</c>, read from syntax.</summary>
    internal static bool IsNameOf(InvocationExpressionSyntax invocation) =>
        invocation is {
            Expression: IdentifierNameSyntax { Identifier.ValueText: "nameof" }, ArgumentList.Arguments.Count: 1
        };

    /// <summary>
    ///     Whether the leftmost receiver of a chain — down the spine of invocations, member, element and
    ///     conditional accesses and postfix <c>!</c> — is a parenthesised expression or a tuple.
    /// </summary>
    /// <remarks>
    ///     ⚠ Read in two places that must agree: the chain group's <see cref="GroupPlan.HoldsLevel" />
    ///     here, for a chain with break points of its own, and the chain <em>frame</em> in
    ///     <see cref="CSharpDocumentBuilder" />, which pays for an author's break before a dot that is
    ///     not a point — `(a).B\n.C()` has one point, before `.B`, so its group is never described and
    ///     the frame is the only mechanism there.
    /// </remarks>
    /// <summary>
    ///     Whether a chain's parenthesised head makes it take its level only when nothing around it
    ///     spends one (SK-DIV-0112) — unless the chain governs a switch expression.
    /// </summary>
    /// <remarks>
    ///     ⚠ Measured for #470's last row (SK-DIV-0158): <c>=&gt;</c> / <c>(</c> / <c>a).B().C() switch {</c>,
    ///     <c>(</c> / <c>a).B</c> / <c>.C() switch</c>, <c>(a + b).C()</c> / <c>.D() switch</c> and
    ///     <c>(a + b).C</c> / <c>.D switch</c> all put the dots one level past the arrow's line, where the
    ///     same chains with no switch after them put the dots on the <c>(</c>'s column. Under <c>var x =</c>
    ///     and <c>return</c> the two agree. The switch's arms then nest from the dots' line
    ///     (<see cref="CSharpDocumentBuilder" />'s anchor at the keyword).
    /// </remarks>
    internal static bool HeadSharesTheLevelAroundIt(SyntaxNode root) =>
        ChainHeadIsParenthesised(root) && !GovernsASwitch(root);

    /// <summary>Whether <paramref name="root" /> is a switch expression's governing expression.</summary>
    internal static bool GovernsASwitch(SyntaxNode root) =>
        root.Parent is SwitchExpressionSyntax owner && owner.GoverningExpression == root;

    internal static bool ChainHeadIsParenthesised(SyntaxNode root) {
        var node = root;
        while (true) {
            switch (node) {
                case ParenthesizedExpressionSyntax or TupleExpressionSyntax:
                    return true;

                case InvocationExpressionSyntax invocation:
                    node = invocation.Expression;
                    continue;

                case MemberAccessExpressionSyntax access:
                    node = access.Expression;
                    continue;

                case ElementAccessExpressionSyntax element:
                    node = element.Expression;
                    continue;

                case ConditionalAccessExpressionSyntax conditional:
                    node = conditional.Expression;
                    continue;

                case PostfixUnaryExpressionSyntax postfix:
                    node = postfix.Operand;
                    continue;

                default:
                    return false;
            }
        }
    }

    /// <summary>
    ///     Whether a chain takes its continuation level only when nothing around it is spending one —
    ///     <c>spendsIndent</c>'s rule — rather than always, as a chain ordinarily does.
    /// </summary>
    /// <remarks>
    ///     A parenthesised head (SK-DIV-0112, see <see cref="PlanChainedCalls" />), and two positions
    ///     measured for #495 (SK-DIV-0184's residue). The whole condition of an <c>if</c>, an
    ///     <c>else if</c>, a <c>while</c> or a <c>do</c>'s <c>while</c>, where
    ///     <c>align_multiline_statement_conditions</c> puts the dots on the aligned column:
    ///     <c>if (source.Select(…)</c> / <c>.Any(p)) {</c> with the <c>.</c> under the <c>s</c>. ⚠ Only
    ///     the whole condition: <c>if (!source…</c>, <c>if (flag</c> / <c>&amp;&amp; source…</c>, and a
    ///     <c>switch (</c>, <c>foreach (… in</c> or <c>using (</c> header all put the dots one level past
    ///     the aligned column. And the body of a lambda that is the call's sole argument, kept on the
    ///     call's line, whose parenthesis already paid: <c>Use(x =&gt; source.Select(…)</c> /
    ///     <c>.Where(p)</c> one level past the statement, a parenthesised parameter list and a <c>!</c>
    ///     before the chain alike. A lambda after another argument keeps the ordinary rule (<c>Use(</c> /
    ///     <c>first,</c> / <c>x =&gt; source…</c> / <c>.Where(p)</c> one level past the lambda's line).
    /// </remarks>
    bool SharesTheLevelAroundIt(SyntaxNode root) =>
        HeadSharesTheLevelAroundIt(root)
        || root.Parent is IfStatementSyntax or WhileStatementSyntax or DoStatementSyntax
        && IsAHeaderCondition(root)
        || options.PlaceSingleMethodArgumentLambdaOnSameLine
        && IsTheBodyOfASoleLambda(root);

    /// <summary>
    ///     Whether a chain is the expression body of a lambda that is its call's sole argument — or the
    ///     operand of a prefix operator that is: <c>Use(x =&gt; !source.Select(…)</c> / <c>.Any(p)</c> sits
    ///     at the same column as without the <c>!</c>.
    /// </summary>
    static bool IsTheBodyOfASoleLambda(SyntaxNode root) {
        var body = root.Parent is PrefixUnaryExpressionSyntax prefix ? prefix : root;

        // ⚠ Not when the call is itself the receiver of a further link: there its argument list nests
        // from the outer chain's continuation line (#418) and the inner chain takes its own level past
        // it — `found.SelectMany(static d => Enumerable.Range(…)` / `.Select(…)` two levels in /
        // `)` / `.OrderByDescending(…)`, Skala's own source.
        return body.Parent is LambdaExpressionSyntax {
                Parent:
                ArgumentSyntax {
                    NameColon: null,
                    Parent:
                    ArgumentListSyntax {
                        Arguments.Count: 1,
                        Parent: InvocationExpressionSyntax { Parent: not MemberAccessExpressionSyntax }
                    }
                }
            } lambda
            && lambda.ExpressionBody == body;
    }

    /// <summary>
    ///     The token a break before a conditional link lands on: the <c>?</c>, not the <c>.</c>.
    /// </summary>
    /// <remarks>
    ///     ⚠ <c>a?.B</c> is three tokens — <c>a</c>, <c>?</c>, <c>.</c> — and only the <c>.</c> belongs to
    ///     the <see cref="MemberBindingExpressionSyntax" />; the <c>?</c> is the enclosing
    ///     <see cref="ConditionalAccessExpressionSyntax" />'s own operator. Breaking on the <c>.</c>
    ///     strands the <c>?</c> at the end of the line above:
    ///     <code>
    /// return model.GetDeclaredSymbol(current, cancellation)?
    ///     .ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
    ///     </code>
    ///     where the oracle writes <c>?.ToDisplayString(…)</c> on the wrapped line. Measured on that
    ///     exact expression, which is Skala's own <c>ArrangementSafety.ContainerOf</c>.
    ///     <para>
    ///         ⚠ Invisible until SK-DIV-0030 was fixed, and then only for a chain whose <em>receiver</em>
    ///         contributes a dot of its own. With a bare identifier receiver the binding's dot is the last
    ///         entry in the list, which <c>skala_wrap_before_first_method_call = false</c> holds back, so it is
    ///         never a break point and the token choice cannot be observed. It is observable the moment
    ///         the receiver is itself a chain — the shape three of Skala's own files carry.
    ///     </para>
    /// </remarks>
    static SyntaxToken ChainDot(MemberBindingExpressionSyntax binding) {
        var previous = binding.OperatorToken.GetPreviousToken();
        return previous.IsKind(SyntaxKind.QuestionToken) ? previous : binding.OperatorToken;
    }

    /// <summary>
    ///     The group that makes <c>chop_if_long</c> mean "chop <em>all</em> of them" for a chain whose
    ///     links each carry a group of their own.
    /// </summary>
    /// <remarks>
    ///     ⚠ It exists because the two behaviours the export asks for cannot live in one group.
    ///     <c>keep_user_linebreaks = true</c> means <c>a &amp;&amp; b\n || c</c> comes back with exactly
    ///     that one break, so each operator keeps its own <see cref="GroupMode.Preserve" /> group;
    ///     <c>skala_wrap_chained_binary_expressions = chop_if_long</c> means a chain that does not fit on one
    ///     line breaks at every operator at once, which no per-operator group can decide. This group
    ///     spans the whole chain, holds no break points of its own, and the operator groups read its
    ///     resolved mode through <see cref="GroupFacts.BreaksWithOwner" />.
    ///     <para>
    ///         ⚠ And it exists at <c>wrap_if_long</c> too, which is the correction. That value used to
    ///         return here without planning anything, so no operator ever broke for width and a
    ///         121-column condition came back whole. Measured, one key flipped, at the export's margin:
    ///         <code>
    /// if (a &gt; 0 &amp;&amp; b &gt; 0 &amp;&amp; c &gt; 0 &amp;&amp; d &gt; 0 &amp;&amp; a &lt; 100 &amp;&amp; b &lt; 100 &amp;&amp; c &lt; 100 &amp;&amp; d &lt; 100 &amp;&amp; a != b &amp;&amp; c != d
    ///     &amp;&amp; a != d) {
    ///         </code>
    ///         — one break, at the last operator that fits, and the same answer for a pattern chain. So
    ///         <c>wrap_if_long</c> is a <em>fill</em> here as it is everywhere else: the chain-wide group
    ///         still answers "does the whole chain fit on one line", and when it says no every operator
    ///         group breaks with it — but each operator's point is a fill point, so it puts its own link
    ///         on the line when the link fits and moves it down when it does not. <c>chop_*</c> keeps
    ///         ordinary points and every link moves together.
    ///     </para>
    /// </remarks>
    void PlanChainWide(SyntaxNode root, WrapStyle style) {
        // ⚠ A force-chopped condition has no use for the chain-wide group, and leaving it in place
        // gets the answer wrong. The group asks "does the whole chain fit on one line"; a
        // GroupMode.Break point inside it hides the flat width (DocumentBuilder), so the answer is
        // always no, and every operator then breaks through BreaksWithOwner — including the ones the
        // forced chop deliberately left alone. Measured: the oracle writes
        // `if (a.Flag && b.Flag\n    || c.Flag)`, and with the group still in place Skala wrote the
        // `&&` broken too. The forced chop has already decided this chain, so the question is moot.
        if (root is BinaryExpressionSyntax chain && forcedChop.Contains(chain.OperatorToken.SpanStart)) {
            return;
        }

        var group = NewGroup();
        chainOwner[Key(root)] = group;
        if (style == WrapStyle.WrapIfLong) {
            chainFills.Add(Key(root));
        }

        var pattern = root is BinaryPatternSyntax;
        Describe(
            root,
            new(
                group,
                style == WrapStyle.ChopAlways ? GroupMode.Break : GroupMode.Preserve,
                // ⚠ A pattern chain the author broke at any one link is chopped at every link, and an
                // expression chain is not (#483, SK-DIV-0124). Measured: `A or B` / `or C` comes back with
                // every `or` on its own line — in a switch arm, after `is`, in a `case` label, in an `if`
                // condition, for `and` as well as `or`, and broken at the inner link as at the outer —
                // although the whole chain fits; `a && b` / `|| c` in the same arm stays as written.
                new(
                    pattern && options.KeepsUserBreaksBetweenItems && PatternChainIsBroken(root),
                    BreaksIfTooLong: true
                ),
                // ⚠ A pattern chain spends a level of its own *and* the continuation the construct
                // around it would have spent; a binary expression chain spends only the latter. See
                // GroupPlan.OwnLevel and docs/plan/04 § "Indentation".
                //
                // ⚠ Except as a statement's condition, where `skala_align_multiline_statement_conditions` puts
                // the continuation level and the alignment at the same column and the oracle writes one
                // step, not two:
                //     if (o is IDisposable
                //         or IAsyncDisposable) {     ← one, where an argument would take two
                pattern && root.Parent is not SubpatternSyntax,
                false,
                // ⚠ And only the outermost combinator's chain: an `and` chain inside an `or` chain is a
                // chain of its own since #483, and the oracle writes its links on the `or`s' column —
                // `rune is >= 0x1100` / `and <= 0x115F` / `or >= 0x2E80` / `and <= 0x303E` all one level in
                // (Skala's own TextWidth.cs, measured).
                // ⚠ Nor a chain whose `is` the author broke before: `next.Parent` / `is A` / `or B` puts the
                // `or`s on the `is`'s own line's column (Skala's own SpaceRules.cs, measured) — that break
                // has already spent the level.
                pattern
                && !IsStatementCondition(root)
                && root.Parent is not BinaryPatternSyntax
                // ⚠ Nor a subpattern's value (#549): `is {` / `Parent: A` / `or B` / `}` puts the `or`s on
                // `Parent:`'s column, in a switch arm's braces as in an `is`'s (measured 2026-10-08).
                && root.Parent is not SubpatternSyntax
                // ⚠ Before the `is` or after it (#550): `keyword is` / `A` / `or B` puts `A` and the `or`s
                // on one column too.
                && !(EnclosingTypeTest(root) is { } test && (BreaksBefore(test.IsKeyword) || BreaksAroundTheIs(test))),
                // ⚠ And that level counts although the `&&` or `||` the type test is the left operand of
                // opened its own on the same line (#560, SK-DIV-0394): `var e = n.P is A` / `or B` /
                // `&& c;` puts the `or` at 16 and the `&&` at 12, after `return` and `var e =`, and with
                // `||`; one level per opening line had collapsed the two onto 12. Measured 2026-10-08.
                AdditiveLevel: pattern
                && EnclosingTypeTest(root) is { Parent: BinaryExpressionSyntax logical } leftTest
                && logical.Left == leftTest
                && logical.Kind() is SyntaxKind.LogicalAndExpression or SyntaxKind.LogicalOrExpression,
                HoldsLevel: pattern ? HoldForASoleLambda(root, group) : HeldLevel.None
            )
        );
    }

    /// <summary>
    ///     Whether a pattern chain is, through its <c>is</c>, the expression body of a lambda that is the
    ///     only argument of a call (#566).
    /// </summary>
    static bool IsSoleLambdaArgumentBody(SyntaxNode pattern) =>
        EnclosingTypeTest(pattern) is { } test
        && test.Parent is LambdaExpressionSyntax lambda
        && lambda.ExpressionBody == test
        && lambda.Parent is ArgumentSyntax { Parent: ArgumentListSyntax { Arguments.Count: 1 } };

    /// <summary>
    ///     A sole lambda argument's pattern chain holds its level while the arrow stays on the call's line
    ///     (<see cref="HeldLevel.WhileArrowFlat" />). ⚠ Not where more links follow the call:
    ///     <c>body.DescendantNodes(x =&gt; x is not A</c> / <c>and not B</c> / <c>)</c> / <c>.Any(…)</c> takes
    ///     two levels in the oracle, the chain's level standing beside the parenthesis's.
    /// </summary>
    HeldLevel HoldForASoleLambda(SyntaxNode pattern, int group) {
        if (!IsSoleLambdaArgumentBody(pattern)
            || EnclosingTypeTest(pattern)?.Parent is not LambdaExpressionSyntax lambda
            || lambda.Parent?.Parent?.Parent is not InvocationExpressionSyntax call
            || call.Parent is MemberAccessExpressionSyntax
            || !arrowGroups.TryGetValue(Key(lambda), out var arrow)) {
            return HeldLevel.None;
        }

        arrowHeldAgainst[group] = arrow;
        return HeldLevel.WhileArrowFlat;
    }

    /// <summary>The <c>is</c> a pattern sits under, through parenthesised and negated patterns.</summary>
    static IsPatternExpressionSyntax? EnclosingTypeTest(SyntaxNode pattern) {
        for (var current = pattern.Parent; current is not null; current = current.Parent) {
            switch (current) {
                case PatternSyntax:
                    continue;
                case IsPatternExpressionSyntax test:
                    return test;
                default:
                    return null;
            }
        }

        return null;
    }

    /// <summary>
    ///     Whether the author broke a binary pattern chain at one of its links' break points — before
    ///     the combinator at <c>skala_wrap_before_binary_pattern_op = true</c>, after it otherwise. A break
    ///     on the other side of the combinator is not the chain's and does not count: the oracle joins
    ///     <c>A or</c> / <c>B or C</c> whole.
    /// </summary>
    bool PatternChainIsBroken(SyntaxNode root) {
        foreach (var node in root.DescendantNodesAndSelf(static node => node is BinaryPatternSyntax)) {
            if (node is BinaryPatternSyntax link
                && ChainRootOf(link) == root
                && BreaksBefore(options.WrapBeforeBinaryPatternOp ? link.OperatorToken : FirstToken(link.Right))) {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    ///     <c>skala_wrap_before_binary_opsign = true</c>: the operator starts the new line, so the gap before
    ///     it is the break point and the gap after it is not one.
    /// </summary>
    /// <remarks>
    ///     ⚠ One group per operator, not one per chain. The oracle keeps the author's break points
    ///     individually: <c>a &amp;&amp; b \n || c</c> comes back unchanged rather than chopped at both
    ///     operators. A chain-wide group would break every operator as soon as one of them was broken,
    ///     which is what <c>chop_if_long</c> does <em>once the chain is being re-wrapped</em> — and
    ///     choosing to re-wrap it is milestone 3's.
    /// </remarks>
    /// <summary>
    ///     <c>force_chop_compound_{if,while,do}_expression = true</c>: a compound statement condition is
    ///     chopped at every operator of its root chain, however well it fits on one line.
    /// </summary>
    /// <remarks>
    ///     ⚠ "Compound" is much narrower than the name, and every clause below is measured against
    ///     <c>jb cleanupcode</c> 2025.2.6 at the repository's margin, one key at a time, on conditions
    ///     that all fit comfortably — so nothing here is the fitter's doing.
    ///     <list type="number">
    ///         <item>
    ///             Only <c>&amp;&amp;</c> and <c>||</c>. <c>if (a.Count &gt; b.Count)</c>,
    ///             <c>if (a.Name == b.Name)</c> and <c>if (a.Flag &amp; b.Flag)</c> do not move — a
    ///             relational root and the single-<c>&amp;</c> bitwise root are both left alone — and
    ///             neither does the pattern combinator in <c>if (a.Inner is string or int)</c>.
    ///         </item>
    ///         <item>
    ///             Only the <em>root operator's own kind</em>, which is why this cannot ride on the
    ///             chain-wide group: <c>if (a.P &amp;&amp; b.P || c.P)</c> comes back as
    ///             <c>a.P &amp;&amp; b.P</c> / <c>|| c.P</c>, broken at the <c>||</c> and whole at the
    ///             <c>&amp;&amp;</c>. Same for the <c>&amp;&amp;</c> nested inside an argument:
    ///             <c>if (Take(a.P &amp;&amp; b.P) &amp;&amp; c.P)</c> breaks at the outer operator only.
    ///         </item>
    ///         <item>
    ///             ⚠ A two-operand chain is chopped <em>only if neither operand is a single token</em>,
    ///             and this is the clause no reading of the option name produces. <c>if (a &amp;&amp; b)</c>,
    ///             <c>if (a &amp;&amp; b.P)</c>, <c>if (a &gt; 0 &amp;&amp; b)</c>, <c>if (F() &amp;&amp; b)</c>
    ///             and <c>if (a &amp;&amp; true)</c> all stay on one line, because one side is a bare name
    ///             or literal; <c>if (a.P &amp;&amp; b.P)</c>, <c>if (F() &amp;&amp; G())</c>,
    ///             <c>if (!a &amp;&amp; !b)</c>, <c>if (a[0] &amp;&amp; b[0])</c>,
    ///             <c>if ((bool)a &amp;&amp; (bool)b)</c>, <c>if ((a) &amp;&amp; (b))</c> and
    ///             <c>if (a is string &amp;&amp; b is string)</c> all chop. It is not a width rule:
    ///             <c>a &gt; 0 &amp;&amp; b</c> and <c>F() &amp;&amp; G()</c> are the same ten columns and
    ///             go opposite ways, and two very long identifiers still do not chop.
    ///         </item>
    ///         <item>Three or more operands always chop, whatever the operands are.</item>
    ///     </list>
    ///     ⚠ The continuation column is not this rule's business: <c>skala_align_multiline_statement_conditions</c>
    ///     already puts it after the <c>(</c>, which is what the oracle writes for all three statements
    ///     — including <c>} else if (</c> and <c>} while (</c>, whose openers sit further right.
    /// </remarks>
    void PlanForcedChopCondition(SyntaxNode node) {
        var condition = node switch {
            IfStatementSyntax statement when options.ForceChopCompoundIfExpression => statement.Condition,
            WhileStatementSyntax statement when options.ForceChopCompoundWhileExpression => statement.Condition,
            DoStatementSyntax statement when options.ForceChopCompoundDoExpression => statement.Condition,
            _ => null
        };

        if (condition is not BinaryExpressionSyntax root
            || !root.OperatorToken.IsKind(SyntaxKind.AmpersandAmpersandToken)
            && !root.OperatorToken.IsKind(SyntaxKind.BarBarToken)) {
            return;
        }

        // The run of the root operator's own kind, down the left spine. `a || b || c` is one run of
        // three operands; `a && b || c` is a run of two, whose left operand is the whole `a && b`.
        var kind = root.OperatorToken.Kind();
        var operators = new List<SyntaxToken>();
        var operands = new List<ExpressionSyntax>();
        var current = root;
        while (true) {
            operators.Add(current.OperatorToken);
            operands.Add(current.Right);
            if (current.Left is BinaryExpressionSyntax next && next.OperatorToken.IsKind(kind)) {
                current = next;
                continue;
            }

            operands.Add(current.Left);
            break;
        }

        if (operands.Count == 2 && (IsSingleToken(operands[0]) || IsSingleToken(operands[1]))) {
            return;
        }

        foreach (var operatorToken in operators) {
            forcedChop.Add(operatorToken.SpanStart);
        }
    }

    /// <summary>Whether an expression is one token — a bare name or a literal.</summary>
    /// <remarks>
    ///     ⚠ Counted rather than matched on node kind, because the measurement is about token count and
    ///     not about which syntax node Roslyn produced: <c>(a)</c> is a parenthesised name and chops,
    ///     <c>a</c> does not, and a list of "simple" node kinds is a second place for that to drift.
    /// </remarks>
    static bool IsSingleToken(SyntaxNode node) {
        using var tokens = node.DescendantTokens().GetEnumerator();
        return tokens.MoveNext() && !tokens.MoveNext();
    }

    void PlanOperator(SyntaxNode node, SyntaxToken operatorToken, SyntaxNode right, bool wrapBefore) {
        if (operatorToken.IsKind(SyntaxKind.None)) {
            return;
        }

        // ⚠ An operator break is undelimited, so the enclosing statement's continuation level is
        // what it lands on. Milestone 1 spent that level from inside its own Break path; a break
        // point has to ask for it explicitly or `return a\n + b;` comes out flush with the `return`.
        var group = NewGroup();

        // ⚠ `wrap_chained_binary_* = wrap_if_long` makes this link's gap a fill point: the chain-wide
        // group still decides whether the chain is being wrapped at all, and each link then decides
        // for itself whether it still fits on the line. See PlanChainWide.
        // ⚠ A break the author wrote is pinned rather than filled, the same correction
        // `PlanList`'s `pinsItemBreaks` makes — `return a && b\n    || c;` comes back from the
        // oracle unchanged at `wrap_if_long`, and a fill point would have re-joined it.
        var fill = chainFills.Contains(Key(ChainRootOf(node)));
        var group0 = group;
        bool broken;
        // ⚠ An author's break after `is` or `as` is kept (#443): the oracle writes `o is` / `P` and
        // `o as` / `P` at the defaults, with the point before the operator left whole, and joins both
        // only at keep_user_linebreaks = false — where the gap is planned as below and the builder's
        // JoinsWithoutKeptBreaks joins it. Flat joined them at both values.
        if (wrapBefore
            && operatorToken.Kind() is SyntaxKind.IsKeyword or SyntaxKind.AsKeyword
            && options.KeepsUserBreaksBetweenItems
            && BreaksBefore(FirstToken(right))) {
            Flat(operatorToken);
            Mandatory(FirstToken(right));
            return;
        }

        if (wrapBefore) {
            broken = Link(operatorToken);
            if (!operatorToken.IsKind(SyntaxKind.IsKeyword) && !operatorToken.IsKind(SyntaxKind.AsKeyword)) {
                Flat(FirstToken(right));
            }
        } else {
            Flat(operatorToken);
            broken = Link(FirstToken(right));
        }

        bool Link(SyntaxToken gap) {
            var broke = BreaksBefore(gap);
            if (fill && broke && options.KeepsUserBreaksBetweenItems) {
                Mandatory(gap);
            } else {
                Point(gap, group0, fill);
            }

            return broke;
        }

        Describe(
            node,
            group,
            // ⚠ Break rather than Preserve, and it outranks everything below: a forced chop is not
            // "break if too long" but "break", which is the whole content of the three
            // `force_chop_compound_*` keys. The facts stay as they are, so the group behaves exactly
            // as it did when the keys are off — which is the export's own configuration.
            forcedChop.Contains(operatorToken.SpanStart) ? GroupMode.Break : GroupMode.Preserve,
            new(
                options.KeepsUserBreaksBetweenItems && broken,
                // ⚠ Deliberately *not* HidesFlatWidthWhenBroken. An argument list around a chain the
                // author broke does chop — `Use(a > 0\n && b > 0)` comes back with the argument on a
                // line of its own — but hiding the flat width is not how to get it: an operator group
                // is nested inside the next operator's group, so an unbreakable inner one makes the
                // outer one break too, and `a && b\n || c` comes back chopped at both operators
                // instead of unchanged. Measured: it costs `breaks/binary-operators.cs` and
                // `wrapping/binary-chains.cs`, and buys 0.01 points. SK-DIV-0007.
                //
                // ⚠ Read together with the mode above: when the forced chop set this group to Break,
                // the owner is irrelevant — Fitter.Decide answers Broken before it looks at any fact.
                BreaksWithOwner: true,
                Owner: ChainOwnerOf(node),
                ChainLink: true,
                // ⚠ A block on the chain's first line nests from the operator's continuation line
                // once the operator broke (SK-DIV-0148), and so does an argument list (#418,
                // SK-DIV-0184). Measured for `+`, `==`, `&&`, `??` and with the operator's level paid
                // by a grouping parenthesis; a ternary's break does not lift a block in its condition
                // and does not carry the fact. ⚠ Nor does a fill's, whose group resolving broken says
                // nothing about this operator: `source.F(x => {` … `}) ?? other` at `wrap_if_long`
                // keeps the body one level in (SK-DIV-0185).
                Continues: !fill
            ),
            // ⚠ Except a pattern chain that is a subpattern's value (#549): `is {` / `Parent: A` /
            // `or B` / `}` puts the `or`s on `Parent:`'s column — the subpattern's break spends no
            // level either (SK-DIV-0081).
            ChainRootOf(node).Parent is not SubpatternSyntax
        );
    }

    /// <summary>
    ///     Whether <paramref name="node" /> is the whole condition of an <c>if</c>, <c>while</c>, <c>do</c> or
    ///     <c>for</c>, whose aligned column is the level a type test's break lands on.
    /// </summary>
    static bool IsAHeaderCondition(SyntaxNode node) =>
        node.Parent switch {
            IfStatementSyntax header => header.Condition == node,
            WhileStatementSyntax header => header.Condition == node,
            DoStatementSyntax header => header.Condition == node,
            ForStatementSyntax header => header.Condition == node,
            _ => false
        };

    /// <summary>
    ///     Whether a pattern has no break point of its own — a type, a constant, a declaration, a
    ///     relational or a negated one — so the only place an <c>is</c> before it can wrap is the keyword.
    /// </summary>
    /// <remarks>
    ///     ⚠ A pattern with points of its own — <c>or</c>/<c>and</c>, a property or list pattern, a
    ///     parenthesized one — wraps inside itself first: <c>is not (A</c> / <c>or B)</c>, never
    ///     <c>is</c> / <c>not (A or B)</c>. Planning the keyword there broke the #418 chain fixture.
    /// </remarks>
    static bool IsUnbreakablePattern(PatternSyntax pattern) =>
        !pattern.DescendantNodesAndSelf()
            .Any(static node => node is BinaryPatternSyntax
                or RecursivePatternSyntax
                or ListPatternSyntax
                or ParenthesizedPatternSyntax
            );

    /// <summary>
    ///     Whether the author broke the line on either side of an <c>is</c> whose pattern can break —
    ///     a combinator chain, a property, positional or list pattern, a parenthesis.
    /// </summary>
    bool BreaksAroundTheIs(IsPatternExpressionSyntax test) =>
        options.KeepsUserBreaksBetweenItems
        // ⚠ Not after a chain headed by a parenthesis, which holds its level (SK-DIV-0112): `|| (b ?? c).D`
        // / `is not {` and `(b ?? c).D` / `is A or B` keep the `is` on the operand's column, where
        // `|| b.D` / `is not {` takes the level (measured 2026-10-08; Skala's own
        // ConcurrentDictionaryMemberAnalyzer.cs).
        && !(IsChainRoot(test.Expression) && ChainHeadIsParenthesised(test.Expression))
        && (BreaksBefore(test.IsKeyword)
            // ⚠ A break before a pattern's `[` or `{` is not kept: `xs is` / `[1, 2]` comes back joined
            // (constructs/wrapping/patterns.cs).
            || BreaksBefore(FirstToken(test.Pattern))
            && (FirstToken(test.Pattern).Kind() is not (SyntaxKind.OpenBracketToken or SyntaxKind.OpenBraceToken)
                || IsAOneLineListPatternAfterIs(FirstToken(test.Pattern))));

    /// <summary>
    ///     An <c>is</c> the author broke before or after, over a pattern that can break: one level past
    ///     the operand's line, which the pattern under it then shares (#550).
    /// </summary>
    /// <remarks>
    ///     ⚠ <see cref="PlanTypeTest" />'s level, which only an unbreakable pattern used to get. Measured
    ///     2026-10-08 with <c>Testing ask</c>: <c>keyword is</c> / <c>A</c> / <c>or B</c> and
    ///     <c>keyword</c> / <c>is A</c> / <c>or B</c> put <c>A</c> (or the <c>is</c>) and every <c>or</c>
    ///     one level past <c>keyword</c>'s line — under an expression body's arrow, after <c>return</c>,
    ///     after <c>var b =</c>, in an argument and under an <c>&amp;&amp;</c> alike — where Skala wrote the
    ///     first line flush with <c>keyword</c> under the arrow (whose break had already spent the
    ///     member's level and there was no group to spend another) and stepped the <c>or</c>s a second
    ///     level after <c>return</c>. The pattern chain's own level is then the one the broken
    ///     <c>is</c> has spent (<see cref="EnclosingTypeTest" />, #520). Found reformatting Skala's own
    ///     <c>SpaceRules.cs</c>.
    /// </remarks>
    void PlanBrokenTypeTest(IsPatternExpressionSyntax node) {
        var group = NewGroup();
        if (BreaksBefore(node.IsKeyword)) {
            Mandatory(node.IsKeyword);
        }

        var after = FirstToken(node.Pattern);
        if (BreaksBefore(after)) {
            Mandatory(after);
        }

        Describe(
            node,
            new(
                group,
                GroupMode.Preserve,
                new(BreaksIfTooLong: true),
                FromLine: !IsAHeaderCondition(node)
            )
        );
    }

    /// <summary>The <c>[</c> of a list pattern written on one line straight after an <c>is</c>.</summary>
    bool IsAOneLineListPatternAfterIs(SyntaxToken open) =>
        open.Parent is ListPatternSyntax { Parent: IsPatternExpressionSyntax } list
        && open.GetPreviousToken().IsKind(SyntaxKind.IsKeyword)
        && !HasLineBreakIn(list);

    bool HasLineBreakIn(SyntaxNode node) => source.AsSpan(node.Span.Start, node.Span.Length).Contains('\n');

    /// <summary>Whether a binary expression is <c>is</c> or <c>as</c> with a type on its right.</summary>
    static bool IsTypeTest(BinaryExpressionSyntax binary) =>
        binary.IsKind(SyntaxKind.IsExpression) || binary.IsKind(SyntaxKind.AsExpression);

    /// <summary>
    ///     <c>is</c> and <c>as</c>: the break point is <em>after</em> the keyword, whatever
    ///     <c>skala_wrap_before_binary_opsign</c> says, and a break the author wrote before it is kept.
    /// </summary>
    /// <remarks>
    ///     ⚠ Measured (#440), and it is not the binary operators' rule although Roslyn calls both a
    ///     binary expression. Past the margin the oracle writes <c>…PropertyName is</c> /
    ///     <c>SomeTypeName;</c> one level past the operand's line, after an <c>=</c> break that did not
    ///     suffice and under an <c>&amp;&amp;</c> alike; it breaks after the <c>=</c> instead when that
    ///     alone fits; <c>o</c> / <c>is string</c> written broken is kept; and a comment that spans lines
    ///     after the keyword keeps the type on its last line (<c>o is /* a</c> / <c>b */ string</c>).
    ///     Planned as a binary operator before it, the break went before the keyword, and the comment's
    ///     unbounded width took it on every line that held one.
    /// </remarks>
    void PlanTypeTest(SyntaxNode node, SyntaxToken keyword, SyntaxNode right) {
        var group = NewGroup();
        var after = FirstToken(right);
        var broken = BreaksAfterTheLastCommentIn(source, after);
        if (broken && options.KeepsUserBreaksBetweenItems) {
            Mandatory(after);
        } else {
            Point(after, group, lastResort: true);
        }

        // ⚠ The level covers a kept break *before* the keyword too: `|| x` / `is null` and
        // `… is not T` / `declaration` sit one level past the operand's line in the oracle (#440).
        // ⚠ And it is one level past that *line*, not past everything open on it (#445): inside a lambda
        // that is an argument, `nodes.Count(c => c.Parent` / `is ArgumentSyntax` is four columns in, not
        // eight. IndentKind.FromLine.
        Describe(
            node,
            new(
                group,
                GroupMode.Preserve,
                // ⚠ A list on the operand's line nests from the keyword's line only when the author broke
                // before the keyword: `Compute(` / … / `)` / `is string` keeps its arguments two levels in,
                // `Compute(` / … / `) is string` one (#445). Resolving broken is not enough, because this
                // group resolves broken whenever the expression is too long, whether or not it wraps.
                new(BreaksIfTooLong: true, Continues: BreaksBefore(keyword)),
                FromLine: !IsAHeaderCondition(node)
            )
        );

        // ⚠ The break goes *before* the keyword exactly when the operand fits on its line and the operand
        // with the keyword does not (#444, SK-DIV-0210). Measured on `return <operand> as string;` a
        // column at a time: with the operand ending at 117 the oracle writes `… as` / `string;`, at 118,
        // 119 and 120 it writes `…` / `as string;`, and past 120 it wraps inside the operand. A break the
        // author wrote there is theirs, and is kept unplanned as before.
        if (node is BinaryExpressionSyntax && !BreaksBefore(keyword)) {
            var before = NewGroup();
            Point(keyword, before, lastResort: true);
            Describe(node, before, GroupMode.Preserve, new(KeywordWidth: keyword.Span.Length));
        }
    }

    /// <summary>
    ///     A property-pattern subpattern's own break point: after its <c>:</c>, landing on the
    ///     subpattern's own column.
    /// </summary>
    /// <remarks>
    ///     ⚠ SK-DIV-0081, and the entry was left open on one question: every other undelimited
    ///     continuation in this formatter spends a level and this one spends none, which was
    ///     "measured once and nowhere else". It is measured three times now, in configurations that do
    ///     not share a cause — aligned at the export's margin, un-aligned at a 60-column margin, and
    ///     nested inside another property pattern — and the value lands on the subpattern's own column
    ///     in all three:
    ///     <code>
    /// var matched = candidate is {          var matched = candidate is {
    ///                                OnlySubpatternPropertyName:      OnlySubpatternPropertyName:
    ///                                "a string long enough …"         "a string long enough …"
    ///                            };         };
    /// align = true, margin 120              align = false, margin 60
    ///     </code>
    ///     ⚠ That also refutes the entry's "reachable only under alignment": it is reachable un-aligned
    ///     at any margin the subpattern overflows, and the export's 120 was simply wider than the
    ///     fixture's line.
    ///     <para>
    ///         ⚠ <c>spendsIndent</c> is left at its default of <see langword="false" />, which is the whole
    ///         of the finding. A positional subpattern has no <c>:</c> and gets no point.
    ///     </para>
    /// </remarks>
    void PlanSubpattern(SubpatternSyntax subpattern) {
        var colon = subpattern.ExpressionColon?.ColonToken ?? default;
        if (colon.IsKind(SyntaxKind.None)) {
            return;
        }

        var value = FirstToken(subpattern.Pattern);
        if (KeepsTheBreakBefore(colon, value)) {
            return;
        }

        var group = NewGroup();
        Flat(colon);
        Point(value, group);

        Describe(
            subpattern,
            group,
            GroupMode.Preserve,
            new(
                // ⚠ Not a break before a bare `{`, which the oracle joins (#549): `Parameter:` / `{ … }`
                // comes back `Parameter: {` / … / `}` in Skala's own PrimaryConstructorWrites.cs, where
                // `Expression:` / `MemberAccessExpressionSyntax { … }` beside it keeps its break.
                options.KeepsUserBreaksBetweenItems && BreaksBefore(value) && !value.IsKind(SyntaxKind.OpenBraceToken),
                BreaksIfTooLong: true,

                // ⚠ The arrow's question, always: the value leaves the name's line only when the line up
                // to the value's first break point has no room (#549). Measured 2026-10-08 with
                // `Testing ask`, found in Skala's own PrimaryConstructorWrites.cs and SpaceRules.cs:
                // `Parameter: {` / the subpatterns / `}` and `Parent: InvocationExpressionSyntax {` / … /
                // `}` break the value's braces open where Skala moved the whole value below the name, and
                // `Parent: A` / `or B` / `or C` chops the chain beside the name. It was asked only for a
                // kept break before a colon inside the value, which is a hard line that would otherwise
                // break this group too: `Q: {` / `X` / `: 1` came out `Q:` / `{` (#436). A value too wide
                // at its own head — a string, a dotted name — still moves down, as SK-DIV-0081 measured.
                // It subsumes #532's reading too — `{ X: (2` / `, 3) }` keeps `X: (2` on one line, where a
                // kept break inside the value made this group break after `X:`.
                BreaksOnlyIfHeadOverflows: true
            )
        );
    }

    /// <summary>
    ///     A named argument's own break point: after its <c>name:</c>, landing on the argument's own
    ///     column — an invocation's, an object creation's, an element access's and an attribute's
    ///     alike.
    /// </summary>
    /// <remarks>
    ///     ⚠ There was no point there at all (#411), so <c>name176: Cast&lt;…&gt;(</c> past the margin
    ///     filled the type argument list where the oracle writes <c>name176:</c> / <c>Cast&lt;…&gt;(</c>,
    ///     and a string or an identifier too wide for the argument's line stayed beside its name. The
    ///     oracle breaks after the colon by the arrow's rule, the ordering rule's second question
    ///     alone (<see cref="GroupFacts.BreaksOnlyIfHeadOverflows" />): the value moves down exactly
    ///     when the line up to the value's first break point has no room — a string, an identifier, a
    ///     binary operand that runs past the margin, a call whose <c>(</c> does — and stays when it
    ///     has, however much of the value would have fitted below: <c>name: Compute(</c>,
    ///     <c>name: source.Select(</c>, <c>name: new Widget(</c>, <c>name: x =&gt; Compute(</c>,
    ///     <c>name: [</c> and <c>name: flag</c> / <c>? a</c> are never broken. The value lands on the
    ///     argument's own column, without a continuation level, like a property pattern's subpattern
    ///     (SK-DIV-0081). An author's break after the colon is kept even when the line fits joined.
    ///     A trailing comma counts. Measured on eleven value shapes at two block depths; the boundary
    ///     inside a type argument list is SK-DIV-0177.
    /// </remarks>
    void PlanArgumentName(SyntaxNode argument, SyntaxToken colon, ExpressionSyntax value) {
        var first = FirstToken(value);
        if (colon.IsKind(SyntaxKind.None) || first.IsKind(SyntaxKind.None)) {
            return;
        }

        if (KeepsTheBreakBefore(colon, first)) {
            return;
        }

        var group = NewGroup();
        Flat(colon);
        Point(first, group);

        Describe(
            argument,
            group,
            GroupMode.Preserve,
            new(
                options.KeepsUserBreaksBetweenItems && BreaksBefore(first),
                BreaksIfTooLong: true,
                BreaksOnlyIfHeadOverflows: true,
                YieldsToOverflowingTypeArguments: ColonFloorOf(argument, value) is var (floor, _) && floor >= 0,
                ColonFloor: ColonFloorOf(argument, value).Floor,
                ColonFloorSlope: ColonFloorOf(argument, value).Slope,
                ColonEdgeFloor: ColonEdgeFloorOf(argument, value)
            )
        );
    }

    /// <summary>
    ///     The argument list width from which a named argument's colon breaks before a generic call whose
    ///     type arguments run past the margin, and how it moves with the head (#490, SK-DIV-0177); −1 when
    ///     the value is not such a call.
    /// </summary>
    /// <remarks>
    ///     ⚠ A table, because no model of it survived: measured with <c>Testing ask</c> one argument list
    ///     width at a time (2 columns apart, 30 to 200) for name lengths 3 to 12, first type arguments of
    ///     2, 8, 14, 20, 25 and 40 columns, the <c>&gt;</c> at 121 to 140 and item indents 12, 16 and 20.
    ///     Every row is a clean threshold: below it the oracle fills the type argument list and above it
    ///     breaks after the colon. A name of three or fewer never breaks the colon and one of eleven or
    ///     more always does; between, the floor falls with the name and rises with the first type argument
    ///     up to 20 columns, and is independent of the <c>&gt;</c>'s column and the indent — except at four,
    ///     where it rises 1.6 columns for every column of head past 118. Interpolated linearly between the
    ///     measured first-argument widths; the floor is the first width measured breaking, less one.
    /// </remarks>
    /// <summary>
    ///     <see cref="ColonFloorOf" />'s floor for the one column where the <c>&gt;</c> ends on the margin
    ///     and only the call's <c>(</c> overflows (#490).
    /// </summary>
    /// <remarks>
    ///     ⚠ Measured on its own grid (names of 3 to 11, first type arguments of 1, 8 and 25 columns,
    ///     argument lists of 6 to 68): from a name of five the colon always breaks, and a name of three
    ///     or four fills the type arguments below a floor 32 columns apart — 40, 44 and 50 for three, 8,
    ///     12 and 18 for four.
    /// </remarks>
    static int ColonEdgeFloorOf(SyntaxNode argument, ExpressionSyntax value) {
        var (floor, _) = ColonFloorOf(argument, value);
        if (floor < 0) {
            return 0;
        }

        var n = argument switch {
            ArgumentSyntax { NameColon: { } colon } => colon.Name.Identifier.Span.Length,
            AttributeArgumentSyntax { NameColon: { } colon } => colon.Name.Identifier.Span.Length,
            _ => 0
        };

        if (n >= 5) {
            return 0;
        }

        var generic = value switch {
            InvocationExpressionSyntax { Expression: GenericNameSyntax name } => name,
            InvocationExpressionSyntax {
                Expression: MemberAccessExpressionSyntax { Name: GenericNameSyntax name }
            } => name,
            _ => null
        };

        var f = Math.Clamp(generic!.TypeArgumentList.Arguments[0].Span.Length, 1, 25);
        var four = f <= 8 ? 8 + (f - 1) * 4 / 7 : 12 + (f - 8) * 6 / 17;
        return (n == 4 ? four : four + 32) - 1;
    }

    static (int Floor, int Slope) ColonFloorOf(SyntaxNode argument, ExpressionSyntax value) {
        var generic = value switch {
            InvocationExpressionSyntax { Expression: GenericNameSyntax name } => name,
            InvocationExpressionSyntax {
                Expression: MemberAccessExpressionSyntax { Name: GenericNameSyntax name }
            } => name,
            _ => null
        };

        var nameColon = argument switch {
            ArgumentSyntax { NameColon: { } colon } => colon.Name.Identifier,
            AttributeArgumentSyntax { NameColon: { } colon } => colon.Name.Identifier,
            _ => default
        };

        if (generic is not { TypeArgumentList.Arguments: [var firstType, _, ..] }
            || nameColon.IsKind(SyntaxKind.None)) {
            return (-1, 0);
        }

        var n = nameColon.Span.Length;
        if (n <= 3) {
            return (int.MaxValue / 4, 0);
        }

        if (n >= 11) {
            return (0, 0);
        }

        // Rows: name length 4 … 10; columns: first type argument 2, 8, 14, 20 and 40 columns (and wider).
        ReadOnlySpan<int> widths = [2, 8, 14, 20, 40];
        ReadOnlySpan<int> table = [
            128, 136, 146, 152, 152,
            96, 100, 102, 106, 108,
            82, 84, 88, 90, 90,
            70, 74, 78, 80, 80,
            62, 66, 68, 72, 72,
            56, 59, 61, 64, 64,
            50, 54, 56, 60, 60
        ];

        var f = Math.Clamp(firstType.Span.Length, widths[0], widths[^1]);
        var column = 0;
        while (column < widths.Length - 2 && f > widths[column + 1]) {
            column++;
        }

        var row = (n - 4) * widths.Length;
        var low = table[row + column];
        var high = table[row + column + 1];
        var first = low + (high - low) * (f - widths[column]) / (widths[column + 1] - widths[column]);
        return (first - 1, n == 4 ? 160 : 0);
    }

    /// <summary>
    ///     The token an expression-bodied declaration's head begins at: its first token after the
    ///     attribute lists — the first modifier, or the return type, or the accessor's keyword.
    /// </summary>
    /// <remarks>
    ///     ⚠ After the attributes, measured: <c>[Obsolete]\npublic int M() =&gt; 1;</c> keeps the arrow
    ///     inline and <c>public\nint O() =&gt; 1;</c> breaks it, so the attribute's line is not the head's
    ///     and the modifier's is (#372). Nothing in the tree starts at that token, which is why the head
    ///     is a marker at a position rather than a group on a node — see <see cref="markers" />.
    /// </remarks>
    static SyntaxToken HeadStartOf(ArrowExpressionClauseSyntax node) =>
        node.Parent is null ? default : FirstTokenAfterAttributes(node.Parent);

    /// <summary>Whether this expression is the condition of an if, while, do, for or switch.</summary>
    internal static bool IsStatementCondition(SyntaxNode node) {
        for (var current = node; current is not null; current = current.Parent) {
            switch (current.Parent) {
                case IfStatementSyntax statement when statement.Condition == current:
                case WhileStatementSyntax statement2 when statement2.Condition == current:
                case DoStatementSyntax statement3 when statement3.Condition == current:
                case SwitchStatementSyntax statement4 when statement4.Expression == current:
                    return true;

                // ⚠ Through the pattern's own nesting — a parenthesised or negated pattern, the `is`, a
                // grouping parenthesis — and never through an operand of a binary (#520). Measured: the
                // oracle writes `if (o is not (Alpha` / `or Beta))` and `if (o is (Alpha` / `or Beta))`
                // with `or` on the condition's column (and `while (` aligns it to 15), while
                // `if (x && o is Alpha` / `or Beta)` and `if (x` / `|| o is Alpha` / `or Beta)` put it one
                // level past the operand's line, as anywhere else.
                case PatternSyntax
                    or IsPatternExpressionSyntax
                    or ParenthesizedExpressionSyntax
                    or PrefixUnaryExpressionSyntax:
                    continue;

                default:
                    return false;
            }
        }

        return false;
    }

    /// <summary>The chain-wide group of the chain this operator belongs to, or −1.</summary>
    int ChainOwnerOf(SyntaxNode node) => chainOwner.TryGetValue(Key(ChainRootOf(node)), out var group) ? group : -1;

    /// <summary>The outermost link of the chain this operator belongs to.</summary>
    static SyntaxNode ChainRootOf(SyntaxNode node) {
        var root = node;
        while (SameChain(root.Parent, root)) {
            root = root.Parent!;
        }

        return root;
    }

    /// <summary>
    ///     The outermost link of a chain of same-precedence binary operators or patterns.
    /// </summary>
    /// <remarks>
    ///     ⚠ Internal because the builder needs the same answer: <c>align_multiline_binary_*</c> anchors
    ///     the whole chain to one column, and "the whole chain" is exactly this node. A second copy of
    ///     the precedence test in the builder would be one place for the two to drift apart.
    /// </remarks>
    internal static bool IsChainRootOperator(SyntaxNode node) => !SameChain(node.Parent, node);

    /// <summary>
    ///     Whether two nested binary nodes belong to the same chain.
    /// </summary>
    /// <remarks>
    ///     ⚠ Same <em>precedence</em>, not merely "both are binary expressions", and getting this wrong
    ///     is visible immediately. <c>a &gt; 0 &amp;&amp; b &gt; 0 &amp;&amp; c &gt; 0</c> is one chain
    ///     of <c>&amp;&amp;</c> whose operands happen to be comparisons; the oracle chops it at the
    ///     <c>&amp;&amp;</c>s and nowhere else. Treating every nested binary as part of the chain makes
    ///     the comparisons break too, and produces
    ///     <code>
    /// if (a
    ///     &gt; 0
    ///     &amp;&amp; b
    ///     </code>
    ///     which is not a shape any formatter writes.
    /// </remarks>
    static bool SameChain(SyntaxNode? parent, SyntaxNode child) =>
        (parent, child) switch {
            (BinaryExpressionSyntax outer, BinaryExpressionSyntax inner) =>
                !IsTypeTest(outer)
                && !IsTypeTest(inner)
                && Precedence(outer.OperatorToken.Kind()) == Precedence(inner.OperatorToken.Kind()),
            // ⚠ The same combinator, which is the same precedence: `and` binds tighter than `or`, and
            // `A and B or C` / `or D` comes back from the oracle chopped at the `or`s with `A and B`
            // whole (#483). Every pattern chain measured before that was of one combinator.
            (BinaryPatternSyntax outer, BinaryPatternSyntax inner) =>
                outer.IsKind(inner.Kind()),
            _ => false
        };

    /// <summary>C#'s binary precedence levels, coarse enough to name a chain and no finer.</summary>
    static int Precedence(SyntaxKind kind) =>
        kind switch {
            SyntaxKind.AsteriskToken or SyntaxKind.SlashToken or SyntaxKind.PercentToken => 1,
            SyntaxKind.PlusToken or SyntaxKind.MinusToken => 2,
            SyntaxKind.LessThanLessThanToken
                or SyntaxKind.GreaterThanGreaterThanToken
                or SyntaxKind.GreaterThanGreaterThanGreaterThanToken => 3,
            SyntaxKind.LessThanToken
                or SyntaxKind.GreaterThanToken
                or SyntaxKind.LessThanEqualsToken
                or SyntaxKind.GreaterThanEqualsToken => 4,
            SyntaxKind.EqualsEqualsToken or SyntaxKind.ExclamationEqualsToken => 5,
            SyntaxKind.AmpersandToken => 6,
            SyntaxKind.CaretToken => 7,
            SyntaxKind.BarToken => 8,
            // ⚠ `&&` and `||` are two chains, not one (#565). The note here said "`a && b || c` is
            // chopped at both operators by the oracle", and that holds only where the `&&` is broken
            // already: measured 2026-10-08 on six conditions too wide for their line — in an `if`, an
            // `=` and a `return`, with the `&&` before, after and between the `||`s — the oracle chops
            // every `||` and leaves each `&&` operand whole on its line (`a && b` / `|| c && d` /
            // `|| e && a`). An `&&` the author broke still breaks its `||`, by containment.
            SyntaxKind.AmpersandAmpersandToken => 9,
            SyntaxKind.BarBarToken => 10,
            SyntaxKind.QuestionQuestionToken => 11,
            _ => 12
        };

    /// <summary>
    ///     Every conditional of a chain, outermost first: <c>a ? x : b ? y : z</c> is two members.
    /// </summary>
    /// <remarks>
    ///     ⚠ The chain runs through <see cref="ConditionalExpressionSyntax.WhenFalse" /> and nowhere
    ///     else, and it does not see through parentheses. Both halves are measured:
    ///     <c>a ? (b ? x : y) : z</c> nests on the <em>true</em> side and the oracle lays it out as a
    ///     single conditional — <c>a\n ? b ? x : y\n : z</c> — and so does
    ///     <c>a ? x : (b ? y : z)</c>, whose tail is a parenthesised expression rather than a
    ///     conditional. This has to agree with <c>IntAlign.CollectConditionalChains</c>, which pads the
    ///     rows this produces; the two walking different chains is how the padding would land on a
    ///     shape the writer never wrote.
    /// </remarks>
    static IEnumerable<ConditionalExpressionSyntax> TernaryChain(ConditionalExpressionSyntax root) {
        for (var member = root;
             member is not null;
             member = member.WhenFalse as ConditionalExpressionSyntax) {
            yield return member;
        }
    }

    /// <summary>Whether this conditional is the tail of another — planned by the chain's root.</summary>
    static bool IsTernaryChainTail(ConditionalExpressionSyntax node) =>
        node.Parent is ConditionalExpressionSyntax parent && parent.WhenFalse == node;

    /// <summary>
    ///     Which of the two conditional layouts this node takes, and who plans it.
    /// </summary>
    /// <remarks>
    ///     ⚠ Two layouts, not one, and which one applies is a property of the <em>shape</em> rather than
    ///     of a key. Measured against <c>jb cleanupcode</c> 2025.2.6 at a 120-column margin:
    ///     <list type="bullet">
    ///         <item>
    ///             A conditional whose tail is <em>not</em> another conditional wraps at its signs —
    ///             <c>skala_wrap_before_ternary_opsigns</c>'s layout, sized by
    ///             <c>skala_wrap_ternary_expr_style</c>. <see cref="PlanTernary" />.
    ///         </item>
    ///         <item>
    ///             A chain of them wraps <em>after each <c>:</c></em>, one member per line, and the two
    ///             keys above move none of it: flipping <c>skala_wrap_ternary_expr_style</c> to
    ///             <c>chop_always</c> or <c>wrap_if_long</c>, or <c>skala_wrap_before_ternary_opsigns</c> to
    ///             <c>false</c>, returns every chain in the probe byte-identical while it moves the
    ///             single conditional beside them. <see cref="PlanTernaryChain" />.
    ///         </item>
    ///     </list>
    ///     ⚠ The author's own breaks at the signs win, and that is <c>keep_user_linebreaks</c> rather
    ///     than an autodetecting chain rule: at <c>keep_user_linebreaks = false</c> the oracle rewrites
    ///     a chain written <c>cond ? x\n : cond ? y\n : z</c> — and one written as a staircase — into
    ///     the one-member-per-line layout. So a chain the author broke at a <c>?</c> or a <c>:</c> is
    ///     planned member by member, exactly as before, and every other chain takes the chain layout.
    /// </remarks>
    void PlanConditional(ConditionalExpressionSyntax node) {
        var root = node;
        while (IsTernaryChainTail(root)) {
            root = (ConditionalExpressionSyntax)root.Parent!;
        }

        if (root.WhenFalse is not ConditionalExpressionSyntax || BreaksAtTernarySigns(root)) {
            PlanTernary(node);
            return;
        }

        if (root == node) {
            PlanTernaryChain(root);
        }
    }

    /// <summary>Whether the author put a break before any <c>?</c> or <c>:</c> of the chain.</summary>
    bool BreaksAtTernarySigns(ConditionalExpressionSyntax root) {
        if (!options.KeepsUserBreaksBetweenItems || !options.WrapBeforeTernaryOpsigns) {
            return false;
        }

        foreach (var member in TernaryChain(root)) {
            if (BreaksBefore(member.QuestionToken) || BreaksBefore(member.ColonToken)) {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    ///     The layout the oracle gives a nested conditional chain: one member per line, the <c>:</c>
    ///     trailing.
    /// </summary>
    /// <remarks>
    ///     <code>
    /// var chain = flag &gt; 10 ? "the first branch here" :
    ///     flag &gt; 5 ? "the second branch here" :
    ///     flag &gt; 1 ? "third" : "d";
    ///     </code>
    ///     ⚠ One group over the whole chain rather than one per link, because the oracle breaks every
    ///     link at once or none of them: a three-member chain one column too wide comes back with two
    ///     breaks, not with the one that would make it fit. The innermost link is not a point — its
    ///     <c>? … : …</c> stays on the last line whatever its width, which is measured on a chain whose
    ///     members are each wider than the margin and which the oracle still breaks only at the links.
    ///     <para>
    ///         ⚠ The <em>last</em> link — the gap before the final else — is not a point and is not
    ///         flat either. The oracle never adds a break there: a chain it re-wraps ends
    ///         <c>flag &gt; 1 ? "third" : "d";</c> however wide that line is. It does keep one the
    ///         author wrote, which a single conditional does not — <c>cond ?\n x :\n y</c> is re-joined
    ///         where a chain's <c>… ? "b" :\n "c"</c> is not — so the gap is pinned to the source
    ///         rather than planned.
    ///     </para>
    ///     <para>
    ///         ⚠ <c>spendsIndent: true</c>, and the builder decides whether the level is there to
    ///         spend. The members sit one continuation level from the statement — as a bare
    ///         initializer at <c>statement + 4</c>, and as a chopped call's argument or an array
    ///         initializer's element on the argument's own column, because
    ///         <c>CanSpendAContinuationLevel</c> has already given that level to the delimiter.
    ///     </para>
    /// </remarks>
    void PlanTernaryChain(ConditionalExpressionSyntax root) {
        var group = NewGroup();
        var broken = false;
        foreach (var member in TernaryChain(root)) {
            Flat(member.QuestionToken);
            Flat(member.ColonToken);
            Flat(FirstToken(member.WhenTrue));
            var link = FirstToken(member.WhenFalse);
            if (member.WhenFalse is not ConditionalExpressionSyntax) {
                Pin(link, options.KeepsUserBreaksBetweenItems && BreaksBefore(link));
                continue;
            }

            Point(link, group);
            broken |= BreaksBefore(link);
        }

        Describe(
            root,
            group,
            GroupMode.Preserve,
            new(
                options.KeepsUserBreaksBetweenItems && broken,
                BreaksIfTooLong: true
            ),
            true
        );
    }

    /// <summary>
    ///     <c>skala_wrap_before_ternary_opsigns = true</c>: <c>?</c> and <c>:</c> start their lines.
    /// </summary>
    void PlanTernary(ConditionalExpressionSyntax node) {
        var group = NewGroup();
        bool broken;

        // ⚠ A ternary keeps the author's breaks one point at a time rather than chopping at both.
        // The shape the oracle preserves is exactly the one people write:
        //     OperatingSystem.IsWindows() ? "win"
        //     : OperatingSystem.IsMacOS() ? "osx"
        //     : "linux";
        // A single group whose points all break together turns that into six lines and a staircase.
        var pins = options.KeepsUserBreaksBetweenItems;

        // ⚠ Asked once, and used twice: it picks the layout of a member the author left flat, and it
        // picks the continuation level that member sits on. The two readings have to be the same
        // reading or the formatter is not idempotent — see the branch below and `ownLevel`.
        var steps = BreaksAtTernaryQuestion(node);

        if (options.WrapBeforeTernaryOpsigns) {
            var atQuestion = BreaksBefore(node.QuestionToken);
            var atColon = BreaksBefore(node.ColonToken);
            if (pins && (atQuestion || atColon) && !IsTernaryChainMember(node)) {
                // ⚠ A single conditional is chopped at both signs once the author broke at either
                // (#518). Measured 2026-10-08: `b ? a` / `: c` in a declarator, a `return`, an
                // argument and a parenthesised operand, `b` / `? a : c`, and `a` / `/* c */` / `? 1 : 2`
                // all come back `b` / `? a` / `: c`. The per-sign pin below is a chain member's, whose
                // `cond ? "win"` / `: cond ? "osx"` / `: "linux"` the oracle keeps as written.
                Mandatory(node.QuestionToken);
                Mandatory(node.ColonToken);
            } else if (pins && steps) {
                // ⚠ A chain the author broke before any `?` is chopped at both signs of every member,
                // however well each fits (#548). The staircase is the oracle's layout of the whole
                // chain, not of the member the author broke: `a` / `? 1` / `: b ? 2 : 3`,
                // `a` / `? 1 : b ? 2 : 3` and `a ? 1 : b` / `? 2 : 3` all come back `a` / `? 1` /
                // `: b` / `? 2` / `: 3`, in a `return` and in a switch arm alike (measured 2026-10-08,
                // found reformatting Skala's own SpaceRules.cs). A chain broken only at its `:`s does
                // not step and keeps the per-sign pins below: `a ? 1` / `: b ? 2 : 3` stays as written.
                Mandatory(node.QuestionToken);
                Mandatory(node.ColonToken);
            } else if (pins && (atQuestion || atColon)) {
                Pin(node.QuestionToken, atQuestion);
                Pin(node.ColonToken, atColon);
            } else if (IsTernaryChainMember(node) && !steps) {
                // ⚠ A chain member the author left flat breaks at its `:` and never at its `?`. The
                // author broke *some* sign of this chain — that is what routed the chain here rather
                // than to `PlanTernaryChain` — but no `?`, so the chain does not step, and a member
                // that has to break joins the colon-only shape the rest of the chain is already in.
                // Measured on `jb cleanupcode` 2025.2.6 at a 120-column margin, on the two ways a
                // member gets here without a break of its own:
                //
                //     a ? "x" : b ? "y" : c ? "z"      ← too wide, and
                //     : "d";                             a ? "x" : b ? "y" : c ? "z"\n: "d" that fits
                //
                //   both come back
                //
                //     a ? "x"
                //     : b ? "y"
                //     : c ? "z"
                //     : "d";
                //
                // and a member still over the margin after that break stays over it — the oracle
                // does not then break its `?` either.
                //
                // ⚠ This is the idempotence of the staircase and not a nicety. Breaking at both
                // signs writes a `?` at the start of a line; `BreaksAtTernaryQuestion` reads the
                // source for exactly that; so the second pass found a `?` the first pass had written
                // and stepped a chain the first pass had laid flat, one level per depth, bounded at
                // pass 3. Six lines, default options — the seed
                // 11815347482968357774 / constructs/alignment/int-align-ternary.cs finding. The
                // first pass was the wrong one of the two: the oracle's answer for that input is the
                // colon-only shape above, and the second pass's staircase is what the oracle gives
                // for the shape the first pass had produced.
                Flat(node.QuestionToken);
                Point(node.ColonToken, group);
            } else {
                Point(node.QuestionToken, group);
                Point(node.ColonToken, group);
            }

            broken = atQuestion || atColon;
            Flat(FirstToken(node.WhenTrue));
            Flat(FirstToken(node.WhenFalse));
        } else {
            Flat(node.QuestionToken);
            Flat(node.ColonToken);
            Point(FirstToken(node.WhenTrue), group);
            Point(FirstToken(node.WhenFalse), group);
            broken = BreaksBefore(FirstToken(node.WhenTrue)) || BreaksBefore(FirstToken(node.WhenFalse));
        }

        Describe(
            node,
            group,
            options.WrapTernaryExprStyle == WrapStyle.ChopAlways ? GroupMode.Break : GroupMode.Preserve,
            new(
                options.KeepsUserBreaksBetweenItems && broken,
                BreaksIfTooLong: options.WrapTernaryExprStyle != WrapStyle.WrapIfLong
            ),
            true,
            // ⚠ The staircase: a nested conditional's signs sit one continuation level deeper than
            // its parent's, and the oracle *produces* it rather than preserving it — a chain written
            // flat at one level comes back stepped, and a four-member chain steps 4, 8, 12. It needs
            // `ownLevel` rather than `spendsIndent`, which is the difference between "a level if no
            // other continuation is open" and "a level, always": the enclosing member's continuation
            // is always open here, so `spendsIndent` alone collapses every depth onto one column.
            //
            // ⚠ Gated on a break before a `?`, and the gate is the whole difference between the two
            // shapes people write. Measured on both:
            //     a ? "win"            ← no `?` starts a line: stays flat, at any length
            //     : b ? "osx"
            //     : "linux";
            //     a                    ← a `?` starts a line: every depth steps
            //         ? "win"
            //         : b
            //             ? "osx"
            //             : "linux";
            // Stepping the first shape as well is what an ungated `IsTernaryChainTail` does, and it
            // costs file fidelity on both corpora — `constructs` 94.37 % → 94.06 %, `corpus/real/`
            // 85.78 % → 85.53 % — because the colon-only chain is the commoner of the two in real
            // code.
            ownLevel: IsTernaryChainTail(node) && steps
        );
    }

    /// <summary>Whether this conditional is one member of a nested chain rather than a lone one.</summary>
    /// <remarks>
    ///     ⚠ A conditional nested in a <c>WhenTrue</c> is <em>not</em> a member: the chain is the
    ///     <c>WhenFalse</c> spine and nothing else, which is the same spine
    ///     <see cref="IsTernaryChainTail" /> and <c>TernaryChain</c> walk.
    /// </remarks>
    static bool IsTernaryChainMember(ConditionalExpressionSyntax node) =>
        IsTernaryChainTail(node) || node.WhenFalse is ConditionalExpressionSyntax;

    /// <summary>Whether the author put a break before any <c>?</c> of the chain this member is in.</summary>
    /// <remarks>
    ///     ⚠ Asked of the whole chain rather than of the member, because the layout is the chain's:
    ///     the oracle steps every depth or none of them, and a chain broken before its first <c>?</c>
    ///     only still comes back fully stepped.
    /// </remarks>
    /// <summary>
    ///     The root of a conditional chain the author stepped — a <c>?</c> of it starts a line — which
    ///     #548 chops at both signs of every member, so it nests like a lone conditional.
    /// </summary>
    public bool IsSteppedChainRoot(ConditionalExpressionSyntax node) =>
        node.WhenFalse is ConditionalExpressionSyntax && !IsTernaryChainTail(node) && BreaksAtTernaryQuestion(node);

    bool BreaksAtTernaryQuestion(ConditionalExpressionSyntax node) {
        if (!options.KeepsUserBreaksBetweenItems || !options.WrapBeforeTernaryOpsigns) {
            return false;
        }

        var root = node;
        while (IsTernaryChainTail(root)) {
            root = (ConditionalExpressionSyntax)root.Parent!;
        }

        foreach (var member in TernaryChain(root)) {
            if (BreaksBefore(member.QuestionToken)) {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    ///     <c>skala_wrap_before_eq = false</c>: a break around an assignment lands after the <c>=</c>, never
    ///     before it.
    /// </summary>
    /// <summary>
    ///     A construct whose only break point is the gap in front of it.
    /// </summary>
    /// <remarks>
    ///     ⚠ <c>leadingGapInside</c>, always: the point is at the node's own first token, so the group
    ///     has to be open before that gap is written or the writer finds the group unresolved and
    ///     renders it flat. See GroupPlan.LeadingGapInside.
    /// </remarks>
    void PlanBreakBefore(SyntaxNode node, SyntaxToken token) {
        if (token.IsKind(SyntaxKind.None)) {
            return;
        }

        var group = NewGroup();
        Point(token, group);
        Describe(
            node,
            group,
            GroupMode.Preserve,
            new(
                options.KeepsUserBreaksBetweenItems && BreaksBefore(token),
                BreaksIfTooLong: true,
                MeasuresHead: true
            ),
            true,
            true
        );
    }

    /// <summary>
    ///     <c>new_line_between_query_expression_clauses = true</c>: a query that does not fit puts every
    ///     one of its clauses on a line of its own.
    /// </summary>
    /// <remarks>
    ///     ⚠ A query has no delimiters, so — like a base list — it opens its own continuation scope
    ///     around its own body rather than living inside one. Measured at the export's 120-column
    ///     margin, with the query's clauses one level in from the statement and <em>not</em> aligned to
    ///     the <c>from</c>:
    ///     <code>
    /// var longQuery = from number in numbers
    ///     where number > 0 &amp;&amp; number &lt; 100
    ///     orderby number descending
    ///     select number * 2;
    ///     </code>
    ///     Four measurements decide the shape, and none of them is readable off the option names:
    ///     <list type="number">
    ///         <item>
    ///             <c>new_line_between_query_expression_clauses = true</c> is a <em>chop</em>, not a
    ///             permission. A query the author broke at one boundary comes back broken at every one —
    ///             <c>from n in xs where n > 0\n orderby n select n;</c> becomes four lines — and a query
    ///             too wide for its line is chopped whole. At <c>false</c> the same two inputs come back
    ///             with exactly the author's breaks and one more only where the line runs out, which is
    ///             the fill.
    ///         </item>
    ///         <item>
    ///             The author's breaks are kept iff <em>both</em> <c>keep_user_linebreaks</c> and
    ///             <c>skala_keep_existing_linebreaks</c>: with either off, a query broken one clause per line
    ///             comes back on one line. A fill therefore pins them rather than re-flowing them, the
    ///             same correction <see cref="PlanList" /> records for a list pattern.
    ///         </item>
    ///         <item>
    ///             <c>skala_place_linq_into_on_new_line</c> governs the <em>continuation's</em> <c>into</c>
    ///             — <c>group … by … into bucket</c> — and not a <c>join … into matches</c>, which the
    ///             oracle leaves on the join's line with the key at <c>true</c> and the query chopped.
    ///             At <c>false</c> the continuation's <c>into</c> is not a point either, and the gap is
    ///             left unplanned rather than flattened: a <c>false</c> placement key is permissive
    ///             (docs/plan/05), and the oracle does keep a break the author put in front of it.
    ///         </item>
    ///         <item>
    ///             <c>skala_align_linq_query</c> needs nothing here. It is
    ///             <see cref="CSharpDocumentBuilder.AlignsFromOwnColumn" />'s already, and what it was
    ///             waiting for is this group: with the clauses breaking, the key moves them from one
    ///             continuation level to the <c>from</c>'s own column.
    ///         </item>
    ///     </list>
    ///     ⚠ <c>HidesFlatWidthWhenBroken</c>, and it is <c>skala_wrap_before_linq_expression</c> that needs
    ///     it. A query the author broke and which may not re-join is certain to break, and the
    ///     <c>=</c> around it has to know: at <c>true</c> the oracle answers
    ///     <c>var q =</c> / <c>from n in xs</c> / … on a query whose own flat width is 37 columns and
    ///     fits with room to spare, which no width test on the value can produce. It does not cost the
    ///     export's answer, because at <c>false</c> the <c>=</c> group is the ordering rule's
    ///     (<c>PrefersOuterBreak</c>) and declines a break that buys nothing — the same query comes
    ///     back with <c>from</c> still on the declaration's line.
    /// </remarks>
    void PlanQuery(QueryExpressionSyntax node) {
        var group = NewGroup();
        var fill = !options.NewLineBetweenQueryExpressionClauses;
        var broken = false;
        PlanQueryBody(node.Body, group, fill, ref broken);

        Describe(
            node,
            group,
            GroupMode.Preserve,
            new(
                options.KeepsUserBreaksBetweenItems && broken,
                BreaksIfTooLong: true,
                HidesFlatWidthWhenBroken: true
            ),
            true
        );
    }

    /// <remarks>
    ///     ⚠ Recursive through the continuation, and every level joins the <em>same</em> group. A
    ///     <c>group … into bucket …</c> is two query bodies in the syntax and one construct on the page:
    ///     the oracle chops the clauses after the <c>into</c> exactly when it chops the ones before it.
    /// </remarks>
    void PlanQueryBody(QueryBodySyntax body, int group, bool fill, ref bool broken) {
        foreach (var clause in body.Clauses) {
            broken |= PlanQueryClause(FirstToken(clause), group, fill);
        }

        broken |= PlanQueryClause(FirstToken(body.SelectOrGroup), group, fill);

        if (body.Continuation is not { } continuation) {
            return;
        }

        if (options.PlaceLinqIntoOnNewLine) {
            broken |= PlanQueryClause(continuation.IntoKeyword, group, fill);
        }

        PlanQueryBody(continuation.Body, group, fill, ref broken);
    }

    bool PlanQueryClause(SyntaxToken token, int group, bool fill) {
        var broke = BreaksBefore(token);

        // ⚠ A fill re-flows every gap it owns and this one must not: at
        // `new_line_between_query_expression_clauses = false` the oracle returns a query the author
        // broke with exactly the author's breaks, so a preserved gap becomes an ordinary required
        // break and the rest stay fill points. The same shape PlanList uses for a list pattern.
        if (fill && broke && options.KeepsUserBreaksBetweenItems) {
            Mandatory(token);
        } else {
            Point(token, group, fill);
        }

        return broke;
    }

    /// <summary>
    ///     An <c>orderby</c> clause's orderings: a fill one continuation level past the clause (#477,
    ///     SK-DIV-0114).
    /// </summary>
    /// <remarks>
    ///     ⚠ Measured 2026-10-08 with <c>Testing ask</c>: <c>orderby a,</c> / <c>b</c>, <c>orderby a</c> /
    ///     <c>, a</c>, <c>orderby a descending,</c> / <c>a ascending</c> and three orderings one per line all
    ///     keep the author's breaks and put the continued ordering one level past <c>orderby</c>'s column,
    ///     where Skala left it on the clause's own; <c>orderby a, a</c> on one line stays; and an
    ///     <c>orderby</c> past the margin <em>fills</em> — <c>orderby x.Length, x.Length,</c> /
    ///     <c>x</c> — where Skala did not wrap it at all. The exemption in <c>SeparatedListPlanTests</c>
    ///     ("nothing to plan") was this list, unmeasured.
    ///     <para>
    ///         ⚠ The level is spent under the query's own: the clause is already on a continuation line of
    ///         the query, which is why the group spends under an open continuation
    ///         (<see cref="GroupPlan.SpendsUnderDelimiters" />). The two open on different lines, so the
    ///         writer's one-level-per-line rule counts both.
    ///     </para>
    /// </remarks>
    void PlanOrderings(OrderByClauseSyntax node) {
        var group = NewGroup();
        var pins = options.KeepsUserBreaksBetweenItems;
        foreach (var comma in node.Orderings.GetSeparators()) {
            var next = comma.GetNextToken();
            if (next.IsKind(SyntaxKind.None)) {
                continue;
            }

            if (options.WrapBeforeComma) {
                PlanItemGap(comma, group, true, pins);
                if (!(pins && BreaksBefore(next))) {
                    Flat(next);
                }
            } else {
                // ⚠ An author's break on the other side of the comma is kept as written, and only while
                // keep_user_linebreaks is: `orderby a` / `, a` stays, and is joined at `false`.
                if (!(pins && BreaksBefore(comma))) {
                    Flat(comma);
                }

                PlanItemGap(next, group, true, pins);
            }
        }

        Describe(
            node,
            new(
                group,
                GroupMode.Preserve,
                new(BreaksIfTooLong: true),
                true,
                SpendsUnderDelimiters: true
            )
        );
    }

    void PlanAroundEquals(SyntaxNode node, SyntaxToken equals, ExpressionSyntax value) {
        if (equals.IsKind(SyntaxKind.None)) {
            return;
        }

        var group = NewGroup();
        bool broken;
        // ⚠ Only one side is planned. `skala_wrap_before_eq = false` says a break the formatter *adds*
        // goes after the `=`; it does not say a break the author put before it is illegal, and the
        // oracle keeps a break before the `=` exactly as written. Registering the other side as a
        // non-point would re-join it, which is a line the author wrote and nobody asked to remove.
        if (options.WrapBeforeEq) {
            Point(equals, group);
            broken = BreaksBefore(equals);
        } else {
            Point(FirstToken(value), group);
            broken = BreaksBefore(FirstToken(value));
        }

        // ⚠ A collection-valued `=` measures its head from the first token of the construct that
        // owns it — the statement, the field's first modifier, a header's `(` — and only the writer
        // knows that token's column, so it is a marker in the #372 pattern. Shared when two
        // declarators sit under one type: `int[] xs = [1], ys = [2]` has one statement start.
        // ⚠ No marker when the head starts at the group's own first token — an assignment, a named
        // attribute argument: a marker there would be entered after the group it serves, and the
        // group's own point width is the head.
        var yieldsToTheBracket = BreakYieldsToTheBracket(value);
        var owner = EqualsOwnerOf(node);
        var callee = owner == EqualsOwner.None ? 0 : CalleeWidthOf(value);

        // ⚠ A conditional value measures its whole condition (#553, GroupFacts.ValueHeadWidth), with the
        // collection's twelve-column head floor: `var v =` keeps the `=` and chops the chain, `var vvvvvvvvvv
        // =` breaks it — measured on chain, binary and identifier conditions alike. ⚠ Only a condition the
        // author left on one line, chain breaks aside: `var properties = replacement is null` / `|| …` keeps
        // its `=` in the oracle where the same condition written flat breaks it (Skala's own source, Lint).
        var conditionHead = owner != EqualsOwner.None
            && value is ConditionalExpressionSyntax conditional
            && !HasLooseBreak(conditional.Condition)
            && conditional.Condition is not (IsPatternExpressionSyntax
                or BinaryExpressionSyntax { RawKind: (int)SyntaxKind.IsExpression or (int)SyntaxKind.AsExpression })
                ? FlatSourceWidth(conditional.Condition)
                : 0;
        var conditionHeadIsWide = conditionHead > 0 && HeadWidthThroughEquals(node, equals) >= MinimumEqualsHead;

        // ⚠ A single call on a receiver, as the whole value: moved down at its dot rather than chopped
        // when the `=` stays (#528). See PlanHeldSingleCall for where the `=` stays.
        var heldKind = owner switch {
            EqualsOwner.TypedLocal => 1,
            EqualsOwner.VarLocal or EqualsOwner.Assignment => HeadWidthThroughEquals(node, equals) < MinimumEqualsHead
                ? 2
                : 3,
            _ => 0,
        };
        var heldCall = heldKind > 0 ? PlanHeldSingleCall(value, heldKind) : null;
        var heldReceiver = heldCall?.Expression is MemberAccessExpressionSyntax heldAccess
            ? FlatSourceWidth(heldAccess.Expression)
            : 0;
        var head = -1;
        if ((yieldsToTheBracket || callee > 0)
            && EqualsHeadStartOf(node) is { RawKind: not 0 } headToken
            && headToken != FirstToken(node)
            && !markers.TryGetValue(headToken.SpanStart, out head)) {
            head = NewGroup();
            markers[headToken.SpanStart] = head;
        }

        Describe(
            node,
            new(
                group,
                GroupMode.Preserve,
                new(
                    options.KeepsUserBreaksBetweenItems && broken,

                    // ⚠ `prefer_wrap_around_eq`, and the reason milestone 2 stopped at presence. The
                    // oracle does break after `=` on a line that is too long — but not always, and
                    // breaking whenever the line is long costs 1.18 points of line fidelity against
                    // leaving it alone (measured on this branch before the ordering rule existed:
                    // 97.47 % → 96.29 %). Which of a long line's candidate points is taken is
                    // GroupFacts.PrefersOuterBreak's rule, and it is what makes this key observable.
                    // ⚠ Not before a lambda whose arrow takes the break instead: `Func<int, string> f =
                    // value =>` / `value.ToString() + "…";` at every width measured, where Skala broke the
                    // `=` because the whole lambda fitted on the line below (#453, SK-DIV-0050).
                    BreaksIfTooLong: !YieldsToTheLambdaArrow(value)
                    && !(value is MemberAccessExpressionSyntax member && IsPlainMemberValue(member))
                    && !KeepsTheEqualsBeforeALambdaCall(node, value)
                    && !InitializerBrokenAfterItsBrace(value),

                    // ⚠ `skala_wrap_before_linq_expression = true` takes the query out of the ordering rule.
                    // Every other right-hand side is measured by what is left of the line and breaks
                    // only when its own break is the one worth taking; a query under this key breaks
                    // whenever the whole query does not fit, which is what puts `from` on a line of its
                    // own. Measured at a 70-column margin: `var q = from … select …;` keeps `from` on
                    // the declaration's line at false and moves it down at true, with nothing else in
                    // the file changing.
                    MeasuresHead: !QueryLeadsTheWay(value),
                    PrefersOuterBreak: !QueryLeadsTheWay(value),

                    // ⚠ A kept `=` break makes whatever list holds the clause multi-line, and a list that
                    // is multi-line chops — "chop if long *or multiline*", the half of chop_if_long a
                    // delimited list already carries for a nested list. Measured on every owner an `=`
                    // can sit in: a parameter default `void M(int a =\n 5)` chops the parameter list, a
                    // lambda's too; `[Obsolete(Message =\n "x")]` chops the attribute's arguments;
                    // `new T { X =\n 1, Y = 2 }` breaks the braces and chops the elements; `for (int i
                    // =\n 0; …)` chops the header; and `int[] xs =\n [1, 2], ys = [3]` chops the
                    // declarators. Without it Skala kept the break and left every one of those lists
                    // whole, `void B(int a =\n        5) { }` (SK-DIV-0103).
                    HidesFlatWidthWhenBroken: true,

                    // ⚠ And a break before a collection expression yields to the bracket when the
                    // bracket is going to break, kept or added alike: `= [` is what the oracle writes for
                    // one that is chopped, too wide for the line below, or holds a multi-line element,
                    // and `=\n[1, 2]` for one that fits there (issues #375 and #379). See
                    // BreakYieldsToTheBracket. An added break also needs a head of twelve columns,
                    // measured from the marker; see GroupFacts.MinimumHead.
                    BreaksOnlyIfTailFits: yieldsToTheBracket,
                    Owner: head,
                    MinimumHead: yieldsToTheBracket || callee > 0 ? MinimumEqualsHead : 0,
                    CalleeWidth: callee,
                    YieldsThroughArrow: ArrowYieldWidthOf(value),
                    LambdaLocal: ArrowYieldWidthOf(value) > 0 ? LambdaLocalOf(node) : LambdaLocal.None,
                    PatternHead: PatternHeadOf(node, equals, value),
                    PatternWidth: PatternHeadOf(node, equals, value) > 0
                        ? ((IsPatternExpressionSyntax)value).Pattern.Span.Length
                        : 0,
                    CalleeOwner: owner,
                    FlatIfHeadOverflows: node is AssignmentExpressionSyntax { Left: var target }
                    && TrailingProperty(target) is not null
                    && ChainPointCount(target, options) == 0,
                    ValueHeadWidth: conditionHead,
                    ValueHeadFitsBelow: value is ConditionalExpressionSyntax {
                        Condition: InvocationExpressionSyntax { Expression: IdentifierNameSyntax or GenericNameSyntax }
                    },
                    ValueHeadIsWide: conditionHeadIsWide,
                    HeldValue: heldCall is null ? 0 : heldKind,
                    HeldValueWidth: heldCall is null
                        ? 0
                        : FlatSourceWidth(value)
                        + (value.GetLastToken().GetNextToken().IsKind(SyntaxKind.SemicolonToken) ? 1 : 0),
                    HeldValueReceiver: heldReceiver,
                    HeldValueHead: heldCall is { Expression: MemberAccessExpressionSyntax heldDot }
                        ? heldCall.ArgumentList.OpenParenToken.Span.End - heldDot.OperatorToken.SpanStart
                        : 0,
                    HeldValueManyArgs: heldCall?.ArgumentList.Arguments.Count > 1
                ),
                true,
                // ⚠ And so does the `=` of a name a comment has already broken onto a continuation line:
                // `string /* c */` / `    s =` / `        "…";` is two levels, not one (#420).
                SpendsUnderDelimiters: IsAListItemsEquals(node) || FollowsABrokenDeclarationHead(node),

                // ⚠ The level is held at zero when the value opens with a parenthesis the author
                // broke after: `var t =\n(\n 1, 2)` puts the `(` at the statement's own indent. See
                // HeadsWithAChoppedParenthesis and GroupPlan.HoldsLevel (SK-DIV-0101).
                HoldsLevel: HoldFor(group, value)
            )
        );
    }

    /// <summary>
    ///     Whether a break at an <c>=</c> or an <c>=&gt;</c> is one of two alternatives — its own or the
    ///     bracket's after it — so that it is kept, and added, exactly when the value fits flat on the
    ///     continuation line. See <see cref="GroupFacts.BreaksOnlyIfTailFits" />.
    /// </summary>
    /// <remarks>
    ///     ⚠ A collection expression and nothing else, and the boundary is the oracle's: asked with the
    ///     same multi-line lambda as the element, <c>var t =\n new[] { 1, () => {…} };</c> and
    ///     <c>var t =\n (1, () => {…});</c> keep the <c>=</c> break, while <c>=\n[1, () => {…}]</c>
    ///     comes back <c>= [</c> — in a local, a field, a property initializer, a deconstruction, a
    ///     parameter default, a lambda's parameter default, an object initializer's element, a named
    ///     attribute argument, a <c>using</c> header and a <c>for</c> header alike, and on either side of
    ///     the <c>=</c> (issue #375). The same collection written to fit on the line below keeps the
    ///     break in every one of them: 120 columns stays and 121 gives the break to the bracket.
    ///     <para>
    ///         ⚠ The flat direction has the same boundary and no margin (issue #379, the Nightly's seed
    ///         3296757264995743770). From a flat line too long to keep, the oracle breaks after the
    ///         <c>=</c> exactly when the bracket fits flat on the line below — a 120-column continuation
    ///         line moves down, 121 is written <c>= [</c> and filled, with the <c>=</c> at column 60 and
    ///         at 100 alike — and it writes <c>= [</c> for a bracket that will not fit there even when
    ///         the <c>[</c> lands past the margin: <c>T v = [</c> at 122 columns, the line measured only
    ///         up to the <c>=</c>, in a local, a field, a property initializer and an assignment, and at
    ///         125 for an assignment with nothing before the <c>=</c> that could break. Once the
    ///         <c>=</c> passes column 120 the oracle breaks the gap between the type and the name
    ///         instead, which Skala has no plan for (SK-DIV-0024's family), so Skala writes the glued
    ///         form there too — the answer the oracle itself gives when no earlier gap exists, and a
    ///         fixed point of both formatters. The ordering rule had been answering the flat direction:
    ///         its second question took the <c>=</c> break as soon as <c>= [</c> overhung, and its first
    ///         declined one the oracle takes whenever the continuation line was within the fitted
    ///         margin of the width; pass two, reading either as the author's, reversed it.
    ///     </para>
    ///     <para>
    ///         ⚠ The arrow of an expression body follows the same rule, measured the same way:
    ///         <c>object[] P =&gt;\n[…]</c> is kept for a 120-column continuation line and written
    ///         <c>P =&gt; [</c> at 121, a flat <c>P =&gt; […]</c> that does not fit breaks the arrow
    ///         exactly when the bracket fits below, and a bracket that does not is glued —
    ///         <c>=&gt; [</c> up to column 120 from a flat line, and at 122 when the chopped bracket is
    ///         given back, where the oracle re-joins Skala's old <c>=&gt;</c> break rather than move the
    ///         name down. Only the glue's own overhang differs: for the arrow the oracle counts the
    ///         <c>[</c> and moves the name down from 121, for the <c>=</c> it counts up to the <c>=</c>.
    ///         Neither gap is Skala's, and the fact does not need the difference.
    ///     </para>
    ///     <para>
    ///         ⚠ This replaces a source test. The exemption used to be "a collection the author broke at
    ///         one of its own gaps" (<c>ListBreaksInSource</c>, SK-DIV-0103), which answers the same
    ///         question for one of the three reasons a bracket breaks and not for the other two — too
    ///         wide for the continuation line, or an element that spans lines. The second was the
    ///         Nightly's seed 5209185227727739433: a list chopped around a multi-line lambda has no break
    ///         at its own gaps on pass one, so the <c>=</c> break was kept, and has them on pass two, so
    ///         it was joined. Only the fitter can answer "does the bracket fit", and it answers it the
    ///         same way from either pass's output.
    ///     </para>
    /// </remarks>
    static bool BreakYieldsToTheBracket(ExpressionSyntax value) => value is CollectionExpressionSyntax;

    /// <summary>
    ///     Whether an <c>=</c>'s value is a creation whose initializer the author broke after its <c>{</c>:
    ///     the <c>=</c> then never breaks, the brace keeps the break (author-layout survey, round four).
    /// </summary>
    /// <remarks>
    ///     ⚠ Measured 2026-10-09 behind heads of 12, 30 and 40 columns, values of 80 to 116 columns: the
    ///     oracle keeps <c>var x = new T {</c> / members / <c>};</c> wherever the statement does not fit on one
    ///     line, including where the same initializer written on one line moves down whole after the
    ///     <c>=</c>; where the statement fits it joins it. Skala broke the <c>=</c> and joined the braces.
    ///     ⚠ Only a brace on the creation's line: Newtonsoft's <c>new T</c> / <c>{</c> / members / <c>};</c>
    ///     moves down whole after the <c>=</c> in the oracle, and reading it as the author's brace break moved
    ///     four of its files away.
    /// </remarks>
    bool InitializerBrokenAfterItsBrace(ExpressionSyntax value) =>
        value switch {
            BaseObjectCreationExpressionSyntax { Initializer: { } initializer } => BreaksAfter(initializer),
            ArrayCreationExpressionSyntax { Initializer: { } initializer } => BreaksAfter(initializer),
            ImplicitArrayCreationExpressionSyntax { Initializer: var initializer } => BreaksAfter(initializer),
            _ => false
        };

    bool BreaksAfter(InitializerExpressionSyntax initializer) =>
        initializer.Expressions.Count > 0
        && !BreaksBefore(initializer.OpenBraceToken)
        && BreaksBefore(initializer.OpenBraceToken.GetNextToken());

    /// <summary>
    ///     The break between a cast and the collection expression it casts, which is one of two
    ///     alternatives exactly as an <c>=</c>'s is.
    /// </summary>
    /// <remarks>
    ///     ⚠ #450, SK-DIV-0012. Measured 2026-10-08: a cast collection too long for its line comes back
    ///     <c>(Kind[])</c> / <c>[a, b, c];</c> one level in when the collection fits flat on the line below,
    ///     and <c>(Kind[]) [</c> / chopped elements / <c>];</c> when it does not — in a local, a
    ///     <c>return</c>, an argument and an expression body. A break the author wrote after the cast is
    ///     kept when the collection fits there (<c>(int[])</c> / <c>[1, 2, 3]</c>) and given to the bracket
    ///     when the collection is itself broken. That is <see cref="GroupFacts.BreaksOnlyIfTailFits" />,
    ///     the rule <see cref="BreakYieldsToTheBracket" /> already gives an <c>=</c> and an arrow; Skala had
    ///     no point between the two tokens at all. The space in front of a broken bracket is the builder's
    ///     (<c>CSharpDocumentBuilder.SpaceIfTheCollectionBreaks</c>).
    /// </remarks>
    void PlanCastBeforeACollection(SyntaxNode node) {
        if (node is not CastExpressionSyntax {
                Expression: CollectionExpressionSyntax { Elements.Count: > 0 } collection
            } cast) {
            return;
        }

        var open = collection.OpenBracketToken;
        var group = NewGroup();
        Point(open, group);
        Describe(
            cast,
            new(
                group,
                GroupMode.Preserve,
                new(
                    options.KeepsUserBreaksBetweenItems && BreaksBefore(open),
                    BreaksIfTooLong: true,
                    PrefersOuterBreak: true,
                    HidesFlatWidthWhenBroken: true,
                    BreaksOnlyIfTailFits: true
                ),
                true
            )
        );
    }

    /// <summary>
    ///     The head a flat line needs before the oracle adds a break after a collection-valued
    ///     <c>=</c>: twelve columns from the owning construct's first token through the <c>=</c>.
    ///     See <see cref="GroupFacts.MinimumHead" />.
    /// </summary>
    /// <remarks>
    ///     ⚠ A constant, not three indents: measured at <c>indent_size = 2</c> the boundary is the same
    ///     eleven-glues, twelve-breaks. <c>var items = […]</c> is eleven and glues; <c>var results =</c>
    ///     is thirteen and breaks (issue #379).
    /// </remarks>
    const int MinimumEqualsHead = 12;

    /// <summary>
    ///     For an <c>=</c> whose value is a call on a plain name with two or more arguments, the width of
    ///     the callee — the value up to its <c>(</c> — which turns on the measured floor of
    ///     <see cref="GroupFacts.CalleeWidth" /> (#446, SK-DIV-0211); zero for any other value.
    /// </summary>
    /// <summary>The measured owner of an <c>=</c> whose floor <see cref="EqualsFloor" /> knows (#446).</summary>
    /// <remarks>
    ///     ⚠ Each owner is its own curve, measured one column at a time: a local with a written type sits a
    ///     column off a <c>var</c> one in places, an assignment statement a column lower, and a field at
    ///     indent 4 is another curve altogether — every row chops up to a <c>(</c> at column 78 and the floor
    ///     then jumps to 62 and stays near 60. Anything else keeps the ordering rule.
    /// </remarks>
    static EqualsOwner EqualsOwnerOf(SyntaxNode node) =>
        node switch {
            EqualsValueClauseSyntax {
                    Parent:
                    VariableDeclaratorSyntax {
                        Parent: VariableDeclarationSyntax { Variables.Count: 1 } declaration
                    }
                } when declaration.Parent is LocalDeclarationStatementSyntax =>
                declaration.Type.IsVar ? EqualsOwner.VarLocal : EqualsOwner.TypedLocal,
            EqualsValueClauseSyntax {
                Parent:
                VariableDeclaratorSyntax {
                    Parent: VariableDeclarationSyntax { Variables.Count: 1, Parent: FieldDeclarationSyntax }
                }
            } => EqualsOwner.Field,
            AssignmentExpressionSyntax { Parent: ExpressionStatementSyntax } assignment
                when assignment.IsKind(SyntaxKind.SimpleAssignmentExpression) => EqualsOwner.Assignment,
            _ => EqualsOwner.None
        };

    /// <summary>
    ///     The flat width of a chain after one of its links, through the <c>;</c> that ends the statement
    ///     when one does: <c>.Where(p).ToList(q);</c> after <c>.Select(…)</c>. A whitespace run counts as
    ///     one space, and as nothing where it holds a line break.
    /// </summary>
    int RestWidth(SyntaxNode link, SyntaxNode root) {
        var width = 0;
        var gap = 0;
        foreach (var c in source.AsSpan(link.Span.End, root.Span.End - link.Span.End)) {
            if (c is '\r' or '\n') {
                gap = 2;
            } else if (c is ' ' or '\t') {
                gap = gap == 0 ? 1 : gap;
            } else {
                width += gap == 1 ? 2 : 1;
                gap = 0;
            }
        }

        return width + (root.GetLastToken().GetNextToken().IsKind(SyntaxKind.SemicolonToken) ? 1 : 0);
    }

    /// <summary>
    ///     Whether a node holds a line break that <see cref="FlatSourceWidth" /> reads as a space: anywhere
    ///     but before a <c>.</c>, a <c>?</c>, a <c>)</c> or a <c>]</c>, or after a <c>(</c> or a <c>[</c>.
    /// </summary>
    static bool HasLooseBreak(SyntaxNode node) {
        var first = true;
        foreach (var token in node.DescendantTokens()) {
            if (!first) {
                var previous = token.GetPreviousToken();
                var breaks = token.LeadingTrivia.Any(static t => t.IsKind(SyntaxKind.EndOfLineTrivia))
                    || previous.TrailingTrivia.Any(static t => t.IsKind(SyntaxKind.EndOfLineTrivia));
                var glued = token.Kind() is SyntaxKind.DotToken
                        or SyntaxKind.QuestionToken
                        or SyntaxKind.CloseParenToken
                        or SyntaxKind.CloseBracketToken
                    || previous.Kind() is SyntaxKind.OpenParenToken or SyntaxKind.OpenBracketToken;
                if (breaks && !glued) {
                    return true;
                }
            }

            first = false;
        }

        return false;
    }

    /// <summary>
    ///     The width a node takes written on one line: its tokens as written, a run of whitespace kept as
    ///     one space, and a line break that the node's own breaks put in front of a <c>.</c>, a <c>?</c>,
    ///     a <c>)</c> or a <c>]</c> read as nothing — so the chain the first pass chopped measures on the
    ///     second pass what it measured flat. See <see cref="GroupFacts.ValueHeadWidth" /> (#553).
    /// </summary>
    static int FlatSourceWidth(SyntaxNode node) {
        var width = 0;
        var first = true;
        foreach (var token in node.DescendantTokens()) {
            if (!first && token.HasLeadingTrivia || !first && token.GetPreviousToken().HasTrailingTrivia) {
                var breaks = token.LeadingTrivia.Any(static t => t.IsKind(SyntaxKind.EndOfLineTrivia))
                    || token.GetPreviousToken().TrailingTrivia.Any(static t => t.IsKind(SyntaxKind.EndOfLineTrivia));
                var glued = token.Kind() is SyntaxKind.DotToken
                        or SyntaxKind.QuestionToken
                        or SyntaxKind.CloseParenToken
                        or SyntaxKind.CloseBracketToken
                    || token.GetPreviousToken().Kind() is SyntaxKind.OpenParenToken or SyntaxKind.OpenBracketToken;
                if (!(breaks && glued)) {
                    width++;
                }
            }

            width += token.Span.Length;
            first = false;
        }

        return width;
    }

    /// <summary>
    ///     The flat width from the construct's head token (<see cref="EqualsHeadStartOf" />) through the
    ///     <c>=</c> — <c>var name =</c> — counted as written, whitespace runs as one space.
    /// </summary>
    static int HeadWidthThroughEquals(SyntaxNode node, SyntaxToken equals) {
        var start = EqualsHeadStartOf(node);
        if (start.IsKind(SyntaxKind.None)) {
            start = FirstToken(node);
        }

        var width = 0;
        for (var token = start; !token.IsKind(SyntaxKind.None); token = token.GetNextToken()) {
            if (token != start && (token.HasLeadingTrivia || token.GetPreviousToken().HasTrailingTrivia)) {
                width++;
            }

            width += token.Span.Length;
            if (token == equals) {
                break;
            }
        }

        return width;
    }

    /// <summary>
    ///     For a local's <c>=</c> whose value is <c>operand is A or B</c> written on one line: the head's width
    ///     through the <c>=</c>, which turns on <see cref="EqualsFloor.BreaksBeforeAPattern" /> (#446,
    ///     SK-DIV-0211); zero otherwise.
    /// </summary>
    static int PatternHeadOf(SyntaxNode node, SyntaxToken equals, ExpressionSyntax value) {
        if (value is not IsPatternExpressionSyntax { Pattern: BinaryPatternSyntax } test
            || EqualsOwnerOf(node) is not (EqualsOwner.VarLocal or EqualsOwner.TypedLocal)
            || test.DescendantTrivia().Any(static trivia => !trivia.IsKind(SyntaxKind.WhitespaceTrivia))
            || EqualsHeadStartOf(node) is not { RawKind: not 0 } head) {
            return 0;
        }

        return equals.Span.End - head.SpanStart;
    }

    /// <summary>
    ///     For an <c>=</c> whose value is a lambda with a bare name for a body, the width from the lambda's
    ///     start through its <c>=&gt;</c>: the <c>=</c> yields to the arrow while that much fits beside it
    ///     (#453); zero otherwise.
    /// </summary>
    /// <remarks>
    ///     ⚠ Measured on <c>Func&lt;…&gt; f = (A a1, B b1) =&gt; Name;</c> and <c>… = () =&gt; Name;</c> over heads of
    ///     12 to 70 and parameter lists of 2 to 70: wherever the line through <c>=&gt;</c> fits, the arrow
    ///     breaks and the <c>=</c> never does, which Skala had the other way round from a head of 30.
    /// </remarks>
    /// <summary>
    ///     A local's <c>=</c> before a lambda whose body is a call, under a declarator name of at most nine
    ///     columns: the oracle never breaks it (#453).
    /// </summary>
    /// <remarks>
    ///     ⚠ Measured on <c>Func&lt;T…&gt; name = () =&gt; Callee(x, y);</c> over type widths of 2 to 58,
    ///     name widths of 1 to 49, the body's <c>(</c> at the head's end and at columns 80 and 100, and
    ///     line ends of 121 to 150: no name of nine columns or fewer breaks the <c>=</c> in any cell. The
    ///     arrow or the arguments take the break by Skala's own rules. Wider names break it by a rule that
    ///     moves with the name, the type and the <c>(</c> separately, and that is not wired.
    /// </remarks>
    static bool KeepsTheEqualsBeforeALambdaCall(SyntaxNode node, ExpressionSyntax value) =>
        value is LambdaExpressionSyntax { ExpressionBody: InvocationExpressionSyntax }
        && node is EqualsValueClauseSyntax {
            Parent: VariableDeclaratorSyntax {
                Parent: VariableDeclarationSyntax { Variables.Count: 1, Parent: LocalDeclarationStatementSyntax }
            } declarator
        }
        && declarator.Identifier.Span.Length <= 9;

    /// <summary>
    ///     A local's <c>=</c> before a lambda with a bare-name body: the gates its declarator's name and
    ///     type widths open (#558). See <see cref="LambdaLocal" />.
    /// </summary>
    static LambdaLocal LambdaLocalOf(SyntaxNode node) {
        if (node is not EqualsValueClauseSyntax {
                Parent: VariableDeclaratorSyntax {
                    Parent: VariableDeclarationSyntax {
                        Variables.Count: 1, Parent: LocalDeclarationStatementSyntax
                    } declaration
                } declarator
            }) {
            return LambdaLocal.None;
        }

        var type = declaration.Type.Span.Length;
        var name = declarator.Identifier.Span.Length;
        var local = LambdaLocal.Measured;
        if (name <= 10 + (type + 4) / 12) {
            local |= LambdaLocal.ArrowWhileItFits;
        }

        if (name <= (type - 6) / 5 + 1) {
            local |= LambdaLocal.ChopsPastTheParenthesis;
        }

        return local;
    }

    static int ArrowYieldWidthOf(ExpressionSyntax value) =>
        value is LambdaExpressionSyntax { ExpressionBody: IdentifierNameSyntax } lambda
        && !lambda.DescendantTrivia().Any(static trivia => !trivia.IsKind(SyntaxKind.WhitespaceTrivia))
            ? lambda.ArrowToken.Span.End - lambda.SpanStart
            : 0;

    static int CalleeWidthOf(ExpressionSyntax value) =>
        value is InvocationExpressionSyntax {
            Expression: IdentifierNameSyntax callee, ArgumentList.Arguments.Count: >= 2
        }
        && !value.DescendantTrivia()
            .Any(static trivia => trivia.IsKind(SyntaxKind.EndOfLineTrivia)
                || trivia.IsKind(SyntaxKind.SingleLineCommentTrivia)
                || trivia.IsKind(SyntaxKind.MultiLineCommentTrivia)
            )
            ? callee.Span.Length
            : 0;

    /// <summary>
    ///     The token a collection-valued <c>=</c> measures its head from: the first token of the
    ///     construct that owns the <c>=</c>, after any attribute lists — or the <c>(</c> of a
    ///     <c>using</c>, <c>for</c> or <c>fixed</c> header, whose declaration the oracle measures from
    ///     the parenthesis. See <see cref="MinimumEqualsHead" />.
    /// </summary>
    /// <remarks>
    ///     ⚠ From the parenthesis and not from the statement: <c>using (var dddd = […]) { }</c> glues
    ///     and <c>using (var ddddd = […]) { }</c> breaks, which is eleven against twelve counted from
    ///     the <c>(</c> and seventeen against eighteen counted from <c>using</c> — the same floor as a
    ///     local's only from the parenthesis. <c>for (var i = […];</c> glues and <c>for (var iiiii =</c>
    ///     breaks, eight against twelve from its <c>(</c>. An assignment and a named attribute
    ///     argument measure from their own first token (<c>d = [</c> and <c>Values = [</c> glue,
    ///     <c>Xxxxxxxxxx =</c> and <c>Valuesxxxx =</c> break at twelve), which for an expression
    ///     statement is the statement's start. Attribute lists are skipped for the reason the arrow's
    ///     head skips them (#372); whether the oracle counts them is not measured.
    /// </remarks>
    static SyntaxToken EqualsHeadStartOf(SyntaxNode node) {
        switch (node) {
            case AssignmentExpressionSyntax or AttributeArgumentSyntax:
                return node.GetFirstToken();

            case EqualsValueClauseSyntax {
                Parent: VariableDeclaratorSyntax { Parent: VariableDeclarationSyntax declaration }
            }:
                return declaration.Parent switch {
                    UsingStatementSyntax statement => statement.OpenParenToken,
                    ForStatementSyntax statement => statement.OpenParenToken,
                    FixedStatementSyntax statement => statement.OpenParenToken,
                    { } owner => FirstTokenAfterAttributes(owner),
                    null => declaration.GetFirstToken()
                };

            case EqualsValueClauseSyntax { Parent: { } owner }:
                return FirstTokenAfterAttributes(owner);

            default:
                return node.GetFirstToken();
        }
    }

    /// <summary>The first token of <paramref name="node" /> that is not inside an attribute list.</summary>
    static SyntaxToken FirstTokenAfterAttributes(SyntaxNode node) {
        foreach (var child in node.ChildNodesAndTokens()) {
            if (child.AsNode() is AttributeListSyntax) {
                continue;
            }

            return child.IsToken ? child.AsToken() : child.AsNode()!.GetFirstToken();
        }

        return node.GetFirstToken();
    }

    bool QueryLeadsTheWay(ExpressionSyntax value) => options.WrapBeforeLinqExpression && value is QueryExpressionSyntax;

    /// <summary>
    ///     Whether a body — the expression after <c>=&gt;</c>, after <c>=</c>, after <c>return</c> —
    ///     begins with a parenthesis or tuple the author broke the line right after, so that the
    ///     <c>(</c> is laid out like an opening brace: at the owner's own indent, with the contents one
    ///     level in and the <c>)</c> back at the owner's.
    /// </summary>
    /// <remarks>
    ///     ⚠ Measured against the oracle on twenty-six shapes (issue #369, SK-DIV-0101), and the rule
    ///     is what the shapes say rather than anything an option name suggests:
    ///     <code>
    /// object A() =&gt;          var t =              object B() =&gt;
    /// (                        (                        (
    ///     1, 2);                   1, 2);                   a)
    ///                                                  + b;
    ///     </code>
    ///     The first two are the rule: the <c>(</c> takes the member's or statement's indent, not the
    ///     continuation the arrow or the <c>=</c> would otherwise spend on the body, and it does so
    ///     whether the parenthesis is the whole body or the receiver of <c>.ToString()</c>, of
    ///     <c>[0]</c>, of <c>switch { … }</c>, or the condition of a ternary. The third is the boundary:
    ///     as the operand of a binary expression the parenthesis keeps the continuation, and so does the
    ///     receiver of a call chain the author broke at a dot. A cast, a unary operator or <c>((</c> in
    ///     front of it means the <c>(</c> does not start the line and nothing here applies. The oracle's
    ///     <c>return (\n 1, 2)</c> and <c>var u = (\n 1, 2)</c> — parenthesis on the owner's line — put
    ///     the contents one level past that line, which is what the parenthesis's own scope already
    ///     does, so those shapes are unaffected by either answer.
    ///     <para>
    ///         The walk goes down the left spine of the body — receiver of a member access, of an
    ///         invocation, of an element access, of a conditional access, governing expression of a switch
    ///         expression, condition of a ternary, operand of a postfix <c>!</c> — and stops at the first
    ///         node of another kind. For the access kinds the operator that follows the receiver must sit
    ///         on the receiver's last line in the source: a break before the <c>.</c> is a chain the author
    ///         broke, and the oracle indents that chain's head. ⚠ Not for the ternary's <c>?</c> or the
    ///         switch's keyword: the oracle chops a ternary whose condition is multi-line, so its own
    ///         output has a break before the <c>?</c>, and asked again it keeps the <c>(</c> where it was
    ///         — reading that break as disqualifying made pass two undo pass one. The leaf must be a
    ///         parenthesised expression or a tuple whose first inner token starts a new line in the source;
    ///         that break is kept by <c>keep_user_linebreaks</c> — neither construct plans the gap — so the
    ///         source is the answer.
    ///     </para>
    ///     <para>
    ///         ⚠ "A chain the author broke at a dot" was the source's answer to a question about the
    ///         output, and the fitter breaks a chain the author did not (issue #404, SK-DIV-0156). A
    ///         chain with a group of its own — <c>(\n a)[0].C()</c>, <c>(\n a).B().C()</c>, two calls
    ///         or more — breaks at its points whenever its head spans lines, which the kept break after
    ///         the <c>(</c> makes certain; the oracle then keeps the <c>(</c> on the continuation
    ///         exactly as for the author's <c>(\n a)[0]\n.C()</c>. Read off the source, pass one held
    ///         the level, broke the chain, and pass two read the break back and gave the level up. So a
    ///         chain root on the spine whose group breaks at a point disqualifies as an author's break
    ///         does, whether or not the source has one — under <c>chop_if_long</c> and
    ///         <c>chop_always</c>.
    ///     </para>
    ///     <para>
    ///         ⚠ A fill (<c>wrap_if_long</c>) breaks by width at the writer's column, so neither the
    ///         source nor the plan can answer it, and "keep the source's answer" was the same defect
    ///         one style over (issue #407, SK-DIV-0158): pass one held the level, the fill broke before
    ///         a dot, and pass two read that break back and gave the level up. The oracle holds the
    ///         level exactly while the chain stays whole — breaks before a middle link and the
    ///         level goes, keeps every link's head and chops the last one's arguments and it stays. So
    ///         the predicate answers yes and names the chain in <paramref name="fillChain" />, and the
    ///         writer decides: it lays the body out held, and spends the level if the chain's group
    ///         took a point (<see cref="HeldLevel.WhileChainWhole" />).
    ///     </para>
    /// </remarks>
    internal static bool HeadsWithAChoppedParenthesis(
        ExpressionSyntax? body,
        string source,
        in PhaseOneOptions options
    ) =>
        HeadsWithAChoppedParenthesis(body, source, options, out _);

    /// <param name="body">The body an arrow, an <c>=</c> or a statement's own break owns.</param>
    /// <param name="source">The source text, for the author's breaks.</param>
    /// <param name="options">The chain wrap style decides which chains disqualify.</param>
    /// <param name="fillChain">
    ///     The outermost chain root on the spine whose points are a fill's, whose break only the writer
    ///     can know; <see langword="null" /> when there is none.
    /// </param>
    internal static bool HeadsWithAChoppedParenthesis(
        ExpressionSyntax? body,
        string source,
        in PhaseOneOptions options,
        out ExpressionSyntax? fillChain
    ) {
        fillChain = null;
        var node = body;
        while (node is not null) {
            if (IsChainRoot(node) && ChainPointCount(node, options) > 0) {
                if (options.WrapChainedMethodCalls != WrapStyle.WrapIfLong) {
                    return false;
                }

                fillChain ??= node;
            }

            var operatorToken = default(SyntaxToken);
            ExpressionSyntax? receiver;
            switch (node) {
                case ParenthesizedExpressionSyntax parenthesized:
                    return BreaksBeforeIn(source, parenthesized.OpenParenToken.GetNextToken());

                case TupleExpressionSyntax tuple:
                    return BreaksBeforeIn(source, tuple.OpenParenToken.GetNextToken());

                case MemberAccessExpressionSyntax access:
                    (receiver, operatorToken) = (access.Expression, access.OperatorToken);
                    break;

                case InvocationExpressionSyntax invocation:
                    (receiver, operatorToken) = (invocation.Expression, invocation.ArgumentList.OpenParenToken);
                    break;

                case ElementAccessExpressionSyntax element:
                    (receiver, operatorToken) = (element.Expression, element.ArgumentList.OpenBracketToken);
                    break;

                case ConditionalAccessExpressionSyntax conditional:
                    // ⚠ The dots to the right of the `?` hang off WhenNotNull, not off the spine, so
                    // a chain the author broke there — `(\n a)?.B\n.C()` — has to be looked for on
                    // that side too; the oracle keeps the `(` on the continuation for it exactly as
                    // for `(\n a).B\n.C()` (SK-DIV-0112).
                    if (BreaksAtADotIn(conditional.WhenNotNull, source)) {
                        return false;
                    }

                    (receiver, operatorToken) = (conditional.Expression, conditional.OperatorToken);
                    break;

                case SwitchExpressionSyntax switchExpression:
                    receiver = switchExpression.GoverningExpression;
                    break;

                case ConditionalExpressionSyntax ternary:
                    receiver = ternary.Condition;
                    break;

                case PostfixUnaryExpressionSyntax postfix:
                    receiver = postfix.Operand;
                    break;

                // ⚠ An expression-bodied lambda is looked through, because the owner an `=` spends
                // its level on is the lambda's body: `f = () =>\n(\n 1, 2);` puts the `(` at the
                // statement's indent, and it was the `=`'s continuation — not the lambda's, which
                // the builder's frame already declines — that put it one level further in. The
                // arrow is not an operator on the spine; nothing is asked of the gap before it.
                case AnonymousFunctionExpressionSyntax { ExpressionBody: { } lambdaBody }:
                    node = lambdaBody;
                    continue;

                default:
                    return false;
            }

            if (BreaksBeforeIn(source, operatorToken)) {
                return false;
            }

            node = receiver;
        }

        return false;
    }

    /// <summary>
    ///     Whether the source breaks before a dot on the left spine of a conditional access's
    ///     <c>WhenNotNull</c> — the <c>.C</c> of <c>?.B.C()</c>.
    /// </summary>
    static bool BreaksAtADotIn(ExpressionSyntax whenNotNull, string source) {
        var node = whenNotNull;
        while (true) {
            switch (node) {
                case InvocationExpressionSyntax invocation:
                    node = invocation.Expression;
                    continue;

                case MemberAccessExpressionSyntax access:
                    if (BreaksBeforeIn(source, access.OperatorToken)) {
                        return true;
                    }

                    node = access.Expression;
                    continue;

                case ElementAccessExpressionSyntax element:
                    node = element.Expression;
                    continue;

                case ConditionalAccessExpressionSyntax nested:
                    if (BreaksAtADotIn(nested.WhenNotNull, source)) {
                        return true;
                    }

                    node = nested.Expression;
                    continue;

                default:
                    return false;
            }
        }
    }

    /// <summary>
    ///     Whether an <c>=</c> clause is an item of a chopped list — the owners on which the oracle was
    ///     measured to give the value a level of its own past the item's. See
    ///     <see cref="GroupPlan.SpendsUnderDelimiters" />.
    /// </summary>
    /// <remarks>
    ///     ⚠ Three owners and not "any <c>=</c> inside a delimiter", because the one header measured
    ///     says otherwise: <c>using (var d =\n default(…))</c> keeps <c>default</c> on the aligned
    ///     column, with no level added, while <c>for (int i =\n 0; …)</c> adds one — two headers, two
    ///     answers, and no rule read off two samples. The list items are consistent across all three.
    /// </remarks>
    static bool FollowsABrokenDeclarationHead(SyntaxNode node) =>
        node is EqualsValueClauseSyntax { Parent: VariableDeclaratorSyntax declarator }
        && CSharpDocumentBuilder.IsFirstDeclaratorBehindItsType(declarator)
        && HasBlockCommentBefore(declarator.Identifier);

    /// <remarks>
    ///     ⚠ And a declarator of a list of them, an initializer's or an incrementor's assignment in a
    ///     <c>for</c> header, and a <c>fixed</c> header's declarator (#468, SK-DIV-0109, SK-DIV-0111).
    ///     Measured 2026-10-08: <c>for (int i =</c> / <c>0;</c>, <c>i +=</c> / <c>1</c>, <c>k = k</c> /
    ///     <c>+ 1</c> and <c>j =</c> / <c>1</c> after <c>int i = 0,</c> all land one level past the
    ///     header's aligned column (17 against 13), <c>fixed (int* p =</c> / <c>&amp;arr[0])</c> at 19
    ///     against 15, and every continuation inside a declarator of a multi-declarator list — a kept
    ///     <c>=</c>, a binary operator, a chopped argument list, a chain's dot, a ternary — one level past
    ///     the list's. ⚠ <c>using (var d =</c> / <c>default(…))</c> was re-asked and still adds none, so
    ///     the <c>using</c> header stays out; and a single declarator outside a header, <c>int z = a</c> /
    ///     <c>+ 1;</c>, is one level in as before.
    /// </remarks>
    static bool IsAListItemsEquals(SyntaxNode node) =>
        node is EqualsValueClauseSyntax { Parent: ParameterSyntax }
            or AssignmentExpressionSyntax { Parent: InitializerExpressionSyntax or ForStatementSyntax }
            or AttributeArgumentSyntax
        || node is EqualsValueClauseSyntax { Parent.Parent: VariableDeclarationSyntax declaration }
        && (declaration.Variables.Count > 1 || declaration.Parent is ForStatementSyntax or FixedStatementSyntax);

    /// <summary>
    ///     How a group whose body is <paramref name="body" /> holds its level: not at all, always, or —
    ///     when a fill chain on the spine may yet break — only while that chain stays whole.
    /// </summary>
    HeldLevel HoldFor(int group, ExpressionSyntax? body) {
        if (!HeadsWithAChoppedParenthesis(body, source, options, out var fillChain)) {
            return HeldLevel.None;
        }

        if (fillChain is null) {
            return HeldLevel.Always;
        }

        heldAgainst[group] = Key(fillChain);
        return HeldLevel.WhileChainWhole;
    }

    /// <summary>
    ///     The chain group a <see cref="HeldLevel.WhileChainWhole" /> hold is decided by, or -1.
    /// </summary>
    public int ChainHeldAgainst(int group) => heldAgainst.TryGetValue(group, out var root) ? ChainGroupOf(root) : -1;

    /// <summary>The arrow group a <see cref="HeldLevel.WhileArrowFlat" /> hold is decided by, or -1.</summary>
    public int ArrowHeldAgainst(int group) => arrowHeldAgainst.TryGetValue(group, out var arrow) ? arrow : -1;

    /// <summary>The group <see cref="PlanChainedCalls" /> opened over the chain rooted at this key, or -1.</summary>
    int ChainGroupOf(long root) => chainGroups.TryGetValue(root, out var group) ? group : -1;

    /// <summary>
    ///     The group <see cref="PlanChainedCalls" /> opened over the chain rooted at <paramref name="root" />, or -1.
    /// </summary>
    public int ChainGroupOf(SyntaxNode root) => ChainGroupOf(Key(root));

    /// <summary>Whether <paramref name="source" /> holds a line break in the gap before this token.</summary>
    internal static bool BreaksBeforeIn(string source, SyntaxToken token) {
        if (token.IsKind(SyntaxKind.None)) {
            return false;
        }

        var previous = token.GetPreviousToken();
        if (previous.IsKind(SyntaxKind.None)) {
            return false;
        }

        for (var i = previous.Span.End; i < token.SpanStart && i < source.Length; i++) {
            if (source[i] == '\n' && !InsideABlockComment(previous, token, i)) {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    ///     Whether <paramref name="position" />, in the gap between the two tokens, is inside a block
    ///     comment — a <c>/* … */</c> or a <c>/** … */</c>.
    /// </summary>
    /// <remarks>
    ///     ⚠ A comment's own line breaks are not the author's break between the tokens (#440). Read as
    ///     one, <c>c ? 1 /* a</c> / <c>b */ : 2</c> was "broken before the <c>:</c>" and a ternary that
    ///     keeps the author's breaks one sign at a time broke there and nowhere else; the oracle chops
    ///     it at both signs, as it chops anything holding a comment that spans lines.
    /// </remarks>
    static bool InsideABlockComment(SyntaxToken previous, SyntaxToken token, int position) =>
        IsInBlockComment(previous.TrailingTrivia, position) || IsInBlockComment(token.LeadingTrivia, position);

    static bool IsInBlockComment(SyntaxTriviaList trivia, int position) {
        foreach (var piece in trivia) {
            if (piece.IsKind(SyntaxKind.MultiLineCommentTrivia)
                || piece.IsKind(SyntaxKind.MultiLineDocumentationCommentTrivia)) {
                if (piece.FullSpan.Contains(position)) {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    ///     Whether <paramref name="source" /> holds a line break between this token and the end of the
    ///     last comment in the gap before it — the whole gap when it holds no comment.
    /// </summary>
    internal static bool BreaksAfterTheLastCommentIn(string source, SyntaxToken token) {
        if (token.IsKind(SyntaxKind.None) || token.GetPreviousToken().IsKind(SyntaxKind.None)) {
            return false;
        }

        for (var i = token.SpanStart - 1; i >= 0 && i < source.Length && char.IsWhiteSpace(source[i]); i--) {
            if (source[i] == '\n') {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    ///     Whether the author broke the line straight after a comment that stands in front of this
    ///     token: <c>2 /* e */</c> / <c>)</c>.
    /// </summary>
    internal static bool BreaksAfterACommentBefore(string source, SyntaxToken token) {
        var previous = token.GetPreviousToken();
        if (token.IsKind(SyntaxKind.None) || previous.IsKind(SyntaxKind.None)) {
            return false;
        }

        var i = token.SpanStart - 1;
        var broke = false;
        for (; i >= previous.Span.End && i < source.Length && char.IsWhiteSpace(source[i]); i--) {
            broke |= source[i] == '\n';
        }

        return broke && i >= previous.Span.End;
    }

    /// <summary>
    ///     <c>place_expr_{method,property,accessor}_on_single_line = if_owner_is_single_line</c>: the
    ///     body shares the declaration's line exactly when the declaration fits on one.
    /// </summary>
    /// <remarks>
    ///     ⚠ <c>skala_keep_existing_expr_member_arrangement = false</c> means a break the author put after the
    ///     <c>=&gt;</c> is <em>not</em> preserved: a short expression-bodied member the author had split
    ///     over two lines is re-joined. It is one of the few places in this export where the formatter
    ///     removes a line break the author chose, and it is measured, not assumed —
    ///     <c>int P =&gt;\n 1;</c> comes back as <c>int P =&gt; 1;</c>.
    /// </remarks>
    void PlanExpressionBody(ArrowExpressionClauseSyntax node) {
        var placement = node.Parent switch {
            PropertyDeclarationSyntax or IndexerDeclarationSyntax => options.PlaceExprPropertyOnSingleLine,
            AccessorDeclarationSyntax => options.PlaceExprAccessorOnSingleLine,
            _ => options.PlaceExprMethodOnSingleLine
        };

        var target = options.WrapBeforeArrowWithExpressions ? node.ArrowToken : FirstToken(node.Expression);
        if (target.IsKind(SyntaxKind.None)) {
            return;
        }

        if (placement == PlacementStyle.Never) {
            Mandatory(target);
            return;
        }

        if (options.WrapBeforeArrowWithExpressions) {
            Flat(FirstToken(node.Expression));
        } else {
            Flat(node.ArrowToken);
        }

        if (placement == PlacementStyle.Always) {
            Flat(target);
            return;
        }

        var group = NewGroup();
        Point(target, group);

        // ⚠ `if_owner_is_single_line`, literally: the owner is the declaration, and it is not single
        // line whenever any break before the arrow is taken. The commonest is a chopped parameter
        // list — the oracle writes
        //     public void RenderPassSetBindGroup(
        //         WebGpuObject pass,
        //         …
        //     ) =>
        //         SetBindGroup(pass, group, bindGroup, dynamicOffsets);
        // and the body's own width says nothing about it: `SetBindGroup(…)` fits on the `) =>` line
        // with sixty columns to spare. A width test on the arrow can never produce this break.
        // ⚠ And not only that one. The oracle breaks the arrow after a filled type parameter list,
        // after a `where` clause moved down, after a kept break inside a type parameter list
        // (SK-DIV-0104) and after a kept break following a modifier — eleven shapes, all with
        // `=> body;` fitting on the head's last line. Reading the parameter list's group for the first
        // and the source for the third answered two of them and disagreed with itself across passes:
        // a type parameter fill's break is not in the source until pass two, so pass one kept the
        // arrow inline and pass two, reading its own break as the author's, moved it (#372). The
        // answer is the writer's line count — a zero-width marker at the head's first token, and the
        // arrow breaks when it is entered on a later line than the marker was. Whoever took the break.
        var head = HeadStartOf(node);
        var owner = -1;
        if (placement == PlacementStyle.IfOwnerIsSingleLine
            && !options.KeepExistingExprMemberArrangement
            && !head.IsKind(SyntaxKind.None)) {
            owner = NewGroup();
            markers[head.SpanStart] = owner;
        }

        Describe(
            node,
            new(
                group,
                GroupMode.Preserve,
                new(
                    // ⚠ Same exception as the `=`'s, and measured the same way: a collection expression
                    // opens with a delimiter of its own, so the arrow's break and the bracket's are
                    // alternatives rather than a pair. `TheoryData<string> Corpus =>\n[…]` comes back
                    // from the oracle as `Corpus => [` when the bracket has to chop, and
                    // `Vector4[] Planes(…) =>\n    [a, b, c];` keeps the arrow's break when it does not.
                    // Leaving both to the ordering rule is what produces the pair — and leaving the
                    // choice to the ordering rule's margin glued `=> [` before a bracket the oracle
                    // moves down whole (a 120-column continuation line, #379). The choice is the
                    // bracket's fit below, exactly as for the `=`: see BreakYieldsToTheBracket.
                    BreaksBefore(target) && !BreakYieldsToTheBracket(node.Expression),
                    PrefersOuterBreak: BreakYieldsToTheBracket(node.Expression),
                    BreaksOnlyIfTailFits: BreakYieldsToTheBracket(node.Expression),
                    Owner: owner,
                    BreaksIfOwnerIsMultiLine: owner >= 0,
                    // skala_keep_existing_expr_member_arrangement = false: a break the author wrote after the
                    // arrow is removed when the declaration fits on one line, and left alone when it
                    // does not. Adding one where the author wrote none is milestone 3's.
                    JoinsIfFits: !options.KeepExistingExprMemberArrangement,
                    // if_owner_is_single_line, the breaking half: the body leaves the declaration's
                    // line exactly when the declaration does not fit on one.
                    // ⚠ Measured against the whole flat width, not the head: "if owner is single line"
                    // means the declaration occupies one line, and a body that spans lines makes it not
                    // single-line however short its first line is. `Target Docs => definition => …` with
                    // a chain under it is the shape that shows the difference. Measuring the head
                    // instead costs 0.12 points of line fidelity on `corpus/real/` and two of the four
                    // preservation corners, which is how the reading was settled rather than argued.
                    // ⚠ And gated on the keep key, the same way a delimited list's placement key is
                    // (see PlanList): `skala_keep_existing_expr_member_arrangement = true` outranks the
                    // placement key in *both* directions, so an arrow the author left on the
                    // declaration's line stays there however unbreakable the body is. Asked directly,
                    // `bool P(object o) => o is {\n First: 1\n };` comes back with the arrow where the
                    // author put it under keep, and moved onto its own line under rearrange — the same
                    // source, the same body, two answers, and only this key between them.
                    BreaksIfTooLong: placement == PlacementStyle.IfOwnerIsSingleLine
                    && !options.KeepExistingExprMemberArrangement
                ),
                true,

                // ⚠ At `skala_wrap_before_arrow_with_expressions = true` the break point IS the gap before the
                // `=>`, and the `=>` is this node's own first token — so the point is written before the
                // group opens, the writer finds the group unresolved, and renders it flat. The same
                // correction a base list needs under `skala_wrap_before_extends_colon` and a list under
                // `wrap_before_*_lpar`; see GroupPlan.LeadingGapInside. Until it was made, `true` never
                // moved the arrow at all and the key's own fixture came back with the declaration
                // whole.
                options.WrapBeforeArrowWithExpressions,

                // ⚠ The level is held at zero when the body opens with a parenthesis the author
                // broke after. The oracle writes `object A() =>\n(\n    1, 2);` — the `(` at the
                // member's own indent, the contents one level in — where Skala put both one level
                // deeper; the paren's own scope supplies the contents' level, and the arrow's was the
                // one too many. See HeadsWithAChoppedParenthesis for the boundary and
                // GroupPlan.HoldsLevel for why the level is held rather than declined (SK-DIV-0101).
                HoldsLevel: HoldFor(group, node.Expression)
            )
        );
    }

    /// <summary>
    ///     A switch expression arm's <c>=&gt;</c>: the gap after it, and the gap before it as the
    ///     fallback when the arrow itself has no room.
    /// </summary>
    /// <remarks>
    ///     ⚠ Neither gap had a plan (issue #378), so both were <c>keep_user_linebreaks</c>' and the
    ///     property pattern heading the arm measured its rest-of-line straight through the arrow into
    ///     the body — through a type argument list's yielding points to the first argument — and
    ///     chopped itself at <c>}</c> = 119 to rescue a tail it cannot rescue. On the second pass the
    ///     body's fill had left a kept break that ended the measure, and the pattern joined again. The
    ///     oracle decides the pattern by its own extent up to the arrow — <c>}</c> at 120 stays, 121
    ///     chops — which is what a point at the arrow gives the trailing measure.
    ///     <para>
    ///         The arrow's own rule is not the <c>=</c>'s. Measured on eleven body shapes: the body
    ///         leaves the head's line only when the head up to the body's first break point does not
    ///         fit — <c>1 =&gt; Body(</c> stays and the arguments chop even when
    ///         <c>Body(first, …, fifth),</c> would have fitted whole on the continuation line, where
    ///         the <c>=</c>'s <see cref="GroupFacts.PrefersOuterBreak" /> would have taken the outer
    ///         break — and <c>… =&gt;</c> / <c>SomeVeryLongIdentifier,</c> when it does not. And the
    ///         arrow is never left past the margin: <c>}</c> at 117 gives <c>{ … } =&gt;</c> /
    ///         <c>Body(</c>, <c>}</c> at 118 — <c> =&gt;</c> ending at 121 — gives <c>{ … }</c> /
    ///         <c>=&gt; Body(</c>, the body continuing on the arrow's line however wide (140 columns
    ///         measured) and the arguments chopping there if they must. The same boundary on a
    ///         non-pattern head, <c>SomeLongConstant.Value when x =&gt;</c>.
    ///         <c>skala_wrap_before_arrow_with_expressions = false</c> is in force; the break before the
    ///         arrow is the fallback and not the option. A break the author wrote on either side is
    ///         kept even when everything fits — <c>1 =&gt;</c> / <c>Body(first),</c> and <c>1</c> /
    ///         <c>=&gt; Body(first),</c> both come back as written.
    ///     </para>
    ///     <para>
    ///         ⚠ Two groups and not two points of one: a group resolved Broken breaks every point it
    ///         owns, and the oracle takes exactly one of the two. The group before the arrow is opened
    ///         by the walk over the arm's children (<see cref="openedAt" />) because no node starts at
    ///         the <c>=&gt;</c>; the body's is opened the same way so that it owns the gap even when the
    ///         body aligns. Both ask to spend the arm's continuation level — the body lands one level in
    ///         from the arm, as a kept break already put it — and the group before the arrow, opened
    ///         first, is the one that gets it. ⚠ So it is that group which holds the level at zero when
    ///         the body opens with a parenthesis the author broke after (SK-DIV-0101), and only while
    ///         the arrow stays on the pattern's line (issue #406, SK-DIV-0157); the body's own hold
    ///         never has a level to hold.
    ///     </para>
    /// </remarks>
    void PlanArmArrow(SwitchExpressionArmSyntax arm) {
        var arrow = arm.EqualsGreaterThanToken;
        if (arrow.IsKind(SyntaxKind.None) || arm.Expression is null) {
            return;
        }

        var before = NewGroup();
        Point(arrow, before);

        // ⚠ An arrow the author put on a line of its own is kept, and then a list in the arm's `when`
        // clause nests from the arm's continuation line, #418's rule (#446, SK-DIV-0212): `X x when
        // Compute(` / the arguments two levels past the arm / `)` one level / `=> 1,` one level,
        // whether the list chopped for width or was chopped by the author. Measured from 100 to 125
        // columns: with the arrow written on the pattern's line it never moves and the arguments sit
        // one level in. A kept break is certain, so the group can open at the arm's start — before the
        // list — without its measure deciding anything.
        var kept = options.KeepsUserBreaksBetweenItems && BreaksBefore(arrow);

        // ⚠ And so does a break the author kept *after* the arrow, for a property pattern's braces and a
        // list pattern's brackets (#549): `X {` / the subpatterns two levels past the arm / `} =>` one
        // level / the body one level, where Skala nested the braces from the arm's own line. Measured
        // 2026-10-08 with `Testing ask` (found reformatting Skala's own SpaceRules.cs): the same with a
        // `when` clause's `prev is {`, as an expression body and under `var x =`; an arrow the author put
        // on a line of its own does it too. With the body on the arrow's line — `X {` / … / `} => 1,` —
        // the braces nest from the arm's line. A group with no point of its own, broken because the
        // break is certain, carrying the arm's continuation level and GroupFacts.Continues from the
        // pattern on.
        // ⚠ Only a head of braces and brackets. The oracle lifts a `when Compute(` / … / `) =>` list
        // too, and a positional pattern's `(` / … / `) =>` puts the elements *and* the `)` one level in,
        // which is neither (SK-DIV-0393); but a list in the head is also what pass one can break for
        // width with the body moved below for width, and pass two would then read the break after the
        // arrow as kept and lift the list — `when Materialise<…,` / `…>() =>` stepped a level on the
        // second pass (generated seed 857717698562573229). The arrow's own width break never comes with
        // braces on the arm's line, so braces alone are stable.
        SyntaxNode[] head = arm.WhenClause is { } clause ? [arm.Pattern, clause] : [arm.Pattern];
        var liftsBraces = head.SelectMany(static part => part.DescendantNodesAndSelf())
                .Any(static node => node is PropertyPatternClauseSyntax or ListPatternSyntax)
            && !head.SelectMany(static part => part.DescendantNodesAndSelf())
                .Any(static node => node is PositionalPatternClauseSyntax
                    or BaseArgumentListSyntax
                    or TypeArgumentListSyntax
                    or AnonymousFunctionExpressionSyntax
                    or InitializerExpressionSyntax
                    or CollectionExpressionSyntax
                    or SwitchExpressionSyntax
                );
        // ⚠ And a `when` clause's argument list (#564), which the lift reaches only once it chops: `when Compute(` / the
        // arguments two levels past the arm / `) =>` one level / the body one level. Measured 2026-10-08
        // written chopped and written whole: whole, the oracle chops the list and keeps the body on the
        // `) =>` line — `) => Body(…),` and `) => "a long string",` past the margin alike — so a break
        // after the arrow beside a chopped list is never the width's, which is what makes the lift
        // idempotent where reading any list in the head was not (generated seed 857717698562573229, a
        // type argument list that fills, still excluded).
        var liftsList = arm.WhenClause is { } when
            && when.DescendantNodes().OfType<ArgumentListSyntax>().Any()
            && !head.SelectMany(static part => part.DescendantNodesAndSelf())
                .Any(static node => node is PositionalPatternClauseSyntax
                        or TypeArgumentListSyntax
                        or AnonymousFunctionExpressionSyntax
                        or InitializerExpressionSyntax
                        or CollectionExpressionSyntax
                        or SwitchExpressionSyntax
                        or QueryExpressionSyntax
                );
        var keptAfter = !kept
            && (liftsBraces || liftsList)
            // ⚠ And never under a body the arrow group holds the level for (#406, SK-DIV-0157).
            && !HeadsWithAChoppedParenthesis(arm.Expression, source, options, out _)
            && options.KeepsUserBreaksBetweenItems
            && BreaksBefore(FirstToken(arm.Expression));
        if (keptAfter) {
            OpenAt(
                arm,
                arm.Pattern.SpanStart,
                new(NewGroup(), GroupMode.Preserve, new(true, Continues: true), true, false)
            );
        }

        OpenAt(
            arm,
            kept && (arm.WhenClause is not null || liftsBraces) ? arm.Pattern.SpanStart : arrow.SpanStart,
            new(
                before,
                GroupMode.Preserve,
                new(
                    kept,
                    BreaksIfTooLong: true,
                    BreaksOnlyIfHeadOverflows: true,
                    Continues: kept && (arm.WhenClause is not null || liftsBraces),
                    LiftsThroughInnerBreaks: kept && arm.WhenClause is not null
                ),
                true,
                !(kept && (arm.WhenClause is not null || liftsBraces)),

                // ⚠ The arm's level is this group's, not the body's: it is opened first and the body's
                // group can spend nothing inside it. So it is this group that holds the level for a
                // body that opens with a parenthesis the author broke after — and only while the arrow
                // stays on the pattern's line (issue #406, SK-DIV-0157). See HeldLevel.WhileFlat.
                HoldsLevel: HoldFor(before, arm.Expression) switch {
                    HeldLevel.None => HeldLevel.None,
                    HeldLevel.Always => HeldLevel.WhileFlat,
                    var hold => hold | HeldLevel.WhileFlat
                }
            )
        );

        PlanArrowBody(
            arm,
            arm.Expression,
            new(
                BreaksIfTooLong: true,
                Owner: before,
                BreaksOnlyIfHeadOverflows: true,
                FlatIfOwnerBroke: true
            )
        );
    }

    /// <summary>A lambda's <c>=&gt;</c>: the gap after it, under the <c>=</c>'s ordering rule.</summary>
    /// <remarks>
    ///     ⚠ Not the switch arm's rule, and the two were measured apart (issue #378). A lambda's body
    ///     moves down whenever that alone finishes the job —
    ///     <c>
    /// Func&lt;int, int&gt; f = someParameterName
    ///     =&gt;
    ///     </c>
    ///     / <c>Convert&lt;CancellationToken, CancellationToken&gt;(…);</c> although
    ///     <c>Convert&lt;…&gt;(</c> still fitted at column 119, and <c>M(someParameterName =&gt;</c> /
    ///     <c>ConvertTheValue&lt;…&gt;(…)</c> / <c>);</c> for a sole argument — and otherwise stays and
    ///     lets the body's own construct wrap: <c>f = x =&gt; Convert(</c> with six arguments chopped
    ///     below, because the whole call would not fit on the continuation line either. That is
    ///     <see cref="GroupFacts.PrefersOuterBreak" />'s two questions in the <c>=</c>'s order, margin
    ///     included: a four-line answer where the same body under a switch arm's arrow keeps
    ///     <c>=&gt; Body(</c> and chops. The point is also what keeps
    ///     <c>
    /// case { … } when static x
    ///     =&gt;
    ///     </c>
    ///     on its label's line — the <c>when</c> measures its head up to this point at column
    ///     105 and stops, where without it the whole type argument list was the head. Only the gap
    ///     after the arrow is planned; the gap before a lambda's arrow stays <c>keep_user_linebreaks</c>'.
    /// </remarks>
    void PlanLambdaArrow(LambdaExpressionSyntax lambda, ExpressionSyntax body) =>
        PlanArrowBody(
            lambda,
            body,
            // ⚠ Over an operand chain the arrow always wins (#453, SK-DIV-0050): the oracle breaks the
            // body's chain in 0 of 5 082 cells of the preference sweep, where an `=` at the same head
            // width breaks it in 3 664 — so the arrow breaks whenever the body does not fit beside it,
            // with no ordering question asked.
            ArrowWinsOverTheChain(lambda)
                ? new GroupFacts(BreaksIfTooLong: true)
                : IsAFilledSoleLambda(lambda, body)
                    ? new GroupFacts(
                        BreaksIfTooLong: true,
                        LambdaParameters: lambda switch {
                            SimpleLambdaExpressionSyntax simple => simple.Parameter.Span.Length,
                            ParenthesizedLambdaExpressionSyntax parenthesized => parenthesized.ParameterList.Span
                                .Length,
                            _ => 1
                        },
                        LambdaHead: lambda.ArrowToken.Span.End - lambda.SpanStart,
                        LambdaIsSimple: lambda is SimpleLambdaExpressionSyntax
                    )
                    : ArrowMovesACallChainDown(body)
                        ? new GroupFacts(BreaksIfTooLong: true, BreaksOnlyIfTailFits: true)
                        : new GroupFacts(BreaksIfTooLong: true, BreaksOnlyIfHeadOverflows: true)
        );

    /// <summary>
    ///     A sole lambda argument whose body is a member access the property fill breaks: its arrow is
    ///     decided by <see cref="GroupFacts.LambdaParameters" />'s measured line (#557).
    /// </summary>
    /// <remarks>
    ///     ⚠ Measured on a statement's call, <c>U(x =&gt; x.A.B…Z)</c>, with parameter lists of one to
    ///     ten columns, lambdas starting at columns 10 to 55 and line ends 112 to 175: 9 of 1 234 cells
    ///     differ, all parenthesised lambdas one column from the boundary. Elsewhere — among other
    ///     arguments, as an <c>=</c>'s value — the arrow breaks when the body fits below, as over a
    ///     chain of calls.
    /// </remarks>
    bool IsAFilledSoleLambda(LambdaExpressionSyntax lambda, ExpressionSyntax body) =>
        options.PlaceSingleMethodArgumentLambdaOnSameLine
        && IsTheBodyOfASoleLambda(body)
        && ChainPointCount(body, options) == 0
        && ArrowMovesACallChainDown(body)
        && lambda.Modifiers.Count == 0;

    /// <summary>
    ///     A lambda whose body is a chain of calls the author did not break: its arrow breaks exactly when
    ///     the whole chain then fits on the line below (#529, SK-DIV-0332).
    /// </summary>
    /// <remarks>
    ///     ⚠ Measured: <c>var r = items.Where(x =&gt;</c> / the chain whole / <c>);</c>, and the same with
    ///     <c>.ToList()</c> after the call; where the chain does not fit below either —
    ///     <c>Use(x =&gt; source.Select(…)</c> / <c>.Where(p)</c> — the arrow stays and the chain breaks,
    ///     which is the arrow's head rule and what Skala did before. The same question the <c>=</c> asks of
    ///     a collection expression (<see cref="GroupFacts.BreaksOnlyIfTailFits" />).
    /// </remarks>
    bool ArrowMovesACallChainDown(ExpressionSyntax body) =>
        IsChainRoot(body)
        && (ChainPointCount(body, options) > 0 || TrailingProperty(body) is not null)
        && source.AsSpan(body.SpanStart, body.Span.Length).IndexOfAny('\r', '\n') < 0;

    /// <summary>
    ///     A lambda that is the value of an <c>=</c>, with a binary operand chain for a body the author
    ///     did not break: the shape the preference sweep measured the arrow winning on (#453).
    /// </summary>
    /// <remarks>
    ///     ⚠ Only as an <c>=</c>'s value, and the boundary is measured on <c>corpus/real/</c>: a sole
    ///     lambda argument keeps its arrow and breaks the chain — Serilog's
    ///     <c>.Where(m =&gt; m.IsDefined(…)</c> / <c>&amp;&amp; m.GetParameters()…</c> — and so does a
    ///     lambda among other arguments whose chain the author broke (<c>x =&gt; x</c> / <c>+ 1</c>).
    /// </remarks>
    bool ArrowWinsOverTheChain(LambdaExpressionSyntax lambda) =>
        lambda.ExpressionBody is BinaryExpressionSyntax binary
        && !IsTypeTest(binary)
        && lambda.Parent is EqualsValueClauseSyntax or AssignmentExpressionSyntax
        && !binary.DescendantNodesAndSelf(static node => node is BinaryExpressionSyntax)
            .OfType<BinaryExpressionSyntax>()
            .Any(link => BreaksBefore(link.OperatorToken) || BreaksBefore(FirstToken(link.Right)));

    /// <summary>
    ///     Whether an <c>=</c>'s value is a lambda whose arrow takes the break the <c>=</c> would
    ///     otherwise take. See <see cref="ArrowWinsOverTheChain" />.
    /// </summary>
    bool YieldsToTheLambdaArrow(ExpressionSyntax value) =>
        value is LambdaExpressionSyntax lambda && ArrowWinsOverTheChain(lambda);

    /// <summary>The group over an arrow's body, opened before the gap that follows the arrow.</summary>
    /// <param name="facts">
    ///     The rule the body breaks by; the kept break and the level are added here. A break the
    ///     author wrote after either arrow is kept even when everything fits.
    /// </param>
    void PlanArrowBody(SyntaxNode owner, ExpressionSyntax body, in GroupFacts facts) {
        var first = FirstToken(body);
        if (first.IsKind(SyntaxKind.None)) {
            return;
        }

        var group = NewGroup();
        arrowGroups[Key(owner)] = group;
        Point(first, group);
        OpenAt(
            owner,
            body.SpanStart,
            new(
                group,
                GroupMode.Preserve,
                facts with { SourceBroken = options.KeepsUserBreaksBetweenItems && BreaksBefore(first) },
                true,
                true,
                HoldsLevel: HoldFor(group, body)
            )
        );
    }

    /// <summary>The gap before a <c>when</c>, in a case label and in a switch expression arm.</summary>
    /// <remarks>
    ///     ⚠ There was no break point before a <c>when</c> at all (issue #378), so a case label whose
    ///     property pattern ended at column 120 chopped the pattern to make room for the clause. The
    ///     oracle breaks before the <c>when</c>, one level in from <c>case</c>, and by the arrow's rule
    ///     rather than the <c>=</c>'s: the clause moves down exactly when <c>when</c> plus the
    ///     condition's head up to its first break point has no room on the label's line —
    ///     <c>case { … }</c> / <c>when Bind(first, …, tenth):</c> — and stays when it has, even when
    ///     the whole clause would have fitted on the line below:
    ///     <c>
    /// case SomeVeryLongTypeName
    ///     someVeryLongVariableName when Bind(
    ///     </c>
    ///     stays and the arguments chop, in a label with a
    ///     declaration pattern and in an arm with <c>{ … } when Bind(</c> / <c>) =&gt; Body(first),</c>
    ///     alike. A kept break before the <c>when</c> is kept (<c>case 1</c> / <c>when x:</c>), and so
    ///     is one after it, which this plan leaves to <c>keep_user_linebreaks</c>.
    /// </remarks>
    void PlanWhenClause(WhenClauseSyntax node) {
        var keyword = node.WhenKeyword;
        if (keyword.IsKind(SyntaxKind.None)) {
            return;
        }

        var group = NewGroup();
        Point(keyword, group);
        Describe(
            node,
            group,
            GroupMode.Preserve,
            new(
                options.KeepsUserBreaksBetweenItems && BreaksBefore(keyword),
                BreaksIfTooLong: true,
                BreaksOnlyIfHeadOverflows: true
            ),
            // spendsIndent, leadingGapInside: the gap before the `when` is the group's own first
            // point, so the group has to open before it (GroupPlan.LeadingGapInside).
            true,
            true
        );
    }

    void OpenAt(SyntaxNode node, int position, GroupPlan plan) {
        var key = (Key(node), position);
        if (!openedAt.TryGetValue(key, out var plans)) {
            openedAt[key] = plans = [];
        }

        plans.Add(plan);
        byId[plan.Id] = plan;
    }

    /// <summary>
    ///     A property pattern's braces and its subpatterns: <c>skala_wrap_property_pattern</c>,
    ///     <c>skala_keep_existing_property_patterns_arrangement</c> and
    ///     <c>skala_place_simple_property_pattern_on_single_line</c>.
    /// </summary>
    /// <remarks>
    ///     ⚠ Two groups, the braced initializer's split (<see cref="PlanBracedElements" />), and not
    ///     <see cref="PlanList" />'s one. Measured on twenty shapes for issue #378: a property pattern
    ///     that overflows puts its braces on their own lines <em>first</em>, and chops the subpatterns
    ///     only when they still do not fit on the continuation line — <c>case {</c> /
    ///     <c>Length: &gt; 0, Name: "…"</c> / <c>}:</c> for two subpatterns of 92 columns, one per line for
    ///     seven that reach 123; <c>SomeType {</c> / <c>Length: …, Name: …</c> / <c>} =&gt; 1,</c> in an
    ///     arm and <c>value is {</c> / … / <c>}</c> in a statement the same way. The one-group plan
    ///     chopped every subpattern as soon as the braces broke. ⚠ The issue's reading — "in a case label
    ///     the chopped pattern fills, in an arm it chops" — was two ends of one rule: a bare <c>{</c> that
    ///     opens an arm's line has four columns of continuation against the four its <c>{ </c> and
    ///     <c> }</c> took, so its subpatterns never fit below when the whole did not fit above, and it was
    ///     never a fill — seven subpatterns whose first four would have shared a line came back one per
    ///     line.
    ///     <para>
    ///         The keys keep the readings <see cref="PlanList" /> established: the placement key at
    ///         <c>false</c> forces the braces apart and re-flows the subpatterns by width (the inner group
    ///         decides at the continuation column, which is what "keeps a three-subpattern clause of 98
    ///         columns together" was measuring), <c>chop_always</c> is gated on the keep key, and the
    ///         gap before an opening <em>bracket</em> is not this construct's.
    ///     </para>
    /// </remarks>
    void PlanPropertyPattern(PropertyPatternClauseSyntax node) {
        var open = node.OpenBraceToken;
        var close = node.CloseBraceToken;
        var items = node.Subpatterns;
        if (open.IsKind(SyntaxKind.None) || close.IsKind(SyntaxKind.None) || items.Count == 0) {
            return;
        }

        var keepExisting = options.KeepExistingPropertyPatternsArrangement;
        var joins = options.PlaceSimplePropertyPatternOnSingleLine && !keepExisting;
        var forced = !options.PlaceSimplePropertyPatternOnSingleLine
            && !keepExisting
            // ⚠ And under a break the author kept after a subpattern's colon (#561, SK-DIV-0395): the
            // value's braces break open however short it is — `Expression:` / `Bar {` / `A: 1` / `}`, and
            // `Expression:` / `MemberAccessExpressionSyntax {` / … / `} access` at every width measured
            // around the margin, 66 to 118 columns on its own line. Measured 2026-10-08 with
            // `Testing ask`, found in Skala's own ReflectiveTypeTestAnalyzer.cs.
            || !keepExisting
            && node.Parent is RecursivePatternSyntax {
                Parent: SubpatternSyntax { ExpressionColon: not null } holder
            } value
            && holder.Pattern == value
            && options.KeepsUserBreaksBetweenItems
            && FirstToken(value) is var head
            && !head.IsKind(SyntaxKind.OpenBraceToken)
            && BreaksBefore(head);
        var chopsAlways = options.WrapPropertyPattern == WrapStyle.ChopAlways && !keepExisting;

        var outer = NewGroup();
        var first = FirstToken(items[0]);
        var delimiterBroken = BreaksBefore(first) || BreaksBefore(close);
        Point(first, outer);
        Point(close, outer);

        var inner = NewGroup();
        var interBroken = false;
        foreach (var comma in items.GetSeparators()) {
            var next = comma.GetNextToken();
            if (next.IsKind(SyntaxKind.None) || next.SpanStart >= close.SpanStart) {
                continue;
            }

            var gap = options.WrapBeforeComma ? comma : next;
            var other = options.WrapBeforeComma ? next : comma;
            Point(gap, inner);
            Flat(other);
            interBroken |= BreaksBefore(gap);
        }

        var keeps = options.KeepsUserBreaksBetweenItems;
        var broken = chopsAlways || forced || keeps && interBroken || keeps && keepExisting && delimiterBroken;

        Describe(
            node,
            outer,
            chopsAlways || forced ? GroupMode.Break : GroupMode.Preserve,
            new(broken, joins, true, HidesFlatWidthWhenBroken: true)
        );

        DescribeInner(
            node,
            inner,
            chopsAlways ? GroupMode.Break : GroupMode.Preserve,
            new(
                chopsAlways || keeps && interBroken,
                joins || forced,
                true,
                HidesFlatWidthWhenBroken: true
            )
        );
    }

    /// <summary>
    ///     <c>skala_place_simple_embedded_statement_on_same_line = if_owner_is_single_line</c>: the statement
    ///     shares its owner's line exactly when the owner fits on one.
    /// </summary>
    /// <remarks>
    ///     ⚠ <c>skala_keep_existing_embedded_arrangement = true</c> in this export, and it outranks the
    ///     placement key in <em>both</em> directions — which is not what this plan used to say. Asked
    ///     one value at a time over the export, with keep at its own value:
    ///     <code>
    /// // skala_place_simple_embedded_statement_on_same_line = never, keep = true
    /// if (c) M(c, d);                     ← left joined. `never` does not get to break it.
    /// if (depth &lt; 0) throw new …(…);      ← broken, because the `if` overflows the margin
    ///     </code>
    ///     So the break the previous note attributed to "`never` is not gated on keep" is the width
    ///     rule and nothing else: under keep the placement key is inert in both directions, and what
    ///     survives is "an owner that does not fit on one line pushes its statement off that line".
    ///     That is why <c>BreaksIfTooLong</c> below is true whenever keep is.
    ///     <para>
    ///         ⚠ And "simple" is load-bearing, in two halves. With keep off and the key at
    ///         <c>always</c> the oracle joins <c>if (c) M(c, d);</c> and <c>while (c) M(c, d);</c> and
    ///         leaves every one of <c>if (c) / if (d) / M()</c>, <c>if (c) / using (…) / M()</c> and the
    ///         nested <c>for</c> exactly where the author put them — so a statement that carries an
    ///         embedded statement of its own is not simple. ⚠ "Where the author put them" was measured on
    ///         broken input only: written on one line, the oracle pushes every one of them off (#519,
    ///         <see cref="IsPushedOffByNesting" />).
    ///     </para>
    ///     <para>
    ///         ⚠ That alone is not enough, and the probe that says so is the one that separates "nested"
    ///         from "embedded". Put the same nesting inside a block and the oracle joins it:
    ///         <code>
    /// if (c) {                       if (c) {                    if (c)
    ///     if (d)            →            if (d) M(c, d);             if (d)
    ///         M(c, d);                }                                   M(c, d);
    /// }                                                          ← unmoved
    ///         </code>
    ///         An <c>else</c> clause joins too. So the second half is a fact about the <em>owner</em>:
    ///         an owner that is itself somebody's embedded statement does not get to join, and one
    ///         whose statement merely sits inside a block does.
    ///     </para>
    /// </remarks>
    void PlanEmbeddedStatement(SyntaxNode owner, StatementSyntax? embedded) {
        if (embedded is null or BlockSyntax) {
            return;
        }

        var first = FirstToken(embedded);
        if (first.IsKind(SyntaxKind.None)) {
            return;
        }

        if (NeverSharesItsOwnersLine(embedded)) {
            Mandatory(first);
            return;
        }

        // ⚠ At every value of the keep key and of the placement key (#469, #519): measured at keep and
        // at `false` under `always`, `if_owner_is_single_line` and `never`, every nesting written on one
        // line comes back one statement per line.
        var keeps = options.KeepExistingEmbeddedArrangement;
        if (IsPushedOffByNesting(owner, embedded)) {
            Mandatory(first);
            return;
        }

        var placement = options.PlaceSimpleEmbeddedStatementOnSameLine;

        // ⚠ And an `if` with an `else` keeps its statement as a simple owner does, though it is itself
        // embedded (#469, #519): its `else` starts a line of its own (#480), so a group over the whole
        // `if` would read that break as the statement not fitting. Measured at keep and at `always`;
        // at `if_owner_is_single_line` the owner is multi-line for the same reason, and it breaks.
        var simple = IsSimpleEmbeddedStatement(owner, embedded)
            || owner is IfStatementSyntax { Else: not null }
            && EmbeddedStatementOf(embedded) is null;

        if (!keeps && placement == PlacementStyle.Never) {
            Mandatory(first);
            return;
        }

        if (!keeps && placement == PlacementStyle.Always && simple) {
            Flat(first);
            return;
        }

        var group = NewGroup();

        // ⚠ Under keep, a simple statement's gap is a *fill point*, and the author's own break there
        // is pinned. "An owner that does not fit on one line pushes its statement off that line" was
        // read as "an owner that is multi-line does", and the oracle separates the two (SK-DIV-0106):
        //     while (                       while (c
        //         c) n++;                          && n > 0) n--;     ← both kept on the `)` line
        // A header the author broke — after the `(`, before an operator — keeps its statement on the
        // closing line, and so does a header the oracle itself chops for width: a 125-column
        // `while (…) n++;` comes back with every `&&` on its own line and `n++` still after the `)`.
        // What pushes the statement off is the *last* line of the header not having room for it —
        // `if (depth < 0) throw new …(…);` where the throw does not fit — which is exactly what a fill
        // point measures, at the column the writer has actually reached after the header. A group
        // point measured the whole owner instead, and any kept break inside the header made that
        // width unbounded.
        // ⚠ Simple owners only. An owner that carries an embedded statement of its own — `if (\n c) if
        // (d) n++;` — is pushed off by the oracle whenever it is multi-line, and keeps the group point.
        if (keeps && simple) {
            if (BreaksBefore(first)) {
                Mandatory(first);
            } else {
                Point(first, group, true, true);
            }

            Describe(owner, group, GroupMode.Preserve, new(BreaksIfTooLong: true));
            return;
        }

        // ⚠ `if_owner_is_single_line` reads an `else`'s owner as the whole `if` (#519): at
        // `keep = false` the oracle writes `else` / `M();` whenever the `if` spans lines, which with a
        // statement that is not a block it always does, and `} else` / `M();` after a block. A group
        // over the `else` clause alone saw `else M();` fit.
        Point(first, group);
        Describe(
            owner is ElseClauseSyntax { Parent: IfStatementSyntax statement } ? statement : owner,
            group,
            GroupMode.Preserve,
            new(
                BreaksBefore(first),
                // Only `always` and `if_owner_is_single_line` join, only a simple statement joins,
                // and keep outranks all of it.
                !keeps && placement != PlacementStyle.Never && simple,
                keeps || placement == PlacementStyle.IfOwnerIsSingleLine
            )
        );
    }

    /// <summary>
    ///     A <c>using</c> directly inside a <c>using</c>, which <see cref="EmbeddedStatementOf" /> leaves
    ///     out so that a stacked pair the author wrote is never joined: written on one line, the oracle
    ///     stacks it (#469).
    /// </summary>
    /// <remarks>
    ///     ⚠ <c>using (D()) using (D()) M();</c> comes back <c>using (D())</c> / <c>using (D())</c> /
    ///     <c>M();</c> under the export's <c>keep_existing_embedded_arrangement = true</c> — the inner
    ///     <c>using</c> carries a statement of its own, which pushes it off its owner's line like any
    ///     other nesting. The column is <c>skala_indent_nested_usings_stmt</c>'s business, not this one's.
    /// </remarks>
    void PlanStackedUsing(SyntaxNode node) {
        if (options.KeepExistingEmbeddedArrangement
            && node is UsingStatementSyntax { Statement: UsingStatementSyntax inner }) {
            Mandatory(FirstToken(inner));
        }
    }

    /// <summary>
    ///     An <c>else</c>, or a <c>do</c>'s <c>while</c>, after a statement that is not a block starts a line
    ///     of its own (#480, SK-DIV-0115).
    /// </summary>
    /// <remarks>
    ///     ⚠ Measured 2026-10-08 with <c>Testing ask</c>, at both values of <c>skala_new_line_before_else</c>
    ///     and of <c>skala_keep_existing_embedded_arrangement</c>: <c>if (b) M(); else M();</c> comes back
    ///     <c>if (b) M();</c> / <c>else M();</c>, and so does every <c>else if</c> of a chain, an
    ///     <c>else</c> after an embedded <c>switch</c>'s or <c>try</c>'s <c>}</c> (<c>}</c> / <c>else M();</c>
    ///     where a block's would be <c>} else M();</c>), and an <c>else</c> behind a block comment
    ///     (<c>M(); /* c */</c> / <c>else M();</c>). <c>do M(); while (b);</c> comes back <c>do M();</c> /
    ///     <c>while (b);</c> the same way. The placement keys only ever governed the gap after a
    ///     <em>block</em>'s <c>}</c>, which is all <c>ShouldJoin</c> reads; this gap had no plan and kept
    ///     whatever the author wrote.
    /// </remarks>
    void PlanClauseAfterAnEmbeddedStatement(SyntaxNode node) {
        switch (node) {
            case IfStatementSyntax { Else: { } clause, Statement: not BlockSyntax }:
                Mandatory(clause.ElseKeyword);
                return;

            case DoStatementSyntax { Statement: not BlockSyntax } loop:
                Mandatory(loop.WhileKeyword);
                return;
        }
    }

    /// <summary>Whether this node is itself somebody else's embedded statement.</summary>
    static bool IsEmbeddedStatement(SyntaxNode node) =>
        node.Parent is { } parent && EmbeddedStatementOf(parent) == node;

    /// <summary>
    ///     Under <c>skala_keep_existing_embedded_arrangement</c>, an embedded statement the oracle puts on a
    ///     line of its own however it was written and however short it is: one that carries an embedded
    ///     statement of its own, and one whose owner is itself an embedded statement (#469, SK-DIV-0106,
    ///     SK-DIV-0115).
    /// </summary>
    /// <remarks>
    ///     ⚠ Measured 2026-10-08 with <c>Testing ask</c> on twenty-eight nestings written on one line.
    ///     <c>if (b) if (c) M();</c>, <c>if (b) using (D()) M();</c>, <c>if (b) while (c) M();</c>,
    ///     <c>while (b) if (c) M();</c>, <c>foreach (…) if (c) M();</c>, <c>lock (this) if (c) M();</c>,
    ///     <c>if (b) for (;;) M();</c>, <c>while (b) lock (this) M();</c>, <c>do if (b) M(); while (b);</c>,
    ///     <c>using (D()) using (D()) M();</c>, three deep, and <c>if (b) if (c) { M(); }</c> all come back
    ///     one statement per line — the fitting, single-line inner owner included, which is what the
    ///     group point (a width test) could never write. Two shapes are exempt, and both are an
    ///     <c>if</c> that the <c>else</c> machinery owns rather than a nesting: an <c>if</c> with an
    ///     <c>else</c> keeps its own statement — <c>if (b)</c> / <c>if (c) M();</c> / <c>else M();</c>,
    ///     under <c>while</c> too — and an <c>else if</c> keeps its — <c>else if (c) M();</c>. Neither
    ///     exemption carries over to the statement after them: <c>else if (b) if (c) M();</c> pushes
    ///     <c>if (c)</c> down and <c>M()</c> with it.
    ///     <para>
    ///         ⚠ At <c>keep = false</c> as well, at every placement value (#519): measured on the same
    ///         nestings, written on one line and written broken.
    ///     </para>
    /// </remarks>
    static bool IsPushedOffByNesting(SyntaxNode owner, StatementSyntax embedded) {
        if (CarriesAnEmbeddedStatement(embedded)) {
            return true;
        }

        if (owner is IfStatementSyntax { Else: not null } or IfStatementSyntax { Parent: ElseClauseSyntax }) {
            return false;
        }

        return IsEmbeddedStatement(owner) || owner.Parent is UsingStatementSyntax outer && outer.Statement == owner;
    }

    static bool CarriesAnEmbeddedStatement(StatementSyntax statement) =>
        statement is IfStatementSyntax
            or WhileStatementSyntax
            or DoStatementSyntax
            or ForStatementSyntax
            or CommonForEachStatementSyntax
            or UsingStatementSyntax
            or FixedStatementSyntax
            or LockStatementSyntax;

    /// <summary>
    ///     ⚠ A <c>switch</c> or a <c>try</c> never shares its owner's line, at the export's
    ///     <c>keep = true</c> and whatever the source wrote: <c>if (b) switch (o) { case 1: break; }</c>,
    ///     the same under <c>while</c>, <c>foreach</c>, <c>lock</c> and after <c>else</c>, an
    ///     <em>empty</em> <c>if (b) switch (o) { }</c>, and <c>if (b) try { M(); } finally { }</c> all
    ///     come back with the statement on the next line (measured for #374). Neither is "simple" in
    ///     any reading, and the width rule cannot reach the empty switch, which fits; so the break is
    ///     required rather than planned.
    /// </summary>
    static bool NeverSharesItsOwnersLine(StatementSyntax embedded) =>
        embedded is SwitchStatementSyntax or TryStatementSyntax;

    /// <summary>
    ///     "Simple", in both halves <see cref="PlanEmbeddedStatement" /> measures: the statement carries
    ///     no embedded statement of its own, and its owner is not itself somebody's embedded statement.
    /// </summary>
    static bool IsSimpleEmbeddedStatement(SyntaxNode owner, StatementSyntax embedded) =>
        EmbeddedStatementOf(embedded) is null && !IsEmbeddedStatement(owner);

    /// <summary>
    ///     A <em>simple</em> switch section — one simple statement, with or without a <c>break;</c> after
    ///     it — keeps the arrangement the author gave it and fills when it does not fit; any other
    ///     section puts every statement on a line of its own, and stacked labels each take a line.
    /// </summary>
    /// <remarks>
    ///     ⚠ <c>skala_place_simple_case_statement_on_same_line</c> is read and deliberately not applied, which
    ///     is a reversal: this plan used to force the break at <c>never</c> and remove it at
    ///     <c>always</c>, and the sweep called the row <c>SPURIOUS</c> because only Skala moved. The key
    ///     is inert under <c>cleanupcode</c>, and it was asked in both directions rather than one:
    ///     <list type="bullet">
    ///         <item>
    ///             a section written broken — one statement, two statements, an empty <c>break;</c>, a
    ///             shared label, a braced section — at <c>always</c>: unchanged.
    ///         </item>
    ///         <item>a section written joined at <c>never</c>: unchanged.</item>
    ///         <item>both of those again with <c>keep_user_linebreaks = false</c>: unchanged.</item>
    ///         <item>
    ///             ⚠ and both again with <c>simple_case_statement_style</c> pushed the same way —
    ///             <c>on_single_line</c> against the broken source, <c>line_break</c> against the joined
    ///             one — because that key is the one this repository's registry claims is masked by this
    ///             one. Unchanged. Neither key governs the shape, and the registry's note on
    ///             <c>simple_case_statement_style</c> ("that key is what governs the shape under this
    ///             export") is wrong about which key wins for the usual reason — it was asked at
    ///             <c>never</c> against a source that was already broken.
    ///         </item>
    ///     </list>
    ///     <para>
    ///         ⚠ "The oracle simply preserves the section", which this remark used to conclude, was
    ///         measured on simple sections only and is false of the rest (issue #374, SK-DIV-0115).
    ///         Re-measured 2026-09-17 with <c>Testing ask</c> on forty section shapes, the export's
    ///         values and each of <c>keep_existing_embedded_block_arrangement</c>,
    ///         <c>keep_existing_declaration_block_arrangement</c>, <c>keep_user_linebreaks</c>,
    ///         <c>csharp_preserve_single_line_blocks</c> and this key flipped on its own — none moved a
    ///         section. What the oracle preserves is a section whose statements are <c>[S]</c> or
    ///         <c>[S, break;]</c> where <c>S</c> is an expression, an empty statement, a <c>return</c>,
    ///         <c>throw</c>, <c>break</c>, <c>continue</c>, <c>goto</c> or <c>yield</c>:
    ///         <c>case 1: M(); break;</c>, <c>case 1: x = 1; break;</c>, <c>case 1: await T(); break;</c>,
    ///         <c>case 1: Run(() =&gt; { M(); }); break;</c>, <c>case 1: yield return 1; break;</c>,
    ///         <c>case 1: return 1;</c>, <c>default: throw new Exception();</c> and <c>case 1: ;</c> all
    ///         come back as written. Everything else is broken one statement per line, the first off
    ///         the label, whatever the author wrote: <c>M(); M(); break;</c>, <c>M(); return;</c>,
    ///         <c>M(); goto case 2;</c>, <c>M(); continue;</c>, <c>yield return 3; yield break;</c>, a
    ///         lone <c>int y = 1;</c> or <c>var y = 1; break;</c>, a lone <c>if (b) M();</c>, and
    ///         <c>lock (o) M(); break;</c> — so "simple" is about the statement's kind and not about
    ///         its count, and a second statement is tolerated only when it is the <c>break</c>.
    ///     </para>
    ///     <para>
    ///         A simple section is a fill, not an all-or-nothing group, and the previous group here was
    ///         the latter: <c>case 1:</c> / <c>M(); break;</c> came back from Skala as three lines and
    ///         from the oracle as written. Measured on the label gap and the gap before the <c>break</c>
    ///         separately: a kept label break stays, and the tail behind it fills (<c>M(); break;</c>
    ///         together at 120 columns, <c>break;</c> pushed down at 121); a joined 121-column section
    ///         moves <c>M(); break;</c> off the label together and splits them only when the tail still
    ///         overflows. ⚠ But a break the author wrote <em>between</em> the two statements —
    ///         <c>case 2: M();</c> / <c>break;</c> — breaks the label gap as well: the oracle answers
    ///         <c>case 2:</c> / <c>M();</c> / <c>break;</c>, and a fill would have kept the first line.
    ///         The label gap is pinned when the source broke it because a fill point re-decides by width
    ///         and would re-join it (SK-DIV-0104's trap).
    ///     </para>
    ///     <para>
    ///         Stacked labels never share a line — <c>case 1: case 2: break;</c> comes back as
    ///         <c>case 1:</c> / <c>case 2: break;</c> — and a braced section is the block's own
    ///         business: <see cref="PlanOnePerLine" /> expands it, and <see cref="Keeps" /> reads the
    ///         <em>embedded</em> key for it, which is the one that kept <c>case 1: { M(); }</c> whole.
    ///         ⚠ A block that is one statement among several (<c>case 1: { M(); } break;</c>): the oracle
    ///         writes <c>case 1: {</c> with the block at the label's column and <c>break;</c> one level in
    ///         (#478). A leading block's gap is left to the brace placement here, and
    ///         <see cref="CSharpDocumentBuilder.VisitSwitchSection" /> puts every block on the label's column.
    ///     </para>
    /// </remarks>
    void PlanCaseStatements(SwitchSectionSyntax node) {
        for (var i = 1; i < node.Labels.Count; i++) {
            Mandatory(FirstToken(node.Labels[i]));
        }

        if (node.Statements.Count == 0 || node.Statements is [BlockSyntax]) {
            return;
        }

        var first = FirstToken(node.Statements[0]);
        if (!IsSimpleSection(node.Statements)) {
            // ⚠ Except a block that comes first, which the brace placement joins to the label as it
            // does a section that is only a block: `case 1: {` under the export, `case 1:` / `{` under
            // `csharp_new_line_before_open_brace = all` (#478). See CSharpDocumentBuilder.VisitSwitchSection.
            foreach (var statement in node.Statements) {
                if (statement is BlockSyntax && statement == node.Statements[0]) {
                    continue;
                }

                Mandatory(FirstToken(statement));
            }

            return;
        }

        var tail = node.Statements.Count == 2 ? FirstToken(node.Statements[1]) : default;
        if (!tail.IsKind(SyntaxKind.None) && BreaksBefore(tail)) {
            Mandatory(first);
            Mandatory(tail);
            return;
        }

        // ⚠ The label gap is the group's point and the `break`'s is a fill: a 121-column section
        // moves `M(); break;` off the label *together*, which a fill at the label gap — measuring only
        // as far as the next point — never did, and the `break` then leaves only if the tail still
        // overflows. A pinned label gap makes the group unbounded, so the fill still decides.
        // ⚠ An inner group, opened by the builder at the *last* label: described on the section it
        // would span the stacked labels' required breaks, measure as unbounded, and push `break;`
        // off `case 2:` in `case 1:` / `case 2: break;`.
        var group = NewGroup();
        if (BreaksBefore(first)) {
            Mandatory(first);
        } else {
            Point(first, group);
        }

        Point(tail, group, true);
        DescribeInner(node, group, GroupMode.Preserve, new(BreaksIfTooLong: true));
    }

    /// <summary>
    ///     <c>[S]</c> or <c>[S, break;]</c>, where <c>S</c> is a statement with nothing nested in it — the
    ///     shape the oracle lets share the label's line. See <see cref="PlanCaseStatements" />.
    /// </summary>
    static bool IsSimpleSection(SyntaxList<StatementSyntax> statements) =>
        statements switch {
            [var only] => IsSimpleStatement(only),
            [var head, BreakStatementSyntax] => IsSimpleStatement(head),
            _ => false
        };

    static bool IsSimpleStatement(StatementSyntax statement) =>
        statement is ExpressionStatementSyntax
            or EmptyStatementSyntax
            or ReturnStatementSyntax
            or ThrowStatementSyntax
            or BreakStatementSyntax
            or ContinueStatementSyntax
            or GotoStatementSyntax
            or YieldStatementSyntax;

    /// <summary>
    ///     Every member and every statement gets a line of its own.
    /// </summary>
    /// <remarks>
    ///     ⚠ Unconditional, which is not what the option names suggest and is what the oracle does.
    ///     <c>csharp_preserve_single_line_blocks = true</c> is in the export and reads like permission
    ///     to leave <c>void M() { Call(); Call(); }</c> alone; ReSharper ignores it there, and
    ///     <c>class B { public int P => 1; public int Q => 2; }</c> comes back as five lines. There is
    ///     no width test and no <c>keep_user_linebreaks</c> in it: a body with anything in it is broken.
    ///     ⚠ Not ignored everywhere, measured for #405: at <c>false</c> the oracle also expands every
    ///     one-statement accessor, lambda and anonymous-method block, <c>get { return _n; }</c> included.
    ///     <see cref="MayShareItsOwnersLine" /> reads the key, below the keep keys that outrank it (#510).
    ///     <para>
    ///         ⚠ Two exclusions, each measured rather than assumed. An <em>empty</em> body stays together
    ///         (<c>skala_empty_block_style = together</c>). And a one-statement block that may share its
    ///         owner's line — an accessor's, a lambda's, an anonymous method's always, a method's or an
    ///         <c>if</c>'s under its <c>keep_existing_*_block_arrangement</c> key — does exactly when its
    ///         statement ends up on that line (<see cref="MayShareItsOwnersLine" />, issue #405).
    ///         ⚠ This used to read "an accessor's body does not break" and "a lambda's block does not",
    ///         from <c>get { return _street; }</c> and <c>Register(() => { Body(); });</c>, which come back
    ///         whole because they fit; <c>get { return Math.Max(</c>↵<c>…); }</c> and
    ///         <c>() => { A(); B(); }</c> are broken open.
    ///     </para>
    ///     <para>
    ///         It is also what makes "single line" a stable property of the output. A member sharing a line
    ///         with the member before it has no answer to <c>skala_blank_lines_around_single_line_field</c>, which
    ///         is why <c>constructs/blank-lines/two-members-on-one-line.cs</c> was committed failing at M2.
    ///     </para>
    ///     <para>
    ///         ⚠ A <c>switch</c> statement's sections are statements' peers here and were not (issue #374,
    ///         SK-DIV-0115): <c>switch (o) { case 1: break; case 2: break; }</c> comes back from the oracle
    ///         as four lines, one section each and the brace on its own, while the one-line <c>if</c>
    ///         block beside it was already expanded. Unconditional like the rest, and measured to be:
    ///         <c>skala_keep_existing_embedded_block_arrangement = true</c> keeps <c>if (b) { M(); }</c>
    ///         and <c>case 1: { M(); }</c> and still expands the sections; the declaration key,
    ///         <c>keep_user_linebreaks = false</c> and <c>csharp_preserve_single_line_blocks = false</c>
    ///         move nothing. An empty <c>switch (o) { }</c> stays together, as an empty block does. What
    ///         happens <em>inside</em> a section is <see cref="PlanCaseStatements" />'.
    ///     </para>
    /// </remarks>
    void PlanOnePerLine(SyntaxNode node) {
        switch (node) {
            case BlockSyntax { Statements.Count: > 0 } block when MayShareItsOwnersLine(block):
                var group = NewGroup();
                Point(FirstToken(block.Statements[0]), group);
                Point(block.CloseBraceToken, group);
                var head = HeadMarkerOf(block);
                Describe(
                    block,
                    group,
                    GroupMode.Preserve,
                    new(
                        BreaksIfTooLong: true,
                        Owner: head,
                        BreaksIfOwnerIsMultiLine: head >= 0
                    )
                );
                return;

            case BlockSyntax { Statements.Count: > 0 } block:
                foreach (var statement in block.Statements) {
                    Mandatory(FirstToken(statement));
                }

                Mandatory(block.CloseBraceToken);
                return;

            case SwitchStatementSyntax { Sections.Count: > 0 } statement:
                foreach (var section in statement.Sections) {
                    Mandatory(FirstToken(section));
                }

                Mandatory(statement.CloseBraceToken);
                return;

            case TypeDeclarationSyntax { Members.Count: > 0 } type
                when !options.KeepExistingDeclarationBlockArrangement:
                MembersOnOwnLines(type.Members, type.CloseBraceToken);
                return;

            // ⚠ A namespace's `using` and `extern alias` are its lines too: `namespace N { using System; }`
            // comes back from the oracle as three lines, with no member to have put it there (#429).
            case NamespaceDeclarationSyntax declaration
                when !options.KeepExistingDeclarationBlockArrangement
                && declaration.Members.Count + declaration.Usings.Count + declaration.Externs.Count > 0:
                OnOwnLines(declaration.Externs);
                OnOwnLines(declaration.Usings);
                MembersOnOwnLines(declaration.Members, declaration.CloseBraceToken);
                return;

            // ⚠ The file's own level had no arm, so nothing put a top-level declaration on a line of its
            // own: `public class A { } public class B { }`, `using A; using B;` and `/* top */ public
            // class D { }` all stayed one line, where the oracle breaks before each (#429). A type's
            // members were planned and the file's were not, and #409's comment rule only ever reaches a
            // gap the plan has. Measured on every kind the level holds — `extern alias`, `using`, an
            // `[assembly: …]` list, a type, a delegate, an enum, a namespace, a top-level statement —
            // with and without a block comment before it, and unmoved by
            // `skala_keep_existing_declaration_block_arrangement`: the oracle answers it with the same
            // bytes at both values, so this is not a declaration block's key.
            case CompilationUnitSyntax unit:
                OnOwnLines(unit.Externs);
                OnOwnLines(unit.Usings);
                OnOwnLines(unit.AttributeLists);
                OnOwnLines(unit.Members);
                return;

            case FileScopedNamespaceDeclarationSyntax declaration:
                OnOwnLines(declaration.Externs);
                OnOwnLines(declaration.Usings);
                OnOwnLines(declaration.Members);
                return;

            case AccessorListSyntax { Accessors.Count: > 0 } accessors:
                PlanAccessorList(accessors);
                return;

            default:
                return;
        }
    }

    /// <summary>
    ///     A property's, an indexer's or an event's accessor list: on the owner's line or one accessor
    ///     per line, decided by what the accessors are and never by where the author broke it.
    /// </summary>
    /// <remarks>
    ///     ⚠ An accessor list is a declaration block by <c>skala_keep_existing_declaration_block_arrangement</c>'s
    ///     own definition, and this had no arm: Skala kept whatever the author wrote, in both directions
    ///     (issues #416 and #417, SK-DIV-0096 and SK-DIV-0183). Measured on seventeen shapes, each written
    ///     four ways — on one line, one accessor per line, all accessors on one inner line, and the first
    ///     accessor on the brace's line — at two indent depths:
    ///     <list type="bullet">
    ///         <item>
    ///             Under the export (the key <c>false</c>), the four spellings of every shape come back
    ///             byte-identical. A list whose accessors are all bodiless — <c>{ get; set; }</c>,
    ///             <c>{ get; private set; }</c>, <c>{ get; init; }</c>, <c>{ readonly get; set; }</c>, an
    ///             abstract indexer's, with or without an initializer after it — is joined onto the owner's
    ///             line; any accessor with a body or an expression body puts every accessor on its own
    ///             line: <c>{ get =&gt; 1; }</c>, <c>{ get { return 1; } }</c>, <c>{ get; set { } }</c>,
    ///             <c>{ get =&gt; 1; set; }</c>, an indexer's and an event's <c>add</c>/<c>remove</c>
    ///             alike. These are ReSharper's <c>place_abstract_accessorholder_on_single_line</c>
    ///             (<c>true</c>) and <c>place_simple_accessorholder_on_single_line</c> (<c>false</c>),
    ///             which the export does not set; flipped, each moves exactly its own half. Neither is a
    ///             registered option, so both are read at those values.
    ///         </item>
    ///         <item>
    ///             Under the key at <c>true</c> neither matters: a list written on one line stays on it, and
    ///             one written over lines gets one accessor per line, <c>{</c>↵<c>get; set;</c>↵<c>}</c>
    ///             included.
    ///         </item>
    ///         <item>
    ///             Either way the margin breaks a joined list one accessor per line —
    ///             <c>… VeryLongName {</c>↵<c>get;</c>↵<c>private set;</c>↵<c>}</c> rather than a break
    ///             inside the type — and an initializer is not part of the measure: the list stays joined
    ///             and the <c>=</c> wraps. An accessor attribute on a line of its own (the export's
    ///             <c>place_accessor_attribute_on_same_line</c>) breaks the list; at <c>always</c>
    ///             <c>{ [A] get; set; }</c> is joined.
    ///         </item>
    ///     </list>
    ///     ⚠ A comment is not a reason to leave a list alone, measured on eight placements after a first
    ///     guess said it was: a block comment that ends a line joins with it — <c>get; /* c */</c>↵<c>set;</c>
    ///     comes back <c>{ get; /* c */ set; }</c>, as do <c>{ /* c */</c>↵… and <c>set; /* c */</c>↵<c>}</c> —
    ///     while a line comment, or a comment on a line of its own, keeps the list expanded. That is the
    ///     builder's ordinary treatment of a comment in a gap (a block comment survives a point, #409; a
    ///     line comment or an own-line comment leaves the gap unplanned and the group unbounded), so the
    ///     group needs no exception for it.
    /// </remarks>
    void PlanAccessorList(AccessorListSyntax node) {
        var bodiless = node.Accessors.All(static a => a.Body is null && a.ExpressionBody is null);
        var broken = BreaksBefore(node.CloseBraceToken);
        foreach (var accessor in node.Accessors) {
            broken |= BreaksBefore(FirstToken(accessor));
        }

        // ⚠ And `csharp_preserve_single_line_blocks = false` expands a bodiless list too: `int R { get;
        // set; }` comes back one accessor per line (#510). Not under the keep key, which outranks it.
        var joins = options.KeepExistingDeclarationBlockArrangement
            ? !broken
            : bodiless && options.PreserveSingleLineBlocks;
        if (!joins) {
            foreach (var accessor in node.Accessors) {
                Mandatory(FirstToken(accessor));
            }

            Mandatory(node.CloseBraceToken);
            return;
        }

        var group = NewGroup();
        foreach (var accessor in node.Accessors) {
            Point(FirstToken(accessor), group);
        }

        Point(node.CloseBraceToken, group);
        Describe(node, group, GroupMode.Preserve, new(BreaksIfTooLong: true));
    }

    /// <summary>
    ///     Whether the author's arrangement of this block wins over the one-per-line rule.
    /// </summary>
    /// <remarks>
    ///     ⚠ Two keys, and which applies is what the block is the body of.
    ///     <c>skala_keep_existing_declaration_block_arrangement</c> governs a method's or a local function's;
    ///     <c>skala_keep_existing_embedded_block_arrangement</c> governs an <c>if</c>'s or a <c>while</c>'s.
    ///     Both are <c>false</c> in the export, which is why the rule looks unconditional there; set
    ///     either to <c>true</c> and the oracle keeps <c>void M() { Body(); }</c> and
    ///     <c>if (flag) { First(); }</c> exactly as written. The four-way preservation table is what
    ///     found this — the two <c>keep_existing_* = true</c> corners were the only ones that moved.
    ///     <para>
    ///         ⚠ A braced switch section's block, <c>case 1: { M(); }</c>, is the embedded key's too, and a
    ///         <see cref="SwitchSectionSyntax" /> is not a statement, so the parent test alone sent it to
    ///         the declaration key. Measured for #374: the embedded key at <c>true</c> keeps it whole, the
    ///         declaration key at <c>true</c> expands it.
    ///     </para>
    /// </remarks>
    /// <remarks>
    ///     ⚠ Only a section whose block is its only statement (#527): `case 3: { M(); } break;` and
    ///     `case 7: { M(); }` / `break;` are expanded by the oracle at the embedded key's <c>true</c> too,
    ///     where `case 1: { M(); }` alone is kept.
    /// </remarks>
    bool Keeps(BlockSyntax block) =>
        block.Parent switch {
            SwitchSectionSyntax { Statements.Count: > 1 } => false,
            (StatementSyntax and not LocalFunctionStatementSyntax)
                or SwitchSectionSyntax
                or AnonymousFunctionExpressionSyntax => options.KeepExistingEmbeddedBlockArrangement,
            _ => options.KeepExistingDeclarationBlockArrangement
        };

    /// <summary>
    ///     Whether a block may stay on its owner's line — and then it does exactly when everything in it
    ///     ends up on that line.
    /// </summary>
    /// <remarks>
    ///     ⚠ "Ends up on that line" is the fitter's containment fact and nothing more: a statement that
    ///     wraps by a kept break, an always-chopped switch, a lambda block of its own or the margin gives
    ///     the block an unbounded flat width, and the group breaks. A look-ahead that wrote the block flat
    ///     on a checkpoint and broke it if that spanned lines was built, and dropped because no probe could
    ///     redden it: a kept <c>=</c>, a kept chain dot, <c>=</c>↵<c>[1, 2]</c>, a verbatim string's
    ///     newline, a type-argument fill, a broken ternary, a broken query, a nested lambda arrow and a
    ///     property pattern all answered identically without it (SK-DIV-0162). A non-idempotency here — a
    ///     block kept flat on pass one whose statement wrapped for a reason the document does not count as
    ///     certain — is where to put it back.
    ///     ⚠ One rule for every block, measured on accessors, lambdas, anonymous methods, methods,
    ///     local functions, <c>if</c> and <c>while</c> (issue #405, SK-DIV-0162). A block with more than
    ///     one statement never does, at every key: <c>set { _n = value; _n++; }</c> comes back four
    ///     lines under the export and under both <c>keep_existing_*_block_arrangement = true</c>, as
    ///     <c>void M() { A(); B(); }</c> and <c>if (c) { A(); B(); }</c> do. A one-statement block does
    ///     when its owner allows it:
    ///     <list type="bullet">
    ///         <item>
    ///             an accessor's, a lambda's and an anonymous method's always — and the author's break
    ///             is not a reason to keep it broken: <c>get {</c>↵<c>return _n;</c>↵<c>}</c> and
    ///             <c>() => {</c>↵<c>_n = 1;</c>↵<c>}</c> are joined; the one key that keeps them is the
    ///             one <see cref="Keeps" /> names — the declaration key for the accessor, ⚠ the
    ///             <em>embedded</em> key for the lambda — and under it a block broken at either of its
    ///             own gaps is broken at both;
    ///         </item>
    ///         <item>
    ///             a method's, a local function's or an <c>if</c>'s only under its key, and only as
    ///             written on one line.
    ///         </item>
    ///     </list>
    ///     <para>
    ///         ⚠ A local function's block is the <em>declaration</em> key's although a local function is a
    ///         statement: <c>void L() { A(); }</c> is kept under the declaration key and expanded under
    ///         the embedded one. Routed by the parent's base type it went to the embedded key and was
    ///         expanded where the oracle keeps it.
    ///     </para>
    /// </remarks>
    /// <summary>
    ///     The head marker of the declaration or function a one-line block belongs to, or −1 for a
    ///     statement's block, which has none.
    /// </summary>
    /// <remarks>
    ///     ⚠ A block stays on its owner's line only while the owner is on one line: measured for #405,
    ///     <c>delegate(</c>↵<c>int first) { return first; }</c>, <c>(</c>↵<c>int first) =&gt; { … }</c>,
    ///     <c>(int first)</c>↵<c>=&gt; { … }</c>, and under the declaration key <c>void N(</c>↵<c>int x)
    ///     { M(); }</c> and the same local function all come back with the block broken open, while
    ///     <c>[Obsolete]</c>↵<c>get { return _n; }</c> keeps it — the attribute's line is not the head's,
    ///     as for an expression body (#372). ⚠ Not an <c>if</c>: <c>if (a</c>↵<c>&amp;&amp; b) { M(); }</c>
    ///     stays whole under the embedded key, as an embedded statement stays on a broken header's last
    ///     line (SK-DIV-0106). Read off the writer's lines, like
    ///     <see cref="GroupFacts.BreaksIfOwnerIsMultiLine" /> everywhere else: a parameter list the fitter
    ///     chops is not in the source on pass one.
    ///     <para>
    ///         This is SK-DIV-0077's block half: Skala used to write <c>) { return first; }</c> on the line
    ///         that closes a parameter list broken across three.
    ///     </para>
    /// </remarks>
    int HeadMarkerOf(BlockSyntax block) {
        if (block.Parent is null or (StatementSyntax and not LocalFunctionStatementSyntax) or SwitchSectionSyntax) {
            return -1;
        }

        var head = FirstTokenAfterAttributes(block.Parent);
        if (head.IsKind(SyntaxKind.None) || head == block.OpenBraceToken) {
            return -1;
        }

        if (!markers.TryGetValue(head.SpanStart, out var marker)) {
            marker = NewGroup();
            markers[head.SpanStart] = marker;
        }

        return marker;
    }

    /// <summary>
    ///     The gap after a one-line block comment that follows an argument list's <c>(</c> or an
    ///     expression body's <c>=&gt;</c>: a point of its own, broken only when the line up to the item's
    ///     first break point has no room (#486, SK-DIV-0165).
    /// </summary>
    /// <remarks>
    ///     ⚠ The oracle's wrap after either token stops at the comment in the common case and does not in
    ///     the other, and the two are one rule. Measured with <c>Testing ask</c>: <c>Compute( /* f */ b,</c> /
    ///     <c>"…"</c> keeps a first item that fits, <c>Compute( /* f */ Inner(</c> and
    ///     <c>=&gt; /* f */ Compute(</c> keep a head that fits and wrap inside it, <c>=&gt; /* f */ "…"</c> /
    ///     <c>+ "…"</c> breaks the binary instead; while <c>Compute( /* f */</c> / <c>"…"</c> / <c>);</c>
    ///     and <c>=&gt; /* f */</c> / <c>"…";</c> move a first item whose head runs past the margin — a
    ///     string of 85 columns as much as one of 120, a first item of two as much as a lone one. That is
    ///     <see cref="GroupFacts.BreaksOnlyIfHeadOverflows" />, the named argument's colon's rule. Before
    ///     this the gap was planned by nothing, so Skala ran the line past the margin or broke the
    ///     <c>=</c> in front of the call instead.
    ///     <para>
    ///         Only where the author wrote the comment and the item on one line, and only for a comment
    ///         that is one line itself; a break the author wrote after the comment is the builder's to
    ///         keep, and a comment spanning lines is #435's.
    ///     </para>
    /// </remarks>
    void PlanPastLeadingComments(SyntaxNode root) {
        foreach (var token in root.DescendantTokens(static node => node is not StructuredTriviaSyntax)) {
            if (!token.TrailingTrivia.Any(static trivia => trivia.IsKind(SyntaxKind.MultiLineCommentTrivia))) {
                continue;
            }

            SyntaxNode owner;
            bool spendsIndent;
            if (token.IsKind(SyntaxKind.OpenParenToken)
                && token.Parent is ArgumentListSyntax { Arguments.Count: > 0 } list) {
                owner = list;
                spendsIndent = false;
            } else if (token.IsKind(SyntaxKind.EqualsGreaterThanToken)
                       && token.Parent is ArrowExpressionClauseSyntax arrow) {
                owner = arrow;
                spendsIndent = true;
            } else {
                continue;
            }

            var next = token.GetNextToken();
            if (next.IsKind(SyntaxKind.None)
                || !gaps.TryGetValue(next.SpanStart, out var spec)
                || spec.Rule != GapRule.Point
                || !OnlyOneLineBlockComments(token.TrailingTrivia)
                || !OnlyOneLineBlockComments(next.LeadingTrivia)) {
                continue;
            }

            var group = NewGroup();
            gaps[next.SpanStart] = new(GapRule.Point, group);
            pastLeadingComments.Add(next.SpanStart);
            var planned = new GroupPlan(
                group,
                GroupMode.Preserve,
                new(BreaksIfTooLong: true, BreaksOnlyIfHeadOverflows: true),
                spendsIndent,
                true
            );

            // An argument list's children are walked by the builder's delimited visitor, which opens
            // the groups described on a node and not those opened at a position: the group is the
            // first argument's.
            if (owner is ArgumentListSyntax arguments) {
                Describe(arguments.Arguments[0], planned);
            } else {
                OpenAt(owner, next.SpanStart, planned);
            }
        }

        static bool OnlyOneLineBlockComments(SyntaxTriviaList trivia) {
            foreach (var piece in trivia) {
                switch (piece.Kind()) {
                    case SyntaxKind.WhitespaceTrivia:
                        continue;
                    case SyntaxKind.MultiLineCommentTrivia when piece.ToString().AsSpan().IndexOfAny('\n', '\r') < 0:
                        continue;
                    default:
                        return false;
                }
            }

            return true;
        }
    }

    /// <summary>
    ///     A member access with no call, element access or other construct in it — <c>receiver.Property</c>,
    ///     <c>a.b.c.D</c> — that is a <c>return</c>'s expression, a local's or an assignment's value: the
    ///     values the dot break was measured on (#446, SK-DIV-0210/0124). The break itself is #482's
    ///     member-access fill (`PlanPropertyFill`), which takes the last dot that fits; only the `=`'s
    ///     yielding to it is read from here.
    /// </summary>
    static bool IsPlainMemberValue(MemberAccessExpressionSyntax access) {
        if (!access.IsKind(SyntaxKind.SimpleMemberAccessExpression)) {
            return false;
        }

        var placed = access.Parent switch {
            ReturnStatementSyntax => true,
            EqualsValueClauseSyntax {
                Parent:
                VariableDeclaratorSyntax {
                    Parent: VariableDeclarationSyntax { Parent: LocalDeclarationStatementSyntax }
                }
            } => true,
            AssignmentExpressionSyntax { Parent: ExpressionStatementSyntax } assignment => assignment.Right == access
                && assignment.IsKind(SyntaxKind.SimpleAssignmentExpression),
            _ => false
        };

        if (!placed) {
            return false;
        }

        for (ExpressionSyntax current = access;;) {
            switch (current) {
                case MemberAccessExpressionSyntax { Name: IdentifierNameSyntax } member
                    when member.IsKind(SyntaxKind.SimpleMemberAccessExpression):
                    current = member.Expression;
                    continue;
                case IdentifierNameSyntax or ThisExpressionSyntax or BaseExpressionSyntax:
                    return !access.DescendantTrivia().Any(static trivia => !trivia.IsKind(SyntaxKind.WhitespaceTrivia));
                default:
                    return false;
            }
        }
    }

    /// <summary>
    ///     The gap after an <c>is</c> before a binary pattern: broken exactly when the line up to the
    ///     pattern's first combinator has no room (#446, SK-DIV-0211).
    /// </summary>
    /// <remarks>
    ///     ⚠ Measured after an <c>=</c> that broke: <c>operand is &gt; 5</c> / <c>and &lt; 10;</c> while the
    ///     first operand fits beside <c>is</c>, and <c>operand is</c> / <c>&gt; 5 and &lt; 10;</c> one level in
    ///     once it does not — the arm arrow's head rule (<see cref="GroupFacts.BreaksOnlyIfHeadOverflows" />).
    ///     A break the author wrote there is kept.
    /// </remarks>
    void PlanAfterIs(IsPatternExpressionSyntax test) {
        var first = FirstToken(test.Pattern);

        // An `is` the author broke before has spent the level, and the chain then continues on its column
        // (#520): the gap after it is not this point's.
        if (first.IsKind(SyntaxKind.None) || gaps.ContainsKey(first.SpanStart) || BreaksBefore(test.IsKeyword)) {
            return;
        }

        var group = NewGroup();
        Point(first, group);
        Describe(
            test.Pattern,
            new(
                group,
                GroupMode.Preserve,
                new(
                    options.KeepsUserBreaksBetweenItems && BreaksBefore(first),
                    BreaksIfTooLong: true,
                    BreaksOnlyIfHeadOverflows: true,
                    HeadSlack: HeadSlackAfterIs(test.Pattern)
                ),
                LeadingGapInside: true,
                FromLine: !IsAHeaderCondition(test)
            )
        );
    }

    /// <summary>
    ///     ⚠ Measured with <c>Testing ask</c> one column at a time on <c>operand is X or Bbb</c> after a broken
    ///     <c>=</c>: a first operand of one or two columns always moves below the <c>is</c>, one of three
    ///     ahead of <c>or</c> moves two columns early (the line through it at 119 and 120), and anything
    ///     wider — or <c>&gt; 5</c> ahead of <c>and</c> — stays while the line through it fits.
    /// </summary>
    static int HeadSlackAfterIs(PatternSyntax pattern) {
        var first = pattern;
        while (first is BinaryPatternSyntax binary) {
            first = binary.Left;
        }

        var width = first.Span.Length;
        return width <= 2 ? 1000
            : width == 3 && pattern is BinaryPatternSyntax { OperatorToken.RawKind: (int)SyntaxKind.OrKeyword } ? 2
            : 0;
    }

    /// <summary>Rewrites the facts of every group described on <paramref name="node" />.</summary>
    void ReviseFacts(SyntaxNode node, Func<GroupFacts, GroupFacts> revise) {
        if (!groups.TryGetValue(Key(node), out var plans)) {
            return;
        }

        for (var i = 0; i < plans.Count; i++) {
            plans[i] = plans[i] with { Facts = revise(plans[i].Facts) };
            byId[plans[i].Id] = plans[i];
        }
    }

    /// <summary>The head marker at <paramref name="token" />, shared with any other group that reads it.</summary>
    int MarkerAt(SyntaxToken token) {
        if (!markers.TryGetValue(token.SpanStart, out var marker)) {
            marker = NewGroup();
            markers[token.SpanStart] = marker;
        }

        return marker;
    }

    bool MayShareItsOwnersLine(BlockSyntax block) {
        if (block.Statements.Count != 1) {
            return false;
        }

        var broken = BreaksBefore(FirstToken(block.Statements[0])) || BreaksBefore(block.CloseBraceToken);

        // ⚠ `csharp_preserve_single_line_blocks = false` takes the "always" away from an accessor's, a
        // lambda's and an anonymous method's block — and only while the keep key that owns the block is
        // off. Measured 2026-10-08 (#510): at `false` under the export `get { return _n; }`,
        // `() => { A(); }`, `delegate { A(); }` and `x => { return x; }` all come back broken open,
        // in a statement and as a sole argument alike; with both `keep_existing_*_block_arrangement`
        // keys `true` the oracle's output at `false` is byte-identical to its output at `true`. A
        // method's, a local function's and an `if`'s block are only ever kept under their keep key,
        // which outranks this one, so they are unaffected either way.
        return block.Parent is AccessorDeclarationSyntax or AnonymousFunctionExpressionSyntax
            ? Keeps(block) ? !broken : options.PreserveSingleLineBlocks
            : !broken && Keeps(block);
    }

    void OnOwnLines<T>(SyntaxList<T> nodes) where T : SyntaxNode {
        foreach (var node in nodes) {
            Mandatory(FirstToken(node));
        }
    }

    void MembersOnOwnLines(SyntaxList<MemberDeclarationSyntax> members, SyntaxToken close) {
        foreach (var member in members) {
            Mandatory(FirstToken(member));
        }

        Mandatory(close);
    }

    /// <summary>
    ///     <c>place_*_attribute_on_same_line</c>: whether an attribute section shares a line with what
    ///     follows it — <c>never</c> forces the break, <c>always</c> removes it, and
    ///     <c>if_owner_is_single_line</c> removes it exactly when the declaration occupies one line.
    /// </summary>
    /// <remarks>
    ///     ⚠ All three values, and until the key-flip sweep only <c>never</c> was planned: the other two
    ///     left the author's break alone, so the oracle joined an attribute onto its owner and Skala did
    ///     not. That is what made four rows — <c>place_attribute_on_same_line</c> and the
    ///     <c>method</c>, <c>accessor</c> and <c>accessorholder</c> keys — one divergence rather than
    ///     four, and the joining half is measured rather than reasoned about:
    ///     <code>
    /// [First]
    /// void SingleLine() { }
    /// [First]
    /// void MultiLine() { int x = 1; Use(x); }
    ///     </code>
    ///     comes back from the oracle with <em>both</em> attributes joined at <c>always</c> and only
    ///     <c>SingleLine</c>'s joined at <c>if_owner_is_single_line</c>. "Owner is single line" is
    ///     therefore a property of the declaration's whole formatted width, exactly as it is for
    ///     <see cref="PlanExpressionBody" />, and not of the attribute's.
    ///     <para>
    ///         ⚠ <c>skala_keep_existing_attribute_arrangement = true</c> outranks the key in the joining
    ///         direction only. It says a break the author wrote is not removed; it does not say a break
    ///         may not be added, which is the same reading <see cref="PlanEmbeddedStatement" /> records
    ///         for the embedded-statement pair.
    ///     </para>
    /// </remarks>
    void PlanAttributes(SyntaxNode node) {
        var lists = node switch {
            MemberDeclarationSyntax member => member.AttributeLists,
            LocalFunctionStatementSyntax local => local.AttributeLists,
            StatementSyntax statement => statement.AttributeLists,
            AccessorDeclarationSyntax accessor => accessor.AttributeLists,
            ParameterSyntax { Parent.Parent: TypeDeclarationSyntax } parameter => parameter.AttributeLists,
            _ => default
        };

        if (lists.Count == 0) {
            return;
        }

        PlanCommentedAttributeGap(node, lists);

        // ⚠ A local function's attribute sections are each on a line of their own whatever any key says
        // (#444, SK-DIV-0207). Measured with the method key at `always` and `if_owner_is_single_line`,
        // `skala_place_attribute_on_same_line = true`, both together and
        // `skala_keep_existing_attribute_arrangement = true`: `[Obsolete] void Local() { }` comes back
        // `[Obsolete]` / `void Local() { }` every time, and a method beside it moves with the keys.
        if (node is LocalFunctionStatementSyntax) {
            foreach (var token in AttributeGaps(node, lists)) {
                Mandatory(token);
            }

            return;
        }

        var placement = AttributePlacement(node);
        if (placement == PlacementStyle.Never) {
            // skala_keep_existing_attribute_arrangement = true leaves whatever the author wrote.
            if (options.KeepExistingAttributeArrangement) {
                return;
            }

            foreach (var token in AttributeGaps(node, lists)) {
                if (!pastAttributeComments.Contains(token.SpanStart)) {
                    Mandatory(token);
                }
            }

            return;
        }

        // The joining half. `skala_keep_existing_attribute_arrangement = true` is the author's break
        // surviving, so there is nothing to plan.
        if (options.KeepExistingAttributeArrangement || !AttributeRunFitsTheCap(lists)) {
            return;
        }

        if (placement == PlacementStyle.Always) {
            foreach (var token in AttributeGaps(node, lists)) {
                Flat(token);
            }

            // ⚠ The join is declined when the joined line overflows by its terminator alone (#438). See
            // GroupFacts.Terminator and TerminatorOf.
            var terminator = TerminatorOf(node);
            var last = lists[^1].CloseBracketToken.GetNextToken();
            if (terminator > 0
                && !last.IsKind(SyntaxKind.None)
                && last.SpanStart <= node.Span.End
                && !BreaksInsideTheSignature(node, last)) {
                var joining = NewGroup();
                Point(last, joining);
                Describe(
                    node,
                    joining,
                    GroupMode.Preserve,
                    new(MeasuresHead: true, Terminator: terminator)
                );
            }

            return;
        }

        var group = NewGroup();
        var broken = false;
        foreach (var token in AttributeGaps(node, lists)) {
            Point(token, group);
            broken |= BreaksBefore(token);
        }

        Describe(
            node,
            group,
            GroupMode.Preserve,
            // ⚠ `JoinsIfFits` and `BreaksIfTooLong` both, which is what makes this
            // `if_owner_is_single_line` rather than `keep`: the author's break goes when the owner
            // fits on one line and comes back when it does not.
            new(broken, true, true)
        );
    }

    /// <summary>
    ///     A field's gap after its last attribute section and a block comment: declined — the comment ends the
    ///     line — when the joined line overflows, unless what overflows can wrap inside the value.
    /// </summary>
    /// <remarks>
    ///     ⚠ Measured with <c>jb cleanupcode</c> 2025.2.6 one column at a time from 120 to 134 (#504, SK-DIV-0201):
    ///     <c>[Obsolete] /* c */ public int F = …;</c> with a binary chain of identifiers, of literals, a single
    ///     identifier, a string, a conditional, a member chain to 128 and <c>private static readonly</c> in front
    ///     comes back with the attribute and the comment on their line and the declaration whole below at
    ///     every overflowing width; an event field the same. A value that is a call or a creation with
    ///     arguments is declined only while its <c>;</c> alone overflows — <c>)</c> at 120 — and from there is
    ///     joined with its arguments chopped, the terminator rule of the joining half (#438). Skala left the
    ///     gap to the author, joined, and broke the <c>=</c>. ⚠ Not a property's or a method's arrow, declined
    ///     at one to three columns past and joined from there, nor an auto-property's initializer, always
    ///     joined: no reading of the overflow alone covers them, so they stay the author's.
    /// </remarks>
    void PlanCommentedAttributeGap(SyntaxNode node, SyntaxList<AttributeListSyntax> lists) {
        if (node is not (FieldDeclarationSyntax or EventFieldDeclarationSyntax)) {
            return;
        }

        var close = lists[^1].CloseBracketToken;
        var next = close.GetNextToken();
        if (next.IsKind(SyntaxKind.None)
            || BreaksBefore(next)
            || !close.TrailingTrivia.Concat(next.LeadingTrivia)
                .Any(static trivia => trivia.IsKind(SyntaxKind.MultiLineCommentTrivia))
            || BreaksInsideTheSignature(node, next)) {
            return;
        }

        var value = node is FieldDeclarationSyntax { Declaration.Variables: [{ Initializer.Value: { } initial }] }
            ? initial
            : null;
        var wrapsInside = value is InvocationExpressionSyntax { ArgumentList.Arguments.Count: > 0 }
            or BaseObjectCreationExpressionSyntax { ArgumentList.Arguments.Count: > 0 };

        var group = NewGroup();
        Point(next, group);
        pastAttributeComments.Add(next.SpanStart);
        Describe(node, group, GroupMode.Preserve, new(MeasuresHead: true, Terminator: wrapsInside ? 1 : WholeLine));
    }

    /// <summary>
    ///     The width of what ends a declaration's first line after its signature, for the owners
    ///     <c>always</c>'s joining half was measured on; zero for every other owner.
    /// </summary>
    /// <remarks>
    ///     ⚠ Measured with <c>jb cleanupcode</c> 2025.2.6 one column at a time at
    ///     <c>place_*_attribute_on_same_line = always</c> (#438, SK-DIV-0201). A method with an empty body
    ///     is joined at 120 columns, declined at 121–124 with its <c>)</c> at 117–120, and joined with its
    ///     parameters chopped once the <c>)</c> is at 121; a block body's <c> {</c> and a field's <c>;</c>
    ///     are the same. ⚠ An expression body is not a terminator: the arrow breaks instead
    ///     (<c>[Obsolete] public int A(…) =&gt;</c> / the body), and neither is a property's accessor
    ///     list, which expands. ⚠ Nor a local function, whose attribute the oracle never joins at all at
    ///     <c>always</c> (a separate divergence). Every other owner keeps the plain join.
    /// </remarks>
    static int TerminatorOf(SyntaxNode node) =>
        node switch {
            BaseMethodDeclarationSyntax { ExpressionBody: not null } => 0,
            BaseMethodDeclarationSyntax { Body: { } body } => body.Statements.Count == 0 ? 4 : 2,
            BaseMethodDeclarationSyntax => 1,
            FieldDeclarationSyntax => 1,

            // ⚠ An event field has nothing the oracle wraps inside it, so any overflow declines the join:
            // `[Obsolete] public event Action<int, …> E;` at 121, 122 and 123 columns alike.
            EventFieldDeclarationSyntax => WholeLine,
            _ => 0
        };

    /// <summary>
    ///     Whether the author broke the line anywhere between the declaration's first token and its body
    ///     or terminator — after an <c>=</c>, inside the parameters.
    /// </summary>
    /// <remarks>
    ///     ⚠ The idempotence of <see cref="TerminatorOf" />'s group. A joined line whose <c>=</c> or parameter
    ///     list then wrapped comes back with that break in the source, and the kept break ends the joined
    ///     line there; measured through it instead — the head measure does not stop at a kept point —
    ///     the line read one column short of the first pass's and the join was declined on pass two.
    /// </remarks>
    bool BreaksInsideTheSignature(SyntaxNode node, SyntaxToken first) {
        var end = node switch {
            BaseMethodDeclarationSyntax { Body: { } body } => body.SpanStart,
            BaseMethodDeclarationSyntax { ExpressionBody: { } arrow } => arrow.SpanStart,
            _ => node.Span.End
        };

        foreach (var token in node.DescendantTokens()) {
            if (token.SpanStart > first.SpanStart && token.SpanStart < end && BreaksBefore(token)) {
                return true;
            }
        }

        return false;
    }

    /// <summary>A terminator as wide as any line: the join is declined whenever the line overflows.</summary>
    const int WholeLine = 1 << 20;

    /// <summary>
    ///     <c>skala_max_attribute_length_for_same_line</c>: an attribute run wider than the cap does not join
    ///     its owner's line however the placement key is set.
    /// </summary>
    /// <remarks>
    ///     ⚠ The key was registered <see cref="PhaseOneOptions.Ids.MaxAttributeLengthForSameLine" />
    ///     inert on the grounds that it is "a length threshold for a placement that never happens", and
    ///     that reason expired the moment <see cref="PlanAttributes" /> grew its joining half. The
    ///     reading below is measured, at the cap and either side of it:
    ///     <code>
    /// // skala_max_attribute_length_for_same_line = 6, skala_place_method_attribute_on_same_line = always
    /// [Aaa] void Four() { }        // 5 — joined
    /// [Aaaa] void Five() { }       // 6 — joined, so the comparison is inclusive
    /// [Aaaaa]                      // 7 — not joined
    /// void Six() { }
    /// [Aa] [Bb]                    // 9 across both sections — not joined at 6, joined at 9,
    /// void TwoSections() { }       //     so the measure is the whole run and not one section
    ///     </code>
    ///     ⚠ Measured off the source spans plus one separator each, which is the run's width in every
    ///     input this repository holds and is *not* the same claim as the formatted width: an author who
    ///     writes <c>[ First ]</c> is measured two columns wider than the oracle would measure the
    ///     section it is about to emit. The exact-width answer needs the formatted attribute text, which
    ///     does not exist when the break plan is built.
    /// </remarks>
    bool AttributeRunFitsTheCap(SyntaxList<AttributeListSyntax> lists) {
        var width = 0;
        foreach (var list in lists) {
            width += list.Span.Length;
        }

        if (options.SpaceBetweenAttributeSections) {
            width += lists.Count - 1;
        }

        return width <= options.MaxAttributeLengthForSameLine;
    }

    /// <summary>
    ///     The gap after each of a declaration's attribute sections — the one place the placement key
    ///     decides.
    /// </summary>
    static IEnumerable<SyntaxToken> AttributeGaps(SyntaxNode node, SyntaxList<AttributeListSyntax> lists) {
        foreach (var list in lists) {
            var next = list.CloseBracketToken.GetNextToken();
            if (!next.IsKind(SyntaxKind.None) && next.SpanStart <= node.Span.End) {
                yield return next;
            }
        }
    }

    PlacementStyle AttributePlacement(SyntaxNode node) =>
        node switch {
            BaseTypeDeclarationSyntax or DelegateDeclarationSyntax => options.PlaceTypeAttributeOnSameLine,
            MethodDeclarationSyntax
                or ConstructorDeclarationSyntax
                or DestructorDeclarationSyntax
                or OperatorDeclarationSyntax
                or ConversionOperatorDeclarationSyntax =>
                options.PlaceMethodAttributeOnSameLine,
            // ⚠ `event Action E;` is an accessor holder too, and it used to be read as a field here.
            // Measured, and it is the same finding `resharper_place_event_attribute_on_same_line`'s
            // `inert` note records from the other side: with the field key at `always` the event's
            // attribute stays on its own line, and with the accessor-holder key at `always` it joins.
            // The two keys are `never` together in the export, which is what hid it.
            PropertyDeclarationSyntax
                or IndexerDeclarationSyntax
                or EventDeclarationSyntax
                or EventFieldDeclarationSyntax =>
                options.PlaceAccessorHolderAttributeOnSameLine,
            AccessorDeclarationSyntax => options.PlaceAccessorAttributeOnSameLine,
            // A record's positional parameter is a field, not a parameter, and has its own key. An
            // ordinary parameter's attribute always stays on the parameter's line.
            ParameterSyntax => options.PlaceRecordFieldAttributeOnSameLine,
            FieldDeclarationSyntax => options.PlaceFieldAttributeOnSameLine,
            _ => options.PlaceAttributeOnSameLine
        };

    // ── Option lookups per construct family ──────────────────────────────────────────────────

    bool InvocationKeeps(ArgumentListSyntax arguments) =>
        arguments.Parent switch {
            PrimaryConstructorBaseTypeSyntax => options.KeepExistingPrimaryConstructorParensArrangement,
            _ => options.KeepExistingInvocationParensArrangement
        };

    /// <summary>
    ///     ⚠ One key for every parameter list, a lambda's and an anonymous method's included.
    /// </summary>
    /// <remarks>
    ///     ⚠ This used to route a lambda's and an anonymous method's parameter list to
    ///     <c>skala_keep_existing_lambda_and_anonymous_function_parens_arrangement</c>, and the C#
    ///     formatter does not answer to that key at all. Measured 2026-08-30 against
    ///     <c>jb cleanupcode</c> 2025.2.6 on <c>constructs/preservation/lambda-parens.cs</c>, with the
    ///     author's break inside the lambda's parentheses:
    ///     <code>
    /// keep_existing_lambda_… = false                    unchanged — the break is kept
    /// keep_existing_lambda_… = true                     unchanged
    /// skala_keep_existing_declaration_parens_arrangement = false   REJOINED
    /// both = false                                      rejoined, and no further
    /// declaration = false, lambda = true                rejoined
    ///     </code>
    ///     So the declaration key decides and the lambda key is inert in both directions and in either
    ///     spelling — the same shape as <c>remove_this_qualifier</c>: a documented editorconfig property
    ///     the C# formatter is not wired to. Skala answered to it, which is why the sweep read
    ///     <c>SPURIOUS</c>. SK-DIV-0093.
    /// </remarks>
    bool DeclarationKeeps() => options.KeepExistingDeclarationParensArrangement;

    static StatementSyntax? EmbeddedStatementOf(SyntaxNode node) =>
        node switch {
            IfStatementSyntax statement => statement.Statement,
            ElseClauseSyntax clause => clause.Statement is IfStatementSyntax ? null : clause.Statement,
            WhileStatementSyntax statement => statement.Statement,
            DoStatementSyntax statement => statement.Statement,
            ForStatementSyntax statement => statement.Statement,
            ForEachStatementSyntax statement => statement.Statement,
            ForEachVariableStatementSyntax statement => statement.Statement,
            UsingStatementSyntax { Statement: not UsingStatementSyntax } statement => statement.Statement,
            FixedStatementSyntax statement => statement.Statement,
            LockStatementSyntax statement => statement.Statement,
            _ => null
        };

    /// <summary>
    ///     The single lambda argument <c>place_single_method_argument_lambda_on_same_line</c> keeps on
    ///     the call's line.
    /// </summary>
    /// <remarks>
    ///     ⚠ Not a named one. Measured for issue #378 on the second Nightly seed's label and on a local:
    ///     <c>Bind&lt;…&gt;(name48: x49 =&gt; …)</c> comes back from the oracle with <c>(</c> broken and the
    ///     argument on its own line, exactly as <c>(name48: (true ? 1 : 2))</c> does, while the same call
    ///     with <c>(x49 =&gt; …)</c> keeps the lambda on the <c>(</c> line and breaks its arrow. The
    ///     name makes it an ordinary argument.
    ///     <para>
    ///         ⚠ And not an anonymous method, which the key's name says and which was not measured until
    ///         #405 made one-line blocks break (SK-DIV-0163): <c>Register(delegate { A(); B(); })</c> comes
    ///         back <c>Register(</c> / <c>delegate {</c> … <c>}</c> / <c>);</c>, as an ordinary argument does,
    ///         alone or after another argument, with or without <c>()</c>, while <c>Register(() =&gt; {</c>
    ///         keeps its line.
    ///     </para>
    /// </remarks>
    static bool IsLambdaArgument(SyntaxNode item) =>
        item is ArgumentSyntax { NameColon: null, Expression: LambdaExpressionSyntax };

    /// <summary>
    ///     The outermost link of an <c>a.B().C()</c> chain — the node the whole chain's group hangs from.
    /// </summary>
    /// <remarks>
    ///     ⚠ The same predicate <see cref="CSharpDocumentBuilder" /> uses to decide which node spends the
    ///     chain's continuation level, and the same one it uses to decide which node opens
    ///     <c>skala_outdent_dots</c>' column scope; all three must agree, or the group and the indent scopes
    ///     are opened around different nodes.
    /// </remarks>
    internal static bool IsChainRoot(SyntaxNode node) =>
        // ⚠ A member access too: a chain whose last link is a property is a chain (#454), and a
        // pure property chain plans no group because it has no call to count.
        node is InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax or MemberBindingExpressionSyntax }
            or MemberAccessExpressionSyntax
            or ConditionalAccessExpressionSyntax
        // ⚠ Not stopped by a `!`: the operand of one is a chain of its own (#455, ChainLinks).
        && node.Parent is not (InvocationExpressionSyntax
            or MemberAccessExpressionSyntax
            or ElementAccessExpressionSyntax
            or ConditionalAccessExpressionSyntax
            or MemberBindingExpressionSyntax);

    // ── Registration ─────────────────────────────────────────────────────────────────────────

    int NewGroup() => nextGroup++;

    void Describe(
        SyntaxNode node,
        int group,
        GroupMode mode,
        in GroupFacts facts,
        bool spendsIndent = false,
        bool leadingGapInside = false,
        bool ownLevel = false,
        HeldLevel holdsLevel = HeldLevel.None
    ) =>
        Describe(
            node,
            new(
                group,
                mode,
                facts,
                spendsIndent,
                leadingGapInside,
                ownLevel,
                HoldsLevel: holdsLevel
            )
        );

    void Describe(SyntaxNode node, GroupPlan plan) {
        var key = Key(node);
        if (!groups.TryGetValue(key, out var plans)) {
            groups[key] = plans = [];
        }

        plans.Add(plan);
        byId[plan.Id] = plan;
    }

    void DescribeInner(SyntaxNode node, int group, GroupMode mode, in GroupFacts facts) {
        var plan = new GroupPlan(group, mode, facts);
        inner[Key(node)] = plan;
        byId[group] = plan;
    }

    /// <summary>
    ///     <c>csharp_new_line_before_open_brace</c>'s split direction: a brace the key puts on a line of
    ///     its own goes there exactly when the line after it breaks (#465, SK-DIV-0091).
    /// </summary>
    /// <remarks>
    ///     ⚠ The placement family used to be a join decision only — <c>ShouldJoin</c> removed the
    ///     author's break before a brace and nothing ever added one — so a K&amp;R input under
    ///     <c>all</c> came back K&amp;R where the oracle writes Allman. Measured 2026-10-08 with
    ///     <c>Testing ask</c> on every construct the key's seven groups cover, written K&amp;R:
    ///     <list type="bullet">
    ///         <item>
    ///             A body that breaks open puts its brace on a line of its own — a type's, a method's,
    ///             every control block's, an accessor list's and an accessor's, a lambda's and an
    ///             anonymous method's, an initializer's, a switch expression's.
    ///         </item>
    ///         <item>
    ///             A body that stays on its owner's line keeps its brace there: <c>get { return _n; }</c>,
    ///             <c>() =&gt; { A(); }</c>, <c>delegate { A(); }</c>, <c>new List&lt;int&gt; { 1, 2 }</c>,
    ///             <c>new { X = 1, Y = 2 }</c>, <c>int B { get; set; }</c>. So the brace's gap is a point
    ///             of whatever decides the gap after it, and the group is entered before the brace,
    ///             which is the column the owner's-line question is asked from.
    ///         </item>
    ///         <item>
    ///             An empty body follows <c>skala_empty_block_style</c> for a type, a namespace, a method,
    ///             a local function, a control block and a switch: <c>together</c> gives
    ///             <c>void M()</c> / <c>{ }</c>, <c>multiline</c> <c>void M()</c> / <c>{</c> / <c>}</c>, and
    ///             <c>together_same_line</c> <c>void M() { }</c>, which <c>ShouldJoin</c> also pulls back
    ///             from an Allman input. An empty accessor, lambda, anonymous method or initializer stays
    ///             <c>{ }</c> on its owner's line at all three values, and is joined back from Allman.
    ///         </item>
    ///     </list>
    /// </remarks>
    void SettleOpenBraces(SyntaxNode root) {
        if (options.NewLineBeforeOpenBraceOwners == BraceOwners.None) {
            return;
        }

        foreach (var open in root.DescendantTokens()) {
            if (!open.IsKind(SyntaxKind.OpenBraceToken)
                || !CSharpDocumentBuilder.OpensAJoinableBody(open)
                || (options.NewLineBeforeOpenBraceOwners & BraceOwnerSet.Of(open)) == 0
                || captured is { Count: > 0 } && open.Parent is { } parent && IsInsideCaptured(parent)) {
                continue;
            }

            var next = open.GetNextToken();
            if (next.IsKind(SyntaxKind.None)) {
                continue;
            }

            if (CSharpDocumentBuilder.IsEmptyBody(open)) {
                if (!CSharpDocumentBuilder.EmptyBodyStaysJoined(open)
                    && options.EmptyBlockStyle != EmptyBlockStyle.TogetherSameLine) {
                    SplitBefore(open);
                }

                continue;
            }

            if (!gaps.TryGetValue(next.SpanStart, out var after)) {
                if (BreaksBefore(next)) {
                    SplitBefore(open);
                }

                continue;
            }

            switch (after.Rule) {
                case GapRule.Flat:
                    continue;

                case GapRule.Mandatory:
                    SplitBefore(open);
                    continue;

                default:
                    if (byId.TryGetValue(after.Group, out var owner) && owner.Mode == GroupMode.Break) {
                        SplitBefore(open);
                        continue;
                    }

                    PointBeforeBrace(open, after.Group);
                    continue;
            }
        }
    }

    /// <summary>
    ///     A collection expression that is a grouping parenthesis's whole contents joins its <c>[</c> to the
    ///     <c>(</c> once it breaks (#485, SK-DIV-0150).
    /// </summary>
    /// <remarks>
    ///     ⚠ Measured 2026-10-08 with <c>Testing ask</c>: <c>(</c> / <c>[</c> / elements / <c>]);</c> comes
    ///     back <c>( [</c> / elements / <c>]);</c>, as <c>([</c> and <c>( [</c> do, under an <c>=</c> and as an
    ///     argument; <c>(</c> / <c>[1, 2]);</c>, a collection that stays on one line, keeps the author's
    ///     break. So the join answers whether the collection is multi-line, which is read off the finished
    ///     plan — a break inside it that survives (<see cref="SourceBreakSurvives" />). A collection written
    ///     on one line is the width's question, answered by a <see cref="GroupFacts.BreaksOnlyIfTailFits" />
    ///     group below (round 5).
    /// </remarks>
    void SettleParenthesisedCollections(SyntaxNode root) {
        foreach (var paren in root.DescendantNodes().OfType<ParenthesizedExpressionSyntax>()) {
            if (paren.Expression is not CollectionExpressionSyntax { Elements.Count: > 0 } collection
                || captured is { Count: > 0 } && IsInsideCaptured(paren)) {
                continue;
            }

            var open = collection.OpenBracketToken;
            if (options.KeepsUserBreaksBetweenItems
                && collection.DescendantTokens()
                    .Any(token => token.SpanStart > open.SpanStart && SourceBreakSurvives(token))) {
                gaps[open.SpanStart] = new(GapRule.Flat, -1);
                continue;
            }

            // ⚠ Otherwise the break after the `(` is the bracket's alternative (#485, round 5): taken —
            // kept when the author wrote it, added when the line is too long — exactly when the
            // collection then fits flat on the line below, which is `GroupFacts.BreaksOnlyIfTailFits`,
            // the rule of the `=` before a collection. Measured 2026-10-08 with `Testing ask`: `(` /
            // `["…", …]);` whose bracket line is 120 columns keeps the break and at 121 comes back `( [` /
            // elements / `]);`, under `=`, `return` and an argument alike; a flat `(["…", …]);` too wide
            // for its line moves the bracket down when it fits there; and at
            // `skala_keep_user_linebreaks = false` the same, with the author's break no longer a reason
            // (`(` / `[1, 2]);` re-joins, as round 4 measured).
            if (gaps.TryGetValue(open.SpanStart, out var existing) && existing.Rule != GapRule.Flat) {
                continue;
            }

            var group = NewGroup();
            gaps[open.SpanStart] = new(GapRule.Point, group);
            Describe(
                paren,
                group,
                GroupMode.Preserve,
                new(
                    options.KeepsUserBreaksBetweenItems && BreaksBefore(open),
                    BreaksIfTooLong: true,
                    BreaksOnlyIfTailFits: true
                )
            );
        }
    }

    bool IsInsideCaptured(SyntaxNode node) {
        for (var current = node; current is not null; current = current.Parent) {
            if (current is ExpressionSyntax && captured!.Contains(current.Span)) {
                return true;
            }
        }

        return false;
    }

    void SplitBefore(SyntaxToken open) {
        if (gaps.TryGetValue(open.SpanStart, out var existing) && existing.Rule != GapRule.Flat) {
            return;
        }

        Mandatory(open);
    }

    /// <summary>
    ///     The brace's gap as a point of <paramref name="group" />, which is entered before the brace
    ///     when it is described on the node the brace starts.
    /// </summary>
    /// <remarks>
    ///     ⚠ A group opened around a node is entered after the gap before that node
    ///     (<see cref="GroupPlan.LeadingGapInside" />), and a block, an accessor list, an initializer and a
    ///     property pattern all start at their brace — so the point would land outside its group and be
    ///     written flat whatever the group decided.
    /// </remarks>
    void PointBeforeBrace(SyntaxToken open, int group) {
        if (gaps.TryGetValue(open.SpanStart, out var existing) && existing.Rule != GapRule.Flat) {
            return;
        }

        if (open.Parent is { } node
            && node.SpanStart == open.SpanStart
            && groups.TryGetValue(Key(node), out var plans)) {
            var index = plans.FindIndex(plan => plan.Id == group);
            if (index < 0) {
                // The group is not one this node opens: its points enclose the brace already.
                Point(open, group);
                return;
            }

            for (var i = 0; i <= index; i++) {
                plans[i] = plans[i] with { LeadingGapInside = true };
                byId[plans[i].Id] = plans[i];
            }
        }

        Point(open, group);
    }

    /// <summary>
    ///     Whether a break the author wrote before this token is still there once every plan has had
    ///     its say: the gap is nobody's (so <c>keep_user_linebreaks</c> keeps it), or a required break,
    ///     or a point of a group that is certain to break.
    /// </summary>
    /// <remarks>
    ///     ⚠ Only meaningful after the walk — a gap not yet planned reads as kept. A point of a fill is
    ///     re-decided by width and a preserve group that may re-join is not certain, so neither counts;
    ///     that errs towards "the header stays whole", which is the direction the oracle errs in.
    /// </remarks>
    bool SourceBreakSurvives(SyntaxToken token) {
        if (!BreaksBefore(token)) {
            return false;
        }

        if (!gaps.TryGetValue(token.SpanStart, out var spec)) {
            return options.KeepsUserBreaksBetweenItems;
        }

        switch (spec.Rule) {
            case GapRule.Flat:
                return false;

            case GapRule.Mandatory:
                return true;

            default:
                return byId.TryGetValue(spec.Group, out var plan)
                    && (plan.Mode == GroupMode.Break
                        || spec.Rule == GapRule.Point
                        && plan.Facts.SourceBroken
                        && !plan.Facts.JoinsIfFits);
        }
    }

    /// <summary>
    ///     <c>chop_if_long</c>'s "or multi-line" for a <c>for</c> header, answered against the finished
    ///     plan: a header is multi-line when a break inside its parentheses <em>survives</em>, not when
    ///     the source merely holds one.
    /// </summary>
    /// <remarks>
    ///     ⚠ Measured on both sides (SK-DIV-0111). The oracle leaves <c>for (int i = 0\n, j = 1; …)</c>,
    ///     <c>for (int i = 0; i &lt;\n n; …)</c> and <c>for (int i = F(\n1); …)</c> on one line — each
    ///     break is one its own construct re-joins — and chops the header for a break kept after a
    ///     declarator's comma, before a binary operator or after an incrementor's <c>+=</c>. Reading
    ///     the source alone chopped all six.
    /// </remarks>
    void SettleForHeaders() {
        foreach (var node in forHeaders) {
            var key = Key(node);
            if (!inner.TryGetValue(key, out var plan)) {
                continue;
            }

            var survives = false;
            foreach (var token in node.DescendantTokens()) {
                if (token.SpanStart <= node.OpenParenToken.SpanStart) {
                    continue;
                }

                if (token.SpanStart > node.CloseParenToken.SpanStart) {
                    break;
                }

                if (SourceBreakSurvives(token)) {
                    survives = true;
                    break;
                }
            }

            var settled = plan with { Facts = plan.Facts with { SourceBroken = survives } };
            inner[key] = settled;
            byId[plan.Id] = settled;
        }
    }

    /// <param name="lastResort">
    ///     The fill point yields to everything before it, the header's own constructs included:
    ///     <see cref="GapRule.LastResortPoint" />.
    /// </param>
    /// <param name="yields">
    ///     The fill point yields to what precedes its list and to nothing inside it:
    ///     <see cref="GapRule.YieldingFillPoint" />.
    /// </param>
    void Point(SyntaxToken token, int group, bool fill = false, bool lastResort = false, bool yields = false) {
        if (token.IsKind(SyntaxKind.None)) {
            return;
        }

        // A point always wins over a Flat left by a nested construct, and never over another point.
        if (gaps.TryGetValue(token.SpanStart, out var existing) && existing.Rule != GapRule.Flat) {
            return;
        }

        gaps[token.SpanStart] = new(
            lastResort ? GapRule.LastResortPoint
            : yields ? GapRule.YieldingFillPoint
            : fill ? GapRule.FillPoint
            : GapRule.Point,
            group
        );
    }

    /// <summary>
    ///     A point of a group that lies after the group's last token, measured through by everything
    ///     before it. See <see cref="GapRule.FollowingPoint" />.
    /// </summary>
    void FollowingPoint(SyntaxToken token, int group) {
        if (token.IsKind(SyntaxKind.None)) {
            return;
        }

        if (gaps.TryGetValue(token.SpanStart, out var existing) && existing.Rule != GapRule.Flat) {
            return;
        }

        gaps[token.SpanStart] = new(GapRule.FollowingPoint, group);
    }

    /// <summary>A point the source broke stays broken; one it did not stays flat.</summary>
    void Pin(SyntaxToken token, bool broken) {
        if (broken) {
            Mandatory(token);
        } else {
            Flat(token);
        }
    }

    void Flat(SyntaxToken token) {
        if (!token.IsKind(SyntaxKind.None) && !gaps.ContainsKey(token.SpanStart)) {
            gaps[token.SpanStart] = new(GapRule.Flat, -1);
        }
    }

    /// <summary>
    ///     The gap before a subpattern's or a named argument's colon: never a point, but an author's break
    ///     there is kept under <c>keep_user_linebreaks</c>.
    /// </summary>
    /// <remarks>
    ///     ⚠ It was always flat, and the oracle keeps the break (#436). Measured with <c>Testing ask</c>:
    ///     <c>{ X</c> / <c>: 1 }</c> keeps the colon on its own line in a property pattern, an extended
    ///     one, a nested one, a switch arm's and a case label's, and the pattern expands around it;
    ///     <c>M(a</c> / <c>: 1, b: 2)</c> keeps it and chops the arguments; <c>(a</c> / <c>: 1, b: 2)</c>
    ///     and a positional pattern keep it and leave the rest of the list on the line. At
    ///     <c>keep_user_linebreaks = false</c> every one of them is joined.
    /// </remarks>
    /// <returns>
    ///     Whether the break was kept, in which case the gap after the colon is the author's too and
    ///     there is no point to plan: the oracle writes <c>X</c> / <c>: 1</c>, never <c>:</c> / <c>1</c>.
    /// </returns>
    bool KeepsTheBreakBefore(SyntaxToken colon, SyntaxToken value) {
        if (!KeepsTheBreakBefore(colon)) {
            return false;
        }

        Mandatory(colon);
        if (BreaksBefore(value)) {
            Mandatory(value);
        } else {
            Flat(value);
        }

        return true;
    }

    bool KeepsTheBreakBefore(SyntaxToken colon) => options.KeepsUserBreaksBetweenItems && BreaksBefore(colon);

    void Mandatory(SyntaxToken token) {
        if (!token.IsKind(SyntaxKind.None)) {
            gaps[token.SpanStart] = new(GapRule.Mandatory, -1);
        }
    }

    /// <summary>
    ///     Plans the gap on the side of a comma the break lands on: a required break where the
    ///     author's break is pinned, a point of the group otherwise. Returns whether the source broke
    ///     there.
    /// </summary>
    /// <param name="yields">
    ///     The point yields to what precedes the list: <see cref="GapRule.YieldingFillPoint" />.
    /// </param>
    /// <remarks>
    ///     ⚠ The pin reads only the stretch after the last comment in the gap, because that is where the
    ///     point is (CSharpDocumentBuilder.PointSurvivesComments). <c>(alpha,</c> / <c>/* f */ beta)</c>
    ///     is an author's break <em>before</em> the comment, which the gap in front of the comment keeps
    ///     by itself; pinning the point as well put <c>beta</c> on a third line, and the oracle keeps
    ///     <c>/* f */ beta)</c> (#409). Whether the list broke at all is still the whole gap's answer.
    /// </remarks>
    bool PlanItemGap(SyntaxToken gap, int group, bool fill, bool pins, bool yields = false) {
        var broke = BreaksBefore(gap);
        if (pins && BreaksAfterTheLastCommentIn(source, gap) || fill && EndsInAMultiLineComment(gap)) {
            Mandatory(gap);
        } else {
            Point(gap, group, fill, yields: yields);
        }

        return broke;
    }

    /// <summary>
    ///     An empty initializer or collection expression that holds nothing but a block comment spanning
    ///     lines closes on a line of its own (#444, SK-DIV-0209).
    /// </summary>
    /// <remarks>
    ///     ⚠ Measured on an array initializer, a collection and an object initializer and a collection
    ///     expression, the comment written beside the opener and on its own line alike:
    ///     <c>new int[] { /* a</c> / <c>b */</c> / <c>};</c>. A comment on one line, <c>new int[] { /* a */ }</c>,
    ///     stays as it is. An argument list is not this: the oracle moves its comment, which is another
    ///     question.
    /// </remarks>
    void CloseAfterAMultiLineComment(SyntaxToken open, SyntaxToken close) {
        if (open.GetNextToken() != close) {
            return;
        }

        foreach (var trivia in open.TrailingTrivia.Concat(close.LeadingTrivia)) {
            if ((trivia.IsKind(SyntaxKind.MultiLineCommentTrivia)
                    || trivia.IsKind(SyntaxKind.MultiLineDocumentationCommentTrivia))
                && trivia.ToFullString().Contains('\n')) {
                Mandatory(close);
                return;
            }
        }
    }

    /// <summary>
    ///     Whether the item before the comma in front of <paramref name="gap" /> ends in a block comment that
    ///     spans lines: <c>2 /* a</c> / <c>b */, 3</c>.
    /// </summary>
    /// <remarks>
    ///     ⚠ Measured on a fill (#440): the oracle puts the next item on a line of its own,
    ///     <c>2 /* a</c> / <c>b */,</c> / <c>3</c>, where the same comment after the comma keeps it —
    ///     <c>2, /* a</c> / <c>b */ 3</c>. The comment belongs to the item it follows, and an item that
    ///     spans lines ends its line.
    /// </remarks>
    static bool EndsInAMultiLineComment(SyntaxToken gap) {
        var comma = gap.GetPreviousToken();
        if (!comma.IsKind(SyntaxKind.CommaToken)) {
            return false;
        }

        foreach (var trivia in comma.LeadingTrivia.Concat(comma.GetPreviousToken().TrailingTrivia)) {
            if ((trivia.IsKind(SyntaxKind.MultiLineCommentTrivia)
                    || trivia.IsKind(SyntaxKind.MultiLineDocumentationCommentTrivia))
                && trivia.ToFullString().Contains('\n')) {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    ///     Plans the comma's other side: kept as a required break where the construct keeps both
    ///     sides and the author broke it, flat otherwise. Returns whether it was kept.
    /// </summary>
    /// <remarks>
    ///     ⚠ A required break rather than a point, because it is not a place the list's style would
    ///     ever break at; it is a line the author wrote and the oracle leaves (SK-DIV-0104).
    /// </remarks>
    bool PlanOtherSideOfComma(SyntaxToken other, bool keeps) {
        if (keeps && BreaksBefore(other)) {
            Mandatory(other);
            return true;
        }

        Flat(other);
        return false;
    }

    /// <summary>
    ///     Joins a break the author wrote in front of a chopped list's comma, under
    ///     <c>skala_wrap_before_comma = false</c>.
    /// </summary>
    /// <remarks>
    ///     ⚠ A switch expression's arms and an enum's members plan the gap <em>after</em> each comma as
    ///     their point and said nothing about the gap before it, so that gap fell through to
    ///     <c>keep_user_linebreaks</c> — and a member written <c>A\n, B</c> came back as three lines,
    ///     <c>A</c>, a comma alone, then <c>B</c>. Measured: the oracle re-lays both constructs at their
    ///     commas exactly as it does an argument list, <c>1 => 1,\n_ => 2</c> and <c>A,\nB</c>
    ///     (SK-DIV-0104). Only the export's value is measured; at <c>true</c> the gap is left as it was.
    /// </remarks>
    void JoinBeforeComma(SyntaxToken comma) {
        if (!options.WrapBeforeComma) {
            Flat(comma);
        }
    }

    static SyntaxToken FirstToken(SyntaxNode node) => node.GetFirstToken();

    static long Key(SyntaxNode node) => ((long)node.SpanStart << 32) | (uint)node.Span.End;

    /// <summary>Whether the source holds <paramref name="character" /> anywhere in a span.</summary>
    bool Holds(char character, int start, int end) {
        for (var i = Math.Max(0, start); i < end && i < source.Length; i++) {
            if (source[i] == character) {
                return true;
            }
        }

        return false;
    }

    /// <summary>Whether the source held a line break in the gap immediately before this token.</summary>
    bool BreaksBefore(SyntaxToken token) => BreaksBeforeIn(source, token);
}
