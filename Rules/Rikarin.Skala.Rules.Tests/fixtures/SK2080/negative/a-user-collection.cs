using System.Collections;
using System.Collections.Generic;

public sealed class Bag : IEnumerable<string> {
    readonly List<string> items = [];

    public void Add(string item) => items.Add(item);

    public IEnumerator<string> GetEnumerator() => items.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

public sealed class Names {
    // `Add` means whatever its author wrote. A name matched by lookup is not a proof of anything.
    public static readonly Bag Known = new() { "alpha", "alpha" };
}
