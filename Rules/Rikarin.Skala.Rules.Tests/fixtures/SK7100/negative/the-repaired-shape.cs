public abstract class Store {
    /// <summary>Removes every entry.</summary>
    public abstract void Clear();
}

public sealed class MemoryStore : Store {
    /// <inheritdoc />
    public override void Clear() { }
}
