// ⚠ #430: `TrueForAll` loops `i < _size` and reads the live size, where `All` over a `List<T>` walks a
// span taken once. Removing the last element during the first call made `All` see three elements and
// answer `False`, and `TrueForAll` stop after two and answer `True`.
using System.Collections.Generic;
using System.Linq;

public static class Probe {
    public static string Run() {
        var list = new List<int> { 1, 2, 3 };
        var all = list.All(n => {
                if (n == 1) {
                    list.RemoveAt(2);
                }

                return n < 3;
            }
        );
        return all + " " + list.Count;
    }
}
