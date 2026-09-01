using System;
using System.Collections.Generic;
using System.Linq;

public sealed class Registry {
    // ⚠ `Func<T, bool>` does not convert to `Predicate<T>`: `values.Exists(ready)` is CS1503.
    public static bool AnyReady(List<int> values, Func<int, bool> ready) => values.Any(ready);
}
