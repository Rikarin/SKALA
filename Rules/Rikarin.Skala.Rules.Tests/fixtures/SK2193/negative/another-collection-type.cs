using System.Collections.Generic;

namespace Fixtures {
    sealed class Ids {
        public static List<int> All() => new List<int> { 1, 2, 3 };

        public static HashSet<int> Set() => new HashSet<int> { 1, 2 };

        public static Dictionary<int, string> Map() => new Dictionary<int, string> { { 1, "a" } };
    }
}
