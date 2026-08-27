using System;
using System.Collections.Generic;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Rikarin.Skala.Rules.Maintainability;

/// <summary>
///     The one walker. Cognitive complexity, statements, nesting depth and the syntactic cyclomatic
///     count, from a single visit of a member.
/// </summary>
/// <remarks>
///     ⚠ docs/plan/07-analysis-host.md § "Metrics": the metrics are "computed in the same pass, from the
///     same trees, because a second traversal of 1.35 M lines to count things is a second traversal".
///     One walker, four counters, one visit — not one walker per metric.
///     <para>
///         ⚠ Cognitive complexity follows <b>Sonar's published definition</b> — "Cognitive Complexity, a new
///         way of measuring understandability", G. Ann Campbell, version 1.7, Appendix B — because the whole
///         value of the number is that it is comparable to SonarQube's on the same code. The specification
///         is three lists and this class is those three lists:
///     </para>
///     <list type="number">
///         <item>
///             <b>B1, increments</b> — <c>if</c>, <c>else if</c>, <c>else</c>, ternary; <c>switch</c>;
///             <c>for</c>, <c>foreach</c>; <c>while</c>, <c>do while</c>; <c>catch</c>; a jump to a label;
///             each <em>sequence</em> of like binary logical operators; each method in a recursion cycle.
///         </item>
///         <item>
///             <b>B2, nesting level</b> — <c>if</c>, <c>else if</c>, <c>else</c>, ternary; <c>switch</c>;
///             loops; <c>catch</c>; and nested methods and method-like structures such as lambdas.
///         </item>
///         <item>
///             <b>B3, nesting increments</b> — <c>if</c>, ternary; <c>switch</c>; loops; <c>catch</c>.
///             ⚠ <c>else</c> and <c>else if</c> are <em>not</em> in this list: "no nesting increment is
///             assessed for these structures because the mental cost has already been paid when reading the
///             if". That single asymmetry is what makes an <c>if</c>/<c>else if</c> chain cost one each rather
///             than growing, and it is the first thing a hand-rolled implementation gets wrong.
///         </item>
///     </list>
///     <para>
///         ⚠ A <c>switch</c> and all its cases cost <b>one</b>, however many cases there are — "a switch can
///         often be taken in at a glance". A twenty-case switch scoring 1 is the headline difference from
///         cyclomatic complexity and is pinned by a fixture.
///     </para>
///     <para>
///         ⚠ <c>??</c>, <c>??=</c> and <c>?.</c> cost nothing: the paper ignores null-coalescing operators
///         by name, "because they allow short-handing multiple lines of code into one". <c>try</c> and
///         <c>finally</c> cost nothing either; only <c>catch</c> does.
///     </para>
///     <para>
///         Where the paper and SonarSource's own C# analyzer disagree, the analyzer wins, because
///         comparability with SonarQube is the goal and SonarQube runs the analyzer. Each such place is
///         marked <c>⚠ SonarAnalyzer</c> below.
///     </para>
/// </remarks>
sealed class MetricsWalker : CSharpSyntaxWalker {
    readonly CancellationToken cancellation;
    readonly string? recursionName;
    readonly int recursionArity;

    // ⚠ The right operand of a like-operator chain, once its parent has paid for the sequence.
    // Reference identity, because two structurally equal `a && b` nodes are two sequences.
    readonly HashSet<SyntaxNode> paidLogicalOperands = new HashSet<SyntaxNode>();

    // ⚠ Method groups cached as delegates. `WithNesting(node, base.VisitIfStatement)` allocates a
    // delegate per `if` otherwise, and over 1.35 M lines that is the metric's whole allocation
    // budget spent on nothing (docs/plan/13).
    readonly Action<IfStatementSyntax> visitIf;
    readonly Action<ConditionalExpressionSyntax> visitConditional;
    readonly Action<SwitchStatementSyntax> visitSwitch;
    readonly Action<SwitchExpressionSyntax> visitSwitchExpression;
    readonly Action<ForStatementSyntax> visitFor;
    readonly Action<ForEachStatementSyntax> visitForEach;
    readonly Action<ForEachVariableStatementSyntax> visitForEachVariable;
    readonly Action<WhileStatementSyntax> visitWhile;
    readonly Action<DoStatementSyntax> visitDo;
    readonly Action<CatchClauseSyntax> visitCatch;

    int cognitiveNesting;
    int blockDepth;
    int insideNestedFunction;

    public MetricsWalker(string? recursionName, int recursionArity, CancellationToken cancellation) {
        this.recursionName = recursionName;
        this.recursionArity = recursionArity;
        this.cancellation = cancellation;

        visitIf = base.VisitIfStatement;
        visitConditional = base.VisitConditionalExpression;
        visitSwitch = base.VisitSwitchStatement;
        visitSwitchExpression = base.VisitSwitchExpression;
        visitFor = base.VisitForStatement;
        visitForEach = base.VisitForEachStatement;
        visitForEachVariable = base.VisitForEachVariableStatement;
        visitWhile = base.VisitWhileStatement;
        visitDo = base.VisitDoStatement;
        visitCatch = base.VisitCatchClause;
    }

    /// <summary>Sonar's cognitive complexity, less the recursion increment.</summary>
    public int Cognitive { get; private set; }

    /// <summary>Statements, not lines. A <c>for</c> header is one statement, not three.</summary>
    public int Statements { get; private set; }

    /// <summary>The deepest block nesting seen; a lambda body restarts the count.</summary>
    public int MaxBlockDepth { get; private set; }

    /// <summary>The syntactic decision-point count, one less than cyclomatic complexity.</summary>
    public int DecisionPoints { get; private set; }

    /// <summary>Whether the member calls itself by name with a matching argument count.</summary>
    public bool HasDirectRecursiveCall { get; private set; }

    public override void Visit(SyntaxNode? node) {
        if (node is null) {
            return;
        }

        cancellation.ThrowIfCancellationRequested();

        // ⚠ A block is not a statement to count: `if (a) { X(); }` is two statements, not three.
        // Counting braces would make the number a function of the formatter, which is what
        // rules.json's SK7003 rationale says statements are counted to avoid.
        if (node is StatementSyntax && node is not BlockSyntax) {
            Statements++;
        }

        CountDecisionPoint(node);

        switch (node) {
            // B2: "nested methods and method-like structures such as lambdas" increment the nesting
            // level and cost nothing themselves. ⚠ SonarAnalyzer skips a *static* local function
            // here because SonarQube measures it as a member in its own right; Skala reports per
            // declared member and a local function is not one, so skipping it would make its
            // complexity invisible rather than reported elsewhere.
            case SimpleLambdaExpressionSyntax:
            case ParenthesizedLambdaExpressionSyntax:
            case AnonymousMethodExpressionSyntax:
            case LocalFunctionStatementSyntax:
                VisitNestedFunction(node);
                return;
        }

        if (IntroducesBlock(node)) {
            blockDepth++;
            if (blockDepth > MaxBlockDepth) {
                MaxBlockDepth = blockDepth;
            }

            base.Visit(node);
            blockDepth--;
            return;
        }

        base.Visit(node);
    }

    // ---- B1 + B3: the structures that pay a structural increment weighted by their nesting ----

    public override void VisitIfStatement(IfStatementSyntax node) {
        // ⚠ An `else if` is an `if` inside an `ElseClause`. The `else` keyword already paid its
        // hybrid +1, so the `if` pays nothing and adds no nesting: the paper's worked examples score
        // `} else if (…) {` at exactly +1 whatever its depth.
        if (node.Parent.IsKind(SyntaxKind.ElseClause)) {
            base.VisitIfStatement(node);
            return;
        }

        IncreaseByNestingPlusOne();
        WithNesting(node, visitIf);
    }

    public override void VisitElseClause(ElseClauseSyntax node) {
        // Hybrid: +1 flat. The clause is already inside the head `if`'s nesting, so the body of an
        // `else` sits at the same level as the body of its `if`.
        Cognitive++;
        base.VisitElseClause(node);
    }

    public override void VisitConditionalExpression(ConditionalExpressionSyntax node) {
        IncreaseByNestingPlusOne();
        WithNesting(node, visitConditional);
    }

    public override void VisitSwitchStatement(SwitchStatementSyntax node) {
        // ⚠ One increment for the whole switch, cases included.
        IncreaseByNestingPlusOne();
        WithNesting(node, visitSwitch);
    }

    public override void VisitSwitchExpression(SwitchExpressionSyntax node) {
        // A switch expression is a switch; SonarAnalyzer scores it the same way.
        IncreaseByNestingPlusOne();
        WithNesting(node, visitSwitchExpression);
    }

    public override void VisitForStatement(ForStatementSyntax node) {
        IncreaseByNestingPlusOne();
        WithNesting(node, visitFor);
    }

    public override void VisitForEachStatement(ForEachStatementSyntax node) {
        IncreaseByNestingPlusOne();
        WithNesting(node, visitForEach);
    }

    public override void VisitForEachVariableStatement(ForEachVariableStatementSyntax node) {
        // `foreach ((var a, var b) in pairs)`. SonarAnalyzer overrides only the non-deconstructing
        // form; a deconstructing foreach is a foreach and the paper's list says `foreach`.
        IncreaseByNestingPlusOne();
        WithNesting(node, visitForEachVariable);
    }

    public override void VisitWhileStatement(WhileStatementSyntax node) {
        IncreaseByNestingPlusOne();
        WithNesting(node, visitWhile);
    }

    public override void VisitDoStatement(DoStatementSyntax node) {
        IncreaseByNestingPlusOne();
        WithNesting(node, visitDo);
    }

    public override void VisitCatchClause(CatchClauseSyntax node) {
        // ⚠ "A catch only adds one point no matter how many exception types are caught. try and
        // finally blocks are ignored altogether."
        IncreaseByNestingPlusOne();
        WithNesting(node, visitCatch);
    }

    public override void VisitGotoStatement(GotoStatementSyntax node) {
        // ⚠ SonarAnalyzer: `goto` takes a *nesting* increment here, where the paper's B3 list would
        // give it a flat +1. The analyzer is what SonarQube runs, and a number that does not match
        // SonarQube's is a number with no reason to exist. `goto case` and `goto default` are the
        // same statement kind and are counted the same way.
        IncreaseByNestingPlusOne();
        base.VisitGotoStatement(node);
    }

    // ---- B1: one increment per *sequence* of like binary logical operators ----

    public override void VisitBinaryExpression(BinaryExpressionSyntax node) {
        var kind = node.Kind();
        if (kind is SyntaxKind.LogicalAndExpression or SyntaxKind.LogicalOrExpression) {
            CountLogicalSequence(node, kind, node.Left, node.Right);
        }

        base.VisitBinaryExpression(node);
    }

    public override void VisitBinaryPattern(BinaryPatternSyntax node) {
        // `x is A and B` reads exactly like `x is A && x is B`, and SonarAnalyzer counts pattern
        // combinators through the same code path.
        var kind = node.Kind();
        if (kind is SyntaxKind.AndPattern or SyntaxKind.OrPattern) {
            CountLogicalSequence(node, kind, node.Left, node.Right);
        }

        base.VisitBinaryPattern(node);
    }

    /// <summary>
    ///     ⚠ The sequence rule, and it is subtler than "one per operator" or "one per expression".
    /// </summary>
    /// <remarks>
    ///     A chain of like operators is one increment; a change of operator starts a new one. The paper:
    ///     <code>
    /// if (a &amp;&amp; b &amp;&amp; c || d || e &amp;&amp; f)   // +1 for `if`, then +1 +1 +1
    /// if (a &amp;&amp; !(b &amp;&amp; c))                       // +1 for `if`, then +1 +1
    ///     </code>
    ///     The second line is why a naive flatten is wrong: <c>!</c> interrupts the sequence even though
    ///     both operators are <c>&amp;&amp;</c>. The rule that produces both numbers is a local one —
    ///     charge for this node unless its left operand is already the same operator, and remember a
    ///     same-operator right operand as paid for — which is SonarAnalyzer's, kept because it is what
    ///     SonarQube computes. Parentheses do not break a sequence; a different operator or any other
    ///     expression shape does.
    /// </remarks>
    void CountLogicalSequence(SyntaxNode node, SyntaxKind kind, SyntaxNode left, SyntaxNode right) {
        if (paidLogicalOperands.Contains(node)) {
            return;
        }

        if (!Unwrap(left).IsKind(kind)) {
            Cognitive++;
        }

        var unwrappedRight = Unwrap(right);
        if (unwrappedRight.IsKind(kind)) {
            paidLogicalOperands.Add(unwrappedRight);
        }
    }

    // ---- B1: each method in a recursion cycle ----

    public override void VisitInvocationExpression(InvocationExpressionSyntax node) {
        // ⚠ Purely syntactic, by name and arity, which is what SonarAnalyzer does — and what lets
        // cognitive complexity stay a Syntax-scoped rule that runs under `--load=loose`. `this.M()`
        // is deliberately not matched: SonarAnalyzer requires a bare identifier.
        if (recursionName is not null
            && node.Expression is IdentifierNameSyntax name
            && node.ArgumentList.Arguments.Count == recursionArity
            && string.Equals(name.Identifier.ValueText, recursionName, StringComparison.Ordinal)) {
            HasDirectRecursiveCall = true;
        }

        base.VisitInvocationExpression(node);
    }

    // ---- shared machinery ----

    void VisitNestedFunction(SyntaxNode node) {
        // ⚠ docs/plan/07 § "Metrics" and rules.json's SK7006: "a lambda body restarts the count,
        // because a lambda is a separate reading context". Nesting *for cognitive complexity* does
        // not restart — the paper is explicit that a lambda increments it — so the two counters move
        // in opposite directions here and that is deliberate.
        var outerDepth = blockDepth;
        blockDepth = 0;
        cognitiveNesting++;
        insideNestedFunction++;

        base.Visit(node);

        insideNestedFunction--;
        cognitiveNesting--;
        blockDepth = outerDepth;
    }

    void IncreaseByNestingPlusOne() => Cognitive += cognitiveNesting + 1;

    void WithNesting<T>(T node, Action<T> visit) where T : SyntaxNode {
        cognitiveNesting++;
        visit(node);
        cognitiveNesting--;
    }

    static SyntaxNode Unwrap(SyntaxNode node) {
        while (true) {
            switch (node) {
                case ParenthesizedExpressionSyntax parenthesized:
                    node = parenthesized.Expression;
                    continue;

                case ParenthesizedPatternSyntax parenthesized:
                    node = parenthesized.Pattern;
                    continue;

                default:
                    return node;
            }
        }
    }

    /// <summary>
    ///     The structures that add a level of block nesting. <c>SK7006</c>.
    /// </summary>
    /// <remarks>
    ///     ⚠ Structures, not braces. <c>if (a) Use(x);</c> nests as deep as the braced form, because the
    ///     metric has to be invariant under the formatter for the same reason SK7003 counts statements
    ///     rather than lines. An <c>else if</c> stays at its chain's level: a five-branch chain is one
    ///     decision a reader makes once, not five levels of indentation.
    /// </remarks>
    static bool IntroducesBlock(SyntaxNode node) =>
        node switch {
            IfStatementSyntax statement => !statement.Parent.IsKind(SyntaxKind.ElseClause),
            ForStatementSyntax => true,
            CommonForEachStatementSyntax => true,
            WhileStatementSyntax => true,
            DoStatementSyntax => true,
            SwitchStatementSyntax => true,
            TryStatementSyntax => true,
            UsingStatementSyntax => true,
            LockStatementSyntax => true,
            FixedStatementSyntax => true,
            CheckedStatementSyntax => true,
            UnsafeStatementSyntax => true,

            // A free-standing `{ … }` used for scoping is a level; a body block is not, because the
            // statement that owns it already counted.
            BlockSyntax block => block.Parent is BlockSyntax,
            _ => false
        };

    /// <summary>
    ///     The syntactic decision points, used for cyclomatic complexity when there is no semantic model
    ///     to build a control-flow graph from.
    /// </summary>
    /// <remarks>
    ///     ⚠ The set is chosen to mirror the conditional branches Roslyn's control-flow graph creates,
    ///     not the textbook keyword list, so that a <c>--load=loose</c> run and a full one report the
    ///     same number for the same member. That agreement is asserted by a test rather than assumed. It
    ///     is why <c>??</c> and <c>?.</c> are counted here and not in cognitive complexity — the compiler
    ///     branches on them even though a reader does not — and why <c>and</c>/<c>or</c> pattern
    ///     combinators are <em>not</em> counted here even though they are counted for cognitive
    ///     complexity: Roslyn's graph evaluates a pattern as one test.
    ///     <para>
    ///         ⚠ A lambda, an anonymous method and a local function are excluded, because Roslyn's graph
    ///         excludes them: each is its own control-flow graph, reachable from the parent's but not part
    ///         of it. Following the graph is what rules.json's <c>SK7001</c> promises — "counted the way the
    ///         compiler sees them rather than the way a regular expression would" — and the cost of a
    ///         complicated lambda is not lost, because cognitive complexity charges it a nesting increment
    ///         and cognitive complexity is the number the gate reads.
    ///     </para>
    /// </remarks>
    void CountDecisionPoint(SyntaxNode node) {
        if (insideNestedFunction > 0) {
            return;
        }

        switch (node.Kind()) {
            case SyntaxKind.IfStatement:
            case SyntaxKind.WhileStatement:
            case SyntaxKind.DoStatement:
            case SyntaxKind.ForEachStatement:
            case SyntaxKind.ForEachVariableStatement:
            case SyntaxKind.ConditionalExpression:
            case SyntaxKind.LogicalAndExpression:
            case SyntaxKind.LogicalOrExpression:
            case SyntaxKind.CoalesceExpression:
            case SyntaxKind.CoalesceAssignmentExpression:
            case SyntaxKind.ConditionalAccessExpression:
            case SyntaxKind.CaseSwitchLabel:
            case SyntaxKind.CasePatternSwitchLabel:
            case SyntaxKind.SwitchExpressionArm:
            case SyntaxKind.CatchFilterClause:
            case SyntaxKind.WhenClause:
                DecisionPoints++;
                break;

            case SyntaxKind.ForStatement:
                // `for (;;)` has no condition and therefore no branch.
                if (((ForStatementSyntax)node).Condition is not null) {
                    DecisionPoints++;
                }

                break;
        }
    }
}
