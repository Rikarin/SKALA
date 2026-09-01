using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Rikarin.Skala.Rules.Metadata;
using System.Collections.Immutable;
using System.Linq;

namespace Rikarin.Skala.Rules.Maintainability;

/// <summary>
///     <c>SK7071</c> — <c>[ExcludeFromCodeCoverage]</c> with no <c>Justification</c>.
/// </summary>
/// <remarks>
///     ⚠ Silent where the property does not exist. <c>Justification</c> arrived on the attribute in
///     .NET 5; on <c>netstandard2.0</c>, <c>net472</c> and anything else carrying the older shape there
///     is no way to satisfy this rule, and a rule that cannot be satisfied is a rule every author in
///     that repository turns off — taking the cases it was right about with it. The property is looked
///     up on the bound type rather than assumed from the target framework moniker.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ExcludeFromCodeCoverageWithoutJustificationAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor =
        SkalaRule.Descriptor(RuleIds.ExcludeFromCodeCoverageWithoutJustification);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(static start => {
                var exclude = start.Compilation.GetTypeByMetadataName(
                    "System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverageAttribute"
                );
                if (exclude is null
                    || !exclude.GetMembers("Justification").OfType<IPropertySymbol>().Any()) {
                    return;
                }

                start.RegisterSyntaxNodeAction(context => Analyze(context, exclude), SyntaxKind.Attribute);
            }
        );
    }

    static void Analyze(SyntaxNodeAnalysisContext context, INamedTypeSymbol exclude) {
        var attribute = (AttributeSyntax)context.Node;
        if (context.SemanticModel.GetSymbolInfo(attribute, context.CancellationToken).Symbol
            is not IMethodSymbol constructor
            || !SymbolEqualityComparer.Default.Equals(constructor.ContainingType, exclude)) {
            return;
        }

        var justification = attribute.ArgumentList?.Arguments.FirstOrDefault(static argument =>
            argument.NameEquals?.Name.Identifier.ValueText == "Justification"
        );

        // ⚠ Present and not constant-foldable counts as written. `SK7051` takes the same position:
        // the rule proves the field is blank, never that the prose in it is true.
        if (justification is not null
            && (context.SemanticModel.GetConstantValue(justification.Expression, context.CancellationToken)
                is not { HasValue: true } constant
                || (constant.Value is string text && Justification.Meaningful(text)))) {
            return;
        }

        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                attribute.Name.GetLocation(),
                "a coverage exclusion with no `Justification` makes the coverage number quietly mean less"
            )
        );
    }
}
