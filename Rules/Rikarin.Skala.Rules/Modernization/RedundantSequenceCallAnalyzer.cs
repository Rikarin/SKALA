using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Rules.Metadata;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace Rikarin.Skala.Rules.Modernization;

/// <summary>
///     <c>SK1081</c> — a sequence call whose result is provably the thing that went in.
/// </summary>
/// <remarks>
///     <para>
///         ⚠
///         <b>
///             The <c>Cast&lt;T&gt;</c> branch is not "the cast is unnecessary" — it is "the call
///             returns its own argument".
///         </b> <c>Enumerable.Cast&lt;T&gt;</c> opens with
///         <c>if (source is IEnumerable&lt;T&gt; typed) return typed;</c>, so on a receiver already typed
///         <c>IEnumerable&lt;T&gt;</c> the deletion preserves reference identity and not merely the
///         sequence.
///     </para>
///     <para>
///         ⚠ <b>The receiver's static type must be <c>IEnumerable&lt;T&gt;</c> itself</b>, never a type
///         that merely implements it. On a <c>List&lt;T&gt;</c> receiver the deletion changes the
///         expression's static type — <c>var xs = list.Cast&lt;T&gt;();</c> types <c>xs</c> as
///         <c>IEnumerable&lt;T&gt;</c> and <c>var xs = list;</c> types it as <c>List&lt;T&gt;</c> — and an
///         overload set containing both then resolves differently. That is the trap <c>SK0234</c> records
///         for identity casts, answered here the same way: by covering the subset where the question
///         cannot be asked.
///     </para>
///     <para>
///         ⚠
///         <b>
///             The copy branch's inner call must preserve order and multiplicity, so only
///             <c>ToList</c> and <c>ToArray</c> may be inner.
///         </b> <c>ToHashSet</c> and <c>Distinct</c> remove
///         duplicates: an inner one of those is a real operation and deleting it changes the result. They
///         are allowed as the <em>outer</em> call, where the inner copy is still unobservable.
///     </para>
///     <para>
///         ⚠ The inner result is a temporary written and read inside one expression, so "enumerated
///         exactly once and not mutated in between" is a syntactic fact here rather than the flow
///         question it is for <c>SK4006</c>. That is the whole reason this ships with a fix and
///         <c>SK4006</c> does not.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class RedundantSequenceCallAnalyzer : DiagnosticAnalyzer {
    static readonly RuleInfo Rule = RuleCatalog.Get(RuleIds.RedundantSequenceCall);
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.RedundantSequenceCall);

    /// <summary>
    ///     Materializers that may be the <em>inner</em> call: each yields exactly the elements it was
    ///     given, in order, with duplicates intact.
    /// </summary>
    static readonly HashSet<string> OrderPreservingCopies = new(StringComparer.Ordinal) { "ToList", "ToArray" };

    /// <summary>
    ///     Materializers that may be the <em>outer</em> call. <c>ToHashSet</c> is here and not above:
    ///     de-duplicating the survivors of a faithful copy gives the same set as de-duplicating the
    ///     original, so it is safe to keep and unsafe to remove.
    /// </summary>
    static readonly HashSet<string> Materializers =
        new(StringComparer.Ordinal) { "ToList", "ToArray", "ToHashSet" };

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

                start.RegisterSyntaxNodeAction(
                    context => Analyze(context, enumerable),
                    SyntaxKind.InvocationExpression
                );
            }
        );
    }

    static void Analyze(SyntaxNodeAnalysisContext context, INamedTypeSymbol enumerable) {
        var invocation = (InvocationExpressionSyntax)context.Node;

        // ⚠ A conditional access is refused in both branches: `xs?.Cast<T>()` cannot simply lose the
        // call, because the `?.` is what the member binding hangs from.
        if (invocation.Expression is not MemberAccessExpressionSyntax {
                RawKind: (int)SyntaxKind.SimpleMemberAccessExpression
            } access) {
            return;
        }

        var name = access.Name.Identifier.ValueText;
        if (string.Equals(name, "Cast", StringComparison.Ordinal)) {
            AnalyzeCast(context, enumerable, invocation, access);
        } else if (Materializers.Contains(name)) {
            AnalyzeCopy(context, enumerable, invocation, access, name);
        }
    }

    /// <summary>
    ///     <c>seq.Cast&lt;T&gt;()</c> where <c>seq</c> is already <c>IEnumerable&lt;T&gt;</c>.
    /// </summary>
    static void AnalyzeCast(
        SyntaxNodeAnalysisContext context,
        INamedTypeSymbol enumerable,
        InvocationExpressionSyntax invocation,
        MemberAccessExpressionSyntax access
    ) {
        if (invocation.ArgumentList.Arguments.Count != 0
            || access.Name is not GenericNameSyntax { TypeArgumentList.Arguments.Count: 1 } generic) {
            return;
        }

        var model = context.SemanticModel;
        var cancellation = context.CancellationToken;

        if (model.GetSymbolInfo(invocation, cancellation).Symbol is not IMethodSymbol cast
            || !SymbolEqualityComparer.Default.Equals(Original(cast).ContainingType, enumerable)) {
            return;
        }

        // ⚠ `IEnumerable<T>` exactly — matched against the *receiver's* declared type, not against
        // an interface it happens to implement. See the type-widening note on the class.
        if (model.GetTypeInfo(access.Expression, cancellation).Type is not INamedTypeSymbol {
                TypeArguments.Length: 1
            } receiver
            || receiver.OriginalDefinition.SpecialType != SpecialType.System_Collections_Generic_IEnumerable_T) {
            return;
        }

        if (model.GetTypeInfo(generic.TypeArgumentList.Arguments[0], cancellation).Type is not { } target
            || !SymbolEqualityComparer.Default.Equals(receiver.TypeArguments[0], target)) {
            return;
        }

        Report(
            context,
            TextSpan.FromBounds(access.OperatorToken.SpanStart, invocation.Span.End),
            string.Empty,
            "`Cast<"
            + generic.TypeArgumentList.Arguments[0]
            + ">` on a sequence that already has that "
            + "element type returns the very same object"
        );
    }

    /// <summary>
    ///     A materializer applied directly to another materializer's result.
    /// </summary>
    /// <remarks>
    ///     ⚠ The outer call is allowed to be <c>List&lt;T&gt;.ToArray()</c> as well as
    ///     <c>Enumerable.ToArray</c>, because that is what <c>xs.ToList().ToArray()</c> actually binds
    ///     to — checking only for the extension would leave the commonest spelling of this shape
    ///     unreported.
    /// </remarks>
    static void AnalyzeCopy(
        SyntaxNodeAnalysisContext context,
        INamedTypeSymbol enumerable,
        InvocationExpressionSyntax invocation,
        MemberAccessExpressionSyntax access,
        string outerName
    ) {
        if (invocation.ArgumentList.Arguments.Count != 0
            || access.Expression is not InvocationExpressionSyntax inner
            || inner.Expression is not MemberAccessExpressionSyntax {
                RawKind: (int)SyntaxKind.SimpleMemberAccessExpression
            } innerAccess
            || inner.ArgumentList.Arguments.Count != 0
            || !OrderPreservingCopies.Contains(innerAccess.Name.Identifier.ValueText)) {
            return;
        }

        var model = context.SemanticModel;
        var cancellation = context.CancellationToken;

        // The inner call has to be the framework's, or the copy it makes is somebody else's business.
        if (model.GetSymbolInfo(inner, cancellation).Symbol is not IMethodSymbol innerSymbol
            || !SymbolEqualityComparer.Default.Equals(Original(innerSymbol).ContainingType, enumerable)) {
            return;
        }

        if (model.GetSymbolInfo(invocation, cancellation).Symbol is not IMethodSymbol outerSymbol) {
            return;
        }

        var outerDefinition = Original(outerSymbol);
        var fromEnumerable = SymbolEqualityComparer.Default.Equals(outerDefinition.ContainingType, enumerable);
        var listToArray = string.Equals(outerName, "ToArray", StringComparison.Ordinal)
            && outerDefinition.ContainingType?.OriginalDefinition.ToDisplayString()
            == "System.Collections.Generic.List<T>";

        if (!fromEnumerable && !listToArray) {
            return;
        }

        // ⚠ The element type must survive the deletion. `xs.ToList().ToArray()` on a `List<Derived>`
        // is `Derived[]` either way, but an inner call that widened the element type — through a
        // covariant assignment the outer call then reads — would change what the outer one produces.
        if (!SymbolEqualityComparer.IncludeNullability.Equals(
                ElementType(model.GetTypeInfo(inner, cancellation).Type),
                ElementType(model.GetTypeInfo(access.Expression, cancellation).Type)
            )) {
            return;
        }

        Report(
            context,
            TextSpan.FromBounds(innerAccess.OperatorToken.SpanStart, inner.Span.End),
            string.Empty,
            "`"
            + innerAccess.Name.Identifier.ValueText
            + "()` copies a sequence so that `"
            + outerName
            + "()` can copy it again; nothing else can see the first copy"
        );
    }

    static void Report(SyntaxNodeAnalysisContext context, TextSpan span, string replacement, string message) {
        // ⚠ The edit deletes text. A comment or a directive inside the span is content, and deleting
        // content is not a fix.
        if (RewriteGuards.ContainsCommentOrDirective(context.Node.SyntaxTree, span)) {
            return;
        }

        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                Location.Create(context.Node.SyntaxTree, span),
                FixEdits.Pack((span, replacement)),
                message
            )
        );
    }

    static ITypeSymbol? ElementType(ITypeSymbol? type) {
        switch (type) {
            case null:
                return null;

            case IArrayTypeSymbol { IsSZArray: true } array:
                return array.ElementType;

            case INamedTypeSymbol named:
                if (named.OriginalDefinition.SpecialType
                    == SpecialType.System_Collections_Generic_IEnumerable_T) {
                    return named.TypeArguments[0];
                }

                ITypeSymbol? found = null;
                foreach (var candidate in named.AllInterfaces) {
                    if (candidate.OriginalDefinition.SpecialType
                        != SpecialType.System_Collections_Generic_IEnumerable_T) {
                        continue;
                    }

                    // Two element types and no reason to prefer either: answer null rather than
                    // guess, because the comparison this feeds has to be exact.
                    if (found is not null) {
                        return null;
                    }

                    found = candidate.TypeArguments[0];
                }

                return found;

            default:
                return null;
        }
    }

    static IMethodSymbol Original(IMethodSymbol method) => (method.ReducedFrom ?? method).OriginalDefinition;
}
