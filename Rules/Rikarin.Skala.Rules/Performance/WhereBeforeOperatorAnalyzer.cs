using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Rules.Metadata;

namespace Rikarin.Skala.Rules.Performance;

/// <summary>
///     <c>SK4010</c> — <c>xs.Where(p).First()</c> is <c>xs.First(p)</c>.
/// </summary>
/// <remarks>
///     docs/plan/08-rule-catalogue.md § "SK4000 — Performance". <c>Enumerable.Where</c> allocates an
///     iterator that the next operator wraps and drives, so every element crosses two
///     <c>MoveNext</c> frames instead of one. Nine operators have an overload that takes the predicate
///     directly and does the same work in one loop, and LINQ defines the two forms to select the same
///     elements in the same order — so the rewrite has no semantic content, which is exactly what makes
///     it a rule rather than a review comment.
///     <para>
///         ⚠ The predicate overload must exist on the <em>compilation's</em> <c>Enumerable</c>, and it is
///         looked up rather than assumed. The rule ships into a netstandard2.0 analyzer that runs against
///         whatever framework the project targets, and a name that happens to match is not a proof that the
///         overload the fix writes will bind.
///     </para>
///     <para>
///         ⚠ <c>Queryable</c> is deliberately excluded. A provider is free to translate
///         <c>Where(p).First()</c> and not <c>First(p)</c>, and a fix that turns a working query into a
///         runtime "unsupported expression" is worse than the allocation it saved.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class WhereBeforeOperatorAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.WhereBeforeLinqOperator);

    /// <summary>
    ///     The operators whose predicate overload is defined to iterate identically.
    /// </summary>
    /// <remarks>
    ///     ⚠ Not every operator that takes a predicate belongs here. <c>Where(p).All(q)</c> and
    ///     <c>Where(p).Sum(f)</c> take a <em>different</em> function, so folding them is a rewrite of
    ///     the program rather than of the pipeline; <c>Where(p).Select(f)</c> has no predicate overload
    ///     at all. This list is the set where the second argument is the same predicate.
    /// </remarks>
    static readonly string[] Consumers = [
        "Any", "Count", "First", "FirstOrDefault", "Last", "LastOrDefault", "LongCount", "Single",
        "SingleOrDefault"
    ];

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(static start => {
                var enumerable = start.Compilation.GetTypeByMetadataName("System.Linq.Enumerable");
                if (enumerable is null || !HasPredicateOverload(enumerable, "Where")) {
                    return;
                }

                var consumers = new HashSet<string>(StringComparer.Ordinal);
                foreach (var name in Consumers) {
                    if (HasPredicateOverload(enumerable, name)) {
                        consumers.Add(name);
                    }
                }

                if (consumers.Count == 0) {
                    return;
                }

                start.RegisterSyntaxNodeAction(
                    context => Analyze(context, enumerable, consumers),
                    SyntaxKind.InvocationExpression
                );
            }
        );
    }

    /// <summary>Whether <c>Enumerable</c> has a <c>name(source, Func&lt;T, bool&gt;)</c> overload.</summary>
    static bool HasPredicateOverload(INamedTypeSymbol enumerable, string name) {
        foreach (var member in enumerable.GetMembers(name)) {
            if (member is IMethodSymbol { IsStatic: true, Parameters.Length: 2 } method
                && IsPredicate(method.Parameters[1].Type)) {
                return true;
            }
        }

        return false;
    }

    static bool IsPredicate(ITypeSymbol type) =>
        type is INamedTypeSymbol { Name: "Func", TypeArguments.Length: 2 } func
        && func.TypeArguments[1].SpecialType == SpecialType.System_Boolean;

    static void Analyze(
        SyntaxNodeAnalysisContext context,
        INamedTypeSymbol enumerable,
        HashSet<string> consumers
    ) {
        var outer = (InvocationExpressionSyntax)context.Node;

        // ⚠ Plain member access at both levels. `xs?.Where(p).First()` binds `Where` through a
        // MemberBindingExpression, and rewriting that means moving the binding rather than
        // replacing a name — a different edit, so a different rule would be needed to get it right.
        if (outer.Expression is not MemberAccessExpressionSyntax {
                RawKind: (int)SyntaxKind.SimpleMemberAccessExpression
            } consumerAccess
            || !consumers.Contains(consumerAccess.Name.Identifier.ValueText)) {
            return;
        }

        if (consumerAccess.Expression is not InvocationExpressionSyntax filter
            || filter.Expression is not MemberAccessExpressionSyntax {
                RawKind: (int)SyntaxKind.SimpleMemberAccessExpression
            } filterAccess
            || !string.Equals(filterAccess.Name.Identifier.ValueText, "Where", StringComparison.Ordinal)) {
            return;
        }

        var model = context.SemanticModel;
        var cancellation = context.CancellationToken;

        // The consuming call must be the plain, argument-less overload — `Count(p)` folded into an
        // existing `Count(q)` would be two predicates and one loop, which is a different program.
        if (model.GetSymbolInfo(outer, cancellation).Symbol is not IMethodSymbol consumer
            || Original(consumer) is not { Parameters.Length: 1 } consumerDefinition
            || !SymbolEqualityComparer.Default.Equals(consumerDefinition.ContainingType, enumerable)) {
            return;
        }

        // ⚠ The `Func<T, int, bool>` overload has no counterpart on any of the consumers: the index
        // it hands the predicate does not survive the fold. `Parameters.Length: 2` alone would not
        // catch it, because the indexed overload has two parameters too — the predicate's shape is
        // what tells them apart.
        if (model.GetSymbolInfo(filter, cancellation).Symbol is not IMethodSymbol where
            || Original(where) is not { Parameters.Length: 2 } whereDefinition
            || !SymbolEqualityComparer.Default.Equals(whereDefinition.ContainingType, enumerable)
            || !IsPredicate(whereDefinition.Parameters[1].Type)) {
            return;
        }

        // ⚠ Both edits delete text. A comment or a directive inside either span is content, and
        // deleting content is not a fix. Layout the formatter would have rewritten anyway is fine.
        if (!IsLayoutOnly(filterAccess.Name.GetTrailingTrivia())
            || !IsLayoutOnly(filter.ArgumentList.OpenParenToken.LeadingTrivia)
            || !IsLayoutOnlyBetween(filter.ArgumentList.CloseParenToken, outer.GetLastToken())) {
            return;
        }

        var name = consumerAccess.Name.Identifier.ValueText;
        var fix = FixEdits.Pack(
            (
                TextSpan.FromBounds(
                    filterAccess.Name.SpanStart,
                    filter.ArgumentList.OpenParenToken.Span.End
                ),
                name + "("
            ),
            (
                TextSpan.FromBounds(filter.ArgumentList.CloseParenToken.Span.End, outer.Span.End),
                string.Empty
            )
        );

        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                Location.Create(
                    outer.SyntaxTree,
                    TextSpan.FromBounds(filterAccess.Name.SpanStart, outer.Span.End)
                ),
                fix,
                "`Where(…)." + name + "()` is `" + name + "(…)`; the intermediate iterator does no work"
            )
        );
    }

    /// <summary>
    ///     The method as <c>Enumerable</c> declares it, whether it was called as an extension or not.
    /// </summary>
    /// <remarks>
    ///     ⚠ <c>Enumerable.Where(xs, p).First()</c> and <c>xs.Where(p).First()</c> are the same call
    ///     and the same fix — the second edit is written from the argument list's parentheses, which
    ///     both forms have in the same place. <c>ReducedFrom</c> is what makes the extension form's
    ///     parameter count comparable to the static form's.
    /// </remarks>
    static IMethodSymbol Original(IMethodSymbol method) => (method.ReducedFrom ?? method).OriginalDefinition;

    static bool IsLayoutOnly(SyntaxTriviaList trivia) {
        foreach (var item in trivia) {
            if (!item.IsKind(SyntaxKind.WhitespaceTrivia) && !item.IsKind(SyntaxKind.EndOfLineTrivia)) {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    ///     Whether everything between the end of <paramref name="first" /> and the end of
    ///     <paramref name="last" /> is whitespace and tokens — no comment, no directive.
    /// </summary>
    /// <remarks>
    ///     ⚠ <paramref name="last" />'s own trailing trivia is deliberately not examined: the deleted
    ///     span ends at <c>Span.End</c>, which is before it. Checking it would refuse a fix over
    ///     `…First(); // why` and there is nothing wrong with that comment.
    /// </remarks>
    static bool IsLayoutOnlyBetween(SyntaxToken first, SyntaxToken last) {
        for (var token = first; token.RawKind != (int)SyntaxKind.None && token != last; token = token.GetNextToken()) {
            var next = token.GetNextToken();
            if (!IsLayoutOnly(token.TrailingTrivia) || !IsLayoutOnly(next.LeadingTrivia)) {
                return false;
            }
        }

        return true;
    }
}
