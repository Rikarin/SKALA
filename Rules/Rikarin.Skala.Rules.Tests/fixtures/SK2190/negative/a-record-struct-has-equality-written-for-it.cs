using System.Collections.Generic;

namespace Fixtures {
    readonly record struct Cell(int Row, int Column);

    sealed class Board {
        readonly Dictionary<Cell, string> labels = new Dictionary<Cell, string>();

        public int Size => labels.Count;
    }
}
