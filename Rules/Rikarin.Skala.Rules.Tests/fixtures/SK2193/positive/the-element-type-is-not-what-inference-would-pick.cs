using System.Collections.Immutable;

namespace Fixtures {
    sealed class Values {
        public static ImmutableArray<object> All() => new ImmutableArray<object> { 1, 2 };
    }
}
