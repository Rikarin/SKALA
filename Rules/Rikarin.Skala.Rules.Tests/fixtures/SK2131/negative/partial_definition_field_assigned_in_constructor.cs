// ⚠ #397, measured: a partial property implemented with the C# 14 `field` keyword and no setter can
// still be assigned from a constructor of its own type, exactly as a get-only auto-property can — the
// assignment writes the compiler's backing field. Here it is, so `Count` is never `default`.
sealed partial class Loader {
    public Loader(int count) {
        Count = count;
    }

    public partial int Count { get; }

    public partial int Count {
        get => field;
    }
}
