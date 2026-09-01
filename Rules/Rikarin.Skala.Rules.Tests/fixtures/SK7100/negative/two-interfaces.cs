public interface IClearable {
    /// <summary>Removes every entry.</summary>
    void Clear();
}

public interface IResettable {
    /// <summary>Removes every entry.</summary>
    void Clear();
}

// One method implements both, so `<inheritdoc />` would have to pick one of them. Picking is not a
// mechanical edit and the rule says nothing.
public sealed class MemoryStore : IClearable, IResettable {
    /// <summary>Removes every entry.</summary>
    public void Clear() { }
}
