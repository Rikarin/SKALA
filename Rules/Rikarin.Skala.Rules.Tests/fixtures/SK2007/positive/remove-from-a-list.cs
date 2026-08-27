using System.Collections.Generic;
using System.Linq;

public sealed class Pruner {
    public void Prune(List<int> items) {
        foreach (var item in items) {
            if (item < 0) {
                items.Remove(item);
            }
        }
    }
}
