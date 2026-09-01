using System;

public sealed class Buffer {
    public void Reserve(int count) {
        if (count < 0) {
            throw new ArgumentOutOfRangeException("Count", count, "must not be negative");
        }
    }
}
