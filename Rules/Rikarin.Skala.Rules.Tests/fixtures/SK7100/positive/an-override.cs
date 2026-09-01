public abstract class Store {
    /// <summary>Removes every entry.</summary>
    public abstract void Clear();
}

public sealed class MemoryStore : Store {
    /// <summary>Removes every entry.</summary>
    public override void Clear() { }
}
