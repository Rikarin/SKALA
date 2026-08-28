using System.Text;
using Rikarin.Skala.Core.Configuration;

namespace Rikarin.Skala.Server;

/// <summary>
///     <c>skala hooks install</c> — docs/plan/11 § "Git hooks".
/// </summary>
/// <remarks>
///     ⚠ It detects an existing hook manager rather than clobbering it. A tool that overwrites somebody's
///     husky, lefthook or pre-commit configuration to install itself has, from that moment, broken every
///     other check the repository ran — and it will be blamed for the one that fails next week rather
///     than for the one it deleted today.
///     <para>
///         The hook is deliberately two lines and deliberately not clever. It calls the same CLI a person
///         calls; there is no second code path to keep in step, which is the same rule the LSP server is
///         held to.
///     </para>
/// </remarks>
public static class GitHooks {
    public const string Marker = "# installed by skala hooks install";

    public static string Script { get; } = string.Join(
        '\n',
        "#!/usr/bin/env bash",
        Marker,
        "set -euo pipefail",
        "",
        "skala format --staged --quiet || exit 1",
        ""
    );

    /// <summary>What an install would do, or did.</summary>
    /// <param name="Path">Where the hook is or would be.</param>
    /// <param name="Outcome">One line, for the user.</param>
    /// <param name="Written">False for a dry run, and for every refusal.</param>
    public sealed record Result(string Path, string Outcome, bool Written);

    public static Result Install(string repositoryRoot, bool apply) {
        var hooks = HooksDirectory(repositoryRoot);
        var path = Path.Combine(hooks, "pre-commit");

        // ⚠ A repository with `core.hooksPath` set, or with a manager's own hook already in place, is
        // a repository whose hooks somebody else owns. Say what to add and stop.
        if (Detect(repositoryRoot) is { } manager) {
            return new Result(
                path,
                $"{manager} manages this repository's hooks. Add `skala format --staged --quiet` to its configuration instead.",
                false
            );
        }

        if (File.Exists(path)) {
            var existing = File.ReadAllText(path);
            if (existing.Contains(Marker, StringComparison.Ordinal)) {
                return new Result(path, "already installed", false);
            }

            return new Result(
                path,
                "a pre-commit hook is already installed and was not written by skala; not touching it.",
                false
            );
        }

        if (!apply) {
            return new Result(path, "would write a pre-commit hook", false);
        }

        Directory.CreateDirectory(hooks);
        File.WriteAllText(path, Script, new UTF8Encoding(false));
        if (!OperatingSystem.IsWindows()) {
            File.SetUnixFileMode(
                path,
                UnixFileMode.UserRead
                | UnixFileMode.UserWrite
                | UnixFileMode.UserExecute
                | UnixFileMode.GroupRead
                | UnixFileMode.GroupExecute
                | UnixFileMode.OtherRead
                | UnixFileMode.OtherExecute
            );
        }

        return new Result(path, "written", true);
    }

    /// <summary>The hook manager that owns this repository's hooks, or null.</summary>
    public static string? Detect(string repositoryRoot) {
        if (File.Exists(Path.Combine(repositoryRoot, ".pre-commit-config.yaml"))) {
            return "pre-commit";
        }

        if (File.Exists(Path.Combine(repositoryRoot, "lefthook.yml"))
            || File.Exists(Path.Combine(repositoryRoot, "lefthook.yaml"))) {
            return "lefthook";
        }

        if (Directory.Exists(Path.Combine(repositoryRoot, ".husky"))) {
            return "husky";
        }

        // ⚠ `core.hooksPath` pointing somewhere else means git will never run `.git/hooks/pre-commit`
        // at all, so writing one there is worse than doing nothing: it looks installed and is inert.
        var configured = HooksPathSetting(repositoryRoot);
        return configured is { Length: > 0 }
            && !string.Equals(
                Path.GetFullPath(configured, repositoryRoot),
                Path.Combine(repositoryRoot, ".git", "hooks"),
                StringComparison.Ordinal
            )
                ? "core.hooksPath (" + configured + ")"
                : null;
    }

    static string HooksDirectory(string repositoryRoot) => Path.Combine(repositoryRoot, ".git", "hooks");

    static string? HooksPathSetting(string repositoryRoot) {
        var config = Path.Combine(repositoryRoot, ".git", "config");
        if (!File.Exists(config)) {
            return null;
        }

        foreach (var line in File.ReadLines(config)) {
            var trimmed = line.Trim();
            if (trimmed.StartsWith("hooksPath", StringComparison.OrdinalIgnoreCase)) {
                var equals = trimmed.IndexOf('=', StringComparison.Ordinal);
                if (equals > 0) {
                    return trimmed[(equals + 1)..].Trim();
                }
            }
        }

        return null;
    }

    /// <summary>The repository root, for the CLI. Shares the CLI's own walk so the two cannot disagree.</summary>
    public static string? FindRepositoryRoot(string path) => Formatting.CSharp.FormatCommand.FindRepositoryRoot(path);

    /// <summary>⚠ Referenced so the hook and the tool cannot drift apart on where the config lives.</summary>
    public static string ConfigurationFile => EditorConfigDocument.FileName;
}
