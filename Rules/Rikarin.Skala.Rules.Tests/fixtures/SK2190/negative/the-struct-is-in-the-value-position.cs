using System.Collections.Generic;

namespace Fixtures {
    struct Cell {
        public int Row;
    }

    sealed class Board {
        readonly Dictionary<int, Cell> byIndex = new Dictionary<int, Cell>();

        public int Size => byIndex.Count;
    }
}
