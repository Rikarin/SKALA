// Measured: a partial method's two halves share ONE comment, and it is the implementation's.
// So this `<inheritdoc/>` does not defer to the definition's summary -- it replaces it, and the
// member renders blank. Deleting it is what brings the definition's prose back.
/// <summary>A loader.</summary>
public sealed partial class Loader {
    /// <summary>Loads everything.</summary>
    public partial void Load();

    /// <inheritdoc />
    public partial void Load() { }
}
