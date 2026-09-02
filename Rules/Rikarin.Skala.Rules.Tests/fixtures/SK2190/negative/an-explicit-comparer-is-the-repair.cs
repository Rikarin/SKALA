using System.Collections.Generic;

namespace Fixtures {
    struct Cell {
        public int Row;
        public int Column;
    }

    sealed class CellComparer : IEqualityComparer<Cell> {
        public bool Equals(Cell left, Cell right) => left.Row == right.Row && left.Column == right.Column;

        public int GetHashCode(Cell cell) => cell.Row * 397 ^ cell.Column;
    }

    sealed class Board {
        readonly Dictionary<Cell, string> labels = new Dictionary<Cell, string>(new CellComparer());

        public int Size => labels.Count;
    }
}
