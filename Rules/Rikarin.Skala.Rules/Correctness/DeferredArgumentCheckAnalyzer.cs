using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Rikarin.Skala.Rules.Metadata;
using System.Collections.Immutable;
using System.Threading;

namespace Rikarin.Skala.Rules.Correctness;

/// <summary>
///     <c>SK2242</c> — an iterator's argument check does not run until something enumerates it.
/// </summary>
/// <remarks>
///     <para>
///         A method containing <c>yield</c> does not run when it is called. The call returns an
///         enumerator and the body starts at the first <c>MoveNext</c>, so the guard written precisely
///         to reject a bad argument <em>early</em> is the one part of the method guaranteed not to be
///         early. The exception surfaces inside somebody else's <c>foreach</c>, with a stack trace
///         naming the consumer rather than the caller that broke the contract — and a caller that never
///         enumerates never sees it at all.
///     </para>
///     <para>
///         ⚠ <b><c>yield</c> anywhere in the body makes the whole method lazy</b>, including everything
///         above the <c>yield</c>. That is what makes this decidable rather than a heuristic: there is
///         no execution in which the guard runs at call time.
///     </para>
///     <para>
///         ⚠ <b>The <c>async</c> half of the concept is measured out, and upstream agrees.</b> An
///         <c>async</c> method's exception lands on the returned task, but the overwhelming majority of
///         call sites <c>await</c> in the same statement, where it surfaces exactly where it would have
///         anyway. SonarQube ships the iterator rule <c>S4456</c> in its default profile and excludes
///         the <c>async</c> rule <c>S4457</c> from it.
///     </para>
///     <para>
///         ⚠ <b>Disjoint from <c>SK3030</c>, which is about a call site.</b> <c>SK3030</c> reports an
///         async iterator invoked as a statement and never enumerated; this reports a declaration. The
///         two register on different node kinds and cannot see the same code.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class DeferredArgumentCheckAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.DeferredArgumentCheck);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(static start => {
                var argument = start.Compilation.GetTypeByMetadataName("System.ArgumentException");
                if (argument is null) {
                    return;
                }

                start.RegisterSyntaxNodeAction(
                    nodeContext => Analyze(nodeContext, argument),
                    SyntaxKind.MethodDeclaration
                );
            }
        );
    }

    static void Analyze(SyntaxNodeAnalysisContext context, INamedTypeSymbol argumentException) {
        var declaration = (MethodDeclarationSyntax)context.Node;

        // An expression-bodied method cannot contain `yield`, so only a block body can be an iterator.
        if (declaration.Body is not { } body) {
            return;
        }

        var cancellation = context.CancellationToken;
        var model = context.SemanticModel;

        // ⚠ The first `yield` of *this* method. A `yield` inside a nested local function belongs to
        // that function's iterator, not to this one, and reading it as this method's would report a
        // method that is not an iterator at all.
        var first = int.MaxValue;
        foreach (var node in Own(body)) {
            cancellation.ThrowIfCancellationRequested();
            if (node is YieldStatementSyntax && node.SpanStart < first) {
                first = node.SpanStart;
            }
        }

        if (first == int.MaxValue) {
            return;
        }

        foreach (var node in Own(body)) {
            cancellation.ThrowIfCancellationRequested();

            // ⚠ Before the first `yield` in source order is what makes it a guard. A throw *after* a
            // `yield` is already running inside the enumeration, which is where the author put it.
            if (node.SpanStart >= first || !IsArgumentCheck(model, node, argumentException, cancellation)) {
                continue;
            }

            // ⚠ A throw inside a handler is error translation, not an entry check, and hoisting it
            // out of the `try` it sits in would change which exception the caller sees.
            if (InsideHandler(node, body)) {
                continue;
            }

            context.ReportDiagnostic(
                Diagnostic.Create(
                    Descriptor,
                    node.GetLocation(),
                    "`"
                    + declaration.Identifier.ValueText
                    + "` is an iterator, so this check does not run when it is called — it runs when "
                    + "something enumerates the result"
                )
            );
            return;
        }
    }

    /// <summary>
    ///     The nodes of the method's own body, not descending into a nested lambda or local function.
    /// </summary>
    /// <remarks>
    ///     ⚠ Both are separate bodies with their own execution. A guard inside one is that body's guard,
    ///     and a <c>yield</c> inside a nested local function makes <em>it</em> the iterator.
    /// </remarks>
    static System.Collections.Generic.IEnumerable<SyntaxNode> Own(BlockSyntax body) =>
        body.DescendantNodes(
            node => ReferenceEquals(node, body)
                || node is not (AnonymousFunctionExpressionSyntax or LocalFunctionStatementSyntax)
        );

    /// <summary>
    ///     Whether the node throws, or calls a helper that throws, an <see cref="System.ArgumentException" />.
    /// </summary>
    /// <remarks>
    ///     ⚠ Only a check the <em>caller</em> violated. A method that throws an
    ///     <c>InvalidOperationException</c> before its first <c>yield</c> is describing its own state
    ///     rather than its caller's mistake, and moving that earlier is not obviously right.
    ///     <para>
    ///         ⚠ The throw-helper test is one question rather than a list of names:
    ///         <c>ArgumentNullException.ThrowIfNull</c>, <c>ArgumentException.ThrowIfNullOrEmpty</c> and
    ///         <c>ArgumentOutOfRangeException.ThrowIfNegative</c> are all static <c>ThrowIf…</c> members of
    ///         a type that <em>is</em> an <c>ArgumentException</c>, which is exactly the shape asked for —
    ///         so a helper added to the framework tomorrow is covered without an edit here.
    ///     </para>
    /// </remarks>
    static bool IsArgumentCheck(
        SemanticModel model,
        SyntaxNode node,
        INamedTypeSymbol argumentException,
        CancellationToken cancellation
    ) {
        switch (node) {
            case ThrowStatementSyntax { Expression: { } thrown }:
                return Derives(model.GetTypeInfo(thrown, cancellation).Type, argumentException);

            case ThrowExpressionSyntax { Expression: { } thrown }:
                return Derives(model.GetTypeInfo(thrown, cancellation).Type, argumentException);

            case InvocationExpressionSyntax invocation:
                return model.GetSymbolInfo(invocation, cancellation).Symbol is IMethodSymbol { IsStatic: true } helper
                    && helper.Name.StartsWith("ThrowIf", System.StringComparison.Ordinal)
                    && Derives(helper.ContainingType, argumentException);

            default:
                return false;
        }
    }

    static bool Derives(ITypeSymbol? type, INamedTypeSymbol argumentException) {
        for (var current = type; current is not null; current = current.BaseType) {
            if (SymbolEqualityComparer.Default.Equals(current, argumentException)) {
                return true;
            }
        }

        return false;
    }

    /// <summary>Whether the node sits inside a <c>try</c>, <c>catch</c> or <c>finally</c> of this body.</summary>
    static bool InsideHandler(SyntaxNode node, BlockSyntax body) {
        for (var current = node.Parent; current is not null && !ReferenceEquals(current, body); current = current.Parent) {
            if (current is TryStatementSyntax) {
                return true;
            }
        }

        return false;
    }
}
