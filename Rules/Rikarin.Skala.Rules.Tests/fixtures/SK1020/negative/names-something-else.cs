using System;

public sealed class Guarded {
    public static void Use(object source, object other) {
        if (source is null) {
            throw new ArgumentNullException(nameof(other));
        }
    }
}
