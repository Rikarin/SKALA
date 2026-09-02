using System;
using System.Collections.Generic;

namespace Fixtures {
    readonly struct Cell : IEquatable<Cell> {
        public int Row { get; init; }

        public int Column { get; init; }

        public bool Equals(Cell other) => other.Row == Row && other.Column == Column;

        public override bool Equals(object? other) => other is Cell cell && Equals(cell);

        public override int GetHashCode() => Row * 397 ^ Column;
    }

    sealed class Board {
        readonly Dictionary<Cell, string> labels = new Dictionary<Cell, string>();

        public int Size => labels.Count;
    }
}
