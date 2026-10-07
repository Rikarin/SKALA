// ⚠ `this` passed to a `this ref` extension is `ref this` without the keyword, which `readonly struct`
// rejects (#412).
static class StampExtensions {
    public static void Reset(this ref Stamp stamp) => stamp = default;
}

struct Stamp {
    readonly long ticks;

    public Stamp(long value) => ticks = value;

    public long Ticks => ticks;

    public void Clear() => this.Reset();
}
