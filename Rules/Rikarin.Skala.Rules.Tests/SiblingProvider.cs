using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;

namespace Rikarin.Skala.Rules.Tests;

/// <summary>
///     Stands in for the loader publishing a multi-targeted project's other monikers to the analyzers
///     (<see cref="ISiblingCompilations" />), so a rule's per-framework guard can be tested without a
///     restore.
/// </summary>
sealed class SiblingProvider(ImmutableArray<Compilation> siblings) : AnalyzerConfigOptionsProvider,
    ISiblingCompilations {
    public ImmutableArray<Compilation> Siblings { get; } = siblings;

    public override AnalyzerConfigOptions GlobalOptions => Empty.Instance;

    public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => Empty.Instance;

    public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) => Empty.Instance;

    /// <summary>Every Skala analyzer over <paramref name="current" />, with <paramref name="siblings" /> published.</summary>
    public static Task<ImmutableArray<Diagnostic>> Analyze(Compilation current, params Compilation[] siblings) =>
        current
            .WithAnalyzers(
                SkalaAnalyzers.All,
                new CompilationWithAnalyzersOptions(
                    new AnalyzerOptions([], new SiblingProvider([.. siblings])),
                    null,
                    true,
                    false,
                    true
                )
            )
            .GetAnalyzerDiagnosticsAsync(TestContext.Current.CancellationToken);

    sealed class Empty : AnalyzerConfigOptions {
        public static Empty Instance { get; } = new();

        public override bool TryGetValue(string key, [NotNullWhen(true)] out string? value) {
            value = null;
            return false;
        }
    }
}
