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
        context.RegisterCompilationStartAction(static start => {
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
                RawKind: (int)SyntaxKind.SimpleMemberAccessExpression,
                Expression: IdentifierNameSyntax receiver
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

        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                invocation.GetLocation(),
                FixEdits.Pack((Deletion(statement), string.Empty)),
                "`" + local.Name + "` is already disposed by its `using`, so this call is redundant"
            )
        );
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
