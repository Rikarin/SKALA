// ⚠ #424: `{ { Count = 7 } }` adds one element, the value of the assignment `Count = 7` to the
// *local* `Count`; `{ Count = 7 }` is an object initializer that sets the *member* `Count`.
// Measured by #412's audit: "0 1 7" became "7 0 0".
using System.Collections;
using System.Collections.Generic;

public sealed class Bag : IEnumerable {
    public int Count;

    public List<int> Items = new();

    public void Add(int x) => Items.Add(x);

    public IEnumerator GetEnumerator() => Items.GetEnumerator();
}

public static class Probe {
    public static string Run() {
        var Count = 0;
        var b = new Bag { { Count = 7 } };
        return b.Count + " " + b.Items.Count + " " + Count;
    }
}
