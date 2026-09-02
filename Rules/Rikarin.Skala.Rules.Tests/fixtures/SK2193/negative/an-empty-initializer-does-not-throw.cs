// `Add` is called zero times, so this does not throw. It yields a `default` array whose
// `IsDefault` is true, which fails later and somewhere else — a different, weaker defect, and
// reporting it under a message that says the code throws would be saying something untrue.
using System.Collections.Immutable;

namespace Fixtures {
    sealed class Ids {
        public static ImmutableArray<int> None() => new ImmutableArray<int>();

        public static ImmutableArray<int> AlsoNone() => new ImmutableArray<int> { };
    }
}
