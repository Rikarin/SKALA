using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Rules.Metadata;
using System.Collections.Immutable;

namespace Rikarin.Skala.Rules.Async;

/// <summary>
///     <c>SK3503</c> — a plain <c>using</c>, or a bare <c>Dispose()</c>, on a type that offers
///     <c>DisposeAsync</c>.
/// </summary>
/// <remarks>
///     docs/plan/08-rule-catalogue.md § "SK3000 — Async, concurrency, lifetime". A type implements
///     <c>IAsyncDisposable</c> because its cleanup has to be waited for; its synchronous
///     <c>Dispose</c> therefore either blocks the thread on that work or skips it, and which one is a
///     decision the type made rather than the caller.
///     <para>
///         ⚠ The rule reports only where <c>await using</c> — or <c>await x.DisposeAsync()</c> — would
///         actually compile: the nearest enclosing body is already <c>async</c> and the position is one an
///         <c>await</c> is legal in. A <c>using</c> in a synchronous method is the same pattern and is not
///         reported, because the repair there is to make the method <c>async</c>, which changes its
///         signature and every caller with it.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class SynchronousAsyncDisposalAnalyzer : DiagnosticAnalyzer {
    static readonly RuleInfo Rule = RuleCatalog.Get(RuleIds.AsyncDisposableDisposedSynchronously);

    static readonly DiagnosticDescriptor Descriptor =
        SkalaRule.Descriptor(RuleIds.AsyncDisposableDisposedSynchronously);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(static start => {
                if (!SkalaRule.MeetsLanguageVersion(start.Compilation, Rule.LanguageVersion)) {
                    return;
                }

                var asyncDisposable = start.Compilation.GetTypeByMetadataName("System.IAsyncDisposable");
                if (asyncDisposable is null) {
                    return;
                }

                start.RegisterSyntaxNodeAction(
                    context => Analyze(context, asyncDisposable),
                    SyntaxKind.UsingStatement,
                    SyntaxKind.LocalDeclarationStatement,
                    SyntaxKind.InvocationExpression
                );
            }
        );
    }

    static void Analyze(SyntaxNodeAnalysisContext context, INamedTypeSymbol asyncDisposable) {
        switch (context.Node) {
            case UsingStatementSyntax statement:
                AnalyzeUsingStatement(context, statement, asyncDisposable);
                return;
            case LocalDeclarationStatementSyntax declaration:
                AnalyzeUsingDeclaration(context, declaration, asyncDisposable);
                return;
            case InvocationExpressionSyntax invocation:
                AnalyzeDisposeCall(context, invocation, asyncDisposable);
                return;
        }
    }

    static void AnalyzeUsingStatement(
        SyntaxNodeAnalysisContext context,
        UsingStatementSyntax statement,
        INamedTypeSymbol asyncDisposable
    ) {
        if (statement.AwaitKeyword.RawKind != (int)SyntaxKind.None || !CanAwaitHere(statement)) {
            return;
        }

        var type = statement.Declaration is { Variables.Count: > 0 } declaration
            ? TypeOfFirst(context, declaration)
            : statement.Expression is { } expression
                ? context.SemanticModel.GetTypeInfo(expression, context.CancellationToken).Type
                : null;

        if (!Implements(type, asyncDisposable)) {
            return;
        }

        Report(context, statement.UsingKeyword.GetLocation(), type!, PrependAwait(statement.UsingKeyword.SpanStart));
    }

    static void AnalyzeUsingDeclaration(
        SyntaxNodeAnalysisContext context,
        LocalDeclarationStatementSyntax declaration,
        INamedTypeSymbol asyncDisposable
    ) {
        if (declaration.UsingKeyword.RawKind == (int)SyntaxKind.None
            || declaration.AwaitKeyword.RawKind != (int)SyntaxKind.None
            || !CanAwaitHere(declaration)) {
            return;
        }

        var type = TypeOfFirst(context, declaration.Declaration);
        if (!Implements(type, asyncDisposable)) {
            return;
        }

        Report(
            context,
            declaration.UsingKeyword.GetLocation(),
            type!,
            PrependAwait(declaration.UsingKeyword.SpanStart)
        );
    }

    /// <summary>
    ///     <c>x.Dispose();</c> on a value that also offers <c>DisposeAsync</c>.
    /// </summary>
    /// <remarks>
    ///     ⚠ Only as a whole expression statement. <c>Dispose()</c> returns <c>void</c> and
    ///     <c>DisposeAsync()</c> returns a <c>ValueTask</c>, so the rewrite is a statement-level one:
    ///     anywhere the call's value is used the two are not interchangeable, and there is nothing to
    ///     rewrite into.
    /// </remarks>
    static void AnalyzeDisposeCall(
        SyntaxNodeAnalysisContext context,
        InvocationExpressionSyntax invocation,
        INamedTypeSymbol asyncDisposable
    ) {
        if (invocation.Parent is not ExpressionStatementSyntax
            || invocation.ArgumentList.Arguments.Count != 0
            || invocation.Expression is not MemberAccessExpressionSyntax { Name.Identifier.ValueText: "Dispose" } access
            || !CanAwaitHere(invocation)) {
            return;
        }

        if (context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol
            is not IMethodSymbol { ReturnsVoid: true, Parameters.IsEmpty: true }) {
            return;
        }

        var type = context.SemanticModel.GetTypeInfo(access.Expression, context.CancellationToken).Type;
        if (!Implements(type, asyncDisposable)) {
            return;
        }

        Report(
            context,
            invocation.GetLocation(),
            type!,
            FixEdits.Pack(
                (new TextSpan(invocation.SpanStart, 0), "await "),
                (access.Name.Span, "DisposeAsync")
            )
        );
    }

    static void Report(
        SyntaxNodeAnalysisContext context,
        Location location,
        ITypeSymbol type,
        ImmutableDictionary<string, string?> fix
    ) =>
        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                location,
                fix,
                "`" + type.Name + "` implements `IAsyncDisposable`; this disposes it synchronously"
            )
        );

    static ImmutableDictionary<string, string?> PrependAwait(int position) =>
        FixEdits.Pack((new TextSpan(position, 0), "await "));

    /// <summary>
    ///     ⚠ Both halves of "the fix compiles": the body is <c>async</c>, and the position allows an
    ///     <c>await</c>.
    /// </summary>
    static bool CanAwaitHere(SyntaxNode node) =>
        AsyncContext.IsInsideAsyncBody(node) && !AsyncContext.IsUnawaitablePosition(node);

    static ITypeSymbol? TypeOfFirst(SyntaxNodeAnalysisContext context, VariableDeclarationSyntax declaration) {
        if (declaration.Variables.Count == 0) {
            return null;
        }

        // ⚠ The declared local rather than the declared type, because `using (var x = Open())` says
        // `var` and the interface list lives on what `Open()` returned.
        return context.SemanticModel.GetDeclaredSymbol(declaration.Variables[0], context.CancellationToken)
            is ILocalSymbol local
                ? local.Type
                : null;
    }

    static bool Implements(ITypeSymbol? type, INamedTypeSymbol asyncDisposable) {
        if (type is null || type.TypeKind == TypeKind.Error) {
            return false;
        }

        if (SymbolEqualityComparer.Default.Equals(type, asyncDisposable)) {
            return true;
        }

        foreach (var candidate in type.AllInterfaces) {
            if (SymbolEqualityComparer.Default.Equals(candidate, asyncDisposable)) {
                return true;
            }
        }

        return false;
    }
}
