using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Rikarin.Skala.Rules.Metadata;
using System;
using System.Collections.Immutable;
using System.Linq;

namespace Rikarin.Skala.Rules.Async;

/// <summary>SK3003: explicit library policy requires configuration on framework task awaits.</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ConfigureAwaitAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.MissingConfigureAwait);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.AwaitExpression);
    }

    static void Analyze(SyntaxNodeAnalysisContext context) {
        var options = context.Options.AnalyzerConfigOptionsProvider.GetOptions(context.Node.SyntaxTree);
        if (!options.TryGetValue("skala_configure_await_analysis_mode", out var mode)) {
            options.TryGetValue("skala_configure_await_analysis_mode", out mode);
        }

        // UI mode's redundant ConfigureAwait(true) inspection is a different concept from SK3003.
        if (!string.Equals(mode?.Trim(), "library", StringComparison.OrdinalIgnoreCase)) {
            return;
        }

        var awaitExpression = (AwaitExpressionSyntax)context.Node;
        var type = context.SemanticModel.GetTypeInfo(awaitExpression.Expression, context.CancellationToken).Type;
        if (type is not INamedTypeSymbol named || named.Locations.Any(static location => location.IsInSource)) {
            return;
        }

        var definition = named.OriginalDefinition;
        var compilation = context.Compilation;
        if (!SymbolEqualityComparer.Default.Equals(
                definition,
                compilation.GetTypeByMetadataName("System.Threading.Tasks.Task")
            )
            && !SymbolEqualityComparer.Default.Equals(
                definition,
                compilation.GetTypeByMetadataName("System.Threading.Tasks.Task`1")
            )
            && !SymbolEqualityComparer.Default.Equals(
                definition,
                compilation.GetTypeByMetadataName("System.Threading.Tasks.ValueTask")
            )
            && !SymbolEqualityComparer.Default.Equals(
                definition,
                compilation.GetTypeByMetadataName("System.Threading.Tasks.ValueTask`1")
            )) {
            return;
        }

        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                awaitExpression.AwaitKeyword.GetLocation(),
                "Library policy requires an explicit ConfigureAwait choice; use ConfigureAwait(false) unless context capture is intentional"
            )
        );
    }
}
