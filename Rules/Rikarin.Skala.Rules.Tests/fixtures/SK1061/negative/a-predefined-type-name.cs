// `typeof(int).Name` is "Int32" and `nameof(int)` does not compile at all.
public sealed class Primitives {
    public string TypeName() => typeof(int).Name;
}
