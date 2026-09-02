using System;
using System.Collections.Generic;

namespace Fixtures.SK2242;

public sealed class ThrowHelperOfAnotherType : IDisposable {
    bool disposed;

    public void Dispose() => disposed = true;

    // ⚠ `ObjectDisposedException.ThrowIf` is a static `ThrowIf…` helper and is *not* an argument
    // check: its type does not derive from `ArgumentException`. Matching on the name alone would
    // report this.
    public IEnumerable<int> Values() {
        ObjectDisposedException.ThrowIf(disposed, this);

        yield return 1;
    }
}
