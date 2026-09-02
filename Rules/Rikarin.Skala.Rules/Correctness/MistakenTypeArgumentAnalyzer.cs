using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Rikarin.Skala.Rules.Metadata;
using System;
using System.Collections.Immutable;

namespace Rikarin.Skala.Rules.Correctness;

/// <summary>
///     <c>SK2233</c> — a written <c>typeof(…)</c> that cannot satisfy the contract the API states.
/// </summary>
/// <remarks>
///     <para>
///         The API says which type it needs and enforces it with a run-time <c>ArgumentException</c>;
///         <c>typeof(…)</c> says which type it was given, right there in the source. Both halves are
///         visible at build time and the compiler relates neither to the other, because <c>Type</c> is
///         <c>Type</c>.
///     </para>
///     <para>
///         ⚠ <b>The failure is total rather than conditional.</b> There is no input on which
///         <c>Enum.GetValues(typeof(Widget))</c> succeeds, which is what separates this from a rule
///         that flags a risk.
///     </para>
///     <para>
///         ⚠ <b>A closed table, matched by parameter <em>name</em> and never by index</b> — the same
///         discipline <c>taint.json</c> uses, and for the same reason: an overload that inserts a
///         parameter shifts every index and changes nothing about a name.
///     </para>
///     <para>
///         ⚠ <b><c>SK1035</c> and this rule cannot both fire.</b> <c>SK1035</c> offers
///         <c>Enum.GetValues&lt;T&gt;()</c> and needs the argument to *be* an enum, because the
///         generic overload is constrained <c>struct, Enum</c>. This one needs it not to be.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class MistakenTypeArgumentAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.MistakenTypeArgument);

    enum Contract {
        Enum,
        Attribute,
        Delegate,
        Instantiable
    }

    /// <summary>Containing type → the parameter name that carries the constraint, and which one.</summary>
    /// <remarks>
    ///     ⚠ Every row is an API whose documentation states the requirement and whose implementation
    ///     throws when it is not met. Nothing here is a style preference, which is why the rule can
    ///     afford to be a table rather than a heuristic.
    /// </remarks>
    static readonly (string Type, string Parameter, Contract Contract)[] Contracts = [
        ("System.Enum", "enumType", Contract.Enum),
        ("System.Attribute", "attributeType", Contract.Attribute),
        ("System.Reflection.CustomAttributeExtensions", "attributeType", Contract.Attribute),
        ("System.Reflection.MemberInfo", "attributeType", Contract.Attribute),
        ("System.Reflection.ICustomAttributeProvider", "attributeType", Contract.Attribute),
        ("System.Delegate", "type", Contract.Delegate),
        ("System.Activator", "type", Contract.Instantiable)
    ];

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.InvocationExpression);
    }

    static void Analyze(SyntaxNodeAnalysisContext context) {
        var invocation = (InvocationExpressionSyntax)context.Node;
        if (invocation.ContainsDiagnostics || invocation.ArgumentList.Arguments.Count == 0) {
            return;
        }

        var model = context.SemanticModel;
        var cancellation = context.CancellationToken;
        if (model.GetSymbolInfo(invocation, cancellation).Symbol is not IMethodSymbol method) {
            return;
        }

        if (Match(method) is not { } matched) {
            return;
        }

        var (parameterName, contract) = matched;
        if (Argument(invocation, method, parameterName) is not TypeOfExpressionSyntax typeOf) {
            return;
        }

        // ⚠ The operand's kind must be a fact. `typeof(T)` inside a generic method names a different
        // type at every instantiation, and a `T` constrained `struct, Enum` is an enum at every one
        // of them — so a type parameter is declined rather than measured.
        if (model.GetTypeInfo(typeOf.Type, cancellation).Type is not { } argument
            || argument.TypeKind is TypeKind.Error or TypeKind.Dynamic or TypeKind.TypeParameter or TypeKind.Unknown) {
            return;
        }

        if (Satisfies(argument, contract, model.Compilation)) {
            return;
        }

        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                typeOf.GetLocation(),
                "`"
                + method.ContainingType.Name
                + "."
                + method.Name
                + "` requires `"
                + parameterName
                + "` to be "
                + Requirement(contract)
                + ", and `"
                + argument.ToDisplayString()
                + "` is not — this throws on every call"
            )
        );
    }

    static (string Parameter, Contract Contract)? Match(IMethodSymbol method) {
        var containing = method.ContainingType?.ToDisplayString();
        if (containing is null) {
            return null;
        }

        foreach (var (type, parameter, contract) in Contracts) {
            if (string.Equals(containing, type, StringComparison.Ordinal)) {
                return (parameter, contract);
            }
        }

        return null;
    }

    /// <summary>
    ///     The argument bound to <paramref name="parameterName" />, whether it was written positionally
    ///     or by name.
    /// </summary>
    /// <remarks>
    ///     ⚠ Resolved through the parameter list rather than by counting, because an extension method
    ///     called in reduced form shifts every index by one and a named argument shifts them
    ///     arbitrarily. The <c>params</c> tail is not walked into: a <c>Type</c> inside one is not the
    ///     constrained parameter.
    /// </remarks>
    static ExpressionSyntax? Argument(
        InvocationExpressionSyntax invocation,
        IMethodSymbol method,
        string parameterName
    ) {
        var index = -1;
        for (var i = 0; i < method.Parameters.Length; i++) {
            if (string.Equals(method.Parameters[i].Name, parameterName, StringComparison.Ordinal)) {
                index = i;
                break;
            }
        }

        if (index < 0) {
            return null;
        }

        var arguments = invocation.ArgumentList.Arguments;
        var positional = 0;
        foreach (var argument in arguments) {
            if (argument.NameColon is { Name.Identifier.ValueText: var named }) {
                if (string.Equals(named, parameterName, StringComparison.Ordinal)) {
                    return argument.Expression;
                }

                continue;
            }

            if (positional == index) {
                return argument.Expression;
            }

            positional++;
        }

        return null;
    }

    static bool Satisfies(ITypeSymbol argument, Contract contract, Compilation compilation) =>
        contract switch {
            Contract.Enum => argument.TypeKind == TypeKind.Enum,
            Contract.Attribute => DerivesFrom(argument, compilation.GetTypeByMetadataName("System.Attribute")),
            Contract.Delegate => argument.TypeKind == TypeKind.Delegate
                || DerivesFrom(argument, compilation.GetTypeByMetadataName("System.Delegate")),

            // ⚠ `IsAbstract` is true for an interface as well as for an abstract class, and a static
            // class is abstract *and* sealed. All three are reported and everything else is silent:
            // a value type, a sealed class, an open generic left unbound by `typeof(List<>)` — the
            // last of these throws too, and is not reported, because it is the one shape where an
            // author may be building a closed type from it later in a way this rule cannot see.
            Contract.Instantiable => argument is not {
                TypeKind: TypeKind.Interface
            } and not { IsAbstract: true } and not { IsStatic: true },
            _ => true
        };

    static bool DerivesFrom(ITypeSymbol candidate, INamedTypeSymbol? target) {
        if (target is null) {
            // ⚠ An unresolvable framework type means the compilation cannot see the contract, and a
            // rule that answered anyway would be answering from its own table rather than from the
            // code in front of it.
            return true;
        }

        for (var current = candidate; current is not null; current = current.BaseType) {
            if (SymbolEqualityComparer.Default.Equals(current, target)) {
                return true;
            }
        }

        return false;
    }

    static string Requirement(Contract contract) =>
        contract switch {
            Contract.Enum => "an enum",
            Contract.Attribute => "an attribute type",
            Contract.Delegate => "a delegate type",
            _ => "a type that can be instantiated"
        };
}
