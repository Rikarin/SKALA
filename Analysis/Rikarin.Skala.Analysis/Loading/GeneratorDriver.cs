using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Rikarin.Skala.Core.Diagnostics;
using Rikarin.Skala.Rules.Metadata;
using System.Collections.Immutable;
using System.Reflection;
using System.Runtime.Loader;

namespace Rikarin.Skala.Analysis.Loading;

/// <summary>
///     Re-runs the build's own source generators, because the compilation is not the program without
///     them.
/// </summary>
/// <remarks>
///     ⚠ docs/plan/07 § binlog expected the generated files to be on the command line when
///     <c>EmitCompilerGeneratedFiles</c> is set. Measured on Vixen, they are not: that property makes
///     <c>csc</c> <em>write</em> them beside the build output, and the compiler still produces them
///     in-process — nothing puts them on a <c>/reference:</c> or a source line. Loading the command line
///     verbatim therefore gives a compilation missing every generated member, which on Vixen is
///     <b>1 675 compiler errors</b>: 894 <c>CS0103</c>, 227 <c>CS8795</c> (a partial method with no
///     implementation), 137 <c>CS9248</c> (a partial property with no implementation). None of them is
///     about the user's code.
///     <para>
///         ⚠ That is not only noise. Every semantic rule reads a semantic model built over a program that
///         does not compile, so it answers questions about error types — which makes it silent where it
///         should fire and, worse, makes "silent" mean two different things. Running the generators is what
///         makes the semantic half trustworthy at all.
///     </para>
///     <para>
///         ⚠ Generated trees are added to the compilation and never to <c>ReportablePaths</c>. Same rule as
///         everywhere else: analysed, never reported on.
///     </para>
/// </remarks>
public static class GeneratorDriver {
    static readonly AnalyzerAssemblyLoader Loader = new();

    /// <summary>
    ///     Every declared analyzer or generator assembly that is not on disk, reported once each.
    ///     <c>true</c> when there was at least one.
    /// </summary>
    /// <remarks>
    ///     ⚠
    ///     <b>
    ///         The two callers pass different severities, and the asymmetry is the point rather than
    ///         an inconsistency.
    ///     </b>
    ///     A binlog's analyzer paths come off a command line <c>csc</c> actually
    ///     ran, so the assembly was there and did its work; a file missing now means the tree was
    ///     cleaned afterwards, which is the same class of fact as <c>SK9020</c> and <c>SK9021</c> and
    ///     carries their settled stance — say so, report the coverage, do not refuse. A workspace's
    ///     paths are a *prediction* of where <c>MSBuildWorkspace</c>'s configuration would put output
    ///     if it were built. A file missing there means the build being described never happened, so
    ///     nothing assembled from those references is evidence of anything, and
    ///     <see cref="WorkspaceLoader" /> refuses.
    ///     <para>
    ///         ⚠ Absence is the signal, not failure to load. <c>SK9031</c> already covers an assembly
    ///         that is present and throws and is deliberately never fatal; that one at least proves a
    ///         real reference to a real file.
    ///     </para>
    /// </remarks>
    public static bool ReportMissingAssemblies(
        IEnumerable<string> analyzerReferences,
        string origin,
        ImmutableArray<SkalaDiagnostic>.Builder diagnostics,
        SkalaSeverity severity
    ) {
        var missing = analyzerReferences
            .Where(static path => path.Length > 0 && !File.Exists(path))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();

        if (missing.Length == 0) {
            return false;
        }

        foreach (var path in missing) {
            diagnostics.Add(
                new(
                    ConfigDiagnosticIds.AnalyzerAssemblyMissing,
                    severity,
                    $"the load names '{Path.GetFileName(path)}' as an analyzer or source generator and "
                    + $"there is no file at '{path}', so whatever it contributes to the program is absent "
                    + "from the compilation",
                    origin
                )
            );
        }

        return true;
    }

    public static Outcome Run(
        CSharpCompilation compilation,
        ImmutableArray<string> analyzerReferences,
        ImmutableArray<string> additionalFiles,
        AnalyzerConfigOptionsProvider? configOptions,
        CSharpParseOptions parseOptions,
        ImmutableArray<SkalaDiagnostic>.Builder diagnostics,
        CancellationToken cancellation
    ) {
        if (analyzerReferences.IsEmpty) {
            return new(compilation, true);
        }

        // ⚠ #336. This used to be a bare `continue`: a generator assembly that is not on disk cost
        // its entire output and said nothing at all, which is the one silence in this type that its
        // own remarks do not cover. Warning rather than fatal here, and the distinction is real
        // rather than a hedge — see ReportMissingAssemblies.
        var complete = !ReportMissingAssemblies(
            analyzerReferences,
            compilation.AssemblyName ?? string.Empty,
            diagnostics,
            SkalaSeverity.Warning
        );

        var generators = ImmutableArray.CreateBuilder<ISourceGenerator>();
        foreach (var path in analyzerReferences) {
            if (!File.Exists(path)) {
                continue;
            }

            try {
                var reference = new AnalyzerFileReference(path, Loader);

                // ⚠ #634. `AnalyzerFileReference` does not throw when a generator type will not load
                // or construct — it raises this event and returns the others, so the `catch` below
                // never saw any of it and a generator that failed here was a generator that was
                // never mentioned. Unsubscribed, the event is the swallow.
                reference.AnalyzerLoadFailed += (_, failure) => {
                    if (failure.ErrorCode == AnalyzerLoadFailureEventArgs.FailureErrorCode.NoAnalyzers) {
                        return;
                    }

                    complete = false;
                    diagnostics.Add(
                        new(
                            RuleIds.AnalyzerFailedToLoad,
                            SkalaSeverity.Warning,
                            $"'{failure.TypeName ?? Path.GetFileName(path)}' in '{Path.GetFileName(path)}' could "
                            + $"not be loaded as a generator ({failure.ErrorCode}): {failure.Message}; "
                            + "the compilation is missing its output",
                            path
                        )
                    );
                };

                // ⚠ #634, the root cause. `GetGeneratorsForAllLanguages()` returns ONE generator per
                // assembly when the assembly holds several `IIncrementalGenerator`s: measured on
                // Vixen.Net.Generators (ReplicationGenerator kept, RpcGenerator dropped) and on three
                // of the SDK's own — ComInterfaceGenerator, JSImportGenerator and the
                // LibraryImportGenerator assembly each lost one. Its sibling `GetGenerators("C#")`
                // returns every one. Nothing reported the loss: the generator that went missing was
                // simply never run, and `arrange` then deleted a `using` whose only types it emits.
                // C# is the only language this driver serves, so asking for C# is not a narrowing.
                generators.AddRange(reference.GetGenerators(LanguageNames.CSharp));
            } catch (Exception exception) when (exception is IOException
                                                    or BadImageFormatException
                                                    or FileLoadException
                                                    or ReflectionTypeLoadException
                                                    or TypeLoadException
                                                    or InvalidOperationException
                                                    or ArgumentException) {
                // ⚠ SK9031, never fatal. A generator that will not load costs its own output; it
                // must not cost the report.
                complete = false;
                diagnostics.Add(
                    new(
                        RuleIds.AnalyzerFailedToLoad,
                        SkalaSeverity.Warning,
                        $"'{Path.GetFileName(path)}' could not be loaded as a generator: {exception.Message}",
                        path
                    )
                );
            }
        }

        if (generators.Count == 0) {
            return new(compilation, complete);
        }

        try {
            // ⚠ The additional files and the build's own analyzer config, both. A generator that
            // reads `.vsl` shaders or a `.g4` grammar produces nothing without the first, and one
            // that reads `build_property.RootNamespace` produces nothing without the second — and
            // "produces nothing" reaches the report as a few hundred CS0103s about names the user's
            // code is entirely right to use.
            var driver = CSharpGeneratorDriver.Create(
                generators.ToImmutable(),
                [
                    ..additionalFiles
                        .Where(File.Exists)
                        .Select(static path => (AdditionalText)new FileText(path))
                ],
                parseOptions,
                configOptions
            );

            var ran = driver.RunGeneratorsAndUpdateCompilation(
                compilation,
                out var updated,
                out var produced,
                cancellation
            );

            // ⚠ #634. Roslyn's driver catches a generator's own exception and turns it into a
            // `CS8784`/`CS8785` *warning*, which the error-only filter below never forwarded — so a
            // generator that threw here was as silent as one that was never loaded. The exception on
            // the run result is the fact; the warning is only its rendering.
            foreach (var result in ran.GetRunResult().Results) {
                if (result.Exception is { } thrown) {
                    complete = false;
                    diagnostics.Add(
                        new(
                            RuleIds.AnalyzerThrew,
                            SkalaSeverity.Warning,
                            $"source generator '{result.Generator.GetGeneratorType().FullName}' threw and the "
                            + $"compilation is missing its output: {thrown.Message}",
                            compilation.AssemblyName ?? string.Empty
                        )
                    );
                }
            }

            foreach (var diagnostic in produced) {
                if (diagnostic.Severity == DiagnosticSeverity.Error) {
                    diagnostics.Add(
                        new(
                            RuleIds.AnalyzerThrew,
                            SkalaSeverity.Info,
                            $"a source generator reported {diagnostic.Id}: {diagnostic.GetMessage(System.Globalization.CultureInfo.InvariantCulture)}",
                            diagnostic.Location.SourceTree?.FilePath ?? compilation.AssemblyName ?? string.Empty
                        )
                    );
                }
            }

            return new((CSharpCompilation)updated, complete);
        } catch (Exception exception) when (exception is InvalidOperationException
                                                or TypeLoadException
                                                or MissingMethodException
                                                or TargetInvocationException) {
            diagnostics.Add(
                new(
                    RuleIds.AnalyzerThrew,
                    SkalaSeverity.Warning,
                    $"a source generator threw and the compilation is missing its output: {exception.Message}",
                    compilation.AssemblyName ?? string.Empty
                )
            );

            return new(compilation, false);
        }
    }

    /// <summary>
    ///     The compilation with the generated half added, and whether every generator the build named
    ///     actually contributed it.
    /// </summary>
    /// <remarks>
    ///     ⚠ #634. <see cref="Complete" /> is <c>false</c> when an assembly was missing, a generator would
    ///     not load or construct, or one threw — every case in which the compilation lacks members the
    ///     build had. A rule may still report against such a compilation, because each case also carries
    ///     its diagnostic; what must not happen is a <em>rewrite</em> justified by a member being absent,
    ///     which is what deleted <c>using Vixen.Net.Generated;</c> and broke the next build.
    /// </remarks>
    public readonly record struct Outcome(CSharpCompilation Compilation, bool Complete);

    /// <summary>An <see cref="AdditionalText" /> over a file on disk.</summary>
    sealed class FileText(string path) : AdditionalText {
        public override string Path { get; } = path;

        public override Microsoft.CodeAnalysis.Text.SourceText? GetText(CancellationToken cancellationToken = default) {
            try {
                using var stream = File.OpenRead(Path);
                return Microsoft.CodeAnalysis.Text.SourceText.From(stream, canBeEmbedded: false);
            } catch (IOException) {
                return null;
            }
        }
    }

    /// <summary>
    ///     ⚠ One <see cref="AssemblyLoadContext" /> per analyzer assembly directory, for the reason
    ///     <c>HostedAnalyzers</c> gives: half of these bundle their own helper libraries and two
    ///     versions of one helper in a single context is a <c>TypeLoadException</c> that names neither.
    ///     Roslyn's own types are deliberately shared, or the generator a package declares is not the
    ///     <c>ISourceGenerator</c> the host knows.
    /// </summary>
    sealed class AnalyzerAssemblyLoader : IAnalyzerAssemblyLoader {
        readonly Dictionary<string, AssemblyLoadContext> contexts = new(StringComparer.Ordinal);
        readonly Lock gate = new();

        public void AddDependencyLocation(string fullPath) { }

        public Assembly LoadFromPath(string fullPath) {
            var directory = Path.GetDirectoryName(Path.GetFullPath(fullPath)) ?? string.Empty;
            AssemblyLoadContext context;
            lock (gate) {
                if (!contexts.TryGetValue(directory, out context!)) {
                    contexts[directory] = context = new DirectoryContext(directory);
                }
            }

            return context.LoadFromAssemblyPath(Path.GetFullPath(fullPath));
        }

        sealed class DirectoryContext(string directory) : AssemblyLoadContext("skala/generators/" + directory) {
            protected override Assembly? Load(AssemblyName assemblyName) {
                if (assemblyName.Name is { } simple
                    && (simple.StartsWith("Microsoft.CodeAnalysis", StringComparison.Ordinal)
                        || simple.StartsWith("System.", StringComparison.Ordinal)
                        || simple is "netstandard" or "mscorlib")) {
                    return null;
                }

                var beside = Path.Combine(directory, assemblyName.Name + ".dll");
                return File.Exists(beside) ? LoadFromAssemblyPath(beside) : null;
            }
        }
    }
}
