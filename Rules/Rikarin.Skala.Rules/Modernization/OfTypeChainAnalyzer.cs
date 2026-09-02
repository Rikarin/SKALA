using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Rules.Metadata;
using System;
using System.Collections.Immutable;
using System.Threading;

namespace Rikarin.Skala.Rules.Modernization;

/// <summary>
///     <c>SK1080</c> — <c>xs.Where(x =&gt; x is T).Cast&lt;T&gt;()</c> is <c>xs.OfType&lt;T&gt;()</c>.
/// </summary>
/// <remarks>
///     <para>
///         <c>OfType&lt;T&gt;</c> <em>is</em> the filter and the cast, and the long form is those two
///         steps written where a reader has to check that they agree with each other. When they stop
///         agreeing — a predicate testing one type and a <c>Cast</c> naming another — the second one
///         throws on an element the first was supposed to have removed, which is the failure the single
///         operator cannot have.
///     </para>
///     <para>
///         ⚠ <b>Null is what makes the two forms identical rather than merely similar.</b> <c>x is T</c>
///         is false for a null element, so nothing null survives the <c>Where</c> and the
///         <c>Cast&lt;T&gt;</c> never sees one; <c>OfType&lt;T&gt;</c> drops nulls for exactly the same
///         reason. A bare <c>Cast&lt;T&gt;</c> would pass null through, which is why this rewrite is only
///         sound behind the type test and is not a general <c>Cast</c> → <c>OfType</c> rule.
///     </para>
///     <para>
///         ⚠ <c>x =&gt; x as T</c> is deliberately not the <c>Select</c> shape this accepts. <c>as</c>
///         yields null where the cast would throw, so on a sequence the filter did not fully clean, the
///         two spellings produce sequences of different lengths.
///     </para>
///     <para>
///         ⚠ <c>SK4010</c> folds a <c>Where</c> predicate into nine consuming operators and this folds it
///         into <c>Cast</c> and <c>Select</c>. The two consumer sets are disjoint by construction, so the
///         two rules can never report the same span.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class OfTypeChainAnalyzer : DiagnosticAnalyzer {
    static readonly RuleInfo Rule = RuleCatalog.Get(RuleIds.OfTypeOverFilterAndCast);
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.OfTypeOverFilterAndCast);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(static start => {
                if (!SkalaRule.MeetsLanguageVersion(start.Compilation, Rule.LanguageVersion)) {
                    return;
                }

                // ⚠ The fix writes `OfType<T>()`, so the compilation's own `Enumerable` has to declare
                // it. This ships as a netstandard2.0 analyzer and runs against whatever framework the
                // project targets; a name that happens to match is not a proof that the call the fix
                // writes will bind.
                var enumerable = start.Compilation.GetTypeByMetadataName("System.Linq.Enumerable");
                if (enumerable is null || !HasProjection(enumerable, "OfType")) {
                    return;
                }

                start.RegisterSyntaxNodeAction(
                    context => Analyze(context, enumerable),
                    SyntaxKind.InvocationExpression
                );
            }
        );
    }

    /// <summary>Whether <c>Enumerable</c> declares <c>name&lt;T&gt;(IEnumerable source)</c>.</summary>
    static bool HasProjection(INamedTypeSymbol enumerable, string name) {
        foreach (var member in enumerable.GetMembers(name)) {
            if (member is IMethodSymbol { IsStatic: true, Arity: 1, Parameters.Length: 1 }) {
                return true;
            }
        }

        return false;
    }

    static void Analyze(SyntaxNodeAnalysisContext context, INamedTypeSymbol enumerable) {
        var outer = (InvocationExpressionSyntax)context.Node;

        // ⚠ Plain member access at both levels. `xs?.Where(p).Cast<T>()` binds through a
        // MemberBindingExpression, and collapsing that means moving the binding rather than replacing
        // a name — a different edit, so not this rule's.
        if (outer.Expression is not MemberAccessExpressionSyntax {
                RawKind: (int)SyntaxKind.SimpleMemberAccessExpression
            } consumerAccess
            || consumerAccess.Expression is not InvocationExpressionSyntax filter
            || filter.Expression is not MemberAccessExpressionSyntax {
                RawKind: (int)SyntaxKind.SimpleMemberAccessExpression
            } filterAccess
            || !string.Equals(filterAccess.Name.Identifier.ValueText, "Where", StringComparison.Ordinal)
            || filter.ArgumentList.Arguments.Count != 1) {
            return;
        }

        var model = context.SemanticModel;
        var cancellation = context.CancellationToken;

        // ⚠ The `Func<T, int, bool>` overload has no counterpart in the single-operator form: the
        // index it hands the predicate does not survive the fold. Two parameters alone would not tell
        // the two overloads apart — the predicate's shape is what does.
        if (model.GetSymbolInfo(filter, cancellation).Symbol is not IMethodSymbol where
            || Original(where) is not { Parameters.Length: 2 } whereDefinition
            || !SymbolEqualityComparer.Default.Equals(whereDefinition.ContainingType, enumerable)
            || !IsPredicate(whereDefinition.Parameters[1].Type)) {
            return;
        }

        var (target, written) = Consumed(consumerAccess, outer, enumerable, model, cancellation);
        if (target is null || written is null) {
            return;
        }

        if (TestedType(filter.ArgumentList.Arguments[0].Expression, model, cancellation) is not { } tested
            || !SymbolEqualityComparer.Default.Equals(tested, target)) {
            return;
        }

        // The whole chain from `Where` to the end of the consuming call becomes one operator. The
        // type argument is carried across as *source text*, so an alias or a `using`-shortened name
        // stays spelled the way the file spells it and no directive is needed that the file lacks.
        var span = TextSpan.FromBounds(filterAccess.Name.SpanStart, outer.Span.End);
        if (RewriteGuards.ContainsCommentOrDirectiveWithinTheEdit(outer.SyntaxTree, span)) {
            return;
        }

        var replacement = "OfType<" + written + ">()";
        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                Location.Create(outer.SyntaxTree, span),
                FixEdits.Pack((span, replacement)),
                "The type test and the cast are one operator: `" + replacement + "`"
            )
        );
    }

    /// <summary>
    ///     The type this chain casts to and how the file spells it, when the consuming call is one of
    ///     the two accepted spellings.
    /// </summary>
    /// <remarks>
    ///     ⚠ <c>Cast&lt;T&gt;()</c> and <c>Select(x =&gt; (T)x)</c> only. <c>Select(x =&gt; x as T)</c>
    ///     is a different sequence and <c>OfType</c> is not it.
    /// </remarks>
    static (ITypeSymbol? Target, string? Written) Consumed(
        MemberAccessExpressionSyntax consumerAccess,
        InvocationExpressionSyntax outer,
        INamedTypeSymbol enumerable,
        SemanticModel model,
        CancellationToken cancellation
    ) {
        var name = consumerAccess.Name.Identifier.ValueText;
        if (model.GetSymbolInfo(outer, cancellation).Symbol is not IMethodSymbol consumer) {
            return (null, null);
        }

        var definition = Original(consumer);
        if (!SymbolEqualityComparer.Default.Equals(definition.ContainingType, enumerable)) {
            return (null, null);
        }

        if (string.Equals(name, "Cast", StringComparison.Ordinal)) {
            if (outer.ArgumentList.Arguments.Count != 0
                || definition.Parameters.Length != 1
                || consumerAccess.Name is not GenericNameSyntax { TypeArgumentList.Arguments.Count: 1 } cast) {
                return (null, null);
            }

            var argument = cast.TypeArgumentList.Arguments[0];
            return (model.GetTypeInfo(argument, cancellation).Type, argument.ToString());
        }

        if (!string.Equals(name, "Select", StringComparison.Ordinal)
            || outer.ArgumentList.Arguments.Count != 1
            || definition.Parameters.Length != 2
            || !IsProjection(definition.Parameters[1].Type)) {
            return (null, null);
        }

        if (Lambda(outer.ArgumentList.Arguments[0].Expression) is not { } projection
            || projection.Body is not CastExpressionSyntax written
            || written.Expression is not IdentifierNameSyntax operand
            || !string.Equals(operand.Identifier.ValueText, projection.Parameter, StringComparison.Ordinal)) {
            return (null, null);
        }

        return (model.GetTypeInfo(written.Type, cancellation).Type, written.Type.ToString());
    }

    /// <summary>
    ///     The type a one-parameter lambda tests its own parameter against with <c>is</c>, or null.
    /// </summary>
    /// <remarks>
    ///     ⚠ The body must be the whole test and nothing else. <c>x =&gt; x is T &amp;&amp; x.Ok</c>
    ///     selects fewer elements than <c>OfType&lt;T&gt;</c> does, and <c>x =&gt; y is T</c> tests
    ///     something the single operator cannot reach. A declaration pattern — <c>x is T t</c>, which
    ///     parses as an <c>IsPatternExpression</c> rather than an <c>IsExpression</c> — is refused with
    ///     it: the name it introduces would have nowhere to go once the lambda is deleted.
    /// </remarks>
    static ITypeSymbol? TestedType(ExpressionSyntax argument, SemanticModel model, CancellationToken cancellation) {
        if (Lambda(argument) is not { } lambda
            || lambda.Body is not BinaryExpressionSyntax { RawKind: (int)SyntaxKind.IsExpression } test
            || test.Left is not IdentifierNameSyntax operand
            || !string.Equals(operand.Identifier.ValueText, lambda.Parameter, StringComparison.Ordinal)
            || test.Right is not TypeSyntax type) {
            return null;
        }

        return model.GetTypeInfo(type, cancellation).Type;
    }

    /// <summary>The parameter name and expression body of a one-parameter lambda, or null.</summary>
    static LambdaShape? Lambda(ExpressionSyntax expression) =>
        expression switch {
            SimpleLambdaExpressionSyntax { ExpressionBody: { } body } simple =>
                new LambdaShape(simple.Parameter.Identifier.ValueText, body),
            ParenthesizedLambdaExpressionSyntax {
                ExpressionBody: { } body,
                ParameterList.Parameters.Count: 1
            } parenthesized =>
                new LambdaShape(parenthesized.ParameterList.Parameters[0].Identifier.ValueText, body),
            ParenthesizedExpressionSyntax inner => Lambda(inner.Expression),
            _ => null
        };

    static bool IsPredicate(ITypeSymbol type) =>
        type is INamedTypeSymbol { Name: "Func", TypeArguments.Length: 2 } func
        && func.TypeArguments[1].SpecialType == SpecialType.System_Boolean;

    static bool IsProjection(ITypeSymbol type) => type is INamedTypeSymbol { Name: "Func", TypeArguments.Length: 2 };

    /// <summary>
    ///     The method as <c>Enumerable</c> declares it, whether it was called as an extension or not.
    /// </summary>
    /// <remarks>
    ///     ⚠ <c>Enumerable.Where(xs, p)</c> and <c>xs.Where(p)</c> are the same call. <c>ReducedFrom</c>
    ///     is what makes the extension form's parameter count comparable to the static form's — without
    ///     it every extension-spelled call is one parameter short and every guard reading a parameter
    ///     index is reading the wrong one.
    /// </remarks>
    static IMethodSymbol Original(IMethodSymbol method) => (method.ReducedFrom ?? method).OriginalDefinition;

    readonly record struct LambdaShape(string Parameter, ExpressionSyntax Body);
}
