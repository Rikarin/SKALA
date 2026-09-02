using System.Collections.Generic;

// ⚠ The first legitimate look-alike. `Count` over `_items` is ordinary indirection: there is no
// `_count`, so the property is not following the naming convention here at all and there is nothing
// for a name to have been crossed with.
sealed class Bag {
    readonly List<string> items = new();

    public int Count => items.Count;

    public void Add(string value) => items.Add(value);
}
