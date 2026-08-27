using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Formatting.CSharp;

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
        if (source.Length == 0 || !stillFails(source)) {
            return source;
        }

        bool Text(string candidate) => candidate.Length > 0 && budget.Take() && stillFails(candidate);

        bool Lines(List<string> candidate) => candidate.Count > 0 && Text(string.Join("\n", candidate));

        // ⚠ Syntax first, lines second, and the order is the difference between a 2 400-character
        // corpus entry and a 200-character one. Removing an arbitrary *line* from C# almost always
        // unbalances a brace, the candidate stops parsing, the property stops failing for the reason
        // it was failing, and ddmin spends its whole budget being told no. Removing a whole member
        // or a whole statement leaves a file that still parses, so nearly every candidate is a real
        // question. Measured on this fuzzer's first idempotency finding: lines alone took 2 494
        // characters to 2 433; syntax first takes it to under 200.
        var text = Nodes(source, Text, budget);
        text = string.Join("\n", Greedy(Split(text), Lines));
        text = string.Join("\n", Ddmin(Split(text), Lines));
        text = string.Join("\n", Greedy(Split(text), Lines));
        text = Narrow(text, Text);

        // ⚠ Verified once more before it is handed back, outside the budget. Every pass above
        // accepts only candidates the predicate said yes to, so this should be unreachable — and it
        // was reachable, because a lossy split made one pass hand back a string it had never tested.
        // A minimiser that returns an artefact which does not fail is worse than one that returns
        // the original: the corpus entry it produces pins nothing and looks as though it does.
        return stillFails(text) ? text : source;
    }

    /// <summary>
    /// Removes whole members and whole statements, largest first, while the failure survives.
    /// </summary>
    /// <remarks>
    /// ⚠ Largest first and re-parsed after every accepted removal. Deleting the outermost node that
    /// can go removes everything under it in one evaluation, and re-parsing keeps every subsequent
    /// span valid — a stale span list after an accepted edit is how a reducer produces a file that
    /// is smaller and is not the failure any more.
    /// </remarks>
    static string Nodes(string source, Func<string, bool> stillFails, MinimiseBudget budget) {
        var changed = true;
        while (changed && !budget.Exhausted) {
            changed = false;
            var tree = CSharpSyntaxTree.ParseText(SourceText.From(source), CSharpFormatter.ParseOptions);
            var removable = tree.GetRoot()
                .DescendantNodes()
                .Where(static node => node is MemberDeclarationSyntax
                        or StatementSyntax
                        or UsingDirectiveSyntax
                        or AttributeListSyntax
                        or SwitchSectionSyntax
                        or CatchClauseSyntax
                        or FinallyClauseSyntax
                )
                .Where(static node => node is not BlockSyntax)
                .OrderByDescending(static node => node.FullSpan.Length)
                .ToList();

            foreach (var node in removable) {
                var span = node.FullSpan;
                if (span.Length == 0 || span.End > source.Length) {
                    continue;
                }

                var candidate = source.Remove(span.Start, span.Length);
                if (!stillFails(candidate)) {
                    continue;
                }

                source = candidate;
                changed = true;
                break;
            }
        }

        return source;
    }

    /// <summary>
    /// Splits on <c>\n</c> only, leaving any <c>\r</c> attached to the line it ends.
    /// </summary>
    /// <remarks>
    /// ⚠ Not <see cref="string.ReplaceLineEndings()"/>, and this was a real defect rather than a
    /// stylistic preference. Normalising here makes the split/join round trip *lossy*: a pass that
    /// removes no line still hands back a different string, one that was never put to the predicate,
    /// and the minimiser silently returns an artefact that does not fail. It cost this fuzzer its
    /// first real finding — a mixed-line-ending case whose whole content was the <c>\r</c> that the
    /// normalisation deleted on the way past.
    /// </remarks>
    static List<string> Split(string source) => [.. source.Split('\n')];

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
