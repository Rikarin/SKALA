using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Analysis.Hosting;
using Rikarin.Skala.Analysis.Loading;
using Rikarin.Skala.Reporting;
using System.Collections.Immutable;

namespace Rikarin.Skala.Analysis;

/// <summary>
///     Roslyn's own fixers for the hosted <c>IDE*</c> ids, turned into the text edits every other
///     <c>skala fix</c> applies.
/// </summary>
/// <remarks>
///     ⚠ <b>Asked only by <c>skala fix --include</c>, never by <c>check</c>.</b> A fixer needs a
///     <see cref="Document" />, which means a workspace over the whole compilation and a second run of
///     the analyzer inside it — Roslyn's fixers read the diagnostic's properties and its location's own
///     tree, so a diagnostic from <c>check</c>'s compilation cannot be handed to them. That is too
///     dear to pay for findings nobody asked to fix, so in <c>check</c>'s report these findings carry
///     no fix, and every one of them is unsafe anyway (<see cref="RoslynCodeStyle.EditFixableIds" />).
///     <para>
///         ⚠ <b>One file, one edit.</b> A fix whose code action changes any document other than the
///         finding's own is dropped: <c>FixCommand</c> verifies and reverts per file, and a cross-file
///         change would be half applied the moment one file reverted. The fix's text changes are
///         joined into a single edit spanning the first change to the last, so that
///         <c>FixCommand</c>'s overlap rule — a later edit that overlaps an earlier one is dropped —
///         drops a whole fix rather than half of one (<c>IDE0290</c> rewrites the constructor and the
///         type's header in the same action).
///     </para>
///     <para>
///         ⚠ <b>The workspace compilation is the loaded one rebuilt</b>, from the same trees' text,
///         options and references, with Skala's severity provider put back on it: the workspace
///         installs its own from its <c>.editorconfig</c> documents, which would leave the hosted
///         defaults out and the analyzer reporting nothing.
///     </para>
/// </remarks>
public static class HostedCodeFixes {
    /// <summary>
    ///     <paramref name="findings" /> with Roslyn's fix attached to every one of <paramref name="ids" />
    ///     that has one.
    /// </summary>
    public static ImmutableArray<Finding> Attach(
        ImmutableArray<Finding> findings,
        LoadedProject? loaded,
        string repositoryRoot,
        IReadOnlyCollection<string> ids,
        CancellationToken cancellation
    ) {
        if (loaded is null || loaded.Mode == LoadMode.Loose || ids.Count == 0) {
            return findings;
        }

        var wanted = findings.Where(finding => !finding.HasFix
                && ids.Contains(finding.RuleId, StringComparer.OrdinalIgnoreCase)
                && finding.Suppression == SuppressionKind.None
            )
            .ToList();
        if (wanted.Count == 0) {
            return findings;
        }

        var codeStyle = RoslynCodeStyle.Load();
        var edits = new Dictionary<(string Path, string RuleId, int Start, int Length), FixEdit>();
        foreach (var unit in loaded.Units) {
            var mine = wanted.Where(finding => unit.ReportablePaths.Contains(finding.Path)
                    && !edits.ContainsKey(Key(finding))
                )
                .ToList();
            if (mine.Count == 0) {
                continue;
            }

            foreach (var (key, edit) in ForUnit(unit, repositoryRoot, mine, codeStyle, cancellation)
                         .GetAwaiter()
                         .GetResult()) {
                edits.TryAdd(key, edit);
            }
        }

        return [
            ..findings.Select(finding => edits.TryGetValue(Key(finding), out var edit)
                ? finding with { Fix = [edit], FixIsSafe = false }
                : finding
            )
        ];
    }

    static (string, string, int, int) Key(Finding finding) =>
        (finding.Path, finding.RuleId.ToUpperInvariant(), finding.Start, finding.Length);

    static async Task<List<((string, string, int, int) Key, FixEdit Edit)>> ForUnit(
        CompilationUnit unit,
        string repositoryRoot,
        List<Finding> findings,
        RoslynCodeStyleResult codeStyle,
        CancellationToken cancellation
    ) {
        var results = new List<((string, string, int, int), FixEdit)>();
        var ids = findings.Select(static finding => finding.RuleId.ToUpperInvariant())
            .ToHashSet(StringComparer.Ordinal);
        var analyzers = codeStyle.Analyzers
            .Where(analyzer => analyzer.SupportedDiagnostics.Any(descriptor => ids.Contains(descriptor.Id)))
            .ToImmutableArray();
        if (analyzers.IsEmpty) {
            return results;
        }

        var targets = findings.Select(Key).ToHashSet();
        using var workspace = new AdhocWorkspace();
        var projectId = ProjectId.CreateNewId(unit.Name);
        var documents = unit.Compilation.SyntaxTrees.Select(tree => DocumentInfo.Create(
                    DocumentId.CreateNewId(projectId, tree.FilePath),
                    Path.GetFileName(tree.FilePath),
                    loader: TextLoader.From(
                        TextAndVersion.Create(tree.GetText(cancellation), VersionStamp.Default, tree.FilePath)
                    ),
                    filePath: tree.FilePath
                )
            )
            .ToList();
        var parseOptions = unit.Compilation.SyntaxTrees.FirstOrDefault()?.Options;
        var solution = workspace.CurrentSolution.AddProject(
            ProjectInfo.Create(
                projectId,
                VersionStamp.Default,
                unit.Name,
                unit.Compilation.AssemblyName ?? unit.Name,
                LanguageNames.CSharp,
                compilationOptions: unit.Compilation.Options,
                parseOptions: parseOptions,
                documents: documents,
                metadataReferences: unit.Compilation.References
            )
        );

        var project = solution.GetProject(projectId)!;
        if (await project.GetCompilationAsync(cancellation).ConfigureAwait(false) is not { } compilation) {
            return results;
        }

        // The options and the severity provider answer by file path, and the paths are the unit's.
        var (options, _, severities) = EditorConfigOptions.For(unit, repositoryRoot);
        var configured = severities is null
            ? compilation
            : compilation.WithOptions(compilation.Options.WithSyntaxTreeOptionsProvider(severities));
        var diagnostics = await configured.WithAnalyzers(
                analyzers,
                new CompilationWithAnalyzersOptions(options, null, true, false, false)
            )
            .GetAnalyzerDiagnosticsAsync(cancellation)
            .ConfigureAwait(false);

        foreach (var diagnostic in diagnostics) {
            if (diagnostic.Location.SourceTree is not { } tree
                || !codeStyle.Fixers.TryGetValue(diagnostic.Id, out var fixer)) {
                continue;
            }

            var path = Path.GetFullPath(tree.FilePath);
            var key = (path, diagnostic.Id, diagnostic.Location.SourceSpan.Start, diagnostic.Location.SourceSpan.Length);
            if (!targets.Contains(key) || solution.GetDocument(tree) is not { } document) {
                continue;
            }

            if (await EditFor(document, diagnostic, fixer, path, cancellation).ConfigureAwait(false) is { } edit) {
                results.Add((key, edit));
            }
        }

        return results;
    }

    static async Task<FixEdit?> EditFor(
        Document document,
        Diagnostic diagnostic,
        CodeFixProvider fixer,
        string path,
        CancellationToken cancellation
    ) {
        try {
            var actions = new List<CodeAction>();
            await fixer.RegisterCodeFixesAsync(
                    new CodeFixContext(document, diagnostic, (action, _) => actions.Add(action), cancellation)
                )
                .ConfigureAwait(false);

            var action = actions.FirstOrDefault();
            while (action is { NestedActions.IsEmpty: false }) {
                action = action.NestedActions[0];
            }

            if (action is null) {
                return null;
            }

            var operations = await action.GetOperationsAsync(cancellation).ConfigureAwait(false);
            if (operations.OfType<ApplyChangesOperation>().FirstOrDefault()?.ChangedSolution is not { } changed) {
                return null;
            }

            var changedDocuments = changed.GetChanges(document.Project.Solution)
                .GetProjectChanges()
                .SelectMany(static change => change.GetChangedDocuments(true)
                    .Concat(change.GetAddedDocuments())
                    .Concat(change.GetRemovedDocuments())
                )
                .ToList();
            if (changedDocuments.Count != 1 || changedDocuments[0] != document.Id) {
                return null;
            }

            var fixedDocument = changed.GetDocument(document.Id)!;
            var before = await document.GetTextAsync(cancellation).ConfigureAwait(false);
            var after = await fixedDocument.GetTextAsync(cancellation).ConfigureAwait(false);
            var changes = (await fixedDocument.GetTextChangesAsync(document, cancellation).ConfigureAwait(false))
                .ToList();
            return Joined(path, before, after, changes);
        } catch (Exception exception) when (exception is not OperationCanceledException) {
            // ⚠ A fixer that throws costs its finding the fix and nothing else: the finding is still
            // reported, and a fix that cannot be computed is never guessed at.
            return null;
        }
    }

    /// <summary>The whole difference between two texts as one replacement.</summary>
    /// <remarks>
    ///     ⚠ The changes are only where the joined span starts and ends. Its replacement is read off
    ///     the fixed text, and everything after the last change is the original text shifted by the
    ///     total length difference — which is checked, so a change list that does not describe
    ///     <paramref name="after" /> produces no edit rather than a wrong one.
    /// </remarks>
    static FixEdit? Joined(string path, SourceText before, SourceText after, List<TextChange> changes) {
        if (changes.Count == 0) {
            return null;
        }

        var start = changes.Min(static change => change.Span.Start);
        var end = changes.Max(static change => change.Span.End);
        var delta = after.Length - before.Length;
        if (end + delta < start
            || end + delta > after.Length
            || !before.ToString(TextSpan.FromBounds(end, before.Length))
                .Equals(after.ToString(TextSpan.FromBounds(end + delta, after.Length)), StringComparison.Ordinal)
            || !before.ToString(TextSpan.FromBounds(0, start))
                .Equals(after.ToString(TextSpan.FromBounds(0, start)), StringComparison.Ordinal)) {
            return null;
        }

        return new(path, start, end - start, after.ToString(TextSpan.FromBounds(start, end + delta)));
    }
}
