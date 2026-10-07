using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

// ⚠ #412's audit: `Where(p).Last()` runs `p` on the first element, which throws; `Last(p)` on a
// `Collection<T>` starts from the end, matches there, and never reaches it.
public sealed class Item {
    public string? Name;

    public Item(string? name) => Name = name;
}

public static class Probe {
    public static string? Run() {
        IList<Item> items = new Collection<Item> { new(null), new("a"), new("bb") };
        return items.Where(item => item.Name!.Length > 0).Last().Name;
    }
}
