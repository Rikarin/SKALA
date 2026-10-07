// The implementation's comment is the one both halves carry, so the definition's
// `<inheritdoc/>` is never read.
/// <summary>A loader.</summary>
public sealed partial class Loader {
    /// <inheritdoc />
    public partial void Load();

    /// <summary>Loads everything.</summary>
    public partial void Load() { }
}
