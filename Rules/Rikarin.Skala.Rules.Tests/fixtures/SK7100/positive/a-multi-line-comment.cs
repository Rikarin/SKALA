public abstract class Store {
    /// <summary>
    ///     Removes every entry, leaving the store usable.
    /// </summary>
    public abstract void Clear();
}

// Indented differently and split across the same lines: the normaliser strips the markers and
// collapses the whitespace, so the two compare equal.
public sealed class MemoryStore : Store {
    /// <summary>
    ///     Removes every entry, leaving the store usable.
    /// </summary>
    public override void Clear() { }
}
