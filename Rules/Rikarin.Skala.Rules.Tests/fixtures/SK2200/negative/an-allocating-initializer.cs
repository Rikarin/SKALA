// The fix deletes the initializer, and a constructor that registers something is exactly what
// SK2013 and CA1806 argue about. This rule will not delete a `new` on its own authority.
public sealed class Batch {
    readonly System.Collections.Generic.List<int> items = new();

    public Batch(System.Collections.Generic.List<int> given) {
        items = given;
    }

    public int Count => items.Count;
}
