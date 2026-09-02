using System.Collections.Generic;
using System.Linq;

public sealed class Feed {
    readonly List<int> entries = new();

    // ⚠ This file exists because sabotaging the plain-name-path guard turned nothing red: every
    // other pipeline fixture was being declined by the *conversion* test instead, so the guard was
    // unwitnessed. `IEnumerable<int>` converts to itself, so only the path test declines this one.
    // Removing the `ToList` here would make the property lazy and hand each reader a fresh query —
    // the multiple-enumeration defect, offered as a fix.
    public IEnumerable<int> Positive => entries.Where(static entry => entry > 0).ToList();
}
