using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Rikarin.Skala.Formatting.CSharp.Arrangement;

/// <summary>
///     The member a rewritten call reaches when it is bound in place of the call the model was built for.
/// </summary>
/// <remarks>
///     A rewrite of a call's arguments is only safe if overload resolution still lands on the same member,
///     and the parameter list of the member it lands on today cannot answer that — the other overloads in
///     the group are what decide it. So the rewrite is bound speculatively at the owner's own position and
///     the caller demands the same symbol back. Shared by <see cref="ObjectCreationRule" />, which converts
///     an argument to <c>new()</c>, and <see cref="ArgumentStyleRule" />, which adds or removes its name.
/// </remarks>
static class SpeculativeRebind {
    /// <summary>
    ///     The member <paramref name="rewritten" /> reaches bound at <paramref name="owner" />'s position;
    ///     null when it reaches none or is ambiguous.
    /// </summary>
    public static ISymbol? Member(SemanticModel model, SyntaxNode owner, SyntaxNode rewritten) {
        // ⚠ `Changed?.Invoke(this, new(…))` binds only with its receiver: the invocation's
        // `.Invoke` is a member binding, which means nothing out of place, and a speculative
        // conditional access answers with no symbol at all. So the statement around it is re-bound
        // instead, and the invocation is asked inside that.
        //
        // ⚠ Anywhere inside a `?.` chain, not only directly under it. `args.FirstOrDefault(…)?[…]
        // .Split(…).Select(p => new KeyValuePair<…>(…))` has the `Select` call deep in the
        // WhenNotNull part, its leftmost receiver a member binding; bound out of place, Roslyn
        // threw a NullReferenceException (SK9095 on Testing/…/Program.cs, found by Lint's
        // self-arrange after #524).
        if (owner.Ancestors()
            .OfType<ConditionalAccessExpressionSyntax>()
            .Any(conditional => conditional.WhenNotNull.Span.Contains(owner.Span))) {
            return InStatement(model, owner, rewritten);
        }

        var info = rewritten switch {
            ConstructorInitializerSyntax initializer => model.GetSpeculativeSymbolInfo(owner.SpanStart, initializer),
            PrimaryConstructorBaseTypeSyntax baseType => model.GetSpeculativeSymbolInfo(owner.SpanStart, baseType),
            // ⚠ A target-typed outer `new(…)` has no type of its own to bind against out of place.
            // A caller that can name the type it targets hands over `new T(…)` instead.
            ImplicitObjectCreationExpressionSyntax => default,
            ExpressionSyntax expression => model.GetSpeculativeSymbolInfo(
                owner.SpanStart,
                expression,
                SpeculativeBindingOption.BindAsExpression
            ),
            _ => default
        };

        return info.CandidateSymbols.IsEmpty ? info.Symbol : null;
    }

    /// <summary>
    ///     <see cref="Member" /> for an owner that cannot be bound out of place, through a speculative
    ///     model of the whole statement holding it; null when it is not in a statement.
    /// </summary>
    static ISymbol? InStatement(SemanticModel model, SyntaxNode owner, SyntaxNode rewritten) {
        if (owner.FirstAncestorOrSelf<StatementSyntax>() is not { } statement) {
            return null;
        }

        var marker = new SyntaxAnnotation();
        var replaced = statement.ReplaceNode(owner, rewritten.WithAdditionalAnnotations(marker));
        if (!model.TryGetSpeculativeSemanticModel(statement.SpanStart, replaced, out var speculative)
            || replaced.GetAnnotatedNodes(marker).FirstOrDefault() is not { } bound) {
            return null;
        }

        var info = speculative.GetSymbolInfo(bound);
        return info.CandidateSymbols.IsEmpty ? info.Symbol : null;
    }
}
