using System.Collections.Immutable;

namespace Fixtures {
    sealed class Ids {
        public static ImmutableArray<int> All() => new ImmutableArray<int> { 1, 2, 3 };
    }
}
