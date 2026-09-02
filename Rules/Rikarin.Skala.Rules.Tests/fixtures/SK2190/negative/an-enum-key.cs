using System.Collections.Generic;

namespace Fixtures {
    enum Level {
        Low,
        High
    }

    sealed class Board {
        readonly Dictionary<Level, string> labels = new Dictionary<Level, string>();

        readonly Dictionary<Level?, string> nullable = new Dictionary<Level?, string>();

        public int Size => labels.Count + nullable.Count;
    }
}
