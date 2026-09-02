// The value of the block is saying the receiver once instead of once per member. With one member
// there is nothing to group and the rewrite is churn.
namespace Fixtures {
    static class OneHelper {
        public static bool IsBlank(this string value) => string.IsNullOrWhiteSpace(value);
    }
}
