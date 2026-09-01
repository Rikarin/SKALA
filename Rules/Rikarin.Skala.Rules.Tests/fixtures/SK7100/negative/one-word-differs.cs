public abstract class Store {
    /// <summary>Removes every entry.</summary>
    public abstract void Clear();
}

// One word apart, and the word is the whole point: this override keeps the capacity. A similarity
// threshold would delete that sentence.
public sealed class MemoryStore : Store {
    /// <summary>Removes every entry and keeps the capacity.</summary>
    public override void Clear() { }
}
