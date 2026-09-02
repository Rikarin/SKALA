using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
using System.Threading;

namespace Rikarin.Skala.Rules;

/// <summary>
///     The one question <c>SK1071</c> and <c>SK2240</c> both have to answer before they may rewrite.
/// </summary>
/// <remarks>
///     ⚠ The two rules translate between <c>new R(…)</c> and <c>x with { … }</c> in opposite
///     directions, and both translations are sound under exactly the same condition: the record's
///     entire instance state is the positional parameters of its primary constructor, and every one of
///     those properties is the auto-property the compiler synthesized. <c>with</c> invokes the copy
///     constructor, which copies <em>every</em> field; the constructor call sets only the parameters.
///     Where the two sets are the same the rewrite cannot change what the object holds, and where they
///     differ it silently can — in whichever direction it is applied.
///     <para>
///         ⚠ It lives here rather than in either rule because a second copy of it would be a second
///         chance to get it subtly wrong, and because Skala's own duplication gate measures it.
///     </para>
/// </remarks>
static class RecordShape {
    /// <summary>
    ///     Whether the record's entire instance state is this constructor's positional parameters.
    /// </summary>
    public static bool WholeStateIsItsParameters(
        INamedTypeSymbol record,
        IMethodSymbol constructor,
        CancellationToken cancellation
    ) {
        // ⚠ Unsealed is the trap: `x with { … }` returns x's *runtime* type through the virtual
        // clone, and `new R(…)` returns exactly R.
        if (!record.IsRecord
            || !record.IsSealed
            || record.BaseType is not (null
                or { SpecialType: SpecialType.System_Object or SpecialType.System_ValueType })
            || constructor.Parameters.Length == 0) {
            return false;
        }

        // The primary constructor and no other: its declaring syntax is the record declaration
        // itself, where a secondary constructor's is a `ConstructorDeclarationSyntax`.
        if (constructor.DeclaringSyntaxReferences.Length != 1
            || constructor.DeclaringSyntaxReferences[0].GetSyntax(cancellation) is not RecordDeclarationSyntax) {
            return false;
        }

        var positional = new HashSet<string>(System.StringComparer.Ordinal);
        return EveryParameterIsItsOwnAutoProperty(record, constructor, positional, cancellation)
            && NothingIsHeldOutsideThoseParameters(record, positional, cancellation);
    }

    /// <summary>
    ///     Whether every positional parameter declared the auto-property of the same name, collecting
    ///     those names into <paramref name="positional" />.
    /// </summary>
    /// <remarks>
    ///     ⚠ The property has to be the one <em>this parameter</em> made, which is asked by comparing the
    ///     two symbols' declaring syntax: a positional record property declares against the
    ///     <see cref="ParameterSyntax" /> itself, and a hand-written one against a
    ///     <c>PropertyDeclarationSyntax</c>. ⚠ <c>IsImplicitlyDeclared</c> is the obvious test and it is
    ///     the wrong one — it is <b>false</b> for a positional record property, because the parameter is
    ///     where it is written down. Reading it as true would silently disable every rule that asks this.
    /// </remarks>
    static bool EveryParameterIsItsOwnAutoProperty(
        INamedTypeSymbol record,
        IMethodSymbol constructor,
        HashSet<string> positional,
        CancellationToken cancellation
    ) {
        foreach (var parameter in constructor.Parameters) {
            cancellation.ThrowIfCancellationRequested();
            if (parameter.RefKind != RefKind.None || parameter.IsParams) {
                return false;
            }

            var named = record.GetMembers(parameter.Name);
            if (named.Length != 1
                || named[0] is not IPropertySymbol { IsStatic: false, IsIndexer: false, SetMethod: not null } property
                || !SymbolEqualityComparer.Default.Equals(property.Type, parameter.Type)
                || !DeclaredBy(property, parameter, cancellation)) {
                return false;
            }

            positional.Add(parameter.Name);
        }

        return true;
    }

    /// <summary>
    ///     Whether the record holds no instance state beyond the named positional properties.
    /// </summary>
    /// <remarks>
    ///     The copy constructor carries every field across and a constructor call sets only the
    ///     parameters, so an instance field that backs nothing positional, an instance event, or a
    ///     settable property outside the parameter list each make the two forms hold different things.
    /// </remarks>
    static bool NothingIsHeldOutsideThoseParameters(
        INamedTypeSymbol record,
        HashSet<string> positional,
        CancellationToken cancellation
    ) {
        foreach (var member in record.GetMembers()) {
            cancellation.ThrowIfCancellationRequested();
            switch (member) {
                case IFieldSymbol { IsStatic: false, IsConst: false } field
                    when field.AssociatedSymbol is not IPropertySymbol associated
                    || !positional.Contains(associated.Name):
                    return false;

                case IEventSymbol { IsStatic: false }:
                    return false;

                case IPropertySymbol { IsStatic: false, IsIndexer: false, SetMethod: not null } property
                    when !positional.Contains(property.Name):
                    return false;
            }
        }

        return true;
    }

    /// <summary>
    ///     Whether the property is the positional one this parameter declared.
    /// </summary>
    /// <remarks>
    ///     ⚠ A positional record property and its parameter are <em>the same piece of source</em>: both
    ///     symbols point at the one <see cref="ParameterSyntax" />. A property written out in the record
    ///     body points at a <c>PropertyDeclarationSyntax</c> instead, and its accessor is called by
    ///     <c>x.A</c> and not called by the copy constructor, which moves the backing field across
    ///     without asking. Comparing the two declarations is the shortest way to say "the compiler wrote
    ///     this one", and it is also why only records declared in source match: a record from metadata
    ///     has no declaring syntax to compare, and nothing else in the symbol tells the two apart.
    /// </remarks>
    public static bool DeclaredBy(
        IPropertySymbol property,
        IParameterSymbol parameter,
        CancellationToken cancellation
    ) {
        if (property.DeclaringSyntaxReferences.Length != 1 || parameter.DeclaringSyntaxReferences.Length != 1) {
            return false;
        }

        return property.DeclaringSyntaxReferences[0].GetSyntax(cancellation) is ParameterSyntax declaration
            && ReferenceEquals(declaration, parameter.DeclaringSyntaxReferences[0].GetSyntax(cancellation));
    }

    /// <summary>
    ///     The record's primary constructor, or null where it has none.
    /// </summary>
    /// <remarks>
    ///     ⚠ Found by its declaring syntax rather than by arity. A record may declare secondary
    ///     constructors with the same number of parameters, and only the primary one is the parameter
    ///     list the positional properties were written in.
    /// </remarks>
    public static IMethodSymbol? PrimaryConstructor(INamedTypeSymbol record, CancellationToken cancellation) {
        foreach (var constructor in record.InstanceConstructors) {
            cancellation.ThrowIfCancellationRequested();
            if (constructor.DeclaringSyntaxReferences.Length == 1
                && constructor.DeclaringSyntaxReferences[0].GetSyntax(cancellation) is RecordDeclarationSyntax) {
                return constructor;
            }
        }

        return null;
    }
}
