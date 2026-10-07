using System;

// ⚠ `_ = () => 4;` and `_ = Size;` are both CS8183, natural delegate type or not: a discard does not
// take one. Neither initializer is evaluated for an effect, so neither is a candidate.
public sealed class Pool {
    static int Size() => 4;

    public void Warm() {
        Func<int> lambda = () => 4;
        Func<int> group = Size;
    }
}
