using Rikarin.Skala.Formatting;

namespace Rikarin.Skala.Formatting.CSharp.Arrangement;

/// <summary>
/// A real edit-to-span map: the minimal edit list that turns one text into another.
/// </summary>
/// <remarks>
/// ⚠ Milestone 4's need #4. The formatter can emit minimal edits without diffing anything, because
/// <see cref="LayoutWriter"/> hands it an <see cref="AnchorPoint"/> per token and every edit is the
/// gap between two of them — the map from output back to input is a by-product of writing. That
/// works while every edit is *local*, which is true of whitespace and false of arrangement: a member
/// that moves 200 lines has no anchor relating where it was to where it is, and
/// <see cref="EditEmitter.Emit"/> over an anchor-less layout collapses to a single edit spanning the
/// whole file.
/// <para>
/// ⚠ A whole-file edit is not merely ugly — it breaks two things that are load-bearing.
/// <c>--range a:b</c> and the LSP's range formatting are <see cref="EditEmitter.Restrict"/> over the
/// whole-file edit list, so a single edit spanning the file means every range "intersects" and range
/// formatting silently becomes whole-file formatting. And <c>arrange --check</c>'s output is a diff
/// a person reads before allowing a tree rewrite; one hunk covering every line is not reviewable.
/// </para>
/// <para>
/// Line-level rather than character-level, and deliberately: an arrangement edit is a member, a
/// statement, or a using — never half a line — and a line-keyed diff makes the hunks land on the
/// boundaries a reviewer already reads by. The character-level prefix/suffix trim inside each hunk
/// then recovers the precision for the common one-token case.
/// </para>
/// </remarks>
public static class ArrangementEdits {
    /// <summary>
    /// The edits that turn <paramref name="before"/> into <paramref name="after"/>, ordered and
    /// disjoint, against the ORIGINAL text (ADR-005).
    /// </summary>
    public static IReadOnlyList<TextEdit> Diff(string before, string after) {
        if (string.Equals(before, after, StringComparison.Ordinal)) {
            return [];
        }

        var left = SplitKeepingBreaks(before);
        var right = SplitKeepingBreaks(after);

        // Trim the identical head and tail first. On a real file this is nearly all of it, and it
        // takes the O(n·m) table below from "every line" to "the changed region".
        var head = 0;
        while (head < left.Count && head < right.Count
            && string.Equals(Text(before, left[head]), Text(after, right[head]), StringComparison.Ordinal)) {
            head++;
        }

        var tail = 0;
        while (tail < left.Count - head
            && tail < right.Count - head
            && string.Equals(
                Text(before, left[^(tail + 1)]),
                Text(after, right[^(tail + 1)]),
                StringComparison.Ordinal
            )) {
            tail++;
        }

        var leftWindow = left.GetRange(head, left.Count - head - tail);
        var rightWindow = right.GetRange(head, right.Count - head - tail);

        var edits = new List<TextEdit>();
        foreach (var hunk in Hunks(before, after, leftWindow, rightWindow)) {
            Add(edits, before, after, hunk);
        }

        return edits;
    }

    readonly record struct Line(int Start, int End);

    readonly record struct Hunk(int BeforeStart, int BeforeEnd, int AfterStart, int AfterEnd);

    static string Text(string source, Line line) => source[line.Start..line.End];

    static List<Line> SplitKeepingBreaks(string text) {
        var lines = new List<Line>();
        var start = 0;
        for (var i = 0; i < text.Length; i++) {
            if (text[i] != '\n') {
                continue;
            }

            lines.Add(new Line(start, i + 1));
            start = i + 1;
        }

        if (start <= text.Length) {
            lines.Add(new Line(start, text.Length));
        }

        return lines;
    }

    /// <summary>
    /// The changed regions, from a longest-common-subsequence over lines.
    /// </summary>
    /// <remarks>
    /// ⚠ Bounded, and the bound is the point. The table is O(n·m) cells, so a pathological pair of
    /// windows — two files that share no line at all — would allocate a matrix the size of their
    /// product. Above <see cref="TableLimit"/> the whole window becomes one hunk, which is exactly
    /// what the anchor-less emitter did and is correct, only coarse. Corpus measurement: the largest
    /// window over <c>corpus/real/</c> is far below the limit, so this path is a guard rather than a
    /// mode.
    /// </remarks>
    static IEnumerable<Hunk> Hunks(string before, string after, List<Line> left, List<Line> right) {
        if (left.Count == 0 && right.Count == 0) {
            yield break;
        }

        if ((long)left.Count * right.Count > TableLimit) {
            yield return new Hunk(
                Start(left, before.Length),
                End(left, Start(left, before.Length)),
                Start(right, after.Length),
                End(right, Start(right, after.Length))
            );

            yield break;
        }

        var lengths = new int[left.Count + 1, right.Count + 1];
        for (var i = left.Count - 1; i >= 0; i--) {
            for (var j = right.Count - 1; j >= 0; j--) {
                lengths[i, j] = string.Equals(Text(before, left[i]), Text(after, right[j]), StringComparison.Ordinal)
                    ? lengths[i + 1, j + 1] + 1
                    : Math.Max(lengths[i + 1, j], lengths[i, j + 1]);
            }
        }

        var x = 0;
        var y = 0;
        var pendingLeft = -1;
        var pendingRight = -1;

        while (x < left.Count || y < right.Count) {
            var matched = x < left.Count
                && y < right.Count
                && string.Equals(Text(before, left[x]), Text(after, right[y]), StringComparison.Ordinal);

            if (matched) {
                if (pendingLeft >= 0) {
                    yield return new Hunk(left[pendingLeft].Start, left[x].Start, right[pendingRight].Start,
                        right[y].Start);
                    pendingLeft = -1;
                    pendingRight = -1;
                }

                x++;
                y++;
                continue;
            }

            if (pendingLeft < 0) {
                pendingLeft = x;
                pendingRight = y;
            }

            if (y < right.Count && (x == left.Count || lengths[x, y + 1] >= lengths[x + 1, y])) {
                y++;
            } else {
                x++;
            }
        }

        if (pendingLeft >= 0) {
            var beforeStart = pendingLeft < left.Count ? left[pendingLeft].Start : End(left, before.Length);
            var afterStart = pendingRight < right.Count ? right[pendingRight].Start : End(right, after.Length);
            yield return new Hunk(beforeStart, End(left, beforeStart), afterStart, End(right, afterStart));
        }
    }

    /// <summary>⚠ Four million cells — a 2 000 × 2 000 window, well past any real file.</summary>
    const long TableLimit = 4_000_000;

    static int Start(List<Line> lines, int fallback) => lines.Count > 0 ? lines[0].Start : fallback;

    static int End(List<Line> lines, int fallback) => lines.Count > 0 ? lines[^1].End : fallback;

    /// <summary>
    /// ⚠ Recovers character precision inside a line-shaped hunk. Without it, changing
    /// <c>String</c> to <c>string</c> reports the whole line as replaced, and a reviewer reading
    /// <c>arrange --check</c> cannot see which token moved.
    /// </summary>
    static void Add(List<TextEdit> edits, string before, string after, Hunk hunk) {
        var beforeLength = hunk.BeforeEnd - hunk.BeforeStart;
        var afterLength = hunk.AfterEnd - hunk.AfterStart;
        if (beforeLength == afterLength
            && string.CompareOrdinal(before, hunk.BeforeStart, after, hunk.AfterStart, beforeLength) == 0) {
            return;
        }

        var prefix = 0;
        var max = Math.Min(beforeLength, afterLength);
        while (prefix < max && before[hunk.BeforeStart + prefix] == after[hunk.AfterStart + prefix]) {
            prefix++;
        }

        var suffix = 0;
        while (suffix < max - prefix && before[hunk.BeforeEnd - 1 - suffix] == after[hunk.AfterEnd - 1 - suffix]) {
            suffix++;
        }

        edits.Add(
            new TextEdit(
                SourceSpan.FromBounds(hunk.BeforeStart + prefix, hunk.BeforeEnd - suffix),
                after[(hunk.AfterStart + prefix)..(hunk.AfterEnd - suffix)]
            )
        );
    }
}
