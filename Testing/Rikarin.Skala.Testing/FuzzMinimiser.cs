namespace Rikarin.Skala.Testing;

/// <summary>How much work a minimisation is allowed to do before it hands back what it has.</summary>
public sealed class MinimiseBudget(int evaluations) {
    int remaining = evaluations;

    public int Used { get; private set; }

    public bool Exhausted => remaining <= 0;

    public bool Take() {
        if (remaining <= 0) {
            return false;
        }

        remaining--;
        Used++;
        return true;
    }
}

/// <summary>
/// Delta debugging, from docs/plan/12 § "Corpus expansion".
/// </summary>
/// <remarks>
/// ⚠ "Any crash, non-idempotent case or token-equivalence failure is minimised (delta-debugging on
/// the input) and committed to <c>corpus/pathological/</c>. The corpus only grows." The minimiser is
/// the half of that sentence that decides whether the other half is worth doing: a fuzz failure
/// arrives as a 400-line file with nineteen mutations applied to it, and a 400-line corpus entry
/// documents nothing — nobody can tell which of its lines is the bug, the fixture is unreadable, and
/// the next person to touch the area cannot tell whether their change fixed it or moved it.
/// <para>
/// ⚠ The predicate is "this input still fails <b>the same property</b>", not "this input fails
/// something". Delta debugging on the weaker predicate slides: a non-idempotency shrinks into an
/// unrelated token-equivalence failure and the committed file pins the wrong bug.
/// </para>
/// </remarks>
public static class FuzzMinimiser {
    /// <summary>
    /// Shrinks <paramref name="source"/> while <paramref name="stillFails"/> holds.
    /// </summary>
    /// <remarks>
    /// Classic ddmin over lines, then a greedy per-line pass, then a greedy shortening of the long
    /// identifiers and string literals the mutation catalogue's <c>widen-identifier</c> leaves
    /// behind. ⚠ Lines rather than characters at the top level: a C# file that has had half of a
    /// token deleted stops parsing, the property stops failing for the reason it was failing, and
    /// ddmin spends its whole budget on candidates that are rejected for the wrong reason.
    /// </remarks>
    public static string Minimise(string source, Func<string, bool> stillFails, MinimiseBudget budget) {
        var lines = Split(source);
        if (lines.Count == 0 || !stillFails(source)) {
            return source;
        }

        bool Test(List<string> candidate) =>
            candidate.Count > 0 && budget.Take() && stillFails(string.Join("\n", candidate));

        lines = Ddmin(lines, Test);
        lines = Greedy(lines, Test);
        var text = string.Join("\n", lines);
        return Narrow(text, candidate => budget.Take() && stillFails(candidate));
    }

    static List<string> Split(string source) => [.. source.ReplaceLineEndings("\n").Split('\n')];

    /// <summary>ddmin — Zeller and Hildebrandt, "Simplifying and Isolating Failure-Inducing Input".</summary>
    static List<string> Ddmin(List<string> lines, Func<List<string>, bool> test) {
        var granularity = 2;
        while (lines.Count >= 2) {
            var size = Math.Max(1, lines.Count / granularity);
            var reduced = false;

            // First the complements: removing a chunk is the big win and is tried before removing
            // everything else, because a failure usually needs a little context rather than none.
            for (var start = 0; start < lines.Count; start += size) {
                var complement = new List<string>(lines.Count);
                complement.AddRange(lines.Take(start));
                complement.AddRange(lines.Skip(start + size));
                if (complement.Count > 0 && complement.Count < lines.Count && test(complement)) {
                    lines = complement;
                    granularity = Math.Max(granularity - 1, 2);
                    reduced = true;
                    break;
                }
            }

            if (!reduced) {
                for (var start = 0; start < lines.Count; start += size) {
                    var chunk = lines.Skip(start).Take(size).ToList();
                    if (chunk.Count < lines.Count && test(chunk)) {
                        lines = chunk;
                        granularity = 2;
                        reduced = true;
                        break;
                    }
                }
            }

            if (reduced) {
                continue;
            }

            if (granularity >= lines.Count) {
                break;
            }

            granularity = Math.Min(granularity * 2, lines.Count);
        }

        return lines;
    }

    /// <summary>One more pass, one line at a time. ddmin's chunking leaves single lines behind.</summary>
    static List<string> Greedy(List<string> lines, Func<List<string>, bool> test) {
        var index = 0;
        while (index < lines.Count && lines.Count > 1) {
            var candidate = new List<string>(lines);
            candidate.RemoveAt(index);
            if (test(candidate)) {
                lines = candidate;
                continue;
            }

            index++;
        }

        return lines;
    }

    /// <summary>
    /// Shortens the long runs a mutation left behind, without changing the line structure.
    /// </summary>
    /// <remarks>
    /// ⚠ <c>widen-identifier</c> is the mutation most likely to produce a finding, because it is the
    /// only one that changes a line's width and the fitting engine's every decision is a function of
    /// width. It is also the one that leaves a 40-character name in the minimised file, where the
    /// interesting fact is usually "the line was two characters too long" rather than "the name was
    /// <c>value_wwwwwwwwwwwwwwwwwwwwwwww</c>". Halving the runs while the failure survives keeps the
    /// width that matters and drops the noise around it.
    /// </remarks>
    static string Narrow(string text, Func<string, bool> stillFails) {
        var runs = new[] { 'w', 's', ' ' };
        var changed = true;
        while (changed) {
            changed = false;
            foreach (var character in runs) {
                var index = 0;
                while (index < text.Length) {
                    var start = text.IndexOf(character, index);
                    if (start < 0) {
                        break;
                    }

                    var end = start;
                    while (end < text.Length && text[end] == character) {
                        end++;
                    }

                    var length = end - start;
                    if (length < 4) {
                        index = end + 1;
                        continue;
                    }

                    var candidate = text.Remove(start, length / 2);
                    if (stillFails(candidate)) {
                        text = candidate;
                        changed = true;
                        index = start;
                        continue;
                    }

                    index = end + 1;
                }
            }
        }

        return text;
    }
}
