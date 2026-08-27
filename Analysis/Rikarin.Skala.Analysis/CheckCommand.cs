using System.Collections.Immutable;
using System.Diagnostics;
using System.Text;
using Rikarin.Skala.Analysis.Hosting;
using Rikarin.Skala.Analysis.Loading;
using Rikarin.Skala.Core;
using Rikarin.Skala.Core.Configuration;
using Rikarin.Skala.Core.Diagnostics;
using Rikarin.Skala.Formatting.CSharp;
using Rikarin.Skala.Reporting;
using Rikarin.Skala.Rules.Metadata;

namespace Rikarin.Skala.Analysis;

/// <summary>What <c>skala check</c> was asked to do.</summary>
public sealed record CheckRequest {
    public IReadOnlyList<string> Paths { get; init; } = [];

    public string? RepositoryRoot { get; init; }

    public LoadMode Mode { get; init; } = LoadMode.Binlog;

    public string? BinlogPath { get; init; }

    public string? ProjectPath { get; init; }

    public bool RequireFreshBinlog { get; init; }

    public string Gate { get; init; } = "local";

    public ReportFormat Format { get; init; } = ReportFormat.Terminal;

    public bool IncludeHints { get; init; }

    public bool NoCache { get; init; }

    /// <summary>⚠ Whether formatting findings (SK0001) take part. <c>verify</c> turns them on.</summary>
    public bool IncludeFormatting { get; init; } = true;

    public bool ShowSuppressions { get; init; }

    public IReadOnlyList<string> Define { get; init; } = [];

    public IReadOnlyList<KeyValuePair<string, string>> Overrides { get; init; } = [];

    /// <summary>Where to write the SARIF. Null means <c>.skala/report.sarif</c>; empty means nowhere.</summary>
    public string? Output { get; init; }

    /// <summary>Rule ids to keep; empty means all of them.</summary>
    public IReadOnlyList<string> Rules { get; init; } = [];

    /// <summary>
    /// Whether a <c>resharper_*_highlighting</c> key may set a Skala rule's severity.
    /// </summary>
    /// <remarks>
    /// ⚠ Off by default, and docs/plan/16 § Q5 records the measurement that decided it: the author's
    /// own export sets <c>resharper_use_throw_if_null_method_highlighting = none</c>, so reading
    /// these keys as authoritative would switch SK1020 off in the repository the tool was built for,
    /// without anyone deciding to. <c>dotnet_diagnostic.SK…</c> always wins over it.
    /// </remarks>
    public bool ReadReSharperSeverities { get; init; }

    /// <summary>
    /// A git ref: only findings on lines it changed count as new (docs/plan/09 § "New-code definition").
    /// </summary>
    public string? Since { get; init; }

    /// <summary>
    /// The baseline to compare against. Empty means <c>.skala/baseline.sarif</c> if it exists.
    /// </summary>
    /// <remarks>
    /// ⚠ Null means "no baseline", empty string means "the default path, if there is one". The
    /// distinction is what lets a gate name a baseline without the command line repeating it and
    /// still lets <c>--no-baseline</c> mean something.
    /// </remarks>
    public string? BaselinePath { get; init; }

    /// <summary>⚠ Audits all four suppression mechanisms, not <c>#pragma</c> (docs/plan/09 § "Gates").</summary>
    public bool NoNewSuppressions { get; init; }

    /// <summary>Append one line to <c>.skala/history.jsonl</c>.</summary>
    public bool Record { get; init; }

    /// <summary>Print only the last three lines of the human report.</summary>
    public bool Summary { get; init; }

    /// <summary>⚠ Off by default: duplication is a whole-repository pass and `verify` must stay sub-second.</summary>
    public bool IncludeDuplication { get; init; }

    /// <summary>Compute the aggregate metrics doc 07 lists. On for <c>check</c>, off for <c>verify</c>.</summary>
    public bool IncludeMetrics { get; init; } = true;
}

/// <summary>
/// The implementation behind <c>skala check</c>.
/// </summary>
/// <remarks>
/// ⚠ It lives here rather than in the CLI because nothing may reference <c>Rikarin.Skala.Cli</c>
/// (docs/plan/02 § "The project graph"): the daemon, the MCP server and MSBuild host the same logic
/// and the CLI is argument parsing and rendering only.
/// </remarks>
public static class CheckCommand {
    public static (CommandResult Result, RunReport Report) Run(
        CheckRequest request,
        CancellationToken cancellation = default
    ) {
        var stopwatch = Stopwatch.StartNew();
        var root = Path.GetFullPath(
            request.RepositoryRoot
            ?? FormatCommand.FindRepositoryRoot(request.Paths.Count > 0 ? request.Paths[0] : ".")
            ?? Directory.GetCurrentDirectory()
        );

        var loaded = ProjectLoader.Load(
            new LoadRequest {
                RepositoryRoot = root,
                Mode = request.Mode,
                BinlogPath = request.BinlogPath,
                ProjectPath = request.ProjectPath,
                RequireFreshBinlog = request.RequireFreshBinlog,
                Paths = request.Paths,
                Define = request.Define
            },
            cancellation
        );

        var diagnostics = ImmutableArray.CreateBuilder<SkalaDiagnostic>();
        diagnostics.AddRange(loaded.Diagnostics);

        if (loaded.IsEmpty) {
            var empty = new RunReport {
                RepositoryRoot = root,
                Mode = loaded.Mode,
                Diagnostics = diagnostics.ToImmutable(),
                LoadSummary = loaded.Summary,
                Duration = stopwatch.Elapsed
            };

            return (
                new CommandResult(
                    ExitCodes.LoadFailure,
                    "skala check: no compilation could be built.\n"
                    + string.Join("\n", diagnostics.Select(static d => "  " + d))
                    + "\n"
                ),
                empty
            );
        }

        var toolConfig = Path.Combine(root, ToolConfiguration.FileName);
        var hosted = HostedAnalyzers.Load(HostedAnalyzers.Read(toolConfig));
        var resharperSeverities =
            request.ReadReSharperSeverities
            || HostedAnalyzers.ReadsReSharperSeverities(toolConfig);

        diagnostics.AddRange(hosted.Diagnostics);

        var findings = new List<Finding>();
        var partial = false;
        var files = 0;
        var lines = 0;

        // ⚠ Sequential over compilations, parallel inside each. docs/plan/07 § "Parallelism": each
        // large compilation holds hundreds of MB of metadata, and the bound is memory rather than
        // CPU. Roslyn's driver is already concurrent within a compilation, which is where the
        // parallelism that matters is.
        foreach (var unit in loaded.Units) {
            var (options, fingerprint, severities) = EditorConfigOptions.For(unit, root, resharperSeverities);

            // ⚠ The severity table has to live on the *compilation*, not on the analyzer options:
            // that is where Roslyn's driver reads `dotnet_diagnostic.X.severity` from.
            var configured = severities is null
                ? unit
                : unit with {
                    Compilation = unit.Compilation.WithOptions(
                        unit.Compilation.Options.WithSyntaxTreeOptionsProvider(severities)
                    )
                };

            var outcome = IncrementalAnalysis.Run(
                configured,
                options,
                hosted.Analyzers,
                loaded.Mode,
                root,
                fingerprint,
                useCache: !request.NoCache,
                cancellation
            );

            findings.AddRange(outcome.Findings);
            diagnostics.AddRange(outcome.Diagnostics);
            partial |= outcome.Partial;
            files += unit.ReportablePaths.Count;
            foreach (var tree in unit.Compilation.SyntaxTrees) {
                lines += tree.GetText(cancellation).Lines.Count;
            }
        }

        var formattingClean = true;
        if (request.IncludeFormatting) {
            var formatting = FormattingFindings.Collect(root, Paths(loaded, request), request, diagnostics);
            formattingClean = formatting.Length == 0;
            findings.AddRange(formatting);
        }

        // ⚠ Duplication is a whole-repository pass and is therefore opt-in. It runs before the
        // merge so its findings take part in supersession and filtering like any other.
        var duplication = new Duplication.DuplicationResult();
        if (request.IncludeDuplication) {
            var (clones, result) = DuplicationPass.Run(
                loaded,
                Paths(loaded, request),
                root,
                Duplication.CloneDetector.DefaultMinTokens,
                useCache: !request.NoCache,
                cancellation
            );

            findings.AddRange(clones);
            duplication = result;
        }

        var merged = AnalyzerHost.Merge(findings);
        merged = Supersession.Apply(merged);
        merged = Filter(merged, request);

        var definition = Gate.Read(toolConfig, request.Gate);

        // ⚠ Ordinals before anything reads a fingerprint. Every downstream step — the baseline
        // comparison, the SARIF, the suppression audit — hashes findings, and a hash computed
        // before the ordinal was assigned is a different hash.
        merged = Fingerprints.Assign(merged);

        var report = new RunReport {
            RepositoryRoot = root,
            Mode = loaded.Mode,
            Findings = merged,
            Diagnostics = diagnostics.ToImmutable(),
            SkippedRules = AnalyzerHost.SkippedFor(loaded.Mode),
            Extensions = hosted.Extensions,
            LoadSummary = loaded.Summary,
            FileCount = files,
            LineCount = lines,
            ConfigurationFingerprint = ConfigurationFingerprint(root),
            HasOverrides = request.Overrides.Count > 0,
            Duration = stopwatch.Elapsed,
            Partial = partial,
            GateThresholds = definition.Metrics,
            Metrics = Metrics(loaded, request, duplication, cancellation)
        };

        report = Scope(report, request, definition, diagnostics);
        report = report with { Diagnostics = diagnostics.ToImmutable(), Duration = stopwatch.Elapsed };

        var gate = Gate.Evaluate(definition, report, formattingClean);
        report = report with { Gate = gate };

        WriteSarif(report, request);
        Record(report, request);

        if (request.Format == ReportFormat.Github) {
            GithubRenderer.WriteStepSummary(report);
        }

        var output = request.Summary
            ? Renderer.Summary(report)
            : Renderer.Render(report, request.Format, request.IncludeHints);

        var exit = !gate.Passed
            ? ExitCodes.GateFailed
            : report.Diagnostics.Any(static d => d.Id == RuleIds.TokenStreamChanged)
            ? ExitCodes.InternalError
            : ExitCodes.Ok;

        return (new CommandResult(exit, output), report);
    }

    /// <summary>The aggregate metrics, with the duplication measurement folded in.</summary>
    /// <remarks>
    /// ⚠ Duplication and the member metrics are separate opt-ins because they cost different things:
    /// duplication is a whole-repository token pass, the member metrics are a syntax walk. A
    /// <c>verify</c> that has to stay sub-second wants neither; a <c>check</c> in CI wants both.
    /// </remarks>
    static MetricsSummary Metrics(
        LoadedProject loaded,
        CheckRequest request,
        Duplication.DuplicationResult duplication,
        CancellationToken cancellation
    ) {
        var metrics = request.IncludeMetrics ? MetricsPass.Run(loaded, cancellation) : MetricsSummary.Empty;
        return request.IncludeDuplication ? DuplicationPass.Fold(metrics, duplication) : metrics;
    }

    /// <summary>
    /// Applies the three composable scopings of docs/plan/09 § "New-code definition".
    /// </summary>
    /// <remarks>
    /// ⚠ Order matters: <c>--since</c> tags findings, then the baseline buckets them, then the
    /// suppression audit runs against the same ref. Running the baseline first would bucket
    /// findings the <c>--since</c> pass is about to change, and the two would disagree.
    /// <para>
    /// ⚠ A failure in any of them is a <em>diagnostic</em>, not a silent skip. <c>--since</c>
    /// against a ref git cannot resolve must not quietly produce "zero changed lines", because a
    /// <c>newIssues: 0</c> gate then passes for the worst possible reason.
    /// </para>
    /// </remarks>
    static RunReport Scope(
        RunReport report,
        CheckRequest request,
        GateDefinition definition,
        ImmutableArray<SkalaDiagnostic>.Builder diagnostics
    ) {
        var reference = request.Since ?? definition.Since;
        if (reference is { Length: > 0 }) {
            try {
                var changed = ChangedLines.Since(report.RepositoryRoot, reference);
                report = report with { Findings = changed.Apply(report.Findings), ChangedCodeReference = reference };
            } catch (Exception exception) when (exception is InvalidOperationException or IOException) {
                diagnostics.Add(
                    new SkalaDiagnostic(
                        RuleIds.AnalyzerThrew,
                        SkalaSeverity.Error,
                        "--since=" + reference + " could not be resolved: " + exception.Message,
                        report.RepositoryRoot
                    )
                );
            }
        }

        var baselinePath = BaselinePathFor(report.RepositoryRoot, request, definition);
        if (baselinePath is not null) {
            // ⚠ A baseline the caller *named* and that does not exist is reported. It is still
            // treated as empty — which fails a `newIssues` gate, the safe direction — but a CI
            // failure reading "0 accepted, 994 new" with no explanation is the failure mode doc 09
            // keeps warning about: right answer, unusable message. The first thing to try is
            // `skala baseline create --apply`, so the diagnostic says so.
            if (!File.Exists(baselinePath)) {
                diagnostics.Add(
                    new SkalaDiagnostic(
                        RuleIds.AnalyzerThrew,
                        SkalaSeverity.Warning,
                        "the gate names a baseline at "
                        + SarifWriter.Relative(report.RepositoryRoot, baselinePath)
                        + " and there is no such file, so every finding counts as new. "
                        + "`skala baseline create --apply` writes one.",
                        baselinePath
                    )
                );
            }

            try {
                var baseline = Baseline.Read(baselinePath);
                var comparison = baseline.Compare(report.Findings);
                report = report with {
                    Findings = comparison.Findings,
                    HasBaseline = true,
                    BaselineSummary = SarifWriter.Relative(report.RepositoryRoot, baselinePath)
                        + " ("
                        + baseline.Count.ToString(System.Globalization.CultureInfo.InvariantCulture)
                        + " accepted)",
                    Fixed = comparison.Fixed
                };
            } catch (Exception exception) when (exception is IOException or InvalidDataException) {
                diagnostics.Add(
                    new SkalaDiagnostic(
                        RuleIds.AnalyzerThrew,
                        SkalaSeverity.Error,
                        "the baseline at " + baselinePath + " could not be read: " + exception.Message,
                        baselinePath
                    )
                );
            }
        }

        if (request.NoNewSuppressions) {
            var against = reference ?? "HEAD";
            try {
                report = report with {
                    Suppressions = SuppressionAuditor.Compare(
                        report.RepositoryRoot,
                        against,
                        baselinePath ?? Baseline.DefaultPath(report.RepositoryRoot)
                    )
                };
            } catch (Exception exception) when (exception is InvalidOperationException or IOException) {
                diagnostics.Add(
                    new SkalaDiagnostic(
                        RuleIds.AnalyzerThrew,
                        SkalaSeverity.Error,
                        "--no-new-suppressions could not compare against " + against + ": " + exception.Message,
                        report.RepositoryRoot
                    )
                );
            }
        }

        return report;
    }

    /// <summary>
    /// Which baseline this run uses, if any.
    /// </summary>
    /// <remarks>
    /// ⚠ The default path is used only when the file exists. A repository that has never run
    /// <c>baseline create</c> gets no baseline rather than an empty one, so every finding is
    /// <see cref="BaselineBucket.Unknown"/> rather than <see cref="BaselineBucket.New"/> — which is
    /// what stops a <c>newIssues</c> gate from failing on a tree that simply has not been
    /// baselined.
    /// </remarks>
    static string? BaselinePathFor(string root, CheckRequest request, GateDefinition definition) {
        if (request.BaselinePath is { Length: > 0 } explicitPath) {
            return Path.GetFullPath(explicitPath);
        }

        if (definition.BaselinePath is { Length: > 0 } fromGate) {
            return Path.GetFullPath(Path.Combine(root, fromGate));
        }

        if (request.BaselinePath is null && definition.MaxNewIssues is null) {
            return null;
        }

        var @default = Baseline.DefaultPath(root);
        return File.Exists(@default) ? @default : null;
    }

    static void Record(RunReport report, CheckRequest request) {
        if (!request.Record) {
            return;
        }

        try {
            History.Append(
                report.RepositoryRoot,
                History.Entry(report, GitSha(report.RepositoryRoot), GitBranch(report.RepositoryRoot))
            );
        } catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) {
            // A read-only tree does not fail a check.
        }
    }

    static string GitSha(string root) => Capture(root, "rev-parse", "HEAD");

    static string GitBranch(string root) => Capture(root, "rev-parse", "--abbrev-ref", "HEAD");

    static string Capture(string root, params string[] arguments) {
        try {
            var start = new ProcessStartInfo("git") {
                WorkingDirectory = root,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            };

            foreach (var argument in arguments) {
                start.ArgumentList.Add(argument);
            }

            using var process = Process.Start(start);
            if (process is null) {
                return string.Empty;
            }

            var output = process.StandardOutput.ReadToEnd().Trim();
            process.StandardError.ReadToEnd();
            process.WaitForExit();
            return process.ExitCode == 0 ? output : string.Empty;
        } catch (Exception exception) when (exception is IOException or InvalidOperationException) {
            return string.Empty;
        }
    }

    static ImmutableArray<Finding> Filter(ImmutableArray<Finding> findings, CheckRequest request) {
        if (request.Rules.Count == 0 && request.ShowSuppressions) {
            return findings;
        }

        var builder = ImmutableArray.CreateBuilder<Finding>(findings.Length);
        foreach (var finding in findings) {
            if (request.Rules.Count > 0
                && !request.Rules.Contains(finding.RuleId, StringComparer.OrdinalIgnoreCase)) {
                continue;
            }

            // ⚠ Suppressed findings are carried in the report and dropped only at the last moment,
            // because reportSuppressedDiagnostics exists so that a baseline and an audit can see
            // them. `--show-suppressions` is the audit.
            if (!request.ShowSuppressions && finding.Suppression == SuppressionKind.Pragma) {
                continue;
            }

            builder.Add(finding);
        }

        return builder.ToImmutable();
    }

    /// <summary>
    /// The files SK0001 is measured over: the compilations' reportable set, filtered by whatever the
    /// caller asked for.
    /// </summary>
    /// <remarks>
    /// ⚠ Derived from the load rather than from the command line. The requested path is usually a
    /// directory, and formatting the directory's name is not a thing; and taking the argument
    /// literally would format generated files, which the analysis half is careful not to report on.
    /// One source of "which files does this run concern", for both halves.
    /// </remarks>
    static List<string> Paths(LoadedProject loaded, CheckRequest request) {
        var reportable = new List<string>();
        foreach (var unit in loaded.Units) {
            reportable.AddRange(unit.ReportablePaths);
        }

        if (request.Paths.Count == 0) {
            return reportable;
        }

        var requested = request.Paths.Select(Path.GetFullPath).ToArray();
        return [
            .. reportable.Where(path => requested.Any(root =>
                    string.Equals(path, root, StringComparison.Ordinal)
                    || path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal)
                )
            )
        ];
    }

    static void WriteSarif(RunReport report, CheckRequest request) {
        if (request.Output is { Length: 0 }) {
            return;
        }

        var path = request.Output ?? SkalaDirectory.PathFor(report.RepositoryRoot, "report.sarif");
        try {
            SkalaDirectory.EnsureForFile(path);
            File.WriteAllText(path, SarifWriter.Serialize(SarifWriter.Build(report)));
        } catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) {
            // A read-only tree does not fail a check; the rendered output is already on stdout.
        }
    }

    /// <summary>
    /// The hash of the effective option set and rule severities.
    /// </summary>
    /// <remarks>
    /// ⚠ doc 09 puts it in the SARIF's <c>tool.driver</c> because two reports with different
    /// fingerprints are not comparable, and a report that does not carry one invites comparing them
    /// anyway.
    /// </remarks>
    static string ConfigurationFingerprint(string root) {
        var builder = new StringBuilder();
        foreach (var document in EditorConfigChain.For(Path.Combine(root, "_.cs")).Documents) {
            builder.Append(document.Path).Append('@').Append(document.Version).Append(';');
        }

        foreach (var rule in RuleCatalog.All) {
            builder.Append(rule.Id).Append('=').Append(rule.DefaultSeverity).Append(',');
        }

        return Convert.ToHexStringLower(
            System.IO.Hashing.XxHash128.Hash(Encoding.UTF8.GetBytes(builder.ToString()))
        )[..16];
    }
}

/// <summary>
/// <c>supersedes</c>: one span, one finding (docs/plan/08 § "Rule metadata").
/// </summary>
/// <remarks>
/// ⚠ Where a hosted analyzer's rule and a Skala rule both fire on the same span, one is dropped and
/// which one is a documented, deterministic choice: the superseding rule wins, and the superseded
/// one stays in the report marked suppressed with the reason, so the SARIF still says the other
/// analyzer had an opinion.
/// </remarks>
public static class Supersession {
    static readonly Dictionary<string, string> SupersededBy = Build();

    static Dictionary<string, string> Build() {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var rule in RuleCatalog.All) {
            foreach (var superseded in rule.Supersedes) {
                map[superseded] = rule.Id;
            }
        }

        return map;
    }

    public static ImmutableArray<Finding> Apply(ImmutableArray<Finding> findings) {
        if (SupersededBy.Count == 0) {
            return findings;
        }

        var winners = new HashSet<(string, string, int, int)>();
        foreach (var finding in findings) {
            if (SupersededBy.ContainsValue(finding.RuleId)) {
                winners.Add((finding.RuleId, finding.Path, finding.Line, finding.Column));
            }
        }

        var builder = ImmutableArray.CreateBuilder<Finding>(findings.Length);
        foreach (var finding in findings) {
            if (SupersededBy.TryGetValue(finding.RuleId, out var winner)
                && winners.Contains((winner, finding.Path, finding.Line, finding.Column))) {
                builder.Add(finding with { Suppression = SuppressionKind.Superseded });
                continue;
            }

            builder.Add(finding);
        }

        return builder.ToImmutable();
    }
}
