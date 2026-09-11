using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Analysis.Loading;
using Rikarin.Skala.Core.Diagnostics;
using Rikarin.Skala.Reporting;
using Rikarin.Skala.Rules;
using Rikarin.Skala.Rules.Metadata;
using System.Collections.Immutable;
using System.Globalization;

namespace Rikarin.Skala.Analysis.Hosting;

/// <summary>What one analyzer cost, for <c>--profile</c>.</summary>
/// <remarks>
///     docs/plan/13 § "Analysis": "<c>--profile</c> surfaces <c>logAnalyzerExecutionTime</c> output
///     ranked by cost. This is how a rule that is accidentally O(n²) in a method's statement count gets
///     found, and every Skala rule's cost is reviewed against it before release."
/// </remarks>
public sealed record AnalyzerCost(string Analyzer, ImmutableArray<string> Rules, TimeSpan Elapsed);

/// <summary>What one compilation's analysis produced.</summary>
public sealed record AnalysisOutcome(
    ImmutableArray<Finding> Findings,
    ImmutableArray<SkalaDiagnostic> Diagnostics,
    bool Partial,
    ImmutableArray<AnalyzerCost> Costs = default) {
    /// <summary>⚠ A <c>default</c> ImmutableArray throws on enumeration; profiling is opt-in.</summary>
    public ImmutableArray<AnalyzerCost> Costs { get; init; } = Costs.IsDefault ? [] : Costs;
}

/// <summary>
///     <c>CompilationWithAnalyzers</c>, configured the way docs/plan/07 § "Running analyzers" says.
/// </summary>
/// <remarks>
///     Four settings, each of which is a decision:
///     <list type="bullet">
///         <item>
///             ⚠ <c>reportSuppressedDiagnostics: true</c> — Skala needs to distinguish "not found" from "found
///             and suppressed by <c>#pragma</c>", because a baseline has to see what was suppressed and because
///             a suppression audit is the SonarQube feature worth keeping.
///         </item>
///         <item>
///             ⚠ <c>onAnalyzerException</c> records <c>SK9030</c> and never aborts. A third-party analyzer that
///             throws on one syntax shape must not be able to end the run early, green or red; the run
///             finishes, every other rule's findings are kept, and the gate fails on the <c>SK9030</c>
///             because the rules the thrower carries reported nothing wherever it threw (#295).
///             ⚠ Roslyn does <em>not</em> disable an analyzer that threw. Measured (#362) with three
///             files and an analyzer that throws in its syntax-tree action: three callbacks, one per
///             file. The message used to say "disabled for the rest of the run" and named the "rule"
///             it threw on as <c>AD0001</c> — Roslyn's id for <em>any</em> analyzer exception — so the
///             line an agent reads was wrong about both what happened next and which rules were lost.
///         </item>
///         <item>
///             Compiler diagnostics are part of the report, so one command answers "does this build and is it clean".
///         </item>
///         <item>
///             <c>concurrentAnalysis: true</c>, with determinism restored by sorting afterwards, never by serialising.
///         </item>
///     </list>
/// </remarks>
public static class AnalyzerHost {
    /// <summary>Skala's own analyzers, as the package declares them.</summary>
    /// <remarks>
    ///     ⚠ This used to be a hand-written list of 290 instances, and the fixture harness kept a
    ///     second hand-written copy of the same 290. Both now read
    ///     <see cref="SkalaAnalyzers.All" />, so a rule cannot be measured by one set and shipped by
    ///     another (#297).
    /// </remarks>
    public static ImmutableArray<DiagnosticAnalyzer> Own => SkalaAnalyzers.All;

    /// <summary>
    ///     The rules that cannot run under a given load mode, with the reason, for the SARIF.
    /// </summary>
    /// <remarks>
    ///     ⚠ docs/plan/07 § loose: the mode "is honest, because the SARIF says <c>loadMode: loose</c>
    ///     and lists the rules that were skipped". A report that omits this is a report whose clean
    ///     result means something different from another clean result.
    /// </remarks>
    public static ImmutableArray<SkippedRule> SkippedFor(LoadMode mode) {
        if (mode != LoadMode.Loose) {
            return [];
        }

        var builder = ImmutableArray.CreateBuilder<SkippedRule>();
        builder.Add(
            new SkippedRule(
                RoslynCodeStyle.NamingDiagnosticId,
                "requires a semantic model; --load=loose has no project (docs/plan/07 § loose)"
            )
        );
        foreach (var rule in RuleCatalog.All) {
            if (rule is { Retired: false, RequiresSemantics: true }) {
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
        CancellationToken cancellation,
        bool profile = false
    ) =>
        Execute(unit, options, Select(mode, hosted), mode, null, false, profile, cancellation);

    /// <summary>
    ///     The warm path: run the per-file analyzers over only the trees whose cache key moved.
    /// </summary>
    /// <remarks>
    ///     ⚠ Syntax <em>and</em> semantic actions, per tree. Running only
    ///     <c>GetAnalyzerSyntaxDiagnosticsAsync</c> would silently drop every semantic rule from a warm
    ///     run, so a file would produce different findings depending on whether the cache was cold —
    ///     which is the cache lying, in the direction that looks like progress.
    ///     <para>
    ///         ⚠ Only the analyzers <see cref="IsPerFileCacheable" /> admits, and the restriction is
    ///         here rather than at the call site so that no caller can run a whole-compilation rule over
    ///         one tree. Such a rule's answer for the tree depends on the trees it was not shown —
    ///         <c>SK2290</c> asks whether <em>every</em> caller discards the value — so a per-tree run
    ///         of it is not a partial answer but a wrong one. Its bucket is
    ///         <see cref="RunCompilationScoped" />.
    ///     </para>
    /// </remarks>
    public static AnalysisOutcome RunForTrees(
        CompilationUnit unit,
        AnalyzerOptions options,
        ImmutableArray<DiagnosticAnalyzer> hosted,
        LoadMode mode,
        IReadOnlyList<SyntaxTree> trees,
        CancellationToken cancellation,
        bool profile = false
    ) =>
        Execute(
            unit,
            options,
            [.. Select(mode, hosted).Where(IsPerFileCacheable)],
            mode,
            trees,
            false,
            profile,
            cancellation
        );

    /// <summary>
    ///     The other bucket of a warm run: the analyzers that cannot be served per file, over the whole
    ///     compilation, on every run in which anything changed.
    /// </summary>
    /// <remarks>
    ///     #364. This is the sentence docs/plan/07 § "The incremental cache" has carried since M5 —
    ///     "<c>Compilation</c>-scoped rules are excluded from per-file caching and re-run whenever
    ///     <em>any</em> file in the compilation changes" — made true. Until this method existed the
    ///     incremental pass had only two paths, and an enabled compilation-scoped rule sent the
    ///     <em>whole</em> rule set down the cold one; since the first of the five such rules shipped
    ///     (2026-09-01) that was every project-backed run in every repository, and the per-file cache
    ///     was written on each of them and read by none.
    ///     <para>
    ///         ⚠ Analyzer diagnostics only — no compiler diagnostics. The compiler's are per file and
    ///         are already in the per-file entries; folding them in here would report every
    ///         <c>CS</c> twice on a warm run.
    ///     </para>
    ///     <para>
    ///         Measured on 2026-09-11 over Skala's own 31 compilations, in-process, steady state: the
    ///         five whole-compilation analyzers alone cost 2.4 s against 6.8 s for all 299, of which 1.2 s
    ///         is the compiler binding any whole-compilation pass pays. So a warm run that changed one
    ///         file now costs roughly the five plus one tree instead of everything — and the 2.4 s is
    ///         the floor a per-file cache cannot lower, which is the case for the second tier
    ///         docs/plan/07 describes and does not build.
    ///     </para>
    /// </remarks>
    public static AnalysisOutcome RunCompilationScoped(
        CompilationUnit unit,
        AnalyzerOptions options,
        ImmutableArray<DiagnosticAnalyzer> hosted,
        LoadMode mode,
        CancellationToken cancellation,
        bool profile = false
    ) =>
        Execute(
            unit,
            options,
            [.. Select(mode, hosted).Where(static analyzer => !IsPerFileCacheable(analyzer))],
            mode,
            null,
            true,
            profile,
            cancellation
        );

    /// <summary>The rule set a load mode allows, as instantiated analyzers.</summary>
    public static ImmutableArray<DiagnosticAnalyzer> EnabledFor(
        LoadMode mode,
        ImmutableArray<DiagnosticAnalyzer> hosted
    ) =>
        Select(mode, hosted);

    /// <summary>
    ///     Whether every rule the analyzer carries may be served from the per-file cache — the
    ///     partition between <see cref="RunForTrees" /> and <see cref="RunCompilationScoped" />.
    /// </summary>
    /// <remarks>
    ///     ⚠ <b>Supported</b> descriptors, not enabled ones, and no severity is read at all. The old
    ///     guard asked <c>IsEnabledByDefault</c> so that <c>SK3001</c> — compilation-scoped, shipping
    ///     <c>defaultSeverity: none</c> — would not switch the cache off for everyone; the price was that
    ///     a repository which turned <c>SK3001</c> on in <c>.editorconfig</c> kept the warm path and lost
    ///     the rule's findings for every unchanged file (docs/plan/16 § "The opt-in that does not cost
    ///     what it says"). In a partition the question does not arise: membership of the
    ///     whole-compilation bucket disables nothing, and Roslyn skips an analyzer whose every descriptor
    ///     is off before invoking it, so <c>SK3001</c> costs nothing while off and is correct when on.
    ///     Reading the effective severity here would buy exactly that skip, which the driver already
    ///     does.
    ///     <para>
    ///         ⚠ An analyzer Skala cannot read the scope of is not per-file. A hosted third-party
    ///         analyzer declares nothing about whether its answer for <c>A.cs</c> depends on
    ///         <c>B.cs</c>, and the previous warm path (M5–M8) cached every one of them per file on no
    ///         basis at all. The one exception is Roslyn's own naming analyzer, which Skala loads
    ///         deliberately (<see cref="RoslynCodeStyle" />) and which decides each symbol on its own.
    ///         <see cref="ForcedCrash" /> is the harness's, throws from a syntax-tree action, and is
    ///         per-file so that a forced run takes whichever path an unforced one would (#363).
    ///     </para>
    /// </remarks>
    public static bool IsPerFileCacheable(DiagnosticAnalyzer analyzer) {
        if (analyzer is ForcedCrash) {
            return true;
        }

        foreach (var descriptor in analyzer.SupportedDiagnostics) {
            if (string.Equals(descriptor.Id, RoslynCodeStyle.NamingDiagnosticId, StringComparison.Ordinal)) {
                continue;
            }

            if (RuleCatalog.Find(descriptor.Id) is not { IsCacheable: true }) {
                return false;
            }
        }

        return true;
    }

    static AnalysisOutcome Execute(
        CompilationUnit unit,
        AnalyzerOptions options,
        ImmutableArray<DiagnosticAnalyzer> analyzers,
        LoadMode mode,
        IReadOnlyList<SyntaxTree>? trees,
        bool analyzerDiagnosticsOnly,
        bool profile,
        CancellationToken cancellation
    ) {
        var diagnostics = ImmutableArray.CreateBuilder<SkalaDiagnostic>();
        var crashes = new Dictionary<string, (DiagnosticAnalyzer Analyzer, string Message, int Count)>(
            StringComparer.Ordinal
        );
        if (analyzers.IsEmpty) {
            return new AnalysisOutcome([], diagnostics.ToImmutable(), false);
        }

        var withAnalyzers = unit.Compilation.WithAnalyzers(
            analyzers,
            new CompilationWithAnalyzersOptions(
                options,
                (exception, analyzer, _) => {
                    // ⚠ Recorded and continued, never rethrown. See the type's remarks. Counted per
                    // analyzer and written out once, below, so that the one `SK9030` says how many
                    // times it happened — "threw once" and "threw on every file" are different
                    // facts about how much of the tree its rules covered.
                    var name = analyzer.GetType().FullName ?? analyzer.GetType().Name;
                    lock (crashes) {
                        crashes[name] = crashes.TryGetValue(name, out var known)
                            ? known with { Count = known.Count + 1 }
                            : (analyzer, exception.Message, 1);
                    }
                },
                true,
                true,
                true
            )
        );

        ImmutableArray<Diagnostic> produced;
        var costs = ImmutableArray<AnalyzerCost>.Empty;
        try {
            if (trees is not null) {
                // ⚠ The warm path is measured on the warm path. `ForTrees` already goes through
                // `GetAnalysisResultAsync`, so profiling it costs nothing and changes nothing --
                // which matters, because an instrument that quietly measured the *cold* path when
                // asked about a warm run would report the one number the budget is not about.
                produced = ForTrees(unit, withAnalyzers, trees, analyzers, profile, ref costs, cancellation);
            } else if (profile) {
                // ⚠ `GetAnalysisResultAsync` rather than `GetAllDiagnosticsAsync`, and the reason
                // is not style. Roslyn returns its analyzer driver to a pool when the run finishes,
                // and the execution times go back with it, so `GetAnalyzerTelemetryInfoAsync` called
                // afterwards reports 0.0 ms for every analyzer -- which looks exactly like a fast
                // run. `AnalysisResult` captures the telemetry before the driver is released. The
                // first `--profile` output ever produced was nineteen analyzers at 0.0 ms, and it
                // was entirely believable.
                var result = withAnalyzers.GetAnalysisResultAsync(cancellation).GetAwaiter().GetResult();
                costs = Measure(result, analyzers);

                // ⚠ `AnalysisResult` carries only analyzer diagnostics; `GetAllDiagnosticsAsync`
                // also folds in the compiler's, which the loop below expects to see -- except from
                // the compilation-scoped bucket, whose compiler diagnostics are in the per-file entries.
                produced = analyzerDiagnosticsOnly
                    ? result.GetAllDiagnostics()
                    : [.. result.GetAllDiagnostics(), .. unit.Compilation.GetDiagnostics(cancellation)];
            } else if (analyzerDiagnosticsOnly) {
                produced = withAnalyzers.GetAnalyzerDiagnosticsAsync(cancellation).GetAwaiter().GetResult();
            } else {
                produced = withAnalyzers.GetAllDiagnosticsAsync(cancellation).GetAwaiter().GetResult();
            }
        } catch (OperationCanceledException) {
            // ⚠ Ctrl-C prints what was found so far, marked partial (docs/plan/07 § "Cancellation").
            return new AnalysisOutcome([], Crashed(unit, crashes, diagnostics), true);
        }

        var findings = ImmutableArray.CreateBuilder<Finding>();

        // ⚠ One semantic model per tree, reused. The enclosing symbol is the fingerprint's third
        // term and needs a model; building a fresh one per finding turns a file with forty findings
        // into forty binds of the same tree.
        var models = new Dictionary<SyntaxTree, SemanticModel>();

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

            // ⚠ The per-descriptor half of the loose-mode filter; see `Select`. An analyzer that
            // reports both semantic and syntactic rules runs, and the semantic ones are dropped
            // here — so what a loose run reports is exactly what `SkippedFor` says it reports.
            if (mode == LoadMode.Loose && RuleCatalog.Find(diagnostic.Id) is { RequiresSemantics: true }) {
                continue;
            }

            if (Convert(diagnostic, unit, models) is { } finding) {
                findings.Add(finding);
            }
        }

        // ⚠ #362: a crashed analyzer no longer marks the outcome partial. `partial |= diagnostics.Count
        // > 0` dated from M5, when the flag was the only thing a crash had to fail a verdict with;
        // #295 gave the crash its own gate clause and #309 made `Partial` mean, by name, "a unit was
        // cancelled and contributed no findings". Both of those were false for a crash — the unit
        // finished and every other rule's findings are in the report — and `check` printed
        // `SK9027 'loose' was cancelled before it finished` beside the `SK9030` that said what really
        // happened. `Partial` is the cancellation; the crash is the diagnostic.
        return new(
            findings.ToImmutable(),
            Crashed(unit, crashes, diagnostics),
            false,
            costs
        );
    }

    /// <summary>
    ///     The run's diagnostics with one <c>SK9030</c> per analyzer that threw, however many times it
    ///     threw.
    /// </summary>
    /// <remarks>
    ///     ⚠ Located at the project, or at the unit's name when the load mode has no project (loose):
    ///     Roslyn's exception diagnostic carries <c>Location.None</c>, so there is no source file to
    ///     name, and a crash is not about a file anyway — it is about every file the analyzer's rules
    ///     were supposed to cover. <c>IncompleteCause.CrashedAnalyzer</c> keeps it out of the banner's
    ///     <c>N of M files</c> fraction for the same reason.
    /// </remarks>
    static ImmutableArray<SkalaDiagnostic> Crashed(
        CompilationUnit unit,
        Dictionary<string, (DiagnosticAnalyzer Analyzer, string Message, int Count)> crashes,
        ImmutableArray<SkalaDiagnostic>.Builder diagnostics
    ) {
        lock (crashes) {
            foreach (var (name, crash) in crashes.OrderBy(static pair => pair.Key, StringComparer.Ordinal)) {
                var rules = crash.Analyzer.SupportedDiagnostics
                    .Select(static descriptor => descriptor.Id)
                    .Distinct(StringComparer.Ordinal)
                    .Order(StringComparer.Ordinal)
                    .ToArray();

                diagnostics.Add(
                    new SkalaDiagnostic(
                        RuleIds.AnalyzerThrew,
                        SkalaSeverity.Warning,
                        $"analyzer '{name}' threw {Times(crash.Count)}, so the rules it carries ({Rules(rules)}) "
                        + $"reported nothing wherever it threw: {crash.Message}",
                        unit.ProjectPath is { Length: > 0 } project ? project : unit.Name
                    )
                );
            }
        }

        return diagnostics.ToImmutable();
    }

    static string Times(int count) => count == 1 ? "once" : count.ToString(CultureInfo.InvariantCulture) + " times";

    /// <summary>Up to six ids, then a count — a banner line, not a catalogue.</summary>
    static string Rules(string[] rules) =>
        rules.Length switch {
            0 => "none declared",
            <= 6 => string.Join(", ", rules),
            _ => string.Join(", ", rules.Take(6))
                + " and "
                + (rules.Length - 6).ToString(CultureInfo.InvariantCulture)
                + " more"
        };

    /// <summary>
    ///     What each analyzer cost, taken off the result rather than asked for afterwards.
    /// </summary>
    /// <remarks>
    ///     ⚠ <c>logAnalyzerExecutionTime: true</c> had been set on every run since M5 and nothing
    ///     ever read it, so doc 13's promise that "every Skala rule's cost is reviewed against it
    ///     before release" had no instrument behind it. This is that instrument.
    /// </remarks>
    static ImmutableArray<AnalyzerCost> Measure(
        AnalysisResult result,
        ImmutableArray<DiagnosticAnalyzer> analyzers
    ) {
        var builder = ImmutableArray.CreateBuilder<AnalyzerCost>();
        foreach (var analyzer in analyzers) {
            if (!result.AnalyzerTelemetryInfo.TryGetValue(analyzer, out var telemetry)) {
                continue;
            }

            builder.Add(
                new AnalyzerCost(
                    analyzer.GetType().Name,
                    [.. analyzer.SupportedDiagnostics.Select(static descriptor => descriptor.Id)],
                    telemetry.ExecutionTime
                )
            );
        }

        return builder.ToImmutable();
    }

    static ImmutableArray<Diagnostic> ForTrees(
        CompilationUnit unit,
        CompilationWithAnalyzers withAnalyzers,
        IReadOnlyList<SyntaxTree> trees,
        ImmutableArray<DiagnosticAnalyzer> analyzers,
        bool profile,
        ref ImmutableArray<AnalyzerCost> costs,
        CancellationToken cancellation
    ) {
        var builder = ImmutableArray.CreateBuilder<Diagnostic>();
        var measured = new List<AnalyzerCost>();
        foreach (var tree in trees) {
            var syntax = withAnalyzers.GetAnalysisResultAsync(tree, cancellation).GetAwaiter().GetResult();
            builder.AddRange(syntax.GetAllDiagnostics());

            var model = unit.Compilation.GetSemanticModel(tree);
            var semantic = withAnalyzers
                .GetAnalysisResultAsync(model, null, cancellation)
                .GetAwaiter()
                .GetResult();
            builder.AddRange(semantic.GetAllDiagnostics());

            if (profile) {
                // ⚠ Both halves. A warm run pays for the syntax actions and the semantic actions
                // separately, and a profile showing only one of them would understate every
                // semantic rule -- which is every rule this milestone added.
                measured.AddRange(Measure(syntax, analyzers));
                measured.AddRange(Measure(semantic, analyzers));
            }

            // ⚠ The compiler's own diagnostics for this tree, so that a warm run answers "does this
            // build and is it clean" the same way a cold one does.
            builder.AddRange(model.GetDiagnostics(null, cancellation));
        }

        costs = [.. measured];
        return builder.ToImmutable();
    }

    static ImmutableArray<DiagnosticAnalyzer> Select(LoadMode mode, ImmutableArray<DiagnosticAnalyzer> hosted) {
        var selected = SelectFor(mode, hosted);
        return ForcedCrash.Requested is { } shouldThrow ? selected.Add(new ForcedCrash(shouldThrow)) : selected;
    }

    /// <summary>
    ///     The in-process way to request <see cref="ForcedCrash" />: which trees its action throws on,
    ///     or null for none. Flows with the caller's async context and nowhere else.
    /// </summary>
    /// <remarks>
    ///     ⚠ The environment variable cannot be set from inside a test process. xUnit runs test classes
    ///     in parallel, <c>Select</c> reads the variable on every run, and every other test sharing the
    ///     process would see the crash for as long as it was set — an intermittent red in whichever
    ///     class happened to be analysing at the time, attributed to nothing. #363 needs the crash
    ///     <em>in-process</em> because the property it proves is <c>IncrementalOutcome.CacheHits</c>,
    ///     which no output of the CLI carries. An <see cref="AsyncLocal{T}" /> is scoped to the test
    ///     that set it and to the work it starts, which is exactly the process-local variable the
    ///     harness needs. The predicate is captured into the analyzer instance when it is selected, so
    ///     Roslyn's worker threads never read this.
    /// </remarks>
    internal static AsyncLocal<Func<SyntaxTree, bool>?> ForcedCrashInProcess { get; } = new();

    static ImmutableArray<DiagnosticAnalyzer> SelectFor(LoadMode mode, ImmutableArray<DiagnosticAnalyzer> hosted) {
        if (mode != LoadMode.Loose) {
            return [.. Own, .. hosted];
        }

        // ⚠ In loose mode only the rules that declare no need for semantics run. A third-party
        // analyzer declares nothing Skala can read, so it does not run either: an analyzer answering
        // "no finding" because a symbol did not resolve is worse than an analyzer that did not run,
        // because only one of the two says so.
        //
        // ⚠ The filter is <b>per descriptor, not per analyzer</b>, and the difference is a whole
        // rule category. M6's metrics arrive as one analyzer reporting seven rules — one walk of the
        // member rather than seven — and only <c>SK7001</c> needs semantics, for the control-flow
        // graph. Dropping the analyzer because one of its seven descriptors needs a model would
        // silence the other six under <c>--load=loose</c> while <see cref="SkippedFor"/> named only
        // the one, which is precisely the "clean report that means two different things" that
        // docs/plan/07 § loose exists to prevent. So the analyzer runs and
        // <see cref="Execute"/> drops the findings of the rules that could not honestly answer.
        var builder = ImmutableArray.CreateBuilder<DiagnosticAnalyzer>();
        foreach (var analyzer in Own) {
            foreach (var descriptor in analyzer.SupportedDiagnostics) {
                if (RuleCatalog.Find(descriptor.Id) is not { RequiresSemantics: true }) {
                    builder.Add(analyzer);
                    break;
                }
            }
        }

        return builder.ToImmutable();
    }

    /// <summary>
    ///     The harness's way of making an analyzer throw, so that <c>SK9030</c> has a behavioural test
    ///     against the real binary.
    /// </summary>
    /// <remarks>
    ///     ⚠ The sibling of <c>CSharpFormatter.ForcedVerificationFailure</c> (<c>SKALA_FORCE_SK9099</c>),
    ///     for the same reason: no Skala analyzer throws on any input the corpus holds, and the one way
    ///     to host a throwing third-party analyzer is a package under the user's <c>~/.skala/packages</c>,
    ///     which a test must not write. Adding a real analyzer that really throws keeps the whole
    ///     downstream path real — Roslyn's <c>onAnalyzerException</c>, the diagnostic's text and
    ///     location, the reliability gate, the banner and the exit code — where a faked diagnostic
    ///     would have measured only the half of it the fake happened to match. #362 was exactly a
    ///     half nobody had measured: the gate failed on <c>SK9030</c> and the <c>agent</c> renderer
    ///     printed <c>OK  nothing to do.</c> above it.
    ///     <para>
    ///         ⚠ Appended after <see cref="SelectFor" />, so it runs under every load mode including
    ///         loose, where hosted analyzers are dropped. Its descriptor is deliberately not a Skala id:
    ///         it must never be confused with a rule that ships, and <c>RuleCatalog.Find</c> answering
    ///         null for it is what keeps it out of every per-rule table. Set by the harness and by nobody
    ///         else.
    ///     </para>
    ///     <para>
    ///         ⚠ #363: it declared <c>SK9030</c>'s own descriptor until this fix, and that made the
    ///         instrument blind to the defect it found. <c>SK9030</c> is <c>Compilation</c>-scoped in
    ///         the catalogue, so it is in <see cref="Caching.DiagnosticCache.Uncacheable" />, and at the
    ///         time an enabled uncacheable descriptor on any selected analyzer was precisely what sent
    ///         <see cref="IncrementalAnalysis" /> down the cold path (since #364 it would instead have
    ///         put the crash in the whole-compilation bucket, away from the per-file store the tests
    ///         are about). Every forced run was therefore a cold run, and "the second run still fails"
    ///         was measuring the guard, not the cache. The descriptor is now the harness's own —
    ///         enabled, because Roslyn skips an analyzer whose every descriptor is suppressed and an
    ///         empty set is vacuously all-suppressed (the first draft declared nothing and never ran) —
    ///         and <see cref="IsPerFileCacheable" /> names the type, so a forced run takes whichever
    ///         path an unforced one would.
    ///     </para>
    /// </remarks>
    // ⚠ RS1001 asks for [DiagnosticAnalyzer], which is the attribute Roslyn's *file* loader
    // discovers analyzers by, and this one is never loaded from a file — it is handed to
    // `CompilationWithAnalyzers` as an instance. Carrying the attribute would trip RS1038/RS1041
    // (this assembly references Workspaces and targets .NET 10), which are real objections to
    // shipping an analyzer from here and not objections to a harness type nobody ships.
#pragma warning disable RS1001 // an in-process harness analyzer, never discovered from a file
    sealed class ForcedCrash(Func<SyntaxTree, bool> shouldThrow) : DiagnosticAnalyzer {
#pragma warning restore RS1001
        internal const string Variable = "SKALA_FORCE_SK9030";

        /// <summary>
        ///     ⚠ The harness's id, not a rule's. <c>RuleCatalog.Find</c> answers null for it, so it is
        ///     in no per-rule table, and the <c>SK9030</c> message names it as what the analyzer
        ///     "carries" — which is right, because what it carries is the switch.
        /// </summary>
        // ⚠ RS2008 wants the id in an analyzer release file. This descriptor never reports and is
        // never shipped; tracking it would list a rule that does not exist.
#pragma warning disable RS2008 // a harness descriptor, never reported and never shipped
        static readonly DiagnosticDescriptor Descriptor = new(
            Variable,
            "Forced analyzer crash",
            "{0}",
            "Skala.Harness",
            DiagnosticSeverity.Warning,
            true
        );
#pragma warning restore RS2008

        /// <summary>
        ///     The trees to throw on, or null when no crash is requested: every tree when the
        ///     environment variable is set, the in-process predicate otherwise.
        /// </summary>
        internal static Func<SyntaxTree, bool>? Requested =>
            !string.IsNullOrEmpty(Environment.GetEnvironmentVariable(Variable))
                ? static _ => true
                : ForcedCrashInProcess.Value;

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Descriptor];

        public override void Initialize(AnalysisContext context) {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.RegisterSyntaxTreeAction(action => {
                    if (shouldThrow(action.Tree)) {
                        throw new InvalidOperationException(
                            "Forced by " + Variable + ". This is the harness, not a Skala bug."
                        );
                    }
                }
            );
        }
    }

    static Finding? Convert(
        Diagnostic diagnostic,
        CompilationUnit unit,
        Dictionary<SyntaxTree, SemanticModel> models
    ) {
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

        return new() {
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
            Suppression = diagnostic.IsSuppressed ? SuppressionKind.Pragma : SuppressionKind.None,
            EnclosingSymbol = EnclosingSymbol(unit, tree, textSpan.Start, models),
            Snippet = Snippet(tree, textSpan)
        };
    }

    /// <summary>
    ///     The display string of the symbol a finding sits in — the fingerprint's third term.
    /// </summary>
    /// <remarks>
    ///     docs/plan/09 § "The fingerprint": <c>Vixen.Core.Foo.Bar(int, string)</c>, "stable across file
    ///     moves".
    ///     <para>
    ///         ⚠ A lambda or a local function reports its <em>containing</em> member instead of itself.
    ///         Roslyn's display string for an anonymous function contains its position in the file, so a
    ///         fingerprint built on it would move whenever anything above it moved — which is the one
    ///         failure this term exists to prevent, reintroduced through the back door.
    ///     </para>
    ///     <para>
    ///         ⚠ Empty rather than throwing when the model cannot be built. A finding with no enclosing
    ///         symbol still gets a fingerprint; it is simply a weaker one, which is better than no finding.
    ///     </para>
    /// </remarks>
    static string EnclosingSymbol(
        CompilationUnit unit,
        SyntaxTree tree,
        int position,
        Dictionary<SyntaxTree, SemanticModel> models
    ) {
        try {
            if (!models.TryGetValue(tree, out var model)) {
                model = unit.Compilation.GetSemanticModel(tree);
                models[tree] = model;
            }

            var symbol = model.GetEnclosingSymbol(position);
            while (symbol is IMethodSymbol { MethodKind: MethodKind.AnonymousFunction or MethodKind.LocalFunction }) {
                symbol = symbol.ContainingSymbol;
            }

            return symbol?.ToDisplayString() ?? string.Empty;
        } catch (ArgumentException) {
            // A position outside the tree, which can happen for a diagnostic whose location was
            // mapped through a #line directive. Not worth failing a run over.
            return string.Empty;
        }
    }

    /// <summary>
    ///     The finding's own span, whitespace collapsed — the fingerprint's second term.
    /// </summary>
    /// <remarks>
    ///     ⚠ Bounded. A finding whose span is a whole 4 000-line type would otherwise put 4 000 lines
    ///     into every fingerprint computation and into the baseline's memory; the leading window is
    ///     enough to identify it and the ordinal disambiguates what is left.
    /// </remarks>
    static string Snippet(SyntaxTree tree, TextSpan span) {
        const int limit = 400;
        var text = tree.GetText();
        if (span.Start < 0 || span.End > text.Length) {
            return string.Empty;
        }

        var bounded = span.Length <= limit ? span : new TextSpan(span.Start, limit);
        return Fingerprints.Normalize(text.ToString(bounded));
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
    ///     Merges near-duplicate findings from a multi-targeted build.
    /// </summary>
    /// <remarks>
    ///     ⚠ docs/plan/07 § "Multi-targeting": merged on <c>(ruleId, file, line, column, message)</c>,
    ///     with the target-framework list carried as a property, "so a finding that only occurs under
    ///     one target is visibly a one-target finding". Dropping the list would make the two cases
    ///     indistinguishable, which is the whole reason the merge is allowed at all.
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
