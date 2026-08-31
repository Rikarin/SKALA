using Rikarin.Skala.Analysis.Loading;
using Rikarin.Skala.Core.Diagnostics;
using Rikarin.Skala.Formatting.CSharp;
using Rikarin.Skala.Formatting.CSharp.Arrangement;
using Rikarin.Skala.Reporting;
using System.Collections.Immutable;

namespace Rikarin.Skala.Analysis;

/// <summary>The structural-cleanup half of <c>verify</c>, produced by <c>arrange --check</c>.</summary>
public static class ArrangementFindings {
    public sealed record Result(ImmutableArray<Finding> Findings, bool Failed);

    public static Result Collect(
        string repositoryRoot,
        IReadOnlyList<string> paths,
        CheckRequest request,
        LoadedProject loaded,
        ImmutableArray<SkalaDiagnostic>.Builder diagnostics,
        CancellationToken cancellation
    ) {
        var findings = ImmutableArray.CreateBuilder<Finding>();
        var incomplete = false;
        var command = ArrangeCommand.Run(
            new ArrangeRequest {
                Paths = paths,
                RepositoryRoot = repositoryRoot,
                Check = true,
                Quiet = true,
                Overrides = request.Overrides,
                Define = request.Define,

                // A loose compilation exists only to host syntax analyzers. Passing it here would
                // falsely claim that semantic arrangement had run against a real project.
                Compilations = loaded.Mode == LoadMode.Loose
                    ? null
                    : _ => loaded.Units.Select(static unit => unit.Compilation).ToArray(),
                Observe = result => {
                    Add(result, repositoryRoot, findings, diagnostics);
                    incomplete |= !result.Converged
                        || result.Diagnostics.Any(static diagnostic =>
                            diagnostic.Id == ArrangeIds.RuleThrew || diagnostic.Severity >= SkalaSeverity.Error
                        );
                }
            },
            cancellation
        );

        var failed = command.ExitCode == ExitCodes.InternalError || incomplete;
        if (failed) {
            diagnostics.Add(
                new SkalaDiagnostic(
                    FormatDiagnosticIds.FileIoFailed,
                    SkalaSeverity.Error,
                    "arrange --check could not inspect every requested file",
                    repositoryRoot,
                    Detail: command.Output.Trim()
                )
            );
        }

        return new(findings.ToImmutable(), failed);
    }

    /// <summary>Semantic arrangement rules omitted by the deliberately projectless loose load.</summary>
    public static ImmutableArray<SkippedRule> SkippedFor(LoadMode mode) {
        if (mode != LoadMode.Loose) {
            return [];
        }

        return [
            .. Arranger.Rules()
                .Where(static rule => rule.NeedsSemantics)
                .Select(static rule => rule.Id)
                .Distinct(StringComparer.Ordinal)
                .Select(static id => new SkippedRule(
                        id,
                        "arrangement requires a semantic model; --load=loose has no project"
                    )
                )
        ];
    }

    static void Add(
        PipelineResult result,
        string repositoryRoot,
        ImmutableArray<Finding>.Builder findings,
        ImmutableArray<SkalaDiagnostic>.Builder diagnostics
    ) {
        diagnostics.AddRange(result.Diagnostics);
        if (result.Edits.IsEmpty || result.Applied.IsEmpty) {
            return;
        }

        var first = result.Edits[0];
        var position = result.Original.Lines.GetLinePosition(first.Span.Start);
        var applied = result.Applied.Distinct(StringComparer.Ordinal).ToArray();
        var relative = Path.GetRelativePath(repositoryRoot, result.Path).Replace('\\', '/');
        var argument = relative.Contains(' ', StringComparison.Ordinal) ? "\"" + relative + "\"" : relative;

        // One finding per document: all arrangement rules contribute to a single fixed-point diff,
        // so separate findings would carry overlapping instructions for the same structural edit.
        findings.Add(
            new Finding {
                RuleId = applied[0],
                Severity = SkalaSeverity.Info,
                Message = "the file is not arranged ("
                    + string.Join(", ", applied.Select(ArrangeIds.NameOf))
                    + "); run: `skala arrange "
                    + argument
                    + "`",
                Path = result.Path,
                Line = position.Line + 1,
                Column = position.Character + 1,
                EndLine = position.Line + 1,
                EndColumn = position.Character + 1,
                Start = first.Span.Start,
                Length = first.Span.End - first.Span.Start
            }
        );
    }
}
