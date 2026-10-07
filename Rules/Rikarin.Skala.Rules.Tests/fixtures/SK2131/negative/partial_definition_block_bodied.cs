// ⚠ #397: the same definition, implemented by a `get` with a block body rather than by an arrow on
// the property. Both are declined by the definition alone, before either implementation is read.
sealed partial class Loader {
    readonly int[] items = [1, 2, 3];

    public partial int Count { get; }

    public partial int Count {
        get {
            return items.Length;
        }
    }
}
