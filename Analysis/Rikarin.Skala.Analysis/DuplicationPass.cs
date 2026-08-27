using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Rikarin.Skala.Analysis.Duplication;
using Rikarin.Skala.Analysis.Loading;
using Rikarin.Skala.Reporting;

namespace Rikarin.Skala.Analysis;

/// <summary>
///     Turns a loaded project into the detector's input, and its answer into findings and a percentage.
/// </summary>
/// <remarks>
///     docs/plan/09 § "Duplication". ⚠ The file set is the whole of the tuning, and getting it wrong
///     makes the percentage meaningless rather than merely wrong:
///     <list type="bullet">
///         <item>
///             ⚠ <b>Generated files are out of the numerator and the denominator.</b> A generator that emits
///             the same 200-token shape for every one of four hundred types would otherwise report a codebase
///             as 60 % duplicated, and every one of those clones is something nobody wrote and nobody can fix.
///             The loader has already decided which files are generated, and this reads its answer rather than
///             asking the question again.
///         </item>
///         <item>
///             ⚠ <b>Test files are measured separately.</b> "Test duplication is often deliberate and gating it
///             drives people to write worse tests."
///         </item>
///         <item>
///             ⚠ <b>A file is offered once.</b> A multi-targeted project compiles the same file two or three
///             times; feeding each compilation's trees in would report every file in it as a perfect clone of
///             itself and take the percentage straight to 100.
///         </item>
///     </list>
/// </remarks>
public static class DuplicationPass {
    /// <summary>
    ///     Whether a file is test code — by assembly name where there is one, and by path otherwise.
    /// </summary>
    /// <remarks>
    ///     docs/plan/08 scopes the test rules "by convention (<c>*.Tests</c>)". The assembly name is
    ///     that convention expressed in the thing the build actually produced and is the better answer
    ///     when it exists.
    ///     <para>
    ///         ⚠ The path fallback is not belt-and-braces; it is what makes the number mean anything under
    ///         <c>--load=loose</c>. Loose mode has no projects, so every file arrives in one synthetic unit
    ///         named after the directory — and with only the assembly-name check, an entire repository's
    ///         tests land in the production numerator. Measured on Vixen that is the difference between a
    ///         production percentage that describes the engine and one that describes the engine plus its
    ///         test suite.
    ///     </para>
    /// </remarks>
    static bool IsTest(CompilationUnit unit, string path) =>
        Matches(unit.Name)
        || path.Split(Path.DirectorySeparatorChar).Any(static segment => Matches(segment));

    static bool Matches(string name) =>
        name.EndsWith(".Tests", StringComparison.OrdinalIgnoreCase)
        || name.EndsWith(".Test", StringComparison.OrdinalIgnoreCase)
        || name.Contains(".Tests.", StringComparison.OrdinalIgnoreCase);

    public static (ImmutableArray<Finding> Findings, DuplicationResult Result) Run(
        LoadedProject loaded,
        IReadOnlyCollection<string> paths,
        string repositoryRoot,
        int minTokens,
        bool useCache,
        CancellationToken cancellation
    ) {
        var inputs = new List<DuplicationInput>();

        // ⚠ One entry per path, whatever the target-framework count. See the type's remarks.
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var wanted = paths.Count == 0 ? null : new HashSet<string>(paths, StringComparer.Ordinal);

        foreach (var unit in loaded.Units) {
            foreach (var tree in unit.Compilation.SyntaxTrees) {
                cancellation.ThrowIfCancellationRequested();

                var path = Path.GetFullPath(tree.FilePath);
                if (path.Length == 0 || !seen.Add(path)) {
                    continue;
                }

                if (wanted is not null && !wanted.Contains(path)) {
                    continue;
                }

                // ⚠ `ReportablePaths` is the loader's own generated/not-generated answer. A file in
                // the compilation but not reportable is generated, and generated files leave the
                // measurement entirely rather than merely leaving the findings.
                if (!unit.ReportablePaths.Contains(path)) {
                    continue;
                }

                inputs.Add(
                    new DuplicationInput(
                        path,
                        tree.GetText(cancellation).ToString(),
                        IsGenerated: false,
                        IsTest(unit, path)
                    )
                );
            }
        }

        if (inputs.Count == 0) {
            return ([], new DuplicationResult());
        }

        var result = CloneDetector.Detect(
            inputs,
            minTokens,
            useCache ? Path.Combine(repositoryRoot, ".skala", "cache") : null,
            cancellation
        );

        return (CloneDetector.ToFindings(result, repositoryRoot), result);
    }

    /// <summary>Folds a detector result into the run's aggregate metrics.</summary>
    public static MetricsSummary Fold(MetricsSummary metrics, DuplicationResult result) =>
        metrics with {
            Duplication = result.Percentage,
            TestDuplication = result.TestPercentage,
            DuplicatedLines = result.DuplicatedLines,
            TotalLines = result.TotalLines,
            CloneGroupCount = result.Groups.Length
        };
}
