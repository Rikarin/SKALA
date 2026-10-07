// The implementation part's comment is the one both halves carry, and it resolves through the
// interface member the definition implements.
/// <summary>Something that loads.</summary>
public interface ILoader {
    /// <summary>Loads.</summary>
    void Load();
}

/// <summary>A loader.</summary>
public sealed partial class Loader : ILoader {
    /// <summary>Loads everything.</summary>
    public partial void Load();

    /// <inheritdoc />
    public partial void Load() { }
}
