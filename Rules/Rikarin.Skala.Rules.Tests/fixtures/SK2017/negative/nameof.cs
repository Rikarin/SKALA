using System;

public sealed class Guards {
    public void Check(string value, int count) {
        if (value is null) {
            throw new ArgumentNullException(nameof(value));
        }

        if (count < 0) {
            throw new ArgumentOutOfRangeException(nameof(count), count, "must not be negative");
        }

        if (value.Length == 0) {
            throw new ArgumentException("must not be empty", nameof(value));
        }
    }
}
