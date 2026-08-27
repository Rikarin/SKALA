using System.Collections.Immutable;
using System.Diagnostics;
using System.Globalization;

namespace Rikarin.Skala.Reporting;

/// <summary>
///     The lines a git ref changed — Sonar's "new code" definition, with no server.
/// </summary>
/// <remarks>
///     docs/plan/09 § "New-code definition". <c>skala check --since=origin/main --gate=pr</c> is a gate
///     that only cares about what this branch touched, "which is the only gate that is adoptable on a
///     tree with existing findings". That sentence is the whole reason this type exists: without it,
///     adopting the analysis half of the tool on a repository with a thousand existing findings means
///     fixing a thousand findings first, and nobody does that.
///     <para>
///         ⚠ Implemented by shelling out to <c>git</c> rather than by linking a git library. The ref
///         spellings people actually type — <c>origin/main</c>, <c>HEAD~3</c>, <c>@{u}</c>, a tag, a
///         branch that only exists as a remote-tracking ref — are all resolved by git itself for free, and
///         a library that resolves nine of them is a library that fails on the tenth in a way the user
///         cannot work around.
///     </para>
/// </remarks>
public sealed class ChangedLines {
    ChangedLines(Dictionary<string, ImmutableArray<LineRange>> ranges, string reference) {
        _ranges = ranges;
        Reference = reference;
    }

    readonly Dictionary<string, ImmutableArray<LineRange>> _ranges;

    /// <summary>The ref the ranges are relative to.</summary>
    public string Reference { get; }

    /// <summary>Files with at least one changed line.</summary>
    public int FileCount => _ranges.Count;

    public int RangeCount => _ranges.Values.Sum(static ranges => ranges.Length);

    /// <summary>A half-open run of lines, one-based, as <c>git diff</c> reports it.</summary>
    public readonly record struct LineRange(int Start, int End) {
        public bool Contains(int line) => line >= Start && line <= End;
    }

    /// <summary>
    ///     Computes the changed ranges between <paramref name="reference" /> and the working tree.
    /// </summary>
    /// <remarks>
    ///     ⚠ <c>--unified=0</c> so that a hunk header names exactly the changed lines and not three
    ///     lines of unchanged context either side. With context, a finding on an untouched line three
    ///     above an edit counts as new code, and a gate built on that fails a PR for something it did
    ///     not do — the single fastest way to make people stop trusting <c>--since</c>.
    ///     <para>
    ///         ⚠ <b>Merge-base semantics against the working tree</b>, and getting there needs two commands
    ///         rather than one clever one. <c>git diff ref</c> with two dots compares the working tree to
    ///         that ref, which on a branch whose base has moved on reports every change the base picked up
    ///         — someone else's commits, attributed to this branch. <c>git diff ref...</c> with three dots
    ///         has the right base and the wrong right-hand side: three-dot syntax needs two commits, so
    ///         <c>ref...</c> means <c>ref...HEAD</c> and the working tree is <em>excluded entirely</em>.
    ///         Uncommitted work — which is most of what a developer runs this against — would be invisible,
    ///         and a <c>newIssues: 0</c> gate would pass on it. So the merge base is resolved explicitly and
    ///         then diffed with two dots, which is the only spelling that has both halves right.
    ///     </para>
    /// </remarks>
    public static ChangedLines Since(
        string repositoryRoot,
        string reference,
        CancellationToken cancellation = default
    ) {
        var output = Git(
            repositoryRoot,
            ["diff", "--unified=0", "--no-color", MergeBase(repositoryRoot, reference, cancellation), "--"],
            cancellation
        );

        var ranges = Parse(repositoryRoot, output);
        AddUntracked(repositoryRoot, ranges, cancellation);
        return new ChangedLines(ranges, reference);
    }

    /// <summary>
    ///     Whole files that git has never seen, every line of which is new code.
    /// </summary>
    /// <remarks>
    ///     ⚠ <c>git diff</c> reports tracked files only, so a brand-new file produces no hunk at all and
    ///     every finding in it would fall outside the changed ranges. A PR gate that ignores the files
    ///     the PR <em>added</em> is worse than no gate: it is quiet in exactly the case it exists for,
    ///     and the quiet reads as approval. Measured while building the demo — an added file with a
    ///     deliberate <c>SK2015</c> in it passed a <c>newIssues: 0</c> gate.
    ///     <para>
    ///         <c>--exclude-standard</c> so that a file the repository's own <c>.gitignore</c> excludes —
    ///         build output, a scratch file — is not counted as somebody's new code.
    ///     </para>
    /// </remarks>
    static void AddUntracked(
        string repositoryRoot,
        Dictionary<string, ImmutableArray<LineRange>> ranges,
        CancellationToken cancellation
    ) {
        string output;
        try {
            output = Git(repositoryRoot, ["ls-files", "--others", "--exclude-standard"], cancellation);
        } catch (InvalidOperationException) {
            return;
        }

        foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries)) {
            var path = Normalize(Path.Combine(repositoryRoot, line.Trim()));

            // The whole file. A range with no upper bound is what "all of it is new" means, and it
            // costs nothing to say so rather than counting the file's lines.
            ranges[path] = [new LineRange(1, int.MaxValue)];
        }
    }

    /// <summary>
    ///     The commit this branch diverged from, or the ref itself when there is no shared history.
    /// </summary>
    /// <remarks>
    ///     ⚠ Falling back to the ref rather than failing. A shallow clone — which is what most CI
    ///     checkouts are — can have no merge base with the ref it was told to compare against, and
    ///     refusing to run there would make <c>--since</c> unusable in exactly the place doc 09 wants
    ///     it. Comparing against the ref directly is the conservative answer: it can only ever mark
    ///     <em>more</em> lines as changed, so a gate errs towards failing rather than towards passing.
    /// </remarks>
    static string MergeBase(string repositoryRoot, string reference, CancellationToken cancellation) {
        try {
            var output = Git(repositoryRoot, ["merge-base", reference, "HEAD"], cancellation).Trim();
            return output.Length > 0 ? output : reference;
        } catch (InvalidOperationException) {
            return reference;
        }
    }

    /// <summary>Whether a finding sits on a line the ref changed.</summary>
    public bool Contains(Finding finding) {
        if (!_ranges.TryGetValue(Normalize(finding.Path), out var ranges)) {
            return false;
        }

        foreach (var range in ranges) {
            // ⚠ The finding's whole span, not only its start. A finding reported on the first line
            // of a method whose body was rewritten is about the change even though its own line did
            // not move.
            if (range.Start <= Math.Max(finding.Line, finding.EndLine) && range.End >= finding.Line) {
                return true;
            }
        }

        return false;
    }

    /// <summary>Tags every finding with whether it is inside the changed ranges.</summary>
    public ImmutableArray<Finding> Apply(ImmutableArray<Finding> findings) =>
        [.. findings.Select(finding => finding with { IsInChangedCode = Contains(finding) })];

    static Dictionary<string, ImmutableArray<LineRange>> Parse(string repositoryRoot, string diff) {
        var result = new Dictionary<string, ImmutableArray<LineRange>>(StringComparer.Ordinal);
        var current = string.Empty;
        var ranges = ImmutableArray.CreateBuilder<LineRange>();

        foreach (var line in diff.Split('\n')) {
            if (line.StartsWith("+++ ", StringComparison.Ordinal)) {
                Flush(result, current, ranges);
                var path = line[4..].Trim();

                // `/dev/null` is a deleted file: nothing in it can carry a finding.
                current = path == "/dev/null"
                    ? string.Empty
                    : Normalize(Path.Combine(repositoryRoot, Strip(path)));

                continue;
            }

            if (current.Length == 0 || !line.StartsWith("@@", StringComparison.Ordinal)) {
                continue;
            }

            if (ParseHunk(line) is { } range) {
                ranges.Add(range);
            }
        }

        Flush(result, current, ranges);
        return result;
    }

    static void Flush(
        Dictionary<string, ImmutableArray<LineRange>> result,
        string path,
        ImmutableArray<LineRange>.Builder ranges
    ) {
        if (path.Length > 0 && ranges.Count > 0) {
            result[path] = result.TryGetValue(path, out var existing)
                ? [.. existing, .. ranges]
                : ranges.ToImmutable();
        }

        ranges.Clear();
    }

    /// <summary>
    ///     <c>@@ -a,b +c,d @@</c> — the <c>+c,d</c> half, which is the post-image and the only one that
    ///     can hold a finding.
    /// </summary>
    /// <remarks>
    ///     ⚠ <c>d</c> of zero is a pure deletion. It has no lines in the new file, so it contributes no
    ///     range at all; treating it as one line would mark whatever now sits at <c>c</c> as changed.
    /// </remarks>
    static LineRange? ParseHunk(string line) {
        var plus = line.IndexOf('+', StringComparison.Ordinal);
        if (plus < 0) {
            return null;
        }

        var end = line.IndexOf(' ', plus);
        var span = end < 0 ? line[(plus + 1)..] : line[(plus + 1)..end];
        var comma = span.IndexOf(',', StringComparison.Ordinal);

        var startText = comma < 0 ? span : span[..comma];
        var countText = comma < 0 ? "1" : span[(comma + 1)..];

        if (!int.TryParse(startText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var start)
            || !int.TryParse(countText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var count)) {
            return null;
        }

        return count == 0 ? null : new LineRange(start, start + count - 1);
    }

    /// <summary>Drops git's <c>a/</c> and <c>b/</c> prefixes.</summary>
    static string Strip(string path) =>
        path.StartsWith("a/", StringComparison.Ordinal) || path.StartsWith("b/", StringComparison.Ordinal)
            ? path[2..]
            : path;

    static string Normalize(string path) => Path.GetFullPath(path).Replace('\\', '/');

    /// <summary>
    ///     Runs git and returns its stdout.
    /// </summary>
    /// <remarks>
    ///     ⚠ A failure throws with git's own stderr attached. <c>--since=orgin/main</c> is a typo
    ///     somebody will make, and the useful answer is git's ("unknown revision") rather than a clean
    ///     run over zero changed lines — which would pass a <c>newIssues: 0</c> gate for the worst
    ///     possible reason.
    /// </remarks>
    static string Git(string repositoryRoot, string[] arguments, CancellationToken cancellation) {
        var start = new ProcessStartInfo("git") {
            WorkingDirectory = repositoryRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };

        foreach (var argument in arguments) {
            start.ArgumentList.Add(argument);
        }

        using var process = Process.Start(start)
            ?? throw new InvalidOperationException("git could not be started; --since needs git on the PATH.");

        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();

        cancellation.ThrowIfCancellationRequested();

        if (process.ExitCode != 0) {
            throw new InvalidOperationException("git " + string.Join(' ', arguments) + " failed: " + error.Trim());
        }

        return output;
    }
}
