using System.Collections;
using System.Collections.Generic;

// A user collection with an `Add` accepts a collection initializer, but the collection-expression
// lowering for it is not the one the constructor wrote and the rule does not guess.
public sealed class Bag : IEnumerable<string> {
    readonly List<string> _items = [];

    public void Add(string item) => _items.Add(item);

    public IEnumerator<string> GetEnumerator() => _items.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

public sealed class Names {
    public Bag All() {
        Bag names = new Bag { "a", "b" };
        return names;
    }
}
