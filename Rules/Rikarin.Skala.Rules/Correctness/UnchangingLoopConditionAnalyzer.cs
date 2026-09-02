using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Rikarin.Skala.Rules.Metadata;
using Rikarin.Skala.Rules.Modernization;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;

namespace Rikarin.Skala.Rules.Correctness;

/// <summary>
///     <c>SK2211</c> — a loop whose condition reads only values the loop cannot change.
/// </summary>
/// <remarks>
///     ⚠ <b>An infinite loop is usually the point, so "the loop has no exit" is the wrong question.</b>
///     <c>while (true)</c> is the event loop, the reactor and the retry pump, and a rule reporting it
///     would be reporting the shape every server is built out of. The narrower and answerable question
///     is whether the condition can ever come out differently: a condition reading only locals and
///     parameters that the body never writes evaluates to the same value on every pass, so the loop
///     runs zero times or hangs. The increment that was meant to be there is missing.
///     <para>
///         ⚠ <b>Everything but a local or a parameter is declined, and that is the whole
///         false-positive story.</b> <c>while (!stopped)</c> on a field, <c>while (queue.Count > 0)</c>,
///         <c>while (reader.Read())</c> — each reads state another statement, another thread or another
///         object changes. Only locals and parameters have a writer set this analysis can enumerate.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class UnchangingLoopConditionAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.UnchangingLoopCondition);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(
            Analyze,
            SyntaxKind.WhileStatement,
            SyntaxKind.DoStatement,
            SyntaxKind.ForStatement
        );
    }

    static void Analyze(SyntaxNodeAnalysisContext context) {
        var (condition, body) = Shape(context.Node);
        if (condition is null || body is null) {
            return;
        }

        var model = context.SemanticModel;
        var cancellation = context.CancellationToken;

        // ⚠ A constant condition is the idiom, not the finding. `while (true)` is deliberate and
        // `while (false)` is somebody's disabled block; neither is a condition that could have
        // changed and did not.
        if (model.GetConstantValue(condition, cancellation).HasValue) {
            return;
        }

        // ⚠ Any exit at all withdraws the finding, reachable or not. Telling somebody their loop
        // hangs when a `return` two branches down ends it is a wrong finding, not a noisy one.
        if (HasExit(body)) {
            return;
        }

        if (ReadVariables(model, condition, cancellation) is not { Count: > 0 } variables) {
            return;
        }

        var written = model.AnalyzeDataFlow(body);
        if (written is not { Succeeded: true }) {
            return;
        }

        foreach (var variable in variables) {
            if (written.WrittenInside.Contains(variable, SymbolEqualityComparer.Default)
                || IsReachableFromAClosure(context.Node, variable)) {
                return;
            }
        }

        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                condition.GetLocation(),
                "nothing in this loop changes '" + string.Join("', '", variables.Select(static v => v.Name)) + "'"
            )
        );
    }

    /// <summary>
    ///     ⚠ A <c>for</c> with any incrementor is declined — that is where the change usually is.
    /// </summary>
    static (ExpressionSyntax? Condition, StatementSyntax? Body) Shape(SyntaxNode loop) =>
        loop switch {
            WhileStatementSyntax w => (w.Condition, w.Statement),
            DoStatementSyntax d => (d.Condition, d.Statement),
            ForStatementSyntax { Incrementors.Count: 0 } f => (f.Condition, f.Statement),
            _ => (null, null)
        };

    /// <summary>
    ///     Every local and parameter the condition reads, or <c>null</c> if it reads anything else.
    /// </summary>
    /// <remarks>
    ///     ⚠ A method call, a property, an element access, a field, <c>this</c> and an <c>await</c> each
    ///     withdraw the finding rather than being skipped over. The proof is "this expression has one
    ///     value for the whole loop", and every one of those reads state whose writers are not in view.
    ///     <para>
    ///         ⚠ <b>Three guards here cover for each other, and sabotage is what showed it.</b> A
    ///         field-only condition is declined by the <c>MemberAccessExpressionSyntax</c> case when it
    ///         is spelled <c>this.stopped</c>, by the <c>IFieldSymbol</c> case when it is spelled
    ///         <c>stopped</c>, and by the empty-list test in the caller when both are removed — because
    ///         a walk that collects nothing has nothing to prove. Removing any one of them left every
    ///         fixture green. The identifier switch is the one that carries the concept, and the fixture
    ///         that isolates it mixes a local with a field so that neither of the other two can reach.
    ///         <c>MemberAccessExpressionSyntax</c> is kept as intent and is not credited.
    ///     </para>
    /// </remarks>
    static List<ISymbol>? ReadVariables(SemanticModel model, ExpressionSyntax condition, CancellationToken cancellation) {
        var result = new List<ISymbol>();
        foreach (var node in condition.DescendantNodesAndSelf()) {
            switch (node) {
                case InvocationExpressionSyntax:
                case ElementAccessExpressionSyntax:
                case AwaitExpressionSyntax:
                case ThisExpressionSyntax:
                case BaseExpressionSyntax:
                case MemberAccessExpressionSyntax:
                case ConditionalAccessExpressionSyntax:
                case ObjectCreationExpressionSyntax:
                    return null;

                case IdentifierNameSyntax identifier:
                    // ⚠ A `ref` local's value can change through the alias without any write appearing
                    // in the body at all, so it is declined with the rest.
                    switch (model.GetSymbolInfo(identifier, cancellation).Symbol) {
                        case ILocalSymbol { IsRef: false } local:
                            result.Add(local);
                            break;

                        case IParameterSymbol { RefKind: RefKind.None } parameter:
                            result.Add(parameter);
                            break;

                        case null:
                        case ILocalSymbol:
                        case IParameterSymbol:
                        case IFieldSymbol:
                        case IPropertySymbol:
                        case IMethodSymbol:
                            return null;
                    }

                    break;
            }
        }

        return result;
    }

    /// <summary>Any jump that can end the loop, anywhere in the body.</summary>
    static bool HasExit(StatementSyntax body) =>
        body.DescendantNodesAndSelf()
            .Any(
                static node => node.IsKind(SyntaxKind.ReturnStatement)
                    || node.IsKind(SyntaxKind.ThrowStatement)
                    || node.IsKind(SyntaxKind.ThrowExpression)
                    || node.IsKind(SyntaxKind.BreakStatement)
                    || node.IsKind(SyntaxKind.YieldBreakStatement)
                    || node.IsKind(SyntaxKind.GotoStatement)
                    || node.IsKind(SyntaxKind.GotoCaseStatement)
                    || node.IsKind(SyntaxKind.GotoDefaultStatement)
            );

    /// <summary>
    ///     ⚠ A delegate created before the loop and invoked inside it writes through the closure without
    ///     the write appearing in the body's data flow at all.
    /// </summary>
    /// <remarks>
    ///     The check over-bails on purpose: any lambda or local function in the member that so much as
    ///     mentions the name is enough to withdraw the finding. Establishing that a particular closure
    ///     cannot reach a particular invocation is the whole-program question this rule exists to avoid,
    ///     and a wrong "your loop hangs" costs more than a missed one.
    /// </remarks>
    static bool IsReachableFromAClosure(SyntaxNode loop, ISymbol variable) {
        var root = RewriteGuards.ScopeRoot(loop);
        foreach (var node in root.DescendantNodes()) {
            if (node is not (AnonymousFunctionExpressionSyntax or LocalFunctionStatementSyntax)) {
                continue;
            }

            foreach (var identifier in node.DescendantNodesAndSelf().OfType<IdentifierNameSyntax>()) {
                if (string.Equals(identifier.Identifier.ValueText, variable.Name, System.StringComparison.Ordinal)) {
                    return true;
                }
            }
        }

        return false;
    }
}
