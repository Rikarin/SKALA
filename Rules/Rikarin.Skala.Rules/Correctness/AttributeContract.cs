using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Collections.Generic;
using System.Threading;

namespace Rikarin.Skala.Rules.Correctness;

/// <summary>
///     The machinery <c>SK2100</c>–<c>SK2103</c> share: read an attribute, read the declaration it is
///     on, and answer only where both of them resolved.
/// </summary>
/// <remarks>
///     ⚠ Every method here returns "I do not know" as a distinct answer from "no", and the four rules
///     that use it report only on "no". An attribute rule that cannot tell an absent member from an
///     unresolved one reports every source slice with no dependency closure as a defect — issue #277's
///     shape, arrived at from the other direction.
///     <para>
///         ⚠ Nothing here throws on an unresolved symbol. A Roslyn analyzer that throws is swallowed as
///         <c>AD0001</c>: the positive fixtures fail and <em>every negative fixture passes</em>, which is
///         the one failure the fixture harness cannot see (#279).
///     </para>
/// </remarks>
static class AttributeContract {
    /// <summary>
    ///     ⚠ Compared as a string rather than against
    ///     <c>Compilation.GetTypeByMetadataName</c>. That method returns null when two referenced
    ///     assemblies both declare the name — and JetBrains' annotations are routinely present twice,
    ///     once from the package and once from a source-embedded <c>Annotations.cs</c>. Resolving by
    ///     name would then silently answer "not that attribute" in exactly the repositories where
    ///     <c>SK2101</c> has the most to say.
    /// </summary>
    static readonly SymbolDisplayFormat FullName = new(
        SymbolDisplayGlobalNamespaceStyle.Omitted,
        SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces
    );

    /// <summary>The attribute class an application names, or null when it did not resolve.</summary>
    public static INamedTypeSymbol? Resolve(
        SemanticModel model,
        AttributeSyntax attribute,
        CancellationToken cancellation
    ) {
        var symbol = model.GetSymbolInfo(attribute, cancellation).Symbol;
        var type = (symbol as IMethodSymbol)?.ContainingType ?? symbol?.ContainingType;
        return type is null || type.TypeKind == TypeKind.Error ? null : type;
    }

    /// <summary>The attribute class's namespace-qualified name, for an exact-identity test.</summary>
    public static string NameOf(INamedTypeSymbol attribute) => attribute.OriginalDefinition.ToDisplayString(FullName);

    /// <summary>
    ///     Whether <c>[AttributeUsage(AllowMultiple = true)]</c> reaches this attribute class.
    /// </summary>
    /// <remarks>
    ///     ⚠ The base chain is walked because that is what the runtime's own lookup does — an
    ///     <c>AttributeUsage</c> on a base attribute class governs everything derived from it. ⚠ The
    ///     answer is <c>false</c> when no <c>AttributeUsage</c> is found anywhere, which is the
    ///     language's default and is also the safe direction: <c>SK2103</c> reports only on
    ///     <c>true</c>.
    /// </remarks>
    public static bool AllowsMultiple(INamedTypeSymbol attribute) {
        for (var current = attribute; current is not null; current = current.BaseType) {
            foreach (var applied in current.GetAttributes()) {
                if (applied.AttributeClass is null
                    || applied.AttributeClass.TypeKind == TypeKind.Error
                    || !string.Equals(
                        NameOf(applied.AttributeClass),
                        "System.AttributeUsageAttribute",
                        StringComparison.Ordinal
                    )) {
                    continue;
                }

                foreach (var named in applied.NamedArguments) {
                    if (string.Equals(named.Key, "AllowMultiple", StringComparison.Ordinal)
                        && named.Value.Value is bool allow) {
                        return allow;
                    }
                }

                return false;
            }
        }

        return false;
    }

    /// <summary>
    ///     Whether a type — or anything it inherits or implements — declares a member of this name.
    ///     Null means the question could not be answered.
    /// </summary>
    /// <remarks>
    ///     ⚠
    ///     <b>
    ///         The interface walk is what carries this, and a sabotage that failed to fail is how that
    ///         was established.
    ///     </b> The first version also matched a mangled explicit-implementation name
    ///     (<c>IFoo.Bar</c> ends with <c>.Bar</c>), on the reasoning that a plain
    ///     <c>GetMembers(name)</c> misses an explicit implementation and a type whose only
    ///     <c>Count</c> is <c>ICollection.Count</c> would be wrongly reported. That reasoning is true
    ///     and the code was still redundant: an explicit implementation requires the interface to be in
    ///     <c>AllInterfaces</c>, where the member is declared under its plain name — so the two guards
    ///     covered exactly the same cases and <em>neither could be sabotaged into failing</em>, each
    ///     masking the other. The mangled match had no case of its own; the interface walk has one the
    ///     type does not declare at all, which is a default interface member.
    /// </remarks>
    public static bool? HasMemberNamed(ITypeSymbol type, string name) {
        for (var current = type; current is not null; current = current.BaseType) {
            if (current.TypeKind == TypeKind.Error) {
                return null;
            }

            if (Declares(current, name)) {
                return true;
            }

            foreach (var contract in current.AllInterfaces) {
                if (contract.TypeKind == TypeKind.Error) {
                    return null;
                }

                if (Declares(contract, name)) {
                    return true;
                }
            }
        }

        return false;
    }

    static bool Declares(ITypeSymbol type, string name) => !type.GetMembers(name).IsEmpty;

    /// <summary>
    ///     The span that deletes one attribute application and nothing else.
    /// </summary>
    /// <remarks>
    ///     ⚠ The separator goes with it, and <b>the one on the right is taken wherever there is one</b>.
    ///     Taking the left separator instead is correct for a single deletion and wrong the moment two
    ///     neighbours in one list are deleted at once: their spans then share the comma between them,
    ///     and two overlapping edits applied in sequence corrupt the text. Preferring the right
    ///     separator makes any set of deletions from one list pairwise disjoint by construction.
    /// </remarks>
    public static TextSpan? Removal(AttributeSyntax attribute) {
        if (attribute.Parent is not AttributeListSyntax list) {
            return null;
        }

        if (list.Attributes.Count == 1) {
            return list.Span;
        }

        var index = list.Attributes.IndexOf(attribute);
        if (index < 0) {
            return null;
        }

        return index < list.Attributes.Count - 1
            ? TextSpan.FromBounds(attribute.SpanStart, list.Attributes.GetSeparator(index).Span.End)
            : TextSpan.FromBounds(list.Attributes.GetSeparator(index - 1).SpanStart, attribute.Span.End);
    }

    /// <summary>
    ///     The spans that delete a whole set of attribute applications at once, guaranteed disjoint.
    /// </summary>
    /// <remarks>
    ///     ⚠ A list every one of whose attributes is being deleted goes as a single span, because
    ///     deleting them one by one would leave <c>[]</c> behind and an empty attribute list does not
    ///     parse.
    /// </remarks>
    public static List<TextSpan> Removals(List<AttributeSyntax> attributes) {
        var spans = new List<TextSpan>();
        foreach (var attribute in attributes) {
            if (attribute.Parent is AttributeListSyntax list && Emptied(list, attributes)) {
                if (!spans.Contains(list.Span)) {
                    spans.Add(list.Span);
                }

                continue;
            }

            var one = Removal(attribute);
            if (one is not null) {
                spans.Add(one.Value);
            }
        }

        return spans;
    }

    static bool Emptied(AttributeListSyntax list, List<AttributeSyntax> removed) {
        foreach (var attribute in list.Attributes) {
            if (!removed.Contains(attribute)) {
                return false;
            }
        }

        return true;
    }

    /// <summary>Every attribute list written directly on one declaration, in source order.</summary>
    /// <remarks>
    ///     ⚠ Read off the parent's children rather than off a typed property. Attribute lists hang
    ///     directly from every node that can carry them — a type, a member, an accessor, a parameter, a
    ///     type parameter, a lambda, a local function, a local declaration statement and the
    ///     compilation unit — and there is no common base type with an <c>AttributeLists</c> property
    ///     that covers all of them. Enumerating children covers every one and cannot go stale when the
    ///     language adds another.
    /// </remarks>
    public static List<AttributeListSyntax> ListsOn(SyntaxNode declaration) {
        var result = new List<AttributeListSyntax>();
        foreach (var child in declaration.ChildNodes()) {
            if (child is AttributeListSyntax list) {
                result.Add(list);
            }
        }

        return result;
    }
}
