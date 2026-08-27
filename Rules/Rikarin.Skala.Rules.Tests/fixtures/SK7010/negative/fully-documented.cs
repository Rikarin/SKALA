// rules.json's SK7010 `good` example.

/// <summary>A set of components stored together.</summary>
public sealed class DocumentedArchetype {
    /// <summary>Creates an archetype.</summary>
    /// <param name="count">How many entities it holds.</param>
    public DocumentedArchetype(int count) => Count = count;

    /// <summary>How many entities the archetype holds.</summary>
    public int Count { get; }
}
