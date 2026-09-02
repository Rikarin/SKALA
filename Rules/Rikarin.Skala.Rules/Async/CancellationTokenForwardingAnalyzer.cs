using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Rules.Metadata;
using System.Collections.Immutable;

namespace Rikarin.Skala.Rules.Async;

/// <summary>
///     <c>SK3004</c> — the method took a <c>CancellationToken</c> and did not pass it on.
/// </summary>
/// <remarks>
///     docs/plan/08-rule-catalogue.md § "SK3000 — Async, concurrency, lifetime". A token that stops at
///     the first frame is a cancellation that does not happen: the caller sees the parameter accepted,
///     assumes the operation is cancellable, and gets work that runs to completion after whoever asked
///     for it has gone.
///     <para>
///         ⚠ Two shapes, and no third. Either the callee declares an <em>optional</em>
///         <c>CancellationToken</c> this call omitted — repaired with a named argument, which is right
///         whether or not the parameters in between were supplied — or an overload exists whose parameter
///         list is this one with a <c>CancellationToken</c> appended, and every argument here is positional
///         — repaired by appending. Anywhere the rule would have to choose between overloads it says
///         nothing, because a fix that changes which method is called is not a fix.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class CancellationTokenForwardingAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.CancellationTokenNotForwarded);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(static start => {
                var token = start.Compilation.GetTypeByMetadataName("System.Threading.CancellationToken");
                if (token is null) {
                    return;
                }

                start.RegisterSyntaxNodeAction(context => Analyze(context, token), SyntaxKind.InvocationExpression);
            }
        );
    }

    static void Analyze(SyntaxNodeAnalysisContext context, INamedTypeSymbol tokenType) {
        var invocation = (InvocationExpressionSyntax)context.Node;

        // ⚠ Cleanup that a cancellation can abort is worse than cleanup that ignores one, and a
        // `catch` or a `finally` is exactly where that cleanup lives.
        if (IsInsideCleanup(invocation)
            || AsyncContext.IsTestCode(invocation, context.SemanticModel, context.CancellationToken)) {
            return;
        }

        var available = CancellationTokens.TokenInScope(
            context.SemanticModel,
            invocation,
            tokenType,
            context.CancellationToken
        );
        if (available is null) {
            return;
        }

        if (context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol
            is not IMethodSymbol { MethodKind: MethodKind.Ordinary or MethodKind.ReducedExtension } target) {
            return;
        }

        var arguments = invocation.ArgumentList.Arguments;
        if (CancellationTokens.Supplies(arguments, target, tokenType)) {
            return;
        }

        var edit = CancellationTokens.Omitted(target, tokenType) is { } optional
            ? Append(invocation, arguments, optional.Name + ": " + available)
            : CancellationTokens.HasAppendedOverload(target, tokenType)
                && CancellationTokens.AllPositional(arguments, target)
                ? Append(invocation, arguments, available)
                : (Edit?)null;

        if (edit is null) {
            return;
        }

        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                invocation.GetLocation(),
                FixEdits.Pack((edit.Value.Span, edit.Value.Text)),
                "`" + target.Name + "` takes a `CancellationToken` and `" + available + "` is not passed to it"
            )
        );
    }

    readonly record struct Edit(TextSpan Span, string Text);

    static Edit? Append(
        InvocationExpressionSyntax invocation,
        SeparatedSyntaxList<ArgumentSyntax> arguments,
        string text
    ) {
        var list = invocation.ArgumentList;
        return arguments.Count == 0
            ? new Edit(new TextSpan(list.CloseParenToken.SpanStart, 0), text)
            : new Edit(new TextSpan(arguments[arguments.Count - 1].Span.End, 0), ", " + text);
    }

    static bool IsInsideCleanup(SyntaxNode node) {
        for (var current = node.Parent; current is not null; current = current.Parent) {
            switch (current) {
                case CatchClauseSyntax:
                case FinallyClauseSyntax:
                    return true;
                case BaseMethodDeclarationSyntax:
                case AccessorDeclarationSyntax:
                    return false;
            }
        }

        return false;
    }
}
