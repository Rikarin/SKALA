using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Rikarin.Skala.Rules.Metadata;
using System.Collections.Immutable;
using System.Linq;

namespace Rikarin.Skala.Rules.Correctness;

/// <summary><c>SK2002</c> — a pure call's result is discarded as a bare statement.</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class DiscardedPureResultAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.DiscardedPureResult);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.ExpressionStatement);
    }

    static void Analyze(SyntaxNodeAnalysisContext context) {
        if (context.Node is not ExpressionStatementSyntax { Expression: InvocationExpressionSyntax invocation }
            || context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol
            is not IMethodSymbol { ReturnsVoid: false } method) {
            return;
        }

        var pure = context.Compilation.GetTypeByMetadataName("System.Diagnostics.Contracts.PureAttribute");
        var declaration = method.ReducedFrom ?? method;
        if (!IsStringTransformation(method)
            && (pure is null
                || !declaration.GetAttributes()
                    .Any(attribute =>
                        SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, pure)
                    ))) {
            return;
        }

        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                invocation.GetLocation(),
                "result of pure method `" + method.Name + "` is discarded; use the result or explicitly discard it"
            )
        );
    }

    static bool IsStringTransformation(IMethodSymbol method) =>
        !method.IsStatic
        && method.ContainingType.SpecialType == SpecialType.System_String
        && method.ReturnType.SpecialType == SpecialType.System_String
        && method.Name is "Trim"
            or "TrimStart"
            or "TrimEnd"
            or "ToLower"
            or "ToLowerInvariant"
            or "ToUpper"
            or "ToUpperInvariant"
            or "Replace"
            or "Normalize"
            or "Substring"
            or "Insert"
            or "Remove"
            or "PadLeft"
            or "PadRight";
}
