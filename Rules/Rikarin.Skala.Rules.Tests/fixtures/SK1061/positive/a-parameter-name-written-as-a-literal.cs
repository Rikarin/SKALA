using System;

public sealed class Buffer {
    public void Take(int count) {
        if (count < 0) {
            throw new ArgumentOutOfRangeException("count", count, null);
        }
    }
}
