// The block's two braces go in at fixed points, and a directive can leave one of them inside a
// branch the other is not in.
namespace Fixtures {
    static class ConditionalHelpers {
        public static bool IsBlank(this string value) => string.IsNullOrWhiteSpace(value);

#if DEBUG
        public static string Describe(this string value) => "debug:" + value;
#else
        public static string Describe(this string value) => value;
#endif
    }
}
