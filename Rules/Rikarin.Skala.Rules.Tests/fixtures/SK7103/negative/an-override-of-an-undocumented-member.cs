// Presence is the test, not documentation: an undocumented base member is still a member to
// inherit from, and reading its text would make the verdict depend on the package cache.
public abstract class Store {
    public abstract void Clear();
}

/// <summary>A memory store.</summary>
public sealed class MemoryStore : Store {
    /// <inheritdoc />
    public override void Clear() { }
}
