using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using Rikarin.Skala.Rules.Metadata;
using System.Collections.Immutable;

namespace Rikarin.Skala.Rules.Correctness;

/// <summary><c>SK2011</c> — a struct call binds to the inherited ValueType.Equals implementation.</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class InheritedValueTypeEqualsAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.InheritedValueTypeEquals);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.InvocationExpression);
    }

    static void Analyze(SyntaxNodeAnalysisContext context) {
        var invocation = (InvocationExpressionSyntax)context.Node;
        if (context.SemanticModel.GetOperation(invocation, context.CancellationToken)
            is not IInvocationOperation { TargetMethod.Name: "Equals", TargetMethod.IsStatic: false } call
            || call.TargetMethod.ContainingType.SpecialType != SpecialType.System_ValueType
            || call.Instance?.Type is not INamedTypeSymbol { TypeKind: TypeKind.Struct } type) {
            return;
        }

        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                invocation.GetLocation(),
                "`"
                + type.Name
                + "` uses inherited ValueType.Equals; implement typed equality to avoid boxing and fallback comparison"
            )
        );
    }
}
