using System.Collections.Immutable;
using System.Reflection;
using System.Runtime.Loader;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Diagnostics;
using Rikarin.Skala.Core.Diagnostics;
using Rikarin.Skala.Rules.Metadata;

namespace Rikarin.Skala.Analysis.Hosting;

/// <summary>Roslyn's own IDE code-style analyzers, shipped beside Skala at the pinned Roslyn version.</summary>
/// <remarks>
///     Naming is deliberately not reimplemented. The three-part <c>dotnet_naming_*</c> language is
///     Roslyn's, including its specificity and word-splitting rules, so Skala loads the supported
///     <c>Microsoft.CodeAnalysis.CSharp.CodeStyle</c> package and selects the analyzer that owns
///     <c>IDE1006</c>. Loading the whole package's rule set would silently turn Skala into
///     <c>dotnet format</c>; only naming belongs here.
/// </remarks>
public static class RoslynCodeStyle {
    public const string NamingDiagnosticId = "IDE1006";

    static readonly Lazy<RoslynCodeStyleResult> Loaded = new(LoadCore, LazyThreadSafetyMode.ExecutionAndPublication);

    public static RoslynCodeStyleResult Load() => Loaded.Value;

    static RoslynCodeStyleResult LoadCore() {
        var directory = Path.Combine(AppContext.BaseDirectory, "RoslynCodeStyle");
        if (!Directory.Exists(directory)) {
            return Failed(directory, "the Roslyn code-style payload is missing from the Skala installation");
        }

        var loader = new CodeStyleAssemblyLoader(directory);
        var analyzers = ImmutableArray.CreateBuilder<DiagnosticAnalyzer>();
        try {
            foreach (var name in new[] {
                         "Microsoft.CodeAnalysis.CodeStyle.dll", "Microsoft.CodeAnalysis.CSharp.CodeStyle.dll"
                     }) {
                var path = Path.Combine(directory, name);
                if (!File.Exists(path)) {
                    return Failed(path, $"'{name}' is missing from the Roslyn code-style payload");
                }

                var reference = new AnalyzerFileReference(path, loader);
                analyzers.AddRange(
                    reference.GetAnalyzers(LanguageNames.CSharp)
                        .Where(static analyzer => analyzer.SupportedDiagnostics.Any(static descriptor => descriptor.Id
                                == NamingDiagnosticId
                            )
                        )
                );
            }
        } catch (Exception exception) when (exception is IOException
                                                or FileLoadException
                                                or BadImageFormatException
                                                or TypeLoadException
                                                or ReflectionTypeLoadException
                                                or InvalidOperationException) {
            return Failed(directory, $"the Roslyn naming analyzer could not be loaded: {exception.Message}");
        }

        var distinct = analyzers.DistinctBy(static analyzer => analyzer.GetType().FullName, StringComparer.Ordinal)
            .ToImmutableArray();
        if (distinct.Length == 0) {
            return Failed(directory, $"the Roslyn code-style payload contains no {NamingDiagnosticId} analyzer");
        }

        CodeFixProvider? namingFixer = null;
        try {
            var fixes = loader.LoadFromPath(Path.Combine(directory, "Microsoft.CodeAnalysis.CodeStyle.Fixes.dll"));
            foreach (var type in fixes.GetTypes()) {
                if (type.IsAbstract
                    || !typeof(CodeFixProvider).IsAssignableFrom(type)
                    || !type.Name.Equals("NamingStyleCodeFixProvider", StringComparison.Ordinal)) {
                    continue;
                }

                if (Activator.CreateInstance(type, nonPublic: true) is CodeFixProvider candidate
                    && candidate.FixableDiagnosticIds.Contains(NamingDiagnosticId, StringComparer.Ordinal)) {
                    namingFixer = candidate;
                    break;
                }
            }
        } catch (Exception exception) when (exception is IOException
                                                or FileLoadException
                                                or BadImageFormatException
                                                or TypeLoadException
                                                or ReflectionTypeLoadException
                                                or MissingMethodException
                                                or TargetInvocationException) {
            return FixerFailed(
                distinct,
                directory,
                $"the Roslyn {NamingDiagnosticId} fixer could not be loaded: {exception.Message}"
            );
        }

        return namingFixer is null
            ? FixerFailed(
                distinct,
                directory,
                $"the Roslyn code-style payload contains no {NamingDiagnosticId} fixer"
            )
            : new RoslynCodeStyleResult(distinct, namingFixer, []);
    }

    static RoslynCodeStyleResult Failed(string path, string message) =>
        new(
            [],
            null,
            [
                new SkalaDiagnostic(
                    RuleIds.AnalyzerFailedToLoad,
                    SkalaSeverity.Warning,
                    message,
                    path
                )
            ]
        );

    static RoslynCodeStyleResult FixerFailed(
        ImmutableArray<DiagnosticAnalyzer> analyzers,
        string path,
        string message
    ) =>
        new(
            analyzers,
            null,
            [new SkalaDiagnostic(RuleIds.AnalyzerFailedToLoad, SkalaSeverity.Warning, message, path)]
        );

    /// <summary>
    ///     Shares the host's Roslyn contracts and resolves the code-style implementation assemblies
    ///     beside one another. A private copy of Microsoft.CodeAnalysis would make its
    ///     DiagnosticAnalyzer a different CLR type from the host's.
    /// </summary>
    sealed class CodeStyleAssemblyLoader(string directory) : IAnalyzerAssemblyLoader {
        readonly AssemblyLoadContext _context = new CodeStyleLoadContext(directory);

        public void AddDependencyLocation(string fullPath) { }

        public Assembly LoadFromPath(string fullPath) {
            var name = AssemblyName.GetAssemblyName(fullPath);
            return FindLoaded(name) ?? _context.LoadFromAssemblyPath(Path.GetFullPath(fullPath));
        }
    }

    sealed class CodeStyleLoadContext(string directory) : AssemblyLoadContext("skala/roslyn-code-style") {
        protected override Assembly? Load(AssemblyName assemblyName) {
            if (FindLoaded(assemblyName) is { } loaded) {
                return loaded;
            }

            var beside = Path.Combine(directory, assemblyName.Name + ".dll");
            return File.Exists(beside) ? LoadFromAssemblyPath(beside) : null;
        }
    }

    static Assembly? FindLoaded(AssemblyName requested) =>
        AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(assembly => AssemblyName.ReferenceMatchesDefinition(assembly.GetName(), requested));
}

public sealed record RoslynCodeStyleResult(
    ImmutableArray<DiagnosticAnalyzer> Analyzers,
    CodeFixProvider? NamingFixer,
    ImmutableArray<SkalaDiagnostic> Diagnostics);
