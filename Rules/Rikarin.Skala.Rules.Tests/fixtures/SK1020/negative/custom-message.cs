using System;

public sealed class Guarded {
    public static void Use(object source) {
        if (source is null) {
            throw new ArgumentNullException(nameof(source), "the caller owns this");
        }
    }
}
