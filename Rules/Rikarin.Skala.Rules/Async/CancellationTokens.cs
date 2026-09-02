using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System;

namespace Rikarin.Skala.Rules.Async;

/// <summary>
///     The questions <c>SK3004</c> and <c>SK3051</c> both ask about a call and a token.
/// </summary>
/// <remarks>
///     ⚠ Shared rather than duplicated, for the reason <see cref="AsyncContext" /> gives: the two rules
///     are the same argument at two points in the call graph — <c>SK3051</c> reports a method with no
///     token to forward, <c>SK3004</c> reports the forwarding once there is one — so a call one of them
///     counts as "wants a token" and the other does not is a pair that contradicts itself. Applying
///     <c>SK3051</c>'s fix has to make <c>SK3004</c> reachable on the same call, and that only holds if
///     both read the call the same way.
/// </remarks>
internal static class CancellationTokens {
    /// <summary>Whether the written type could be <c>CancellationToken</c>, by its last name.</summary>
    /// <remarks>
    ///     ⚠ The name in the source before the symbol behind it, and it is worth 300 ms of the 330
    ///     <c>SK3004</c> cost before the order was measured (docs/plan/13 § "Analysis"). This runs on
    ///     every invocation in the tree, and where most methods take no token, resolving every parameter
    ///     to ask is the whole cost of the rule.
    ///     <para>
    ///         ⚠ The symbol still decides; the text only decides whether to ask. What it costs is a
    ///         method whose token parameter is written through a <c>using</c> alias, which the filter
    ///         reads as some other type and the rule then never sees. That is a missed finding rather
    ///         than a wrong one, and it is the direction both rules err in everywhere else too.
    ///     </para>
    /// </remarks>
    public static bool NamesCancellationToken(TypeSyntax? type) =>
        type switch {
            IdentifierNameSyntax name => string.Equals(
                name.Identifier.ValueText,
                "CancellationToken",
                StringComparison.Ordinal
            ),
            QualifiedNameSyntax qualified => NamesCancellationToken(qualified.Right),
            AliasQualifiedNameSyntax aliased => NamesCancellationToken(aliased.Name),
            NullableTypeSyntax nullable => NamesCancellationToken(nullable.ElementType),
            _ => false
        };

    /// <summary>
    ///     The one <c>CancellationToken</c> parameter in scope, or null when there is none or several.
    /// </summary>
    /// <remarks>
    ///     ⚠ Several is not a harder case; it is a different one. Which token an inner call should get
    ///     when two are in scope is a decision about intent, and a rule that picks is a rule that is
    ///     sometimes silently wrong rather than sometimes silent.
    ///     <para>
    ///         ⚠ Null therefore means two different things — "no token" and "too many to choose from" —
    ///         and <c>SK3051</c> needs them apart, which is what <see cref="CountInScope" /> is for.
    ///     </para>
    /// </remarks>
    public static string? TokenInScope(
        SemanticModel model,
        SyntaxNode node,
        INamedTypeSymbol tokenType,
        System.Threading.CancellationToken cancellation
    ) {
        string? found = null;
        foreach (var parameter in Enclosing(node)) {
            if (!NamesCancellationToken(parameter.Type)
                || model.GetDeclaredSymbol(parameter, cancellation) is not { } symbol
                || !SymbolEqualityComparer.Default.Equals(symbol.Type, tokenType)) {
                continue;
            }

            if (found is not null) {
                return null;
            }

            found = parameter.Identifier.ValueText;
        }

        return found;
    }

    /// <summary>How many <c>CancellationToken</c> parameters the enclosing bodies declare.</summary>
    public static int CountInScope(
        SemanticModel model,
        SyntaxNode node,
        INamedTypeSymbol tokenType,
        System.Threading.CancellationToken cancellation
    ) {
        var count = 0;
        foreach (var parameter in Enclosing(node)) {
            if (NamesCancellationToken(parameter.Type)
                && model.GetDeclaredSymbol(parameter, cancellation) is { } symbol
                && SymbolEqualityComparer.Default.Equals(symbol.Type, tokenType)) {
                count++;
            }
        }

        return count;
    }

    /// <summary>Every parameter of every body enclosing <paramref name="node" />, out to its member.</summary>
    static System.Collections.Generic.IEnumerable<ParameterSyntax> Enclosing(SyntaxNode node) {
        for (var current = node; current is not null; current = current.Parent) {
            var parameters = current switch {
                MethodDeclarationSyntax method => method.ParameterList.Parameters,
                LocalFunctionStatementSyntax local => local.ParameterList.Parameters,
                ParenthesizedLambdaExpressionSyntax lambda => lambda.ParameterList.Parameters,
                _ => default
            };

            foreach (var parameter in parameters) {
                yield return parameter;
            }

            if (current is MemberDeclarationSyntax) {
                break;
            }
        }
    }

    /// <summary>An optional <c>CancellationToken</c> parameter, which a named argument can fill.</summary>
    public static IParameterSymbol? Omitted(IMethodSymbol target, INamedTypeSymbol tokenType) {
        foreach (var parameter in target.Parameters) {
            if (parameter.IsOptional && SymbolEqualityComparer.Default.Equals(parameter.Type, tokenType)) {
                return parameter;
            }
        }

        return null;
    }

    /// <summary>
    ///     Whether a sibling overload is this method's parameter list with a token appended.
    /// </summary>
    /// <remarks>
    ///     ⚠ Appended, and nothing else changed. That is what makes appending an argument select it:
    ///     any other difference and the rule would be guessing at overload resolution, which is how a
    ///     fix comes to call a different method than the one it was reported against.
    /// </remarks>
    public static bool HasAppendedOverload(IMethodSymbol target, INamedTypeSymbol tokenType) {
        foreach (var member in target.ContainingType.GetMembers(target.Name)) {
            if (member is not IMethodSymbol candidate
                || SymbolEqualityComparer.Default.Equals(candidate, target)
                || candidate.Parameters.Length != target.Parameters.Length + 1
                || candidate.TypeParameters.Length != target.TypeParameters.Length
                || candidate.IsStatic != target.IsStatic
                || !SymbolEqualityComparer.Default.Equals(
                    candidate.Parameters[candidate.Parameters.Length - 1].Type,
                    tokenType
                )) {
                continue;
            }

            var matches = true;
            for (var i = 0; i < target.Parameters.Length; i++) {
                if (target.Parameters[i].RefKind != candidate.Parameters[i].RefKind
                    || !SymbolEqualityComparer.Default.Equals(
                        target.Parameters[i].Type,
                        candidate.Parameters[i].Type
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

    /// <summary>
    ///     Whether the call already hands the callee a token, positionally or by name.
    /// </summary>
    /// <remarks>
    ///     ⚠ Includes <c>CancellationToken.None</c> and <c>default</c>. Writing the token out is how an
    ///     author says a call is deliberately not cancellable; a rule that overrides that is arguing
    ///     with a decision rather than finding an omission.
    /// </remarks>
    public static bool Supplies(
        SeparatedSyntaxList<ArgumentSyntax> arguments,
        IMethodSymbol target,
        INamedTypeSymbol tokenType
    ) {
        for (var i = 0; i < arguments.Count; i++) {
            var argument = arguments[i];
            if (argument.NameColon is { } name) {
                foreach (var parameter in target.Parameters) {
                    if (string.Equals(parameter.Name, name.Name.Identifier.ValueText, StringComparison.Ordinal)
                        && SymbolEqualityComparer.Default.Equals(parameter.Type, tokenType)) {
                        return true;
                    }
                }

                continue;
            }

            if (i < target.Parameters.Length
                && SymbolEqualityComparer.Default.Equals(target.Parameters[i].Type, tokenType)) {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    ///     Whether every parameter is filled positionally, so appending one more selects the overload.
    /// </summary>
    public static bool AllPositional(SeparatedSyntaxList<ArgumentSyntax> arguments, IMethodSymbol target) {
        if (arguments.Count != target.Parameters.Length) {
            return false;
        }

        foreach (var argument in arguments) {
            if (argument.NameColon is not null || argument.RefKindKeyword.RawKind != (int)SyntaxKind.None) {
                return false;
            }
        }

        foreach (var parameter in target.Parameters) {
            if (parameter.IsParams || parameter.RefKind != RefKind.None) {
                return false;
            }
        }

        return true;
    }

    /// <summary>The edit that hands one call a token, and the callee it names.</summary>
    public readonly struct Forward {
        public Forward(TextSpan span, string text, string callee) {
            Span = span;
            Text = text;
            Callee = callee;
        }

        public TextSpan Span { get; }

        public string Text { get; }

        public string Callee { get; }
    }

    /// <summary>
    ///     The edit that passes <paramref name="token" /> to this call, or null when there is none.
    /// </summary>
    /// <remarks>
    ///     ⚠ The two shapes <c>SK3004</c> can repair, and no third: an omitted <em>optional</em>
    ///     parameter, or an overload that is this parameter list with a token appended and every
    ///     argument positional. Anywhere else the rule would have to choose between overloads, and a fix
    ///     that changes which method is called is not a fix.
    ///     <para>
    ///         ⚠ <b>The edit, not just the verdict, and both rules take it from here (#328).</b>
    ///         <c>SK3051</c> used to answer only "would this call have taken a token", append a
    ///         parameter, and stop — which produced a signature advertising a cancellation the body
    ///         dropped. Its fix now emits one of these per call in the body, so "wants a token" and
    ///         "here is the argument that gives it one" cannot drift apart between the two rules.
    ///     </para>
    /// </remarks>
    public static Forward? Forwarding(
        SemanticModel model,
        InvocationExpressionSyntax invocation,
        INamedTypeSymbol tokenType,
        string token,
        System.Threading.CancellationToken cancellation
    ) {
        if (model.GetSymbolInfo(invocation, cancellation).Symbol
            is not IMethodSymbol { MethodKind: MethodKind.Ordinary or MethodKind.ReducedExtension } target) {
            return null;
        }

        var arguments = invocation.ArgumentList.Arguments;
        if (Supplies(arguments, target, tokenType)) {
            return null;
        }

        var argument = Omitted(target, tokenType) is { } optional
            ? optional.Name + ": " + token
            : HasAppendedOverload(target, tokenType) && AllPositional(arguments, target)
                ? token
                : null;

        if (argument is null) {
            return null;
        }

        var list = invocation.ArgumentList;
        return arguments.Count == 0
            ? new Forward(new TextSpan(list.CloseParenToken.SpanStart, 0), argument, target.Name)
            : new Forward(
                new TextSpan(arguments[arguments.Count - 1].Span.End, 0),
                ", " + argument,
                target.Name
            );
    }

    /// <summary>
    ///     Whether this call would take a <c>CancellationToken</c> and was not given one.
    /// </summary>
    public static bool WantsAToken(
        SemanticModel model,
        InvocationExpressionSyntax invocation,
        INamedTypeSymbol tokenType,
        System.Threading.CancellationToken cancellation
    ) =>
        Forwarding(model, invocation, tokenType, "cancellationToken", cancellation) is not null;
}
