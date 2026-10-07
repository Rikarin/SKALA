// ⚠ `readonly struct Holder<T>` compiles here — the compiler writes a defensive copy of `counter` —
// but when `T` is a struct, `Reset` writes the capture today and would write a discarded copy after
// the fix. Compiling is not the same as meaning the same thing, so the rule declines.
interface ICounter {
    int Count { get; set; }
}

struct Holder<T>(T counter)
    where T : ICounter {
    public void Reset() => counter.Count = 0;

    public int Count => counter.Count;
}
