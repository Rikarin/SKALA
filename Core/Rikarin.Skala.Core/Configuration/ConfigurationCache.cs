using System.Collections.Concurrent;
using System.Text;
using Rikarin.Skala.Options;

namespace Rikarin.Skala.Core.Configuration;

/// <summary>
///     Process-wide memoisation of everything between a path and its <see cref="FormattingOptions" />.
/// </summary>
/// <remarks>
///     ⚠ Not a micro-optimisation. Resolving one file's options re-reads every <c>.editorconfig</c>
///     above it, re-parses ~900 assignment lines, and then allocates two 483-element arrays and 483
///     <see cref="ResolvedOption" /> records — per file. Over Vixen's 4 708 files that is 4.3 M line
///     parses and 2.3 M records for an answer that is the same for nearly every file in the tree.
///     <para>
///         ⚠ The resolution key is the <em>matched sections</em>, not the directory. An
///         <c>.editorconfig</c> may carry <c>[*.Designer.cs]</c> or <c>[Program.cs]</c>, so two files in one
///         directory can resolve differently, and keying on the directory would hand one file the other's
///         options — the category of bug that makes output depend on the order the tree was walked in.
///         Sections are matched by <see cref="SectionMatcher" />, which is the compiler's own globbing
///         (ADR-001); only the result of that matching is cached.
///     </para>
///     <para>
///         ⚠ The chain walk itself is <em>not</em> cached, deliberately. It is a handful of
///         <c>File.Exists</c> calls per file, and caching it by directory would not notice an
///         <c>.editorconfig</c> that appeared in between — a stale answer that a daemon would keep serving.
///         Freshness of each document is <c>(mtime, length)</c>, checked on every load, so a config edited
///         under a running daemon is picked up on the next file. A document that reloads gets a new
///         <see cref="EditorConfigDocument.Version" />, which is part of the resolution key, so every
///         resolution derived from it is invalidated with it.
///     </para>
/// </remarks>
public static class ConfigurationCache {
    static readonly ConcurrentDictionary<string, CachedDocument> Documents = new(StringComparer.Ordinal);
    static readonly ConcurrentDictionary<string, FormattingOptions> Resolutions = new(StringComparer.Ordinal);

    /// <summary>
    ///     Whether the cache is consulted at all. <c>SKALA_NO_CACHE=1</c> and <c>--no-cache</c> turn it off.
    /// </summary>
    public static bool Enabled { get; set; } =
        !string.Equals(Environment.GetEnvironmentVariable("SKALA_NO_CACHE"), "1", StringComparison.Ordinal);

    /// <summary>Loads a document, reusing the parse when the file has not changed underneath it.</summary>
    public static EditorConfigDocument Load(string path) {
        if (!Enabled) {
            return EditorConfigDocument.Load(path);
        }

        long ticks;
        long length;
        try {
            var info = new FileInfo(path);
            ticks = info.LastWriteTimeUtc.Ticks;
            length = info.Length;
        } catch (IOException) {
            return EditorConfigDocument.Load(path);
        }

        if (Documents.TryGetValue(path, out var cached) && cached.Ticks == ticks && cached.Length == length) {
            return cached.Document;
        }

        var document = EditorConfigDocument.Load(path);
        Documents[path] = new CachedDocument(ticks, length, document);
        return document;
    }

    /// <summary>The formatting options for one file, memoised on the sections that actually matched it.</summary>
    public static FormattingOptions Options(
        EditorConfigChain chain,
        IReadOnlyList<KeyValuePair<string, string>>? overrides
    ) {
        if (!Enabled) {
            return OptionResolver.Resolve(chain, overrides).Options;
        }

        var key = SignatureOf(chain, overrides);
        if (Resolutions.TryGetValue(key, out var options)) {
            return options;
        }

        var resolved = OptionResolver.Resolve(chain, overrides).Options;
        Resolutions[key] = resolved;
        return resolved;
    }

    /// <summary>Forgets everything. <c>skala cache clear</c>, and every test that rewrites a config.</summary>
    public static void Clear() {
        Documents.Clear();
        Resolutions.Clear();
    }

    /// <summary>The identity of a resolution: which sections of which documents apply, in order.</summary>
    static string SignatureOf(EditorConfigChain chain, IReadOnlyList<KeyValuePair<string, string>>? overrides) {
        var builder = new StringBuilder();
        foreach (var document in chain.Documents) {
            builder.Append(document.Path).Append('@').Append(document.Version).Append(':');
            foreach (var section in document.Sections) {
                if (section.Name is not null && SectionMatcher.Matches(section, chain.SourcePath)) {
                    builder.Append(section.Order).Append(',');
                }
            }

            builder.Append(';');
        }

        if (overrides is { Count: > 0 }) {
            builder.Append('|');
            foreach (var (name, value) in overrides) {
                builder.Append(name).Append('=').Append(value).Append(';');
            }
        }

        return builder.ToString();
    }

    readonly record struct CachedDocument(long Ticks, long Length, EditorConfigDocument Document);
}
