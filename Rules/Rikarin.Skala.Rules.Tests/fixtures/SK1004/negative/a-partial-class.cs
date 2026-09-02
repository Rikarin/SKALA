// The other part is not in front of the analyzer, so "every member is an extension method" is a
// question this file cannot answer.
namespace Fixtures {
    static partial class PartialHelpers {
        public static bool IsBlank(this string value) => string.IsNullOrWhiteSpace(value);

        public static string Repeat(this string value, int times) => new string('x', times) + value;
    }

    static partial class PartialHelpers {
        public static int Version => 1;
    }
}
