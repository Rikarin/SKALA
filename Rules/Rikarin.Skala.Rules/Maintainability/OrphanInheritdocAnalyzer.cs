using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Rikarin.Skala.Rules.Metadata;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;

namespace Rikarin.Skala.Rules.Maintainability;

/// <summary>
///     <c>SK7103</c> — an <c>&lt;inheritdoc/&gt;</c> with nothing to inherit.
/// </summary>
/// <remarks>
///     ⚠ <b>The resolution is Roslyn's, measured rather than assumed.</b> The IDE's own expansion
///     (<c>ISymbolExtensions.GetDocumentationComment</c> with <c>expandInheritdoc</c>) was asked about every
///     shape below, and this rule reads the same candidates: the overridden member, an explicit or implicit
///     interface member, a base constructor with the same parameters, and for a type its base class or
///     interface. Anything else — a member hidden with <c>new</c>, a member of a nested type, a constructor
///     whose base has no such signature — expands to nothing, and the page renders blank while
///     <c>CS1591</c> stays satisfied by the comment's mere presence.
///     <para>
///         ⚠ <b>Presence is the test, not documentation.</b> <c>SK7100</c>'s resolver asks whether the
///         inherited text is a copy; this asks whether there is a member to inherit from at all, and an
///         undocumented base member still counts. Reading its text would make the verdict depend on
///         whether a referenced assembly shipped its XML file, and an answer that changes with the
///         package cache is not a finding.
///     </para>
///     <para>
///         ⚠ <b><c>System.Object</c> and <c>System.ValueType</c> are nothing to inherit.</b> Roslyn does
///         resolve a base-less class's <c>&lt;inheritdoc/&gt;</c> — to "Supports all classes in the .NET class
///         hierarchy…", and its parameterless constructor's to "Initializes a new instance of the Object
///         class" — which documents <c>object</c>, not the type it is rendered on.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class OrphanInheritdocAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.InheritdocWithNothingToInherit);

    static readonly ImmutableArray<SyntaxKind> Kinds = ImmutableArray.Create(
        SyntaxKind.MethodDeclaration,
        SyntaxKind.ConstructorDeclaration,
        SyntaxKind.OperatorDeclaration,
        SyntaxKind.ConversionOperatorDeclaration,
        SyntaxKind.PropertyDeclaration,
        SyntaxKind.IndexerDeclaration,
        SyntaxKind.EventDeclaration,
        SyntaxKind.EventFieldDeclaration,
        SyntaxKind.FieldDeclaration,
        SyntaxKind.EnumMemberDeclaration,
        SyntaxKind.ClassDeclaration,
        SyntaxKind.StructDeclaration,
        SyntaxKind.InterfaceDeclaration,
        SyntaxKind.RecordDeclaration,
        SyntaxKind.RecordStructDeclaration,
        SyntaxKind.EnumDeclaration,
        SyntaxKind.DelegateDeclaration
    );

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        var registrar = PartialConstructorDefinitions.Visiting(context);
        registrar.RegisterSyntaxNodeAction(Analyze, Kinds);
    }

    static void Analyze(SyntaxNodeAnalysisContext context) {
        var declaration = context.Node;
        var elements = DocumentationElements.Anywhere(declaration, "inheritdoc").ToList();

        // ⚠ Any `cref` hands the question to the compiler: a resolvable one is something to inherit,
        // and an unresolvable one is already CS1574. `path` alone selects within the default target
        // and leaves the question unchanged.
        if (elements.Count == 0
            || elements.Exists(static element => DocumentationElements.AttributesOf(element)
                .Any(static attribute => attribute.Name.LocalName.ValueText == "cref")
            )) {
            return;
        }

        var cancellation = context.CancellationToken;
        var symbols = Symbols(context.SemanticModel, declaration, cancellation);
        if (symbols.Count == 0) {
            return;
        }

        string? reason = null;
        foreach (var symbol in symbols) {
            reason = Orphaned(symbol, declaration, cancellation);
            if (reason is null) {
                return;
            }
        }

        var text = declaration.SyntaxTree.GetText(cancellation);
        foreach (var element in elements) {
            context.ReportDiagnostic(
                Diagnostic.Create(
                    Descriptor,
                    element.GetLocation(),
                    FixEdits.Pack((DocumentationElements.DeletionSpan(text, element), string.Empty)),
                    reason
                )
            );
        }
    }

    /// <summary>The symbols one declaration declares — several for a multi-variable field.</summary>
    static List<ISymbol> Symbols(SemanticModel model, SyntaxNode declaration, CancellationToken cancellation) {
        var result = new List<ISymbol>();
        if (declaration is BaseFieldDeclarationSyntax field) {
            foreach (var variable in field.Declaration.Variables) {
                if (model.GetDeclaredSymbol(variable, cancellation) is { } declared) {
                    result.Add(declared);
                }
            }
        } else if (model.GetDeclaredSymbol(declaration, cancellation) is { } declared) {
            result.Add(declared);
        }

        return result;
    }

    /// <summary>Why the <c>&lt;inheritdoc/&gt;</c> resolves to nothing, or <c>null</c> when it resolves.</summary>
    static string? Orphaned(ISymbol symbol, SyntaxNode declaration, CancellationToken cancellation) {
        // ⚠ A hierarchy that does not bind cannot be judged. The first corpus sweep reported 150
        // findings in Vixen and one in Serilog, and every one was a member implementing an interface
        // the compilation could not resolve (CS0246): an error type has no members, so the implicit
        // implementation was invisible and the member looked like it implemented nothing.
        if (Unbound(symbol as INamedTypeSymbol ?? symbol.ContainingType) || HasUnboundSignature(symbol)) {
            return null;
        }

        var name = "`" + symbol.Name + "`";
        if (symbol is INamedTypeSymbol type) {
            return TypeOrphaned(type, declaration, cancellation, name);
        }

        // ⚠ A partial member's two halves share one comment, and it is the IMPLEMENTATION's when it has
        // one — measured: both parts' GetDocumentationCommentXml return the implementation's text. So an
        // `<inheritdoc/>` on the implementation replaces the definition's prose rather than deferring to
        // it, and one on the definition is dead text whenever the implementation is documented.
        var definition = symbol;
        var masksDefinition = false;
        switch (symbol) {
            case IMethodSymbol { PartialDefinitionPart: { } methodDefinition }:
                definition = methodDefinition;
                masksDefinition = HasOwnProse(methodDefinition, cancellation);
                break;

            case IPropertySymbol { PartialDefinitionPart: { } propertyDefinition }:
                definition = propertyDefinition;
                masksDefinition = HasOwnProse(propertyDefinition, cancellation);
                break;

            // ⚠ A partial event is the same shape (#397), and was missing: its implementation's
            // `<inheritdoc/>` was reported as overriding nothing rather than as masking the definition.
            case IEventSymbol { PartialDefinitionPart: { } eventDefinition }:
                definition = eventDefinition;
                masksDefinition = HasOwnProse(eventDefinition, cancellation);
                break;

            case IMethodSymbol { PartialImplementationPart: { } methodImplementation }
                when IsDocumented(methodImplementation, cancellation):
            case IPropertySymbol { PartialImplementationPart: { } propertyImplementation }
                when IsDocumented(propertyImplementation, cancellation):
            case IEventSymbol { PartialImplementationPart: { } eventImplementation }
                when IsDocumented(eventImplementation, cancellation):
                return null;
        }

        if (HasInheritanceSource(symbol) || HasInheritanceSource(definition)) {
            return null;
        }

        if (masksDefinition) {
            return "The implementation part's comment replaces the definition's, so `<inheritdoc/>` on "
                + name
                + " hides the documentation written on the definition and resolves to nothing";
        }

        if (symbol is IMethodSymbol { MethodKind: MethodKind.Constructor or MethodKind.StaticConstructor }) {
            return "No base constructor of "
                + name
                + " takes the same parameters, so `<inheritdoc/>` resolves to nothing"
                + " — or to `object`'s, which documents `object`";
        }

        if (IsNew(declaration) && Hidden(symbol) is { } hidden) {
            return name
                + " hides `"
                + hidden.ContainingType.Name
                + "."
                + hidden.Name
                + "` with `new`, and `<inheritdoc/>` follows only an override or an interface;"
                + " name the member with `cref`";
        }

        return name + " overrides nothing and implements no interface member, so `<inheritdoc/>` resolves to nothing";
    }

    static string? TypeOrphaned(
        INamedTypeSymbol type,
        SyntaxNode declaration,
        CancellationToken cancellation,
        string name
    ) {
        // A partial type's comment is every part's comment, so another part's prose is still there.
        foreach (var reference in type.DeclaringSyntaxReferences) {
            if (reference.SyntaxTree == declaration.SyntaxTree && reference.Span.Contains(declaration.Span)) {
                continue;
            }

            if (DocumentationElements.CommentsOf(reference.GetSyntax(cancellation)).Any()) {
                return null;
            }
        }

        // ⚠ An enum and a delegate never inherit, whatever their runtime base implements: Roslyn's
        // expansion answers nothing for both, and `System.Enum`'s IComparable or MulticastDelegate's
        // ICloneable are interfaces the declaration never chose.
        if (type.TypeKind is TypeKind.Enum or TypeKind.Delegate) {
            return name
                + " is "
                + (type.TypeKind == TypeKind.Enum ? "an enum" : "a delegate")
                + ", and neither inherits documentation from anything, so `<inheritdoc/>` resolves to nothing";
        }

        // ⚠ Interfaces silence every other kind, records included: their synthesized `IEquatable<T>`
        // counts. Roslyn's expansion ignores a struct's interfaces, but documentation generators that
        // resolve `<inheritdoc/>` themselves do not, and between the two the rule takes the quiet answer.
        if (type.AllInterfaces.Length > 0) {
            return null;
        }

        if (type.TypeKind is TypeKind.Class && type.BaseType is { } baseType && !IsRoot(baseType)) {
            return null;
        }

        return name
            + " has no base type or interface to inherit documentation from, so `<inheritdoc/>` resolves to"
            + " nothing — or to `object`'s, which documents `object`";
    }

    /// <summary>Whether Roslyn's expansion has any member to take documentation from.</summary>
    /// <remarks>
    ///     ⚠ Presence, and ambiguity counts as presence: a member implementing two interface members has
    ///     something to inherit whichever one a generator picks. That is the inverse of <c>SK7100</c>'s
    ///     "ambiguity is silence", and lands on the same side — no finding.
    /// </remarks>
    static bool HasInheritanceSource(ISymbol symbol) {
        switch (symbol) {
            case IMethodSymbol { OverriddenMethod: not null }:
            case IPropertySymbol { OverriddenProperty: not null }:
            case IEventSymbol { OverriddenEvent: not null }:
            case IMethodSymbol { ExplicitInterfaceImplementations.Length: > 0 }:
            case IPropertySymbol { ExplicitInterfaceImplementations.Length: > 0 }:
            case IEventSymbol { ExplicitInterfaceImplementations.Length: > 0 }:
                return true;

            case IMethodSymbol { MethodKind: MethodKind.Constructor or MethodKind.StaticConstructor } constructor:
                return BaseConstructorWithSameParameters(constructor);
        }

        if (symbol is not (IMethodSymbol or IPropertySymbol or IEventSymbol)
            || symbol.ContainingType is not { } container) {
            return false;
        }

        foreach (var implemented in container.AllInterfaces) {
            foreach (var member in implemented.GetMembers()) {
                if (container.FindImplementationForInterfaceMember(member) is { } implementation
                    && (SymbolEqualityComparer.Default.Equals(implementation, symbol)
                        || SymbolEqualityComparer.Default.Equals(
                            implementation.OriginalDefinition,
                            symbol.OriginalDefinition
                        ))) {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    ///     Roslyn's constructor rule: the containing type's base has a constructor with the same parameter
    ///     types. <c>object()</c> and <c>ValueType()</c> are excluded, per the remarks on the class.
    /// </summary>
    static bool BaseConstructorWithSameParameters(IMethodSymbol constructor) {
        if (constructor.ContainingType?.BaseType is not { } baseType || IsRoot(baseType)) {
            return false;
        }

        foreach (var candidate in baseType.InstanceConstructors) {
            if (candidate.Parameters.Length == constructor.Parameters.Length
                && candidate.Parameters.Zip(constructor.Parameters, static (a, b) => (a, b))
                    .All(static pair => pair.a.RefKind == pair.b.RefKind
                        && SymbolEqualityComparer.Default.Equals(pair.a.Type, pair.b.Type)
                    )) {
                return true;
            }
        }

        return false;
    }

    /// <summary>Whether any type in the base chain, or any interface it names, failed to bind.</summary>
    static bool Unbound(INamedTypeSymbol? type) {
        for (var current = type; current is not null; current = current.BaseType) {
            if (current.TypeKind == TypeKind.Error
                || current.Interfaces.Any(static implemented => implemented.TypeKind == TypeKind.Error)
                || current.AllInterfaces.Any(static implemented =>
                    implemented.Interfaces.Any(static inner => inner.TypeKind == TypeKind.Error)
                )) {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    ///     A member whose parameter or return type failed to bind matches no interface member, so its
    ///     implementation is invisible for the same reason as an unbound interface's.
    /// </summary>
    static bool HasUnboundSignature(ISymbol symbol) {
        var types = symbol switch {
            IMethodSymbol method => method.Parameters.Select(static parameter => parameter.Type)
                .Append(method.ReturnType),
            IPropertySymbol property => property.Parameters.Select(static parameter => parameter.Type)
                .Append(property.Type),
            IEventSymbol declaredEvent => new[] { declaredEvent.Type },
            _ => Enumerable.Empty<ITypeSymbol>()
        };

        return types.Any(static type => type.TypeKind == TypeKind.Error
            || type is INamedTypeSymbol { IsGenericType: true } generic
            && generic.TypeArguments.Any(static argument => argument.TypeKind == TypeKind.Error)
        );
    }

    static bool IsRoot(INamedTypeSymbol type) =>
        type.SpecialType is SpecialType.System_Object or SpecialType.System_ValueType;

    /// <summary>The base member a <c>new</c> member hides, for the message; <c>null</c> when it hides none.</summary>
    static ISymbol? Hidden(ISymbol symbol) {
        if (symbol.ContainingType is not { } container) {
            return null;
        }

        var bases = container.TypeKind == TypeKind.Interface
            ? container.AllInterfaces.Cast<INamedTypeSymbol>()
            : Ancestors(container);
        return bases.SelectMany(ancestor => ancestor.GetMembers(symbol.Name))
            .FirstOrDefault(static member => !member.IsImplicitlyDeclared);
    }

    static IEnumerable<INamedTypeSymbol> Ancestors(INamedTypeSymbol type) {
        for (var current = type.BaseType; current is not null; current = current.BaseType) {
            yield return current;
        }
    }

    static bool IsNew(SyntaxNode declaration) =>
        declaration is MemberDeclarationSyntax member
        && member.Modifiers.Any(static modifier => modifier.IsKind(SyntaxKind.NewKeyword));

    static bool IsDocumented(ISymbol symbol, CancellationToken cancellation) =>
        symbol.DeclaringSyntaxReferences.Any(reference =>
            DocumentationElements.CommentsOf(Commented(reference.GetSyntax(cancellation))).Any()
        );

    /// <summary>Documented in prose of its own, not by another <c>&lt;inheritdoc/&gt;</c>.</summary>
    static bool HasOwnProse(ISymbol symbol, CancellationToken cancellation) =>
        symbol.DeclaringSyntaxReferences.Any(reference => {
                var node = Commented(reference.GetSyntax(cancellation));
                return DocumentationElements.CommentsOf(node).Any()
                    && !DocumentationElements.Anywhere(node, "inheritdoc").Any();
            }
        );

    /// <summary>
    ///     ⚠ The node a declaration's comment is attached to. A field-like event's — and a partial event
    ///     definition's — declaring syntax is its variable declarator, whose leading trivia is empty; the
    ///     comment is on the declaration around it.
    /// </summary>
    static SyntaxNode Commented(SyntaxNode node) =>
        node is VariableDeclaratorSyntax { Parent.Parent: BaseFieldDeclarationSyntax field } ? field : node;
}
