using System;

public sealed class Guard {
    public static void Check(int count) {
        if (count < 0) {
            throw new ArgumentOutOfRangeException(nameof(count));
        }
    }
}
