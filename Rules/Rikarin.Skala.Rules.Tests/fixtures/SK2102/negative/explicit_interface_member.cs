using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;

// ⚠ An explicit implementation is stored under a mangled name, so a plain lookup misses it. This
// type really does have a `Count` and reporting it would be a false positive on correct code.
[DebuggerDisplay("{Count}")]
sealed class Bag : IReadOnlyCollection<int> {
    int IReadOnlyCollection<int>.Count => 0;

    IEnumerator<int> IEnumerable<int>.GetEnumerator() => new List<int>().GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => new List<int>().GetEnumerator();
}
