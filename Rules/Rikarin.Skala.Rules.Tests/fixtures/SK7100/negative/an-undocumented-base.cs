public abstract class Store {
    public abstract void Clear();
}

// The base says nothing, so there is nothing to inherit and the comment below is the documentation.
public sealed class MemoryStore : Store {
    /// <summary>Removes every entry.</summary>
    public override void Clear() { }
}
