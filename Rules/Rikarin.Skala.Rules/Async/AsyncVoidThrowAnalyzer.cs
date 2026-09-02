using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Rikarin.Skala.Rules.Metadata;
using System.Collections.Immutable;

namespace Rikarin.Skala.Rules.Async;

/// <summary>
///     <c>SK3050</c> — a <c>throw</c> whose enclosing method or local function is <c>async void</c>.
/// </summary>
/// <remarks>
///     docs/plan/08-rule-catalogue.md § "SK3000". An <c>async void</c> method has nowhere to put an
///     exception. There is no task to fault, so the compiler-generated builder hands it to whatever
///     <c>SynchronizationContext</c> was captured — and where there is none, which is every console
///     application and every thread-pool callback, that is an unhandled exception on a pool thread and
///     the process ends. ⚠ <b>The caller's <c>try</c>/<c>catch</c> does not see it</b>, and neither
///     does an <c>await</c>, because there is nothing to await.
///     <para>
///         ⚠ <b>Event handlers are reported, and that is the decision.</b> <c>SK3001</c> excludes them
///         because its remedy — return <c>Task</c> — is not available to a method whose signature an
///         event declares. This rule's remedy is different and is available to every <c>async void</c>
///         there is: handle the exception where it is thrown. Excluding handlers would exclude the one
///         place <c>async void</c> is legitimate and therefore the place this defect actually reaches
///         production.
///     </para>
///     <para>
///         ⚠ <b>Lambdas are not matched at all, and that is what makes this disjoint from
///         <c>SK3052</c>.</b> An <c>async</c> lambda converted to a <c>void</c>-returning delegate is
///         <c>async void</c> too, and it is reported once, at the conversion, by <c>SK3052</c> — which
///         is where the remedy is. Reporting the throw inside it as well would be two findings about
///         one mistake with only one of them actionable, so the owner has to be a declaration that
///         writes <c>void</c> in its own source.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class AsyncVoidThrowAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.AsyncVoidThrow);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.ThrowStatement, SyntaxKind.ThrowExpression);
    }

    static void Analyze(SyntaxNodeAnalysisContext context) {
        var node = context.Node;

        // ⚠ A `throw` in a synchronous lambda or local function nested inside an `async void` method
        // belongs to that nested body, not to the method — which is why the owner is taken from
        // `AsyncContext` rather than by walking to the first `MethodDeclarationSyntax`.
        var owner = AsyncContext.NearestAsyncOwner(node);
        if (!IsAsyncVoidDeclaration(owner) || AsyncContext.IsTestMethod(node)) {
            return;
        }

        // ⚠ An exception the same body catches never leaves it, so it never reaches the
        // synchronization context and there is nothing to report. The test is deliberately
        // over-broad: any `catch` at all on an enclosing `try` silences the finding, even one whose
        // filter could not possibly match. Being silent where the exception does escape is the
        // direction doc 00's false-positive bar asks this rule to err in.
        if (MayBeCaughtLocally(node, owner!)) {
            return;
        }

        var keyword = node is ThrowStatementSyntax statement
            ? statement.ThrowKeyword
            : ((ThrowExpressionSyntax)node).ThrowKeyword;

        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                keyword.GetLocation(),
                "this exception leaves an `async void` body, so no caller can catch it"
            )
        );
    }

    /// <summary>
    ///     Whether the owner is a declaration that writes <c>async</c> and <c>void</c> in its source.
    /// </summary>
    /// <remarks>
    ///     ⚠ Methods and local functions only. See the type's remarks for why a lambda is excluded, and
    ///     <c>SK3052</c> for what reports it instead.
    /// </remarks>
    static bool IsAsyncVoidDeclaration(SyntaxNode? owner) {
        var (modifiers, returnType) = owner switch {
            MethodDeclarationSyntax method => (method.Modifiers, method.ReturnType),
            LocalFunctionStatementSyntax local => (local.Modifiers, local.ReturnType),
            _ => (default, null)
        };

        if (returnType is not PredefinedTypeSyntax { Keyword.RawKind: (int)SyntaxKind.VoidKeyword }) {
            return false;
        }

        foreach (var modifier in modifiers) {
            if (modifier.IsKind(SyntaxKind.AsyncKeyword)) {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    ///     Whether a <c>try</c> between the throw and the body's edge could catch it.
    /// </summary>
    /// <remarks>
    ///     ⚠ Only a throw sitting in the <c>try</c> block itself is a candidate for that <c>try</c>'s
    ///     own handlers. A <c>throw</c> written inside a <c>catch</c> — the rethrow this rule exists
    ///     for — is not caught by the clause it is written in, nor by any sibling clause of the same
    ///     <c>try</c>, so the walk continues outwards past it.
    /// </remarks>
    static bool MayBeCaughtLocally(SyntaxNode node, SyntaxNode owner) {
        for (var current = node; current is not null && !ReferenceEquals(current, owner); current = current.Parent) {
            if (current.Parent is TryStatementSyntax @try
                && ReferenceEquals(@try.Block, current)
                && @try.Catches.Count > 0) {
                return true;
            }
        }

        return false;
    }
}
