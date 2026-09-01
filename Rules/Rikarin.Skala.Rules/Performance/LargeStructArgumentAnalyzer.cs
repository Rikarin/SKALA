using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using Rikarin.Skala.Rules.Metadata;
using Rikarin.Skala.Rules.Modernization;
using System.Collections.Immutable;
using System.Globalization;

namespace Rikarin.Skala.Rules.Performance;

/// <summary>SK4007: known-large struct locals/parameters passed by value in a loop body.</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class LargeStructArgumentAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.LargeStructArgument);
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.InvocationExpression);
    }

    static void Analyze(SyntaxNodeAnalysisContext context) {
        var invocation = (InvocationExpressionSyntax)context.Node;
        if (!InLoopBody(invocation)
            || context.SemanticModel.GetOperation(
                invocation,
                context.CancellationToken
            ) is not IInvocationOperation call) {
            return;
        }

        var options = context.Options.AnalyzerConfigOptionsProvider.GetOptions(invocation.SyntaxTree);
        var threshold = options.TryGetValue("dotnet_code_quality.SK4007.threshold", out var configured)
            && int.TryParse(configured, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            && parsed > 0
                ? parsed
                : 64;
        foreach (var argument in call.Arguments) {
            if (argument.IsImplicit
                || !argument.InConversion.IsIdentity
                || argument.Parameter is not { RefKind: RefKind.None, Type.TypeKind: TypeKind.Struct } parameter
                || argument.Value.Syntax is not ExpressionSyntax expression
                || context.SemanticModel.GetSymbolInfo(
                    PatternSafety.Unwrap(expression),
                    context.CancellationToken
                ).Symbol is not (ILocalSymbol or IParameterSymbol)
                || !SymbolEqualityComparer.Default.Equals(
                    context.SemanticModel.GetTypeInfo(expression, context.CancellationToken).Type,
                    parameter.Type
                )
                || !SymbolEqualityComparer.Default.Equals(argument.Value.Type, parameter.Type)) {
                continue;
            }

            var size = StructSizeLowerBound.Read(parameter.Type, context.SemanticModel, context.CancellationToken);
            if (size <= threshold) {
                continue;
            }

            var properties = ImmutableDictionary<string, string?>.Empty.Add(
                "skala.size.lower_bound",
                size.ToString(CultureInfo.InvariantCulture)
            );
            context.ReportDiagnostic(
                Diagnostic.Create(
                    Descriptor,
                    expression.GetLocation(),
                    properties,
                    "Struct `"
                    + parameter.Type.Name
                    + "` has at least "
                    + size.ToString(CultureInfo.InvariantCulture)
                    + " bytes of known field payload and is passed by value in a loop; review copying costs"
                )
            );
        }
    }

    static bool InLoopBody(SyntaxNode node) {
        foreach (var ancestor in node.Ancestors()) {
            if (ancestor is AnonymousFunctionExpressionSyntax
                or LocalFunctionStatementSyntax
                or BaseMethodDeclarationSyntax) {
                return false;
            }

            var body = ancestor switch {
                ForStatementSyntax loop => loop.Statement,
                CommonForEachStatementSyntax loop => loop.Statement,
                WhileStatementSyntax loop => loop.Statement,
                DoStatementSyntax loop => loop.Statement,
                _ => null
            };
            if (body?.Span.Contains(node.Span) == true) {
                return true;
            }
        }

        return false;
    }
}
