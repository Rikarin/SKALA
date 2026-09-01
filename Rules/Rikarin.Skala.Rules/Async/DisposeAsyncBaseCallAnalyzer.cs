using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Rikarin.Skala.Rules.Metadata;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;

namespace Rikarin.Skala.Rules.Async;

/// <summary>
///     <c>SK3531</c> — a <c>DisposeAsync</c> override that never reaches the implementation it
///     replaced.
/// </summary>
/// <remarks>
///     docs/plan/08-rule-catalogue.md § "SK3000 — Async, concurrency, lifetime". The asynchronous
///     disposal pattern is a chain — <c>DisposeAsync</c> calls a virtual <c>DisposeAsyncCore</c>, and
///     each override finishes by calling the one it overrode. An override that does not call the base
///     breaks the chain at that link: the base type's flush, its graceful close, its return to a pool
///     stop happening, and nothing says so. There is no compiler diagnostic, no test that fails, and
///     the type still satisfies <c>IAsyncDisposable</c>. <c>CA2215</c> pins exactly this for the
///     synchronous <c>Dispose</c> and has no asynchronous counterpart.
///     <para>
///         ⚠ <b>Every finding is provable, and that is what the guards are for.</b> The base method
///         must be declared in this compilation and its body must actually call something; a base in
///         metadata cannot be read, and a base whose body is <c>ValueTask.CompletedTask</c> loses
///         nothing when it is skipped. Both withdraw. The cost is a real gap — a base type in another
///         assembly is not covered — and the alternative is a rule whose findings are a guess about
///         code it cannot see.
///     </para>
///     <para>
///         ⚠ <b>No fix, deliberately.</b> The call goes last in an override and first in a wrapper, it
///         has to be awaited in some bodies and returned in others, and where it belongs among the
///         override's own cleanup is a decision about ordering that no edit can read off the text.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class DisposeAsyncBaseCallAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.DisposeAsyncWithoutBaseCall);

    static readonly string[] TaskTypes = ["System.Threading.Tasks.Task", "System.Threading.Tasks.ValueTask"];

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(static start => {
                var asyncDisposable = start.Compilation.GetTypeByMetadataName("System.IAsyncDisposable");
                if (asyncDisposable is null) {
                    return;
                }

                var tasks = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
                foreach (var name in TaskTypes) {
                    if (start.Compilation.GetTypeByMetadataName(name) is { } type) {
                        tasks.Add(type);
                    }
                }

                if (tasks.Count == 0) {
                    return;
                }

                start.RegisterSyntaxNodeAction(
                    context => Analyze(context, asyncDisposable, tasks),
                    SyntaxKind.MethodDeclaration
                );
            }
        );
    }

    static void Analyze(
        SyntaxNodeAnalysisContext context,
        INamedTypeSymbol asyncDisposable,
        HashSet<INamedTypeSymbol> tasks
    ) {
        var declaration = (MethodDeclarationSyntax)context.Node;

        // ⚠ Syntax before symbols. Almost no method in a codebase is an override with one of these
        // two names, and answering that costs no semantic model at all.
        if (declaration.Identifier.ValueText is not ("DisposeAsync" or "DisposeAsyncCore")
            || !declaration.Modifiers.Any(static modifier => modifier.IsKind(SyntaxKind.OverrideKeyword))) {
            return;
        }

        var body = (SyntaxNode?)declaration.Body ?? declaration.ExpressionBody?.Expression;
        if (body is null || Throws(body)) {
            return;
        }

        if (context.SemanticModel.GetDeclaredSymbol(declaration, context.CancellationToken)
            is not IMethodSymbol method
            || method.OverriddenMethod is not { IsAbstract: false } overridden
            || method.ReturnType is not INamedTypeSymbol { IsGenericType: false } returned
            || !tasks.Contains(returned)
            || !UsingResource.Implements(method.ContainingType, asyncDisposable)) {
            return;
        }

        // ⚠ The base has to be readable and has to do something. Both halves are what makes the
        // finding provable rather than probable: a base in another assembly is a body this rule
        // never sees, and a base whose body is `ValueTask.CompletedTask` loses nothing by being
        // skipped. Neither is reported, and the miss is stated rather than hidden.
        if (!DoesWork(overridden, context.CancellationToken)) {
            return;
        }

        // ⚠ Any `base.` call withdraws it, not only a call to this exact method. An override that
        // reaches the base type through a different member is participating in the chain in a way
        // this rule has no business second-guessing.
        foreach (var invocation in body.DescendantNodesAndSelf().OfType<InvocationExpressionSyntax>()) {
            if (invocation.Expression is MemberAccessExpressionSyntax { Expression: BaseExpressionSyntax }) {
                return;
            }
        }

        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                declaration.Identifier.GetLocation(),
                "`"
                + overridden.ContainingType.Name
                + "."
                + overridden.Name
                + "` is never called, so its asynchronous cleanup never runs"
            )
        );
    }

    /// <summary>Whether the overridden method is in this compilation and its body calls something.</summary>
    /// <remarks>
    ///     ⚠ "Calls something" is a proxy for "disposes something", and it is the conservative one: a
    ///     body that releases a resource invokes <c>DisposeAsync</c>, <c>Dispose</c>, <c>FlushAsync</c>
    ///     or a helper, while the no-op bodies the pattern is full of — <c>ValueTask.CompletedTask</c>,
    ///     <c>default</c>, an empty block — contain no invocation at all.
    /// </remarks>
    static bool DoesWork(IMethodSymbol overridden, CancellationToken cancellation) {
        foreach (var reference in overridden.DeclaringSyntaxReferences) {
            if (reference.GetSyntax(cancellation) is not MethodDeclarationSyntax declaration) {
                continue;
            }

            var body = (SyntaxNode?)declaration.Body ?? declaration.ExpressionBody?.Expression;
            if (body is not null && body.DescendantNodesAndSelf().OfType<InvocationExpressionSyntax>().Any()) {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    ///     ⚠ An override whose whole body is a <c>throw</c> is refusing the operation, not forgetting
    ///     the base.
    /// </summary>
    /// <remarks>
    ///     ⚠ Written out rather than as a list pattern: this assembly targets <c>netstandard2.0</c>,
    ///     where <c>System.Index</c> does not exist and <c>[ThrowStatementSyntax]</c> is CS0518.
    /// </remarks>
    static bool Throws(SyntaxNode body) =>
        body is ThrowExpressionSyntax
        || body is BlockSyntax { Statements.Count: 1 } block
        && block.Statements[0] is ThrowStatementSyntax;
}
