using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Rikarin.Skala.Rules.Metadata;
using System.Collections.Immutable;
using System.Linq;

namespace Rikarin.Skala.Rules.Correctness;

/// <summary>
///     <c>SK2221</c> — an <c>[UnsafeAccessor]</c> declaration names a member the target type does not
///     have, so the first call throws.
/// </summary>
/// <remarks>
///     <c>UnsafeAccessorAttribute</c> is bound by the runtime, not by the compiler: the name in the
///     attribute is a string, nothing checks it, and a typo compiles cleanly and then throws
///     <c>MissingMethodException</c> or <c>MissingFieldException</c> the first time the accessor is
///     called. That is a compile-time-knowable fact reported at run time, which is exactly the trade an
///     analyzer exists to undo.
///     <para>
///         ⚠
///         <b>
///             Every shape reported here was confirmed by running it on .NET 10, not by reading the
///             documentation.
///         </b> A name the target does not declare throws <c>MissingFieldException</c>
///         — <i>Field not found: 'Target._buffer'</i>; a <c>Field</c> kind naming a member that is a
///         method throws the same; and <c>UnsafeAccessorKind.Constructor</c> carrying
///         <c>Name = "Create"</c> throws
///         <b>
///             <c>BadImageFormatException</c>
///         </b>,
///         <i>
///             Invalid usage of
///             UnsafeAccessorAttribute
///         </i>. The correctly spelled field accessor and the unnamed
///         constructor accessor both succeeded in the same run, so the probe was measuring this
///         rule's subject rather than a broken harness. That is what justifies <c>error</c> severity.
///     </para>
///     <para>
///         ⚠
///         <b>
///             The target type must be declared in this compilation's own source, and that restriction
///             is the whole reason the rule can be trusted.
///         </b> A reference assembly does not carry private
///         members — stripping them is what a reference assembly is for — so
///         <c>GetMembers("secret")</c> against a type from a referenced assembly returns nothing
///         whether the member is absent or merely invisible. Reporting on that would turn every correct
///         cross-assembly accessor, which is the commonest use of the attribute, into a false error. The
///         rule therefore proves what it can prove: for a target the compilation itself declares, the
///         member list is complete and a missing name is missing.
///     </para>
///     <para>
///         ⚠ <b>Signature comparison is not attempted and the omission is deliberate.</b> Overload
///         resolution against a private member set, through <c>ref</c>, <c>in</c>, <c>out</c>, generic
///         substitution and the attribute's own convention that the first parameter is the receiver, is
///         a second implementation of the runtime's binder — and a binder that is subtly wrong reports
///         working code as broken. What is checked instead is the fact that needs no binder: whether
///         <em>any</em> member of the named kind answers to that name at all.
///     </para>
///     <para>
///         ⚠ <b>Generic accessors are not reported</b>, though an upstream idea proposes it. Generic
///         <c>[UnsafeAccessor]</c> support was added after the attribute shipped and the set of shapes
///         the runtime accepts is version-dependent; a rule that called them all invalid would be
///         wrong on the newer runtimes and could not tell which one it was compiling for.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class UnsafeAccessorTargetAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.UnsafeAccessorTargetMismatch);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(static start => {
                var attribute = start.Compilation.GetTypeByMetadataName(
                    "System.Runtime.CompilerServices.UnsafeAccessorAttribute"
                );
                if (attribute is null) {
                    return;
                }

                start.RegisterSymbolAction(context => Analyze(context, attribute), SymbolKind.Method);
            }
        );
    }

    static void Analyze(SymbolAnalysisContext context, INamedTypeSymbol attributeType) {
        var accessor = (IMethodSymbol)context.Symbol;
        if (accessor.GetAttributes()
                .FirstOrDefault(a => SymbolEqualityComparer.Default.Equals(a.AttributeClass, attributeType))
            is not { } attribute) {
            return;
        }

        if (attribute.ConstructorArguments.Length != 1
            || attribute.ConstructorArguments[0].Value is not int rawKind) {
            return;
        }

        // ⚠ A generic accessor is out of scope; see the type's remarks.
        if (accessor.IsGenericMethod || accessor.ContainingType.IsGenericType) {
            return;
        }

        var kind = (AccessorKind)rawKind;

        // A constructor accessor names nothing: the runtime's own convention is that `Name` is either
        // unset or the literal `.ctor`, and anything else is a name that will never be looked for.
        // This needs no member list, so it holds for a target in any assembly.
        var declaredName = attribute.NamedArguments
            .FirstOrDefault(static pair => pair.Key == "Name")
            .Value.Value
            as string;

        if (kind == AccessorKind.Constructor) {
            AnalyzeConstructor(context, accessor, declaredName);
            return;
        }

        if (TargetType(accessor, kind) is not { } target) {
            return;
        }

        // ⚠ Source-declared targets only. See the type's remarks: a reference assembly carries no
        // private members, so on a metadata type "not found" and "not published" are the same answer.
        if (!SymbolEqualityComparer.Default.Equals(target.ContainingAssembly, context.Compilation.Assembly)
            || target.TypeKind is TypeKind.Error or TypeKind.TypeParameter or TypeKind.Dynamic) {
            return;
        }

        // With no `Name`, the accessor's own name is the name looked for.
        var name = declaredName ?? accessor.Name;

        var wantsField = kind is AccessorKind.Field or AccessorKind.StaticField;
        var candidates = Members(target, name, wantsField).ToList();

        if (candidates.Count > 0) {
            return;
        }

        Report(context, accessor, Message(target, name, wantsField, Members(target, name, !wantsField).Any()));
    }

    /// <summary>
    ///     ⚠ <c>UnsafeAccessorKind.Constructor</c> is answered without any member lookup, so this one
    ///     part holds for a target in any assembly.
    /// </summary>
    /// <remarks>
    ///     The runtime looks for <c>.ctor</c> and nothing else — measured by running it: an accessor
    ///     carrying <c>Name = "Create"</c> throws <c>BadImageFormatException</c> at the call.
    /// </remarks>
    static void AnalyzeConstructor(SymbolAnalysisContext context, IMethodSymbol accessor, string? declaredName) {
        if (declaredName is null || declaredName == ".ctor") {
            return;
        }

        Report(
            context,
            accessor,
            "`UnsafeAccessorKind.Constructor` ignores every name but `.ctor`, so `Name = \""
            + declaredName
            + "\"` names nothing the runtime will look for"
        );
    }

    /// <summary>Which of the two things went wrong, said in the words of the kind that was asked for.</summary>
    static string Message(INamedTypeSymbol target, string name, bool wantsField, bool otherKind) {
        if (!otherKind) {
            return "`"
                + target.Name
                + "` declares no member named `"
                + name
                + "`, so the first call to this accessor throws at run time";
        }

        return "`"
            + target.Name
            + "` has no "
            + (wantsField ? "field" : "method")
            + " named `"
            + name
            + "` — the member of that name is a "
            + (wantsField ? "method" : "field")
            + ", so the runtime binds nothing and the first call throws";
    }

    static System.Collections.Generic.IEnumerable<ISymbol> Members(
        INamedTypeSymbol target,
        string name,
        bool fields
    ) {
        // ⚠ Base types are walked. The runtime looks up the hierarchy for a method, and a rule that
        // stopped at the declared type would report an inherited private helper as absent.
        for (var current = target; current is not null; current = current.BaseType) {
            foreach (var member in current.GetMembers(name)) {
                if (fields ? member is IFieldSymbol : member is IMethodSymbol or IPropertySymbol) {
                    yield return member;
                }
            }
        }
    }

    /// <summary>
    ///     The type the accessor reaches into: the first parameter's type for every kind but the
    ///     constructor, which uses the return type.
    /// </summary>
    static INamedTypeSymbol? TargetType(IMethodSymbol accessor, AccessorKind kind) {
        if (kind == AccessorKind.Constructor) {
            return accessor.ReturnType as INamedTypeSymbol;
        }

        return accessor.Parameters.Length == 0 ? null : accessor.Parameters[0].Type as INamedTypeSymbol;
    }

    /// <summary>⚠ The first source location, and only that one — an accessor is reported once.</summary>
    static void Report(SymbolAnalysisContext context, IMethodSymbol accessor, string message) {
        var location = accessor.Locations.FirstOrDefault(static candidate => candidate.IsInSource);
        if (location is not null) {
            context.ReportDiagnostic(Diagnostic.Create(Descriptor, location, message));
        }
    }

    /// <summary>
    ///     ⚠ The runtime's <c>UnsafeAccessorKind</c> values, restated rather than referenced.
    /// </summary>
    /// <remarks>
    ///     The enum lives in <c>System.Private.CoreLib</c> and does not exist on the
    ///     <c>netstandard2.0</c> surface this assembly targets, so the analyzer reads the constant the
    ///     attribute's own metadata carries. The values are part of the shipped public contract and
    ///     cannot change.
    /// </remarks>
    enum AccessorKind {
        Constructor = 0,
        Method = 1,
        StaticMethod = 2,
        Field = 3,
        StaticField = 4
    }
}
