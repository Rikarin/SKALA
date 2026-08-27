using System.Collections.Immutable;
using System.Globalization;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Analysis.Loading;
using Rikarin.Skala.Core.Diagnostics;
using Rikarin.Skala.Reporting;
using Rikarin.Skala.Rules;
using Rikarin.Skala.Rules.Metadata;
using Rikarin.Skala.Rules.Modernization;

namespace Rikarin.Skala.Analysis.Hosting;

/// <summary>What one compilation's analysis produced.</summary>
public sealed record AnalysisOutcome(
    ImmutableArray<Finding> Findings,
    ImmutableArray<SkalaDiagnostic> Diagnostics,
    bool Partial);

/// <summary>
/// <c>CompilationWithAnalyzers</c>, configured the way docs/plan/07 § "Running analyzers" says.
/// </summary>
/// <remarks>
/// Four settings, each of which is a decision:
/// <list type="bullet">
/// <item>
/// ⚠ <c>reportSuppressedDiagnostics: true</c> — Skala needs to distinguish "not found" from "found
/// and suppressed by <c>#pragma</c>", because a baseline has to see what was suppressed and because
/// a suppression audit is the SonarQube feature worth keeping.
/// </item>
/// <item>
/// ⚠ <c>onAnalyzerException</c> records <c>SK9030</c> and never aborts. A third-party analyzer that
/// throws on one syntax shape must not be able to turn a CI gate red for unrelated reasons — or,
/// worse, green by aborting the run early.
/// </item>
/// <item>Compiler diagnostics are part of the report, so one command answers "does this build and is it clean".</item>
/// <item><c>concurrentAnalysis: true</c>, with determinism restored by sorting afterwards, never by serialising.</item>
/// </list>
/// </remarks>
public static class AnalyzerHost {
    /// <summary>Skala's own analyzers. One instance set, reused across compilations (ADR-006).</summary>
    public static ImmutableArray<DiagnosticAnalyzer> Own { get; } = [
        new FileScopedNamespaceAnalyzer(), new NullPatternAnalyzer(), new ThrowIfNullAnalyzer(), new NullCoalescingAssignmentAnalyzer(),
        new CountPropertyAnalyzer(), new EnumGetValuesAnalyzer()
    ];

    /// <summary>
    /// The rules that cannot run under a given load mode, with the reason, for the SARIF.
    /// </summary>
    /// <remarks>
    /// ⚠ docs/plan/07 § loose: the mode "is honest, because the SARIF says <c>loadMode: loose</c>
    /// and lists the rules that were skipped". A report that omits this is a report whose clean
    /// result means something different from another clean result.
    /// </remarks>
    public static ImmutableArray<SkippedRule> SkippedFor(LoadMode mode) {
        if (mode != LoadMode.Loose) {
            return [];
        }

        var builder = ImmutableArray.CreateBuilder<SkippedRule>();
        foreach (var rule in RuleCatalog.All) {
            if (!rule.Retired && rule.RequiresSemantics) {
                builder.Add(
                    new SkippedRule(
                        rule.Id,
                        "requires a semantic model; --load=loose has no project (docs/plan/07 § loose)"
                    )
                );
            }
        }

        return builder.ToImmutable();
    }

    public static AnalysisOutcome Run(
        CompilationUnit unit,
        AnalyzerOptions options,
        ImmutableArray<DiagnosticAnalyzer> hosted,
        LoadMode mode,
        CancellationToken cancellation
    ) =>
        Execute(unit, options, hosted, mode, trees: null, cancellation);

    /// <summary>
    /// The warm path: run the analyzers over only the trees whose cache key moved.
    /// </summary>
    /// <remarks>
    /// ⚠ Syntax <em>and</em> semantic actions, per tree. Running only
    /// <c>GetAnalyzerSyntaxDiagnosticsAsync</c> would silently drop every semantic rule from a warm
    /// run, so a file would produce different findings depending on whether the cache was cold —
    /// which is the cache lying, in the direction that looks like progress.
    /// </remarks>
    public static AnalysisOutcome RunForTrees(
        CompilationUnit unit,
        AnalyzerOptions options,
        ImmutableArray<DiagnosticAnalyzer> hosted,
        LoadMode mode,
        IReadOnlyList<SyntaxTree> trees,
        CancellationToken cancellation
    ) =>
        Execute(unit, options, hosted, mode, trees, cancellation);

    /// <summary>The rule set a load mode allows, as instantiated analyzers.</summary>
    public static ImmutableArray<DiagnosticAnalyzer> EnabledFor(
        LoadMode mode,
        ImmutableArray<DiagnosticAnalyzer> hosted
    ) =>
        Select(mode, hosted);

    static AnalysisOutcome Execute(
        CompilationUnit unit,
        AnalyzerOptions options,
        ImmutableArray<DiagnosticAnalyzer> hosted,
        LoadMode mode,
        IReadOnlyList<SyntaxTree>? trees,
        CancellationToken cancellation
    ) {
        var diagnostics = ImmutableArray.CreateBuilder<SkalaDiagnostic>();
        var failed = new HashSet<string>(StringComparer.Ordinal);
        var analyzers = Select(mode, hosted);
        if (analyzers.IsEmpty) {
            return new AnalysisOutcome([], diagnostics.ToImmutable(), false);
        }

        var withAnalyzers = unit.Compilation.WithAnalyzers(
            analyzers,
            new CompilationWithAnalyzersOptions(
                options,
                onAnalyzerException: (exception, analyzer, diagnostic) => {
                    // ⚠ Recorded and continued, never rethrown. See the type's remarks.
                    var name = analyzer.GetType().FullName ?? analyzer.GetType().Name;
                    lock (failed) {
                        if (!failed.Add(name)) {
                            return;
                        }
                    }

                    lock (diagnostics) {
                        diagnostics.Add(
                            new SkalaDiagnostic(
                                RuleIds.AnalyzerThrew,
                                SkalaSeverity.Warning,
                                $"analyzer '{name}' threw on rule '{diagnostic.Id}' and was disabled for the rest of the run: {exception.Message}",
                                diagnostic.Location.SourceTree?.FilePath ?? unit.Name
                            )
                        );
                    }
                },
                concurrentAnalysis: true,
                logAnalyzerExecutionTime: true,
                reportSuppressedDiagnostics: true
            )
        );

        ImmutableArray<Diagnostic> produced;
        var partial = false;
        try {
            produced = trees is null
                ? withAnalyzers.GetAllDiagnosticsAsync(cancellation).GetAwaiter().GetResult()
                : ForTrees(unit, withAnalyzers, trees, cancellation);
        } catch (OperationCanceledException) {
            // ⚠ Ctrl-C prints what was found so far, marked partial (docs/plan/07 § "Cancellation").
            return new AnalysisOutcome([], diagnostics.ToImmutable(), true);
        }

        var findings = ImmutableArray.CreateBuilder<Finding>();
        foreach (var diagnostic in produced) {
            // ⚠ In loose mode the compiler's own diagnostics are dropped, and it is not a
            // convenience. There is no project, so half the references are missing and CS0246 is
            // the expected state rather than a finding; reporting them would bury the rules the
            // mode exists to run under a few hundred complaints about the user's own code being
            // broken. Roslyn will not let an *error* be suppressed through
            // specificDiagnosticOptions, so the filter has to be here.
            if (mode == LoadMode.Loose && diagnostic.Id.StartsWith("CS", StringComparison.Ordinal)) {
                continue;
            }

            if (Convert(diagnostic, unit) is { } finding) {
                findings.Add(finding);
            }
        }

        partial |= diagnostics.Count > 0;
        return new AnalysisOutcome(findings.ToImmutable(), diagnostics.ToImmutable(), partial);
    }

    static ImmutableArray<Diagnostic> ForTrees(
        CompilationUnit unit,
        CompilationWithAnalyzers withAnalyzers,
        IReadOnlyList<SyntaxTree> trees,
        CancellationToken cancellation
    ) {
        var builder = ImmutableArray.CreateBuilder<Diagnostic>();
        foreach (var tree in trees) {
            var syntax = withAnalyzers.GetAnalysisResultAsync(tree, cancellation).GetAwaiter().GetResult();
            builder.AddRange(syntax.GetAllDiagnostics());

            var model = unit.Compilation.GetSemanticModel(tree);
            var semantic = withAnalyzers
                .GetAnalysisResultAsync(model, filterSpan: null, cancellation)
                .GetAwaiter()
                .GetResult();
            builder.AddRange(semantic.GetAllDiagnostics());

            // ⚠ The compiler's own diagnostics for this tree, so that a warm run answers "does this
            // build and is it clean" the same way a cold one does.
            builder.AddRange(model.GetDiagnostics(null, cancellation));
        }

        return builder.ToImmutable();
    }

    static ImmutableArray<DiagnosticAnalyzer> Select(LoadMode mode, ImmutableArray<DiagnosticAnalyzer> hosted) {
        if (mode != LoadMode.Loose) {
            return [.. Own, .. hosted];
        }

        // ⚠ In loose mode only the rules that declare no need for semantics run. A third-party
        // analyzer declares nothing Skala can read, so it does not run either: an analyzer answering
        // "no finding" because a symbol did not resolve is worse than an analyzer that did not run,
        // because only one of the two says so.
        var builder = ImmutableArray.CreateBuilder<DiagnosticAnalyzer>();
        foreach (var analyzer in Own) {
            var needsSemantics = false;
            foreach (var descriptor in analyzer.SupportedDiagnostics) {
                if (RuleCatalog.Find(descriptor.Id) is { RequiresSemantics: true }) {
                    needsSemantics = true;
                    break;
                }
            }

            if (!needsSemantics) {
                builder.Add(analyzer);
            }
        }

        return builder.ToImmutable();
    }

    static Finding? Convert(Diagnostic diagnostic, CompilationUnit unit) {
        var tree = diagnostic.Location.SourceTree;
        if (tree is null) {
            return null;
        }

        var path = Path.GetFullPath(tree.FilePath);

        // ⚠ Analysed, never reported on. A diagnostic in a file the user cannot edit is noise, and
        // the generated file is in the compilation because leaving it out changes what the semantic
        // model says about everything else.
        if (!unit.ReportablePaths.Contains(path)) {
            return null;
        }

        var span = diagnostic.Location.GetLineSpan();
        var textSpan = diagnostic.Location.SourceSpan;

        return new Finding {
            RuleId = diagnostic.Id,
            Severity = Severity(diagnostic),
            Message = diagnostic.GetMessage(CultureInfo.InvariantCulture),
            Path = path,
            Line = span.StartLinePosition.Line + 1,
            Column = span.StartLinePosition.Character + 1,
            EndLine = span.EndLinePosition.Line + 1,
            EndColumn = span.EndLinePosition.Character + 1,
            Start = textSpan.Start,
            Length = textSpan.Length,
            Fix = ReadFix(diagnostic, path),
            FixIsSafe = RuleCatalog.Find(diagnostic.Id) is { FixIsSafe: true },
            TargetFrameworks = unit.TargetFramework.Length == 0 ? [] : [unit.TargetFramework],
            Suppression = diagnostic.IsSuppressed ? SuppressionKind.Pragma : SuppressionKind.None
        };
    }

    /// <summary>Unpacks the text edits a Skala rule attached to its diagnostic.</summary>
    static ImmutableArray<FixEdit> ReadFix(Diagnostic diagnostic, string path) {
        if (!diagnostic.Properties.TryGetValue(FixEdits.CountKey, out var countText)
            || !int.TryParse(countText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var count)
            || count <= 0) {
            return [];
        }

        var builder = ImmutableArray.CreateBuilder<FixEdit>(count);
        for (var i = 0; i < count; i++) {
            if (!diagnostic.Properties.TryGetValue(FixEdits.StartKey(i), out var startText)
                || !diagnostic.Properties.TryGetValue(FixEdits.LengthKey(i), out var lengthText)
                || !diagnostic.Properties.TryGetValue(FixEdits.TextKey(i), out var text)
                || !int.TryParse(startText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var start)
                || !int.TryParse(lengthText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var length)) {
                return [];
            }

            builder.Add(new FixEdit(path, start, length, text ?? string.Empty));
        }

        return builder.ToImmutable();
    }

    static SkalaSeverity Severity(Diagnostic diagnostic) =>
        diagnostic.Severity switch {
            DiagnosticSeverity.Error => SkalaSeverity.Error,
            DiagnosticSeverity.Warning => SkalaSeverity.Warning,
            DiagnosticSeverity.Info => SkalaSeverity.Info,
            _ => SkalaSeverity.Hidden
        };

    /// <summary>
    /// Merges near-duplicate findings from a multi-targeted build.
    /// </summary>
    /// <remarks>
    /// ⚠ docs/plan/07 § "Multi-targeting": merged on <c>(ruleId, file, line, column, message)</c>,
    /// with the target-framework list carried as a property, "so a finding that only occurs under
    /// one target is visibly a one-target finding". Dropping the list would make the two cases
    /// indistinguishable, which is the whole reason the merge is allowed at all.
    /// </remarks>
    public static ImmutableArray<Finding> Merge(IEnumerable<Finding> findings) {
        var order = new List<(string, string, int, int, string)>();
        var merged = new Dictionary<(string, string, int, int, string), Finding>();

        foreach (var finding in findings) {
            var key = finding.MergeKey;
            if (merged.TryGetValue(key, out var existing)) {
                var frameworks = existing.TargetFrameworks;
                foreach (var framework in finding.TargetFrameworks) {
                    if (!frameworks.Contains(framework)) {
                        frameworks = frameworks.Add(framework);
                    }
                }

                merged[key] = existing with { TargetFrameworks = frameworks };
                continue;
            }

            order.Add(key);
            merged[key] = finding;
        }

        var builder = ImmutableArray.CreateBuilder<Finding>(order.Count);
        foreach (var key in order) {
            var finding = merged[key];
            builder.Add(finding with { TargetFrameworks = [.. finding.TargetFrameworks.Sort(StringComparer.Ordinal)] });
        }

        return builder.ToImmutable();
    }
}
