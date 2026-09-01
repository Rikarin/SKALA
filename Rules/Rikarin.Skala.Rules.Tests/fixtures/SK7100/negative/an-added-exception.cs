using System;

public abstract class Store {
    /// <summary>Removes every entry.</summary>
    public abstract void Clear();
}

// The summary is identical and the member says something more. Replacing the block would delete the
// `<exception>`, which is the only place that fact is written down.
public sealed class MemoryStore : Store {
    /// <summary>Removes every entry.</summary>
    /// <exception cref="InvalidOperationException">The store is frozen.</exception>
    public override void Clear() { }
}
