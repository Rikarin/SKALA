using System;
using System.Collections.Generic;

// ⚠ The rule's stated position. `NotSupportedException` is a permanent statement about what this
// type offers, not a note that the work is unfinished, so nobody owes an implementation.
public sealed class ReadOnlyBag {
    readonly List<int> items = new();

    public void Add(int value) => throw new NotSupportedException("the bag is read-only");

    public IReadOnlyList<int> Items => items;
}
