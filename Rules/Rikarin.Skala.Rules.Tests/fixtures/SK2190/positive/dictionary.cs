using System.Collections.Generic;

namespace Fixtures {
    struct Cell {
        public int Row;
        public int Column;
    }

    sealed class Board {
        readonly Dictionary<Cell, string> labels = new Dictionary<Cell, string>();

        public string? Label(Cell cell) => labels.TryGetValue(cell, out var label) ? label : null;
    }
}
