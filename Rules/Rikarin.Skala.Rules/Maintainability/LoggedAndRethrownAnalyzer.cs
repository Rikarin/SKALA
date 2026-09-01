using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Rikarin.Skala.Rules.Metadata;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Rikarin.Skala.Rules.Maintainability;

/// <summary>
///     <c>SK7092</c> — a <c>catch</c> that both records the exception and lets it go on.
/// </summary>
/// <remarks>
///     Every layer that does this multiplies the log. One failure arrives as five entries with five
///     different stack depths, nothing in any of them says they are the same event, and the one that
///     finally handles it is indistinguishable from the four that did not. Either handle the exception
///     — and then it is not the caller's problem — or propagate it and let whoever handles it record
///     it once.
///     <para>
///         ⚠
///         <b>The rule ships the half it can prove, and the omitted half is named.</b> A finding
///         requires the logging call to be handed <em>the caught exception itself</em>. Deciding that
///         an arbitrary call "is logging" is name-matching, and name-matching a bare
///         <c>logger.LogError("failed")</c> next to a <c>throw;</c> would report every method called
///         <c>Error</c> in the tree. Passing the caught exception to something in the logging
///         vocabulary is not a guess: nothing else does that.
///     </para>
///     <para>
///         ⚠ <b>Wrapping is not rethrowing, and is deliberately not reported.</b>
///         <c>throw new ImportException(message, ex)</c> translates the failure at a boundary and
///         produces one record, not two; logging the original before translating it is how the
///         low-level detail survives a translation that drops it. Only a bare <c>throw;</c> and
///         <c>throw ex;</c> — the two forms that propagate the same exception the same layer just
///         recorded — count.
///     </para>
///     <para>
///         Report-only. "Handle it or propagate it" names two repairs and choosing between them is a
///         decision about what this layer is for. An edit that deleted the log would lose the context
///         the log adds; one that deleted the <c>throw</c> would swallow a failure. Both are worse than
///         the finding.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class LoggedAndRethrownAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.LoggedAndRethrown);

    /// <summary>What the logging libraries in use call the act of recording something.</summary>
    /// <remarks>
    ///     ⚠ Names alone would be a guess. This set is only ever consulted for a call that was handed
    ///     the caught exception, which is what makes it evidence rather than a heuristic.
    /// </remarks>
    static readonly HashSet<string> LogNames = new(System.StringComparer.Ordinal) {
        "Log",
        "LogTrace",
        "LogDebug",
        "LogInformation",
        "LogWarning",
        "LogError",
        "LogCritical",
        "Trace",
        "Debug",
        "Verbose",
        "Info",
        "Information",
        "Warn",
        "Warning",
        "Error",
        "Fatal",
        "Critical"
    };

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(static start => {
                var sinks = ImmutableArray.CreateBuilder<INamedTypeSymbol>();
                foreach (var name in new[] {
                             "System.Console", "System.Diagnostics.Trace", "System.Diagnostics.Debug",
                             "System.IO.TextWriter"
                         }) {
                    if (start.Compilation.GetTypeByMetadataName(name) is { } type) {
                        sinks.Add(type);
                    }
                }

                var resolved = sinks.ToImmutable();
                start.RegisterSyntaxNodeAction(context => Analyze(context, resolved), SyntaxKind.CatchClause);
            }
        );
    }

    static void Analyze(SyntaxNodeAnalysisContext context, ImmutableArray<INamedTypeSymbol> sinks) {
        var clause = (CatchClauseSyntax)context.Node;

        // No exception variable means nothing can be handed to a logger, so there is nothing this
        // rule can prove about the call beside the `throw`.
        if (clause.Declaration is not { } declaration
            || declaration.Identifier.ValueText.Length == 0
            || context.SemanticModel.GetDeclaredSymbol(declaration, context.CancellationToken)
            is not { } caught) {
            return;
        }

        if (!Rethrows(context, clause, caught)) {
            return;
        }

        foreach (var invocation in Owned<InvocationExpressionSyntax>(clause)) {
            if (context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol
                is not IMethodSymbol method
                || !IsLoggingCall(method, sinks)
                || invocation.ArgumentList is null
                || !Mentions(context, invocation.ArgumentList, caught)) {
                continue;
            }

            context.ReportDiagnostic(
                Diagnostic.Create(
                    Descriptor,
                    invocation.GetLocation(),
                    "the exception is recorded here and propagated from the same `catch`, so one "
                    + "failure arrives as two entries with two stack depths and nothing saying they "
                    + "are the same event"
                )
            );

            // One finding per `catch`. Two log calls before one `throw` is one duplication, and two
            // diagnostics on it would read as two problems.
            return;
        }
    }

    /// <summary>
    ///     <c>throw;</c> or <c>throw ex;</c> — the two forms that send on the same exception.
    /// </summary>
    static bool Rethrows(SyntaxNodeAnalysisContext context, CatchClauseSyntax clause, ISymbol caught) {
        foreach (var statement in Owned<ThrowStatementSyntax>(clause)) {
            if (statement.Expression is null) {
                return true;
            }

            if (statement.Expression is IdentifierNameSyntax name
                && SymbolEqualityComparer.Default.Equals(
                    context.SemanticModel.GetSymbolInfo(name, context.CancellationToken).Symbol,
                    caught
                )) {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    ///     The nodes this <c>catch</c> owns: a nested <c>try</c>'s own <c>catch</c> is not this one.
    /// </summary>
    static IEnumerable<T> Owned<T>(CatchClauseSyntax clause) where T : SyntaxNode =>
        clause.Block
            .DescendantNodes(descendIntoTrivia: false)
            .OfType<T>()
            .Where(node => node.FirstAncestorOrSelf<CatchClauseSyntax>() == clause);

    static bool IsLoggingCall(IMethodSymbol method, ImmutableArray<INamedTypeSymbol> sinks) {
        if (LogNames.Contains(method.Name)) {
            return true;
        }

        if (method.Name is not ("Write" or "WriteLine")) {
            return false;
        }

        for (var type = method.ContainingType; type is not null; type = type.BaseType) {
            if (sinks.Any(sink => SymbolEqualityComparer.Default.Equals(sink, type))) {
                return true;
            }
        }

        return false;
    }

    static bool Mentions(SyntaxNodeAnalysisContext context, ArgumentListSyntax arguments, ISymbol caught) =>
        arguments.DescendantNodes()
            .OfType<IdentifierNameSyntax>()
            .Any(name =>
                SymbolEqualityComparer.Default.Equals(
                    context.SemanticModel.GetSymbolInfo(name, context.CancellationToken).Symbol,
                    caught
                )
            );
}
