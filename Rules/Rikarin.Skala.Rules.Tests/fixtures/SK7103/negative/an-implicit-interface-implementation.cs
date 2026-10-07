// The 441 syntactic sites #391 counted were essentially all this shape.
/// <summary>Something that can be reset.</summary>
public interface IResettable {
    /// <summary>Resets the state.</summary>
    void Reset();
}

/// <summary>A counter.</summary>
public sealed class Counter : IResettable {
    /// <inheritdoc />
    public void Reset() { }
}
