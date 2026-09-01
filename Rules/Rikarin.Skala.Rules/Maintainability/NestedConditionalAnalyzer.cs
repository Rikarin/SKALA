using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Rikarin.Skala.Rules.Metadata;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;

namespace Rikarin.Skala.Rules.Maintainability;

/// <summary>
///     <c>SK7082</c>: how deeply one conditional expression nests inside another.
/// </summary>
/// <remarks>
///     ⚠ <b>A right-associated ladder is one level, not many.</b> <c>a ? x : b ? y : z</c> is an
///     <c>else if</c> chain written as an expression: it reads top to bottom, each condition is tested
///     once, and nobody has ever had to count brackets to follow one. Charging it a level per rung would
///     make the rule fire on the one nested shape that is idiomatic, which is how a rule teaches people
///     to switch its category off. What costs a level is a <c>?:</c> anywhere else inside another —
///     in the condition, in the <c>true</c> branch, or buried inside the <c>false</c> branch rather than
///     being it — because there the reader has to hold a partial answer while working out the thing
///     that selects it.
///     <para>
///         ⚠ A lambda body restarts the count, for the same reason <c>SK7006</c>'s nesting depth does: a
///         lambda is a separate reading context, and <c>xs.Select(x =&gt; x &gt; 0 ? a : b)</c> written
///         inside a conditional is not a nested conditional in any sense a reader experiences.
///     </para>
///     <para>
///         ⚠ <b>An interpolation hole restarts it too, and for the rule's own reason.</b> This rule exists
///         because precedence and reader expectation disagree about <c>?:</c>. Inside <c>$"…{…}…"</c> they
///         cannot: the hole has explicit delimiters, and C# <em>requires</em> a conditional written in one
///         to be parenthesised, because a bare <c>:</c> there is the format specifier. There is nothing
///         left to mis-group.
///     </para>
///     <para>
///         ⚠ Only the outermost conditional of a nest reports. Every conditional in the compilation
///         reaches this action, so reporting each would produce one finding per level pointing at the
///         same expression, with the innermost carrying the smallest number.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class NestedConditionalAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.NestedConditional);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.ConditionalExpression);
    }

    static void Analyze(SyntaxNodeAnalysisContext context) {
        var conditional = (ConditionalExpressionSyntax)context.Node;
        if (IsInsideAnotherConditional(conditional)) {
            return;
        }

        var depth = Depth(conditional);
        var threshold = MetricThresholds
            .Read(context.Options.AnalyzerConfigOptionsProvider.GetOptions(conditional.SyntaxTree))
            .ConditionalNesting;

        if (depth <= threshold) {
            return;
        }

        var properties = ImmutableDictionary<string, string?>.Empty.Add(
            MemberMetrics.ValueKey,
            depth.ToString(CultureInfo.InvariantCulture)
        );

        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                conditional.QuestionToken.GetLocation(),
                properties,
                "The conditional expressions are nested "
                + depth.ToString(CultureInfo.InvariantCulture)
                + " deep, over the threshold of "
                + threshold.ToString(CultureInfo.InvariantCulture)
            )
        );
    }

    static bool IsInsideAnotherConditional(ConditionalExpressionSyntax conditional) {
        for (var node = conditional.Parent; node is not null; node = node.Parent) {
            switch (node) {
                case ConditionalExpressionSyntax:
                    return true;
                case AnonymousFunctionExpressionSyntax:
                case LocalFunctionStatementSyntax:
                case InterpolationSyntax:
                case MemberDeclarationSyntax:
                    return false;
            }
        }

        return false;
    }

    /// <summary>The deepest nest rooted at this conditional, with the ladder walked for free.</summary>
    static int Depth(ConditionalExpressionSyntax conditional) {
        var deepest = 1;
        var rung = conditional;
        while (true) {
            foreach (var inner in Outermost(rung.Condition)) {
                deepest = Math.Max(deepest, 1 + Depth(inner));
            }

            foreach (var inner in Outermost(rung.WhenTrue)) {
                deepest = Math.Max(deepest, 1 + Depth(inner));
            }

            // The ladder: `a ? x : b ? y : z`. The next rung is free, and parentheses around it —
            // which some house styles add and some do not — must not change the number.
            if (Unwrap(rung.WhenFalse) is ConditionalExpressionSyntax next) {
                rung = next;
                continue;
            }

            foreach (var inner in Outermost(rung.WhenFalse)) {
                deepest = Math.Max(deepest, 1 + Depth(inner));
            }

            return deepest;
        }
    }

    static ExpressionSyntax Unwrap(ExpressionSyntax expression) {
        while (expression is ParenthesizedExpressionSyntax parenthesized) {
            expression = parenthesized.Expression;
        }

        return expression;
    }

    /// <summary>
    ///     The conditionals nearest the surface of <paramref name="root" />, without entering a lambda
    ///     and without descending past one that was found.
    /// </summary>
    static IEnumerable<ConditionalExpressionSyntax> Outermost(SyntaxNode root) {
        var pending = new Stack<SyntaxNode>();
        pending.Push(root);
        while (pending.Count > 0) {
            var node = pending.Pop();
            if (node is AnonymousFunctionExpressionSyntax or LocalFunctionStatementSyntax
                or InterpolationSyntax) {
                continue;
            }

            if (node is ConditionalExpressionSyntax conditional) {
                yield return conditional;
                continue;
            }

            foreach (var child in node.ChildNodes()) {
                pending.Push(child);
            }
        }
    }
}
