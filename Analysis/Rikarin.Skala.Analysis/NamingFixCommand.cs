using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.MSBuild;
using Rikarin.Skala.Analysis.Hosting;
using Rikarin.Skala.Analysis.Loading;
using Rikarin.Skala.Reporting;
using System.Collections.Immutable;
using System.Text;

namespace Rikarin.Skala.Analysis;

/// <summary>The result of an explicit, solution-wide <c>IDE1006</c> rename pass.</summary>
public sealed record NamingFixOutcome(
    int Applied,
    ImmutableArray<string> ChangedPaths,
    string? Error = null,
    ImmutableArray<string> Skipped = default);

/// <summary>
///     Applies Roslyn's own naming code action over an MSBuild workspace, one symbol at a time.
/// </summary>
/// <remarks>
///     A naming fix is a rename, not a text replacement: references in other documents and projects
///     move with the declaration. It is consequently workspace-only and is reached only through the
///     explicit unsafe form <c>skala fix --include IDE1006</c>, which infers workspace mode. It never
///     participates in <c>--safe</c>, formatting, or arrangement.
/// </remarks>
public static class NamingFixCommand {
    const int MaximumRenames = 10_000;

    public static NamingFixOutcome Run(
        FixRequest request,
        string repositoryRoot,
        IReadOnlyCollection<string> reportablePaths,
        CancellationToken cancellation = default
    ) {
        if (request.Mode != LoadMode.Workspace) {
            return new NamingFixOutcome(
                0,
                [],
                "IDE1006 fixes require a workspace so Roslyn can rename references across the solution"
            );
        }

        var codeStyle = RoslynCodeStyle.Load();
        if (codeStyle.NamingFixer is null || codeStyle.Analyzers.IsEmpty) {
            var detail = codeStyle.Diagnostics.FirstOrDefault()?.Message;
            return new NamingFixOutcome(
                0,
                [],
                detail ?? "the Roslyn IDE1006 analyzer and fixer are unavailable"
            );
        }

        var resolution = WorkspaceLoader.Resolve(
            new LoadRequest {
                RepositoryRoot = repositoryRoot,
                Mode = LoadMode.Workspace,
                ProjectPath = request.ProjectPath,
                Paths = request.Paths
            }
        );
        if (resolution.Error is { } resolutionError) {
            return new NamingFixOutcome(0, [], resolutionError);
        }

        var target = resolution.Target;
        if (target is null) {
            return new NamingFixOutcome(0, [], "no .slnx, .sln or .csproj was found for the IDE1006 fix");
        }

        if (!MSBuildRuntime.Ensure(out var locatorError)) {
            return new NamingFixOutcome(0, [], $"no MSBuild could be located: {locatorError}");
        }

        try {
            return RunCore(
                request,
                target,
                reportablePaths,
                codeStyle.Analyzers,
                codeStyle.NamingFixer,
                cancellation
            );
        } catch (Exception exception) when (exception is IOException
                                                or InvalidOperationException
                                                or NotSupportedException
                                                or FileLoadException
                                                or TypeLoadException
                                                or BadImageFormatException) {
            return new NamingFixOutcome(0, [], $"the IDE1006 workspace fix could not run: {exception.Message}");
        }
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    static NamingFixOutcome RunCore(
        FixRequest request,
        string target,
        IReadOnlyCollection<string> reportablePaths,
        ImmutableArray<DiagnosticAnalyzer> analyzers,
        CodeFixProvider fixer,
        CancellationToken cancellation
    ) {
        using var workspace = MSBuildWorkspace.Create();
        workspace.LoadMetadataForReferencedProjects = true;

        Solution solution;
        if (target.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase)) {
            solution = workspace.OpenProjectAsync(target, cancellationToken: cancellation)
                .GetAwaiter()
                .GetResult()
                .Solution;
        } else {
            solution = workspace.OpenSolutionAsync(target, cancellationToken: cancellation)
                .GetAwaiter()
                .GetResult();
        }

        if (workspace.Diagnostics.Any(static diagnostic => diagnostic.Kind == WorkspaceDiagnosticKind.Failure)) {
            return new NamingFixOutcome(
                0,
                [],
                "the IDE1006 fix refused a workspace with load failures: "
                + workspace.Diagnostics.First(static diagnostic => diagnostic.Kind == WorkspaceDiagnosticKind.Failure)
                    .Message
            );
        }

        var original = solution;
        var beforeErrors = CompilerErrors(original, cancellation);
        var comparison = SarifWriter.PathComparison == StringComparison.OrdinalIgnoreCase
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;
        var allowed = reportablePaths.Select(Path.GetFullPath).ToHashSet(comparison);
        var applied = 0;
        var attempted = 0;
        var rejected = new HashSet<NamingTarget>();
        var skipped = ImmutableArray.CreateBuilder<string>();

        while (attempted < MaximumRenames) {
            var next = FindFirst(solution, analyzers, allowed, rejected, cancellation);
            if (next is null) {
                break;
            }

            attempted++;

            var document = solution.GetDocument(next.Location.SourceTree!);
            if (document is null) {
                return new NamingFixOutcome(applied, [], "Roslyn reported IDE1006 outside the loaded solution");
            }

            var actions = new List<CodeAction>();
            var context = new CodeFixContext(
                document,
                next,
                (action, _) => actions.Add(action),
                cancellation
            );
            fixer.RegisterCodeFixesAsync(context).GetAwaiter().GetResult();
            if (actions.Count == 0) {
                var span = next.Location.GetLineSpan().StartLinePosition;
                return new NamingFixOutcome(
                    applied,
                    [],
                    $"Roslyn offered no IDE1006 rename for {document.FilePath}:{span.Line + 1}:{span.Character + 1}"
                );
            }

            var changed = actions[0]
                .GetOperationsAsync(cancellation)
                .GetAwaiter()
                .GetResult()
                .OfType<ApplyChangesOperation>()
                .Select(static operation => operation.ChangedSolution)
                .FirstOrDefault();
            if (changed is null || ChangedPaths(solution, changed, cancellation).IsEmpty) {
                return new NamingFixOutcome(applied, [], "Roslyn's IDE1006 rename produced no solution change");
            }

            // Roslyn's naming fixer can offer a spelling that is valid by the naming rule but not by
            // binding. `_ranges` -> `ranges` beside `out var ranges`, for example, produces CS0844.
            // Validate this candidate before adding it to the accumulated solution, reject only that
            // symbol, and continue with the remaining IDE1006 findings.
            var candidateErrors = IntroducedErrors(solution, changed, cancellation);
            if (!candidateErrors.IsEmpty) {
                var targetKey = Target(next, cancellation);
                rejected.Add(targetKey);
                skipped.Add(
                    targetKey.Path
                    + ":"
                    + (targetKey.Line + 1).ToString(System.Globalization.CultureInfo.InvariantCulture)
                    + " '"
                    + targetKey.Identifier
                    + "' because it would introduce "
                    + string.Join(", ", candidateErrors.Take(3))
                );
                continue;
            }

            solution = changed;
            applied++;
        }

        if (attempted == MaximumRenames
            && FindFirst(solution, analyzers, allowed, rejected, cancellation) is not null) {
            return new NamingFixOutcome(
                applied,
                [],
                $"the IDE1006 fix stopped after {MaximumRenames} rename attempts; violations remain"
            );
        }

        var introduced = IntroducedErrors(beforeErrors, CompilerErrors(solution, cancellation));
        if (introduced.Length > 0) {
            return new NamingFixOutcome(
                0,
                [],
                "the IDE1006 rename was reverted because it introduced compiler diagnostics: "
                + string.Join(", ", introduced.Take(3))
            );
        }

        var changedPaths = ChangedPaths(original, solution, cancellation);
        foreach (var path in changedPaths) {
            var before = original.GetDocumentIdsWithFilePath(path)
                .Select(original.GetDocument)
                .FirstOrDefault(static document => document is not null);
            var after = solution.GetDocumentIdsWithFilePath(path)
                .Select(solution.GetDocument)
                .FirstOrDefault(static document => document is not null);
            if (before is null || after is null) {
                continue;
            }

            var beforeText = before.GetTextAsync(cancellation).GetAwaiter().GetResult();
            var guard = FixCommand.TagGuard(path, beforeText.ToString());
            if (!guard.IsEmpty
                && after.GetTextChangesAsync(before, cancellation)
                    .GetAwaiter()
                    .GetResult()
                    .Any(change => guard.Touches(change.Span))) {
                return new NamingFixOutcome(
                    0,
                    [],
                    $"the IDE1006 rename would change a protected formatter-off region in '{path}'"
                );
            }
        }

        if (!request.DryRun) {
            foreach (var path in changedPaths) {
                var document = solution.GetDocumentIdsWithFilePath(path)
                    .Select(solution.GetDocument)
                    .FirstOrDefault(static document => document is not null)!;
                var text = document.GetTextAsync(cancellation).GetAwaiter().GetResult().ToString();
                File.WriteAllText(path, text, new UTF8Encoding(false));
            }
        }

        return new(applied, changedPaths, Skipped: skipped.ToImmutable());
    }

    static Diagnostic? FindFirst(
        Solution solution,
        ImmutableArray<DiagnosticAnalyzer> analyzers,
        HashSet<string> allowed,
        HashSet<NamingTarget> rejected,
        CancellationToken cancellation
    ) {
        foreach (var project in solution.Projects.OrderBy(static project => project.FilePath, StringComparer.Ordinal)) {
            if (project.Language != LanguageNames.CSharp
                || project.GetCompilationAsync(cancellation).GetAwaiter().GetResult() is not { } compilation) {
                continue;
            }

            var diagnostics = compilation.WithAnalyzers(
                analyzers,
                new CompilationWithAnalyzersOptions(
                    project.AnalyzerOptions,
                    null,
                    true,
                    false,
                    false
                )
            )
                .GetAnalyzerDiagnosticsAsync(cancellation)
                .GetAwaiter()
                .GetResult();

            var next = diagnostics.Where(static diagnostic => diagnostic.Id == RoslynCodeStyle.NamingDiagnosticId)
                .Where(diagnostic => diagnostic.Location.SourceTree?.FilePath is { } path
                    && allowed.Contains(Path.GetFullPath(path))
                )
                .Where(diagnostic => !rejected.Contains(Target(diagnostic, cancellation)))
                .OrderBy(static diagnostic => diagnostic.Location.SourceTree!.FilePath, StringComparer.Ordinal)
                .ThenBy(static diagnostic => diagnostic.Location.SourceSpan.Start)
                .FirstOrDefault();
            if (next is not null) {
                return next;
            }
        }

        return null;
    }

    readonly record struct NamingTarget(string Path, int Line, string Identifier);

    static NamingTarget Target(Diagnostic diagnostic, CancellationToken cancellation) {
        var location = diagnostic.Location;
        var tree = location.SourceTree!;
        return new(
            Path.GetFullPath(tree.FilePath),
            location.GetLineSpan().StartLinePosition.Line,
            tree.GetText(cancellation).ToString(location.SourceSpan)
        );
    }

    static ImmutableArray<string> IntroducedErrors(
        Solution before,
        Solution after,
        CancellationToken cancellation
    ) {
        var affected = after.GetChanges(before)
            .GetProjectChanges()
            .Select(static change => change.ProjectId)
            .ToHashSet();
        return IntroducedErrors(
            CompilerErrors(before, cancellation, affected),
            CompilerErrors(after, cancellation, affected)
        );
    }

    static ImmutableArray<string> CompilerErrors(
        Solution solution,
        CancellationToken cancellation,
        HashSet<ProjectId>? projects = null
    ) {
        var errors = ImmutableArray.CreateBuilder<string>();
        foreach (var project in solution.Projects.Where(project =>
                     project.Language == LanguageNames.CSharp
                     && (projects is null || projects.Contains(project.Id))
                 )) {
            if (project.GetCompilationAsync(cancellation).GetAwaiter().GetResult() is not { } compilation) {
                continue;
            }

            foreach (var diagnostic in compilation.GetDiagnostics(cancellation)
                         .Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)) {
                errors.Add(
                    diagnostic.Id
                    + "@"
                    + (diagnostic.Location.SourceTree?.FilePath ?? project.FilePath ?? project.Name)
                );
            }
        }

        return errors.ToImmutable();
    }

    static ImmutableArray<string> IntroducedErrors(
        ImmutableArray<string> before,
        ImmutableArray<string> after
    ) {
        var remaining = before.GroupBy(static error => error, StringComparer.Ordinal)
            .ToDictionary(static group => group.Key, static group => group.Count(), StringComparer.Ordinal);
        var introduced = ImmutableArray.CreateBuilder<string>();
        foreach (var error in after) {
            if (remaining.TryGetValue(error, out var count) && count > 0) {
                remaining[error] = count - 1;
            } else {
                introduced.Add(error);
            }
        }

        return introduced.ToImmutable();
    }

    static ImmutableArray<string> ChangedPaths(
        Solution original,
        Solution changed,
        CancellationToken cancellation
    ) {
        var paths = ImmutableArray.CreateBuilder<string>();
        foreach (var projectChange in changed.GetChanges(original).GetProjectChanges()) {
            foreach (var id in projectChange.GetChangedDocuments()) {
                var before = original.GetDocument(id);
                var after = changed.GetDocument(id);
                if (before?.FilePath is not { Length: > 0 } path || after is null) {
                    continue;
                }

                var beforeText = before.GetTextAsync(cancellation).GetAwaiter().GetResult();
                var afterText = after.GetTextAsync(cancellation).GetAwaiter().GetResult();
                if (!beforeText.ContentEquals(afterText)) {
                    paths.Add(Path.GetFullPath(path));
                }
            }
        }

        return [.. paths.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)];
    }
}
