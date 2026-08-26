using System.Collections.Immutable;

namespace Rikarin.Skala.Core.Configuration;

/// <summary>
/// The <c>.editorconfig</c> files that apply to one path, outermost first, stopping at the first
/// <c>root = true</c> walking upwards.
/// </summary>
public sealed class EditorConfigChain {
    EditorConfigChain(string sourcePath, ImmutableArray<EditorConfigDocument> documents, bool stoppedAtRoot) {
        SourcePath = sourcePath;
        Documents = documents;
        StoppedAtRoot = stoppedAtRoot;
    }

    public string SourcePath { get; }

    /// <summary>Outermost first, so that a later document overrides an earlier one.</summary>
    public ImmutableArray<EditorConfigDocument> Documents { get; }

    /// <summary>False when the walk ran out of directories before finding <c>root = true</c>.</summary>
    public bool StoppedAtRoot { get; }

    public static EditorConfigChain For(string sourcePath) {
        var full = Path.GetFullPath(sourcePath);
        var directory = Directory.Exists(full) ? full : Path.GetDirectoryName(full);
        var found = new List<EditorConfigDocument>();
        var stoppedAtRoot = false;

        while (directory is not null) {
            var candidate = Path.Combine(directory, EditorConfigDocument.FileName);
            if (File.Exists(candidate)) {
                var document = EditorConfigDocument.Load(candidate);
                found.Add(document);
                if (document.IsRoot) {
                    stoppedAtRoot = true;
                    break;
                }
            }

            var parent = Path.GetDirectoryName(directory);
            directory = string.Equals(parent, directory, StringComparison.Ordinal) ? null : parent;
        }

        found.Reverse();
        return new EditorConfigChain(full, [.. found], stoppedAtRoot);
    }

    /// <summary>A chain built from documents the caller already has. For tests and for `config diff`.</summary>
    public static EditorConfigChain Of(string sourcePath, params EditorConfigDocument[] documents) =>
        new(Path.GetFullPath(sourcePath), [.. documents], documents.Any(static d => d.IsRoot));

    /// <summary>
    /// The documents that live above <paramref name="repositoryRoot"/>. SK9002 exists because the
    /// Rider export has no <c>root = true</c>, so a stray <c>.editorconfig</c> in a home directory
    /// silently joins the configuration.
    /// </summary>
    public IEnumerable<EditorConfigDocument> Above(string repositoryRoot) {
        var root = Path.GetFullPath(repositoryRoot).TrimEnd(Path.DirectorySeparatorChar);
        foreach (var document in Documents) {
            var directory = document.Directory.TrimEnd(Path.DirectorySeparatorChar);
            if (!directory.StartsWith(root, StringComparison.Ordinal)) {
                yield return document;
            }
        }
    }
}
