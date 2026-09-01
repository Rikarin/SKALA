using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Rikarin.Skala.Rules.Metadata;
using System.Collections.Immutable;

namespace Rikarin.Skala.Rules.Correctness;

/// <summary>
///     <c>SK2033</c> — a <c>stackalloc</c> a loop re-evaluates.
/// </summary>
/// <remarks>
///     Stack memory is reclaimed when the <em>method</em> returns, not when the block ends, so a
///     <c>stackalloc</c> inside a loop grows the frame by an amount the loop bound decides. The overflow
///     it eventually causes kills the process: no catchable exception, and no diagnostic naming this
///     line.
///     <para>
///         ⚠ <b>No fix, and that is a conclusion rather than an omission.</b> Hoisting the buffer out of
///         the loop changes its lifetime and makes every iteration share it. That is a decision about
///         what the program means, and a rewrite that guessed at it would be wrong exactly where the
///         iterations were supposed to be independent.
///     </para>
///     <para>
///         ⚠ <b>A loop that cannot reach a second iteration is not this defect.</b> The rule excludes
///         <c>do { … } while (false)</c>, <c>while (false)</c>, and a body whose direct statements
///         include an unconditional <c>break</c>, <c>return</c> or <c>throw</c> — the "loop as a labelled
///         block" idiom. That exclusion is withdrawn when the body has a <c>continue</c> or a
///         <c>goto</c> of its own, because either can reach the <c>stackalloc</c> again and the
///         <c>break</c> below it then proves nothing.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class StackAllocInLoopAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.StackallocInLoop);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(
            Analyze,
            SyntaxKind.StackAllocArrayCreationExpression,
            SyntaxKind.ImplicitStackAllocArrayCreationExpression
        );
    }

    static void Analyze(SyntaxNodeAnalysisContext context) {
        var loop = RepeatingLoop(context.Node);
        if (loop is null || RunsAtMostOnce(loop)) {
            return;
        }

        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                context.Node.GetLocation(),
                "`stackalloc` in a "
                + Keyword(loop)
                + " grows the frame once per iteration and is not released until the method returns"
            )
        );
    }

    /// <summary>
    ///     The innermost enclosing loop that evaluates this node more than once, or null.
    /// </summary>
    /// <remarks>
    ///     ⚠ The walk stops at a lambda, an anonymous method, a local function and a member body,
    ///     because each is a separate frame: its stack is released when it returns, however many times
    ///     the loop calls it.
    ///     <para>
    ///         ⚠ It also walks <em>past</em> the positions a loop evaluates once — a <c>for</c>
    ///         initializer or declaration, and a <c>foreach</c> source — rather than stopping there. A
    ///         <c>for</c> initializer is safe inside its own loop and is still a repeat if that whole
    ///         loop sits inside another one.
    ///     </para>
    /// </remarks>
    static StatementSyntax? RepeatingLoop(SyntaxNode node) {
        var child = node;
        for (var current = node.Parent; current is not null; current = current.Parent) {
            switch (current) {
                case SimpleLambdaExpressionSyntax:
                case ParenthesizedLambdaExpressionSyntax:
                case AnonymousMethodExpressionSyntax:
                case LocalFunctionStatementSyntax:
                case AccessorDeclarationSyntax:
                case BaseMethodDeclarationSyntax:
                    return null;

                case ForStatementSyntax loop when !RunsOnce(loop, child):
                    return loop;

                case CommonForEachStatementSyntax loop when !ReferenceEquals(loop.Expression, child):
                    return loop;

                case WhileStatementSyntax loop:
                    return loop;

                case DoStatementSyntax loop:
                    return loop;
            }

            child = current;
        }

        return null;
    }

    /// <summary>Whether the node sits in the part of a <c>for</c> that runs exactly once.</summary>
    static bool RunsOnce(ForStatementSyntax loop, SyntaxNode child) {
        if (ReferenceEquals(loop.Declaration, child)) {
            return true;
        }

        foreach (var initializer in loop.Initializers) {
            if (ReferenceEquals(initializer, child)) {
                return true;
            }
        }

        return false;
    }

    static bool RunsAtMostOnce(StatementSyntax loop) {
        // ⚠ `doLoop` and `whileLoop`, not `@do` and `@while`: SK2034 reported this line the day it
        // shipped, one commit after this one landed.
        if (loop is DoStatementSyntax doLoop
            && doLoop.Condition.IsKind(SyntaxKind.FalseLiteralExpression)
            || loop is WhileStatementSyntax whileLoop
            && whileLoop.Condition.IsKind(SyntaxKind.FalseLiteralExpression)) {
            return true;
        }

        if (Body(loop) is not BlockSyntax block || ReachesTheTopAgain(block)) {
            return false;
        }

        foreach (var statement in block.Statements) {
            if (statement is BreakStatementSyntax or ReturnStatementSyntax or ThrowStatementSyntax) {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    ///     Whether the loop body can jump back to its own start, which makes a trailing <c>break</c>
    ///     no evidence that the body runs once.
    /// </summary>
    /// <remarks>
    ///     ⚠ A <c>continue</c> inside a <em>nested</em> loop belongs to that loop and is not walked
    ///     into. A <c>goto</c> is counted wherever it is, because its target is a label the rule does
    ///     not resolve.
    /// </remarks>
    static bool ReachesTheTopAgain(SyntaxNode node) {
        foreach (var child in node.ChildNodes()) {
            switch (child) {
                case ContinueStatementSyntax:
                case GotoStatementSyntax:
                    return true;

                case ForStatementSyntax:
                case CommonForEachStatementSyntax:
                case WhileStatementSyntax:
                case DoStatementSyntax:
                case SimpleLambdaExpressionSyntax:
                case ParenthesizedLambdaExpressionSyntax:
                case AnonymousMethodExpressionSyntax:
                case LocalFunctionStatementSyntax:
                    continue;

                default:
                    if (ReachesTheTopAgain(child)) {
                        return true;
                    }

                    break;
            }
        }

        return false;
    }

    static StatementSyntax? Body(StatementSyntax loop) =>
        loop switch {
            ForStatementSyntax value => value.Statement,
            CommonForEachStatementSyntax value => value.Statement,
            WhileStatementSyntax value => value.Statement,
            DoStatementSyntax value => value.Statement,
            _ => null
        };

    static string Keyword(StatementSyntax loop) =>
        loop switch {
            ForStatementSyntax => "`for` loop",
            CommonForEachStatementSyntax => "`foreach` loop",
            WhileStatementSyntax => "`while` loop",
            _ => "`do` loop"
        };
}
