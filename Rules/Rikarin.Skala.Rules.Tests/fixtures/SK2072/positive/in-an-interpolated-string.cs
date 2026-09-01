// The text part of a non-verbatim interpolated string takes escapes, so the fix applies.
// contains: U+200D
namespace Fixtures;

sealed class Report {
    public static string Render(int count) =>
        $"loaded {count}‍ items";
}
