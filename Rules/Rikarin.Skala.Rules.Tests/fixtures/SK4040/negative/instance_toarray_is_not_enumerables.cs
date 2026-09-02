using System.Collections.Generic;
using System.Linq;

public sealed class Buffer {
    readonly List<int> values = new();

    // ⚠ `List<T>.ToArray()` is the collection's own method, not `Enumerable.ToArray`, and the rule
    // binds the symbol rather than reading the identifier. The conversion here succeeds, so this
    // file witnesses the binding test and nothing else.
    public IReadOnlyList<int> Values => values.ToArray();
}
