using System;
using System.Collections.Generic;

public sealed class Guarded {
    public static void Use(List<int> source) {
        if (source == null) {
            throw new ArgumentNullException(nameof(source));
        }
    }
}
