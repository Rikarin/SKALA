using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Rikarin.Skala.Rules.Metadata;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace Rikarin.Skala.Rules.Async;

/// <summary>
///     <c>SK3512</c> — a variable an enclosing <c>using</c> owns is handed to the caller.
/// </summary>
/// <remarks>
///     docs/plan/08-rule-catalogue.md § "SK3000 — Async, concurrency, lifetime". The <c>using</c>
///     disposes at the <c>return</c>, before the caller has the value, so the method's contract and its
///     body disagree: it says "here is a resource" and delivers one that is already closed. What the
///     caller sees is an <c>ObjectDisposedException</c> on first use, from a method that looks
///     correct, or — for a type whose <c>Dispose</c> only releases a pooled buffer — silent corruption
///     when that buffer is handed to someone else.
///     <para>
///         ⚠ <b>No fix, deliberately.</b> Deleting the <c>using</c> makes the method leak; keeping it
///         makes the method wrong. Which one the author meant is the whole question, and there is no
///         edit that answers it. <c>rules.json</c> carries <c>hasFix: false</c> rather than a guess.
///     </para>
///     <para>
///         ⚠
///         <b>
///             The <c>Task</c> shape belongs to <c>SK3007</c> and is excluded here rather than
///             deduplicated afterwards.
///         </b> The two rules overlap on exactly one shape —
///         <c>return x;</c> where <c>x</c> is a <c>using</c> variable of a task type — and
///         <c>SK3007</c> carries a fix for it. <c>supersedes</c> would suppress the finding that has
///         the fix, or need an edit to <c>SK3007</c>'s own metadata; a type test here costs nothing
///         and keeps each rule's report a property of that rule.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class UsingVariableReturnedAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.UsingVariableReturned);

    static readonly string[] TaskTypes = [
        "System.Threading.Tasks.Task", "System.Threading.Tasks.Task`1", "System.Threading.Tasks.ValueTask",
        "System.Threading.Tasks.ValueTask`1"
    ];

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(static start => {
                var tasks = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
                foreach (var name in TaskTypes) {
                    if (start.Compilation.GetTypeByMetadataName(name) is { } type) {
                        tasks.Add(type);
                    }
                }

                start.RegisterSyntaxNodeAction(context => Analyze(context, tasks), SyntaxKind.ReturnStatement);
            }
        );
    }

    static void Analyze(SyntaxNodeAnalysisContext context, HashSet<INamedTypeSymbol> tasks) {
        var statement = (ReturnStatementSyntax)context.Node;

        // ⚠ The returned expression has to *be* the variable. `return x.Stream;` and
        // `return Wrap(x);` hand out something whose lifetime depends on `x` and may well be the
        // same bug, but proving that needs to know what the member or the method did with it — and
        // a rule that guesses about ownership reports the correct code around the incorrect code.
        if (statement.Expression is null
            || UsingResource.Unwrap(statement.Expression) is not IdentifierNameSyntax identifier) {
            return;
        }

        if (context.SemanticModel.GetSymbolInfo(identifier, context.CancellationToken).Symbol
            is not ILocalSymbol local) {
            return;
        }

        var owner = UsingResource.OwnerOf(local, context.CancellationToken);
        if (owner is null || !ReferenceEquals(BodyOf(statement), BodyOf(owner))) {
            return;
        }

        // SK3007's shape, and it has the fix for it.
        if (local.Type is INamedTypeSymbol named && tasks.Contains(named.OriginalDefinition)) {
            return;
        }

        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                statement.GetLocation(),
                "`" + local.Name + "` is disposed by its `using` before the caller ever sees it"
            )
        );
    }

    /// <summary>
    ///     The member, lambda or local function a node belongs to.
    /// </summary>
    /// <remarks>
    ///     ⚠ Both sides are asked, and they have to be the same one. A <c>return</c> inside a lambda
    ///     returns from the lambda, and a lambda that closes over a <c>using</c> variable may run at a
    ///     time the <c>using</c> says nothing about; equality here is what keeps the rule from reading
    ///     one function's <c>return</c> against another function's scope.
    /// </remarks>
    static SyntaxNode? BodyOf(SyntaxNode node) {
        for (var current = node; current is not null; current = current.Parent) {
            switch (current) {
                case AnonymousFunctionExpressionSyntax:
                case LocalFunctionStatementSyntax:
                case BaseMethodDeclarationSyntax:
                case AccessorDeclarationSyntax:
                    return current;
            }
        }

        return null;
    }
}
