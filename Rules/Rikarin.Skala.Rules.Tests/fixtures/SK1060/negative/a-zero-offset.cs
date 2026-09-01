using System.Collections.Generic;

// `^0` is `Count`, not the last element. Both throw, and only one of them looks like it should.
public sealed class OffByOne {
    public string Past(List<string> items) => items[items.Count - 0];
}
