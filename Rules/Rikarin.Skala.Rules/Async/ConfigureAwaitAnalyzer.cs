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

        // ⚠ One lookup, because the option has one spelling. This was two — a hand-rolled precedence
        // ladder trying the prefixed key and then the bare one, a second implementation of
        // `OptionResolver`'s rule that nothing kept in step with it. The `skala_` rename collapsed
        // both branches onto the same string, which is what made it obvious.
        //
        // ⚠ `Rikarin.Skala.Rules` deliberately does not reference `Rikarin.Skala.Options` — an
        // analyzer ships on its own — so this cannot ask the registry and the spelling is a literal.
        // `OptionRegistryTests.TheAnalyzerReadOptions_HaveExactlyOneSpelling` is what fails if an
        // alias is ever added, because this file could not notice.
        options.TryGetValue("skala_configure_await_analysis_mode", out var mode);

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
