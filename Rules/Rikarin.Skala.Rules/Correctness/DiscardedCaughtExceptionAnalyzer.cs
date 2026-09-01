using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Rules.Metadata;
using System.Collections.Immutable;
using System.Linq;

namespace Rikarin.Skala.Rules.Correctness;

/// <summary>
///     <c>SK2093</c> — the handler names the exception, throws a different one, and never passes the
///     first to the second.
/// </summary>
/// <remarks>
///     docs/plan/08-rule-catalogue.md § "SK2000 — Correctness".
///     <c>catch (SqlException error) { throw new ImportException("the import failed"); }</c> keeps the
///     word "failed" and throws away the stack trace, the message, the error code and the cause. The
///     caller is handed a sentence it cannot act on, and the frame that actually broke is not in the
///     process any more. The repair is one argument.
///     <para>
///         ⚠ <b>This is not <c>SK2014</c>.</b> That rule reports a <c>catch</c> whose block is
///         <em>empty</em>, and its negative fixture asserts silence on any clause containing a statement.
///         This one requires a statement — a <c>throw</c> of a newly constructed exception — so the two
///         cannot both fire on one clause.
///     </para>
///     <para>
///         ⚠ <b>Disjoint from <c>SK7092</c> by construction, not by filter.</b> <c>SK7092</c> requires the
///         clause to propagate the same exception, by <c>throw;</c> or <c>throw error;</c>. This rule
///         requires that it does <b>not</b>: a clause holding either form is the wrapping case
///         <c>SK7092</c> already reasons about and is never reported here. The two conditions are
///         negations of one another, so no clause can produce both findings and no <c>supersedes</c>
///         entry is involved.
///     </para>
///     <para>
///         ⚠ <b>The rule ships the half it can repair, and the omitted half is named.</b> A finding needs
///         a <c>catch</c> that <em>declared a variable</em> — the author's own statement that the
///         exception mattered — and a replacement type that already offers a constructor taking the same
///         arguments plus an inner <see cref="System.Exception" />. Where no such constructor exists there
///         is no edit to propose and the finding would be advice to redesign a type; where the clause
///         binds no variable at all, <c>catch (FileNotFoundException) { throw new ConfigMissing(…); }</c>
///         is a deliberate translation in which the type was the whole of the information. Both are
///         silent, which under-reports in the direction that keeps the rule answerable.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class DiscardedCaughtExceptionAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.CaughtExceptionDiscarded);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(static start => {
                if (start.Compilation.GetTypeByMetadataName("System.Exception") is not { } exception) {
                    return;
                }

                start.RegisterSyntaxNodeAction(
                    context => Analyze(context, exception),
                    SyntaxKind.CatchClause
                );
            }
        );
    }

    static void Analyze(SyntaxNodeAnalysisContext context, INamedTypeSymbol exception) {
        var clause = (CatchClauseSyntax)context.Node;

        // No name means nothing was bound and nothing can be passed on. `SK2014` covers the empty
        // clause; a translation that never named the exception is deliberately out of scope.
        if (clause.Declaration is not { Identifier.ValueText.Length: > 0 } declaration
            || context.SemanticModel.GetDeclaredSymbol(declaration, context.CancellationToken) is not { } caught) {
            return;
        }

        // ⚠ The disjointness with SK7092, and the reason it is a condition rather than a filter: a
        // clause that propagates what it caught is that rule's subject, whatever else it does.
        if (Propagates(context, clause, caught)) {
            return;
        }

        foreach (var thrown in ExceptionFlow.Throws(clause.Block)) {
            if (thrown.FirstAncestorOrSelf<CatchClauseSyntax>() != clause
                || !ExceptionFlow.CanEscape(thrown, clause.Block)) {
                continue;
            }

            var expression = thrown switch {
                ThrowStatementSyntax statement => statement.Expression,
                ThrowExpressionSyntax value => value.Expression,
                _ => null
            };

            if (expression is not ObjectCreationExpressionSyntax { ArgumentList: { } arguments } creation) {
                continue;
            }

            var created = context.SemanticModel.GetTypeInfo(creation, context.CancellationToken).Type;
            if (created is null || created.TypeKind == TypeKind.Error || !Derives(created, exception)) {
                continue;
            }

            // Handing the caught exception to the replacement — as the inner exception, or anywhere
            // else in the construction — is the thing this rule asks for.
            if (Mentions(context, creation, caught)) {
                continue;
            }

            if (context.SemanticModel.GetSymbolInfo(creation, context.CancellationToken).Symbol
                is not IMethodSymbol constructor
                || !HasChainingOverload(context, creation, created, constructor, exception)) {
                continue;
            }

            var insertion = new TextSpan(arguments.CloseParenToken.SpanStart, 0);
            var fix = FixEdits.Pack(
                (insertion, (arguments.Arguments.Count == 0 ? "" : ", ") + declaration.Identifier.ValueText)
            );

            context.ReportDiagnostic(
                Diagnostic.Create(
                    Descriptor,
                    creation.GetLocation(),
                    fix,
                    "`" + created.Name + "` replaces `" + declaration.Identifier.ValueText
                    + "` without carrying it, so the stack trace, the message and the cause are gone by "
                    + "the time the caller sees the failure"
                )
            );
        }
    }

    /// <summary>
    ///     <c>throw;</c> or <c>throw error;</c> — the two forms that send on the same exception.
    /// </summary>
    /// <remarks>
    ///     ⚠ Not scoped to the throws this clause owns, and that is deliberate. A <c>throw;</c> anywhere
    ///     inside the block — including in a nested handler's own <c>catch</c> — means the clause is
    ///     capable of propagating, and this rule stands down wherever that is in doubt.
    /// </remarks>
    static bool Propagates(SyntaxNodeAnalysisContext context, CatchClauseSyntax clause, ISymbol caught) {
        foreach (var node in ExceptionFlow.Throws(clause.Block)) {
            if (node is not ThrowStatementSyntax statement) {
                continue;
            }

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
    ///     Whether the replacement type offers this call's arguments plus a trailing inner exception.
    /// </summary>
    /// <remarks>
    ///     ⚠ The precondition that turns the finding into an edit. Without a matching overload the
    ///     advice would be "give this type an inner-exception constructor", which is a change to a
    ///     public surface somewhere else and not something a <c>catch</c> block's author can apply.
    ///     Requiring it costs recall on exception types that never took a cause, and those are the ones
    ///     for which the rule has nothing to say anyway.
    /// </remarks>
    static bool HasChainingOverload(
        SyntaxNodeAnalysisContext context,
        ObjectCreationExpressionSyntax creation,
        ITypeSymbol created,
        IMethodSymbol constructor,
        INamedTypeSymbol exception
    ) {
        if (created is not INamedTypeSymbol type) {
            return false;
        }

        // ⚠ No `^1` — this project targets the analyzer's own framework, where `System.Index` does not
        // exist and the index-from-end operator is a compile error rather than a style choice.
        foreach (var candidate in type.InstanceConstructors) {
            if (candidate.Parameters.Length != constructor.Parameters.Length + 1
                || !SymbolEqualityComparer.Default.Equals(
                    candidate.Parameters[candidate.Parameters.Length - 1].Type,
                    exception
                )
                || !context.SemanticModel.IsAccessible(creation.SpanStart, candidate)) {
                continue;
            }

            var matches = true;
            for (var i = 0; i < constructor.Parameters.Length; i++) {
                if (!SymbolEqualityComparer.Default.Equals(
                        candidate.Parameters[i].Type,
                        constructor.Parameters[i].Type
                    )) {
                    matches = false;
                    break;
                }
            }

            if (matches) {
                return true;
            }
        }

        return false;
    }

    static bool Mentions(SyntaxNodeAnalysisContext context, SyntaxNode node, ISymbol caught) =>
        node.DescendantNodes()
            .OfType<IdentifierNameSyntax>()
            .Any(name =>
                SymbolEqualityComparer.Default.Equals(
                    context.SemanticModel.GetSymbolInfo(name, context.CancellationToken).Symbol,
                    caught
                )
            );

    static bool Derives(ITypeSymbol type, INamedTypeSymbol exception) {
        for (var current = type; current is not null; current = current.BaseType) {
            if (SymbolEqualityComparer.Default.Equals(current, exception)) {
                return true;
            }
        }

        return false;
    }
}
