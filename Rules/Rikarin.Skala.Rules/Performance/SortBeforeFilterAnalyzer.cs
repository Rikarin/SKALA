using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Rules.Metadata;
using System;
using System.Collections.Immutable;

namespace Rikarin.Skala.Rules.Performance;

/// <summary>
///     <c>SK4034</c> — <c>xs.OrderBy(k).Where(p)</c> sorts the elements it is about to discard.
/// </summary>
/// <remarks>
///     <para>
///         <c>OrderBy</c> buffers the entire sequence, computes a key for every element and sorts all of
///         them; <c>Where</c> then throws some away. Filtering first makes the buffer, the key array
///         and the sort proportional to what survives instead of to what arrived — the one rewrite in
///         this range that changes an <em>asymptotic</em> constant rather than an allocation count.
///     </para>
///     <para>
///         ⚠ <b>The output sequence is identical, and that rests on <c>OrderBy</c> being stable.</b>
///         A stable sort leaves elements with equal keys in their original relative order, and
///         <c>Where</c> preserves relative order, so both spellings emit the survivors ordered by key
///         with ties broken by source position. An unstable sort would make the two orders differ for
///         equal keys, and this rule would be wrong; LINQ documents <c>OrderBy</c> as stable.
///     </para>
///     <para>
///         ⚠ The indexed <c>Where(Func&lt;T, int, bool&gt;)</c> overload is refused. After the sort the
///         index is a position in sorted order and before it is a position in source order, so moving
///         that call is a rewrite of the program rather than of the pipeline.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class SortBeforeFilterAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.SortBeforeFilter);

    static readonly string[] Sorts = ["OrderBy", "OrderByDescending", "Order", "OrderDescending"];

    /// <summary>
    ///     ⚠ <c>SK4010</c>'s consumers. Where one of these follows the <c>Where</c>, that rule offers
    ///     an edit over an overlapping span, and two fixes that overlap cannot both be applied.
    /// </summary>
    static readonly string[] Sk4010Consumers = [
        "Any", "Count", "First", "FirstOrDefault", "Last", "LastOrDefault", "LongCount", "Single",
        "SingleOrDefault"
    ];

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(static start => {
                var enumerable = start.Compilation.GetTypeByMetadataName("System.Linq.Enumerable");
                if (enumerable is null) {
                    return;
                }

                start.RegisterSyntaxNodeAction(
                    context => Analyze(context, enumerable),
                    SyntaxKind.InvocationExpression
                );
            }
        );
    }

    static void Analyze(SyntaxNodeAnalysisContext context, INamedTypeSymbol enumerable) {
        var filter = (InvocationExpressionSyntax)context.Node;

        // ⚠ Plain member access at both levels. A conditional access binds through a
        // MemberBindingExpression, and swapping two of those means moving the binding as well.
        if (filter.Expression is not MemberAccessExpressionSyntax {
                RawKind: (int)SyntaxKind.SimpleMemberAccessExpression,
                Name.Identifier.ValueText: "Where"
            } filterAccess
            || filter.ArgumentList.Arguments.Count != 1
            || filterAccess.Expression is not InvocationExpressionSyntax sort
            || sort.Expression is not MemberAccessExpressionSyntax {
                RawKind: (int)SyntaxKind.SimpleMemberAccessExpression
            } sortAccess
            || Array.IndexOf(Sorts, sortAccess.Name.Identifier.ValueText) < 0) {
            return;
        }

        var model = context.SemanticModel;
        var cancellation = context.CancellationToken;

        // ⚠ The `Func<T, int, bool>` overload counts positions in the sorted sequence. Parameter
        // count alone would not tell it apart from `Where(source, predicate)`; the predicate's
        // shape is what does.
        if (model.GetSymbolInfo(filter, cancellation).Symbol is not IMethodSymbol filterMethod
            || Original(filterMethod) is not { Parameters.Length: 2 } filterDefinition
            || !SymbolEqualityComparer.Default.Equals(filterDefinition.ContainingType, enumerable)
            || !IsPredicate(filterDefinition.Parameters[1].Type)) {
            return;
        }

        // ⚠ Queryable is excluded for the same reason SK4010 excludes it: a provider is free to
        // translate one ordering of the pipeline and not the other, and a fix that turns a working
        // query into a runtime "unsupported expression" is worse than the sort it saved.
        if (model.GetSymbolInfo(sort, cancellation).Symbol is not IMethodSymbol sortMethod
            || !SymbolEqualityComparer.Default.Equals(Original(sortMethod).ContainingType, enumerable)) {
            return;
        }

        if (FoldsIntoTheNextOperator(filter)) {
            return;
        }

        var sortSpan = TextSpan.FromBounds(sortAccess.Name.SpanStart, sort.Span.End);
        var filterSpan = TextSpan.FromBounds(filterAccess.Name.SpanStart, filter.Span.End);

        // ⚠ Both texts are moved verbatim, so a comment anywhere inside either call — or in the
        // dot between them — would land somewhere it does not belong.
        if (CallShape.ContainsComment(sort) || CallShape.ContainsComment(filter)) {
            return;
        }

        var tree = filter.SyntaxTree;
        var sortText = tree.GetText(cancellation).ToString(sortSpan);
        var filterText = tree.GetText(cancellation).ToString(filterSpan);

        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                Location.Create(tree, TextSpan.FromBounds(sortSpan.Start, filterSpan.End)),
                FixEdits.Pack((sortSpan, filterText), (filterSpan, sortText)),
                "`"
                + sortAccess.Name.Identifier.ValueText
                + "` sorts every element, including the ones `Where` is about to discard; filtering "
                + "first makes the buffer and the sort proportional to what survives"
            )
        );
    }

    /// <summary>
    ///     Whether <c>SK4010</c> would offer to fold this <c>Where</c> into the operator after it.
    /// </summary>
    /// <remarks>
    ///     ⚠ Both rules are right about <c>xs.OrderBy(k).Where(p).First()</c>, and their edits
    ///     overlap, so only one of them may speak. <c>SK4010</c> keeps it: folding the predicate into
    ///     <c>First</c> lets the search stop at the first match, and once it has been applied this
    ///     shape no longer exists to report.
    /// </remarks>
    static bool FoldsIntoTheNextOperator(InvocationExpressionSyntax filter) =>
        filter.Parent is MemberAccessExpressionSyntax { RawKind: (int)SyntaxKind.SimpleMemberAccessExpression } access
        && access.Parent is InvocationExpressionSyntax { ArgumentList.Arguments.Count: 0 }
        && Array.IndexOf(Sk4010Consumers, access.Name.Identifier.ValueText) >= 0;

    static IMethodSymbol Original(IMethodSymbol method) => (method.ReducedFrom ?? method).OriginalDefinition;

    static bool IsPredicate(ITypeSymbol type) =>
        type is INamedTypeSymbol { Name: "Func", TypeArguments.Length: 2 } func
        && func.TypeArguments[1].SpecialType == SpecialType.System_Boolean;
}
