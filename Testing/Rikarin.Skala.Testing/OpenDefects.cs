using System.Text.RegularExpressions;

namespace Rikarin.Skala.Testing;

/// <summary>One minimised fuzz finding that has not been fixed yet.</summary>
public sealed record OpenDefect(string Id, string Summary, string File, string Property, string Seed) {
    public string Path => System.IO.Path.Combine(OpenDefects.Root, File);

    public override string ToString() => Id + " (" + File + ")";
}

/// <summary>
/// The register in <c>Testing/corpus/pathological/open/register.md</c>.
/// </summary>
/// <remarks>
/// ⚠ Read out of a markdown file rather than declared in code, for the reason
/// <see cref="Divergences"/> is: the argument for each entry is the point, an argument does not fit
/// in an attribute, and the person who has to decide whether a defect is still worth having is
/// reading prose. The code here needs four fields; the file carries the case.
/// </remarks>
public static class OpenDefects {
    public static string Root { get; } =
        Path.Combine(Corpus.SetRoot(Corpus.Pathological), OpenDirectory);

    /// <summary>
    /// ⚠ Excluded from <see cref="Corpus.Files"/>. See the register for why.
    /// </summary>
    public const string OpenDirectory = "open";

    public static string RegisterPath { get; } = Path.Combine(Root, "register.md");

    public static IReadOnlyList<OpenDefect> Register { get; } = Read();

    /// <summary>The <c>.cs</c> files in the directory, which the register must account for exactly.</summary>
    public static IReadOnlyList<string> Files() =>
        Directory.Exists(Root)
            ? [
                .. Directory.EnumerateFiles(Root, "*.cs", SearchOption.TopDirectoryOnly)
                    .Select(Path.GetFileName)
                    .OfType<string>()
                    .Order(StringComparer.Ordinal)
            ]
            : [];

    static List<OpenDefect> Read() {
        var entries = new List<OpenDefect>();
        if (!File.Exists(RegisterPath)) {
            return entries;
        }

        string? id = null;
        string? summary = null;
        string? file = null;
        string? property = null;
        string? seed = null;

        foreach (var line in File.ReadLines(RegisterPath)) {
            var heading = Regex.Match(line, @"^##\s+(SK-FUZZ-\d{4})\s*—\s*(.+)$");
            if (heading.Success) {
                Flush(entries, ref id, ref summary, ref file, ref property, ref seed);
                id = heading.Groups[1].Value;
                summary = heading.Groups[2].Value.Trim();
                continue;
            }

            if (id is null) {
                continue;
            }

            var field = Regex.Match(line, @"^-\s+(file|property|seed):\s*`?([^`]+)`?\s*$");
            if (!field.Success) {
                continue;
            }

            switch (field.Groups[1].Value) {
                case "file":
                    file = field.Groups[2].Value.Trim();
                    break;
                case "property":
                    property = field.Groups[2].Value.Trim();
                    break;
                default:
                    seed = field.Groups[2].Value.Trim();
                    break;
            }
        }

        Flush(entries, ref id, ref summary, ref file, ref property, ref seed);
        return entries;
    }

    static void Flush(
        List<OpenDefect> entries,
        ref string? id,
        ref string? summary,
        ref string? file,
        ref string? property,
        ref string? seed
    ) {
        if (id is not null && file is not null && property is not null) {
            entries.Add(new OpenDefect(id, summary ?? string.Empty, file, property, seed ?? string.Empty));
        }

        id = null;
        summary = null;
        file = null;
        property = null;
        seed = null;
    }
}
