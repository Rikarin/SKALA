public interface IStore {
    /// <summary>How many entries the store holds.</summary>
    int Count { get; }
}

// An implicit implementation carries no syntax saying what it implements, which is why the base
// member has to be searched for rather than read off the declaration.
public sealed class MemoryStore : IStore {
    /// <summary>How many entries the store holds.</summary>
    public int Count => 0;
}
