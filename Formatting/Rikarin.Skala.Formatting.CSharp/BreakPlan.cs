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
    /// ⚠ Not a break point of the construct that encloses it, so it never holds a break — even if
    /// the author put one there.
    /// </summary>
    /// <remarks>
    /// This is the half of the break-position model that removes lines. With
    /// <c>wrap_before_binary_opsign = true</c> the gap before the operator is a break point and the
    /// gap after it is not, so <c>a +\n b</c> is re-joined and <c>a\n + b</c> is kept. Milestone 1
    /// had nowhere to express the difference and kept both.
    /// </remarks>
    Flat,

    /// <summary>A break the rules require, whatever the source did.</summary>
    Mandatory,

    /// <summary>
    /// A break point of a <em>fill</em>: it breaks when what follows would not fit on the line, and
    /// not merely because its group broke. <c>wrap_if_long</c>.
    /// </summary>
    FillPoint
}

/// <summary>One gap's rule.</summary>
public readonly record struct GapSpec(GapRule Rule, int Group);

// ⚠ A break point carries no "what it looks like when flat". Its flat form is whatever the ninety
// space rules say about that pair of tokens, and asking the plan instead means the plan has to know
// space_after_comma, space_within_parentheses, space_before_ternary_quest and the rest — which is
// how three Tier A spacing keys silently stopped being observable the first time this was written
// with a bool.

/// <summary>A group the builder opens around one syntax node.</summary>
/// <param name="SpendsIndent">
/// ⚠ The group's break points are not inside a delimiter of their own, so the group opens the
/// continuation scope itself, around its own body. Milestone 1 opened that scope lazily at the break
/// and closed it at the enclosing statement, which was fine while the document had nothing but
/// indent scopes on its stack; with groups on the same stack the two interleave and the group's
/// close pops the indent instead of the group. The symptom is the second operand of
/// <c>a\n + b\n + c</c> landing a level short of the first.
/// </param>
/// <param name="LeadingGapInside">
/// ⚠ The group's first break point is the gap <em>before</em> the node, so the group has to open
/// before that gap is written or the point is emitted outside the group it belongs to and the
/// writer, finding the group unresolved, renders it flat. The one construct that needs it is a base
/// list under <c>wrap_before_extends_colon = true</c>, whose only break point is the colon that
/// starts the node. Every other group's first point is at a token in its interior.
/// </param>
/// <param name="OwnLevel">
/// ⚠ A second continuation level, on top of whatever the construct around it already spends. Only a
/// binary <em>pattern</em> chain asks for it, and docs/plan/04 § "Indentation" is where the
/// asymmetry is recorded: a binary expression chain spends no level of its own and a binary pattern
/// chain spends one. It looks arbitrary and it is what the oracle writes —
/// <c>return x is A\n        or B;</c> puts the operand two levels in and
/// <c>return a\n    + b;</c> puts it one.
/// </param>
public readonly record struct GroupPlan(
    int Id,
    GroupMode Mode,
    GroupFacts Facts,
    bool SpendsIndent = false,
    bool LeadingGapInside = false,
    bool OwnLevel = false);

/// <summary>
/// Decides, before a token is emitted, which gaps of a construct may break and which may not.
/// </summary>
/// <remarks>
/// ⚠ This is the model milestone 1 did not have. M1 decided <em>whether</em> a gap holds a break by
/// copying the source; M2 has to decide <em>which side of a token</em> a break lands on, because
/// that is what <c>wrap_before_binary_opsign</c>, <c>wrap_after_invocation_lpar</c>,
/// <c>wrap_before_invocation_rpar</c>, <c>wrap_after_dot_in_method_calls</c> and
/// <c>wrap_before_comma</c> configure, and a gap model with only "break / do not break" has nowhere
/// to put the answer.
/// <para>
/// It is a pre-pass over the syntax tree rather than a decision taken during the walk, for one
/// reason: a gap can be at the structural level of two constructs at once. In
/// <c>Foo(\n a + b, c)</c> the gap before <c>a</c> is the argument list's first break point and the
/// binary chain's first non-point, and only a pass that sees both can let the point win. During the
/// walk the innermost open construct is the binary chain, and it gives the wrong answer.
/// </para>
/// <para>
/// The rules are established against the oracle, not read off the option names. The three that
/// matter, and that the option documentation does not state:
/// </para>
/// <list type="number">
/// <item>
/// A break <em>between two items</em> of a list is preserved iff <c>keep_user_linebreaks</c>. A
/// break <em>right after the opening delimiter or before the closing one</em> is preserved iff that
/// construct's <c>keep_existing_*_arrangement</c> — which is what makes those keys observable, and
/// it is why <c>Foo1(\n a)</c> re-joins where <c>Foo2(\n a,\n b)</c> does not.
/// </item>
/// <item>
/// Once a construct is broken at all, a <c>chop_*</c> style breaks <em>every</em> one of its points,
/// the two at the delimiters included. That is why the oracle's output over <c>corpus/real/</c> has
/// 1 006 lines that are nothing but a closing parenthesis and milestone 1's had 573.
/// </item>
/// <item>
/// <c>keep_user_wrapping</c> has no observable effect in this export. Both values produce identical
/// output on every shape tried; <c>keep_user_linebreaks</c> is the key that governs.
/// </item>
/// </list>
/// </remarks>
public sealed class BreakPlan {
    readonly Dictionary<int, GapSpec> _gaps = [];

    /// <summary>
    /// The groups opened around one node, outermost first.
    /// </summary>
    /// <remarks>
    /// ⚠ A list rather than one plan, because two constructs can start and end at the same token and
    /// need two groups. A binary chain is the case that forces it: the operators keep their own
    /// groups so that <c>a &amp;&amp; b\n || c</c> comes back unchanged, and the chain needs a group
    /// of its own on the same node so that <c>chop_if_long</c> can break <em>all</em> of them at once
    /// when the whole chain is too wide. One group cannot be both.
    /// </remarks>
    readonly Dictionary<long, List<GroupPlan>> _groups = [];

    /// <summary>The chain-wide group of a binary chain, keyed by its root node.</summary>
    readonly Dictionary<long, int> _chainOwner = [];

    /// <summary>The group of a delimited list, keyed by the list node.</summary>
    /// <remarks>
    /// ⚠ Recorded so that a construct <em>outside</em> the list can read whether the list broke.
    /// <c>place_expr_method_on_single_line = if_owner_is_single_line</c> asks whether the
    /// declaration occupies one line, and a chopped parameter list is the commonest way for it not
    /// to — which no width test on the arrow itself can see.
    /// </remarks>
    readonly Dictionary<long, int> _delimited = [];

    /// <summary>
    /// The group opened <em>inside</em> a construct's delimiters rather than around them.
    /// </summary>
    /// <remarks>
    /// ⚠ It cannot be one of <see cref="_groups"/>, because those are opened around the node and are
    /// therefore entered at the column the node starts at. The elements of a braced initializer are
    /// measured against the column they land on <em>after</em> the brace has broken, which is one
    /// continuation level in and one line down, so the group has to be opened where the elements
    /// begin. <see cref="CSharpDocumentBuilder.VisitBraced"/> opens it.
    /// </remarks>
    readonly Dictionary<long, GroupPlan> _inner = [];

    readonly string _source;
    readonly PhaseOneOptions _options;
    int[] _forced = [];
    int _nextGroup;

    BreakPlan(string source, in PhaseOneOptions options) {
        _source = source;
        _options = options;
    }

    /// <summary>Group ids handed out; the builder pre-allocates that many on the document.</summary>
    public int GroupCount => _nextGroup;

    public static BreakPlan Build(SyntaxNode root, string source, in PhaseOneOptions options) {
        var plan = new BreakPlan(source, options);
        plan.Walk(root);
        plan.CollectForcedBreaks();
        return plan;
    }

    /// <summary>The rule for the gap immediately before <paramref name="position"/>, if any.</summary>
    public bool TryGap(int position, out GapSpec spec) => _gaps.TryGetValue(position, out spec);

    /// <summary>The groups the builder opens around <paramref name="node"/>, outermost first.</summary>
    public IReadOnlyList<GroupPlan> GroupsOf(SyntaxNode node) =>
        _groups.TryGetValue(Key(node), out var plans) ? plans : [];

    /// <summary>The group the builder opens just inside <paramref name="node"/>'s delimiters, if any.</summary>
    public bool TryInnerGroup(SyntaxNode node, out GroupPlan plan) => _inner.TryGetValue(Key(node), out plan);

    /// <summary>Every group the plan created, so the builder can describe them to the document.</summary>
    public IEnumerable<GroupPlan> Groups {
        get {
            foreach (var plans in _groups.Values) {
                foreach (var plan in plans) {
                    yield return plan;
                }
            }

            foreach (var plan in _inner.Values) {
                yield return plan;
            }
        }
    }

    /// <summary>
    /// Whether anything between <paramref name="start"/> and <paramref name="end"/> is certain to
    /// break, whatever the source did.
    /// </summary>
    /// <remarks>
    /// ⚠ The blank-line rules need this, which is not obvious until it bites. Whether a member takes
    /// <c>blank_lines_around_field</c> or <c>blank_lines_around_single_line_field</c> depends on
    /// whether it is single-line — in the <em>output</em>. A one-line field the formatter is about to
    /// chop is not single-line, and reading the input instead makes the first pass emit no blank line
    /// and the second pass emit one. That is a non-idempotency the corpus does not contain, because
    /// milestone 1 chopped nothing.
    /// </remarks>
    public bool HasForcedBreakIn(int start, int end) {
        var index = Array.BinarySearch(_forced, start);
        if (index < 0) {
            index = ~index;
        }

        return index < _forced.Length && _forced[index] < end;
    }

    /// <summary>
    /// The positions at which a break is certain: a <see cref="GroupMode.Break"/> group's points and
    /// every <see cref="GapRule.Mandatory"/> gap. Sorted once so the blank-line rules can ask about a
    /// member's span without walking the whole plan per member.
    /// </summary>
    void CollectForcedBreaks() {
        var forced = new List<int>();
        foreach (var (key, plans) in _groups) {
            foreach (var plan in plans) {
                if (plan.Mode == GroupMode.Break) {
                    forced.Add((int)(key >> 32));
                }
            }
        }

        foreach (var (position, spec) in _gaps) {
            if (spec.Rule == GapRule.Mandatory) {
                forced.Add(position);
            }
        }

        forced.Sort();
        _forced = [.. forced];
    }

    // ── The walk ─────────────────────────────────────────────────────────────────────────────

    /// <remarks>
    /// ⚠ Outer nodes are planned before inner ones, and a point never overwrites a point but always
    /// overwrites a <see cref="GapRule.Flat"/>. That ordering is the conflict rule: the enclosing
    /// construct's break point wins over the nested construct's non-point.
    /// </remarks>
    void Walk(SyntaxNode node) {
        Plan(node);
        foreach (var child in node.ChildNodes()) {
            Walk(child);
        }
    }

    void Plan(SyntaxNode node) {
        PlanAttributes(node);
        PlanEmbeddedStatement(node, EmbeddedStatementOf(node));
        PlanOnePerLine(node);

        switch (node) {
            case EnumDeclarationSyntax enumeration:
                PlanEnum(enumeration);
                return;

            case SwitchExpressionSyntax switchExpression:
                PlanSwitchExpression(switchExpression);
                return;

            case ArgumentListSyntax arguments:
                PlanList(
                    node,
                    arguments.OpenParenToken,
                    arguments.CloseParenToken,
                    arguments.Arguments,
                    arguments.Arguments.GetSeparators(),
                    InvocationKeeps(arguments),
                    _options.WrapArgumentsStyle,
                    _options.WrapAfterInvocationLpar,
                    _options.WrapBeforeInvocationRpar,
                    _options.MaxInvocationArgumentsOnLine
                );
                return;

            case AttributeArgumentListSyntax attributeArguments:
                PlanList(
                    node,
                    attributeArguments.OpenParenToken,
                    attributeArguments.CloseParenToken,
                    attributeArguments.Arguments,
                    attributeArguments.Arguments.GetSeparators(),
                    _options.KeepExistingInvocationParensArrangement,
                    _options.WrapArgumentsStyle,
                    _options.WrapAfterInvocationLpar,
                    _options.WrapBeforeInvocationRpar,
                    _options.MaxInvocationArgumentsOnLine
                );
                return;

            case ParameterListSyntax { Parent: TypeDeclarationSyntax } primaryParameters:
                // ⚠ A primary constructor has its own four keys, and they do not agree with the
                // declaration ones: wrap_before_primary_constructor_declaration_rpar is false where
                // wrap_before_declaration_rpar is true, so `record R(\n int Y,\n int Z);` keeps the
                // closing parenthesis on the last parameter's line.
                PlanList(
                    node,
                    primaryParameters.OpenParenToken,
                    primaryParameters.CloseParenToken,
                    primaryParameters.Parameters,
                    primaryParameters.Parameters.GetSeparators(),
                    _options.KeepExistingPrimaryConstructorParensArrangement,
                    _options.WrapPrimaryConstructorParametersStyle,
                    _options.WrapAfterPrimaryConstructorLpar,
                    _options.WrapBeforePrimaryConstructorRpar,
                    _options.MaxPrimaryConstructorParametersOnLine
                );
                return;

            case ParameterListSyntax parameters:
                PlanList(
                    node,
                    parameters.OpenParenToken,
                    parameters.CloseParenToken,
                    parameters.Parameters,
                    parameters.Parameters.GetSeparators(),
                    DeclarationKeeps(parameters),
                    _options.WrapParametersStyle,
                    _options.WrapAfterDeclarationLpar,
                    _options.WrapBeforeDeclarationRpar,
                    _options.MaxFormalParametersOnLine
                );
                return;

            case InitializerExpressionSyntax initializer:
                PlanInitializer(initializer);
                return;

            case AnonymousObjectCreationExpressionSyntax anonymous:
                PlanAnonymousObject(anonymous);
                return;

            case CollectionExpressionSyntax collection:
                PlanList(
                    node,
                    collection.OpenBracketToken,
                    collection.CloseBracketToken,
                    collection.Elements,
                    collection.Elements.GetSeparators(),
                    _options.KeepExistingListPatternsArrangement,
                    _options.WrapListPattern,
                    wrapAfterOpen: true,
                    wrapBeforeClose: true,
                    placeOnSingleLine: _options.PlaceSimpleListPatternOnSingleLine
                );
                return;

            case ListPatternSyntax listPattern:
                PlanList(
                    node,
                    listPattern.OpenBracketToken,
                    listPattern.CloseBracketToken,
                    listPattern.Patterns,
                    listPattern.Patterns.GetSeparators(),
                    _options.KeepExistingListPatternsArrangement,
                    _options.WrapListPattern,
                    wrapAfterOpen: true,
                    wrapBeforeClose: true,
                    placeOnSingleLine: _options.PlaceSimpleListPatternOnSingleLine
                );
                return;

            case PropertyPatternClauseSyntax propertyPattern:
                PlanList(
                    node,
                    propertyPattern.OpenBraceToken,
                    propertyPattern.CloseBraceToken,
                    propertyPattern.Subpatterns,
                    propertyPattern.Subpatterns.GetSeparators(),
                    _options.KeepExistingPropertyPatternsArrangement,
                    _options.WrapPropertyPattern,
                    _options.WrapAfterExpressionLbrace,
                    _options.WrapBeforeExpressionRbrace,
                    placeOnSingleLine: _options.PlaceSimplePropertyPatternOnSingleLine
                );
                return;

            case BaseListSyntax baseList:
                PlanBaseList(baseList);
                return;

            case VariableDeclarationSyntax { Variables.Count: > 1 } declaration:
                PlanDeclarators(declaration);
                return;

            case BinaryExpressionSyntax binary:
                if (IsChainRootOperator(binary)) {
                    PlanChainWide(binary, _options.WrapChainedBinaryExpressions);
                }

                PlanOperator(binary, binary.OperatorToken, binary.Right, _options.WrapBeforeBinaryOpsign);
                return;

            case BinaryPatternSyntax pattern:
                if (IsChainRootOperator(pattern)) {
                    PlanChainWide(pattern, _options.WrapChainedBinaryPatterns);
                }

                PlanOperator(pattern, pattern.OperatorToken, pattern.Right, _options.WrapBeforeBinaryPatternOp);
                return;

            case InvocationExpressionSyntax or ConditionalAccessExpressionSyntax when IsChainRoot(node):
                PlanChainedCalls(node);
                return;

            case ConditionalExpressionSyntax ternary:
                PlanTernary(ternary);
                return;

            case AssignmentExpressionSyntax assignment:
                PlanAroundEquals(assignment, assignment.OperatorToken, assignment.Right);
                return;

            case EqualsValueClauseSyntax { Value: not null } initializer:
                PlanAroundEquals(initializer, initializer.EqualsToken, initializer.Value);
                return;

            case ArrowExpressionClauseSyntax { Expression: not null } arrow:
                PlanExpressionBody(arrow);
                return;


            case SwitchSectionSyntax section:
                PlanCaseStatements(section);
                return;

            case TypeParameterConstraintClauseSyntax constraint when !_options.PlaceTypeConstraintsOnSameLine:
                // place_type_constraints_on_same_line = false: every `where` starts its own line.
                Mandatory(constraint.WhereKeyword);
                return;

            case ConstructorInitializerSyntax initializer when !_options.PlaceConstructorInitializerOnSameLine:
                Mandatory(initializer.ColonToken);
                return;

            case PrimaryConstructorBaseTypeSyntax primary when !_options.PlacePrimaryConstructorInitializerOnSameLine:
                Mandatory(FirstToken(primary));
                return;

            default:
                return;
        }
    }

    // ── Constructs ───────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// <c>wrap_enum_declaration = chop_always</c> with <c>max_enum_members_on_line = 1</c>: one
    /// member per line, always, whatever the source did.
    /// </summary>
    /// <remarks>
    /// ⚠ docs/plan/05 names <c>resharper_new_line_before_enumerators</c> for this. That key is in
    /// the export template and is <em>not</em> in <c>options.json</c> — the M0 importer dropped it
    /// along with about forty other C#-relevant unprefixed keys — so the mechanism here is the two
    /// keys that are registered and that produce the same layout. See the M2 report.
    /// </remarks>
    void PlanEnum(EnumDeclarationSyntax node) {
        if (node.Members.Count == 0) {
            return;
        }

        // ⚠ keep_existing_enum_arrangement wins over chop_always: with it on, the oracle leaves
        // `enum E { A, B, C }` on its line even though the wrap style says every member gets one.
        var always = _options.WrapEnumDeclaration == WrapStyle.ChopAlways
            && _options.MaxEnumMembersOnLine <= 1
            && !_options.KeepExistingEnumArrangement;
        var group = NewGroup();
        Point(FirstToken(node.Members[0]), group);
        var broken = BreaksBefore(FirstToken(node.Members[0]));

        foreach (var separator in node.Members.GetSeparators()) {
            var next = separator.GetNextToken();
            if (!next.IsKind(SyntaxKind.None) && next.SpanStart < node.CloseBraceToken.SpanStart) {
                Point(next, group);
                broken |= BreaksBefore(next);
            }
        }

        Point(node.CloseBraceToken, group);
        broken |= BreaksBefore(node.CloseBraceToken);

        Describe(
            node,
            group,
            always ? GroupMode.Break : GroupMode.Preserve,
            new GroupFacts(
                SourceBroken: _options.KeepExistingEnumArrangement && broken,
                BreaksIfTooLong: _options.WrapEnumDeclaration == WrapStyle.ChopIfLong
            )
        );
    }

    /// <summary>
    /// <c>wrap_switch_expression = chop_always</c> and
    /// <c>place_simple_switch_expression_on_single_line = false</c>: every arm, always.
    /// </summary>
    void PlanSwitchExpression(SwitchExpressionSyntax node) {
        if (node.Arms.Count == 0) {
            return;
        }

        var group = NewGroup();
        Point(FirstToken(node.Arms[0]), group);
        var broken = BreaksBefore(FirstToken(node.Arms[0]));

        foreach (var separator in node.Arms.GetSeparators()) {
            var next = separator.GetNextToken();
            if (!next.IsKind(SyntaxKind.None) && next.SpanStart < node.CloseBraceToken.SpanStart) {
                Point(next, group);
                broken |= BreaksBefore(next);
            }
        }

        Point(node.CloseBraceToken, group);
        broken |= BreaksBefore(node.CloseBraceToken);

        // ⚠ `place_simple_switch_expression_on_single_line` outranks `chop_always`, and that is why
        // both keys are observable rather than only the wrap style. With it on, `x switch { 1 => 1,
        // _ => 0 }` stays on its line although the wrap style says every arm gets one;
        // `keep_existing_switch_expression_arrangement` is the other direction, keeping the author's
        // breaks when the placement rule would otherwise join them.
        // ⚠ `keep_existing_switch_expression_arrangement` outranks `chop_always`, which the option
        // names do not suggest and the oracle settles: with it on, `value switch { 1 => 1, _ => 0 }`
        // comes back on one line although the wrap style says every arm gets one of its own. With it
        // off — the export's value — the same expression is chopped. That is what makes both keys
        // observable rather than only the wrap style.
        var keep = _options.KeepExistingSwitchExpressionArrangement;
        var always = _options.WrapSwitchExpression == WrapStyle.ChopAlways
            && !keep
            && !_options.PlaceSimpleSwitchExpressionOnSingleLine;

        Describe(
            node,
            group,
            always ? GroupMode.Break : GroupMode.Preserve,
            new GroupFacts(
                SourceBroken: broken,
                JoinsIfFits: _options.PlaceSimpleSwitchExpressionOnSingleLine && !keep,
                BreaksIfTooLong: _options.WrapSwitchExpression != WrapStyle.WrapIfLong
            )
        );
    }

    /// <summary>
    /// A parenthesised list: <c>wrap_after_*_lpar</c>, <c>wrap_before_*_rpar</c>,
    /// <c>wrap_before_comma</c>, and a <c>chop_*</c> or <c>wrap_if_long</c> style.
    /// </summary>
    /// <remarks>
    /// ⚠ The two delimiter points and the inter-item points are preserved by different keys, and
    /// conflating them makes the <c>keep_existing_*</c> family unobservable. Measured against the
    /// oracle: with <c>keep_existing_invocation_parens_arrangement = false</c>, <c>Foo1(\n a)</c>
    /// re-joins and <c>Foo2(\n a,\n b)</c> does not, because the first has no inter-item break to
    /// keep and the second has one.
    /// </remarks>
    /// <param name="maxOnLine">
    /// <c>max_*_on_line</c>. ⚠ A hard chop and not a fill: measured against the oracle,
    /// <c>new List&lt;int&gt; { 1, 2, 3, 4, 5 }</c> comes back with one element per line under
    /// <c>max_initializer_elements_on_line = 4</c> although it is 41 columns wide, while
    /// <c>new[] { 1, 2, 3, 4, 5 }</c> — governed by <c>max_array_initializer_elements_on_line =
    /// 10000</c> — does not move. The counter is not a width and does not consult one.
    /// </param>
    /// <param name="placeOnSingleLine">
    /// A <c>place_simple_*_on_single_line</c> key, or null where the construct has none.
    /// </param>
    /// <remarks>
    /// ⚠ The key runs in both directions and the name only suggests one of them. At <c>true</c> it
    /// joins, overriding <c>keep_user_linebreaks</c>: a four-line
    /// <c>new Thing\n{\n A = 1,\n B = 2\n}</c> comes back as <c>new Thing { A = 1, B = 2 }</c>. At
    /// <c>false</c> it <em>forces</em> the delimiters apart however short the construct is —
    /// <c>xs is [1, 2, 3]</c> becomes three lines. Measured against the oracle, because "place on
    /// single line = false" reads like permission withheld rather than a break required.
    /// </remarks>
    void PlanList<T>(
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
        bool? placeOnSingleLine = null
    )
        where T : SyntaxNode {
        if (open.IsKind(SyntaxKind.None) || close.IsKind(SyntaxKind.None) || items.Count == 0) {
            return;
        }

        // ⚠ `wrap_if_long` is a fill: the delimiters break together with the group and the gaps
        // between items break one at a time, as the line runs out. Milestone 2 declined to plan
        // these constructs at all rather than chop them, which is why an over-long initializer came
        // back untouched.
        var fill = style == WrapStyle.WrapIfLong;

        // ⚠ place_single_method_argument_lambda_on_same_line = true governs the OPENING parenthesis
        // only. `Assert.Throws(() => {` keeps the lambda on the call's line however long its body
        // is — and the oracle still moves the closing parenthesis to a line of its own, so the body
        // gains a continuation level and the call ends `}\n);`. Flattening both sides is the
        // intuitive reading of the option name and it is wrong.
        var soleLambda = items.Count == 1
            && _options.PlaceSingleMethodArgumentLambdaOnSameLine
            && IsLambdaArgument(items[0]);

        var group = NewGroup();
        _delimited[Key(node)] = group;
        var first = FirstToken(items[0]);
        var delimiterBroken = !soleLambda && BreaksBefore(first) || BreaksBefore(close);

        // ⚠ `wrap_after_X_lpar = false` means "do not put the first item on a line of its own",
        // not "join one the author put there". The two readings differ on
        // `record R(\n a,\n b\n)`, where the oracle keeps the closing parenthesis where the author
        // left it and Flat would pull it back up. The sole-lambda case below is the one place the
        // oracle really does re-join, and it says so with its own key.
        if (wrapAfterOpen && !soleLambda) {
            Point(first, group);
        } else if (soleLambda) {
            Flat(first);
        }

        // ⚠ A fill re-flows every gap it owns, and one construct family will not have that.
        // `keep_existing_list_patterns_arrangement = true` preserves the author's break at each
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
        var pinsItemBreaks = fill && keepExisting && _options.KeepsUserBreaksBetweenItems;

        var interBroken = false;
        foreach (var comma in separators) {
            var next = comma.GetNextToken();
            if (next.IsKind(SyntaxKind.None) || next.SpanStart >= close.SpanStart) {
                continue;
            }

            // wrap_before_comma = false puts the break after the comma, which is the gap before the
            // next item; true puts it before the comma.
            var gap = _options.WrapBeforeComma ? comma : next;
            var broke = BreaksBefore(gap);
            if (pinsItemBreaks && broke) {
                Mandatory(gap);
            } else {
                Point(gap, group, fill);
            }

            Flat(_options.WrapBeforeComma ? next : comma);
            interBroken |= broke;
        }

        if (wrapBeforeClose) {
            Point(close, group);
        }

        // ⚠ Two keys, two kinds of gap, and the second is gated by the first. Measured against the
        // oracle in all four corners of docs/plan/05's table (constructs/preservation/*):
        //   keep_user_linebreaks | keep_existing_X | delimiters | between items
        //   true                 | true            | kept       | kept
        //   true                 | false           | re-joined  | kept
        //   false                | true            | re-joined  | re-joined
        //   false                | false           | re-joined  | re-joined
        // The global switch turns the per-construct one off; the per-construct one does not turn the
        // global one on.
        // ⚠ The counter wins over everything else, joining included: over the cap the construct is
        // chopped whatever its width and whatever the author wrote.
        var overCap = items.Count > maxOnLine;

        // ⚠ And the per-construct `keep_existing_*` key outranks the placement key in both
        // directions: with keep on, neither the join at `true` nor the forced break at `false`
        // happens at all.
        var joins = placeOnSingleLine == true && !keepExisting;
        var forced = placeOnSingleLine == false && !keepExisting;

        var broken = style == WrapStyle.ChopAlways
            || overCap
            || forced
            || _options.KeepsUserBreaksBetweenItems
            && interBroken
            || _options.KeepsUserBreaksBetweenItems
            && keepExisting
            && delimiterBroken;

        // ⚠ The per-construct `keep_existing_*` key outranks `place_simple_*_on_single_line`, and the
        // oracle is the only place that says so. With
        // `keep_existing_list_patterns_arrangement = true` — the export's value — a list pattern the
        // author split over three lines stays split, although `place_simple_list_pattern_on_single_line`
        // is also true and would otherwise join it; flipping the keep key to false joins it. Reading
        // the placement key as the stronger of the two makes both of them unobservable at once.
        Describe(
            node,
            group,
            style == WrapStyle.ChopAlways || overCap || forced ? GroupMode.Break : GroupMode.Preserve,
            new GroupFacts(
                SourceBroken: broken,
                JoinsIfFits: joins && !overCap,
                BreaksIfTooLong: true,
                HidesFlatWidthWhenBroken: true
            )
        );
    }

    /// <summary>
    /// An initializer's braces: <c>wrap_array_initializer_style = wrap_if_long</c> plus the two
    /// element counters and <c>place_simple_initializer_on_single_line</c>.
    /// </summary>
    /// <remarks>
    /// ⚠ Two counters, and which one applies is the syntax kind rather than the option name.
    /// Measured against the oracle: <c>new List&lt;int&gt; { 1, 2, 3, 4, 5 }</c> comes back with one
    /// element per line and <c>new[] { 1, 2, 3, 4, 5 }</c> does not, because the first is a
    /// collection initializer (<c>max_initializer_elements_on_line = 4</c>) and the second an array
    /// initializer (<c>max_array_initializer_elements_on_line = 10000</c>). Reading
    /// "array initializer" as "any initializer of a collection" gets both wrong at once.
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
            array: false
        );

    /// <summary>
    /// A braced initializer: two groups, because it has three layouts and one group has two.
    /// </summary>
    /// <remarks>
    /// ⚠ Established against the oracle, and it is not what the option names suggest. The three
    /// layouts an initializer can take are
    /// <code>
    /// new Thing { A = 1, B = 2 }                          — everything on the owner's line
    /// new Thing {
    ///     A = 1, B = 2, C = 3                             — braces broken, elements together
    /// }
    /// new Thing {
    ///     A = "…", B = "…", C = "…", D = "…"              — braces broken, one element per line
    /// }                                                     (written one per line)
    /// </code>
    /// and the second and third are one group's decision while the first is another's. A single
    /// group can only offer two of the three, which is why milestone 3's first attempt filled
    /// <c>Title = "Episode VII", Description = "…", Categories =\n    new List&lt;string&gt; {…}</c>
    /// where the oracle writes four lines.
    /// <para>
    /// ⚠ The inner group is a <em>fill</em> for an array initializer and a chop for an object or
    /// collection one, and that distinction is real: <c>new[] { six, long, string, literals, here,
    /// again }</c> comes back with five on one line and one on the next, while
    /// <c>new List&lt;string&gt; { four, long, string, literals }</c> comes back with one per line
    /// even though two of them would have shared. It matches the two counters —
    /// <c>max_array_initializer_elements_on_line = 10000</c> against
    /// <c>max_initializer_elements_on_line = 4</c> — being separate keys.
    /// </para>
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
        if (open.IsKind(SyntaxKind.None) || close.IsKind(SyntaxKind.None) || items.Count == 0) {
            return;
        }

        var style = _options.WrapArrayInitializerStyle;
        var cap = array ? _options.MaxArrayInitializerElementsOnLine : _options.MaxInitializerElementsOnLine;
        var overCap = items.Count > cap;
        var joins = _options.PlaceSimpleInitializerOnSingleLine && !overCap;
        var forced = !_options.PlaceSimpleInitializerOnSingleLine;

        var outer = NewGroup();
        var first = FirstToken(items[0]);
        var broken = BreaksBefore(first) || BreaksBefore(close);

        if (_options.WrapAfterExpressionLbrace) {
            Point(first, outer);
        }

        if (_options.WrapBeforeExpressionRbrace) {
            Point(close, outer);
        }

        // ⚠ A fill only for an array initializer; an object or collection initializer chops.
        var fill = array && style == WrapStyle.WrapIfLong;
        var inner = NewGroup();
        var interBroken = false;
        foreach (var comma in separators) {
            var next = comma.GetNextToken();
            if (next.IsKind(SyntaxKind.None) || next.SpanStart >= close.SpanStart) {
                continue;
            }

            if (_options.WrapBeforeComma) {
                Point(comma, inner, fill);
                Flat(next);
                interBroken |= BreaksBefore(comma);
            } else {
                Flat(comma);
                Point(next, inner, fill);
                interBroken |= BreaksBefore(next);
            }
        }

        broken |= interBroken;
        var mode = style == WrapStyle.ChopAlways || overCap || forced ? GroupMode.Break : GroupMode.Preserve;
        var facts = new GroupFacts(
            SourceBroken: _options.KeepUserLinebreaks && broken || forced,
            JoinsIfFits: joins,
            BreaksIfTooLong: true
        );

        Describe(node, outer, mode, facts);
        DescribeInner(
            node,
            inner,
            mode,
            facts with { SourceBroken = _options.KeepsUserBreaksBetweenItems && interBroken }
        );
    }

    /// <summary>
    /// <c>wrap_extends_list_style = chop_if_long</c>: a long base list puts one base type per line.
    /// </summary>
    /// <remarks>
    /// ⚠ Neither delimiter is a break point. <c>wrap_before_extends_colon = false</c> keeps the
    /// <c>:</c> and the first base type on the declaration's line, and there is no closing delimiter
    /// to move, so the only points are the commas:
    /// <code>
    /// class C : Base,
    ///     IFirst,
    ///     ISecond {
    /// </code>
    /// The list therefore opens its own continuation scope rather than living inside a delimiter's.
    /// </remarks>
    void PlanBaseList(BaseListSyntax node) {
        if (node.Types.Count == 0) {
            return;
        }

        if (node.Types.Count < 2 && !_options.WrapBeforeExtendsColon) {
            return;
        }

        var group = NewGroup();
        var broken = false;

        // ⚠ `wrap_before_extends_colon = true` makes the `:` itself a break point, which is the only
        // way a base list with a single base type can wrap at all. At `false` — the export's value —
        // the gap is left unplanned rather than marked flat: a `false` placement key is permissive
        // and does not remove a break the author wrote, which is the correction docs/plan/05 records
        // for the whole `place_*_on_same_line` family.
        if (_options.WrapBeforeExtendsColon) {
            Point(node.ColonToken, group);
            broken |= BreaksBefore(node.ColonToken);
        }

        foreach (var comma in node.Types.GetSeparators()) {
            var next = comma.GetNextToken();
            if (next.IsKind(SyntaxKind.None)) {
                continue;
            }

            if (_options.WrapBeforeCommaInBaseClause) {
                Point(comma, group);
                Flat(next);
                broken |= BreaksBefore(comma);
            } else {
                Flat(comma);
                Point(next, group);
                broken |= BreaksBefore(next);
            }
        }

        Describe(
            node,
            group,
            _options.WrapExtendsListStyle == WrapStyle.ChopAlways ? GroupMode.Break : GroupMode.Preserve,
            new GroupFacts(
                SourceBroken: _options.KeepsUserBreaksBetweenItems && broken,
                BreaksIfTooLong: _options.WrapExtendsListStyle != WrapStyle.WrapIfLong
            ),
            spendsIndent: true,
            leadingGapInside: _options.WrapBeforeExtendsColon
        );
    }

    /// <summary>
    /// <c>wrap_multiple_declaration_style = chop_if_long</c>: <c>int a = 1, b = 2, c = 3;</c> puts
    /// one declarator per line when it does not fit.
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

        Describe(
            node,
            group,
            _options.WrapMultipleDeclarationStyle == WrapStyle.ChopAlways ? GroupMode.Break : GroupMode.Preserve,
            new GroupFacts(
                SourceBroken: _options.KeepsUserBreaksBetweenItems && broken,
                BreaksIfTooLong: _options.WrapMultipleDeclarationStyle != WrapStyle.WrapIfLong
            ),
            spendsIndent: true
        );
    }

    /// <summary>
    /// <c>wrap_chained_method_calls = chop_if_long</c>: every <c>.</c> of a chain that does not fit
    /// starts a line, and the first call does not.
    /// </summary>
    /// <remarks>
    /// ⚠ Three keys decide which dots are points, and the answer is not "all of them".
    /// <c>wrap_before_first_method_call = false</c> keeps <c>source.Where(…)</c> together, so the
    /// first invoked dot is not a point; <c>wrap_after_property_in_chained_method_calls = false</c>
    /// means a dot that reaches a property rather than a method is not one either. Verified against
    /// the oracle, which writes
    /// <code>
    /// var q = source.Where(x => x.IsActive)
    ///     .OrderBy(x => x.Name)
    ///     .Select(x => x.Id);
    /// </code>
    /// and not a break before <c>.Where</c>.
    /// </remarks>
    void PlanChainedCalls(SyntaxNode root) {
        var dots = new List<SyntaxToken>();
        Collect(root);

        if (dots.Count < 2) {
            return;
        }

        // wrap_before_first_method_call = false: the first invoked dot stays with its receiver.
        // ⚠ The list is built outermost-first by the walk below, so the *last* entry is the first
        // dot of the chain.
        var first = _options.WrapBeforeFirstMethodCall ? dots.Count : dots.Count - 1;
        var group = NewGroup();
        var broken = false;
        for (var i = 0; i < first; i++) {
            if (_options.WrapAfterDotInMethodCalls) {
                Flat(dots[i]);
                var next = dots[i].GetNextToken();
                Point(next, group);
                broken |= BreaksBefore(next);
            } else {
                Point(dots[i], group);
                broken |= BreaksBefore(dots[i]);
            }
        }

        if (first == 0) {
            return;
        }

        Describe(
            root,
            group,
            _options.WrapChainedMethodCalls == WrapStyle.ChopAlways ? GroupMode.Break : GroupMode.Preserve,
            new GroupFacts(
                SourceBroken: _options.KeepsUserBreaksBetweenItems && broken,
                BreaksIfTooLong: _options.WrapChainedMethodCalls != WrapStyle.WrapIfLong,
                HidesFlatWidthWhenBroken: true
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
            ownLevel: true
        );

        void Collect(SyntaxNode node) {
            switch (node) {
                case InvocationExpressionSyntax invocation:
                    if (invocation.Expression is MemberAccessExpressionSyntax access) {
                        // ⚠ `wrap_after_property_in_chained_method_calls = false` does not mean "a
                        // property's dot is not a break point"; it means the break lands *before*
                        // the property rather than after it, so the property travels with the call
                        // it feeds. The oracle writes
                        //     .ToList()
                        //     .Count.ToString()
                        // and not `.ToList().Count` followed by `.ToString()`. Registering the
                        // invoked dot and skipping the property's gives exactly the wrong one of the
                        // two.
                        var dot = access.OperatorToken;
                        var receiver = access.Expression;
                        while (!_options.WrapAfterPropertyInChainedMethodCalls
                               && receiver is MemberAccessExpressionSyntax property) {
                            dot = property.OperatorToken;
                            receiver = property.Expression;
                        }

                        dots.Add(dot);
                        Collect(receiver);
                        return;
                    }

                    if (invocation.Expression is MemberBindingExpressionSyntax binding) {
                        dots.Add(binding.OperatorToken);
                        return;
                    }

                    Collect(invocation.Expression);
                    return;

                case ConditionalAccessExpressionSyntax conditional:
                    // `a?.B().C()` — the `?.` is the binding's dot, already added by the invocation
                    // above; what remains is the receiver.
                    Collect(conditional.Expression);
                    return;

                case MemberAccessExpressionSyntax member:
                    // ⚠ A dot that reaches a property is not a point in this export
                    // (`wrap_after_property_in_chained_method_calls = false`), but it is still part
                    // of the chain and its receiver still has to be walked.
                    if (_options.WrapAfterPropertyInChainedMethodCalls) {
                        dots.Add(member.OperatorToken);
                    }

                    Collect(member.Expression);
                    return;

                case ElementAccessExpressionSyntax element:
                    Collect(element.Expression);
                    return;

                case PostfixUnaryExpressionSyntax postfix:
                    Collect(postfix.Operand);
                    return;

                default:
                    return;
            }
        }
    }

    /// <summary>
    /// The group that makes <c>chop_if_long</c> mean "chop <em>all</em> of them" for a chain whose
    /// links each carry a group of their own.
    /// </summary>
    /// <remarks>
    /// ⚠ It exists because the two behaviours the export asks for cannot live in one group.
    /// <c>keep_user_linebreaks = true</c> means <c>a &amp;&amp; b\n || c</c> comes back with exactly
    /// that one break, so each operator keeps its own <see cref="GroupMode.Preserve"/> group;
    /// <c>wrap_chained_binary_expressions = chop_if_long</c> means a chain that does not fit on one
    /// line breaks at every operator at once, which no per-operator group can decide. This group
    /// spans the whole chain, holds no break points of its own, and the operator groups read its
    /// resolved mode through <see cref="GroupFacts.BreaksWithOwner"/>.
    /// </remarks>
    void PlanChainWide(SyntaxNode root, WrapStyle style) {
        if (style == WrapStyle.WrapIfLong) {
            return;
        }

        var group = NewGroup();
        _chainOwner[Key(root)] = group;
        var pattern = root is BinaryPatternSyntax;
        Describe(
            root,
            group,
            style == WrapStyle.ChopAlways ? GroupMode.Break : GroupMode.Preserve,
            new GroupFacts(BreaksIfTooLong: true),
            // ⚠ A pattern chain spends a level of its own *and* the continuation the construct
            // around it would have spent; a binary expression chain spends only the latter. See
            // GroupPlan.OwnLevel and docs/plan/04 § "Indentation".
            //
            // ⚠ Except as a statement's condition, where `align_multiline_statement_conditions` puts
            // the continuation level and the alignment at the same column and the oracle writes one
            // step, not two:
            //     if (o is IDisposable
            //         or IAsyncDisposable) {     ← one, where an argument would take two
            spendsIndent: pattern,
            ownLevel: pattern && !IsStatementCondition(root)
        );
    }

    /// <summary>
    /// <c>wrap_before_binary_opsign = true</c>: the operator starts the new line, so the gap before
    /// it is the break point and the gap after it is not one.
    /// </summary>
    /// <remarks>
    /// ⚠ One group per operator, not one per chain. The oracle keeps the author's break points
    /// individually: <c>a &amp;&amp; b \n || c</c> comes back unchanged rather than chopped at both
    /// operators. A chain-wide group would break every operator as soon as one of them was broken,
    /// which is what <c>chop_if_long</c> does <em>once the chain is being re-wrapped</em> — and
    /// choosing to re-wrap it is milestone 3's.
    /// </remarks>
    void PlanOperator(SyntaxNode node, SyntaxToken operatorToken, SyntaxNode right, bool wrapBefore) {
        if (operatorToken.IsKind(SyntaxKind.None)) {
            return;
        }

        // ⚠ An operator break is undelimited, so the enclosing statement's continuation level is
        // what it lands on. Milestone 1 spent that level from inside its own Break path; a break
        // point has to ask for it explicitly or `return a\n + b;` comes out flush with the `return`.
        var group = NewGroup();
        bool broken;
        if (wrapBefore) {
            Point(operatorToken, group);
            broken = BreaksBefore(operatorToken);
            Flat(FirstToken(right));
        } else {
            Flat(operatorToken);
            Point(FirstToken(right), group);
            broken = BreaksBefore(FirstToken(right));
        }

        Describe(
            node,
            group,
            GroupMode.Preserve,
            new GroupFacts(
                SourceBroken: _options.KeepsUserBreaksBetweenItems && broken,
                // ⚠ Deliberately *not* HidesFlatWidthWhenBroken. An argument list around a chain the
                // author broke does chop — `Use(a > 0\n && b > 0)` comes back with the argument on a
                // line of its own — but hiding the flat width is not how to get it: an operator group
                // is nested inside the next operator's group, so an unbreakable inner one makes the
                // outer one break too, and `a && b\n || c` comes back chopped at both operators
                // instead of unchanged. Measured: it costs `breaks/binary-operators.cs` and
                // `wrapping/binary-chains.cs`, and buys 0.01 points. SK-DIV-0007.
                BreaksWithOwner: true,
                Owner: ChainOwnerOf(node)
            ),
            spendsIndent: true
        );
    }

    /// <summary>The parameter list whose breaking makes an expression-bodied member multi-line.</summary>
    static SyntaxNode? OwnerListOf(ArrowExpressionClauseSyntax node) =>
        node.Parent switch {
            BaseMethodDeclarationSyntax method => method.ParameterList,
            LocalFunctionStatementSyntax function => function.ParameterList,
            IndexerDeclarationSyntax indexer => indexer.ParameterList,
            _ => null
        };

    /// <summary>Whether this expression is the condition of an if, while, do, for or switch.</summary>
    static bool IsStatementCondition(SyntaxNode node) {
        for (var current = node; current is not null; current = current.Parent) {
            switch (current.Parent) {
                case IfStatementSyntax statement when statement.Condition == current:
                case WhileStatementSyntax statement2 when statement2.Condition == current:
                case DoStatementSyntax statement3 when statement3.Condition == current:
                case SwitchStatementSyntax statement4 when statement4.Expression == current:
                    return true;

                case ExpressionSyntax:
                    continue;

                default:
                    return false;
            }
        }

        return false;
    }

    /// <summary>The chain-wide group of the chain this operator belongs to, or −1.</summary>
    int ChainOwnerOf(SyntaxNode node) {
        var root = node;
        while (SameChain(root.Parent, root)) {
            root = root.Parent!;
        }

        return _chainOwner.TryGetValue(Key(root), out var group) ? group : -1;
    }

    static bool IsChainRootOperator(SyntaxNode node) => !SameChain(node.Parent, node);

    /// <summary>
    /// Whether two nested binary nodes belong to the same chain.
    /// </summary>
    /// <remarks>
    /// ⚠ Same <em>precedence</em>, not merely "both are binary expressions", and getting this wrong
    /// is visible immediately. <c>a &gt; 0 &amp;&amp; b &gt; 0 &amp;&amp; c &gt; 0</c> is one chain
    /// of <c>&amp;&amp;</c> whose operands happen to be comparisons; the oracle chops it at the
    /// <c>&amp;&amp;</c>s and nowhere else. Treating every nested binary as part of the chain makes
    /// the comparisons break too, and produces
    /// <code>
    /// if (a
    ///     &gt; 0
    ///     &amp;&amp; b
    /// </code>
    /// which is not a shape any formatter writes.
    /// </remarks>
    static bool SameChain(SyntaxNode? parent, SyntaxNode child) =>
        (parent, child) switch {
            (BinaryExpressionSyntax outer, BinaryExpressionSyntax inner) =>
                Precedence(outer.OperatorToken.Kind()) == Precedence(inner.OperatorToken.Kind()),
            (BinaryPatternSyntax, BinaryPatternSyntax) => true,
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
            // ⚠ `&&` and `||` are one chain, not two. `a && b || c` is chopped at both operators by
            // the oracle, which is what `wrap_chained_binary_expressions` means by "chained".
            SyntaxKind.AmpersandAmpersandToken or SyntaxKind.BarBarToken => 9,
            SyntaxKind.QuestionQuestionToken => 10,
            _ => 11
        };

    /// <summary>
    /// <c>wrap_before_ternary_opsigns = true</c>: <c>?</c> and <c>:</c> start their lines.
    /// </summary>
    void PlanTernary(ConditionalExpressionSyntax node) {
        var group = NewGroup();
        bool broken;

        if (_options.WrapBeforeTernaryOpsigns) {
            Point(node.QuestionToken, group);
            Point(node.ColonToken, group);
            broken = BreaksBefore(node.QuestionToken) || BreaksBefore(node.ColonToken);
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
            _options.WrapTernaryExprStyle == WrapStyle.ChopAlways ? GroupMode.Break : GroupMode.Preserve,
            new GroupFacts(
                SourceBroken: _options.KeepsUserBreaksBetweenItems && broken,
                BreaksIfTooLong: _options.WrapTernaryExprStyle != WrapStyle.WrapIfLong
            ),
            spendsIndent: true
        );
    }

    /// <summary>
    /// <c>wrap_before_eq = false</c>: a break around an assignment lands after the <c>=</c>, never
    /// before it.
    /// </summary>
    void PlanAroundEquals(SyntaxNode node, SyntaxToken equals, ExpressionSyntax value) {
        if (equals.IsKind(SyntaxKind.None)) {
            return;
        }

        var group = NewGroup();
        bool broken;
        // ⚠ Only one side is planned. `wrap_before_eq = false` says a break the formatter *adds*
        // goes after the `=`; it does not say a break the author put before it is illegal, and the
        // oracle keeps a break before the `=` exactly as written. Registering the other side as a
        // non-point would re-join it, which is a line the author wrote and nobody asked to remove.
        if (_options.WrapBeforeEq) {
            Point(equals, group);
            broken = BreaksBefore(equals);
        } else {
            Point(FirstToken(value), group);
            broken = BreaksBefore(FirstToken(value));
        }

        Describe(
            node,
            group,
            GroupMode.Preserve,
            new GroupFacts(
                // ⚠ Not preserved when the value opens with a delimiter of its own, which in C# is
                // the collection expression and nothing else. Asked directly, `int[] y =\n[\n 1,\n
                // 2\n];` comes back `int[] y = [` while `= \n new[] {`, `= \n new Thing {` and
                // `= \n Make(` all keep the break the author wrote. The `=` break and the `[`'s are
                // alternatives rather than a pair, so leaving the decision to the ordering rule is
                // what reproduces both halves: a bracket that fits on a continuation line still gets
                // the `=` break, and one that has to chop does not.
                SourceBroken: _options.KeepsUserBreaksBetweenItems && broken
                && value is not CollectionExpressionSyntax,

                // ⚠ `prefer_wrap_around_eq`, and the reason milestone 2 stopped at presence. The
                // oracle does break after `=` on a line that is too long — but not always, and
                // breaking whenever the line is long costs 1.18 points of line fidelity against
                // leaving it alone (measured on this branch before the ordering rule existed:
                // 97.47 % → 96.29 %). Which of a long line's candidate points is taken is
                // GroupFacts.PrefersOuterBreak's rule, and it is what makes this key observable.
                BreaksIfTooLong: true,
                MeasuresHead: true,
                PrefersOuterBreak: true
            ),
            spendsIndent: true
        );
    }

    /// <summary>
    /// <c>place_expr_{method,property,accessor}_on_single_line = if_owner_is_single_line</c>: the
    /// body shares the declaration's line exactly when the declaration fits on one.
    /// </summary>
    /// <remarks>
    /// ⚠ <c>keep_existing_expr_member_arrangement = false</c> means a break the author put after the
    /// <c>=&gt;</c> is <em>not</em> preserved: a short expression-bodied member the author had split
    /// over two lines is re-joined. It is one of the few places in this export where the formatter
    /// removes a line break the author chose, and it is measured, not assumed —
    /// <c>int P =&gt;\n 1;</c> comes back as <c>int P =&gt; 1;</c>.
    /// </remarks>
    void PlanExpressionBody(ArrowExpressionClauseSyntax node) {
        var placement = node.Parent switch {
            PropertyDeclarationSyntax or IndexerDeclarationSyntax => _options.PlaceExprPropertyOnSingleLine,
            AccessorDeclarationSyntax => _options.PlaceExprAccessorOnSingleLine,
            _ => _options.PlaceExprMethodOnSingleLine
        };

        var target = _options.WrapBeforeArrowWithExpressions ? node.ArrowToken : FirstToken(node.Expression);
        if (target.IsKind(SyntaxKind.None)) {
            return;
        }

        if (placement == PlacementStyle.Never) {
            Mandatory(target);
            return;
        }

        if (_options.WrapBeforeArrowWithExpressions) {
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

        // ⚠ `if_owner_is_single_line`, literally: the owner is the declaration, and the commonest
        // way for a declaration not to occupy one line is a chopped parameter list. Measured — the
        // oracle writes
        //     public void RenderPassSetBindGroup(
        //         WebGpuObject pass,
        //         …
        //     ) =>
        //         SetBindGroup(pass, group, bindGroup, dynamicOffsets);
        // and the body's own width says nothing about it: `SetBindGroup(…)` fits on the `) =>` line
        // with sixty columns to spare. A width test on the arrow can never produce this break.
        var ownerGroup = OwnerListOf(node) is { } list && _delimited.TryGetValue(Key(list), out var id) ? id : -1;
        Describe(
            node,
            group,
            GroupMode.Preserve,
            new GroupFacts(
                // ⚠ Same exception as the `=`'s, and measured the same way: a collection expression
                // opens with a delimiter of its own, so the arrow's break and the bracket's are
                // alternatives rather than a pair. `TheoryData<string> Corpus =>\n[…]` comes back
                // from the oracle as `Corpus => [` when the bracket has to chop, and
                // `Vector4[] Planes(…) =>\n    [a, b, c];` keeps the arrow's break when it does not.
                // Leaving both to the ordering rule is what produces the pair.
                SourceBroken: BreaksBefore(target) && node.Expression is not CollectionExpressionSyntax,
                PrefersOuterBreak: node.Expression is CollectionExpressionSyntax,
                BreaksWithOwner: ownerGroup >= 0,
                Owner: ownerGroup,
                // keep_existing_expr_member_arrangement = false: a break the author wrote after the
                // arrow is removed when the declaration fits on one line, and left alone when it
                // does not. Adding one where the author wrote none is milestone 3's.
                JoinsIfFits: !_options.KeepExistingExprMemberArrangement,
                // if_owner_is_single_line, the breaking half: the body leaves the declaration's
                // line exactly when the declaration does not fit on one.
                // ⚠ Measured against the whole flat width, not the head: "if owner is single line"
                // means the declaration occupies one line, and a body that spans lines makes it not
                // single-line however short its first line is. `Target Docs => definition => …` with
                // a chain under it is the shape that shows the difference. Measuring the head
                // instead costs 0.12 points of line fidelity on `corpus/real/` and two of the four
                // preservation corners, which is how the reading was settled rather than argued.
                // ⚠ And gated on the keep key, the same way a delimited list's placement key is
                // (see PlanList): `keep_existing_expr_member_arrangement = true` outranks the
                // placement key in *both* directions, so an arrow the author left on the
                // declaration's line stays there however unbreakable the body is. Asked directly,
                // `bool P(object o) => o is {\n First: 1\n };` comes back with the arrow where the
                // author put it under keep, and moved onto its own line under rearrange — the same
                // source, the same body, two answers, and only this key between them.
                BreaksIfTooLong: placement == PlacementStyle.IfOwnerIsSingleLine
                && !_options.KeepExistingExprMemberArrangement
            ),
            spendsIndent: true
        );
    }

    /// <summary>
    /// <c>place_simple_embedded_statement_on_same_line = if_owner_is_single_line</c>: the statement
    /// shares its owner's line exactly when the owner fits on one.
    /// </summary>
    /// <remarks>
    /// ⚠ <c>keep_existing_embedded_arrangement = true</c> in this export, which means the author's
    /// choice wins over the placement rule in both directions and this plan is a no-op for the
    /// repository's own configuration. It is still the mechanism, and flipping either key moves the
    /// output — which is what makes both of them Tier A rather than wiring.
    /// </remarks>
    void PlanEmbeddedStatement(SyntaxNode owner, StatementSyntax? embedded) {
        if (embedded is null or BlockSyntax) {
            return;
        }

        var first = FirstToken(embedded);
        if (first.IsKind(SyntaxKind.None)) {
            return;
        }

        if (_options.PlaceSimpleEmbeddedStatementOnSameLine == PlacementStyle.Never) {
            Mandatory(first);
            return;
        }

        if (_options.PlaceSimpleEmbeddedStatementOnSameLine == PlacementStyle.Always) {
            Flat(first);
            return;
        }

        var group = NewGroup();
        Point(first, group);
        Describe(
            owner,
            group,
            GroupMode.Preserve,
            new GroupFacts(
                SourceBroken: BreaksBefore(first),
                JoinsIfFits: !_options.KeepExistingEmbeddedArrangement,
                BreaksIfTooLong: !_options.KeepExistingEmbeddedArrangement
            )
        );
    }

    /// <summary>
    /// <c>place_simple_case_statement_on_same_line = if_owner_is_single_line</c>: <c>case 1: F();
    /// break;</c> stays on the label's line exactly when the whole section fits on one.
    /// </summary>
    void PlanCaseStatements(SwitchSectionSyntax node) {
        if (node.Statements.Count == 0 || node.Statements is [BlockSyntax]) {
            return;
        }

        var placement = _options.PlaceSimpleCaseStatementOnSameLine;
        var group = placement == PlacementStyle.IfOwnerIsSingleLine ? NewGroup() : -1;
        var broken = false;

        foreach (var statement in node.Statements) {
            var first = FirstToken(statement);
            if (first.IsKind(SyntaxKind.None)) {
                continue;
            }

            switch (placement) {
                case PlacementStyle.Never:
                    Mandatory(first);
                    break;

                case PlacementStyle.Always:
                    Flat(first);
                    break;

                default:
                    Point(first, group);
                    broken |= BreaksBefore(first);
                    break;
            }
        }

        if (group >= 0) {
            Describe(node, group, GroupMode.Preserve, new GroupFacts(SourceBroken: broken, BreaksIfTooLong: true));
        }
    }

    /// <summary>
    /// Every member and every statement gets a line of its own.
    /// </summary>
    /// <remarks>
    /// ⚠ Unconditional, which is not what the option names suggest and is what the oracle does.
    /// <c>csharp_preserve_single_line_blocks = true</c> is in the export and reads like permission
    /// to leave <c>void M() { Call(); Call(); }</c> alone; ReSharper ignores it, and
    /// <c>class B { public int P => 1; public int Q => 2; }</c> comes back as five lines. There is
    /// no width test and no <c>keep_user_linebreaks</c> in it: a body with anything in it is broken.
    /// <para>
    /// ⚠ Three exclusions, each measured rather than assumed. An <em>empty</em> body stays together
    /// (<c>empty_block_style = together</c>). An accessor's body does not break —
    /// <c>get { return _street; }</c> comes back from the oracle exactly as written, and
    /// <c>public int X { get; set; }</c> is one line and has its own spacing keys. And a lambda's or
    /// anonymous method's block does not, because the call it is an argument to keeps it on its line:
    /// <c>Register(() => { Body(); });</c> comes back whole.
    /// </para>
    /// <para>
    /// It is also what makes "single line" a stable property of the output. A member sharing a line
    /// with the member before it has no answer to <c>blank_lines_around_single_line_field</c>, which
    /// is why <c>constructs/blank-lines/two-members-on-one-line.cs</c> was committed failing at M2.
    /// </para>
    /// </remarks>
    void PlanOnePerLine(SyntaxNode node) {
        switch (node) {
            case BlockSyntax { Statements.Count: > 0 } block
                when block.Parent is not (AnonymousFunctionExpressionSyntax or AccessorDeclarationSyntax)
                && !Keeps(block):
                foreach (var statement in block.Statements) {
                    Mandatory(FirstToken(statement));
                }

                Mandatory(block.CloseBraceToken);
                return;

            case TypeDeclarationSyntax { Members.Count: > 0 } type
                when !_options.KeepExistingDeclarationBlockArrangement:
                MembersOnOwnLines(type.Members, type.CloseBraceToken);
                return;

            case NamespaceDeclarationSyntax { Members.Count: > 0 } declaration
                when !_options.KeepExistingDeclarationBlockArrangement:
                MembersOnOwnLines(declaration.Members, declaration.CloseBraceToken);
                return;

            default:
                return;
        }
    }

    /// <summary>
    /// Whether the author's arrangement of this block wins over the one-per-line rule.
    /// </summary>
    /// <remarks>
    /// ⚠ Two keys, and which applies is what the block is the body of.
    /// <c>keep_existing_declaration_block_arrangement</c> governs a method's or a local function's;
    /// <c>keep_existing_embedded_block_arrangement</c> governs an <c>if</c>'s or a <c>while</c>'s.
    /// Both are <c>false</c> in the export, which is why the rule looks unconditional there; set
    /// either to <c>true</c> and the oracle keeps <c>void M() { Body(); }</c> and
    /// <c>if (flag) { First(); }</c> exactly as written. The four-way preservation table is what
    /// found this — the two <c>keep_existing_* = true</c> corners were the only ones that moved.
    /// </remarks>
    bool Keeps(BlockSyntax block) =>
        block.Parent is StatementSyntax
            ? _options.KeepExistingEmbeddedBlockArrangement
            : _options.KeepExistingDeclarationBlockArrangement;

    void MembersOnOwnLines(SyntaxList<MemberDeclarationSyntax> members, SyntaxToken close) {
        foreach (var member in members) {
            Mandatory(FirstToken(member));
        }

        Mandatory(close);
    }

    /// <summary>
    /// <c>place_*_attribute_on_same_line = never</c>: an attribute section never shares a line with
    /// what follows it.
    /// </summary>
    void PlanAttributes(SyntaxNode node) {
        var lists = node switch {
            MemberDeclarationSyntax member => member.AttributeLists,
            LocalFunctionStatementSyntax local => local.AttributeLists,
            StatementSyntax statement => statement.AttributeLists,
            AccessorDeclarationSyntax accessor => accessor.AttributeLists,
            ParameterSyntax { Parent.Parent: TypeDeclarationSyntax } parameter => parameter.AttributeLists,
            _ => default
        };

        if (lists.Count == 0 || AttributePlacement(node) != PlacementStyle.Never) {
            return;
        }

        // keep_existing_attribute_arrangement = true leaves whatever the author wrote.
        if (_options.KeepExistingAttributeArrangement) {
            return;
        }

        foreach (var list in lists) {
            var next = list.CloseBracketToken.GetNextToken();
            if (!next.IsKind(SyntaxKind.None) && next.SpanStart <= node.Span.End) {
                Mandatory(next);
            }
        }
    }

    PlacementStyle AttributePlacement(SyntaxNode node) =>
        node switch {
            BaseTypeDeclarationSyntax or DelegateDeclarationSyntax => _options.PlaceTypeAttributeOnSameLine,
            MethodDeclarationSyntax
                or ConstructorDeclarationSyntax
                or DestructorDeclarationSyntax
                or OperatorDeclarationSyntax
                or ConversionOperatorDeclarationSyntax
                or LocalFunctionStatementSyntax =>
                _options.PlaceMethodAttributeOnSameLine,
            PropertyDeclarationSyntax or IndexerDeclarationSyntax or EventDeclarationSyntax =>
                _options.PlaceAccessorHolderAttributeOnSameLine,
            AccessorDeclarationSyntax => _options.PlaceAccessorAttributeOnSameLine,
            // A record's positional parameter is a field, not a parameter, and has its own key. An
            // ordinary parameter's attribute always stays on the parameter's line.
            ParameterSyntax => _options.PlaceRecordFieldAttributeOnSameLine,
            FieldDeclarationSyntax or EventFieldDeclarationSyntax => _options.PlaceFieldAttributeOnSameLine,
            _ => _options.PlaceAttributeOnSameLine
        };

    // ── Option lookups per construct family ──────────────────────────────────────────────────

    bool InvocationKeeps(ArgumentListSyntax arguments) =>
        arguments.Parent switch {
            PrimaryConstructorBaseTypeSyntax => _options.KeepExistingPrimaryConstructorParensArrangement,
            _ => _options.KeepExistingInvocationParensArrangement
        };

    bool DeclarationKeeps(ParameterListSyntax parameters) =>
        parameters.Parent switch {
            ParenthesizedLambdaExpressionSyntax or AnonymousMethodExpressionSyntax =>
                _options.KeepExistingLambdaParensArrangement,
            _ => _options.KeepExistingDeclarationParensArrangement
        };

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

    static bool IsLambdaArgument(SyntaxNode item) =>
        item is ArgumentSyntax { Expression: AnonymousFunctionExpressionSyntax };

    /// <summary>
    /// The outermost link of an <c>a.B().C()</c> chain — the node the whole chain's group hangs from.
    /// </summary>
    /// <remarks>
    /// ⚠ The same predicate <see cref="CSharpDocumentBuilder"/> uses to decide which node spends the
    /// chain's continuation level; the two must agree, or the group and the indent scope are opened
    /// around different nodes.
    /// </remarks>
    static bool IsChainRoot(SyntaxNode node) =>
        node is InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax or MemberBindingExpressionSyntax }
            or ConditionalAccessExpressionSyntax
        && node.Parent is not (InvocationExpressionSyntax
            or MemberAccessExpressionSyntax
            or ElementAccessExpressionSyntax
            or ConditionalAccessExpressionSyntax
            or MemberBindingExpressionSyntax
            or PostfixUnaryExpressionSyntax);

    // ── Registration ─────────────────────────────────────────────────────────────────────────

    int NewGroup() => _nextGroup++;

    void Describe(
        SyntaxNode node,
        int group,
        GroupMode mode,
        in GroupFacts facts,
        bool spendsIndent = false,
        bool leadingGapInside = false,
        bool ownLevel = false
    ) {
        var key = Key(node);
        if (!_groups.TryGetValue(key, out var plans)) {
            _groups[key] = plans = [];
        }

        plans.Add(new GroupPlan(group, mode, facts, spendsIndent, leadingGapInside, ownLevel));
    }

    void DescribeInner(SyntaxNode node, int group, GroupMode mode, in GroupFacts facts) =>
        _inner[Key(node)] = new GroupPlan(group, mode, facts);

    void Point(SyntaxToken token, int group, bool fill = false) {
        if (token.IsKind(SyntaxKind.None)) {
            return;
        }

        // A point always wins over a Flat left by a nested construct, and never over another point.
        if (_gaps.TryGetValue(token.SpanStart, out var existing) && existing.Rule != GapRule.Flat) {
            return;
        }

        _gaps[token.SpanStart] = new GapSpec(fill ? GapRule.FillPoint : GapRule.Point, group);
    }

    void Flat(SyntaxToken token) {
        if (!token.IsKind(SyntaxKind.None) && !_gaps.ContainsKey(token.SpanStart)) {
            _gaps[token.SpanStart] = new GapSpec(GapRule.Flat, -1);
        }
    }

    void Mandatory(SyntaxToken token) {
        if (!token.IsKind(SyntaxKind.None)) {
            _gaps[token.SpanStart] = new GapSpec(GapRule.Mandatory, -1);
        }
    }

    static SyntaxToken FirstToken(SyntaxNode node) => node.GetFirstToken();

    static long Key(SyntaxNode node) => ((long)node.SpanStart << 32) | (uint)node.Span.End;

    /// <summary>Whether the source held a line break in the gap immediately before this token.</summary>
    bool BreaksBefore(SyntaxToken token) {
        var previous = token.GetPreviousToken();
        if (previous.IsKind(SyntaxKind.None)) {
            return false;
        }

        for (var i = previous.Span.End; i < token.SpanStart; i++) {
            if (_source[i] == '\n') {
                return true;
            }
        }

        return false;
    }
}
