using System;

// `_ = null;` and `_ = (null, 1);` are CS8183. `_ = stackalloc int[4];` compiles, as a `Span<int>`,
// but has no effect worth keeping. None of the three is a candidate.
public sealed class Pool {
    public void Warm() {
        string? name = null;
        Span<int> scratch = stackalloc int[4];
        (string? Key, int Value) pair = (null, 1);
    }
}
