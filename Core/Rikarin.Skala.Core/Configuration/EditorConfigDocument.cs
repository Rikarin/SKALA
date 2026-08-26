using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;

namespace Rikarin.Skala.Core.Configuration;

/// <summary>One <c>key = value</c> line, with the provenance <c>config explain</c> needs.</summary>
public sealed record EditorConfigAssignment(string Key, string Value, int Line, EditorConfigSection Section) {
    public string File => Section.Document.Path;

    public override string ToString() => $"{Key} = {Value} ({File}:{Line})";
}

/// <summary>
/// A <c>[glob]</c> section, or the preamble before the first one where <c>root = true</c> lives.
/// </summary>
public sealed class EditorConfigSection {
    readonly List<EditorConfigAssignment> _assignments = [];

    internal EditorConfigSection(EditorConfigDocument document, string? name, int line, int order) {
        Document = document;
        Name = name;
        Line = line;
        Order = order;
    }

    public EditorConfigDocument Document { get; }

    /// <summary>The glob, or null for the preamble.</summary>
    public string? Name { get; }

    public int Line { get; }

    /// <summary>Position in the file. Later sections win over earlier ones.</summary>
    public int Order { get; }

    public IReadOnlyList<EditorConfigAssignment> Assignments => _assignments;

    internal void Add(EditorConfigAssignment assignment) => _assignments.Add(assignment);
}

/// <summary>
/// One <c>.editorconfig</c>, parsed twice: once by Roslyn so that glob matching is the compiler's
/// own, and once here so that every value can name the file and line it came from.
/// </summary>
/// <remarks>
/// ⚠ Roslyn's <c>AnalyzerConfig</c> exposes only <c>Parse</c> publicly — its sections, its
/// <c>root</c> flag and its properties are internal — so provenance cannot come from it.
/// <see cref="SectionMatcher"/> is how the compiler's globbing is still the only globbing in play.
/// </remarks>
public sealed class EditorConfigDocument {
    public const string FileName = ".editorconfig";

    static int _nextVersion;

    EditorConfigDocument(string path, string text) {
        Path = path;
        Directory = System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(path)) ?? path;
        Text = text;
        Sections = Parse(this, text, out var isRoot);
        IsRoot = isRoot;
        Version = System.Threading.Interlocked.Increment(ref _nextVersion);
    }

    public string Path { get; }

    /// <summary>
    /// A process-unique stamp, handed out at construction.
    /// </summary>
    /// <remarks>
    /// ⚠ It exists so that <see cref="ConfigurationCache"/> can key a resolution on the documents it
    /// came from without hashing their text. A document that is re-read because the file changed is
    /// a different instance with a different version, so every cached answer derived from the old
    /// one is unreachable rather than stale.
    /// </remarks>
    public int Version { get; }

    public string Directory { get; }

    public string Text { get; }

    /// <summary><c>root = true</c> in the preamble. Stops the chain walk.</summary>
    public bool IsRoot { get; }

    public IReadOnlyList<EditorConfigSection> Sections { get; }

    public IEnumerable<EditorConfigAssignment> Assignments => Sections.SelectMany(static section => section.Assignments);

    public static EditorConfigDocument Load(string path) => new(path, File.ReadAllText(path));

    public static EditorConfigDocument FromText(string path, string text) => new(path, text);

    static ImmutableArray<EditorConfigSection> Parse(EditorConfigDocument document, string text, out bool isRoot) {
        isRoot = false;
        var sections = ImmutableArray.CreateBuilder<EditorConfigSection>();
        var current = new EditorConfigSection(document, null, 0, 0);
        sections.Add(current);

        var line = 0;
        foreach (var raw in SplitLines(text)) {
            line++;
            var trimmed = raw.Trim();
            if (trimmed.Length == 0 || trimmed[0] is '#' or ';') {
                continue;
            }

            if (trimmed[0] == '[' && trimmed[^1] == ']') {
                current = new EditorConfigSection(document, trimmed[1..^1], line, sections.Count);
                sections.Add(current);
                continue;
            }

            var separator = trimmed.IndexOf('=', StringComparison.Ordinal);
            if (separator < 0) {
                continue;
            }

            // EditorConfig folds key case; values keep theirs.
            var key = trimmed[..separator].Trim().ToLowerInvariant();
            var value = trimmed[(separator + 1)..].Trim();
            if (key.Length == 0) {
                continue;
            }

            current.Add(new EditorConfigAssignment(key, value, line, current));
            if (current.Name is null && key == "root" && value.Equals("true", StringComparison.OrdinalIgnoreCase)) {
                isRoot = true;
            }
        }

        return sections.ToImmutable();
    }

    static IEnumerable<string> SplitLines(string text) {
        var start = 0;
        for (var i = 0; i < text.Length; i++) {
            if (text[i] is not ('\n' or '\r')) {
                continue;
            }

            yield return text[start..i];
            if (text[i] == '\r' && i + 1 < text.Length && text[i + 1] == '\n') {
                i++;
            }

            start = i + 1;
        }

        if (start < text.Length) {
            yield return text[start..];
        }
    }

    [SuppressMessage("Design", "CA1024:Use properties where appropriate", Justification = "Reads the filesystem.")]
    public override string ToString() => Path;
}
