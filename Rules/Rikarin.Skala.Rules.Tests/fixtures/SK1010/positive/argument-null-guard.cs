using System;

public sealed class Guarded {
    public static void Use(object source) {
        if (source == null) {
            throw new ArgumentNullException(nameof(source));
        }
    }
}
