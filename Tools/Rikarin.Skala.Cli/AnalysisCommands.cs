using Rikarin.Skala.Analysis;
using Rikarin.Skala.Analysis.Caching;
using Rikarin.Skala.Analysis.Loading;
using Rikarin.Skala.Core.Configuration;
using Rikarin.Skala.Core.Diagnostics;
using Rikarin.Skala.Mcp;
using Rikarin.Skala.Options;
using Rikarin.Skala.Reporting;
using Rikarin.Skala.Rules.Metadata;
using System.CommandLine;

namespace Rikarin.Skala.Cli;

/// <summary>
///     The analysis half of the <c>skala</c> surface: <c>check</c>, <c>verify</c>, <c>fix</c>,
///     <c>explain</c>, <c>rules</c>, <c>docs</c>, <c>cache</c> and <c>mcp</c>.
/// </summary>
/// <remarks>
///     Argument parsing and rendering only, like the rest of the CLI. Every behaviour lives in
///     <c>Rikarin.Skala.Analysis</c>, because the daemon and the MCP server host the same logic and
///     nothing may reference this assembly (docs/plan/02 § "The project graph").
/// </remarks>
public static partial class SkalaCommandLine {
    /// <summary><c>skala check</c> — docs/plan/09.</summary>
    static Command CreateCheckCommand() {
        var paths = new Argument<string[]>("paths") {
            Description = "Files, directories or globs. Empty means the repository root.",
            Arity = ArgumentArity.ZeroOrMore
        };

        var load = new Option<string>("--load") {
            Description = "binlog | workspace | loose. Default binlog, falling back to workspace then loose.",
            DefaultValueFactory = static _ => "binlog"
        };

        var binlog = new Option<string?>("--binlog") {
            Description = "The binary log to read, instead of the conventional locations."
        };

        var project = new Option<string?>("--project") { Description = "The .slnx/.sln/.csproj for --load=workspace." };
        var requireFresh = new Option<bool>("--require-fresh-binlog") {
            Description = "Fail rather than analyse against a binlog older than the sources. CI sets it."
        };

        var gate = new Option<string>("--gate") {
            Description = "The named gate from skala.jsonc to evaluate.", DefaultValueFactory = static _ => "local"
        };

        var format = new Option<string>("--format") {
            Description = "terminal | plain | json | github | agent | markdown | junit.",
            DefaultValueFactory = static _ => "terminal"
        };

        var output = new Option<string?>("--output", "-o") {
            Description = "Where to write the SARIF. Default .skala/report.sarif."
        };
        var includeHints = new Option<bool>("--include-hints") { Description = "Show hint-level findings too." };
        var noCache = new Option<bool>("--no-cache") {
            Description = "Ignore the incremental cache and re-run every analyzer."
        };
        var noColor = new Option<bool>("--no-color") { Description = "Plain output, for a pipe or a CI log." };
        var showSuppressions = new Option<bool>("--show-suppressions") {
            Description = "Include findings suppressed by #pragma or [SuppressMessage] in the report."
        };

        var rules = new Option<string[]>("--rules") {
            Description = "Only these rule ids.", Arity = ArgumentArity.ZeroOrMore
        };

        var define = new Option<string[]>("--define", "-d") {
            Description = "Preprocessor symbols, for --load=loose.", Arity = ArgumentArity.ZeroOrMore
        };

        var noFormatting = new Option<bool>("--no-formatting") {
            Description = "Leave SK0001 out; report only the analyzers' findings."
        };

        // ⚠ Off by default. docs/plan/16 § Q5: the severities in a Rider export were chosen for
        // ReSharper's inspections, and the author's own export would switch SK1020 off.
        var resharperSeverities = new Option<bool>("--resharper-severities") {
            Description =
                "Let a resharper_*_highlighting key set a Skala rule's severity. dotnet_diagnostic.SK… still wins."
        };

        // ⚠ docs/plan/09 § "New-code definition": the three scopings are composable, so they are
        // three options rather than one mode. `--since` alone answers "did this branch make things
        // worse", `--baseline` alone answers "did anything new appear", and together they answer
        // "did this branch add a finding on a line it touched" — which is the PR gate.
        var since = new Option<string?>("--since") {
            Description = "A git ref. Findings on lines it changed count as new. Composes with --baseline."
        };

        var baseline = new Option<string?>("--baseline") {
            Description = "Compare against this baseline. Empty uses .skala/baseline.sarif when it exists.",
            Arity = ArgumentArity.ZeroOrOne
        };

        // ⚠ All four mechanisms, not #pragma. docs/plan/09: an .editorconfig severity turned down
        // suppresses far more than a pragma and looks like configuration in the diff.
        var noNewSuppressions = new Option<bool>("--no-new-suppressions") {
            Description =
                "Fail on a suppression added since the ref: #pragma, [SuppressMessage], an .editorconfig severity, or a baseline entry."
        };

        var record = new Option<bool>("--record") {
            Description = "Append one line to .skala/history.jsonl. `skala trend` renders it."
        };

        var summary = new Option<bool>("--summary") {
            Description = "Print only the totals, the metrics and the gate."
        };

        var duplication = new Option<bool>("--duplication") {
            Description = "Also run token-level clone detection (SK7020). Off by default: it is a whole-repository pass."
        };

        // ⚠ docs/plan/13 § "Analysis" promised this and nothing implemented it, so the sentence
        // "every Skala rule's cost is reviewed against it before release" had no instrument behind
        // it until M8. README lists its output under "explicitly not a contract".
        var profile = new Option<bool>("--profile") {
            Description = "Rank the analyzers by what they cost. An instrument, not a contract."
        };

        var command = new Command("check", "Run the analyzers and report, with a gate.");
        command.Arguments.Add(paths);
        foreach (var option in new Option[] {
                     load, binlog, project, requireFresh, gate, format, output, includeHints, noCache, noColor,
                     showSuppressions, rules, define, noFormatting, resharperSeverities, since, baseline,
                     noNewSuppressions, record, summary, duplication, profile
                 }) {
            command.Options.Add(option);
        }

        command.SetAction(parse => {
                if (!LoadModes.TryParse(parse.GetValue(load), out var mode)) {
                    Console.Error.WriteLine("skala check: --load must be binlog, workspace or loose.");
                    return ExitCodes.ConfigurationError;
                }

                var request = new CheckRequest {
                    Paths = parse.GetValue(paths) ?? [],
                    Mode = mode,
                    BinlogPath = parse.GetValue(binlog),
                    ProjectPath = parse.GetValue(project),
                    RequireFreshBinlog = parse.GetValue(requireFresh),
                    Gate = parse.GetValue(gate) ?? "local",
                    Format = ParseFormat(parse.GetValue(format), parse.GetValue(noColor)),
                    IncludeHints = parse.GetValue(includeHints),
                    NoCache = parse.GetValue(noCache),
                    ShowSuppressions = parse.GetValue(showSuppressions),
                    Rules = parse.GetValue(rules) ?? [],
                    Define = ParseDefines(parse.GetValue(define)),
                    IncludeFormatting = !parse.GetValue(noFormatting),
                    ReadReSharperSeverities = parse.GetValue(resharperSeverities),
                    Output = parse.GetValue(output),
                    Since = parse.GetValue(since),

                    // ⚠ Null and empty mean different things here. `--baseline` with no value is
                    // "use the conventional path if it exists"; the option absent entirely is "no
                    // baseline", which is what keeps a `newIssues` gate from failing on a tree
                    // nobody has baselined yet.
                    BaselinePath = parse.GetResult(baseline) is null ? null : parse.GetValue(baseline) ?? string.Empty,
                    NoNewSuppressions = parse.GetValue(noNewSuppressions),
                    Record = parse.GetValue(record),
                    Summary = parse.GetValue(summary),
                    IncludeDuplication = parse.GetValue(duplication),
                    Profile = parse.GetValue(profile),
                    Verbose = parse.GetValue(Verbose)
                };

                return RunCancellable(token => CheckCommand.Run(request, token).Result);
            }
        );

        return command;
    }

    /// <summary>
    ///     <c>skala verify</c> — docs/plan/10 § "`skala verify` — the one command".
    /// </summary>
    /// <remarks>
    ///     ⚠ Its defaults are the contract. Auto workspace discovery, agent output, formatting and
    ///     arrangement included:
    ///     it uses real semantics when one target is unambiguous and still works with no project, build or
    ///     network when the agent just wrote a file into a scratch directory.
    /// </remarks>
    static Command CreateVerifyCommand() {
        var paths = new Argument<string[]>("paths") {
            Description = "Files or directories. Empty means the repository root.", Arity = ArgumentArity.ZeroOrMore
        };

        var fix = new Option<bool>("--fix") { Description = "Apply the safe fixes first, then verify what is left." };
        var format = new Option<string>("--format") {
            Description = "agent | json | plain.", DefaultValueFactory = static _ => "agent"
        };

        var load = new Option<string>("--load") {
            Description = "auto | binlog | workspace | loose. Default auto: one workspace target, else loose.",
            DefaultValueFactory = static _ => "auto"
        };

        var project = new Option<string?>("--project") {
            Description = "The .slnx/.sln/.csproj for auto or --load=workspace."
        };

        var define = new Option<string[]>("--define", "-d") {
            Description = "Preprocessor symbols.", Arity = ArgumentArity.ZeroOrMore
        };

        var noCache = new Option<bool>("--no-cache") { Description = "Ignore the incremental cache." };

        // ⚠ The same two scopings `check` has, on the command an agent actually runs. Without them
        // `verify` on an adopted repository reported 778 findings needing a decision on every run
        // for ever — doc 10's report is a decision queue, and a queue nobody can drain is noise.
        var since = new Option<string?>("--since") {
            Description = "A git ref. Only findings on lines it changed are to do. Composes with --baseline."
        };

        var baseline = new Option<string?>("--baseline") {
            Description = "Findings this repository has already accepted are not to do. "
                + "Empty uses .skala/baseline.sarif when it exists.",
            Arity = ArgumentArity.ZeroOrOne
        };

        var command = new Command(
            "verify",
            "format --check + arrange --check + check --gate=local, shaped for an agent. Exit 0 means nothing to do."
        );

        command.Arguments.Add(paths);
        foreach (var option in new Option[] { fix, format, load, project, define, noCache, since, baseline }) {
            command.Options.Add(option);
        }

        command.SetAction(parse => {
                var loadText = parse.GetValue(load);
                LoadMode? mode = null;
                if (!string.Equals(loadText, "auto", StringComparison.OrdinalIgnoreCase)) {
                    if (!LoadModes.TryParse(loadText, out var parsed)) {
                        Console.Error.WriteLine("skala verify: --load must be auto, binlog, workspace or loose.");
                        return ExitCodes.ConfigurationError;
                    }

                    mode = parsed;
                }

                var request = new VerifyRequest {
                    Paths = parse.GetValue(paths) ?? [],
                    Mode = mode,
                    ProjectPath = parse.GetValue(project),
                    Fix = parse.GetValue(fix),
                    Format = ParseFormat(parse.GetValue(format), false),
                    NoCache = parse.GetValue(noCache),
                    Define = ParseDefines(parse.GetValue(define)),
                    Since = parse.GetValue(since),

                    // ⚠ Tri-state, exactly as on `check`: absent is null, bare `--baseline` is the
                    // empty string and means the conventional path if it is there, a value is that
                    // path. `GetValue` alone cannot tell "absent" from "given with no value".
                    BaselinePath = parse.GetResult(baseline) is null
                        ? null
                        : parse.GetValue(baseline) ?? string.Empty
                };

                return RunCancellable(token => VerifyCommand.Run(request, token));
            }
        );

        return command;
    }

    /// <summary><c>skala fix</c> — docs/plan/10 § "Fixes".</summary>
    static Command CreateFixCommand() {
        var paths = new Argument<string[]>("paths") {
            Description = "Files or directories. Empty means the repository root.", Arity = ArgumentArity.ZeroOrMore
        };

        var safe = new Option<bool>("--safe") {
            Description = "Apply every fix the catalogue marks safe. The default and the only unqualified mode."
        };

        var include = new Option<string[]>("--include") {
            Description = "⚠ Required without --safe: unsafe rules to apply. IDE1006 infers workspace mode.",
            Arity = ArgumentArity.ZeroOrMore
        };

        var dryRun = new Option<bool>("--dry-run") { Description = "Say what would be applied and write nothing." };
        var load = new Option<string>("--load") {
            Description = "auto | binlog | workspace | loose. Default auto; IDE1006 infers workspace.",
            DefaultValueFactory = static _ => "auto"
        };

        var binlog = new Option<string?>("--binlog") {
            Description = "The binary log to read, instead of the conventional locations."
        };

        var project = new Option<string?>("--project") {
            Description = "The workspace target for IDE1006 or --load=workspace."
        };

        var define = new Option<string[]>("--define", "-d") {
            Description = "Preprocessor symbols.", Arity = ArgumentArity.ZeroOrMore
        };

        var command = new Command("fix", "Apply the fixes the findings carry, verify each one, and re-format.");
        command.Arguments.Add(paths);
        foreach (var option in new Option[] { safe, include, dryRun, load, binlog, project, define }) {
            command.Options.Add(option);
        }

        command.SetAction(parse => {
                var loadText = parse.GetValue(load);
                LoadMode? mode = null;
                if (!string.Equals(loadText, "auto", StringComparison.OrdinalIgnoreCase)) {
                    if (!LoadModes.TryParse(loadText, out var parsed)) {
                        Console.Error.WriteLine("skala fix: --load must be auto, binlog, workspace or loose.");
                        return ExitCodes.ConfigurationError;
                    }

                    mode = parsed;
                }

                var included = parse.GetValue(include) ?? [];
                var request = new FixRequest {
                    Paths = parse.GetValue(paths) ?? [],
                    Mode = mode,
                    BinlogPath = parse.GetValue(binlog),
                    ProjectPath = parse.GetValue(project),
                    SafeOnly = parse.GetValue(safe) || included.Length == 0,
                    Include = included,
                    DryRun = parse.GetValue(dryRun),
                    Define = ParseDefines(parse.GetValue(define))
                };

                return RunCancellable(token => FixCommand.Run(request, token));
            }
        );

        return command;
    }

    /// <summary>
    ///     <c>skala explain SK1010</c> and <c>skala explain csharp_indent_case_contents</c> —
    ///     docs/plan/08 § "Documentation" and docs/plan/11's <c>&lt;ruleId | optionKey&gt;</c>.
    /// </summary>
    static Command CreateExplainCommand() {
        var ruleId = new Argument<string>("rule|option") {
            Description = "A rule id, e.g. SK1010, or an .editorconfig option key, e.g. csharp_indent_case_contents."
        };
        var command = new Command(
            "explain",
            "Print a rule's rationale and examples, or what an .editorconfig option governs."
        );
        command.Arguments.Add(ruleId);
        command.SetAction(parse => Run(() => ExplainCommand.Run(parse.GetValue(ruleId)!)));
        return command;
    }

    /// <summary><c>skala rules list|docs</c>.</summary>
    static Command CreateRulesCommand() {
        var rules = new Command("rules", "The rule catalogue.");

        var list = new Command("list", "Every allocated rule id, with its severity and scope.");
        list.SetAction(_ => {
                Console.Out.Write(ExplainCommand.RenderIndex());
                return ExitCodes.Ok;
            }
        );

        var directory = new Argument<string>("directory") {
            Description = "Where to write the pages.", DefaultValueFactory = static _ => "docs/rules"
        };

        var docs = new Command("docs", "Regenerate docs/rules/ and doc 08's coverage block from rules.json.");
        docs.Arguments.Add(directory);
        docs.SetAction(parse => {
                var target = parse.GetValue(directory)!;
                Directory.CreateDirectory(target);
                foreach (var rule in RuleCatalog.All) {
                    File.WriteAllText(Path.Combine(target, rule.Id + ".md"), ExplainCommand.RenderMarkdown(rule));
                }

                File.WriteAllText(Path.Combine(target, "README.md"), ExplainCommand.RenderIndex());
                Console.Out.WriteLine($"{RuleCatalog.All.Count} page(s) written to {target}.");
                return WriteCoverageBlock();
            }
        );

        rules.Subcommands.Add(list);
        rules.Subcommands.Add(docs);
        return rules;
    }

    /// <summary>
    ///     Rewrites the generated coverage block in <c>docs/plan/08-rule-catalogue.md</c>.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         ⚠ The catalogue's own coverage figure used to be typed by hand and it went stale inside one
    ///         merge — "21 shipped, 19.8 %" at <c>8cbd66d</c>, with M8's five <c>SK5xxx</c> landing
    ///         afterwards. A document that misreports its own coverage is the same failure as one
    ///         describing behaviour the tool does not have.
    ///     </para>
    ///     <para>
    ///         ⚠ It is quiet when the catalogue is not there. This runs from an installed tool as well as
    ///         from the repository, and <c>skala rules docs</c> writing rule pages is useful on its own;
    ///         failing it because a plan document is absent would make the command unusable everywhere
    ///         except here.
    ///     </para>
    /// </remarks>
    static int WriteCoverageBlock() {
        var path = Path.Combine("docs", "plan", "08-rule-catalogue.md");
        if (!File.Exists(path)) {
            return ExitCodes.Ok;
        }

        var catalogue = File.ReadAllText(path);
        var coverage = RuleCoverage.Compute(catalogue, RuleCatalog.All.Select(static rule => rule.Id));
        if (RuleCoverage.Replace(catalogue, RuleCoverage.Render(coverage)) is not { } updated) {
            Console.Error.WriteLine(
                $"{path} has no {RuleCoverage.BeginMarker} … {RuleCoverage.EndMarker} block; coverage not written."
            );

            return ExitCodes.ConfigurationError;
        }

        if (!string.Equals(updated, catalogue, StringComparison.Ordinal)) {
            File.WriteAllText(path, updated);
        }

        Console.Out.WriteLine(
            $"coverage: {coverage.Count(RuleCoverage.State.Shipped)} of {coverage.Named} shipped "
            + $"({coverage.Percentage:0.0} %), {coverage.Count(RuleCoverage.State.Cut)} cut, "
            + $"{coverage.Count(RuleCoverage.State.Outstanding)} outstanding → {path}"
        );

        return ExitCodes.Ok;
    }

    /// <summary>
    ///     <c>skala docs site</c> — docs/plan/15 § M7, "Documentation site generation from rules.json +
    ///     options.json".
    /// </summary>
    /// <remarks>
    ///     ⚠ Deliberately a sibling of <c>skala rules docs</c> rather than a replacement for it. The two
    ///     render the same catalogue for different readers — <c>docs/rules/*.md</c> is what GitHub shows
    ///     beside the source and what an agent reads through the MCP server; the site is browsable and
    ///     cross-linked and is the only surface where the option registry appears at all. Neither holds
    ///     a word of its own: both are <see cref="RuleCatalog" /> and <see cref="OptionRegistry" />
    ///     rendered (docs/plan/08 § "Documentation").
    /// </remarks>
    static Command CreateDocsCommand() {
        var docs = new Command("docs", "The generated documentation.");

        var directory = new Argument<string>("directory") {
            Description = "Where to write the site.", DefaultValueFactory = static _ => "docs/site"
        };

        var site = new Command(
            "site",
            "Regenerate the static documentation site from rules.json and options.json."
        );

        site.Arguments.Add(directory);
        site.SetAction(parse => {
                var target = parse.GetValue(directory)!;
                var count = DocsSite.Write(target);
                Console.Out.WriteLine($"{count} file(s) written to {target}.");
                return ExitCodes.Ok;
            }
        );

        docs.Subcommands.Add(site);
        return docs;
    }

    /// <summary><c>skala cache clear|stats</c> — docs/plan/07 § "The incremental cache".</summary>
    static Command CreateCacheCommand() {
        var cache = new Command("cache", "The incremental analysis cache.");
        var path = new Argument<string>("path") {
            Description = "Any path inside the repository.", DefaultValueFactory = static _ => "."
        };

        var clear = new Command(
            "clear",
            "Forget everything. ⚠ Never needed for correctness; a bad read already discards."
        );
        clear.Arguments.Add(path);
        clear.SetAction(parse => {
                var root = Root(parse.GetValue(path)!);
                DiagnosticCache.Clear(root);
                Console.Out.WriteLine($"cache cleared: {Path.Combine(root, ".skala", "cache")}");
                return ExitCodes.Ok;
            }
        );

        var stats = new Command("stats", "How much is held, and how large it is.");
        stats.Arguments.Add(path);
        stats.SetAction(parse => {
                var directory = Path.Combine(Root(parse.GetValue(path)!), ".skala", "cache");
                if (!Directory.Exists(directory)) {
                    Console.Out.WriteLine("no cache");
                    return ExitCodes.Ok;
                }

                var files = Directory.GetFiles(directory);
                var bytes = files.Sum(static file => new FileInfo(file).Length);
                Console.Out.WriteLine($"{files.Length} compilation(s), {bytes / 1024} KB, in {directory}");

                return ExitCodes.Ok;
            }
        );

        cache.Subcommands.Add(clear);
        cache.Subcommands.Add(stats);
        return cache;
    }

    /// <summary><c>skala mcp</c> — ADR-014.</summary>
    static Command CreateMcpCommand() {
        var path = new Argument<string>("path") {
            Description = "The repository the server answers about.", DefaultValueFactory = static _ => "."
        };

        var command = new Command("mcp", "Speak the Model Context Protocol over stdio.");
        command.Arguments.Add(path);
        command.SetAction(parse => {
                McpServer.RunAsync(Root(parse.GetValue(path)!), CancellationToken.None).GetAwaiter().GetResult();
                return ExitCodes.Ok;
            }
        );

        return command;
    }

    static ReportFormat ParseFormat(string? value, bool noColor) =>
        value?.ToLowerInvariant() switch {
            "plain" => ReportFormat.Plain,
            "json" => ReportFormat.Json,
            "github" => ReportFormat.Github,
            "agent" => ReportFormat.Agent,
            "markdown" => ReportFormat.Markdown,
            "junit" => ReportFormat.JUnit,
            _ => noColor || Console.IsOutputRedirected ? ReportFormat.Plain : ReportFormat.Terminal
        };

    /// <summary>
    ///     ⚠ Ctrl-C cancels and prints what was found so far, marked partial (docs/plan/07).
    /// </summary>
    /// <remarks>
    ///     Exit 130 is the documented code for it, and hooks depend on the codes being fixed.
    /// </remarks>
    static int RunCancellable(Func<CancellationToken, CommandResult> command) {
        using var cancellation = new CancellationTokenSource();
        ConsoleCancelEventHandler handler = (_, args) => {
            args.Cancel = true;
            cancellation.Cancel();
        };

        Console.CancelKeyPress += handler;
        try {
            var result = command(cancellation.Token);
            Console.Out.Write(result.Output);
            return cancellation.IsCancellationRequested ? ExitCodes.Cancelled : result.ExitCode;
        } catch (OperationCanceledException) {
            return ExitCodes.Cancelled;
        } catch (IOException exception) {
            Console.Error.WriteLine($"skala: {exception.Message}");
            return ExitCodes.InternalError;
        } catch (UnauthorizedAccessException exception) {
            Console.Error.WriteLine($"skala: {exception.Message}");
            return ExitCodes.InternalError;
        } finally {
            Console.CancelKeyPress -= handler;
        }
    }
}
