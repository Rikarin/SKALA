using System.Collections.Generic;

// A generic name is a name: `_ = new List<int>(4);`.
public sealed class Pool {
    public void Warm() {
        List<int> buffer = new(4);
    }
}
