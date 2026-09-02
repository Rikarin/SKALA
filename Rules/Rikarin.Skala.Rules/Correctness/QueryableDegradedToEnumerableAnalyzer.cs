using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Rikarin.Skala.Rules.Metadata;
using System.Collections.Immutable;

namespace Rikarin.Skala.Rules.Correctness;

/// <summary>
///     <c>SK2153</c> — a deferred LINQ operator bound to <c>Enumerable</c> on an <c>IQueryable</c>.
/// </summary>
/// <remarks>
///     docs/plan/08-rule-catalogue.md § "Culture, comparison policy and query shape".
///     <para>
///         Once one operator binds to <c>System.Linq.Enumerable</c>, the provider has already been
///         asked to enumerate everything it has: the <c>Where</c> meant to become a <c>WHERE</c> clause
///         becomes a full table read, and every operator after it runs on the wire's output. Nothing
///         fails and nothing warns; the only symptom is a query that got slower in proportion to the
///         table.
///     </para>
///     <para>
///         ⚠ <b>The deliberate <c>.AsEnumerable()</c> is excluded by construction, not by a filter.</b>
///         That call returns <c>IEnumerable&lt;T&gt;</c>, so its result's static type does not implement
///         <c>IQueryable</c> and whatever is chained onto it can never reach this rule. <c>ToList</c>
///         and <c>ToArray</c> are outside for the same reason. No name has to be special-cased, which
///         means no name can be forgotten.
///     </para>
///     <para>
///         ⚠ <b>Only operators that return a sequence are reported.</b> <c>ToList</c>,
///         <c>ToDictionary</c> and friends have no <c>Queryable</c> counterpart, so binding them to
///         <c>Enumerable</c> is the intended way to materialise — reporting those would report every
///         correct query in the repository. Requiring the return type to be <c>IEnumerable&lt;T&gt;</c>
///         or <c>IOrderedEnumerable&lt;T&gt;</c> selects exactly the operators whose result could still
///         have been a query.
///     </para>
///     <para>
///         ⚠ <b>Scalar operators are silent even though <c>Queryable</c> has them.</b> A <c>Count()</c>
///         bound to <c>Enumerable</c> on an <c>IQueryable</c> really does fetch the table, but it
///         normally sits at the end of a chain this rule has already reported, and reporting both
///         turns one defect into two findings pointing at one line's worth of cause.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class QueryableDegradedToEnumerableAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.QueryableDegradedToEnumerable);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(static start => {
                var enumerable = start.Compilation.GetTypeByMetadataName("System.Linq.Enumerable");
                var queryable = start.Compilation.GetTypeByMetadataName("System.Linq.IQueryable");
                var sequence = start.Compilation.GetTypeByMetadataName("System.Collections.Generic.IEnumerable`1");
                var ordered = start.Compilation.GetTypeByMetadataName("System.Linq.IOrderedEnumerable`1");
                if (enumerable is null || queryable is null || sequence is null) {
                    return;
                }

                start.RegisterSyntaxNodeAction(
                    context => Analyze(context, enumerable, queryable, sequence, ordered),
                    SyntaxKind.InvocationExpression
                );
            }
        );
    }

    static void Analyze(
        SyntaxNodeAnalysisContext context,
        INamedTypeSymbol enumerable,
        INamedTypeSymbol queryable,
        INamedTypeSymbol sequence,
        INamedTypeSymbol? ordered
    ) {
        var invocation = (InvocationExpressionSyntax)context.Node;
        if (invocation.Expression is not MemberAccessExpressionSyntax access
            || context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol
            is not IMethodSymbol method) {
            return;
        }

        // The extension had to be *called* as one: `Enumerable.Where(source, p)` written out is a
        // deliberate spelling and is not this defect.
        if (method.ReducedFrom is null
            || !SymbolEqualityComparer.Default.Equals(method.ContainingType, enumerable)) {
            return;
        }

        if (!ReturnsASequence(method, sequence, ordered)) {
            return;
        }

        // ⚠ The receiver's *static* type, which is the whole rule. A runtime `IQueryable` reached
        // through an `IEnumerable<T>` variable is already degraded and nothing here can see it; a
        // static `IQueryable` is the one case where the source still said "query".
        var receiver = context.SemanticModel.GetTypeInfo(access.Expression, context.CancellationToken).Type;
        if (receiver is null || receiver.TypeKind == TypeKind.Error || !Implements(receiver, queryable)) {
            return;
        }

        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                invocation.GetLocation(),
                "`"
                + method.Name
                + "` binds to Enumerable on an IQueryable, so the provider is asked to enumerate "
                + "everything and this operator and every one after it run in the client; pass an "
                + "Expression<> so it stays a query, or say `.AsEnumerable()` if that was the intent"
            )
        );
    }

    static bool ReturnsASequence(IMethodSymbol method, INamedTypeSymbol sequence, INamedTypeSymbol? ordered) {
        if (method.ReturnType is not INamedTypeSymbol { IsGenericType: true } type) {
            return false;
        }

        var definition = type.OriginalDefinition;

        return SymbolEqualityComparer.Default.Equals(definition, sequence)
            || (ordered is not null && SymbolEqualityComparer.Default.Equals(definition, ordered));
    }

    static bool Implements(ITypeSymbol type, INamedTypeSymbol queryable) {
        if (SymbolEqualityComparer.Default.Equals(type.OriginalDefinition, queryable)) {
            return true;
        }

        foreach (var candidate in type.AllInterfaces) {
            if (SymbolEqualityComparer.Default.Equals(candidate.OriginalDefinition, queryable)) {
                return true;
            }
        }

        return false;
    }
}
