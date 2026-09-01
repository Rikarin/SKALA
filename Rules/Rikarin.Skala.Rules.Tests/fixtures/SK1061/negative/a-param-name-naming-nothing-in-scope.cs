using System;

// SK2017's shape, not this one: the literal names no parameter, so there is nothing to write
// `nameof` of. Two rules reporting one line would be one of them being wrong.
public sealed class Guard {
    public void Take(int count) {
        if (count < 0) {
            throw new ArgumentOutOfRangeException("size", count, null);
        }
    }
}
