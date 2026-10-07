// ⚠ #425: an empty collection expression of an array type is `Array.Empty<T>()`, one instance shared by
// the whole process, where each `new T[] { }` is a fresh one. Measured for #412's audit:
// `ReferenceEquals` `False` before the fix and `True` after it. A fresh empty array taken as a lock or a
// sentinel relies on exactly that.
public static class Probe {
    static object[] Gate() {
        object[] gate = new object[] { };
        return gate;
    }

    public static bool Run() => ReferenceEquals(Gate(), Gate());
}
