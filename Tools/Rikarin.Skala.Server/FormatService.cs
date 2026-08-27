using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Core.Configuration;
using Rikarin.Skala.Formatting.CSharp;

namespace Rikarin.Skala.Server;

/// <summary>
///     The one implementation the daemon and the LSP server both call.
/// </summary>
/// <remarks>
///     ⚠ docs/plan/11's correctness rule:
///     <b>
///         every command must work identically with
///         <c>SKALA_NO_DAEMON=1</c>
///     </b>. The way to keep that true is for the warm path to be the cold path
///     plus a cache, and never a second implementation — so this holds results, not decisions.
///     <para>
///         The cache is keyed on the file's <em>content hash</em> together with the resolved configuration's
///         identity, not on the path and not on a timestamp. A daemon never watches the filesystem
///         (docs/plan/11: "It is asked; it does not observe"), so a path-keyed cache would serve a stale
///         answer for as long as nobody thought to invalidate it, and file watching is exactly where daemons
///         acquire their stale-state bugs.
///     </para>
/// </remarks>
public sealed class FormatService {
    readonly ConcurrentDictionary<string, Entry> _cache = new(StringComparer.Ordinal);
    readonly Lock _eviction = new();
    long _tick;
    long _bytes;

    /// <summary>
    ///     ⚠ <b>The bound is bytes, and it used to be entries.</b> docs/plan/13 § "Memory" says
    ///     "LRU by content hash, capped at 400 MB" and this held 4 096 entries of unbounded size: over a
    ///     corpus whose tail is "a handful of 20 000-line generated files" (doc 13 § "Parallelism"),
    ///     4 096 entries is several gigabytes, and the number that was supposed to be the memory bound
    ///     bore no relation to memory at all.
    /// </summary>
    public long CapacityBytes { get; init; } = 400L * 1024 * 1024;

    /// <summary>
    ///     ⚠ <b>And it is an LRU, which it also used to not be.</b> The old comment argued that
    ///     "an LRU needs a lock on the hot path to be an LRU at all", so it cleared the whole dictionary
    ///     on overflow — which throws away every hot entry along with the cold ones and gives a daemon
    ///     that periodically forgets the file the developer is editing. It does not need a lock: the hit
    ///     path stamps a monotonic tick with one interlocked write, and only the miss path — which is
    ///     already doing a full format — ever sorts or evicts.
    /// </summary>
    public int Held => _cache.Count;

    /// <summary>Approximate retained bytes. Read by <see cref="MemoryPolicy" /> and by `daemon status`.</summary>
    public long Bytes => Interlocked.Read(ref _bytes);

    public long Hits { get; private set; }

    public long Misses { get; private set; }

    public long Evictions { get; private set; }

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

            // The whole cost of being an LRU on the hot path: one interlocked increment and one
            // interlocked write.
            cached.Touch(Interlocked.Increment(ref _tick));
            return cached.Result;
        }

        Misses++;
        var result = CSharpFormatter.Format(path, source, options, crashRoot, preprocessorSymbols);

        var entry = new Entry(result, Interlocked.Increment(ref _tick), Weigh(key, source.Length, result));
        if (_cache.TryAdd(key, entry)) {
            Interlocked.Add(ref _bytes, entry.Bytes);
        }

        Trim(CapacityBytes);
        return result;
    }

    /// <summary>
    ///     Drops least-recently-used entries until the cache is under <paramref name="target" />.
    /// </summary>
    /// <remarks>
    ///     ⚠ Trims to 80 % of the target rather than to the target exactly. Evicting one entry per
    ///     insertion once the cache is full turns every subsequent miss into a full sort, and the sort
    ///     is O(n) over four thousand entries; the hysteresis makes eviction a rare batch instead of a
    ///     per-miss tax.
    /// </remarks>
    public void Trim(long target) {
        if (Bytes <= target) {
            return;
        }

        // ⚠ One evictor at a time, or several threads each compute the same ordering and evict each
        // other's choices until the cache is empty — the wholesale clear this replaced, by accident.
        lock (_eviction) {
            var low = (long)(target * 0.8);
            if (Bytes <= low) {
                return;
            }

            var ordered = _cache.ToArray();
            Array.Sort(ordered, static (left, right) => left.Value.LastUsed.CompareTo(right.Value.LastUsed));

            foreach (var candidate in ordered) {
                if (Bytes <= low) {
                    return;
                }

                if (_cache.TryRemove(candidate.Key, out var removed)) {
                    Interlocked.Add(ref _bytes, -removed.Bytes);
                    Evictions++;
                }
            }
        }
    }

    public void Clear() {
        lock (_eviction) {
            _cache.Clear();
            Interlocked.Exchange(ref _bytes, 0);
        }
    }

    /// <summary>
    ///     What one entry costs, approximately. ⚠ Approximate on purpose: an exact answer needs
    ///     <c>GC.GetAllocatedBytesForCurrentThread</c> around the format call, which measures garbage as
    ///     well as retention and would be wrong in the expensive direction. The dominant terms are the
    ///     two texts and the edit list, and two bytes per char is the CLR's string layout.
    /// </summary>
    static long Weigh(string key, int sourceLength, FormatResult result) =>
        (key.Length * 2L)
        + (sourceLength * 2L)
        + ((result.Formatted?.Length ?? 0) * 2L)
        + (result.Edits.Length * 64L)
        + 128L;

    /// <summary>
    ///     The identity of a formatting answer: the bytes in, and the configuration that applied.
    /// </summary>
    /// <remarks>
    ///     ⚠ The configuration's identity is every <c>.editorconfig</c> in the chain together with the
    ///     version stamp each carries, so a config edited under a running daemon changes the key rather
    ///     than needing an invalidation message. <see cref="ConfigurationCache" /> re-reads a document
    ///     whose <c>(mtime, length)</c> moved and hands out a new version with it.
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

    sealed class Entry(FormatResult result, long tick, long bytes) {
        public FormatResult Result { get; } = result;

        public long Bytes { get; } = bytes;

        public long LastUsed => Interlocked.Read(ref _lastUsed);

        long _lastUsed = tick;

        public void Touch(long tick) => Interlocked.Exchange(ref _lastUsed, tick);
    }
}
