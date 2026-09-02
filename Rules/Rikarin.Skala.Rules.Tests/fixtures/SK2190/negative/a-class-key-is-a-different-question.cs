using System.Collections.Generic;

namespace Fixtures {
    sealed class Cell {
        public int Row { get; init; }
    }

    sealed class Board {
        readonly Dictionary<Cell, string> labels = new Dictionary<Cell, string>();

        public int Size => labels.Count;
    }
}
