using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Rikarin.Skala.Rules.Metadata;
using System;
using System.Collections.Immutable;

namespace Rikarin.Skala.Rules.Maintainability;

/// <summary><c>SK7051</c> — a suppression attribute with no meaningful justification.</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class SuppressMessageWithoutJustificationAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor =
        SkalaRule.Descriptor(RuleIds.SuppressMessageWithoutJustification);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(static start => {
                var suppress = start.Compilation.GetTypeByMetadataName(
                    "System.Diagnostics.CodeAnalysis.SuppressMessageAttribute"
                );
                var unconditional = start.Compilation.GetTypeByMetadataName(
                    "System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessageAttribute"
                );
                if (suppress is null && unconditional is null) {
                    return;
                }

                start.RegisterSyntaxNodeAction(
                    context => Analyze(context, suppress, unconditional),
                    SyntaxKind.Attribute
                );
            }
        );
    }

    static void Analyze(
        SyntaxNodeAnalysisContext context,
        INamedTypeSymbol? suppress,
        INamedTypeSymbol? unconditional
    ) {
        var attribute = (AttributeSyntax)context.Node;
        if (!AttributeBinding.Matches(context, attribute, suppress, unconditional)) {
            return;
        }

        var justification = AttributeBinding.NamedArgument(attribute, "Justification");
        if (justification is not null
            && context.SemanticModel.GetConstantValue(justification.Expression, context.CancellationToken)
            is { HasValue: true, Value: string value }
            && Meaningful(value)) {
            return;
        }

        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                attribute.Name.GetLocation(),
                "suppression attribute has no meaningful `Justification`"
            )
        );
    }

    static bool Meaningful(string value) {
        var text = value.Trim();
        return text.Length > 0
            && !text.StartsWith("TODO", StringComparison.OrdinalIgnoreCase)
            && !text.StartsWith("FIXME", StringComparison.OrdinalIgnoreCase)
            && !text.Equals("TBD", StringComparison.OrdinalIgnoreCase)
            && !text.Equals("N/A", StringComparison.OrdinalIgnoreCase)
            && !text.Equals("NONE", StringComparison.OrdinalIgnoreCase);
    }
}
