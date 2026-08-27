using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Rikarin.Skala.Rules.Metadata;

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
        context.RegisterCompilationStartAction(static start => {
                var enumType = start.Compilation.GetTypeByMetadataName("System.Enum");
                if (enumType is null || !HasGenericGetValues(enumType)) {
                    return;
                }

                start.RegisterSyntaxNodeAction(
                    context => Analyze(context, enumType),
                    SyntaxKind.InvocationExpression
                );
            }
        );
    }

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
                IsStatic: true,
                Arity: 0,
                Parameters.Length: 1
            } method
            || !SymbolEqualityComparer.Default.Equals(method.ContainingType, enumType)) {
            return;
        }

        // ⚠ A `typeof` over a type parameter is not the generic overload's argument — `GetValues<T>`
        // needs `T : struct, Enum`, and a bare `T` here has neither constraint proven.
        if (model.GetTypeInfo(typeOf.Type, cancellation).Type is not INamedTypeSymbol { TypeKind: TypeKind.Enum }) {
            return;
        }

        if (!IsSafePosition(invocation)) {
            return;
        }

        var replacement = access.Expression + ".GetValues<" + typeOf.Type + ">()";
        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                Location.Create(invocation.SyntaxTree, invocation.Span),
                FixEdits.Pack((invocation.Span, replacement)),
                "Use Enum.GetValues<" + typeOf.Type + ">(), which is typed and does not box"
            )
        );
    }

    static bool IsSafePosition(InvocationExpressionSyntax invocation) =>
        invocation.Parent switch {
            ForEachStatementSyntax statement => ReferenceEquals(statement.Expression, invocation),
            ForEachVariableStatementSyntax statement => ReferenceEquals(statement.Expression, invocation),
            MemberAccessExpressionSyntax {
                RawKind: (int)SyntaxKind.SimpleMemberAccessExpression
            } access => ReferenceEquals(access.Expression, invocation),
            _ => false
        };
}
