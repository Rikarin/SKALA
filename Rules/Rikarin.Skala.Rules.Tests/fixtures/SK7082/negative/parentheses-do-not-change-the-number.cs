// The same ladder with every rung parenthesised. Some house styles write these and some do not, so
// they must not change the measurement — the walk unwraps them before deciding a rung is a rung.
namespace Fixtures;

class Sizes {
    public static string Describe(int n) => n < 10 ? "tiny" : (n < 100 ? "small" : (n < 1000 ? "medium" : "large"));
}
