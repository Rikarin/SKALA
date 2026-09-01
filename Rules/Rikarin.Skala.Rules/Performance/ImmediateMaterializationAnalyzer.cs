using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using Rikarin.Skala.Rules.Metadata;
using Rikarin.Skala.Rules.Modernization;
using System.Collections.Immutable;

namespace Rikarin.Skala.Rules.Performance;

/// <summary>SK4006: a temporary LINQ materialization used only as a foreach input.</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ImmediateMaterializationAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.ImmediateMaterialization);
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.ForEachStatement, SyntaxKind.ForEachVariableStatement);
    }

    static void Analyze(SyntaxNodeAnalysisContext context) {
        var loop = (CommonForEachStatementSyntax)context.Node;
        var model = context.SemanticModel;
        var cancellation = context.CancellationToken;
        if (!loop.AwaitKeyword.IsKind(SyntaxKind.None)
            || PatternSafety.Unwrap(loop.Expression) is not InvocationExpressionSyntax invocation
            || model.GetOperation(invocation, cancellation) is not IInvocationOperation {
                TargetMethod.Name: "ToList" or "ToArray",
                Arguments.Length: 1
            } call
            || !HotPathLinqAnalyzer.IsEnumerable(call.TargetMethod, context.Compilation)
            || call.Arguments[0].Value.Syntax is not ExpressionSyntax source
            || PatternSafety.StableVariable(model, source, cancellation) is not { } symbol) {
            return;
        }

        // Preserve obvious snapshot use, including the ToList fix for SK2007. A hidden mutation
        // through a called method still requires review, so this rule deliberately has no fix.
        foreach (var node in loop.Statement.DescendantNodesAndSelf()) {
            if (node is AwaitExpressionSyntax or YieldStatementSyntax
                || node is IdentifierNameSyntax name
                && SymbolEqualityComparer.Default.Equals(symbol, model.GetSymbolInfo(name, cancellation).Symbol)) {
                return;
            }
        }

        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                invocation.GetLocation(),
                "This temporary "
                + call.TargetMethod.Name
                + " result is used only for foreach; review whether a snapshot is needed"
            )
        );
    }
}
