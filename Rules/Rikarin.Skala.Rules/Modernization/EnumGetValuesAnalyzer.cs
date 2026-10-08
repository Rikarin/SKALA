using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Rikarin.Skala.Rules.Metadata;
using System.Collections.Immutable;

namespace Rikarin.Skala.Rules.Modernization;

/// <summary>
///     <c>SK1035</c> — <c>Enum.GetValues(typeof(T))</c> where <c>Enum.GetValues&lt;T&gt;()</c> exists.
/// </summary>
/// <remarks>
///     ⚠ The non-generic overload returns <c>System.Array</c> and the generic one returns <c>T[]</c>,
///     so the rewrite changes the expression's type. That is an improvement everywhere it compiles and
///     a break where the <c>Array</c>-ness was being used, and there is no cheap way to prove which. So
///     the rule fires only in the two positions where <c>T[]</c> is unambiguously fine: the collection
///     of a <c>foreach</c>, and the receiver of a LINQ call. Everywhere else it is silent — which on a
///     corpus means it fires rarely, and that is the intended trade.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class EnumGetValuesAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.GenericEnumGetvalues);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        var registrar = PartialConstructorDefinitions.Visiting(context);
        registrar.RegisterCompilationStartAction(static start => {
                if (start.Compilation.GetTypeByMetadataName("System.Enum") is not { } enumType
                    || !HasGenericGetValues(enumType)) {
                    return;
                }

                // ⚠ #511: a *member* availability question is per target framework exactly as a type
                // one is. `System.Enum` exists on every moniker, so nothing that only watched type
                // lookups saw this; `GetValues<T>()` is .NET 5+, and on a `netstandard2.1;net10.0`
                // project the net10.0 leg reported a rewrite the netstandard2.1 leg cannot compile
                // (CS0308). The whole predicate is asked of every sibling.
                var unavailable = FrameworkAvailability.PathsWithout(start.Options, Supports);
                start.RegisterSyntaxNodeAction(
                    context => {
                        if (unavailable.IsEmpty || !unavailable.Contains(context.Node.SyntaxTree.FilePath)) {
                            Analyze(context, enumType);
                        }
                    },
                    SyntaxKind.InvocationExpression
                );
            }
        );
    }

    /// <summary>Whether <paramref name="compilation" />'s <c>System.Enum</c> has <c>GetValues&lt;T&gt;()</c>.</summary>
    static bool Supports(Compilation compilation) =>
        compilation.GetTypeByMetadataName("System.Enum") is { } enumType && HasGenericGetValues(enumType);

    static bool HasGenericGetValues(INamedTypeSymbol enumType) {
        foreach (var member in enumType.GetMembers("GetValues")) {
            if (member is IMethodSymbol { IsStatic: true, Arity: 1, Parameters.Length: 0 }) {
                return true;
            }
        }

        return false;
    }

    static void Analyze(SyntaxNodeAnalysisContext context, INamedTypeSymbol enumType) {
        var invocation = (InvocationExpressionSyntax)context.Node;
        if (invocation.ArgumentList.Arguments.Count != 1
            || invocation.Expression is not MemberAccessExpressionSyntax {
                Name: IdentifierNameSyntax { Identifier.ValueText: "GetValues" }
            } access) {
            return;
        }

        if (invocation.ArgumentList.Arguments[0].Expression is not TypeOfExpressionSyntax typeOf) {
            return;
        }

        var cancellation = context.CancellationToken;
        var model = context.SemanticModel;
        if (model.GetSymbolInfo(invocation, cancellation).Symbol is not IMethodSymbol {
                IsStatic: true, Arity: 0, Parameters.Length: 1
            } method
            || !SymbolEqualityComparer.Default.Equals(method.ContainingType, enumType)) {
            return;
        }

        // ⚠ A `typeof` over a type parameter is not the generic overload's argument — `GetValues<T>`
        // needs `T : struct, Enum`, and a bare `T` here has neither constraint proven.
        if (model.GetTypeInfo(typeOf.Type, cancellation).Type is not INamedTypeSymbol { TypeKind: TypeKind.Enum }) {
            return;
        }

        var replacement = access.Expression + ".GetValues<" + typeOf.Type + ">()";
        if (!IsSafePosition(invocation, replacement, model, cancellation)) {
            return;
        }

        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                Location.Create(invocation.SyntaxTree, invocation.Span),
                FixEdits.Pack((invocation.Span, replacement)),
                "Use Enum.GetValues<" + typeOf.Type + ">(), which is typed and does not box"
            )
        );
    }

    /// <summary>
    ///     Whether the expression's type changing from <c>Array</c> to <c>T[]</c> is invisible here.
    /// </summary>
    /// <remarks>
    ///     ⚠ #425: "a <c>foreach</c> collection" was not enough, because the loop's element type is the
    ///     collection's. Measured for #412's audit: <c>foreach (var v in …)</c> makes <c>v</c> an
    ///     <c>object</c> before and the enum after, so <c>Show(v)</c> picked another overload and
    ///     <c>v = "x"</c> became CS0029; and <c>foreach (long n in …)</c> unboxed an <c>object</c> before
    ///     (<c>InvalidCastException</c>) and converts an enum after. So the loop variable must be written
    ///     as the enum itself or as <c>object</c>, which are exactly the two types that read the same
    ///     element both ways. A member access must bind, with the generic call in its place, to the very
    ///     same member — <c>Cast&lt;T&gt;()</c>, <c>Length</c> — and not to one the typed array newly
    ///     makes applicable.
    /// </remarks>
    static bool IsSafePosition(
        InvocationExpressionSyntax invocation,
        string replacement,
        SemanticModel model,
        System.Threading.CancellationToken cancellation
    ) {
        switch (invocation.Parent) {
            case ForEachStatementSyntax statement when ReferenceEquals(statement.Expression, invocation):
                if (statement.Type.IsVar) {
                    return false;
                }

                var element = model.GetTypeInfo(statement.Type, cancellation).Type;
                var enumType = model.GetTypeInfo(
                        ((TypeOfExpressionSyntax)invocation.ArgumentList.Arguments[0].Expression).Type,
                        cancellation
                    )
                    .Type;
                return element is not null
                    && (element.SpecialType == SpecialType.System_Object
                        || SymbolEqualityComparer.Default.Equals(element, enumType));

            case MemberAccessExpressionSyntax { RawKind: (int)SyntaxKind.SimpleMemberAccessExpression } access
                when ReferenceEquals(access.Expression, invocation):
                var was = model.GetSymbolInfo(access, cancellation).Symbol;
                return was is not null
                    && FixRebind.TrySpeculate(
                        model,
                        invocation,
                        SyntaxFactory.ParseExpression(replacement),
                        out var speculative,
                        out var placed
                    )
                    && placed.Parent is MemberAccessExpressionSyntax rebound
                    && SymbolEqualityComparer.Default.Equals(
                        was,
                        speculative.GetSymbolInfo(rebound, cancellation).Symbol
                    );

            default:
                return false;
        }
    }
}
