using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Core.Configuration;
using Rikarin.Skala.Formatting.CSharp;

namespace Rikarin.Skala.Server;

/// <summary>
/// The one implementation the daemon and the LSP server both call.
/// </summary>
/// <remarks>
/// ⚠ docs/plan/11's correctness rule: <b>every command must work identically with
/// <c>SKALA_NO_DAEMON=1</c></b>. The way to keep that true is for the warm path to be the cold path
/// plus a cache, and never a second implementation — so this holds results, not decisions.
/// <para>
/// The cache is keyed on the file's <em>content hash</em> together with the resolved configuration's
/// identity, not on the path and not on a timestamp. A daemon never watches the filesystem
/// (docs/plan/11: "It is asked; it does not observe"), so a path-keyed cache would serve a stale
/// answer for as long as nobody thought to invalidate it, and file watching is exactly where daemons
/// acquire their stale-state bugs.
/// </para>
/// </remarks>
public sealed class FormatService {
    readonly ConcurrentDictionary<string, Entry> _cache = new(StringComparer.Ordinal);

    /// <summary>⚠ A bound, because a daemon that holds everything is a daemon that swaps.</summary>
    public int Capacity { get; init; } = 4096;

    public int Held => _cache.Count;

    public long Hits { get; private set; }

    public long Misses { get; private set; }

    /// <summary>Formats a file, from disk or from the text the client already has.</summary>
    public FormatResult Format(
        string path,
        string? text,
        IReadOnlyList<KeyValuePair<string, string>>? overrides,
        string? crashRoot,
        IReadOnlyList<string>? preprocessorSymbols = null
    ) {
        var source = text is null ? CSharpFormatter.Read(path) : SourceText.From(text, Encoding.UTF8);
        var chain = EditorConfigChain.For(path);
        var options = ConfigurationCache.Options(chain, overrides);
        var key = KeyOf(source, chain, overrides, preprocessorSymbols);

        if (_cache.TryGetValue(key, out var cached)) {
            Hits++;
            return cached.Result;
        }

        Misses++;
        var result = CSharpFormatter.Format(path, source, options, crashRoot, preprocessorSymbols);

        // ⚠ Evict wholesale rather than by age. An LRU needs a lock on the hot path to be an LRU at
        // all, and the thing being protected is a bound on memory, not a hit rate.
        if (_cache.Count >= Capacity) {
            _cache.Clear();
        }

        _cache[key] = new Entry(result);
        return result;
    }

    /// <summary>
    /// The identity of a formatting answer: the bytes in, and the configuration that applied.
    /// </summary>
    /// <remarks>
    /// ⚠ The configuration's identity is every <c>.editorconfig</c> in the chain together with the
    /// version stamp each carries, so a config edited under a running daemon changes the key rather
    /// than needing an invalidation message. <see cref="ConfigurationCache"/> re-reads a document
    /// whose <c>(mtime, length)</c> moved and hands out a new version with it.
    /// </remarks>
    static string KeyOf(
        SourceText source,
        EditorConfigChain chain,
        IReadOnlyList<KeyValuePair<string, string>>? overrides,
        IReadOnlyList<string>? preprocessorSymbols
    ) {
        var builder = new StringBuilder();
        builder.Append(Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(source.ToString()))));
        builder.Append('|').Append(chain.SourcePath);
        foreach (var document in chain.Documents) {
            builder.Append('|').Append(document.Path).Append('@').Append(document.Version);
        }

        if (overrides is { Count: > 0 }) {
            foreach (var (key, value) in overrides) {
                builder.Append('|').Append(key).Append('=').Append(value);
            }
        }

        // ⚠ Part of the key, and it has to be: which branch of a `#if` is disabled text is a parse
        // decision, so the same bytes under two symbol sets are two different formatted files.
        if (preprocessorSymbols is { Count: > 0 }) {
            foreach (var symbol in preprocessorSymbols.Order(StringComparer.Ordinal)) {
                builder.Append("|#").Append(symbol);
            }
        }

        return builder.ToString();
    }

    sealed record Entry(FormatResult Result);
}
