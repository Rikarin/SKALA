using System;
using System.Collections.Generic;
using System.Linq;

public sealed class Registry {
    // ⚠ A set carries an IEqualityComparer<T>, so `Contains` and `==` genuinely disagree: this one
    // is case-insensitive and the lambda is ordinal. HashSet has no Find/Exists/TrueForAll either.
    public static bool Knows(HashSet<string> names, string wanted) => names.Any(name => name == wanted);

    public static HashSet<string> Insensitive() => new(StringComparer.OrdinalIgnoreCase);
}
