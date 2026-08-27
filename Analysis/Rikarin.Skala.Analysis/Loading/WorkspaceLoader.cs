using System.Collections.Immutable;
using System.Globalization;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.MSBuild;
using Rikarin.Skala.Core.Diagnostics;
using Rikarin.Skala.Reporting;

namespace Rikarin.Skala.Analysis.Loading;

/// <summary>
/// The fallback (ADR-007): <c>MSBuildLocator</c> plus <c>MSBuildWorkspace</c>.
/// </summary>
/// <remarks>
/// Present because "I have a solution and no binlog" is a real situation. It is slower, it is
/// sensitive to custom targets, and — the point of the whole type —
/// ⚠ <b>its <c>WorkspaceDiagnostics</c> are surfaced verbatim rather than swallowed</b>. A
/// partially-loaded workspace that silently analyses half a solution produces a clean report about
/// a third of the code, and nothing in that report says so unless this does.
/// </remarks>
public static class WorkspaceLoader {
    public static LoadedProject Load(LoadRequest request, CancellationToken cancellation) {
        var diagnostics = ImmutableArray.CreateBuilder<SkalaDiagnostic>();
        var target = Resolve(request);
        if (target is null) {
            diagnostics.Add(
                new SkalaDiagnostic(
                    "SK9024",
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
                    "SK9024",
                    SkalaSeverity.Error,
                    $"no MSBuild could be located: {locatorError}",
                    target
                )
            );

            return new LoadedProject { Mode = LoadMode.Workspace, Diagnostics = diagnostics.ToImmutable() };
        }

        return LoadCore(request, target, diagnostics, cancellation);
    }

    /// <summary>
    /// ⚠ Isolated behind a method that is not inlined into <see cref="Load"/>.
    /// </summary>
    /// <remarks>
    /// <c>MSBuildLocator</c> resolves MSBuild assemblies through an <c>AssemblyResolve</c> handler
    /// installed by <c>RegisterDefaults</c>. If the JIT has already prepared a method that
    /// references <c>MSBuildWorkspace</c>, the load happens before the handler exists and fails with
    /// a message about a file that is obviously present. Keeping every workspace type behind a
    /// separate frame is the documented workaround and the reason this method exists.
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
                    "SK9024",
                    SkalaSeverity.Error,
                    $"'{target}' could not be opened: {exception.Message}",
                    target
                )
            );

            return new LoadedProject { Mode = LoadMode.Workspace, Diagnostics = diagnostics.ToImmutable() };
        }

        // ⚠ Verbatim. Every one of these is a project that did not load, or loaded without its
        // references; the difference between "clean" and "not analysed" lives here.
        foreach (var diagnostic in workspace.Diagnostics) {
            diagnostics.Add(
                new SkalaDiagnostic(
                    "SK9024",
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

        return new LoadedProject {
            Mode = LoadMode.Workspace,
            Units = units.ToImmutable(),
            Diagnostics = diagnostics.ToImmutable(),
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
