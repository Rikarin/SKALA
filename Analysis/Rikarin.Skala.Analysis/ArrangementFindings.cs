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

    /// <summary>The ids a finding from <see cref="Collect" /> can carry: one per arrangement rule.</summary>
    /// <remarks>
    ///     ⚠ Taken from <see cref="Arranger.Rules" /> rather than listed, so a rule added to the
    ///     arranger is covered on the day it lands. No analyzer reports under any of them — the
    ///     <c>SK023x</c>–<c>SK028x</c> cleanup analyzers are a separate range and stay baselineable.
    /// </remarks>
    public static ImmutableHashSet<string> RuleIds { get; } =
        Arranger.Rules().Select(static rule => rule.Id).ToImmutableHashSet(StringComparer.Ordinal);

    /// <summary>
    ///     ⚠ #592: whether a finding is one the baseline may not accept, because its fix is
    ///     <c>skala arrange</c>.
    /// </summary>
    /// <remarks>
    ///     <c>arrange --check</c> — Lint, and the arrange half of <c>verify</c> — has no baseline and
    ///     fails on an unarranged file whatever <c>.skala/baseline.sarif</c> holds, and <c>check</c>
    ///     does not collect these findings at all. So an accepted arrangement finding suppressed
    ///     nothing any gate decides on, while <c>baseline update</c> reported it accepted: five
    ///     <c>SK0210</c> entries went in during a self-gate cleanup, <c>check --gate=ci</c> passed,
    ///     and Lint stayed red on the same files. An arrangement is switched off in
    ///     <c>.editorconfig</c>, or applied; there is no third state of "accepted for now", and one
    ///     finding per file keyed on whichever rule applied first is not a stable identity to accept
    ///     anyway.
    /// </remarks>
    public static bool Owns(string ruleId) => RuleIds.Contains(ruleId);

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
            new() {
                Paths = paths,
                RepositoryRoot = repositoryRoot,
                Check = true,
                Quiet = true,
                Overrides = request.Overrides,
                Define = request.Define,

                // ⚠ #395: the same decision `skala arrange` takes, from the same function, or `verify`
                // stops being `arrange --check` plus two other stages. Measured, not argued: a loose
                // compilation binds a file against something other than its project — no project
                // symbols, the runtime's assemblies, no siblings — and four semantic rules rewrote
                // probes the project compilation declined, one into a build break (`SK0210` deleting a
                // using needed under `#if`) and three into silent meaning changes (`SK0202`, `SK0205`,
                // `SK0211`). See `ArrangementCompilations` for the probes.
                Compilations = _ => ArrangementCompilations.For(loaded),
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
            // ⚠ #345: the message names the *stage*, because this diagnostic and the per-file one
            // above it are the whole of what a reader gets to explain an exit 5. "arrange --check
            // could not inspect every requested file" named a command line nobody typed; `verify`
            // runs three stages and the reader's first question is which of them stopped.
            diagnostics.Add(
                new(
                    FormatDiagnosticIds.FileIoFailed,
                    SkalaSeverity.Error,
                    "the arrange stage could not inspect every requested file; the files it names above "
                    + "were left exactly as they were and are not covered by this report",
                    repositoryRoot,
                    Detail: command.Output.Trim()
                )
            );
        }

        return new(findings.ToImmutable(), failed);
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
            new() {
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
