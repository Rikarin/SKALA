using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Rikarin.Skala.Rules.Metadata;
using System.Collections.Immutable;

namespace Rikarin.Skala.Rules.Correctness;

/// <summary>
///     <c>SK2154</c> — a sort that falls back to a <c>Comparer&lt;T&gt;.Default</c> which cannot exist.
/// </summary>
/// <remarks>
///     docs/plan/08-rule-catalogue.md § "Culture, comparison policy and query shape".
///     <para>
///         <c>Comparer&lt;T&gt;.Default</c> throws <see cref="System.InvalidOperationException" /> on the
///         first comparison it is asked to make when <c>T</c> implements neither
///         <c>IComparable&lt;T&gt;</c> nor <c>IComparable</c>. The call compiles cleanly, and it needs
///         <em>two</em> elements to fail — so an empty list and a single-element list both sort
///         successfully, which is what the unit test contains and what production does not.
///     </para>
///     <para>
///         ⚠
///         <b>
///             The question is only decidable for a sealed type, and that is the finding rather than a
///             limitation to apologise for.
///         </b> <c>Comparer&lt;T&gt;.Default</c> for an unsealed <c>T</c>
///         builds an <c>ObjectComparer</c> that casts each <em>element</em> to <c>IComparable</c> at run
///         time, so a <c>List&lt;Animal&gt;</c> holding <c>Dog : Animal, IComparable&lt;Dog&gt;</c> sorts
///         correctly even though <c>Animal</c> implements nothing. A non-sealed class therefore cannot
///         be reported without guessing what is in the list.
///     </para>
///     <para>
///         ⚠ <b>A type parameter is never reported, and this was the open question on the issue.</b> The
///         answer is that it is not decidable: <c>T</c> in an open generic is substituted at every call
///         site and <c>IComparable</c> may well be among the substitutions, so there is no fact here to
///         check. An interface, <c>object</c>, <c>dynamic</c> and an error type are silent for the same
///         reason.
///     </para>
///     <para>
///         ⚠ <b><c>Nullable&lt;T&gt;</c> is unwrapped rather than tested.</b> <c>Nullable&lt;T&gt;</c>
///         implements neither interface itself, so a literal reading reports <c>List&lt;int?&gt;.Sort()</c>
///         — which works, because <c>ComparerHelpers</c> builds a dedicated <c>NullableComparer</c>
///         whenever the underlying type is comparable. That is the false positive this rule would
///         otherwise have shipped with.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class SortWithoutOrderingAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.SortWithoutOrdering);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(static start => {
                var comparable = start.Compilation.GetTypeByMetadataName("System.IComparable");
                var generic = start.Compilation.GetTypeByMetadataName("System.IComparable`1");
                if (comparable is null || generic is null) {
                    return;
                }

                var known = new Frame(
                    comparable,
                    generic,
                    start.Compilation.GetTypeByMetadataName("System.Collections.Generic.List`1"),
                    start.Compilation.GetTypeByMetadataName("System.Array"),
                    start.Compilation.GetTypeByMetadataName("System.Linq.Enumerable"),
                    start.Compilation.GetTypeByMetadataName("System.Nullable`1")
                );

                start.RegisterSyntaxNodeAction(context => Analyze(context, known), SyntaxKind.InvocationExpression);
            }
        );
    }

    sealed class Frame {
        public Frame(
            INamedTypeSymbol comparable,
            INamedTypeSymbol generic,
            INamedTypeSymbol? list,
            INamedTypeSymbol? array,
            INamedTypeSymbol? enumerable,
            INamedTypeSymbol? nullable
        ) {
            Comparable = comparable;
            Generic = generic;
            List = list;
            Array = array;
            Enumerable = enumerable;
            Nullable = nullable;
        }

        public INamedTypeSymbol Comparable { get; }

        public INamedTypeSymbol Generic { get; }

        public INamedTypeSymbol? List { get; }

        public INamedTypeSymbol? Array { get; }

        public INamedTypeSymbol? Enumerable { get; }

        public INamedTypeSymbol? Nullable { get; }
    }

    static void Analyze(SyntaxNodeAnalysisContext context, Frame frame) {
        var invocation = (InvocationExpressionSyntax)context.Node;
        if (context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol
            is not IMethodSymbol method) {
            return;
        }

        var ordered = Ordered(method, frame);
        if (ordered is null || !ThrowsAtRuntime(ordered, frame)) {
            return;
        }

        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                invocation.GetLocation(),
                "`"
                + method.Name
                + "` falls back to Comparer<"
                + ordered.Name
                + ">.Default, and `"
                + ordered.Name
                + "` implements neither IComparable<"
                + ordered.Name
                + "> nor IComparable, so this throws on the first comparison — which needs two "
                + "elements, so an empty or single-element test passes"
            )
        );
    }

    /// <summary>The type whose ordering is being asked for, or <c>null</c> if this is not a sort.</summary>
    static ITypeSymbol? Ordered(IMethodSymbol method, Frame frame) {
        // ⚠ Unreduce before counting parameters. Called as an extension, `OrderBy(key)` presents one
        // parameter and `OrderBy(key, comparer)` presents two — so a count of 2 against the reduced
        // form selects exactly the overload that *supplies* the ordering and misses every one that
        // does not. The rule read as working because `List<T>.Sort()` covered the positives while the
        // whole LINQ arm was inverted; the comparer negative is what exposed it.
        var definition = (method.ReducedFrom ?? method).OriginalDefinition;
        var container = method.ContainingType?.OriginalDefinition;

        // `List<T>.Sort()`. Every other overload takes a `Comparison<T>` or an `IComparer<T>`, which
        // is the author supplying the ordering this rule reports the absence of.
        if (frame.List is not null
            && SymbolEqualityComparer.Default.Equals(container, frame.List)
            && method.Name == "Sort"
            && definition.Parameters.Length == 0
            && method.ContainingType is { TypeArguments.Length: 1 } list) {
            return list.TypeArguments[0];
        }

        // `Array.Sort<T>(T[])`. The non-generic `Sort(Array)` gives no element type to check.
        if (frame.Array is not null
            && SymbolEqualityComparer.Default.Equals(container, frame.Array)
            && method.Name == "Sort"
            && definition.Parameters.Length == 1
            && method.TypeArguments.Length == 1) {
            return method.TypeArguments[0];
        }

        if (frame.Enumerable is null || !SymbolEqualityComparer.Default.Equals(container, frame.Enumerable)) {
            return null;
        }

        // ⚠ `Queryable`'s overloads are excluded by this containing-type check alone: an `OrderBy`
        // that becomes an `ORDER BY` is ordered by the database and never touches
        // `Comparer<T>.Default`.
        switch (method.Name) {
            // `OrderBy<TSource, TKey>(source, keySelector)` — two parameters unreduced. The
            // three-parameter overload is the one taking the comparer.
            case "OrderBy":
            case "OrderByDescending":
            case "ThenBy":
            case "ThenByDescending":
                return definition.Parameters.Length == 2 && method.TypeArguments.Length == 2
                    ? method.TypeArguments[1]
                    : null;

            // `Order<T>(source)` and `OrderDescending<T>(source)` order by the element itself.
            case "Order":
            case "OrderDescending":
                return definition.Parameters.Length == 1 && method.TypeArguments.Length == 1
                    ? method.TypeArguments[0]
                    : null;

            default:
                return null;
        }
    }

    static bool ThrowsAtRuntime(ITypeSymbol type, Frame frame) {
        // ⚠ `int?` sorts fine even though `Nullable<T>` implements neither interface, because
        // `ComparerHelpers` builds a `NullableComparer` over the underlying type. Unwrap first.
        if (frame.Nullable is not null
            && type is INamedTypeSymbol { TypeArguments.Length: 1 } candidate
            && SymbolEqualityComparer.Default.Equals(candidate.OriginalDefinition, frame.Nullable)) {
            type = candidate.TypeArguments[0];
        }

        // The decidability gate. Anything whose runtime type may be a subtype is out of reach, and so
        // is anything with no declaration to read.
        if (type.TypeKind is TypeKind.TypeParameter
            or TypeKind.Interface
            or TypeKind.Error
            or TypeKind.Dynamic
            or TypeKind.Delegate
            or TypeKind.Array
            || type.SpecialType == SpecialType.System_Object
            || (type.TypeKind == TypeKind.Class && !type.IsSealed)) {
            return false;
        }

        // An enum's ordering comes from `System.Enum`, which implements `IComparable`. Named rather
        // than left to the interface walk, because the walk answers this one inconsistently across
        // reference sets and a wrong `false` here is a false positive on every `OrderBy` over an enum.
        if (type.TypeKind == TypeKind.Enum) {
            return false;
        }

        foreach (var implemented in type.AllInterfaces) {
            if (SymbolEqualityComparer.Default.Equals(implemented, frame.Comparable)
                || SymbolEqualityComparer.Default.Equals(implemented.OriginalDefinition, frame.Generic)) {
                return false;
            }
        }

        return true;
    }
}
