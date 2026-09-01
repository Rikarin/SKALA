/// <summary>A set of components stored together.</summary>
public abstract class Store {
    /// <summary>Removes every entry.</summary>
    protected abstract void Clear();
}

/// <summary>A store held in memory.</summary>
public sealed class MemoryStore : Store {
    /// <inheritdoc />
    protected override void Clear() { }
}
