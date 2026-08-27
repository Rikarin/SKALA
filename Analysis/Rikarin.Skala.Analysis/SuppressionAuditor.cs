using System.Collections.Immutable;
using System.Diagnostics;
using System.Text.RegularExpressions;
using Rikarin.Skala.Reporting;

namespace Rikarin.Skala.Analysis;

/// <summary>
///     <c>--no-new-suppressions</c>: everything that makes a finding go away without fixing it.
/// </summary>
/// <remarks>
///     docs/plan/09 § "Gates". ⚠ <b>A grep for <c>#pragma</c> is not a constraint.</b> There are four
///     ways to silence a rule and the pragma is the only one that shows up in review as an obvious
///     suppression; the other three are the ones that get used when somebody would rather not have the
///     conversation:
///     <list type="number">
///         <item><c>#pragma warning disable</c> — visible, local, and the one everybody checks for.</item>
///         <item><c>[SuppressMessage]</c> — visible, but attached to a symbol and easy to read past.</item>
///         <item>
///             ⚠ An <c>.editorconfig</c> severity turned down. The widest of the four by a long way: one line
///             under a section header silences a rule for a whole directory tree, and its diff looks like
///             configuration rather than like a suppression.
///         </item>
///         <item>
///             ⚠ A baseline addition. Invisible in the source entirely. The baseline's diff is meant to be the
///             conversation (doc 09 § "The baseline"), and this is what makes it one.
///         </item>
///     </list>
///     <para>
///         The comparison is against a git ref, because "new" is only meaningful relative to something. The
///         old side is read with <c>git grep</c> and <c>git show</c> rather than from a checkout, so the
///         audit never touches the working tree.
///     </para>
/// </remarks>
public static class SuppressionAuditor {
    /// <summary>
    ///     ⚠ Only the disable half. <c>#pragma warning restore</c> ends a suppression rather than
    ///     starting one, and counting both would make every correctly-scoped pragma look like two.
    /// </summary>
    static readonly Regex PragmaPattern = new(
        @"#\s*pragma\s+warning\s+disable\s+(?<ids>[^\r\n/]+)",
        RegexOptions.CultureInvariant | RegexOptions.Compiled
    );

    static readonly Regex SuppressMessagePattern = new(
        """\[\s*(?:System\.Diagnostics\.CodeAnalysis\.)?SuppressMessage\s*\(\s*"[^"]*"\s*,\s*"(?<id>[A-Za-z]+[0-9]+)""",
        RegexOptions.CultureInvariant | RegexOptions.Compiled
    );

    /// <summary><c>dotnet_diagnostic.SK1010.severity</c> and the ReSharper spelling beside it.</summary>
    static readonly Regex SeverityPattern = new(
        @"^\s*(?<key>dotnet_diagnostic\.(?<id>[A-Za-z]+[0-9]+)\.severity|resharper_[a-z0-9_]+_highlighting)\s*=\s*(?<value>[a-z]+)",
        RegexOptions.CultureInvariant | RegexOptions.Compiled
    );

    /// <summary>
    ///     ⚠ The severities that silence or soften, in order — because "turned down" is a comparison
    ///     and not a membership test. <c>warning</c> → <c>suggestion</c> is a downgrade even though
    ///     neither end is <c>none</c>.
    /// </summary>
    static readonly string[] SeverityOrder = ["none", "silent", "hint", "suggestion", "info", "warning", "error"];

    /// <summary>
    ///     One <c>git grep</c> pattern for both single-line source forms.
    /// </summary>
    /// <remarks>
    ///     ⚠ POSIX bracket expressions rather than <c>\s</c>: <c>git grep -E</c> is POSIX ERE, where
    ///     <c>\s</c> is not a character class and matches a literal <c>s</c> on some builds.
    /// </remarks>
    const string GrepPattern = "#[[:space:]]*pragma[[:space:]]+warning[[:space:]]+disable|SuppressMessage";

    /// <summary>Compares the working tree's suppressions to those at <paramref name="reference" />.</summary>
    public static SuppressionAudit Compare(
        string repositoryRoot,
        string reference,
        string? baselinePath,
        CancellationToken cancellation = default
    ) {
        var now = Collect(repositoryRoot, baselinePath, cancellation);
        var before = CollectAt(repositoryRoot, reference, baselinePath, cancellation);

        var previous = before.Select(static entry => entry.Key).ToHashSet();
        var current = now.Select(static entry => entry.Key).ToHashSet();

        return new SuppressionAudit {
            Enforced = true,
            Reference = reference,
            Current = now,
            Added = [.. now.Where(entry => !previous.Contains(entry.Key)).OrderBy(Describe, StringComparer.Ordinal)],
            Removed = [
                .. before.Where(entry => !current.Contains(entry.Key)).OrderBy(Describe, StringComparer.Ordinal)
            ]
        };
    }

    static string Describe(SuppressionEntry entry) => entry.Describe();

    /// <summary>Every suppression in the working tree.</summary>
    public static ImmutableArray<SuppressionEntry> Collect(
        string repositoryRoot,
        string? baselinePath,
        CancellationToken cancellation = default
    ) {
        var entries = ImmutableArray.CreateBuilder<SuppressionEntry>();
        foreach (var (path, line) in Grep(repositoryRoot, reference: null, cancellation)) {
            ScanSource(entries, path, line);
        }

        foreach (var path in Tracked(repositoryRoot, cancellation).Where(IsEditorConfig)) {
            var full = Path.Combine(repositoryRoot, path);
            if (File.Exists(full)) {
                ScanEditorConfig(entries, path, File.ReadAllText(full));
            }
        }

        AddBaseline(
            entries,
            baselinePath is not null && File.Exists(baselinePath)
                ? File.ReadAllText(baselinePath)
                : null
        );

        return entries.ToImmutable();
    }

    static ImmutableArray<SuppressionEntry> CollectAt(
        string repositoryRoot,
        string reference,
        string? baselinePath,
        CancellationToken cancellation
    ) {
        var entries = ImmutableArray.CreateBuilder<SuppressionEntry>();
        foreach (var (path, line) in Grep(repositoryRoot, reference, cancellation)) {
            ScanSource(entries, path, line);
        }

        foreach (var path in TrackedAt(repositoryRoot, reference, cancellation).Where(IsEditorConfig)) {
            if (Show(repositoryRoot, reference, path, cancellation) is { } content) {
                ScanEditorConfig(entries, path, content);
            }
        }

        var relative = baselinePath is null
            ? null
            : Path.GetRelativePath(repositoryRoot, baselinePath).Replace('\\', '/');

        AddBaseline(
            entries,
            relative is null ? null : Show(repositoryRoot, reference, relative, cancellation)
        );

        return entries.ToImmutable();
    }

    static bool IsEditorConfig(string path) => path.EndsWith(".editorconfig", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    ///     Every source line in a tree that could carry a suppression, in <b>one</b> git invocation.
    /// </summary>
    /// <remarks>
    ///     ⚠ A performance decision with a correctness consequence, so it is worth writing down. The
    ///     obvious implementation reads each file's old side with <c>git show ref:path</c>, which is one
    ///     subprocess per file: measured on a 2 705-file tree that took <b>3 m 19 s</b>, and a gate
    ///     condition costing three minutes is a gate condition somebody deletes. <c>git grep</c>
    ///     searches a whole tree — the worktree, or any ref — in one process, and both C# suppression
    ///     forms are single-line, so a line-oriented search loses nothing. Measured after: under a
    ///     second on the same tree.
    ///     <para>
    ///         ⚠ <c>.editorconfig</c> is deliberately <em>not</em> read this way: its entries mean nothing
    ///         without their section header and a grep hit carries no section. There are a handful of those
    ///         files and they are read whole.
    ///     </para>
    /// </remarks>
    static List<(string Path, string Line)> Grep(
        string repositoryRoot,
        string? reference,
        CancellationToken cancellation
    ) {
        string[] arguments = reference is null
            ? ["grep", "-I", "-n", "-E", GrepPattern, "--", "*.cs"]
            : ["grep", "-I", "-n", "-E", GrepPattern, reference, "--", "*.cs"];

        var (output, exit) = Run(repositoryRoot, arguments, cancellation);

        // ⚠ 1 is "nothing matched" and is an ordinary answer; anything above it is a real failure
        // and must not be reported as "this tree suppresses nothing", which would make every
        // suppression in the repository look newly added.
        if (exit > 1) {
            throw new InvalidOperationException(
                "git " + string.Join(' ', arguments.Take(2)) + " failed with exit " + exit
            );
        }

        var prefix = reference is null ? string.Empty : reference + ":";
        var result = new List<(string, string)>();

        foreach (var raw in output.Split('\n')) {
            if (raw.Length == 0) {
                continue;
            }

            var line = prefix.Length > 0 && raw.StartsWith(prefix, StringComparison.Ordinal)
                ? raw[prefix.Length..]
                : raw;

            // `path:line:content`
            var first = line.IndexOf(':', StringComparison.Ordinal);
            var second = first < 0 ? -1 : line.IndexOf(':', first + 1);
            if (first <= 0 || second <= first) {
                continue;
            }

            result.Add((line[..first], line[(second + 1)..]));
        }

        return result;
    }

    static void ScanSource(ImmutableArray<SuppressionEntry>.Builder entries, string path, string line) {
        foreach (Match match in PragmaPattern.Matches(line)) {
            foreach (var id in match.Groups["ids"].Value.Split(',', StringSplitOptions.RemoveEmptyEntries)) {
                var trimmed = id.Trim();
                if (trimmed.Length > 0) {
                    entries.Add(new SuppressionEntry(SuppressionSource.Pragma, trimmed, path, string.Empty));
                }
            }
        }

        foreach (Match match in SuppressMessagePattern.Matches(line)) {
            entries.Add(
                new SuppressionEntry(SuppressionSource.Attribute, match.Groups["id"].Value, path, string.Empty)
            );
        }
    }

    /// <summary>
    ///     The <c>.editorconfig</c> half, tracked per section.
    /// </summary>
    /// <remarks>
    ///     ⚠ The section header is part of the identity. Moving
    ///     <c>dotnet_diagnostic.SK3002.severity = none</c> from <c>[Tools/**/*.cs]</c> to
    ///     <c>[**/*.cs]</c> changes nothing textually about the line and changes everything about what
    ///     it suppresses, so an audit that ignored the section would call that edit a no-op.
    /// </remarks>
    static void ScanEditorConfig(ImmutableArray<SuppressionEntry>.Builder entries, string path, string content) {
        var section = "*";
        foreach (var raw in content.Split('\n')) {
            var line = raw.Trim();
            if (line.StartsWith('[') && line.EndsWith(']')) {
                section = line[1..^1];
                continue;
            }

            var match = SeverityPattern.Match(line);
            if (!match.Success) {
                continue;
            }

            var value = match.Groups["value"].Value;
            if (Rank(value) > Rank("suggestion")) {
                // Not a suppression: `warning` and `error` make a rule louder, not quieter.
                continue;
            }

            var id = match.Groups["id"].Success ? match.Groups["id"].Value : match.Groups["key"].Value;
            entries.Add(new SuppressionEntry(SuppressionSource.EditorConfig, id, path + " [" + section + "]", value));
        }
    }

    static int Rank(string severity) {
        var index = Array.IndexOf(SeverityOrder, severity);
        return index < 0 ? SeverityOrder.Length : index;
    }

    static void AddBaseline(ImmutableArray<SuppressionEntry>.Builder entries, string? sarif) {
        if (sarif is null) {
            return;
        }

        var temporary = Path.GetTempFileName();
        try {
            File.WriteAllText(temporary, sarif);
            foreach (var entry in Baseline.Read(temporary).Entries) {
                entries.Add(
                    new SuppressionEntry(
                        SuppressionSource.Baseline,
                        entry.RuleId,
                        entry.Path,

                        // ⚠ The fingerprint, not the message. A baseline entry whose message was
                        // reworded is the same suppression; one whose fingerprint changed is a new
                        // one, which is exactly what the audit is looking for.
                        entry.FingerprintV2.Length > 0 ? entry.FingerprintV2 : entry.FingerprintV1
                    )
                );
            }
        } catch (Exception exception) when (exception is IOException or InvalidDataException) {
            // An unreadable baseline is reported by the check itself; the audit does not duplicate it.
        } finally {
            try {
                File.Delete(temporary);
            } catch (IOException) { }
        }
    }

    /// <summary>
    ///     Whether a tracked path is one this audit reads whole.
    /// </summary>
    /// <remarks>
    ///     ⚠ Filtered here rather than by a git pathspec, and that is not a style preference.
    ///     <c>git ls-files "*.cs"</c> matches nested paths; <c>git ls-tree -r -- "*.cs"</c> does
    ///     <b>not</b>. Measured on a 2 705-file tree: <c>ls-files</c> returned 2 705 and
    ///     <c>ls-tree</c> returned <b>0</b>, so every suppression in the repository read as newly added
    ///     and the gate failed with 1 012 violations that did not exist. Two commands with the
    ///     same-looking pathspec and different matching rules is exactly the asymmetry to keep out of
    ///     the query and put in one predicate both sides share.
    /// </remarks>
    static IEnumerable<string> Tracked(string repositoryRoot, CancellationToken cancellation) =>
        Lines(repositoryRoot, ["ls-files"], cancellation);

    static IEnumerable<string> TrackedAt(string repositoryRoot, string reference, CancellationToken cancellation) =>
        Lines(repositoryRoot, ["ls-tree", "-r", "--name-only", reference], cancellation);

    static string? Show(string repositoryRoot, string reference, string path, CancellationToken cancellation) {
        var (output, exit) = Run(repositoryRoot, ["show", reference + ":" + path], cancellation);

        // A file absent at the ref is not an error: it is a new file, and everything it suppresses
        // is new.
        return exit == 0 ? output : null;
    }

    static IEnumerable<string> Lines(string repositoryRoot, string[] arguments, CancellationToken cancellation) {
        var (output, exit) = Run(repositoryRoot, arguments, cancellation);
        return exit != 0
            ? []
            : output.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(static line => line.Trim());
    }

    static (string Output, int ExitCode) Run(
        string repositoryRoot,
        string[] arguments,
        CancellationToken cancellation
    ) {
        var start = new ProcessStartInfo("git") {
            WorkingDirectory = repositoryRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };

        foreach (var argument in arguments) {
            start.ArgumentList.Add(argument);
        }

        using var process = Process.Start(start);
        if (process is null) {
            return (string.Empty, -1);
        }

        var output = process.StandardOutput.ReadToEnd();
        process.StandardError.ReadToEnd();
        process.WaitForExit();
        cancellation.ThrowIfCancellationRequested();

        return (output, process.ExitCode);
    }
}
