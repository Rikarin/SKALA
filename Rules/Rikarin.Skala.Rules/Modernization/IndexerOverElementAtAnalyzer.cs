using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Rules.Metadata;
using System;
using System.Collections.Immutable;

namespace Rikarin.Skala.Rules.Modernization;

/// <summary>
///     <c>SK1082</c> — <c>list.ElementAt(i)</c> is <c>list[i]</c>.
/// </summary>
/// <remarks>
///     <para>
///         <c>ElementAt</c> exists so that a bare <c>IEnumerable&lt;T&gt;</c> can be indexed at all. On a
///         list the only branch it ever takes is the one that reaches for the indexer the receiver
///         already declares, so the call is a generic entry point wrapped around language syntax.
///     </para>
///     <para>
///         ⚠
///         <b>
///             The receiver set is <c>List&lt;T&gt;</c> and <c>ImmutableList&lt;T&gt;</c>, and the
///             reason is the exception type.
///         </b> <c>Enumerable.ElementAt</c> bounds-checks itself and throws
///         <c>ArgumentOutOfRangeException</c> on every path; those two throw
///         <c>ArgumentOutOfRangeException</c> from their indexers, so the type of the failure survives
///         the rewrite. <b>An array does not qualify and is the shape this exclusion exists for</b>:
///         <c>array[i]</c> throws <c>IndexOutOfRangeException</c>, so a <c>catch</c> written for one
///         stops catching. <c>IList&lt;T&gt;</c> and <c>IReadOnlyList&lt;T&gt;</c> go with it, because an
///         array is a legal value for both.
///     </para>
///     <para>
///         ⚠
///         <b>
///             <c>ImmutableArray&lt;T&gt;</c> is refused a guard earlier than that, and the difference
///             was found by a sabotage that turned nothing red.
///         </b> <c>System.Linq.ImmutableArrayExtensions</c> declares its own
///         <c>ElementAt(this ImmutableArray&lt;T&gt;, int)</c>, so the call never binds to
///         <c>Enumerable.ElementAt</c> and the receiver set is never consulted for it. Adding it to that
///         set changes nothing, which is exactly what a guard nobody can see looks like. The fixture
///         that holds the receiver set down is <c>Collection&lt;T&gt;</c>: it binds
///         <c>Enumerable.ElementAt</c>, it declares an <c>int</c> indexer, and only the closed list
///         refuses it.
///     </para>
///     <para>
///         ⚠ This is the same receiver set as <c>SK4030</c>, reached from the other direction: that rule
///         substitutes one <em>method</em> for another and this one removes the call in favour of syntax.
///     </para>
///     <para>
///         ⚠ <c>ElementAtOrDefault</c> is not reported. It returns <c>default(T)</c> where the indexer
///         throws, so there is no indexer expression that means the same thing.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class IndexerOverElementAtAnalyzer : DiagnosticAnalyzer {
    static readonly RuleInfo Rule = RuleCatalog.Get(RuleIds.IndexerOverElementAt);
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.IndexerOverElementAt);

    /// <summary>
    ///     ⚠ The two receivers whose indexer throws what <c>ElementAt</c> throws. Every other indexable
    ///     type in the framework either throws a different exception or cannot be told apart from one
    ///     that does.
    /// </summary>
    static readonly string[] Receivers = [
        "System.Collections.Generic.List`1",
        "System.Collections.Immutable.ImmutableList`1"
    ];

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(static start => {
                if (!SkalaRule.MeetsLanguageVersion(start.Compilation, Rule.LanguageVersion)) {
                    return;
                }

                var enumerable = start.Compilation.GetTypeByMetadataName("System.Linq.Enumerable");
                if (enumerable is null) {
                    return;
                }

                var receivers = ImmutableArray.CreateBuilder<INamedTypeSymbol>();
                foreach (var name in Receivers) {
                    // ⚠ Resolved rather than compared by name. `ImmutableList<T>` is not in every
                    // reference set, and a source type spelled the same way is not the framework's.
                    if (start.Compilation.GetTypeByMetadataName(name) is { } type) {
                        receivers.Add(type);
                    }
                }

                if (receivers.Count == 0) {
                    return;
                }

                var resolved = receivers.ToImmutable();
                start.RegisterSyntaxNodeAction(
                    context => Analyze(context, enumerable, resolved),
                    SyntaxKind.InvocationExpression
                );
            }
        );
    }

    static void Analyze(
        SyntaxNodeAnalysisContext context,
        INamedTypeSymbol enumerable,
        ImmutableArray<INamedTypeSymbol> receivers
    ) {
        var invocation = (InvocationExpressionSyntax)context.Node;

        // ⚠ `xs?.ElementAt(i)` becomes `xs?[i]`, which is a different edit in a different place.
        if (invocation.Expression is not MemberAccessExpressionSyntax {
                RawKind: (int)SyntaxKind.SimpleMemberAccessExpression
            } access
            || !string.Equals(access.Name.Identifier.ValueText, "ElementAt", StringComparison.Ordinal)
            || invocation.ArgumentList.Arguments.Count != 1) {
            return;
        }

        var argument = invocation.ArgumentList.Arguments[0];
        if (argument.NameColon is not null || !argument.RefKindKeyword.IsKind(SyntaxKind.None)) {
            return;
        }

        var model = context.SemanticModel;
        var cancellation = context.CancellationToken;

        if (model.GetSymbolInfo(invocation, cancellation).Symbol is not IMethodSymbol symbol) {
            return;
        }

        var definition = (symbol.ReducedFrom ?? symbol).OriginalDefinition;
        if (!SymbolEqualityComparer.Default.Equals(definition.ContainingType, enumerable)
            || definition.Parameters.Length != 2) {
            return;
        }

        // ⚠ The `Index`-typed overload is refused: `list[^1]` needs C# 8 and is `SK1060`'s rewrite,
        // not this one. Matching on the parameter's special type is what tells the two apart —
        // a parameter count would not.
        if (definition.Parameters[1].Type.SpecialType != SpecialType.System_Int32) {
            return;
        }

        // ⚠ The static-call spelling `Enumerable.ElementAt(xs, i)` has no receiver to index and its
        // fix would be a different edit; `IsExtensionMethodInvocation` is the check that separates them.
        if (symbol.ReducedFrom is null) {
            return;
        }

        if (model.GetTypeInfo(access.Expression, cancellation).Type is not INamedTypeSymbol receiver) {
            return;
        }

        var matched = false;
        foreach (var candidate in receivers) {
            if (SymbolEqualityComparer.Default.Equals(receiver.OriginalDefinition, candidate)) {
                matched = true;
                break;
            }
        }

        if (!matched) {
            return;
        }

        // Everything from the `.` to the end of the call becomes `[index]`. The index is carried
        // across as source text so whatever was written inside the parentheses survives verbatim.
        var span = TextSpan.FromBounds(access.OperatorToken.SpanStart, invocation.Span.End);
        if (RewriteGuards.ContainsCommentOrDirective(invocation.SyntaxTree, span)) {
            return;
        }

        var index = argument.Expression.ToString();
        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                Location.Create(invocation.SyntaxTree, span),
                FixEdits.Pack((span, "[" + index + "]")),
                "The receiver declares an indexer: `[" + RewriteGuards.Trim(index) + "]`"
            )
        );
    }
}
