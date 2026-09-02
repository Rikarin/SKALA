// SortedDictionary and SortedSet reach for Comparer<T>.Default, which throws without IComparable
// rather than quietly hashing by reflection. A different failure with a different repair.
using System.Collections.Generic;

namespace Fixtures {
    struct Cell {
        public int Row;
    }

    sealed class Board {
        readonly SortedDictionary<Cell, string> labels = new SortedDictionary<Cell, string>();

        readonly SortedSet<Cell> order = new SortedSet<Cell>();

        public int Size => labels.Count + order.Count;
    }
}
