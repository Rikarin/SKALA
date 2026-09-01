using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using Rikarin.Skala.Rules.Metadata;
using Rikarin.Skala.Rules.Modernization;
using System.Collections.Immutable;
using System.Linq;

namespace Rikarin.Skala.Rules.Performance;

/// <summary>SK4004: boxing a value-constrained type parameter to a contract it already provides.</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ConstrainedBoxingAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.ConstrainedBoxing);
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.InvocationExpression);
    }

    static void Analyze(SyntaxNodeAnalysisContext context) {
        var invocation = (InvocationExpressionSyntax)context.Node;
        if (invocation.Expression is not MemberAccessExpressionSyntax access
            || PatternSafety.Unwrap(access.Expression) is not CastExpressionSyntax cast) {
            return;
        }

        var model = context.SemanticModel;
        var cancellation = context.CancellationToken;
        if (model.GetTypeInfo(cast.Expression, cancellation).Type is not ITypeParameterSymbol {
                HasValueTypeConstraint: true
            } parameter
            || model.GetTypeInfo(cast.Type, cancellation).Type is not INamedTypeSymbol {
                TypeKind: TypeKind.Interface
            } contract
            || !model.ClassifyConversion(cast.Expression, contract).IsBoxing
            || !parameter.ConstraintTypes.Any(type => SymbolEqualityComparer.Default.Equals(type, contract)
                || type.AllInterfaces.Contains(contract, SymbolEqualityComparer.Default)
            )
            || model.GetOperation(invocation, cancellation) is not IInvocationOperation {
                TargetMethod.ContainingType.TypeKind: TypeKind.Interface
            }
            || NullComparison.InsideExpressionTree(model, invocation, cancellation)) {
            return;
        }

        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                cast.GetLocation(),
                "This interface cast boxes `"
                + parameter.Name
                + "` despite its existing constraint; review a constrained call"
            )
        );
    }
}
