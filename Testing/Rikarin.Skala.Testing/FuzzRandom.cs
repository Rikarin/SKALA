using System.Globalization;

namespace Rikarin.Skala.Testing;

/// <summary>
/// The fuzzer's only source of randomness, and the reason a failure is replayable.
/// </summary>
/// <remarks>
/// ⚠ Not <see cref="Random"/>, and the reason is docs/plan/12 § "Fuzzing": "seeded and
/// reproducible". <see cref="Random"/>'s sequence for a given seed is an implementation detail that
/// .NET has changed before and is free to change again, so a seed recorded in a nightly log this
/// year would replay a different stream next year — which makes the recorded seed a decoration
/// rather than a reproduction. SplitMix64 is eleven lines, is specified by its constants, and will
/// produce the same stream on every runtime and every platform forever. The seed is the input.
/// <para>
/// ⚠ Nothing in the fuzzer may read the clock in a way that reaches a case. The wall clock bounds
/// the *loop* — doc 12 asks for a time budget rather than a case count — but the case index and the
/// seed determine everything inside a case, so <c>fuzz --replay=&lt;seed&gt;</c> reconstructs it
/// exactly regardless of how long the run that found it lasted.
/// </para>
/// </remarks>
public sealed class FuzzRandom {
    ulong state;

    public FuzzRandom(ulong seed) {
        Seed = seed;
        state = seed;
    }

    /// <summary>The seed this stream started from — what a failure report prints.</summary>
    public ulong Seed { get; }

    /// <summary>
    /// Derives a sub-seed from a seed and an index, without consuming the stream.
    /// </summary>
    /// <remarks>
    /// ⚠ This is what makes a *case* replayable rather than a *run*. A run draws case seeds by
    /// <c>Derive(root, i)</c>, so case 4 719 of a twelve-hour nightly can be replayed on its own in
    /// a second, and a run that stopped at a time budget still names every case it executed.
    /// </remarks>
    public static ulong Derive(ulong seed, long index) => Mix(seed + 0x9E3779B97F4A7C15UL * (ulong)index);

    /// <summary>Parses a seed written as decimal or as <c>0x…</c>.</summary>
    public static ulong Parse(string text) =>
        text.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
            ? ulong.Parse(text[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture)
            : ulong.Parse(text, CultureInfo.InvariantCulture);

    public static string Format(ulong seed) => seed.ToString(CultureInfo.InvariantCulture);

    public ulong NextULong() {
        state += 0x9E3779B97F4A7C15UL;
        return Mix(state);
    }

    /// <summary>Uniform in <c>[0, bound)</c>. Debiased by rejection, so the stream is not skewed.</summary>
    public int Next(int bound) {
        if (bound <= 1) {
            return 0;
        }

        var limit = ulong.MaxValue - ulong.MaxValue % (ulong)bound;
        ulong draw;
        do {
            draw = NextULong();
        } while (draw >= limit);

        return (int)(draw % (ulong)bound);
    }

    public int Next(int low, int high) => low + Next(high - low);

    public double NextDouble() => (NextULong() >> 11) * (1.0 / (1UL << 53));

    public bool Chance(double probability) => NextDouble() < probability;

    public T Pick<T>(IReadOnlyList<T> items) => items[Next(items.Count)];

    /// <summary>Picks by weight; <paramref name="weights"/> is parallel to <paramref name="items"/>.</summary>
    public T Pick<T>(IReadOnlyList<T> items, IReadOnlyList<int> weights) {
        var total = 0;
        for (var i = 0; i < weights.Count; i++) {
            total += weights[i];
        }

        var draw = Next(total);
        for (var i = 0; i < items.Count; i++) {
            draw -= weights[i];
            if (draw < 0) {
                return items[i];
            }
        }

        return items[^1];
    }

    /// <summary>Fisher–Yates, in place.</summary>
    public void Shuffle<T>(IList<T> items) {
        for (var i = items.Count - 1; i > 0; i--) {
            var j = Next(i + 1);
            (items[i], items[j]) = (items[j], items[i]);
        }
    }

    static ulong Mix(ulong z) {
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        return z ^ (z >> 31);
    }
}
