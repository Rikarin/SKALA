using Count = System.Nullable<int>;

// A `using` alias is a declaration of a name. Rewriting the aliased type here is a change to a
// declaration the rest of the file reads through, not a local simplification.
public sealed class Aliased {
    public Count Total { get; init; }
}
