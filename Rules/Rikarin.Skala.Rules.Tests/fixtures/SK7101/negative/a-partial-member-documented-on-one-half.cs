using System;

// ⚠ #397. A partial member is one member written twice, with one comment between its two halves, and
// the compiler takes whichever half carries it. Each member here is documented on exactly one half —
// the definition for some, the implementation for others — and every half without a comment of its
// own used to be reported as an undocumented member.
/// <summary>Holds the counters.</summary>
internal sealed partial class Counters {
    readonly int[] slots = new int[4];

    EventHandler? changed;

    /// <summary>Creates the counters.</summary>
    internal partial Counters(int seed);

    internal partial Counters(int seed) => slots[0] = seed;

    /// <summary>How many counters there are.</summary>
    internal partial int Count { get; }

    internal partial int Count => slots.Length;

    internal partial int this[int index] { get; }

    /// <summary>One counter.</summary>
    internal partial int this[int index] => slots[index];

    /// <summary>Raised after a counter moves.</summary>
    internal partial event EventHandler? Changed;

    internal partial event EventHandler? Changed {
        add => changed += value;
        remove => changed -= value;
    }

    /// <summary>Resets every counter.</summary>
    internal partial void Reset();

    internal partial void Reset() {
        Array.Clear(slots);
        changed?.Invoke(this, EventArgs.Empty);
    }
}
