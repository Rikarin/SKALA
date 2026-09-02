using System.Collections.Immutable;

namespace Fixtures {
    sealed class Ids {
        public static int Count() {
            var ids = new ImmutableArray<int> { 7 };
            return ids.Length;
        }
    }
}
