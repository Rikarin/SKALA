using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Rikarin.Skala.Rules.Metadata;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Rikarin.Skala.Rules.Async;

/// <summary>
///     <c>SK3541</c> — an <c>HttpClient</c> whose declaration says it dies at the end of the method.
/// </summary>
/// <remarks>
///     docs/plan/08-rule-catalogue.md § "SK3000 — Async, concurrency, lifetime".
///     <c>HttpClient</c> is the one disposable in the framework whose correct lifetime is the opposite
///     of every other one's: disposing it per call closes the connection pool underneath it, leaves
///     the sockets in <c>TIME_WAIT</c> for the operating system's timeout, and exhausts the ephemeral
///     port range under load. The failure appears only under concurrency, on a machine that is not the
///     author's, as <c>SocketException</c> from a call that worked yesterday.
///     <para>
///         ⚠ <b>The whole family this rule lives in says "this is not disposed" and this one says "this
///         is disposed".</b> <c>SK3501</c>, <c>SK3502</c>, <c>SK3530</c> and <c>SK3532</c> all report a
///         resource whose release is missing; here the release is present, correct by the shape of
///         every other disposable, and wrong. That inversion is why it needs its own rule rather than an
///         exception inside one of theirs — and it is why a rule that reports <c>HttpClient</c> for
///         <em>not</em> being disposed would be reporting the fix.
///     </para>
///     <para>
///         ⚠ <b>What is proved is the declaration, not the lifetime.</b> The rule speaks only where the
///         <c>using</c> is the thing that ends the client's life — a <c>using</c> statement or a
///         <c>using</c> declaration whose resource is a direct <c>new HttpClient(…)</c>. A client
///         assigned to a field, returned, or handed to something else is a lifetime this rule cannot
///         read and does not guess at.
///     </para>
///     <para>
///         ⚠ Three exclusions, each of which is a shape where the same text is correct. An entry point
///         disposes once for the process, which is not per-call and is not a leak. A constructor taking
///         an <c>HttpMessageHandler</c> is the documented mitigation — the sockets live on the shared
///         handler and the client above it is cheap — so disposing that client is right. And a
///         <c>static</c> field's initializer is the canonical correct form, which never reaches this
///         rule because it is not a <c>using</c>.
///     </para>
///     <para>
///         ⚠ <b>Fixless, and not for want of an edit.</b> The repair is a <c>static readonly</c>
///         client, a constructor-injected one, or <c>IHttpClientFactory</c> — a decision about where
///         this type gets its dependencies, taken at the type or the container. Deleting the
///         <c>using</c> in place would turn a bounded leak into an unbounded one.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ShortLivedHttpClientAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.ShortLivedHttpClient);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(static start => {
                var client = start.Compilation.GetTypeByMetadataName("System.Net.Http.HttpClient");
                var handler = start.Compilation.GetTypeByMetadataName("System.Net.Http.HttpMessageHandler");
                if (client is null) {
                    return;
                }

                start.RegisterSyntaxNodeAction(
                    context => Analyze(context, client, handler),
                    SyntaxKind.UsingStatement,
                    SyntaxKind.LocalDeclarationStatement
                );
            }
        );
    }

    static void Analyze(SyntaxNodeAnalysisContext context, INamedTypeSymbol client, INamedTypeSymbol? handler) {
        foreach (var creation in Resources(context.Node)) {
            if (!IsClient(context, creation, client)
                // ⚠ `new HttpClient(sharedHandler, disposeHandler: false)` is the documented
                // mitigation and not the bug: the sockets belong to the handler, the client above
                // it is a thin wrapper, and disposing that wrapper per call costs nothing.
                || TakesAHandler(context, creation, handler)
                || InAnEntryPoint(context.Node)) {
                continue;
            }

            context.ReportDiagnostic(
                Diagnostic.Create(
                    Descriptor,
                    creation.GetLocation(),
                    "`using` ends this `HttpClient` at the end of the call, closing its connection pool "
                    + "and leaving the sockets in `TIME_WAIT`; hold one for the process instead"
                )
            );
        }
    }

    /// <summary>The object creations a <c>using</c> takes ownership of, in either spelling.</summary>
    static IEnumerable<ExpressionSyntax> Resources(SyntaxNode node) {
        switch (node) {
            case UsingStatementSyntax statement:
                if (statement.Expression is not null) {
                    yield return UsingResource.Unwrap(statement.Expression);
                }

                if (statement.Declaration is { } declared) {
                    foreach (var variable in declared.Variables) {
                        if (variable.Initializer?.Value is { } value) {
                            yield return UsingResource.Unwrap(value);
                        }
                    }
                }

                break;

            // ⚠ `using var client = …;`. The `using` keyword is a modifier on the declaration here,
            // and a local declaration without it is a different rule's subject entirely — an
            // undisposed client is what this rule wants and `SK3501` is told to leave alone.
            case LocalDeclarationStatementSyntax {
                UsingKeyword.RawKind: (int)SyntaxKind.UsingKeyword
            } declaration:
                foreach (var variable in declaration.Declaration.Variables) {
                    if (variable.Initializer?.Value is { } value) {
                        yield return UsingResource.Unwrap(value);
                    }
                }

                break;
        }
    }

    static bool IsClient(SyntaxNodeAnalysisContext context, ExpressionSyntax expression, INamedTypeSymbol client) {
        if (expression is not BaseObjectCreationExpressionSyntax) {
            return false;
        }

        var type = context.SemanticModel.GetTypeInfo(expression, context.CancellationToken).Type;
        for (var candidate = type; candidate is not null; candidate = candidate.BaseType) {
            if (candidate.TypeKind == TypeKind.Error) {
                return false;
            }

            if (SymbolEqualityComparer.Default.Equals(candidate, client)) {
                return true;
            }
        }

        return false;
    }

    static bool TakesAHandler(
        SyntaxNodeAnalysisContext context,
        ExpressionSyntax expression,
        INamedTypeSymbol? handler
    ) {
        if (handler is null
            || expression is not BaseObjectCreationExpressionSyntax { ArgumentList.Arguments.Count: > 0 } creation) {
            return false;
        }

        foreach (var argument in creation.ArgumentList!.Arguments) {
            var type = context.SemanticModel.GetTypeInfo(argument.Expression, context.CancellationToken).Type;
            for (var candidate = type; candidate is not null; candidate = candidate.BaseType) {
                if (SymbolEqualityComparer.Default.Equals(candidate, handler)) {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    ///     ⚠ A client disposed once for the whole process is not a client disposed per call.
    /// </summary>
    /// <remarks>
    ///     The test is the declaration's own shape — a static method named <c>Main</c> — rather than
    ///     <c>Compilation.GetEntryPoint</c>, because a library compiled without an
    ///     <c>OutputKind.ConsoleApplication</c> has no entry point at all and the same source is the
    ///     same decision. Top-level statements are excluded for the same reason and by the same walk:
    ///     their enclosing member is the synthesised <c>Main</c>, so a <c>using</c> there sits directly
    ///     under a <c>GlobalStatementSyntax</c> and no method declaration is above it.
    /// </remarks>
    static bool InAnEntryPoint(SyntaxNode node) {
        for (var current = node; current is not null; current = current.Parent) {
            switch (current) {
                case GlobalStatementSyntax:
                    return true;

                case MethodDeclarationSyntax method:
                    return method.Identifier.ValueText == "Main"
                        && method.Modifiers.Any(static modifier => modifier.IsKind(SyntaxKind.StaticKeyword));

                case LocalFunctionStatementSyntax:
                case AnonymousFunctionExpressionSyntax:
                case BaseTypeDeclarationSyntax:
                    return false;
            }
        }

        return false;
    }
}
