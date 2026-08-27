using System.Collections.Generic;

// `{ Capacity = 4 }` sets a property. It is not element syntax, and `List<T>` accepts it.
public sealed class Names {
    public List<string> All() {
        List<string> names = new List<string> { Capacity = 4 };
        return names;
    }
}
