/// <summary>A store.</summary>
public abstract class Store {
    /// <summary>Removes every entry.</summary>
    public abstract void Clear();
}

/// <summary>A memory store.</summary>
public sealed class MemoryStore : Store {
    /// <inheritdoc />
    public override void Clear() { }
}
