// The struct half-declared equality: `Equals(Cell)` with no `IEquatable<Cell>`, which
// `EqualityComparer<Cell>.Default` ignores. That is SK2044's finding, on the declaration where
// the interface can be added — not a second finding here on every collection that uses it.
using System.Collections.Generic;

namespace Fixtures {
    struct Cell {
        public int Row;

        public bool Equals(Cell other) => other.Row == Row;
    }

    sealed class Board {
        readonly Dictionary<Cell, string> labels = new Dictionary<Cell, string>();

        public int Size => labels.Count;
    }
}
