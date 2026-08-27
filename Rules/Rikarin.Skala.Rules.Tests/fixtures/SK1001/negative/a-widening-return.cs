// The method returns `object[]` and the expression creates a `string[]`. `return […];` would return
// an array of `object`, which is a different object at run time.
public sealed class Names {
    public object[] All() {
        return new string[] { "a", "b" };
    }
}
