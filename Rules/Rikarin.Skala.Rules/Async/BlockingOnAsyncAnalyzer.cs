using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Rikarin.Skala.Rules.Metadata;
using Rikarin.Skala.Rules.Modernization;

namespace Rikarin.Skala.Rules.Async;

/// <summary>
/// <c>SK3002</c> — <c>.Result</c>, <c>.Wait()</c> and <c>GetAwaiter().GetResult()</c> on a task.
/// </summary>
/// <remarks>
/// docs/plan/08-rule-catalogue.md § "SK3000 — Async, concurrency, lifetime". Blocking a thread on a
/// task is the ASP.NET and WPF deadlock: the continuation wants the synchronization context the
/// blocked thread is holding and neither side moves again. Where it does not deadlock it still
/// occupies a pool thread, and it re-wraps the exception in an <c>AggregateException</c> so the
/// <c>catch</c> a reader expects does not fire.
/// <para>
/// ⚠ The receiver's type is resolved, never guessed from the member name. A user type with a
/// <c>Result</c> property is common — every <c>Result&lt;T&gt;</c> monad in every functional
/// helper library has one — and a rule that fires on those is a rule that gets switched off in the
/// first week.
/// </para>
/// <para>
/// ⚠ A fix is offered only where the enclosing body is <em>already</em> <c>async</c>. Making a
/// method <c>async</c> changes its signature and therefore every caller, which is a refactor and
/// not an edit a tool may apply unreviewed (docs/plan/10). Elsewhere the finding stands with no
/// fix, because the finding is still true.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class BlockingOnAsyncAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.BlockingOnAsync);

    /// <summary>The types whose members block. Metadata names, matched on the original definition.</summary>
    static readonly string[] TaskTypes = [
        "System.Threading.Tasks.Task", "System.Threading.Tasks.Task`1", "System.Threading.Tasks.ValueTask", "System.Threading.Tasks.ValueTask`1"
    ];

    /// <summary>
    /// The awaiter types whose <c>GetResult()</c> blocks.
    /// </summary>
    /// <remarks>
    /// ⚠ The configured variants are included, because <c>x.ConfigureAwait(false).GetAwaiter()
    /// .GetResult()</c> is the spelling people reach for when they have been told that
    /// <c>ConfigureAwait</c> fixes the deadlock. It does not: it removes one of the two ways to
    /// deadlock and leaves the blocked thread.
    /// </remarks>
    static readonly string[] AwaiterTypes = [
        "System.Runtime.CompilerServices.TaskAwaiter", "System.Runtime.CompilerServices.TaskAwaiter`1", "System.Runtime.CompilerServices.ValueTaskAwaiter",
        "System.Runtime.CompilerServices.ValueTaskAwaiter`1", "System.Runtime.CompilerServices.ConfiguredTaskAwaitable",
        "System.Runtime.CompilerServices.ConfiguredTaskAwaitable`1", "System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable",
        "System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable`1"
    ];

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(static start => {
                var tasks = Resolve(start.Compilation, TaskTypes);
                if (tasks.Count == 0) {
                    return;
                }

                var awaiters = Resolve(start.Compilation, AwaiterTypes);
                start.RegisterSyntaxNodeAction(
                    context => Analyze(context, tasks, awaiters),
                    SyntaxKind.SimpleMemberAccessExpression,
                    SyntaxKind.InvocationExpression
                );
            }
        );
    }

    static HashSet<INamedTypeSymbol> Resolve(Compilation compilation, string[] names) {
        var result = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
        foreach (var name in names) {
            if (compilation.GetTypeByMetadataName(name) is { } type) {
                result.Add(type);
            }
        }

        return result;
    }

    static void Analyze(
        SyntaxNodeAnalysisContext context,
        HashSet<INamedTypeSymbol> tasks,
        HashSet<INamedTypeSymbol> awaiters
    ) {
        var node = context.Node;

        // ⚠ `x.Result` inside `x.Result.Length` is visited once as the member access and again as
        // part of the outer one; taking only the innermost blocking form keeps it to one finding.
        var blocking = Match(context, node, tasks, awaiters);
        if (blocking is null) {
            return;
        }

        var (span, receiver, member, producesValue) = blocking.Value;

        if (AsyncContext.IsTestMethod(node)
            || AsyncContext.IsUnawaitablePosition(node)
            || IsEntryPoint(node)
            || AsyncContext.InsideExpressionTree(context.SemanticModel, node, context.CancellationToken)) {
            return;
        }

        var receiverText = receiver.ToString();
        var properties = ImmutableDictionary<string, string?>.Empty;

        // The fix exists only where `await` is already legal. See the type's remarks.
        if (AsyncContext.IsInsideAsyncBody(node) && !node.SpanContainsComment()) {
            var replacement = producesValue && AsyncContext.NeedsParentheses(node)
                ? "(await " + receiverText + ")"
                : "await " + receiverText;

            properties = FixEdits.Pack((span, replacement));
        }

        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                Location.Create(node.SyntaxTree, span),
                properties,
                "Blocking on an async call with `" + member + "`; use `await " + receiverText + "`"
            )
        );
    }

    /// <summary>
    /// The blocking form at this node, or null when there is none.
    /// </summary>
    /// <remarks>
    /// Three shapes, each resolved through the symbol rather than the spelling: the
    /// <c>Result</c> property of a task, the <c>Wait</c> method of a task, and the
    /// <c>GetResult</c> method of a task awaiter.
    /// </remarks>
    static (Microsoft.CodeAnalysis.Text.TextSpan Span, ExpressionSyntax Receiver, string Member, bool ProducesValue)?
        Match(
            SyntaxNodeAnalysisContext context,
            SyntaxNode node,
            HashSet<INamedTypeSymbol> tasks,
            HashSet<INamedTypeSymbol> awaiters
        ) {
        var cancellation = context.CancellationToken;

        switch (node) {
            case MemberAccessExpressionSyntax access
                when string.Equals(access.Name.Identifier.ValueText, "Result", StringComparison.Ordinal): {
                // ⚠ Only the property read. `x.Result` as the left of an assignment cannot happen on
                // a task, but the check costs nothing and keeps the rule from inventing a rewrite
                // for a shape it has not seen.
                if (access.Parent is InvocationExpressionSyntax invocation
                    && ReferenceEquals(invocation.Expression, access)) {
                    return null;
                }

                var symbol = context.SemanticModel.GetSymbolInfo(access, cancellation).Symbol;
                if (symbol is not IPropertySymbol property || !Owns(tasks, property.ContainingType)) {
                    return null;
                }

                return (access.Span, access.Expression, ".Result", true);
            }

            case InvocationExpressionSyntax invocation: {
                if (invocation.Expression is not MemberAccessExpressionSyntax member) {
                    return null;
                }

                var name = member.Name.Identifier.ValueText;
                if (!string.Equals(name, "Wait", StringComparison.Ordinal)
                    && !string.Equals(name, "GetResult", StringComparison.Ordinal)) {
                    return null;
                }

                if (context.SemanticModel.GetSymbolInfo(invocation, cancellation).Symbol is not IMethodSymbol method) {
                    return null;
                }

                if (string.Equals(name, "Wait", StringComparison.Ordinal)) {
                    // ⚠ `Wait(timeout)` is a bounded wait and a different intention: it can return
                    // false, and `await` has no equivalent. Only the unbounded overload is reported.
                    return Owns(tasks, method.ContainingType) && method.Parameters.Length == 0
                        ? (invocation.Span, member.Expression, ".Wait()", false)
                        : null;
                }

                if (!Owns(awaiters, method.ContainingType)) {
                    return null;
                }

                // `x.GetAwaiter().GetResult()` — the receiver is whatever produced the awaiter, so
                // the rewrite awaits that rather than the awaiter.
                if (member.Expression is not InvocationExpressionSyntax awaiterCall
                    || awaiterCall.Expression is not MemberAccessExpressionSyntax awaiterAccess
                    || !string.Equals(
                        awaiterAccess.Name.Identifier.ValueText,
                        "GetAwaiter",
                        StringComparison.Ordinal
                    )) {
                    return null;
                }

                return (
                    invocation.Span,
                    awaiterAccess.Expression,
                    ".GetAwaiter().GetResult()",
                    !method.ReturnsVoid
                );
            }

            default:
                return null;
        }
    }

    static bool Owns(HashSet<INamedTypeSymbol> types, INamedTypeSymbol? containing) =>
        containing is not null && types.Contains(containing.OriginalDefinition);

    /// <summary>
    /// ⚠ An entry point is the one place blocking is the correct thing to do.
    /// </summary>
    /// <remarks>
    /// A synchronous <c>Main</c> that blocks on an async pipeline has nowhere to await from and no
    /// synchronization context to deadlock against. Reporting it would be reporting the idiom the
    /// language recommended before C# 7.1.
    /// </remarks>
    static bool IsEntryPoint(SyntaxNode node) {
        for (var current = node; current is not null; current = current.Parent) {
            if (current is MethodDeclarationSyntax method) {
                return string.Equals(method.Identifier.ValueText, "Main", StringComparison.Ordinal);
            }

            // Top-level statements: the compilation unit is the entry point body.
            if (current is GlobalStatementSyntax) {
                return true;
            }
        }

        return false;
    }
}
