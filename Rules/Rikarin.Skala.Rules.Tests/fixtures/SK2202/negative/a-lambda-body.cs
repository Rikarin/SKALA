using System.Collections.Generic;

// When the lambda's body runs is decided by whoever invokes it, not by the `?.`.
public sealed class Sum {
    int total;

    public void Add(List<int>? values) {
        values?.ForEach(value => total += value);
    }

    public int Total => total;
}
