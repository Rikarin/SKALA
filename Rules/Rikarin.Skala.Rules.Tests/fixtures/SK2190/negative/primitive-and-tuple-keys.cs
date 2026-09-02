using System.Collections.Generic;

namespace Fixtures {
    sealed class Tables {
        readonly Dictionary<int, string> byNumber = new Dictionary<int, string>();

        readonly Dictionary<(int Row, int Column), string> byPair = new Dictionary<(int Row, int Column), string>();

        readonly HashSet<char> letters = new HashSet<char>();

        public int Size => byNumber.Count + byPair.Count + letters.Count;
    }
}
