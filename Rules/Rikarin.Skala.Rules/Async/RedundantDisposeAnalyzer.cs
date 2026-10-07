using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Rules.Metadata;
using System.Collections.Immutable;

namespace Rikarin.Skala.Rules.Async;

/// <summary>
///     <c>SK3510</c> — a variable a <c>using</c> already owns is disposed a second time by hand.
/// </summary>
/// <remarks>
///     docs/plan/08-rule-catalogue.md § "SK3000 — Async, concurrency, lifetime". The <c>using</c>
///     disposes at the end of the scope whatever else happens, so the explicit call is always
///     redundant. It is only <em>harmless</em> for a type whose <c>Dispose</c> is idempotent, which the
///     framework asks for and not every type delivers — a second call that closes a handle number the
///     process has since reissued is the failure this shape produces, and it is not reproducible.
///     <para>
///         ⚠ The reason this fix can be safe is a language guarantee rather than an analysis: a
///         <c>using</c> variable is read-only, so nothing between the declaration and the explicit
///         <c>Dispose</c> can have made the two calls land on different objects.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class RedundantDisposeAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.UsingVariableDisposedAgain);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        var registrar = PartialConstructorDefinitions.Visiting(context);
        registrar.RegisterCompilationStartAction(static start => {
                var disposable = start.Compilation.GetTypeByMetadataName("System.IDisposable");
                var asyncDisposable = start.Compilation.GetTypeByMetadataName("System.IAsyncDisposable");
                if (disposable is null && asyncDisposable is null) {
                    return;
                }

                start.RegisterSyntaxNodeAction(
                    context => Analyze(context, disposable, asyncDisposable),
                    SyntaxKind.InvocationExpression
                );
            }
        );
    }

    static void Analyze(
        SyntaxNodeAnalysisContext context,
        INamedTypeSymbol? disposable,
        INamedTypeSymbol? asyncDisposable
    ) {
        var invocation = (InvocationExpressionSyntax)context.Node;

        // ⚠ Syntax first, and it is nearly the whole cost of the rule: `x.Dispose()` with no
        // arguments, as an entire statement, is a shape almost no invocation has, and answering it
        // needs no symbols.
        if (invocation.ArgumentList.Arguments.Count != 0
            || invocation.Expression is not MemberAccessExpressionSyntax {
                RawKind: (int)SyntaxKind.SimpleMemberAccessExpression, Expression: IdentifierNameSyntax receiver
            } access) {
            return;
        }

        var asynchronous = access.Name.Identifier.ValueText switch {
            "Dispose" => false,
            "DisposeAsync" => true,
            _ => (bool?)null
        };

        if (asynchronous is null) {
            return;
        }

        // ⚠ The deletable unit is the whole statement, so the call has to *be* the statement. A
        // `Dispose()` whose value is read, or one behind an `&&`, has nothing the fix can remove.
        var statement = asynchronous.Value
            ? invocation.Parent is AwaitExpressionSyntax await ? await.Parent as ExpressionStatementSyntax : null
            : invocation.Parent as ExpressionStatementSyntax;

        // ⚠ Only out of a block. `if (failed) reader.Dispose();` has no braces to keep the `if`
        // legal once the statement is gone, and a fix that produces text which does not parse is
        // the one failure a fixing tool may not have.
        if (statement is not { Parent: BlockSyntax }) {
            return;
        }

        if (context.SemanticModel.GetSymbolInfo(receiver, context.CancellationToken).Symbol is not ILocalSymbol local) {
            return;
        }

        var owner = UsingResource.OwnerOf(local, context.CancellationToken);
        if (owner is null || UsingResource.CrossesAFunctionBoundary(statement, owner)) {
            return;
        }

        // ⚠ The name is not enough. `Dispose` is an ordinary identifier and a type may declare one
        // that does something else entirely; the call has to be the disposal contract the `using`
        // will invoke, resolved by the model.
        if (context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol
            is not IMethodSymbol { Parameters.IsEmpty: true } method) {
            return;
        }

        if (asynchronous.Value
                ? !UsingResource.Implements(local.Type, asyncDisposable)
                : !method.ReturnsVoid || !UsingResource.Implements(local.Type, disposable)) {
            return;
        }

        // ⚠ #425: three ways the deleted call was not the one the `using` makes, each measured by
        // #412's audit. A plain `using` calls `Dispose`, so an awaited `DisposeAsync` on it is the only
        // asynchronous disposal there is. A public `Dispose` beside an explicit `IDisposable.Dispose` is
        // a different method from the one the `using` calls. And a call that is not the last thing the
        // scope does disposes earlier than the `using` would: `w.Dispose()` flushed a writer that the
        // next statement read back ("hello" → "").
        if (asynchronous.Value != IsAwaitUsing(owner)
            || !IsTheContract(local.Type, asynchronous.Value ? asyncDisposable : disposable, method)
            || !IsTheLastThingTheScopeDoes(context, statement, owner, local)) {
            return;
        }

        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                invocation.GetLocation(),
                FixEdits.Pack((Deletion(statement), string.Empty)),
                "`" + local.Name + "` is already disposed by its `using`, so this call is redundant"
            )
        );
    }

    static bool IsAwaitUsing(StatementSyntax owner) =>
        owner switch {
            UsingStatementSyntax use => !use.AwaitKeyword.IsKind(SyntaxKind.None),
            LocalDeclarationStatementSyntax declaration => !declaration.AwaitKeyword.IsKind(SyntaxKind.None),
            _ => false
        };

    /// <summary>Whether the called method is the interface member the <c>using</c> itself calls.</summary>
    static bool IsTheContract(ITypeSymbol type, INamedTypeSymbol? contract, IMethodSymbol method) {
        if (contract is null) {
            return false;
        }

        foreach (var member in contract.GetMembers(method.Name)) {
            if (member is not IMethodSymbol { Parameters.IsEmpty: true } required) {
                continue;
            }

            return SymbolEqualityComparer.Default.Equals(method, required)
                || SymbolEqualityComparer.Default.Equals(type.FindImplementationForInterfaceMember(required), method);
        }

        return false;
    }

    /// <summary>
    ///     ⚠ Whether nothing runs between the call and the <c>using</c>'s own disposal: the statement is
    ///     the last of its block, every block out to the scope is the last of its own, and no other
    ///     <c>using</c> declared later in the scope is disposed in between.
    /// </summary>
    /// <remarks>
    ///     A <c>return</c> of a literal, or of a local or parameter other than the resource, may follow:
    ///     reading one runs nothing the disposal can have changed, and the <c>using</c> disposes as the
    ///     <c>return</c> leaves.
    /// </remarks>
    static bool IsTheLastThingTheScopeDoes(
        SyntaxNodeAnalysisContext context,
        StatementSyntax statement,
        StatementSyntax owner,
        ILocalSymbol resource
    ) {
        var scope = owner is UsingStatementSyntax use ? use.Statement : owner.Parent;
        if (scope is not BlockSyntax) {
            return false;
        }

        SyntaxNode current = statement;
        while (!ReferenceEquals(current, scope)) {
            if (current.Parent is not BlockSyntax block) {
                return false;
            }

            var index = block.Statements.IndexOf((StatementSyntax)current);
            var rest = block.Statements.Count - index - 1;
            if (rest > 1
                || (rest == 1
                    && (!ReferenceEquals(block, scope)
                        || !IsAnInertReturn(context, block.Statements[index + 1], resource)))) {
                return false;
            }

            current = block;
        }

        foreach (var node in scope.DescendantNodes()) {
            if (node is LocalDeclarationStatementSyntax { UsingKeyword.RawKind: not (int)SyntaxKind.None } later
                && !ReferenceEquals(later, owner)
                && later.SpanStart > owner.SpanStart) {
                return false;
            }
        }

        return true;
    }

    static bool IsAnInertReturn(SyntaxNodeAnalysisContext context, StatementSyntax next, ILocalSymbol resource) {
        if (next is not ReturnStatementSyntax returned) {
            return false;
        }

        switch (returned.Expression) {
            case null:
            case LiteralExpressionSyntax:
                return true;

            case IdentifierNameSyntax name:
                var symbol = context.SemanticModel.GetSymbolInfo(name, context.CancellationToken).Symbol;
                return symbol is ILocalSymbol { RefKind: RefKind.None } or IParameterSymbol { RefKind: RefKind.None }
                    && !SymbolEqualityComparer.Default.Equals(symbol, resource);

            default:
                return false;
        }
    }

    /// <summary>
    ///     The statement, its own indentation, and the newline that ended it.
    /// </summary>
    /// <remarks>
    ///     ⚠ Not <c>FullSpan</c>: leading trivia belongs to the statement and carries the comment
    ///     written above it, which is about the code that stays. Trailing trivia is consumed only as
    ///     far as the first end-of-line, so <c>stream.Dispose(); // belt and braces</c> keeps the
    ///     comment rather than deleting a line the author wrote.
    /// </remarks>
    static TextSpan Deletion(ExpressionStatementSyntax statement) {
        var start = statement.SpanStart - UsingResource.IndentOf(statement).Length;
        var end = statement.Span.End;
        foreach (var trivia in statement.GetTrailingTrivia()) {
            if (trivia.IsKind(SyntaxKind.WhitespaceTrivia)) {
                end = trivia.Span.End;
                continue;
            }

            if (trivia.IsKind(SyntaxKind.EndOfLineTrivia)) {
                end = trivia.Span.End;
            }

            break;
        }

        return TextSpan.FromBounds(start, end);
    }
}
