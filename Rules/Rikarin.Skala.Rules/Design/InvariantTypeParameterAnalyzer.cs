using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Rules.Metadata;
using Rikarin.Skala.Rules.Modernization;
using System.Collections.Immutable;

namespace Rikarin.Skala.Rules.Design;

/// <summary>
///     <c>SK6060</c> — an interface type parameter that occurs in one direction only and is not
///     declared <c>out</c> or <c>in</c>.
/// </summary>
/// <remarks>
///     <para>
///         Variance is the one part of an interface's design that costs the declarer nothing and is
///         invisible until somebody needs it. Where it was available and not taken, every consumer
///         pays with a copy, a <c>Cast&lt;T&gt;()</c> or a second overload, and none of them can tell
///         that the restriction was accidental.
///     </para>
///     <para>
///         ⚠
///         <b>
///             This is the compiler's own variance-safety rule run in reverse, not an approximation
///             of it.
///         </b> Each occurrence of the parameter is classified by the position it sits in and
///         then composed through the declared variance of every generic type enclosing it — flipping
///         on a contravariant parameter, collapsing to invariant on an invariant one. That
///         composition is the entire content of the rule: it is what separates
///         <c>IEnumerable&lt;T&gt; Get()</c> from <c>List&lt;T&gt; Get()</c> and
///         <c>Action&lt;T&gt; Get()</c>, which look identical at the level of "the parameter is in a
///         return type" and of which only the first can be <c>out</c>.
///     </para>
///     <para>
///         ⚠ Eight shapes were compiled with the modifier already applied before this was written,
///         and the algorithm agrees with <c>CS1961</c> on all eight. The fixture harness re-binds
///         every fix, so each positive is checked against the real compiler on every run rather than
///         against the argument above.
///     </para>
///     <para>
///         ⚠ <c>partial</c> is declined. C# requires the variance modifiers to match on every partial
///         declaration, so an edit to one part is <c>CS0264</c>; a multi-part edit is a different
///         rule. Delegates are declined too — the argument is identical and shipping them is a
///         separate decision.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class InvariantTypeParameterAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.InvariantTypeParameter);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.InterfaceDeclaration);
    }

    static void Analyze(SyntaxNodeAnalysisContext context) {
        var declaration = (InterfaceDeclarationSyntax)context.Node;
        if (declaration.TypeParameterList is not { Parameters.Count: > 0 } list) {
            return;
        }

        // ⚠ Every partial declaration repeats the type parameter list and C# requires the variance
        // modifiers to agree across all of them. Editing one part is CS0264, so this is silence
        // rather than a fix that does not compile.
        foreach (var modifier in declaration.Modifiers) {
            if (modifier.IsKind(SyntaxKind.PartialKeyword)) {
                return;
            }
        }

        if (context.SemanticModel.GetDeclaredSymbol(declaration, context.CancellationToken)
            is not { TypeKind: TypeKind.Interface } type) {
            return;
        }

        // ⚠ Any nested type declaration withdraws the interface, and the two reasons are different
        // errors. A nested enum, class or struct is CS8427 outright — "cannot be declared in an
        // interface that has an 'in' or 'out' type parameter" — so the modifier is illegal no matter
        // what the signatures say. A nested delegate or interface is legal but is variance-checked
        // through its *own* members, which are not this interface's members and are not walked here;
        // both were confirmed against the compiler. One guard covers both, and a nested type inside
        // an interface is rare enough that declining costs nothing.
        if (type.GetTypeMembers().Length > 0) {
            return;
        }

        for (var i = 0; i < list.Parameters.Count && i < type.TypeParameters.Length; i++) {
            var syntax = list.Parameters[i];
            if (syntax.VarianceKeyword.RawKind != (int)SyntaxKind.None) {
                continue;
            }

            Report(context, type, type.TypeParameters[i], syntax);
        }
    }

    static void Report(
        SyntaxNodeAnalysisContext context,
        INamedTypeSymbol type,
        ITypeParameterSymbol parameter,
        TypeParameterSyntax syntax
    ) {
        // A value type admits no reference conversion, so `out T where T : struct` compiles and buys
        // nothing. Declining it is a judgement about worth, not about validity.
        if (parameter.HasValueTypeConstraint || parameter.HasUnmanagedTypeConstraint) {
            return;
        }

        var occurrences = new Occurrences();
        Walk(type, parameter, occurrences);

        // A parameter nobody mentions is an unused type parameter, which is a different finding.
        if (!occurrences.Any || occurrences.Invariant) {
            return;
        }

        if (occurrences.Covariant == occurrences.Contravariant) {
            return;
        }

        var keyword = occurrences.Covariant ? "out" : "in";
        var span = new TextSpan(syntax.Identifier.SpanStart, 0);

        if (RewriteGuards.ContainsCommentOrDirectiveWithinTheEdit(context.Node.SyntaxTree, syntax.Span)) {
            return;
        }

        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                syntax.Identifier.GetLocation(),
                FixEdits.Pack((span, keyword + " ")),
                "`"
                + parameter.Name
                + "` occurs only in "
                + (occurrences.Covariant ? "output" : "input")
                + " positions on `"
                + type.Name
                + "`, so it can be declared `"
                + keyword
                + " "
                + parameter.Name
                + "`"
            )
        );
    }

    /// <summary>
    ///     Records, across every member signature, whether the parameter ever lands somewhere that
    ///     only <c>out</c> admits, somewhere that only <c>in</c> admits, or somewhere neither does.
    /// </summary>
    sealed class Occurrences {
        public bool Covariant;
        public bool Contravariant;
        public bool Invariant;

        public bool Any => Covariant || Contravariant || Invariant;

        public void Note(Position position) {
            switch (position) {
                case Position.Covariant:
                    Covariant = true;
                    break;

                case Position.Contravariant:
                    Contravariant = true;
                    break;

                default:
                    Invariant = true;
                    break;
            }
        }
    }

    enum Position {
        Covariant,
        Contravariant,
        Invariant
    }

    static Position Flip(Position position) =>
        position switch {
            Position.Covariant => Position.Contravariant,
            Position.Contravariant => Position.Covariant,
            _ => Position.Invariant
        };

    /// <summary>Every position the parameter occupies in the interface's own signatures.</summary>
    /// <remarks>
    ///     ⚠ Only members declared on this interface. A base interface contributes through its own
    ///     appearance in the base list, where the type arguments carry the parameter and the
    ///     composition below classifies them; walking the inherited members as well would ask the
    ///     same question twice and, on a base whose own parameter is already variant, answer it
    ///     wrongly the second time.
    /// </remarks>
    static void Walk(INamedTypeSymbol type, ITypeParameterSymbol parameter, Occurrences occurrences) {
        foreach (var implemented in type.Interfaces) {
            Classify(implemented, parameter, Position.Covariant, occurrences);
        }

        foreach (var member in type.GetMembers()) {
            switch (member) {
                case IMethodSymbol method:
                    WalkMethod(method, parameter, occurrences);
                    break;

                case IPropertySymbol property:
                    WalkProperty(property, parameter, occurrences);
                    break;

                case IEventSymbol declaredEvent:
                    // Both accessors take the delegate, so the type is an input position.
                    Classify(declaredEvent.Type, parameter, Position.Contravariant, occurrences);
                    break;

                case IFieldSymbol field:
                    Classify(field.Type, parameter, Position.Invariant, occurrences);
                    break;
            }
        }
    }

    static void WalkMethod(IMethodSymbol method, ITypeParameterSymbol parameter, Occurrences occurrences) {
        // An accessor's signature is already covered by the property or event that owns it, and
        // classifying it twice cannot change the answer — but it can turn a property's invariant
        // pair into two separate one-directional readings, which is a different answer.
        if (method.AssociatedSymbol is not null) {
            return;
        }

        // ⚠ A `ref` return hands out storage the caller may write through, so it is invariant rather
        // than covariant. Confirmed against the compiler: `ref T Slot();` under `out T` is CS1961,
        // and it is the one shape in this rule that reads as an ordinary return type and is not.
        Classify(
            method.ReturnType,
            parameter,
            method.ReturnsByRef || method.ReturnsByRefReadonly ? Position.Invariant : Position.Covariant,
            occurrences
        );

        WalkParameters(method.Parameters, parameter, occurrences);

        // `where U : T` puts the parameter where the caller supplies the type, which is an input.
        foreach (var own in method.TypeParameters) {
            foreach (var constraint in own.ConstraintTypes) {
                Classify(constraint, parameter, Position.Contravariant, occurrences);
            }
        }
    }

    static void WalkProperty(IPropertySymbol property, ITypeParameterSymbol parameter, Occurrences occurrences) {
        var position = (property.GetMethod, property.SetMethod) switch {
            (not null, not null) => Position.Invariant,
            (not null, null) => Position.Covariant,
            (null, not null) => Position.Contravariant,
            _ => Position.Invariant
        };

        // ⚠ A `ref` property hands out storage, which is invariant regardless of its accessors.
        Classify(
            property.Type,
            parameter,
            property.ReturnsByRef || property.ReturnsByRefReadonly ? Position.Invariant : position,
            occurrences
        );

        WalkParameters(property.Parameters, parameter, occurrences);
    }

    static void WalkParameters(
        ImmutableArray<IParameterSymbol> parameters,
        ITypeParameterSymbol parameter,
        Occurrences occurrences
    ) {
        foreach (var each in parameters) {
            Classify(
                each.Type,
                parameter,
                each.RefKind is RefKind.Ref or RefKind.Out or RefKind.RefReadOnlyParameter
                    ? Position.Invariant
                    : Position.Contravariant,
                occurrences
            );
        }
    }

    /// <summary>
    ///     Records where <paramref name="parameter" /> lands inside <paramref name="type" />, given
    ///     that <paramref name="type" /> itself sits at <paramref name="position" />.
    /// </summary>
    /// <remarks>
    ///     ⚠ This is the composition, and it is the rule. A type argument's position is the enclosing
    ///     position composed with the declared variance of the parameter it fills: an <c>out</c>
    ///     parameter passes the position through, an <c>in</c> parameter flips it, and an invariant
    ///     parameter collapses it — after which nothing beneath can be reported in either direction.
    ///     An array's element type carries the position through unchanged, which is why
    ///     <c>T[] Get()</c> is reportable and <c>List&lt;T&gt; Get()</c> is not.
    /// </remarks>
    static void Classify(
        ITypeSymbol? type,
        ITypeParameterSymbol parameter,
        Position position,
        Occurrences occurrences
    ) {
        switch (type) {
            case null:
                return;

            case ITypeParameterSymbol candidate:
                if (SymbolEqualityComparer.Default.Equals(candidate, parameter)) {
                    occurrences.Note(position);
                }

                return;

            case IArrayTypeSymbol array:
                Classify(array.ElementType, parameter, position, occurrences);
                return;

            case IPointerTypeSymbol pointer:
                // A pointer type cannot mention a variance-relevant type parameter, but walking it
                // costs nothing and keeps "the parameter occurs here" from being missed.
                Classify(pointer.PointedAtType, parameter, Position.Invariant, occurrences);
                return;

            case INamedTypeSymbol named:
                ClassifyNamed(named, parameter, position, occurrences);
                return;
        }
    }

    static void ClassifyNamed(
        INamedTypeSymbol type,
        ITypeParameterSymbol parameter,
        Position position,
        Occurrences occurrences
    ) {
        // A nested generic carries its container's arguments, and they sit in the same position.
        if (type.ContainingType is { IsGenericType: true } container) {
            ClassifyNamed(container, parameter, position, occurrences);
        }

        var definition = type.OriginalDefinition;
        for (var i = 0; i < type.TypeArguments.Length; i++) {
            var declared = i < definition.TypeParameters.Length
                ? definition.TypeParameters[i].Variance
                : VarianceKind.None;

            var composed = declared switch {
                VarianceKind.Out => position,
                VarianceKind.In => Flip(position),
                _ => Position.Invariant
            };

            Classify(type.TypeArguments[i], parameter, composed, occurrences);
        }
    }
}
