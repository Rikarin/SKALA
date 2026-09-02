using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Rules.Metadata;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace Rikarin.Skala.Rules.Modernization;

/// <summary>
///     <c>SK1042</c> — an <c>if</c> whose entire body is another <c>if</c> is one condition written
///     as two.
/// </summary>
/// <remarks>
///     ⚠ <b>The whole chain, in one finding, reported at the top.</b> Reporting each adjacent pair
///     would produce overlapping fixes on a triple nesting, and a rule that still fires after its own
///     fix turns <c>skala fix</c> into a loop —
///     <c>RuleFixtureTests.EveryFix_SilencesTheRuleAndIntroducesNoDiagnostic</c> is that as a test. So
///     the chain is collapsed to a single <c>&amp;&amp;</c> condition and the inner <c>if</c>s
///     disappear together.
///     <para>
///         ⚠ <c>&amp;&amp;</c> short-circuits, so evaluation order and count are unchanged; the
///         interesting risk is elsewhere. A pattern variable or <c>out var</c> declared in an inner
///         condition is scoped to that <c>if</c>'s enclosing block, and lifting the condition outwards
///         widens that scope — enough to collide with a name the surrounding member already declares
///         and produce a fix that does not compile. Every name the merged conditions introduce is
///         checked against the rest of the member first.
///     </para>
///     <para>
///         ⚠ Precedence is not free either: <c>a || b</c> merged with <c>c</c> is
///         <c>(a || b) &amp;&amp; c</c> and never <c>a || b &amp;&amp; c</c>, which is a different
///         predicate.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class MergeableIfAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.MergeableIf);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.IfStatement);
    }

    static void Analyze(SyntaxNodeAnalysisContext context) {
        var outer = (IfStatementSyntax)context.Node;

        // The top of the chain owns the finding. Anything nested inside a mergeable `if` is part of
        // somebody else's rewrite.
        if (IsSoleBodyOfMergeable(outer)) {
            return;
        }

        var chain = new List<IfStatementSyntax> { outer };
        while (SoleInnerIf(chain[chain.Count - 1]) is { } inner) {
            chain.Add(inner);
        }

        if (chain.Count < 2) {
            return;
        }

        var innermost = chain[chain.Count - 1];
        var tree = outer.SyntaxTree;
        var text = tree.GetText();

        // Everything outside the surviving body is rewritten or deleted. A comment there is content
        // and a directive is a second parse of the file.
        if (RewriteGuards.ContainsCommentOrDirectiveWithinTheEdit(
                tree,
                TextSpan.FromBounds(outer.SpanStart, innermost.Statement.SpanStart)
            )
            || RewriteGuards.ContainsCommentOrDirectiveWithinTheEdit(
                tree,
                TextSpan.FromBounds(innermost.Statement.Span.End, outer.Span.End)
            )) {
            return;
        }

        // ⚠ The scoping guard. `s` in `if (a) { if (o is string s) … }` is scoped to the outer if's
        // block; merged, it is scoped to the block the merged if lives in, and a `s` already
        // declared there becomes CS0128.
        for (var i = 1; i < chain.Count; i++) {
            foreach (var node in chain[i].Condition.DescendantNodesAndSelf()) {
                foreach (var name in RewriteGuards.DeclaredNames(node)) {
                    if (RewriteGuards.DeclaredElsewhereInMember(outer, name)) {
                        return;
                    }
                }
            }
        }

        var merged = Operand(text, outer.Condition);
        for (var i = 1; i < chain.Count; i++) {
            merged += " && " + Operand(text, chain[i].Condition);
        }

        var edits = new List<(TextSpan Span, string Text)> { (outer.Condition.Span, merged) };

        // Two blocks collapse into one; anything else replaces the first inner `if` with the body
        // that survives it, which keeps whatever braces were already there.
        if (outer.Statement is BlockSyntax outerBlock && innermost.Statement is BlockSyntax innerBlock) {
            edits.Add((outerBlock.Span, text.ToString(innerBlock.Span)));
        } else {
            edits.Add((chain[1].Span, text.ToString(innermost.Statement.Span)));
        }

        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                Location.Create(tree, TextSpan.FromBounds(outer.IfKeyword.SpanStart, outer.CloseParenToken.Span.End)),
                FixEdits.Pack(edits.ToArray()),
                "The nested `if` statements can be combined: `if (" + RewriteGuards.Trim(merged) + ")`"
            )
        );
    }

    /// <summary>Whether this <c>if</c> is the entire body of an <c>if</c> that will merge it.</summary>
    static bool IsSoleBodyOfMergeable(IfStatementSyntax node) {
        if (node.Else is not null) {
            return false;
        }

        var body = node.Parent is BlockSyntax { Statements.Count: 1 } block ? (SyntaxNode)block : node;
        return body.Parent is IfStatementSyntax { Else: null } parent && parent.Statement == body;
    }

    /// <summary>The <c>if</c> that is this one's entire body, if there is one and it has no else.</summary>
    static IfStatementSyntax? SoleInnerIf(IfStatementSyntax node) {
        if (node.Else is not null) {
            return null;
        }

        var body = node.Statement is BlockSyntax { Statements.Count: 1 } block ? block.Statements[0] : node.Statement;
        return body is IfStatementSyntax { Else: null } inner ? inner : null;
    }

    /// <summary>One operand of the merged condition, parenthesised where <c>&amp;&amp;</c> binds tighter.</summary>
    static string Operand(SourceText text, ExpressionSyntax condition) {
        var source = text.ToString(condition.Span);
        return NeedsParentheses(condition) ? "(" + source + ")" : source;
    }

    /// <summary>
    ///     ⚠ Only the expressions <c>&amp;&amp;</c> would capture. Everything else — a relational
    ///     operator, <c>is</c>, a <c>switch</c> expression, <c>await</c>, an invocation — already binds
    ///     tighter, and wrapping it would be noise the formatter is not allowed to remove.
    /// </summary>
    static bool NeedsParentheses(ExpressionSyntax condition) =>
        condition.Kind() switch {
            SyntaxKind.LogicalOrExpression => true,
            SyntaxKind.CoalesceExpression => true,
            SyntaxKind.ConditionalExpression => true,
            SyntaxKind.SimpleAssignmentExpression => true,
            SyntaxKind.SimpleLambdaExpression => true,
            SyntaxKind.ParenthesizedLambdaExpression => true,
            SyntaxKind.QueryExpression => true,
            SyntaxKind.ThrowExpression => true,
            _ => condition is AssignmentExpressionSyntax
        };
}
