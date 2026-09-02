// `new() { … }` writes no type syntax, so there is no qualifier to build `Create` from and no
// way to know whether the file imported the namespace. Declined rather than guessed at.
using System.Collections.Immutable;

namespace Fixtures {
    sealed class Ids {
        public static ImmutableArray<int> All() {
            ImmutableArray<int> ids = new() { 1, 2, 3 };
            return ids;
        }
    }
}
