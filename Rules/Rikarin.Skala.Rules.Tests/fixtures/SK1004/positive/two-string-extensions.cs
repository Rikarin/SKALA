namespace Fixtures {
    static class StringExtensions {
        public static bool IsBlank(this string value) => string.IsNullOrWhiteSpace(value);

        public static string Repeat(this string value, int times) => new string('x', times) + value;
    }
}
