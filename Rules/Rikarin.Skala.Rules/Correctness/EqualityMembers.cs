using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Rikarin.Skala.Rules.Correctness;

/// <summary>
///     The equality surface of one type: which equality members it declares, and which of its own
///     members each of their bodies reads.
/// </summary>
/// <remarks>
///     ⚠ <c>SK2040</c>–<c>SK2044</c> all need the same two answers —
///     <em>
///         which equality members does
///         this type have
///     </em> and <em>what state does each of them touch</em> — so the answers are
///     computed here once rather than five times slightly differently. The rules stay disjoint by
///     construction and not by filtering: each one asks this type a different question, and the
///     partition of "a member the hash code reads" between <c>SK2042</c> and <c>SK2043</c> is total
///     (see <see cref="HashCodeContract" />).
/// </remarks>
static class EqualityMembers {
    /// <summary>The <c>Equals(object)</c> override declared on this type, if it declares one.</summary>
    public static IMethodSymbol? ObjectEquals(INamedTypeSymbol type) =>
        type.GetMembers("Equals")
            .OfType<IMethodSymbol>()
            .FirstOrDefault(static method =>
                method is { IsOverride: true, IsStatic: false, Parameters.Length: 1 }
                && method.Parameters[0].Type.SpecialType == SpecialType.System_Object
                && method.ReturnType.SpecialType == SpecialType.System_Boolean
            );

    /// <summary>
    ///     Whether an <c>Equals(object)</c> override exists on the type or on a base below
    ///     <c>object</c>/<c>ValueType</c> — value semantics inherited is still value semantics.
    /// </summary>
    public static bool InheritsObjectEquals(INamedTypeSymbol type) {
        for (var current = (INamedTypeSymbol?)type;
             current is not null
             && current.SpecialType is not (SpecialType.System_Object or SpecialType.System_ValueType);
             current = current.BaseType) {
            if (ObjectEquals(current) is not null) {
                return true;
            }
        }

        return false;
    }

    /// <summary>Every <c>bool Equals(Self)</c> the type declares itself.</summary>
    public static IEnumerable<IMethodSymbol> TypedEquals(INamedTypeSymbol type) =>
        type.GetMembers("Equals")
            .OfType<IMethodSymbol>()
            .Where(method =>
                method is { IsStatic: false, IsOverride: false, Parameters.Length: 1 }
                && method.ReturnType.SpecialType == SpecialType.System_Boolean
                && SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, type)
            );

    /// <summary>The <c>GetHashCode()</c> override declared on this type, if it declares one.</summary>
    public static IMethodSymbol? HashCode(INamedTypeSymbol type) =>
        type.GetMembers("GetHashCode")
            .OfType<IMethodSymbol>()
            .FirstOrDefault(static method =>
                method is { IsOverride: true, IsStatic: false, Parameters.Length: 0 }
            );

    /// <summary>The user-defined operator of that metadata name the type declares, if any.</summary>
    public static IMethodSymbol? Operator(INamedTypeSymbol type, string name) =>
        type.GetMembers(name)
            .OfType<IMethodSymbol>()
            .FirstOrDefault(static method => method.MethodKind == MethodKind.UserDefinedOperator);

    /// <summary>Whether the type implements <c>IEquatable&lt;Self&gt;</c>.</summary>
    public static bool ImplementsEquatable(INamedTypeSymbol type, Compilation compilation) {
        var equatable = compilation.GetTypeByMetadataName("System.IEquatable`1");
        return equatable is not null
            && type.AllInterfaces.Any(contract =>
                SymbolEqualityComparer.Default.Equals(contract.OriginalDefinition, equatable)
                && SymbolEqualityComparer.Default.Equals(contract.TypeArguments[0], type)
            );
    }

    /// <summary>
    ///     Whether the type's base list bound at all — no error type among its bases or its
    ///     interfaces.
    /// </summary>
    /// <remarks>
    ///     ⚠
    ///     <b>
    ///         This is not defensive tidiness; without it the rules report the opposite of the
    ///         truth.
    ///     </b> Measured on the reference trees: <c>Vixen.Raven</c>'s <c>BufferTypeSymbol</c>
    ///     declares <c>IEquatable&lt;BufferTypeSymbol&gt;</c> in its base list, and in a compilation
    ///     without the SDK's implicit global usings that name binds to an <em>error</em> type — so
    ///     <c>AllInterfaces</c> holds <c>IEquatable&lt;&gt;</c>, the comparison against
    ///     <c>System.IEquatable`1</c> fails, and <c>SK2044</c> reports a type for not implementing
    ///     the interface it does implement. A base that did not bind is a base nobody read, and a
    ///     rule that reads it anyway is inventing an answer. Same discipline as <c>SK7080</c>'s
    ///     "an error type anywhere on the chain withdraws the measurement".
    /// </remarks>
    public static bool BindsCompletely(INamedTypeSymbol type) {
        if (type.BaseType is { TypeKind: TypeKind.Error }) {
            return false;
        }

        foreach (var contract in type.AllInterfaces) {
            if (contract.TypeKind == TypeKind.Error || contract.IsUnboundGenericType) {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    ///     ⚠ <c>SK2004</c>'s exact precondition, so the rules in this file can hold off the span it
    ///     already reports.
    /// </summary>
    /// <remarks>
    ///     <c>SK2004</c> reports a type implementing <c>IEquatable&lt;Self&gt;</c> with no
    ///     <c>Equals(object)</c> override. Two rules arguing over one declaration is worse than one
    ///     rule saying less, so <c>SK2044</c> asks this first and stays quiet where the answer is yes.
    /// </remarks>
    public static bool IsReportedByIncompleteEqualityContract(INamedTypeSymbol type, Compilation compilation) =>
        ImplementsEquatable(type, compilation) && !InheritsObjectEquals(type);

    /// <summary>
    ///     Whether the node sits inside a member whose job is equality — where an identity comparison
    ///     is a deliberate short circuit rather than a mistake.
    /// </summary>
    public static bool InsideAnEqualityMember(SyntaxNode node) {
        for (var current = node; current is not null; current = current.Parent) {
            switch (current) {
                case MethodDeclarationSyntax method:
                    var name = method.Identifier.ValueText;
                    return name is "Equals" or "GetHashCode" or "CompareTo" or "ReferenceEquals";
                case OperatorDeclarationSyntax @operator:
                    return @operator.OperatorToken.IsKind(SyntaxKind.EqualsEqualsToken)
                        || @operator.OperatorToken.IsKind(SyntaxKind.ExclamationEqualsToken);
                case ConversionOperatorDeclarationSyntax:
                case PropertyDeclarationSyntax:
                case ConstructorDeclarationSyntax:
                    return false;
            }
        }

        return false;
    }

    /// <summary>
    ///     Maps every member of the type onto one canonical symbol, so that a property and the field
    ///     behind it are the same piece of state.
    /// </summary>
    /// <remarks>
    ///     ⚠ Without this the member-set comparison is nonsense in the direction that produces wrong
    ///     findings: an <c>Equals</c> reading <c>Name</c> and a <c>GetHashCode</c> reading <c>name</c>
    ///     would look like two disjoint sets and <c>SK2042</c> would report every hand-written
    ///     property in the repository. An auto-property canonicalises to the property, because the
    ///     backing field has no name anybody can write; a hand-written property whose getter returns
    ///     exactly one instance field canonicalises to that field, because both spellings occur.
    /// </remarks>
    public static Dictionary<ISymbol, ISymbol> Canonicalisation(
        INamedTypeSymbol type,
        SemanticModel model,
        CancellationToken cancellation
    ) {
        var map = new Dictionary<ISymbol, ISymbol>(SymbolEqualityComparer.Default);
        foreach (var member in type.GetMembers()) {
            switch (member) {
                case IFieldSymbol { AssociatedSymbol: IPropertySymbol property }:
                    map[member] = property;
                    break;
                case IPropertySymbol { IsStatic: false, IsIndexer: false } declared:
                    if (BackingField(declared, model, cancellation) is { } field) {
                        map[declared] = field;
                    }

                    break;
            }
        }

        return map;
    }

    /// <summary>The single instance field a hand-written property's getter returns, if it is that simple.</summary>
    static IFieldSymbol? BackingField(IPropertySymbol property, SemanticModel model, CancellationToken cancellation) {
        if (property.DeclaringSyntaxReferences.Length != 1
            || property.DeclaringSyntaxReferences[0].GetSyntax(cancellation) is not PropertyDeclarationSyntax syntax
            || syntax.SyntaxTree != model.SyntaxTree) {
            return null;
        }

        var body = syntax.ExpressionBody?.Expression;
        if (body is null) {
            var getter = syntax.AccessorList?.Accessors
                .FirstOrDefault(static accessor => accessor.IsKind(SyntaxKind.GetAccessorDeclaration));
            body = getter is null ? null : Returned(getter);
        }

        if (body is null) {
            return null;
        }

        return model.GetSymbolInfo(body, cancellation).Symbol is IFieldSymbol { IsStatic: false } resolved
            && SymbolEqualityComparer.Default.Equals(resolved.ContainingType, property.ContainingType)
                ? resolved
                : null;
    }

    static ExpressionSyntax? Returned(AccessorDeclarationSyntax accessor) {
        if (accessor.ExpressionBody?.Expression is { } expression) {
            return expression;
        }

        var statements = accessor.Body?.Statements;
        return statements is { Count: 1 } && statements.Value[0] is ReturnStatementSyntax returned
            ? returned.Expression
            : null;
    }

    /// <summary>
    ///     Every instance field or property of <paramref name="type" /> that the method's body reads,
    ///     canonicalised — or <see langword="null" /> when the body could not be read completely.
    /// </summary>
    /// <remarks>
    ///     ⚠ <b>The null answer is the rule.</b> A body that hands the comparison to a helper this
    ///     analysis cannot see would produce an <em>under</em>-counted equality set, and an
    ///     under-counted equality set is exactly what makes <c>SK2042</c> report a member the type
    ///     does compare. Recall is the cheap thing to give up here; a wrong finding about equality
    ///     sends somebody to read equality code that was correct.
    /// </remarks>
    public static Dictionary<ISymbol, Location>? Reads(
        INamedTypeSymbol type,
        IMethodSymbol method,
        SemanticModel model,
        IReadOnlyList<IMethodSymbol> permitted,
        Dictionary<ISymbol, ISymbol> canonical,
        CancellationToken cancellation
    ) {
        if (method.DeclaringSyntaxReferences.Length != 1
            || method.DeclaringSyntaxReferences[0].GetSyntax(cancellation) is not MethodDeclarationSyntax syntax
            || syntax.SyntaxTree != model.SyntaxTree) {
            return null;
        }

        SyntaxNode? body = syntax.Body;
        body ??= syntax.ExpressionBody?.Expression;
        if (body is null) {
            return null;
        }

        var found = new Dictionary<ISymbol, Location>(SymbolEqualityComparer.Default);
        return Walk(body, type, model, permitted, canonical, found, cancellation) ? found : null;
    }

    static bool Walk(
        SyntaxNode node,
        INamedTypeSymbol type,
        SemanticModel model,
        IReadOnlyList<IMethodSymbol> permitted,
        Dictionary<ISymbol, ISymbol> canonical,
        Dictionary<ISymbol, Location> found,
        CancellationToken cancellation
    ) {
        cancellation.ThrowIfCancellationRequested();

        // ⚠ `nameof(x)` names a member and reads nothing. Counting it would put a member into the
        // equality set that equality never looked at, which silences a real finding.
        if (node is InvocationExpressionSyntax {
                Expression: IdentifierNameSyntax { Identifier.ValueText: "nameof" }
            }) {
            return true;
        }

        if (node is InvocationExpressionSyntax invocation
            && model.GetSymbolInfo(invocation, cancellation).Symbol is IMethodSymbol target
            && target.MethodKind != MethodKind.LocalFunction
            && SymbolEqualityComparer.Default.Equals(target.ContainingType?.OriginalDefinition, type.OriginalDefinition)
            && !permitted.Any(allowed =>
                SymbolEqualityComparer.Default.Equals(allowed.OriginalDefinition, target.OriginalDefinition)
            )) {
            return false;
        }

        if (node is SimpleNameSyntax
            && model.GetSymbolInfo(node, cancellation).Symbol is { IsStatic: false } symbol
            && symbol is IFieldSymbol or IPropertySymbol
            && SymbolEqualityComparer.Default.Equals(
                symbol.ContainingType?.OriginalDefinition,
                type.OriginalDefinition
            )) {
            var member = canonical.TryGetValue(symbol, out var mapped) ? mapped : symbol;
            if (!found.ContainsKey(member)) {
                found[member] = node.GetLocation();
            }
        }

        foreach (var child in node.ChildNodes()) {
            if (!Walk(child, type, model, permitted, canonical, found, cancellation)) {
                return false;
            }
        }

        return true;
    }
}
