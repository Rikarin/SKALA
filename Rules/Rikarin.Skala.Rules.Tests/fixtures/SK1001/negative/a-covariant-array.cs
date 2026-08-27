// `object[] a = new string[] { … }` is an array of `string` at run time: reading gives strings and
// writing an `int` throws. `object[] a = […]` is an array of `object` and does neither.
public sealed class Names {
    public object[] All() {
        object[] names = new string[] { "a", "b" };
        return names;
    }
}
