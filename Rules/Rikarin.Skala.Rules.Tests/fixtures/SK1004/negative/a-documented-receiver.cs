// The receiver ceases to exist, so the `<param>` would document nothing and the build would gain a
// CS1572 on a public API — a fix that trades a suggestion for a warning.
namespace Fixtures {
    static class DocumentedHelpers {
        /// <summary>Whether the text is blank.</summary>
        /// <param name="value">The text to test.</param>
        /// <returns>Whether it is blank.</returns>
        public static bool IsBlank(this string value) => string.IsNullOrWhiteSpace(value);

        /// <summary>The text, repeated.</summary>
        /// <param name="value">The text to repeat.</param>
        /// <param name="times">How many times.</param>
        /// <returns>The repeated text.</returns>
        public static string Repeat(this string value, int times) => new string('x', times) + value;
    }
}
