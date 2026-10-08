// fixture-option: LangVersion = 14
using System.Collections.Generic;
using System.Linq;

public sealed class Inventory {
    readonly List<string> items = new();

    public string[] All => items.ToArray();

    public List<string> Copy() {
        return items.ToList();
    }

    public string[] Sorted() => items.OrderBy(static item => item).ToArray();
}
