using System.Collections.Immutable;
using System.Globalization;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Core.Diagnostics;
using Rikarin.Skala.Reporting;

namespace Rikarin.Skala.Analysis.Loading;

/// <summary>
///     No project at all: parse the files, reference the running framework, and say so.
/// </summary>
/// <remarks>
///     ⚠ This mode exists for one consumer:
///     <b>
///         an agent that has just written a file and wants to know
///         whether it is acceptable, before anything is wired into a project
///     </b> (docs/plan/07 § loose,
///     docs/plan/10). It is fast — no build, no MSBuild, no restore — it is honest, because the SARIF
///     says <c>loadMode: loose</c> and lists the rules that were skipped, and it is the default for the
///     MCP <c>skala_check</c> tool when no project is named.
///     <para>
///         ⚠ Most type resolution fails and that is expected. Rules that declare
///         <c>requiresSemantics</c> do not run here; the alternative — running them and letting them
///         silently answer "no finding" because a symbol did not resolve — would make a clean report mean
///         two different things depending on something invisible.
///     </para>
/// </remarks>
public static class LooseLoader {
    public static LoadedProject Load(LoadRequest request) {
        var files = Collect(request).ToList();
        if (files.Count == 0) {
            return new LoadedProject {
                Mode = LoadMode.Loose,
                Summary = "loose (no .cs files found)",
                Diagnostics = [
                    new SkalaDiagnostic(
                        ConfigDiagnosticIds.NoSourceFiles,
                        SkalaSeverity.Info,
                        "no C# files were found under the requested paths",
                        request.RepositoryRoot
                    )
                ]
            };
        }

        var parseOptions = new CSharpParseOptions(LanguageVersion.Preview)
            .WithDocumentationMode(DocumentationMode.Parse)
            .WithPreprocessorSymbols(request.Define);

        var trees = ImmutableArray.CreateBuilder<SyntaxTree>(files.Count);
        var reportable = ImmutableHashSet.CreateBuilder<string>(StringComparer.Ordinal);
        foreach (var file in files) {
            try {
                using var stream = File.OpenRead(file);
                trees.Add(
                    CSharpSyntaxTree.ParseText(SourceText.From(stream, canBeEmbedded: false), parseOptions, file)
                );

                // ⚠ Analysed, never reported on. Same rule as the binlog path: a diagnostic in a
                // file the user cannot edit is noise.
                if (!BinlogLoader.IsGenerated(file)) {
                    reportable.Add(file);
                }
            } catch (IOException) {
                // A file that vanished between the enumeration and the read is not an error worth
                // failing an agent's verify over.
            }
        }

        var compilation = CSharpCompilation.Create(
            "loose",
            trees.ToImmutable(),
            SharedFrameworkReferences.Value,
            new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                allowUnsafe: true,
                nullableContextOptions: NullableContextOptions.Enable,
                concurrentBuild: true
            )
        );

        return new LoadedProject {
            Mode = LoadMode.Loose,
            Units = [
                new CompilationUnit {
                    Name = "loose",
                    Compilation = compilation,
                    PreprocessorSymbols = [.. request.Define],
                    ReportablePaths = reportable.ToImmutable()
                }
            ],
            Summary = $"loose ({files.Count.ToString(CultureInfo.InvariantCulture)} file(s), no project)"
        };
    }

    static IEnumerable<string> Collect(LoadRequest request) {
        var roots = request.Paths.Count > 0 ? request.Paths : [request.RepositoryRoot];
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var path in roots) {
            var full = Path.GetFullPath(path);
            if (File.Exists(full)) {
                if (full.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) && seen.Add(full)) {
                    yield return full;
                }

                continue;
            }

            if (!Directory.Exists(full)) {
                continue;
            }

            foreach (var file in BinlogLoader.EnumerateSources(full)
                         .OrderBy(
                             static file => file,
                             StringComparer.Ordinal
                         )) {
                if (seen.Add(file)) {
                    yield return file;
                }
            }
        }
    }
}

/// <summary>
///     The running framework's assemblies, as <see cref="MetadataReference" />s.
/// </summary>
/// <remarks>
///     ⚠ The <em>running</em> framework, not a reference pack: a global tool has the shared framework
///     it is executing on and nothing else guaranteed on disk. It is close enough for the syntactic
///     rule set and for the BCL-shaped questions the semantic ones ask, and it costs no restore, which
///     is what keeps the agent path under a second.
/// </remarks>
public static class SharedFrameworkReferences {
    public static ImmutableArray<MetadataReference> Value { get; } = Build();

    static ImmutableArray<MetadataReference> Build() {
        var builder = ImmutableArray.CreateBuilder<MetadataReference>();
        if (AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") is string assemblies) {
            foreach (var path in assemblies.Split(Path.PathSeparator)) {
                if (path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)
                    && MetadataReferenceCache.Get(path) is { } reference) {
                    builder.Add(reference);
                }
            }
        }

        if (builder.Count == 0) {
            // A single-file or trimmed host has no TPA list. One reference is not a framework, but
            // it is enough for `object` to resolve, which is enough for the syntactic rules.
            var location = typeof(object).Assembly.Location;
            if (location.Length > 0 && MetadataReferenceCache.Get(location) is { } core) {
                builder.Add(core);
            }
        }

        return builder.ToImmutable();
    }
}
