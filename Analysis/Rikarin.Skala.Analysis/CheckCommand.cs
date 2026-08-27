using System.Collections.Immutable;
using System.Diagnostics;
using System.Text;
using Rikarin.Skala.Analysis.Hosting;
using Rikarin.Skala.Analysis.Loading;
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

        var merged = AnalyzerHost.Merge(findings);
        merged = Supersession.Apply(merged);
        merged = Filter(merged, request);

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
            Partial = partial
        };

        var gate = Gate.Evaluate(
            Gate.Read(toolConfig, request.Gate),
            report,
            formattingClean
        );

        report = report with { Gate = gate };
        WriteSarif(report, request);

        var output = Renderer.Render(report, request.Format, request.IncludeHints);
        var exit = !gate.Passed
            ? ExitCodes.GateFailed
            : report.Diagnostics.Any(static d => d.Id == RuleIds.TokenStreamChanged)
            ? ExitCodes.InternalError
            : ExitCodes.Ok;

        return (new CommandResult(exit, output), report);
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

        var path = request.Output ?? Path.Combine(report.RepositoryRoot, ".skala", "report.sarif");
        try {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
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
