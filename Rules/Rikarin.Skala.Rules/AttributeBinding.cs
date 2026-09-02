using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Rikarin.Skala.Rules;

/// <summary>
///     Binding an attribute written in source to the well-known type it names, and reading one of its
///     named arguments.
/// </summary>
/// <remarks>
///     ⚠ Shared rather than duplicated: <c>SK7051</c> asks it of
///     <c>SuppressMessageAttribute</c>/<c>UnconditionalSuppressMessageAttribute</c> and <c>SK8006</c>
///     of <c>FactAttribute</c>/<c>TheoryAttribute</c>, and both wrote the same two steps out by hand.
///     The pair of nulls is the part worth having in one place — a compilation that references neither
///     type resolves both to null, and
///     <see cref="SymbolEqualityComparer" /> compares a real containing type unequal to null, so the
///     bind fails closed. A copy that reordered those clauses would report every attribute in a
///     compilation that does not reference the framework at all.
/// </remarks>
internal static class AttributeBinding {
    /// <summary>Whether the attribute's constructor belongs to either named type.</summary>
    /// <remarks>
    ///     Either symbol may be null — the compilation does not reference that framework — and a null
    ///     never matches.
    /// </remarks>
    public static bool Matches(
        SyntaxNodeAnalysisContext context,
        AttributeSyntax attribute,
        INamedTypeSymbol? first,
        INamedTypeSymbol? second
    ) =>
        context.SemanticModel.GetSymbolInfo(attribute, context.CancellationToken).Symbol
        is IMethodSymbol constructor
        && (SymbolEqualityComparer.Default.Equals(constructor.ContainingType, first)
            || SymbolEqualityComparer.Default.Equals(constructor.ContainingType, second));

    /// <summary>The <c>Name = …</c> argument the attribute writes, or null when it writes none.</summary>
    /// <remarks>
    ///     ⚠ A loop rather than <c>FirstOrDefault</c>: the predicate would have to close over
    ///     <paramref name="name" />, and this runs on every attribute in the tree.
    /// </remarks>
    public static AttributeArgumentSyntax? NamedArgument(AttributeSyntax attribute, string name) {
        if (attribute.ArgumentList is not { } arguments) {
            return null;
        }

        foreach (var argument in arguments.Arguments) {
            if (argument.NameEquals?.Name.Identifier.ValueText == name) {
                return argument;
            }
        }

        return null;
    }
}
