using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Rikarin.Skala.Analysis.Loading;
using System.Collections.Immutable;

namespace Rikarin.Skala.Analysis.Hosting;

/// <summary>
///     The <c>.editorconfig</c> chain, as Roslyn's analyzer driver wants to see it.
/// </summary>
/// <remarks>
///     ⚠ ADR-001: <c>.editorconfig</c> is the only style configuration language and the compiler's own
///     globbing is the one Skala speaks. That is not a slogan here — it is
///     <see cref="AnalyzerConfigSet" />, the same public API <c>csc</c> uses, so
///     <c>dotnet_diagnostic.SK1010.severity = none</c> in a scoped section means exactly what it means
///     in the IDE and in the build, including the parts of section matching (<c>{a,b}</c> groups,
///     <c>**</c>) that a hand-rolled matcher gets subtly wrong.
/// </remarks>
public static class EditorConfigOptions {
    /// <summary>
    ///     The driver's options, and the fingerprint of the text they were built from.
    /// </summary>
    /// <remarks>
    ///     ⚠ The fingerprint is over the raw config text, not over the resolved global options. The
    ///     resolved view is per source path — that is the whole point of scoped sections — so hashing
    ///     the global view would let an edit to a <c>[Testing/**]</c> section leave every cache key
    ///     unmoved, which is a stale finding by construction.
    ///     <para>
    ///         ⚠ The fingerprint used to carry a <c>|resharper</c> suffix when the
    ///         <c>resharper_*_highlighting</c> severity bridge was switched on. The bridge is gone and
    ///         so is the suffix — and that changes no existing cache key, because the suffix was only
    ///         ever appended for a run that opted in. Every run that did not is fingerprinted exactly
    ///         as before.
    ///     </para>
    /// </remarks>
    public static (AnalyzerOptions Options, string Fingerprint, SyntaxTreeOptionsProvider? Severities)
        For(CompilationUnit unit, string repositoryRoot) {
        var paths = unit.AnalyzerConfigPaths.IsEmpty
            ? Discover(unit, repositoryRoot)
            : unit.AnalyzerConfigPaths;

        var configs = ImmutableArray.CreateBuilder<AnalyzerConfig>();
        var fingerprint = new System.Text.StringBuilder();
        foreach (var path in paths.OrderBy(static path => path, StringComparer.Ordinal)) {
            try {
                if (!File.Exists(path)) {
                    continue;
                }

                var text = File.ReadAllText(path);
                configs.Add(AnalyzerConfig.Parse(text, Path.GetFullPath(path)));
                fingerprint.Append(path).Append('@').Append(text.Length).Append(':');
                fingerprint.Append(
                    Convert.ToHexStringLower(System.IO.Hashing.XxHash128.Hash(System.Text.Encoding.UTF8.GetBytes(text)))
                )
                    .Append(';');
            } catch (IOException) {
                // A config that cannot be read is a config that does not apply; the SK9001 family
                // reports configuration problems and this is not the place to fail a run.
            }
        }

        if (configs.Count == 0) {
            return (new AnalyzerOptions([]), fingerprint.ToString(), null);
        }

        var set = AnalyzerConfigSet.Create(configs.ToImmutable());
        return (
            new AnalyzerOptions([], new SetProvider(set)),
            fingerprint.ToString(),
            new SeverityProvider(set)
        );
    }

    /// <summary>
    ///     <c>dotnet_diagnostic.SK1010.severity = none</c>, honoured.
    /// </summary>
    /// <remarks>
    ///     ⚠ docs/plan/07 § "Suppression", mechanism 3: "the right way to turn a rule off for a folder,
    ///     and the reason <c>[Testing/**]</c> sections exist". Roslyn's analyzer driver reads severities
    ///     through <see cref="SyntaxTreeOptionsProvider" /> on the <em>compilation options</em>, not
    ///     through <see cref="AnalyzerOptions" /> — <c>csc</c> sets one and a hand-built
    ///     <c>CSharpCompilation</c> does not, so a scoped severity would be silently ignored without
    ///     this. That is the failure where a repository turns a rule off, the IDE agrees, and CI keeps
    ///     reporting it.
    /// </remarks>
    sealed class SeverityProvider(AnalyzerConfigSet set) : SyntaxTreeOptionsProvider {
        public override GeneratedKind IsGenerated(SyntaxTree tree, CancellationToken cancellationToken) =>
            GeneratedKind.Unknown;

        public override bool TryGetDiagnosticValue(
            SyntaxTree tree,
            string diagnosticId,
            CancellationToken cancellationToken,
            out ReportDiagnostic severity
        ) {
            // ⚠ `dotnet_diagnostic.SK1010.severity` is the only spelling, because it names the Skala
            // rule and therefore cannot mean anything else. There used to be a second, opt-in axis
            // here reading `resharper_*_highlighting`; it is gone, and doc 16 § Q5 records why.
            return set.GetOptionsForSourcePath(tree.FilePath).TreeOptions.TryGetValue(diagnosticId, out severity);
        }

        public override bool TryGetGlobalDiagnosticValue(
            string diagnosticId,
            CancellationToken cancellationToken,
            out ReportDiagnostic severity
        ) =>
            set.GlobalConfigOptions.TreeOptions.TryGetValue(diagnosticId, out severity);
    }

    /// <summary>
    ///     An options provider over a set of analyzer-config paths, for the generator driver.
    /// </summary>
    /// <remarks>
    ///     ⚠ The build's <c>/analyzerconfig:</c> list includes the SDK's generated global config, which
    ///     is where <c>build_property.RootNamespace</c> and every other MSBuild property a generator
    ///     reads actually lives. A generator handed no options produces nothing and says nothing.
    /// </remarks>
    public static AnalyzerConfigOptionsProvider? ProviderFor(ImmutableArray<string> configPaths) {
        var configs = ImmutableArray.CreateBuilder<AnalyzerConfig>();
        foreach (var path in configPaths) {
            try {
                if (File.Exists(path)) {
                    configs.Add(AnalyzerConfig.Parse(File.ReadAllText(path), Path.GetFullPath(path)));
                }
            } catch (IOException) {
                // The same best-effort read as `For` above, and for the same reason: a config that
                // cannot be read is a config that does not apply. Handing the generator driver the
                // rest of the chain beats failing the run, and the SK9001 family is where a
                // configuration problem is reported.
            }
        }

        return configs.Count == 0 ? null : new SetProvider(AnalyzerConfigSet.Create(configs.ToImmutable()));
    }

    /// <summary>
    ///     Every <c>.editorconfig</c> from the repository root down to each source directory.
    /// </summary>
    /// <remarks>
    ///     Used when the load mode has no command line to read them off — <c>loose</c>, and a workspace
    ///     whose project did not surface them. ⚠ Outermost first, because <see cref="AnalyzerConfigSet" />
    ///     resolves specificity itself and only needs the set.
    /// </remarks>
    static ImmutableArray<string> Discover(CompilationUnit unit, string repositoryRoot) {
        var directories = new HashSet<string>(StringComparer.Ordinal);
        foreach (var tree in unit.Compilation.SyntaxTrees) {
            if (Path.GetDirectoryName(Path.GetFullPath(tree.FilePath)) is { } directory) {
                directories.Add(directory);
            }
        }

        var found = new SortedSet<string>(StringComparer.Ordinal);
        var root = Path.GetFullPath(repositoryRoot);
        foreach (var start in directories) {
            for (var directory = start; directory is not null; directory = Path.GetDirectoryName(directory)) {
                var candidate = Path.Combine(directory, ".editorconfig");
                if (File.Exists(candidate)) {
                    found.Add(candidate);
                }

                if (string.Equals(directory, root, StringComparison.Ordinal)) {
                    break;
                }
            }
        }

        return [.. found];
    }

    sealed class SetProvider(AnalyzerConfigSet set) : AnalyzerConfigOptionsProvider {
        public override AnalyzerConfigOptions GlobalOptions { get; } =
            new Options(set.GlobalConfigOptions.AnalyzerOptions);

        public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) =>
            new Options(set.GetOptionsForSourcePath(tree.FilePath).AnalyzerOptions);

        public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) =>
            new Options(set.GetOptionsForSourcePath(textFile.Path).AnalyzerOptions);
    }

    sealed class Options(ImmutableDictionary<string, string> values) : AnalyzerConfigOptions {
        public override bool TryGetValue(string key, out string value) => values.TryGetValue(key, out value!);

        public override IEnumerable<string> Keys => values.Keys;
    }
}
