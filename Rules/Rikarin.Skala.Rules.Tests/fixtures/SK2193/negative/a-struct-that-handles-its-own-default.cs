// ⚠ This fixture exists because two sabotage rounds found nothing without it. Every other
// negative here is a class, has no initializer, or writes a non-generic type name — so the
// struct check and the one-type-argument check were catching them all, and "it must be
// ImmutableArray<T>" was doing no work anything could observe. A one-argument generic struct
// with its own Add that copes with being default is the shape that tells them apart.
using System.Collections;
using System.Collections.Generic;

namespace Fixtures {
    struct Bag<T> : IEnumerable<T> {
        List<T>? items;

        public void Add(T value) => (items ??= new List<T>()).Add(value);

        public IEnumerator<T> GetEnumerator() => (items ?? new List<T>()).GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    sealed class Bags {
        public static int Count() {
            var bag = new Bag<int> { 1, 2, 3 };
            var total = 0;
            foreach (var value in bag) {
                total += value;
            }

            return total;
        }
    }
}
