using System.Collections.Immutable;

namespace Fixtures {
    sealed class Ids {
        public static ImmutableArray<int> All() => ImmutableArray.Create<int>(1, 2, 3);

        public static ImmutableArray<int> Built() {
            var builder = ImmutableArray.CreateBuilder<int>();
            builder.Add(1);
            return builder.ToImmutable();
        }
    }
}
