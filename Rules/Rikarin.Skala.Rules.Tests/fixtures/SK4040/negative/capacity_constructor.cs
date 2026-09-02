using System.Collections.Generic;

public sealed class Pool {
    // ⚠ Spelled identically to `new List<int>(items)` and copying nothing. The discriminator is the
    // constructor parameter's type, not the argument count.
    public List<int> Fresh => new List<int>(16);
}
