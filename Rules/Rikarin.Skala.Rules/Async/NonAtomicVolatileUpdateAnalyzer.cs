using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using Rikarin.Skala.Rules.Metadata;
using System.Collections.Immutable;

namespace Rikarin.Skala.Rules.Async;

/// <summary>
///     <c>SK3041</c> — a compound operation is applied to a <c>volatile</c> field.
/// </summary>
/// <remarks>
///     docs/plan/08-rule-catalogue.md § "SK3000 — Async, concurrency, lifetime".
///     <c>volatile</c> buys visibility and orders nothing else: the read and the write of
///     <c>counter++</c> are still two instructions with a window between them, so two threads lose an
///     update exactly as they would without the keyword. What makes this worth a warning rather than a
///     hint is the keyword itself — it is the mark of an author who thought about threading and
///     concluded wrongly, so the next reader trusts the field and does not look again.
///     <para>
///         ⚠ The rule withdraws inside a <c>lock</c>. <c>volatile</c> beside a monitor is redundant, not
///         wrong, and the update under the monitor is atomic; reporting it would send a reader to
///         threading code that is correct, which docs/plan/16 § R3 calls the most expensive kind of
///         false positive there is.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class NonAtomicVolatileUpdateAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.NonAtomicVolatileUpdate);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterOperationAction(
            Analyze,
            OperationKind.CompoundAssignment,
            OperationKind.Increment,
            OperationKind.Decrement
        );
    }

    static void Analyze(OperationAnalysisContext context) {
        var target = context.Operation switch {
            ICompoundAssignmentOperation compound => compound.Target,
            IIncrementOrDecrementOperation step => step.Target,
            _ => null
        };

        // ⚠ `IsVolatile` is read off the resolved symbol, so it holds for a field declared in another
        // file or another assembly. There is no syntax for the modifier at the use site to match on.
        if (target is not IFieldReferenceOperation { Field: { IsVolatile: true } field }) {
            return;
        }

        if (IsGuarded(context.Operation.Syntax)) {
            return;
        }

        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                context.Operation.Syntax.GetLocation(),
                "`"
                + Operator(context.Operation)
                + "` on the volatile field `"
                + field.Name
                + "` is a read and a write, not one operation; `volatile` gives visibility and never atomicity"
            )
        );
    }

    /// <summary>The operator as it is written, so the message names what the reader is looking at.</summary>
    static string Operator(IOperation operation) =>
        operation.Syntax switch {
            AssignmentExpressionSyntax assignment => assignment.OperatorToken.Text,
            PrefixUnaryExpressionSyntax prefix => prefix.OperatorToken.Text,
            PostfixUnaryExpressionSyntax postfix => postfix.OperatorToken.Text,
            _ => "the compound operator"
        };

    /// <summary>
    ///     Whether the update sits somewhere the rule declines to report.
    /// </summary>
    /// <remarks>
    ///     Two withdrawals, both lexical and both conservative — they can only silence a finding, never
    ///     produce one:
    ///     <list type="bullet">
    ///         <item>
    ///             An enclosing <c>lock</c> statement. The monitor already makes the read-modify-write
    ///             atomic and the <c>volatile</c> is merely redundant.
    ///         </item>
    ///         <item>
    ///             A constructor body, where no other thread can hold a reference to the instance yet.
    ///             ⚠ Not through a lambda: a delegate <em>written</em> in a constructor runs whenever
    ///             somebody invokes it, which is exactly the case the constructor argument does not
    ///             cover.
    ///         </item>
    ///     </list>
    ///     ⚠ <c>Monitor.Enter</c> is not recognised, and neither is a semaphore held across the
    ///     statement. Both are the same guarantee spelled without the keyword, and deciding that the
    ///     matching release runs on every path is a dataflow question this rule does not ask. The
    ///     consequence is a missed withdrawal — a false positive, not a miss — which is why the negative
    ///     fixture set records it.
    /// </remarks>
    static bool IsGuarded(SyntaxNode node) {
        var lambda = false;
        for (var current = node.Parent; current is not null; current = current.Parent) {
            switch (current) {
                case LockStatementSyntax:
                    return true;
                case AnonymousFunctionExpressionSyntax:
                case LocalFunctionStatementSyntax:
                    lambda = true;
                    break;
                case ConstructorDeclarationSyntax:
                    return !lambda;
                case MemberDeclarationSyntax:
                    return false;
            }
        }

        return false;
    }
}
