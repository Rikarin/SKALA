using System.Collections.Generic;

namespace Fixtures {
    struct Cell {
        public int Row;

        public override bool Equals(object? other) => other is Cell cell && cell.Row == Row;

        public override int GetHashCode() => Row;
    }

    sealed class Board {
        readonly HashSet<Cell> seen = new HashSet<Cell>();

        public int Size => seen.Count;
    }
}
