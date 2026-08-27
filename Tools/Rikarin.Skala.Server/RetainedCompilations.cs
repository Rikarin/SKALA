using System.Collections.Concurrent;

namespace Rikarin.Skala.Server;

/// <summary>
///     At most four compilations, the oldest evicted. docs/plan/13 § "Memory".
/// </summary>
/// <remarks>
///     ⚠ <b>Four, and the number is not arbitrary.</b> "A Vixen-sized compilation with references is
///     200–400 MB" (doc 13), so four is 0.8–1.6 GB and five is over the daemon's whole RSS budget. The
///     bound is on <em>count</em> rather than bytes here — unlike the tree cache, whose bound is bytes —
///     because a compilation's retained size is dominated by metadata references that cannot be measured
///     without walking them, and four is small enough that the worst case is still inside the budget.
///     <para>
///         ⚠ <b>What this is for, stated honestly.</b> doc 13's analysis section says the 5 s warm `check`
///         budget "needs the *compilation* cached, not the diagnostics, which is a daemon that holds
///         <c>CSharpCompilation</c> objects across invocations" — and that daemon is still unbuilt. The
///         daemon serves <c>format</c> and nothing else, so nothing populates this store in production
///         today. It exists now, with its bound and its eviction and its tests, because the memory policy
///         has to be able to drop compilations before it exits, and a policy whose second step is a
///         no-op is a policy that has never been run. When `check` moves into the daemon it gets a bound
///         that already works instead of one written under time pressure afterwards.
///     </para>
///     <para>
///         The value is <see cref="object" /> rather than <c>CSharpCompilation</c> so that this project does
///         not acquire a Roslyn reference for a store that does not inspect what it holds.
///     </para>
/// </remarks>
public sealed class RetainedCompilations {
    readonly ConcurrentDictionary<string, Entry> _entries = new(StringComparer.Ordinal);
    readonly Lock _eviction = new();
    long _tick;

    public int Capacity { get; init; } = 4;

    public int Held => _entries.Count;

    public long Evictions { get; private set; }

    public bool TryGet(string key, out object? value) {
        if (_entries.TryGetValue(key, out var entry)) {
            entry.Touch(Interlocked.Increment(ref _tick));
            value = entry.Value;
            return true;
        }

        value = null;
        return false;
    }

    public void Put(string key, object value) {
        _entries[key] = new Entry(value, Interlocked.Increment(ref _tick));
        Evict();
    }

    void Evict() {
        if (_entries.Count <= Capacity) {
            return;
        }

        // ⚠ One evictor at a time. Without this every thread that overflows the bound computes the
        // same ordering and they race to evict each other's entries, which on a four-entry store
        // empties it.
        lock (_eviction) {
            while (_entries.Count > Capacity) {
                var oldest = default(KeyValuePair<string, Entry>);
                var oldestTick = long.MaxValue;
                foreach (var candidate in _entries) {
                    var tick = candidate.Value.LastUsed;
                    if (tick < oldestTick) {
                        oldestTick = tick;
                        oldest = candidate;
                    }
                }

                if (oldest.Key is null || !_entries.TryRemove(oldest.Key, out _)) {
                    return;
                }

                Evictions++;
            }
        }
    }

    public void Clear() => _entries.Clear();

    sealed class Entry(object value, long tick) {
        public object Value { get; } = value;

        public long LastUsed => Interlocked.Read(ref _lastUsed);

        long _lastUsed = tick;

        public void Touch(long tick) => Interlocked.Exchange(ref _lastUsed, tick);
    }
}
