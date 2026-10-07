// The shape a refactor leaves: the interface was dropped from the base list and the one-line
// comment on what used to implement it stayed.
/// <summary>Something that can be reset.</summary>
public interface IResettable {
    /// <summary>Resets the state.</summary>
    void Reset();
}

/// <summary>A counter.</summary>
public sealed class Counter {
    /// <inheritdoc />
    public void Reset() { }
}
