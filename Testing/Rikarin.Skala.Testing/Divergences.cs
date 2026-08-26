using System.Text.RegularExpressions;

namespace Rikarin.Skala.Testing;

/// <summary>One deliberate disagreement with the oracle, and the argument for it.</summary>
public sealed record DivergenceEntry(string Id, string Summary, IReadOnlyList<string> Options);

/// <summary>
/// The divergence register, read out of <c>docs/divergences.md</c>.
/// </summary>
/// <remarks>
/// ⚠ docs/plan/12 § "Where the oracle is wrong": a divergence is a decision, an unexplained
/// difference is a bug, and the harness cannot tell them apart without this file. The register is
/// the file, not a table in code, because the argument for each entry is the point and an argument
/// does not fit in an attribute.
/// </remarks>
public static class Divergences {
    public static string Path { get; } = System.IO.Path.Combine(Corpus.RepositoryRoot, "docs", "divergences.md");

    public static IReadOnlyList<DivergenceEntry> Register { get; } = Read();

    static List<DivergenceEntry> Read() {
        if (!File.Exists(Path)) {
            return [];
        }

        var entries = new List<DivergenceEntry>();
        string? id = null;
        string? summary = null;
        var options = new List<string>();

        foreach (var line in File.ReadLines(Path)) {
            var heading = Regex.Match(line, @"^##\s+(SK-DIV-\d{4})\s*—\s*(.+)$");
            if (heading.Success) {
                Flush(entries, ref id, ref summary, options);
                id = heading.Groups[1].Value;
                summary = heading.Groups[2].Value.Trim();
                continue;
            }

            if (id is not null && line.StartsWith("- options:", StringComparison.Ordinal)) {
                foreach (var option in line["- options:".Length..].Split(',', StringSplitOptions.RemoveEmptyEntries)) {
                    options.Add(option.Trim().Trim('`'));
                }
            }
        }

        Flush(entries, ref id, ref summary, options);
        return entries;
    }

    static void Flush(List<DivergenceEntry> entries, ref string? id, ref string? summary, List<string> options) {
        if (id is not null) {
            entries.Add(new DivergenceEntry(id, summary ?? string.Empty, [.. options]));
        }

        id = null;
        summary = null;
        options.Clear();
    }
}
