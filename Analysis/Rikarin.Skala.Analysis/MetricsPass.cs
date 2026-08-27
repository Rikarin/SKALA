using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Rikarin.Skala.Analysis.Loading;
using Rikarin.Skala.Reporting;
using Rikarin.Skala.Rules.Maintainability;

namespace Rikarin.Skala.Analysis;

/// <summary>
///     The aggregate half of docs/plan/07 § "Metrics": the numbers a gate reads and a trend plots.
/// </summary>
/// <remarks>
///     ⚠ <b>The same <see cref="MemberMetrics" /> the analyzer uses, deliberately.</b> A finding says
///     "this member is over the threshold" and an aggregate says "the codebase's p95 is 9"; those are
///     two surfaces over one measurement, and a second implementation of the measurement is a way for
///     the gate and the findings to disagree about the same method.
///     <para>
///         ⚠ It is nonetheless a <em>second walk</em> of the syntax trees, and that is a real cost the
///         design could not avoid: a <c>DiagnosticAnalyzer</c> can report diagnostics and cannot publish
///         anything else out of Roslyn's driver, so an aggregate computed inside the analyzer has no way
///         out. The alternatives were worse — emitting a hidden diagnostic per member turns 1.35 M lines
///         into a diagnostic per member, and computing the aggregate outside the analyzer from a *different*
///         walker reintroduces exactly the disagreement above. The walk is syntax-only and shares the trees
///         the loader already parsed, so it costs no re-parse; measured on Vixen it is the smaller half of
///         the run.
///     </para>
///     <para>
///         ⚠ Generated files are counted here even though the rules ignore them — doc 07 § binlog: "SK7xxx
///         metrics count them separately, because a generator that emits 200 000 lines of pathological code
///         is a fact worth having". They are kept out of the reported percentiles and carried in their own
///         counters.
///     </para>
/// </remarks>
public static class MetricsPass {
    public static MetricsSummary Run(LoadedProject loaded, CancellationToken cancellation) {
        var cognitive = new List<int>();
        var cyclomatic = new List<int>();
        var statements = new List<int>();
        var nesting = 0;
        var parameters = 0;
        var documentable = 0;
        var documented = 0;

        // ⚠ One visit per path. A multi-targeted project holds the same file in two compilations,
        // and counting it twice skews every percentile towards whatever multi-targets.
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var unit in loaded.Units) {
            foreach (var tree in unit.Compilation.SyntaxTrees) {
                cancellation.ThrowIfCancellationRequested();

                var path = Path.GetFullPath(tree.FilePath);
                if (!seen.Add(path) || !unit.ReportablePaths.Contains(path)) {
                    continue;
                }

                // ⚠ No semantic model. The aggregate uses the syntactic cyclomatic count rather than
                // the control-flow graph: building a CFG per member over a whole repository is the
                // module's only real cost, and a percentile over 1.35 M lines does not move when one
                // `foreach` scores 2 instead of 3. The *findings* still use the CFG, which is where
                // the precision is worth paying for, and `CyclomaticFromControlFlowGraph` is how a
                // reader tells the two apart.
                foreach (var node in tree.GetRoot(cancellation).DescendantNodes()) {
                    switch (node) {
                        case BaseMethodDeclarationSyntax:
                        case PropertyDeclarationSyntax:
                        case AccessorDeclarationSyntax:
                        case LocalFunctionStatementSyntax: {
                            var values = MemberMetrics.Compute(node, model: null, cancellation);
                            cognitive.Add(values.Cognitive);
                            cyclomatic.Add(values.Cyclomatic);
                            statements.Add(values.Statements);
                            nesting = Math.Max(nesting, values.NestingDepth);
                            parameters = Math.Max(parameters, values.Parameters);
                            break;
                        }
                    }

                    if (MemberMetrics.IsDocumentable(node) && MemberMetrics.IsPublicApi(node)) {
                        documentable++;
                        if (MemberMetrics.HasDocumentation(node)) {
                            documented++;
                        }
                    }
                }
            }
        }

        if (cognitive.Count == 0) {
            return MetricsSummary.Empty;
        }

        cognitive.Sort();
        cyclomatic.Sort();
        statements.Sort();

        return new MetricsSummary {
            MemberCount = cognitive.Count,
            CognitiveComplexityP95 = MetricsSummary.Percentile(cognitive, 0.95),
            CognitiveComplexityMax = cognitive[^1],
            CyclomaticComplexityP95 = MetricsSummary.Percentile(cyclomatic, 0.95),
            CyclomaticComplexityMax = cyclomatic[^1],
            MethodLengthP95 = MetricsSummary.Percentile(statements, 0.95),
            NestingDepthMax = nesting,
            ParameterCountMax = parameters,
            CommentDensity = documentable == 0 ? 0 : documented * 100.0 / documentable
        };
    }
}
