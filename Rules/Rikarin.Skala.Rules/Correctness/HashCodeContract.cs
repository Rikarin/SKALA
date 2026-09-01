using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
using System.Threading;

namespace Rikarin.Skala.Rules.Correctness;

/// <summary>
///     The members one type's <c>GetHashCode</c> reads, split into the two halves <c>SK2042</c> and
///     <c>SK2043</c> report.
/// </summary>
/// <remarks>
///     ⚠ <b>The split is what keeps the two rules off each other's spans, and it is a partition
///     rather than a filter.</b> A member the hash code reads is either compared by the type's
///     equality — and then the only open question is whether it can change, which is
///     <c>SK2043</c> — or it is not compared, and then the finding is <c>SK2042</c>'s regardless of
///     mutability. Every hashed member falls in exactly one half, both rules compute the halves with
///     this one implementation, and neither looks at the other's output. A fixture that a filter
///     would have let both rules see produces one finding here because there is only one half it can
///     be in.
/// </remarks>
readonly struct HashCodeContract {
    HashCodeContract(
        Dictionary<ISymbol, Location> uncompared,
        Dictionary<ISymbol, Location> compared,
        TypeDeclarationSyntax declaration,
        Dictionary<ISymbol, ISymbol> canonical
    ) {
        Uncompared = uncompared;
        Compared = compared;
        Declaration = declaration;
        Canonical = canonical;
        Valid = true;
    }

    /// <summary>Whether the type could be read at all. A false answer is never a finding either way.</summary>
    public bool Valid { get; }

    /// <summary>Hashed members no <c>Equals</c> compares — <c>SK2042</c>'s half.</summary>
    public Dictionary<ISymbol, Location> Uncompared { get; }

    /// <summary>Hashed members equality does compare — <c>SK2043</c>'s half.</summary>
    public Dictionary<ISymbol, Location> Compared { get; }

    public TypeDeclarationSyntax Declaration { get; }

    public Dictionary<ISymbol, ISymbol> Canonical { get; }

    /// <summary>
    ///     Resolves the contract for the type this declaration declares, or an invalid result.
    /// </summary>
    public static HashCodeContract Resolve(
        TypeDeclarationSyntax declaration,
        SemanticModel model,
        CancellationToken cancellation
    ) {
        if (model.GetDeclaredSymbol(declaration, cancellation) is not INamedTypeSymbol { IsRecord: false } type
            || type.DeclaringSyntaxReferences.Length != 1
            || !EqualityMembers.BindsCompletely(type)
            || EqualityMembers.HashCode(type) is not { } hash) {
            return default;
        }

        var equality = new List<IMethodSymbol>();
        if (EqualityMembers.ObjectEquals(type) is { } objectEquals) {
            equality.Add(objectEquals);
        }

        equality.AddRange(EqualityMembers.TypedEquals(type));

        var permitted = new List<IMethodSymbol>(equality) { hash };
        var canonical = EqualityMembers.Canonicalisation(type, model, cancellation);
        var hashed = EqualityMembers.Reads(type, hash, model, permitted, canonical, cancellation);
        if (hashed is null) {
            return default;
        }

        // ⚠ A null or empty equality set is "not known", not "compares nothing". Everything the
        // hash reads then goes to SK2043, which asks a question that does not need the equality
        // set, and SK2042 — which does — reports nothing at all.
        var compared = EqualitySet(type, equality, model, permitted, canonical, cancellation);
        var uncomparedHalf = new Dictionary<ISymbol, Location>(SymbolEqualityComparer.Default);
        var comparedHalf = new Dictionary<ISymbol, Location>(SymbolEqualityComparer.Default);
        foreach (var pair in hashed) {
            if (compared is { Count: > 0 } && !compared.Contains(pair.Key)) {
                uncomparedHalf[pair.Key] = pair.Value;
            } else {
                comparedHalf[pair.Key] = pair.Value;
            }
        }

        return new(uncomparedHalf, comparedHalf, declaration, canonical);
    }

    static HashSet<ISymbol>? EqualitySet(
        INamedTypeSymbol type,
        List<IMethodSymbol> equality,
        SemanticModel model,
        List<IMethodSymbol> permitted,
        Dictionary<ISymbol, ISymbol> canonical,
        CancellationToken cancellation
    ) {
        if (equality.Count == 0) {
            return null;
        }

        var union = new HashSet<ISymbol>(SymbolEqualityComparer.Default);
        foreach (var method in equality) {
            var read = EqualityMembers.Reads(type, method, model, permitted, canonical, cancellation);
            if (read is null) {
                return null;
            }

            foreach (var member in read.Keys) {
                union.Add(member);
            }
        }

        return union;
    }

    /// <summary>
    ///     Whether the member can still be assigned once the object exists.
    /// </summary>
    /// <remarks>
    ///     ⚠ The private case is the one that decides whether this rule is usable. A private field
    ///     nobody marked <c>readonly</c> and nobody assigns after construction cannot change, and it
    ///     is by far the most common shape of "a non-readonly member in a hash code". It is also
    ///     decidable rather than a guess: private means the assignments are all inside this
    ///     declaration, so they can be counted — which is why a type declared in more than one file
    ///     is refused earlier.
    /// </remarks>
    public bool CanChangeAfterConstruction(ISymbol member, SemanticModel model, CancellationToken cancellation) {
        switch (member) {
            case IPropertySymbol { IsStatic: false, SetMethod: { IsInitOnly: false } setter }:
                return setter.DeclaredAccessibility != Accessibility.Private
                    || AssignedOutsideAConstructor(member, model, cancellation);
            case IFieldSymbol { IsStatic: false, IsReadOnly: false, IsConst: false } field:
                return field.DeclaredAccessibility != Accessibility.Private
                    || AssignedOutsideAConstructor(member, model, cancellation);
            default:
                return false;
        }
    }

    bool AssignedOutsideAConstructor(ISymbol member, SemanticModel model, CancellationToken cancellation) {
        foreach (var node in Declaration.DescendantNodes()) {
            cancellation.ThrowIfCancellationRequested();
            var target = Target(node);
            if (target is null
                || model.GetSymbolInfo(target, cancellation).Symbol is not { } symbol) {
                continue;
            }

            var resolved = Canonical.TryGetValue(symbol, out var mapped) ? mapped : symbol;
            if (SymbolEqualityComparer.Default.Equals(resolved, member) && !InsideAConstructor(node)) {
                return true;
            }
        }

        return false;
    }

    static ExpressionSyntax? Target(SyntaxNode node) =>
        node switch {
            AssignmentExpressionSyntax assignment => assignment.Left,
            PrefixUnaryExpressionSyntax prefix when prefix.IsKind(SyntaxKind.PreIncrementExpression)
                || prefix.IsKind(SyntaxKind.PreDecrementExpression) => prefix.Operand,
            PostfixUnaryExpressionSyntax postfix when postfix.IsKind(SyntaxKind.PostIncrementExpression)
                || postfix.IsKind(SyntaxKind.PostDecrementExpression) => postfix.Operand,
            ArgumentSyntax { RefOrOutKeyword.RawKind: not 0 } argument => argument.Expression,
            _ => null
        };

    static bool InsideAConstructor(SyntaxNode node) {
        for (var current = node; current is not null; current = current.Parent) {
            switch (current) {
                case ConstructorDeclarationSyntax:
                    return true;
                case MemberDeclarationSyntax:
                    return false;
            }
        }

        return false;
    }
}
