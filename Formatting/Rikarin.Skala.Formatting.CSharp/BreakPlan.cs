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
    Mandatory
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
public readonly record struct GroupPlan(int Id, GroupMode Mode, GroupFacts Facts, bool SpendsIndent = false);

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
    readonly Dictionary<long, GroupPlan> _groups = [];
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

    /// <summary>The group the builder opens around <paramref name="node"/>, if any.</summary>
    public bool TryGroup(SyntaxNode node, out GroupPlan plan) => _groups.TryGetValue(Key(node), out plan);

    /// <summary>Every group the plan created, so the builder can describe them to the document.</summary>
    public IEnumerable<GroupPlan> Groups => _groups.Values;

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
        foreach (var (key, plan) in _groups) {
            if (plan.Mode == GroupMode.Break) {
                forced.Add((int)(key >> 32));
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
                    _options.WrapBeforeInvocationRpar
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
                    _options.WrapBeforeInvocationRpar
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
                    _options.WrapBeforePrimaryConstructorRpar
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
                    _options.WrapBeforeDeclarationRpar
                );
                return;

            case BinaryExpressionSyntax binary:
                PlanOperator(binary, binary.OperatorToken, binary.Right, _options.WrapBeforeBinaryOpsign);
                return;

            case BinaryPatternSyntax pattern:
                PlanOperator(pattern, pattern.OperatorToken, pattern.Right, _options.WrapBeforeBinaryPatternOp);
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

        Describe(
            node,
            group,
            _options.WrapSwitchExpression == WrapStyle.ChopAlways ? GroupMode.Break : GroupMode.Preserve,
            new GroupFacts(
                SourceBroken: broken,
                BreaksIfTooLong: _options.WrapSwitchExpression == WrapStyle.ChopIfLong
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
    void PlanList<T>(
        SyntaxNode node,
        SyntaxToken open,
        SyntaxToken close,
        SeparatedSyntaxList<T> items,
        IEnumerable<SyntaxToken> separators,
        bool keepExisting,
        WrapStyle style,
        bool wrapAfterOpen,
        bool wrapBeforeClose
    )
        where T : SyntaxNode {
        if (open.IsKind(SyntaxKind.None) || close.IsKind(SyntaxKind.None) || items.Count == 0) {
            return;
        }

        // wrap_if_long is a fill, which milestone 3 owns; only the chop styles are planned here, and
        // a construct with no plan keeps milestone 1's behaviour of copying the source.
        if (style == WrapStyle.WrapIfLong) {
            return;
        }

        // ⚠ place_single_method_argument_lambda_on_same_line = true governs the OPENING parenthesis
        // only. `Assert.Throws(() => {` keeps the lambda on the call's line however long its body
        // is — and the oracle still moves the closing parenthesis to a line of its own, so the body
        // gains a continuation level and the call ends `}\n);`. Flattening both sides is the
        // intuitive reading of the option name and it is wrong.
        var soleLambda = items.Count == 1
            && _options.PlaceSingleMethodArgumentLambdaOnSameLine
            && IsLambdaArgument(items[0]);

        var group = NewGroup();
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

        var interBroken = false;
        foreach (var comma in separators) {
            var next = comma.GetNextToken();
            if (next.IsKind(SyntaxKind.None) || next.SpanStart >= close.SpanStart) {
                continue;
            }

            // wrap_before_comma = false puts the break after the comma, which is the gap before the
            // next item; true puts it before the comma.
            if (_options.WrapBeforeComma) {
                Point(comma, group);
                Flat(next);
                interBroken |= BreaksBefore(comma);
            } else {
                Flat(comma);
                Point(next, group);
                interBroken |= BreaksBefore(next);
            }
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
        var broken = style == WrapStyle.ChopAlways
            || _options.KeepsUserBreaksBetweenItems && interBroken
            || _options.KeepsUserBreaksBetweenItems && keepExisting && delimiterBroken;

        Describe(
            node,
            group,
            style == WrapStyle.ChopAlways ? GroupMode.Break : GroupMode.Preserve,
            new GroupFacts(SourceBroken: broken, BreaksIfTooLong: true)
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
            new GroupFacts(SourceBroken: _options.KeepsUserBreaksBetweenItems && broken),
            spendsIndent: true
        );
    }

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
            GroupMode.Preserve,
            new GroupFacts(SourceBroken: _options.KeepsUserBreaksBetweenItems && broken),
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
                SourceBroken: _options.KeepsUserBreaksBetweenItems && broken,
                // ⚠ Presence only, again. The oracle does break after `=` on a line that is too
                // long, and preferring that break over chopping the call on the same line is
                // prefer_wrap_around_eq's ordering. Breaking here whenever the line is long, without
                // the ordering, lands one line away from the oracle often enough to cost 0.24 points
                // of line fidelity and five points of file fidelity — measured, not feared. M3.
                MeasuresHead: true
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
        Describe(
            node,
            group,
            GroupMode.Preserve,
            new GroupFacts(
                SourceBroken: BreaksBefore(target),
                // keep_existing_expr_member_arrangement = false: a break the author wrote after the
                // arrow is removed when the declaration fits on one line, and left alone when it
                // does not. Adding one where the author wrote none is milestone 3's.
                JoinsIfFits: !_options.KeepExistingExprMemberArrangement,
                // if_owner_is_single_line, the breaking half: the body leaves the declaration's
                // line exactly when the declaration does not fit on one.
                // ⚠ Measured against the whole flat width, not the head: "if owner is single line"
                // means the declaration occupies one line, and a body that spans lines makes it not
                // single-line however short its first line is. `Target Docs => definition => …` with
                // a chain under it is the shape that shows the difference.
                BreaksIfTooLong: placement == PlacementStyle.IfOwnerIsSingleLine
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
            MethodDeclarationSyntax or ConstructorDeclarationSyntax or DestructorDeclarationSyntax
                or OperatorDeclarationSyntax or ConversionOperatorDeclarationSyntax or LocalFunctionStatementSyntax =>
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

    // ── Registration ─────────────────────────────────────────────────────────────────────────

    int NewGroup() => _nextGroup++;

    void Describe(SyntaxNode node, int group, GroupMode mode, in GroupFacts facts, bool spendsIndent = false) =>
        _groups[Key(node)] = new GroupPlan(group, mode, facts, spendsIndent);

    void Point(SyntaxToken token, int group) {
        if (token.IsKind(SyntaxKind.None)) {
            return;
        }

        // A point always wins over a Flat left by a nested construct, and never over another point.
        if (_gaps.TryGetValue(token.SpanStart, out var existing) && existing.Rule != GapRule.Flat) {
            return;
        }

        _gaps[token.SpanStart] = new GapSpec(GapRule.Point, group);
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
