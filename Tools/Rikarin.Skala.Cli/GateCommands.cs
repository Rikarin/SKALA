using System.CommandLine;
using Rikarin.Skala.Analysis;
using Rikarin.Skala.Analysis.Loading;
using Rikarin.Skala.Core.Diagnostics;
using Rikarin.Skala.Reporting;

namespace Rikarin.Skala.Cli;

/// <summary>
///     The lifecycle half of the command surface: <c>baseline</c>, <c>report</c>, <c>trend</c>.
/// </summary>
/// <remarks>
///     docs/plan/09. ⚠ SonarQube's genuinely valuable part is not its rules — it is the lifecycle: a
///     baseline, a new-code definition, a gate that fails a build, and a report a human reads in thirty
///     seconds. These three commands and <c>check --since/--gate</c> are that lifecycle, without a
///     server.
/// </remarks>
public static partial class SkalaCommandLine {
    /// <summary>
    ///     <c>skala baseline create | update | prune | show</c>.
    /// </summary>
    /// <remarks>
    ///     ⚠ Every writing verb needs <c>--apply</c>. The baseline is a committed artefact whose diff
    ///     is the review conversation, so writing it is never something a command does because it was
    ///     run — it is something a person asked for. The same rule <c>config fix</c> and
    ///     <c>hooks install</c> already follow.
    /// </remarks>
    static Command CreateBaselineCommand() {
        var baseline = new Command(
            "baseline",
            "The findings the repository has accepted for now: .skala/baseline.sarif."
        );

        foreach (var (name, verb, description) in new[] {
                     ("create", BaselineCommand.Verb.Create,
                         "Accept everything that fires now, replacing any existing baseline."),
                     ("update", BaselineCommand.Verb.Update,
                         "Accept what fires now in addition to what is already accepted. Never removes."),
                     ("prune", BaselineCommand.Verb.Prune,
                         "⚠ Remove accepted entries that no longer fire. Deliberately separate from `update`."),
                     ("show", BaselineCommand.Verb.Show, "What the baseline holds, and how a fresh run compares to it.")
                 }) {
            baseline.Subcommands.Add(CreateBaselineVerb(name, verb, description));
        }

        return baseline;
    }

    static Command CreateBaselineVerb(string name, BaselineCommand.Verb verb, string description) {
        var paths = new Argument<string[]>("paths") {
            Description = "Files or directories to analyse. Empty means the repository root.",
            Arity = ArgumentArity.ZeroOrMore
        };

        var load = new Option<string>("--load") {
            Description = "binlog | workspace | loose.", DefaultValueFactory = static _ => "binlog"
        };

        var binlog = new Option<string?>("--binlog") { Description = "The binary log to read." };
        var file = new Option<string?>("--file") { Description = "The baseline path. Default .skala/baseline.sarif." };

        var apply = new Option<bool>("--apply") {
            Description = "Write the file. Without it, the command only says what it would do."
        };

        var define = new Option<string[]>("--define", "-d") {
            Description = "Preprocessor symbols, for --load=loose.", Arity = ArgumentArity.ZeroOrMore
        };

        var noFormatting = new Option<bool>("--no-formatting") { Description = "Leave SK0001 out of the baseline." };

        // ⚠ A baseline has to be creatable over the same rule set the gate evaluates. Without this,
        // a `ci` gate run with --duplication compares 308 SK7020 findings against a baseline that
        // was never offered them and reports every one as new — the gate failing because the two
        // commands disagreed about which rules exist, not because anything regressed.
        var duplication = new Option<bool>("--duplication") {
            Description = "Include SK7020 clone findings, matching `check --duplication`."
        };

        var command = new Command(name, description);
        command.Arguments.Add(paths);
        foreach (var option in new Option[] { load, binlog, file, apply, define, noFormatting, duplication }) {
            command.Options.Add(option);
        }

        command.SetAction(parse => {
                if (!LoadModes.TryParse(parse.GetValue(load), out var mode)) {
                    Console.Error.WriteLine("skala baseline: --load must be binlog, workspace or loose.");
                    return ExitCodes.ConfigurationError;
                }

                var request = new CheckRequest {
                    Paths = parse.GetValue(paths) ?? [],
                    Mode = mode,
                    BinlogPath = parse.GetValue(binlog),
                    BaselinePath = parse.GetValue(file),
                    Define = ParseDefines(parse.GetValue(define)),
                    IncludeFormatting = !parse.GetValue(noFormatting),
                    IncludeArrangement = true,

                    // ⚠ Hints are in the baseline even though they are hidden in the report. A rule
                    // later promoted from `hint` to `warning` must not turn a thousand accepted
                    // findings new on the day of the promotion.
                    IncludeHints = true,
                    IncludeDuplication = parse.GetValue(duplication)
                };

                return RunCancellable(token => BaselineCommand.Run(verb, request, parse.GetValue(apply), token).Result);
            }
        );

        return command;
    }

    /// <summary>
    ///     <c>skala report</c> — re-render a stored SARIF, running nothing.
    /// </summary>
    /// <remarks>
    ///     ⚠ doc 09: "which is what CI uses to produce a PR comment from an artifact". The job that
    ///     comments is not the job that analysed, and making it re-analyse would have it analyse a
    ///     different tree.
    /// </remarks>
    static Command CreateReportCommand() {
        var file = new Argument<string>("sarif") {
            Description = "The SARIF to render.", DefaultValueFactory = static _ => ".skala/report.sarif"
        };

        var format = new Option<string>("--format") {
            Description = "terminal | plain | json | github | agent | markdown | junit.",
            DefaultValueFactory = static _ => "terminal"
        };

        var noColor = new Option<bool>("--no-color") { Description = "Plain output, for a pipe or a CI log." };
        var includeHints = new Option<bool>("--include-hints") { Description = "Show hint-level findings too." };
        var summary = new Option<bool>("--summary") {
            Description = "Print only the totals, the metrics and the gate."
        };

        var command = new Command("report", "Re-render a stored SARIF. Runs no analysis.");
        command.Arguments.Add(file);
        foreach (var option in new Option[] { format, noColor, includeHints, summary }) {
            command.Options.Add(option);
        }

        command.SetAction(parse => {
                var path = Path.GetFullPath(parse.GetValue(file)!);
                return Run(() => ReportCommand.Run(
                        path,
                        FindRepositoryRoot(path) ?? Directory.GetCurrentDirectory(),
                        ParseFormat(parse.GetValue(format), parse.GetValue(noColor)),
                        parse.GetValue(includeHints),
                        parse.GetValue(summary)
                    )
                );
            }
        );

        return command;
    }

    /// <summary><c>skala trend</c> — <c>.skala/history.jsonl</c>, rendered.</summary>
    static Command CreateTrendCommand() {
        var path = new Argument<string>("path") {
            Description = "Any path inside the repository.", DefaultValueFactory = static _ => "."
        };

        var limit = new Option<int>("--limit", "-n") {
            Description = "How many of the most recent runs to show.",
            DefaultValueFactory = static _ => TrendCommand.DefaultLimit
        };

        var command = new Command("trend", "The recorded history: findings, duplication and the gate over time.");
        command.Arguments.Add(path);
        command.Options.Add(limit);
        command.SetAction(parse => Run(() => TrendCommand.Run(Root(parse.GetValue(path)!), parse.GetValue(limit))));
        return command;
    }
}
