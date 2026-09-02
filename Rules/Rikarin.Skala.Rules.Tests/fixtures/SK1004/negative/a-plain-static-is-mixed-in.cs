// The block would have to wrap a subset of the members, and a subset need not be contiguous, so
// there is no pair of insertion points that expresses it.
namespace Fixtures {
    static class MixedHelpers {
        public static bool IsBlank(this string value) => string.IsNullOrWhiteSpace(value);

        public static string Empty() => string.Empty;
    }
}
