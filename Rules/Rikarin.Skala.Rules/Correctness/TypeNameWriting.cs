using Microsoft.CodeAnalysis;
using System.Linq;

namespace Rikarin.Skala.Rules.Correctness;

/// <summary>
///     Writing a type into a fix so that it compiles where the fix puts it.
/// </summary>
/// <remarks>
///     ⚠ The short name is only correct where it already binds to that type at that position. A file
///     with no matching <c>using</c>, or one where the simple name is taken by something else, needs
///     the fully qualified spelling — and <c>SK2150</c> learned the same thing about
///     <c>StringComparison</c>. Both rules that use this replace a type *name* in source, so an edit
///     that does not bind is an edit that breaks the build on the tool's advice.
/// </remarks>
internal static class TypeNameWriting {
    static readonly SymbolDisplayFormat Qualified =
        SymbolDisplayFormat.FullyQualifiedFormat.WithGlobalNamespaceStyle(SymbolDisplayGlobalNamespaceStyle.Omitted);

    /// <summary>The shortest spelling of <paramref name="type" /> that binds to it at a position.</summary>
    public static string At(ITypeSymbol type, SemanticModel model, int position) {
        var minimal = type.ToMinimalDisplayString(model, position);
        return Binds(type, model, position, minimal) ? minimal : type.ToDisplayString(Qualified);
    }

    static bool Binds(ITypeSymbol type, SemanticModel model, int position, string written) {
        // A dotted or generic spelling is not a simple name and cannot be checked by lookup; the
        // minimal form is already qualified enough in that case.
        // ⚠ netstandard2.0: no `System.Range`, so this is `Substring` and not a range expression.
        var head = written;
        var cut = head.IndexOfAny(new[] { '.', '<' });
        if (cut >= 0) {
            head = head.Substring(0, cut);
        }

        return model.LookupNamespacesAndTypes(position, name: head)
            .Any(symbol => SymbolEqualityComparer.Default.Equals(Unconstructed(symbol), Unconstructed(type)));
    }

    static ISymbol Unconstructed(ISymbol symbol) =>
        symbol is INamedTypeSymbol named ? named.OriginalDefinition : symbol;
}
