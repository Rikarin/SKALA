using System.CommandLine;
using Rikarin.Skala.Analysis;
using Rikarin.Skala.Analysis.Caching;
using Rikarin.Skala.Analysis.Loading;
using Rikarin.Skala.Core.Configuration;
using Rikarin.Skala.Core.Diagnostics;
using Rikarin.Skala.Formatting.CSharp;
using Rikarin.Skala.Formatting.CSharp.Arrangement;
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
    /// <summary>
    /// ⚠ Global, recursive, and it exists.
    /// </summary>
    /// <remarks>
    /// docs/plan/04 § "What it does not do" says generated files are "reported as skipped in
    /// <c>--verbose</c>", and until M9 there was no such option: <c>skala check --load loose
    /// --verbose</c> bound <c>--verbose</c> to the variadic <c>&lt;paths&gt;</c> argument, looked
    /// for C# files in a directory of that name, found none and exited 4. The flag being missing was
    /// bad; the flag being silently eaten was the defect.
    /// <para>
    /// It is declared once on the root and marked recursive, so every subcommand accepts it and no
    /// subcommand can spell it differently. <c>format</c>, <c>arrange</c> and <c>check</c> act on
    /// it — each has something it currently swallows — and the rest accept it without effect rather
    /// than rejecting it, which is what "global" has to mean for a flag a script puts in a variable.
    /// </para>
    /// </remarks>
    public static Option<bool> Verbose { get; } = new("--verbose") {
        Description = "Report what was skipped and why: generated files, unparseable files, rules that did not run.",
        Recursive = true
    };

    public static RootCommand Create() {
        var root = new RootCommand("Skala — one configuration, the same formatting and analysis everywhere.");
        root.Options.Add(Verbose);
        root.Subcommands.Add(CreateFormatCommand());
        root.Subcommands.Add(CreateArrangeCommand());
        root.Subcommands.Add(CreateCheckCommand());
        root.Subcommands.Add(CreateVerifyCommand());
        root.Subcommands.Add(CreateFixCommand());
        root.Subcommands.Add(CreateExplainCommand());
        root.Subcommands.Add(CreateRulesCommand());
        root.Subcommands.Add(CreateDocsCommand());
        root.Subcommands.Add(CreateBaselineCommand());
        root.Subcommands.Add(CreateReportCommand());
        root.Subcommands.Add(CreateTrendCommand());
        root.Subcommands.Add(CreateCacheCommand());
        root.Subcommands.Add(CreateConfigCommand());
        root.Subcommands.Add(CreateDaemonCommand());
        root.Subcommands.Add(CreateLspCommand());
        root.Subcommands.Add(CreateMcpCommand());
        root.Subcommands.Add(CreateHooksCommand());
        RejectOptionLikeTokens(root);
        return root;
    }

    /// <summary>
    /// ⚠ A positional token that begins with <c>-</c> is a mistyped option, not a path.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>skala check --load loose --verbose</c> used to report <c>SK9023: no C# files were found</c>
    /// and exit 4 from a repository full of C# files. <c>&lt;paths&gt;</c> is variadic, so
    /// System.CommandLine handed it every token it could not match as an option — and a variadic
    /// argument matches anything. Every typo'd or unimplemented flag on every command with a
    /// variadic argument behaved the same way, and the failure was indistinguishable from an empty
    /// directory, which is why it survived: the tool answered a question nobody had asked and
    /// sounded confident doing it.
    /// </para>
    /// <para>
    /// This is docs/plan/00's non-negotiable 4 — unknown configuration is a diagnostic, never a
    /// silent default — applied to argv. It is installed by walking the finished command tree
    /// rather than at each <c>new Argument&lt;T&gt;</c> site, so an argument added later is covered
    /// without anybody remembering to do it. That is the whole reason it lives here.
    /// </para>
    /// <para>
    /// ⚠ A path that genuinely starts with <c>-</c> is spelled <c>./-weird.cs</c>, which is also
    /// what every other POSIX tool requires. A lone <c>-</c> is left alone: it is a stdin
    /// convention, not an option.
    /// </para>
    /// </remarks>
    static void RejectOptionLikeTokens(Command command) {
        foreach (var argument in command.Arguments) {
            argument.Validators.Add(static result => {
                foreach (var token in result.Tokens) {
                    if (!LooksLikeAnOption(token.Value)) {
                        continue;
                    }

                    result.AddError(
                        $"Unrecognized option '{token.Value}'. "
                        + "Run with --help for the options this command accepts. "
                        + $"If you meant a file or directory called '{token.Value}', write './{token.Value}'."
                    );
                }
            });

            // ⚠ And a path that does not exist is named, rather than analysed as nothing.
            //
            // `skala format --check no-such-dir` exited **0** — "0 files would be reformatted, 0
            // left alone" — because a directory that is not there contributes no files and no files
            // is indistinguishable from no findings. `skala check no-such-dir` was only slightly
            // better: exit 4, SK9023, naming the repository root instead of the path it was given.
            // A gate that passes because a CI script has a typo in a path is quiet in exactly the
            // case it exists for, and the quiet reads as approval (docs/plan/09 § "New-code
            // definition" makes the same argument about untracked files).
            if (argument.Name == "paths") {
                argument.Validators.Add(static result => {
                    foreach (var token in result.Tokens) {
                        if (LooksLikeAnOption(token.Value) || IsGlob(token.Value)) {
                            continue;
                        }

                        if (!File.Exists(token.Value) && !Directory.Exists(token.Value)) {
                            result.AddError($"'{token.Value}' does not exist.");
                        }
                    }
                });
            }
        }

        foreach (var subcommand in command.Subcommands) {
            RejectOptionLikeTokens(subcommand);
        }
    }

    /// <summary>
    /// A token carrying glob metacharacters, which is matched later rather than opened now.
    /// </summary>
    /// <remarks>
    /// ⚠ `paths` is documented as "files, directories or globs", so the existence check has to skip
    /// the third kind. A glob that matches nothing is a different question from a path that is not
    /// there, and it is <see cref="FormatCommand.Collect"/>'s to answer.
    /// </remarks>
    static bool IsGlob(string token) => token.AsSpan().IndexOfAny('*', '?', '[') >= 0;

    /// <summary>A token that begins with <c>-</c> and is longer than a bare <c>-</c>.</summary>
    /// <remarks>
    /// ⚠ A negative number is not excluded, because no Skala argument takes one. If one is ever
    /// added, the exemption belongs on that argument rather than on this predicate.
    /// </remarks>
    static bool LooksLikeAnOption(string token) => token.Length > 1 && token[0] == '-';

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
            Description =
                "Format the staged files and write back to both the worktree and the index. --staged=worktree formats staged files that also have unstaged changes.",
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
            Description =
                "Take preprocessor symbols from a loaded project: binlog | workspace | loose | none (default none)."
        };

        // ⚠ docs/plan/06: "The default for `skala format` is **whitespace only**, because it must
        // work with no project, in under a second, on a file an agent just wrote." `--arrange` opts
        // in; `--arrange=syntactic` is the subset that needs no compilation and is what an agent
        // gets for free on a loose file.
        var arrange = new Option<string?>("--arrange") {
            Description = "Also rewrite the tree: syntactic (no project needed) | full. Default off.",
            Arity = ArgumentArity.ZeroOrOne
        };

        var command = new Command(
            "format",
            "Format C# files: spaces, blank lines, braces, indentation, breaks and wrapping."
        );
        command.Arguments.Add(paths);
        command.Options.Add(arrange);
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

                // `--arrange` with no value means syntactic, which is the mode that always works.
                if (parse.GetResult(arrange) is not null) {
                    var full = string.Equals(parse.GetValue(arrange), "full", StringComparison.OrdinalIgnoreCase);
                    var arrangeRequest = new ArrangeRequest {
                        Paths = parse.GetValue(paths) ?? [],
                        Check = parse.GetValue(check),
                        Diff = parse.GetValue(diff),
                        Quiet = parse.GetValue(quiet),
                        Range = parse.GetValue(range),
                        Overrides = ParseOverrides(parse.GetValue(option)),
                        Define = symbols,
                        Compilations = full ? files => CompilationsFor(files, parse.GetValue(load) ?? "loose") : null
                    };

                    return Run(() => ArrangeCommand.Run(arrangeRequest));
                }

                var request = new FormatRequest {
                    Paths = parse.GetValue(paths) ?? [],
                    Define = symbols,
                    Check = parse.GetValue(check),
                    Diff = parse.GetValue(diff),
                    Range = parse.GetValue(range),
                    Staged = stagedValue,
                    Quiet = parse.GetValue(quiet),
                    Verbose = parse.GetValue(Verbose),
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
    /// <c>skala arrange</c> — docs/plan/06, docs/plan/11 § "Command surface".
    /// </summary>
    /// <remarks>
    /// ⚠ A separate verb from <c>format</c>, and deliberately. <c>format</c> changes whitespace,
    /// needs no project, runs in under a second on a file an agent just wrote, and is reversible by
    /// reformatting. <c>arrange</c> changes the tree, wants a <c>Compilation</c>, is minutes-scale on
    /// a large tree, and is reversible by <c>git revert</c>. Making the second the default for the
    /// first would put a tree rewrite inside every save.
    /// </remarks>
    static Command CreateArrangeCommand() {
        var paths = new Argument<string[]>("paths") {
            Description = "Files, directories or globs. Empty means the repository root.",
            Arity = ArgumentArity.ZeroOrMore
        };

        var check = new Option<bool>("--check") {
            Description = "Report what would change and write nothing. Exit 1 when there is anything."
        };
        var diff = new Option<bool>("--diff") { Description = "Print a unified diff over the edits." };
        var quiet = new Option<bool>("--quiet") { Description = "Print nothing but diagnostics." };
        var range = new Option<string?>("--range") { Description = "a:b — character offsets." };

        // ⚠ docs/plan/06 § "Qualification and redundancy": parenthesis removal is the highest-risk
        // rewrite in the tool, the oracle's own cleanup profile performs it, and Skala gates it for
        // the first release regardless. The cost of the gate is measured, not assumed — see the M4
        // numbers in docs/plan/15.
        var aggressive = new Option<bool>("--aggressive") {
            Description = "Also remove redundant parentheses. Off by default; the export asks for it and Skala does not."
        };

        var include = new Option<string[]>("--include") {
            Description = "Only these rule ids (SK2001…). Repeatable.", Arity = ArgumentArity.ZeroOrMore
        };
        var exclude = new Option<string[]>("--exclude") {
            Description = "Every rule but these. Repeatable.", Arity = ArgumentArity.ZeroOrMore
        };

        var option = new Option<string[]>("--option") {
            Description = "key=value, repeatable.", Arity = ArgumentArity.ZeroOrMore
        };

        var define = new Option<string[]>("--define", "-d") {
            Description = "Preprocessor symbols to parse with, repeatable and comma-separated.",
            Arity = ArgumentArity.ZeroOrMore
        };

        var load = new Option<string?>("--load") {
            Description = "How to find the compilation: binlog | workspace | loose | none (default loose)."
        };

        var command = new Command(
            "arrange",
            "Rewrite the tree: body styles, var, target-typed new, qualifiers, usings. Needs a project for the semantic half."
        );

        command.Arguments.Add(paths);
        command.Options.Add(check);
        command.Options.Add(diff);
        command.Options.Add(quiet);
        command.Options.Add(range);
        command.Options.Add(aggressive);
        command.Options.Add(include);
        command.Options.Add(exclude);
        command.Options.Add(option);
        command.Options.Add(define);
        command.Options.Add(load);

        command.SetAction(parse => {
                var mode = parse.GetValue(load) ?? "loose";
                var request = new ArrangeRequest {
                    Paths = parse.GetValue(paths) ?? [],
                    Check = parse.GetValue(check),
                    Diff = parse.GetValue(diff),
                    Quiet = parse.GetValue(quiet),
                    Range = parse.GetValue(range),
                    Aggressive = parse.GetValue(aggressive),
                    Include = parse.GetValue(include) ?? [],
                    Exclude = parse.GetValue(exclude) ?? [],
                    Overrides = ParseOverrides(parse.GetValue(option)),
                    Define = ParseDefines(parse.GetValue(define)),
                    Compilations = string.Equals(mode, "none", StringComparison.OrdinalIgnoreCase)
                        ? null
                        : files => CompilationsFor(files, mode)
                };

                return Run(() => ArrangeCommand.Run(request));
            }
        );

        return command;
    }

    /// <summary>
    /// Every loaded compilation, so that <c>arrange</c> can intersect its using removal across them.
    /// </summary>
    /// <remarks>
    /// ⚠ All of them, not the one that covers the first path. docs/plan/06: "Skala removes a using
    /// only when it is unused in *every* compilation the file participates in — multi-targeting is
    /// not an edge case in this ecosystem." Handing back one compilation would make a multi-targeted
    /// repository lose the usings only one of its targets needs.
    /// </remarks>
    static IReadOnlyList<Microsoft.CodeAnalysis.CSharp.CSharpCompilation> CompilationsFor(
        IReadOnlyList<string> files,
        string mode
    ) {
        try {
            var root = FindRepositoryRoot(files.Count > 0 ? files[0] : ".") ?? Directory.GetCurrentDirectory();
            var loaded = ProjectLoader.Load(new LoadRequest { RepositoryRoot = root, Mode = LoadModes.Parse(mode) });
            return [.. loaded.Units.Select(static unit => unit.Compilation)];
        } catch (IOException) {
            return [];
        }
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
        config.Subcommands.Add(CreateSync());
        config.Subcommands.Add(CreateCanonical());
        return config;
    }

    static Command CreateSync() {
        var path = new Argument<string>("path") {
            Description = "The repository root.", DefaultValueFactory = static _ => "."
        };
        var apply = new Option<bool>("--apply") {
            Description = "Write the file. Without it, sync only says what it would do."
        };

        var command = new Command(
            "sync",
            "Write the canonical block into .editorconfig, preserving the local block below it."
        );
        command.Arguments.Add(path);
        command.Options.Add(apply);
        command.SetAction(parse => Run(() => ConfigCommands.Sync(parse.GetValue(path)!, parse.GetValue(apply))));
        return command;
    }

    static Command CreateCanonical() {
        var template = new Argument<string>("template") {
            Description = "The Rider export to compose from.",
            DefaultValueFactory = static _ => "editor_config_template"
        };
        var output = new Option<string>("--out", "-o") {
            Description = "The distribution directory to write into.", Required = true
        };
        var version = new Option<string>("--version") {
            Description = "The version to stamp into the manifest.", DefaultValueFactory = static _ => "0.1.0"
        };

        var command = new Command(
            "canonical",
            "Regenerate the distributable canonical payload from a Rider export. Maintainer command; `./build.sh Canonical` runs it."
        );
        command.Arguments.Add(template);
        command.Options.Add(output);
        command.Options.Add(version);
        command.SetAction(parse => Run(() => ConfigCommands.BuildCanonical(
                    parse.GetValue(template)!,
                    parse.GetValue(output)!,
                    parse.GetValue(version)!
                )
            )
        );

        return command;
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
        var left = new Argument<string?>("a") {
            Description = "The .editorconfig to compare from.", Arity = ArgumentArity.ZeroOrOne
        };
        var right = new Argument<string?>("b") {
            Description = "The .editorconfig to compare to.", Arity = ArgumentArity.ZeroOrOne
        };
        var canonical = new Option<bool>("--canonical") {
            Description =
                "Compare the repository's managed block against the canonical instead. Exits 3 on drift; this is the gate condition."
        };
        var options = new Option<bool>("--options") {
            Description = "With --canonical, also price the upgrade option by option."
        };

        var command = new Command(
            "diff",
            "What changes between two .editorconfig files, semantically — or between this repository and the canonical."
        );
        command.Arguments.Add(left);
        command.Arguments.Add(right);
        command.Options.Add(canonical);
        command.Options.Add(options);
        command.SetAction(parse => Run(() => {
                    if (parse.GetValue(canonical)) {
                        return ConfigCommands.DiffCanonical(parse.GetValue(left) ?? ".", parse.GetValue(options));
                    }

                    var a = parse.GetValue(left);
                    var b = parse.GetValue(right);
                    // ⚠ 3, not 2. Being invoked with the wrong arguments is a configuration error;
                    // 2 is "formatting changes are needed", and `config diff` never formats
                    // anything.
                    return a is null || b is null
                        ? new CommandResult(
                            ExitCodes.ConfigurationError,
                            "skala: `config diff` needs two files, or --canonical and a repository path."
                            + Environment.NewLine
                        )
                        : ConfigCommands.Diff(a, b);
                }
            )
        );

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

    /// <summary>
    /// The nearest directory above <paramref name="path"/> that looks like a repository.
    /// </summary>
    /// <remarks>
    /// ⚠ Delegates rather than duplicating. This was a second copy of
    /// <see cref="FormatCommand.FindRepositoryRoot"/> that had drifted: it tested only
    /// <c>Directory.Exists(".git")</c>, so in a git <b>worktree</b> or a <b>submodule</b> — where
    /// <c>.git</c> is a file containing <c>gitdir: …</c> and not a directory — it walked past the
    /// root, returned null, and every path the daemon commands printed came out absolute. Two
    /// implementations of "where is the repository" is one more than the number that can be right.
    /// </remarks>
    public static string? FindRepositoryRoot(string path) => FormatCommand.FindRepositoryRoot(path);
}
