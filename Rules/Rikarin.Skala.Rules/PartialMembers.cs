using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Linq;

namespace Rikarin.Skala.Rules;

/// <summary>
///     The two halves of a <c>partial</c> member, told apart from syntax alone (#397).
/// </summary>
/// <remarks>
///     ⚠ C# 13 and 14 made properties, indexers, events and constructors partial, and each one's
///     <em>defining</em> declaration is spelled exactly like a shape rules already had an opinion about:
///     <c>partial int Count { get; }</c> is an auto-property, <c>partial event EventHandler E;</c> is a
///     field-like event, <c>partial C(int x);</c> is a constructor with no body. A rule reading the shape
///     sees one member twice, and the half it sees first is the one that says least. Sweeping every
///     fixture with its members split into a definition and an implementation found eight rules that
///     reported a finding the merged member does not have, and two whose fix edited one half and left
///     the pair disagreeing (<c>CS8799</c>, <c>CS9275</c>).
///     <para>
///         ⚠ The syntax is enough to tell the halves apart, and that is why this is syntax rather than
///         <c>IsPartialDefinition</c>: several of the rules that need it are <c>Syntax</c>-scoped and
///         run under <c>--load=loose</c>. The implementation is the half with a body — an accessor body,
///         an arrow, a block, or an accessor list on an event — because the language forbids an
///         implementation that is itself an auto-property, a field-like event or a bodiless constructor.
///     </para>
///     <para>
///         ⚠
///         <b>
///             Measured on Roslyn 5.9: a syntax-node action never visits a partial constructor's
///             defining declaration
///         </b>, nor any node inside it. <c>SK6003</c> and <c>SK7110</c> each reported
///         the implementation alone, which is why their fixes edited one half. A rule that must change
///         both halves therefore reports from the implementation and finds the definition here, rather
///         than trusting the driver to show it the other one. Since #401 every node action registered
///         through <see cref="PartialConstructorDefinitions.Visiting" /> is handed the definition anyway,
///         from a semantic-model action; the carrier below stayed where it was, so that no finding moved.
///     </para>
/// </remarks>
static class PartialMembers {
    /// <summary>Whether the declaration carries the <c>partial</c> modifier and is a member, not a type.</summary>
    public static bool IsPartialMember(SyntaxNode? declaration) =>
        declaration is MemberDeclarationSyntax member and not BaseTypeDeclarationSyntax
        && member.Modifiers.Any(SyntaxKind.PartialKeyword);

    /// <summary>The half of a partial member that has a body.</summary>
    public static bool IsImplementation(SyntaxNode? declaration) =>
        IsPartialMember(declaration)
        && declaration switch {
            BaseMethodDeclarationSyntax method => method.Body is not null || method.ExpressionBody is not null,
            PropertyDeclarationSyntax property => property.ExpressionBody is not null
                || HasAccessorBody(property.AccessorList),
            IndexerDeclarationSyntax indexer => indexer.ExpressionBody is not null
                || HasAccessorBody(indexer.AccessorList),
            EventDeclarationSyntax => true,
            _ => false
        };

    /// <summary>
    ///     The half of a partial member that has no body: the contract a generator or a second file
    ///     fulfils.
    /// </summary>
    public static bool IsDefinition(SyntaxNode? declaration) =>
        IsPartialMember(declaration) && !IsImplementation(declaration);

    /// <summary>
    ///     Whether this declaration is the half a once-per-member finding is reported on.
    /// </summary>
    /// <remarks>
    ///     The definition, because it is the half that is always hand-written — the implementation is
    ///     routinely a generator's, and a generated half is never reported on. ⚠ Except two kinds, where
    ///     the implementation carries it:
    ///     <list type="bullet">
    ///         <item>
    ///             a constructor, whose definition Roslyn's driver never visits (#401 dispatches it now) —
    ///             decided here by kind rather than by which half happened to be shown, so that neither
    ///             path to the definition can double a finding;
    ///         </item>
    ///         <item>
    ///             an event, whose definition is spelled as a field-like event, which is a declaration the
    ///             member-level rules deliberately do not measure.
    ///         </item>
    ///     </list>
    /// </remarks>
    public static bool CarriesTheFinding(SyntaxNode? declaration) =>
        !IsPartialMember(declaration)
        || (declaration is ConstructorDeclarationSyntax or EventDeclarationSyntax or EventFieldDeclarationSyntax
            ? IsImplementation(declaration)
            : IsDefinition(declaration));

    /// <summary>
    ///     The other half of a partial member, when it is declared in the same type declaration.
    /// </summary>
    /// <remarks>
    ///     ⚠ Null is an answer and not a failure: the other half may be in another file or in generated
    ///     source, and a <c>Syntax</c>-scoped rule cannot look there without making its result depend on a
    ///     file its cache key does not cover. Every caller decides what null means for it, and each
    ///     decides it in the direction of saying less.
    /// </remarks>
    public static MemberDeclarationSyntax? Sibling(SyntaxNode? declaration) {
        if (!IsPartialMember(declaration)
            || declaration is not MemberDeclarationSyntax member
            || member.Parent is not TypeDeclarationSyntax type) {
            return null;
        }

        var implementation = IsImplementation(member);
        foreach (var candidate in type.Members) {
            if (candidate != member
                && IsPartialMember(candidate)
                && IsImplementation(candidate) != implementation
                && SameSignature(member, candidate)) {
                return candidate;
            }
        }

        return null;
    }

    /// <summary>
    ///     The other half through the symbol, wherever it is declared — for a <c>Semantic</c> rule.
    /// </summary>
    public static ISymbol? OtherPart(ISymbol? symbol) =>
        symbol switch {
            IMethodSymbol method => (ISymbol?)method.PartialDefinitionPart ?? method.PartialImplementationPart,
            IPropertySymbol property => (ISymbol?)property.PartialDefinitionPart ?? property.PartialImplementationPart,
            IEventSymbol declaredEvent => (ISymbol?)declaredEvent.PartialDefinitionPart
                ?? declaredEvent.PartialImplementationPart,
            _ => null
        };

    /// <summary>Whether a symbol is the defining half of a partial member.</summary>
    public static bool IsDefinitionSymbol(ISymbol? symbol) =>
        symbol switch {
            IMethodSymbol method => method.IsPartialDefinition,
            IPropertySymbol property => property.IsPartialDefinition,
            IEventSymbol declaredEvent => declaredEvent.IsPartialDefinition,
            _ => false
        };

    static bool HasAccessorBody(AccessorListSyntax? accessors) =>
        accessors is not null
        && accessors.Accessors.Any(static accessor => accessor.Body is not null || accessor.ExpressionBody is not null);

    /// <summary>
    ///     ⚠ Kind, name and parameter types as written. Written rather than bound, because the two halves
    ///     must agree on the types and a mismatch is a compile error — so text equality is exact for
    ///     every pair that compiles, up to an alias or a qualifier, where it fails safe as "not found".
    /// </summary>
    static bool SameSignature(MemberDeclarationSyntax left, MemberDeclarationSyntax right) =>
        (left, right) switch {
            (PropertyDeclarationSyntax a, PropertyDeclarationSyntax b) => a.Identifier.ValueText
                == b.Identifier.ValueText,
            (IndexerDeclarationSyntax a, IndexerDeclarationSyntax b) => SameParameters(
                a.ParameterList,
                b.ParameterList
            ),
            (EventFieldDeclarationSyntax a, EventDeclarationSyntax b) => SameEvent(a, b),
            (EventDeclarationSyntax a, EventFieldDeclarationSyntax b) => SameEvent(b, a),
            (ConstructorDeclarationSyntax a, ConstructorDeclarationSyntax b) =>
                a.Modifiers.Any(SyntaxKind.StaticKeyword) == b.Modifiers.Any(SyntaxKind.StaticKeyword)
                && SameParameters(a.ParameterList, b.ParameterList),
            (MethodDeclarationSyntax a, MethodDeclarationSyntax b) =>
                a.Identifier.ValueText == b.Identifier.ValueText
                && (a.TypeParameterList?.Parameters.Count ?? 0) == (b.TypeParameterList?.Parameters.Count ?? 0)
                && SameParameters(a.ParameterList, b.ParameterList),
            _ => false
        };

    static bool SameEvent(EventFieldDeclarationSyntax definition, EventDeclarationSyntax implementation) =>
        definition.Declaration.Variables.Count == 1
        && definition.Declaration.Variables[0].Identifier.ValueText == implementation.Identifier.ValueText;

    static bool SameParameters(BaseParameterListSyntax left, BaseParameterListSyntax right) {
        if (left.Parameters.Count != right.Parameters.Count) {
            return false;
        }

        for (var i = 0; i < left.Parameters.Count; i++) {
            if (left.Parameters[i].Type?.ToString() != right.Parameters[i].Type?.ToString()) {
                return false;
            }
        }

        return true;
    }
}
