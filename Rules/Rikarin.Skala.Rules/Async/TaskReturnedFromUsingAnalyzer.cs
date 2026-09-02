using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Rules.Metadata;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Rikarin.Skala.Rules.Async;

/// <summary>
///     <c>SK3007</c> — a task built from a <c>using</c> variable is returned instead of awaited.
/// </summary>
/// <remarks>
///     docs/plan/08-rule-catalogue.md § "SK3000 — Async, concurrency, lifetime". The <c>using</c>
///     disposes at the <c>return</c>, not when the task finishes, so the operation runs on against a
///     disposed stream, connection or scope. What the caller sees is an <c>ObjectDisposedException</c>
///     with no visible connection to the <c>using</c> — or nothing at all, when the operation happened
///     to complete first, which is how the bug survives every test and appears under load.
///     <para>
///         ⚠ The finding is withheld unless the whole repair is available. Adding <c>async</c> obliges
///         every other <c>return</c> in the method to be awaited too, so the rule collects them, refuses
///         the shapes it cannot rewrite, and reports nothing where it could only rewrite half the method.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class TaskReturnedFromUsingAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.TaskReturnedFromUsing);

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

                if (tasks.Count == 0) {
                    return;
                }

                start.RegisterSyntaxNodeAction(context => Analyze(context, tasks), SyntaxKind.ReturnStatement);
            }
        );
    }

    static void Analyze(SyntaxNodeAnalysisContext context, HashSet<INamedTypeSymbol> tasks) {
        var statement = (ReturnStatementSyntax)context.Node;
        if (statement.Expression is null) {
            return;
        }

        var function = EnclosingFunction(statement);
        if (function is null) {
            return;
        }

        // ⚠ Syntax before semantics, and it is worth 380 ms of the 400 this rule cost before the
        // order was measured (docs/plan/13 § "Analysis"). Almost no `return` in a codebase sits
        // inside a `using` that its expression names, and answering that question needs no symbols
        // at all — so it is asked first, and `IsRewritable`'s body walks run on the handful left.
        //
        // ⚠ The returned expression has to name a resource the enclosing `using`s dispose. A task
        // that never mentions the resource may still be wrong — a `using` that is a lock scope is
        // the case — but the rule cannot prove it, and guessing about ownership is how a rule comes
        // to report the correct code around the incorrect code.
        var resource = DisposedResourceNamedIn(statement, BodyOf(function));
        if (resource is null || !IsRewritable(context, function, tasks, out var returnType, out var body)) {
            return;
        }

        var generic = returnType.IsGenericType;
        var edits = new List<(TextSpan Span, string Text)>();
        foreach (var other in Returns(body)) {
            if (other.Expression is null
                || !SymbolEqualityComparer.Default.Equals(
                    context.SemanticModel.GetTypeInfo(other.Expression, context.CancellationToken).Type,
                    returnType
                )) {
                return;
            }

            if (generic) {
                // `return await expr;` in an `async Task<T>` method returns the T.
                edits.Add((new TextSpan(other.Expression.SpanStart, 0), "await "));
            } else if (ReferenceEquals(other, statement) && IsInTailPosition(other, body)) {
                // ⚠ `return await expr;` is CS1997 in an `async Task` method, so the non-generic
                // form drops the `return` — which is only equivalent where falling off the end is
                // what the `return` was going to do anyway.
                edits.Add((statement.ReturnKeyword.Span, "await"));
            } else {
                return;
            }
        }

        if (edits.Count == 0) {
            return;
        }

        edits.Add((new TextSpan(ReturnTypeOf(function).SpanStart, 0), "async "));
        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                statement.GetLocation(),
                FixEdits.Pack([.. edits]),
                "`" + resource + "` is disposed when this returns, before the task it produced completes"
            )
        );
    }

    /// <summary>The method or local function the return belongs to; null across a lambda.</summary>
    /// <remarks>
    ///     ⚠ Lambdas are excluded rather than handled. <c>async</c> goes in a different place in each
    ///     of their four spellings, and a delegate's return type is inferred from a conversion the rule
    ///     would have to re-check after the edit.
    /// </remarks>
    static SyntaxNode? EnclosingFunction(SyntaxNode node) {
        for (var current = node.Parent; current is not null; current = current.Parent) {
            switch (current) {
                case MethodDeclarationSyntax:
                case LocalFunctionStatementSyntax:
                    return current;
                case AnonymousFunctionExpressionSyntax:
                case BaseMethodDeclarationSyntax:
                case AccessorDeclarationSyntax:
                    return null;
            }
        }

        return null;
    }

    static BlockSyntax? BodyOf(SyntaxNode function) =>
        function is MethodDeclarationSyntax method ? method.Body : ((LocalFunctionStatementSyntax)function).Body;

    static TypeSyntax ReturnTypeOf(SyntaxNode function) =>
        function is MethodDeclarationSyntax method
            ? method.ReturnType
            : ((LocalFunctionStatementSyntax)function).ReturnType;

    /// <summary>
    ///     Whether <c>async</c> can be added to this function at all, and what it returns.
    /// </summary>
    static bool IsRewritable(
        SyntaxNodeAnalysisContext context,
        SyntaxNode function,
        HashSet<INamedTypeSymbol> tasks,
        out INamedTypeSymbol returnType,
        out BlockSyntax body
    ) {
        returnType = null!;
        body = null!;

        SyntaxTokenList modifiers;
        BlockSyntax? block;
        ParameterListSyntax parameters;
        switch (function) {
            case MethodDeclarationSyntax method:
                (modifiers, block, parameters) = (method.Modifiers, method.Body, method.ParameterList);
                break;
            case LocalFunctionStatementSyntax local:
                (modifiers, block, parameters) = (local.Modifiers, local.Body, local.ParameterList);
                break;
            default:
                return false;
        }

        if (block is null) {
            return false;
        }

        foreach (var modifier in modifiers) {
            switch ((SyntaxKind)modifier.RawKind) {
                // Already async; `partial` splits the signature across declarations; `unsafe` and
                // an async body are a combination not worth reasoning about here.
                case SyntaxKind.AsyncKeyword:
                case SyntaxKind.PartialKeyword:
                case SyntaxKind.UnsafeKeyword:
                case SyntaxKind.ExternKeyword:
                case SyntaxKind.AbstractKeyword:
                    return false;
            }
        }

        // ⚠ CS4012: an async method may not take a `ref`, `out` or `in` parameter. Syntax, so it
        // comes before anything that costs a symbol.
        foreach (var parameter in parameters.Parameters) {
            foreach (var modifier in parameter.Modifiers) {
                switch ((SyntaxKind)modifier.RawKind) {
                    case SyntaxKind.RefKeyword:
                    case SyntaxKind.OutKeyword:
                    case SyntaxKind.InKeyword:
                        return false;
                }
            }
        }

        // An iterator cannot return a task, and a `yield` in the body means it is one.
        foreach (var node in block.DescendantNodes(static child => child is not AnonymousFunctionExpressionSyntax
                         and not LocalFunctionStatementSyntax
                 )) {
            if (node is YieldStatementSyntax) {
                return false;
            }
        }

        // ⚠ CS4013: an async method may not hold a byref-like local or parameter across an await.
        // Adding `async` to either shape produces a fix that parses and does not compile, which is
        // the one failure a fixing tool may not have.
        foreach (var parameter in parameters.Parameters) {
            if (context.SemanticModel.GetDeclaredSymbol(parameter, context.CancellationToken) is {
                    Type.IsRefLikeType: true
                }) {
                return false;
            }
        }

        foreach (var declarator in block.DescendantNodes(static child => child is not AnonymousFunctionExpressionSyntax
                         and not LocalFunctionStatementSyntax
                 )
                     .OfType<VariableDeclaratorSyntax>()) {
            if (context.SemanticModel.GetDeclaredSymbol(declarator, context.CancellationToken) is ILocalSymbol {
                    Type.IsRefLikeType: true
                }) {
                return false;
            }
        }

        var declared = context.SemanticModel.GetDeclaredSymbol(function, context.CancellationToken) as IMethodSymbol;
        if (declared?.ReturnType is not INamedTypeSymbol named
            || !tasks.Contains(named.OriginalDefinition)) {
            return false;
        }

        returnType = named;
        body = block;
        return true;
    }

    /// <summary>Every <c>return</c> belonging to this body, skipping nested functions.</summary>
    static IEnumerable<ReturnStatementSyntax> Returns(BlockSyntax body) =>
        body.DescendantNodes(static child => child is not AnonymousFunctionExpressionSyntax
                and not LocalFunctionStatementSyntax
        )
            .OfType<ReturnStatementSyntax>();

    /// <summary>
    ///     The name of a <c>using</c> resource the returned expression mentions, or null.
    /// </summary>
    static string? DisposedResourceNamedIn(ReturnStatementSyntax statement, BlockSyntax? body) {
        if (body is null) {
            return null;
        }

        var mentioned = new HashSet<string>(StringComparer.Ordinal);
        foreach (var identifier in statement.Expression!.DescendantNodesAndSelf().OfType<IdentifierNameSyntax>()) {
            mentioned.Add(identifier.Identifier.ValueText);
        }

        for (var current = statement.Parent; current is not null; current = current.Parent) {
            switch (current) {
                case UsingStatementSyntax { Declaration: { } declaration }:
                    foreach (var variable in declaration.Variables) {
                        if (mentioned.Contains(variable.Identifier.ValueText)) {
                            return variable.Identifier.ValueText;
                        }
                    }

                    break;

                case BlockSyntax block:
                    foreach (var child in block.Statements) {
                        if (ReferenceEquals(child, statement)) {
                            break;
                        }

                        if (child is LocalDeclarationStatementSyntax { UsingKeyword.RawKind: not (int)SyntaxKind.None }
                            local) {
                            foreach (var variable in local.Declaration.Variables) {
                                if (mentioned.Contains(variable.Identifier.ValueText)) {
                                    return variable.Identifier.ValueText;
                                }
                            }
                        }
                    }

                    break;
            }

            if (ReferenceEquals(current, body)) {
                break;
            }
        }

        return null;
    }

    /// <summary>
    ///     Whether falling off the end of the body would reach the same place this <c>return</c> does.
    /// </summary>
    /// <remarks>
    ///     ⚠ Syntactic and conservative: the statement is the last of its block, and so is every block
    ///     between it and the body. Anything else and dropping the `return` keyword would run code the
    ///     original never ran.
    /// </remarks>
    static bool IsInTailPosition(StatementSyntax statement, BlockSyntax body) {
        SyntaxNode current = statement;
        while (!ReferenceEquals(current, body)) {
            var parent = current.Parent;
            switch (parent) {
                case BlockSyntax block when block.Statements.Count > 0
                    && ReferenceEquals(block.Statements[block.Statements.Count - 1], current):
                    current = block;
                    continue;
                case UsingStatementSyntax use when ReferenceEquals(use.Statement, current):
                    current = use;
                    continue;
                default:
                    return false;
            }
        }

        return true;
    }
}
