using System;

public sealed class Guarded {
    public static void Use(object source) => ArgumentNullException.ThrowIfNull(source);
}
