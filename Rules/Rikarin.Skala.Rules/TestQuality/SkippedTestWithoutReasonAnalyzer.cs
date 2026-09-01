using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Rikarin.Skala.Rules.Metadata;
using System;
using System.Collections.Immutable;
using System.Linq;

namespace Rikarin.Skala.Rules.TestQuality;

/// <summary><c>SK8006</c> — an xUnit test skipped without a meaningful reason.</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class SkippedTestWithoutReasonAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.SkippedTestWithoutReason);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(static start => {
                var fact = start.Compilation.GetTypeByMetadataName("Xunit.FactAttribute");
                var theory = start.Compilation.GetTypeByMetadataName("Xunit.TheoryAttribute");
                if (fact is null && theory is null) {
                    return;
                }

                start.RegisterSyntaxNodeAction(
                    context => Analyze(context, fact, theory),
                    SyntaxKind.Attribute
                );
            }
        );
    }

    static void Analyze(SyntaxNodeAnalysisContext context, INamedTypeSymbol? fact, INamedTypeSymbol? theory) {
        var attribute = (AttributeSyntax)context.Node;
        if (context.SemanticModel.GetSymbolInfo(attribute, context.CancellationToken).Symbol
            is not IMethodSymbol constructor
            || (!SymbolEqualityComparer.Default.Equals(constructor.ContainingType, fact)
                && !SymbolEqualityComparer.Default.Equals(constructor.ContainingType, theory))) {
            return;
        }

        var skip = attribute.ArgumentList?.Arguments.FirstOrDefault(static argument =>
            argument.NameEquals?.Name.Identifier.ValueText == "Skip"
        );
        if (skip is null) {
            return;
        }

        var constant = context.SemanticModel.GetConstantValue(skip.Expression, context.CancellationToken);
        if (!constant.HasValue || constant.Value is string value && Meaningful(value)) {
            return;
        }

        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                skip.Expression.GetLocation(),
                "skipped test has no meaningful reason"
            )
        );
    }

    static bool Meaningful(string value) {
        var text = value.Trim();
        return text.Length > 0
            && !text.StartsWith("TODO", StringComparison.OrdinalIgnoreCase)
            && !text.StartsWith("FIXME", StringComparison.OrdinalIgnoreCase)
            && !text.Equals("TBD", StringComparison.OrdinalIgnoreCase);
    }
}
