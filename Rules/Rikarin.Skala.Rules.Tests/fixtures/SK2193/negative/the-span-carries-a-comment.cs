using System.Collections.Immutable;

namespace Fixtures {
    sealed class Ids {
        public static ImmutableArray<int> All() =>
            new ImmutableArray<int> {
                1, // the first identifier the loader hands out
                2
            };
    }
}
