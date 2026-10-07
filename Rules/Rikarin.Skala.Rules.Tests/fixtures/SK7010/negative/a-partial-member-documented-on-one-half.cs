using System;

namespace Fixtures;

// ⚠ #397: the public-API half of the same defect. Each partial member is documented on exactly one
// half, and the compiler's documentation file carries that comment for the member; the half without
// one is not a second undocumented member.
/// <summary>Holds the counters.</summary>
public sealed partial class Counters {
    readonly int[] slots = new int[4];

    EventHandler? changed;

    /// <summary>Creates the counters.</summary>
    public partial Counters(int seed);

    public partial Counters(int seed) => slots[0] = seed;

    /// <summary>How many counters there are.</summary>
    public partial int Count { get; }

    public partial int Count => slots.Length;

    public partial int this[int index] { get; }

    /// <summary>One counter.</summary>
    public partial int this[int index] => slots[index];

    /// <summary>Raised after a counter moves.</summary>
    public partial event EventHandler? Changed;

    public partial event EventHandler? Changed {
        add => changed += value;
        remove => changed -= value;
    }

    /// <summary>Resets every counter.</summary>
    public partial void Reset();

    public partial void Reset() {
        Array.Clear(slots);
        changed?.Invoke(this, EventArgs.Empty);
    }
}
