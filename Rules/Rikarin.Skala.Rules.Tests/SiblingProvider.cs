using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;

namespace Rikarin.Skala.Rules.Tests;

/// <summary>
///     Stands in for the loader publishing a multi-targeted project's other monikers
///     (<see cref="ISiblingCompilations" />) and the binlog's compiler path (<see cref="ICompilerIdentity" />)
///     to the analyzers, so a rule's guard can be tested without a restore or a build.
/// </summary>
sealed class SiblingProvider(ImmutableArray<Compilation> siblings, string compilerPath = "")
    : AnalyzerConfigOptionsProvider, ISiblingCompilations, ICompilerIdentity {
    public ImmutableArray<Compilation> Siblings { get; } = siblings;

    public string CompilerPath { get; } = compilerPath;

    public override AnalyzerConfigOptions GlobalOptions => Empty.Instance;

    public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => Empty.Instance;

    public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) => Empty.Instance;

    /// <summary>Every Skala analyzer over <paramref name="current" />, with <paramref name="siblings" /> published.</summary>
    public static Task<ImmutableArray<Diagnostic>> Analyze(Compilation current, params Compilation[] siblings) =>
        Run(current, new SiblingProvider([.. siblings]));

    /// <summary>Every Skala analyzer over <paramref name="current" />, built by <paramref name="compilerPath" />.</summary>
    public static Task<ImmutableArray<Diagnostic>> AnalyzeBuiltBy(Compilation current, string compilerPath) =>
        Run(current, new SiblingProvider([], compilerPath));

    static Task<ImmutableArray<Diagnostic>> Run(Compilation current, SiblingProvider provider) =>
        current
            .WithAnalyzers(
                SkalaAnalyzers.All,
                new CompilationWithAnalyzersOptions(
                    new AnalyzerOptions([], provider),
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
