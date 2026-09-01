using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Rikarin.Skala.Rules.Metadata;
using System.Collections.Immutable;
using System.Linq;

namespace Rikarin.Skala.Rules.Design;

/// <summary>
///     <c>SK6052</c> — a method whose return type is a sequence returns <c>null</c>.
/// </summary>
/// <remarks>
///     Every caller now has to null-check before iterating, and the ones that forget fail at a
///     <c>foreach</c> a long way from the return that caused it. An empty array costs nothing —
///     <c>[]</c> for a <c>T[]</c> is <c>Array.Empty&lt;T&gt;()</c>, which is a singleton — and it turns a
///     branch every caller has to write into no branch at all.
///     <para>
///         ⚠
///         <b>
///             Disjoint from <c>SK3020</c> by construction, and the overlap is where an
///             <c>async</c> method sits.
///         </b> <c>SK3020</c> reports a non-<c>async</c> method whose declared
///         return type is <c>Task</c> returning null — a null <em>task</em>, which throws at the
///         <c>await</c>. This rule requires the effective return type to be a sequence: for a
///         non-<c>async</c> <c>Task&lt;IEnumerable&lt;T&gt;&gt;</c> the declared type is a task, so this
///         rule declines and <c>SK3020</c> reports; for the <c>async</c> form <c>SK3020</c> declines by
///         its own <c>async</c> guard and this rule reports the null sequence inside a real task. The two
///         predicates cannot both hold, which is why neither declares <c>supersedes</c> on the other.
///     </para>
///     <para>
///         ⚠ An <c>IEnumerable&lt;T&gt;?</c> return type is the author saying null is a value this method
///         returns, and it is declined. The contract already carries the warning that an unguarded
///         <c>foreach</c> would be wrong, and the rule has nothing to add to a decision that was made on
///         purpose.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class NullSequenceReturnAnalyzer : DiagnosticAnalyzer {
    /// <summary>
    ///     ⚠ The sequence types a collection expression is guaranteed to construct, and no others.
    /// </summary>
    /// <remarks>
    ///     The fix is <c>[]</c>, so the list is exactly what C# 12 target-types a collection expression
    ///     to: the five sequence interfaces, <c>List&lt;T&gt;</c>, and any array. <c>IDictionary</c> is
    ///     deliberately absent — it is not one of them, and a fix that did not compile would be worse
    ///     than a finding that was never made. <c>string</c> is an <c>IEnumerable&lt;char&gt;</c> and is
    ///     never a sequence in the sense this rule means; it is excluded by being matched on the declared
    ///     type rather than on assignability.
    /// </remarks>
    static readonly string[] SequenceTypes = [
        "System.Collections.Generic.IEnumerable`1", "System.Collections.Generic.ICollection`1",
        "System.Collections.Generic.IList`1", "System.Collections.Generic.IReadOnlyCollection`1",
        "System.Collections.Generic.IReadOnlyList`1", "System.Collections.Generic.List`1"
    ];

    static readonly RuleInfo Rule = RuleCatalog.Get(RuleIds.NullReturnedInsteadOfEmpty);

    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.NullReturnedInsteadOfEmpty);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(static start => {
                // ⚠ The fix is a collection expression, so the rule is silent below C# 12 rather than
                // offering an edit that does not compile. `hasFix: true` is a promise for every finding.
                if (!SkalaRule.MeetsLanguageVersion(start.Compilation, Rule.LanguageVersion)) {
                    return;
                }

                var sequences = ImmutableArray.CreateRange(
                    SequenceTypes.Select(start.Compilation.GetTypeByMetadataName)
                        .Where(static type => type is not null)
                        .Select(static type => type!)
                );
                if (sequences.IsEmpty) {
                    return;
                }

                var task = start.Compilation.GetTypeByMetadataName("System.Threading.Tasks.Task`1");
                var valueTask = start.Compilation.GetTypeByMetadataName("System.Threading.Tasks.ValueTask`1");
                start.RegisterSyntaxNodeAction(
                    context => Analyze(context, sequences, task, valueTask),
                    SyntaxKind.ReturnStatement,
                    SyntaxKind.ArrowExpressionClause
                );
            }
        );
    }

    static void Analyze(
        SyntaxNodeAnalysisContext context,
        ImmutableArray<INamedTypeSymbol> sequences,
        INamedTypeSymbol? task,
        INamedTypeSymbol? valueTask
    ) {
        var (expression, function) = context.Node switch {
            ReturnStatementSyntax { Expression: { } value } statement => (value, EnclosingFunction(statement)),
            ArrowExpressionClauseSyntax arrow => (arrow.Expression, arrow.Parent),
            _ => (null, null)
        };

        // ⚠ Syntax first: almost no `return` is `null` or `default`, so the question that costs nothing
        // is asked before any symbol is looked up. The same order SK3020 uses.
        if (expression is null
            || !expression.IsKind(SyntaxKind.NullLiteralExpression)
            && !expression.IsKind(SyntaxKind.DefaultLiteralExpression)) {
            return;
        }

        var model = context.SemanticModel;
        var declared = function switch {
            MethodDeclarationSyntax method => model.GetDeclaredSymbol(method, context.CancellationToken),
            LocalFunctionStatementSyntax local => model.GetDeclaredSymbol(local, context.CancellationToken),
            _ => null
        };
        if (declared is null) {
            return;
        }

        // ⚠ The annotation comes off the *symbol*, and a first draft that read
        // `GetTypeInfo(returnTypeSyntax).Type.NullableAnnotation` reported every `IEnumerable<T>?` in
        // the tree: type info for a type syntax carries no nullable annotation, so the guard read
        // `None` for annotated and unannotated alike and could never decline. The fixture caught it.
        var (effective, annotation) = declared.IsAsync
            ? Awaited(declared.ReturnType, task, valueTask)
            : (declared.ReturnType, declared.ReturnNullableAnnotation);

        if (effective is null || annotation == NullableAnnotation.Annotated || !IsSequence(effective, sequences)) {
            return;
        }

        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                expression.GetLocation(),
                FixEdits.Pack((expression.Span, "[]")),
                "every caller has to null-check before iterating; an empty sequence is free and needs no branch"
            )
        );
    }

    /// <summary>
    ///     The type an <c>async</c> method's callers see after awaiting it, with its own annotation.
    /// </summary>
    /// <remarks>
    ///     ⚠ The annotation comes from <c>TypeArgumentNullableAnnotations</c> rather than from the type
    ///     argument's own <c>NullableAnnotation</c>: for <c>Task&lt;IEnumerable&lt;T&gt;?&gt;</c> the <c>?</c>
    ///     belongs to the argument as it appears in the task, which is where the author wrote it.
    /// </remarks>
    static (ITypeSymbol? Type, NullableAnnotation Annotation) Awaited(
        ITypeSymbol declared,
        INamedTypeSymbol? task,
        INamedTypeSymbol? valueTask
    ) {
        if (declared is not INamedTypeSymbol { IsGenericType: true } named) {
            return (null, NullableAnnotation.None);
        }

        var definition = named.OriginalDefinition;

        return SymbolEqualityComparer.Default.Equals(definition, task)
            || SymbolEqualityComparer.Default.Equals(definition, valueTask)
                ? (named.TypeArguments[0], named.TypeArgumentNullableAnnotations[0])
                : (null, NullableAnnotation.None);
    }

    /// <summary>
    ///     ⚠ The declared type, never assignability. A method returning <c>string</c> returns an
    ///     <c>IEnumerable&lt;char&gt;</c> and is not a sequence in the sense this rule means; so is every
    ///     concrete collection whose <c>null</c> the fix could not replace with <c>[]</c>.
    /// </summary>
    static bool IsSequence(ITypeSymbol type, ImmutableArray<INamedTypeSymbol> sequences) {
        if (type is IArrayTypeSymbol) {
            return true;
        }

        if (type is not INamedTypeSymbol { IsGenericType: true } named) {
            return false;
        }

        foreach (var sequence in sequences) {
            if (SymbolEqualityComparer.Default.Equals(named.OriginalDefinition, sequence)) {
                return true;
            }
        }

        return false;
    }

    static SyntaxNode? EnclosingFunction(SyntaxNode node) {
        for (var current = node.Parent; current is not null; current = current.Parent) {
            switch (current) {
                case MethodDeclarationSyntax:
                case LocalFunctionStatementSyntax:
                    return current;

                // A `return` inside any of these belongs to it, not to the method around it.
                case AnonymousFunctionExpressionSyntax:
                case BaseMethodDeclarationSyntax:
                case AccessorDeclarationSyntax:
                    return null;
            }
        }

        return null;
    }
}
