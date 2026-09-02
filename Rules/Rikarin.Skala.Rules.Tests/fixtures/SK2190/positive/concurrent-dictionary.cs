using System.Collections.Concurrent;

namespace Fixtures {
    struct Slot {
        public int Index;
    }

    sealed class Cache {
        readonly ConcurrentDictionary<Slot, int> counts = new ConcurrentDictionary<Slot, int>();

        public int Count(Slot slot) => counts.GetOrAdd(slot, 0);
    }
}
