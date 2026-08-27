using System.CommandLine;
using Rikarin.Skala.Analysis;
using Rikarin.Skala.Analysis.Caching;
using Rikarin.Skala.Analysis.Loading;
using Rikarin.Skala.Core.Configuration;
using Rikarin.Skala.Formatting.CSharp;
using Rikarin.Skala.Mcp;
using Rikarin.Skala.Reporting;
using Rikarin.Skala.Server;

namespace Rikarin.Skala.Cli;

/// <summary>
/// The <c>skala</c> command surface.
/// </summary>
/// <remarks>
/// Argument parsing and rendering only. Every command's behaviour lives in
/// <see cref="ConfigCommands"/> in Core, because the daemon, MSBuild and MCP host the same logic
/// and nothing may reference this assembly (docs/plan/02 § "The project graph").
/// </remarks>
public static partial class SkalaCommandLine {
    public static RootCommand Create() {
        var root = new RootCommand("Skala — one configuration, the same formatting and analysis everywhere.");
        root.Subcommands.Add(CreateFormatCommand());
        root.Subcommands.Add(CreateCheckCommand());
        root.Subcommands.Add(CreateVerifyCommand());
        root.Subcommands.Add(CreateFixCommand());
        root.Subcommands.Add(CreateExplainCommand());
        root.Subcommands.Add(CreateRulesCommand());
        root.Subcommands.Add(CreateBaselineCommand());
        root.Subcommands.Add(CreateReportCommand());
        root.Subcommands.Add(CreateTrendCommand());
        root.Subcommands.Add(CreateCacheCommand());
        root.Subcommands.Add(CreateConfigCommand());
        root.Subcommands.Add(CreateDaemonCommand());
        root.Subcommands.Add(CreateLspCommand());
        root.Subcommands.Add(CreateMcpCommand());
        root.Subcommands.Add(CreateHooksCommand());
        return root;
    }

    /// <summary>
    /// <c>skala format</c> — docs/plan/11 § "Command surface".
    /// </summary>
    static Command CreateFormatCommand() {
        var paths = new Argument<string[]>("paths") {
            Description = "Files, directories or globs. Empty means the repository root.",
            Arity = ArgumentArity.ZeroOrMore
        };

        var check = new Option<bool>("--check") {
            Description = "Report what would change and write nothing. Exit 1 when there is anything."
        };
        var diff = new Option<bool>("--diff") { Description = "Print a unified diff over the edits." };
        var range = new Option<string?>("--range") {
            Description = "a:b — character offsets. Filtered after full-file fitting."
        };
        var staged = new Option<string?>("--staged") {
            Description = "Format the staged files and write back to both the worktree and the index. --staged=worktree formats staged files that also have unstaged changes.",
            Arity = ArgumentArity.ZeroOrOne
        };

        var quiet = new Option<bool>("--quiet") { Description = "Print nothing but diagnostics." };
        var option = new Option<string[]>("--option") {
            Description = "key=value, repeatable. For debugging and for the conformance harness.",
            Arity = ArgumentArity.ZeroOrMore
        };

        var jobs = new Option<int?>("--jobs", "-j") {
            Description = "How many files to format at once. Default min(cores, 10); 1 is sequential."
        };

        var noCache = new Option<bool>("--no-cache") {
            Description = "Re-read and re-resolve every .editorconfig per file instead of memoising it."
        };

        var noDaemon = new Option<bool>("--no-daemon") {
            Description = "Do everything in this process. The daemon is only ever an optimisation."
        };

        // ⚠ SK-DIV-0004. Without symbols Roslyn hands back every `#if DEBUG` body as disabled text
        // and Skala correctly refuses to touch it, so the conditional half of a tree is not
        // formatted at all. `--load` takes the symbols from what the build actually compiled;
        // `--define` is for a repository with no build.
        var define = new Option<string[]>("--define", "-d") {
            Description = "Preprocessor symbols to parse with, repeatable and comma-separated (DEBUG,TRACE).",
            Arity = ArgumentArity.ZeroOrMore
        };

        var load = new Option<string?>("--load") {
            Description = "Take preprocessor symbols from a loaded project: binlog | workspace | loose | none (default none)."
        };

        var command = new Command(
            "format",
            "Format C# files: spaces, blank lines, braces, indentation, breaks and wrapping."
        );
        command.Arguments.Add(paths);
        command.Options.Add(check);
        command.Options.Add(diff);
        command.Options.Add(range);
        command.Options.Add(staged);
        command.Options.Add(quiet);
        command.Options.Add(option);
        command.Options.Add(jobs);
        command.Options.Add(noCache);
        command.Options.Add(noDaemon);
        command.Options.Add(define);
        command.Options.Add(load);

        command.SetAction(parse => {
                var stagedValue = parse.GetResult(staged) is null
                    ? StagedMode.Off
                    : string.Equals(parse.GetValue(staged), "worktree", StringComparison.Ordinal)
                        ? StagedMode.Worktree
                        : StagedMode.Strict;

                if (parse.GetValue(noCache)) {
                    ConfigurationCache.Enabled = false;
                }

                var symbols = ParseDefines(parse.GetValue(define));
                if (parse.GetValue(load) is { Length: > 0 } loadMode
                    && !string.Equals(loadMode, "none", StringComparison.OrdinalIgnoreCase)) {
                    symbols = [.. symbols, .. SymbolsFromProject(parse.GetValue(paths) ?? [], loadMode)];
                }

                var request = new FormatRequest {
                    Paths = parse.GetValue(paths) ?? [],
                    Define = symbols,
                    Check = parse.GetValue(check),
                    Diff = parse.GetValue(diff),
                    Range = parse.GetValue(range),
                    Staged = stagedValue,
                    Quiet = parse.GetValue(quiet),
                    Overrides = ParseOverrides(parse.GetValue(option)),
                    Jobs = parse.GetValue(jobs)
                };

                // ⚠ The daemon is tried first and its failure is never an error. docs/plan/11's
                // correctness rule is that every command works identically with SKALA_NO_DAEMON=1, so a
                // daemon that is absent, stale or of another version has to fall through silently to the
                // same code the daemon itself would have run.
                if (!parse.GetValue(noDaemon) && DaemonUse.TryFormat(request) is { } served) {
                    return Run(() => served);
                }

                return Run(() => FormatCommand.Run(request));
            }
        );

        return command;
    }

    /// <summary>
    /// <c>skala daemon status|stop|run</c> — docs/plan/11 § "The daemon".
    /// </summary>
    /// <remarks>
    /// ⚠ There is no `start`. The daemon is started lazily by whatever needs it and exits after
    /// thirty minutes idle; a `start` verb invites a person to run one by hand and then wonder why
    /// their editor is using a different one. `run` is the foreground form, for a supervisor and for
    /// the tests.
    /// </remarks>
    static Command CreateDaemonCommand() {
        var daemon = new Command("daemon", "The per-repository format daemon.");
        var path = new Argument<string>("path") {
            Description = "Any path inside the repository.", DefaultValueFactory = static _ => "."
        };

        var status = new Command("status", "Whether a daemon is running, and what it is holding.");
        status.Arguments.Add(path);
        status.SetAction(parse => Run(() => DaemonCommands.Status(Root(parse.GetValue(path)!))));

        var stop = new Command("stop", "Ask the daemon to exit.");
        stop.Arguments.Add(path);
        stop.SetAction(parse => Run(() => DaemonCommands.Stop(Root(parse.GetValue(path)!))));

        var run = new Command("run", "Run the daemon in the foreground.");
        run.Arguments.Add(path);
        run.SetAction(parse => DaemonCommands.RunAsync(Root(parse.GetValue(path)!), CancellationToken.None)
            .GetAwaiter()
            .GetResult()
        );

        daemon.Subcommands.Add(status);
        daemon.Subcommands.Add(stop);
        daemon.Subcommands.Add(run);
        return daemon;
    }

    /// <summary><c>skala lsp</c> — stdio, four capabilities (docs/plan/11 § "LSP").</summary>
    static Command CreateLspCommand() {
        var command = new Command("lsp", "Speak the Language Server Protocol over stdio.");
        command.SetAction(_ => {
                var server = new LanguageServer(Console.In, Console.Out);
                server.RunAsync(CancellationToken.None).GetAwaiter().GetResult();
                return 0;
            }
        );

        return command;
    }

    /// <summary><c>skala hooks install</c> — docs/plan/11 § "Git hooks".</summary>
    static Command CreateHooksCommand() {
        var hooks = new Command("hooks", "Install the pre-commit hook, or say what it would do.");
        var path = new Argument<string>("path") {
            Description = "Any path inside the repository.", DefaultValueFactory = static _ => "."
        };

        var apply = new Option<bool>("--apply") {
            Description = "Write the hook. Without it, install only says what it would do."
        };

        var install = new Command("install", "Write .git/hooks/pre-commit, unless a hook manager owns it.");
        install.Arguments.Add(path);
        install.Options.Add(apply);
        install.SetAction(parse => Run(() => DaemonCommands.InstallHooks(
                    Root(parse.GetValue(path)!),
                    parse.GetValue(apply)
                )
            )
        );

        hooks.Subcommands.Add(install);
        return hooks;
    }

    static string Root(string path) => FindRepositoryRoot(path) ?? Path.GetFullPath(path);

    /// <summary>
    /// <c>--define A --define B,C</c> — both spellings, because both are what people type.
    /// </summary>
    static List<string> ParseDefines(string[]? values) {
        var result = new List<string>();
        foreach (var value in values ?? []) {
            foreach (var part in value.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries)) {
                var trimmed = part.Trim();
                if (trimmed.Length > 0 && !result.Contains(trimmed, StringComparer.Ordinal)) {
                    result.Add(trimmed);
                }
            }
        }

        return result;
    }

    /// <summary>
    /// The preprocessor symbols of whatever compilation covers the first path.
    /// </summary>
    /// <remarks>
    /// ⚠ The union across every compilation that names the file, not the intersection. A file
    /// compiled for two target frameworks is formatted once, and the branch that is disabled under
    /// one target is still code someone maintains; formatting it under the union means every branch
    /// that any target compiles is laid out, and the ones nobody compiles stay verbatim. Taking the
    /// intersection would mean a multi-targeted repository formats nothing conditional at all.
    /// </remarks>
    static List<string> SymbolsFromProject(string[] paths, string mode) {
        try {
            var root = FindRepositoryRoot(paths.Length > 0 ? paths[0] : ".") ?? Directory.GetCurrentDirectory();
            var loaded = ProjectLoader.Load(new LoadRequest { RepositoryRoot = root, Mode = LoadModes.Parse(mode) });
            var symbols = new List<string>();
            foreach (var unit in loaded.Units) {
                foreach (var symbol in unit.PreprocessorSymbols) {
                    if (!symbols.Contains(symbol, StringComparer.Ordinal)) {
                        symbols.Add(symbol);
                    }
                }
            }

            return symbols;
        } catch (IOException) {
            return [];
        }
    }

    static List<KeyValuePair<string, string>> ParseOverrides(string[]? values) {
        if (values is null || values.Length == 0) {
            return [];
        }

        var result = new List<KeyValuePair<string, string>>(values.Length);
        foreach (var value in values) {
            var equals = value.IndexOf('=', StringComparison.Ordinal);
            if (equals > 0) {
                result.Add(new KeyValuePair<string, string>(value[..equals].Trim(), value[(equals + 1)..].Trim()));
            }
        }

        return result;
    }

    static Command CreateConfigCommand() {
        var config = new Command("config", "Inspect, check and reshape the .editorconfig Skala reads.");
        config.Subcommands.Add(CreateExplain());
        config.Subcommands.Add(CreateCheck());
        config.Subcommands.Add(CreateDiff());
        config.Subcommands.Add(CreateDistill());
        config.Subcommands.Add(CreateFix());
        return config;
    }

    static Command CreateExplain() {
        var path = new Argument<string>("path") {
            Description = "The file whose effective options to print.", DefaultValueFactory = static _ => "."
        };
        var repositoryRoot = new Option<string?>("--repository-root") {
            Description = "Where the repository starts, for SK9002."
        };
        var configuredOnly = new Option<bool>("--configured-only") {
            Description = "Only options the configuration actually sets."
        };
        var configPath = new Option<string?>("--config") {
            Description = "Resolve against this .editorconfig instead of the chain above the file."
        };

        var command = new Command(
            "explain",
            "The effective option set for a file, each with its source file:line and tier."
        );
        command.Arguments.Add(path);
        command.Options.Add(repositoryRoot);
        command.Options.Add(configuredOnly);
        command.Options.Add(configPath);
        command.SetAction(parse => Run(() => ConfigCommands.Explain(
                    parse.GetValue(path)!,
                    parse.GetValue(repositoryRoot) ?? FindRepositoryRoot(parse.GetValue(path)!),
                    parse.GetValue(configuredOnly),
                    parse.GetValue(configPath)
                )
            )
        );

        return command;
    }

    static Command CreateCheck() {
        var path = new Argument<string>("path") {
            Description = "The repository root.", DefaultValueFactory = static _ => "."
        };
        var strict = new Option<bool>("--strict") { Description = "Exit non-zero when there is any warning." };

        var command = new Command("check", "The tier report, the contradictions, and what the export is missing.");
        command.Arguments.Add(path);
        command.Options.Add(strict);
        command.SetAction(parse => Run(() => ConfigCommands.Check(parse.GetValue(path)!, parse.GetValue(strict))));
        return command;
    }

    static Command CreateDiff() {
        var left = new Argument<string>("a") { Description = "The .editorconfig to compare from." };
        var right = new Argument<string>("b") { Description = "The .editorconfig to compare to." };

        var command = new Command("diff", "What changes between two .editorconfig files, semantically.");
        command.Arguments.Add(left);
        command.Arguments.Add(right);
        command.SetAction(parse => Run(() => ConfigCommands.Diff(parse.GetValue(left)!, parse.GetValue(right)!)));
        return command;
    }

    static Command CreateDistill() {
        var path = new Argument<string>("path") {
            Description = "The .editorconfig to distill.", DefaultValueFactory = static _ => ".editorconfig"
        };
        var output = new Option<string?>("--out", "-o") { Description = "Write the result here instead of to stdout." };

        var command = new Command(
            "distill",
            "Write back the subset of an export that differs from ReSharper's defaults."
        );
        command.Arguments.Add(path);
        command.Options.Add(output);
        command.SetAction(parse => Run(() => ConfigCommands.Distill(parse.GetValue(path)!, parse.GetValue(output))));
        return command;
    }

    static Command CreateFix() {
        var path = new Argument<string>("path") {
            Description = "The .editorconfig to repair.", DefaultValueFactory = static _ => ".editorconfig"
        };
        var apply = new Option<bool>("--apply") {
            Description = "Write the file. Without it, fix only says what it would do."
        };
        var contradictions = new Option<bool>("--resolve-contradictions") {
            Description = "Also make a losing key agree with the one that already wins."
        };

        var command = new Command(
            "fix",
            "Add `root = true` and `max_line_length`, and optionally resolve contradictions."
        );
        command.Arguments.Add(path);
        command.Options.Add(apply);
        command.Options.Add(contradictions);
        command.SetAction(parse => Run(() => ConfigCommands.Fix(
                    parse.GetValue(path)!,
                    parse.GetValue(apply),
                    parse.GetValue(contradictions)
                )
            )
        );
        return command;
    }

    static int Run(Func<CommandResult> command) {
        try {
            var result = command();
            Console.Out.Write(result.Output);
            return result.ExitCode;
        } catch (IOException exception) {
            Console.Error.WriteLine($"skala: {exception.Message}");
            return 2;
        } catch (UnauthorizedAccessException exception) {
            Console.Error.WriteLine($"skala: {exception.Message}");
            return 2;
        }
    }

    /// <summary>The nearest directory above <paramref name="path"/> that looks like a repository.</summary>
    public static string? FindRepositoryRoot(string path) {
        var full = Path.GetFullPath(path);
        var directory = Directory.Exists(full) ? full : Path.GetDirectoryName(full);
        while (directory is not null) {
            if (Directory.Exists(Path.Combine(directory, ".git"))) {
                return directory;
            }

            var parent = Path.GetDirectoryName(directory);
            directory = string.Equals(parent, directory, StringComparison.Ordinal) ? null : parent;
        }

        return null;
    }
}
