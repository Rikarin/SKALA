using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using Rikarin.Skala.Rules.Metadata;
using System.Collections.Immutable;
using System.Linq;

namespace Rikarin.Skala.Rules.Correctness;

/// <summary>
///     <c>SK2190</c> — a hash-based collection is built over a struct key that has written down no
///     equality at all, so every lookup goes through the reflection fallback.
/// </summary>
/// <remarks>
///     ⚠ <b><c>SK2011</c> was read before this was written, and the issue proposing the rule had it
///     backwards.</b> Issue #4 says <c>SK2011</c> reports at the <em>declaration</em>; it does not —
///     <c>InheritedValueTypeEqualsAnalyzer</c> registers on <c>InvocationExpression</c> and fires on
///     the <c>.Equals</c> call site. So the three inspections in that issue about a comparison
///     (<c>UsageOfDefaultStructEquality</c> and both <c>DefaultStructEqualityIsUsed</c> scopes) are
///     already covered where the comparison is *written*. What is not covered is the use site where
///     nothing is written at all: a <c>Dictionary&lt;S, …&gt;</c> has no <c>.Equals</c> anywhere in
///     it, and the reflection-based <c>ValueType.Equals</c> and <c>ValueType.GetHashCode</c> run on
///     every insert and every lookup for the lifetime of the collection.
///     <para>
///         ⚠ <b><c>CA1815</c> is not this rule and was measured rather than assumed.</b> A probe built
///         outside this repository with an empty <c>Directory.Build.props</c> shows <c>CA1815</c>
///         reporting nothing at the SDK's default analysis mode — not at <c>Hidden</c>, nothing at all
///         in the SARIF error log — and reporting at <c>AnalysisMode=All</c> on the struct's
///         *declaration*, whether or not anything ever compares it, and only when the struct is
///         publicly visible. Different span, different trigger, off by default.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class StructKeyWithoutEqualityAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.StructKeyWithoutEquality);

    /// <summary>
    ///     ⚠ The hash-based ones only. <c>SortedDictionary</c> and <c>SortedSet</c> order their keys
    ///     with <c>Comparer&lt;T&gt;.Default</c>, which needs <c>IComparable</c> and throws without
    ///     it rather than falling back to reflection — a different failure with a different repair.
    /// </summary>
    static readonly string[] HashedTypes = [
        "System.Collections.Generic.Dictionary`2",
        "System.Collections.Generic.HashSet`1",
        "System.Collections.Concurrent.ConcurrentDictionary`2"
    ];

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(static start => {
                var hashed = HashedTypes
                    .Select(start.Compilation.GetTypeByMetadataName)
                    .Where(static type => type is not null)
                    .ToImmutableArray();
                if (hashed.Length == 0) {
                    return;
                }

                start.RegisterOperationAction(context => Analyze(context, hashed), OperationKind.ObjectCreation);
            }
        );
    }

    static void Analyze(OperationAnalysisContext context, ImmutableArray<INamedTypeSymbol?> hashed) {
        var operation = (IObjectCreationOperation)context.Operation;
        if (operation.Type is not INamedTypeSymbol { IsGenericType: true } created
            || !hashed.Any(type => SymbolEqualityComparer.Default.Equals(type, created.OriginalDefinition))
            || created.TypeArguments.Length == 0
            || CarriesAComparer(operation)
            || created.TypeArguments[0] is not INamedTypeSymbol key
            || !LacksEqualityEntirely(key, context.Compilation)) {
            return;
        }

        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                operation.Syntax.GetLocation(),
                "`"
                + key.Name
                + "` declares no equality, so this collection hashes and compares it by reflection; "
                + "implement equality on the struct or pass an IEqualityComparer"
            )
        );
    }

    /// <summary>
    ///     ⚠ An explicit comparer is the repair, so a call that already passes one is not a finding.
    /// </summary>
    /// <remarks>
    ///     Judged on the <em>parameter</em> rather than on the argument's own type, because
    ///     <c>new Dictionary&lt;S, int&gt;(other, comparer)</c> and the single-argument overload have
    ///     the same shape at the call and only the parameter says which is which. An omitted optional
    ///     argument counts as no comparer.
    /// </remarks>
    static bool CarriesAComparer(IObjectCreationOperation operation) =>
        operation.Arguments.Any(static argument =>
            argument.ArgumentKind == ArgumentKind.Explicit
            && argument.Parameter?.Type.OriginalDefinition.ToDisplayString()
            == "System.Collections.Generic.IEqualityComparer<T>"
        );

    /// <summary>
    ///     A source struct that has said nothing about equality: no <c>Equals(object)</c> override
    ///     anywhere below <c>ValueType</c>, no <c>GetHashCode</c> override, no <c>IEquatable</c>, no
    ///     <c>operator ==</c>, and no typed <c>Equals</c> either.
    /// </summary>
    /// <remarks>
    ///     ⚠ <b>A typed <c>Equals(Self)</c> with no <c>IEquatable&lt;Self&gt;</c> withdraws the type
    ///     rather than confirming the finding, and that is a deliberate concession.</b>
    ///     <c>EqualityComparer&lt;T&gt;.Default</c> would indeed ignore that method and fall back to
    ///     reflection, so the defect is real — and it is already <c>SK2044</c>'s finding, reported on
    ///     the declaration where the missing interface can be added. Two rules arguing over one
    ///     type is what this catalogue avoids, so the type that half-declared equality belongs to the
    ///     rule that can tell it to finish.
    /// </remarks>
    static bool LacksEqualityEntirely(INamedTypeSymbol key, Compilation compilation) =>
        key is {
            TypeKind: TypeKind.Struct,
            IsRecord: false,
            IsTupleType: false,
            IsAnonymousType: false,
            SpecialType: SpecialType.None
        }
        && key.Locations.Any(static location => location.IsInSource)
        && EqualityMembers.BindsCompletely(key)
        && !EqualityMembers.InheritsObjectEquals(key)
        && EqualityMembers.HashCode(key) is null
        && EqualityMembers.Operator(key, "op_Equality") is null
        && !EqualityMembers.TypedEquals(key).Any()
        && !EqualityMembers.ImplementsEquatable(key, compilation);
}
