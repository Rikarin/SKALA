using System.Collections.Generic;

namespace Fixtures {
    struct Cell {
        public int Row;

        public static bool operator ==(Cell left, Cell right) => left.Row == right.Row;

        public static bool operator !=(Cell left, Cell right) => !(left == right);

        public override bool Equals(object? other) => other is Cell cell && cell.Row == Row;

        public override int GetHashCode() => Row;
    }

    sealed class Board {
        readonly Dictionary<Cell, string> labels = new Dictionary<Cell, string>();

        public int Size => labels.Count;
    }
}
