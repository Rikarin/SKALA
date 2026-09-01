using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Rikarin.Skala.Rules.Metadata;
using System.Collections.Immutable;
using System.Linq;

namespace Rikarin.Skala.Rules.Correctness;

/// <summary><c>SK2010</c> — string comparisons whose culture policy is implicit.</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ImplicitStringCultureAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.ImplicitStringCulture);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.InvocationExpression);
    }

    static void Analyze(SyntaxNodeAnalysisContext context) {
        var invocation = (InvocationExpressionSyntax)context.Node;
        if (context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol
            is not IMethodSymbol method
            || method.ContainingType.SpecialType != SpecialType.System_String) {
            return;
        }

        var comparison = method.IsStatic
            && method.Name == "Compare"
            && !method.Parameters.Any(parameter => IsCultureParameter(parameter.Type, context.Compilation));
        var casing = !method.IsStatic
            && method.Name is "ToLower" or "ToUpper"
            && method.Parameters.IsEmpty
            && IsCompared(invocation);
        if (!comparison && !casing) {
            return;
        }

        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                invocation.GetLocation(),
                "string comparison implicitly uses the current culture; choose an explicit StringComparison or CultureInfo"
            )
        );
    }

    static bool IsCultureParameter(ITypeSymbol type, Compilation compilation) =>
        SymbolEqualityComparer.Default.Equals(type, compilation.GetTypeByMetadataName("System.StringComparison"))
        || SymbolEqualityComparer.Default.Equals(
            type,
            compilation.GetTypeByMetadataName("System.Globalization.CultureInfo")
        );

    static bool IsCompared(ExpressionSyntax expression) {
        while (expression.Parent is ParenthesizedExpressionSyntax parenthesized) {
            expression = parenthesized;
        }

        return expression.Parent is BinaryExpressionSyntax binary
            && (binary.IsKind(SyntaxKind.EqualsExpression) || binary.IsKind(SyntaxKind.NotEqualsExpression));
    }
}
