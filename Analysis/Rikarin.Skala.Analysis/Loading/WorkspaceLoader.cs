using System.Collections.Immutable;
using System.Globalization;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.MSBuild;
using Rikarin.Skala.Core.Diagnostics;
using Rikarin.Skala.Reporting;

namespace Rikarin.Skala.Analysis.Loading;

/// <summary>
///     The fallback (ADR-007): <c>MSBuildLocator</c> plus <c>MSBuildWorkspace</c>.
/// </summary>
/// <remarks>
///     Present because "I have a solution and no binlog" is a real situation. It is slower, it is
///     sensitive to custom targets, and — the point of the whole type —
///     ⚠ <b>its <c>WorkspaceDiagnostics</c> are surfaced verbatim rather than swallowed</b>. A
///     partially-loaded workspace that silently analyses half a solution produces a clean report about
///     a third of the code, and nothing in that report says so unless this does.
/// </remarks>
public static class WorkspaceLoader {
    public static LoadedProject Load(LoadRequest request, CancellationToken cancellation) {
        var diagnostics = ImmutableArray.CreateBuilder<SkalaDiagnostic>();
        var target = Resolve(request);
        if (target is null) {
            diagnostics.Add(
                new SkalaDiagnostic(
                    ConfigDiagnosticIds.NothingToLoad,
                    SkalaSeverity.Warning,
                    "no .slnx, .sln or .csproj was found to load",
                    request.RepositoryRoot
                )
            );

            return new LoadedProject { Mode = LoadMode.Workspace, Diagnostics = diagnostics.ToImmutable() };
        }

        if (!MSBuildRuntime.Ensure(out var locatorError)) {
            diagnostics.Add(
                new SkalaDiagnostic(
                    ConfigDiagnosticIds.NothingToLoad,
                    SkalaSeverity.Error,
                    $"no MSBuild could be located: {locatorError}",
                    target
                )
            );

            return new LoadedProject {
                Mode = LoadMode.Workspace, Diagnostics = diagnostics.ToImmutable(), Failed = true
            };
        }

        try {
            return LoadCore(request, target, diagnostics, cancellation);
        } catch (Exception exception) when (exception is FileNotFoundException
                                                or FileLoadException
                                                or TypeLoadException
                                                or BadImageFormatException) {
            // ⚠ **The tool is broken, and this is the branch that has to say so out loud.** Every
            // exception here is a missing or unloadable assembly, which reaches this frame rather
            // than LoadCore's own `catch` because the JIT resolves LoadCore's MSBuildWorkspace
            // reference while preparing the method — before its first line runs.
            //
            // It shipped: `Microsoft.CodeAnalysis.Workspaces.MSBuild` carried `ExcludeAssets=runtime`
            // (it is Roslyn's assembly, not MSBuild's, so the SDK never supplied a copy), and the
            // FileNotFoundException escaped as far as the CLI's catch-all, which called it an
            // internal error. Under `SkalaMode=check` that is a warning and a green build.
            //
            // `Failed` is what stops the loose fallback from turning it back into exit 0. The
            // reference is fixed; this exists because the next dependency to go missing must produce
            // a load failure that names itself, not a pass.
            diagnostics.Add(
                new SkalaDiagnostic(
                    ConfigDiagnosticIds.NothingToLoad,
                    SkalaSeverity.Error,
                    $"the workspace loader could not be initialised — a required assembly is missing from the Skala installation: {exception.Message}",
                    target
                )
            );

            return new LoadedProject {
                Mode = LoadMode.Workspace, Diagnostics = diagnostics.ToImmutable(), Failed = true
            };
        }
    }

    /// <summary>
    ///     ⚠ Isolated behind a method that is not inlined into <see cref="Load" />.
    /// </summary>
    /// <remarks>
    ///     <c>MSBuildLocator</c> resolves MSBuild assemblies through an <c>AssemblyResolve</c> handler
    ///     installed by <c>RegisterDefaults</c>. If the JIT has already prepared a method that
    ///     references <c>MSBuildWorkspace</c>, the load happens before the handler exists and fails with
    ///     a message about a file that is obviously present. Keeping every workspace type behind a
    ///     separate frame is the documented workaround and the reason this method exists.
    /// </remarks>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    static LoadedProject LoadCore(
        LoadRequest request,
        string target,
        ImmutableArray<SkalaDiagnostic>.Builder diagnostics,
        CancellationToken cancellation
    ) {
        using var workspace = MSBuildWorkspace.Create();
        workspace.LoadMetadataForReferencedProjects = true;

        try {
            if (target.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase)) {
                workspace.OpenProjectAsync(target, cancellationToken: cancellation).GetAwaiter().GetResult();
            } else {
                workspace.OpenSolutionAsync(target, cancellationToken: cancellation).GetAwaiter().GetResult();
            }
        } catch (Exception exception) when (exception is IOException
                                                or InvalidOperationException
                                                or NotSupportedException) {
            diagnostics.Add(
                new SkalaDiagnostic(
                    ConfigDiagnosticIds.NothingToLoad,
                    SkalaSeverity.Error,
                    $"'{target}' could not be opened: {exception.Message}",
                    target
                )
            );

            // ⚠ A named target that will not open is a failure, not an absence: the caller pointed at
            // a solution and it could not be read. Falling through to the syntactic loader here is
            // what produced a clean report over a solution nobody had managed to load.
            return new LoadedProject {
                Mode = LoadMode.Workspace, Diagnostics = diagnostics.ToImmutable(), Failed = true
            };
        }

        // ⚠ Verbatim. Every one of these is a project that did not load, or loaded without its
        // references; the difference between "clean" and "not analysed" lives here.
        foreach (var diagnostic in workspace.Diagnostics) {
            diagnostics.Add(
                new SkalaDiagnostic(
                    ConfigDiagnosticIds.NothingToLoad,
                    diagnostic.Kind == WorkspaceDiagnosticKind.Failure ? SkalaSeverity.Warning : SkalaSeverity.Info,
                    "workspace: " + diagnostic.Message,
                    target
                )
            );
        }

        var units = ImmutableArray.CreateBuilder<CompilationUnit>();
        foreach (var project in workspace.CurrentSolution.Projects) {
            if (project.Language != LanguageNames.CSharp) {
                continue;
            }

            if (project.GetCompilationAsync(cancellation).GetAwaiter().GetResult()
                is not CSharpCompilation compilation) {
                continue;
            }

            var reportable = ImmutableHashSet.CreateBuilder<string>(StringComparer.Ordinal);
            foreach (var document in project.Documents) {
                if (document.FilePath is { Length: > 0 } path && !BinlogLoader.IsGenerated(path)) {
                    reportable.Add(Path.GetFullPath(path));
                }
            }

            var parseOptions = project.ParseOptions as CSharpParseOptions;
            units.Add(
                new CompilationUnit {
                    Name = project.Name,
                    Compilation = compilation,
                    TargetFramework = project.Name.Contains('(', StringComparison.Ordinal)
                        ? project.Name[(project.Name.IndexOf('(', StringComparison.Ordinal) + 1)..].TrimEnd(')')
                        : string.Empty,
                    PreprocessorSymbols = parseOptions is null ? [] : [.. parseOptions.PreprocessorSymbolNames],
                    ReportablePaths = reportable.ToImmutable(),
                    AnalyzerReferences = [
                        .. project.AnalyzerReferences
                            .Select(static reference => reference.FullPath ?? string.Empty)
                            .Where(static path => path.Length > 0)
                    ],
                    ProjectPath = project.FilePath ?? string.Empty
                }
            );
        }

        // ⚠ **Nothing analysable plus a failure diagnostic is a failed load, not an empty one**, and
        // telling those two apart is this type's stated purpose applied one level up. The paragraph
        // above surfaces the failures verbatim; surfacing them is not the same as acting on them, and
        // until this block nothing downstream read a single one.
        //
        // ⚠ It counts *documents*, not projects, and that is the entire subtlety. `MSBuildWorkspace`
        // does not throw when a project will not evaluate — an unresolvable SDK, a custom target that
        // errors, an unrestored reference. It records the failure in `Diagnostics` and hands back a
        // placeholder project with **no documents in it**, so a project count of 1 is not evidence
        // that anything loaded. Measured on a .csproj naming an SDK that does not exist: one
        // `WorkspaceDiagnosticKind.Failure` reading "Msbuild failed when processing the file", one
        // project, zero documents, zero findings, **exit 0** — a gate reporting a clean tree over a
        // project MSBuild had just refused to evaluate.
        //
        // A *partial* load — some projects in, some out — deliberately stays a warning. Refusing
        // there would make the gate unsatisfiable on any repository holding one unbuildable project,
        // which is the mistake BinlogLoader.CoverageSeverity documents at length. The line drawn here
        // is the narrow one: not one line of code came back, and something failed.
        var analysable = units.Sum(static unit => unit.ReportablePaths.Count);
        var failedOutright = analysable == 0
                             && workspace.Diagnostics.Any(
                                 static diagnostic => diagnostic.Kind == WorkspaceDiagnosticKind.Failure
                             );

        if (failedOutright) {
            diagnostics.Add(
                new SkalaDiagnostic(
                    ConfigDiagnosticIds.NothingToLoad,
                    SkalaSeverity.Error,
                    $"'{target}' yielded no analysable source; every project in it failed to load",
                    target
                )
            );
        }

        return new LoadedProject {
            Mode = LoadMode.Workspace,
            Units = units.ToImmutable(),
            Diagnostics = diagnostics.ToImmutable(),
            Failed = failedOutright,
            Summary =
                $"workspace {Path.GetFileName(target)} ({units.Count.ToString(CultureInfo.InvariantCulture)} project(s), {workspace.Diagnostics.Count.ToString(CultureInfo.InvariantCulture)} workspace diagnostic(s))"
        };
    }

    static string? Resolve(LoadRequest request) {
        if (request.ProjectPath is { Length: > 0 } named) {
            return File.Exists(named) ? Path.GetFullPath(named) : null;
        }

        // ⚠ .slnx before .sln: this repository and Vixen are both on the XML solution format, and
        // finding the stale .sln beside it would load a different set of projects.
        foreach (var pattern in new[] { "*.slnx", "*.sln", "*.csproj" }) {
            var matches = Directory.GetFiles(request.RepositoryRoot, pattern)
                .OrderBy(
                    static file => file,
                    StringComparer.Ordinal
                )
                .ToArray();

            if (matches.Length > 0) {
                return matches[0];
            }
        }

        return null;
    }
}
