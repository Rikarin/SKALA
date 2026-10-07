// An unresolvable cref is the compiler's CS1574 wherever documentation is generated; two
// diagnostics for one typo is noise, so any cref hands the question over.
/// <summary>A cache.</summary>
public sealed class Cache {
    /// <inheritdoc cref="Nowhere.AtAll" />
    public void Clear() { }
}
