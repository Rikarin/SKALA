using System.CommandLine;
using Rikarin.Skala.Core.Configuration;

namespace Rikarin.Skala.Cli;

/// <summary>
/// The <c>skala</c> command surface.
/// </summary>
/// <remarks>
/// Argument parsing and rendering only. Every command's behaviour lives in
/// <see cref="ConfigCommands"/> in Core, because the daemon, MSBuild and MCP host the same logic
/// and nothing may reference this assembly (docs/plan/02 § "The project graph").
/// </remarks>
public static class SkalaCommandLine {
    public static RootCommand Create() {
        var root = new RootCommand("Skala — one configuration, the same formatting and analysis everywhere.");
        root.Subcommands.Add(CreateConfigCommand());
        return root;
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
        var path = new Argument<string>("path") { Description = "The repository root.", DefaultValueFactory = static _ => "." };
        var apply = new Option<bool>("--apply") { Description = "Write the file. Without it, sync only says what it would do." };

        var command = new Command("sync", "Write the canonical block into .editorconfig, preserving the local block below it.");
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
        var output = new Option<string>("--out", "-o") { Description = "The distribution directory to write into.", Required = true };
        var version = new Option<string>("--version") { Description = "The version to stamp into the manifest.", DefaultValueFactory = static _ => "0.1.0" };

        var command = new Command(
            "canonical",
            "Regenerate the distributable canonical payload from a Rider export. Maintainer command; `./build.sh Canonical` runs it.");
        command.Arguments.Add(template);
        command.Options.Add(output);
        command.Options.Add(version);
        command.SetAction(parse => Run(() => ConfigCommands.BuildCanonical(
            parse.GetValue(template)!,
            parse.GetValue(output)!,
            parse.GetValue(version)!)));

        return command;
    }

    static Command CreateExplain() {
        var path = new Argument<string>("path") { Description = "The file whose effective options to print.", DefaultValueFactory = static _ => "." };
        var repositoryRoot = new Option<string?>("--repository-root") { Description = "Where the repository starts, for SK9002." };
        var configuredOnly = new Option<bool>("--configured-only") { Description = "Only options the configuration actually sets." };
        var configPath = new Option<string?>("--config") { Description = "Resolve against this .editorconfig instead of the chain above the file." };

        var command = new Command("explain", "The effective option set for a file, each with its source file:line and tier.");
        command.Arguments.Add(path);
        command.Options.Add(repositoryRoot);
        command.Options.Add(configuredOnly);
        command.Options.Add(configPath);
        command.SetAction(parse => Run(() => ConfigCommands.Explain(
            parse.GetValue(path)!,
            parse.GetValue(repositoryRoot) ?? FindRepositoryRoot(parse.GetValue(path)!),
            parse.GetValue(configuredOnly),
            parse.GetValue(configPath))));

        return command;
    }

    static Command CreateCheck() {
        var path = new Argument<string>("path") { Description = "The repository root.", DefaultValueFactory = static _ => "." };
        var strict = new Option<bool>("--strict") { Description = "Exit non-zero when there is any warning." };

        var command = new Command("check", "The tier report, the contradictions, and what the export is missing.");
        command.Arguments.Add(path);
        command.Options.Add(strict);
        command.SetAction(parse => Run(() => ConfigCommands.Check(parse.GetValue(path)!, parse.GetValue(strict))));
        return command;
    }

    static Command CreateDiff() {
        var left = new Argument<string?>("a") { Description = "The .editorconfig to compare from.", Arity = ArgumentArity.ZeroOrOne };
        var right = new Argument<string?>("b") { Description = "The .editorconfig to compare to.", Arity = ArgumentArity.ZeroOrOne };
        var canonical = new Option<bool>("--canonical") {
            Description = "Compare the repository's managed block against the canonical instead. Exits 3 on drift; this is the gate condition."
        };
        var options = new Option<bool>("--options") { Description = "With --canonical, also price the upgrade option by option." };

        var command = new Command("diff", "What changes between two .editorconfig files, semantically — or between this repository and the canonical.");
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
            return a is null || b is null
                ? new CommandResult(2, "skala: `config diff` needs two files, or --canonical and a repository path." + Environment.NewLine)
                : ConfigCommands.Diff(a, b);
        }));

        return command;
    }

    static Command CreateDistill() {
        var path = new Argument<string>("path") { Description = "The .editorconfig to distill.", DefaultValueFactory = static _ => ".editorconfig" };
        var output = new Option<string?>("--out", "-o") { Description = "Write the result here instead of to stdout." };

        var command = new Command("distill", "Write back the subset of an export that differs from ReSharper's defaults.");
        command.Arguments.Add(path);
        command.Options.Add(output);
        command.SetAction(parse => Run(() => ConfigCommands.Distill(parse.GetValue(path)!, parse.GetValue(output))));
        return command;
    }

    static Command CreateFix() {
        var path = new Argument<string>("path") { Description = "The .editorconfig to repair.", DefaultValueFactory = static _ => ".editorconfig" };
        var apply = new Option<bool>("--apply") { Description = "Write the file. Without it, fix only says what it would do." };
        var contradictions = new Option<bool>("--resolve-contradictions") { Description = "Also make a losing key agree with the one that already wins." };

        var command = new Command("fix", "Add `root = true` and `max_line_length`, and optionally resolve contradictions.");
        command.Arguments.Add(path);
        command.Options.Add(apply);
        command.Options.Add(contradictions);
        command.SetAction(parse => Run(() => ConfigCommands.Fix(parse.GetValue(path)!, parse.GetValue(apply), parse.GetValue(contradictions))));
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
