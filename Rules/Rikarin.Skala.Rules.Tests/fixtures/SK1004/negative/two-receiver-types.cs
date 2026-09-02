// Two receivers need two blocks, and nothing here says what order to put them in.
namespace Fixtures {
    static class TwoReceivers {
        public static bool IsBlank(this string value) => string.IsNullOrWhiteSpace(value);

        public static bool IsEmpty(this int[] items) => items.Length == 0;
    }
}
