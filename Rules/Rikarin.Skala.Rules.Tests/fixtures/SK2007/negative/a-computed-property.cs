using System.Collections.Generic;
using System.Linq;

public sealed class Registry {
    readonly List<int> _items = new List<int>();

    // ⚠ Two reads of this property are two lists. The `foreach` enumerates one and the `Remove`
    // modifies another, so nothing throws — which is why the rule reports locals, parameters and
    // fields, and never a property.
    public List<int> Items => _items.ToList();

    public void Prune() {
        foreach (var item in Items) {
            if (item < 0) {
                Items.Remove(item);
            }
        }
    }
}
