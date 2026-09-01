using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Rules.Metadata;
using System.Collections.Immutable;

namespace Rikarin.Skala.Rules.Async;

/// <summary>
///     <c>SK3030</c> — an async iterator is called as a statement, so its body never runs.
/// </summary>
/// <remarks>
///     docs/plan/08-rule-catalogue.md § "SK3000 — Async, concurrency, lifetime". An
///     <c>IAsyncEnumerable&lt;T&gt;</c> consumed by a plain <c>foreach</c> does not compile, which is
///     why the mistake takes this shape instead: the iterator is <em>invoked</em> and the result
///     dropped. That compiles, produces no warning, and does nothing at all — the method's body has
///     not started, because an async iterator does not begin until something enumerates it. The call
///     reads as work being done and the work is never done.
///     <para>
///         ⚠ <b>Only where the repair compiles.</b> The nearest enclosing body must already be
///         <c>async</c> and the statement must sit in a block, in a position an <c>await</c> is legal
///         in. In a synchronous method the same call is the same bug and is not reported, because the
///         repair there makes the method <c>async</c> and changes every caller — the same line
///         <c>SK3503</c> draws, drawn in the same place.
///     </para>
///     <para>
///         ⚠ <b>The finding is withheld when <c>_</c> is already in scope.</b> The rewrite has to name
///         the loop variable, and a name that shadows an existing one is CS0136 — a fix that parses and
///         does not compile, which is the one failure a fixing tool may not have.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class AsyncIteratorNotEnumeratedAnalyzer : DiagnosticAnalyzer {
    static readonly RuleInfo Rule = RuleCatalog.Get(RuleIds.AsyncIteratorNotEnumerated);
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.AsyncIteratorNotEnumerated);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(static start => {
                if (!SkalaRule.MeetsLanguageVersion(start.Compilation, Rule.LanguageVersion)) {
                    return;
                }

                var enumerable = start.Compilation.GetTypeByMetadataName(
                    "System.Collections.Generic.IAsyncEnumerable`1"
                );
                if (enumerable is null) {
                    return;
                }

                start.RegisterSyntaxNodeAction(
                    context => Analyze(context, enumerable),
                    SyntaxKind.ExpressionStatement
                );
            }
        );
    }

    static void Analyze(SyntaxNodeAnalysisContext context, INamedTypeSymbol enumerable) {
        var statement = (ExpressionStatementSyntax)context.Node;

        // ⚠ Syntax first. An invocation standing alone in a block is a cheap question, and it is the
        // only shape the rewrite has anywhere to put a loop body.
        if (statement.Expression is not InvocationExpressionSyntax invocation
            || statement.Parent is not BlockSyntax
            || !AsyncContext.IsInsideAsyncBody(statement)
            || AsyncContext.IsUnawaitablePosition(statement)) {
            return;
        }

        if (context.SemanticModel.GetTypeInfo(invocation, context.CancellationToken).Type
            is not INamedTypeSymbol { IsGenericType: true } type
            || !SymbolEqualityComparer.Default.Equals(type.OriginalDefinition, enumerable)) {
            return;
        }

        // ⚠ CS0136: a loop variable named `_` may not shadow a local of the same name. The finding
        // goes with the fix rather than being reported without one.
        if (!context.SemanticModel.LookupSymbols(statement.SpanStart, name: "_").IsEmpty) {
            return;
        }

        var indent = UsingResource.IndentOf(statement);
        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                statement.GetLocation(),
                FixEdits.Pack(
                    (new TextSpan(statement.SpanStart, 0), "await foreach (var _ in "),
                    (statement.SemicolonToken.Span, ") {\n" + indent + "}")
                ),
                "this returns an `IAsyncEnumerable`; nothing enumerates it, so the iterator never runs"
            )
        );
    }
}
