// The capacity overload is not the comparer overload, and the reflection fallback is still what runs.
using System.Collections.Generic;

namespace Fixtures {
    struct Handle {
        public int Value;
    }

    sealed class Pool {
        readonly Dictionary<Handle, object> entries = new Dictionary<Handle, object>(16);

        public int Size => entries.Count;
    }
}
