using System.Collections.Immutable;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Core.Configuration;
using Rikarin.Skala.Core.Diagnostics;

namespace Rikarin.Skala.Formatting.CSharp;

/// <summary>What <c>skala format</c> was asked to do.</summary>
public sealed record FormatRequest {
    public IReadOnlyList<string> Paths { get; init; } = [];

    /// <summary>Report, do not write. Exit 1 when there are edits.</summary>
    public bool Check { get; init; }

    /// <summary>Print a unified diff over the edits. ⚠ Reports; never writes.</summary>
    public bool Diff { get; init; }

    /// <summary><c>a:b</c> — character offsets, filtered after full-file fitting.</summary>
    public string? Range { get; init; }

    /// <summary>Format the git index; see <see cref="StagedMode"/>.</summary>
    public StagedMode Staged { get; init; } = StagedMode.Off;

    public bool Quiet { get; init; }

    public IReadOnlyList<KeyValuePair<string, string>> Overrides { get; init; } = [];

    public string? RepositoryRoot { get; init; }

    /// <summary>
    /// <c>--jobs</c>: how many files are formatted at once. Null means <c>min(cores, 10)</c>.
    /// </summary>
    /// <remarks>
    /// ⚠ Formatting is embarrassingly parallel and this loop was sequential until milestone 3.
    /// Measured on Vixen at M2: 34.3 s of wall time for 36.7 s of CPU, a speedup of 1.07× on a
    /// ten-core machine against a 20 s budget (docs/plan/13 § "Parallelism"). The missing factor was
    /// never the formatter.
    /// </remarks>
    public int? Jobs { get; init; }

    /// <summary>
    /// <c>--define</c>: the preprocessor symbols to parse with.
    /// </summary>
    /// <remarks>
    /// ⚠ SK-DIV-0004, and the reason it is a formatter option rather than an analysis one. Without
    /// symbols Roslyn hands every <c>#if DEBUG</c> body back as disabled text and Skala leaves it
    /// byte-for-byte, so on a tree with much conditional code the conditional half is not formatted
    /// at all. A loaded compilation knows the symbols; <c>--define</c> is how a repository with no
    /// build says them itself.
    /// </remarks>
    public IReadOnlyList<string> Define { get; init; } = [];

    /// <summary>
    /// <c>--xmldoc</c>: also re-wrap documentation comments.
    /// </summary>
    /// <remarks>
    /// ⚠ SK-DIV-0006, and the reason it is a flag rather than a key. The whole
    /// <c>resharper_xmldoc_*</c> family is set in the export and <c>jb cleanupcode</c> honours none
    /// of it — measured, not assumed — so a Skala that re-wrapped doc comments by default would
    /// disagree with Rider on every doc comment in every repository. Off is the setting that agrees
    /// with the oracle; this is the setting for a tree that wants the layout its .editorconfig
    /// describes and accepts that Rider will not reproduce it.
    /// <para>
    /// ⚠ It makes <c>--diff</c> and <c>--range</c> coarser around a re-wrapped comment, and only
    /// there. The anchor points that make an edit minimal are offsets into the text the
    /// sub-formatter rewrites, and an anchor inside a re-wrapped comment is dropped rather than
    /// guessed at — an anchor that lies about where a piece went produces an edit that overwrites
    /// the wrong bytes.
    /// </para>
    /// </remarks>
    public bool XmlDoc { get; init; }
}

/// <summary>How <c>--staged</c> behaves in the presence of unstaged edits.</summary>
public enum StagedMode {
    Off,

    /// <summary>
    /// ⚠ Refuses to run when a staged file also has unstaged changes: formatting the worktree copy
    /// would stage work the author did not mean to commit, and formatting the index copy would
    /// leave the two disagreeing.
    /// </summary>
    Strict,

    /// <summary>Format the worktree copy and stage the result anyway, as the author asked.</summary>
    Worktree
}

/// <summary>
/// The implementation behind <c>skala format</c>.
/// </summary>
/// <remarks>
/// ⚠ It lives here rather than in the CLI because nothing may reference
/// <c>Rikarin.Skala.Cli</c> (docs/plan/02 § "The project graph"): MSBuild, the daemon and MCP host
/// the same logic and the CLI is argument parsing and rendering only.
/// </remarks>
public static class FormatCommand {
    public const int ChangesFound = 1;
    public const int Failed = 2;

    public static CommandResult Run(FormatRequest request) {
        var output = new StringBuilder();
        var root = request.RepositoryRoot ?? FindRepositoryRoot(request.Paths.Count > 0 ? request.Paths[0] : ".");
        var crashRoot = root is null ? null : Path.Combine(root, ".skala");

        List<string> files;
        if (request.Staged != StagedMode.Off) {
            if (root is null) {
                return new CommandResult(Failed, "skala format --staged: not inside a git repository\n");
            }

            var staged = GitIndex.StagedFiles(root);
            if (request.Staged == StagedMode.Strict) {
                var dirty = staged.Where(file => GitIndex.HasUnstagedChanges(root, file)).ToList();
                if (dirty.Count > 0) {
                    output.AppendLine("skala format --staged: these files have unstaged changes:");
                    foreach (var file in dirty) {
                        output.Append("  ").AppendLine(file);
                    }

                    output.AppendLine();
                    output.AppendLine("Formatting the worktree copy would stage work you did not mean to commit.");
                    output.AppendLine("Pass --staged=worktree to format and stage them anyway.");
                    return new CommandResult(Failed, output.ToString());
                }
            }

            files = [.. staged.Select(file => Path.Combine(root, file))];
        } else {
            files = [.. Collect(request.Paths)];
        }

        var range = ParseRange(request.Range);
        var outcomes = FormatAll(files, request, crashRoot, root, range);

        var changed = 0;
        var failures = 0;
        var diagnostics = new List<SkalaDiagnostic>();

        // ⚠ The results are consumed in the order the files were collected, not the order they
        // finished. docs/plan/13 § "Parallelism": "determinism is restored by sorting after the
        // fact, never by serialising." Two runs of `--check` over the same tree must print the same
        // bytes, or the output is unusable in CI and in a review.
        for (var i = 0; i < outcomes.Length; i++) {
            var outcome = outcomes[i];
            if (outcome is null) {
                continue;
            }

            diagnostics.AddRange(outcome.Diagnostics);
            if (outcome.Failed) {
                failures++;
                continue;
            }

            if (!outcome.Changed) {
                continue;
            }

            changed++;
            if (request.Diff) {
                output.Append(outcome.Diff);
            } else if (!request.Quiet) {
                output.Append(Relative(root, files[i])).AppendLine();
            }

            if (request.Staged != StagedMode.Off && root is not null && !request.Check && !request.Diff) {
                // ⚠ Serial, and after the writes. `git add` is a process launch against one index
                // file; running 4 700 of them at ten-way parallelism is both slower than doing it in
                // order and a lock contention on `.git/index` that git reports as a failure rather
                // than a wait.
                GitIndex.Add(root, files[i]);
            }
        }

        foreach (var diagnostic in diagnostics.Where(static d => d.Severity >= SkalaSeverity.Info)) {
            output.AppendLine(diagnostic.ToString());
            if (diagnostic.Detail is { } detail) {
                output.Append("    ").AppendLine(detail);
            }
        }

        if (!request.Quiet) {
            output.Append(changed.ToString(CultureInfo.InvariantCulture))
                .Append(changed == 1 ? " file " : " files ")
                .Append(request.Check || request.Diff ? "would be reformatted" : "reformatted")
                .Append(", ")
                .Append((files.Count - changed).ToString(CultureInfo.InvariantCulture))
                .AppendLine(" left alone");
        }

        var exit = failures > 0 ? Failed : (request.Check || request.Diff) && changed > 0 ? ChangesFound : 0;
        return new CommandResult(exit, output.ToString());
    }

    /// <summary>One file's result, reduced to what the ordered pass needs.</summary>
    sealed record FileOutcome(
        bool Failed,
        bool Changed,
        string? Diff,
        IReadOnlyList<SkalaDiagnostic> Diagnostics);

    /// <summary>
    /// Formats every file, in parallel, into a result slot of its own.
    /// </summary>
    /// <remarks>
    /// ⚠ The write happens inside the parallel body and the <em>reporting</em> does not. Writing is
    /// per-file and independent; appending to one <see cref="StringBuilder"/> from ten threads is
    /// neither, and sorting the pieces afterwards is not the same thing as writing them in order —
    /// a diff whose hunks arrive interleaved is not a diff.
    /// </remarks>
    static FileOutcome?[] FormatAll(
        List<string> files,
        FormatRequest request,
        string? crashRoot,
        string? root,
        SourceSpan? range
    ) {
        var outcomes = new FileOutcome?[files.Count];
        var jobs = request.Jobs is { } requested and > 0
            ? requested
            : Math.Min(Environment.ProcessorCount, 10);

        if (jobs == 1 || files.Count <= 1) {
            for (var i = 0; i < files.Count; i++) {
                outcomes[i] = FormatOne(files[i], request, crashRoot, root, range);
            }

            return outcomes;
        }

        Parallel.For(
            0,
            files.Count,
            new ParallelOptions { MaxDegreeOfParallelism = jobs },
            index => outcomes[index] = FormatOne(files[index], request, crashRoot, root, range)
        );

        return outcomes;
    }

    static FileOutcome FormatOne(
        string file,
        FormatRequest request,
        string? crashRoot,
        string? root,
        SourceSpan? range
    ) {
        FormatResult result;
        try {
            result = CSharpFormatter.FormatFile(
                file,
                request.Overrides,
                crashRoot,
                request.Define,
                request.XmlDoc
            );
        } catch (IOException exception) {
            return new FileOutcome(
                true,
                false,
                null,
                [new SkalaDiagnostic(FormatDiagnosticIds.FileIoFailed, SkalaSeverity.Error, exception.Message, file)]
            );
        }

        if (result.Outcome == FormatOutcome.VerificationFailed) {
            return new FileOutcome(true, false, null, result.Diagnostics);
        }

        var edits = range is { } span ? EditEmitter.Restrict(result.Edits, span) : result.Edits;
        if (edits.Count == 0) {
            return new FileOutcome(false, false, null, result.Diagnostics);
        }

        var original = result.Original.ToString();
        var text = EditEmitter.Apply(original, edits);

        // ⚠ `--diff` does not write, any more than `--check` does. docs/plan/04 § "Emitting minimal
        // edits" says so — "`--diff` is a unified diff over the edits" — and a reporting flag that
        // also rewrites the tree is the kind of thing a person discovers by running it on someone
        // else's repository. It rewrote 9 000 files across four worktrees once.
        if (!request.Check && !request.Diff) {
            File.WriteAllText(file, text, result.Original.Encoding ?? new UTF8Encoding(false));
        }

        return new FileOutcome(
            false,
            true,
            request.Diff ? UnifiedDiff.Render(Relative(root, file), original, text) : null,
            result.Diagnostics
        );
    }

    static SourceSpan? ParseRange(string? range) {
        if (range is null) {
            return null;
        }

        var parts = range.Split(':');
        if (parts.Length == 2
            && int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var start)
            && int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var end)) {
            return SourceSpan.FromBounds(start, Math.Max(start, end));
        }

        return null;
    }

    public static IEnumerable<string> Collect(IReadOnlyList<string> paths) {
        var roots = paths.Count == 0 ? [Directory.GetCurrentDirectory()] : paths;
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var path in roots) {
            var full = Path.GetFullPath(path);
            if (File.Exists(full)) {
                if (seen.Add(full)) {
                    yield return full;
                }

                continue;
            }

            if (!Directory.Exists(full)) {
                continue;
            }

            foreach (var file in Directory.EnumerateFiles(full, "*.cs", SearchOption.AllDirectories)
                         .OrderBy(
                             static f => f,
                             StringComparer.Ordinal
                         )) {
                // A formatter that reformats artifacts/ is a formatter that is quietly very slow.
                if (IsExcluded(file) || !seen.Add(file)) {
                    continue;
                }

                yield return file;
            }
        }
    }

    static bool IsExcluded(string path) {
        var separator = Path.DirectorySeparatorChar;
        return path.Contains($"{separator}obj{separator}", StringComparison.Ordinal)
            || path.Contains($"{separator}bin{separator}", StringComparison.Ordinal)
            || path.Contains($"{separator}.git{separator}", StringComparison.Ordinal)
            || path.Contains($"{separator}artifacts{separator}", StringComparison.Ordinal);
    }

    static string Relative(string? root, string file) =>
        root is null ? file : Path.GetRelativePath(root, file).Replace('\\', '/');

    public static string? FindRepositoryRoot(string path) {
        var full = Path.GetFullPath(path);
        var directory = Directory.Exists(full) ? full : Path.GetDirectoryName(full);
        while (directory is not null) {
            if (Directory.Exists(Path.Combine(directory, ".git")) || File.Exists(Path.Combine(directory, ".git"))) {
                return directory;
            }

            var parent = Path.GetDirectoryName(directory);
            directory = string.Equals(parent, directory, StringComparison.Ordinal) ? null : parent;
        }

        return null;
    }
}

/// <summary>The three git questions <c>--staged</c> asks.</summary>
public static class GitIndex {
    public static ImmutableArray<string> StagedFiles(string root) {
        var output = Run(root, "diff", "--name-only", "--cached", "--diff-filter=ACMR", "--", "*.cs");
        return [.. output.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(static line => line.Trim())];
    }

    public static bool HasUnstagedChanges(string root, string relativePath) =>
        Run(root, "diff", "--name-only", "--", relativePath).Trim().Length > 0;

    public static void Add(string root, string path) => Run(root, "add", "--", path);

    static string Run(string root, params string[] arguments) {
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

        var output = process.StandardOutput.ReadToEnd();
        process.StandardError.ReadToEnd();
        process.WaitForExit();
        return output;
    }
}
